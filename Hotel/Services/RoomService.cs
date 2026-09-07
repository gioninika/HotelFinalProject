using Hotel.Data;
using Hotel.Dtos;
using Hotel.Exceptions;
using Hotel.Models;
using Hotel.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Services;

public class RoomService : IRoomService
{
    private readonly HmsDbContext _context;
    private readonly IGenericRepository<Room> _repository;

    public RoomService(HmsDbContext context, IGenericRepository<Room> repository)
    {
        _context = context;
        _repository = repository;
    }

    public async Task<List<RoomDto>> GetAllForHotelAsync(int hotelId, RoomFilterDto filter)
    {
        await CheckHotelAsync(hotelId);
        if (filter.MinPrice.HasValue && filter.MaxPrice.HasValue && filter.MinPrice > filter.MaxPrice)
            throw new ArgumentException("Minimum price cannot be greater than maximum price.");
        if (filter.CheckInDate.HasValue != filter.CheckOutDate.HasValue)
            throw new ArgumentException("Both check-in and check-out dates are needed for availability search.");
        if (filter.CheckInDate >= filter.CheckOutDate)
            throw new ArgumentException("Check-out date must be after check-in date.");

        IQueryable<Room> query = _context.Rooms.Where(x => x.HotelId == hotelId);
        if (filter.MinPrice.HasValue) query = query.Where(x => x.Price >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue) query = query.Where(x => x.Price <= filter.MaxPrice.Value);
        if (filter.CheckInDate.HasValue)
        {
            var checkIn = filter.CheckInDate.Value;
            var checkOut = filter.CheckOutDate!.Value;
            query = query.Where(x => !x.ReservationRooms.Any(rr =>
                rr.Reservation!.CheckInDate < checkOut && rr.Reservation.CheckOutDate > checkIn));
        }

        return (await query.AsNoTracking().ToListAsync()).Adapt<List<RoomDto>>();
    }

    public async Task<RoomDto> GetByIdAsync(int hotelId, int roomId)
    {
        var room = await FindRoomAsync(hotelId, roomId);
        return room.Adapt<RoomDto>();
    }

    public async Task<RoomDto> CreateAsync(int hotelId, CreateRoomDto request)
    {
        await CheckHotelAsync(hotelId);
        if (request.Price <= 0) throw new ArgumentException("Room price must be greater than zero.");

        var room = request.Adapt<Room>();
        room.HotelId = hotelId;
        await _repository.AddAsync(room);
        await _repository.SaveAsync();
        return room.Adapt<RoomDto>();
    }

    public async Task<RoomDto> UpdateAsync(int hotelId, int roomId, UpdateRoomDto request)
    {
        if (request.Price <= 0) throw new ArgumentException("Room price must be greater than zero.");
        var room = await FindRoomAsync(hotelId, roomId);
        room.Name = request.Name;
        room.Price = request.Price;
        _repository.Update(room);
        await _repository.SaveAsync();
        return room.Adapt<RoomDto>();
    }

    public async Task DeleteAsync(int hotelId, int roomId)
    {
        var room = await FindRoomAsync(hotelId, roomId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reservedNowOrLater = await _context.ReservationRooms.AnyAsync(x =>
            x.RoomId == roomId && x.Reservation!.CheckOutDate >= today);
        if (reservedNowOrLater)
            throw new BusinessRuleException("A room with active or future reservations cannot be deleted.");

        _repository.Delete(room);
        await _repository.SaveAsync();
    }

    private async Task CheckHotelAsync(int hotelId)
    {
        if (!await _context.Hotels.AnyAsync(x => x.Id == hotelId))
            throw new KeyNotFoundException("Hotel was not found.");
    }

    private async Task<Room> FindRoomAsync(int hotelId, int roomId)
    {
        return await _context.Rooms.FirstOrDefaultAsync(x => x.Id == roomId && x.HotelId == hotelId)
            ?? throw new KeyNotFoundException("Room was not found for this hotel.");
    }
}
