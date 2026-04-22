
namespace Email
{
    public interface ISendEmailService
    {
        Task<bool> SendEmail(EmailData emailData);
        Task<bool> SendBulkEmail(MultiUserEmailData emailData);
    }
}
