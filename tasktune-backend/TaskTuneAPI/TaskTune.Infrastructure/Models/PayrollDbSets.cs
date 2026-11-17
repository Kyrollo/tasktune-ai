using Microsoft.EntityFrameworkCore;

namespace TaskTune.Infrastructure.Models;

public partial class TaskTuneDbContext
{
    public virtual DbSet<PayrollPeriod> PayrollPeriods { get; set; }
    public virtual DbSet<Payslip> Payslips { get; set; }
}
