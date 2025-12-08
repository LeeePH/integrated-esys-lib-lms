using MongoDB.Driver;
using MongoDB.Bson;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class BookCopyService : IBookCopyService
    {
        private readonly IMongoCollection<BookCopy> _bookCopies;
        private readonly IMongoCollection<Book> _books;

        public BookCopyService(IMongoDbService mongoDbService)
        {
            _bookCopies = mongoDbService.GetCollection<BookCopy>("bookcopies");
            _books = mongoDbService.GetCollection<Book>("books");
        }

        public async Task<BookCopy?> GetBookCopyByIdAsync(string copyId)
        {
            return await _bookCopies.Find(c => c._id == copyId).FirstOrDefaultAsync();
        }

        public async Task<BookCopy?> GetBookCopyByBarcodeAsync(string barcode)
        {
            return await _bookCopies.Find(c => c.Barcode == barcode).FirstOrDefaultAsync();
        }

        public async Task<List<BookCopy>> GetBookCopiesByBookIdAsync(string bookId)
        {
            return await _bookCopies.Find(c => c.BookId == bookId).ToListAsync();
        }

        public async Task<List<BookCopy>> GetAvailableCopiesByBookIdAsync(string bookId)
        {
            return await _bookCopies.Find(c => c.BookId == bookId && c.Status == BookCopyStatus.Available).ToListAsync();
        }

        public async Task<BookCopy> CreateBookCopyAsync(BookCopy bookCopy)
        {
            await _bookCopies.InsertOneAsync(bookCopy);
            return bookCopy;
        }

        public async Task<bool> UpdateBookCopyAsync(BookCopy bookCopy)
        {
            var result = await _bookCopies.ReplaceOneAsync(c => c._id == bookCopy._id, bookCopy);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteBookCopyAsync(string copyId)
        {
            var result = await _bookCopies.DeleteOneAsync(c => c._id == copyId);
            return result.DeletedCount > 0;
        }

        public async Task<List<BookCopy>> CreateMultipleCopiesAsync(string bookId, int numberOfCopies, string createdBy)
        {
            var book = await _books.Find(b => b._id.ToString() == bookId).FirstOrDefaultAsync();
            if (book == null)
                throw new ArgumentException("Book not found");

            var copies = new List<BookCopy>();
            var currentCopyNumber = book.NextCopyNumber;

            for (int i = 0; i < numberOfCopies; i++)
            {
                var copyId = $"{book.CopyPrefix}-{currentCopyNumber:D4}";
                var barcode = GenerateBarcode(bookId, currentCopyNumber);

                var bookCopy = new BookCopy
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    BookId = bookId,
                    CopyId = copyId,
                    Barcode = barcode,
                    Status = BookCopyStatus.Available,
                    Condition = CopyCondition.Good,
                    Location = "IT - 001 - A3", // Default location
                    AcquisitionDate = DateTime.UtcNow,
                    BorrowCount = 0,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.UtcNow
                };

                copies.Add(bookCopy);
                currentCopyNumber++;
            }

            if (copies.Any())
            {
                await _bookCopies.InsertManyAsync(copies);
                
                // Update book's next copy number
                var update = Builders<Book>.Update.Set(b => b.NextCopyNumber, currentCopyNumber);
                await _books.UpdateOneAsync(b => b._id.ToString() == bookId, update);
            }

            return copies;
        }

        public async Task<bool> UpdateCopyStatusAsync(string copyId, BookCopyStatus status)
        {
            var update = Builders<BookCopy>.Update
                .Set(c => c.Status, status)
                .Set(c => c.ModifiedDate, DateTime.UtcNow);

            var result = await _bookCopies.UpdateOneAsync(c => c._id == copyId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateCopyConditionAsync(string copyId, CopyCondition condition, string notes = "")
        {
            var update = Builders<BookCopy>.Update
                .Set(c => c.Condition, condition)
                .Set(c => c.Notes, notes)
                .Set(c => c.ModifiedDate, DateTime.UtcNow);

            var result = await _bookCopies.UpdateOneAsync(c => c._id == copyId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> MarkCopyAsLostAsync(string copyId, string reason = "")
        {
            var update = Builders<BookCopy>.Update
                .Set(c => c.Status, BookCopyStatus.Lost)
                .Set(c => c.Notes, reason)
                .Set(c => c.ModifiedDate, DateTime.UtcNow);

            var result = await _bookCopies.UpdateOneAsync(c => c._id == copyId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> MarkCopyAsDamagedAsync(string copyId, string damageNotes = "")
        {
            var update = Builders<BookCopy>.Update
                .Set(c => c.Status, BookCopyStatus.Damaged)
                .Set(c => c.Condition, CopyCondition.Damaged)
                .Set(c => c.Notes, damageNotes)
                .Set(c => c.ModifiedDate, DateTime.UtcNow);

            var result = await _bookCopies.UpdateOneAsync(c => c._id == copyId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> MarkCopyAsFoundAsync(string copyId)
        {
            var update = Builders<BookCopy>.Update
                .Set(c => c.Status, BookCopyStatus.Available)
                .Set(c => c.ModifiedDate, DateTime.UtcNow);

            var result = await _bookCopies.UpdateOneAsync(c => c._id == copyId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> MarkCopyAsRepairedAsync(string copyId)
        {
            var update = Builders<BookCopy>.Update
                .Set(c => c.Status, BookCopyStatus.Available)
                .Set(c => c.Condition, CopyCondition.Good)
                .Set(c => c.ModifiedDate, DateTime.UtcNow);

            var result = await _bookCopies.UpdateOneAsync(c => c._id == copyId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> IsCopyAvailableAsync(string copyId)
        {
            var copy = await _bookCopies.Find(c => c._id == copyId).FirstOrDefaultAsync();
            return copy?.Status == BookCopyStatus.Available;
        }

        public async Task<bool> IsCopyBorrowedAsync(string copyId)
        {
            var copy = await _bookCopies.Find(c => c._id == copyId).FirstOrDefaultAsync();
            return copy?.Status == BookCopyStatus.Borrowed;
        }

        public async Task<BookCopyStatus> GetCopyStatusAsync(string copyId)
        {
            var copy = await _bookCopies.Find(c => c._id == copyId).FirstOrDefaultAsync();
            return copy?.Status ?? BookCopyStatus.Available;
        }

        public async Task<List<BookCopy>> GetCopiesByStatusAsync(BookCopyStatus status)
        {
            return await _bookCopies.Find(c => c.Status == status).ToListAsync();
        }

        public async Task<List<BookCopy>> GetLostCopiesAsync()
        {
            return await _bookCopies.Find(c => c.Status == BookCopyStatus.Lost).ToListAsync();
        }

        public async Task<List<BookCopy>> GetDamagedCopiesAsync()
        {
            return await _bookCopies.Find(c => c.Status == BookCopyStatus.Damaged).ToListAsync();
        }

        public async Task<int> GetTotalCopiesCountAsync(string bookId)
        {
            return (int)await _bookCopies.CountDocumentsAsync(c => c.BookId == bookId);
        }

        public async Task<int> GetAvailableCopiesCountAsync(string bookId)
        {
            return (int)await _bookCopies.CountDocumentsAsync(c => c.BookId == bookId && c.Status == BookCopyStatus.Available);
        }

        public async Task<int> GetBorrowedCopiesCountAsync(string bookId)
        {
            return (int)await _bookCopies.CountDocumentsAsync(c => c.BookId == bookId && c.Status == BookCopyStatus.Borrowed);
        }

        public async Task<int> GetLostCopiesCountAsync(string bookId)
        {
            return (int)await _bookCopies.CountDocumentsAsync(c => c.BookId == bookId && c.Status == BookCopyStatus.Lost);
        }

        public async Task<int> GetDamagedCopiesCountAsync(string bookId)
        {
            return (int)await _bookCopies.CountDocumentsAsync(c => c.BookId == bookId && c.Status == BookCopyStatus.Damaged);
        }

        public async Task<Dictionary<BookCopyStatus, int>> GetCopyStatusSummaryAsync(string bookId)
        {
            var pipeline = new[]
            {
                new BsonDocument("$match", new BsonDocument("book_id", bookId)),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$status" },
                    { "count", new BsonDocument("$sum", 1) }
                })
            };

            var results = await _bookCopies.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var summary = new Dictionary<BookCopyStatus, int>();

            foreach (var result in results)
            {
                var status = (BookCopyStatus)result["_id"].AsInt32;
                var count = result["count"].AsInt32;
                summary[status] = count;
            }

            return summary;
        }

        public async Task<string> GenerateNextCopyIdAsync(string bookId)
        {
            var book = await _books.Find(b => b._id.ToString() == bookId).FirstOrDefaultAsync();
            if (book == null)
                throw new ArgumentException("Book not found");

            var nextNumber = book.NextCopyNumber;
            var copyId = $"{book.CopyPrefix}-{nextNumber:D4}";

            // Update the book's next copy number
            var update = Builders<Book>.Update.Set(b => b.NextCopyNumber, nextNumber + 1);
            await _books.UpdateOneAsync(b => b._id.ToString() == bookId, update);

            return copyId;
        }

        public async Task<bool> ValidateCopyIdAsync(string copyId)
        {
            var copy = await _bookCopies.Find(c => c.CopyId == copyId).FirstOrDefaultAsync();
            return copy != null;
        }

        public async Task<List<BookCopy>> GetCopiesNeedingAttentionAsync()
        {
            var filter = Builders<BookCopy>.Filter.In(c => c.Status, new[]
            {
                BookCopyStatus.Lost,
                BookCopyStatus.Damaged,
                BookCopyStatus.Maintenance
            });

            return await _bookCopies.Find(filter).ToListAsync();
        }

        private string GenerateBarcode(string bookId, int copyNumber)
        {
            // Generate a simple barcode format: BOOK_ID + COPY_NUMBER
            return $"{bookId.Substring(0, 8)}{copyNumber:D4}";
        }
    }
}
