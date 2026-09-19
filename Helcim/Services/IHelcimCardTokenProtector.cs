namespace Helcim.Services;

public interface IHelcimCardTokenProtector
{
    ProtectedHelcimCardToken Protect(string cardToken);
    string Unprotect(ProtectedHelcimCardToken protectedToken);
    string CreateCardFingerprint(string cardNumber);
}

public sealed class ProtectedHelcimCardToken
{
    public byte[] Ciphertext { get; init; } = Array.Empty<byte>();
    public byte[] Nonce { get; init; } = Array.Empty<byte>();
    public byte[] Tag { get; init; } = Array.Empty<byte>();
    public string Hash { get; init; } = string.Empty;
}
