using Microsoft.EntityFrameworkCore;
using RushMyBookings.Crm.Entities.Attendance;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.ViewModels.Attendance;

namespace RushMyBookings.Crm.Services;

/// <summary>
/// Shifts, team/employee week-offs, and future roster.
/// Kept in a separate file so the main attendance punches stay easy to read.
/// </summary>
public partial class AttendanceService
{
    public async Task<IReadOnlyList<ShiftListItem>> GetShiftsAsync(bool activeOnly = true)
    {
        var q = db.ShiftTemplates.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            q = q.Where(s => s.IsActive);
        }

        var list = await q.OrderBy(s => s.StartTime).ThenBy(s => s.Name).ToListAsync();
        return list.Select(s => new ShiftListItem
        {
            ShiftId = s.ShiftId,
            Name = s.Name,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            CrossesMidnight = s.CrossesMidnight,
            TimingLabel = AttendanceClock.FormatShift(s.StartTime, s.EndTime),
            IsActive = s.IsActive
        }).ToList();
    }

    public async Task<(bool Ok, string Message)> CreateShiftAsync(string name, TimeSpan start, TimeSpan end)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Shift name is required.");
        }

        var trimmed = name.Trim();
        if (await db.ShiftTemplates.AnyAsync(s => s.Name == trimmed))
        {
            return (false, "A shift with this name already exists.");
        }

        if (start == end)
        {
            return (false, "Start and end time cannot be the same.");
        }

        db.ShiftTemplates.Add(new ShiftTemplate
        {
            Name = trimmed,
            StartTime = start,
            EndTime = end,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var label = AttendanceClock.FormatShift(start, end);
        return (true, $"Shift \"{trimmed}\" saved ({label}).");
    }

    public async Task<(bool Ok, string Message)> SetShiftActiveAsync(int shiftId, bool isActive)
    {
        var shift = await db.ShiftTemplates.FirstOrDefaultAsync(s => s.ShiftId == shiftId);
        if (shift is null)
        {
            return (false, "Shift not found.");
        }

        shift.IsActive = isActive;
        await db.SaveChangesAsync();
        return (true, isActive ? "Shift activated." : "Shift deactivated.");
    }

    public async Task<ScheduleEditViewModel?> GetTeamScheduleEditAsync(int teamId)
    {
        var team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == teamId);
        if (team is null)
        {
            return null;
        }

        var row = await db.TeamSchedules.AsNoTracking().FirstOrDefaultAsync(s => s.TeamId == teamId);
        var mask = row?.WeekOffMask ?? WeekOffDays.Weekend;

        return new ScheduleEditViewModel
        {
            TeamId = teamId,
            Title = $"Team schedule — {team.Name}",
            ShiftId = row?.ShiftId,
            InheritShift = false,
            InheritWeekOff = false,
            OffMon = WeekOffDays.IsOff(mask, DayOfWeek.Monday),
            OffTue = WeekOffDays.IsOff(mask, DayOfWeek.Tuesday),
            OffWed = WeekOffDays.IsOff(mask, DayOfWeek.Wednesday),
            OffThu = WeekOffDays.IsOff(mask, DayOfWeek.Thursday),
            OffFri = WeekOffDays.IsOff(mask, DayOfWeek.Friday),
            OffSat = WeekOffDays.IsOff(mask, DayOfWeek.Saturday),
            OffSun = WeekOffDays.IsOff(mask, DayOfWeek.Sunday),
            Shifts = await GetShiftsAsync()
        };
    }

    public async Task<(bool Ok, string Message)> SaveTeamScheduleAsync(ScheduleEditViewModel model)
    {
        if (model.TeamId is null or <= 0)
        {
            return (false, "Team is required.");
        }

        if (!await db.Teams.AnyAsync(t => t.TeamId == model.TeamId))
        {
            return (false, "Team not found.");
        }

        var mask = WeekOffDays.FromFlags(
            model.OffMon, model.OffTue, model.OffWed, model.OffThu,
            model.OffFri, model.OffSat, model.OffSun);

        var row = await db.TeamSchedules.FirstOrDefaultAsync(s => s.TeamId == model.TeamId);
        if (row is null)
        {
            db.TeamSchedules.Add(new TeamSchedule
            {
                TeamId = model.TeamId.Value,
                ShiftId = model.ShiftId,
                WeekOffMask = mask
            });
        }
        else
        {
            row.ShiftId = model.ShiftId;
            row.WeekOffMask = mask;
        }

        await db.SaveChangesAsync();
        return (true, $"Team schedule saved. Week-offs: {WeekOffDays.Describe(mask)}.");
    }

    public async Task<ScheduleEditViewModel?> GetEmployeeScheduleEditAsync(int employeeId)
    {
        var emp = await GetEmployeeAsync(employeeId);
        if (emp is null)
        {
            return null;
        }

        var row = await db.EmployeeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId);

        var inheritWeekOff = row?.WeekOffMask is null;
        var mask = row?.WeekOffMask ?? WeekOffDays.Weekend;

        return new ScheduleEditViewModel
        {
            EmployeeId = employeeId,
            Title = $"Employee schedule — {emp.EmpCode} {emp.FullName}",
            ShiftId = row?.ShiftId,
            InheritShift = row?.ShiftId is null,
            InheritWeekOff = inheritWeekOff,
            OffMon = WeekOffDays.IsOff(mask, DayOfWeek.Monday),
            OffTue = WeekOffDays.IsOff(mask, DayOfWeek.Tuesday),
            OffWed = WeekOffDays.IsOff(mask, DayOfWeek.Wednesday),
            OffThu = WeekOffDays.IsOff(mask, DayOfWeek.Thursday),
            OffFri = WeekOffDays.IsOff(mask, DayOfWeek.Friday),
            OffSat = WeekOffDays.IsOff(mask, DayOfWeek.Saturday),
            OffSun = WeekOffDays.IsOff(mask, DayOfWeek.Sunday),
            Shifts = await GetShiftsAsync()
        };
    }

    public async Task<(bool Ok, string Message)> SaveEmployeeScheduleAsync(ScheduleEditViewModel model)
    {
        if (model.EmployeeId is null or <= 0)
        {
            return (false, "Employee is required.");
        }

        if (!await db.Employees.AnyAsync(e => e.EmployeeId == model.EmployeeId))
        {
            return (false, "Employee not found.");
        }

        byte? mask = model.InheritWeekOff
            ? null
            : WeekOffDays.FromFlags(
                model.OffMon, model.OffTue, model.OffWed, model.OffThu,
                model.OffFri, model.OffSat, model.OffSun);

        int? shiftId = model.InheritShift ? null : model.ShiftId;

        var row = await db.EmployeeSchedules.FirstOrDefaultAsync(s => s.EmployeeId == model.EmployeeId);
        if (row is null)
        {
            if (shiftId is null && mask is null)
            {
                return (true, "Using team defaults (nothing to save).");
            }

            db.EmployeeSchedules.Add(new EmployeeSchedule
            {
                EmployeeId = model.EmployeeId.Value,
                ShiftId = shiftId,
                WeekOffMask = mask
            });
        }
        else if (shiftId is null && mask is null)
        {
            db.EmployeeSchedules.Remove(row);
        }
        else
        {
            row.ShiftId = shiftId;
            row.WeekOffMask = mask;
        }

        await db.SaveChangesAsync();
        return (true, "Employee schedule saved.");
    }

    public async Task<ResolvedWorkDay> ResolveWorkDayAsync(int employeeId, DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        var emp = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

        var empSched = await db.EmployeeSchedules.AsNoTracking()
            .Include(s => s.Shift)
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId);

        TeamSchedule? teamSched = null;
        if (emp?.TeamId is int teamId)
        {
            teamSched = await db.TeamSchedules.AsNoTracking()
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.TeamId == teamId);
        }

        // Employee override wins; else team; else no shift (calendar day).
        ShiftTemplate? shift = null;
        if (empSched?.ShiftId is not null)
        {
            shift = empSched.Shift
                ?? await db.ShiftTemplates.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ShiftId == empSched.ShiftId);
        }
        else if (teamSched?.ShiftId is not null)
        {
            shift = teamSched.Shift
                ?? await db.ShiftTemplates.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ShiftId == teamSched.ShiftId);
        }

        var weekOffMask = empSched?.WeekOffMask
            ?? teamSched?.WeekOffMask
            ?? WeekOffDays.Weekend;

        var attendanceDate = AttendanceClock.GetAttendanceDate(
            now,
            shift?.StartTime,
            shift?.EndTime);

        // Roster one-off for that attendance date (future week-off / forced work).
        var roster = await db.RosterEntries.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.RosterDate == attendanceDate);

        bool isWeekOff;
        if (roster is not null)
        {
            isWeekOff = string.Equals(roster.EntryType, RosterEntryTypes.WeekOff, StringComparison.OrdinalIgnoreCase);
            if (roster.ShiftId is int rosterShiftId)
            {
                shift = await db.ShiftTemplates.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ShiftId == rosterShiftId)
                    ?? shift;
                attendanceDate = AttendanceClock.GetAttendanceDate(now, shift?.StartTime, shift?.EndTime);
            }
        }
        else
        {
            isWeekOff = WeekOffDays.IsOff(weekOffMask, attendanceDate.DayOfWeek);
        }

        return new ResolvedWorkDay
        {
            AttendanceDate = attendanceDate,
            ShiftName = shift?.Name,
            ShiftStart = shift?.StartTime,
            ShiftEnd = shift?.EndTime,
            IsWeekOff = isWeekOff,
            WeekOffMask = weekOffMask
        };
    }

    /// <summary>All week-off dates for one employee in a date range (schedule + roster).</summary>
    public async Task<HashSet<DateOnly>> GetWeekOffDatesAsync(int employeeId, DateOnly from, DateOnly to)
    {
        var result = new HashSet<DateOnly>();
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        var empSched = await db.EmployeeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId);

        TeamSchedule? teamSched = null;
        if (emp?.TeamId is int teamId)
        {
            teamSched = await db.TeamSchedules.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TeamId == teamId);
        }

        var mask = empSched?.WeekOffMask ?? teamSched?.WeekOffMask ?? WeekOffDays.Weekend;

        var roster = await db.RosterEntries.AsNoTracking()
            .Where(r => r.EmployeeId == employeeId && r.RosterDate >= from && r.RosterDate <= to)
            .ToListAsync();
        var rosterByDate = roster.ToDictionary(r => r.RosterDate);

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (rosterByDate.TryGetValue(d, out var entry))
            {
                if (string.Equals(entry.EntryType, RosterEntryTypes.WeekOff, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(d);
                }

                // Forced Work → not a week-off
                continue;
            }

            if (WeekOffDays.IsOff(mask, d.DayOfWeek))
            {
                result.Add(d);
            }
        }

        return result;
    }

    public async Task<RosterWeekViewModel> GetRosterWeekAsync(
        DateOnly weekStart,
        int? teamId,
        int viewerEmployeeId,
        bool canManageAll,
        bool teamLeadOnly)
    {
        // Monday → Sunday for the selected week
        var start = weekStart.AddDays(-(((int)weekStart.DayOfWeek + 6) % 7));
        var days = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();
        var end = days[^1];

        var teams = canManageAll
            ? await GetTeamsAsync()
            : await db.Teams.AsNoTracking()
                .Where(t => t.IsActive && t.LeadEmployeeId == viewerEmployeeId)
                .OrderBy(t => t.Name)
                .Select(t => new TeamOption { TeamId = t.TeamId, Name = t.Name })
                .ToListAsync();

        // ---- 1) Employees (one query) ----
        var empQuery = db.Employees.AsNoTracking()
            .Include(e => e.Team)
            .Include(e => e.Role)
            .Where(e => e.IsActive
                        && e.Role != null
                        && e.Role.Code != AttendanceRoles.Admin
                        && e.Role.Code != AttendanceRoles.SystemAdministrator);

        if (canManageAll)
        {
            if (teamId.HasValue)
            {
                empQuery = empQuery.Where(e => e.TeamId == teamId);
            }
        }
        else if (teamLeadOnly)
        {
            empQuery = empQuery.Where(e =>
                e.ReportingLeadId == viewerEmployeeId ||
                (e.Team != null && e.Team.LeadEmployeeId == viewerEmployeeId));
            if (teamId.HasValue)
            {
                empQuery = empQuery.Where(e => e.TeamId == teamId);
            }
        }
        else
        {
            empQuery = empQuery.Where(e => e.EmployeeId == viewerEmployeeId);
        }

        var employees = await empQuery.OrderBy(e => e.EmpCode).ToListAsync();
        if (employees.Count == 0)
        {
            return new RosterWeekViewModel
            {
                WeekStart = start,
                TeamId = teamId,
                Teams = teams,
                Days = days,
                Rows = [],
                CanEdit = canManageAll || teamLeadOnly
            };
        }

        var ids = employees.Select(e => e.EmployeeId).ToList();
        var teamIds = employees
            .Where(e => e.TeamId.HasValue)
            .Select(e => e.TeamId!.Value)
            .Distinct()
            .ToList();

        // ---- 2) Schedules + shifts + week roster (a few queries, not per employee) ----
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

        var roster = await db.RosterEntries.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.RosterDate >= start && r.RosterDate <= end)
            .ToListAsync();
        var rosterByEmp = roster.ToLookup(r => r.EmployeeId);

        // ---- 3) Build grid in memory ----
        var rows = new List<RosterEmployeeRow>(employees.Count);
        foreach (var emp in employees)
        {
            empSchedules.TryGetValue(emp.EmployeeId, out var empSched);
            TeamSchedule? teamSched = null;
            if (emp.TeamId is int tid)
            {
                teamSchedules.TryGetValue(tid, out teamSched);
            }

            // Employee shift/week-off override wins; else team; else weekend default
            int? shiftId = empSched?.ShiftId ?? teamSched?.ShiftId;
            ShiftTemplate? shift = shiftId is int sid && shifts.TryGetValue(sid, out var sh) ? sh : null;
            var weekOffMask = empSched?.WeekOffMask ?? teamSched?.WeekOffMask ?? WeekOffDays.Weekend;

            var empRoster = rosterByEmp[emp.EmployeeId].ToDictionary(r => r.RosterDate);
            var dayCells = new List<RosterDayCell>(7);
            foreach (var d in days)
            {
                empRoster.TryGetValue(d, out var entry);
                var forcedWork = entry is not null &&
                    string.Equals(entry.EntryType, RosterEntryTypes.Work, StringComparison.OrdinalIgnoreCase);
                var rosterOff = entry is not null &&
                    string.Equals(entry.EntryType, RosterEntryTypes.WeekOff, StringComparison.OrdinalIgnoreCase);

                bool isOff;
                if (entry is not null)
                {
                    isOff = rosterOff;
                }
                else
                {
                    isOff = WeekOffDays.IsOff(weekOffMask, d.DayOfWeek);
                }

                dayCells.Add(new RosterDayCell
                {
                    Date = d,
                    IsWeekOff = isOff,
                    FromRoster = rosterOff,
                    ForcedWork = forcedWork,
                    Label = forcedWork ? "Work" : (isOff ? "Off" : "—")
                });
            }

            rows.Add(new RosterEmployeeRow
            {
                EmployeeId = emp.EmployeeId,
                EmpCode = emp.EmpCode ?? "",
                FullName = string.IsNullOrWhiteSpace(emp.LastName)
                    ? (emp.FirstName ?? "")
                    : $"{emp.FirstName} {emp.LastName}",
                TeamName = emp.Team?.Name,
                ShiftLabel = shift is null
                    ? "No shift set"
                    : $"{shift.Name} ({AttendanceClock.FormatShift(shift.StartTime, shift.EndTime)})",
                DefaultWeekOffs = WeekOffDays.Describe(weekOffMask),
                Days = dayCells
            });
        }

        return new RosterWeekViewModel
        {
            WeekStart = start,
            TeamId = teamId,
            Teams = teams,
            Days = days,
            Rows = rows,
            CanEdit = canManageAll || teamLeadOnly
        };
    }

    public async Task<(bool Ok, string Message)> SetRosterWeekOffAsync(
        int employeeId,
        DateOnly date,
        bool isWeekOff,
        int actorEmployeeId)
    {
        // Don't require IsActive == true — NULL IsActive in SQL was blocking toggles.
        if (!await db.Employees.AnyAsync(e => e.EmployeeId == employeeId))
        {
            return (false, "Employee not found.");
        }

        var existing = await db.RosterEntries
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.RosterDate == date);

        // Decide against the default weekday pattern.
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        var empSched = await db.EmployeeSchedules.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId);
        TeamSchedule? teamSched = null;
        if (emp?.TeamId is int teamId)
        {
            teamSched = await db.TeamSchedules.AsNoTracking().FirstOrDefaultAsync(s => s.TeamId == teamId);
        }

        var mask = empSched?.WeekOffMask ?? teamSched?.WeekOffMask ?? WeekOffDays.Weekend;
        var defaultOff = WeekOffDays.IsOff(mask, date.DayOfWeek);

        if (isWeekOff)
        {
            if (defaultOff)
            {
                // Already off by schedule — remove any forced-work override
                if (existing is not null)
                {
                    db.RosterEntries.Remove(existing);
                    await db.SaveChangesAsync();
                }

                return (true, $"{date:ddd dd MMM} is already a week-off by schedule.");
            }

            if (existing is null)
            {
                db.RosterEntries.Add(new RosterEntry
                {
                    EmployeeId = employeeId,
                    RosterDate = date,
                    EntryType = RosterEntryTypes.WeekOff,
                    CreatedByEmployeeId = actorEmployeeId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.EntryType = RosterEntryTypes.WeekOff;
                existing.ShiftId = null;
                existing.CreatedByEmployeeId = actorEmployeeId;
                existing.CreatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return (true, $"Marked {date:ddd dd MMM} as week-off.");
        }

        // Turn week-off OFF → either remove WeekOff roster, or force Work if default is off
        if (defaultOff)
        {
            if (existing is null)
            {
                db.RosterEntries.Add(new RosterEntry
                {
                    EmployeeId = employeeId,
                    RosterDate = date,
                    EntryType = RosterEntryTypes.Work,
                    CreatedByEmployeeId = actorEmployeeId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.EntryType = RosterEntryTypes.Work;
                existing.CreatedByEmployeeId = actorEmployeeId;
                existing.CreatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return (true, $"Marked {date:ddd dd MMM} as working day (override).");
        }

        if (existing is not null)
        {
            db.RosterEntries.Remove(existing);
            await db.SaveChangesAsync();
        }

        return (true, $"Cleared roster override for {date:ddd dd MMM}.");
    }
}
