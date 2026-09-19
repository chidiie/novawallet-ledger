using NovaWallet.Domain.Exceptions;

namespace NovaWallet.Domain.Common;

/// <summary>
/// A non-negative amount of Nigerian currency, held as a whole number of kobo.
/// <para>
/// This is the ONLY type used for money anywhere in the system. There is no
/// sub-kobo value, so no rounding decision can be made implicitly; any division
/// must be written out explicitly with its remainder handled.
/// </para>
/// <para>
/// Implemented as a readonly record struct: value equality and comparison come
/// for free, and it costs no heap allocation.
/// </para>
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    /// <summary>100 kobo in one Naira.</summary>
    public const int KoboPerNaira = 100;

    /// <summary>The amount, as a whole number of kobo. Never negative.</summary>
    public long Kobo { get; }

    private Money(long kobo)
    {
        if (kobo < 0)
        {
            throw InvalidAmountException.Negative(kobo);
        }

        Kobo = kobo;
    }

    public static Money Zero => new(0);

    public static Money FromKobo(long kobo) => new(kobo);

    /// <summary>
    /// Builds an amount from whole Naira. Takes a <see cref="long"/>, not a
    /// decimal, on purpose: callers with a fractional value must decide for
    /// themselves how it becomes kobo rather than having this type guess.
    /// </summary>
    public static Money FromNaira(long naira) => new(checked(naira * KoboPerNaira));

    public bool IsZero => Kobo == 0;

    public bool IsPositive => Kobo > 0;

    /// <summary>
    /// Throws unless this amount is strictly positive. Used to guard operations
    /// where a zero-value transfer or credit would be meaningless.
    /// </summary>
    public Money EnsurePositive()
    {
        if (!IsPositive)
        {
            throw InvalidAmountException.NotPositive(Kobo);
        }

        return this;
    }

    // `checked` on every operator: an overflow must throw, never wrap around
    // silently into a nonsense balance.
    public static Money operator +(Money left, Money right) =>
        new(checked(left.Kobo + right.Kobo));

    /// <summary>
    /// Subtraction. Throws if the result would be negative — a negative Money
    /// cannot exist, so an underflow is caught here rather than persisted.
    /// Callers that expect a shortfall should compare first and raise a more
    /// meaningful error (see <see cref="Wallets.Wallet.Debit"/>).
    /// </summary>
    public static Money operator -(Money left, Money right) =>
        new(checked(left.Kobo - right.Kobo));

    public static bool operator <(Money left, Money right) => left.Kobo < right.Kobo;
    public static bool operator >(Money left, Money right) => left.Kobo > right.Kobo;
    public static bool operator <=(Money left, Money right) => left.Kobo <= right.Kobo;
    public static bool operator >=(Money left, Money right) => left.Kobo >= right.Kobo;

    public int CompareTo(Money other) => Kobo.CompareTo(other.Kobo);

    /// <summary>
    /// DISPLAY ONLY. Converts to Naira as a decimal for formatting at the API
    /// edge. Never feed the result back into a calculation — do the arithmetic
    /// in kobo and convert once, at the boundary.
    /// </summary>
    public decimal ToNaira() => Kobo / (decimal)KoboPerNaira;

    public override string ToString() => $"NGN {ToNaira():N2}";
}