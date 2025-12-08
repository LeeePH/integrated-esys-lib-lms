using System.ComponentModel.DataAnnotations;
using SystemLibrary.Models;

namespace SystemLibrary.ViewModels
{
    public class StudentAccountViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string ContactNumber { get; set; }
        public string StudentNumber { get; set; }

        // NEW: Enhanced Penalty Information
        public decimal TotalPenalties { get; set; }
        public List<PenaltyRecord> PenaltyHistory { get; set; } = new List<PenaltyRecord>();
        public decimal UnpaidPenalties { get; set; }

        // EXISTING: Legacy Penalty Fields (kept for backward compatibility)
        public decimal PenaltyAmount { get; set; }
        public string OverdueBookTitle { get; set; }
        public DateTime? PenaltyDate { get; set; }

        // Overdue Books Information
        public int OverdueBooksCount { get; set; }

        // Helper properties
        public bool HasPenalty => TotalPenalties > 0 || PenaltyAmount > 0;
        public bool HasUnpaidPenalties => UnpaidPenalties > 0;
        public int TotalPenaltyRecords => PenaltyHistory?.Count ?? 0;
        public int UnpaidRecords => PenaltyHistory?.Count(p => !p.IsPaid) ?? 0;
    }

    public class UpdateAccountViewModel
    {
        [Required(ErrorMessage = "New password is required")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }
    }
}