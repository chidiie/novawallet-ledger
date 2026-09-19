using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace NovaWallet.Api.Logging;

public static class LoggingSetup
{
    /// <summary>
    /// Console-only logging in compact JSON, which is what a container
    /// orchestrator expects — it collects stdout. Writing to files inside a
    /// container means logs vanish when the container does.
    /// </summary>
    public static void ConfigureSerilog(
        this LoggerConfiguration logger, IConfiguration configuration, IHostEnvironment env)
    {
        logger
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Application", "NovaWallet.Ledger")
            .Enrich.WithProperty("Environment", env.EnvironmentName);

        if (env.IsDevelopment())
        {
            logger.WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
        }
        else
        {
            logger.WriteTo.Console(new CompactJsonFormatter());
        }
    }
}