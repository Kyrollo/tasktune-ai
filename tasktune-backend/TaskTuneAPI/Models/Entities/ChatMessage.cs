using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MessageId { get; set; }

        public int ThreadId { get; set; }
        public byte Role { get; set; }

        [Required]
        public string Content { get; set; }

        public int? TokensUsed { get; set; }

        [MaxLength(50)]
        public string ModelVersion { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime SentAt { get; set; }

        public virtual ChatThread Thread { get; set; }
    }
}