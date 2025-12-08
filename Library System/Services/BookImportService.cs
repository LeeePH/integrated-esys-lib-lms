using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class BookImportService : IBookImportService
    {
        private readonly IMongoCollection<DummyBookImport> _imports;
        private readonly IMongoCollection<Book> _books;

        public BookImportService(IMongoDbService mongoDbService)
        {
            _imports = mongoDbService.GetCollection<DummyBookImport>("DummyBookImports");
            _books = mongoDbService.GetCollection<Book>("Books");
        }

        public async Task<bool> StageAsync(string isbn, int quantity, string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(isbn) || quantity <= 0) return false;
            var doc = new DummyBookImport
            {
                _id = ObjectId.GenerateNewId(),
                ISBN = isbn.Trim(),
                Quantity = quantity,
                Notes = notes,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };
            await _imports.InsertOneAsync(doc);
            return true;
        }

        public async Task<bool> ProcessAsync(string importId)
        {
            if (!ObjectId.TryParse(importId, out var oid)) return false;
            var import = await _imports.Find(i => i._id == oid).FirstOrDefaultAsync();
            if (import == null) return false;
            return await ProcessCoreAsync(import);
        }

        public async Task<bool> ProcessByIsbnAsync(string isbn, int quantity)
        {
            var import = new DummyBookImport
            {
                _id = ObjectId.GenerateNewId(),
                ISBN = isbn,
                Quantity = quantity,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };
            return await ProcessCoreAsync(import, persistStaging: false);
        }

        public async Task<List<DummyBookImport>> GetStagedAsync(string? status = null)
        {
            if (string.IsNullOrEmpty(status))
                return await _imports.Find(_ => true).SortByDescending(i => i.CreatedAt).ToListAsync();
            return await _imports.Find(i => i.Status == status).SortByDescending(i => i.CreatedAt).ToListAsync();
        }

        public async Task<int> ProcessByAuthorAsync(string authorName, int quantityPerTitle)
        {
            // MOCK data lookup removed - this method is no longer available
            // Books must be imported individually by ISBN or created manually
            throw new NotSupportedException("ProcessByAuthorAsync is no longer supported. MOCK data has been removed. Please import books individually by ISBN or create them manually.");
        }

        private async Task<bool> ProcessCoreAsync(DummyBookImport import, bool persistStaging = true)
        {
            try
            {
                // Check if book already exists by ISBN
                var existing = await _books.Find(b => b.ISBN == import.ISBN).FirstOrDefaultAsync();
                
                if (existing == null)
                {
                    // Create a new book with minimal information
                    // Since MOCK data is removed, books must be created with ISBN only
                    // Additional details can be added manually through the catalog interface
                    var newBook = new Book
                    {
                        _id = ObjectId.GenerateNewId(),
                        Title = $"Book - {import.ISBN}", // Placeholder title
                        Author = "Unknown", // Placeholder author
                        Publisher = string.Empty,
                        ClassificationNo = string.Empty,
                        ISBN = import.ISBN,
                        Subject = string.Empty,
                        Image = "/images/default-book.png",
                        TotalCopies = Math.Max(1, import.Quantity),
                        AvailableCopies = Math.Max(1, import.Quantity),
                        PublicationDate = DateTime.UtcNow.Date,
                        Restrictions = null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = null,
                        CopyManagementEnabled = false,
                        CopyPrefix = "SDS",
                        NextCopyNumber = 1
                    };
                    await _books.InsertOneAsync(newBook);
                }
                else
                {
                    // Book exists, just increase copies
                    var update = Builders<Book>.Update
                        .Inc(b => b.TotalCopies, import.Quantity)
                        .Inc(b => b.AvailableCopies, import.Quantity)
                        .Set(b => b.UpdatedAt, DateTime.UtcNow);
                    await _books.UpdateOneAsync(b => b._id == existing._id, update);
                }

                if (persistStaging)
                {
                    var upd = Builders<DummyBookImport>.Update
                        .Set(i => i.Status, "completed")
                        .Set(i => i.CompletedAt, DateTime.UtcNow);
                    await _imports.UpdateOneAsync(i => i._id == import._id, upd);
                }
                return true;
            }
            catch (Exception ex)
            {
                await MarkFailedAsync(import, ex.Message);
                return false;
            }
        }

        private async Task MarkFailedAsync(DummyBookImport import, string reason)
        {
            var upd = Builders<DummyBookImport>.Update
                .Set(i => i.Status, "failed")
                .Set(i => i.Notes, reason)
                .Set(i => i.CompletedAt, DateTime.UtcNow);
            await _imports.UpdateOneAsync(i => i._id == import._id, upd, new UpdateOptions { IsUpsert = true });
        }

        private static DateTime ParsePublicationDate(string? value)
        {
            if (DateTime.TryParse(value, out var dt)) return dt;
            return DateTime.UtcNow.Date;
        }
    }
}


