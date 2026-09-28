namespace Hotel.Dtos;

public class HotelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class RoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int HotelId { get; set; }
}

public class ManagerDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PersonalNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int HotelId { get; set; }
}

public class GuestDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PersonalNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class ReservationDto
{
    public int Id { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int GuestId { get; set; }
    public List<int> RoomIds { get; set; } = [];
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public class RegistrationResponseDto
{
    public string Email { get; set; } = string.Empty;
}
