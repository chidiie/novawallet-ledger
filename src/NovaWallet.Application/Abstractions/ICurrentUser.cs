namespace NovaWallet.Application.Abstractions;

/// <summary>
/// The authenticated caller, as resolved from the bearer token. Lets handlers
/// record who caused a change and authorise wallet access without taking a
/// dependency on HttpContext.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The customer id from the token's subject claim.</summary>
    string CustomerId { get; }

    /// <summary>The correlation id for the request in flight, for the audit trail.</summary>
    string CorrelationId { get; }
}