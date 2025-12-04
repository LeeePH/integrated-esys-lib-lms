using MongoDB.Driver;
using MongoDB.Bson;
using SystemLibrary.Models;
using MongoDB.Bson.IO;

namespace SystemLibrary.Services
{
    public class BackupService : IBackupService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<BackupMetadata> _backupMetadata;
        private readonly IWebHostEnvironment _environment;
        private readonly string _backupDirectory;

        public BackupService(IMongoDbService mongoDbService, IWebHostEnvironment environment)
        {
            _database = mongoDbService.Database;
            _backupMetadata = _database.GetCollection<BackupMetadata>("BackupMetadata");
            _environment = environment;

            // Create backups directory in wwwroot
            _backupDirectory = Path.Combine(_environment.WebRootPath, "backups");
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }
        }

        public async Task<(bool success, string backupId, string message)> CreateBackupAsync(string createdBy)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var backupName = $"library_backup_{timestamp}.json";
                var backupPath = Path.Combine(_backupDirectory, backupName);

                // Get all collection names
                var collectionNames = await _database.ListCollectionNames().ToListAsync();
                collectionNames = collectionNames.Where(c => c != "BackupMetadata").ToList();

                // Create main backup document
                var backupDoc = new BsonDocument();

                // Backup each collection
                foreach (var collectionName in collectionNames)
                {
                    var collection = _database.GetCollection<BsonDocument>(collectionName);
                    var documents = await collection.Find(new BsonDocument()).ToListAsync();

                    // Store documents as BsonArray
                    backupDoc[collectionName] = new BsonArray(documents);
                }

                // Write to file using MongoDB's JSON format
                var jsonWriterSettings = new JsonWriterSettings
                {
                    OutputMode = JsonOutputMode.RelaxedExtendedJson,
                    Indent = true
                };

                var jsonString = backupDoc.ToJson(jsonWriterSettings);
                await File.WriteAllTextAsync(backupPath, jsonString);

                // Get file size
                var fileInfo = new FileInfo(backupPath);

                // Create metadata
                var metadata = new BackupMetadata
                {
                    BackupName = backupName,
                    FilePath = backupPath,
                    FileSize = fileInfo.Length,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    CollectionsIncluded = collectionNames,
                    Status = "Success",
                    Description = "Full database backup"
                };

                await _backupMetadata.InsertOneAsync(metadata);

                return (true, metadata._id, $"Backup created successfully: {backupName}");
            }
            catch (Exception ex)
            {
                return (false, null, $"Backup failed: {ex.Message}");
            }
        }

        public async Task<List<BackupMetadata>> GetAllBackupsAsync()
        {
            return await _backupMetadata
                .Find(_ => true)
                .SortByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<BackupMetadata> GetLatestBackupAsync()
        {
            return await _backupMetadata
                .Find(b => b.Status == "Success")
                .SortByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<(bool success, byte[] fileData, string fileName)> DownloadBackupAsync(string backupId)
        {
            try
            {
                var backup = await GetBackupByIdAsync(backupId);
                if (backup == null || !File.Exists(backup.FilePath))
                {
                    return (false, null, null);
                }

                var fileData = await File.ReadAllBytesAsync(backup.FilePath);
                return (true, fileData, backup.BackupName);
            }
            catch (Exception)
            {
                return (false, null, null);
            }
        }

        public async Task<(bool success, string message)> RestoreBackupAsync(string backupId, string restoredBy)
        {
            try
            {
                var backup = await GetBackupByIdAsync(backupId);
                if (backup == null || !File.Exists(backup.FilePath))
                {
                    return (false, "Backup file not found");
                }

                // Read backup file
                var jsonString = await File.ReadAllTextAsync(backup.FilePath);

                // Parse the backup file
                BsonDocument backupDoc;
                try
                {
                    backupDoc = BsonDocument.Parse(jsonString);
                }
                catch (Exception parseEx)
                {
                    return (false, $"Failed to parse backup file: {parseEx.Message}");
                }

                // Keep track of collections to restore
                int collectionsRestored = 0;
                int documentsRestored = 0;

                // Restore each collection
                foreach (var element in backupDoc.Elements)
                {
                    var collectionName = element.Name;

                    // Skip if not an array
                    if (!element.Value.IsBsonArray)
                    {
                        continue;
                    }

                    var documentsArray = element.Value.AsBsonArray;

                    // Don't restore BackupMetadata (to preserve backup history)
                    if (collectionName == "BackupMetadata")
                    {
                        continue;
                    }

                    try
                    {
                        // Drop existing collection
                        await _database.DropCollectionAsync(collectionName);

                        // Insert backed up documents
                        if (documentsArray.Count > 0)
                        {
                            var collection = _database.GetCollection<BsonDocument>(collectionName);
                            var documents = new List<BsonDocument>();

                            foreach (var docElement in documentsArray)
                            {
                                if (docElement.IsBsonDocument)
                                {
                                    documents.Add(docElement.AsBsonDocument);
                                }
                            }

                            if (documents.Count > 0)
                            {
                                await collection.InsertManyAsync(documents);
                                documentsRestored += documents.Count;
                            }
                        }

                        collectionsRestored++;
                    }
                    catch (Exception collEx)
                    {
                        return (false, $"Failed to restore collection '{collectionName}': {collEx.Message}");
                    }
                }

                return (true, $"Database restored successfully! {collectionsRestored} collections and {documentsRestored} documents restored from: {backup.BackupName}");
            }
            catch (Exception ex)
            {
                return (false, $"Restore failed: {ex.Message}");
            }
        }

        public async Task<bool> DeleteBackupAsync(string backupId)
        {
            try
            {
                var backup = await GetBackupByIdAsync(backupId);
                if (backup == null)
                {
                    return false;
                }

                // Delete file
                if (File.Exists(backup.FilePath))
                {
                    File.Delete(backup.FilePath);
                }

                // Delete metadata
                var result = await _backupMetadata.DeleteOneAsync(b => b._id == backupId);
                return result.DeletedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<BackupMetadata> GetBackupByIdAsync(string backupId)
        {
            if (!ObjectId.TryParse(backupId, out var objectId))
            {
                return null;
            }

            return await _backupMetadata
                .Find(b => b._id == backupId)
                .FirstOrDefaultAsync();
        }
    }
}