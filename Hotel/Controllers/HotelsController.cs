using Hotel.Dtos;
using Hotel.Models;
using Hotel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hotel.Controllers;

[ApiController]
[Route("api/hotels")]
public class HotelsController : BaseApiController
{
    private readonly IHotelService _hotelService;
    private readonly IRoomService _roomService;
    private readonly IAuthService _authService;

    public HotelsController(IHotelService hotelService, IRoomService roomService, IAuthService authService)
    {
        _hotelService = hotelService;
        _roomService = roomService;
        _authService = authService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<HotelDto>>>> GetAll([FromQuery] HotelFilterDto filter)
    {
        var hotels = await _hotelService.GetAllAsync(filter);
        return Ok(ApiResponse<List<HotelDto>>.Ok(hotels));
    }

    [HttpGet("{hotelId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<HotelDto>>> GetById(int hotelId)
    {
        var hotel = await _hotelService.GetByIdAsync(hotelId);
        return Ok(ApiResponse<HotelDto>.Ok(hotel));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse<HotelDto>>> Create(CreateHotelDto request)
    {
        var hotel = await _hotelService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { hotelId = hotel.Id }, ApiResponse<HotelDto>.Ok(hotel, "Hotel created successfully."));
    }

    [HttpPut("{hotelId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
    public async Task<ActionResult<ApiResponse<HotelDto>>> Update(int hotelId, UpdateHotelDto request)
    {
        if (!CanManageHotel(hotelId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<HotelDto>.Fail("You can manage only your own hotel."));
        var hotel = await _hotelService.UpdateAsync(hotelId, request);
        return Ok(ApiResponse<HotelDto>.Ok(hotel, "Hotel updated successfully."));
    }

    [HttpDelete("{hotelId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int hotelId)
    {
        if (!CanManageHotel(hotelId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("You can manage only your own hotel."));
        await _hotelService.DeleteAsync(hotelId);
        return Ok(ApiResponse<object>.Ok(null, "Hotel deleted successfully."));
    }

    [HttpGet("{hotelId:int}/rooms")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<RoomDto>>>> GetRooms(int hotelId, [FromQuery] RoomFilterDto filter)
    {
        var rooms = await _roomService.GetAllForHotelAsync(hotelId, filter);
        return Ok(ApiResponse<List<RoomDto>>.Ok(rooms));
    }

    [HttpPost("{hotelId:int}/rooms")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
    public async Task<ActionResult<ApiResponse<RoomDto>>> CreateRoom(int hotelId, CreateRoomDto request)
    {
        if (!CanManageHotel(hotelId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<RoomDto>.Fail("You can manage only your own hotel."));
        var room = await _roomService.CreateAsync(hotelId, request);
        return CreatedAtAction(nameof(GetRoomById), new { hotelId, roomId = room.Id }, ApiResponse<RoomDto>.Ok(room, "Room created successfully."));
    }

    [HttpGet("{hotelId:int}/rooms/{roomId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<RoomDto>>> GetRoomById(int hotelId, int roomId)
    {
        var room = await _roomService.GetByIdAsync(hotelId, roomId);
        return Ok(ApiResponse<RoomDto>.Ok(room));
    }

    [HttpPut("{hotelId:int}/rooms/{roomId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
    public async Task<ActionResult<ApiResponse<RoomDto>>> UpdateRoom(int hotelId, int roomId, UpdateRoomDto request)
    {
        if (!CanManageHotel(hotelId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<RoomDto>.Fail("You can manage only your own hotel."));
        var room = await _roomService.UpdateAsync(hotelId, roomId, request);
        return Ok(ApiResponse<RoomDto>.Ok(room, "Room updated successfully."));
    }

    [HttpDelete("{hotelId:int}/rooms/{roomId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRoom(int hotelId, int roomId)
    {
        if (!CanManageHotel(hotelId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("You can manage only your own hotel."));
        await _roomService.DeleteAsync(hotelId, roomId);
        return Ok(ApiResponse<object>.Ok(null, "Room deleted successfully."));
    }

    [HttpPost("{hotelId:int}/managers")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Manager)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> CreateManager(int hotelId, RegisterDto request)
    {
        if (!CanManageHotel(hotelId))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AuthResponseDto>.Fail("You can manage only your own hotel."));
        var result = await _authService.RegisterManagerAsync(hotelId, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponseDto>.Ok(result, "Manager created successfully."));
    }
}
