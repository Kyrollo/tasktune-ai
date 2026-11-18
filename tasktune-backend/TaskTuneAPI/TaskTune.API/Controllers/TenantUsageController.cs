using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTune.API.Support;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/tenant/usage")]
[Authorize]
public sealed class TenantUsageController(TenantUsageMeterService meter) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetCurrentMonth(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var selectedYear = year ?? now.Year;
        var selectedMonth = month ?? now.Month;
        if (selectedYear is < 2000 or > 2100 || selectedMonth is < 1 or > 12)
            return BadRequest(new { message = "Year must be between 2000 and 2100 and month must be between 1 and 12." });

        try
        {
            var summary = await meter.GetCurrentTenantSummaryAsync(selectedYear, selectedMonth, cancellationToken);
            return Ok(summary);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { message = "A valid tenant context is required." });
        }
    }
}
