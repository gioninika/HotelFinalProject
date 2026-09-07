using Hotel.Dtos;
using Hotel.Models;

namespace Hotel.Services;

public interface IHotelService
{
    Task<List<HotelDto>> GetAllAsync(HotelFilterDto filter);
    Task<HotelDto> GetByIdAsync(int id);
    Task<HotelDto> CreateAsync(CreateHotelDto request);
    Task<HotelDto> UpdateAsync(int id, UpdateHotelDto request);
    Task DeleteAsync(int id);
}

public interface IRoomService
{
    Task<List<RoomDto>> GetAllForHotelAsync(int hotelId, RoomFilterDto filter);
    Task<RoomDto> GetByIdAsync(int hotelId, int roomId);
    Task<RoomDto> CreateAsync(int hotelId, CreateRoomDto request);
    Task<RoomDto> UpdateAsync(int hotelId, int roomId, UpdateRoomDto request);
    Task DeleteAsync(int hotelId, int roomId);
}

public interface IManagerService
{
    Task<ManagerDto> GetByIdAsync(int id);
    Task<ManagerDto> UpdateAsync(int id, UpdateManagerDto request);
    Task DeleteAsync(int id);
}

public interface IGuestService
{
    Task<GuestDto> GetByIdAsync(int id);
    Task<GuestDto> UpdateAsync(int id, UpdateGuestDto request);
    Task DeleteAsync(int id);
}

public interface IReservationService
{
    Task<ReservationDto> GetByIdAsync(int id);
    Task<ReservationDto> CreateAsync(int hotelId, int guestId, CreateReservationDto request);
    Task<ReservationDto> UpdateAsync(int id, UpdateReservationDto request);
    Task DeleteAsync(int id);
    Task<List<ReservationDto>> SearchAsync(ReservationFilterDto filter);
    Task<bool> IsForHotelAsync(int reservationId, int hotelId);
}

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto request);
    Task<AuthResponseDto> RegisterManagerAsync(int hotelId, RegisterDto request);
    Task<AuthResponseDto> LoginAsync(LoginDto request);
}

public interface IJwtService
{
    AuthResponseDto CreateToken(AppUser user, int? hotelId = null);
}
