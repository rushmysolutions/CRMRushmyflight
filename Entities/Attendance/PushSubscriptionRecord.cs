using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

public class PushSubscriptionRecord
{
    public int PushSubscriptionId { get; set; }

    public int EmployeeId { get; set; }

    [MaxLength(500)]
    public string Endpoint { get; set; } = string.Empty;

    [MaxLength(200)]
    public string P256dh { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Auth { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Last attendance date we already reminded to log in.</summary>
    public DateOnly? LastLoginReminderFor { get; set; }

    /// <summary>Last attendance date we already reminded to log out / opt out.</summary>
    public DateOnly? LastLogoutReminderFor { get; set; }

    public Employee? Employee { get; set; }
}
