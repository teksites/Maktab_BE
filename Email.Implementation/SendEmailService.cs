using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

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

            using (MailMessage mm = new MailMessage(fromAddress, emailData.To))
            {
                mm.Subject = emailData.Subject;
                mm.Body = emailData.Body;
                mm.IsBodyHtml = true;
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

        public async Task<bool> SendEmailSmtp(EmailData emailData)
        {
            string host = _configuration["Smtp:Server"].ToString();
            int port = (int)Convert.ToUInt64(_configuration["Smtp:Port"]);
            string fromAddress = _configuration["Smtp:FromAddress"].ToString();
            string userName = _configuration["Smtp:UserName"].ToString();
            string password = _configuration["Smtp:Password"].ToString();

            using (MailMessage mm = new MailMessage(fromAddress, emailData.To))
            {
                mm.Subject = emailData.Subject;
                mm.Body = emailData.Body;
                mm.IsBodyHtml = true;
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

    }
}
