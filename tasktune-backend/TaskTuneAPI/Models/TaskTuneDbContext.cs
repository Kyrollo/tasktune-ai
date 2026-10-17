using System.Data.Entity;

namespace TaskTuneAPI.Models
{
    public class TaskTuneDbContext : DbContext
    {
        public TaskTuneDbContext() : base("name=TaskTuneDb")
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<TaskAccessControl> TaskAccessControls { get; set; }
        public DbSet<Subtask> Subtasks { get; set; }
        public DbSet<TaskDependency> TaskDependencies { get; set; }
        public DbSet<TaskProgress> TaskProgress { get; set; }
        public DbSet<TaskAttachment> TaskAttachments { get; set; }
        public DbSet<Evaluation> Evaluations { get; set; }
        public DbSet<Bug> Bugs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CommunicationLog> CommunicationLogs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AIActionLog> AIActionsLog { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<ChatThread> ChatThreads { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Company
            modelBuilder.Entity<Company>().ToTable("Companies").HasKey(c => c.CompanyId);

            // Department
            modelBuilder.Entity<Department>().ToTable("Departments").HasKey(d => d.DepartmentId);
            modelBuilder.Entity<Department>()
                .HasRequired(d => d.Company)
                .WithMany(c => c.Departments)
                .HasForeignKey(d => d.CompanyId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Department>()
                .HasOptional(d => d.ParentDepartment)
                .WithMany()
                .HasForeignKey(d => d.ParentDepartmentId);

            // Position
            modelBuilder.Entity<Position>().ToTable("Positions").HasKey(p => p.PositionId);
            modelBuilder.Entity<Position>()
                .HasRequired(p => p.Company)
                .WithMany(c => c.Positions)
                .HasForeignKey(p => p.CompanyId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Position>()
                .HasRequired(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Position>()
                .HasOptional(p => p.ReportsToPosition)
                .WithMany()
                .HasForeignKey(p => p.ReportsToPositionId);

            // User
            modelBuilder.Entity<User>().ToTable("Users").HasKey(u => u.UserId);
            modelBuilder.Entity<User>()
                .HasRequired(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<User>()
                .HasOptional(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId);
            modelBuilder.Entity<User>()
                .HasOptional(u => u.Position)
                .WithMany()
                .HasForeignKey(u => u.PositionId);
            modelBuilder.Entity<User>()
                .Property(u => u.FullName)
                .HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed);

            // RolePermission
            modelBuilder.Entity<RolePermission>().ToTable("RolePermissions").HasKey(r => r.PermissionId);
            modelBuilder.Entity<RolePermission>()
                .HasRequired(r => r.Company)
                .WithMany(c => c.RolePermissions)
                .HasForeignKey(r => r.CompanyId)
                .WillCascadeOnDelete(true);

            // Project
            modelBuilder.Entity<Project>().ToTable("Projects").HasKey(p => p.ProjectId);
            modelBuilder.Entity<Project>()
                .HasRequired(p => p.Company)
                .WithMany(c => c.Projects)
                .HasForeignKey(p => p.CompanyId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Project>()
                .HasRequired(p => p.Manager)
                .WithMany()
                .HasForeignKey(p => p.ManagerId)
                .WillCascadeOnDelete(false);

            // Task
            modelBuilder.Entity<TaskItem>().ToTable("Tasks").HasKey(t => t.TaskId);
            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.AssignedToUser)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedTo)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.CreatedByUser)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(t => t.CreatedBy)
                .WillCascadeOnDelete(false);

            // Subtask
            modelBuilder.Entity<Subtask>().ToTable("Subtasks").HasKey(s => s.SubtaskId);
            modelBuilder.Entity<Subtask>()
                .HasRequired(s => s.Task)
                .WithMany(t => t.Subtasks)
                .HasForeignKey(s => s.TaskId)
                .WillCascadeOnDelete(true);

            // TaskAccessControl
            modelBuilder.Entity<TaskAccessControl>().ToTable("TaskAccessControl").HasKey(a => a.AccessId);
            modelBuilder.Entity<TaskAccessControl>()
                .HasRequired(a => a.Task)
                .WithMany(t => t.AccessControls)
                .HasForeignKey(a => a.TaskId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<TaskAccessControl>()
                .HasRequired(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskAccessControl>()
                .HasRequired(a => a.GrantedByUser)
                .WithMany()
                .HasForeignKey(a => a.GrantedBy)
                .WillCascadeOnDelete(false);

            // TaskDependency
            modelBuilder.Entity<TaskDependency>().ToTable("TaskDependencies").HasKey(d => d.DependencyId);
            modelBuilder.Entity<TaskDependency>()
                .HasRequired(d => d.Task)
                .WithMany(t => t.Dependencies)
                .HasForeignKey(d => d.TaskId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<TaskDependency>()
                .HasRequired(d => d.DependsOnTask)
                .WithMany()
                .HasForeignKey(d => d.DependsOnTaskId)
                .WillCascadeOnDelete(false);

            // TaskProgress
            modelBuilder.Entity<TaskProgress>().ToTable("TaskProgress").HasKey(p => p.ProgressId);
            modelBuilder.Entity<TaskProgress>()
                .HasRequired(p => p.Task)
                .WithMany(t => t.ProgressHistory)
                .HasForeignKey(p => p.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskProgress>()
                .HasRequired(p => p.SubmittedByUser)
                .WithMany()
                .HasForeignKey(p => p.SubmittedBy)
                .WillCascadeOnDelete(false);

            // TaskAttachment
            modelBuilder.Entity<TaskAttachment>().ToTable("TaskAttachments").HasKey(a => a.AttachmentId);
            modelBuilder.Entity<TaskAttachment>()
                .HasRequired(a => a.Task)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<TaskAttachment>()
                .HasOptional(a => a.Progress)
                .WithMany()
                .HasForeignKey(a => a.ProgressId);
            modelBuilder.Entity<TaskAttachment>()
                .HasRequired(a => a.UploadedByUser)
                .WithMany()
                .HasForeignKey(a => a.UploadedBy)
                .WillCascadeOnDelete(false);

            // Evaluation
            modelBuilder.Entity<Evaluation>().ToTable("Evaluations").HasKey(e => e.EvaluationId);
            modelBuilder.Entity<Evaluation>()
                .HasRequired(e => e.Task)
                .WithMany(t => t.Evaluations)
                .HasForeignKey(e => e.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Evaluation>()
                .HasRequired(e => e.EvaluatedUser)
                .WithMany(u => u.EvaluationsReceived)
                .HasForeignKey(e => e.EvaluatedUserId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Evaluation>()
                .HasRequired(e => e.Evaluator)
                .WithMany(u => u.EvaluationsGiven)
                .HasForeignKey(e => e.EvaluatorId)
                .WillCascadeOnDelete(false);

            // Bug
            modelBuilder.Entity<Bug>().ToTable("Bugs").HasKey(b => b.BugId);
            modelBuilder.Entity<Bug>()
                .HasRequired(b => b.Task)
                .WithMany(t => t.Bugs)
                .HasForeignKey(b => b.TaskId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Bug>()
                .HasRequired(b => b.ReportedByUser)
                .WithMany()
                .HasForeignKey(b => b.ReportedBy)
                .WillCascadeOnDelete(false);

            // Notification
            modelBuilder.Entity<Notification>().ToTable("Notifications").HasKey(n => n.NotificationId);
            modelBuilder.Entity<Notification>()
                .HasRequired(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<Notification>()
                .HasOptional(n => n.RelatedTask)
                .WithMany()
                .HasForeignKey(n => n.RelatedTaskId);
            modelBuilder.Entity<Notification>()
                .HasOptional(n => n.RelatedProject)
                .WithMany()
                .HasForeignKey(n => n.RelatedProjectId);

            // CommunicationLog
            modelBuilder.Entity<CommunicationLog>().ToTable("CommunicationLog").HasKey(c => c.CommunicationId);
            modelBuilder.Entity<CommunicationLog>()
                .HasRequired(c => c.Company)
                .WithMany()
                .HasForeignKey(c => c.CompanyId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<CommunicationLog>()
                .HasRequired(c => c.SentByUser)
                .WithMany()
                .HasForeignKey(c => c.SentBy)
                .WillCascadeOnDelete(false);

            // AuditLog
            modelBuilder.Entity<AuditLog>().ToTable("AuditLogs").HasKey(a => a.LogId);
            modelBuilder.Entity<AuditLog>()
                .HasRequired(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .WillCascadeOnDelete(false);

            // AIActionLog
            modelBuilder.Entity<AIActionLog>().ToTable("AIActionsLog").HasKey(a => a.AiLogId);

            // Report
            modelBuilder.Entity<Report>().ToTable("Reports").HasKey(r => r.ReportId);
            modelBuilder.Entity<Report>()
                .HasRequired(r => r.Company)
                .WithMany()
                .HasForeignKey(r => r.CompanyId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Report>()
                .HasRequired(r => r.GeneratedByUser)
                .WithMany()
                .HasForeignKey(r => r.GeneratedBy)
                .WillCascadeOnDelete(false);

            // ChatThread
            modelBuilder.Entity<ChatThread>().ToTable("ChatThreads").HasKey(c => c.ThreadId);
            modelBuilder.Entity<ChatThread>()
                .HasRequired(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .WillCascadeOnDelete(true);

            // ChatMessage
            modelBuilder.Entity<ChatMessage>().ToTable("ChatMessages").HasKey(c => c.MessageId);
            modelBuilder.Entity<ChatMessage>()
                .HasRequired(c => c.Thread)
                .WithMany(t => t.Messages)
                .HasForeignKey(c => c.ThreadId)
                .WillCascadeOnDelete(true);

            base.OnModelCreating(modelBuilder);
        }
    }
}