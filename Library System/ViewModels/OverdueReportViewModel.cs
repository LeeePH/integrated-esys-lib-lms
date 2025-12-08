using System;
using System.Collections.Generic;

namespace SystemLibrary.ViewModels
{
    public class OverdueReportViewModel
    {
        public int TotalOverdueAccounts { get; set; }
        public int TotalOverdueBooks { get; set; }
        public decimal TotalFees { get; set; }
        public List<OverdueAccountDetail> OverdueAccounts { get; set; } = new List<OverdueAccountDetail>();
        public int DaysRange { get; set; } = 30;
    }

    public class OverdueAccountDetail
    {
        public string UserId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string StudentNumber { get; set; } = "";
        public int OverdueBookCount { get; set; }
        public int MaxDaysOverdue { get; set; }
        public decimal TotalLateFees { get; set; }
        public bool IsRestricted { get; set; }
        public bool HasPendingRequest { get; set; } = false;
        public List<OverdueBookInfo> OverdueBooks { get; set; } = new List<OverdueBookInfo>();
    }

    public class OverdueBookInfo
    {
        public string BookTitle { get; set; } = "";
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public decimal LateFee { get; set; }
    }
}