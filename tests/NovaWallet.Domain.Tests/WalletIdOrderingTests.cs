using FluentAssertions;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Domain.Tests;

public class WalletIdOrderingTests
{
    [Fact]
    public void Ordering_is_total_and_symmetric_so_lock_order_is_deterministic()
    {
        var a = WalletId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var b = WalletId.From(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        // Whichever direction a transfer runs, the ordering must agree.
        var forward = a.CompareTo(b);
        var backward = b.CompareTo(a);

        forward.Should().BeNegative();
        backward.Should().BePositive();
        (forward < 0).Should().Be(backward > 0);
    }

    [Fact]
    public void An_id_compares_equal_to_itself()
    {
        var id = WalletId.New();
        id.CompareTo(id).Should().Be(0);
    }
}