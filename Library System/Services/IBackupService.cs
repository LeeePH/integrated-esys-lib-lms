using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IBackupService
    {
        // Create backup
        Task<(bool success, string backupId, string message)> CreateBackupAsync(string createdBy);

        // Get all backups
        Task<List<BackupMetadata>> GetAllBackupsAsync();

        // Get latest backup
        Task<BackupMetadata> GetLatestBackupAsync();

        // Download backup
        Task<(bool success, byte[] fileData, string fileName)> DownloadBackupAsync(string backupId);

        // Restore backup
        Task<(bool success, string message)> RestoreBackupAsync(string backupId, string restoredBy);

        // Delete backup
        Task<bool> DeleteBackupAsync(string backupId);

        // Get backup by ID
        Task<BackupMetadata> GetBackupByIdAsync(string backupId);
    }
}

