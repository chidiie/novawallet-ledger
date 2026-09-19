using MediatR;
using NovaWallet.Application.Wallets.Dtos;

namespace NovaWallet.Application.Wallets.Commands.CreateWallet;

/// <summary>
/// Opens a wallet for the authenticated customer. The customer id is taken from
/// the token, never from the request body.
/// </summary>
public sealed record CreateWalletCommand : IRequest<WalletResponse>;