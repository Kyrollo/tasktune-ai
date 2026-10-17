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
    [RoutePrefix("api/users")]
    public class UsersController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize(RoleNames.Admin, RoleNames.HR, RoleNames.Manager, RoleNames.CEO)]
        public async Task<IHttpActionResult> GetAll(int page = 1, int pageSize = 25, byte? role = null, bool? isActive = null, string search = null)
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var companyId = principal.GetCompanyId();

            var query = db.Users
                .Include(u => u.Department)
                .Include(u => u.Position)
                .Where(u => u.CompanyId == companyId);

            if (role.HasValue) query = query.Where(u => u.Role == role.Value);
            if (isActive.HasValue) query = query.Where(u => u.IsActive == isActive.Value);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.FirstName.Contains(search) || u.LastName.Contains(search) || u.Email.Contains(search));

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PagedResultDto<UserResponseDto>
            {
                Items = users.Select(MapUser).ToList(),
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpGet, Route("{id:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Get(int id)
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var companyId = principal.GetCompanyId();

            var user = await db.Users
                .Include(u => u.Company)
                .Include(u => u.Department)
                .Include(u => u.Position)
                .FirstOrDefaultAsync(u => u.UserId == id && u.CompanyId == companyId);

            if (user == null) return NotFound();
            return Ok(MapUser(user));
        }

        [HttpPost, Route("")]
        [JwtAuthorize(RoleNames.Admin)]
        public async Task<IHttpActionResult> Create(CreateUserDto dto)
        {
            if (dto == null) return BadRequest("Request body is required");
            if (await db.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("Email already exists");

            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var companyId = principal.GetCompanyId();

            var user = new User
            {
                CompanyId = companyId,
                DepartmentId = dto.DepartmentId,
                PositionId = dto.PositionId,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Phone = dto.Phone,
                Role = dto.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Created($"api/users/{user.UserId}", MapUser(user));
        }

        [HttpPut, Route("{id:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Update(int id, UpdateUserDto dto)
        {
            if (dto == null) return BadRequest();
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var companyId = principal.GetCompanyId();
            var currentUserId = principal.GetUserId();
            var currentRole = principal.GetRole();

            if (currentUserId != id && currentRole != RoleNames.Admin)
                return StatusCode(System.Net.HttpStatusCode.Forbidden);

            var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == id && u.CompanyId == companyId);
            if (user == null) return NotFound();

            if (dto.DepartmentId.HasValue) user.DepartmentId = dto.DepartmentId;
            if (dto.PositionId.HasValue) user.PositionId = dto.PositionId;
            if (!string.IsNullOrEmpty(dto.FirstName)) user.FirstName = dto.FirstName;
            if (!string.IsNullOrEmpty(dto.LastName)) user.LastName = dto.LastName;
            if (dto.Phone != null) user.Phone = dto.Phone;
            if (dto.Role.HasValue && currentRole == RoleNames.Admin) user.Role = dto.Role.Value;
            if (dto.IsActive.HasValue && currentRole == RoleNames.Admin) user.IsActive = dto.IsActive.Value;
            user.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Ok(MapUser(user));
        }

        [HttpDelete, Route("{id:int}")]
        [JwtAuthorize(RoleNames.Admin)]
        public async Task<IHttpActionResult> Deactivate(int id)
        {
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var companyId = principal.GetCompanyId();

            var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == id && u.CompanyId == companyId);
            if (user == null) return NotFound();

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return StatusCode(System.Net.HttpStatusCode.NoContent);
        }

        private UserResponseDto MapUser(User u)
        {
            return new UserResponseDto
            {
                UserId = u.UserId,
                CompanyId = u.CompanyId,
                CompanyName = u.Company?.CompanyName,
                DepartmentId = u.DepartmentId,
                DepartmentName = u.Department?.DepartmentName,
                PositionId = u.PositionId,
                PositionTitle = u.Position?.PositionTitle,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                FullName = u.FullName ?? (u.FirstName + " " + u.LastName),
                Phone = u.Phone,
                Role = u.Role,
                RoleName = RoleNames.GetName(u.Role),
                IsActive = u.IsActive,
                LastLogin = u.LastLogin,
                CreatedAt = u.CreatedAt
            };
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}