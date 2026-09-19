using MediatR;
using NovaWallet.Application.Common;
using NovaWallet.Application.Wallets.Dtos;

namespace NovaWallet.Application.Wallets.Queries.GetStatement;

public sealed record GetStatementQuery(Guid WalletId, int Page, int PageSize)
    : IRequest<PagedResult<StatementEntryResponse>>;