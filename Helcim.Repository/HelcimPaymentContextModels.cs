using MaktabDataContracts.Enums;

namespace Helcim.Repository;

public sealed class HelcimPaymentContext
{
    public Guid PaymentContextId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string PaymentCode { get; init; } = string.Empty;
    public Guid MaktabTransactionId { get; init; }
    public PaymentInitiationType PaymentType { get; init; } = PaymentInitiationType.Course;
    public Guid? CampaignId { get; init; }
    public Guid? UserId { get; init; }
    public Guid? FamilyId { get; init; }
    public bool SaveCardInfo { get; init; }
    public decimal Amount { get; init; }
}

public interface IHelcimPaymentContextRepository
{
    Task Save(HelcimPaymentContext context);
    Task<HelcimPaymentContext?> GetByInvoiceNumber(string invoiceNumber);
    Task<HelcimPaymentContext?> GetByPaymentContextId(Guid paymentContextId);
    Task<HelcimPaymentContext?> GetByPaymentCode(string paymentCode);
}
