using System.Security.Cryptography;
using System.Text;

namespace FlashFix.Api.Security;

public sealed class SecretCodec
{
    private readonly byte[] _key;

    public SecretCodec(IConfiguration configuration)
    {
        var configured = configuration["FLASHFIX_KEY_HASH_SECRET"];
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("FLASHFIX_KEY_HASH_SECRET is required.");

        try { _key = Convert.FromBase64String(configured); }
        catch (FormatException e) { throw new InvalidOperationException("FLASHFIX_KEY_HASH_SECRET must be Base64.", e); }
        if (_key.Length < 32)
            throw new InvalidOperationException("FLASHFIX_KEY_HASH_SECRET must contain at least 32 random bytes.");
    }

    public string HashKey(string key) => Hmac(key.Trim().ToUpperInvariant());
    public string HashDevice(string deviceId) => Hmac("DEVICE:" + deviceId.Trim().ToLowerInvariant());
    public string HashDevicePublicKey(byte[] publicKey) => Hmac("DEVICE-KEY:" + Convert.ToBase64String(publicKey));
    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string Hmac(string value) => Convert.ToHexString(
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)));

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string NewKey()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return $"FF-{hex[..8]}-{hex[8..16]}-{hex[16..24]}-{hex[24..]}";
    }
}
