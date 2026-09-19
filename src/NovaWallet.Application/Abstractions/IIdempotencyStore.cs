namespace NovaWallet.Application.Abstractions;

/// <summary>
/// Backs the Idempotency-Key header on the transfer endpoint.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Claims a key for this request. Must be atomic — the implementation
    /// inserts first and lets a unique-constraint violation reveal a duplicate,
    /// rather than checking for existence and then inserting.
    /// </summary>
    Task<IdempotencyClaim> TryClaimAsync(
        string key, string requestHash, CancellationToken cancellationToken);

    /// <summary>Stores the response so a later replay of the same key returns it.</summary>
    Task CompleteAsync(
        string key, int statusCode, string responseBody, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a claim whose request failed, so the caller can retry with the
    /// same key. Without this, a transient failure would permanently burn the key.
    /// </summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken);
}

public enum IdempotencyClaimStatus
{
    /// <summary>First time this key has been seen — proceed.</summary>
    Claimed = 1,

    /// <summary>Same key, same payload, already completed — replay the stored response.</summary>
    AlreadyCompleted = 2,

    /// <summary>Same key, different payload — reject.</summary>
    PayloadMismatch = 3,

    /// <summary>
    /// Same key, same payload, still running. A client retried before the first
    /// attempt finished; the safe answer is to tell them to wait, not to run it twice.
    /// </summary>
    InProgress = 4
}

public sealed record IdempotencyClaim(
    IdempotencyClaimStatus Status,
    int? StoredStatusCode = null,
    string? StoredResponseBody = null)
{
    public static IdempotencyClaim Claimed() => new(IdempotencyClaimStatus.Claimed);

    public static IdempotencyClaim AlreadyCompleted(int statusCode, string body) =>
        new(IdempotencyClaimStatus.AlreadyCompleted, statusCode, body);

    public static IdempotencyClaim PayloadMismatch() =>
        new(IdempotencyClaimStatus.PayloadMismatch);

    public static IdempotencyClaim InProgress() => new(IdempotencyClaimStatus.InProgress);
}