using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }

        public int UserId { get; set; }
        public byte NotificationType { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public int? RelatedTaskId { get; set; }
        public int? RelatedProjectId { get; set; }
        public bool IsRead { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }

        public virtual User User { get; set; }
        public virtual TaskItem RelatedTask { get; set; }
        public virtual Project RelatedProject { get; set; }
    }
}