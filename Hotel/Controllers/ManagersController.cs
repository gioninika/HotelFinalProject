using Hotel.Dtos;
using Hotel.Models;
using Hotel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hotel.Controllers;

[ApiController]
[Route("api/managers")]
[Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
public class ManagersController : BaseApiController
{
    private readonly IManagerService _managerService;

    public ManagersController(IManagerService managerService)
    {
        _managerService = managerService;
    }

    [HttpPut("{managerId:int}")]
    public async Task<ActionResult<ApiResponse<ManagerDto>>> Update(int managerId, UpdateManagerDto request)
    {
        var manager = await _managerService.GetByIdAsync(managerId);
        if (!CanManageHotel(manager.HotelId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ManagerDto>.Fail("You can manage only managers from your hotel."));

        var updatedManager = await _managerService.UpdateAsync(managerId, request);
        return Ok(ApiResponse<ManagerDto>.Ok(updatedManager, "Manager updated successfully."));
    }

    [HttpDelete("{managerId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int managerId)
    {
        var manager = await _managerService.GetByIdAsync(managerId);
        if (!CanManageHotel(manager.HotelId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("You can manage only managers from your hotel."));

        await _managerService.DeleteAsync(managerId);
        return Ok(ApiResponse<object>.Ok(null, "Manager deleted successfully."));
    }
}
