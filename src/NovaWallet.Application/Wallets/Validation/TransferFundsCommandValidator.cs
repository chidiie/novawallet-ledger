using FluentValidation;
using NovaWallet.Application.Wallets.Commands.TransferFunds;

namespace NovaWallet.Application.Wallets.Validation;

public sealed class TransferFundsCommandValidator
    : AbstractValidator<TransferFundsCommand>
{
    public TransferFundsCommandValidator()
    {
        RuleFor(x => x.SourceWalletId).NotEmpty();

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("An Idempotency-Key header is required.")
            .MaximumLength(128);

        RuleFor(x => x.Body).NotNull()
            .SetValidator(new TransferRequestValidator()!);
    }
}