using MediatR;
using NovaWallet.Application.Abstractions;
using NovaWallet.Application.Wallets.Dtos;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Application.Wallets.Commands.CreateWallet;

public sealed class CreateWalletCommandHandler
    : IRequestHandler<CreateWalletCommand, WalletResponse>
{
    private readonly IWalletRepository _wallets;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public CreateWalletCommandHandler(
        IWalletRepository wallets, ICurrentUser currentUser, IClock clock)
    {
        _wallets = wallets;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<WalletResponse> Handle(
        CreateWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = Wallet.Create(_currentUser.CustomerId, _clock.UtcNow);

        await _wallets.AddAsync(wallet, cancellationToken);

        return new WalletResponse(
            wallet.Id.Value,
            wallet.CustomerId,
            wallet.Balance.Kobo,
            wallet.Currency,
            wallet.CreatedAtUtc);
    }
}