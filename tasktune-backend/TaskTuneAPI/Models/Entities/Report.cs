using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Report
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReportId { get; set; }

        public int CompanyId { get; set; }
        public int GeneratedBy { get; set; }
        public byte ReportType { get; set; }

        [Column(TypeName = "date")]
        public DateTime ReportPeriodStart { get; set; }

        [Column(TypeName = "date")]
        public DateTime ReportPeriodEnd { get; set; }

        [MaxLength(500)]
        public string FilePath { get; set; }

        public string MetadataJson { get; set; }
        public DateTime GeneratedAt { get; set; }

        public virtual Company Company { get; set; }
        public virtual User GeneratedByUser { get; set; }
    }
}