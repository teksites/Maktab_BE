namespace Helcim.Repository;

public sealed class HelcimCheckoutContext
{
    public string InvoiceNumber { get; init; } = string.Empty;
    public string? CheckoutToken { get; init; }
    public Guid UserId { get; init; }
    public Guid FamilyId { get; init; }
    public bool SaveCardInfo { get; init; }
}

public interface IHelcimCheckoutContextRepository
{
    Task Save(HelcimCheckoutContext context);
    Task<HelcimCheckoutContext?> Get(string invoiceNumber);
    Task<HelcimCheckoutContext?> GetByCheckoutToken(string checkoutToken);
    Task Deactivate(string invoiceNumber);
}
