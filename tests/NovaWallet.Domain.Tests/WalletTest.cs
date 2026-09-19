using FluentAssertions;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Domain.Tests;

public class WalletTests
{
    private static readonly DateTime Now = new(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);

    private static Wallet AWalletHolding(long kobo)
    {
        var wallet = Wallet.Create("customer-1", Now);

        if (kobo > 0)
        {
            wallet.Credit(Money.FromKobo(kobo));
        }

        return wallet;
    }

    [Fact]
    public void A_new_wallet_starts_empty_and_in_naira()
    {
        var wallet = Wallet.Create("customer-1", Now);

        wallet.Balance.Should().Be(Money.Zero);
        wallet.Currency.Should().Be("NGN");
        wallet.CustomerId.Should().Be("customer-1");
    }

    [Fact]
    public void Create_requires_a_customer_id()
    {
        var act = () => Wallet.Create("   ", Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Credit_increases_the_balance()
    {
        var wallet = AWalletHolding(0);

        wallet.Credit(Money.FromNaira(1_000));

        wallet.Balance.Should().Be(Money.FromKobo(100_000));
    }

    [Fact]
    public void Credit_rejects_a_zero_amount()
    {
        var wallet = AWalletHolding(0);

        var act = () => wallet.Credit(Money.Zero);

        act.Should().Throw<InvalidAmountException>();
    }

    [Fact]
    public void Debit_reduces_the_balance()
    {
        var wallet = AWalletHolding(100_000);

        wallet.Debit(Money.FromKobo(40_000));

        wallet.Balance.Should().Be(Money.FromKobo(60_000));
    }

    [Fact]
    public void Debit_of_the_entire_balance_is_allowed()
    {
        var wallet = AWalletHolding(100_000);

        wallet.Debit(Money.FromKobo(100_000));

        wallet.Balance.Should().Be(Money.Zero);
    }

    [Fact]
    public void Debit_beyond_the_balance_is_refused_and_leaves_the_balance_untouched()
    {
        var wallet = AWalletHolding(100_000);

        var act = () => wallet.Debit(Money.FromKobo(100_001));

        act.Should().Throw<InsufficientFundsException>()
            .Which.Balance.Should().Be(Money.FromKobo(100_000));

        wallet.Balance.Should().Be(Money.FromKobo(100_000));
    }

    [Fact]
    public void Debit_rejects_a_zero_amount()
    {
        var wallet = AWalletHolding(100_000);

        var act = () => wallet.Debit(Money.Zero);

        act.Should().Throw<InvalidAmountException>();
    }
}