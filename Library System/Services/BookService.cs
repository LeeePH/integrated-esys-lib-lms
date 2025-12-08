using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class BookService : IBookService
    {
        private readonly IMongoCollection<Book> _books;
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IMongoCollection<ReturnTransaction> _returns;
        private readonly IReservationService _reservationService;

        public BookService(IMongoDbService mongoDbService, IReservationService reservationService)
        {
            _books = mongoDbService.GetCollection<Book>("Books");
            _reservations = mongoDbService.GetCollection<Reservation>("Reservations");
            _returns = mongoDbService.GetCollection<ReturnTransaction>("Returns");
            _reservationService = reservationService;
        }

        // ✅ Get all books
        public async Task<List<Book>> GetAllBooksAsync()
        {
            return await _books.Find(_ => true).ToListAsync();
        }

        // ✅ Get book by ID
        public async Task<Book> GetBookByIdAsync(string bookId)
        {
            if (ObjectId.TryParse(bookId, out ObjectId objectId))
            {
                return await _books.Find(b => b._id == objectId).FirstOrDefaultAsync();
            }
            return null;
        }

        // ✅ Get only available books
        public async Task<List<Book>> GetAvailableBooksAsync()
        {
            return await _books.Find(b => b.AvailableCopies > 0).ToListAsync();
        }

        // ✅ Search books by multiple fields
        public async Task<List<Book>> SearchBooksAsync(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                return await GetAllBooksAsync();
            }

            var filter = Builders<Book>.Filter.Or(
                Builders<Book>.Filter.Regex(b => b.Title, new BsonRegularExpression(searchTerm, "i")),
                Builders<Book>.Filter.Regex(b => b.Author, new BsonRegularExpression(searchTerm, "i")),
                Builders<Book>.Filter.Regex(b => b.ISBN, new BsonRegularExpression(searchTerm, "i")),
                Builders<Book>.Filter.Regex(b => b.Subject, new BsonRegularExpression(searchTerm, "i")),
                Builders<Book>.Filter.Regex(b => b.ClassificationNo, new BsonRegularExpression(searchTerm, "i")),
                Builders<Book>.Filter.Regex(b => b.Publisher, new BsonRegularExpression(searchTerm, "i"))
            );

            return await _books.Find(filter).ToListAsync();
        }

        // ✅ Add a new book
        public async Task<bool> AddBookAsync(Book book)
        {
            try
            {
                book._id = ObjectId.GenerateNewId();
                book.CreatedAt = DateTime.UtcNow;
                book.UpdatedAt = null;

                // Default AvailableCopies = TotalCopies if not set
                if (book.AvailableCopies <= 0)
                {
                    book.AvailableCopies = book.TotalCopies;
                }

                await _books.InsertOneAsync(book);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding book: {ex.Message}");
                return false;
            }
        }

        // ✅ Update a book (preserving CreatedAt & handling copies correctly)
        public async Task<bool> UpdateBookAsync(string bookId, Book book)
        {
            try
            {
                if (!ObjectId.TryParse(bookId, out ObjectId objectId))
                    return false;

                var existingBook = await GetBookByIdAsync(bookId);
                if (existingBook == null)
                    return false;

                // Calculate available copy difference
                var copyDifference = book.TotalCopies - existingBook.TotalCopies;
                var newAvailableCopies = existingBook.AvailableCopies + copyDifference;
                newAvailableCopies = Math.Max(0, Math.Min(newAvailableCopies, book.TotalCopies));

                // Build the update document
                var updateBuilder = Builders<Book>.Update
                    .Set(b => b.Title, book.Title)
                    .Set(b => b.Author, book.Author)
                    .Set(b => b.Publisher, book.Publisher)
                    .Set(b => b.ClassificationNo, book.ClassificationNo)
                    .Set(b => b.ISBN, book.ISBN)
                    .Set(b => b.Subject, book.Subject)
                    .Set(b => b.Image, book.Image)
                    .Set(b => b.TotalCopies, book.TotalCopies)
                    .Set(b => b.AvailableCopies, newAvailableCopies)
                    .Set(b => b.PublicationDate, book.PublicationDate)
                    .Set(b => b.Restrictions, book.Restrictions)
                    .Set(b => b.IsActive, book.IsActive)
                    .Set(b => b.IsReferenceOnly, book.IsReferenceOnly)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow)
                    .Set(b => b.CreatedAt, existingBook.CreatedAt);

                // Add copy management fields if they're being changed
                if (book.CopyManagementEnabled != existingBook.CopyManagementEnabled)
                {
                    updateBuilder = updateBuilder.Set(b => b.CopyManagementEnabled, book.CopyManagementEnabled);
                }
                
                if (!string.IsNullOrEmpty(book.CopyPrefix))
                {
                    updateBuilder = updateBuilder.Set(b => b.CopyPrefix, book.CopyPrefix);
                }
                
                if (book.NextCopyNumber > 0)
                {
                    updateBuilder = updateBuilder.Set(b => b.NextCopyNumber, book.NextCopyNumber);
                }

                var update = updateBuilder;

                var result = await _books.UpdateOneAsync(b => b._id == objectId, update);
                
                // If copies were added and there are now available copies, try to auto-approve waitlisted students
                if (result.ModifiedCount > 0 && copyDifference > 0 && newAvailableCopies > 0)
                {
                    try
                    {
                        // Try to approve the next waitlisted student for each newly available copy
                        for (int i = 0; i < copyDifference && i < newAvailableCopies; i++)
                        {
                            var autoApproved = await _reservationService.ApproveNextAndHoldAsync(bookId);
                            if (!autoApproved)
                            {
                                // No more waitlisted students, stop trying
                                break;
                            }
                        }
                        Console.WriteLine($"[BookService] After adding {copyDifference} copy/copies, attempted to auto-approve waitlisted students for book {bookId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BookService] Error auto-approving waitlisted students after adding copies: {ex.Message}");
                        // Don't fail the update if auto-approval fails
                    }
                }
                
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating book: {ex.Message}");
                return false;
            }
        }

        // ✅ Delete a book (prevent deletion if still borrowed)
        public async Task<bool> DeleteBookAsync(string bookId)
        {
            try
            {
                if (ObjectId.TryParse(bookId, out ObjectId objectId))
                {
                    var book = await GetBookByIdAsync(bookId);
                    if (book != null && book.BorrowedCopies > 0)
                    {
                        // Prevent deletion if book is currently borrowed
                        return false;
                    }

                    var result = await _books.DeleteOneAsync(b => b._id == objectId);
                    return result.DeletedCount > 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting book: {ex.Message}");
                return false;
            }
        }

        // ✅ Borrow a book (decrease available copies)
        public async Task<bool> BorrowBookAsync(string bookId)
        {
            if (ObjectId.TryParse(bookId, out ObjectId objectId))
            {
                var filter = Builders<Book>.Filter.And(
                    Builders<Book>.Filter.Eq(b => b._id, objectId),
                    Builders<Book>.Filter.Gt(b => b.AvailableCopies, 0)
                );

                var update = Builders<Book>.Update
                    .Inc(b => b.AvailableCopies, -1)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                var result = await _books.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            return false;
        }

        // ✅ Return a book (increase available copies if not full)
        public async Task<bool> ReturnBookAsync(string bookId)
        {
            if (ObjectId.TryParse(bookId, out ObjectId objectId))
            {
                var filter = Builders<Book>.Filter.And(
                    Builders<Book>.Filter.Eq(b => b._id, objectId),
                    new BsonDocument("$expr",
                        new BsonDocument("$lt", new BsonArray { "$available_copies", "$total_copies" })
                    )
                );

                var update = Builders<Book>.Update
                    .Inc(b => b.AvailableCopies, 1)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                var result = await _books.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            return false;
        }

        // ✅ Get Book of the Day (deterministic random based on day of year)
        public async Task<Book?> GetBookOfTheDayAsync()
        {
            try
            {
                // Get current day of year (1-365)
                int dayOfYear = DateTime.Now.DayOfYear;
                
                // Get all available books
                var books = await _books.Find(b => b.AvailableCopies > 0).ToListAsync();
                
                if (books == null || !books.Any())
                    return null;

                // Use day of year as seed for deterministic "random" selection
                int index = dayOfYear % books.Count;
                return books[index];
            }
            catch
            {
                return null;
            }
        }

        // ✅ Get Trending Books (books with most reservations in last 30 days)
        public async Task<List<Book>> GetTrendingBooksAsync(int count = 10)
        {
            try
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                
                // Get all reservations from last 30 days
                var recentReservations = await _reservations
                    .Find(r => r.ReservationDate >= thirtyDaysAgo)
                    .ToListAsync();

                // Count reservations per book
                var bookReservationCounts = recentReservations
                    .GroupBy(r => r.BookId)
                    .Select(g => new { BookId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(count)
                    .ToList();

                if (!bookReservationCounts.Any())
                {
                    // Fallback: return most recently added books
                    return await _books.Find(_ => true)
                        .SortByDescending(b => b.CreatedAt)
                        .Limit(count)
                        .ToListAsync();
                }

                // Fetch the actual book objects
                var trendingBookIds = bookReservationCounts.Select(b => b.BookId).ToList();
                var bookObjectIds = trendingBookIds
                    .Where(id => ObjectId.TryParse(id, out _))
                    .Select(id => ObjectId.Parse(id))
                    .ToList();
                var books = await _books.Find(b => bookObjectIds.Contains(b._id)).ToListAsync();

                // Return in order of popularity
                return books.OrderByDescending(b => {
                    var match = bookReservationCounts.FirstOrDefault(br => br.BookId == b._id.ToString());
                    return match?.Count ?? 0;
                }).Take(count).ToList();
            }
            catch
            {
                // Fallback: return recent books
                return await _books.Find(_ => true)
                    .SortByDescending(b => b.CreatedAt)
                    .Limit(count)
                    .ToListAsync();
            }
        }

        // ✅ Get Recommended Books (based on borrowing patterns, availability, and diversity)
        public async Task<List<Book>> GetRecommendedBooksAsync(string? userId = null, int count = 10)
        {
            try
            {
                // Get books with borrowing history (books that have been borrowed/returned)
                var borrowedBooks = await _returns
                    .Find(_ => true)
                    .ToListAsync();

                if (borrowedBooks == null || !borrowedBooks.Any())
                {
                    // No borrowing history, recommend available books by popularity
                    return await _books.Find(b => b.AvailableCopies > 0)
                        .SortByDescending(b => b.TotalCopies)
                        .Limit(count)
                        .ToListAsync();
                }

                // Count how many times each book has been borrowed
                var bookBorrowCounts = borrowedBooks
                    .GroupBy(b => b.BookId.ToString())
                    .Select(g => new { BookId = g.Key, BorrowCount = g.Count() })
                    .OrderByDescending(x => x.BorrowCount)
                    .ToList();

                // Get book IDs that have been borrowed
                var popularBookIds = bookBorrowCounts.Select(b => b.BookId).ToList();

                // Fetch books, prioritizing those with borrowing history
                var bookObjectIds = popularBookIds
                    .Where(id => ObjectId.TryParse(id, out _))
                    .Select(id => ObjectId.Parse(id))
                    .ToList();
                var recommendedBooks = await _books.Find(b => 
                    bookObjectIds.Contains(b._id) && b.AvailableCopies > 0)
                    .ToListAsync();

                // Sort by borrow count and availability
                var sortedBooks = recommendedBooks.OrderByDescending(b => {
                    var match = bookBorrowCounts.FirstOrDefault(br => br.BookId == b._id.ToString());
                    return match?.BorrowCount ?? 0;
                }).ToList();
                
                // Convert ObjectId to string for comparison
                var existingIds = sortedBooks.Select(b => b._id.ToString()).ToList();

                // If we don't have enough books, add available books with no history
                if (sortedBooks.Count < count)
                {
                    var additionalBooks = await _books.Find(b => 
                        b.AvailableCopies > 0 && !existingIds.Contains(b._id.ToString()))
                        .Limit(count - sortedBooks.Count)
                        .ToListAsync();
                    sortedBooks.AddRange(additionalBooks);
                }

                return sortedBooks.Take(count).ToList();
            }
            catch
            {
                // Fallback: return available books ordered by total copies
                return await _books.Find(b => b.AvailableCopies > 0)
                    .SortByDescending(b => b.TotalCopies)
                    .Limit(count)
                    .ToListAsync();
            }
        }
    }
}
