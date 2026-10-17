using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;
using TaskTuneAPI.Models.DTOs;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/dashboards")]
    public class DashboardsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("employee")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Employee()
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var userId = principal.GetUserId();
            var companyId = principal.GetCompanyId();

            var tasks = await db.Tasks.Include(t => t.Project).Include(t => t.AssignedToUser).Include(t => t.CreatedByUser).Include(t => t.Subtasks).Include(t => t.Bugs)
                .Where(t => t.AssignedTo == userId && t.Project.CompanyId == companyId).ToListAsync();
            var evals = await db.Evaluations.Where(e => e.EvaluatedUserId == userId).ToListAsync();
            var notifs = await db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.SentAt).Take(5).ToListAsync();

            var now = DateTime.UtcNow;
            return Ok(new EmployeeDashboardDto
            {
                TotalTasks = tasks.Count,
                TasksCompleted = tasks.Count(t => t.Status == 2),
                ActiveTasks = tasks.Count(t => t.Status == 1),
                AverageScore = evals.Any() ? evals.Average(e => e.OverallScore) : 0,
                UpcomingDeadlines = tasks.Count(t => t.Status != 2 && t.DueDate >= now && t.DueDate <= now.AddDays(7)),
                OverdueTasks = tasks.Count(t => t.Status != 2 && t.DueDate < now),
                MyTasks = tasks.Select(TasksController.MapTaskStatic).ToList(),
                RecentNotifications = notifs.Select(n => new NotificationResponseDto
                { NotificationId = n.NotificationId, NotificationType = n.NotificationType, Title = n.Title, Message = n.Message, RelatedTaskId = n.RelatedTaskId, RelatedProjectId = n.RelatedProjectId, IsRead = n.IsRead, SentAt = n.SentAt, ReadAt = n.ReadAt }).ToList()
            });
        }

        [HttpGet, Route("supervisor")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO)]
        public async Task<IHttpActionResult> Supervisor()
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var userId = principal.GetUserId();
            var companyId = principal.GetCompanyId();

            var user = await db.Users.FindAsync(userId);
            var deptId = user?.DepartmentId;

            var teamTasks = await db.Tasks.Include(t => t.Project).Include(t => t.AssignedToUser).Include(t => t.CreatedByUser).Include(t => t.Subtasks).Include(t => t.Bugs)
                .Where(t => t.Project.CompanyId == companyId && (deptId == null || t.AssignedToUser.DepartmentId == deptId)).ToListAsync();
            var teamMembers = await db.Users.CountAsync(u => u.CompanyId == companyId && u.DepartmentId == deptId && u.IsActive);
            var completedTasks = teamTasks.Where(t => t.Status == 2).ToList();
            var teamEvals = await db.Evaluations.Where(e => completedTasks.Select(t => t.TaskId).Contains(e.TaskId)).ToListAsync();
            var openBugs = await db.Bugs.CountAsync(b => b.Status != 2 && teamTasks.Select(t => t.TaskId).Contains(b.TaskId));
            var now = DateTime.UtcNow;

            return Ok(new SupervisorDashboardDto
            {
                TotalTeamTasks = teamTasks.Count,
                PendingReviews = teamTasks.Count(t => t.Status == 2 && !t.Evaluations.Any()),
                TeamMembers = teamMembers,
                TeamAverageScore = teamEvals.Any() ? teamEvals.Average(e => e.OverallScore) : 0,
                OverdueTasks = teamTasks.Count(t => t.Status != 2 && t.DueDate < now),
                OpenBugs = openBugs,
                ReviewQueue = teamTasks.Where(t => t.Status == 1 || t.Status == 2).Take(10).Select(TasksController.MapTaskStatic).ToList()
            });
        }

        [HttpGet, Route("executive")]
        [JwtAuthorize(RoleNames.CEO)]
        public async Task<IHttpActionResult> Executive()
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var employees = await db.Users.CountAsync(u => u.CompanyId == companyId && u.IsActive);
            var projects = await db.Projects.CountAsync(p => p.CompanyId == companyId);
            var tasks = await db.Tasks.Where(t => t.Project.CompanyId == companyId).ToListAsync();
            var evals = await db.Evaluations.Where(e => e.EvaluatedUser.CompanyId == companyId).ToListAsync();
            var bugs = await db.Bugs.CountAsync(b => b.Status != 2 && b.Task.Project.CompanyId == companyId);
            var now = DateTime.UtcNow;

            var deptStats = await db.Departments.Where(d => d.CompanyId == companyId)
                .Select(d => new DepartmentStatsDto
                {
                    DepartmentName = d.DepartmentName,
                    EmployeeCount = d.Users.Count(u => u.IsActive),
                    TaskCount = db.Tasks.Count(t => t.AssignedToUser.DepartmentId == d.DepartmentId && t.Project.CompanyId == companyId),
                    CompletedCount = db.Tasks.Count(t => t.AssignedToUser.DepartmentId == d.DepartmentId && t.Status == 2 && t.Project.CompanyId == companyId),
                    AverageScore = db.Evaluations.Where(e => e.EvaluatedUser.DepartmentId == d.DepartmentId).Select(e => (decimal?)e.OverallScore).Average() ?? 0
                }).ToListAsync();

            return Ok(new ExecutiveDashboardDto
            {
                TotalEmployees = employees,
                TotalProjects = projects,
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == 2),
                CompanyAverageScore = evals.Any() ? evals.Average(e => e.OverallScore) : 0,
                OverdueTasks = tasks.Count(t => t.Status != 2 && t.DueDate < now),
                OpenBugs = bugs,
                DepartmentStats = deptStats
            });
        }

        [HttpGet, Route("hr")]
        [JwtAuthorize(RoleNames.HR)]
        public async Task<IHttpActionResult> Hr()
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var totalEmp = await db.Users.CountAsync(u => u.CompanyId == companyId);
            var activeEmp = await db.Users.CountAsync(u => u.CompanyId == companyId && u.IsActive);
            var newHires = await db.Users.CountAsync(u => u.CompanyId == companyId && u.CreatedAt >= monthStart);
            var evals = await db.Evaluations.Where(e => e.EvaluatedUser.CompanyId == companyId).ToListAsync();
            var overdue = await db.Tasks.CountAsync(t => t.Project.CompanyId == companyId && t.Status != 2 && t.DueDate < now);
            var lowPerf = evals.Count(e => e.OverallScore < 60);
            var comms = await db.CommunicationLogs.Where(c => c.CompanyId == companyId).OrderByDescending(c => c.SentAt).Take(5).ToListAsync();

            return Ok(new HrDashboardDto
            {
                TotalEmployees = totalEmp,
                ActiveEmployees = activeEmp,
                NewHiresThisMonth = newHires,
                CompanyAverageScore = evals.Any() ? evals.Average(e => e.OverallScore) : 0,
                OverdueTaskAlerts = overdue,
                LowPerformanceAlerts = lowPerf,
                RecentCommunications = comms.Select(c => new CommunicationResponseDto
                { CommunicationId = c.CommunicationId, Subject = c.Subject, Body = c.Body, CommunicationType = c.CommunicationType, SentAt = c.SentAt }).ToList()
            });
        }

        [HttpGet, Route("admin")]
        [JwtAuthorize(RoleNames.Admin)]
        public async Task<IHttpActionResult> Admin()
        {
            var totalUsers = await db.Users.CountAsync();
            var activeUsers = await db.Users.CountAsync(u => u.IsActive);
            var totalCompanies = await db.Companies.CountAsync();
            var totalAudit = await db.AuditLogs.CountAsync();
            var totalAi = await db.AIActionsLog.CountAsync();
            var recentLogs = await db.AuditLogs.Include(a => a.User).OrderByDescending(a => a.Timestamp).Take(10).ToListAsync();

            return Ok(new AdminDashboardDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                TotalCompanies = totalCompanies,
                TotalAuditLogs = totalAudit,
                TotalAiActions = totalAi,
                RecentAuditLogs = recentLogs.Select(a => new AuditLogResponseDto
                { LogId = a.LogId, UserName = a.User?.FullName ?? (a.User?.FirstName + " " + a.User?.LastName), Action = a.Action, ResourceType = a.ResourceType, ResourceId = a.ResourceId, ChangesJson = a.ChangesJson, IpAddress = a.IpAddress, Timestamp = a.Timestamp }).ToList()
            });
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}