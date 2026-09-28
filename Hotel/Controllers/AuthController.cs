using System.Security.Claims;
using Hotel.Dtos;
using Hotel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hotel.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<RegistrationResponseDto>>> Register(
        RegisterDto request)
    {
        var result = await _authService.RegisterAsync(request);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<RegistrationResponseDto>.Ok(
                result,
                "Check your email for a confirmation code."));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> VerifyEmail(
        VerifyEmailDto request)
    {
        var result = await _authService.VerifyEmailAsync(request);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Email confirmed."));
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ResendVerification(
        EmailOnlyDto request)
    {
        await _authService.ResendVerificationAsync(request);
        return Ok(ApiResponse<object>.Ok(
            null,
            "Check your inbox and spam folder. If you requested a code within the last 60 seconds, the resend was rate-limited; wait 60 seconds and try again."));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(
        EmailOnlyDto request)
    {
        await _authService.RequestPasswordResetAsync(request);
        return Ok(ApiResponse<object>.Ok(
            null,
            "If that account can reset its password, a code will be sent."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(
        ResetPasswordDto request)
    {
        await _authService.ResetPasswordAsync(request);
        return Ok(ApiResponse<object>.Ok(null, "Password reset successfully."));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        ChangePasswordDto request)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(
                ApiResponse<object>.Fail("User identity was not found."));
        }

        await _authService.ChangePasswordAsync(userId, request);
        return Ok(ApiResponse<object>.Ok(null, "Password changed successfully."));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(LoginDto request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Login completed successfully."));
    }
}
