using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NovaWallet.Infrastructure.Authentication;

namespace NovaWallet.Api.RateLimiting;

public static class RateLimitingSetup
{
    public static IServiceCollection AddNovaWalletRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Defaults are the production values. Only the Testing environment
        // loosens them, so the concurrency test exercises the database rather
        // than bouncing off the limiter.
        var tokenLimit =
            configuration.GetValue("RateLimits:Transfers:TokenLimit", 10);
        var tokensPerPeriod =
            configuration.GetValue("RateLimits:Transfers:TokensPerPeriod", 5);
        var replenishmentSeconds =
            configuration.GetValue("RateLimits:Transfers:ReplenishmentPeriodSeconds", 10);

        var tokenIssuanceLimit =
            configuration.GetValue("RateLimits:TokenIssuance:PermitLimit", 10);
        var tokenIssuanceWindowSeconds =
            configuration.GetValue("RateLimits:TokenIssuance:WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // --- Transfers --------------------------------------------------
            // Partitioned per customer, not globally: one busy customer must not
            // be able to throttle everyone else. Falls back to remote IP if the
            // request somehow reaches here unauthenticated.
            options.AddPolicy(RateLimitPolicies.Transfers, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: ResolvePartitionKey(context),
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        // A token bucket rather than a fixed window: it allows a
                        // legitimate burst (paying several people at once) while
                        // capping the sustained rate. A fixed window would also
                        // permit double the limit across a window boundary.
                        TokenLimit = tokenLimit,
                        TokensPerPeriod = tokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(replenishmentSeconds),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));

            // --- Token issuance ---------------------------------------------
            // Unauthenticated and credential-minting, so partition by IP. This
            // is the most obvious abuse target in the service.
            options.AddPolicy(RateLimitPolicies.TokenIssuance, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = tokenIssuanceLimit,
                        Window = TimeSpan.FromSeconds(tokenIssuanceWindowSeconds),
                        QueueLimit = 0
                    }));

            // Rejections use the same Problem Details shape as every other
            // error rather than an empty 429 body.
            options.OnRejected = async (context, cancellationToken) =>
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Type = "https://novawallet.firstbank.ng/problems/rate-limit-exceeded",
                    Title = "Too many requests",
                    Detail = "Requests are being sent too quickly. Retry shortly.",
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["correlationId"] =
                    context.HttpContext.Items[CurrentUser.CorrelationIdItemKey] as string;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                    problem.Extensions["retryAfterSeconds"] = (int)retryAfter.TotalSeconds;
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            };
        });

        return services;
    }

    private static string ResolvePartitionKey(HttpContext context)
    {
        var customerId =
            context.User.FindFirstValue(NovaWalletClaims.CustomerId)
            ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return customerId is not null
            ? $"customer:{customerId}"
            : $"ip:{context.Connection.RemoteIpAddress}";
    }
}