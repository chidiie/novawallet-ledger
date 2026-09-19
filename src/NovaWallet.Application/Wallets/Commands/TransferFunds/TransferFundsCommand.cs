using MediatR;
using NovaWallet.Application.Wallets.Dtos;

namespace NovaWallet.Application.Wallets.Commands.TransferFunds;

public sealed record TransferFundsCommand(
    Guid SourceWalletId,
    TransferRequest Body,
    string IdempotencyKey) : IRequest<TransferResponse>;