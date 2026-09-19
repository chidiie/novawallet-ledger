using NovaWallet.Domain.Wallets;

namespace NovaWallet.Domain.Exceptions;

public sealed class WalletNotFoundException : DomainException
{
    public WalletId WalletId { get; }

    public WalletNotFoundException(WalletId walletId)
        : base($"Wallet {walletId} was not found.")
        => WalletId = walletId;
}