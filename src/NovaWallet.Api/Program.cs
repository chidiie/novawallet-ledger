using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using NovaWallet.Api;
using NovaWallet.Api.HealthChecks;
using NovaWallet.Api.Logging;
using NovaWallet.Api.Middleware;
using NovaWallet.Api.RateLimiting;
using NovaWallet.Api.Swagger;
using NovaWallet.Application;
using NovaWallet.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ConfigureSerilog(context.Configuration, context.HostingEnvironment));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddNovaWalletRateLimiting(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddNovaWalletSwagger();
    builder.Services.AddNovaWalletHealthChecks(builder.Configuration);

    var app = builder.Build();

    // --- Pipeline. Order is deliberate; see the middleware classes. ---------

    // 1. Correlation id: outermost, so every log line and error carries it.
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 2. Exception handling: wraps everything that can throw.
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 3. Request logging: inside error handling, so a failed request still
    //    produces a completion log line with its status code.
    app.UseSerilogRequestLogging(RequestLoggingOptions.Configure);

    // 4/5. Authentication before rate limiting, so the limiter can partition
    //      per customer rather than per IP. See RateLimitingSetup.
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NovaWallet Ledger v1");
        options.DocumentTitle = "NovaWallet Ledger Service";
    });

    app.MapControllers();
    // Liveness: is the process running? Deliberately checks NOTHING external —
    // a database outage must not cause every instance to be restarted.
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    }).AllowAnonymous();

    // Readiness: can this instance serve traffic? Checks the database, so an
    // instance that has lost its datastore is removed from the load balancer
    // without being killed.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(HealthCheckSetup.ReadinessTag),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    }).AllowAnonymous();

    Log.Information("NovaWallet Ledger Service starting in {Environment}",
        app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "NovaWallet Ledger Service failed to start.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;