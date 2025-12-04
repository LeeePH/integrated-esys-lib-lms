using System;
using System.Collections.Generic;

namespace SystemLibrary.ViewModels
{
    public class StudentActivityReportViewModel
    {
        public int ActiveBorrowers { get; set; }
        public decimal AverageBooksPerStudent { get; set; }
        public decimal AverageLoanDays { get; set; }
        public int TotalBorrowings { get; set; }
        public int DaysRange { get; set; } = 30;

        // Monthly activity data for chart
        public List<MonthlyActivityData> MonthlyActivity { get; set; } = new List<MonthlyActivityData>();

        // Most borrowed books
        public List<MostBorrowedBookReport> MostBorrowedBooks { get; set; } = new List<MostBorrowedBookReport>();

        // Most active students
        public List<ActiveStudentInfo> MostActiveStudents { get; set; } = new List<ActiveStudentInfo>();

        public List<CourseSummaryViewModel> CourseSummary { get; set; } = new();




        // Peak borrowing times
        public string PeakBorrowingMonth { get; set; } = "";
        public int PeakBorrowingCount { get; set; }
    }


    public class MonthlyActivityData
    {
        public string Month { get; set; } = "";
        public int BooksBorrowed { get; set; }
        public int BooksReturned { get; set; }
        public int ActiveStudents { get; set; }
    }

    public class MostBorrowedBookReport
    {
        public string BookId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public int BorrowCount { get; set; }
        public int UniqueStudents { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ActiveStudentInfo
    {
        public string UserId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string StudentNumber { get; set; } = "";
        public int TotalBorrowings { get; set; }
        public int CurrentBorrowings { get; set; }
        public DateTime LastBorrowDate { get; set; }
        public decimal AverageLoanDays { get; set; }
    }
    public class CourseSummaryViewModel
    {
        public string Course { get; set; } = "";
        public int TotalBorrowings { get; set; }
        public int TotalReturned { get; set; }
        public int TotalOverdue { get; set; }
        public int Rank { get; set; }
    }

}

