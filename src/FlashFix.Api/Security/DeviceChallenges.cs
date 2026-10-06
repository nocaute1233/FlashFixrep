using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace FlashFix.Api.Security;

public sealed class DeviceChallenges
{
    private sealed record Challenge(string Username, string Purpose, byte[] Payload, DateTime ExpiresAt);
    private readonly ConcurrentDictionary<string, Challenge> _pending = new();

    public (string Id, string Payload)? Create(string username, string purpose)
    {
        var now = DateTime.UtcNow;
        foreach (var item in _pending.Where(x => x.Value.ExpiresAt <= now))
            _pending.TryRemove(item.Key, out _);
        if (_pending.Count >= 10_000) return null;

        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var payload = Encoding.UTF8.GetBytes($"FlashFix device proof v1\n{purpose}\n{username}\n{nonce}");
        _pending[id] = new Challenge(username, purpose, payload, now.AddMinutes(2));
        return (id, Convert.ToBase64String(payload));
    }

    public bool Consume(string? id, string username, string purpose, string? publicKey,
        string? signature, SecretCodec codec, out string deviceHash)
    {
        deviceHash = string.Empty;
        if (id is null || id.Length != 32 || publicKey is null || publicKey.Length > 2048 ||
            signature is null || signature.Length > 1024 || !_pending.TryRemove(id, out var challenge) ||
            challenge.ExpiresAt <= DateTime.UtcNow || challenge.Username != username ||
            challenge.Purpose != purpose)
            return false;

        try
        {
            var publicBytes = Convert.FromBase64String(publicKey);
            var signatureBytes = Convert.FromBase64String(signature);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicBytes, out var read);
            if (read != publicBytes.Length || rsa.KeySize < 2048 || rsa.KeySize > 4096 ||
                !rsa.VerifyData(challenge.Payload, signatureBytes, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1))
                return false;
            deviceHash = codec.HashDevicePublicKey(rsa.ExportSubjectPublicKeyInfo());
            return true;
        }
        catch (Exception error) when (error is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
