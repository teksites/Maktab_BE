using Courses.Services;
using Helcim.Configuration;
using Helcim.Implementation.Services;
using Helcim.Repository;
using Helcim.Services;
using Helcim;
using InternalContracts;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Enums.Helcim;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Helcim;
using MaktabDataContracts.Responses.Transactions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SystemTextJsonSerializer = System.Text.Json.JsonSerializer;
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
                new() { InvoiceNumber = "INV-PAY001-202605051200-2" }
            });
        repository
            .Setup(repo => repo.GetByPaymentCode("PAY001"))
            .ReturnsAsync(new List<HelcimTransactionResponse>
            {
                new() { InvoiceNumber = "INV-PAY001-202605051210-5" },
                new() { InvoiceNumber = "INV-OTHER-202605051210-9" }
            });

        JsonMessageData? capturedPayload = null;
        var sender = new Mock<IWebMsgSenderService>();
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Post))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedPayload = payload)
            .ReturnsAsync("{\"secretToken\":\"sec_123\",\"checkoutToken\":\"chk_456\"}");

        var service = CreateService(repository.Object, sender.Object);

        var request = new InitiatePaymentRequest
        {
            PaymentCode = "PAY001",
            Amount = 99,
            TransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserIp = "127.0.0.1"
        };

        var response = await service.InitializePayment(request);

        //Assert.Equal("sec_123", response.SecretToken);
        Assert.Equal("chk_456", response.CheckoutToken);
        Assert.NotNull(capturedPayload);
        Assert.Equal("https://api.helcim.com/v2/helcim-pay/initialize", capturedPayload!.ExternalEndpoint);
        Assert.Equal("application/json", capturedPayload.Payload!.Headers.ContentType!.MediaType);
        Assert.Equal("application/json", capturedPayload.Headers!["accept"]);
        Assert.Equal("test-token", capturedPayload.Headers["api-token"]);

        var payloadJson = await capturedPayload.Payload.ReadAsStringAsync();
        var payload = JObject.Parse(payloadJson);

        Assert.Null(payload["invoiceNumber"]);
        Assert.Equal("purchase", payload["paymentType"]!.Value<string>());
        Assert.Equal("CAD", payload["currency"]!.Value<string>());
        Assert.Equal("cc-ach", payload["paymentMethod"]!.Value<string>());
        Assert.Equal(1, payload["HelcimDigitalWalletRequest"]!.Value<int>());
        Assert.Equal(99m, payload["amount"]!.Value<decimal>());
        Assert.Matches(@"^INV-PAY001-\d{12}-6$", payload["invoiceRequest"]!["invoiceNumber"]!.Value<string>());
        Assert.Equal("PAY001", payload["invoiceRequest"]!["notes"]!.Value<string>());
        Assert.Equal("INVOICE", payload["invoiceRequest"]!["type"]!.Value<string>());
        Assert.Equal(request.TransactionId.ToString(), payload["invoiceRequest"]!["lineItems"]![0]!["sku"]!.Value<string>());
        Assert.Equal(request.UserIp, payload["invoiceRequest"]!["lineItems"]![0]!["description"]!.Value<string>());
        Assert.Equal(1m, payload["invoiceRequest"]!["lineItems"]![0]!["quantity"]!.Value<decimal>());
        Assert.Equal(99m, payload["invoiceRequest"]!["lineItems"]![0]!["price"]!.Value<decimal>());
        Assert.Equal(99m, payload["invoiceRequest"]!["lineItems"]![0]!["total"]!.Value<decimal>());
    }

    [Fact]
    public async Task InitializePayment_IncludesResolvedTerminalIdFromPaymentCode()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByMaktabTransactionId(It.IsAny<Guid>()))
            .ReturnsAsync(new List<HelcimTransactionResponse>());
        repository
            .Setup(repo => repo.GetByPaymentCode("PAY001"))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        JsonMessageData? capturedPayload = null;
        var sender = new Mock<IWebMsgSenderService>();
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Post))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedPayload = payload)
            .ReturnsAsync("{\"checkoutToken\":\"chk_456\"}");

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetTransactionByPaymentCode("PAY001"))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                    }
                }
            });

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetHelcimTerminalId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")))
            .ReturnsAsync(54181);

        var service = CreateService(
            repository.Object,
            sender.Object,
            courseService: courseService.Object,
            studentCourseTransactionService: transactionService.Object);

        await service.InitializePayment(
            new InitiatePaymentRequest
            {
                PaymentCode = "PAY001",
                Amount = 99,
                TransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserIp = "127.0.0.1"
            });

        var payloadJson = await capturedPayload!.Payload!.ReadAsStringAsync();
        var payload = JObject.Parse(payloadJson);

        Assert.Equal(54181, payload["terminalId"]!.Value<int>());
    }

    [Fact]
    public async Task InitializePayment_UsesTransactionIdToResolveTerminalIdWhenPaymentCodeIsMissing()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByMaktabTransactionId(It.IsAny<Guid>()))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        JsonMessageData? capturedPayload = null;
        var sender = new Mock<IWebMsgSenderService>();
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Post))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedPayload = payload)
            .ReturnsAsync("{\"checkoutToken\":\"chk_456\"}");

        var transactionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var courseId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetTransaction(transactionId))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = transactionId,
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = courseId
                    }
                }
            });

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetHelcimTerminalId(courseId))
            .ReturnsAsync(65432);

        var service = CreateService(
            repository.Object,
            sender.Object,
            courseService: courseService.Object,
            studentCourseTransactionService: transactionService.Object);

        await service.InitializePayment(new InitiatePaymentRequest
        {
            PaymentCode = string.Empty,
            Amount = 99,
            TransactionId = transactionId,
            UserIp = "127.0.0.1"
        });

        var payloadJson = await capturedPayload!.Payload!.ReadAsStringAsync();
        var payload = JObject.Parse(payloadJson);

        Assert.Equal(65432, payload["terminalId"]!.Value<int>());
    }

    [Fact]
    public async Task InitializePayment_DoesNotSerializeUnsetOptionalFields()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByMaktabTransactionId(It.IsAny<Guid>()))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        JsonMessageData? capturedPayload = null;
        var sender = new Mock<IWebMsgSenderService>();
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Post))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedPayload = payload)
            .ReturnsAsync("{\"secretToken\":\"sec_123\",\"checkoutToken\":\"chk_456\"}");

        var service = CreateService(repository.Object, sender.Object);

        await service.InitializePayment(new InitiatePaymentRequest
        {
            PaymentCode = string.Empty,
            Amount = 99,
            TransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserIp = string.Empty
        });

        Assert.NotNull(capturedPayload);

        var payloadJson = await capturedPayload!.Payload!.ReadAsStringAsync();
        var payload = JObject.Parse(payloadJson);

        Assert.Null(payload["invoiceNumber"]);
        Assert.Equal(1, payload["HelcimDigitalWalletRequest"]!.Value<int>());

        var invoiceRequestProperties = payload["invoiceRequest"]!.Children<JProperty>().Select(property => property.Name);
        var lineItemProperties = payload["invoiceRequest"]!["lineItems"]![0]!.Children<JProperty>().Select(property => property.Name);

        Assert.DoesNotContain("notes", invoiceRequestProperties);
        Assert.DoesNotContain("description", lineItemProperties);
    }

    [Fact]
    public async Task InitializePayment_RejectsNonPositiveResolvedTerminalId()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByMaktabTransactionId(It.IsAny<Guid>()))
            .ReturnsAsync(new List<HelcimTransactionResponse>());
        repository
            .Setup(repo => repo.GetByPaymentCode("PAY001"))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetTransactionByPaymentCode("PAY001"))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                    }
                }
            });

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetHelcimTerminalId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")))
            .ReturnsAsync(0);

        var service = CreateService(
            repository.Object,
            Mock.Of<IWebMsgSenderService>(),
            courseService: courseService.Object,
            studentCourseTransactionService: transactionService.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.InitializePayment(
            new InitiatePaymentRequest
            {
                PaymentCode = "PAY001",
                Amount = 99,
                TransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserIp = "127.0.0.1"
            }));
    }

    [Fact]
    public async Task InitializePayment_MatchesLiveAcceptedHelcimPayloadShape()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByMaktabTransactionId(It.IsAny<Guid>()))
            .ReturnsAsync(new List<HelcimTransactionResponse>());
        repository
            .Setup(repo => repo.GetByPaymentCode("HVW2TF"))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        JsonMessageData? capturedPayload = null;
        var sender = new Mock<IWebMsgSenderService>();
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Post))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedPayload = payload)
            .ReturnsAsync("{\"secretToken\":\"sec_123\",\"checkoutToken\":\"chk_456\"}");

        var service = CreateService(repository.Object, sender.Object);

        await service.InitializePayment(new InitiatePaymentRequest
        {
            PaymentCode = "HVW2TF",
            Amount = 100,
            TransactionId = Guid.Parse("c2a3c210-c2bf-c3a1-b608-c2a04cc2b7c3"),
            UserIp = "192.168.1.1"
        });

        Assert.NotNull(capturedPayload);

        var payloadJson = await capturedPayload!.Payload!.ReadAsStringAsync();
        var payload = JObject.Parse(payloadJson);

        Assert.Equal(
            new[] { "paymentType", "amount", "currency", "paymentMethod", "HelcimDigitalWalletRequest", "invoiceRequest" },
            payload.Properties().Select(property => property.Name).ToArray());

        Assert.Null(payload["invoiceNumber"]);
        Assert.Equal("purchase", payload["paymentType"]!.Value<string>());
        Assert.Equal(100m, payload["amount"]!.Value<decimal>());
        Assert.Equal("CAD", payload["currency"]!.Value<string>());
        Assert.Equal("cc-ach", payload["paymentMethod"]!.Value<string>());
        Assert.Equal(1, payload["HelcimDigitalWalletRequest"]!.Value<int>());

        var invoiceRequest = (JObject)payload["invoiceRequest"]!;
        Assert.Equal(
            new[] { "type", "lineItems", "invoiceNumber", "notes" },
            invoiceRequest.Properties().Select(property => property.Name).ToArray());
        Assert.Equal("INVOICE", invoiceRequest["type"]!.Value<string>());
        Assert.Equal("HVW2TF", invoiceRequest["notes"]!.Value<string>());
        Assert.Matches(@"^INV-HVW2TF-\d{12}-1$", invoiceRequest["invoiceNumber"]!.Value<string>());

        var lineItem = (JObject)invoiceRequest["lineItems"]![0]!;
        Assert.Equal(
            new[] { "sku", "quantity", "price", "total", "description" },
            lineItem.Properties().Select(property => property.Name).ToArray());
        Assert.Equal("c2a3c210-c2bf-c3a1-b608-c2a04cc2b7c3", lineItem["sku"]!.Value<string>());
        Assert.Equal(1m, lineItem["quantity"]!.Value<decimal>());
        Assert.Equal(100m, lineItem["price"]!.Value<decimal>());
        Assert.Equal(100m, lineItem["total"]!.Value<decimal>());
        Assert.Equal("192.168.1.1", lineItem["description"]!.Value<string>());
    }

    [Fact]
    public void HelcimPayInitializeResponse_SystemTextJsonSerialization_UsesCheckoutTokenPropertyName()
    {
        var response = new HelcimPayInitializeResponse
        {
            CheckoutToken = "chk_456"
        };

        var json = SystemTextJsonSerializer.Serialize(response);
        using var payload = JsonDocument.Parse(json);

        Assert.Equal("chk_456", payload.RootElement.GetProperty("checkoutToken").GetString());
        Assert.False(payload.RootElement.TryGetProperty("CheckoutToken", out _));
    }

    [Fact]
    public void HelcimPayInitializeRequest_DirectSerialization_OmitsDefaultEnumValuesFromUpdatedContract()
    {
        var request = new HelcimPayInitializeRequest
        {
            PaymentType = HelcimPaymentType.Purchase,
            Amount = 99,
            Currency = HelcimCurrency.Cad,
            TerminalId = 54181,
            PaymentMethod = HelcimPaymentMethod.CreditCardOrAch,
            InvoiceNumber = "INV-PAY001-202605051210-1",
            InvoiceRequest = new HelcimInvoiceRequest
            {
                InvoiceNumber = "INV-PAY001-202605051210-1",
                PaymentCode = null,
                Type = HelcimInvoiceType.Invoice,
                LineItems = new List<HelcimInvoiceLineItemRequest>
                {
                    new()
                    {
                        MaktabTransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        UserIp = null,
                        Quantity = 1m,
                        Price = 99m,
                        Total = 99m
                    }
                }
            }
        };

        var payload = JObject.Parse(JsonConvert.SerializeObject(request));

        Assert.Null(payload["paymentType"]);
        Assert.Null(payload["currency"]);
        Assert.Equal(54181, payload["terminalId"]!.Value<int>());
        Assert.Equal("cc-ach", payload["paymentMethod"]!.Value<string>());
        Assert.Equal("INV-PAY001-202605051210-1", payload["invoiceNumber"]!.Value<string>());
        Assert.Null(payload["invoiceRequest"]!["notes"]);
        Assert.Null(payload["invoiceRequest"]!["lineItems"]![0]!["description"]);
    }

    [Fact]
    public async Task HandleWebhook_FetchesHelcimDataAndSavesTransactionDetailsWithRawResponse()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        SetupWebhookProcessingDefaults(repository);
        repository
            .Setup(repo => repo.GetByTransactionId(47889842))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        AddHelcimTransactionDetails? capturedDetails = null;
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Callback<AddHelcimTransactionDetails>(details => capturedDetails = details)
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var capturedEndpoints = new List<string>();
        var responses = new Queue<string>(new[]
        {
            "{\"transactionId\":47889842,\"dateCreated\":\"2026-05-03 11:42:09\",\"cardBatchId\":6429263,\"status\":\"APPROVED\",\"user\":\"Helcim System\",\"type\":\"purchase\",\"amount\":99,\"currency\":\"CAD\",\"avsResponse\":\"X\",\"cvvResponse\":\"M\",\"cardType\":\"MC\",\"invoiceNumber\":\"ORD-20260503-CXYOW\",\"customerCode\":\"CST1010\",\"approvalCode\":\"T8E7ST\",\"cardToken\":\"zbsEjBVPQMmRs9I7EZTLEQ\",\"cardNumber\":\"5413330011\",\"cardHolderName\":\"malik ten\",\"warning\":\"\"}",
            "[{\"invoiceId\":63677011,\"invoiceNumber\":\"ORD-20260503-CXYOW\",\"token\":\"cca8a4d3e05f1d91c28e94\",\"notes\":\"MWSWAQ\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":99,\"amountPaid\":99,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{\"sku\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":99,\"total\":99}]}]"
        });

        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedEndpoints.Add(payload.ExternalEndpoint))
            .ReturnsAsync(() => responses.Dequeue());

        var service = CreateService(repository.Object, sender.Object);

        var rawBody = "{\"id\":47889842,\"type\":\"cardTransaction\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = 47889842,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_1",
            timestamp,
            CreateWebhookSignature("msg_1", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Processed, status);

        Assert.NotNull(capturedDetails);
        Assert.Equal(
            new[]
            {
                "https://api.helcim.com/v2/card-transactions/47889842",
                "https://api.helcim.com/v2/invoices/?invoiceNumber=ORD-20260503-CXYOW"
            },
            capturedEndpoints);
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
        Assert.Contains("\"webhookPayload\"", capturedDetails.RawResponse);
        Assert.Contains("\"invoiceResponse\"", capturedDetails.RawResponse);
        Assert.Contains("\"webhookPayload\"", capturedDetails.TransactionResponse);
        Assert.Contains("\"transactionResponse\"", capturedDetails.TransactionResponse);
    }

    [Fact]
    public async Task HandleWebhook_ForAchPayload_FetchesAchDataAndSavesTransactionDetails()
    {
        var invoiceId = 63677015;
        var transactionId = 1020;
        var paymentCode = "ACHPAY";
        var studentTransactionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var familyId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var repository = new Mock<IHelcimTransactionRepository>();
        SetupWebhookProcessingDefaults(repository);
        repository
            .Setup(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        AddHelcimTransactionDetails? capturedDetails = null;
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Callback<AddHelcimTransactionDetails>(details => capturedDetails = details)
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var capturedEndpoints = new List<string>();
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedEndpoints.Add(payload.ExternalEndpoint))
            .Returns<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) =>
            {
                return payload.ExternalEndpoint switch
                {
                    "https://api.helcim.com/v2/card-transactions/1020" => throw new InvalidOperationException("Not a card transaction"),
                    "https://api.helcim.com/v2/ach/transactions/1020" => Task.FromResult("{\"transaction\":{\"id\":1020,\"orderId\":63677015,\"dateCreated\":\"2023-04-20 13:56:31\",\"statusAuth\":\"PENDING\",\"statusClearing\":\"OPENED\",\"batchId\":5220,\"type\":\"WITHDRAWAL\",\"amount\":100.00,\"currency\":\"CAD\",\"customerCode\":\"CST1200\",\"approvalCode\":\"\",\"bankAccountNumber\":\"100200300\",\"bankToken\":\"fcc48b4cb9a8ecd6531b49\"}}"),
                    "https://api.helcim.com/v2/invoices/63677015" => Task.FromResult($"{{\"invoiceId\":{invoiceId},\"invoiceNumber\":\"INV000020\",\"token\":\"ach-token\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":100.00,\"amountPaid\":100.00,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":100.00,\"total\":100.00}}]}}"),
                    _ => Task.FromException<string>(new InvalidOperationException($"Unexpected endpoint {payload.ExternalEndpoint}"))
                };
            });

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode
            });

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object);

        var rawBody = "{\"id\":1020,\"type\":\"achTransaction\",\"invoiceId\":63677015,\"invoiceNumber\":\"INV000020\",\"statusAuth\":\"PENDING\",\"statusClearing\":\"OPENED\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var status = await service.HandleWebhook(
            rawBody,
            "msg_ach",
            timestamp,
            CreateWebhookSignature("msg_ach", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Processed, status);
        Assert.Equal(
            new[]
            {
                "https://api.helcim.com/v2/card-transactions/1020",
                "https://api.helcim.com/v2/ach/transactions/1020",
                "https://api.helcim.com/v2/invoices/63677015"
            },
            capturedEndpoints);
        Assert.NotNull(capturedDetails);
        Assert.Equal("ACH", capturedDetails!.CardType);
        Assert.Equal(transactionId, capturedDetails.TransactionId);
        Assert.Contains("\"webhookPayload\"", capturedDetails.RawResponse);
        Assert.Contains("\"webhookPayload\"", capturedDetails.TransactionResponse);
    }

    [Fact]
    public async Task CompleteHelcimPayPayment_ForAchResponse_FetchesAchDataAndSavesTransactionDetails()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByTransactionId(1020))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        AddHelcimTransactionDetails? capturedDetails = null;
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Callback<AddHelcimTransactionDetails>(details => capturedDetails = details)
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var capturedEndpoints = new List<string>();
        var responses = new Queue<string>(new[]
        {
            "[{\"invoiceId\":63677015,\"invoiceNumber\":\"INV000020\",\"token\":\"ach-token\",\"notes\":\"ACHPAY\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":100.00,\"amountPaid\":100.00,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{\"sku\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":100.00,\"total\":100.00}]}]",
            "{\"transaction\":{\"id\":1020,\"dateCreated\":\"2023-04-20 13:56:31\",\"statusAuth\":\"PENDING\",\"statusClearing\":\"OPENED\",\"batchId\":5220,\"type\":\"WITHDRAWAL\",\"amount\":100.00}}"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedEndpoints.Add(payload.ExternalEndpoint))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode("ACHPAY"))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                FamilyId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                PaymentCode = "ACHPAY"
            });

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object);

        using var jsonDocument = JsonDocument.Parse("{\"transactionId\":\"1020\",\"invoiceNumber\":\"INV000020\",\"amount\":\"100.00\",\"approvalCode\":\"\",\"bankAccountNumber\":\"100200300\",\"bankToken\":\"fcc48b4cb9a8ecd6531b49\",\"batchId\":\"5220\",\"currency\":\"CAD\",\"customerCode\":\"CST1200\",\"dateCreated\":\"2023-04-20 13:56:31\",\"statusAuth\":\"PENDING\",\"statusClearing\":\"OPENED\",\"type\":\"WITHDRAWAL\"}");
        var result = await service.CompleteHelcimPayPayment(new CompleteHelcimPayPaymentRequest
        {
            RawDataResponse = jsonDocument.RootElement.Clone()
        });

        Assert.True(result.Success);
        Assert.False(result.Duplicate);
        Assert.Equal(63677015, result.InvoiceId);
        Assert.Equal("ach", result.PaymentFlow);
        Assert.Equal(1020, result.TransactionId);
        Assert.Equal("INV000020", result.InvoiceNumber);
        Assert.Equal(
            new[]
            {
                "https://api.helcim.com/v2/invoices/?invoiceNumber=INV000020",
                "https://api.helcim.com/v2/ach/transactions/1020"
            },
            capturedEndpoints);
        Assert.NotNull(capturedDetails);
        Assert.Equal("ACHPAY", capturedDetails!.PaymentCode);
        Assert.Equal("CST1200", capturedDetails.CustomerCode);
        Assert.Equal(1020, capturedDetails.TransactionId);
        Assert.Equal(5220, capturedDetails.CardBatchId);
        Assert.Equal("ACH", capturedDetails.CardType);
        Assert.Equal("****0300", capturedDetails.CardNumber);
        Assert.Equal("fcc48b4cb9a8ecd6531b49", capturedDetails.CardToken);
        Assert.Equal(100m, capturedDetails.Amount);
        Assert.Equal(HelcimCurrency.Cad, capturedDetails.Currency);
        Assert.Equal(HelcimInvoiceStatus.Paid, capturedDetails.InvoiceStatus);
        Assert.Equal(HelcimCardTransactionType.Purchase, capturedDetails.CardTransactionType);
        Assert.Contains("\"statusAuth\":\"PENDING\"", capturedDetails.TransactionResponse);
    }

    [Fact]
    public async Task SyncInvoicePaymentByInvoiceId_ForCardInvoice_FetchesCardTransactionAndSavesPayment()
    {
        var invoiceId = 63677012;
        var transactionId = 47889843;
        var paymentCode = "HELPAID";
        var studentTransactionId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var familyId = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");

        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        AddHelcimTransactionDetails? capturedDetails = null;
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Callback<AddHelcimTransactionDetails>(details => capturedDetails = details)
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var capturedEndpoints = new List<string>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"invoiceId\":{invoiceId},\"invoiceNumber\":\"ORD-20260503-PAID\",\"token\":\"cca8a4d3e05f1d91c28e94\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":99,\"amountPaid\":99,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":99,\"total\":99}}]}}",
            $"[{{\"transactionId\":{transactionId},\"dateCreated\":\"2026-05-03 11:42:09\",\"cardBatchId\":6429263,\"status\":\"APPROVED\",\"user\":\"Helcim System\",\"type\":\"purchase\",\"amount\":99,\"currency\":\"CAD\",\"avsResponse\":\"X\",\"cvvResponse\":\"M\",\"cardType\":\"MC\",\"invoiceNumber\":\"ORD-20260503-PAID\",\"customerCode\":\"CST1010\",\"approvalCode\":\"T8E7ST\",\"cardToken\":\"zbsEjBVPQMmRs9I7EZTLEQ\",\"cardNumber\":\"5413330011\",\"cardHolderName\":\"malik ten\",\"warning\":\"\"}}]"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedEndpoints.Add(payload.ExternalEndpoint))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode
            });

        AddCoursePayment? capturedPayment = null;
        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync((new CoursePaymentResponse(), true));

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object,
            coursePaymentService: coursePaymentService.Object);

        var result = await service.SyncInvoicePaymentByInvoiceId(invoiceId);

        Assert.True(result.Success);
        Assert.False(result.Duplicate);
        Assert.Equal(invoiceId, result.InvoiceId);
        Assert.Equal(transactionId, result.TransactionId);
        Assert.Equal("card", result.PaymentFlow);
        Assert.Equal(
            new[]
            {
                $"https://api.helcim.com/v2/invoices/{invoiceId}",
                "https://api.helcim.com/v2/card-transactions?invoiceNumber=ORD-20260503-PAID"
            },
            capturedEndpoints);
        Assert.NotNull(capturedDetails);
        Assert.Equal(paymentCode, capturedDetails!.PaymentCode);
        Assert.Equal(familyId, capturedDetails.FamilyId);
        Assert.NotNull(capturedPayment);
        Assert.Equal(transactionId.ToString(), capturedPayment!.ExternalPaymentId);
        Assert.Equal(PaymentMode.Helcim, capturedPayment.PaymentMode);
    }

    [Fact]
    public async Task SyncInvoicePaymentByInvoiceId_ForAchInvoice_FetchesAchTransactionAndSavesPayment()
    {
        var invoiceId = 63677015;
        var transactionId = 1020;
        var paymentCode = "ACHPAY";
        var studentTransactionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var familyId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        AddHelcimTransactionDetails? capturedDetails = null;
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Callback<AddHelcimTransactionDetails>(details => capturedDetails = details)
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var capturedEndpoints = new List<string>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"invoiceId\":{invoiceId},\"invoiceNumber\":\"INV000020\",\"token\":\"ach-token\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":100.00,\"amountPaid\":100.00,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":100.00,\"total\":100.00}}]}}",
            "[]",
            $"[{{\"id\":{transactionId},\"orderId\":{invoiceId},\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":5220,\"type\":\"WITHDRAWAL\",\"amount\":100.00,\"dateClosed\":\"2026-05-04 10:00:00\"}}]",
            "{\"transaction\":{\"id\":1020,\"orderId\":63677015,\"dateCreated\":\"2023-04-20 13:56:31\",\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":5220,\"type\":\"WITHDRAWAL\",\"amount\":100.00,\"currency\":1,\"customerCode\":\"CST1200\",\"approvalCode\":\"\",\"bankAccountNumber\":\"100200300\",\"bankToken\":\"fcc48b4cb9a8ecd6531b49\",\"dateClosed\":\"2026-05-04 10:00:00\"}}"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedEndpoints.Add(payload.ExternalEndpoint))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode
            });

        AddCoursePayment? capturedPayment = null;
        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync((new CoursePaymentResponse(), true));

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object,
            coursePaymentService: coursePaymentService.Object);

        var result = await service.SyncInvoicePaymentByInvoiceId(invoiceId);

        Assert.True(result.Success);
        Assert.False(result.Duplicate);
        Assert.Equal(invoiceId, result.InvoiceId);
        Assert.Equal(transactionId, result.TransactionId);
        Assert.Equal("ach", result.PaymentFlow);
        Assert.Equal($"https://api.helcim.com/v2/invoices/{invoiceId}", capturedEndpoints[0]);
        Assert.Equal("https://api.helcim.com/v2/card-transactions?invoiceNumber=INV000020", capturedEndpoints[1]);
        Assert.Contains("https://api.helcim.com/v2/ach/transactions?startDate=", capturedEndpoints[2]);
        Assert.Contains("&page=1&limit=125", capturedEndpoints[2]);
        Assert.Equal("https://api.helcim.com/v2/ach/transactions/1020", capturedEndpoints[3]);
        Assert.NotNull(capturedDetails);
        Assert.Equal(paymentCode, capturedDetails!.PaymentCode);
        Assert.Equal("ACH", capturedDetails.CardType);
        Assert.Equal("****0300", capturedDetails.CardNumber);
        Assert.NotNull(capturedPayment);
        Assert.Equal(transactionId.ToString(), capturedPayment!.ExternalPaymentId);
        Assert.Equal(PaymentType.Credit, capturedPayment.PaymentType);
    }

    [Fact]
    public async Task SyncInvoicePaymentByInvoiceId_ForAchInvoice_PaginatesUntilTransactionIsFound()
    {
        var invoiceId = 63677017;
        var transactionId = 1022;
        var paymentCode = "ACHPAGE2";
        var studentTransactionId = Guid.Parse("aaaaaaaa-dddd-dddd-dddd-dddddddddddd");
        var firstPageTransactions = "[" + string.Join(",",
            Enumerable.Range(1, 125).Select(index =>
                $"{{\"id\":{9000 + index},\"orderId\":{80000000 + index},\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":{5200 + index},\"type\":\"WITHDRAWAL\",\"amount\":100.00,\"dateClosed\":\"2026-05-04 10:00:00\"}}")) + "]";

        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>());
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var capturedEndpoints = new List<string>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"invoiceId\":{invoiceId},\"invoiceNumber\":\"INV000022\",\"token\":\"ach-token-3\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":100.00,\"amountPaid\":100.00,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":100.00,\"total\":100.00}}]}}",
            "[]",
            firstPageTransactions,
            $"[{{\"id\":{transactionId},\"orderId\":{invoiceId},\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":5223,\"type\":\"WITHDRAWAL\",\"amount\":100.00,\"dateClosed\":\"2026-05-04 10:00:00\"}}]",
            "{\"transaction\":{\"id\":1022,\"orderId\":63677017,\"dateCreated\":\"2023-04-20 13:56:31\",\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":5223,\"type\":\"WITHDRAWAL\",\"amount\":100.00,\"currency\":1,\"customerCode\":\"CST1202\",\"approvalCode\":\"\",\"bankAccountNumber\":\"100200302\",\"bankToken\":\"bank-token-1022\",\"dateClosed\":\"2026-05-04 10:00:00\"}}"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedEndpoints.Add(payload.ExternalEndpoint))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = Guid.Parse("bbbbbbbb-dddd-dddd-dddd-dddddddddddd"),
                PaymentCode = paymentCode
            });

        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .ReturnsAsync((new CoursePaymentResponse(), true));

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object,
            coursePaymentService: coursePaymentService.Object);

        await service.SyncInvoicePaymentByInvoiceId(invoiceId);

        Assert.Equal($"https://api.helcim.com/v2/invoices/{invoiceId}", capturedEndpoints[0]);
        Assert.Equal("https://api.helcim.com/v2/card-transactions?invoiceNumber=INV000022", capturedEndpoints[1]);
        Assert.Contains("&page=1&limit=125", capturedEndpoints[2]);
        Assert.Contains("&page=2&limit=125", capturedEndpoints[3]);
        Assert.Equal("https://api.helcim.com/v2/ach/transactions/1022", capturedEndpoints[4]);
    }

    [Fact]
    public async Task SyncInvoicePaymentByInvoiceId_ForPendingAchInvoice_SavesDetailsWithoutAddingPayment()
    {
        var invoiceId = 63677016;
        var transactionId = 1021;
        var paymentCode = "ACHPENDING";
        var studentTransactionId = Guid.Parse("aaaaaaaa-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var familyId = Guid.Parse("bbbbbbbb-cccc-cccc-cccc-cccccccccccc");

        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>());

        AddHelcimTransactionDetails? capturedDetails = null;
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Callback<AddHelcimTransactionDetails>(details => capturedDetails = details)
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"invoiceId\":{invoiceId},\"invoiceNumber\":\"INV000021\",\"token\":\"ach-token-2\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":100.00,\"amountPaid\":100.00,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":100.00,\"total\":100.00}}]}}",
            "[]",
            $"[{{\"id\":{transactionId},\"orderId\":{invoiceId},\"statusAuth\":5,\"statusClearing\":0,\"statusBatch\":1,\"batchId\":5221,\"type\":\"WITHDRAWAL\",\"amount\":100.00}}]",
            "{\"transaction\":{\"id\":1021,\"orderId\":63677016,\"dateCreated\":\"2023-04-20 13:56:31\",\"statusAuth\":5,\"statusClearing\":0,\"statusBatch\":1,\"batchId\":5221,\"type\":\"WITHDRAWAL\",\"amount\":100.00,\"currency\":1,\"customerCode\":\"CST1201\",\"approvalCode\":\"\",\"bankAccountNumber\":\"100200301\",\"bankToken\":\"bank-token-1021\"}}"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode
            });

        AddCoursePayment? capturedPayment = null;
        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync((new CoursePaymentResponse(), true));

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object,
            coursePaymentService: coursePaymentService.Object);

        var result = await service.SyncInvoicePaymentByInvoiceId(invoiceId);

        Assert.True(result.Success);
        Assert.Equal("ach", result.PaymentFlow);
        Assert.NotNull(capturedDetails);
        Assert.Equal("ACH", capturedDetails!.CardType);
        Assert.Null(capturedPayment);
    }

    [Fact]
    public async Task RefundAchTransaction_ForClosedApprovedTransaction_SendsPutRequestWithIdempotencyKey()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        JsonMessageData? capturedPutPayload = null;

        var sender = new Mock<IWebMsgSenderService>();
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync("{\"transaction\":{\"id\":2040,\"orderId\":63677015,\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":5220,\"amount\":100.00,\"currency\":1,\"dateClosed\":\"2026-05-04 10:00:00\"}}");
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Put))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedPutPayload = payload)
            .ReturnsAsync("{\"status\":\"accepted\"}");

        var service = CreateService(repository.Object, sender.Object);

        var result = await service.RefundAchTransaction(new RefundAchTransactionRequest
        {
            TransactionId = 2040,
            Amount = 25m,
            IdempotencyKey = "refund-key-2040"
        });

        Assert.True(result.Success);
        Assert.Equal(2040, result.TransactionId);
        Assert.Equal(25m, result.Amount);
        Assert.Equal("refund-key-2040", result.IdempotencyKey);
        Assert.NotNull(capturedPutPayload);
        Assert.Equal("https://api.helcim.com/v2/ach/transactions/2040/refund", capturedPutPayload!.ExternalEndpoint);
        Assert.Equal("refund-key-2040", capturedPutPayload.Headers["idempotency-key"]);

        var payloadJson = await capturedPutPayload.Payload!.ReadAsStringAsync();
        Assert.Equal("{\"amount\":25.0}", payloadJson);
    }

    [Fact]
    public async Task RefundAchInvoice_AppliesImmediateRefundPaymentForRecalculation()
    {
        const int invoiceId = 63677015;
        const int transactionId = 2040;
        const string paymentCode = "ACHREFUND1";
        var studentTransactionId = Guid.Parse("aaaaaaaa-4444-4444-4444-444444444444");
        var familyId = Guid.Parse("bbbbbbbb-4444-4444-4444-444444444444");
        JsonMessageData? capturedPutPayload = null;

        var repository = new Mock<IHelcimTransactionRepository>();
        var sender = new Mock<IWebMsgSenderService>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"invoiceId\":{invoiceId},\"invoiceNumber\":\"ORD-20260802-ACHREFUND\",\"token\":\"tok-1\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-08-01 10:00:00\",\"dateUpdated\":\"2026-08-02 10:00:00\",\"datePaid\":\"2026-08-01 11:00:00\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":100.00,\"amountPaid\":100.00,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":100.00,\"total\":100.00}}]}}",
            $"[{{\"id\":{transactionId},\"orderId\":{invoiceId},\"invoiceNumber\":\"ORD-20260802-ACHREFUND\",\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":5220,\"amount\":100.00,\"currency\":1,\"dateCreated\":\"2026-08-01 11:00:00\",\"dateClosed\":\"2026-08-02 10:00:00\"}}]",
            $"[{{\"id\":{transactionId},\"orderId\":{invoiceId},\"invoiceNumber\":\"ORD-20260802-ACHREFUND\",\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":5220,\"amount\":100.00,\"currency\":1,\"dateCreated\":\"2026-08-01 11:00:00\",\"dateClosed\":\"2026-08-02 10:00:00\"}}]"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync(() => responses.Dequeue());
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Put))
            .Callback<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>((payload, _, _) => capturedPutPayload = payload)
            .ReturnsAsync("{\"status\":\"accepted\"}");

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode,
                Enrollments = new List<StudentCourseEnrollmentResponse>()
            });

        AddCoursePayment? capturedPayment = null;
        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync((new CoursePaymentResponse(), true));

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object,
            coursePaymentService: coursePaymentService.Object);

        var result = await service.RefundAchInvoice(new RefundAchInvoiceRequest
        {
            InvoiceId = invoiceId,
            Amount = 25m,
            IdempotencyKey = "invoice-refund-key"
        });

        Assert.True(result.Success);
        Assert.Equal(invoiceId, result.InvoiceId);
        Assert.Equal(transactionId, result.TransactionId);
        Assert.Equal("ORD-20260802-ACHREFUND", result.InvoiceNumber);
        Assert.True(result.LocalRefundRecorded);
        Assert.True(result.RequiresReconciliation);
        Assert.NotNull(capturedPutPayload);
        Assert.NotNull(capturedPayment);
        Assert.Equal(PaymentType.Refund, capturedPayment!.PaymentType);
        Assert.Equal(PaymentMode.Helcim, capturedPayment.PaymentMode);
        Assert.Equal("HEL-REFUND-2040", capturedPayment.ExternalPaymentId);
        Assert.Equal(studentTransactionId, capturedPayment.StudentCourseTransactionId);
        Assert.Equal(familyId, capturedPayment.FamilyId);
    }

    [Fact]
    public async Task GetAchRefundInvoices_WhenOnlyRefundable_ReturnsOnlyRefundablePurchaseTransactions()
    {
        const int refundableInvoiceId = 7001;
        const int refundableTransactionId = 3001;
        const string paymentCode = "REFLIST1";
        var studentTransactionId = Guid.Parse("aaaaaaaa-5555-5555-5555-555555555555");
        var familyId = Guid.Parse("bbbbbbbb-5555-5555-5555-555555555555");

        var repository = new Mock<IHelcimTransactionRepository>();
        var sender = new Mock<IWebMsgSenderService>();
        var responses = new Queue<string>(new[]
        {
            "[" +
            $"{{\"id\":{refundableTransactionId},\"orderId\":{refundableInvoiceId},\"invoiceNumber\":\"ORD-REF-LIST-1\",\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":700,\"amount\":120.00,\"currency\":1,\"dateCreated\":\"2026-08-01 09:00:00\",\"dateClosed\":\"2026-08-02 09:30:00\",\"customerCode\":\"CST1001\"}}," +
            "{\"id\":3002,\"orderId\":7002,\"invoiceNumber\":\"ORD-REF-LIST-2\",\"statusAuth\":1,\"statusClearing\":0,\"statusBatch\":1,\"batchId\":701,\"amount\":90.00,\"currency\":1,\"dateCreated\":\"2026-08-01 08:00:00\",\"customerCode\":\"CST1002\"}," +
            "{\"id\":3003,\"orderId\":7003,\"invoiceNumber\":\"ORD-REF-LIST-3\",\"statusAuth\":1,\"statusClearing\":1,\"statusBatch\":2,\"batchId\":702,\"amount\":30.00,\"currency\":1,\"dateCreated\":\"2026-08-01 07:00:00\",\"dateClosed\":\"2026-08-02 07:30:00\",\"customerCode\":\"CST1003\",\"originalTransactionId\":3001}" +
            "]",
            $"{{\"invoiceId\":{refundableInvoiceId},\"invoiceNumber\":\"ORD-REF-LIST-1\",\"token\":\"tok-2\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-08-01 08:00:00\",\"dateUpdated\":\"2026-08-02 10:00:00\",\"datePaid\":\"2026-08-02 09:30:00\",\"status\":\"PAID\",\"customerId\":40490001,\"amount\":120.00,\"amountPaid\":120.00,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":120.00,\"total\":120.00}}]}}"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode,
                Enrollments = new List<StudentCourseEnrollmentResponse>()
            });

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object);

        var result = await service.GetAchRefundInvoices(new GetAchRefundInvoicesRequest
        {
            StartDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 8, 2),
            OnlyRefundable = true
        });

        Assert.Single(result);
        Assert.Equal(refundableInvoiceId, result[0].InvoiceId);
        Assert.Equal(refundableTransactionId, result[0].TransactionId);
        Assert.Equal(paymentCode, result[0].PaymentCode);
        Assert.Equal(studentTransactionId, result[0].MaktabTransactionId);
        Assert.Equal(familyId, result[0].FamilyId);
        Assert.True(result[0].IsRefundable);
        Assert.False(result[0].IsRefundTransaction);
    }

    [Fact]
    public async Task HandleWebhook_DoesNothingWhenTransactionAlreadyExists()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        SetupWebhookProcessingDefaults(repository);
        repository
            .Setup(repo => repo.GetByTransactionId(123))
            .ReturnsAsync(new List<HelcimTransactionResponse> { new() { TransactionId = 123, InvoiceNumber = "INV-PAY001-202605051210-1" } });

        var sender = new Mock<IWebMsgSenderService>();

        var service = CreateService(repository.Object, sender.Object);

        var rawBody = "{\"id\":123,\"type\":\"cardTransaction\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = 123,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_2",
            timestamp,
            CreateWebhookSignature("msg_2", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Duplicate, status);
        sender.Verify(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), It.IsAny<HttpMethod>()), Times.Never);
        repository.Verify(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()), Times.Never);
    }

    [Fact]
    public async Task HandleWebhook_ReturnsInvalidSignatureWhenSignatureDoesNotMatch()
    {
        var service = CreateService(Mock.Of<IHelcimTransactionRepository>(), Mock.Of<IWebMsgSenderService>());

        var rawBody = "{\"id\":123,\"type\":\"cardTransaction\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = 123,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_invalid_sig",
            timestamp,
            "v1,invalid-signature");

        Assert.Equal(HelcimWebhookHandlingStatus.InvalidSignature, status);
    }

    [Fact]
    public async Task HandleWebhook_AcceptsAnyMatchingSignatureFromSignatureHeader()
    {
        var service = CreateService(Mock.Of<IHelcimTransactionRepository>(), Mock.Of<IWebMsgSenderService>());

        var rawBody = "{\"type\":\"terminalCancel\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var validSignature = CreateWebhookSignature("msg_multi_sig", timestamp, rawBody);

        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Type = HelcimWebhookEventType.TerminalCancel
            },
            rawBody,
            "msg_multi_sig",
            timestamp,
            $"v0,invalid-signature {validSignature}");

        Assert.Equal(HelcimWebhookHandlingStatus.Ignored, status);
    }

    [Fact]
    public async Task HandleWebhook_ReturnsInvalidTimestampWhenTimestampIsExpired()
    {
        var service = CreateService(Mock.Of<IHelcimTransactionRepository>(), Mock.Of<IWebMsgSenderService>());

        var rawBody = "{\"id\":123,\"type\":\"cardTransaction\"}";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();

        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = 123,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_expired",
            timestamp,
            CreateWebhookSignature("msg_expired", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.InvalidTimestamp, status);
    }

    [Fact]
    public async Task HandleWebhook_IgnoresNonCardTransactionWhenSignatureIsValid()
    {
        var sender = new Mock<IWebMsgSenderService>();
        var repository = new Mock<IHelcimTransactionRepository>();

        var service = CreateService(repository.Object, sender.Object);

        var rawBody = "{\"type\":\"terminalCancel\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Type = HelcimWebhookEventType.TerminalCancel
            },
            rawBody,
            "msg_terminal_cancel",
            timestamp,
            CreateWebhookSignature("msg_terminal_cancel", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Ignored, status);
        sender.Verify(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), It.IsAny<HttpMethod>()), Times.Never);
        repository.Verify(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()), Times.Never);
    }

    [Fact]
    public async Task HandleWebhook_ForPaidInvoice_AddsHelcimPaymentAndRecalculatesFee()
    {
        var transactionId = 47889843;
        var paymentCode = "HELPAID";
        var studentTransactionId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var familyId = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
        var courseId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");

        var repository = new Mock<IHelcimTransactionRepository>();
        SetupWebhookProcessingDefaults(repository);
        repository
            .Setup(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>());
        AddHelcimTransactionDetails? capturedDetails = null;
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Callback<AddHelcimTransactionDetails>(details => capturedDetails = details)
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"transactionId\":{transactionId},\"dateCreated\":\"2026-05-03 11:42:09\",\"cardBatchId\":6429263,\"status\":\"APPROVED\",\"user\":\"Helcim System\",\"type\":\"purchase\",\"amount\":99,\"currency\":\"CAD\",\"avsResponse\":\"X\",\"cvvResponse\":\"M\",\"cardType\":\"MC\",\"invoiceNumber\":\"ORD-20260503-PAID\",\"customerCode\":\"CST1010\",\"approvalCode\":\"T8E7ST\",\"cardToken\":\"zbsEjBVPQMmRs9I7EZTLEQ\",\"cardNumber\":\"5413330011\",\"cardHolderName\":\"malik ten\",\"warning\":\"\"}}",
            $"[{{\"invoiceId\":63677012,\"invoiceNumber\":\"ORD-20260503-PAID\",\"token\":\"cca8a4d3e05f1d91c28e94\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":99,\"amountPaid\":99,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":99,\"total\":99}}]}}]"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode,
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = courseId
                    }
                }
            });

        AddCoursePayment? capturedPayment = null;
        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync((new CoursePaymentResponse(), true));

        var studentCourseEnrollmentService = new Mock<IStudentCourseEnrollmentService>();

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object,
            studentCourseEnrollmentService: studentCourseEnrollmentService.Object,
            coursePaymentService: coursePaymentService.Object);

        var rawBody = $"{{\"id\":{transactionId},\"type\":\"cardTransaction\"}}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = transactionId,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_paid",
            timestamp,
            CreateWebhookSignature("msg_paid", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Processed, status);
        Assert.NotNull(capturedPayment);
        Assert.Equal(studentTransactionId, capturedPayment!.StudentCourseTransactionId);
        Assert.Equal(familyId, capturedPayment.FamilyId);
        Assert.Equal(99m, capturedPayment.AmountPaid);
        Assert.Equal($"Helcim payment applied for transactionId: {transactionId}", capturedPayment.Comments);
        Assert.Equal(transactionId.ToString(), capturedPayment.ExternalPaymentId);
        Assert.Equal(MaktabDataContracts.Enums.PaymentType.Credit, capturedPayment.PaymentType);
        Assert.Equal(PaymentMode.Helcim, capturedPayment.PaymentMode);
        Assert.True(capturedPayment.IsActive);
        Assert.NotNull(capturedDetails);
        Assert.Equal(familyId, capturedDetails!.FamilyId);
        coursePaymentService.Verify(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()), Times.Once);
        studentCourseEnrollmentService.Verify(service => service.RecalculateCourseFee(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HandleWebhook_ForPaidInvoice_DoesNotAddDuplicateLocalPayment()
    {
        var transactionId = 47889844;
        var paymentCode = "HELPAID";
        var studentTransactionId = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222");
        var familyId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
        var courseId = Guid.Parse("cccccccc-2222-2222-2222-222222222222");
        var paymentMarker = $"Helcim payment applied for transactionId: {transactionId}";

        var repository = new Mock<IHelcimTransactionRepository>();
        SetupWebhookProcessingDefaults(repository);
        repository
            .Setup(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>());
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"transactionId\":{transactionId},\"dateCreated\":\"2026-05-03 11:42:09\",\"cardBatchId\":6429263,\"status\":\"APPROVED\",\"user\":\"Helcim System\",\"type\":\"purchase\",\"amount\":99,\"currency\":\"CAD\",\"avsResponse\":\"X\",\"cvvResponse\":\"M\",\"cardType\":\"MC\",\"invoiceNumber\":\"ORD-20260503-DUPPAY\",\"customerCode\":\"CST1010\",\"approvalCode\":\"T8E7ST\",\"cardToken\":\"zbsEjBVPQMmRs9I7EZTLEQ\",\"cardNumber\":\"5413330011\",\"cardHolderName\":\"malik ten\",\"warning\":\"\"}}",
            $"[{{\"invoiceId\":63677013,\"invoiceNumber\":\"ORD-20260503-DUPPAY\",\"token\":\"cca8a4d3e05f1d91c28e94\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":99,\"amountPaid\":99,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":99,\"total\":99}}]}}]"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode,
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = courseId
                    }
                }
            });

        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .ReturnsAsync((new CoursePaymentResponse
            {
                Comments = paymentMarker,
                ExternalPaymentId = transactionId.ToString(),
                PaymentMode = PaymentMode.Helcim
            }, false));

        var studentCourseEnrollmentService = new Mock<IStudentCourseEnrollmentService>();

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object,
            studentCourseEnrollmentService: studentCourseEnrollmentService.Object,
            coursePaymentService: coursePaymentService.Object);

        var rawBody = $"{{\"id\":{transactionId},\"type\":\"cardTransaction\"}}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = transactionId,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_duplicate_payment",
            timestamp,
            CreateWebhookSignature("msg_duplicate_payment", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Processed, status);
        coursePaymentService.Verify(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()), Times.Once);
        studentCourseEnrollmentService.Verify(service => service.RecalculateCourseFee(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HandleWebhook_ForRefundTransaction_AddsRefundPaymentType()
    {
        var transactionId = 47889846;
        var paymentCode = "HELREFUND";
        var studentTransactionId = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
        var familyId = Guid.Parse("bbbbbbbb-3333-3333-3333-333333333333");
        var courseId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

        var repository = new Mock<IHelcimTransactionRepository>();
        SetupWebhookProcessingDefaults(repository);
        repository
            .Setup(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>());
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"transactionId\":{transactionId},\"dateCreated\":\"2026-05-03 11:42:09\",\"cardBatchId\":6429263,\"status\":\"APPROVED\",\"user\":\"Helcim System\",\"type\":\"refund\",\"amount\":25,\"currency\":\"CAD\",\"avsResponse\":\"X\",\"cvvResponse\":\"M\",\"cardType\":\"MC\",\"invoiceNumber\":\"ORD-20260503-REFUND\",\"customerCode\":\"CST1010\",\"approvalCode\":\"T8E7ST\",\"cardToken\":\"zbsEjBVPQMmRs9I7EZTLEQ\",\"cardNumber\":\"5413330011\",\"cardHolderName\":\"malik ten\",\"warning\":\"\"}}",
            $"[{{\"invoiceId\":63677014,\"invoiceNumber\":\"ORD-20260503-REFUND\",\"token\":\"cca8a4d3e05f1d91c28e94\",\"notes\":\"{paymentCode}\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":25,\"amountPaid\":25,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{{\"sku\":\"{studentTransactionId}\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":25,\"total\":25}}]}}]"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync(() => responses.Dequeue());

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransactionByPaymentCode(paymentCode))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = paymentCode,
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = courseId
                    }
                }
            });

        AddCoursePayment? capturedPayment = null;
        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync((new CoursePaymentResponse(), true));

        var service = CreateService(
            repository.Object,
            sender.Object,
            studentCourseTransactionService: studentCourseTransactionService.Object,
            studentCourseEnrollmentService: Mock.Of<IStudentCourseEnrollmentService>(),
            coursePaymentService: coursePaymentService.Object);

        var rawBody = $"{{\"id\":{transactionId},\"type\":\"cardTransaction\"}}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = transactionId,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_refund",
            timestamp,
            CreateWebhookSignature("msg_refund", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Processed, status);
        Assert.NotNull(capturedPayment);
        Assert.Equal(MaktabDataContracts.Enums.PaymentType.Refund, capturedPayment!.PaymentType);
    }

    [Fact]
    public async Task HandleWebhook_ReturnsDuplicateWhenWebhookReservationIsAlreadyProcessed()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.TryReserveWebhookProcessing(It.IsAny<ReserveHelcimWebhookProcessing>()))
            .ReturnsAsync(HelcimWebhookReservationResult.AlreadyProcessed);
        repository
            .Setup(repo => repo.UpdateWebhookProcessing(It.IsAny<UpdateHelcimWebhookProcessing>()))
            .Returns(Task.CompletedTask);

        var sender = new Mock<IWebMsgSenderService>();
        var service = CreateService(repository.Object, sender.Object);

        var rawBody = "{\"id\":47889845,\"type\":\"cardTransaction\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var duplicateStatus = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = 47889845,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_duplicate_reservation",
            timestamp,
            CreateWebhookSignature("msg_duplicate_reservation", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Duplicate, duplicateStatus);
        repository.Verify(repo => repo.GetByTransactionId(It.IsAny<int>()), Times.Never);
        sender.Verify(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), It.IsAny<HttpMethod>()), Times.Never);
    }

    [Fact]
    public async Task HandleWebhook_ReturnsRetryLaterWhenWebhookReservationIsAlreadyProcessing()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.TryReserveWebhookProcessing(It.IsAny<ReserveHelcimWebhookProcessing>()))
            .ReturnsAsync(HelcimWebhookReservationResult.AlreadyProcessing);

        var sender = new Mock<IWebMsgSenderService>();
        var service = CreateService(repository.Object, sender.Object);

        var rawBody = "{\"id\":47889846,\"type\":\"cardTransaction\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = 47889846,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_already_processing",
            timestamp,
            CreateWebhookSignature("msg_already_processing", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.RetryLater, status);
        repository.Verify(repo => repo.GetByTransactionId(It.IsAny<int>()), Times.Never);
        sender.Verify(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), It.IsAny<HttpMethod>()), Times.Never);
    }

    [Fact]
    public async Task HandleWebhook_ReturnsDuplicateWhenTransactionInsertRaceAlreadySavedByAnotherProcessor()
    {
        var transactionId = 47889847;
        var repository = new Mock<IHelcimTransactionRepository>();
        SetupWebhookProcessingDefaults(repository);
        repository
            .SetupSequence(repo => repo.GetByTransactionId(transactionId))
            .ReturnsAsync(new List<HelcimTransactionResponse>())
            .ReturnsAsync(new List<HelcimTransactionResponse>
            {
                new()
                {
                    TransactionId = transactionId,
                    InvoiceNumber = "ORD-20260503-RACE"
                }
            })
            .ReturnsAsync(new List<HelcimTransactionResponse>
            {
                new()
                {
                    TransactionId = transactionId,
                    InvoiceNumber = "ORD-20260503-RACE"
                }
            });
        repository
            .Setup(repo => repo.Add(It.IsAny<AddHelcimTransactionDetails>()))
            .ThrowsAsync(new InvalidOperationException("Duplicate transaction insert"));

        var sender = new Mock<IWebMsgSenderService>();
        var responses = new Queue<string>(new[]
        {
            $"{{\"transactionId\":{transactionId},\"dateCreated\":\"2026-05-03 11:42:09\",\"cardBatchId\":6429263,\"status\":\"APPROVED\",\"user\":\"Helcim System\",\"type\":\"purchase\",\"amount\":99,\"currency\":\"CAD\",\"avsResponse\":\"X\",\"cvvResponse\":\"M\",\"cardType\":\"MC\",\"invoiceNumber\":\"ORD-20260503-RACE\",\"customerCode\":\"CST1010\",\"approvalCode\":\"T8E7ST\",\"cardToken\":\"zbsEjBVPQMmRs9I7EZTLEQ\",\"cardNumber\":\"5413330011\",\"cardHolderName\":\"malik ten\",\"warning\":\"\"}}",
            "[{\"invoiceId\":63677014,\"invoiceNumber\":\"ORD-20260503-RACE\",\"token\":\"cca8a4d3e05f1d91c28e94\",\"notes\":\"MWSWAQ\",\"dateCreated\":\"2026-05-03 11:42:08\",\"dateUpdated\":\"2026-05-03 11:42:09\",\"datePaid\":\"2026-05-03 11:42:09\",\"status\":\"PAID\",\"customerId\":40499452,\"amount\":99,\"amountPaid\":99,\"currency\":\"CAD\",\"type\":\"INVOICE\",\"lineItems\":[{\"sku\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"description\":\"127.0.0.1\",\"quantity\":1,\"price\":99,\"total\":99}]}]"
        });
        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .ReturnsAsync(() => responses.Dequeue());

        var service = CreateService(repository.Object, sender.Object);

        var rawBody = $"{{\"id\":{transactionId},\"type\":\"cardTransaction\"}}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var status = await service.HandleWebhook(
            new HelcimCardTransactionWebhookResponse
            {
                Id = transactionId,
                Type = HelcimWebhookEventType.CardTransaction
            },
            rawBody,
            "msg_insert_race",
            timestamp,
            CreateWebhookSignature("msg_insert_race", timestamp, rawBody));

        Assert.Equal(HelcimWebhookHandlingStatus.Duplicate, status);
    }

    [Fact]
    public async Task ReconcileTransactions_WhenRunAlreadyInProgress_ReturnsAlreadyRunningWithoutStartingAnotherFetch()
    {
        var repository = new Mock<IHelcimTransactionRepository>();
        var sender = new Mock<IWebMsgSenderService>();
        var firstRequestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstRequestToContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;

        sender
            .Setup(service => service.SendMessage(It.IsAny<JsonMessageData>(), It.IsAny<IHelcimClientConfiguration>(), HttpMethod.Get))
            .Returns<JsonMessageData, InternalContracts.IClientConfiguration, HttpMethod>(async (_, _, _) =>
            {
                var currentCall = Interlocked.Increment(ref callCount);
                if (currentCall == 1)
                {
                    firstRequestStarted.TrySetResult(true);
                    await allowFirstRequestToContinue.Task;
                }

                return "[]";
            });

        var service = CreateService(repository.Object, sender.Object);
        var request = new HelcimReconciliationRequest
        {
            StartDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 8, 2)
        };

        var firstRunTask = service.ReconcileTransactions(request);
        await firstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            var overlappingRun = await service.ReconcileTransactions(request);

            Assert.True(overlappingRun.AlreadyRunning);
            Assert.Equal(request.StartDate.Value.Date, overlappingRun.StartDate);
            Assert.Equal(request.EndDate.Value.Date, overlappingRun.EndDate);
            Assert.Equal("Helcim reconciliation is already running. Wait for the current run to finish before starting another run.", overlappingRun.Message);
            Assert.Equal(1, Volatile.Read(ref callCount));
        }
        finally
        {
            allowFirstRequestToContinue.TrySetResult(true);
        }

        var completedRun = await firstRunTask;

        Assert.False(completedRun.AlreadyRunning);
        Assert.Equal("Helcim reconciliation completed.", completedRun.Message);
        Assert.Equal(2, Volatile.Read(ref callCount));
    }

    [Fact]
    public async Task GetByFamilyId_ReturnsRepositoryResults()
    {
        var familyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var expected = new List<HelcimTransactionResponse>
        {
            new() { FamilyId = familyId, PaymentCode = "PAY001", TransactionId = 1 }
        };

        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetByFamilyId(familyId))
            .ReturnsAsync(expected);

        var service = CreateService(repository.Object, Mock.Of<IWebMsgSenderService>());

        var result = await service.GetByFamilyId(familyId);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetDetailedByPaymentCode_ReturnsRepositoryResults()
    {
        var expected = new List<HelcimTransactionResponseDetailed>
        {
            new() { PaymentCode = "PAY001", TransactionResponse = "{\"transactionId\":1}", RawResponse = "[{\"invoiceId\":1}]" }
        };

        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetDetailedByPaymentCode("PAY001"))
            .ReturnsAsync(expected);

        var service = CreateService(repository.Object, Mock.Of<IWebMsgSenderService>());

        var result = await service.GetDetailedByPaymentCode("PAY001");

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetDetailedByMaktabTransactionId_ReturnsRepositoryResults()
    {
        var transactionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var expected = new List<HelcimTransactionResponseDetailed>
        {
            new() { MaktabTransactionId = transactionId, TransactionResponse = "{\"transactionId\":1}", RawResponse = "[{\"invoiceId\":1}]" }
        };

        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetDetailedByMaktabTransactionId(transactionId))
            .ReturnsAsync(expected);

        var service = CreateService(repository.Object, Mock.Of<IWebMsgSenderService>());

        var result = await service.GetDetailedByMaktabTransactionId(transactionId);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetDetailedByFamilyId_ReturnsRepositoryResults()
    {
        var familyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var expected = new List<HelcimTransactionResponseDetailed>
        {
            new() { FamilyId = familyId, TransactionResponse = "{\"transactionId\":1}", RawResponse = "[{\"invoiceId\":1}]" }
        };

        var repository = new Mock<IHelcimTransactionRepository>();
        repository
            .Setup(repo => repo.GetDetailedByFamilyId(familyId))
            .ReturnsAsync(expected);

        var service = CreateService(repository.Object, Mock.Of<IWebMsgSenderService>());

        var result = await service.GetDetailedByFamilyId(familyId);

        Assert.Same(expected, result);
    }

    private sealed class TestHelcimClientConfiguration : IHelcimClientConfiguration
    {
        public string BaseUrl { get; init; } = "https://api.helcim.com";
        public string ApiVersionPath { get; init; } = "/v2";
        public HttpClientType ClientType { get; init; } = HttpClientType.Helcim;
        public string RelativeUrl { get; init; } = "/helcim-pay/initialize";
        public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
        public int RetryAttempts { get; init; } = 3;
        public string ApiToken { get; init; } = "test-token";
        public string SignatureVerificationToken { get; init; } = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-signature-token"));
        public bool ReconciliationEnabled { get; init; } = true;
        public string ReconciliationCronSchedule { get; init; } = "0 0 2 * * ?";
        public int ReconciliationLookbackDays { get; init; } = 7;
        public int AchReconciliationPageSize { get; init; } = 125;
        public int CardReconciliationPageSize { get; init; } = 1000;
    }

    private static string CreateWebhookSignature(string webhookId, string webhookTimestamp, string rawBody)
    {
        var keyBytes = Convert.FromBase64String(Convert.ToBase64String(Encoding.UTF8.GetBytes("test-signature-token")));
        var signedContent = $"{webhookId}.{webhookTimestamp}.{rawBody}";
        using var hmac = new HMACSHA256(keyBytes);
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent)));
        return $"v1,{signature}";
    }

    private static HelcimTransactionService CreateService(
        IHelcimTransactionRepository repository,
        IWebMsgSenderService senderService,
        ICourseService? courseService = null,
        IStudentCourseTransactionService? studentCourseTransactionService = null,
        IStudentCourseEnrollmentService? studentCourseEnrollmentService = null,
        ICoursePaymentService? coursePaymentService = null)
    {
        studentCourseTransactionService ??= CreateStudentCourseTransactionService().Object;
        courseService ??= CreateCourseService().Object;

        return new HelcimTransactionService(
            repository,
            new TestHelcimClientConfiguration(),
            senderService,
            courseService,
            studentCourseTransactionService,
            studentCourseEnrollmentService ?? Mock.Of<IStudentCourseEnrollmentService>(),
            coursePaymentService ?? Mock.Of<ICoursePaymentService>());
    }

    private static Mock<IStudentCourseTransactionService> CreateStudentCourseTransactionService()
    {
        var service = new Mock<IStudentCourseTransactionService>();
        service
            .Setup(instance => instance.GetTransaction(It.IsAny<Guid>()))
            .ReturnsAsync((StudentCourseTransactionResponse?)null);
        service
            .Setup(instance => instance.GetTransactionByPaymentCode(It.IsAny<string>()))
            .ReturnsAsync((StudentCourseTransactionResponse?)null);
        return service;
    }

    private static Mock<ICourseService> CreateCourseService()
    {
        var service = new Mock<ICourseService>();
        service
            .Setup(instance => instance.GetHelcimTerminalId(It.IsAny<Guid>()))
            .ReturnsAsync((int?)null);
        return service;
    }

    private static void SetupWebhookProcessingDefaults(Mock<IHelcimTransactionRepository> repository)
    {
        repository
            .Setup(repo => repo.TryReserveWebhookProcessing(It.IsAny<ReserveHelcimWebhookProcessing>()))
            .ReturnsAsync(HelcimWebhookReservationResult.Reserved);
        repository
            .Setup(repo => repo.UpdateWebhookProcessing(It.IsAny<UpdateHelcimWebhookProcessing>()))
            .Returns(Task.CompletedTask);
    }
}
