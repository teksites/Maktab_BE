namespace Stripe.Repository;

public interface IStripeWebhookRepository
{
    Task<StripeWebhookReservationResult> TryReserveAsync(StripeWebhookReservation reservation);
    Task UpdateAsync(StripeWebhookProcessingUpdate update);
}
