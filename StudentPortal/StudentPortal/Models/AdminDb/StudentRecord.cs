namespace StudentPortal.Models.AdminDb
{
    // StudentRecord.cs
    public class StudentRecord
    {
        public string Id { get; set; } = string.Empty;       // MongoDB ObjectId
        public string ClassId { get; set; } = string.Empty;  // ClassId from ClassItem
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public double Grade { get; set; } = 0.0;
        public string Status { get; set; } = "Active";
    }


}
