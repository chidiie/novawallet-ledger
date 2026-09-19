using NovaWallet.Infrastructure.Authentication;
using Serilog.Context;

namespace NovaWallet.Api.Middleware;

/// <summary>
/// Gives every request a correlation id, pushes it into the Serilog LogContext
/// so all downstream log lines carry it, and echoes it to the caller.
/// </summary>
/// <remarks>
/// Placed first so nothing — including the exception handler — runs without one.
/// The id also reaches the audit trail via ICurrentUser.CorrelationId, so a row
/// in audit_logs can be traced back to the exact HTTP request that caused it.
/// </remarks>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[CurrentUser.CorrelationIdItemKey] = correlationId;
        context.TraceIdentifier = correlationId;

        // Set the response header before the pipeline runs — once the response
        // has started, headers are immutable and this would throw.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var supplied))
        {
            var value = supplied.ToString();

            // Accept a caller-supplied id so a trace can span services, but
            // cap the length: this value ends up in logs and in the audit
            // table, and an unbounded header is a log-injection vector.
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= 64)
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}