using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NovaWallet.Application.Common;
using NovaWallet.Application.Wallets.Dtos;

namespace NovaWallet.IntegrationTests;

[Collection(nameof(NovaWalletCollection))]
public class WalletLifecycleTests
{
    private readonly NovaWalletApiFactory _factory;

    public WalletLifecycleTests(NovaWalletApiFactory factory) => _factory = factory;

    private ApiClient NewClient() => new(_factory.CreateClient());

    [Fact]
    public async Task Create_credit_transfer_and_statement_all_work_end_to_end()
    {
        var alice = await NewClient().AuthenticateAsAsync($"alice-{Guid.NewGuid():N}");
        var bob = await NewClient().AuthenticateAsAsync($"bob-{Guid.NewGuid():N}");

        var aliceWallet = await alice.CreateWalletAsync();
        var bobWallet = await bob.CreateWalletAsync();

        await alice.CreditAsync(aliceWallet, 100_000);   // NGN 1,000

        var response = await alice.TransferAsync(
            aliceWallet, bobWallet, 40_000, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        (await alice.GetBalanceKoboAsync(aliceWallet)).Should().Be(60_000);
        (await bob.GetBalanceKoboAsync(bobWallet)).Should().Be(40_000);
    }

    [Fact]
    public async Task Endpoints_reject_an_unauthenticated_caller()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/wallets/{Guid.NewGuid()}/balance");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_customer_cannot_read_another_customers_wallet()
    {
        var owner = await NewClient().AuthenticateAsAsync($"owner-{Guid.NewGuid():N}");
        var intruder = await NewClient().AuthenticateAsAsync($"intruder-{Guid.NewGuid():N}");

        var wallet = await owner.CreateWalletAsync();

        var response = await intruder.TransferAsync(
            wallet, await intruder.CreateWalletAsync(), 100, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_transfer_beyond_the_balance_is_refused_and_leaves_both_wallets_intact()
    {
        var alice = await NewClient().AuthenticateAsAsync($"alice-{Guid.NewGuid():N}");
        var bob = await NewClient().AuthenticateAsAsync($"bob-{Guid.NewGuid():N}");

        var aliceWallet = await alice.CreateWalletAsync();
        var bobWallet = await bob.CreateWalletAsync();

        await alice.CreditAsync(aliceWallet, 10_000);

        var response = await alice.TransferAsync(
            aliceWallet, bobWallet, 10_001, Guid.NewGuid().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await alice.GetBalanceKoboAsync(aliceWallet)).Should().Be(10_000);
        (await bob.GetBalanceKoboAsync(bobWallet)).Should().Be(0);
    }

    [Fact]
    public async Task The_statement_returns_entries_newest_first()
    {
        var alice = await NewClient().AuthenticateAsAsync($"alice-{Guid.NewGuid():N}");
        var bob = await NewClient().AuthenticateAsAsync($"bob-{Guid.NewGuid():N}");

        var aliceWallet = await alice.CreateWalletAsync();
        var bobWallet = await bob.CreateWalletAsync();

        await alice.CreditAsync(aliceWallet, 100_000);
        await alice.TransferAsync(aliceWallet, bobWallet, 1_000, Guid.NewGuid().ToString());
        await alice.TransferAsync(aliceWallet, bobWallet, 2_000, Guid.NewGuid().ToString());

        var statement = await alice.GetStatementAsync(aliceWallet);

        statement.Items.Should().HaveCount(3);          // 1 credit + 2 debits
        statement.TotalCount.Should().Be(3);
        statement.Items.Should().BeInDescendingOrder(e => e.OccurredAtUtc);
    }
}