namespace NovaWallet.Application.Wallets.Dtos;

/// <summary>
/// A wallet's balance. Carries kobo as the authoritative value; the formatted
/// Naira string is a convenience for display and must not be parsed back.
/// </summary>
public sealed record BalanceResponse(
    Guid WalletId,
    long BalanceKobo,
    string Currency,
    string FormattedBalance,
    DateTime AsOfUtc);