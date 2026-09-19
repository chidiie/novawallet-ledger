namespace NovaWallet.Application.Wallets.Dtos;

public sealed record StatementEntryResponse(
    Guid EntryId,
    string Direction,
    long AmountKobo,
    long BalanceAfterKobo,
    string Reference,
    string? Narration,
    DateTime OccurredAtUtc);