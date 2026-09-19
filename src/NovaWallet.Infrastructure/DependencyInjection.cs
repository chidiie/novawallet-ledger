using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NovaWallet.Application.Abstractions;
using NovaWallet.Infrastructure.Authentication;
using NovaWallet.Infrastructure.Persistence;
using NovaWallet.Infrastructure.Services;

namespace NovaWallet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NovaWallet")
            ?? throw new InvalidOperationException(
                "Connection string 'NovaWallet' is not configured.");

        services.AddDbContext<NovaWalletDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddHostedService<DatabaseMigrator>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IReferenceGenerator, ReferenceGenerator>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ITransferExecutor, TransferExecutor>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddHostedService<IdempotencyReaper>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddNovaWalletAuthentication(configuration);

        // IIdempotencyStore and IAuditLogWriter land in Stage 7.

        return services;
    }
}