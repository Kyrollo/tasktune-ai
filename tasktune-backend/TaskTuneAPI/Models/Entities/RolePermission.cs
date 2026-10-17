using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class RolePermission
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PermissionId { get; set; }

        public int CompanyId { get; set; }
        public byte Role { get; set; }

        [Required, MaxLength(100)]
        public string Resource { get; set; }

        [Required, MaxLength(50)]
        public string Action { get; set; }

        public bool CanPerform { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual Company Company { get; set; }
    }
}