using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Observability.Infrastructure.Authentication;

public sealed class ApiKeyHasherOptions
{
    public string Pepper { get; set; } = string.Empty;
}

public interface IApiKeyHasher
{
    string Hash(string plaintext);
}

public sealed class ApiKeyHasher : IApiKeyHasher
{
    private readonly byte[] _pepper;

    public ApiKeyHasher(IOptions<ApiKeyHasherOptions> options)
    {
        var pepper = options.Value.Pepper;
        if (string.IsNullOrWhiteSpace(pepper))
        {
            throw new InvalidOperationException(
                "Observability:ApiKeyHashPepper is not configured. Set it via Key Vault (deployed) or appsettings.Development.json (local).");
        }
        _pepper = Encoding.UTF8.GetBytes(pepper);
    }

    public string Hash(string plaintext)
    {
        var keyBytes = Encoding.UTF8.GetBytes(plaintext);
        var combined = new byte[_pepper.Length + keyBytes.Length];
        Buffer.BlockCopy(_pepper, 0, combined, 0, _pepper.Length);
        Buffer.BlockCopy(keyBytes, 0, combined, _pepper.Length, keyBytes.Length);
        return Convert.ToHexString(SHA256.HashData(combined)).ToLowerInvariant();
    }
}
