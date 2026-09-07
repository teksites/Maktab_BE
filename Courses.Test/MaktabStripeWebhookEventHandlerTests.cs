using Courses.Services;
using Maktab.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using Moq;
using Stripe.Contracts;

namespace Courses.Test;

public class MaktabStripeWebhookEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenSucceededIntentIsNew_AddsStripeCredit()
    {
        var transactionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var transactions = CreateTransactions(transactionId, familyId, totalPayable: 150m, totalPaid: 0m);
        var payments = new Mock<ICoursePaymentService>();
        payments.Setup(service => service.GetAllPaymentsByStudentTransactionId(transactionId))
            .ReturnsAsync(Array.Empty<CoursePaymentResponse>());
        payments.Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .ReturnsAsync((new CoursePaymentResponse(), true));

        await new MaktabStripeWebhookEventHandler(transactions.Object, payments.Object)
            .HandleAsync(CreateSucceededPaymentEvent(transactionId, familyId, "pi_new", 15000));

        payments.Verify(service => service.TryAddPayment(It.Is<AddCoursePayment>(payment =>
            payment.PaymentMode == PaymentMode.Stripe
            && payment.PaymentType == PaymentType.Credit
            && payment.ExternalPaymentId == "pi_new"
            && payment.AmountPaid == 150m)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSucceededIntentWasAlreadyRecorded_DoesNotAddAgain()
    {
        var transactionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var transactions = CreateTransactions(transactionId, familyId, totalPayable: 150m, totalPaid: 150m);
        var payments = new Mock<ICoursePaymentService>();
        payments.Setup(service => service.GetAllPaymentsByStudentTransactionId(transactionId))
            .ReturnsAsync(new[]
            {
                new CoursePaymentResponse
                {
                    IsActive = true,
                    PaymentMode = PaymentMode.Stripe,
                    PaymentType = PaymentType.Credit,
                    ExternalPaymentId = "pi_existing"
                }
            });

        await new MaktabStripeWebhookEventHandler(transactions.Object, payments.Object)
            .HandleAsync(CreateSucceededPaymentEvent(transactionId, familyId, "pi_existing", 15000));

        payments.Verify(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenSucceededIntentExceedsOutstanding_FailsWithoutLedgerMutation()
    {
        var transactionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var transactions = CreateTransactions(transactionId, familyId, totalPayable: 150m, totalPaid: 100m);
        var payments = new Mock<ICoursePaymentService>();
        payments.Setup(service => service.GetAllPaymentsByStudentTransactionId(transactionId))
            .ReturnsAsync(Array.Empty<CoursePaymentResponse>());

        var handler = new MaktabStripeWebhookEventHandler(transactions.Object, payments.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(CreateSucceededPaymentEvent(transactionId, familyId, "pi_overpay", 6000)));

        payments.Verify(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()), Times.Never);
    }

    private static Mock<IStudentCourseTransactionService> CreateTransactions(Guid transactionId, Guid familyId, decimal totalPayable, decimal totalPaid)
    {
        var transactions = new Mock<IStudentCourseTransactionService>();
        transactions.Setup(service => service.GetTransaction(transactionId))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = transactionId,
                FamilyId = familyId,
                IsActive = true,
                TotalPayable = totalPayable,
                TotalAmountPaid = totalPaid
            });
        return transactions;
    }

    private static StripeWebhookHandlingResult CreateSucceededPaymentEvent(Guid transactionId, Guid familyId, string paymentIntentId, long amountReceivedMinor)
    {
        var referenceData = $"{{\"application\":\"maktab\",\"version\":1,\"studentCourseTransactionId\":\"{transactionId}\",\"familyId\":\"{familyId}\"}}";
        return new StripeWebhookHandlingResult
        {
            EventType = "payment_intent.succeeded",
            RawPayload = $"{{\"data\":{{\"object\":{{\"id\":\"{paymentIntentId}\",\"amount_received\":{amountReceivedMinor},\"metadata\":{{\"reference_data\":{System.Text.Json.JsonSerializer.Serialize(referenceData)}}}}}}}}}"
        };
    }
}
