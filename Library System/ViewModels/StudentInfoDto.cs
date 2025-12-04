public class StudentInfoDto
{
    public string StudentId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string IdNumber { get; set; }
    public string Status { get; set; } // "good", "restricted", "limit"
    public string EnrollmentStatus { get; set; }
    public int BorrowedBooks { get; set; }
    public int MaxBooksAllowed { get; set; }
    public int OverdueBooks { get; set; }
}