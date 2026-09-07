using Hotel.Dtos;
using Hotel.Models;
using Hotel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hotel.Controllers;

[ApiController]
[Route("api/guests")]
[Authorize(Roles = Roles.Admin + "," + Roles.Guest)]
public class GuestsController : BaseApiController
{
    private readonly IGuestService _guestService;

    public GuestsController(IGuestService guestService)
    {
        _guestService = guestService;
    }

    [HttpPut("{guestId:int}")]
    public async Task<ActionResult<ApiResponse<GuestDto>>> Update(int guestId, UpdateGuestDto request)
    {
        if (!IsAdmin() && GetGuestId() != guestId)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<GuestDto>.Fail("You can update only your own guest profile."));
        var guest = await _guestService.UpdateAsync(guestId, request);
        return Ok(ApiResponse<GuestDto>.Ok(guest, "Guest updated successfully."));
    }

    [HttpDelete("{guestId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int guestId)
    {
        if (!IsAdmin() && GetGuestId() != guestId)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("You can delete only your own guest profile."));
        await _guestService.DeleteAsync(guestId);
        return Ok(ApiResponse<object>.Ok(null, "Guest deleted successfully."));
    }
}
