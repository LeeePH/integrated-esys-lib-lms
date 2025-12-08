using MongoDB.Bson;
using System.Collections.Generic;

namespace SystemLibrary.Models
{
    public class DashboardViewModel
    {
        public List<Reservation> Reservations { get; set; } = new List<Reservation>();
        public List<Book> Books { get; set; } = new List<Book>();
        public List<Reservation> ActiveReturns { get; set; } = new List<Reservation>();
        public List<StudentProfile> StudentProfiles { get; set; } = new List<StudentProfile>();
        public List<User> Users { get; set; } = new List<User>();
        
        // Dashboard Statistics
        public int TotalBooks { get; set; }
        public int ActiveBorrowings { get; set; }
        public int TotalReturns { get; set; }
        public int PendingReservations { get; set; }
        
        // Chart Data
        public ChartData PieChartData { get; set; } = new ChartData();
        public MonthlyData MonthlyBorrowingVsReturns { get; set; } = new MonthlyData();
        
        // New Analytics Data
        public List<MostBorrowedBook> MostBorrowedBooks { get; set; } = new List<MostBorrowedBook>();
        public List<MostActiveStudent> MostActiveStudents { get; set; } = new List<MostActiveStudent>();
        public List<TopCourseProgram> TopCoursePrograms { get; set; } = new List<TopCourseProgram>();
        
        // Recent Activity
        public List<Reservation> RecentReservations { get; set; } = new List<Reservation>();
    }

    public class ChartData
    {
        public int OverDueAccounts { get; set; }
        public int OverdueBooks { get; set; }
        public int TotalFees { get; set; }
    }

    public class MonthlyData
    {
        public List<MonthlyStat> MonthlyStats { get; set; } = new List<MonthlyStat>();
    }

    public class MonthlyStat
    {
        public string Month { get; set; } = "";
        public int Borrowings { get; set; }
        public int Returns { get; set; }
    }

    public class MostBorrowedBook
    {
        public string BookId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public int BorrowCount { get; set; }
        public string Category { get; set; } = "";
    }

    public class MostActiveStudent
    {
        public string UserId { get; set; } = "";
        public string StudentNumber { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Course { get; set; } = "";
        public int BorrowCount { get; set; }
        public int ReturnCount { get; set; }
        public int TotalActivity { get; set; }
    }

    public class TopCourseProgram
    {
        public string CourseProgram { get; set; } = "";
        public int StudentCount { get; set; }
        public int TotalBorrowings { get; set; }
        public int ActiveStudents { get; set; }
    }
}
