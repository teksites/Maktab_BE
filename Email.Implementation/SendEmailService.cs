using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace Email.Implementation
{
    public class SendEmailService : ISendEmailService
    {
        public IConfiguration _configuration;

        public SendEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendEmail(EmailData emailData)
        {
            string host = _configuration["Smtp:Server"].ToString();
            int port = (int)Convert.ToUInt64(_configuration["Smtp:Port"]);
            string fromAddress = _configuration["Smtp:FromAddress"].ToString();
            string userName = _configuration["Smtp:UserName"].ToString();
            string password = _configuration["Smtp:Password"].ToString();
            bool enableSsl = GetEnableSsl();
            string schoolName = GetFooterSchoolName();
            using (MailMessage mm = BuildMailMessage(fromAddress, emailData, AppendSystemFooter(emailData.Body, schoolName)))
            {
                try
                {
                    using (SmtpClient smtp = new SmtpClient())
                    {
                        smtp.Host = host;
                        smtp.EnableSsl = enableSsl;
                        NetworkCredential NetworkCred = new NetworkCredential(userName, password);
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = NetworkCred;
                        smtp.DeliveryMethod=  SmtpDeliveryMethod.Network;
                        smtp.Port = port;
                    
                        await smtp.SendMailAsync(mm).ConfigureAwait(false);
                        //smtp.Send(mm);

                        return true;
                    }
                }
                catch(Exception) {
                    throw;
                }
            }
        }

        public async Task<bool> SendBulkEmail(MultiUserEmailData emailData)
        {
            string host = _configuration["Smtp:Server"].ToString();
            int port = (int)Convert.ToUInt64(_configuration["Smtp:Port"]);
            string fromAddress = _configuration["Smtp:FromAddress"].ToString();
            string userName = _configuration["Smtp:UserName"].ToString();
            string password = _configuration["Smtp:Password"].ToString();
            bool enableSsl = GetEnableSsl();
            string schoolName = GetFooterSchoolName();
            using (MailMessage mm = BuildMailMessage(fromAddress, emailData, AppendSystemFooter(emailData.Body, schoolName)))
            {
                try
                {
                    using (SmtpClient smtp = new SmtpClient())
                    {
                        smtp.Host = host;
                        smtp.EnableSsl = enableSsl;
                        NetworkCredential NetworkCred = new NetworkCredential(userName, password);
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = NetworkCred;
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtp.Port = port;

                        await smtp.SendMailAsync(mm).ConfigureAwait(false);

                        return true;
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        public async Task<bool> SendEmailSmtp(EmailData emailData)
        {
            string host = _configuration["Smtp:Server"].ToString();
            int port = (int)Convert.ToUInt64(_configuration["Smtp:Port"]);
            string fromAddress = _configuration["Smtp:FromAddress"].ToString();
            string userName = _configuration["Smtp:UserName"].ToString();
            string password = _configuration["Smtp:Password"].ToString();
            bool enableSsl = GetEnableSsl();
            string schoolName = GetFooterSchoolName();
            using (MailMessage mm = BuildMailMessage(fromAddress, emailData, AppendSystemFooter(emailData.Body, schoolName)))
            {
                try
                {
                    using (SmtpClient smtp = new SmtpClient())
                    {
                        smtp.Host = host;
                        smtp.EnableSsl = enableSsl;
                        NetworkCredential NetworkCred = new NetworkCredential(userName, password);
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = NetworkCred;
                        smtp.Port = port;

                        //await smtp.SendMailAsync(mm).ConfigureAwait(false);
                        smtp.Send(mm);

                        return true;
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private static MailMessage BuildMailMessage(string fromAddress, EmailData emailData)
        {
            var recipients = new[] { emailData.To }
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildMailMessage(fromAddress, recipients, emailData.Cc, emailData.Bcc, emailData.Attachments, emailData.Subject, emailData.Body);
        }

        private static MailMessage BuildMailMessage(string fromAddress, EmailData emailData, string body)
        {
            var recipients = new[] { emailData.To }
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildMailMessage(fromAddress, recipients, emailData.Cc, emailData.Bcc, emailData.Attachments, emailData.Subject, body);
        }

        private static MailMessage BuildMailMessage(string fromAddress, MultiUserEmailData emailData)
        {
            var recipients = emailData.To
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildMailMessage(fromAddress, recipients, emailData.Cc, emailData.Bcc, emailData.Attachments, emailData.Subject, emailData.Body);
        }

        private static MailMessage BuildMailMessage(string fromAddress, MultiUserEmailData emailData, string body)
        {
            var recipients = emailData.To
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildMailMessage(fromAddress, recipients, emailData.Cc, emailData.Bcc, emailData.Attachments, emailData.Subject, body);
        }

        private static MailMessage BuildMailMessage(
            string fromAddress,
            IEnumerable<string> recipients,
            IEnumerable<string> ccRecipients,
            IEnumerable<string> bccRecipients,
            IEnumerable<EmailAttachmentPayload> attachments,
            string subject,
            string body)
        {
            var recipientList = recipients.ToList();

            if (!recipientList.Any())
            {
                throw new ArgumentException("At least one recipient must be provided.", nameof(recipients));
            }

            var message = new MailMessage
            {
                From = new MailAddress(fromAddress),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            foreach (var recipient in recipientList)
            {
                message.To.Add(recipient);
            }

            foreach (var cc in (ccRecipients ?? Enumerable.Empty<string>())
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                message.CC.Add(cc);
            }

            foreach (var bcc in (bccRecipients ?? Enumerable.Empty<string>())
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                message.Bcc.Add(bcc);
            }

            foreach (var attachment in (attachments ?? Enumerable.Empty<EmailAttachmentPayload>())
                .Where(attachment => attachment != null
                    && !string.IsNullOrWhiteSpace(attachment.FileName)
                    && attachment.Content != null
                    && attachment.Content.Length > 0))
            {
                var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                    ? MediaTypeNames.Application.Octet
                    : attachment.ContentType;
                var stream = new MemoryStream(attachment.Content, writable: false);
                var mailAttachment = new Attachment(stream, attachment.FileName, contentType);
                mailAttachment.ContentDisposition.Inline = false;
                mailAttachment.ContentDisposition.DispositionType = DispositionTypeNames.Attachment;
                mailAttachment.ContentType.Name = attachment.FileName;
                message.Attachments.Add(mailAttachment);
            }

            return message;
        }

        private string GetFooterSchoolName()
            => _configuration["Smtp:FooterSchoolName"]?.Trim() ?? "ICC Brossard Schools and Activities";

        private bool GetEnableSsl()
            => bool.TryParse(_configuration["Smtp:EnableSsl"], out var enableSsl)
                ? enableSsl
                : true;

        private string AppendSystemFooter(string body, string schoolName)
        {
            var normalizedBody = body ?? string.Empty;
            var encodedSchoolName = WebUtility.HtmlEncode(schoolName?.Trim() ?? string.Empty);

            var footer = new StringBuilder();
            footer.Append(normalizedBody);
            footer.Append("<hr/>");
            footer.Append("<div style=\"margin-top:16px;font-size:13px;color:#555;\">");
            footer.Append("<p><em>Please don't reply to this email. If you have any questions or concerns, please reach out to ");
            footer.Append(encodedSchoolName);
            footer.Append(" using the Contact Us form in the portal.</em></p>");
            footer.Append("<p><em>Veuillez ne pas répondre à ce courriel. Si vous avez des questions ou des préoccupations, veuillez communiquer avec ");
            footer.Append(encodedSchoolName);
            footer.Append(" en utilisant le formulaire Nous joindre du portail.</em></p>");
            footer.Append("</div>");
            return footer.ToString();
        }

    }
}
