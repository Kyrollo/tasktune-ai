using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Subtask
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SubtaskId { get; set; }

        public int TaskId { get; set; }

        [Required, MaxLength(255)]
        public string SubtaskName { get; set; }

        public string Description { get; set; }
        public byte Status { get; set; }
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual TaskItem Task { get; set; }
    }
}