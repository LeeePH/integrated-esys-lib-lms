using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
namespace E_SysV0._01.Services
{
    public class EmailServices
    {
        private readonly string _smtpServer; private readonly int _smtpPort; private readonly string _senderEmail; private readonly string _senderPassword;
        public EmailServices(IConfiguration config)
        {
            _smtpServer = config["Smtp:Host"] ?? "smtp.gmail.com";
            _smtpPort = int.TryParse(config["Smtp:Port"], out var port) ? port : 587;
            _senderEmail = config["Smtp:From"] ?? "";
            _senderPassword = config["Smtp:Password"] ?? "";
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetLink)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("University Enrollment", _senderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Password reset instructions";

            var bodyText =
                "You requested a password reset. Use the link below to set a new password. This link is valid for 60 minutes.\n\n" +
                $"{resetLink}\n\n" +
                "If you did not request this, ignore this email.";

            message.Body = new TextPart("plain") { Text = bodyText };
            await SendInternalAsync(message);
        }
    

        // Updated: optional PDF attachment
        public async Task SendAcceptanceEmailAsync(string email, string tempUsername, string tempPassword, byte[]? registrationPdf = null)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("University Enrollment", _senderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Enrollment Accepted";

            var bodyText =
                "Congratulations! Your enrollment has been accepted.\n\n" +
                "To access your student portal, use your email address and the temporary password below.\n" +
                $"Email: {email}\nTemporary Password: {tempPassword}\n\n" +
                "For security, log in and change your password immediately from your dashboard." +
                (registrationPdf != null ? "\n\nAttached: Your Registration Form (PDF)." : "");

            var builder = new BodyBuilder { TextBody = bodyText };
            if (registrationPdf != null && registrationPdf.Length > 0)
            {
                builder.Attachments.Add("RegistrationForm.pdf", registrationPdf, new ContentType("application", "pdf"));
            }

            message.Body = builder.ToMessageBody();
            await SendInternalAsync(message);
        }

        // Add this method to EmailServices class (around line 300)

        /// <summary>
        /// Send email notification for year level progression
        /// </summary>
      
        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, byte[]? pdfAttachment = null, string? pdfFileName = null)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("University Enrollment", _senderEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };

            // Add PDF attachment if provided
            if (pdfAttachment != null && pdfAttachment.Length > 0)
            {
                var fileName = string.IsNullOrWhiteSpace(pdfFileName) ? "Attachment.pdf" : pdfFileName;
                builder.Attachments.Add(fileName, pdfAttachment, new ContentType("application", "pdf"));
            }

            message.Body = builder.ToMessageBody();
            await SendInternalAsync(message);
        }
        public async Task SendShifterEnrollmentEmailAsync(string email, string accessLink)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("QCU Enrollment System", _senderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "QCU - Shifter Enrollment Form";

            var bodyText =
                "You have requested to shift programs at Quezon City University.\n\n" +
                "Please use the secure link below to access your Shifter Enrollment Form:\n\n" +
                $"{accessLink}\n\n" +
                "This link will expire in 7 days. If you did not request this, please ignore this email.\n\n" +
                "Important Reminders:\n" +
                "- Shifter enrollment is only available during the 2nd semester for 1st and 2nd year students.\n" +
                "- You can only shift to a different program (BSIT ↔ BSENT).\n" +
                "- Upon acceptance, you will start as 1st Year, 1st Semester in the new program.\n" +
                "- Subjects you passed that exist in both programs will be credited.\n\n" +
                "For questions, contact the registrar's office.";

            message.Body = new TextPart("plain") { Text = bodyText };
            await SendInternalAsync(message);
        }
        public async Task SendSecondSemesterAcceptanceEmailAsync(string email, byte[] registrationPdf)
        {
            if (registrationPdf == null || registrationPdf.Length == 0)
                throw new System.InvalidOperationException("Registration slip PDF is required for second semester acceptance.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("University Enrollment", _senderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "2nd Semester Enrollment Accepted";

            var bodyText =
                "Congratulations! Your 2nd Semester enrollment has been accepted.\n\n" +
                "Attached is your Registration Form (PDF). Please print and have it stamped on-site at the registrar's office.\n\n" +
                "Note: Your portal account remains the same; no new credentials are issued.";

            var builder = new BodyBuilder { TextBody = bodyText };
            builder.Attachments.Add("RegistrationForm.pdf", registrationPdf, new ContentType("application", "pdf"));
            message.Body = builder.ToMessageBody();

            await SendInternalAsync(message);
        }

        public async Task SendRejectionEmailAsync(string email, string reason, string? editLink)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("University Enrollment", _senderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Enrollment Rejected";
            var extra = editLink == null
                ? ""
                : $"\n\nYou may revise and resubmit using this secure link (valid for 7 days):\n{editLink}\n";
            message.Body = new TextPart("plain")
            {
                Text = $"We regret to inform you that your enrollment was rejected.\nReason: {reason}{extra}"
            };
            await SendInternalAsync(message);
        }

        // Detailed rejection email. If editLink is provided, it will append a resubmission section.
        public async Task SendRejectionEmailDetailedAsync(string email, string reason, string flagsSummary, string? notes, string? editLink)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("University Enrollment", _senderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Enrollment Rejected";

            var notesPart = string.IsNullOrWhiteSpace(notes) ? "" : $"\nNotes: {notes.Trim()}";
            var resubPart = string.IsNullOrWhiteSpace(editLink) ? "" : $"\n\nYou may revise and resubmit using this secure link:\n{editLink}\n";

            var body =
                "We regret to inform you that your enrollment was rejected." +
                $"\nReason: {reason}" +
                notesPart +
                "\n\nDocument flags summary:\n" +
                flagsSummary +
                resubPart;

            message.Body = new TextPart("plain") { Text = body };
            await SendInternalAsync(message);
        }

        // Detailed resubmission email with reason, notes, flags summary and validity.
        public async Task SendResubmissionEmailDetailedAsync(string email, string reason, string flagsSummary, string? notes, string editLink, int tokenDays)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("University Enrollment", _senderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Resubmission Allowed";

            var notesPart = string.IsNullOrWhiteSpace(notes) ? "" : $"\nNotes: {notes.Trim()}";

            var body =
                "Your enrollment was previously rejected." +
                $"\nReason: {reason}" +
                notesPart +
                "\n\nDocument flags summary:\n" +
                flagsSummary +
                $"\n\nYou may revise and resubmit using this secure link (valid for {tokenDays} day(s)):\n{editLink}\n";

            message.Body = new TextPart("plain") { Text = body };
            await SendInternalAsync(message);
        }

        // NEW: Send email when a request is put on hold.
        public async Task SendOnHoldEmailDetailedAsync(string email, string reason, string flagsSummary)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("University Enrollment", _senderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Enrollment On Hold";

            var body =
                "Your enrollment request has been placed on hold." +
                $"\nReason: {reason}" +
                "\n\nDocument flags summary:\n" +
                flagsSummary +
                "\n\nPlease wait for further Updates.";

            message.Body = new TextPart("plain") { Text = body };
            await SendInternalAsync(message);
        }

        private async Task SendInternalAsync(MimeMessage message)
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            if (!string.IsNullOrWhiteSpace(_senderEmail))
                await client.AuthenticateAsync(_senderEmail, _senderPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}