namespace NovaWallet.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "novawallet-mock-issuer";
    public string Audience { get; init; } = "novawallet-api";

    /// <summary>
    /// Symmetric signing key. Supplied by configuration/environment, never
    /// committed. Must be at least 32 bytes for HS256.
    /// </summary>
    public string SigningKey { get; init; } = string.Empty;

    public int TokenLifetimeMinutes { get; init; } = 60;
}