using Email;
using MaktabDataContracts.Requests.Email;
using Moq;

namespace Courses.Test;

public class EmailControllerTests
{
    [Fact]
    public async Task SendEmail_PassesRecipientsCcAndBccToService()
    {
        MultiUserEmailData? capturedEmail = null;
        var service = new Mock<ISendEmailService>();
        service
            .Setup(x => x.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => capturedEmail = email)
            .ReturnsAsync(true);

        var controller = new EmailController(service.Object);
        var request = new SendEmailRequest
        {
            Subject = "subject",
            Body = "<p>body</p>",
            Recipients = new List<string> { "to@example.com" },
            RecipientsCC = new List<string> { "cc@example.com" },
            RecipientsBCC = new List<string> { "bcc@example.com" }
        };

        var result = await controller.SendEmail(request);

        Assert.True(result);
        Assert.NotNull(capturedEmail);
        Assert.Equal(request.Recipients, capturedEmail!.To);
        Assert.Equal(request.RecipientsCC, capturedEmail.Cc);
        Assert.Equal(request.RecipientsBCC, capturedEmail.Bcc);
    }

    [Fact]
    public async Task SendEmail_AllowsEmptyCcAndBcc()
    {
        MultiUserEmailData? capturedEmail = null;
        var service = new Mock<ISendEmailService>();
        service
            .Setup(x => x.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => capturedEmail = email)
            .ReturnsAsync(true);

        var controller = new EmailController(service.Object);
        var request = new SendEmailRequest
        {
            Subject = "subject",
            Body = "<p>body</p>",
            Recipients = new List<string> { "to@example.com" },
            RecipientsCC = new List<string>(),
            RecipientsBCC = new List<string>()
        };

        var result = await controller.SendEmail(request);

        Assert.True(result);
        Assert.NotNull(capturedEmail);
        Assert.Empty(capturedEmail!.Cc);
        Assert.Empty(capturedEmail.Bcc);
    }
}
