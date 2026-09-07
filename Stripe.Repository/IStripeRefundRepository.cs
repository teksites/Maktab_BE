namespace Stripe.Repository;

public interface IStripeRefundRepository
{
    Task<StripeRefundRecord?> GetByIdempotencyKeyAsync(string idempotencyKey);
    Task<StripeRefundRecord?> GetByStripeRefundIdAsync(string stripeRefundId);
    Task ReserveAsync(StripeRefundRecord record);
    Task UpdateAsync(StripeRefundRecord record);
}
