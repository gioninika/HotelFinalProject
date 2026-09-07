using Hotel.Data;
using Hotel.Dtos;
using Hotel.Exceptions;
using Hotel.Models;
using Hotel.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Services;

public class ManagerService : IManagerService
{
    private readonly HmsDbContext _context;
    private readonly IGenericRepository<Manager> _repository;

    public ManagerService(HmsDbContext context, IGenericRepository<Manager> repository)
    {
        _context = context;
        _repository = repository;
    }

    public async Task<ManagerDto> GetByIdAsync(int id)
    {
        var manager = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Manager was not found.");
        return manager.Adapt<ManagerDto>();
    }

    public async Task<ManagerDto> UpdateAsync(int id, UpdateManagerDto request)
    {
        var manager = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Manager was not found.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _context.Managers.AnyAsync(x => x.Id != id && (x.Email == email || x.PersonalNumber == request.PersonalNumber)))
            throw new BusinessRuleException("Manager email or personal number is already registered.");
        var user = await _context.AppUsers.FirstOrDefaultAsync(x => x.ManagerId == id);
        if (user is not null && await _context.AppUsers.AnyAsync(x => x.Id != user.Id && x.UserName == email))
            throw new BusinessRuleException("This login name is already used.");

        manager.FirstName = request.FirstName;
        manager.LastName = request.LastName;
        manager.PersonalNumber = request.PersonalNumber;
        manager.Email = email;
        manager.PhoneNumber = request.PhoneNumber;
        if (user is not null) user.UserName = email;
        _repository.Update(manager);
        await _repository.SaveAsync();
        return manager.Adapt<ManagerDto>();
    }

    public async Task DeleteAsync(int id)
    {
        var manager = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Manager was not found.");
        var anotherManagerExists = await _context.Managers.AnyAsync(x => x.HotelId == manager.HotelId && x.Id != id);
        if (!anotherManagerExists)
            throw new BusinessRuleException("The hotel must have another manager before this manager can be deleted.");

        var user = await _context.AppUsers.FirstOrDefaultAsync(x => x.ManagerId == id);
        if (user is not null) _context.AppUsers.Remove(user);
        _repository.Delete(manager);
        await _repository.SaveAsync();
    }
}
