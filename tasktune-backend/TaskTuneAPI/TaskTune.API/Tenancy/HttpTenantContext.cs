using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Tenancy;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;
    public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public int? CompanyId
    {
        get
        {
            var value = _accessor.HttpContext?.User.FindFirst("companyId")?.Value;
            return int.TryParse(value, out var companyId) && companyId > 0 ? companyId : null;
        }
    }
}
