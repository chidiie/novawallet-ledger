using MediatR;
using NovaWallet.Application.Wallets.Dtos;

namespace NovaWallet.Application.Wallets.Commands.CreditWallet;

/// <summary>Simulates an inbound NIP transfer landing in a wallet.</summary>
public sealed record CreditWalletCommand(Guid WalletId, CreditWalletRequest Body)
    : IRequest<BalanceResponse>;