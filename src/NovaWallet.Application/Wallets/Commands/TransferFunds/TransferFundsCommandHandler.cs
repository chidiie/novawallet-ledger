using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using NovaWallet.Application.Abstractions;
using NovaWallet.Application.Common;
using NovaWallet.Application.Exceptions;
using NovaWallet.Application.Wallets.Dtos;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Application.Wallets.Commands.TransferFunds;

/// <summary>
/// Moves funds between two wallets.
/// </summary>
/// <remarks>
/// TRANSACTION BOUNDARIES — the important part of this class.
///
/// OUTSIDE the money transaction (safe to be non-atomic, and deliberately so):
///   • the idempotency claim, which must commit on its own so a concurrent
///     replay can see it immediately. If it shared the transfer's transaction,
///     no other request could observe the claim until the transfer committed,
///     and the whole mechanism would be pointless.
///   • the ownership and existence pre-checks, which are fast-fail conveniences.
///     They are re-established under lock inside the executor; nothing here is
///     trusted by the time money moves.
///
/// INSIDE the money transaction (all-or-nothing, owned by ITransferExecutor):
///   • locking both wallet rows in a deterministic order
///   • re-reading both balances under those locks
///   • the daily-limit check and its counter increment
///   • the debit and the credit
///   • both ledger entries and the audit entry
///
/// The limit check MUST be inside. Checking it out here would leave a window in
/// which two concurrent transfers both pass, and the wallet exceeds its limit.
/// </remarks>
public sealed class TransferFundsCommandHandler
    : IRequestHandler<TransferFundsCommand, TransferResponse>
{
    private readonly IWalletRepository _wallets;
    private readonly ITransferExecutor _executor;
    private readonly IIdempotencyStore _idempotency;
    private readonly ICurrentUser _currentUser;
    private readonly IReferenceGenerator _references;
    private readonly IClock _clock;
    private readonly ILogger<TransferFundsCommandHandler> _logger;

    public TransferFundsCommandHandler(
        IWalletRepository wallets,
        ITransferExecutor executor,
        IIdempotencyStore idempotency,
        ICurrentUser currentUser,
        IReferenceGenerator references,
        IClock clock,
        ILogger<TransferFundsCommandHandler> logger)
    {
        _wallets = wallets;
        _executor = executor;
        _idempotency = idempotency;
        _currentUser = currentUser;
        _logger = logger;   
        _references = references;
        _clock = clock;
    }

    public async Task<TransferResponse> Handle(
        TransferFundsCommand request, CancellationToken cancellationToken)
    {
        var sourceId = WalletId.From(request.SourceWalletId);
        var destinationId = WalletId.From(request.Body.DestinationWalletId);

        if (sourceId == destinationId)
        {
            throw new SelfTransferException();
        }

        var amount = Money.FromKobo(request.Body.AmountKobo).EnsurePositive();

        // --- Step 1: claim the idempotency key -------------------------------
        // Done first, before any validation that could be expensive, and before
        // any money moves. The hash covers the route parameter as well as the
        // body, so the same key aimed at a different source wallet is a conflict.
        var requestHash = RequestHasher.Hash(new
        {
            request.SourceWalletId,
            request.Body.DestinationWalletId,
            request.Body.AmountKobo
        });

        var claim = await _idempotency.TryClaimAsync(
            request.IdempotencyKey, requestHash, cancellationToken);

        switch (claim.Status)
        {
            case IdempotencyClaimStatus.AlreadyCompleted:
                return Replay(claim);

            case IdempotencyClaimStatus.PayloadMismatch:
                throw new IdempotencyConflictException(request.IdempotencyKey);

            case IdempotencyClaimStatus.InProgress:
                throw new IdempotencyInProgressException(request.IdempotencyKey);
        }

        // --- Step 2: fast-fail checks ----------------------------------------
        // From here on, any failure must release the claim, or the customer can
        // never retry with the same key.
        try
        {
            var source = await _wallets.GetByIdAsync(sourceId, cancellationToken)
                ?? throw new WalletNotFoundException(sourceId);

            if (source.CustomerId != _currentUser.CustomerId)
            {
                throw new WalletAccessDeniedException();
            }

            _ = await _wallets.GetByIdAsync(destinationId, cancellationToken)
                ?? throw new WalletNotFoundException(destinationId);

            // --- Step 3: the atomic money movement ---------------------------
            var now = _clock.UtcNow;

            var instruction = new TransferInstruction(
                SourceWalletId: sourceId,
                DestinationWalletId: destinationId,
                Amount: amount,
                Reference: _references.NewTransferReference(),
                Narration: request.Body.Narration,
                Actor: _currentUser.CustomerId,
                CorrelationId: _currentUser.CorrelationId,
                OccurredAtUtc: now,
                WatDate: WestAfricaTime.DateAt(now));

            var outcome = await _executor.ExecuteAsync(instruction, cancellationToken);

            var response = new TransferResponse(
                outcome.Reference,
                sourceId.Value,
                destinationId.Value,
                amount.Kobo,
                outcome.SourceBalanceAfter.Kobo,
                outcome.CompletedAtUtc);

            // --- Step 4: record the response for future replays --------------
            await _idempotency.CompleteAsync(
                request.IdempotencyKey,
                StatusCodes.Created,
                JsonSerializer.Serialize(response),
                cancellationToken);

            return response;
        }
        catch (Exception ex) when (IsSafeToRelease(ex))
        {
            // These failures all occur before ITransferExecutor commits, so the
            // key can be freed for a retry with no risk of double-processing.
            await _idempotency.ReleaseAsync(request.IdempotencyKey, CancellationToken.None);
            throw;
        }
        catch
        {
            // Anything else may have failed AFTER the transfer committed. Leave
            // the key claimed: a stuck key is recoverable by support; a double
            // debit is not. The caller gets 409 InProgress if they retry.
            _logger.LogError(
                "Transfer with idempotency key {Key} failed in an indeterminate state; " +
                "key deliberately left claimed.", request.IdempotencyKey);
            throw;
        }
    }

    private static TransferResponse Replay(IdempotencyClaim claim)
    {
        var stored = JsonSerializer.Deserialize<TransferResponse>(claim.StoredResponseBody!)
            ?? throw new InvalidOperationException(
                "A completed idempotency record held an unreadable response body.");

        return stored;
    }

    /// <summary>
    /// True for failures that can only have happened before any money moved.
    /// </summary>
    private static bool IsSafeToRelease(Exception ex) =>
        ex is WalletNotFoundException
            or WalletAccessDeniedException
            or InsufficientFundsException
            or DailyLimitExceededException
            or InvalidAmountException;

    private static class StatusCodes
    {
        public const int Created = 201;
    }
}