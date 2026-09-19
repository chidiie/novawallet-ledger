using NovaWallet.Domain.Auditing;

namespace NovaWallet.Application.Abstractions;

/// <summary>
/// Appends to the audit trail. Write-only by design — there is no update,
/// no delete, and no read, because nothing in this service has any business
/// doing those things.
/// </summary>
public interface IAuditLogWriter
{
    Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken);
}