using Microsoft.EntityFrameworkCore;

namespace TaskTune.Infrastructure.Models;

public partial class TaskTuneDbContext
{
    public virtual DbSet<JobApplicant> JobApplicants { get; set; }
    public virtual DbSet<OnboardingTask> OnboardingTasks { get; set; }
    public virtual DbSet<PerformanceGoal> PerformanceGoals { get; set; }
    public virtual DbSet<ExpenseClaim> ExpenseClaims { get; set; }
    public virtual DbSet<CompanyAsset> CompanyAssets { get; set; }
    public virtual DbSet<BenefitPlan> BenefitPlans { get; set; }
    public virtual DbSet<BenefitEnrollment> BenefitEnrollments { get; set; }
}
