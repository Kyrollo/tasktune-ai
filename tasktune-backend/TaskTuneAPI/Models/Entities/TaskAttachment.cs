using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class TaskAttachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AttachmentId { get; set; }

        public int TaskId { get; set; }
        public int? ProgressId { get; set; }
        public int UploadedBy { get; set; }

        [Required, MaxLength(255)]
        public string FileName { get; set; }

        [Required, MaxLength(500)]
        public string FilePath { get; set; }

        public int FileSize { get; set; }

        [Required, MaxLength(100)]
        public string MimeType { get; set; }

        public DateTime UploadedAt { get; set; }

        public virtual TaskItem Task { get; set; }
        public virtual TaskProgress Progress { get; set; }
        public virtual User UploadedByUser { get; set; }
    }
}