using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

public class Team
{
    public int TeamId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int? LeadEmployeeId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Employee? Lead { get; set; }

    public ICollection<Employee> Members { get; set; } = new List<Employee>();
}
