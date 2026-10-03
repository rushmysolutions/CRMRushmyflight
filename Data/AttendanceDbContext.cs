using Microsoft.EntityFrameworkCore;
using RushMyBookings.Crm.Entities.Attendance;

namespace RushMyBookings.Crm.Data;

public class AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DailyAttendance> DailyAttendances => Set<DailyAttendance>();
    public DbSet<MonthlyAttendance> MonthlyAttendances => Set<MonthlyAttendance>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<AllowedIpAddress> AllowedIpAddresses => Set<AllowedIpAddress>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ShiftTemplate> ShiftTemplates => Set<ShiftTemplate>();
    public DbSet<TeamSchedule> TeamSchedules => Set<TeamSchedule>();
    public DbSet<EmployeeSchedule> EmployeeSchedules => Set<EmployeeSchedule>();
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>(e =>
        {
            e.ToTable("Teams");
            e.HasKey(x => x.TeamId);
            e.Property(x => x.Name).IsRequired();
            e.HasOne(x => x.Lead)
                .WithMany()
                .HasForeignKey(x => x.LeadEmployeeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Designation>(e =>
        {
            e.ToTable("Designations");
            e.HasKey(x => x.DesignationId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(80);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Department>(e =>
        {
            e.ToTable("Departments");
            e.HasKey(x => x.DepartmentId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(80);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.ToTable("Employees");
            e.HasKey(x => x.EmployeeId);
            e.HasIndex(x => x.EmpCode).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email);
            e.Property(x => x.EmpCode).IsRequired();
            e.Property(x => x.FirstName).IsRequired();
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.Username).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.RoleId).IsRequired();

            e.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Team)
                .WithMany(t => t.Members)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.ReportingLead)
                .WithMany()
                .HasForeignKey(x => x.ReportingLeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<DailyAttendance>(e =>
        {
            e.ToTable("DailyAttendances");
            e.HasKey(x => x.AttendanceId);
            e.HasIndex(x => new { x.EmployeeId, x.AttendanceDate }).IsUnique();
            e.Property(x => x.Status).IsRequired();
            // Reuse existing columns so EnsureCreated databases keep working
            e.Property(x => x.OptInTime).HasColumnName("CheckInTime");
            e.Property(x => x.OptOutTime).HasColumnName("CheckOutTime");
            e.HasOne(x => x.Employee)
                .WithMany(emp => emp.DailyAttendances)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonthlyAttendance>(e =>
        {
            e.ToTable("MonthlyAttendances");
            e.HasKey(x => x.MonthlyId);
            e.HasIndex(x => new { x.EmployeeId, x.Year, x.Month }).IsUnique();
            e.Property(x => x.AttendancePercent).HasPrecision(5, 2);
            e.HasOne(x => x.Employee)
                .WithMany(emp => emp.MonthlyAttendances)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Holiday>(e =>
        {
            e.ToTable("Holidays");
            e.HasKey(x => x.HolidayId);
            e.HasIndex(x => x.HolidayDate).IsUnique();
            e.Property(x => x.Name).IsRequired();
        });

        modelBuilder.Entity<AllowedIpAddress>(e =>
        {
            e.ToTable("AllowedIpAddresses");
            e.HasKey(x => x.AllowedIpId);
            e.Property(x => x.IpAddress).IsRequired().HasMaxLength(45);
            e.HasIndex(x => x.IpAddress).IsUnique();
            e.Property(x => x.Note).HasMaxLength(200);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.RoleId);
            e.Property(x => x.Code).IsRequired().HasMaxLength(30);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(80);
        });

        modelBuilder.Entity<ShiftTemplate>(e =>
        {
            e.ToTable("ShiftTemplates");
            e.HasKey(x => x.ShiftId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(80);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<TeamSchedule>(e =>
        {
            e.ToTable("TeamSchedules");
            e.HasKey(x => x.TeamId);
            e.HasOne(x => x.Team)
                .WithOne()
                .HasForeignKey<TeamSchedule>(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Shift)
                .WithMany()
                .HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EmployeeSchedule>(e =>
        {
            e.ToTable("EmployeeSchedules");
            e.HasKey(x => x.EmployeeId);
            e.HasOne(x => x.Employee)
                .WithOne()
                .HasForeignKey<EmployeeSchedule>(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Shift)
                .WithMany()
                .HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RosterEntry>(e =>
        {
            e.ToTable("RosterEntries");
            e.HasKey(x => x.RosterId);
            e.HasIndex(x => new { x.EmployeeId, x.RosterDate }).IsUnique();
            e.Property(x => x.EntryType).IsRequired().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Shift)
                .WithMany()
                .HasForeignKey(x => x.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
