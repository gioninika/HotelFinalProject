using Hotel.Dtos;
using Hotel.Models;
using Hotel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hotel.Controllers;

[ApiController]
[Route("api")]
public class ReservationsController : BaseApiController
{
    private readonly IReservationService _reservationService;

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost("hotels/{hotelId:int}/reservations")]
    [Authorize(Roles = Roles.Guest)]
    public async Task<ActionResult<ApiResponse<ReservationDto>>> Create(int hotelId, CreateReservationDto request)
    {
        var guestId = GetGuestId();
        if (!guestId.HasValue)
            return Unauthorized(ApiResponse<ReservationDto>.Fail("Guest identity was not found in the token."));

        var reservation = await _reservationService.CreateAsync(hotelId, guestId.Value, request);
        return CreatedAtAction(nameof(GetById), new { reservationId = reservation.Id }, ApiResponse<ReservationDto>.Ok(reservation, "Reservation created successfully."));
    }

    [HttpGet("reservations/{reservationId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager + "," + Roles.Guest)]
    public async Task<ActionResult<ApiResponse<ReservationDto>>> GetById(int reservationId)
    {
        if (!await CanAccessReservationAsync(reservationId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ReservationDto>.Fail("You cannot access this reservation."));
        var reservation = await _reservationService.GetByIdAsync(reservationId);
        return Ok(ApiResponse<ReservationDto>.Ok(reservation));
    }

    [HttpPut("reservations/{reservationId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager + "," + Roles.Guest)]
    public async Task<ActionResult<ApiResponse<ReservationDto>>> Update(int reservationId, UpdateReservationDto request)
    {
        if (!await CanAccessReservationAsync(reservationId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<ReservationDto>.Fail("You cannot update this reservation."));
        var reservation = await _reservationService.UpdateAsync(reservationId, request);
        return Ok(ApiResponse<ReservationDto>.Ok(reservation, "Reservation dates updated successfully."));
    }

    [HttpDelete("reservations/{reservationId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager + "," + Roles.Guest)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int reservationId)
    {
        if (!await CanAccessReservationAsync(reservationId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("You cannot cancel this reservation."));
        await _reservationService.DeleteAsync(reservationId);
        return Ok(ApiResponse<object>.Ok(null, "Reservation cancelled successfully."));
    }

    [HttpGet("reservations")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager + "," + Roles.Guest)]
    public async Task<ActionResult<ApiResponse<List<ReservationDto>>>> Search([FromQuery] ReservationFilterDto filter)
    {
        if (User.IsInRole(Roles.Guest))
            filter.GuestId = GetGuestId();
        else if (User.IsInRole(Roles.Manager))
            filter.HotelId = GetManagerHotelId();

        var reservations = await _reservationService.SearchAsync(filter);
        return Ok(ApiResponse<List<ReservationDto>>.Ok(reservations));
    }

    private async Task<bool> CanAccessReservationAsync(int reservationId)
    {
        if (IsAdmin()) return true;
        var reservation = await _reservationService.GetByIdAsync(reservationId);
        if (User.IsInRole(Roles.Guest)) return GetGuestId() == reservation.GuestId;
        if (User.IsInRole(Roles.Manager) && GetManagerHotelId().HasValue)
            return await _reservationService.IsForHotelAsync(reservationId, GetManagerHotelId()!.Value);
        return false;
    }
}
