using FluentValidation;

namespace NovaWallet.Application.Wallets.Validation;

public sealed record StatementQueryParameters(int Page = 1, int PageSize = 20);

public sealed class StatementQueryValidator : AbstractValidator<StatementQueryParameters>
{
    public const int MaxPageSize = 100;

    public StatementQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(MaxPageSize)
            .WithMessage($"Page size must be between 1 and {MaxPageSize}.");
    }
}