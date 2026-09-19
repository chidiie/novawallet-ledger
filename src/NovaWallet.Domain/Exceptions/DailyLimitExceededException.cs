using NovaWallet.Domain.Common;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Domain.Exceptions;

public sealed class DailyLimitExceededException : DomainException
{
    public WalletId WalletId { get; }
    public Money AlreadySentToday { get; }
    public Money Requested { get; }
    public Money DailyLimit { get; }

    public DailyLimitExceededException(
        WalletId walletId, Money alreadySentToday, Money requested, Money dailyLimit)
        : base($"Wallet {walletId} has sent {alreadySentToday.Kobo} kobo today. " +
               $"A further {requested.Kobo} kobo would exceed the daily outbound " +
               $"limit of {dailyLimit.Kobo} kobo.")
    {
        WalletId = walletId;
        AlreadySentToday = alreadySentToday;
        Requested = requested;
        DailyLimit = dailyLimit;
    }
}