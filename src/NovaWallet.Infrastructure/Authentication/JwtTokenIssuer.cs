using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NovaWallet.Application.Abstractions;

namespace NovaWallet.Infrastructure.Authentication;

/// <summary>
/// MOCK TOKEN ISSUER — FOR ASSESSMENT ONLY.
/// </summary>
/// <remarks>
/// This issues a signed JWT for any customer id presented to it. There is no
/// password, no user store, no BVN/NIN verification, no refresh token and no
/// revocation. It exists so the panel can obtain a bearer token and exercise
/// the protected endpoints.
///
/// In a real NovaPay deployment this endpoint would not exist: tokens would be
/// issued by FirstBank's identity provider after tiered KYC, and this service
/// would only VALIDATE them against that issuer's public keys (RS256/JWKS),
/// never mint them with a shared symmetric secret.
/// </remarks>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public IssuedToken Issue(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("A customer id is required.", nameof(customerId));
        }

        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.TokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customerId.Trim()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(NovaWalletClaims.CustomerId, customerId.Trim())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new IssuedToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            (int)(expires - now).TotalSeconds);
    }
}