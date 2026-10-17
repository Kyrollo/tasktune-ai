using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        public int CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public int? PositionId { get; set; }

        [Required, MaxLength(255)]
        public string Email { get; set; }

        [Required, MaxLength(255)]
        public string PasswordHash { get; set; }

        [Required, MaxLength(100)]
        public string FirstName { get; set; }

        [Required, MaxLength(100)]
        public string LastName { get; set; }

        [MaxLength(20)]
        public string Phone { get; set; }

        public byte Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }

        [MaxLength(500)]
        public string RefreshToken { get; set; }

        public DateTime? RefreshTokenExpiry { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string FullName { get; set; }

        public virtual Company Company { get; set; }
        public virtual Department Department { get; set; }
        public virtual Position Position { get; set; }
        public virtual ICollection<TaskItem> AssignedTasks { get; set; }
        public virtual ICollection<TaskItem> CreatedTasks { get; set; }
        public virtual ICollection<Evaluation> EvaluationsReceived { get; set; }
        public virtual ICollection<Evaluation> EvaluationsGiven { get; set; }
        public virtual ICollection<Notification> Notifications { get; set; }
    }
}