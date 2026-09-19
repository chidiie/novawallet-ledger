namespace NovaWallet.Infrastructure.Persistence.Entities;

public enum IdempotencyState
{
    InProgress = 1,
    Completed = 2
}

public sealed class IdempotencyRecord
{
    public string Key { get; set; } = null!;

    /// <summary>SHA-256 of the canonical request, to detect key reuse.</summary>
    public string RequestHash { get; set; } = null!;

    public IdempotencyState State { get; set; }

    public int? ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}