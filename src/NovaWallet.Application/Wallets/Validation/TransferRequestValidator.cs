using FluentValidation;
using NovaWallet.Application.Wallets.Dtos;
using NovaWallet.Domain.Wallets;

namespace NovaWallet.Application.Wallets.Validation;

public sealed class TransferRequestValidator : AbstractValidator<TransferRequest>
{
    public TransferRequestValidator()
    {
        RuleFor(x => x.DestinationWalletId)
            .NotEmpty()
            .WithMessage("A destination wallet id is required.");

        RuleFor(x => x.AmountKobo)
            .GreaterThan(0)
            .WithMessage("The transfer amount must be greater than zero kobo.")
            .LessThanOrEqualTo(WalletLimits.DailyOutboundLimit.Kobo)
            .WithMessage(
                "A single transfer cannot exceed the daily outbound limit of " +
                $"{WalletLimits.DailyOutboundLimit.Kobo} kobo.");

        RuleFor(x => x.Narration)
            .MaximumLength(140)
            .When(x => x.Narration is not null);
    }
}