namespace Helcim.Repository;

public sealed class HelcimCheckoutContext
{
    public string InvoiceNumber { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public Guid FamilyId { get; init; }
    public bool SaveCardInfo { get; init; }
}

public interface IHelcimCheckoutContextRepository
{
    Task Save(HelcimCheckoutContext context);
    Task<HelcimCheckoutContext?> Get(string invoiceNumber);
}
