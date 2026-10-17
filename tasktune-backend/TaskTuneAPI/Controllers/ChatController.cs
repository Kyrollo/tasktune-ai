using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;
using TaskTuneAPI.Models.DTOs;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/chat")]
    public class ChatController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("threads")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetThreads()
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();

            var threads = await db.ChatThreads
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.UpdatedAt)
                .Select(t => new
                {
                    t.ThreadId,
                    t.Title,
                    t.Status,
                    t.RelatedTaskId,
                    t.RelatedProjectId,
                    t.CreatedAt,
                    t.UpdatedAt,
                    MessageCount = t.Messages.Count(m => !m.IsDeleted)
                })
                .ToListAsync();

            return Ok(threads);
        }

        [HttpPost, Route("threads")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> CreateThread([FromBody] CreateChatThreadDto dto)
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();

            var thread = new ChatThread
            {
                UserId = userId,
                Title = dto?.Title ?? "New Chat",
                RelatedTaskId = dto?.RelatedTaskId,
                RelatedProjectId = dto?.RelatedProjectId,
                Status = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.ChatThreads.Add(thread);
            await db.SaveChangesAsync();

            return Created($"api/chat/threads/{thread.ThreadId}", new
            {
                thread.ThreadId,
                thread.Title,
                thread.CreatedAt
            });
        }

        [HttpGet, Route("threads/{threadId:int}/messages")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetMessages(int threadId)
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();

            var thread = await db.ChatThreads
                .FirstOrDefaultAsync(t => t.ThreadId == threadId && t.UserId == userId);

            if (thread == null) return NotFound();

            var messages = await db.ChatMessages
                .Where(m => m.ThreadId == threadId && !m.IsDeleted)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.MessageId,
                    m.Role,
                    m.Content,
                    m.TokensUsed,
                    m.ModelVersion,
                    m.SentAt
                })
                .ToListAsync();

            return Ok(messages);
        }

        [HttpPost, Route("threads/{threadId:int}/messages")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> SendMessage(int threadId, [FromBody] SendChatMessageDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Content))
                return BadRequest("Message content is required");

            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();

            var thread = await db.ChatThreads
                .FirstOrDefaultAsync(t => t.ThreadId == threadId && t.UserId == userId);

            if (thread == null) return NotFound();

            // Save user message
            var userMessage = new ChatMessage
            {
                ThreadId = threadId,
                Role = 0, // User
                Content = dto.Content,
                IsDeleted = false,
                SentAt = DateTime.UtcNow
            };
            db.ChatMessages.Add(userMessage);

            // Generate placeholder AI response
            var aiResponseText = $"Thank you for your message. I'm TaskTune AI assistant. You said: \"{dto.Content.Substring(0, Math.Min(50, dto.Content.Length))}...\". How can I help you further?";

            var aiMessage = new ChatMessage
            {
                ThreadId = threadId,
                Role = 1, // Assistant
                Content = aiResponseText,
                ModelVersion = "placeholder-v1",
                TokensUsed = aiResponseText.Length / 4,
                IsDeleted = false,
                SentAt = DateTime.UtcNow.AddMilliseconds(500)
            };
            db.ChatMessages.Add(aiMessage);

            thread.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new
            {
                userMessage = new
                {
                    userMessage.MessageId,
                    userMessage.Role,
                    userMessage.Content,
                    userMessage.SentAt
                },
                aiMessage = new
                {
                    aiMessage.MessageId,
                    aiMessage.Role,
                    aiMessage.Content,
                    aiMessage.ModelVersion,
                    aiMessage.TokensUsed,
                    aiMessage.SentAt
                }
            });
        }

        [HttpDelete, Route("threads/{threadId:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> DeleteThread(int threadId)
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var thread = await db.ChatThreads
                .FirstOrDefaultAsync(t => t.ThreadId == threadId && t.UserId == userId);

            if (thread == null) return NotFound();

            db.ChatThreads.Remove(thread); // CASCADE deletes messages
            await db.SaveChangesAsync();

            return StatusCode(System.Net.HttpStatusCode.NoContent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    public class CreateChatThreadDto
    {
        public string Title { get; set; }
        public int? RelatedTaskId { get; set; }
        public int? RelatedProjectId { get; set; }
    }

    public class SendChatMessageDto
    {
        public string Content { get; set; }
    }
}