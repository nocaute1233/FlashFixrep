namespace FlashFix.Client;

public interface IDeviceProofProvider
{
    string PublicKey { get; }
    string Sign(byte[] payload);
}
