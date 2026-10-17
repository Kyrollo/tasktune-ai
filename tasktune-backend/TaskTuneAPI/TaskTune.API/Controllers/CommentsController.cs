using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/tasks/{taskId:int}/comments")]
[Authorize]
public class CommentsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public CommentsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetComments(int taskId)
    {
        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return NotFound();

        var items = await _db.TaskComments
            .Where(c => c.TaskId == taskId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.CommentId,
                c.TaskId,
                c.UserId,
                c.Content,
                c.CreatedAt,
                UserName = c.User.FullName
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> CreateComment(int taskId, [FromBody] CreateCommentDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return NotFound();

        var comment = new TaskComment
        {
            TaskId = taskId,
            UserId = userId.Value,
            Content = dto.Content,
            CreatedAt = DateTime.UtcNow
        };

        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId.Value);

        return CreatedAtAction(nameof(GetComments), new { taskId }, new
        {
            comment.CommentId,
            comment.TaskId,
            comment.UserId,
            comment.Content,
            comment.CreatedAt,
            UserName = user?.FullName
        });
    }
}

public record CreateCommentDto(string Content);
