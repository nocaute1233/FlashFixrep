using System.Security.Cryptography;
using System.Text;

namespace FlashFix.Core.Updates;

public sealed record SignedReleaseManifest(
    string Version, string PackageUrl, string Sha256, string Signature);

public sealed class VerifiedRelease
{
    private readonly byte[] _packageHash;
    internal VerifiedRelease(Version version, Uri packageUrl, byte[] packageHash)
    {
        Version = version;
        PackageUrl = packageUrl;
        _packageHash = packageHash;
    }

    public Version Version { get; }
    public Uri PackageUrl { get; }

    public async Task<bool> VerifyPackageAsync(Stream package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var actual = await SHA256.HashDataAsync(package, cancellationToken);
        return CryptographicOperations.FixedTimeEquals(actual, _packageHash);
    }
}

public static class ReleaseIntegrity
{
    public static byte[] SigningPayload(SignedReleaseManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!TryValidateFields(manifest, out _, out _))
            throw new ArgumentException("Manifesto de atualização inválido.", nameof(manifest));
        return Encoding.UTF8.GetBytes(
            $"{manifest.Version}\n{manifest.PackageUrl}\n{manifest.Sha256}\n");
    }

    public static VerifiedRelease? Verify(SignedReleaseManifest manifest, RSA trustedPublicKey)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(trustedPublicKey);
        if (trustedPublicKey.KeySize < 2048 ||
            !TryValidateFields(manifest, out var version, out var packageUrl) ||
            manifest.Signature is null or { Length: > 4096 })
            return null;

        byte[] signature;
        byte[] hash;
        try
        {
            signature = Convert.FromBase64String(manifest.Signature);
            hash = Convert.FromHexString(manifest.Sha256);
        }
        catch (FormatException) { return null; }

        if (!trustedPublicKey.VerifyData(SigningPayload(manifest), signature,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            return null;
        return new VerifiedRelease(version!, packageUrl!, hash);
    }

    private static bool TryValidateFields(
        SignedReleaseManifest manifest, out Version? version, out Uri? packageUrl)
    {
        version = null;
        packageUrl = null;
        if (manifest.Version is null or { Length: > 32 } ||
            manifest.PackageUrl is null or { Length: > 2048 } ||
            manifest.Sha256 is null or { Length: not 64 } ||
            !System.Version.TryParse(manifest.Version, out version) ||
            !Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out packageUrl) ||
            packageUrl.Scheme != Uri.UriSchemeHttps ||
            packageUrl.UserInfo.Length != 0 ||
            !manifest.Sha256.All(char.IsAsciiHexDigit) ||
            manifest.Sha256 != manifest.Sha256.ToUpperInvariant())
            return false;
        return true;
    }
}
