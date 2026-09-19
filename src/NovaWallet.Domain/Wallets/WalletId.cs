namespace NovaWallet.Domain.Wallets;

/// <summary>
/// Strongly-typed wallet identifier. Prevents a customer id, a transfer id and
/// a wallet id from being swapped by accident — they would all be bare Guids
/// otherwise, and the compiler could not tell them apart.
/// </summary>
public readonly record struct WalletId(Guid Value) : IComparable<WalletId>
{
    public static WalletId New() => new(Guid.NewGuid());

    public static WalletId From(Guid value) => new(value);

    public static bool TryParse(string? input, out WalletId walletId)
    {
        if (Guid.TryParse(input, out var guid))
        {
            walletId = new WalletId(guid);
            return true;
        }

        walletId = default;
        return false;
    }

    /// <summary>
    /// Ordering exists specifically so the infrastructure layer can lock two
    /// wallet rows in a deterministic sequence during a transfer. See Stage 6.
    /// </summary>
    public int CompareTo(WalletId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}