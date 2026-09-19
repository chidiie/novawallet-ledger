using Microsoft.EntityFrameworkCore;
using NovaWallet.Application.Abstractions;
using NovaWallet.Domain.Auditing;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Ledger;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Infrastructure.Persistence;

public sealed class WalletRepository : IWalletRepository
{
    private readonly NovaWalletDbContext _db;

    public WalletRepository(NovaWalletDbContext db) => _db = db;

    public async Task<Wallet?> GetByIdAsync(
        WalletId walletId, CancellationToken cancellationToken) =>
        await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken);

    public async Task<bool> ExistsForCustomerAsync(
        string customerId, CancellationToken cancellationToken) =>
        await _db.Wallets.AnyAsync(w => w.CustomerId == customerId, cancellationToken);

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        await _db.Wallets.AddAsync(wallet, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Credits a wallet under the same locking discipline as a transfer — one
    /// wallet rather than two, but the row is still locked and the balance
    /// re-read, because concurrent credits to one wallet race just as readily.
    /// </summary>
    public async Task CreditAsync(
        CreditInstruction instruction, CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var wallet = await _db.Wallets
                .FromSqlInterpolated(
                    $"SELECT * FROM wallets WHERE id = {instruction.WalletId.Value} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new WalletNotFoundException(instruction.WalletId);

            var before = wallet.Balance;
            wallet.Credit(instruction.Amount);

            _db.LedgerEntries.Add(LedgerEntry.ForCredit(
                wallet.Id, instruction.Amount, wallet.Balance,
                instruction.Reference, instruction.Narration, instruction.OccurredAtUtc));

            _db.AuditLogs.Add(AuditLogEntry.Record(
                wallet.Id, AuditEventType.WalletCredited, instruction.Amount,
                before, wallet.Balance, instruction.Actor,
                instruction.CorrelationId, instruction.Reference, instruction.OccurredAtUtc));

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetStatementAsync(
        WalletId walletId, int page, int pageSize, CancellationToken cancellationToken) =>
        await _db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == walletId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountStatementEntriesAsync(
        WalletId walletId, CancellationToken cancellationToken) =>
        await _db.LedgerEntries.CountAsync(e => e.WalletId == walletId, cancellationToken);
}