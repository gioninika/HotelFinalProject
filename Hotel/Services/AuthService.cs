using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hotel.Data;
using Hotel.Dtos;
using Hotel.Exceptions;
using Hotel.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Services;

public class AuthService : IAuthService
{
    private const string ConfirmEmailPurpose = "ConfirmEmail";
    private const string ResetPasswordPurpose = "ResetPassword";
    private const int MaxCodeAttempts = 5;

    private readonly HmsDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IEmailSender _emailSender;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();
    private readonly byte[] _codeKey;

    public AuthService(
        HmsDbContext context,
        IJwtService jwtService,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _context = context;
        _jwtService = jwtService;
        _emailSender = emailSender;

        var key = configuration["Auth:CodeHmacKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException(
                "Auth:CodeHmacKey must be configured and at least 32 characters long.");

        _codeKey = Encoding.UTF8.GetBytes(key);
    }

    public Task<RegistrationResponseDto> RegisterAsync(RegisterDto request)
    {
        // Public registration is guest-only. Manager accounts use the protected hotel route.
        if (!string.Equals(request.Role, Roles.Guest, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only guests can register through this endpoint.");

        return RegisterUserAsync(request, null, Roles.Guest);
    }

    public Task<RegistrationResponseDto> RegisterManagerAsync(
        int hotelId,
        RegisterDto request)
    {
        request.Role = Roles.Manager;
        request.HotelId = hotelId;
        return RegisterUserAsync(request, hotelId, Roles.Manager);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _context.AppUsers
            .FirstOrDefaultAsync(x => x.Email == email)
            ?? throw new UnauthorizedException("Invalid email or password.");

        var result = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid email or password.");

        // Keep legacy guest accounts (which predate email collection) usable.
        // Every newly registered account has an email and must confirm it.
        if (user.Email is not null && !user.EmailConfirmed)
            throw new UnauthorizedException("Confirm your email before logging in.");

        return _jwtService.CreateToken(user);
    }

    public async Task<AuthResponseDto> VerifyEmailAsync(VerifyEmailDto request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _context.AppUsers
            .FirstOrDefaultAsync(x => x.Email == email)
            ?? throw new UnauthorizedException("Invalid or expired code.");

        await ValidateCodeAsync(user, ConfirmEmailPurpose, request.Code);

        user.EmailConfirmed = true;
        ClearCode(user);
        await _context.SaveChangesAsync();

        return _jwtService.CreateToken(user);
    }

    public async Task ResendVerificationAsync(EmailOnlyDto request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _context.AppUsers
            .FirstOrDefaultAsync(x => x.Email == email);

        // Keep the response the same whether the account exists or not.
        if (user is null || user.EmailConfirmed)
            return;

        await SendCodeAsync(user, ConfirmEmailPurpose);
    }

    public async Task RequestPasswordResetAsync(EmailOnlyDto request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _context.AppUsers
            .FirstOrDefaultAsync(x => x.Email == email && x.EmailConfirmed);

        // Keep the response the same whether the account exists or not.
        if (user is null)
            return;

        await SendCodeAsync(user, ResetPasswordPurpose);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _context.AppUsers
            .FirstOrDefaultAsync(x => x.Email == email && x.EmailConfirmed)
            ?? throw new UnauthorizedException("Invalid or expired code.");

        await ValidateCodeAsync(user, ResetPasswordPurpose, request.Code);

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        ClearCode(user);
        await _context.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordDto request)
    {
        var user = await _context.AppUsers.FindAsync(userId)
            ?? throw new UnauthorizedException("Account was not found.");

        var result = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.CurrentPassword);

        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        ClearCode(user);
        await _context.SaveChangesAsync();
    }

    private async Task<RegistrationResponseDto> RegisterUserAsync(
        RegisterDto request,
        int? hotelId,
        string role)
    {
        var email = NormalizeEmail(request.Email);

        if (await _context.AppUsers.AnyAsync(x => x.Email == email))
            throw new BusinessRuleException("This email is already registered.");

        AppUser user;

        if (role == Roles.Manager)
        {
            if (!hotelId.HasValue ||
                !await _context.Hotels.AnyAsync(x => x.Id == hotelId.Value))
            {
                throw new KeyNotFoundException("Hotel was not found.");
            }

            if (await _context.Managers.AnyAsync(
                x => x.Email == email || x.PersonalNumber == request.PersonalNumber))
            {
                throw new BusinessRuleException(
                    "Manager email or personal number is already registered.");
            }

            await CheckUserNameAsync(email);

            var manager = new Manager
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                PersonalNumber = request.PersonalNumber,
                Email = email,
                PhoneNumber = request.PhoneNumber,
                HotelId = hotelId.Value
            };

            _context.Managers.Add(manager);
            await _context.SaveChangesAsync();

            user = new AppUser
            {
                UserName = email,
                Email = email,
                Role = Roles.Manager,
                ManagerId = manager.Id
            };
        }
        else
        {
            if (await _context.Guests.AnyAsync(
                x => x.PersonalNumber == request.PersonalNumber ||
                     x.PhoneNumber == request.PhoneNumber))
            {
                throw new BusinessRuleException(
                    "Guest personal number or phone number is already registered.");
            }

            var userName = email;
            await CheckUserNameAsync(userName);

            var guest = new Guest
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                PersonalNumber = request.PersonalNumber,
                PhoneNumber = request.PhoneNumber
            };

            _context.Guests.Add(guest);
            await _context.SaveChangesAsync();

            user = new AppUser
            {
                UserName = userName,
                Email = email,
                Role = Roles.Guest,
                GuestId = guest.Id
            };
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        user.EmailConfirmed = false;

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();

        await SendCodeAsync(user, ConfirmEmailPurpose);

        return new RegistrationResponseDto { Email = email };
    }

    private async Task SendCodeAsync(AppUser user, string purpose)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return;

        var now = DateTime.UtcNow;

        // At most one code email per minute per account.
        if (user.EmailCodePurpose == purpose &&
            user.EmailCodeSentAtUtc is DateTime lastSent &&
            lastSent > now.AddMinutes(-1))
        {
            return;
        }

        var previousCodeHash = user.EmailCodeHash;
        var previousPurpose = user.EmailCodePurpose;
        var previousExpiresAt = user.EmailCodeExpiresAtUtc;
        var previousSentAt = user.EmailCodeSentAtUtc;
        var previousFailedAttempts = user.EmailCodeFailedAttempts;

        var code = RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);

        user.EmailCodeHash = HashCode(code);
        user.EmailCodePurpose = purpose;
        user.EmailCodeExpiresAtUtc = now.AddMinutes(10);
        user.EmailCodeSentAtUtc = now;
        user.EmailCodeFailedAttempts = 0;

        await _context.SaveChangesAsync();

        var isConfirmation = purpose == ConfirmEmailPurpose;
        var subject = isConfirmation ? "Confirm your email" : "Reset your password";
        var instruction = isConfirmation
            ? "Use this code to confirm your email."
            : "Use this code to reset your password.";

        try
        {
            await _emailSender.SendAsync(
                user.Email,
                subject,
                $"{instruction} Your code is {code}. It expires in 10 minutes.");
        }
        catch
        {
            // SMTP failures should not leave a saved cooldown behind. Restore the
            // previous code state so the user can retry immediately.
            user.EmailCodeHash = previousCodeHash;
            user.EmailCodePurpose = previousPurpose;
            user.EmailCodeExpiresAtUtc = previousExpiresAt;
            user.EmailCodeSentAtUtc = previousSentAt;
            user.EmailCodeFailedAttempts = previousFailedAttempts;
            await _context.SaveChangesAsync();
            throw;
        }
    }

    private async Task ValidateCodeAsync(AppUser user, string purpose, string code)
    {
        var now = DateTime.UtcNow;

        if (user.EmailCodeHash is null ||
            user.EmailCodePurpose != purpose ||
            user.EmailCodeExpiresAtUtc is null ||
            user.EmailCodeExpiresAtUtc.Value <= now ||
            user.EmailCodeFailedAttempts >= MaxCodeAttempts)
        {
            throw new UnauthorizedException("Invalid or expired code.");
        }

        var expected = Convert.FromBase64String(user.EmailCodeHash);
        var supplied = Convert.FromBase64String(HashCode(code));

        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            user.EmailCodeFailedAttempts++;

            if (user.EmailCodeFailedAttempts >= MaxCodeAttempts)
                user.EmailCodeExpiresAtUtc = now;

            await _context.SaveChangesAsync();
            throw new UnauthorizedException("Invalid or expired code.");
        }
    }

    private string HashCode(string code)
    {
        var hash = HMACSHA256.HashData(_codeKey, Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(hash);
    }

    private static void ClearCode(AppUser user)
    {
        user.EmailCodeHash = null;
        user.EmailCodeExpiresAtUtc = null;
        user.EmailCodeFailedAttempts = 0;
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private async Task CheckUserNameAsync(string userName)
    {
        if (await _context.AppUsers.AnyAsync(x => x.UserName == userName))
            throw new BusinessRuleException("This login name is already used.");
    }
}
