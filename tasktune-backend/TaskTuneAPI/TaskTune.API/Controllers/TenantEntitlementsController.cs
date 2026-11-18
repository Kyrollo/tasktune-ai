using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTune.API.Support;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/tenant/entitlements")]
[Authorize]
public sealed class TenantEntitlementsController : BaseApiController
{
    private readonly TenantEntitlementService _entitlements;

    public TenantEntitlementsController(TenantEntitlementService entitlements)
    {
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyEntitlements(CancellationToken cancellationToken)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null)
            return Unauthorized();

        var summary = await _entitlements.GetTenantSummaryAsync(companyId.Value, cancellationToken);
        return Ok(summary);
    }
}
