using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NovaWallet.Application.Common;
using NovaWallet.Application.Wallets.Commands.CreateWallet;
using NovaWallet.Application.Wallets.Commands.CreditWallet;
using NovaWallet.Application.Wallets.Commands.TransferFunds;
using NovaWallet.Application.Wallets.Dtos;
using NovaWallet.Application.Wallets.Queries.GetBalance;
using NovaWallet.Application.Wallets.Queries.GetStatement;

namespace NovaWallet.Api.Controllers;

/// <summary>NovaWallet ledger operations. All endpoints require a bearer token.</summary>
[ApiController]
[Route("api/wallets")]
[Authorize]
[Produces("application/json")]
public sealed class WalletsController : ControllerBase
{
    /// <summary>Header carrying the idempotency key on transfers.</summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly ISender _sender;

    public WalletsController(ISender sender) => _sender = sender;

    /// <summary>Opens a wallet for the authenticated customer, with a zero balance.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WalletResponse>> CreateWallet(
        CancellationToken cancellationToken)
    {
        var wallet = await _sender.Send(new CreateWalletCommand(), cancellationToken);

        return CreatedAtAction(
            nameof(GetBalance), new { walletId = wallet.WalletId }, wallet);
    }

    /// <summary>Returns the wallet's current balance in kobo.</summary>
    [HttpGet("{walletId:guid}/balance")]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalanceResponse>> GetBalance(
        Guid walletId, CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new GetBalanceQuery(walletId), cancellationToken));

    /// <summary>Credits a wallet, simulating an inbound NIBSS NIP transfer.</summary>
    [HttpPost("{walletId:guid}/credit")]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalanceResponse>> Credit(
        Guid walletId,
        [FromBody] CreditWalletRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _sender.Send(new CreditWalletCommand(walletId, request), cancellationToken));

    /// <summary>
    /// Moves funds to another wallet. Requires an Idempotency-Key header;
    /// replaying the same key returns the original result without moving money again.
    /// </summary>
    /// <response code="201">The transfer completed.</response>
    /// <response code="409">The key is in progress, or was reused with a different payload.</response>
    /// <response code="422">Insufficient funds, or the daily outbound limit was exceeded.</response>
    /// <response code="429">Too many transfer requests.</response>
    [HttpPost("{walletId:guid}/transfers")]
    [EnableRateLimiting(RateLimitPolicies.Transfers)]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TransferResponse>> Transfer(
        Guid walletId,
        [FromBody] TransferRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new TransferFundsCommand(walletId, request, idempotencyKey ?? string.Empty),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Paginated transaction history, newest first.</summary>
    [HttpGet("{walletId:guid}/statement")]
    [ProducesResponseType(typeof(PagedResult<StatementEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<StatementEntryResponse>>> GetStatement(
        Guid walletId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _sender.Send(
            new GetStatementQuery(walletId, page, pageSize), cancellationToken));
}