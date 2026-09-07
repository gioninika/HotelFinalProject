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
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(RegisterDto request)
    {
        var result = await _authService.RegisterAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponseDto>.Ok(result, "Registration completed successfully."));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(LoginDto request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Login completed successfully."));
    }
}
