using System.Net;
using FluentAssertions;

namespace NovaWallet.IntegrationTests;

[Collection(nameof(NovaWalletCollection))]
public class DailyLimitTests
{
    private const long Limit = 50_000_000; // NGN 500,000 in kobo

    private readonly NovaWalletApiFactory _factory;

    public DailyLimitTests(NovaWalletApiFactory factory) => _factory = factory;

    private ApiClient NewClient() => new(_factory.CreateClient());

    [Fact]
    public async Task Transfers_are_refused_once_the_daily_limit_is_reached()
    {
        var alice = await NewClient().AuthenticateAsAsync($"alice-{Guid.NewGuid():N}");
        var bob = await NewClient().AuthenticateAsAsync($"bob-{Guid.NewGuid():N}");

        var aliceWallet = await alice.CreateWalletAsync();
        var bobWallet = await bob.CreateWalletAsync();

        // Fund well beyond the limit so the ONLY thing that can stop the
        // transfer is the limit itself, not the balance.
        await alice.CreditAsync(aliceWallet, Limit * 3);

        // Send exactly the limit, in two halves.
        var first = await alice.TransferAsync(
            aliceWallet, bobWallet, Limit / 2, Guid.NewGuid().ToString());
        var second = await alice.TransferAsync(
            aliceWallet, bobWallet, Limit / 2, Guid.NewGuid().ToString());

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        // One kobo more must fail.
        var overLimit = await alice.TransferAsync(
            aliceWallet, bobWallet, 1, Guid.NewGuid().ToString());

        overLimit.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await bob.GetBalanceKoboAsync(bobWallet)).Should().Be(Limit);
    }
}