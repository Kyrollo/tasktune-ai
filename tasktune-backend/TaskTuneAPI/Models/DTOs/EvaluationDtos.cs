using System;

namespace TaskTuneAPI.Models.DTOs
{
    public class EvaluationResponseDto
    {
        public int EvaluationId { get; set; }
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public UserSummaryDto EvaluatedUser { get; set; }
        public UserSummaryDto Evaluator { get; set; }
        public decimal OverallScore { get; set; }
        public decimal QualityScore { get; set; }
        public decimal TimelinessScore { get; set; }
        public decimal CollaborationScore { get; set; }
        public byte RiskLevel { get; set; }
        public string ManualFeedback { get; set; }
        public string AiGeneratedFeedback { get; set; }
        public byte Status { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateEvaluationDto
    {
        public int TaskId { get; set; }
        public int EvaluatedUserId { get; set; }
        public decimal OverallScore { get; set; }
        public decimal QualityScore { get; set; }
        public decimal TimelinessScore { get; set; }
        public decimal CollaborationScore { get; set; }
        public byte RiskLevel { get; set; }
        public string ManualFeedback { get; set; }
    }

    public class UpdateEvaluationDto
    {
        public decimal? OverallScore { get; set; }
        public decimal? QualityScore { get; set; }
        public decimal? TimelinessScore { get; set; }
        public decimal? CollaborationScore { get; set; }
        public byte? RiskLevel { get; set; }
        public string ManualFeedback { get; set; }
        public byte? Status { get; set; }
    }
}