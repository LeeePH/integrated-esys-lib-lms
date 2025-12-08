using System.Collections.Generic;

namespace SystemLibrary.ViewModels
{
    public class InventoryReportViewModel
    {
        public List<CategoryInventory> Categories { get; set; } = new List<CategoryInventory>();
        public int TotalBooks { get; set; }
        public int TotalAvailable { get; set; }
        public int TotalBorrowed { get; set; }
        public int TotalReserved { get; set; }
        public int DaysRange { get; set; } = 30;
    }

    public class CategoryInventory
    {
        public string Category { get; set; } = "";
        public int TotalBooks { get; set; }
        public int Available { get; set; }
        public int Borrowed { get; set; }
        public int Reserved { get; set; }
        public double Utilization { get; set; }
    }
}
