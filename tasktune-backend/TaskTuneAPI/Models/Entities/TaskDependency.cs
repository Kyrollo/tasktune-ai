using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTuneAPI.Models
{
    public class TaskDependency
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DependencyId { get; set; }

        public int TaskId { get; set; }
        public int DependsOnTaskId { get; set; }
        public byte DependencyType { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual TaskItem Task { get; set; }
        public virtual TaskItem DependsOnTask { get; set; }
    }
}