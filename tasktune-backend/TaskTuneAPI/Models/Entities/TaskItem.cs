using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    [Table("Tasks")]
    public class TaskItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TaskId { get; set; }

        public int ProjectId { get; set; }

        [Required, MaxLength(255)]
        public string TaskName { get; set; }

        public string Description { get; set; }
        public byte Priority { get; set; }
        public byte Status { get; set; }
        public int AssignedTo { get; set; }
        public int CreatedBy { get; set; }

        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime DueDate { get; set; }

        public int CompletionPercentage { get; set; }

        [MaxLength(255)]
        public string SubmitMethod { get; set; }

        public string DefinitionOfDone { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual Project Project { get; set; }
        public virtual User AssignedToUser { get; set; }
        public virtual User CreatedByUser { get; set; }
        public virtual ICollection<Subtask> Subtasks { get; set; }
        public virtual ICollection<TaskAccessControl> AccessControls { get; set; }
        public virtual ICollection<TaskDependency> Dependencies { get; set; }
        public virtual ICollection<TaskProgress> ProgressHistory { get; set; }
        public virtual ICollection<TaskAttachment> Attachments { get; set; }
        public virtual ICollection<Evaluation> Evaluations { get; set; }
        public virtual ICollection<Bug> Bugs { get; set; }
    }
}