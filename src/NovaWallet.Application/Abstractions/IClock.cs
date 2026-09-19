namespace NovaWallet.Application.Abstractions;

/// <summary>
/// The current time, behind an interface so tests can control it. Needed for
/// audit timestamps and, critically, for deciding which WAT day a transfer
/// falls in — a daily-limit test that depended on the real clock would behave
/// differently at 23:59 than at 00:01.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}