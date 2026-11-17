using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaskTune.API.Support;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireModuleAttribute : TypeFilterAttribute
{
    public RequireModuleAttribute(string moduleCode)
        : base(typeof(RequireModuleFilter))
    {
        Arguments = [moduleCode];
    }
}

public sealed class RequireModuleFilter : IAsyncActionFilter
{
    private readonly string _moduleCode;
    private readonly TenantEntitlementService _entitlements;

    public RequireModuleFilter(string moduleCode, TenantEntitlementService entitlements)
    {
        _moduleCode = moduleCode;
        _entitlements = entitlements;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var companyClaim = context.HttpContext.User.FindFirst("companyId")?.Value;
        if (!int.TryParse(companyClaim, out var companyId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        try
        {
            await _entitlements.EnsureModuleEnabledAsync(companyId, _moduleCode);
        }
        catch (TenantEntitlementException ex)
        {
            context.Result = new ObjectResult(new
            {
                message = ex.Message,
                traceId = context.HttpContext.TraceIdentifier
            })
            {
                StatusCode = ex.StatusCode
            };
            return;
        }

        await next();
    }
}
