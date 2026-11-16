using Microsoft.EntityFrameworkCore;

namespace TaskTune.Infrastructure.Models;

public partial class TaskTuneDbContext
{
    public virtual DbSet<UserPermissionOverride> UserPermissionOverrides { get; set; }
    public virtual DbSet<EmployeeReportingLine> EmployeeReportingLines { get; set; }
    public virtual DbSet<LeaveType> LeaveTypes { get; set; }
    public virtual DbSet<LeaveBalance> LeaveBalances { get; set; }
    public virtual DbSet<LeaveRequest> LeaveRequests { get; set; }
    public virtual DbSet<CompanyHoliday> CompanyHolidays { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPermissionOverride>(entity =>
        {
            entity.ToTable("UserPermissionOverrides");
            entity.HasKey(e => e.PermissionOverrideId);
            entity.HasIndex(e => new { e.CompanyId, e.UserId, e.Resource, e.Action, e.ScopeResourceId }, "UQ_UserPermissionOverrides")
                .IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.UserId, e.ExpiresAt }, "IX_UserPermissionOverrides_Active");

            entity.Property(e => e.Resource).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Scope).HasMaxLength(20).HasDefaultValue("self").IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UserPermissionOverrides_Companies");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UserPermissionOverrides_Users");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.GrantedBy)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UserPermissionOverrides_GrantedBy");
        });

        modelBuilder.Entity<EmployeeReportingLine>(entity =>
        {
            entity.ToTable("EmployeeReportingLines");
            entity.HasKey(e => e.ReportingLineId);
            entity.HasIndex(e => new
                {
                    e.CompanyId, e.EmployeeUserId, e.ManagerUserId, e.RelationshipType, e.EffectiveFrom
                }, "UQ_EmployeeReportingLines").IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.EmployeeUserId, e.EffectiveTo }, "IX_EmployeeReportingLines_Employee");
            entity.HasIndex(e => new { e.CompanyId, e.ManagerUserId, e.EffectiveTo }, "IX_EmployeeReportingLines_Manager");

            entity.Property(e => e.RelationshipType).HasMaxLength(30).HasDefaultValue("line").IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeReportingLines_Companies");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.EmployeeUserId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeReportingLines_Employee");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.ManagerUserId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeReportingLines_Manager");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeReportingLines_CreatedBy");
        });

        modelBuilder.Entity<LeaveType>(entity =>
        {
            entity.ToTable("LeaveTypes");
            entity.HasKey(e => e.LeaveTypeId);
            entity.HasIndex(e => new { e.CompanyId, e.Code }, "UQ_LeaveTypes_Company_Code").IsUnique();
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.AnnualAllowanceDays).HasPrecision(8, 2);
            entity.Property(e => e.MaximumConsecutiveDays).HasPrecision(8, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(SYSUTCDATETIME())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(SYSUTCDATETIME())");
            entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveTypes_Companies");
        });

        modelBuilder.Entity<LeaveBalance>(entity =>
        {
            entity.ToTable("LeaveBalances");
            entity.HasKey(e => e.LeaveBalanceId);
            entity.HasIndex(e => new { e.CompanyId, e.UserId, e.LeaveTypeId, e.Year }, "UQ_LeaveBalances_Annual").IsUnique();
            entity.Property(e => e.AllocatedDays).HasPrecision(8, 2);
            entity.Property(e => e.CarriedOverDays).HasPrecision(8, 2);
            entity.Property(e => e.UsedDays).HasPrecision(8, 2);
            entity.Property(e => e.PendingDays).HasPrecision(8, 2);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(SYSUTCDATETIME())");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveBalances_Companies");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveBalances_Users");
            entity.HasOne<LeaveType>().WithMany().HasForeignKey(e => e.LeaveTypeId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveBalances_LeaveTypes");
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("LeaveRequests");
            entity.HasKey(e => e.LeaveRequestId);
            entity.HasIndex(e => new { e.CompanyId, e.UserId, e.StartDate, e.EndDate }, "IX_LeaveRequests_Employee_Dates");
            entity.HasIndex(e => new { e.CompanyId, e.ApproverUserId, e.Status }, "IX_LeaveRequests_Approver_Status");
            entity.Property(e => e.RequestedDays).HasPrecision(8, 2);
            entity.Property(e => e.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DecisionComment).HasMaxLength(2000);
            entity.Property(e => e.SubmittedAt).HasDefaultValueSql("(SYSUTCDATETIME())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(SYSUTCDATETIME())");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveRequests_Companies");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveRequests_Users");
            entity.HasOne<LeaveType>().WithMany().HasForeignKey(e => e.LeaveTypeId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveRequests_LeaveTypes");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.ApproverUserId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveRequests_Approver");
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.DecisionBy)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_LeaveRequests_DecisionBy");
        });

        modelBuilder.Entity<CompanyHoliday>(entity =>
        {
            entity.ToTable("CompanyHolidays");
            entity.HasKey(e => e.CompanyHolidayId);
            entity.HasIndex(e => new { e.CompanyId, e.HolidayDate }, "IX_CompanyHolidays_Date");
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(SYSUTCDATETIME())");
            entity.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_CompanyHolidays_Companies");
        });
    }
}
