using NovaWallet.Application.Abstractions;

namespace NovaWallet.Infrastructure.Services;

/// <summary>
/// Human-readable references. Uses a sortable timestamp prefix plus random
/// suffix rather than a bare Guid — support staff read these aloud.
/// </summary>
public sealed class ReferenceGenerator : IReferenceGenerator
{
    private readonly IClock _clock;

    public ReferenceGenerator(IClock clock) => _clock = clock;

    public string NewTransferReference() => Build("NWT");

    public string NewCreditReference() => Build("NWC");

    private string Build(string prefix)
    {
        var stamp = _clock.UtcNow.ToString("yyyyMMddHHmmss");
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"{prefix}-{stamp}-{suffix}";
    }
}