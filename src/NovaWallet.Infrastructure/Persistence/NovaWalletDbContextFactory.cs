using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NovaWallet.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core CLI when generating migrations. The connection
/// string here is never used at runtime — it just has to be well-formed so the
/// provider can build the model.
/// </summary>
public sealed class NovaWalletDbContextFactory
    : IDesignTimeDbContextFactory<NovaWalletDbContext>
{
    public NovaWalletDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NovaWalletDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=novawallet;Username=novawallet;Password=novawallet_dev_password")
            .Options;

        return new NovaWalletDbContext(options);
    }
}