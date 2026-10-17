using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Bug
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BugId { get; set; }

        public int TaskId { get; set; }
        public int ReportedBy { get; set; }

        [Required]
        public string BugDescription { get; set; }

        public byte RiskLevel { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public virtual TaskItem Task { get; set; }
        public virtual User ReportedByUser { get; set; }
    }
}