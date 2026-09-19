using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NovaWallet.Application.Abstractions;
using NovaWallet.Infrastructure.Persistence.Entities;

namespace NovaWallet.Infrastructure.Persistence;

/// <summary>
/// Clears idempotency claims abandoned by a crashed request, so a key is not
/// locked out forever. Only InProgress records older than the timeout are
/// touched; Completed records are retained for replay.
/// </summary>
public sealed class IdempotencyReaper : BackgroundService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _services;
    private readonly ILogger<IdempotencyReaper> _logger;

    public IdempotencyReaper(IServiceProvider services, ILogger<IdempotencyReaper> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                var cutoff = clock.UtcNow - StaleAfter;

                var removed = await db.IdempotencyRecords
                    .Where(r => r.State == IdempotencyState.InProgress
                                && r.CreatedAtUtc < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (removed > 0)
                {
                    _logger.LogInformation(
                        "Reaped {Count} stale idempotency claims.", removed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Housekeeping must never take the service down.
                _logger.LogError(ex, "Idempotency reaper pass failed.");
            }
        }
    }
}