using Helcim.Configuration;
using Helcim.Implementation.Services;
using Helcim.Repository;
using InternalContracts;
using MaktabDataContracts.Enums.Helcim;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;
using Moq;
using Newtonsoft.Json.Linq;
using WebMsgSender;

namespace Courses.Test;

public class HelcimTransactionServiceTests
{
    [Fact]
    public async Task InitializePayment_BuildsExpectedPayloadAndReturnsTokens()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByMaktabTransactionId(It.IsAny<Guid>()))
            .ReturnsAsync(new List<HelcimTransactionResponse>
            {
                new() { InvoiceNumber = "INV-PAY001-20260505120000-2" }
            });
        repository
            .Setup(repo => repo.GetByPaymentCode("PAY001"))
            .ReturnsAsync(new List<HelcimTransactionResponse>
            {
                new() { InvoiceNumber = "INV-PAY001-20260505121000-5" },
                new() { InvoiceNumber = "INV-OTHER-20260505121000-9" }
            });

        JsonMessageData? capturedPayload = null;
        var sender = new Mock<IWebMsgSenderService>();
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Post))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedPayload = payload)
            .ReturnsAsync("{\"secretToken\":\"sec_123\",\"checkoutToken\":\"chk_456\"}");

        var service = new HelcimTransactionService(
            repository.Object,
            new TestHelcimClientConfiguration(),
            sender.Object);

        var request = new InitiatePaymentRequest
        {
            PaymentCode = "PAY001",
            Amount = 99,
            TransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserIp = "127.0.0.1"
        };

        var response = await service.InitializePayment(request);

        Assert.Equal("sec_123", response.SecretToken);
        Assert.Equal("chk_456", response.CheckoutToken);
        Assert.NotNull(capturedPayload);
        Assert.Equal("https://api.helcim.com/v2/helcim-pay/initialize", capturedPayload!.ExternalEndpoint);
        Assert.Equal("application/json", capturedPayload.Payload!.Headers.ContentType!.MediaType);
        Assert.Equal("application/json", capturedPayload.Headers!["accept"]);
        Assert.Equal("test-token", capturedPayload.Headers["api-token"]);

        var payloadJson = await capturedPayload.Payload.ReadAsStringAsync();
        var payload = JObject.Parse(payloadJson);

        Assert.True(payload["invoiceNumber"] is null || payload["invoiceNumber"]!.Type == JTokenType.Null);
        Assert.Equal("purchase", payload["paymentType"]!.Value<string>());
        Assert.Equal("CAD", payload["currency"]!.Value<string>());
        Assert.Equal("cc-ach", payload["paymentMethod"]!.Value<string>());
        Assert.Equal(99m, payload["amount"]!.Value<decimal>());
        Assert.Matches(@"^INV-PAY001-\d{14}-6$", payload["invoiceRequest"]!["invoiceNumber"]!.Value<string>());
        Assert.Equal("PAY001", payload["invoiceRequest"]!["notes"]!.Value<string>());
        Assert.Equal("INVOICE", payload["invoiceRequest"]!["type"]!.Value<string>());
        Assert.Equal(request.TransactionId.ToString(), payload["invoiceRequest"]!["lineItems"]![0]!["sku"]!.Value<string>());
        Assert.Equal("payment", payload["invoiceRequest"]!["lineItems"]![0]!["description"]!.Value<string>());
        Assert.Equal(1m, payload["invoiceRequest"]!["lineItems"]![0]!["quantity"]!.Value<decimal>());
        Assert.Equal(99m, payload["invoiceRequest"]!["lineItems"]![0]!["price"]!.Value<decimal>());
        Assert.Equal(99m, payload["invoiceRequest"]!["lineItems"]![0]!["total"]!.Value<decimal>());
    }

    [Fact]
    public async Task HandleWebhook_FetchesHelcimDataAndSavesTransactionDetailsWithRawResponse()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByTransactionId(47889842))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        AddHelcimTransactionDetails? capturedDetails = null;
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Callback<AddHelcimTransactionDetails>(details => capturedDetails = details)
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        sender
            .SetupSequence(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync("{\"transactionId\":47889842,\"dateCreated\":\"2026-05-03 11:42:09\",\"cardBatchId\":6429263,\"status\":\"APPROVED\",\"user\":\"Helcim System\",\"type\":\"purchase\",\"amount\":99,\"currency\":\"CAD\",\"avsResponse\":\"X\",\"cvvResponse\":\"M\",\"cardType\":\"MC\",\"invoiceNumber\":\"ORD-20260503-CXYOW\",\"customerCode\":\"CST1010\",\"approvalCode\":\"T8E7ST\",\"cardToken\":\"zbsEjBVPQMmRs9I7EZTLEQ\",\"cardNumber\":\"5413330011\",\"cardHolderName\":\"malik ten\",\"warning\":\"\"}")
            .ReturnsAsync("[{\"invoiceId\":63677011,\"invoiceNumber\":\"ORD-20260503-CXYOW\",\"token\":\"cca8a4d3e05f1d91c28e94\",\"notes\":\"MWSWAQ\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":99,\"amountPaid\":99,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{\"sku\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":99,\"total\":99}]}]");

        var service = new HelcimTransactionService(
            repository.Object,
            new TestHelcimClientConfiguration(),
            sender.Object);

        await service.HandleWebhook(new HelcimCardTransactionWebhookResponse
        {
            Id = 47889842,
            Type = HelcimWebhookEventType.CardTransaction
        });

        Assert.NotNull(capturedDetails);
        Assert.Equal(63677011, capturedDetails!.InvoiceId);
        Assert.Equal("ORD-20260503-CXYOW", capturedDetails.InvoiceNumber);
        Assert.Equal("MWSWAQ", capturedDetails.PaymentCode);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), capturedDetails.MaktabTransactionId);
        Assert.Equal("127.0.0.1", capturedDetails.UserIp);
        Assert.Equal("cca8a4d3e05f1d91c28e94", capturedDetails.InvoiceToken);
        Assert.Equal(40499452, capturedDetails.CustomerId);
        Assert.Equal("CST1010", capturedDetails.CustomerCode);
        Assert.Equal(47889842, capturedDetails.TransactionId);
        Assert.Equal(6429263, capturedDetails.CardBatchId);
        Assert.Equal("Helcim System", capturedDetails.User);
        Assert.Equal("T8E7ST", capturedDetails.ApprovalCode);
        Assert.Equal("zbsEjBVPQMmRs9I7EZTLEQ", capturedDetails.CardToken);
        Assert.Equal("5413330011", capturedDetails.CardNumber);
        Assert.Equal("malik ten", capturedDetails.CardHolderName);
        Assert.Equal("MC", capturedDetails.CardType);
        Assert.Equal("X", capturedDetails.AvsResponse);
        Assert.Equal("M", capturedDetails.CvvResponse);
        Assert.Equal(string.Empty, capturedDetails.Warning);
        Assert.Equal(99m, capturedDetails.Amount);
        Assert.Equal(99m, capturedDetails.AmountPaid);
        Assert.Equal(HelcimCurrency.Cad, capturedDetails.Currency);
        Assert.Equal(HelcimInvoiceStatus.Paid, capturedDetails.InvoiceStatus);
        Assert.Equal(HelcimCardTransactionStatus.Approved, capturedDetails.CardTransactionStatus);
        Assert.Equal(HelcimInvoiceType.Invoice, capturedDetails.InvoiceType);
        Assert.Equal(HelcimCardTransactionType.Purchase, capturedDetails.CardTransactionType);
        Assert.True(capturedDetails.IsActive);

        var rawResponse = JObject.Parse(capturedDetails.RawResponse);
        Assert.NotNull(rawResponse["WebhookRequest"]);
        Assert.NotNull(rawResponse["CardTransactionResponse"]);
        Assert.NotNull(rawResponse["InvoiceResponse"]);
    }

    [Fact]
    public async Task HandleWebhook_DoesNothingWhenTransactionAlreadyExists()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByTransactionId(123))
            .ReturnsAsync(new List<HelcimTransactionResponse> { new() { TransactionId = 123, InvoiceNumber = "INV-PAY001-20260505121000-1" } });

        var sender = new Mock<IWebMsgSenderService>();

        var service = new HelcimTransactionService(
            repository.Object,
            new TestHelcimClientConfiguration(),
            sender.Object);

        await service.HandleWebhook(new HelcimCardTransactionWebhookResponse
        {
            Id = 123,
            Type = HelcimWebhookEventType.CardTransaction
        });

        sender.Verify(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), It.IsAny<HttpMethod>()), Times.Never);
        repository.Verify(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()), Times.Never);
    }

    private sealed class TestHelcimClientConfiguration : IHelcimClientConfiguration
    {
        public string BaseUrl { get; init; } = "https://api.helcim.com";
        public HttpClientType ClientType { get; init; } = HttpClientType.Helcim;
        public string RelativeUrl { get; init; } = "/v2/helcim-pay/initialize";
        public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
        public int RetryAttempts { get; init; } = 3;
        public string ApiToken { get; init; } = "test-token";
    }
}
