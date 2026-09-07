using Moq;
using Stripe.Contracts;
using Stripe.Implementation.Services;
using Stripe.Repository;
using Stripe.Services;

namespace Courses.Test;

public class StripeWebhookServiceTests
{
    [Fact]
    public async Task HandleAsync_WhenReserved_InvokesHandlerAndMarksProcessed()
    {
        var payments = new Mock<IStripePaymentService>();
        payments.Setup(service => service.VerifyWebhook("{}", "signature"))
            .Returns(new StripeWebhookEventResponse { EventId = "evt_1", EventType = "payment_intent.succeeded" });
        var repository = new Mock<IStripeWebhookRepository>();
        repository.Setup(repo => repo.TryReserveAsync(It.IsAny<StripeWebhookReservation>()))
            .ReturnsAsync(StripeWebhookReservationResult.Reserved);
        var handler = new Mock<IStripeWebhookEventHandler>();

        var service = new StripeWebhookService(payments.Object, repository.Object, new[] { handler.Object });
        var result = await service.HandleAsync("{}", "signature");

        Assert.Equal(StripeWebhookHandlingStatus.Processed, result.Status);
        handler.Verify(item => item.HandleAsync(It.IsAny<StripeWebhookHandlingResult>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.UpdateAsync(It.Is<StripeWebhookProcessingUpdate>(update => update.Status == StripeWebhookProcessingStatus.Processed)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyProcessed_DoesNotInvokeHandler()
    {
        var payments = new Mock<IStripePaymentService>();
        payments.Setup(service => service.VerifyWebhook("{}", "signature"))
            .Returns(new StripeWebhookEventResponse { EventId = "evt_2", EventType = "payment_intent.succeeded" });
        var repository = new Mock<IStripeWebhookRepository>();
        repository.Setup(repo => repo.TryReserveAsync(It.IsAny<StripeWebhookReservation>()))
            .ReturnsAsync(StripeWebhookReservationResult.AlreadyProcessed);
        var handler = new Mock<IStripeWebhookEventHandler>();

        var result = await new StripeWebhookService(payments.Object, repository.Object, new[] { handler.Object }).HandleAsync("{}", "signature");

        Assert.Equal(StripeWebhookHandlingStatus.Duplicate, result.Status);
        handler.Verify(item => item.HandleAsync(It.IsAny<StripeWebhookHandlingResult>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(item => item.UpdateAsync(It.IsAny<StripeWebhookProcessingUpdate>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerFails_MarksEventFailedAndRethrows()
    {
        var payments = new Mock<IStripePaymentService>();
        payments.Setup(service => service.VerifyWebhook("{}", "signature"))
            .Returns(new StripeWebhookEventResponse { EventId = "evt_3", EventType = "payment_intent.succeeded" });
        var repository = new Mock<IStripeWebhookRepository>();
        repository.Setup(repo => repo.TryReserveAsync(It.IsAny<StripeWebhookReservation>()))
            .ReturnsAsync(StripeWebhookReservationResult.Reserved);
        var handler = new Mock<IStripeWebhookEventHandler>();
        handler.Setup(item => item.HandleAsync(It.IsAny<StripeWebhookHandlingResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ledger unavailable"));

        var service = new StripeWebhookService(payments.Object, repository.Object, new[] { handler.Object });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleAsync("{}", "signature"));
        repository.Verify(item => item.UpdateAsync(It.Is<StripeWebhookProcessingUpdate>(update => update.Status == StripeWebhookProcessingStatus.Failed)), Times.Once);
    }
}
