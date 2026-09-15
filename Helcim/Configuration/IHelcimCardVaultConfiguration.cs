namespace Helcim.Configuration;

public interface IHelcimCardVaultConfiguration
{
    bool Enabled { get; }
    byte[] EncryptionKey { get; }
}
