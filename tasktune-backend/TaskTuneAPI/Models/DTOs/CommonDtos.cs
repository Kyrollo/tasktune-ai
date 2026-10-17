using System.Collections.Generic;

namespace TaskTuneAPI.Models.DTOs
{
    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class NotificationResponseDto
    {
        public int NotificationId { get; set; }
        public byte NotificationType { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public int? RelatedTaskId { get; set; }
        public int? RelatedProjectId { get; set; }
        public bool IsRead { get; set; }
        public System.DateTime SentAt { get; set; }
        public System.DateTime? ReadAt { get; set; }
    }

    public class BugResponseDto
    {
        public int BugId { get; set; }
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public string ReportedByName { get; set; }
        public string BugDescription { get; set; }
        public byte RiskLevel { get; set; }
        public byte Status { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public System.DateTime? ResolvedAt { get; set; }
    }

    public class CreateBugDto
    {
        public string BugDescription { get; set; }
        public byte RiskLevel { get; set; }
    }

    public class CommunicationResponseDto
    {
        public int CommunicationId { get; set; }
        public string SentByName { get; set; }
        public int? RecipientUserId { get; set; }
        public string RecipientName { get; set; }
        public int? RecipientDepartmentId { get; set; }
        public string RecipientDepartmentName { get; set; }
        public byte CommunicationType { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public System.DateTime SentAt { get; set; }
    }

    public class CreateCommunicationDto
    {
        public int? RecipientUserId { get; set; }
        public int? RecipientDepartmentId { get; set; }
        public byte CommunicationType { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    public class AuditLogResponseDto
    {
        public int LogId { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; }
        public string ResourceType { get; set; }
        public int ResourceId { get; set; }
        public string ChangesJson { get; set; }
        public string IpAddress { get; set; }
        public System.DateTime Timestamp { get; set; }
    }

    public class ReportResponseDto
    {
        public int ReportId { get; set; }
        public string GeneratedByName { get; set; }
        public byte ReportType { get; set; }
        public System.DateTime ReportPeriodStart { get; set; }
        public System.DateTime ReportPeriodEnd { get; set; }
        public string FilePath { get; set; }
        public string MetadataJson { get; set; }
        public System.DateTime GeneratedAt { get; set; }
    }

    public class CreateReportDto
    {
        public byte ReportType { get; set; }
        public System.DateTime ReportPeriodStart { get; set; }
        public System.DateTime ReportPeriodEnd { get; set; }
        public string MetadataJson { get; set; }
    }

    public class RolePermissionDto
    {
        public int PermissionId { get; set; }
        public byte Role { get; set; }
        public string Resource { get; set; }
        public string Action { get; set; }
        public bool CanPerform { get; set; }
    }

    public class UpdateRolePermissionsDto
    {
        public List<RolePermissionUpdateItem> Permissions { get; set; }
    }

    public class RolePermissionUpdateItem
    {
        public byte Role { get; set; }
        public string Resource { get; set; }
        public string Action { get; set; }
        public bool CanPerform { get; set; }
    }

    public class DepartmentResponseDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string Description { get; set; }
        public int? ParentDepartmentId { get; set; }
        public int EmployeeCount { get; set; }
    }

    public class PositionResponseDto
    {
        public int PositionId { get; set; }
        public string PositionTitle { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public int HierarchyLevel { get; set; }
        public int? ReportsToPositionId { get; set; }
    }
}