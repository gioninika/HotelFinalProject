using Hotel.Models;
using Microsoft.EntityFrameworkCore;

namespace Hotel.Data;

public class HmsDbContext : DbContext
{
    public HmsDbContext(DbContextOptions<HmsDbContext> options) : base(options)
    {
    }

    public DbSet<Hotel.Models.Hotel> Hotels => Set<Hotel.Models.Hotel>();
    public DbSet<Manager> Managers => Set<Manager>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationRoom> ReservationRooms => Set<ReservationRoom>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Manager>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Manager>().HasIndex(x => x.PersonalNumber).IsUnique();
        modelBuilder.Entity<Guest>().HasIndex(x => x.PersonalNumber).IsUnique();
        modelBuilder.Entity<Guest>().HasIndex(x => x.PhoneNumber).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.UserName).IsUnique();

        modelBuilder.Entity<Room>().Property(x => x.Price).HasColumnType("decimal(18,2)");

        modelBuilder.Entity<ReservationRoom>()
            .HasKey(x => new { x.ReservationId, x.RoomId });

        modelBuilder.Entity<ReservationRoom>()
            .HasOne(x => x.Reservation)
            .WithMany(x => x.ReservationRooms)
            .HasForeignKey(x => x.ReservationId);

        modelBuilder.Entity<ReservationRoom>()
            .HasOne(x => x.Room)
            .WithMany(x => x.ReservationRooms)
            .HasForeignKey(x => x.RoomId);

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hotel.Models.Hotel>().HasData(new Hotel.Models.Hotel
        {
            Id = 1,
            Name = "Sample Hotel",
            Rating = 4,
            Country = "Georgia",
            City = "Tbilisi",
            Address = "1 Rustaveli Avenue"
        });

        var admin = new AppUser
        {
            Id = 1,
            UserName = "admin@hms.com",
            Role = Roles.Admin,
            // The matching seeded password is Admin123!. This fixed value prevents
            // EF from detecting a false seed-data change every time it builds the model.
            PasswordHash = "AQAAAAIAAYagAAAAEC+7ZsCrhlsT7/CMbDx+a+Lq+oh2A5WFog191fzL+y97y0/RQgJt63SM2BCoRnLHgQ=="
        };
        modelBuilder.Entity<AppUser>().HasData(admin);
    }
}
