using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace StudentPortal.Services
{
    public class EmailService
    {
        private readonly string _smtpServer = "smtp.gmail.com";
        private readonly int _smtpPort = 587;
        private readonly string _smtpUser = "mysuqcotp@gmail.com";   // your Gmail
        private readonly string _smtpPass = "eqlp oyav adtw ulzf";   // your App Password

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var email = new MimeMessage();

            // From must match the Gmail account you authenticated with
            email.From.Add(new MailboxAddress("MySUQC", _smtpUser));

            // Recipient (the actual user)
            email.To.Add(MailboxAddress.Parse(toEmail));

            email.Subject = subject;
            email.Body = new TextPart("plain") { Text = message };

            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_smtpUser, _smtpPass);

                Console.WriteLine($"📨 Sending email from {_smtpUser} to {toEmail} ...");

                await client.SendAsync(email);
                await client.DisconnectAsync(true);

                Console.WriteLine("✅ Email sent successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Failed to send email: " + ex.ToString());
            }

        }
    }
}
