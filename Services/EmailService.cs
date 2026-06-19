using System.Net.Mail;
using Microsoft.Extensions.Options;
using KEYREGISTERAUTOMATION.Models;

namespace KEYREGISTERAUTOMATION.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return;

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.Address, _settings.DisplayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(toEmail);

            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = true,
                EnableSsl = false
            };

            await client.SendMailAsync(message);
        }
    }
}