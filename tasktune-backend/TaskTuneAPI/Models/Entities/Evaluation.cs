using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Evaluation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EvaluationId { get; set; }

        public int TaskId { get; set; }
        public int EvaluatedUserId { get; set; }
        public int EvaluatorId { get; set; }
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

        public virtual TaskItem Task { get; set; }
        public virtual User EvaluatedUser { get; set; }
        public virtual User Evaluator { get; set; }
    }
}