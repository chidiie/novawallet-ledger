using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NovaWallet.Infrastructure.Authentication;

namespace NovaWallet.Api.Controllers;

/// <summary>
/// MOCK token issuance, for assessment purposes only. See JwtTokenIssuer.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly ITokenIssuer _issuer;

    public AuthController(ITokenIssuer issuer) => _issuer = issuer;

    /// <summary>Issues a bearer token for a customer id. No password required — this is a stand-in for FirstBank's real identity provider.</summary>
    /// <response code="200">A signed JWT valid for the configured lifetime.</response>
    [HttpPost("token")]
    [EnableRateLimiting(RateLimitPolicies.TokenIssuance)]
    [ProducesResponseType(typeof(IssuedToken), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IssuedToken> IssueToken([FromBody] TokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "A customer id is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(_issuer.Issue(request.CustomerId));
    }
}

/// <param name="CustomerId">Any identifier. In production this would be the subject of a verified KYC record.</param>
public sealed record TokenRequest(string CustomerId);