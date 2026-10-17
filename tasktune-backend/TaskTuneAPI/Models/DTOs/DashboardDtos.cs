using System.Collections.Generic;

namespace TaskTuneAPI.Models.DTOs
{
    public class EmployeeDashboardDto
    {
        public int TasksCompleted { get; set; }
        public int TotalTasks { get; set; }
        public int ActiveTasks { get; set; }
        public decimal AverageScore { get; set; }
        public int UpcomingDeadlines { get; set; }
        public int OverdueTasks { get; set; }
        public List<TaskResponseDto> MyTasks { get; set; }
        public List<NotificationResponseDto> RecentNotifications { get; set; }
    }

    public class SupervisorDashboardDto
    {
        public int TotalTeamTasks { get; set; }
        public int PendingReviews { get; set; }
        public int TeamMembers { get; set; }
        public decimal TeamAverageScore { get; set; }
        public int OverdueTasks { get; set; }
        public int OpenBugs { get; set; }
        public List<TaskResponseDto> ReviewQueue { get; set; }
    }

    public class ManagerDashboardDto
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public decimal DepartmentAverageScore { get; set; }
        public int OverdueTasks { get; set; }
        public List<ProjectResponseDto> Projects { get; set; }
    }

    public class ExecutiveDashboardDto
    {
        public int TotalEmployees { get; set; }
        public int TotalProjects { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public decimal CompanyAverageScore { get; set; }
        public int OverdueTasks { get; set; }
        public int OpenBugs { get; set; }
        public List<DepartmentStatsDto> DepartmentStats { get; set; }
        public List<UserSummaryDto> TopPerformers { get; set; }
    }

    public class DepartmentStatsDto
    {
        public string DepartmentName { get; set; }
        public int EmployeeCount { get; set; }
        public int TaskCount { get; set; }
        public int CompletedCount { get; set; }
        public decimal AverageScore { get; set; }
    }

    public class HrDashboardDto
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int NewHiresThisMonth { get; set; }
        public decimal CompanyAverageScore { get; set; }
        public int OverdueTaskAlerts { get; set; }
        public int LowPerformanceAlerts { get; set; }
        public List<CommunicationResponseDto> RecentCommunications { get; set; }
    }

    public class AdminDashboardDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalCompanies { get; set; }
        public int TotalAuditLogs { get; set; }
        public int TotalAiActions { get; set; }
        public List<AuditLogResponseDto> RecentAuditLogs { get; set; }
    }
}