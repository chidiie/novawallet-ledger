using NovaWallet.Domain.Common;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Domain.Exceptions;

public sealed class InsufficientFundsException : DomainException
{
    public WalletId WalletId { get; }
    public Money Balance { get; }
    public Money Requested { get; }

    public InsufficientFundsException(WalletId walletId, Money balance, Money requested)
        : base($"Wallet {walletId} has a balance of {balance.Kobo} kobo, " +
               $"which cannot cover a debit of {requested.Kobo} kobo.")
    {
        WalletId = walletId;
        Balance = balance;
        Requested = requested;
    }
}