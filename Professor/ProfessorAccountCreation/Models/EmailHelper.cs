using System.Net;
using System.Net.Mail;

namespace ProfessorAccountCreation.Models
{
    public static class EmailHelper
    {
        public static void SendOTP(string email, string otp)
        {
            var smtpHost = "smtp.gmail.com"; // from appsettings
            var smtpPort = 587;
            var smtpUser = "anjelocalderon123@gmail.com";
            var smtpPass = "ecye zvxp mank xpvt";

            var mail = new MailMessage();
            mail.From = new MailAddress(smtpUser, "LMS System");
            mail.To.Add(email);
            mail.Subject = "Your OTP for LMS Login";
            mail.Body = $"Your OTP is: {otp} \n It is valid for 6 hours.";

            using var smtp = new SmtpClient(smtpHost, smtpPort);
            smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);
            smtp.EnableSsl = true;
            smtp.Send(mail);
        }
    }
}
