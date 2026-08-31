using Email;
using Email.Implementation;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using System.Reflection;

namespace Courses.Test;

public class SendEmailServiceTests
{
    [Fact]
    public void BuildMailMessage_AddsCcAndBccRecipients()
    {
        var message = InvokeBuildMailMessage(new MultiUserEmailData
        {
            To = new[] { "to@example.com" },
            Cc = new[] { "cc@example.com" },
            Bcc = new[] { "bcc@example.com" },
            Subject = "subject",
            Body = "<p>body</p>"
        });

        Assert.Equal("to@example.com", Assert.Single(message.To).Address);
        Assert.Equal("cc@example.com", Assert.Single(message.CC).Address);
        Assert.Equal("bcc@example.com", Assert.Single(message.Bcc).Address);
    }

    [Fact]
    public void BuildMailMessage_AllowsEmptyOrNullCcAndBcc()
    {
        var emptyMessage = InvokeBuildMailMessage(new MultiUserEmailData
        {
            To = new[] { "to@example.com" },
            Cc = Array.Empty<string>(),
            Bcc = Array.Empty<string>(),
            Subject = "subject",
            Body = "<p>body</p>"
        });

        Assert.Empty(emptyMessage.CC);
        Assert.Empty(emptyMessage.Bcc);

        var nullMessage = InvokeBuildMailMessage(new MultiUserEmailData
        {
            To = new[] { "to@example.com" },
            Cc = null!,
            Bcc = null!,
            Subject = "subject",
            Body = "<p>body</p>"
        });

        Assert.Empty(nullMessage.CC);
        Assert.Empty(nullMessage.Bcc);
    }

    [Fact]
    public void BuildMailMessage_AddsAttachments()
    {
        var message = InvokeBuildMailMessage(new MultiUserEmailData
        {
            To = new[] { "to@example.com" },
            Subject = "subject",
            Body = "<p>body</p>",
            Attachments = new[]
            {
                new EmailAttachmentPayload
                {
                    FileName = "sample.png",
                    ContentType = "image/png",
                    Content = new byte[] { 1, 2, 3 }
                }
            }
        });

        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("sample.png", attachment.Name);
        Assert.Equal("image/png", attachment.ContentType.MediaType);
        Assert.False(attachment.ContentDisposition.Inline);
        Assert.Equal("attachment", attachment.ContentDisposition.DispositionType);
    }

    [Fact]
    public void AppendSystemFooter_AppendsBilingualReplyDisclaimer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:FooterSchoolName"] = "ICC Brossard Schools and Activities"
            })
            .Build();

        var service = new SendEmailService(configuration);
        var method = typeof(SendEmailService).GetMethod(
            "AppendSystemFooter",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        var body = Assert.IsType<string>(method!.Invoke(service, new object[]
        {
            "<p>Hello</p>",
            "ICC Brossard Schools and Activities"
        }));

        Assert.Contains("<p>Hello</p>", body);
        Assert.Contains("Please don't reply to this email.", body);
        Assert.Contains("ICC Brossard Schools and Activities", body);
        Assert.Contains("Contact Us form in the portal", body);
        Assert.Contains("Veuillez ne pas", body);
        Assert.Contains("formulaire Nous joindre du portail", body);
    }

    private static MailMessage InvokeBuildMailMessage(MultiUserEmailData emailData)
    {
        var method = typeof(SendEmailService).GetMethod(
            "BuildMailMessage",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(MultiUserEmailData) },
            modifiers: null);

        Assert.NotNull(method);
        var message = method!.Invoke(null, new object[] { "from@example.com", emailData });
        return Assert.IsType<MailMessage>(message);
    }
}
