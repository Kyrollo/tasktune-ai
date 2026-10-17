using System;
using System.Collections.Generic;

namespace TaskTuneAPI.Models.DTOs
{
    public class TaskResponseDto
    {
        public int TaskId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string TaskName { get; set; }
        public string Description { get; set; }
        public byte Priority { get; set; }
        public byte Status { get; set; }
        public UserSummaryDto AssignedTo { get; set; }
        public UserSummaryDto CreatedBy { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public int CompletionPercentage { get; set; }
        public string SubmitMethod { get; set; }
        public string DefinitionOfDone { get; set; }
        public int SubtaskCount { get; set; }
        public int SubtaskCompletedCount { get; set; }
        public int BugCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class TaskDetailResponseDto : TaskResponseDto
    {
        public List<SubtaskResponseDto> Subtasks { get; set; }
        public List<TaskDependencyResponseDto> Dependencies { get; set; }
        public List<TaskProgressResponseDto> ProgressHistory { get; set; }
        public List<TaskAttachmentResponseDto> Attachments { get; set; }
    }

    public class CreateTaskDto
    {
        public int ProjectId { get; set; }
        public string TaskName { get; set; }
        public string Description { get; set; }
        public byte Priority { get; set; }
        public int AssignedTo { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime DueDate { get; set; }
        public string SubmitMethod { get; set; }
        public string DefinitionOfDone { get; set; }
        public List<CreateSubtaskDto> Subtasks { get; set; }
        public List<int> DependsOnTaskIds { get; set; }
    }

    public class UpdateTaskDto
    {
        public string TaskName { get; set; }
        public string Description { get; set; }
        public byte? Priority { get; set; }
        public byte? Status { get; set; }
        public int? AssignedTo { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int? CompletionPercentage { get; set; }
        public string SubmitMethod { get; set; }
        public string DefinitionOfDone { get; set; }
    }

    public class SubmitProgressDto
    {
        public int CompletionPercentage { get; set; }
        public string Notes { get; set; }
        public string Blockers { get; set; }
    }

    public class SubtaskResponseDto
    {
        public int SubtaskId { get; set; }
        public string SubtaskName { get; set; }
        public string Description { get; set; }
        public byte Status { get; set; }
        public int OrderIndex { get; set; }
    }

    public class CreateSubtaskDto
    {
        public string SubtaskName { get; set; }
        public string Description { get; set; }
        public int OrderIndex { get; set; }
    }

    public class UpdateSubtaskDto
    {
        public byte? Status { get; set; }
        public string SubtaskName { get; set; }
    }

    public class TaskDependencyResponseDto
    {
        public int DependencyId { get; set; }
        public int DependsOnTaskId { get; set; }
        public string DependsOnTaskName { get; set; }
        public byte DependencyType { get; set; }
    }

    public class TaskProgressResponseDto
    {
        public int ProgressId { get; set; }
        public int CompletionPercentage { get; set; }
        public string Notes { get; set; }
        public string Blockers { get; set; }
        public string SubmittedByName { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class TaskAttachmentResponseDto
    {
        public int AttachmentId { get; set; }
        public string FileName { get; set; }
        public int FileSize { get; set; }
        public string MimeType { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}