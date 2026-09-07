using System.ComponentModel.DataAnnotations;

namespace Hotel.Dtos;

public class CreateHotelDto
{
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Range(1, 5)] public int Rating { get; set; }
    [Required, StringLength(80)] public string Country { get; set; } = string.Empty;
    [Required, StringLength(80)] public string City { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Address { get; set; } = string.Empty;
}

public class UpdateHotelDto
{
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Range(1, 5)] public int Rating { get; set; }
    [Required, StringLength(200)] public string Address { get; set; } = string.Empty;
}

public class CreateRoomDto
{
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "999999999")] public decimal Price { get; set; }
}

public class UpdateRoomDto : CreateRoomDto
{
}

public class RegisterDto
{
    [Required, StringLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LastName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string PersonalNumber { get; set; } = string.Empty;
    [Required, StringLength(30)] public string PhoneNumber { get; set; } = string.Empty;
    [EmailAddress] public string? Email { get; set; }
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = string.Empty;
    public int? HotelId { get; set; }
}

public class UpdateManagerDto
{
    [Required, StringLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LastName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string PersonalNumber { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, StringLength(30)] public string PhoneNumber { get; set; } = string.Empty;
}

public class UpdateGuestDto
{
    [Required, StringLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LastName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string PersonalNumber { get; set; } = string.Empty;
    [Required, StringLength(30)] public string PhoneNumber { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required] public string UserName { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class CreateReservationDto
{
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    [Required, MinLength(1)] public List<int> RoomIds { get; set; } = [];
}

public class UpdateReservationDto
{
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
}

public class HotelFilterDto
{
    public string? Country { get; set; }
    public string? City { get; set; }
    [Range(1, 5)] public int? Rating { get; set; }
}

public class RoomFilterDto
{
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }
}

public class ReservationFilterDto
{
    public int? HotelId { get; set; }
    public int? GuestId { get; set; }
    public int? RoomId { get; set; }
    public DateOnly? Date { get; set; }
    public string? Status { get; set; }
}
