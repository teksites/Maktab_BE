using System.Security.Cryptography;
using System.Text;
using Helcim.Configuration;
using Helcim.Services;

namespace Helcim.Implementation.Services;

public sealed class HelcimCardTokenProtector : IHelcimCardTokenProtector
{
    private readonly IHelcimCardVaultConfiguration _configuration;

    public HelcimCardTokenProtector(IHelcimCardVaultConfiguration configuration)
        => _configuration = configuration;

    public ProtectedHelcimCardToken Protect(string cardToken)
    {
        EnsureEnabled();
        if (string.IsNullOrWhiteSpace(cardToken))
            throw new ArgumentException("A Helcim card token is required.", nameof(cardToken));

        var plaintext = Encoding.UTF8.GetBytes(cardToken);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var cipher = new AesGcm(_configuration.EncryptionKey, tag.Length);
        cipher.Encrypt(nonce, plaintext, ciphertext, tag);

        return new ProtectedHelcimCardToken
        {
            Ciphertext = ciphertext,
            Nonce = nonce,
            Tag = tag,
            Hash = Convert.ToHexString(SHA256.HashData(plaintext))
        };
    }

    public string Unprotect(ProtectedHelcimCardToken protectedToken)
    {
        EnsureEnabled();
        ArgumentNullException.ThrowIfNull(protectedToken);
        var plaintext = new byte[protectedToken.Ciphertext.Length];
        using var cipher = new AesGcm(_configuration.EncryptionKey, protectedToken.Tag.Length);
        cipher.Decrypt(protectedToken.Nonce, protectedToken.Ciphertext, protectedToken.Tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public string CreateCardFingerprint(string cardNumber)
    {
        EnsureEnabled();
        var digits = new string((cardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
            throw new ArgumentException("Helcim must return the card first-six and last-four digits.", nameof(cardNumber));

        // HMAC prevents the stored identity from being useful outside this card vault.
        return Convert.ToHexString(HMACSHA256.HashData(_configuration.EncryptionKey, Encoding.UTF8.GetBytes(digits)));
    }

    private void EnsureEnabled()
    {
        if (!_configuration.Enabled)
            throw new InvalidOperationException("Saved-card payments are disabled.");
    }
}
