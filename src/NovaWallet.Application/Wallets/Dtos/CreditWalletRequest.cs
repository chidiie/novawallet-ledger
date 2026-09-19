namespace NovaWallet.Application.Wallets.Dtos;

public sealed record CreditWalletRequest(
    long AmountKobo,
    string? Narration,
    string? ExternalReference);