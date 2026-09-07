using Stripe;
using Stripe.Configuration;
using Stripe.Contracts;
using Stripe.Implementation.Services;

namespace Courses.Test;

public class StripePaymentServiceTests
{
    [Fact]
    public async Task CreatePaymentIntent_WhenStripeIsDisabled_DoesNotCallProvider()
    {
        var service = CreateService(enabled: false);

        var exception = await Assert.ThrowsAsync<StripeIntegrationException>(() => service.CreatePaymentIntentAsync(
            new StripePaymentIntentCreateRequest
            {
                AmountMinor = 15000,
                Currency = "cad",
                IdempotencyKey = "payment-intent-001"
            }));

        Assert.Equal("Stripe integration is disabled.", exception.Message);
    }

    [Fact]
    public async Task CreatePaymentIntent_WhenAmountIsInvalid_RejectsBeforeProviderCall()
    {
        var service = CreateService(enabled: true);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.CreatePaymentIntentAsync(
            new StripePaymentIntentCreateRequest
            {
                AmountMinor = 0,
                Currency = "cad",
                IdempotencyKey = "payment-intent-002"
            }));
    }

    [Fact]
    public async Task CreateRefund_WhenBothPaymentReferencesAreProvided_RejectsRequest()
    {
        var service = CreateService(enabled: true);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRefundAsync(
            new StripeRefundCreateRequest
            {
                PaymentIntentId = "pi_123",
                ChargeId = "ch_123",
                IdempotencyKey = "refund-001"
            }));

        Assert.Contains("exactly one", exception.Message);
    }

    [Fact]
    public void VerifyWebhook_WhenSignatureIsMissing_RejectsBeforeProviderVerification()
    {
        var service = CreateService(enabled: true);

        Assert.Throws<ArgumentException>(() => service.VerifyWebhook("{}", string.Empty));
    }

    private static StripePaymentService CreateService(bool enabled)
        => new(new TestStripeConfiguration { Enabled = enabled });

    private class TestStripeConfiguration : IStripeConfiguration
    {
        public bool Enabled { get; init; }
        public string SecretKey { get; init; } = "sk_test_not_used";
        public string PublishableKey { get; init; } = "pk_test_not_used";
        public string WebhookSigningSecret { get; init; } = "whsec_not_used";
    }
}
