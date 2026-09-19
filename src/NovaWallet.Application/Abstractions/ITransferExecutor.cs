using NovaWallet.Domain.Common;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Application.Abstractions;

/// <summary>
/// Executes a transfer between two wallets atomically and safely under
/// concurrency.
/// </summary>
/// <remarks>
/// <para>
/// This is separate from <see cref="IWalletRepository"/> on purpose. A transfer
/// is not "load two wallets, change them, save" — that shape has a race between
/// the read and the write, no matter how carefully the handler is written.
/// The guarantee can only be made where the transaction and the row locks live,
/// so the whole operation crosses the boundary as ONE call and the
/// implementation owns the ordering.
/// </para>
/// <para>
/// The implementation must, in a single transaction: lock both wallet rows in a
/// deterministic order, re-read the balances under those locks, re-check the
/// daily limit, apply the debit and credit, and write both ledger entries and
/// the audit entry. See Stage 6.
/// </para>
/// </remarks>
public interface ITransferExecutor
{
    Task<TransferOutcome> ExecuteAsync(
        TransferInstruction instruction, CancellationToken cancellationToken);
}

/// <summary>Everything the executor needs, gathered by the handler beforehand.</summary>
public sealed record TransferInstruction(
    WalletId SourceWalletId,
    WalletId DestinationWalletId,
    Money Amount,
    string Reference,
    string? Narration,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc,
    DateOnly WatDate);

/// <summary>Everything the handler needs back to build a response.</summary>
public sealed record TransferOutcome(
    string Reference,
    Money SourceBalanceAfter,
    DateTime CompletedAtUtc);

/// <summary>A single-wallet credit, applied transactionally.</summary>
public sealed record CreditInstruction(
    WalletId WalletId,
    Money Amount,
    string Reference,
    string? Narration,
    string Actor,
    string CorrelationId,
    DateTime OccurredAtUtc);