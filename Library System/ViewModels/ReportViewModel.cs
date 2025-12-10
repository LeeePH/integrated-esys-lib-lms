using System;
using System.Collections.Generic;

namespace SystemLibrary.ViewModels
{
    public class ReportViewModel
    {
        public string TimeRange { get; set; } = "ThisMonth";
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public BorrowingTrendReport BorrowingTrend { get; set; } = new BorrowingTrendReport();
        public List<OverdueAccountReport> OverdueAccounts { get; set; } = new List<OverdueAccountReport>();
        public InventoryStatusReport InventoryStatus { get; set; } = new InventoryStatusReport();
        public List<StudentActivityReport> StudentActivity { get; set; } = new List<StudentActivityReport>();
    }

    public class BorrowingTrendReport
    {
        public int TotalBorrowing { get; set; }
        public int TotalReturn { get; set; }
        public int CurrentlyBorrowed { get; set; }
        public List<MonthlyTrendData> MonthlyData { get; set; } = new List<MonthlyTrendData>();
    }

    // RENAMED to avoid conflict with BorrowingReportViewModel.MonthlyBorrowingData
    public class MonthlyTrendData
    {
        public string Month { get; set; } = "";
        public int Borrowed { get; set; }
        public int Returned { get; set; }
    }

    public class OverdueAccountReport
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string BookTitle { get; set; } = "";
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public decimal LateFees { get; set; }
    }

    public class InventoryStatusReport
    {
        public int TotalBooks { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
        public int BorrowedCopies { get; set; }
        public List<BookInventoryItem> LowStockBooks { get; set; } = new List<BookInventoryItem>();
        public List<BookBorrowingStats> MostBorrowedBooks { get; set; } = new List<BookBorrowingStats>();
    }

    public class BookInventoryItem
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public int AvailableCopies { get; set; }
    }

    public class BookBorrowingStats
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public int BorrowCount { get; set; }
    }

    public class StudentActivityReport
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public int TotalBorrowings { get; set; }
        public int CurrentBorrowings { get; set; }
        public int OverdueBooks { get; set; }
        public decimal TotalLateFees { get; set; }
        public DateTime LastActivity { get; set; }
    }
}