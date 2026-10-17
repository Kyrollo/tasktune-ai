using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        public int UserId { get; set; }

        [Required, MaxLength(100)]
        public string Action { get; set; }

        [Required, MaxLength(100)]
        public string ResourceType { get; set; }

        public int ResourceId { get; set; }
        public string ChangesJson { get; set; }

        [MaxLength(45)]
        public string IpAddress { get; set; }

        [MaxLength(500)]
        public string UserAgent { get; set; }

        public DateTime Timestamp { get; set; }

        public virtual User User { get; set; }
    }
}