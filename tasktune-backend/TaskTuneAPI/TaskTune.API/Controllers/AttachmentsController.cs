using System.Data;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/tasks/{taskId:int}/attachments")]
[RequireModule("tasks")]
[Authorize]
public class AttachmentsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly TenantEntitlementService _entitlements;
    private const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv"
    };

    public AttachmentsController(
        TaskTuneDbContext db,
        IWebHostEnvironment env,
        TenantEntitlementService entitlements)
    {
        _db = db;
        _env = env;
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<IActionResult> GetAttachments(int taskId)
    {
        if (!await CanViewTaskAsync(_db, taskId)) return NotFound();

        var items = await _db.TaskAttachments.AsNoTracking()
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new
            {
                a.AttachmentId,
                a.TaskId,
                a.FileName,
                a.FileSize,
                a.MimeType,
                a.UploadedAt,
                a.UploadedBy,
                UploaderName = a.UploadedByNavigation.FullName,
                DownloadUrl = $"/api/tasks/{taskId}/attachments/{a.AttachmentId}/download"
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
    public async Task<IActionResult> UploadAttachment(int taskId, IFormFile? file)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();
        if (!await CanViewTaskAsync(_db, taskId)) return NotFound();

        if (file == null || file.Length <= 0)
            return BadRequest(new { message = "No file provided." });
        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "File size exceeds the 10 MB limit." });

        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        if (string.IsNullOrWhiteSpace(extension) || !AllowedContentTypes.TryGetValue(extension, out var expectedMime))
            return BadRequest(new { message = "This file type is not supported. Upload PDF, PNG, JPEG, TXT or CSV files." });

        if (!await HasValidFileSignatureAsync(file, extension))
            return BadRequest(new { message = "The uploaded file content does not match its file type." });

        var displayName = SanitizeDisplayFileName(file.FileName, extension);
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativePath = Path.Combine(
            "company", companyId.Value.ToString(),
            "tasks", taskId.ToString(),
            storedName);

        var storageRoot = GetPrivateStorageRoot();
        var absolutePath = Path.GetFullPath(Path.Combine(storageRoot, relativePath));
        if (!absolutePath.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return BadRequest(new { message = "Invalid storage location." });

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        try
        {
            await using (var source = file.OpenReadStream())
            await using (var destination = new FileStream(
                absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination);
                await destination.FlushAsync();
            }
        }
        catch (IOException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "The file could not be stored. Please retry." });
        }

        var attachment = new TaskAttachment
        {
            TaskId = taskId,
            UploadedBy = userId.Value,
            FileName = displayName,
            FilePath = relativePath,
            FileSize = checked((int)file.Length),
            MimeType = expectedMime,
            UploadedAt = DateTime.UtcNow
        };

        await using (var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            try
            {
                await _entitlements.EnsureStorageCapacityAsync(companyId.Value, file.Length);

                _db.TaskAttachments.Add(attachment);
                await _db.SaveChangesAsync();

                await _entitlements.RecordUsageAsync(
                    companyId.Value,
                    "storage_bytes",
                    attachment.FileSize,
                    $"attachment:{attachment.AttachmentId}:upload",
                    "TaskAttachment",
                    attachment.AttachmentId.ToString());

                await AuditLogWriter.WriteAsync(_db, HttpContext, userId, "UPLOAD", "TaskAttachment", attachment.AttachmentId,
                    after: new { attachment.TaskId, attachment.FileName, attachment.FileSize, attachment.MimeType });
                await transaction.CommitAsync();
            }
            catch (TenantEntitlementException ex)
            {
                await transaction.RollbackAsync();
                TryDeleteFile(absolutePath);
                return StatusCode(ex.StatusCode, new
                {
                    message = ex.Message,
                    traceId = HttpContext.TraceIdentifier
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                TryDeleteFile(absolutePath);
                throw;
            }
        }

        return CreatedAtAction(nameof(GetAttachments), new { taskId }, new
        {
            attachment.AttachmentId,
            attachment.TaskId,
            attachment.FileName,
            attachment.FileSize,
            attachment.MimeType,
            attachment.UploadedAt,
            attachment.UploadedBy,
            DownloadUrl = $"/api/tasks/{taskId}/attachments/{attachment.AttachmentId}/download"
        });
    }

    [HttpGet("{attachmentId:int}/download")]
    public async Task<IActionResult> DownloadAttachment(int taskId, int attachmentId)
    {
        if (!await CanViewTaskAsync(_db, taskId)) return NotFound();

        var attachment = await _db.TaskAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AttachmentId == attachmentId && a.TaskId == taskId);
        if (attachment == null) return NotFound();

        var storageRoot = GetPrivateStorageRoot();
        var absolutePath = Path.GetFullPath(Path.Combine(storageRoot, attachment.FilePath));
        if (!absolutePath.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return NotFound();
        if (!System.IO.File.Exists(absolutePath)) return NotFound();

        Response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(attachment.FileName)}";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(absolutePath, attachment.MimeType, enableRangeProcessing: true);
    }

    [HttpDelete("{attachmentId:int}")]
    public async Task<IActionResult> DeleteAttachment(int taskId, int attachmentId)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();
        if (!await CanViewTaskAsync(_db, taskId)) return NotFound();

        var attachment = await _db.TaskAttachments
            .Include(a => a.Task)
                .ThenInclude(t => t.Project)
            .FirstOrDefaultAsync(a => a.AttachmentId == attachmentId &&
                                      a.TaskId == taskId &&
                                      a.Task.Project.CompanyId == companyId.Value);
        if (attachment == null) return NotFound();

        var role = GetCurrentRole();
        if (attachment.UploadedBy != userId.Value &&
            role is not ("Supervisor" or "Manager" or "CEO" or "Admin"))
            return Forbid();

        var before = new { attachment.FileName, attachment.TaskId, attachment.FileSize };
        var storageRoot = GetPrivateStorageRoot();
        var absolutePath = Path.GetFullPath(Path.Combine(storageRoot, attachment.FilePath));
        await using (var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var wasMetered = await _db.TenantUsageEvents.AnyAsync(e =>
                e.CompanyId == companyId.Value &&
                e.MeterKey == "storage_bytes" &&
                e.ResourceType == "TaskAttachment" &&
                e.ResourceId == attachmentId.ToString() &&
                e.Quantity > 0);

            _db.TaskAttachments.Remove(attachment);
            await _db.SaveChangesAsync();

            if (wasMetered)
            {
                await _entitlements.RecordUsageAsync(
                    companyId.Value,
                    "storage_bytes",
                    -attachment.FileSize,
                    $"attachment:{attachmentId}:delete",
                    "TaskAttachment",
                    attachmentId.ToString());
            }

            await AuditLogWriter.WriteAsync(_db, HttpContext, userId, "DELETE", "TaskAttachment", attachmentId,
                before: before);
            await transaction.CommitAsync();
        }

        TryDeleteFile(absolutePath);
        return NoContent();
    }

    private string GetPrivateStorageRoot()
    {
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", "private-uploads"));
        Directory.CreateDirectory(root);
        return root.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string SanitizeDisplayFileName(string originalName, string extension)
    {
        var name = Path.GetFileName(originalName);
        var withoutExtension = Path.GetFileNameWithoutExtension(name);
        var safe = new string(withoutExtension
            .Where(c => !char.IsControl(c) && c != '/' && c != '\\')
            .Take(150)
            .ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safe)) safe = "attachment";
        return safe + extension.ToLowerInvariant();
    }

    private static async Task<bool> HasValidFileSignatureAsync(IFormFile file, string extension)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[Math.Min(16, (int)file.Length)];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (read == 0) return false;

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => read >= 5 && Encoding.ASCII.GetString(header, 0, 5) == "%PDF-",
            ".png" => read >= 8 && header.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".txt" or ".csv" => IsTextContent(header, read),
            _ => false
        };
    }

    private static bool IsTextContent(byte[] bytes, int length)
    {
        if (bytes.Take(length).Any(b => b == 0)) return false;
        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes, 0, length);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch (IOException)
        {
            // A cleanup failure must not turn a committed metadata operation into a false success.
        }
        catch (UnauthorizedAccessException)
        {
            // A cleanup failure is logged by the hosting file-system monitor.
        }
    }
}
