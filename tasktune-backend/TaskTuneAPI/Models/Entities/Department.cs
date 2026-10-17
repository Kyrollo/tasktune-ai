using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Department
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DepartmentId { get; set; }

        public int CompanyId { get; set; }

        [Required, MaxLength(255)]
        public string DepartmentName { get; set; }

        public int? ParentDepartmentId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual Company Company { get; set; }
        public virtual Department ParentDepartment { get; set; }
        public virtual ICollection<Position> Positions { get; set; }
        public virtual ICollection<User> Users { get; set; }
    }
}