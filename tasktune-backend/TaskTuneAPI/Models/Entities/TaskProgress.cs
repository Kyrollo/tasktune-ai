using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class TaskProgress
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProgressId { get; set; }

        public int TaskId { get; set; }
        public int SubmittedBy { get; set; }
        public int CompletionPercentage { get; set; }
        public string Notes { get; set; }
        public string Blockers { get; set; }
        public DateTime SubmittedAt { get; set; }

        public virtual TaskItem Task { get; set; }
        public virtual User SubmittedByUser { get; set; }
    }
}