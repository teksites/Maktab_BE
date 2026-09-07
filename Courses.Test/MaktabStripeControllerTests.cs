using Courses.Services;
using Maktab.Contracts;
using Maktab.Controllers;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Stripe;
using Stripe.Contracts;
using Stripe.Services;
using Users.Services;

namespace Courses.Test;

public class MaktabStripeControllerTests
{
    [Fact]
    public async Task CreateRefund_WhenOriginalStripeCreditBelongsToTransaction_CreatesRefund()
    {
        var transactionId = Guid.NewGuid();
        var transactions = CreateTransactions(transactionId);
        var payments = new Mock<ICoursePaymentService>();
        payments.Setup(service => service.GetAllPaymentsByStudentTransactionId(transactionId)).ReturnsAsync(new[]
        {
            new CoursePaymentResponse
            {
                PaymentId = Guid.NewGuid(), IsActive = true, PaymentMode = PaymentMode.Stripe,
                PaymentType = PaymentType.Credit, ExternalPaymentId = "pi_owned", AmountPaid = 150m
            }
        });
        var stripe = new Mock<IStripePaymentService>();
        stripe.Setup(service => service.CreateRefundAsync(It.IsAny<StripeRefundCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeRefundResponse { RefundId = "re_123", Status = "pending", AmountMinor = 5000, Currency = "cad", PaymentIntentId = "pi_owned" });

        var result = await CreateController(transactions.Object, stripe.Object, payments.Object).CreateRefund(new CreateStripeRefundRequest
        {
            StudentCourseTransactionId = transactionId, PaymentIntentId = "pi_owned", Amount = 50m, IdempotencyKey = "refund-retry-key"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("re_123", Assert.IsType<StripeRefundResponse>(ok.Value).RefundId);
        stripe.Verify(service => service.CreateRefundAsync(It.Is<StripeRefundCreateRequest>(request =>
            request.PaymentIntentId == "pi_owned" && request.AmountMinor == 5000 && request.IdempotencyKey == "refund-retry-key"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRefund_WhenPaymentIntentDoesNotBelongToTransaction_RejectsWithoutCallingStripe()
    {
        var transactionId = Guid.NewGuid();
        var transactions = CreateTransactions(transactionId);
        var payments = new Mock<ICoursePaymentService>();
        payments.Setup(service => service.GetAllPaymentsByStudentTransactionId(transactionId)).ReturnsAsync(Array.Empty<CoursePaymentResponse>());
        var stripe = new Mock<IStripePaymentService>();

        var result = await CreateController(transactions.Object, stripe.Object, payments.Object).CreateRefund(new CreateStripeRefundRequest
        {
            StudentCourseTransactionId = transactionId, PaymentIntentId = "pi_other", Amount = 50m, IdempotencyKey = "refund-retry-key"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("not an active Maktab payment", Assert.IsType<string>(badRequest.Value));
        stripe.Verify(service => service.CreateRefundAsync(It.IsAny<StripeRefundCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateRefund_WhenStripeIsUnavailable_ReturnsRetryableBadGateway()
    {
        var transactionId = Guid.NewGuid();
        var transactions = CreateTransactions(transactionId);
        var payments = new Mock<ICoursePaymentService>();
        payments.Setup(service => service.GetAllPaymentsByStudentTransactionId(transactionId)).ReturnsAsync(new[]
        {
            new CoursePaymentResponse { IsActive = true, PaymentMode = PaymentMode.Stripe, PaymentType = PaymentType.Credit, ExternalPaymentId = "pi_owned" }
        });
        var stripe = new Mock<IStripePaymentService>();
        stripe.Setup(service => service.CreateRefundAsync(It.IsAny<StripeRefundCreateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeIntegrationException("Stripe is unavailable.") { IsUpstreamFailure = true });

        var result = await CreateController(transactions.Object, stripe.Object, payments.Object).CreateRefund(new CreateStripeRefundRequest
        {
            StudentCourseTransactionId = transactionId, PaymentIntentId = "pi_owned", Amount = 50m, IdempotencyKey = "refund-retry-key"
        });

        var gateway = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(502, gateway.StatusCode);
        var error = Assert.IsType<StripeApiErrorResponse>(gateway.Value).Error;
        Assert.True(error.Retryable);
        Assert.Equal("Stripe is unavailable.", error.Message);
    }

    private static Mock<IStudentCourseTransactionService> CreateTransactions(Guid transactionId)
    {
        var transactions = new Mock<IStudentCourseTransactionService>();
        transactions.Setup(service => service.GetTransaction(transactionId)).ReturnsAsync(new StudentCourseTransactionResponse
        {
            StudentCourseTransactionId = transactionId, FamilyId = Guid.NewGuid(), IsActive = true, TotalPayable = 150m, TotalAmountPaid = 150m
        });
        return transactions;
    }

    private static MaktabStripeController CreateController(IStudentCourseTransactionService transactions, IStripePaymentService stripe, ICoursePaymentService payments)
        => new(transactions, stripe, new Mock<IDataAccessVerificationService>().Object, payments);
}
