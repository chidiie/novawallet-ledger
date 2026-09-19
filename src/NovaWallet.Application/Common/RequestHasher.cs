using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NovaWallet.Application.Common;

/// <summary>
/// Fingerprints a request body so a replayed idempotency key can be checked
/// against the payload it was first used with.
/// </summary>
public static class RequestHasher
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        // Property order must be stable, or the same logical request hashes
        // differently between runs.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Hash<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, CanonicalOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }
}