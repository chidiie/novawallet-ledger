namespace NovaWallet.Application.Wallets.Dtos;

public sealed record TransferRequest(
    Guid DestinationWalletId,
    long AmountKobo,
    string? Narration);