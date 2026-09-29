using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

public class Holiday
{
    public int HolidayId { get; set; }

    public DateOnly HolidayDate { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public bool IsOptional { get; set; }
}
