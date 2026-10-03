using System.Security.Claims;
using RushMyBookings.Crm.Entities.Attendance;

namespace RushMyBookings.Crm.Helpers;

public static class AttendanceAccess
{
    public static int? GetEmployeeId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AttendanceClaimTypes.EmployeeId);
        return int.TryParse(value, out var id) ? id : null;
    }

    public static string? GetAttendanceRole(ClaimsPrincipal user)
    {
        return user.FindFirstValue(AttendanceClaimTypes.AttendanceRole)
            ?? user.FindFirstValue(ClaimTypes.Role);
    }

    public static int? GetTeamId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AttendanceClaimTypes.TeamId);
        return int.TryParse(value, out var id) ? id : null;
    }

    public static bool HasAttendanceAccess(ClaimsPrincipal user) =>
        GetEmployeeId(user).HasValue;

    /// <summary>Employees, teams, departments, designations, holidays, HR day overrides.</summary>
    public static bool CanManageEmployees(ClaimsPrincipal user)
    {
        var role = GetAttendanceRole(user);
        return role is AttendanceRoles.SystemAdministrator
            or AttendanceRoles.Admin
            or AttendanceRoles.HR;
    }

    /// <summary>Allowed IPs — System Administrator and Administrator only.</summary>
    public static bool CanManageIpAddresses(ClaimsPrincipal user)
    {
        var role = GetAttendanceRole(user);
        return role is AttendanceRoles.SystemAdministrator or AttendanceRoles.Admin;
    }

    /// <summary>Close / lock / unlock month — System Administrator only.</summary>
    public static bool CanCloseMonth(ClaimsPrincipal user)
    {
        var role = GetAttendanceRole(user);
        return role is AttendanceRoles.SystemAdministrator;
    }

    public static bool CanViewAll(ClaimsPrincipal user)
    {
        var role = GetAttendanceRole(user);
        return role is AttendanceRoles.SystemAdministrator
            or AttendanceRoles.Admin
            or AttendanceRoles.HR
            or AttendanceRoles.Head;
    }

    public static bool CanViewTeam(ClaimsPrincipal user)
    {
        var role = GetAttendanceRole(user);
        return role is AttendanceRoles.TeamLead
            or AttendanceRoles.SystemAdministrator
            or AttendanceRoles.Admin
            or AttendanceRoles.HR
            or AttendanceRoles.Head;
    }

    public static bool IsSystemAdministrator(ClaimsPrincipal user)
    {
        var role = GetAttendanceRole(user);
        return string.Equals(role, AttendanceRoles.SystemAdministrator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Shifts, team/employee schedules, full roster — SysAdmin / Admin / HR.</summary>
    public static bool CanManageAllRosters(ClaimsPrincipal user)
    {
        var role = GetAttendanceRole(user);
        return role is AttendanceRoles.SystemAdministrator
            or AttendanceRoles.Admin
            or AttendanceRoles.HR;
    }

    /// <summary>Team Lead can plan week-offs for their own team members.</summary>
    public static bool CanManageTeamRoster(ClaimsPrincipal user)
    {
        var role = GetAttendanceRole(user);
        return role is AttendanceRoles.TeamLead
            or AttendanceRoles.SystemAdministrator
            or AttendanceRoles.Admin
            or AttendanceRoles.HR;
    }

    public static string GetLoginPortal(ClaimsPrincipal user) =>
        user.FindFirstValue(AttendanceClaimTypes.LoginPortal) ?? LoginPortals.Crm;

    public static bool IsCrmPortal(ClaimsPrincipal user) =>
        GetLoginPortal(user).Equals(LoginPortals.Crm, StringComparison.OrdinalIgnoreCase);

    public static bool IsInternalPortal(ClaimsPrincipal user) =>
        GetLoginPortal(user).Equals(LoginPortals.Internal, StringComparison.OrdinalIgnoreCase);

    public static string RoleDisplayName(string? role) => role switch
    {
        AttendanceRoles.SystemAdministrator => "System Administrator",
        AttendanceRoles.Admin => "Administrator",
        AttendanceRoles.TeamLead => "Team Lead",
        AttendanceRoles.HR => "HR",
        AttendanceRoles.Head => "Head",
        AttendanceRoles.Employee => "Employee",
        _ => role ?? "—"
    };
}
