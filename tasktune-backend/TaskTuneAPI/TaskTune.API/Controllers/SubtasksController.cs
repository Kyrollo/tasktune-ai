using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/tasks/{taskId:int}/subtasks")]
[Authorize]
public class SubtasksController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public SubtasksController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetSubtasks(int taskId)
    {
        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return NotFound();

        var items = await _db.Subtasks
            .Where(s => s.TaskId == taskId)
            .OrderBy(s => s.OrderIndex)
            .Select(s => new
            {
                s.SubtaskId,
                s.TaskId,
                s.SubtaskName,
                s.Description,
                s.Status,
                s.OrderIndex,
                s.CreatedAt,
                s.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubtask(int taskId, [FromBody] CreateSubtaskDto dto)
    {
        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return NotFound();

        var maxOrder = await _db.Subtasks
            .Where(s => s.TaskId == taskId)
            .Select(s => (int?)s.OrderIndex)
            .MaxAsync();

        var subtask = new Subtask
        {
            TaskId = taskId,
            SubtaskName = dto.SubtaskName,
            Description = dto.Description,
            Status = 0,
            OrderIndex = (maxOrder ?? -1) + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Subtasks.Add(subtask);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSubtasks), new { taskId }, new
        {
            subtask.SubtaskId,
            subtask.TaskId,
            subtask.SubtaskName,
            subtask.Description,
            subtask.Status,
            subtask.OrderIndex
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateSubtask(int taskId, int id, [FromBody] UpdateSubtaskDto dto)
    {
        var subtask = await _db.Subtasks
            .FirstOrDefaultAsync(s => s.SubtaskId == id && s.TaskId == taskId);
        if (subtask == null) return NotFound();

        subtask.SubtaskName = dto.SubtaskName;
        subtask.Description = dto.Description;
        subtask.Status = dto.Status;
        subtask.OrderIndex = dto.OrderIndex;
        subtask.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new
        {
            subtask.SubtaskId,
            subtask.SubtaskName,
            subtask.Description,
            subtask.Status,
            subtask.OrderIndex
        });
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<IActionResult> ToggleSubtaskStatus(int taskId, int id)
    {
        var subtask = await _db.Subtasks
            .FirstOrDefaultAsync(s => s.SubtaskId == id && s.TaskId == taskId);
        if (subtask == null) return NotFound();

        // 0=pending, 1=in-progress, 2=done - cycle: pending -> in-progress -> done -> pending
        subtask.Status = subtask.Status switch
        {
            0 => (byte)1,
            1 => (byte)2,
            _ => (byte)0
        };
        subtask.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { subtask.SubtaskId, subtask.Status });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSubtask(int taskId, int id)
    {
        var subtask = await _db.Subtasks
            .FirstOrDefaultAsync(s => s.SubtaskId == id && s.TaskId == taskId);
        if (subtask == null) return NotFound();

        _db.Subtasks.Remove(subtask);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateSubtaskDto(string SubtaskName, string? Description);
public record UpdateSubtaskDto(string SubtaskName, string? Description, byte Status, int OrderIndex);
