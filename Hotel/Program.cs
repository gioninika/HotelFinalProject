
using System.Text;
using Hotel.Data;
using Hotel.Dtos;
using Hotel.Middleware;
using Hotel.Repositories;
using Hotel.Services;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Hotel;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<HmsDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
                ApiResponse<Dictionary<string, string[]>>.Fail("Validation failed."));
        });
        builder.Services.AddOpenApi();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSingleton(TypeAdapterConfig.GlobalSettings);

        builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        builder.Services.AddScoped<IHotelService, HotelService>();
        builder.Services.AddScoped<IRoomService, RoomService>();
        builder.Services.AddScoped<IManagerService, ManagerService>();
        builder.Services.AddScoped<IGuestService, GuestService>();
        builder.Services.AddScoped<IReservationService, ReservationService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IJwtService, JwtService>();

        var jwtKey = builder.Configuration["Jwt:Key"]!;
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<ExceptionMiddleware>();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}
