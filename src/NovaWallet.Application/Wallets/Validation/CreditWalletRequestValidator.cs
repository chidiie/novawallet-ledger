using FluentValidation;
using NovaWallet.Application.Wallets.Dtos;

namespace NovaWallet.Application.Wallets.Validation;

public sealed class CreditWalletRequestValidator : AbstractValidator<CreditWalletRequest>
{
    public CreditWalletRequestValidator()
    {
        RuleFor(x => x.AmountKobo)
            .GreaterThan(0)
            .WithMessage("The credit amount must be greater than zero kobo.");

        RuleFor(x => x.Narration).MaximumLength(140).When(x => x.Narration is not null);

        RuleFor(x => x.ExternalReference)
            .MaximumLength(64).When(x => x.ExternalReference is not null);
    }
}