using NovaWallet.Application.Abstractions;

namespace NovaWallet.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}