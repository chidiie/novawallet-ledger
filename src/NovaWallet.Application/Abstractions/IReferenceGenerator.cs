namespace NovaWallet.Application.Abstractions;

/// <summary>
/// Produces the reference that ties a transfer's two ledger entries and its
/// audit entry together. Behind an interface so tests can assert on an exact
/// value instead of matching a pattern.
/// </summary>
public interface IReferenceGenerator
{
    string NewTransferReference();
    string NewCreditReference();
}