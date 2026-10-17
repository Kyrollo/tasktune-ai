using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class Company
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CompanyId { get; set; }

        [Required, MaxLength(255)]
        public string CompanyName { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string SettingsJson { get; set; }

        public virtual ICollection<Department> Departments { get; set; }
        public virtual ICollection<Position> Positions { get; set; }
        public virtual ICollection<User> Users { get; set; }
        public virtual ICollection<Project> Projects { get; set; }
        public virtual ICollection<RolePermission> RolePermissions { get; set; }
    }
}