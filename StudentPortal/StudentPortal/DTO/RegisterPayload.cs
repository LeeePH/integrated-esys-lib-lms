using System.ComponentModel.DataAnnotations;

namespace StudentPortal.DTO
{
    public class RegisterPayload
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;

        //[Required(ErrorMessage = "OTP is required.")]
        //[StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits.")]
        //public string OTP { get; set; } = string.Empty;
    }
}
