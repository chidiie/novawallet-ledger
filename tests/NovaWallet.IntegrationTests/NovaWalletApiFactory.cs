using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using NovaWallet.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace NovaWallet.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container.
/// </summary>
/// <remarks>
/// The container is real Postgres 16 — the same engine as production — because
/// the concurrency guarantees under test (SELECT ... FOR UPDATE, row locking,
/// CHECK constraints) simply do not exist in EF Core's in-memory provider.
/// </remarks>
public sealed class NovaWalletApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("novawallet_test")
        .WithUsername("novawallet")
        .WithPassword("test_password")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Must be set BEFORE the host is built — Program.cs reads configuration
        // during service registration, which happens before WebApplicationFactory's
        // own configuration callbacks are applied. The double underscore is the
        // standard separator for nested keys.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__NovaWallet", _postgres.GetConnectionString());

        // Touching Services here is what triggers the host build, so the
        // environment variable above must already be in place.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        await db.Database.MigrateAsync();
    }


    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(NovaWalletCollection))]
public sealed class NovaWalletCollection : ICollectionFixture<NovaWalletApiFactory>;