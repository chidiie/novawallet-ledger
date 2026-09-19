using NovaWallet.Domain.Common;
using NovaWallet.Domain.Exceptions;

namespace NovaWallet.Domain.Wallets;

/// <summary>
/// A customer's NGN wallet. Owns its own balance invariants: the balance can
/// only change through <see cref="Credit"/> and <see cref="Debit"/>, and can
/// never become negative.
/// </summary>
/// <remarks>
/// A transfer touches two wallets, so it is NOT modelled as a single aggregate.
/// Each wallet enforces its own rules here; the atomicity and isolation of the
/// pair is a persistence concern, handled in the infrastructure layer with a
/// database transaction and row locks. See the note in the README.
/// </remarks>
public sealed class Wallet
{
    public const string NairaCurrencyCode = "NGN";

    public WalletId Id { get; private set; }

    /// <summary>The owning customer, as supplied by the identity system.</summary>
    public string CustomerId { get; private set; }

    public Money Balance { get; private set; }

    public string Currency { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Parameterless constructor for EF Core materialisation only.
    private Wallet()
    {
        CustomerId = null!;
        Currency = null!;
    }

    private Wallet(WalletId id, string customerId, DateTime createdAtUtc)
    {
        Id = id;
        CustomerId = customerId;
        Balance = Money.Zero;
        Currency = NairaCurrencyCode;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Opens a new wallet with a zero balance.</summary>
    public static Wallet Create(string customerId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("A customer id is required.", nameof(customerId));
        }

        return new Wallet(WalletId.New(), customerId.Trim(), nowUtc);
    }

    /// <summary>Adds funds. The amount must be strictly positive.</summary>
    public void Credit(Money amount)
    {
        amount.EnsurePositive();
        Balance += amount;
    }

    /// <summary>
    /// Removes funds. The amount must be strictly positive and must not exceed
    /// the current balance.
    /// </summary>
    /// <remarks>
    /// This check is necessary but NOT sufficient on its own: under concurrency
    /// the balance read here could be stale. The infrastructure layer re-reads
    /// and re-checks inside a row lock, and the database carries a
    /// CHECK (balance_kobo &gt;= 0) constraint as a final backstop.
    /// </remarks>
    public void Debit(Money amount)
    {
        amount.EnsurePositive();

        if (Balance < amount)
        {
            throw new InsufficientFundsException(Id, Balance, amount);
        }

        Balance -= amount;
    }
}