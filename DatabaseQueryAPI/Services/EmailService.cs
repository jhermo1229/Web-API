using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace DatabaseQueryAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailWithAttachmentAsync(
            string toEmail,
            string subject,
            string body,
            byte[] attachmentBytes,
            string attachmentFileName)
        {
            var smtpServer = _config["Email:SmtpServer"];
            var port = int.Parse(_config["Email:Port"]);
            var senderEmail = _config["Email:SenderEmail"];
            var senderName = _config["Email:SenderName"];
            var username = _config["Email:Username"];
            var password = _config["Email:Password"];

            using var client = new SmtpClient(smtpServer, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            mail.To.Add(toEmail);

            if (attachmentBytes != null && attachmentBytes.Length > 0)
            {
                var stream = new MemoryStream(attachmentBytes);
                var attachment = new Attachment(stream, attachmentFileName,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                mail.Attachments.Add(attachment);
            }

            await client.SendMailAsync(mail);
        }
    }
}
