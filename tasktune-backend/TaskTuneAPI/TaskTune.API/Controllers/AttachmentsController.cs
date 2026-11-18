using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Application.Interfaces;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/tasks/{taskId:int}/attachments")]
[Authorize]
public class AttachmentsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ITaskService _taskService;
    private readonly TenantStorageCapacityService _storageCapacity;
    private const long MaxFileSize = 10 * 1024 * 1024;

    public AttachmentsController(
        TaskTuneDbContext db,
        IWebHostEnvironment env,
        ITaskService taskService,
        TenantStorageCapacityService storageCapacity)
    {
        _db = db;
        _env = env;
        _taskService = taskService;
        _storageCapacity = storageCapacity;
    }

    private async Task<bool> CanAccessTaskAsync(int taskId)
    {
        var userId = GetCurrentUserId();
        return userId.HasValue && await _taskService.CanAccessTaskAsync(taskId, userId.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetAttachments(int taskId)
    {
        if (!await CanAccessTaskAsync(taskId)) return NotFound();
        var items = await _db.TaskAttachments.Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new
            {
                a.AttachmentId, a.TaskId, a.FileName, a.FileSize, a.MimeType,
                a.UploadedAt, a.UploadedBy,
                UploaderName = a.UploadedByNavigation.FullName,
                DownloadUrl = Url.Action(nameof(DownloadAttachment), new { taskId, attachmentId = a.AttachmentId })
            }).ToListAsync();
        return Ok(new { items });
    }

    [HttpGet("{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(int taskId, int attachmentId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        if (!await _taskService.CanAccessTaskAsync(taskId, userId.Value)) return NotFound();

        var attachment = await _db.TaskAttachments
            .FirstOrDefaultAsync(a => a.AttachmentId == attachmentId && a.TaskId == taskId);
        if (attachment == null) return NotFound();

        var storageRoot = GetPrivateUploadRoot();
        var relativePath = (attachment.FilePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Split('/').Any(segment => segment is "" or "." or ".."))
            return NotFound();

        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = Path.GetFullPath(storageRoot) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(rootPrefix, comparison) || !System.IO.System.IO.File.Exists(fullPath))
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return File(stream, "application/octet-stream", Path.GetFileName(attachment.FileName), enableRangeProcessing: true);
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> UploadAttachment(int taskId, IFormFile file, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();
        if (!await _taskService.CanAccessTaskAsync(taskId, userId.Value)) return NotFound();

        if (file == null || file.Length <= 0) return BadRequest(new { message = "No file provided." });
        if (file.Length > MaxFileSize) return BadRequest(new { message = "File size exceeds 10 MB limit." });
        var safeFileName = Path.GetFileName((file.FileName ?? string.Empty).Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 255 || safeFileName.Any(char.IsControl))
            return BadRequest(new { message = "Invalid file name." });

        var uniqueName = $"{Guid.NewGuid():N}_{safeFileName}";
        var storageKey = $"tasks/{taskId}/{uniqueName}";
        var uploadDirectory = Path.Combine(GetPrivateUploadRoot(), "tasks", taskId.ToString());
        Directory.CreateDirectory(uploadDirectory);
        var fullPath = Path.Combine(uploadDirectory, uniqueName);
        var temporaryPath = Path.Combine(uploadDirectory, $".{Guid.NewGuid():N}.uploading");
        string? committedFilePath = null;

        try
        {
            // Stage into private storage first; do not publish the final object until the
            // subscription quota and metadata insert are committed together.
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, bufferSize: 81920, FileOptions.Asynchronous))
            {
                await file.CopyToAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            var actualBytes = new FileInfo(temporaryPath).Length;
            if (actualBytes <= 0 || actualBytes > MaxFileSize)
                return BadRequest(new { message = "File size exceeds 10 MB limit." });

            var relational = _db.Database.IsRelational();
            await using var transaction = relational
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var capacity = await _storageCapacity.CheckAndLockAsync(companyId.Value, actualBytes, cancellationToken);
            if (capacity.Result != TenantStorageCapacityResult.Allowed)
            {
                if (transaction != null)
                    await transaction.RollbackAsync(cancellationToken);
                var statusCode = capacity.Result == TenantStorageCapacityResult.LimitExceeded
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status403Forbidden;
                return StatusCode(statusCode, new
                {
                    code = "tenant_storage_capacity",
                    message = TenantStorageCapacityService.GetErrorMessage(capacity),
                    capacity.CurrentStorageBytes,
                    capacity.RequestedBytes,
                    capacity.MaxStorageBytes
                });
            }

            System.IO.File.Move(temporaryPath, fullPath);
            committedFilePath = fullPath;
            var attachment = new TaskAttachment
            {
                TaskId = taskId,
                UploadedBy = userId.Value,
                FileName = safeFileName,
                FilePath = storageKey,
                FileSize = checked((int)actualBytes),
                MimeType = "application/octet-stream",
                UploadedAt = DateTime.UtcNow
            };
            _db.TaskAttachments.Add(attachment);
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value, cancellationToken);
            return CreatedAtAction(nameof(DownloadAttachment),
                new { taskId, attachmentId = attachment.AttachmentId },
                new
                {
                    attachment.AttachmentId, attachment.TaskId, attachment.FileName,
                    attachment.FileSize, attachment.UploadedAt, attachment.UploadedBy,
                    UploaderName = user?.FullName
                });
        }
        catch
        {
            if (System.IO.System.IO.File.Exists(temporaryPath)) System.IO.System.IO.File.Delete(temporaryPath);
            if (committedFilePath != null && System.IO.System.IO.File.Exists(committedFilePath))
                System.IO.System.IO.File.Delete(committedFilePath);
            throw;
        }
        finally
        {
            if (System.IO.System.IO.File.Exists(temporaryPath)) System.IO.System.IO.File.Delete(temporaryPath);
        }
    }

    private string GetPrivateUploadRoot() =>
        Path.Combine(_env.ContentRootPath, "App_Data", "tasktune-private", "uploads");
}
