using MediatR;
using NovaWallet.Application.Abstractions;
using NovaWallet.Application.Wallets.Dtos;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Application.Wallets.Commands.CreditWallet;

public sealed class CreditWalletCommandHandler
    : IRequestHandler<CreditWalletCommand, BalanceResponse>
{
    private readonly IWalletRepository _wallets;
    private readonly ICurrentUser _currentUser;
    private readonly IReferenceGenerator _references;
    private readonly IClock _clock;

    public CreditWalletCommandHandler(
        IWalletRepository wallets,
        ICurrentUser currentUser,
        IReferenceGenerator references,
        IClock clock)
    {
        _wallets = wallets;
        _currentUser = currentUser;
        _references = references;
        _clock = clock;
    }

    public async Task<BalanceResponse> Handle(
        CreditWalletCommand request, CancellationToken cancellationToken)
    {
        var walletId = WalletId.From(request.WalletId);
        var amount = Money.FromKobo(request.Body.AmountKobo).EnsurePositive();

        // Existence is confirmed here for a clean 404, but the credit itself is
        // applied inside the repository's own transaction — this read is not a
        // guarantee that the row still exists when the write lands.
        var wallet = await _wallets.GetByIdAsync(walletId, cancellationToken)
            ?? throw new WalletNotFoundException(walletId);

        var instruction = new CreditInstruction(
            WalletId: walletId,
            Amount: amount,
            Reference: request.Body.ExternalReference ?? _references.NewCreditReference(),
            Narration: request.Body.Narration,
            Actor: _currentUser.CustomerId,
            CorrelationId: _currentUser.CorrelationId,
            OccurredAtUtc: _clock.UtcNow);

        await _wallets.CreditAsync(instruction, cancellationToken);

        var updated = await _wallets.GetByIdAsync(walletId, cancellationToken)
            ?? throw new WalletNotFoundException(walletId);

        return new BalanceResponse(
            updated.Id.Value,
            updated.Balance.Kobo,
            updated.Currency,
            updated.Balance.ToString(),
            _clock.UtcNow);
    }
}