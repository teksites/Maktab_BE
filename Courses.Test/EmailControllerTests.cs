using AppConfigurations.Services;
using Email;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Email;
using MaktabDataContracts.Responses.Configs;
using Moq;
using System.Text.Json;

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

        var appConfigService = new Mock<IAppConfigService>(MockBehavior.Strict);
        var controller = new EmailController(service.Object, appConfigService.Object);
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
        Assert.Empty(capturedEmail.Attachments);
        Assert.False(capturedEmail.IncludeSystemFooter);
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

        var appConfigService = new Mock<IAppConfigService>(MockBehavior.Strict);
        var controller = new EmailController(service.Object, appConfigService.Object);
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
        Assert.Empty(capturedEmail.Attachments);
    }

    [Fact]
    public async Task SendEmail_WithAttachmentsAndNoActivePolicy_ReturnsFalse()
    {
        var service = new Mock<ISendEmailService>(MockBehavior.Strict);
        var appConfigService = new Mock<IAppConfigService>();
        appConfigService
            .Setup(x => x.GetLatestAppConfigByType(ConfigurationType.EmailAttachments, true))
            .ReturnsAsync((AppConfigResponse)null!);

        var controller = new EmailController(service.Object, appConfigService.Object);
        var request = new SendEmailRequest
        {
            Subject = "subject",
            Body = "<p>body</p>",
            Recipients = new List<string> { "to@example.com" },
            Attachments = new List<EmailAttachmentData>
            {
                new()
                {
                    FileName = "sample.png",
                    ContentType = "image/png",
                    Content = new byte[] { 1, 2, 3 }
                }
            }
        };

        var result = await controller.SendEmail(request);

        Assert.False(result);
        service.Verify(x => x.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Never);
    }

    [Fact]
    public async Task SendEmail_WithAllowedAttachments_PassesAttachmentsToService()
    {
        MultiUserEmailData? capturedEmail = null;
        var service = new Mock<ISendEmailService>();
        service
            .Setup(x => x.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => capturedEmail = email)
            .ReturnsAsync(true);

        var settings = new EmailAttachmentSettings
        {
            IncludeAttachments = true,
            AllowedFileTypes = new List<string> { ".png", "application/pdf" },
            MaxAttachmentSizeInBytes = 1024,
            MaxAttachments = 2
        };

        var appConfigService = new Mock<IAppConfigService>();
        appConfigService
            .Setup(x => x.GetLatestAppConfigByType(ConfigurationType.EmailAttachments, true))
            .ReturnsAsync(new AppConfigResponse
            {
                Id = Guid.NewGuid(),
                ConfigurationType = ConfigurationType.EmailAttachments,
                IsActive = true,
                Content = JsonSerializer.Serialize(settings),
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            });

        var controller = new EmailController(service.Object, appConfigService.Object);
        var request = new SendEmailRequest
        {
            Subject = "subject",
            Body = "<p>body</p>",
            Recipients = new List<string> { "to@example.com" },
            Attachments = new List<EmailAttachmentData>
            {
                new()
                {
                    FileName = "sample.png",
                    ContentType = "image/png",
                    Content = new byte[] { 1, 2, 3, 4 }
                }
            }
        };

        var result = await controller.SendEmail(request);

        Assert.True(result);
        Assert.NotNull(capturedEmail);
        var attachment = Assert.Single(capturedEmail!.Attachments);
        Assert.Equal("sample.png", attachment.FileName);
        Assert.Equal("image/png", attachment.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, attachment.Content);
    }

}
