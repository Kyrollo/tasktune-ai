namespace TaskTune.Infrastructure.Models;

/// <summary>Resolves the company attached to the authenticated request.</summary>
public interface ITenantContext
{
    int? CompanyId { get; }
}
