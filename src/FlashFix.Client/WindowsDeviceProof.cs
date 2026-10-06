using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace FlashFix.Client;

[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceProof : IDeviceProofProvider
{
    private static readonly CngProvider PlatformProvider = new("Microsoft Platform Crypto Provider");
    private static readonly CngProvider SoftwareProvider = CngProvider.MicrosoftSoftwareKeyStorageProvider;
    private readonly string _keyName;
    private readonly Lazy<CngProvider> _provider;

    public WindowsDeviceProof(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("O nome da chave é obrigatório.", nameof(keyName));
        _keyName = keyName;
        _provider = new Lazy<CngProvider>(ResolveProvider);
    }

    public string PublicKey
    {
        get
        {
            using var key = CngKey.Open(_keyName, _provider.Value);
            using var rsa = new RSACng(key);
            return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        }
    }

    public string Sign(byte[] payload)
    {
        using var key = CngKey.Open(_keyName, _provider.Value);
        using var rsa = new RSACng(key);
        return Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    private CngProvider ResolveProvider()
    {
        try
        {
            if (CngKey.Exists(_keyName, PlatformProvider))
                return PlatformProvider;
        }
        catch (CryptographicException) { }
        if (CngKey.Exists(_keyName, SoftwareProvider))
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

    private void CreateKey(CngProvider provider)
    {
        var parameters = new CngKeyCreationParameters
        {
            Provider = provider,
            ExportPolicy = CngExportPolicies.None,
            KeyUsage = CngKeyUsages.Signing
        };
        parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None));
        using var key = CngKey.Create(CngAlgorithm.Rsa, _keyName, parameters);
    }
}
