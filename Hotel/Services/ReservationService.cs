using Hotel.Data;
using Hotel.Dtos;
using Hotel.Exceptions;
using Hotel.Models;
using Hotel.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Services;

public class ReservationService : IReservationService
{
    private readonly HmsDbContext _context;
    private readonly IGenericRepository<Reservation> _repository;

    public ReservationService(HmsDbContext context, IGenericRepository<Reservation> repository)
    {
        _context = context;
        _repository = repository;
    }

    public async Task<ReservationDto> GetByIdAsync(int id)
    {
        var reservation = await GetReservationWithRoomsAsync(id);
        return ToDto(reservation);
    }

    public async Task<ReservationDto> CreateAsync(int hotelId, int guestId, CreateReservationDto request)
    {
        ValidateDates(request.CheckInDate, request.CheckOutDate, true);
        if (!await _context.Hotels.AnyAsync(x => x.Id == hotelId))
            throw new KeyNotFoundException("Hotel was not found.");
        if (!await _context.Guests.AnyAsync(x => x.Id == guestId))
            throw new KeyNotFoundException("Guest was not found.");

        var roomIds = request.RoomIds.Distinct().ToList();
        var rooms = await _context.Rooms.Where(x => roomIds.Contains(x.Id) && x.HotelId == hotelId).ToListAsync();
        if (rooms.Count != roomIds.Count)
            throw new BusinessRuleException("Every selected room must belong to this hotel.");
        if (await HasUnavailableRoomAsync(roomIds, request.CheckInDate, request.CheckOutDate))
            throw new BusinessRuleException("One or more selected rooms are already reserved for these dates.");

        var reservation = new Reservation
        {
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            GuestId = guestId,
            ReservationRooms = roomIds.Select(id => new ReservationRoom { RoomId = id }).ToList()
        };
        await _repository.AddAsync(reservation);
        await _repository.SaveAsync();
        return ToDto(reservation);
    }

    public async Task<ReservationDto> UpdateAsync(int id, UpdateReservationDto request)
    {
        ValidateDates(request.CheckInDate, request.CheckOutDate, true);
        var reservation = await GetReservationWithRoomsAsync(id);
        var roomIds = reservation.ReservationRooms.Select(x => x.RoomId).ToList();
        if (await HasUnavailableRoomAsync(roomIds, request.CheckInDate, request.CheckOutDate, id))
            throw new BusinessRuleException("The reservation dates overlap with another reservation.");

        reservation.CheckInDate = request.CheckInDate;
        reservation.CheckOutDate = request.CheckOutDate;
        _repository.Update(reservation);
        await _repository.SaveAsync();
        return ToDto(reservation);
    }

    public async Task DeleteAsync(int id)
    {
        var reservation = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Reservation was not found.");
        _repository.Delete(reservation);
        await _repository.SaveAsync();
    }

    public async Task<List<ReservationDto>> SearchAsync(ReservationFilterDto filter)
    {
        IQueryable<Reservation> query = _context.Reservations
            .Include(x => x.ReservationRooms)
            .ThenInclude(x => x.Room)
            .AsNoTracking();

        if (filter.HotelId.HasValue)
            query = query.Where(x => x.ReservationRooms.Any(rr => rr.Room!.HotelId == filter.HotelId.Value));
        if (filter.GuestId.HasValue)
            query = query.Where(x => x.GuestId == filter.GuestId.Value);
        if (filter.RoomId.HasValue)
            query = query.Where(x => x.ReservationRooms.Any(rr => rr.RoomId == filter.RoomId.Value));
        if (filter.Date.HasValue)
            query = query.Where(x => x.CheckInDate <= filter.Date.Value && x.CheckOutDate > filter.Date.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (filter.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => x.CheckInDate <= today && x.CheckOutDate > today);
            else if (filter.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                query = query.Where(x => x.CheckOutDate <= today);
            else
                throw new ArgumentException("Status must be active or completed.");
        }

        return (await query.ToListAsync()).Select(ToDto).ToList();
    }

    public Task<bool> IsForHotelAsync(int reservationId, int hotelId) => _context.Reservations.AnyAsync(x =>
        x.Id == reservationId && x.ReservationRooms.Any(rr => rr.Room!.HotelId == hotelId));

    private async Task<Reservation> GetReservationWithRoomsAsync(int id)
    {
        return await _context.Reservations
            .Include(x => x.ReservationRooms)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Reservation was not found.");
    }

    private async Task<bool> HasUnavailableRoomAsync(List<int> roomIds, DateOnly checkIn, DateOnly checkOut, int? currentReservationId = null)
    {
        return await _context.ReservationRooms.AnyAsync(x => roomIds.Contains(x.RoomId) &&
            (!currentReservationId.HasValue || x.ReservationId != currentReservationId.Value) &&
            x.Reservation!.CheckInDate < checkOut && x.Reservation.CheckOutDate > checkIn);
    }

    private static void ValidateDates(DateOnly checkIn, DateOnly checkOut, bool mustStartTodayOrLater)
    {
        if (mustStartTodayOrLater && checkIn < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Check-in date must be today or later.");
        if (checkOut <= checkIn)
            throw new ArgumentException("Check-out date must be after check-in date.");
    }

    private static ReservationDto ToDto(Reservation reservation) => new()
    {
        Id = reservation.Id,
        CheckInDate = reservation.CheckInDate,
        CheckOutDate = reservation.CheckOutDate,
        GuestId = reservation.GuestId,
        RoomIds = reservation.ReservationRooms.Select(x => x.RoomId).ToList()
    };
}
