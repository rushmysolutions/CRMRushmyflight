namespace RushMyBookings.Crm.Entities.Attendance;

/// <summary>
/// Optional override for one employee.
/// Null ShiftId / WeekOffMask means "use the team default".
/// </summary>
public class EmployeeSchedule
{
    public int EmployeeId { get; set; }

    public int? ShiftId { get; set; }

    public byte? WeekOffMask { get; set; }

    public Employee? Employee { get; set; }

    public ShiftTemplate? Shift { get; set; }
}
