namespace Helcim.Repository;

public sealed class HelcimSavedCardRecord
{
    public Guid CardId { get; init; }
    public Guid UserId { get; init; }
    public Guid FamilyId { get; init; }
    public byte[] TokenCiphertext { get; init; } = Array.Empty<byte>();
    public byte[] TokenNonce { get; init; } = Array.Empty<byte>();
    public byte[] TokenTag { get; init; } = Array.Empty<byte>();
    public string TokenHash { get; init; } = string.Empty;
    public string CardFingerprint { get; init; } = string.Empty;
    public string CardCompany { get; init; } = string.Empty;
    public string CardFundingType { get; init; } = "Unknown";
    public string LastFourDigits { get; init; } = string.Empty;
    public string CardHolderName { get; init; } = string.Empty;
    public int? ExpiryMonth { get; init; }
    public int? ExpiryYear { get; init; }
    public int? SourceHelcimTransactionId { get; init; }
    public bool IsDefault { get; init; }
    public DateTime CreatedAt { get; init; }
}

public interface IHelcimCardVaultRepository
{
    Task<IReadOnlyList<HelcimSavedCardRecord>> GetActiveCards(Guid userId);
    Task<HelcimSavedCardRecord?> GetActiveCard(Guid cardId, Guid userId);
    Task SaveOrReplace(HelcimSavedCardRecord card);
    Task SetDefault(Guid cardId, Guid userId);
    Task Deactivate(Guid cardId, Guid userId);
}
