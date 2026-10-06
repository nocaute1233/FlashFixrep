using System.Security.Cryptography;
using FlashFix.Client;

namespace FlashFix_Desktop.Security;

public sealed class WindowsDeviceProof : IDeviceProofProvider
{
    private const string KeyName = "FlashFix.License.v1";
    private static readonly CngProvider PlatformProvider = new("Microsoft Platform Crypto Provider");
    private static readonly CngProvider SoftwareProvider = CngProvider.MicrosoftSoftwareKeyStorageProvider;
    private readonly Lazy<CngProvider> _provider = new(ResolveProvider);

    private static CngProvider ResolveProvider()
    {
        try
        {
            if (CngKey.Exists(KeyName, PlatformProvider))
                return PlatformProvider;
        }
        catch (CryptographicException) { }
        if (CngKey.Exists(KeyName, SoftwareProvider))
            return SoftwareProvider;
        try
        {
            CreateKey(PlatformProvider);
            return PlatformProvider;
        }
        catch (CryptographicException)
        {
            CreateKey(SoftwareProvider);
            return SoftwareProvider;
        }
    }

    public string PublicKey
    {
        get
        {
            using var key = CngKey.Open(KeyName, _provider.Value);
            using var rsa = new RSACng(key);
            return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        }
    }

    public string Sign(byte[] payload)
    {
        using var key = CngKey.Open(KeyName, _provider.Value);
        using var rsa = new RSACng(key);
        return Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    private static void CreateKey(CngProvider provider)
    {
        var parameters = new CngKeyCreationParameters
        {
            Provider = provider,
            ExportPolicy = CngExportPolicies.None,
            KeyUsage = CngKeyUsages.Signing
        };
        parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None));
        using var key = CngKey.Create(CngAlgorithm.Rsa, KeyName, parameters);
    }
}
