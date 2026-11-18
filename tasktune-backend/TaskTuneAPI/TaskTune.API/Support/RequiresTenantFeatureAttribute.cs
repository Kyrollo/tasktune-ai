using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaskTune.API.Support;

/// <summary>
/// Server-side plan entitlement gate for a controller action. Always use with [Authorize].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresTenantFeatureAttribute(string featureCode) : Attribute, IAsyncAuthorizationFilter
{
    public string FeatureCode { get; } = featureCode;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var companyClaim = context.HttpContext.User.FindFirst("companyId")?.Value;
        if (!int.TryParse(companyClaim, out var companyId) || companyId <= 0)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var entitlements = context.HttpContext.RequestServices.GetRequiredService<TenantEntitlementService>();
        var access = await entitlements.GetFeatureAccessAsync(companyId, FeatureCode, context.HttpContext.RequestAborted);
        if (access.Enabled) return;

        context.Result = new ObjectResult(new
        {
            message = "This feature is not enabled for the current tenant subscription.",
            featureCode = access.FeatureCode,
            planCode = access.PlanCode,
            reason = access.Reason
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
