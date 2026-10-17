using System;

namespace TaskTuneAPI.Models.DTOs
{
    public class ProjectResponseDto
    {
        public int ProjectId { get; set; }
        public int CompanyId { get; set; }
        public string ProjectName { get; set; }
        public string Description { get; set; }
        public int ManagerId { get; set; }
        public string ManagerName { get; set; }
        public byte Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateProjectDto
    {
        public string ProjectName { get; set; }
        public string Description { get; set; }
        public int ManagerId { get; set; }
        public byte Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class UpdateProjectDto
    {
        public string ProjectName { get; set; }
        public string Description { get; set; }
        public int? ManagerId { get; set; }
        public byte? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
