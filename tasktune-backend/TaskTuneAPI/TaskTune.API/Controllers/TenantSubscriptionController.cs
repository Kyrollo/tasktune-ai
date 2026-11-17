using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTune.API.Support;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
public sealed class TenantSubscriptionController : BaseApiController
{
    private readonly TenantEntitlementService _entitlements;

    public TenantSubscriptionController(TenantEntitlementService entitlements)
        => _entitlements = entitlements;

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        try
        {
            return Ok(await _entitlements.GetUsageSnapshotAsync(companyId.Value));
        }
        catch (TenantEntitlementException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        try
        {
            var snapshot = await _entitlements.GetUsageSnapshotAsync(companyId.Value);
            return Ok(snapshot);
        }
        catch (TenantEntitlementException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }
    }
}
