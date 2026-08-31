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
            using (MailMessage mm = BuildMailMessage(fromAddress, emailData, BuildEmailBody(emailData)))
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
            using (MailMessage mm = BuildMailMessage(fromAddress, emailData, BuildEmailBody(emailData)))
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
            using (MailMessage mm = BuildMailMessage(fromAddress, emailData, BuildEmailBody(emailData)))
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

        private string BuildEmailBody(EmailData emailData)
            => emailData.IncludeSystemFooter
                ? AppendSystemFooter(emailData.Body, emailData.SchoolContacts)
                : emailData.Body ?? string.Empty;

        private string BuildEmailBody(MultiUserEmailData emailData)
            => emailData.IncludeSystemFooter
                ? AppendSystemFooter(emailData.Body, emailData.SchoolContacts)
                : emailData.Body ?? string.Empty;

        private EmailSchoolContact GetDefaultSchoolContact()
            => new()
            {
                Name = _configuration["Smtp:FooterSchoolName"]?.Trim() ?? "ICC Brossard Schools and Activities",
                NameFr = _configuration["Smtp:FooterSchoolNameFr"]?.Trim() ?? "ICC Brossard Schools and Activities",
                Email = _configuration["Smtp:FooterSchoolEmail"]?.Trim() ?? "schools@iccbrossard.com",
                Phone = _configuration["Smtp:FooterSchoolPhone"]?.Trim() ?? string.Empty
            };

        private bool GetEnableSsl()
            => bool.TryParse(_configuration["Smtp:EnableSsl"], out var enableSsl)
                ? enableSsl
                : true;

        private string AppendSystemFooter(string body, IEnumerable<EmailSchoolContact>? schoolContacts)
        {
            var normalizedBody = body ?? string.Empty;
            var defaultContact = GetDefaultSchoolContact();
            var contacts = (schoolContacts ?? Enumerable.Empty<EmailSchoolContact>())
                .Where(contact => contact != null)
                .Select(contact => NormalizeContact(contact, defaultContact))
                .GroupBy(contact => $"{contact.Name}|{contact.Email}|{contact.Phone}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (!contacts.Any())
            {
                contacts.Add(defaultContact);
            }

            var footer = new StringBuilder();
            footer.Append(normalizedBody);
            footer.Append("<hr/>");
            footer.Append("<div style=\"margin-top:16px;font-size:13px;color:#555;\">");
            footer.Append("<p><em>Please don't reply to this email.</em></p>");

            foreach (var contact in contacts)
            {
                AppendContactFooter(footer, contact);
            }

            footer.Append("</div>");
            return footer.ToString();
        }

        private static EmailSchoolContact NormalizeContact(EmailSchoolContact contact, EmailSchoolContact fallback)
            => new()
            {
                Name = string.IsNullOrWhiteSpace(contact.Name) ? fallback.Name : contact.Name.Trim(),
                NameFr = string.IsNullOrWhiteSpace(contact.NameFr) ? fallback.NameFr : contact.NameFr.Trim(),
                Email = string.IsNullOrWhiteSpace(contact.Email) ? fallback.Email : contact.Email.Trim(),
                Phone = string.IsNullOrWhiteSpace(contact.Phone) ? fallback.Phone : contact.Phone.Trim()
            };

        private static void AppendContactFooter(StringBuilder footer, EmailSchoolContact contact)
        {
            var schoolName = WebUtility.HtmlEncode(contact.Name);
            var schoolNameFr = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contact.NameFr) ? contact.Name : contact.NameFr);
            var email = WebUtility.HtmlEncode(contact.Email);
            var phone = WebUtility.HtmlEncode(contact.Phone);
            var emailLink = string.IsNullOrWhiteSpace(contact.Email)
                ? string.Empty
                : $"<a href=\"mailto:{email}\">{email}</a>";
            var englishContactMethod = BuildContactMethod(emailLink, phone, "at", "or");
            var frenchContactMethod = BuildContactMethod(emailLink, phone, "à", "ou au");

            footer.Append("<p><em>For assistance please contact ");
            footer.Append(schoolName);
            footer.Append(englishContactMethod);
            footer.Append(".</em></p>");
            footer.Append("<p><em>Pour obtenir de l'aide, veuillez svp contacter ");
            footer.Append(schoolNameFr);
            footer.Append(frenchContactMethod);
            footer.Append(".</em></p>");
        }

        private static string BuildContactMethod(string emailLink, string phone, string emailPreposition, string phoneJoiner)
        {
            if (!string.IsNullOrWhiteSpace(emailLink) && !string.IsNullOrWhiteSpace(phone))
            {
                return $" {emailPreposition} {emailLink} {phoneJoiner} {phone}";
            }

            if (!string.IsNullOrWhiteSpace(emailLink))
            {
                return $" {emailPreposition} {emailLink}";
            }

            return string.IsNullOrWhiteSpace(phone) ? string.Empty : $" {emailPreposition} {phone}";
        }

    }
}
