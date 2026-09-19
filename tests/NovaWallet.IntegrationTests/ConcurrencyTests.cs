using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaWallet.Domain.Wallets;
using NovaWallet.Infrastructure.Persistence;

namespace NovaWallet.IntegrationTests;

/// <summary>
/// The hard constraint from section 2.2: the balance must never go negative
/// under any interleaving of concurrent requests, and no double-spend is possible.
/// </summary>
[Collection(nameof(NovaWalletCollection))]
public class ConcurrencyTests
{
    private readonly NovaWalletApiFactory _factory;

    public ConcurrencyTests(NovaWalletApiFactory factory) => _factory = factory;

    private ApiClient NewClient() => new(_factory.CreateClient());

    /// <summary>
    /// Fires 50 concurrent transfers of NGN 100 against a wallet holding
    /// NGN 2,500 — enough for exactly 25 of them.
    /// </summary>
    /// <remarks>
    /// The assertions are EXACT, not approximate. "The balance didn't go
    /// negative" alone would pass even if money were destroyed or created; we
    /// assert that successes × amount equals the decrease in the source balance
    /// AND the increase in the destination balance, so the books balance.
    /// </remarks>
    [Fact]
    public async Task Fifty_concurrent_transfers_never_overdraw_and_never_lose_money()
    {
        const int attempts = 50;
        const long amountKobo = 10_000;          // NGN 100
        const long fundedKobo = 25 * amountKobo; // funds exactly 25 attempts

        var aliceCustomerId = $"alice-{Guid.NewGuid():N}";
        var bobCustomerId = $"bob-{Guid.NewGuid():N}";

        var alice = await NewClient().AuthenticateAsAsync(aliceCustomerId);
        var bob = await NewClient().AuthenticateAsAsync(bobCustomerId);

        var aliceWallet = await alice.CreateWalletAsync();
        var bobWallet = await bob.CreateWalletAsync();
        await alice.CreditAsync(aliceWallet, fundedKobo);

        // 50 independent clients, all authenticated as Alice, each with its own
        // idempotency key — so these are 50 genuinely distinct transfers, not
        // replays, and none is deduplicated.
        var clients = await Task.WhenAll(
            Enumerable.Range(0, attempts)
                .Select(_ => NewClient().AuthenticateAsAsync(aliceCustomerId)));

        // Hold every request at a gate, then release them together so they
        // contend as closely as possible.
        using var gate = new SemaphoreSlim(0, attempts);

        var tasks = clients.Select(async client =>
        {
            await gate.WaitAsync();
            return await client.TransferAsync(
                aliceWallet, bobWallet, amountKobo, Guid.NewGuid().ToString());
        }).ToArray();

        gate.Release(attempts);

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        var throttled = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        // Diagnostic: if this fails, show what actually came back rather than
        // just a count mismatch.
        var unexpected = responses
            .Where(r => r.StatusCode is not (HttpStatusCode.Created
                or HttpStatusCode.UnprocessableEntity
                or HttpStatusCode.TooManyRequests))
            .Select(r => r.StatusCode)
            .ToList();

        unexpected.Should().BeEmpty(
            "no request should fail in an unexpected way, but got: {0}",
            string.Join(", ", unexpected));

        (succeeded + rejected + throttled).Should().Be(attempts);

        var aliceBalance = await alice.GetBalanceKoboAsync(aliceWallet);
        var bobBalance = await bob.GetBalanceKoboAsync(bobWallet);

        // 1. NEVER NEGATIVE.
        aliceBalance.Should().BeGreaterThanOrEqualTo(0);

        // 2. NO MONEY CREATED OR DESTROYED.
        aliceBalance.Should().Be(fundedKobo - (succeeded * amountKobo));
        bobBalance.Should().Be(succeeded * amountKobo);
        (aliceBalance + bobBalance).Should().Be(fundedKobo);

        // 3. NO DOUBLE-SPEND — the balance could not fund more than 25.
        succeeded.Should().BeLessThanOrEqualTo(25);

        // 4. LEDGER RECONSTRUCTS THE BALANCE EXACTLY.
        await AssertLedgerMatchesBalanceAsync(aliceWallet, aliceBalance);
        await AssertLedgerMatchesBalanceAsync(bobWallet, bobBalance);
    }

    /// <summary>
    /// Two wallets transferring to each other simultaneously in opposite
    /// directions — the interleaving that deadlocks without a fixed lock order.
    /// </summary>
    [Fact]
    public async Task Opposing_concurrent_transfers_do_not_deadlock()
    {
        const int pairs = 20;
        const long amountKobo = 1_000;

        var aliceCustomerId = $"alice-{Guid.NewGuid():N}";
        var bobCustomerId = $"bob-{Guid.NewGuid():N}";

        var alice = await NewClient().AuthenticateAsAsync(aliceCustomerId);
        var bob = await NewClient().AuthenticateAsAsync(bobCustomerId);

        var aliceWallet = await alice.CreateWalletAsync();
        var bobWallet = await bob.CreateWalletAsync();

        await alice.CreditAsync(aliceWallet, 1_000_000);
        await bob.CreditAsync(bobWallet, 1_000_000);

        var aliceClients = await Task.WhenAll(Enumerable.Range(0, pairs)
            .Select(_ => NewClient().AuthenticateAsAsync(aliceCustomerId)));

        var bobClients = await Task.WhenAll(Enumerable.Range(0, pairs)
            .Select(_ => NewClient().AuthenticateAsAsync(bobCustomerId)));

        var aToB = aliceClients.Select(c =>
            c.TransferAsync(aliceWallet, bobWallet, amountKobo, Guid.NewGuid().ToString()));

        var bToA = bobClients.Select(c =>
            c.TransferAsync(bobWallet, aliceWallet, amountKobo, Guid.NewGuid().ToString()));

        var responses = await Task.WhenAll(aToB.Concat(bToA));

        // A deadlock surfaces as a 500 (PostgresException 40P01). There must be
        // none: ascending-wallet-id lock ordering makes a cycle impossible.
        responses.Should().NotContain(
            r => r.StatusCode == HttpStatusCode.InternalServerError,
            "a fixed lock ordering must prevent deadlocks between opposing transfers");

        // Whatever succeeded, the combined total is unchanged.
        var total = await alice.GetBalanceKoboAsync(aliceWallet)
                    + await bob.GetBalanceKoboAsync(bobWallet);

        total.Should().Be(2_000_000);
    }


    private async Task AssertLedgerMatchesBalanceAsync(Guid walletId, long expectedBalance)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();

        var id = WalletId.From(walletId);

        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == id)
            .ToListAsync();

        var net = entries.Sum(e =>
            e.Direction == Domain.Ledger.LedgerDirection.Credit
                ? e.Amount.Kobo
                : -e.Amount.Kobo);

        net.Should().Be(expectedBalance,
            "the ledger must reconstruct the balance exactly — no orphaned or missing entries");

        // Every balance mutation must also have an audit row.
        var auditCount = await db.AuditLogs.CountAsync(a => a.WalletId == id);
        auditCount.Should().Be(entries.Count,
            "every ledger entry must have a matching audit entry");
    }

    private static async Task<string> GetCustomerIdAsync(ApiClient client) =>
        await Task.FromResult("placeholder"); // see note below

    private static async Task<ApiClient[]> AuthenticateManyAsync(ApiClient owner, int count) =>
        await Task.WhenAll(Enumerable.Range(0, count)
            .Select(_ => Task.FromResult(owner)));
}