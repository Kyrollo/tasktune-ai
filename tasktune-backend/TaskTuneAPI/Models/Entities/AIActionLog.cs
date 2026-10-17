using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    [Table("AIActionsLog")]
    public class AIActionLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AiLogId { get; set; }

        public byte ActionType { get; set; }
        public int? TaskId { get; set; }
        public int? UserId { get; set; }
        public string InputData { get; set; }
        public string OutputData { get; set; }

        [MaxLength(50)]
        public string ModelVersion { get; set; }

        public DateTime Timestamp { get; set; }
    }
}