using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

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

            using (MailMessage mm = BuildMailMessage(fromAddress, emailData))
            {
                try
                {
                    using (SmtpClient smtp = new SmtpClient())
                    {
                        smtp.Host = host;
                        smtp.EnableSsl = false;
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

            using (MailMessage mm = BuildMailMessage(fromAddress, emailData))
            {
                try
                {
                    using (SmtpClient smtp = new SmtpClient())
                    {
                        smtp.Host = host;
                        smtp.EnableSsl = false;
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

            using (MailMessage mm = BuildMailMessage(fromAddress, emailData))
            {
                try
                {
                    using (SmtpClient smtp = new SmtpClient())
                    {
                        smtp.Host = host;
                        smtp.EnableSsl = true;
                        NetworkCredential NetworkCred = new NetworkCredential(userName, password);
                        smtp.UseDefaultCredentials = true;
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

            return BuildMailMessage(fromAddress, recipients, emailData.Cc, emailData.Subject, emailData.Body);
        }

        private static MailMessage BuildMailMessage(string fromAddress, MultiUserEmailData emailData)
        {
            var recipients = emailData.To
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildMailMessage(fromAddress, recipients, emailData.Cc, emailData.Subject, emailData.Body);
        }

        private static MailMessage BuildMailMessage(
            string fromAddress,
            IEnumerable<string> recipients,
            IEnumerable<string> ccRecipients,
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

            foreach (var cc in ccRecipients.Where(address => !string.IsNullOrWhiteSpace(address)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                message.CC.Add(cc);
            }

            return message;
        }

    }
}
