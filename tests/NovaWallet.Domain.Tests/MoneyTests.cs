using FluentAssertions;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Exceptions;

namespace NovaWallet.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void FromNaira_converts_to_kobo()
    {
        Money.FromNaira(500_000).Kobo.Should().Be(50_000_000);
    }

    [Fact]
    public void FromKobo_rejects_a_negative_amount()
    {
        var act = () => Money.FromKobo(-1);
        act.Should().Throw<InvalidAmountException>();
    }

    [Fact]
    public void Subtracting_more_than_is_held_throws_rather_than_going_negative()
    {
        var act = () => Money.FromKobo(100) - Money.FromKobo(101);
        act.Should().Throw<InvalidAmountException>();
    }

    [Fact]
    public void Addition_that_overflows_throws_rather_than_wrapping()
    {
        var act = () => Money.FromKobo(long.MaxValue) + Money.FromKobo(1);
        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void EnsurePositive_rejects_zero()
    {
        var act = () => Money.Zero.EnsurePositive();
        act.Should().Throw<InvalidAmountException>();
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(100, 100, true)]
    [InlineData(100, 101, false)]
    public void Two_amounts_are_equal_when_their_kobo_match(long a, long b, bool expected)
    {
        (Money.FromKobo(a) == Money.FromKobo(b)).Should().Be(expected);
    }

    [Fact]
    public void ToNaira_formats_for_display_without_losing_kobo()
    {
        Money.FromKobo(50_000_050).ToNaira().Should().Be(500_000.50m);
    }
}