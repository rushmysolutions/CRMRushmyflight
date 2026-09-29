namespace RushMyBookings.Crm.Entities.Attendance;

public static class AttendanceRoles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string Admin = "Admin";
    public const string Employee = "Employee";
    public const string TeamLead = "TeamLead";
    public const string HR = "HR";
    public const string Head = "Head";

    public static readonly string[] All =
    [
        SystemAdministrator,
        Admin,
        Employee,
        TeamLead,
        HR,
        Head
    ];

    /// <summary>Roles excluded from team/monthly staff lists (not day-to-day attendees).</summary>
    public static bool IsSystemRole(string? role) =>
        role is SystemAdministrator or Admin;

    public const string ManageEmployees = SystemAdministrator + "," + Admin + "," + HR;
    public const string ManageIp = SystemAdministrator + "," + Admin;
    public const string ViewTeam = TeamLead + "," + HR + "," + Head + "," + Admin + "," + SystemAdministrator;
    public const string ViewAll = HR + "," + Head + "," + Admin + "," + SystemAdministrator;
    public const string CloseMonth = SystemAdministrator;
    public const string MarkOwn = Employee + "," + TeamLead + "," + HR + "," + Head + "," + Admin + "," + SystemAdministrator;
}

public static class AttendanceClaimTypes
{
    public const string EmployeeId = "attendance_employee_id";
    public const string AttendanceRole = "attendance_role";
    public const string TeamId = "attendance_team_id";
    public const string LoginPortal = "login_portal";
}

public static class LoginPortals
{
    public const string Crm = "CRM";
    public const string Internal = "Internal";

    public static readonly string[] All = [Crm, Internal];
}

public static class DailyAttendanceStatus
{
    public const string Present = "Present";
    public const string Absent = "Absent";
    public const string HalfDay = "HalfDay";
    public const string Leave = "Leave";
    public const string Wfh = "WFH";
    public const string Holiday = "Holiday";

    public static readonly string[] All =
    [
        Present,
        Absent,
        HalfDay,
        Leave,
        Wfh,
        Holiday
    ];

    /// <summary>Statuses an employee can pick for their own day.</summary>
    public static readonly string[] SelfSelectable =
    [
        Present,
        Wfh,
        HalfDay,
        Leave
    ];
}

public static class MonthlyAttendanceStatus
{
    public const string Draft = "Draft";
    public const string Locked = "Locked";
}
