namespace StudentPortal.Models
{
    public class ForgotPasswordViewModel
    {
        public string Email { get; set; }
        public string VerificationCode { get; set; }
        public string NewPassword { get; set; }
    }
}
