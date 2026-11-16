using Microsoft.EntityFrameworkCore;

namespace TaskTune.Infrastructure.Models;

public partial class TaskTuneDbContext
{
    public virtual DbSet<UserPermissionOverride> UserPermissionOverrides { get; set; }
    public virtual DbSet<EmployeeReportingLine> EmployeeReportingLines { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPermissionOverride>(entity =>
        {
            entity.ToTable("UserPermissionOverrides");
            entity.HasKey(e => e.PermissionOverrideId);
            entity.HasIndex(e => new { e.CompanyId, e.UserId, e.Resource, e.Action }, "UQ_UserPermissionOverrides")
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
    }
}
