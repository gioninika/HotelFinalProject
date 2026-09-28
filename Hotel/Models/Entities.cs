namespace Hotel.Models;

public class Hotel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public ICollection<Manager> Managers { get; set; } = new List<Manager>();
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}

public class Manager
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PersonalNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int HotelId { get; set; }

    public Hotel? Hotel { get; set; }
}

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int HotelId { get; set; }

    public Hotel? Hotel { get; set; }
    public ICollection<ReservationRoom> ReservationRooms { get; set; } = new List<ReservationRoom>();
}

public class Guest
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PersonalNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public class Reservation
{
    public int Id { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int GuestId { get; set; }

    public Guest? Guest { get; set; }
    public ICollection<ReservationRoom> ReservationRooms { get; set; } = new List<ReservationRoom>();
}

public class ReservationRoom
{
    public int ReservationId { get; set; }
    public int RoomId { get; set; }

    public Reservation? Reservation { get; set; }
    public Room? Room { get; set; }
}

// This table stores login data. It is separate from Manager and Guest because passwords
// are not part of the business entities from the assignment.
public class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? EmailCodeHash { get; set; }
    public string? EmailCodePurpose { get; set; }
    public DateTime? EmailCodeExpiresAtUtc { get; set; }
    public DateTime? EmailCodeSentAtUtc { get; set; }
    public int EmailCodeFailedAttempts { get; set; }
    public int? ManagerId { get; set; }
    public int? GuestId { get; set; }
}
