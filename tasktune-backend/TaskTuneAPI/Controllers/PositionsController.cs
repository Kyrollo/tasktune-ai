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
    [RoutePrefix("api/positions")]
    public class PositionsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetAll(int? departmentId = null)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var query = db.Positions
                .Include(p => p.Department)
                .Where(p => p.CompanyId == companyId);

            if (departmentId.HasValue)
                query = query.Where(p => p.DepartmentId == departmentId.Value);

            var positions = await query.OrderBy(p => p.HierarchyLevel).ToListAsync();

            return Ok(positions.Select(p => new PositionResponseDto
            {
                PositionId = p.PositionId,
                PositionTitle = p.PositionTitle,
                DepartmentId = p.DepartmentId,
                DepartmentName = p.Department?.DepartmentName,
                HierarchyLevel = p.HierarchyLevel,
                ReportsToPositionId = p.ReportsToPositionId
            }).ToList());
        }

        [HttpPost, Route("")]
        [JwtAuthorize(RoleNames.Admin, RoleNames.CEO)]
        public async Task<IHttpActionResult> Create([FromBody] CreatePositionDto dto)
        {
            if (dto == null) return BadRequest();

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var position = new Position
            {
                CompanyId = companyId,
                DepartmentId = dto.DepartmentId,
                PositionTitle = dto.PositionTitle,
                HierarchyLevel = dto.HierarchyLevel,
                Responsibilities = dto.Responsibilities,
                ReportsToPositionId = dto.ReportsToPositionId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Positions.Add(position);
            await db.SaveChangesAsync();

            return Created($"api/positions/{position.PositionId}", new PositionResponseDto
            {
                PositionId = position.PositionId,
                PositionTitle = position.PositionTitle,
                DepartmentId = position.DepartmentId,
                HierarchyLevel = position.HierarchyLevel,
                ReportsToPositionId = position.ReportsToPositionId
            });
        }

        [HttpPut, Route("{id:int}")]
        [JwtAuthorize(RoleNames.Admin, RoleNames.CEO)]
        public async Task<IHttpActionResult> Update(int id, [FromBody] UpdatePositionDto dto)
        {
            if (dto == null) return BadRequest();

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var position = await db.Positions
                .FirstOrDefaultAsync(p => p.PositionId == id && p.CompanyId == companyId);

            if (position == null) return NotFound();

            if (!string.IsNullOrEmpty(dto.PositionTitle)) position.PositionTitle = dto.PositionTitle;
            if (dto.DepartmentId.HasValue) position.DepartmentId = dto.DepartmentId.Value;
            if (dto.HierarchyLevel.HasValue) position.HierarchyLevel = dto.HierarchyLevel.Value;
            if (dto.Responsibilities != null) position.Responsibilities = dto.Responsibilities;
            if (dto.ReportsToPositionId.HasValue) position.ReportsToPositionId = dto.ReportsToPositionId;
            position.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new PositionResponseDto
            {
                PositionId = position.PositionId,
                PositionTitle = position.PositionTitle,
                DepartmentId = position.DepartmentId,
                HierarchyLevel = position.HierarchyLevel,
                ReportsToPositionId = position.ReportsToPositionId
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    public class CreatePositionDto
    {
        public int DepartmentId { get; set; }
        public string PositionTitle { get; set; }
        public int HierarchyLevel { get; set; }
        public string Responsibilities { get; set; }
        public int? ReportsToPositionId { get; set; }
    }

    public class UpdatePositionDto
    {
        public int? DepartmentId { get; set; }
        public string PositionTitle { get; set; }
        public int? HierarchyLevel { get; set; }
        public string Responsibilities { get; set; }
        public int? ReportsToPositionId { get; set; }
    }
}