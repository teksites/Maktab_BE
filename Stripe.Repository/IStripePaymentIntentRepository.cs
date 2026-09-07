namespace Stripe.Repository;

public interface IStripePaymentIntentRepository
{
    Task<StripePaymentIntentRecord?> GetByIdempotencyKeyAsync(string idempotencyKey);
    Task<StripePaymentIntentRecord?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId);
    Task ReserveAsync(StripePaymentIntentRecord record);
    Task UpdateAsync(StripePaymentIntentRecord record);
}
