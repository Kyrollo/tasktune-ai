using System;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;
using TaskTuneAPI.Models.DTOs;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/companies")]
    public class CompaniesController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("current")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetCurrent()
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var company = await db.Companies.FindAsync(companyId);
            if (company == null) return NotFound();

            return Ok(new
            {
                company.CompanyId,
                company.CompanyName,
                company.SettingsJson,
                company.CreatedAt,
                company.UpdatedAt
            });
        }

        [HttpPut, Route("current")]
        [JwtAuthorize(RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> UpdateCurrent([FromBody] UpdateCompanyDto dto)
        {
            if (dto == null) return BadRequest();

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var company = await db.Companies.FindAsync(companyId);
            if (company == null) return NotFound();

            if (!string.IsNullOrEmpty(dto.CompanyName)) company.CompanyName = dto.CompanyName;
            if (dto.SettingsJson != null) company.SettingsJson = dto.SettingsJson;
            company.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new
            {
                company.CompanyId,
                company.CompanyName,
                company.SettingsJson,
                company.UpdatedAt
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    public class UpdateCompanyDto
    {
        public string CompanyName { get; set; }
        public string SettingsJson { get; set; }
    }
}