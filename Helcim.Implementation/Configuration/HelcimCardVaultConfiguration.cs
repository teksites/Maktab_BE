using Helcim.Configuration;
using Microsoft.Extensions.Configuration;

namespace Helcim.Implementation.Configuration;

public sealed class HelcimCardVaultConfiguration : IHelcimCardVaultConfiguration
{
    public bool Enabled { get; }
    public byte[] EncryptionKey { get; }

    public HelcimCardVaultConfiguration(IConfiguration configuration)
    {
        Enabled = bool.TryParse(configuration["Helcim:CardVault:Enabled"], out var enabled) && enabled;
        var configuredKey = configuration["Helcim:CardVault:EncryptionKey"];
        if (!Enabled)
        {
            EncryptionKey = Array.Empty<byte>();
            return;
        }

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "Helcim:CardVault:EncryptionKey must be configured before saved-card payments are enabled.");
        }

        try
        {
            EncryptionKey = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Helcim:CardVault:EncryptionKey must be Base64 encoded.", exception);
        }

        if (EncryptionKey.Length != 32)
        {
            throw new InvalidOperationException("Helcim:CardVault:EncryptionKey must decode to exactly 32 bytes.");
        }
    }
}
