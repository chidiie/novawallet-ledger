using Serilog;
using Serilog.Events;

namespace NovaWallet.Api.Logging;

public static class RequestLoggingOptions
{
    public static void Configure(Serilog.AspNetCore.RequestLoggingOptions options)
    {
        options.MessageTemplate =
            "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        options.GetLevel = (httpContext, elapsed, exception) =>
        {
            // Health probes fire constantly; logging them at Information buries
            // everything else.
            if (httpContext.Request.Path.StartsWithSegments("/health"))
            {
                return LogEventLevel.Verbose;
            }

            if (exception is not null || httpContext.Response.StatusCode >= 500)
            {
                return LogEventLevel.Error;
            }

            return httpContext.Response.StatusCode >= 400
                ? LogEventLevel.Warning
                : LogEventLevel.Information;
        };

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());

            // Deliberately NOT logged: the Authorization header, the
            // Idempotency-Key, or any request body. Tokens are credentials and
            // bodies contain customer financial data (NDPA 2023).
        };
    }
}