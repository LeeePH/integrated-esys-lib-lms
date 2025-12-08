using System.Threading.Tasks;

namespace SystemLibrary.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task SendEmailAsync(string toEmail, string subject, string htmlBody, string? attachmentPath);
    }
}


