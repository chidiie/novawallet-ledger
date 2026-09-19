using NovaWallet.Domain.Ledger;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Application.Abstractions;

/// <summary>
/// Reads and writes wallets and their ledger entries.
/// </summary>
/// <remarks>
/// Deliberately does NOT expose transfers. A transfer needs two wallets locked
/// together inside one transaction, which is a different shape of operation
/// from "fetch this row" — see <see cref="ITransferExecutor"/>.
/// </remarks>
public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(WalletId walletId, CancellationToken cancellationToken);

    Task<bool> ExistsForCustomerAsync(string customerId, CancellationToken cancellationToken);

    Task AddAsync(Wallet wallet, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a credit to a single wallet, writing the balance change, the
    /// ledger entry and the audit entry in one transaction.
    /// </summary>
    Task CreditAsync(CreditInstruction instruction, CancellationToken cancellationToken);

    /// <summary>
    /// A page of ledger entries for a wallet, newest first.
    /// </summary>
    Task<IReadOnlyList<LedgerEntry>> GetStatementAsync(
        WalletId walletId, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountStatementEntriesAsync(
        WalletId walletId, CancellationToken cancellationToken);
}