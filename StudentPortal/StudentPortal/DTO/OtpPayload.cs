using System.ComponentModel.DataAnnotations;

namespace StudentPortal.DTO
{
    public class OtpPayload
    {
        [Required(ErrorMessage = "Email Address is Required")]
        public string EmailAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "OTP is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits.")]
        public string OTP { get; set; } = string.Empty;
    }
}
