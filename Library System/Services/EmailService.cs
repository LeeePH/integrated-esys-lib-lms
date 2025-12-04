using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System;

namespace SystemLibrary.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

                private string BuildProfessionalHtml(string title, string contentHtml)
                {
                        return $@"
<div style='font-family: Inter, Arial, sans-serif; color: #222;'>
    <div style='max-width:700px;margin:0 auto;padding:24px;border:1px solid #e9ecef;border-radius:8px;background:#ffffff'>
        <header style='border-bottom:1px solid #f1f1f1;padding-bottom:12px;margin-bottom:18px;'>
            <h2 style='margin:0;color:#1f2937;font-size:20px'>{System.Net.WebUtility.HtmlEncode(title)}</h2>
        </header>
        <section style='color:#374151;font-size:14px;line-height:1.6'>
            {contentHtml}
        </section>
        <footer style='margin-top:22px;border-top:1px solid #f1f1f1;padding-top:12px;color:#6b7280;font-size:12px'>
            Sent by {System.Net.WebUtility.HtmlEncode(_configuration["SmtpSettings:FromName"] ?? "Library System")} — <span style='color:#9ca3af'>Please do not reply to this automated message.</span>
        </footer>
    </div>
</div>
";
                }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            await SendEmailAsync(toEmail, subject, htmlBody, null);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string? attachmentPath)
        {
            // Basic recipient validation to avoid runtime exceptions
            try
            {
                _ = new MailAddress(toEmail);
            }
            catch
            {
                Console.WriteLine($"[EmailService] Invalid recipient email: '{toEmail}' — skipping send.");
                return;
            }

            var host = _configuration["SmtpSettings:Host"];
            var port = _configuration.GetValue<int>("SmtpSettings:Port");
            var enableSsl = _configuration.GetValue<bool>("SmtpSettings:EnableSsl");
            var username = (_configuration["SmtpSettings:Username"] ?? string.Empty).Trim();
            // Prefer an environment-provided password for security; fallback to config if not set.
            var envPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
            var password = !string.IsNullOrWhiteSpace(envPassword)
                ? envPassword
                : _configuration["SmtpSettings:Password"];
            var fromEmail = _configuration["SmtpSettings:FromEmail"] ?? username;
            var fromName = _configuration["SmtpSettings:FromName"] ?? "Library System";

            using var smtp = new SmtpClient(host, port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl,
                Timeout = 15000
            };

            // Wrap the provided html body in a simple, professional template.
            var wrappedBody = BuildProfessionalHtml(subject, htmlBody);

            using var message = new MailMessage()
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = wrappedBody,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(toEmail));

            // Add attachment if provided
            if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
            {
                message.Attachments.Add(new Attachment(attachmentPath));
                Console.WriteLine($"[EmailService] Added attachment: {attachmentPath}");
            }

            try
            {
                Console.WriteLine($"[EmailService] Sending email → To: {toEmail}, Subject: {subject}, Host: {host}:{port}, SSL: {enableSsl}");
                await smtp.SendMailAsync(message);
                Console.WriteLine("[EmailService] Email sent successfully.");
            }
            catch (SmtpException smtpEx)
            {
                Console.WriteLine($"[EmailService] SMTP error sending email: {smtpEx.StatusCode} - {smtpEx.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] Error sending email: {ex.Message}");
                throw;
            }
        }
    }
}


