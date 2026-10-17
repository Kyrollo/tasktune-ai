using System;
using System.Collections.Generic;
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
    // DTO for applying AI-suggested subtasks
    public class ApplyGuidanceSubtasksDto
    {
        public List<string> Subtasks { get; set; }
    }

    [RoutePrefix("api/tasks")]
    public class TasksController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetAll(int page = 1, int pageSize = 25, byte? status = null, byte? priority = null, int? projectId = null, int? assignedTo = null)
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var companyId = principal.GetCompanyId();
            var userId = principal.GetUserId();
            var role = principal.GetRole();

            var query = db.Tasks
                .Include(t => t.Project).Include(t => t.AssignedToUser).Include(t => t.CreatedByUser)
                .Include(t => t.Subtasks).Include(t => t.Bugs)
                .Where(t => t.Project.CompanyId == companyId);

            // Employees see only their assigned tasks
            if (role == RoleNames.Employee) query = query.Where(t => t.AssignedTo == userId);

            if (status.HasValue) query = query.Where(t => t.Status == status.Value);
            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);
            if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
            if (assignedTo.HasValue) query = query.Where(t => t.AssignedTo == assignedTo.Value);

            var total = await query.CountAsync();
            var tasks = await query.OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new PagedResultDto<TaskResponseDto>
            {
                Items = tasks.Select(MapTaskStatic).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpGet, Route("{id:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Get(int id)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var task = await db.Tasks
                .Include(t => t.Project).Include(t => t.AssignedToUser).Include(t => t.CreatedByUser)
                .Include(t => t.Subtasks).Include(t => t.Dependencies.Select(d => d.DependsOnTask))
                .Include(t => t.ProgressHistory.Select(p => p.SubmittedByUser))
                .Include(t => t.Attachments.Select(a => a.UploadedByUser))
                .Include(t => t.Bugs)
                .FirstOrDefaultAsync(t => t.TaskId == id && t.Project.CompanyId == companyId);

            if (task == null) return NotFound();

            var dto = new TaskDetailResponseDto
            {
                TaskId = task.TaskId,
                ProjectId = task.ProjectId,
                ProjectName = task.Project?.ProjectName,
                TaskName = task.TaskName,
                Description = task.Description,
                Priority = task.Priority,
                Status = task.Status,
                AssignedTo = MapUserSummary(task.AssignedToUser),
                CreatedBy = MapUserSummary(task.CreatedByUser),
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                CompletionPercentage = task.CompletionPercentage,
                SubmitMethod = task.SubmitMethod,
                DefinitionOfDone = task.DefinitionOfDone,
                SubtaskCount = task.Subtasks?.Count ?? 0,
                SubtaskCompletedCount = task.Subtasks?.Count(s => s.Status == 2) ?? 0,
                BugCount = task.Bugs?.Count ?? 0,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                Subtasks = task.Subtasks?.OrderBy(s => s.OrderIndex).Select(s => new SubtaskResponseDto
                { SubtaskId = s.SubtaskId, SubtaskName = s.SubtaskName, Description = s.Description, Status = s.Status, OrderIndex = s.OrderIndex }).ToList(),
                Dependencies = task.Dependencies?.Select(d => new TaskDependencyResponseDto
                { DependencyId = d.DependencyId, DependsOnTaskId = d.DependsOnTaskId, DependsOnTaskName = d.DependsOnTask?.TaskName, DependencyType = d.DependencyType }).ToList(),
                ProgressHistory = task.ProgressHistory?.OrderByDescending(p => p.SubmittedAt).Select(p => new TaskProgressResponseDto
                { ProgressId = p.ProgressId, CompletionPercentage = p.CompletionPercentage, Notes = p.Notes, Blockers = p.Blockers, SubmittedByName = p.SubmittedByUser?.FullName ?? (p.SubmittedByUser?.FirstName + " " + p.SubmittedByUser?.LastName), SubmittedAt = p.SubmittedAt }).ToList(),
                Attachments = task.Attachments?.Select(a => new TaskAttachmentResponseDto
                { AttachmentId = a.AttachmentId, FileName = a.FileName, FileSize = a.FileSize, MimeType = a.MimeType, UploadedByName = a.UploadedByUser?.FullName ?? (a.UploadedByUser?.FirstName + " " + a.UploadedByUser?.LastName), UploadedAt = a.UploadedAt }).ToList()
            };

            return Ok(dto);
        }

        [HttpPost, Route("")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Create(CreateTaskDto dto)
        {
            if (dto == null) return BadRequest();
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();

            var task = new TaskItem
            {
                ProjectId = dto.ProjectId,
                TaskName = dto.TaskName,
                Description = dto.Description,
                Priority = dto.Priority,
                Status = 0,
                AssignedTo = dto.AssignedTo,
                CreatedBy = userId,
                StartDate = dto.StartDate,
                DueDate = dto.DueDate,
                CompletionPercentage = 0,
                SubmitMethod = dto.SubmitMethod,
                DefinitionOfDone = dto.DefinitionOfDone,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();

            if (dto.Subtasks != null)
            {
                foreach (var st in dto.Subtasks)
                {
                    db.Subtasks.Add(new Subtask
                    {
                        TaskId = task.TaskId,
                        SubtaskName = st.SubtaskName,
                        Description = st.Description,
                        Status = 0,
                        OrderIndex = st.OrderIndex,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            if (dto.DependsOnTaskIds != null)
            {
                foreach (var depId in dto.DependsOnTaskIds)
                {
                    db.TaskDependencies.Add(new TaskDependency
                    { TaskId = task.TaskId, DependsOnTaskId = depId, DependencyType = 0, CreatedAt = DateTime.UtcNow });
                }
            }

            await db.SaveChangesAsync();
            return Created($"api/tasks/{task.TaskId}", MapTaskStatic(task));
        }

        /// <summary>
        /// Calls the AI Task Guidance agent with raw task data (no task ID needed).
        /// Used by the "Create Task" modal to get AI recommendations before saving.
        /// </summary>
        [HttpPost, Route("recommend")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> RecommendTask(CreateTaskDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.TaskName))
                return BadRequest("Task name is required.");

            var principal = (System.Security.Claims.ClaimsPrincipal)User;

            // Look up the project name for context
            string projectName = "General";
            if (dto.ProjectId > 0)
            {
                var project = await db.Projects.FindAsync(dto.ProjectId);
                if (project != null) projectName = project.ProjectName;
            }

            var taskPayload = new
            {
                task_name = dto.TaskName,
                description = dto.Description ?? "",
                //subtasks = dto.Subtasks?.Select(s => new { name = s.SubtaskName, status = 0 }).ToList()
                //           ?? new List<object>(),
                // Cast the Select result to List<object>
                subtasks = dto.Subtasks?.Select(s => (object)new { name = s.SubtaskName, status = 0 }).ToList() ?? new List<object>(),

                definition_of_done = dto.DefinitionOfDone ?? "",
                due_date = dto.DueDate != default(DateTime) ? dto.DueDate.ToString("yyyy-MM-dd") : "N/A",
                dependencies = new List<string>(),
                industry = projectName,
                user_role = RoleNames.GetName(principal.GetRole()),
                completion_percentage = 0
            };

            var inputJson = Newtonsoft.Json.JsonConvert.SerializeObject(taskPayload);

            // Call the Task Guidance FastAPI agent
            string agentUrl = System.Configuration.ConfigurationManager.AppSettings["AgentTaskGuidanceUrl"]
                              ?? "http://localhost:5050/api/guidance";
            try
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(120);
                    var content = new System.Net.Http.StringContent(
                        inputJson, System.Text.Encoding.UTF8, "application/json");
                    var response = await http.PostAsync(agentUrl, content);
                    var outputJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return InternalServerError(new Exception(
                            $"Agent HTTP {(int)response.StatusCode}: {outputJson}"));
                    }

                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject(outputJson);
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception(
                    "Could not reach the AI Task Guidance agent: " + ex.Message
                    + ". Start the agent: uvicorn agent2_task_guidance.server:app --port 5050"));
            }
        }

        [HttpPut, Route("{id:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Update(int id, UpdateTaskDto dto)
        {
            if (dto == null) return BadRequest();
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var task = await db.Tasks.Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.TaskId == id && t.Project.CompanyId == companyId);
            if (task == null) return NotFound();

            if (!string.IsNullOrEmpty(dto.TaskName)) task.TaskName = dto.TaskName;
            if (dto.Description != null) task.Description = dto.Description;
            if (dto.Priority.HasValue) task.Priority = dto.Priority.Value;
            if (dto.Status.HasValue) task.Status = dto.Status.Value;
            if (dto.AssignedTo.HasValue) task.AssignedTo = dto.AssignedTo.Value;
            if (dto.StartDate.HasValue) task.StartDate = dto.StartDate.Value;
            if (dto.DueDate.HasValue) task.DueDate = dto.DueDate.Value;
            if (dto.CompletionPercentage.HasValue) task.CompletionPercentage = dto.CompletionPercentage.Value;
            if (dto.SubmitMethod != null) task.SubmitMethod = dto.SubmitMethod;
            if (dto.DefinitionOfDone != null) task.DefinitionOfDone = dto.DefinitionOfDone;
            task.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Ok(MapTaskStatic(task));
        }

        [HttpDelete, Route("{id:int}")]
        [JwtAuthorize(RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var task = await db.Tasks.Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.TaskId == id && t.Project.CompanyId == companyId);
            if (task == null) return NotFound();
            db.Tasks.Remove(task);
            await db.SaveChangesAsync();
            return StatusCode(System.Net.HttpStatusCode.NoContent);
        }

        [HttpPost, Route("{id:int}/progress")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> SubmitProgress(int id, SubmitProgressDto dto)
        {
            if (dto == null) return BadRequest();
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var task = await db.Tasks.FindAsync(id);
            if (task == null) return NotFound();

            var progress = new Models.TaskProgress
            {
                TaskId = id,
                SubmittedBy = userId,
                CompletionPercentage = dto.CompletionPercentage,
                Notes = dto.Notes,
                Blockers = dto.Blockers,
                SubmittedAt = DateTime.UtcNow
            };
            db.TaskProgress.Add(progress);

            task.CompletionPercentage = dto.CompletionPercentage;
            if (dto.CompletionPercentage == 100) task.Status = 2;
            else if (dto.CompletionPercentage > 0) task.Status = 1;
            task.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Ok(new TaskProgressResponseDto
            {
                ProgressId = progress.ProgressId,
                CompletionPercentage = progress.CompletionPercentage,
                Notes = progress.Notes,
                Blockers = progress.Blockers,
                SubmittedAt = progress.SubmittedAt
            });
        }

        [HttpGet, Route("{id:int}/progress")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetProgress(int id)
        {
            var history = await db.TaskProgress.Include(p => p.SubmittedByUser)
                .Where(p => p.TaskId == id).OrderByDescending(p => p.SubmittedAt).ToListAsync();
            return Ok(history.Select(p => new TaskProgressResponseDto
            {
                ProgressId = p.ProgressId,
                CompletionPercentage = p.CompletionPercentage,
                Notes = p.Notes,
                Blockers = p.Blockers,
                SubmittedByName = p.SubmittedByUser?.FullName ?? (p.SubmittedByUser?.FirstName + " " + p.SubmittedByUser?.LastName),
                SubmittedAt = p.SubmittedAt
            }).ToList());
        }

        [HttpGet, Route("{id:int}/subtasks")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetSubtasks(int id)
        {
            var subtasks = await db.Subtasks.Where(s => s.TaskId == id).OrderBy(s => s.OrderIndex).ToListAsync();
            return Ok(subtasks.Select(s => new SubtaskResponseDto
            { SubtaskId = s.SubtaskId, SubtaskName = s.SubtaskName, Description = s.Description, Status = s.Status, OrderIndex = s.OrderIndex }).ToList());
        }

        [HttpPatch, Route("{id:int}/subtasks/{subtaskId:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> UpdateSubtask(int id, int subtaskId, UpdateSubtaskDto dto)
        {
            var subtask = await db.Subtasks.FirstOrDefaultAsync(s => s.SubtaskId == subtaskId && s.TaskId == id);
            if (subtask == null) return NotFound();

            if (dto.Status.HasValue) subtask.Status = dto.Status.Value;
            if (!string.IsNullOrEmpty(dto.SubtaskName)) subtask.SubtaskName = dto.SubtaskName;
            subtask.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Ok(new SubtaskResponseDto
            { SubtaskId = subtask.SubtaskId, SubtaskName = subtask.SubtaskName, Description = subtask.Description, Status = subtask.Status, OrderIndex = subtask.OrderIndex });
        }

        // Static mapping helper used by other controllers
        public static TaskResponseDto MapTaskStatic(TaskItem t)
        {
            return new TaskResponseDto
            {
                TaskId = t.TaskId,
                ProjectId = t.ProjectId,
                ProjectName = t.Project?.ProjectName,
                TaskName = t.TaskName,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                AssignedTo = t.AssignedToUser != null ? MapUserSummary(t.AssignedToUser) : null,
                CreatedBy = t.CreatedByUser != null ? MapUserSummary(t.CreatedByUser) : null,
                StartDate = t.StartDate,
                DueDate = t.DueDate,
                CompletionPercentage = t.CompletionPercentage,
                SubmitMethod = t.SubmitMethod,
                DefinitionOfDone = t.DefinitionOfDone,
                SubtaskCount = t.Subtasks?.Count ?? 0,
                SubtaskCompletedCount = t.Subtasks?.Count(s => s.Status == 2) ?? 0,
                BugCount = t.Bugs?.Count ?? 0,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            };
        }

        private static UserSummaryDto MapUserSummary(User u)
        {
            if (u == null) return null;
            return new UserSummaryDto
            { UserId = u.UserId, FullName = u.FullName ?? (u.FirstName + " " + u.LastName), Email = u.Email, Role = u.Role, RoleName = RoleNames.GetName(u.Role) };
        }

        [HttpGet, Route("{id:int}/guidance")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetGuidance(int id)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var task = await db.Tasks.Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.TaskId == id && t.Project.CompanyId == companyId);
            if (task == null) return NotFound();

            // Return the most recent cached guidance output for this task
            var log = await db.AIActionsLog
                .Where(l => l.TaskId == id && l.ActionType == 1 && l.OutputData != null)
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync();

            if (log == null) return Ok((object)null);

            try
            {
                var output = Newtonsoft.Json.JsonConvert.DeserializeObject(log.OutputData);
                return Ok(output);
            }
            catch
            {
                return Ok((object)null);
            }
        }

        [HttpPost, Route("{id:int}/guidance/generate")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GenerateGuidance(int id)
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var companyId = principal.GetCompanyId();
            var userId = principal.GetUserId();

            var task = await db.Tasks
                .Include(t => t.Project)
                .Include(t => t.Subtasks)
                .Include(t => t.Dependencies.Select(d => d.DependsOnTask))
                .FirstOrDefaultAsync(t => t.TaskId == id && t.Project.CompanyId == companyId);
            if (task == null) return NotFound();

            // Build the payload that the Python agent expects
            var taskPayload = new
            {
                task_name = task.TaskName,
                description = task.Description ?? "",
                subtasks = task.Subtasks?.Select(s => new { name = s.SubtaskName, status = s.Status }).ToList(),
                definition_of_done = task.DefinitionOfDone ?? "",
                due_date = task.DueDate != default(DateTime) ? task.DueDate.ToString("yyyy-MM-dd") : "N/A",
                dependencies = task.Dependencies?.Select(d => d.DependsOnTask?.TaskName ?? "").Where(n => n != "").ToList(),
                industry = task.Project?.ProjectName ?? "General",
                user_role = RoleNames.GetName(principal.GetRole()),
                completion_percentage = task.CompletionPercentage
            };

            var inputJson = Newtonsoft.Json.JsonConvert.SerializeObject(taskPayload);

            string outputJson;

            // --- Strategy 1: Call the Python agent via HTTP (FastAPI server) ---
            string agentUrl = System.Configuration.ConfigurationManager.AppSettings["AgentTaskGuidanceUrl"]
                              ?? "http://localhost:5050/api/guidance";
            try
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(120);
                    var content = new System.Net.Http.StringContent(
                        inputJson, System.Text.Encoding.UTF8, "application/json");
                    var response = await http.PostAsync(agentUrl, content);

                    outputJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return InternalServerError(new Exception(
                            $"Agent HTTP {(int)response.StatusCode}: {outputJson}"));
                    }
                }
            }
            catch (Exception httpEx)
            {
                // --- Strategy 2: Fall back to subprocess ---
                string pythonExe = System.Configuration.ConfigurationManager.AppSettings["PythonExe"] ?? "python";
                string agentModule = System.Configuration.ConfigurationManager.AppSettings["AgentTaskGuidance"] ?? "agent2_task_guidance";
                string workDir = System.Configuration.ConfigurationManager.AppSettings["AgentWorkDir"] ?? "";

                // Resolve workDir: if empty, derive from the project layout
                if (string.IsNullOrWhiteSpace(workDir))
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                    var filesDir = System.IO.Path.GetFullPath(
                        System.IO.Path.Combine(baseDir, @"..\..\..\..\tasktune-agents"));
                    if (System.IO.Directory.Exists(filesDir))
                        workDir = filesDir;
                    else
                        workDir = baseDir;
                }

                if (!System.IO.Directory.Exists(workDir))
                    return InternalServerError(new Exception(
                        $"Agent API unreachable ({httpEx.Message}) and AgentWorkDir does not exist: {workDir}. "
                        + "Please start the agent server: uvicorn agent2_task_guidance.server:app --port 5050"));

                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = $"-m {agentModule}",
                        WorkingDirectory = workDir,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                    };

                    using (var proc = System.Diagnostics.Process.Start(psi))
                    {
                        await proc.StandardInput.WriteAsync(inputJson);
                        proc.StandardInput.Close();

                        var readOutput = proc.StandardOutput.ReadToEndAsync();
                        var readError = proc.StandardError.ReadToEndAsync();
                        await System.Threading.Tasks.Task.WhenAll(readOutput, readError);

                        proc.WaitForExit();

                        if (proc.ExitCode != 0)
                        {
                            string errText = await readError;
                            return InternalServerError(new Exception("Agent error: " + errText));
                        }

                        outputJson = await readOutput;
                    }
                }
                catch (Exception ex)
                {
                    return InternalServerError(new Exception(
                        "Could not reach the AI agent via HTTP or subprocess: " + ex.Message
                        + ". Start the agent: uvicorn agent2_task_guidance.server:app --port 5050"));
                }
            }

            // Persist to AIActionsLog (ActionType 1 = TaskGuidance)
            db.AIActionsLog.Add(new AIActionLog
            {
                ActionType = 1,
                TaskId = id,
                UserId = userId,
                InputData = inputJson,
                OutputData = outputJson,
                ModelVersion = "gemini-3-flash-preview",
                Timestamp = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var result = Newtonsoft.Json.JsonConvert.DeserializeObject(outputJson);
            return Ok(result);
        }

        /// <summary>
        /// Calls the AI Progress Tracking agent to analyze task progress,
        /// predict completion, assess risk, and generate recommendations.
        /// </summary>
        [HttpPost, Route("{id:int}/progress/ai-analyze")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> AnalyzeProgress(int id)
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var companyId = principal.GetCompanyId();
            var userId = principal.GetUserId();

            var task = await db.Tasks
                .Include(t => t.Project)
                .Include(t => t.ProgressHistory.Select(p => p.SubmittedByUser))
                .FirstOrDefaultAsync(t => t.TaskId == id && t.Project.CompanyId == companyId);
            if (task == null) return NotFound();

            // Build progress updates list from history
            //var progressUpdates = task.ProgressHistory?
            //    .OrderBy(p => p.SubmittedAt)
            //    .Select(p => new
            //    {
            //        submitted_at = p.SubmittedAt.ToString("o"),
            //        completion_percentage = p.CompletionPercentage,
            //        notes = p.Notes ?? "",
            //        blockers = p.Blockers ?? ""
            //    }).ToList() ?? new List<object>();

            var progressUpdates = task.ProgressHistory?
            .OrderBy(p => p.SubmittedAt)
            .Select(p => (object)new
            {
                submitted_at = p.SubmittedAt.ToString("o"),
                completion_percentage = p.CompletionPercentage,
                notes = p.Notes ?? "",
                blockers = p.Blockers ?? ""
            }).ToList() ?? new List<object>();


            // Extract blockers from most recent progress entries
            var recentBlockers = task.ProgressHistory?
                .OrderByDescending(p => p.SubmittedAt)
                .Where(p => !string.IsNullOrWhiteSpace(p.Blockers))
                .Take(3)
                .Select(p => p.Blockers)
                .ToList() ?? new List<string>();

            var taskPayload = new
            {
                task_id = task.TaskId.ToString(),
                task_title = task.TaskName ?? "",
                task_description = task.Description ?? "",
                current_completion_percentage = task.CompletionPercentage,
                start_date = task.StartDate != default(DateTime) ? task.StartDate.ToString("yyyy-MM-dd") : "",
                due_date = task.DueDate != default(DateTime) ? task.DueDate.ToString("yyyy-MM-dd") : "N/A",
                progress_updates = progressUpdates,
                current_blockers = recentBlockers,
                user_performance_history = new List<object>()
            };

            var inputJson = Newtonsoft.Json.JsonConvert.SerializeObject(taskPayload);
            string outputJson;

            // --- Strategy 1: Call the Python agent via HTTP (FastAPI server) ---
            string agentUrl = System.Configuration.ConfigurationManager.AppSettings["AgentProgressTrackingUrl"]
                              ?? "http://localhost:8003/api/ai/progress-tracking";
            try
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(120);
                    var content = new System.Net.Http.StringContent(
                        inputJson, System.Text.Encoding.UTF8, "application/json");
                    var response = await http.PostAsync(agentUrl, content);

                    outputJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return InternalServerError(new Exception(
                            $"Agent HTTP {(int)response.StatusCode}: {outputJson}"));
                    }
                }
            }
            catch (Exception httpEx)
            {
                // --- Strategy 2: Fall back to subprocess ---
                string pythonExe = System.Configuration.ConfigurationManager.AppSettings["PythonExe"] ?? "python";
                string agentModule = System.Configuration.ConfigurationManager.AppSettings["AgentProgressTracking"] ?? "agent3_progress_tracking";
                string workDir = System.Configuration.ConfigurationManager.AppSettings["AgentWorkDir"] ?? "";

                if (string.IsNullOrWhiteSpace(workDir))
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                    var filesDir = System.IO.Path.GetFullPath(
                        System.IO.Path.Combine(baseDir, @"..\..\..\..\tasktune-agents"));
                    if (System.IO.Directory.Exists(filesDir))
                        workDir = filesDir;
                    else
                        workDir = baseDir;
                }

                if (!System.IO.Directory.Exists(workDir))
                    return InternalServerError(new Exception(
                        $"Agent API unreachable ({httpEx.Message}) and AgentWorkDir does not exist: {workDir}. "
                        + "Please start the agent server: uvicorn agent3_progress_tracking.server:app --port 8003"));

                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = $"-m {agentModule}",
                        WorkingDirectory = workDir,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                    };

                    using (var proc = System.Diagnostics.Process.Start(psi))
                    {
                        await proc.StandardInput.WriteAsync(inputJson);
                        proc.StandardInput.Close();

                        var readOutput = proc.StandardOutput.ReadToEndAsync();
                        var readError = proc.StandardError.ReadToEndAsync();
                        await System.Threading.Tasks.Task.WhenAll(readOutput, readError);

                        proc.WaitForExit();

                        if (proc.ExitCode != 0)
                        {
                            string errText = await readError;
                            return InternalServerError(new Exception("Agent error: " + errText));
                        }

                        outputJson = await readOutput;
                    }
                }
                catch (Exception ex)
                {
                    return InternalServerError(new Exception(
                        "Could not reach the AI agent via HTTP or subprocess: " + ex.Message
                        + ". Start the agent: uvicorn agent3_progress_tracking.server:app --port 8003"));
                }
            }

            // Persist to AIActionsLog (ActionType 3 = ProgressTracking)
            db.AIActionsLog.Add(new AIActionLog
            {
                ActionType = 3,
                TaskId = id,
                UserId = userId,
                InputData = inputJson,
                OutputData = outputJson,
                ModelVersion = "gemini-2.0-flash",
                Timestamp = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var result = Newtonsoft.Json.JsonConvert.DeserializeObject(outputJson);
            return Ok(result);
        }

        [HttpPost, Route("{id:int}/guidance/apply-subtasks")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> ApplyGuidanceSubtasks(int id, ApplyGuidanceSubtasksDto dto)
        {
            if (dto == null || dto.Subtasks == null || !dto.Subtasks.Any())
                return BadRequest();

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var task = await db.Tasks.Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.TaskId == id && t.Project.CompanyId == companyId);
            if (task == null) return NotFound();

            // Determine the next OrderIndex
            var maxOrder = await db.Subtasks.Where(s => s.TaskId == id)
                .Select(s => (int?)s.OrderIndex).MaxAsync() ?? 0;

            int idx = maxOrder + 1;
            foreach (var stepName in dto.Subtasks)
            {
                if (string.IsNullOrWhiteSpace(stepName)) continue;
                db.Subtasks.Add(new Subtask
                {
                    TaskId = id,
                    SubtaskName = stepName.Length > 500 ? stepName.Substring(0, 500) : stepName,
                    Description = null,
                    Status = 0,
                    OrderIndex = idx++,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();

            var subtasks = await db.Subtasks.Where(s => s.TaskId == id).OrderBy(s => s.OrderIndex).ToListAsync();
            return Ok(subtasks.Select(s => new SubtaskResponseDto
            { SubtaskId = s.SubtaskId, SubtaskName = s.SubtaskName, Description = s.Description, Status = s.Status, OrderIndex = s.OrderIndex }).ToList());
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}