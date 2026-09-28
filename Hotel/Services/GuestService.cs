using Hotel.Data;
using Hotel.Dtos;
using Hotel.Exceptions;
using Hotel.Models;
using Hotel.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Services;

public class GuestService : IGuestService
{
    private readonly HmsDbContext _context;
    private readonly IGenericRepository<Guest> _repository;

    public GuestService(HmsDbContext context, IGenericRepository<Guest> repository)
    {
        _context = context;
        _repository = repository;
    }

    public async Task<GuestDto> GetByIdAsync(int id)
    {
        var guest = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Guest was not found.");
        return guest.Adapt<GuestDto>();
    }

    public async Task<GuestDto> UpdateAsync(int id, UpdateGuestDto request)
    {
        var guest = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Guest was not found.");
        if (await _context.Guests.AnyAsync(x => x.Id != id &&
            (x.PhoneNumber == request.PhoneNumber || x.PersonalNumber == request.PersonalNumber)))
            throw new BusinessRuleException("Personal number or phone number is already used by another guest.");
        guest.FirstName = request.FirstName;
        guest.LastName = request.LastName;
        guest.PersonalNumber = request.PersonalNumber;
        guest.PhoneNumber = request.PhoneNumber;
        _repository.Update(guest);
        await _repository.SaveAsync();
        return guest.Adapt<GuestDto>();
    }

    public async Task DeleteAsync(int id)
    {
        var guest = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Guest was not found.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (await _context.Reservations.AnyAsync(x => x.GuestId == id && x.CheckOutDate >= today))
            throw new BusinessRuleException("A guest with active or future reservations cannot be deleted.");

        var user = await _context.AppUsers.FirstOrDefaultAsync(x => x.GuestId == id);
        if (user is not null) _context.AppUsers.Remove(user);
        _repository.Delete(guest);
        await _repository.SaveAsync();
    }
}
