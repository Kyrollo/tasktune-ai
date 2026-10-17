using System;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;
using TaskTuneAPI.Models.DTOs;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/files")]
    public class FileUploadController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".gif", ".zip", ".txt", ".csv" };

        private const int MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

        [HttpPost, Route("upload")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Upload()
        {
            if (!Request.Content.IsMimeMultipartContent())
                return BadRequest("Multipart content expected");

            var uploadPath = HttpContext.Current.Server.MapPath(
                ConfigurationManager.AppSettings["UploadPath"] ?? "~/Uploads/");

            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            var provider = new MultipartFormDataStreamProvider(uploadPath);

            try
            {
                await Request.Content.ReadAsMultipartAsync(provider);
            }
            catch (Exception ex)
            {
                return BadRequest("Error reading upload: " + ex.Message);
            }

            // Read form fields
            var taskIdStr = provider.FormData["taskId"];
            var progressIdStr = provider.FormData["progressId"];

            int.TryParse(taskIdStr, out int taskId);
            int? progressId = null;
            if (int.TryParse(progressIdStr, out int pid)) progressId = pid;

            if (taskId <= 0)
            {
                // Cleanup temp files
                foreach (var file in provider.FileData)
                    if (File.Exists(file.LocalFileName)) File.Delete(file.LocalFileName);
                return BadRequest("taskId is required");
            }

            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var results = new System.Collections.Generic.List<TaskAttachmentResponseDto>();

            foreach (var fileData in provider.FileData)
            {
                var originalFileName = fileData.Headers.ContentDisposition.FileName?.Trim('"') ?? "unknown";
                var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

                if (!AllowedExtensions.Contains(extension))
                {
                    if (File.Exists(fileData.LocalFileName)) File.Delete(fileData.LocalFileName);
                    continue;
                }

                var fileInfo = new FileInfo(fileData.LocalFileName);
                if (fileInfo.Length > MaxFileSizeBytes)
                {
                    if (File.Exists(fileData.LocalFileName)) File.Delete(fileData.LocalFileName);
                    continue;
                }

                // Rename file with unique name
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var targetPath = Path.Combine(uploadPath, uniqueFileName);
                File.Move(fileData.LocalFileName, targetPath);

                var attachment = new TaskAttachment
                {
                    TaskId = taskId,
                    ProgressId = progressId,
                    UploadedBy = userId,
                    FileName = originalFileName,
                    FilePath = $"/Uploads/{uniqueFileName}",
                    FileSize = (int)fileInfo.Length,
                    MimeType = fileData.Headers.ContentType?.MediaType ?? "application/octet-stream",
                    UploadedAt = DateTime.UtcNow
                };

                db.TaskAttachments.Add(attachment);
                await db.SaveChangesAsync();

                results.Add(new TaskAttachmentResponseDto
                {
                    AttachmentId = attachment.AttachmentId,
                    FileName = attachment.FileName,
                    FileSize = attachment.FileSize,
                    MimeType = attachment.MimeType,
                    UploadedAt = attachment.UploadedAt
                });
            }

            return Ok(results);
        }

        [HttpGet, Route("{id:int}")]
        [JwtAuthorize]
        public async Task<HttpResponseMessage> Download(int id)
        {
            var attachment = await db.TaskAttachments.FindAsync(id);
            if (attachment == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "File not found");

            var physicalPath = HttpContext.Current.Server.MapPath("~" + attachment.FilePath);
            if (!File.Exists(physicalPath))
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Physical file not found");

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new FileStream(physicalPath, FileMode.Open, FileAccess.Read))
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(attachment.MimeType);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = attachment.FileName
            };

            return response;
        }

        [HttpDelete, Route("{id:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var role = ((System.Security.Claims.ClaimsPrincipal)User).GetRole();

            var attachment = await db.TaskAttachments.FindAsync(id);
            if (attachment == null) return NotFound();

            // Only uploader or Admin can delete
            if (attachment.UploadedBy != userId && role != RoleNames.Admin)
                return StatusCode(HttpStatusCode.Forbidden);

            var physicalPath = HttpContext.Current.Server.MapPath("~" + attachment.FilePath);
            if (File.Exists(physicalPath))
                File.Delete(physicalPath);

            db.TaskAttachments.Remove(attachment);
            await db.SaveChangesAsync();

            return StatusCode(HttpStatusCode.NoContent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
