using NovaWallet.Application.Common;
using NovaWallet.Application.Wallets.Dtos;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NovaWallet.IntegrationTests;

/// <summary>Thin wrapper over HttpClient so tests read like scenarios, not plumbing.</summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    public async Task<ApiClient> AuthenticateAsAsync(string customerId)
    {
        var response = await _http.PostAsJsonAsync(
            "/api/auth/token", new { customerId });

        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenPayload>();

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        return this;
    }

    public async Task<Guid> CreateWalletAsync()
    {
        var response = await _http.PostAsync("/api/wallets", null);
        response.EnsureSuccessStatusCode();

        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        return wallet!.WalletId;
    }

    public async Task CreditAsync(Guid walletId, long amountKobo)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/wallets/{walletId}/credit",
            new { amountKobo, narration = "test funding", externalReference = (string?)null });

        response.EnsureSuccessStatusCode();
    }

    public async Task<long> GetBalanceKoboAsync(Guid walletId)
    {
        var response = await _http.GetAsync($"/api/wallets/{walletId}/balance");
        response.EnsureSuccessStatusCode();

        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        return balance!.BalanceKobo;
    }

    public Task<HttpResponseMessage> TransferAsync(
        Guid sourceWalletId, Guid destinationWalletId, long amountKobo, string idempotencyKey)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/wallets/{sourceWalletId}/transfers")
        {
            Content = JsonContent.Create(
                new { destinationWalletId, amountKobo, narration = (string?)null })
        };

        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return _http.SendAsync(request);
    }

    public async Task<PagedResult<StatementEntryResponse>> GetStatementAsync(
    Guid walletId, int page = 1, int pageSize = 20)
    {
        var response = await _http.GetAsync(
            $"/api/wallets/{walletId}/statement?page={page}&pageSize={pageSize}");

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<PagedResult<StatementEntryResponse>>())!;
    }

    private sealed record TokenPayload(string AccessToken, string TokenType, int ExpiresInSeconds);
}