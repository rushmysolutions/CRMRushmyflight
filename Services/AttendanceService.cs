using Microsoft.EntityFrameworkCore;
using RushMyBookings.Crm.Data;
using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Services;

public partial class AttendanceService(AttendanceDbContext db) : IAttendanceService
{
    public async Task EnsureDatabaseAsync()
    {
        await db.Database.EnsureCreatedAsync();

        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                new Role { Code = AttendanceRoles.SystemAdministrator, DisplayName = "System Administrator", SortOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Role { Code = AttendanceRoles.Admin, DisplayName = "Administrator", SortOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Role { Code = AttendanceRoles.HR, DisplayName = "HR", SortOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Role { Code = AttendanceRoles.Head, DisplayName = "Head", SortOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Role { Code = AttendanceRoles.TeamLead, DisplayName = "Team Lead", SortOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow },
                new Role { Code = AttendanceRoles.Employee, DisplayName = "Employee", SortOrder = 6, IsActive = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        if (!await db.Employees.AnyAsync())
        {
            var sysAdminRoleId = await db.Roles
                .Where(r => r.Code == AttendanceRoles.SystemAdministrator)
                .Select(r => r.RoleId)
                .FirstAsync();

            db.Employees.Add(new Employee
            {
                EmpCode = "ADM001",
                FirstName = "System",
                LastName = "Admin",
                Email = "admin@rushmybookings.local",
                Username = "admin",
                PasswordHash = AttendancePasswordHasher.Hash("Admin@123"),
                RoleId = sysAdminRoleId,
                Department = "Administration",
                Designation = "Administrator",
                JoinDate = DateTime.Today,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Designations.AnyAsync())
        {
            db.Designations.AddRange(
                new Designation { Name = "Administrator", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Designation { Name = "Team Lead", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Designation { Name = "Software Engineer", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Designation { Name = "HR Executive", IsActive = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(
                new Department { Name = "Administration", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Department { Name = "Operations", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Department { Name = "Sales", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Department { Name = "Human Resources", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Department { Name = "IT", IsActive = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
    }

    public async Task<Employee?> ValidateEmployeeLoginAsync(string login, string password)
    {
        var key = login.Trim();
        var employee = await db.Employees
            .Include(e => e.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.IsActive &&
                (e.Username == key || e.Email == key || e.EmpCode == key));

        if (employee is null || !AttendancePasswordHasher.Verify(password, employee.PasswordHash))
        {
            return null;
        }

        return employee;
    }

    public async Task<Employee?> FindEmployeeByCrmUserAsync(int crmUserId, string? email)
    {
        var query = db.Employees.AsNoTracking().Include(e => e.Role).Where(e => e.IsActive);

        if (crmUserId > 0)
        {
            var byCrm = await query.FirstOrDefaultAsync(e => e.CrmUserId == crmUserId);
            if (byCrm is not null)
            {
                return byCrm;
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return await query.FirstOrDefaultAsync(e => e.Email == email);
        }

        return null;
    }

    public async Task<Employee?> GetEmployeeAsync(int employeeId)
    {
        // ISNULL keeps dirty/null SQL columns from throwing SqlNullValueException.
        var rows = await db.Database.SqlQueryRaw<EmployeeEditSqlRow>("""
            SELECT
                e.EmployeeId,
                ISNULL(e.EmpCode, N'') AS EmpCode,
                ISNULL(e.FirstName, N'') AS FirstName,
                e.LastName,
                ISNULL(e.Email, N'') AS Email,
                e.Phone,
                e.Department,
                e.Designation,
                ISNULL(e.Username, N'') AS Username,
                ISNULL(e.PasswordHash, N'') AS PasswordHash,
                ISNULL(e.RoleId, 0) AS RoleId,
                e.TeamId,
                e.ReportingLeadId,
                e.CrmUserId,
                ISNULL(e.JoinDate, CAST(GETDATE() AS datetime2)) AS JoinDate,
                CAST(ISNULL(e.IsActive, 1) AS bit) AS IsActive,
                ISNULL(e.CreatedAt, SYSUTCDATETIME()) AS CreatedAt,
                e.CreatedByEmployeeId,
                r.RoleId AS RoleNavId,
                ISNULL(r.Code, N'') AS RoleCode,
                ISNULL(r.DisplayName, N'') AS RoleDisplayName,
                ISNULL(r.SortOrder, 0) AS RoleSortOrder,
                CAST(ISNULL(r.IsActive, 1) AS bit) AS RoleIsActive,
                ISNULL(r.CreatedAt, SYSUTCDATETIME()) AS RoleCreatedAt,
                t.TeamId AS TeamNavId,
                ISNULL(t.Name, N'') AS TeamName,
                t.LeadEmployeeId AS TeamLeadEmployeeId,
                CAST(ISNULL(t.IsActive, 1) AS bit) AS TeamIsActive,
                ISNULL(t.CreatedAt, SYSUTCDATETIME()) AS TeamCreatedAt
            FROM dbo.Employees e
            LEFT JOIN dbo.Roles r ON r.RoleId = e.RoleId
            LEFT JOIN dbo.Teams t ON t.TeamId = e.TeamId
            WHERE e.EmployeeId = {0}
            """, employeeId).ToListAsync();

        var row = rows.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return new Employee
        {
            EmployeeId = row.EmployeeId,
            EmpCode = row.EmpCode,
            FirstName = row.FirstName,
            LastName = row.LastName,
            Email = row.Email,
            Phone = row.Phone,
            Department = row.Department,
            Designation = row.Designation,
            Username = row.Username,
            PasswordHash = row.PasswordHash,
            RoleId = row.RoleId,
            TeamId = row.TeamId,
            ReportingLeadId = row.ReportingLeadId,
            CrmUserId = row.CrmUserId,
            JoinDate = row.JoinDate,
            IsActive = row.IsActive,
            CreatedAt = row.CreatedAt,
            CreatedByEmployeeId = row.CreatedByEmployeeId,
            Role = row.RoleNavId is null
                ? null
                : new Role
                {
                    RoleId = row.RoleNavId.Value,
                    Code = row.RoleCode,
                    DisplayName = row.RoleDisplayName,
                    SortOrder = row.RoleSortOrder,
                    IsActive = row.RoleIsActive,
                    CreatedAt = row.RoleCreatedAt
                },
            Team = row.TeamNavId is null
                ? null
                : new Team
                {
                    TeamId = row.TeamNavId.Value,
                    Name = row.TeamName,
                    LeadEmployeeId = row.TeamLeadEmployeeId,
                    IsActive = row.TeamIsActive,
                    CreatedAt = row.TeamCreatedAt
                }
        };
    }

    public async Task<IReadOnlyList<EmployeeListItem>> GetEmployeesAsync(string? search = null)
    {
        // Raw SQL + ISNULL: null EmpCode/Email/IsActive/Role etc. won't crash the page.
        const string selectSql = """
            SELECT
                e.EmployeeId AS EmployeeId,
                ISNULL(e.EmpCode, N'') AS EmpCode,
                CASE
                    WHEN e.LastName IS NULL OR LTRIM(RTRIM(e.LastName)) = N''
                        THEN ISNULL(e.FirstName, N'')
                    ELSE ISNULL(e.FirstName, N'') + N' ' + e.LastName
                END AS FullName,
                ISNULL(e.Email, N'') AS Email,
                ISNULL(r.DisplayName, N'') AS Role,
                e.Department AS Department,
                t.Name AS TeamName,
                CAST(ISNULL(e.IsActive, 1) AS bit) AS IsActive
            FROM dbo.Employees e
            LEFT JOIN dbo.Roles r ON r.RoleId = e.RoleId
            LEFT JOIN dbo.Teams t ON t.TeamId = e.TeamId
            """;

        if (string.IsNullOrWhiteSpace(search))
        {
            return await db.Database
                .SqlQueryRaw<EmployeeListItem>(selectSql + " ORDER BY EmpCode")
                .ToListAsync();
        }

        var like = "%" + search.Trim() + "%";
        return await db.Database
            .SqlQueryRaw<EmployeeListItem>(
                selectSql + """
                     WHERE e.EmpCode LIKE {0}
                        OR e.FirstName LIKE {0}
                        OR e.LastName LIKE {0}
                        OR e.Email LIKE {0}
                        OR e.Username LIKE {0}
                     ORDER BY EmpCode
                    """,
                like)
            .ToListAsync();
    }

    /// <summary>Column bag for null-safe employee load (Edit / details).</summary>
    private sealed class EmployeeEditSqlRow
    {
        public int EmployeeId { get; set; }
        public string EmpCode { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string? LastName { get; set; }
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public string? Designation { get; set; }
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public int RoleId { get; set; }
        public int? TeamId { get; set; }
        public int? ReportingLeadId { get; set; }
        public int? CrmUserId { get; set; }
        public DateTime JoinDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedByEmployeeId { get; set; }
        public int? RoleNavId { get; set; }
        public string RoleCode { get; set; } = "";
        public string RoleDisplayName { get; set; } = "";
        public int RoleSortOrder { get; set; }
        public bool RoleIsActive { get; set; }
        public DateTime RoleCreatedAt { get; set; }
        public int? TeamNavId { get; set; }
        public string TeamName { get; set; } = "";
        public int? TeamLeadEmployeeId { get; set; }
        public bool TeamIsActive { get; set; }
        public DateTime TeamCreatedAt { get; set; }
    }

    public async Task<IReadOnlyList<TeamOption>> GetTeamsAsync() =>
        await db.Teams
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new TeamOption { TeamId = t.TeamId, Name = t.Name })
            .ToListAsync();

    public async Task<IReadOnlyList<DesignationOption>> GetDesignationsAsync() =>
        await db.Designations
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DesignationOption { DesignationId = d.DesignationId, Name = d.Name })
            .ToListAsync();

    public async Task<IReadOnlyList<DepartmentOption>> GetDepartmentsAsync() =>
        await db.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentOption { DepartmentId = d.DepartmentId, Name = d.Name })
            .ToListAsync();

    public async Task<IReadOnlyList<RoleOption>> GetRolesAsync() =>
        await db.Roles
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.DisplayName)
            .Select(r => new RoleOption
            {
                RoleId = r.RoleId,
                Code = r.Code,
                DisplayName = r.DisplayName
            })
            .ToListAsync();

    public async Task<IReadOnlyList<EmployeeOption>> GetLeadOptionsAsync()
    {
        return await db.Database.SqlQueryRaw<EmployeeOption>("""
            SELECT
                e.EmployeeId AS EmployeeId,
                ISNULL(e.EmpCode, N'') + N' - ' + ISNULL(e.FirstName, N'')
                    + CASE WHEN e.LastName IS NULL OR LTRIM(RTRIM(e.LastName)) = N'' THEN N'' ELSE N' ' + e.LastName END
                    AS Display
            FROM dbo.Employees e
            INNER JOIN dbo.Roles r ON r.RoleId = e.RoleId
            WHERE ISNULL(e.IsActive, 1) = 1
              AND r.Code IN ({0}, {1}, {2}, {3}, {4})
            ORDER BY e.FirstName
            """,
            AttendanceRoles.TeamLead,
            AttendanceRoles.HR,
            AttendanceRoles.Head,
            AttendanceRoles.Admin,
            AttendanceRoles.SystemAdministrator).ToListAsync();
    }

    public async Task<(bool Ok, string Message, int EmployeeId)> CreateEmployeeAsync(
        EmployeeEditViewModel model,
        int? createdBy)
    {
        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
        {
            return (false, "Password is required and must be at least 6 characters.", 0);
        }

        if (string.IsNullOrWhiteSpace(model.Department))
        {
            return (false, "Department is required to generate employee code.", 0);
        }

        var roleId = await db.Roles.AsNoTracking()
            .Where(r => r.IsActive && r.RoleId == model.RoleId)
            .Select(r => (int?)r.RoleId)
            .FirstOrDefaultAsync();
        if (roleId is null)
        {
            return (false, "Invalid role selected.", 0);
        }

        var empCode = await GenerateNextEmpCodeAsync(model.Department);
        if (string.IsNullOrWhiteSpace(empCode))
        {
            return (false, "Could not generate employee code for this department.", 0);
        }

        if (await db.Employees.AnyAsync(e => e.EmpCode == empCode))
        {
            return (false, "Employee code already exists. Try again.", 0);
        }

        if (await db.Employees.AnyAsync(e => e.Username == model.Username.Trim()))
        {
            return (false, "Username already exists.", 0);
        }

        var employee = new Employee
        {
            EmpCode = empCode,
            FirstName = model.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim(),
            Email = model.Email.Trim(),
            Phone = model.Phone?.Trim(),
            Department = model.Department.Trim(),
            Designation = model.Designation?.Trim(),
            Username = model.Username.Trim(),
            PasswordHash = AttendancePasswordHasher.Hash(model.Password),
            RoleId = roleId.Value,
            TeamId = model.TeamId,
            ReportingLeadId = model.ReportingLeadId,
            CrmUserId = model.CrmUserId,
            JoinDate = model.JoinDate.Date,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedByEmployeeId = createdBy
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return (true, $"Employee created successfully ({empCode}).", employee.EmployeeId);
    }

    public async Task<string?> GenerateNextEmpCodeAsync(string departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            return null;
        }

        var prefix = BuildDepartmentPrefix(departmentName.Trim());
        if (string.IsNullOrEmpty(prefix))
        {
            return null;
        }

        var existing = await db.Employees.AsNoTracking()
            .Where(e => e.EmpCode.StartsWith(prefix))
            .Select(e => e.EmpCode)
            .ToListAsync();

        var max = 0;
        foreach (var code in existing)
        {
            if (code.Length <= prefix.Length)
            {
                continue;
            }

            var suffix = code[prefix.Length..];
            if (suffix.All(char.IsDigit) && int.TryParse(suffix, out var n) && n > max)
            {
                max = n;
            }
        }

        return $"{prefix}{(max + 1):D3}";
    }

    internal static string BuildDepartmentPrefix(string departmentName)
    {
        var key = departmentName.Trim().ToLowerInvariant();
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["administration"] = "ADM",
            ["operations"] = "OPS",
            ["sales"] = "SAL",
            ["human resources"] = "HR",
            ["hr"] = "HR",
            ["it"] = "IT",
            ["information technology"] = "IT"
        };

        if (known.TryGetValue(key, out var mapped))
        {
            return mapped;
        }

        var words = departmentName.Trim()
            .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

        if (words.Length >= 2)
        {
            return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        }

        var word = new string(words[0].Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (word.Length == 0)
        {
            return "EMP";
        }

        return word.Length <= 3 ? word : word[..3];
    }

    public async Task<(bool Ok, string Message)> UpdateEmployeeAsync(
        EmployeeEditViewModel model)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == model.EmployeeId);
        if (employee is null)
        {
            return (false, "Employee not found.");
        }

        var roleId = await db.Roles.AsNoTracking()
            .Where(r => r.IsActive && r.RoleId == model.RoleId)
            .Select(r => (int?)r.RoleId)
            .FirstOrDefaultAsync();
        if (roleId is null)
        {
            return (false, "Invalid role selected.");
        }

        if (await db.Employees.AnyAsync(e => e.EmpCode == model.EmpCode.Trim() && e.EmployeeId != model.EmployeeId))
        {
            return (false, "Employee code already exists.");
        }

        if (await db.Employees.AnyAsync(e => e.Username == model.Username.Trim() && e.EmployeeId != model.EmployeeId))
        {
            return (false, "Username already exists.");
        }

        employee.EmpCode = model.EmpCode.Trim();
        employee.FirstName = model.FirstName.Trim();
        employee.LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim();
        employee.Email = model.Email.Trim();
        employee.Phone = model.Phone?.Trim();
        employee.Department = model.Department?.Trim();
        employee.Designation = model.Designation?.Trim();
        employee.Username = model.Username.Trim();
        employee.RoleId = roleId.Value;
        employee.TeamId = model.TeamId;
        employee.ReportingLeadId = model.ReportingLeadId;
        employee.CrmUserId = model.CrmUserId;
        employee.JoinDate = model.JoinDate.Date;
        employee.IsActive = model.IsActive;

        // Password changes go through Change Password (SysAdmin: anyone; others: own only).

        await db.SaveChangesAsync();
        return (true, "Employee updated successfully.");
    }

    public async Task<(bool Ok, string Message)> DeleteEmployeeAsync(int employeeId, int actorEmployeeId)
    {
        if (employeeId == actorEmployeeId)
        {
            return (false, "You cannot delete your own account.");
        }

        var employee = await db.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return (false, "Employee not found.");
        }

        // Keep at least one System Administrator
        if (string.Equals(employee.Role?.Code, AttendanceRoles.SystemAdministrator, StringComparison.OrdinalIgnoreCase))
        {
            var otherSysAdmins = await db.Employees.CountAsync(e =>
                e.EmployeeId != employeeId &&
                e.Role != null &&
                e.Role.Code == AttendanceRoles.SystemAdministrator);
            if (otherSysAdmins == 0)
            {
                return (false, "Cannot delete the only System Administrator.");
            }
        }

        var code = employee.EmpCode ?? employeeId.ToString();
        var name = string.IsNullOrWhiteSpace(employee.LastName)
            ? (employee.FirstName ?? code)
            : $"{employee.FirstName} {employee.LastName}";

        // Clear pointers that block delete
        await db.Employees
            .Where(e => e.ReportingLeadId == employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ReportingLeadId, (int?)null));

        await db.Employees
            .Where(e => e.CreatedByEmployeeId == employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedByEmployeeId, (int?)null));

        await db.Teams
            .Where(t => t.LeadEmployeeId == employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LeadEmployeeId, (int?)null));

        await db.DailyAttendances
            .Where(a => a.MarkedByEmployeeId == employeeId && a.EmployeeId != employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.MarkedByEmployeeId, actorEmployeeId));

        await db.MonthlyAttendances
            .Where(m => m.CalculatedByEmployeeId == employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CalculatedByEmployeeId, (int?)null));

        await db.AllowedIpAddresses
            .Where(ip => ip.CreatedByEmployeeId == employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(ip => ip.CreatedByEmployeeId, (int?)null));

        // Own daily/monthly/schedule/roster rows cascade with this delete
        db.Employees.Remove(employee);
        await db.SaveChangesAsync();

        return (true, $"Deleted {code} — {name}.");
    }

    public async Task<(bool Ok, string Message)> ChangePasswordAsync(
        int employeeId,
        string newPassword,
        string? currentPassword,
        bool requireCurrentPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return (false, "New password must be at least 6 characters.");
        }

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return (false, "Employee not found.");
        }

        if (!employee.IsActive)
        {
            return (false, "Cannot change password for an inactive employee.");
        }

        if (requireCurrentPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) ||
                !AttendancePasswordHasher.Verify(currentPassword, employee.PasswordHash))
            {
                return (false, "Current password is incorrect.");
            }
        }

        employee.PasswordHash = AttendancePasswordHasher.Hash(newPassword);
        await db.SaveChangesAsync();
        return (true, "Password changed successfully.");
    }

    public async Task<(bool Ok, string Message)> CreateTeamAsync(
        string name,
        int? leadEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Team name is required.");
        }

        var trimmed = name.Trim();
        if (await db.Teams.AnyAsync(t => t.Name == trimmed))
        {
            return (false, "A team with this name already exists.");
        }

        db.Teams.Add(new Team
        {
            Name = trimmed,
            LeadEmployeeId = leadEmployeeId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (true, "Team created successfully.");
    }

    public async Task<(bool Ok, string Message)> CreateDesignationAsync(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Designation name is required.");
        }

        var trimmed = name.Trim();
        if (await db.Designations.AnyAsync(d => d.Name == trimmed))
        {
            return (false, "This designation already exists.");
        }

        db.Designations.Add(new Designation
        {
            Name = trimmed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (true, "Designation created successfully.");
    }

    public async Task<(bool Ok, string Message)> CreateDepartmentAsync(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Department name is required.");
        }

        var trimmed = name.Trim();
        if (await db.Departments.AnyAsync(d => d.Name == trimmed))
        {
            return (false, "This department already exists.");
        }

        db.Departments.Add(new Department
        {
            Name = trimmed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (true, "Department created successfully.");
    }

    public async Task<IReadOnlyList<HolidayListItem>> GetHolidaysAsync(
        int? year = null)
    {
        var y = year ?? DateTime.Today.Year;
        var from = new DateOnly(y, 1, 1);
        var to = new DateOnly(y, 12, 31);

        return await db.Holidays
            .AsNoTracking()
            .Where(h => h.HolidayDate >= from && h.HolidayDate <= to)
            .OrderBy(h => h.HolidayDate)
            .Select(h => new HolidayListItem
            {
                HolidayId = h.HolidayId,
                HolidayDate = h.HolidayDate,
                Name = h.Name,
                IsOptional = h.IsOptional
            })
            .ToListAsync();
    }

    public async Task<(bool Ok, string Message)> CreateHolidayAsync(
        DateOnly holidayDate,
        string name,
        bool isOptional)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Holiday name is required.");
        }

        var trimmed = name.Trim();
        if (await db.Holidays.AnyAsync(h => h.HolidayDate == holidayDate))
        {
            return (false, "A holiday already exists on this date.");
        }

        db.Holidays.Add(new Holiday
        {
            HolidayDate = holidayDate,
            Name = trimmed,
            IsOptional = isOptional
        });
        await db.SaveChangesAsync();

        var kind = isOptional ? "optional" : "working-day holiday";
        return (true, $"Holiday saved ({kind}). Re-run Close Month to refresh monthly totals.");
    }

    public async Task<(bool Ok, string Message)> DeleteHolidayAsync(
        int holidayId)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.HolidayId == holidayId);
        if (holiday is null)
        {
            return (false, "Holiday not found.");
        }

        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync();
        return (true, "Holiday removed. Re-run Close Month to refresh monthly totals.");
    }

    public async Task<DailyAttendance?> GetTodayAttendanceAsync(int employeeId)
    {
        var day = await ResolveWorkDayAsync(employeeId);
        return await db.DailyAttendances
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == day.AttendanceDate);
    }

    public async Task<(bool Ok, string Message)> ToggleOptInOutAsync(
        int employeeId,
        string? status = null)
    {
        var employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.IsActive);
        if (employee is null)
        {
            return (false, "Employee not found or inactive.");
        }

        var nowLocal = DateTime.Now;
        var work = await ResolveWorkDayAsync(employeeId, nowLocal);
        var today = work.AttendanceDate;

        if (await IsMonthLockedAsync(employeeId, today.Year, today.Month))
        {
            return (false, "This month is locked. Contact HR to make changes.");
        }

        if (work.IsWeekOff)
        {
            return (false, "Today is a week-off on your roster/schedule. Contact HR if you need to work.");
        }

        // If a shift is set, only allow punches inside Start → End (cutoff), including after midnight for night shifts.
        if (work.HasShift &&
            !AttendanceClock.IsWithinShiftWindow(nowLocal, today, work.ShiftStart!.Value, work.ShiftEnd!.Value))
        {
            var timing = AttendanceClock.FormatShift(work.ShiftStart.Value, work.ShiftEnd.Value);
            return (false, $"Outside your shift window ({work.ShiftName}: {timing}).");
        }

        var now = new TimeSpan(nowLocal.Hour, nowLocal.Minute, 0);

        var existing = await db.DailyAttendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == today);

        // Opt-in
        if (existing is null || !existing.OptInTime.HasValue)
        {
            var chosen = NormalizeSelfStatus(status) ?? DailyAttendanceStatus.Present;
            if (chosen == DailyAttendanceStatus.Leave)
            {
                return (false, "For Leave, use Mark Leave — no opt-in needed.");
            }

            if (existing is null)
            {
                db.DailyAttendances.Add(new DailyAttendance
                {
                    EmployeeId = employeeId,
                    AttendanceDate = today,
                    OptInTime = now,
                    OptOutTime = null,
                    Status = chosen,
                    WorkMinutes = null,
                    MarkedByEmployeeId = employeeId,
                    MarkedAt = DateTime.UtcNow,
                    Source = "Self"
                });
            }
            else
            {
                existing.OptInTime = now;
                existing.OptOutTime = null;
                existing.WorkMinutes = null;
                existing.Status = chosen;
                existing.MarkedByEmployeeId = employeeId;
                existing.MarkedAt = DateTime.UtcNow;
                existing.Source = "Self";
            }

            await db.SaveChangesAsync();
            var nightNote = work.HasShift && work.ShiftEnd < work.ShiftStart
                ? $" Attendance date is {today:dd MMM} (shift start day)."
                : "";
            return (true, $"Opted in as {LabelStatus(chosen)}.{nightNote} Click Opt-out when you finish.");
        }

        // Opt-out
        if (existing.OptOutTime.HasValue)
        {
            return (false, "You have already opted out for this attendance day.");
        }

        existing.OptOutTime = now;
        existing.WorkMinutes = AttendanceClock.CalcWorkMinutes(existing.OptInTime.Value, now);
        existing.MarkedByEmployeeId = employeeId;
        existing.MarkedAt = DateTime.UtcNow;
        existing.Source = "Self";

        await db.SaveChangesAsync();
        return (true, "Opted out. Attendance will use this for the month report.");
    }

    public async Task<(bool Ok, string Message)> MarkOwnStatusAsync(
        int employeeId,
        string status)
    {
        var employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.IsActive);
        if (employee is null)
        {
            return (false, "Employee not found or inactive.");
        }

        var work = await ResolveWorkDayAsync(employeeId);
        var today = work.AttendanceDate;

        if (await IsMonthLockedAsync(employeeId, today.Year, today.Month))
        {
            return (false, "This month is locked. Contact HR to make changes.");
        }

        var chosen = NormalizeSelfStatus(status);
        if (chosen is null)
        {
            return (false, "Invalid status. Choose Present, WFH, Half Day, or Leave.");
        }

        if (work.IsWeekOff && chosen != DailyAttendanceStatus.Leave)
        {
            return (false, "Today is a week-off. You can only mark Leave, or ask HR to change the roster.");
        }

        var existing = await db.DailyAttendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == today);

        // Once opted in, don't overwrite with a plain status change (use opt-out instead)
        if (existing?.OptInTime is not null && chosen != DailyAttendanceStatus.Leave)
        {
            return (false, "You already opted in today. Use Opt-out when you finish.");
        }

        if (chosen == DailyAttendanceStatus.Leave)
        {
            if (existing is null)
            {
                db.DailyAttendances.Add(new DailyAttendance
                {
                    EmployeeId = employeeId,
                    AttendanceDate = today,
                    Status = DailyAttendanceStatus.Leave,
                    OptInTime = null,
                    OptOutTime = null,
                    WorkMinutes = null,
                    MarkedByEmployeeId = employeeId,
                    MarkedAt = DateTime.UtcNow,
                    Source = "Self"
                });
            }
            else
            {
                existing.Status = DailyAttendanceStatus.Leave;
                existing.OptInTime = null;
                existing.OptOutTime = null;
                existing.WorkMinutes = null;
                existing.MarkedByEmployeeId = employeeId;
                existing.MarkedAt = DateTime.UtcNow;
                existing.Source = "Self";
            }

            await db.SaveChangesAsync();
            return (true, "Marked as Leave for today.");
        }

        // Pre-select status before opt-in (no punch yet)
        if (existing is null)
        {
            db.DailyAttendances.Add(new DailyAttendance
            {
                EmployeeId = employeeId,
                AttendanceDate = today,
                Status = chosen,
                MarkedByEmployeeId = employeeId,
                MarkedAt = DateTime.UtcNow,
                Source = "Self"
            });
        }
        else
        {
            existing.Status = chosen;
            existing.MarkedByEmployeeId = employeeId;
            existing.MarkedAt = DateTime.UtcNow;
            existing.Source = "Self";
        }

        await db.SaveChangesAsync();
        return (true, $"Status set to {LabelStatus(chosen)}. Click Opt-in when you start.");
    }

    public async Task<(bool Ok, string Message)> MarkAttendanceAsync(
        MarkAttendanceViewModel model,
        int actorEmployeeId,
        bool isSelf)
    {
        var employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == model.EmployeeId && e.IsActive);
        if (employee is null)
        {
            return (false, "Employee not found or inactive.");
        }

        if (model.AttendanceDate > DateOnly.FromDateTime(DateTime.Today))
        {
            return (false, "Cannot mark attendance for a future date.");
        }

        // Employees use Opt-in / Opt-out; Mark is for HR/Admin status overrides
        if (isSelf)
        {
            return (false, "Use Opt-in / Opt-out to mark your attendance.");
        }

        if (await IsMonthLockedAsync(model.EmployeeId, model.AttendanceDate.Year, model.AttendanceDate.Month))
        {
            return (false, "This month is locked. Contact HR to make changes.");
        }

        var normalizedStatus = DailyAttendanceStatus.All.FirstOrDefault(s =>
            string.Equals(s, model.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalizedStatus is null)
        {
            return (false, "Invalid attendance status.");
        }

        model.Status = normalizedStatus;

        var existing = await db.DailyAttendances
            .FirstOrDefaultAsync(a => a.EmployeeId == model.EmployeeId && a.AttendanceDate == model.AttendanceDate);

        if (existing is null)
        {
            db.DailyAttendances.Add(new DailyAttendance
            {
                EmployeeId = model.EmployeeId,
                AttendanceDate = model.AttendanceDate,
                OptInTime = null,
                OptOutTime = null,
                Status = model.Status,
                WorkMinutes = null,
                Remarks = model.Remarks?.Trim(),
                MarkedByEmployeeId = actorEmployeeId,
                MarkedAt = DateTime.UtcNow,
                Source = "HR"
            });
        }
        else
        {
            existing.Status = model.Status;
            existing.Remarks = model.Remarks?.Trim();
            existing.MarkedByEmployeeId = actorEmployeeId;
            existing.MarkedAt = DateTime.UtcNow;
            existing.Source = "HR";
            // Keep existing OptIn/OptOut times so HR calculation still has punch data
        }

        await db.SaveChangesAsync();
        return (true, "Attendance saved.");
    }

    public async Task<bool> IsMonthLockedAsync(int employeeId, int year, int month)
    {
        var row = await db.MonthlyAttendances.AsNoTracking()
            .FirstOrDefaultAsync(m => m.EmployeeId == employeeId && m.Year == year && m.Month == month);
        return row?.Status == MonthlyAttendanceStatus.Locked;
    }

    public async Task<AttendanceMonthViewModel> GetMonthViewAsync(
        int employeeId,
        int year,
        int month)
    {
        var employee = await GetEmployeeAsync(employeeId)
            ?? throw new InvalidOperationException("Employee not found.");

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, daysInMonth);

        var records = await db.DailyAttendances.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.AttendanceDate >= from && a.AttendanceDate <= to)
            .ToListAsync();

        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.HolidayDate >= from && h.HolidayDate <= to)
            .Select(h => h.HolidayDate)
            .ToListAsync();
        var holidaySet = holidays.ToHashSet();

        var byDate = records.ToDictionary(r => r.AttendanceDate);
        var weekOffDates = await GetWeekOffDatesAsync(employeeId, from, to);
        var currentAttDate = (await ResolveWorkDayAsync(employeeId)).AttendanceDate;
        var cells = new List<DayAttendanceCell>();

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            byDate.TryGetValue(date, out var rec);
            var effective = ResolveEffectiveStatus(rec, currentAttDate);

            cells.Add(new DayAttendanceCell
            {
                Date = date,
                Status = effective,
                OptIn = rec?.OptInTime,
                OptOut = rec?.OptOutTime,
                WorkMinutes = rec?.WorkMinutes,
                Remarks = rec?.Remarks,
                IsWeekend = weekOffDates.Contains(date),
                IsHoliday = holidaySet.Contains(date)
            });
        }

        var summary = await db.MonthlyAttendances.AsNoTracking()
            .FirstOrDefaultAsync(m => m.EmployeeId == employeeId && m.Year == year && m.Month == month);

        var locked = summary?.Status == MonthlyAttendanceStatus.Locked;

        return new AttendanceMonthViewModel
        {
            EmployeeId = employeeId,
            EmployeeName = employee.FullName,
            EmpCode = employee.EmpCode,
            Year = year,
            Month = month,
            Summary = summary,
            Days = cells,
            CanEdit = !locked
        };
    }

    public async Task<IReadOnlyList<EmployeeOption>> GetEmployeeFilterOptionsAsync(string? viewerRole)
    {
        // Small projection for Monthly dropdown — not the full employee list page query.
        var q = ApplyMonthlyVisibleRoles(db.Employees.AsNoTracking(), viewerRole);

        return await q
            .OrderBy(e => e.EmpCode)
            .Select(e => new EmployeeOption
            {
                EmployeeId = e.EmployeeId,
                Display = (e.EmpCode ?? "") + " - " + (e.FirstName ?? "")
                    + (e.LastName == null || e.LastName == "" ? "" : " " + e.LastName)
            })
            .ToListAsync();
    }

    /// <summary>
    /// Monthly Report who is visible:
    /// - SystemAdministrator → all roles (Admin, HR, staff, and themselves)
    /// - Admin / HR / everyone else → all except SystemAdministrator (includes themselves)
    /// </summary>
    private static IQueryable<Employee> ApplyMonthlyVisibleRoles(
        IQueryable<Employee> query,
        string? viewerRole)
    {
        query = query.Where(e => e.IsActive && e.Role != null);

        var isSysAdmin = string.Equals(
            viewerRole,
            AttendanceRoles.SystemAdministrator,
            StringComparison.OrdinalIgnoreCase);

        if (!isSysAdmin)
        {
            query = query.Where(e => e.Role!.Code != AttendanceRoles.SystemAdministrator);
        }

        return query;
    }

    public async Task<IReadOnlyList<TeamAttendanceRow>> GetTeamAttendanceAsync(
        int leadEmployeeId,
        int? teamId,
        DateOnly date,
        bool canViewAll,
        string? search = null)
    {
        // Two queries total: employees + that day's punches.
        var empQuery = db.Employees.AsNoTracking()
            .Where(e => e.IsActive
                        && e.Role != null
                        && e.Role.Code != AttendanceRoles.Admin
                        && e.Role.Code != AttendanceRoles.SystemAdministrator);

        if (!canViewAll)
        {
            empQuery = empQuery.Where(e =>
                e.ReportingLeadId == leadEmployeeId ||
                (e.Team != null && e.Team.LeadEmployeeId == leadEmployeeId) ||
                e.EmployeeId == leadEmployeeId);
        }

        if (teamId.HasValue)
        {
            empQuery = empQuery.Where(e => e.TeamId == teamId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            empQuery = empQuery.Where(e =>
                (e.EmpCode != null && e.EmpCode.Contains(q)) ||
                (e.FirstName != null && e.FirstName.Contains(q)) ||
                (e.LastName != null && e.LastName.Contains(q)));
        }

        var list = await empQuery
            .OrderBy(e => e.EmpCode)
            .Select(e => new
            {
                e.EmployeeId,
                EmpCode = e.EmpCode ?? "",
                FullName = e.LastName == null || e.LastName == ""
                    ? (e.FirstName ?? "")
                    : (e.FirstName ?? "") + " " + e.LastName,
                TeamName = e.Team != null ? e.Team.Name : null
            })
            .ToListAsync();

        if (list.Count == 0)
        {
            return [];
        }

        var ids = list.Select(e => e.EmployeeId).ToList();
        var attendance = await db.DailyAttendances.AsNoTracking()
            .Where(a => a.AttendanceDate == date && ids.Contains(a.EmployeeId))
            .Select(a => new { a.EmployeeId, a.Status, a.OptInTime, a.OptOutTime })
            .ToListAsync();
        var byEmp = attendance.ToDictionary(a => a.EmployeeId);

        return list.Select(e =>
        {
            byEmp.TryGetValue(e.EmployeeId, out var a);
            return new TeamAttendanceRow
            {
                EmployeeId = e.EmployeeId,
                EmpCode = e.EmpCode,
                FullName = e.FullName,
                TeamName = e.TeamName,
                Status = a?.Status,
                OptIn = a?.OptInTime,
                OptOut = a?.OptOutTime
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<MonthlySummaryRow>> GetMonthlySummariesAsync(
        int year,
        int month,
        int? filterEmployeeId,
        int? viewerEmployeeId,
        bool canViewAll,
        bool canViewTeam,
        string? viewerRole,
        string? search = null)
    {
        // ---- 1) Employees (one query) ----
        // SysAdmin sees everyone (including Admin/HR/SysAdmin). Others never see SysAdmin.
        var employeeQuery = ApplyMonthlyVisibleRoles(db.Employees.AsNoTracking(), viewerRole);

        if (filterEmployeeId.HasValue)
        {
            employeeQuery = employeeQuery.Where(e => e.EmployeeId == filterEmployeeId);
        }
        else if (!canViewAll && canViewTeam && viewerEmployeeId.HasValue)
        {
            var leadId = viewerEmployeeId.Value;
            employeeQuery = employeeQuery.Where(e =>
                e.ReportingLeadId == leadId ||
                (e.Team != null && e.Team.LeadEmployeeId == leadId) ||
                e.EmployeeId == leadId);
        }
        else if (!canViewAll && viewerEmployeeId.HasValue)
        {
            employeeQuery = employeeQuery.Where(e => e.EmployeeId == viewerEmployeeId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            employeeQuery = employeeQuery.Where(e =>
                (e.EmpCode != null && e.EmpCode.Contains(q)) ||
                (e.FirstName != null && e.FirstName.Contains(q)) ||
                (e.LastName != null && e.LastName.Contains(q)));
        }

        var employees = await employeeQuery
            .OrderBy(e => e.EmpCode)
            .Select(e => new
            {
                e.EmployeeId,
                EmpCode = e.EmpCode ?? "",
                FullName = e.LastName == null || e.LastName == ""
                    ? (e.FirstName ?? "")
                    : (e.FirstName ?? "") + " " + e.LastName,
                TeamName = e.Team != null ? e.Team.Name : null,
                e.TeamId
            })
            .ToListAsync();

        if (employees.Count == 0)
        {
            return [];
        }

        var ids = employees.Select(e => e.EmployeeId).ToList();
        var teamIds = employees.Where(e => e.TeamId.HasValue).Select(e => e.TeamId!.Value).Distinct().ToList();
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, daysInMonth);
        var now = DateTime.Now;

        // ---- 2) Batch load everything else (no per-employee DB calls) ----
        var allDaily = await db.DailyAttendances.AsNoTracking()
            .Where(a => ids.Contains(a.EmployeeId) && a.AttendanceDate >= from && a.AttendanceDate <= to)
            .ToListAsync();
        var dailyByEmp = allDaily
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.AttendanceDate));

        var monthlyRows = await db.MonthlyAttendances.AsNoTracking()
            .Where(m => ids.Contains(m.EmployeeId) && m.Year == year && m.Month == month)
            .ToDictionaryAsync(m => m.EmployeeId);

        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.HolidayDate >= from && h.HolidayDate <= to && !h.IsOptional)
            .Select(h => h.HolidayDate)
            .ToListAsync();
        var holidaySet = holidays.ToHashSet();

        var empSchedules = await db.EmployeeSchedules.AsNoTracking()
            .Where(s => ids.Contains(s.EmployeeId))
            .ToDictionaryAsync(s => s.EmployeeId);

        var teamSchedules = teamIds.Count == 0
            ? new Dictionary<int, TeamSchedule>()
            : await db.TeamSchedules.AsNoTracking()
                .Where(s => teamIds.Contains(s.TeamId))
                .ToDictionaryAsync(s => s.TeamId);

        var shiftIds = empSchedules.Values.Select(s => s.ShiftId)
            .Concat(teamSchedules.Values.Select(s => s.ShiftId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var shifts = shiftIds.Count == 0
            ? new Dictionary<int, ShiftTemplate>()
            : await db.ShiftTemplates.AsNoTracking()
                .Where(s => shiftIds.Contains(s.ShiftId))
                .ToDictionaryAsync(s => s.ShiftId);

        var rosterAll = await db.RosterEntries.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.RosterDate >= from && r.RosterDate <= to)
            .ToListAsync();
        var rosterByEmp = rosterAll.ToLookup(r => r.EmployeeId);

        // ---- 3) Build rows in memory ----
        var rows = new List<MonthlySummaryRow>(employees.Count);
        foreach (var emp in employees)
        {
            dailyByEmp.TryGetValue(emp.EmployeeId, out var byDate);
            byDate ??= new Dictionary<DateOnly, DailyAttendance>();
            monthlyRows.TryGetValue(emp.EmployeeId, out var stored);

            empSchedules.TryGetValue(emp.EmployeeId, out var empSched);
            TeamSchedule? teamSched = null;
            if (emp.TeamId is int tid)
            {
                teamSchedules.TryGetValue(tid, out teamSched);
            }

            var weekOffMask = empSched?.WeekOffMask ?? teamSched?.WeekOffMask ?? WeekOffDays.Weekend;
            int? shiftId = empSched?.ShiftId ?? teamSched?.ShiftId;
            ShiftTemplate? shift = shiftId is int sid && shifts.TryGetValue(sid, out var sh) ? sh : null;

            var weekOffDates = BuildWeekOffDates(from, to, weekOffMask, rosterByEmp[emp.EmployeeId]);
            var currentAttDate = AttendanceClock.GetAttendanceDate(now, shift?.StartTime, shift?.EndTime);
            var dayCells = BuildMonthDayCells(year, month, byDate, holidaySet, weekOffDates, currentAttDate);

            MonthStats calc;
            if (stored is not null && stored.Status == MonthlyAttendanceStatus.Locked)
            {
                calc = new MonthStats(
                    stored.WorkingDaysInMonth,
                    stored.PresentDays,
                    stored.AbsentDays,
                    stored.HalfDays,
                    stored.LeaveDays,
                    stored.WfhDays,
                    stored.HolidayDays,
                    stored.TotalWorkMinutes,
                    stored.AttendancePercent);
            }
            else
            {
                calc = ComputeMonthStatsFromRecords(
                    byDate.Values.ToList(), year, month, holidaySet, weekOffDates, currentAttDate);
            }

            rows.Add(new MonthlySummaryRow
            {
                EmployeeId = emp.EmployeeId,
                EmpCode = emp.EmpCode,
                FullName = emp.FullName,
                TeamName = emp.TeamName,
                PresentDays = calc.PresentDays,
                AbsentDays = calc.AbsentDays,
                HalfDays = calc.HalfDays,
                LeaveDays = calc.LeaveDays,
                WfhDays = calc.WfhDays,
                WorkingDaysInMonth = calc.WorkingDaysInMonth,
                TotalWorkMinutes = calc.TotalWorkMinutes,
                AttendancePercent = calc.AttendancePercent,
                Status = stored?.Status ?? MonthlyAttendanceStatus.Draft,
                Days = dayCells
            });
        }

        return rows;
    }

    public async Task<(bool Ok, string Message, int Count)> CalculateMonthAsync(
        int year,
        int month,
        int calculatedByEmployeeId,
        bool lockMonth)
    {
        if (month is < 1 or > 12)
        {
            return (false, "Invalid month.", 0);
        }

        var employees = await db.Employees
            .Where(e => e.IsActive
                        && e.Role != null
                        && e.Role.Code != AttendanceRoles.Admin
                        && e.Role.Code != AttendanceRoles.SystemAdministrator)
            .ToListAsync();

        // Past days with opt-in but no opt-out become Half Day before totals are frozen
        await ApplyMissingOptOutsAsync(year, month);

        var count = 0;
        foreach (var emp in employees)
        {
            var stats = await ComputeMonthStatsAsync(emp.EmployeeId, year, month);

            var existing = await db.MonthlyAttendances
                .FirstOrDefaultAsync(m => m.EmployeeId == emp.EmployeeId && m.Year == year && m.Month == month);

            if (existing is null)
            {
                existing = new MonthlyAttendance
                {
                    EmployeeId = emp.EmployeeId,
                    Year = year,
                    Month = month
                };
                db.MonthlyAttendances.Add(existing);
            }

            existing.WorkingDaysInMonth = stats.WorkingDaysInMonth;
            existing.PresentDays = stats.PresentDays;
            existing.AbsentDays = stats.AbsentDays;
            existing.HalfDays = stats.HalfDays;
            existing.LeaveDays = stats.LeaveDays;
            existing.WfhDays = stats.WfhDays;
            existing.HolidayDays = stats.HolidayDays;
            existing.TotalWorkMinutes = stats.TotalWorkMinutes;
            existing.AttendancePercent = stats.AttendancePercent;
            existing.CalculatedAt = DateTime.UtcNow;
            existing.CalculatedByEmployeeId = calculatedByEmployeeId > 0 ? calculatedByEmployeeId : null;
            existing.Status = lockMonth ? MonthlyAttendanceStatus.Locked : MonthlyAttendanceStatus.Draft;
            count++;
        }

        await db.SaveChangesAsync();
        var action = lockMonth ? "calculated and locked" : "calculated and unlocked (Draft)";
        return (true, $"Month {month}/{year} {action} for {count} employee(s).", count);
    }

    public async Task<(bool Ran, string Message)> AutoLockPreviousMonthIfDueAsync(
        )
    {
        var today = DateTime.Today;
        var previous = today.AddMonths(-1);
        var year = previous.Year;
        var month = previous.Month;

        var employeeIds = await db.Employees.AsNoTracking()
            .Where(e => e.IsActive
                        && e.Role != null
                        && e.Role.Code != AttendanceRoles.Admin
                        && e.Role.Code != AttendanceRoles.SystemAdministrator)
            .Select(e => e.EmployeeId)
            .ToListAsync();

        if (employeeIds.Count == 0)
        {
            return (false, "No employees to lock.");
        }

        var locked = await db.MonthlyAttendances.AsNoTracking()
            .CountAsync(m =>
                m.Year == year &&
                m.Month == month &&
                m.Status == MonthlyAttendanceStatus.Locked &&
                employeeIds.Contains(m.EmployeeId));

        if (locked >= employeeIds.Count)
        {
            return (false, $"{previous:MMMM yyyy} is already locked.");
        }

        // System auto-lock (no HR user) — use 0 for CalculatedBy
        var result = await CalculateMonthAsync(year, month, calculatedByEmployeeId: 0, lockMonth: true);
        return (result.Ok, result.Ok
            ? $"Auto-locked {previous:MMMM yyyy} for {result.Count} employee(s)."
            : result.Message);
    }

    public async Task<(bool Ok, string Message)> SetEmployeeMonthLockAsync(
        int employeeId,
        int year,
        int month,
        bool lockMonth,
        int actorEmployeeId)
    {
        if (!AttendancePeriodHelper.IsAllowed(year, month))
        {
            return (false, "You can only lock/unlock the current month or the previous month.");
        }

        var employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.IsActive);
        if (employee is null)
        {
            return (false, "Employee not found.");
        }

        var stats = await ComputeMonthStatsAsync(employeeId, year, month);
        var existing = await db.MonthlyAttendances
            .FirstOrDefaultAsync(m => m.EmployeeId == employeeId && m.Year == year && m.Month == month);

        if (existing is null)
        {
            existing = new MonthlyAttendance
            {
                EmployeeId = employeeId,
                Year = year,
                Month = month
            };
            db.MonthlyAttendances.Add(existing);
        }

        existing.WorkingDaysInMonth = stats.WorkingDaysInMonth;
        existing.PresentDays = stats.PresentDays;
        existing.AbsentDays = stats.AbsentDays;
        existing.HalfDays = stats.HalfDays;
        existing.LeaveDays = stats.LeaveDays;
        existing.WfhDays = stats.WfhDays;
        existing.HolidayDays = stats.HolidayDays;
        existing.TotalWorkMinutes = stats.TotalWorkMinutes;
        existing.AttendancePercent = stats.AttendancePercent;
        existing.CalculatedAt = DateTime.UtcNow;
        existing.CalculatedByEmployeeId = actorEmployeeId;
        existing.Status = lockMonth ? MonthlyAttendanceStatus.Locked : MonthlyAttendanceStatus.Draft;

        await db.SaveChangesAsync();

        var name = employee.FullName;
        return lockMonth
            ? (true, $"Attendance locked for {name} ({month}/{year}).")
            : (true, $"Attendance unlocked for {name} ({month}/{year}). Employee can edit again.");
    }

    public async Task<bool> CanViewerAccessEmployeeAsync(
        int viewerEmployeeId,
        string? viewerRole,
        int targetEmployeeId)
    {
        if (viewerEmployeeId == targetEmployeeId)
        {
            return true;
        }

        if (viewerRole is AttendanceRoles.SystemAdministrator
            or AttendanceRoles.Admin
            or AttendanceRoles.HR
            or AttendanceRoles.Head)
        {
            return true;
        }

        if (viewerRole != AttendanceRoles.TeamLead)
        {
            return false;
        }

        var target = await db.Employees.AsNoTracking()
            .Include(e => e.Team)
            .FirstOrDefaultAsync(e => e.EmployeeId == targetEmployeeId);

        if (target is null)
        {
            return false;
        }

        return target.ReportingLeadId == viewerEmployeeId
               || target.Team?.LeadEmployeeId == viewerEmployeeId;
    }

    private async Task ApplyMissingOptOutsAsync(int year, int month)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        if (to >= today)
        {
            to = today.AddDays(-1);
        }

        if (to < from)
        {
            return;
        }

        var open = await db.DailyAttendances
            .Where(a =>
                a.AttendanceDate >= from &&
                a.AttendanceDate <= to &&
                a.OptInTime != null &&
                a.OptOutTime == null &&
                (a.Status == DailyAttendanceStatus.Present || a.Status == DailyAttendanceStatus.Wfh))
            .ToListAsync();

        var changed = false;
        foreach (var row in open)
        {
            // Night shifts: don't half-day while the same attendance day is still open after midnight.
            var current = (await ResolveWorkDayAsync(row.EmployeeId)).AttendanceDate;
            if (row.AttendanceDate >= current)
            {
                continue;
            }

            row.Status = DailyAttendanceStatus.HalfDay;
            row.Remarks = string.IsNullOrWhiteSpace(row.Remarks)
                ? "Auto: missed opt-out"
                : row.Remarks;
            row.MarkedAt = DateTime.UtcNow;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private async Task<MonthStats> ComputeMonthStatsAsync(int employeeId, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, daysInMonth);

        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.HolidayDate >= from && h.HolidayDate <= to && !h.IsOptional)
            .Select(h => h.HolidayDate)
            .ToListAsync();
        var holidaySet = holidays.ToHashSet();
        var weekOffDates = await GetWeekOffDatesAsync(employeeId, from, to);
        var currentAttDate = (await ResolveWorkDayAsync(employeeId)).AttendanceDate;

        var records = await db.DailyAttendances.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.AttendanceDate >= from && a.AttendanceDate <= to)
            .ToListAsync();

        return ComputeMonthStatsFromRecords(records, year, month, holidaySet, weekOffDates, currentAttDate);
    }

    private static MonthStats ComputeMonthStatsFromRecords(
        IReadOnlyList<DailyAttendance> records,
        int year,
        int month,
        HashSet<DateOnly> holidaySet,
        HashSet<DateOnly> weekOffDates,
        DateOnly currentAttendanceDate)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var workingDays = 0;
        for (var d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(year, month, d);
            if (weekOffDates.Contains(date) || holidaySet.Contains(date))
            {
                continue;
            }

            workingDays++;
        }

        var effective = records
            .Select(r => (Rec: r, Status: ResolveEffectiveStatus(r, currentAttendanceDate)))
            .Where(x => !string.IsNullOrEmpty(x.Status))
            .ToList();

        var officePresent = effective.Count(x => x.Status == DailyAttendanceStatus.Present);
        var half = effective.Count(x => x.Status == DailyAttendanceStatus.HalfDay);
        var leave = effective.Count(x => x.Status == DailyAttendanceStatus.Leave);
        var wfh = effective.Count(x => x.Status == DailyAttendanceStatus.Wfh);
        var holidayMarked = effective.Count(x => x.Status == DailyAttendanceStatus.Holiday);
        var absentMarked = effective.Count(x => x.Status == DailyAttendanceStatus.Absent);

        var presentDays = officePresent + wfh;

        var countable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DailyAttendanceStatus.Present,
            DailyAttendanceStatus.Wfh,
            DailyAttendanceStatus.HalfDay,
            DailyAttendanceStatus.Leave,
            DailyAttendanceStatus.Absent,
            DailyAttendanceStatus.Holiday
        };

        var markedWorking = effective.Count(x =>
            !holidaySet.Contains(x.Rec.AttendanceDate) &&
            !weekOffDates.Contains(x.Rec.AttendanceDate) &&
            countable.Contains(x.Status!));

        var absent = absentMarked + Math.Max(0, workingDays - markedWorking);
        var credited = presentDays + (half * 0.5m);
        var percent = workingDays == 0 ? 0 : Math.Round(credited / workingDays * 100m, 2);
        var totalMinutes = records.Sum(r => r.WorkMinutes ?? 0);

        return new MonthStats(
            workingDays,
            presentDays,
            absent,
            half,
            leave,
            wfh,
            holidayMarked + holidaySet.Count,
            totalMinutes,
            percent);
    }

    private static List<MonthlyDayCell> BuildMonthDayCells(
        int year,
        int month,
        IReadOnlyDictionary<DateOnly, DailyAttendance> byDate,
        HashSet<DateOnly> holidaySet,
        HashSet<DateOnly> weekOffDates,
        DateOnly currentAttendanceDate)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var cells = new List<MonthlyDayCell>(daysInMonth);

        for (var d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(year, month, d);
            byDate.TryGetValue(date, out var rec);
            var weekOff = weekOffDates.Contains(date);
            var holiday = holidaySet.Contains(date);
            var missingOptOut = rec is not null
                && rec.OptInTime.HasValue
                && !rec.OptOutTime.HasValue
                && date < currentAttendanceDate;

            cells.Add(new MonthlyDayCell
            {
                Day = d,
                Status = ResolveEffectiveStatus(rec, currentAttendanceDate),
                IsWeekend = weekOff,
                IsHoliday = holiday,
                MissingOptOut = missingOptOut
            });
        }

        return cells;
    }

    /// <summary>
    /// Past attendance day opted in but never opted out → Half Day.
    /// Current attendance day (night shift after midnight) stays open.
    /// </summary>
    internal static string? ResolveEffectiveStatus(DailyAttendance? rec, DateOnly currentAttendanceDate)
    {
        if (rec is null || string.IsNullOrWhiteSpace(rec.Status))
        {
            return null;
        }

        var status = rec.Status.Trim();
        var forgotOptOut = rec.AttendanceDate < currentAttendanceDate
            && rec.OptInTime.HasValue
            && !rec.OptOutTime.HasValue
            && (string.Equals(status, DailyAttendanceStatus.Present, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, DailyAttendanceStatus.Wfh, StringComparison.OrdinalIgnoreCase));

        return forgotOptOut ? DailyAttendanceStatus.HalfDay : status;
    }

    private static string? NormalizeSelfStatus(string? status) =>
        DailyAttendanceStatus.SelfSelectable.FirstOrDefault(s =>
            string.Equals(s, status?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string LabelStatus(string status) => status switch
    {
        DailyAttendanceStatus.HalfDay => "Half Day",
        DailyAttendanceStatus.Wfh => "WFH",
        _ => status
    };

    private sealed record MonthStats(
        int WorkingDaysInMonth,
        int PresentDays,
        int AbsentDays,
        int HalfDays,
        int LeaveDays,
        int WfhDays,
        int HolidayDays,
        int TotalWorkMinutes,
        decimal AttendancePercent);
}
