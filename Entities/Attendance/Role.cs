using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

public class Role
{
    public int RoleId { get; set; }

    /// <summary>Stable code saved conceptually as Role.Code; Employees store RoleId.</summary>
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(80)]
    public string DisplayName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
