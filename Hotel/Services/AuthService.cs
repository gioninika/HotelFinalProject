using Hotel.Data;
using Hotel.Dtos;
using Hotel.Exceptions;
using Hotel.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Services;

public class AuthService : IAuthService
{
    private readonly HmsDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public AuthService(HmsDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public Task<AuthResponseDto> RegisterAsync(RegisterDto request)
    {
        if (string.Equals(request.Role, Roles.Manager, StringComparison.OrdinalIgnoreCase) && !request.HotelId.HasValue)
            throw new ArgumentException("HotelId is required when registering a manager.");
        return RegisterUserAsync(request, request.HotelId);
    }

    public Task<AuthResponseDto> RegisterManagerAsync(int hotelId, RegisterDto request)
    {
        request.Role = Roles.Manager;
        request.HotelId = hotelId;
        return RegisterUserAsync(request, hotelId);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto request)
    {
        var user = await _context.AppUsers.FirstOrDefaultAsync(x => x.UserName == request.UserName)
            ?? throw new UnauthorizedException("Invalid username or password.");

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid username or password.");

        return _jwtService.CreateToken(user);
    }

    private async Task<AuthResponseDto> RegisterUserAsync(RegisterDto request, int? hotelId)
    {
        var role = request.Role?.Trim() ?? string.Empty;
        if (role.Equals(Roles.Manager, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Email is required for manager registration.");
            if (!hotelId.HasValue || !await _context.Hotels.AnyAsync(x => x.Id == hotelId.Value))
                throw new KeyNotFoundException("Hotel was not found.");
            if (await _context.Managers.AnyAsync(x => x.Email == request.Email || x.PersonalNumber == request.PersonalNumber))
                throw new BusinessRuleException("Manager email or personal number is already registered.");

            var userName = request.Email.Trim().ToLowerInvariant();
            await CheckUserNameAsync(userName);
            var manager = new Manager
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                PersonalNumber = request.PersonalNumber,
                Email = userName,
                PhoneNumber = request.PhoneNumber,
                HotelId = hotelId.Value
            };
            _context.Managers.Add(manager);
            await _context.SaveChangesAsync();

            var user = new AppUser
            {
                UserName = userName,
                Role = Roles.Manager,
                ManagerId = manager.Id
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            return _jwtService.CreateToken(user, hotelId);
        }

        if (role.Equals(Roles.Guest, StringComparison.OrdinalIgnoreCase))
        {
            if (await _context.Guests.AnyAsync(x => x.PersonalNumber == request.PersonalNumber || x.PhoneNumber == request.PhoneNumber))
                throw new BusinessRuleException("Guest personal number or phone number is already registered.");

            var userName = request.PersonalNumber.Trim();
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

            var user = new AppUser
            {
                UserName = userName,
                Role = Roles.Guest,
                GuestId = guest.Id
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            return _jwtService.CreateToken(user);
        }

        throw new ArgumentException("Role must be Manager or Guest.");
    }

    private async Task CheckUserNameAsync(string userName)
    {
        if (await _context.AppUsers.AnyAsync(x => x.UserName == userName))
            throw new BusinessRuleException("This login name is already used.");
    }
}
