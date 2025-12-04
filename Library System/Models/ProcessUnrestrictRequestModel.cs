namespace SystemLibrary.Models
{
    public class ProcessUnrestrictRequestModel
    {
        public string RequestId { get; set; }
        public string Status { get; set; } // "Approved" or "Rejected"
        public string AdminNotes { get; set; }
    }
}
