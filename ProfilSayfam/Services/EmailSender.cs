using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
namespace ProfilSayfam.Services
{
   

    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var email = new MimeMessage();

                email.From.Add(new MailboxAddress(
                "Ergün Özbudaklı", // Görünecek isim
                _configuration["EmailSettings:Username"]
                ));
                email.To.Add(MailboxAddress.Parse(to));
                email.Subject = subject;
                email.Body = new TextPart(MimeKit.Text.TextFormat.Plain) { Text = body };

                using var smtp = new SmtpClient();
                smtp.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
                await smtp.ConnectAsync(
                    _configuration["EmailSettings:SmtpServer"],
                    int.Parse(_configuration["EmailSettings:Port"]!),
                    SecureSocketOptions.Auto);
                await smtp.AuthenticateAsync(
                    _configuration["EmailSettings:Username"],
                    _configuration["EmailSettings:Password"]);

                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Hata loglaması yapabilirsiniz
                throw;
            }
        }
    }
}
