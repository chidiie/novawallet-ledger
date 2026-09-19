using MediatR;
using NovaWallet.Application.Abstractions;
using NovaWallet.Application.Common;
using NovaWallet.Application.Exceptions;
using NovaWallet.Application.Wallets.Dtos;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Application.Wallets.Queries.GetStatement;

public sealed class GetStatementQueryHandler
    : IRequestHandler<GetStatementQuery, PagedResult<StatementEntryResponse>>
{
    private readonly IWalletRepository _wallets;
    private readonly ICurrentUser _currentUser;

    public GetStatementQueryHandler(IWalletRepository wallets, ICurrentUser currentUser)
    {
        _wallets = wallets;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<StatementEntryResponse>> Handle(
        GetStatementQuery request, CancellationToken cancellationToken)
    {
        var walletId = WalletId.From(request.WalletId);

        var wallet = await _wallets.GetByIdAsync(walletId, cancellationToken)
            ?? throw new WalletNotFoundException(walletId);

        if (wallet.CustomerId != _currentUser.CustomerId)
        {
            throw new WalletAccessDeniedException();
        }

        var entries = await _wallets.GetStatementAsync(
            walletId, request.Page, request.PageSize, cancellationToken);

        var total = await _wallets.CountStatementEntriesAsync(walletId, cancellationToken);

        var items = entries
            .Select(e => new StatementEntryResponse(
                e.Id,
                e.Direction.ToString(),
                e.Amount.Kobo,
                e.BalanceAfter.Kobo,
                e.Reference,
                e.Narration,
                e.OccurredAtUtc))
            .ToList();

        return new PagedResult<StatementEntryResponse>(
            items, request.Page, request.PageSize, total);
    }
}