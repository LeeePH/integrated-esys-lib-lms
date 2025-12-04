using System.ComponentModel.DataAnnotations;

namespace SystemLibrary.ViewModels
{
    public class CreateUserFromMOCKViewModel
    {
        [Required(ErrorMessage = "User type is required")]
        public string UserType { get; set; } // "student" or "staff"

        public string? Username { get; set; }

        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string? Password { get; set; }

        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string? ConfirmPassword { get; set; }

        // For students
        public string? StudentNumber { get; set; }

        // For staff
        public string? EmployeeId { get; set; }
        public string? Role { get; set; } // "admin" or "librarian"
    }
}
