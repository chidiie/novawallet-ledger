using NovaWallet.Domain.Common;

namespace NovaWallet.Domain.Wallets;

/// <summary>
/// Server-side transfer limits. Held in the domain rather than in configuration
/// because it is a business rule, not a deployment knob — a client must never be
/// able to influence it.
/// </summary>
public static class WalletLimits
{
    /// <summary>
    /// Maximum total outbound transfer value per wallet per day (WAT),
    /// as required by the brief.
    /// </summary>
    public static Money DailyOutboundLimit => Money.FromNaira(500_000);
}