using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Application.Interfaces;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/tasks/{taskId:int}/comments")]
[Authorize]
public class CommentsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly ITaskService _taskService;

    public CommentsController(TaskTuneDbContext db, ITaskService taskService)
    {
        _db = db;
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<IActionResult> GetComments(int taskId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        if (!await _taskService.CanAccessTaskAsync(taskId, userId.Value)) return NotFound();

        var items = await _db.TaskComments.Where(c => c.TaskId == taskId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.CommentId, c.TaskId, c.UserId, c.Content, c.CreatedAt,
                UserName = c.User.FullName
            }).ToListAsync();
        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> CreateComment(int taskId, [FromBody] CreateCommentDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        if (!await _taskService.CanAccessTaskAsync(taskId, userId.Value)) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Content) || dto.Content.Length > 2000)
            return BadRequest(new { message = "Comment content must be between 1 and 2000 characters." });

        var comment = new TaskComment
        {
            TaskId = taskId, UserId = userId.Value,
            Content = dto.Content.Trim(), CreatedAt = DateTime.UtcNow
        };
        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);

        return CreatedAtAction(nameof(GetComments), new { taskId }, new
        {
            comment.CommentId, comment.TaskId, comment.UserId, comment.Content, comment.CreatedAt,
            UserName = user?.FullName
        });
    }
}

public record CreateCommentDto(string Content);
