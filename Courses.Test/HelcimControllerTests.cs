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
        Assert.NotNull(errorObject);
        Assert.IsNotType<string>(errorObject);
        var errorProperty = errorObject.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        var errorMessage = Assert.IsType<string>(errorProperty!.GetValue(errorObject));
        Assert.Contains("cardType DB", errorMessage);
        Assert.Contains("payment hardware", errorMessage);
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
        Assert.NotNull(errorObject);
        var errorProperty = errorObject!.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        var errorMessage = Assert.IsType<string>(errorProperty!.GetValue(errorObject));
        Assert.Contains("empty response", errorMessage);
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
        Assert.NotNull(errorObject);
        Assert.IsNotType<string>(errorObject);
        var errorProperty = errorObject.GetType().GetProperty("error");
        Assert.NotNull(errorProperty);
        var errorMessage = Assert.IsType<string>(errorProperty!.GetValue(errorObject));
        Assert.Contains("cardType DB", errorMessage);
        Assert.Contains("payment hardware", errorMessage);
    }
}
