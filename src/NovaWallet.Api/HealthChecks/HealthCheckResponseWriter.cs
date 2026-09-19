using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NovaWallet.Api.HealthChecks;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,

                // The exception message is deliberately omitted. A failed
                // Postgres check can surface the host, port and username in its
                // message, and these endpoints are typically unauthenticated.
                description = entry.Value.Status == HealthStatus.Healthy
                    ? null
                    : "Check failed. See service logs for detail."
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, Options));
    }
}