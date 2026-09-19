using NovaWallet.Domain.Common;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Domain.Auditing;

/// <summary>
/// An append-only record of a balance mutation, kept separately from the ledger.
/// The ledger is what a customer sees; this is what an auditor sees.
/// </summary>
/// <remarks>
/// Immutability is enforced structurally: there is no public constructor, no
/// setter, and no method that changes an existing instance. Nothing in the
/// codebase can modify an audit row because there is no code path that does so.
/// The database grants reinforce this — see the README.
/// </remarks>
public sealed class AuditLogEntry
{
    public Guid Id { get; private set; }

    public WalletId WalletId { get; private set; }

    public AuditEventType EventType { get; private set; }

    public Money Amount { get; private set; }

    public Money BalanceBefore { get; private set; }

    public Money BalanceAfter { get; private set; }

    /// <summary>Who caused this change — the subject claim from the caller's token.</summary>
    public string Actor { get; private set; }

    /// <summary>Ties this entry to a single inbound HTTP request.</summary>
    public string CorrelationId { get; private set; }

    /// <summary>Ties this entry to the ledger entries it accompanies.</summary>
    public string Reference { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    private AuditLogEntry() // EF Core
    {
        Actor = null!;
        CorrelationId = null!;
        Reference = null!;
    }

    public static AuditLogEntry Record(
        WalletId walletId,
        AuditEventType eventType,
        Money amount,
        Money balanceBefore,
        Money balanceAfter,
        string actor,
        string correlationId,
        string reference,
        DateTime occurredAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            EventType = eventType,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            Actor = actor,
            CorrelationId = correlationId,
            Reference = reference,
            OccurredAtUtc = occurredAtUtc
        };
}