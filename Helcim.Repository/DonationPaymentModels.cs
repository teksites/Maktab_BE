using MaktabDataContracts.Enums.Helcim;

namespace Helcim.Repository;

// Immutable provider event snapshot. Refunds and reversals are stored as separate rows.
public sealed class DonationPaymentRecord
{
    public Guid DonationPaymentId { get; init; }
    public Guid PaymentContextId { get; init; }
    public Guid CampaignId { get; init; }
    public Guid? UserId { get; init; }
    public int HelcimTransactionId { get; init; }
    public int? HelcimInvoiceId { get; init; }
    public string HelcimInvoiceNumber { get; init; } = string.Empty;
    public string PaymentCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal NetAmount { get; init; }
    public HelcimCurrency Currency { get; init; }
    public HelcimInvoiceStatus InvoiceStatus { get; init; }
    public HelcimCardTransactionStatus TransactionStatus { get; init; }
    public HelcimCardTransactionType TransactionType { get; init; }
    public string CardCompany { get; init; } = string.Empty;
    public string CardFundingType { get; init; } = "Unknown";
    public bool CardFundingTypeKnown { get; init; }
    public string LastFourDigits { get; init; } = string.Empty;
    public string CardHolderName { get; init; } = string.Empty;
    public string CardType { get; init; } = string.Empty;
    public DateTime? PaidAt { get; init; }
}

public interface IDonationPaymentRepository
{
    Task AddOrUpdate(DonationPaymentRecord payment);
}
