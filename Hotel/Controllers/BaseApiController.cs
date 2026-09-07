using System.Security.Claims;
using Hotel.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hotel.Controllers;

public abstract class BaseApiController : ControllerBase
{
    protected bool IsAdmin() => User.IsInRole(Roles.Admin);

    protected bool CanManageHotel(int hotelId)
    {
        return IsAdmin() || (User.IsInRole(Roles.Manager) && GetClaimInt("HotelId") == hotelId);
    }

    protected int? GetGuestId() => GetClaimInt("GuestId");
    protected int? GetManagerHotelId() => GetClaimInt("HotelId");

    private int? GetClaimInt(string claimType)
    {
        var value = User.FindFirstValue(claimType);
        return int.TryParse(value, out var id) ? id : null;
    }
}
