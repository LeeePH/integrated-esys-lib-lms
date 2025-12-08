using System;
using System.Collections.Generic;
using SystemLibrary.Models;

namespace SystemLibrary.ViewModels
{
    public class StudentDashboardViewModel
    {
        public string StudentName { get; set; } = "";
        public int ActiveBorrowings { get; set; }
        public int OverdueBooks { get; set; }
        public List<CurrentBorrowingViewModel> CurrentBorrowings { get; set; } = new List<CurrentBorrowingViewModel>();

        public List<Reservation> Reservations { get; set; } = new List<Reservation>();
        public List<Notification> Notifications { get; set; } = new List<Notification>();
        public int UnreadNotificationCount { get; set; }
        public bool HasPendingPenalties { get; set; }
        public decimal TotalPendingPenalties { get; set; }
        public List<Penalty> PendingPenalties { get; set; } = new List<Penalty>();
    }

    public class CurrentBorrowingViewModel
    {
        public string ReservationId { get; set; } = "";
        public string BookId { get; set; } = "";
        public string BookTitle { get; set; } = "";
        public string BookAuthor { get; set; } = "";
        public string BookCover { get; set; } = "/images/sample.png";
        public DateTime BorrowedDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "";
        public int DaysRemaining { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class NotificationViewModel
    {
        public string Type { get; set; } = ""; // RESERVATION_CONFIRMED, BOOK_DUE_SOON, OVERDUE, etc.
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}