#nullable disable
using System;

namespace TaskTune.Infrastructure.Models;

public partial class TaskComment
{
    public int CommentId { get; set; }
    public int TaskId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual Task Task { get; set; }
    public virtual User User { get; set; }
}
