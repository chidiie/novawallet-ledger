using NovaWallet.Domain.Wallets;

namespace NovaWallet.Infrastructure.Persistence.Entities;

/// <summary>
/// Running total of outbound transfers for one wallet on one WAT date.
/// Updated inside the transfer transaction so the limit cannot be raced.
/// </summary>
public sealed class DailyTransferTotal
{
    public WalletId WalletId { get; set; }

    /// <summary>The WAT calendar date, not UTC. See WestAfricaTime.</summary>
    public DateOnly WatDate { get; set; }

    public long TotalSentKobo { get; set; }
}