using Helcim.Services;
using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Users.Services;

namespace Courses.Test;

public class HelcimSavedCardsControllerTests
{
    [Fact]
    public async Task Charge_UsesSessionOwnershipAndReturnsAttemptResponse()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var request = new ChargeSavedCardRequest
        {
            CardId = Guid.NewGuid(), PaymentCode = "PAY001", TransactionId = Guid.NewGuid(), Amount = 10m, IdempotencyKey = "attempt-1"
        };
        var service = new Mock<IHelcimTransactionService>();
        service.Setup(instance => instance.ChargeSavedCard(request, userId, familyId)).ReturnsAsync(new SavedCardPaymentAttemptResponse
        {
            PaymentAttemptId = Guid.NewGuid(), Status = "awaiting_confirmation", AwaitingConfirmation = true
        });
        var access = new Mock<IDataAccessVerificationService>();
        access.Setup(instance => instance.GetSessionAccessContext(sessionId)).ReturnsAsync(new SessionAccessContext { UserId = userId, FamilyId = familyId });
        var controller = CreateController(service.Object, access.Object, sessionId);

        var result = await controller.Charge(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("awaiting_confirmation", Assert.IsType<SavedCardPaymentAttemptResponse>(ok.Value).Status);
        service.Verify(instance => instance.ChargeSavedCard(request, userId, familyId), Times.Once);
    }

    [Fact]
    public async Task Charge_WhenSavedCardIsUnavailable_ReturnsNotFoundErrorCode()
    {
        var sessionId = Guid.NewGuid();
        var service = new Mock<IHelcimTransactionService>();
        service.Setup(instance => instance.ChargeSavedCard(It.IsAny<ChargeSavedCardRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new KeyNotFoundException("The selected saved card is unavailable."));
        var access = new Mock<IDataAccessVerificationService>();
        access.Setup(instance => instance.GetSessionAccessContext(sessionId)).ReturnsAsync(new SessionAccessContext { UserId = Guid.NewGuid(), FamilyId = Guid.NewGuid() });
        var controller = CreateController(service.Object, access.Object, sessionId);

        var result = await controller.Charge(new ChargeSavedCardRequest { CardId = Guid.NewGuid() });

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains("saved_card_or_transaction_not_found", notFound.Value!.ToString());
    }

    [Fact]
    public async Task InitializeVerification_UsesAuthenticatedSessionAndReturnsCheckoutToken()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var service = new Mock<IHelcimTransactionService>();
        service.Setup(instance => instance.InitializeSavedCardVerification(userId, familyId))
            .ReturnsAsync(new HelcimPayInitializeResponse { CheckoutToken = "verify_checkout_token" });
        var access = new Mock<IDataAccessVerificationService>();
        access.Setup(instance => instance.GetSessionAccessContext(sessionId))
            .ReturnsAsync(new SessionAccessContext { UserId = userId, FamilyId = familyId });
        var controller = CreateController(service.Object, access.Object, sessionId);

        var result = await controller.InitializeVerification();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("verify_checkout_token", Assert.IsType<HelcimPayInitializeResponse>(ok.Value).CheckoutToken);
        service.Verify(instance => instance.InitializeSavedCardVerification(userId, familyId), Times.Once);
    }

    [Fact]
    public async Task CompleteVerification_UsesAuthenticatedUserAndNeverAcceptsUserOwnershipFromTheRequest()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new CompleteSavedCardVerificationRequest
        {
            CheckoutToken = "verify_checkout_token",
            HelcimTransactionId = 54951592
        };
        var service = new Mock<IHelcimTransactionService>();
        var access = new Mock<IDataAccessVerificationService>();
        access.Setup(instance => instance.GetSessionAccessContext(sessionId))
            .ReturnsAsync(new SessionAccessContext { UserId = userId, FamilyId = Guid.NewGuid() });
        var controller = CreateController(service.Object, access.Object, sessionId);

        var result = await controller.CompleteVerification(request);

        Assert.IsType<NoContentResult>(result);
        service.Verify(instance => instance.CompleteSavedCardVerification(request, userId), Times.Once);
    }

    private static HelcimSavedCardsController CreateController(
        IHelcimTransactionService service,
        IDataAccessVerificationService access,
        Guid sessionId)
    {
        var controller = new HelcimSavedCardsController(Mock.Of<Helcim.Repository.IHelcimCardVaultRepository>(), service, access);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Headers["Session_Info"] = sessionId.ToString();
        return controller;
    }
}
