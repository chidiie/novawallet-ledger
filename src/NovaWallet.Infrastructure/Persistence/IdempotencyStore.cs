using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NovaWallet.Application.Abstractions;
using NovaWallet.Infrastructure.Persistence.Entities;

namespace NovaWallet.Infrastructure.Persistence;

/// <summary>
/// Idempotency backed by the primary key on idempotency_records.
/// </summary>
/// <remarks>
/// WHY INSERT-FIRST RATHER THAN CHECK-THEN-INSERT.
///
/// The obvious implementation is:
///     if (await db.Records.AnyAsync(r => r.Key == key)) { ...replay... }
///     else { db.Records.Add(new Record(key)); await db.SaveChangesAsync(); }
///
/// That has a race. Two concurrent requests with the same key both run the
/// AnyAsync, both see nothing, both insert. One insert fails on the primary key
/// — so the SYMPTOM is a 500 rather than a double-spend — but the logic is
/// wrong, and if the key column were merely indexed rather than unique, both
/// would succeed and the transfer would run twice.
///
/// Inserting first inverts the control flow: the database's primary key does
/// the mutual exclusion, atomically, with no window between the check and the
/// write. A duplicate reveals itself as a 23505 unique violation, which we
/// catch and interpret. There is no interleaving in which two callers both
/// believe they claimed the key.
/// </remarks>
public sealed class IdempotencyStore : IIdempotencyStore
{
    /// <summary>PostgreSQL SQLSTATE for a unique/primary-key violation.</summary>
    private const string UniqueViolation = "23505";

    private readonly NovaWalletDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<IdempotencyStore> _logger;

    public IdempotencyStore(
        NovaWalletDbContext db, IClock clock, ILogger<IdempotencyStore> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IdempotencyClaim> TryClaimAsync(
        string key, string requestHash, CancellationToken cancellationToken)
    {
        var record = new IdempotencyRecord
        {
            Key = key,
            RequestHash = requestHash,
            State = IdempotencyState.InProgress,
            CreatedAtUtc = _clock.UtcNow
        };

        try
        {
            _db.IdempotencyRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);

            return IdempotencyClaim.Claimed();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Someone already holds this key. Detach our failed insert so the
            // change tracker isn't left holding an entity that was never saved
            // — otherwise the next SaveChanges on this scoped context retries it.
            _db.Entry(record).State = EntityState.Detached;

            return await InspectExistingAsync(key, requestHash, cancellationToken);
        }
    }

    private async Task<IdempotencyClaim> InspectExistingAsync(
        string key, string requestHash, CancellationToken cancellationToken)
    {
        var existing = await _db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

        if (existing is null)
        {
            // The holder released the key between our failed insert and this
            // read. Rare, and safe: treat it as contention and let the caller
            // retry rather than guessing.
            _logger.LogWarning(
                "Idempotency key {Key} vanished between insert and inspection.", key);
            return IdempotencyClaim.InProgress();
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return IdempotencyClaim.PayloadMismatch();
        }

        return existing.State switch
        {
            IdempotencyState.Completed => IdempotencyClaim.AlreadyCompleted(
                existing.ResponseStatusCode ?? 200,
                existing.ResponseBody ?? string.Empty),

            // Same key, same payload, first attempt still running. Telling the
            // caller to wait is the only safe answer — we cannot know whether
            // the in-flight transfer will succeed.
            _ => IdempotencyClaim.InProgress()
        };
    }

    public async Task CompleteAsync(
        string key, int statusCode, string responseBody, CancellationToken cancellationToken)
    {
        var record = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning(
                "Tried to complete idempotency key {Key}, which no longer exists.", key);
            return;
        }

        record.State = IdempotencyState.Completed;
        record.ResponseStatusCode = statusCode;
        record.ResponseBody = responseBody;
        record.CompletedAtUtc = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Frees a key whose request failed, so the customer can retry.
    /// </summary>
    /// <remarks>
    /// Deletes only records still InProgress. A Completed record must survive —
    /// deleting it would let the same key run the transfer a second time, which
    /// is the exact failure idempotency exists to prevent.
    /// </remarks>
    public async Task ReleaseAsync(string key, CancellationToken cancellationToken)
    {
        var record = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

        if (record is null || record.State == IdempotencyState.Completed)
        {
            return;
        }

        _db.IdempotencyRecords.Remove(record);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolation };
}