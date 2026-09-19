using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NovaWallet.Application.Abstractions;
using NovaWallet.Domain.Auditing;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Ledger;
using NovaWallet.Domain.Wallets;
using NovaWallet.Infrastructure.Persistence.Entities;

namespace NovaWallet.Infrastructure.Persistence;

/// <summary>
/// Moves funds between two wallets atomically and safely under concurrency.
/// </summary>
/// <remarks>
/// THE CONCURRENCY ARGUMENT, in full:
///
/// 1. Everything happens in ONE transaction. Either every effect lands or none does.
///
/// 2. Both wallet rows are locked with SELECT ... FOR UPDATE. This is a genuine
///    row-level exclusive lock: a second transaction reaching the same row blocks
///    until the first commits or rolls back. It cannot read a stale balance,
///    because it cannot read at all.
///
/// 3. The locks are taken in ASCENDING WALLET ID ORDER, never in source-then-
///    destination order. Two transfers A→B and B→A therefore both want the lower
///    id first, so one waits for the other instead of each holding what the other
///    needs. No lock cycle can form, so no deadlock is possible.
///
/// 4. Balances are re-read AFTER the locks are held. The handler's earlier read
///    is treated as worthless — by the time we get here it may be minutes stale.
///
/// 5. The daily-limit row is updated in the same transaction, so two concurrent
///    transfers cannot both slip under the limit.
///
/// 6. A CHECK (balance_kobo >= 0) constraint in the database is the final
///    backstop. If every argument above were somehow wrong, the write still fails.
/// </remarks>
public sealed class TransferExecutor : ITransferExecutor
{
    private readonly NovaWalletDbContext _db;
    private readonly ILogger<TransferExecutor> _logger;

    public TransferExecutor(NovaWalletDbContext db, ILogger<TransferExecutor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TransferOutcome> ExecuteAsync(
        TransferInstruction instruction, CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // STEP 1 — lock both rows, lowest id first.
            var (first, second) = OrderForLocking(
                instruction.SourceWalletId, instruction.DestinationWalletId);

            var lockedFirst = await LockWalletAsync(first, cancellationToken);
            var lockedSecond = await LockWalletAsync(second, cancellationToken);

            // Re-associate the locked rows with their roles in this transfer.
            var source = lockedFirst.Id == instruction.SourceWalletId
                ? lockedFirst : lockedSecond;
            var destination = lockedFirst.Id == instruction.DestinationWalletId
                ? lockedFirst : lockedSecond;

            // STEP 2 — daily limit, under the source wallet's lock.
            await EnforceDailyLimitAsync(instruction, cancellationToken);

            // STEP 3 — the balance change. Wallet.Debit re-checks sufficiency
            // against the freshly locked balance and throws if it falls short.
            var sourceBefore = source.Balance;
            var destinationBefore = destination.Balance;

            source.Debit(instruction.Amount);
            destination.Credit(instruction.Amount);

            // STEP 4 — ledger entries, one per side, sharing a reference.
            _db.LedgerEntries.Add(LedgerEntry.ForDebit(
                source.Id, instruction.Amount, source.Balance,
                instruction.Reference, instruction.Narration, instruction.OccurredAtUtc));

            _db.LedgerEntries.Add(LedgerEntry.ForCredit(
                destination.Id, instruction.Amount, destination.Balance,
                instruction.Reference, instruction.Narration, instruction.OccurredAtUtc));

            // STEP 5 — audit trail, both sides.
            _db.AuditLogs.Add(AuditLogEntry.Record(
                source.Id, AuditEventType.TransferDebited, instruction.Amount,
                sourceBefore, source.Balance, instruction.Actor,
                instruction.CorrelationId, instruction.Reference, instruction.OccurredAtUtc));

            _db.AuditLogs.Add(AuditLogEntry.Record(
                destination.Id, AuditEventType.TransferCredited, instruction.Amount,
                destinationBefore, destination.Balance, instruction.Actor,
                instruction.CorrelationId, instruction.Reference, instruction.OccurredAtUtc));

            // STEP 6 — commit everything, or nothing.
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Transfer {Reference} moved {AmountKobo} kobo from {SourceWalletId} to {DestinationWalletId}",
                instruction.Reference, instruction.Amount.Kobo,
                source.Id, destination.Id);

            return new TransferOutcome(
                instruction.Reference, source.Balance, instruction.OccurredAtUtc);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// The deadlock-prevention rule, isolated so it can be tested and pointed at.
    /// Ordering is by wallet id and NOTHING else — never by which wallet is
    /// sending, because that varies per request and is exactly what creates cycles.
    /// </summary>
    private static (WalletId First, WalletId Second) OrderForLocking(
        WalletId source, WalletId destination) =>
        source.CompareTo(destination) <= 0
            ? (source, destination)
            : (destination, source);

    /// <summary>
    /// Loads a wallet under a row-level exclusive lock.
    /// </summary>
    /// <remarks>
    /// FromSqlInterpolated is used because EF Core has no LINQ expression for
    /// FOR UPDATE. The interpolation is parameterised by EF, not concatenated,
    /// so this is not a SQL-injection surface.
    /// </remarks>
    private async Task<Wallet> LockWalletAsync(
    WalletId walletId, CancellationToken cancellationToken)
    {
        var wallet = await _db.Wallets
            .FromSqlInterpolated(
                $"SELECT * FROM wallets WHERE id = {walletId.Value} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        return wallet ?? throw new WalletNotFoundException(walletId);
    }

    /// <summary>
    /// Checks and increments the wallet's outbound total for the WAT day.
    /// </summary>
    private async Task EnforceDailyLimitAsync(
        TransferInstruction instruction, CancellationToken cancellationToken)
    {
        var total = await _db.DailyTransferTotals
            .FirstOrDefaultAsync(
                d => d.WalletId == instruction.SourceWalletId
                     && d.WatDate == instruction.WatDate,
                cancellationToken);

        var alreadySent = Money.FromKobo(total?.TotalSentKobo ?? 0);
        var wouldBeTotal = alreadySent + instruction.Amount;
        var limit = WalletLimits.DailyOutboundLimit;

        if (wouldBeTotal > limit)
        {
            throw new DailyLimitExceededException(
                instruction.SourceWalletId, alreadySent, instruction.Amount, limit);
        }

        if (total is null)
        {
            _db.DailyTransferTotals.Add(new DailyTransferTotal
            {
                WalletId = instruction.SourceWalletId,
                WatDate = instruction.WatDate,
                TotalSentKobo = instruction.Amount.Kobo
            });
        }
        else
        {
            total.TotalSentKobo = wouldBeTotal.Kobo;
        }
    }
}