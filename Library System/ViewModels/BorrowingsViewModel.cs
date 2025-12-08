using System;
using System.Collections.Generic;

namespace SystemLibrary.ViewModels
{
    public class BorrowingsViewModel
    {
        public List<BorrowedBookViewModel> BorrowedBooks { get; set; } = new List<BorrowedBookViewModel>();
        public List<WaitlistViewModel> Waitlist { get; set; } = new List<WaitlistViewModel>();
        public List<CurrentBorrowingViewModel> ActiveBorrowings { get; set; } = new List<CurrentBorrowingViewModel>();
    }

    public class BorrowedBookViewModel
    {
        public string ReservationId { get; set; } = "";
        public string BookId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Image { get; set; } = "";
        public DateTime BorrowedDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "";
        public int DaysRemaining { get; set; }
        public bool IsOverdue => DaysRemaining < 0;
        public bool IsDueSoon => DaysRemaining <= 3 && DaysRemaining >= 0;
    }

     public class BorrowingHistoryViewModel
    {
        public string BookTitle { get; set; }
        public string BookAuthor { get; set; }
        public DateTime BorrowedDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public string Status { get; set; } // "Returned", "Overdue Returned", "Active"
    }

    public class WaitlistViewModel
    {
        public string Id { get; set; } = "";
        public string BookTitle { get; set; } = "";
        public string BookAuthor { get; set; } = "";
        public string ReservationType { get; set; } = "";
        public DateTime ReservationDate { get; set; }
        public string Status { get; set; } = "";
    }

}