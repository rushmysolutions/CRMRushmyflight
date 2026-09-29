using System.ComponentModel.DataAnnotations;

namespace RushMyBookings.Crm.Entities.Attendance;

public class Employee
{
    public int EmployeeId { get; set; }

    [MaxLength(30)]
    public string EmpCode { get; set; } = string.Empty;

    [MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? LastName { get; set; }

    [MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(80)]
    public string? Department { get; set; }

    [MaxLength(80)]
    public string? Designation { get; set; }

    [MaxLength(40)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(128)]
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public int? TeamId { get; set; }

    public int? ReportingLeadId { get; set; }

    /// <summary>Optional link to CRM tbl_users.user_id</summary>
    public int? CrmUserId { get; set; }

    public DateTime JoinDate { get; set; } = DateTime.Today;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedByEmployeeId { get; set; }

    public Role? Role { get; set; }

    public Team? Team { get; set; }

    public Employee? ReportingLead { get; set; }

    public ICollection<DailyAttendance> DailyAttendances { get; set; } = new List<DailyAttendance>();

    public ICollection<MonthlyAttendance> MonthlyAttendances { get; set; } = new List<MonthlyAttendance>();

    public string FullName =>
        string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";

    /// <summary>Role code for claims/permissions (requires Role navigation loaded).</summary>
    public string RoleCode => Role?.Code ?? string.Empty;
}
