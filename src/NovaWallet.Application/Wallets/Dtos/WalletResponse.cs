namespace NovaWallet.Application.Wallets.Dtos;

public sealed record WalletResponse(
    Guid WalletId,
    string CustomerId,
    long BalanceKobo,
    string Currency,
    DateTime CreatedAtUtc);