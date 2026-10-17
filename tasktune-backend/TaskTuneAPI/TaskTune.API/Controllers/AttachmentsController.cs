using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/tasks/{taskId:int}/attachments")]
[Authorize]
public class AttachmentsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public AttachmentsController(TaskTuneDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetAttachments(int taskId)
    {
        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return NotFound();

        var items = await _db.TaskAttachments
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new
            {
                a.AttachmentId,
                a.TaskId,
                a.FileName,
                a.FilePath,
                a.FileSize,
                a.MimeType,
                a.UploadedAt,
                a.UploadedBy,
                UploaderName = a.UploadedByNavigation.FullName
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> UploadAttachment(int taskId, IFormFile file)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var task = await _db.Tasks.FindAsync(taskId);
        if (task == null) return NotFound();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "File size exceeds 10 MB limit." });

        var uploadDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads", "tasks", taskId.ToString());
        Directory.CreateDirectory(uploadDir);

        var uniqueName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadDir, uniqueName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath = $"/uploads/tasks/{taskId}/{uniqueName}";

        var attachment = new TaskAttachment
        {
            TaskId = taskId,
            UploadedBy = userId.Value,
            FileName = file.FileName,
            FilePath = relativePath,
            FileSize = (int)file.Length,
            MimeType = file.ContentType ?? "application/octet-stream",
            UploadedAt = DateTime.UtcNow
        };

        _db.TaskAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId.Value);

        return CreatedAtAction(nameof(GetAttachments), new { taskId }, new
        {
            attachment.AttachmentId,
            attachment.TaskId,
            attachment.FileName,
            attachment.FilePath,
            attachment.FileSize,
            attachment.MimeType,
            attachment.UploadedAt,
            attachment.UploadedBy,
            UploaderName = user?.FullName
        });
    }
}
