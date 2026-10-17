using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Project
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProjectId { get; set; }

        public int CompanyId { get; set; }

        [Required, MaxLength(255)]
        public string ProjectName { get; set; }

        public string Description { get; set; }
        public int ManagerId { get; set; }
        public byte Status { get; set; }

        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime EndDate { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual Company Company { get; set; }
        public virtual User Manager { get; set; }
        public virtual ICollection<TaskItem> Tasks { get; set; }
    }
}