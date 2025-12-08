public class ReturnTransactionViewModel
{
    public string ReservationId { get; set; } = "";
    public string BookTitle { get; set; } = "";
    public string UserName { get; set; } = "";
    public string BookCondition { get; set; } = "";  // e.g. “Good”, “Damage”, “Lost”
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public int DaysBorrowed { get; set; }
    public decimal LateFee { get; set; }
    // etc.
}

public class ReturnPageViewModel
{
    public ReturnTransactionViewModel? ReturnData { get; set; }
    // You may also want search term, status, etc.
}
