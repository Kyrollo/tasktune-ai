using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class TaskAccessControl
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccessId { get; set; }

        public int TaskId { get; set; }
        public int UserId { get; set; }
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanViewEvaluation { get; set; }
        public bool CanEditEvaluation { get; set; }
        public int GrantedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual TaskItem Task { get; set; }
        public virtual User User { get; set; }
        public virtual User GrantedByUser { get; set; }
    }
}