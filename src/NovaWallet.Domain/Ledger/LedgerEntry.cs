using NovaWallet.Domain.Common;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Domain.Ledger;

/// <summary>
/// One movement of money against one wallet. This is the customer-facing
/// transaction history that backs the statement endpoint.
/// </summary>
public sealed class LedgerEntry
{
    public Guid Id { get; private set; }

    public WalletId WalletId { get; private set; }

    public LedgerDirection Direction { get; private set; }

    public Money Amount { get; private set; }

    /// <summary>The wallet's balance immediately after this entry was applied.</summary>
    public Money BalanceAfter { get; private set; }

    /// <summary>
    /// Groups related entries. A transfer produces two entries — one debit, one
    /// credit — sharing a reference, so both sides can be found together.
    /// </summary>
    public string Reference { get; private set; }

    public string? Narration { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    private LedgerEntry() => Reference = null!; // EF Core

    private LedgerEntry(
        WalletId walletId,
        LedgerDirection direction,
        Money amount,
        Money balanceAfter,
        string reference,
        string? narration,
        DateTime occurredAtUtc)
    {
        Id = Guid.NewGuid();
        WalletId = walletId;
        Direction = direction;
        Amount = amount.EnsurePositive();
        BalanceAfter = balanceAfter;
        Reference = reference;
        Narration = narration;
        OccurredAtUtc = occurredAtUtc;
    }

    public static LedgerEntry ForCredit(
        WalletId walletId, Money amount, Money balanceAfter,
        string reference, string? narration, DateTime occurredAtUtc) =>
        new(walletId, LedgerDirection.Credit, amount, balanceAfter,
            reference, narration, occurredAtUtc);

    public static LedgerEntry ForDebit(
        WalletId walletId, Money amount, Money balanceAfter,
        string reference, string? narration, DateTime occurredAtUtc) =>
        new(walletId, LedgerDirection.Debit, amount, balanceAfter,
            reference, narration, occurredAtUtc);
}