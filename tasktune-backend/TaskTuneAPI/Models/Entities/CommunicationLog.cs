using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class CommunicationLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CommunicationId { get; set; }

        public int CompanyId { get; set; }
        public int SentBy { get; set; }
        public int? RecipientUserId { get; set; }
        public int? RecipientDepartmentId { get; set; }
        public byte CommunicationType { get; set; }

        [Required, MaxLength(255)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        public DateTime SentAt { get; set; }

        public virtual Company Company { get; set; }
        public virtual User SentByUser { get; set; }
    }
}