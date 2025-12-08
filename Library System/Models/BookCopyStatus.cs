namespace SystemLibrary.Models
{
    public enum BookCopyStatus
    {
        Available = 0,      // Available for borrowing
        Borrowed = 1,       // Currently borrowed by a user
        Lost = 2,          // Lost/missing copy
        Damaged = 3,       // Damaged and needs repair
        Maintenance = 4,   // Under maintenance/repair
        Retired = 5        // Retired from circulation
    }
}
