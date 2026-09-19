using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NovaWallet.Application.Abstractions;

namespace NovaWallet.Infrastructure.Authentication;

/// <summary>
/// Resolves the caller from the validated bearer token. Throws rather than
/// returning empty: an unauthenticated request should never have reached a
/// handler, so a missing claim is a bug, not a normal path.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    public const string CorrelationIdItemKey = "NovaWallet.CorrelationId";

    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public string CustomerId =>
        _accessor.HttpContext?.User.FindFirstValue(NovaWalletClaims.CustomerId)
        ?? _accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException(
            "No authenticated customer on the current request.");

    public string CorrelationId =>
        _accessor.HttpContext?.Items[CorrelationIdItemKey] as string
        ?? "no-correlation-id";
}