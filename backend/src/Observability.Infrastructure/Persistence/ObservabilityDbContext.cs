using Microsoft.EntityFrameworkCore;

namespace Observability.Infrastructure.Persistence;

/// <summary>
/// Phase 0 placeholder DbContext. Entities (Applications, Events, Errors, SafetyViolations, ApiKeys)
/// land in Phase 1.
/// </summary>
public class ObservabilityDbContext : DbContext
{
    public ObservabilityDbContext(DbContextOptions<ObservabilityDbContext> options) : base(options) { }
}
