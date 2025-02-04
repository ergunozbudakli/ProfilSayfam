using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProfilSayfam.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var recipientEmail = _configuration["EmailSettings:RecipientEmail"];
                if (string.IsNullOrEmpty(recipientEmail))
                {
                    throw new InvalidOperationException("Alıcı e-posta adresi yapılandırılmamış.");
                }

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(
                    _configuration["EmailSettings:SenderName"] ?? "Ergün Özbudaklı",
                    _configuration["EmailSettings:Username"]
                ));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = subject;
                email.Body = new TextPart(MimeKit.Text.TextFormat.Plain) { Text = body };

                using var smtp = new SmtpClient();
                smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;

                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
                var username = _configuration["EmailSettings:Username"];
                var password = _configuration["EmailSettings:Password"];

                _logger.LogInformation($"SMTP Bağlantısı başlatılıyor: {smtpServer}:{port}");

                await smtp.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(username, password);

                _logger.LogInformation("E-posta gönderiliyor...");
                await smtp.SendAsync(email);
                _logger.LogInformation("E-posta başarıyla gönderildi");

                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "E-posta gönderirken hata oluştu");
                throw new Exception("E-posta gönderirken bir hata oluştu. Lütfen daha sonra tekrar deneyiniz.", ex);
            }
        }
    }
}
