namespace NovaWallet.Application.Wallets.Dtos;

public sealed record TransferResponse(
    string Reference,
    Guid SourceWalletId,
    Guid DestinationWalletId,
    long AmountKobo,
    long SourceBalanceAfterKobo,
    DateTime CompletedAtUtc);