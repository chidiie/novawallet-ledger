using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace NovaWallet.Infrastructure.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddNovaWalletAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Stop the handler rewriting "sub" into a ClaimTypes URI. Without this,
        // reading JwtRegisteredClaimNames.Sub from the principal returns null
        // and the cause is not obvious.
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it via configuration or the " +
                "Jwt__SigningKey environment variable.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes for HMAC-SHA256.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,

                    ValidateAudience = true,
                    ValidAudience = options.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(options.SigningKey)),

                    ValidateLifetime = true,

                    // Default is five minutes, which silently accepts tokens
                    // that expired up to five minutes ago. For a money API,
                    // expired means expired.
                    ClockSkew = TimeSpan.Zero,

                    // Reject "alg": "none" and algorithm-confusion attacks by
                    // accepting exactly one algorithm.
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
                };

                // Tokens carry customer identity; never write them to logs.
                jwt.SaveToken = false;
            });

        services.AddAuthorization();

        return services;
    }
}