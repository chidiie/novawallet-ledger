using MediatR;
using NovaWallet.Application.Wallets.Dtos;

namespace NovaWallet.Application.Wallets.Queries.GetBalance;

public sealed record GetBalanceQuery(Guid WalletId) : IRequest<BalanceResponse>;