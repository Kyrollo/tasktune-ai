using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class ChatThread
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ThreadId { get; set; }

        public int UserId { get; set; }
        public int? RelatedTaskId { get; set; }
        public int? RelatedProjectId { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; }

        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual User User { get; set; }
        public virtual ICollection<ChatMessage> Messages { get; set; }
    }
}