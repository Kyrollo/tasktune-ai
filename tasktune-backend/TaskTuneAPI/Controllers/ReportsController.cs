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
    /// <summary>DTO for the AI report generation request.</summary>
    public class AIReportRequestDto
    {
        public string ReportType { get; set; } = "performance";
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public string Department { get; set; } = "All Departments";
    }

    [RoutePrefix("api/reports")]
    public class ReportsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpPost, Route("generate")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Generate(CreateReportDto dto)
        {
            if (dto == null) return BadRequest("Request body is required");

            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var userId = principal.GetUserId();
            var companyId = principal.GetCompanyId();

            var report = new Report
            {
                CompanyId = companyId,
                GeneratedBy = userId,
                ReportType = dto.ReportType,
                ReportPeriodStart = dto.ReportPeriodStart,
                ReportPeriodEnd = dto.ReportPeriodEnd,
                MetadataJson = dto.MetadataJson,
                FilePath = $"/reports/co{companyId}/report_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                GeneratedAt = DateTime.UtcNow
            };

            db.Reports.Add(report);
            await db.SaveChangesAsync();

            return Created($"api/reports/{report.ReportId}", MapReport(report));
        }

        /// <summary>
        /// Calls the AI Report Agent (Agent 4) to generate an AI-powered performance report.
        /// Gathers all task/employee data from the DB and sends it to the Python agent.
        /// Same pattern as Agent 2 (TaskGuidance) and Agent 3 (ProgressTracking).
        /// </summary>
        [HttpPost, Route("ai-generate")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> AIGenerate(AIReportRequestDto dto)
        {
            if (dto == null) dto = new AIReportRequestDto();

            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var userId = principal.GetUserId();
            var companyId = principal.GetCompanyId();
            var now = DateTime.UtcNow;

            // 1. Gather all tasks with employee + project + progress info
            var tasks = await db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedToUser)
                .Include(t => t.ProgressHistory)
                .Where(t => t.Project.CompanyId == companyId)
                .OrderBy(t => t.AssignedTo)
                .ThenBy(t => t.DueDate)
                .ToListAsync();

            // 2. Build task list for the agent
            var taskList = tasks.Select(t =>
            {
                var latestBlocker = t.ProgressHistory?
                    .Where(p => !string.IsNullOrWhiteSpace(p.Blockers))
                    .OrderByDescending(p => p.SubmittedAt)
                    .Select(p => p.Blockers)
                    .FirstOrDefault();

                return new
                {
                    task_id = t.TaskId.ToString(),
                    task_name = t.TaskName ?? "",
                    project_id = t.ProjectId.ToString(),
                    project_name = t.Project?.ProjectName ?? "",
                    employee_id = t.AssignedTo.ToString(),
                    employee_name = t.AssignedToUser != null
                        ? (t.AssignedToUser.FullName ?? (t.AssignedToUser.FirstName + " " + t.AssignedToUser.LastName))
                        : "Unassigned",
                    completion_percentage = t.CompletionPercentage,
                    due_date = t.DueDate.ToString("yyyy-MM-dd"),
                    status = t.Status == 0 ? "not-started" : t.Status == 1 ? "in-progress" : "completed",
                    blockers = latestBlocker
                };
            }).ToList();

            // 3. Get current user as the manager/requestor
            var currentUser = await db.Users.FindAsync(userId);
            var managerName = currentUser != null
                ? (currentUser.FullName ?? (currentUser.FirstName + " " + currentUser.LastName))
                : "System";
            var managerEmail = currentUser?.Email ?? "";

            // 4. Build the full payload for the Python agent
            var agentPayload = new
            {
                report_type = dto.ReportType ?? "performance",
                start_date = dto.StartDate ?? "",
                end_date = dto.EndDate ?? "",
                department = dto.Department ?? "All Departments",
                company_id = companyId.ToString(),
                manager_id = userId.ToString(),
                manager_name = managerName,
                manager_email = managerEmail,
                run_date = now.ToString("yyyy-MM-dd"),
                tasks = taskList,
                total_tasks = taskList.Count,
                total_employees = taskList.Select(t => t.employee_id).Distinct().Count()
            };

            var inputJson = Newtonsoft.Json.JsonConvert.SerializeObject(agentPayload);
            string outputJson;

            // 5. Call the Agent 4 Python FastAPI server
            string agentUrl = System.Configuration.ConfigurationManager.AppSettings["AgentReportUrl"]
                              ?? "http://localhost:8004/api/report/generate";
            try
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(180);
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
            catch (Exception ex)
            {
                return InternalServerError(new Exception(
                    "Could not reach the AI Report Agent: " + ex.Message
                    + ". Start the agent: uvicorn agent4_Report_Agent.server:app --port 8004"));
            }

            // 6. Persist to AIActionsLog (ActionType 4 = ReportGeneration)
            db.AIActionsLog.Add(new AIActionLog
            {
                ActionType = 4,
                TaskId = null,
                UserId = userId,
                InputData = inputJson,
                OutputData = outputJson,
                ModelVersion = "llama-3.3-70b-versatile",
                Timestamp = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var result = Newtonsoft.Json.JsonConvert.DeserializeObject(outputJson);
            return Ok(result);
        }

        [HttpGet, Route("")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> GetAll(int page = 1, int pageSize = 25, byte? reportType = null)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var query = db.Reports
                .Include(r => r.GeneratedByUser)
                .Where(r => r.CompanyId == companyId);

            if (reportType.HasValue)
                query = query.Where(r => r.ReportType == reportType.Value);

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(r => r.GeneratedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new PagedResultDto<ReportResponseDto>
            {
                Items = items.Select(MapReport).ToList(),
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
            var report = await db.Reports
                .Include(r => r.GeneratedByUser)
                .FirstOrDefaultAsync(r => r.ReportId == id && r.CompanyId == companyId);

            if (report == null) return NotFound();
            return Ok(MapReport(report));
        }

        private ReportResponseDto MapReport(Report r)
        {
            return new ReportResponseDto
            {
                ReportId = r.ReportId,
                GeneratedByName = r.GeneratedByUser != null
                    ? (r.GeneratedByUser.FullName ?? (r.GeneratedByUser.FirstName + " " + r.GeneratedByUser.LastName))
                    : null,
                ReportType = r.ReportType,
                ReportPeriodStart = r.ReportPeriodStart,
                ReportPeriodEnd = r.ReportPeriodEnd,
                FilePath = r.FilePath,
                MetadataJson = r.MetadataJson,
                GeneratedAt = r.GeneratedAt
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}