using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hotel.Data;
using Hotel.Dtos;
using Hotel.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Hotel.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly HmsDbContext _context;

    public JwtService(IConfiguration configuration, HmsDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public AuthResponseDto CreateToken(AppUser user, int? hotelId = null)
    {
        var expirationMinutes = _configuration.GetValue<int>("Jwt:ExpirationMinutes");
        var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role)
        };

        if (user.GuestId.HasValue)
            claims.Add(new Claim("GuestId", user.GuestId.Value.ToString()));
        if (user.ManagerId.HasValue)
        {
            var managerHotelId = hotelId ?? _context.Managers
                .Where(x => x.Id == user.ManagerId.Value)
                .Select(x => (int?)x.HotelId)
                .FirstOrDefault();
            if (managerHotelId.HasValue)
                claims.Add(new Claim("HotelId", managerHotelId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Role = user.Role,
            ExpiresAtUtc = expires
        };
    }
}
