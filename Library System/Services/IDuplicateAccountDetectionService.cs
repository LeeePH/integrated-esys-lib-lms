using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IDuplicateAccountDetectionService
    {
        Task<List<DuplicateAccountConflict>> DetectDuplicateAccountsAsync();
        Task<List<DuplicateAccountConflict>> DetectStaffAsStudentAsync();
        Task<List<DuplicateAccountConflict>> GetAllConflictsAsync(bool includeResolved = false);
        Task<bool> ResolveConflictAsync(string conflictId, string adminId, string resolutionNotes);
        Task<int> RunDetectionAsync();
    }
}

