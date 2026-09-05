using Helcim;
using Helcim.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Courses.Test;

public class HelcimControllerTests
{
    [Fact]
    public async Task RefundTransaction_WhenDebitRefundBlocked_ReturnsBadRequestWithErrorBody()
    {
        var service = new Mock<IHelcimTransactionService>();
        service
            .Setup(x => x.RefundTransaction(It.IsAny<RefundTransactionRequest>()))
            .ThrowsAsync(new InvalidOperationException(
                "Helcim card transaction 2060 was processed as debit (cardType DB). Debit refunds must be completed in person using Helcim payment hardware."));

        var controller = new HelcimController(service.Object);

        var result = await controller.RefundTransaction(new RefundTransactionRequest
        {
            TransactionId = 2060,
            IpAddress = "192.168.1.3"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorObject = badRequest.Value;
        var errorMessage = Assert.IsType<HelcimRefundFailureResponse>(errorObject).Error;
        Assert.Contains("cardType DB", errorMessage);
        Assert.Contains("payment hardware", errorMessage);

        AssertRefundFailure(errorObject, "Maktab", isUpstreamFailure: false, retryable: false);
    }

    [Fact]
    public async Task RefundTransaction_WhenHelcimUpstreamFails_ReturnsBadGatewayWithErrorBody()
    {
        var service = new Mock<IHelcimTransactionService>();
        service
            .Setup(x => x.RefundTransaction(It.IsAny<RefundTransactionRequest>()))
            .ThrowsAsync(new HelcimRequestException(
                "Helcim POST request failed or returned an empty response for endpoint https://api.helcim.com/v2/payment/refund.",
                isUpstreamFailure: true));

        var controller = new HelcimController(service.Object);

        var result = await controller.RefundTransaction(new RefundTransactionRequest
        {
            TransactionId = 2061
        });

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(502, statusResult.StatusCode);
        var errorObject = statusResult.Value;
        var errorMessage = Assert.IsType<HelcimRefundFailureResponse>(errorObject).Error;
        Assert.Contains("empty response", errorMessage);

        AssertRefundFailure(errorObject, "Helcim", isUpstreamFailure: true, retryable: true);
    }

    [Fact]
    public async Task RefundCardTransaction_WhenDebitRefundBlocked_ReturnsBadRequestWithErrorBody()
    {
        var service = new Mock<IHelcimTransactionService>();
        service
            .Setup(x => x.RefundCardTransaction(It.IsAny<RefundCardTransactionRequest>()))
            .ThrowsAsync(new InvalidOperationException(
                "Helcim card transaction 2060 was processed as debit (cardType DB). Debit refunds must be completed in person using Helcim payment hardware."));

        var controller = new HelcimController(service.Object);

        var result = await controller.RefundCardTransaction(new RefundCardTransactionRequest
        {
            TransactionId = 2060,
            IpAddress = "192.168.1.3"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorObject = badRequest.Value;
        var errorMessage = Assert.IsType<HelcimRefundFailureResponse>(errorObject).Error;
        Assert.Contains("cardType DB", errorMessage);
        Assert.Contains("payment hardware", errorMessage);

        AssertRefundFailure(errorObject, "Maktab", isUpstreamFailure: false, retryable: false);
    }

    [Fact]
    public async Task RefundAchTransaction_WhenHelcimRejectsRefund_ReturnsProviderFailureDetails()
    {
        var service = new Mock<IHelcimTransactionService>();
        service
            .Setup(x => x.RefundAchTransaction(It.IsAny<RefundAchTransactionRequest>()))
            .ThrowsAsync(new HelcimRequestException(
                "The ACH transaction is no longer refundable.",
                isUpstreamFailure: false,
                rawResponse: "{\"status\":\"error\"}"));

        var controller = new HelcimController(service.Object);

        var result = await controller.RefundAchTransaction(new RefundAchTransactionRequest
        {
            TransactionId = 2062
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        AssertRefundFailure(badRequest.Value, "Helcim", isUpstreamFailure: false, retryable: false);
        var errorMessage = Assert.IsType<HelcimRefundFailureResponse>(badRequest.Value).Error;
        Assert.Equal("The ACH transaction is no longer refundable.", errorMessage);
    }

    private static void AssertRefundFailure(
        object? value,
        string expectedErrorSource,
        bool isUpstreamFailure,
        bool retryable)
    {
        var failure = Assert.IsType<HelcimRefundFailureResponse>(value);
        Assert.False(failure.Success);
        Assert.Equal(expectedErrorSource, failure.ErrorSource);
        Assert.Equal(isUpstreamFailure, failure.IsUpstreamFailure);
        Assert.Equal(retryable, failure.Retryable);
    }
}
