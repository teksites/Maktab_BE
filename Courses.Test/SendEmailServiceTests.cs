using Email;
using Email.Implementation;
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
