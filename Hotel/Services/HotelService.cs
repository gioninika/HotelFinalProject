using Hotel.Data;
using Hotel.Dtos;
using Hotel.Exceptions;
using Hotel.Models;
using Hotel.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Services;

public class HotelService : IHotelService
{
    private readonly HmsDbContext _context;
    private readonly IGenericRepository<Models.Hotel> _repository;

    public HotelService(HmsDbContext context, IGenericRepository<Models.Hotel> repository)
    {
        _context = context;
        _repository = repository;
    }

    public async Task<List<HotelDto>> GetAllAsync(HotelFilterDto filter)
    {
        IQueryable<Models.Hotel> query = _context.Hotels.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(x => x.Country == filter.Country);
        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(x => x.City == filter.City);
        if (filter.Rating.HasValue)
            query = query.Where(x => x.Rating == filter.Rating.Value);

        return (await query.ToListAsync()).Adapt<List<HotelDto>>();
    }

    public async Task<HotelDto> GetByIdAsync(int id)
    {
        var hotel = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Hotel was not found.");
        return hotel.Adapt<HotelDto>();
    }

    public async Task<HotelDto> CreateAsync(CreateHotelDto request)
    {
        var hotel = request.Adapt<Models.Hotel>();
        await _repository.AddAsync(hotel);
        await _repository.SaveAsync();
        return hotel.Adapt<HotelDto>();
    }

    public async Task<HotelDto> UpdateAsync(int id, UpdateHotelDto request)
    {
        var hotel = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Hotel was not found.");
        hotel.Name = request.Name;
        hotel.Address = request.Address;
        hotel.Rating = request.Rating;
        _repository.Update(hotel);
        await _repository.SaveAsync();
        return hotel.Adapt<HotelDto>();
    }

    public async Task DeleteAsync(int id)
    {
        var hotel = await _context.Hotels
            .Include(x => x.Rooms)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Hotel was not found.");

        if (hotel.Rooms.Count != 0)
            throw new BusinessRuleException("A hotel with rooms cannot be deleted.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasActiveReservation = await _context.Reservations.AnyAsync(x =>
            x.CheckInDate <= today && x.CheckOutDate > today &&
            x.ReservationRooms.Any(rr => rr.Room!.HotelId == id));
        if (hasActiveReservation)
            throw new BusinessRuleException("A hotel with active reservations cannot be deleted.");

        var managerIds = await _context.Managers.Where(x => x.HotelId == id).Select(x => x.Id).ToListAsync();
        var loginUsers = await _context.AppUsers
            .Where(x => x.ManagerId.HasValue && managerIds.Contains(x.ManagerId.Value))
            .ToListAsync();
        _context.AppUsers.RemoveRange(loginUsers);
        _repository.Delete(hotel);
        await _repository.SaveAsync();
    }
}
