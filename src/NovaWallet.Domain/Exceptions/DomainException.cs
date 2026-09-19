namespace NovaWallet.Domain.Exceptions;

/// <summary>
/// Base type for all business-rule violations raised by the domain.
/// The API layer maps these to specific HTTP status codes; the domain
/// itself knows nothing about HTTP.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}