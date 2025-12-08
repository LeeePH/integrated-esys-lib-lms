namespace SystemLibrary.ViewModels
{
    public class BorrowingReportViewModel
    {
        public int TotalBorrowing { get; set; }
        public int TotalReturns { get; set; }
        public int CurrentlyBorrowed { get; set; }
        public List<MonthlyBorrowingData> MonthlyData { get; set; } = new List<MonthlyBorrowingData>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DaysRange { get; set; } = 30;
    }

    public class MonthlyBorrowingData
    {
        public string Month { get; set; } = "";
        public int Year { get; set; }
        public int TotalBorrowed { get; set; }
        public int TotalReturned { get; set; }
    }
}