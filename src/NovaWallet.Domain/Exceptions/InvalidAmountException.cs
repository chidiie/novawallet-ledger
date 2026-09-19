namespace NovaWallet.Domain.Exceptions;

/// <summary>
/// Raised when an amount is not valid for the operation — negative where
/// only non-negative is meaningful, or zero where a positive value is required.
/// </summary>
public sealed class InvalidAmountException : DomainException
{
    public InvalidAmountException(string message) : base(message) { }

    public static InvalidAmountException Negative(long kobo) =>
        new($"A monetary amount cannot be negative. Received {kobo} kobo.");

    public static InvalidAmountException NotPositive(long kobo) =>
        new($"This operation requires a positive amount. Received {kobo} kobo.");
}