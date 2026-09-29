using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

public class Designation
{
    public int DesignationId { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
