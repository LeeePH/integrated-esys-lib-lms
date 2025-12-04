namespace SystemLibrary.Models
{
    public class ProcessReturnRequest
    {
        public string ReservationId { get; set; } = "";
        public string BookId { get; set; } = "";
        public string UserId { get; set; } = "";
        public string BookTitle { get; set; } = "";
        public string BorrowDate { get; set; } = "";
        public string DueDate { get; set; } = "";
        public int DaysLate { get; set; }
        public decimal LateFees { get; set; }
        public string BookCondition { get; set; } = "Good";
        public string? DamageType { get; set; }  // ⭐ THIS IS CRITICAL
        public decimal DamagePenalty { get; set; }  // ⭐ THIS IS CRITICAL
        public decimal PenaltyAmount { get; set; }
        public decimal TotalPenalty { get; set; }
    }
}