using System.Security.Cryptography;
using System.Text;
using FlashFix.Core.Updates;

namespace FlashFix.Core.Tests;

public sealed class ReleaseIntegrityTests
{
    [Fact]
    public async Task SignedManifestAcceptsOnlyTheExpectedPackage()
    {
        using var key = RSA.Create(2048);
        var package = Encoding.UTF8.GetBytes("FlashFix test package");
        var unsigned = new SignedReleaseManifest("1.2.3", "https://updates.flashfix.example/releases/1.2.3.zip",
            Convert.ToHexString(SHA256.HashData(package)), "");
        var signed = unsigned with
        {
            Signature = Convert.ToBase64String(key.SignData(
                ReleaseIntegrity.SigningPayload(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
        };

        var verified = ReleaseIntegrity.Verify(signed, key);
        Assert.NotNull(verified);
        Assert.True(await verified.VerifyPackageAsync(new MemoryStream(package)));
        Assert.False(await verified.VerifyPackageAsync(new MemoryStream(Encoding.UTF8.GetBytes("altered"))));
        Assert.Null(ReleaseIntegrity.Verify(signed with { Version = "1.2.4" }, key));
        Assert.Null(ReleaseIntegrity.Verify(signed with { PackageUrl = "http://updates.flashfix.example/a.zip" }, key));
        Assert.Null(ReleaseIntegrity.Verify(signed with { Sha256 = new string('A', 64) }, key));
    }
}
