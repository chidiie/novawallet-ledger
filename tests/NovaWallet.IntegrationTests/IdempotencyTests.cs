using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NovaWallet.Application.Wallets.Dtos;

namespace NovaWallet.IntegrationTests;

[Collection(nameof(NovaWalletCollection))]
public class IdempotencyTests
{
    private readonly NovaWalletApiFactory _factory;

    public IdempotencyTests(NovaWalletApiFactory factory) => _factory = factory;

    private ApiClient NewClient() => new(_factory.CreateClient());

    [Fact]
    public async Task Replaying_a_key_returns_the_same_result_and_moves_money_once()
    {
        var alice = await NewClient().AuthenticateAsAsync($"alice-{Guid.NewGuid():N}");
        var bob = await NewClient().AuthenticateAsAsync($"bob-{Guid.NewGuid():N}");

        var aliceWallet = await alice.CreateWalletAsync();
        var bobWallet = await bob.CreateWalletAsync();
        await alice.CreditAsync(aliceWallet, 100_000);

        var key = Guid.NewGuid().ToString();

        var first = await alice.TransferAsync(aliceWallet, bobWallet, 25_000, key);
        var second = await alice.TransferAsync(aliceWallet, bobWallet, 25_000, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstBody = await first.Content.ReadFromJsonAsync<TransferResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<TransferResponse>();

        // Identical reference proves the second call replayed rather than
        // executing a new transfer.
        secondBody!.Reference.Should().Be(firstBody!.Reference);

        // And the money moved exactly once.
        (await alice.GetBalanceKoboAsync(aliceWallet)).Should().Be(75_000);
        (await bob.GetBalanceKoboAsync(bobWallet)).Should().Be(25_000);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_payload_is_rejected()
    {
        var alice = await NewClient().AuthenticateAsAsync($"alice-{Guid.NewGuid():N}");
        var bob = await NewClient().AuthenticateAsAsync($"bob-{Guid.NewGuid():N}");

        var aliceWallet = await alice.CreateWalletAsync();
        var bobWallet = await bob.CreateWalletAsync();
        await alice.CreditAsync(aliceWallet, 100_000);

        var key = Guid.NewGuid().ToString();

        await alice.TransferAsync(aliceWallet, bobWallet, 10_000, key);
        var conflicting = await alice.TransferAsync(aliceWallet, bobWallet, 20_000, key);

        conflicting.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The second amount must not have been applied.
        (await alice.GetBalanceKoboAsync(aliceWallet)).Should().Be(90_000);
    }

    [Fact]
    public async Task A_missing_idempotency_key_is_rejected()
    {
        var alice = await NewClient().AuthenticateAsAsync($"alice-{Guid.NewGuid():N}");
        var aliceWallet = await alice.CreateWalletAsync();

        var response = await alice.TransferAsync(
            aliceWallet, Guid.NewGuid(), 1_000, string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}