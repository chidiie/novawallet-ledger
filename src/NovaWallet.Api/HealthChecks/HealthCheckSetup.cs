using HealthChecks.NpgSql;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NovaWallet.Api.HealthChecks;

public static class HealthCheckSetup
{
    /// <summary>Tag marking checks that belong to readiness only.</summary>
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddNovaWalletHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NovaWallet")
            ?? throw new InvalidOperationException(
                "Connection string 'NovaWallet' is not configured.");

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: connectionString,
                // A trivial query: we are testing that a connection can be
                // opened and a round trip completed, not that any table exists.
                healthQuery: "SELECT 1;",
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { ReadinessTag },
                timeout: TimeSpan.FromSeconds(3));

        return services;
    }
}