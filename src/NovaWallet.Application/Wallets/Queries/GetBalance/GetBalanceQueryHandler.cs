using MediatR;
using NovaWallet.Application.Abstractions;
using NovaWallet.Application.Exceptions;
using NovaWallet.Application.Wallets.Dtos;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Application.Wallets.Queries.GetBalance;

public sealed class GetBalanceQueryHandler
    : IRequestHandler<GetBalanceQuery, BalanceResponse>
{
    private readonly IWalletRepository _wallets;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public GetBalanceQueryHandler(
        IWalletRepository wallets, ICurrentUser currentUser, IClock clock)
    {
        _wallets = wallets;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<BalanceResponse> Handle(
        GetBalanceQuery request, CancellationToken cancellationToken)
    {
        var walletId = WalletId.From(request.WalletId);

        var wallet = await _wallets.GetByIdAsync(walletId, cancellationToken)
            ?? throw new WalletNotFoundException(walletId);

        if (wallet.CustomerId != _currentUser.CustomerId)
        {
            throw new WalletAccessDeniedException();
        }

        return new BalanceResponse(
            wallet.Id.Value,
            wallet.Balance.Kobo,
            wallet.Currency,
            wallet.Balance.ToString(),
            _clock.UtcNow);
    }
}