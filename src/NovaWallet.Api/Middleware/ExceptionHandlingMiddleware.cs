using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NovaWallet.Application.Exceptions;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Infrastructure.Authentication;

namespace NovaWallet.Api.Middleware;

/// <summary>
/// Converts every unhandled exception into an RFC 7807 Problem Details response.
/// </summary>
/// <remarks>
/// This is the ONLY place exceptions become HTTP responses. Controllers contain
/// no try/catch, so there is one mapping table rather than a scattering of
/// ad hoc error shapes.
///
/// Known business failures are logged at Warning with their detail; unknown
/// failures are logged at Error with the full exception but returned to the
/// caller as a bare 500 carrying only the correlation id. Internal details
/// never cross the wire.
/// </remarks>
public sealed class ExceptionHandlingMiddleware
{
    private const string ProblemTypeBase = "https://novawallet.firstbank.ng/problems/";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            // Too late to write a different response; the best we can do is
            // record it and let the connection fail.
            _logger.LogError(exception,
                "Exception thrown after the response had started.");
            throw exception;
        }

        var problem = Map(exception);

        problem.Instance = context.Request.Path;
        problem.Extensions["correlationId"] =
            context.Items[CurrentUser.CorrelationIdItemKey] as string;

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception,
                "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Request rejected: {Title} on {Method} {Path}",
                problem.Title, context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = problem.Status ?? 500;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        // --- Validation -----------------------------------------------------
        ValidationException validation => BuildValidationProblem(validation),

        // --- Not found ------------------------------------------------------
        WalletNotFoundException e => Build(
            StatusCodes.Status404NotFound, "wallet-not-found", "Wallet not found", e.Message),

        // --- Authorisation --------------------------------------------------
        WalletAccessDeniedException e => Build(
            StatusCodes.Status403Forbidden, "wallet-access-denied", "Access denied", e.Message),

        // --- Business rules the caller could retry differently ---------------
        // 422 rather than 400: the request was well-formed and understood,
        // but the current state of the account prevents it.
        InsufficientFundsException e => Build(
            StatusCodes.Status422UnprocessableEntity, "insufficient-funds",
            "Insufficient funds", e.Message),

        DailyLimitExceededException e => Build(
            StatusCodes.Status422UnprocessableEntity, "daily-limit-exceeded",
            "Daily transfer limit exceeded", e.Message),

        InvalidAmountException e => Build(
            StatusCodes.Status400BadRequest, "invalid-amount", "Invalid amount", e.Message),

        SelfTransferException e => Build(
            StatusCodes.Status400BadRequest, "self-transfer", "Invalid transfer", e.Message),

        // --- Idempotency ----------------------------------------------------
        IdempotencyConflictException e => Build(
            StatusCodes.Status409Conflict, "idempotency-key-conflict",
            "Idempotency key conflict", e.Message),

        IdempotencyInProgressException e => Build(
            StatusCodes.Status409Conflict, "idempotency-key-in-progress",
            "Request already in progress", e.Message),

        // --- Client hung up --------------------------------------------------
        // 499 is nginx's convention. Not a real failure; avoids polluting the
        // 500 rate with cancellations.
        OperationCanceledException => Build(
            499, "request-cancelled", "Request cancelled",
            "The request was cancelled before it completed."),

        // --- Anything else: reveal nothing ------------------------------------
        _ => Build(
            StatusCodes.Status500InternalServerError, "internal-error",
            "An unexpected error occurred",
            "The request could not be completed. Quote the correlation id when contacting support.")
    };

    private static ProblemDetails BuildValidationProblem(ValidationException exception)
    {
        var problem = Build(
            StatusCodes.Status400BadRequest, "validation-failed",
            "One or more validation errors occurred",
            "See the errors property for details.");

        problem.Extensions["errors"] = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(e => e.ErrorMessage).ToArray());

        return problem;
    }

    private static ProblemDetails Build(
        int status, string type, string title, string detail) =>
        new()
        {
            Status = status,
            Type = ProblemTypeBase + type,
            Title = title,
            Detail = detail
        };
}