using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Position
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PositionId { get; set; }

        public int CompanyId { get; set; }
        public int DepartmentId { get; set; }

        [Required, MaxLength(255)]
        public string PositionTitle { get; set; }

        public int HierarchyLevel { get; set; }
        public string Responsibilities { get; set; }
        public int? ReportsToPositionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual Company Company { get; set; }
        public virtual Department Department { get; set; }
        public virtual Position ReportsToPosition { get; set; }
    }
}