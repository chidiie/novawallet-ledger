using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NovaWallet.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations at startup. Required by the brief's "single
/// docker compose up" constraint — the panel will not run a migration command.
/// </summary>
/// <remarks>
/// Fine for this service. On a multi-replica deployment you would run migrations
/// as a separate job instead, so several instances don't race to migrate at once.
/// Noted in the README.
/// </remarks>
public sealed class DatabaseMigrator : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(IServiceProvider services, ILogger<DatabaseMigrator> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();

        _logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Database migrations applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}