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
    [RoutePrefix("api/departments")]
    public class DepartmentsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetAll()
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var departments = await db.Departments
                .Include(d => d.Users)
                .Where(d => d.CompanyId == companyId)
                .OrderBy(d => d.DepartmentName)
                .ToListAsync();

            return Ok(departments.Select(d => new DepartmentResponseDto
            {
                DepartmentId = d.DepartmentId,
                DepartmentName = d.DepartmentName,
                Description = d.Description,
                ParentDepartmentId = d.ParentDepartmentId,
                EmployeeCount = d.Users?.Count(u => u.IsActive) ?? 0
            }).ToList());
        }

        [HttpPost, Route("")]
        [JwtAuthorize(RoleNames.Admin, RoleNames.CEO)]
        public async Task<IHttpActionResult> Create([FromBody] CreateDepartmentDto dto)
        {
            if (dto == null) return BadRequest();

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var dept = new Department
            {
                CompanyId = companyId,
                DepartmentName = dto.DepartmentName,
                Description = dto.Description,
                ParentDepartmentId = dto.ParentDepartmentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Departments.Add(dept);
            await db.SaveChangesAsync();

            return Created($"api/departments/{dept.DepartmentId}", new DepartmentResponseDto
            {
                DepartmentId = dept.DepartmentId,
                DepartmentName = dept.DepartmentName,
                Description = dept.Description,
                ParentDepartmentId = dept.ParentDepartmentId,
                EmployeeCount = 0
            });
        }

        [HttpPut, Route("{id:int}")]
        [JwtAuthorize(RoleNames.Admin, RoleNames.CEO)]
        public async Task<IHttpActionResult> Update(int id, [FromBody] UpdateDepartmentDto dto)
        {
            if (dto == null) return BadRequest();

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var dept = await db.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == id && d.CompanyId == companyId);

            if (dept == null) return NotFound();

            if (!string.IsNullOrEmpty(dto.DepartmentName)) dept.DepartmentName = dto.DepartmentName;
            if (dto.Description != null) dept.Description = dto.Description;
            if (dto.ParentDepartmentId.HasValue) dept.ParentDepartmentId = dto.ParentDepartmentId;
            dept.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new DepartmentResponseDto
            {
                DepartmentId = dept.DepartmentId,
                DepartmentName = dept.DepartmentName,
                Description = dept.Description,
                ParentDepartmentId = dept.ParentDepartmentId
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    public class CreateDepartmentDto
    {
        public string DepartmentName { get; set; }
        public string Description { get; set; }
        public int? ParentDepartmentId { get; set; }
    }

    public class UpdateDepartmentDto
    {
        public string DepartmentName { get; set; }
        public string Description { get; set; }
        public int? ParentDepartmentId { get; set; }
    }
}