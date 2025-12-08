using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class AuthorService : IAuthorService
    {
        private readonly IMongoCollection<Author> _authors;
        private readonly IMongoCollection<Book> _books;

        public AuthorService(IMongoDbService mongoDbService)
        {
            _authors = mongoDbService.GetCollection<Author>("Authors");
            _books = mongoDbService.GetCollection<Book>("Books");
        }

        public async Task<List<Author>> GetAllAsync()
        {
            return await _authors.Find(_ => true).ToListAsync();
        }

        public async Task<Author?> GetByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return null;
            return await _authors.Find(a => a._id == oid).FirstOrDefaultAsync();
        }

        public async Task<Author?> GetByNameAsync(string name)
        {
            var filter = Builders<Author>.Filter.Regex(a => a.Name, new BsonRegularExpression($"^{RegexEscape(name)}$", "i"));
            return await _authors.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<bool> CreateAsync(Author author)
        {
            author._id = ObjectId.GenerateNewId();
            author.CreatedAt = DateTime.UtcNow;
            await _authors.InsertOneAsync(author);
            return true;
        }

        public async Task<bool> UpdateAsync(string id, Author author)
        {
            if (!ObjectId.TryParse(id, out var oid)) return false;
            author._id = oid;
            author.UpdatedAt = DateTime.UtcNow;
            var result = await _authors.ReplaceOneAsync(a => a._id == oid, author);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return false;
            var result = await _authors.DeleteOneAsync(a => a._id == oid);
            return result.DeletedCount > 0;
        }

        public async Task<List<Book>> GetBooksByAuthorAsync(string authorName)
        {
            var filter = Builders<Book>.Filter.Regex(b => b.Author, new BsonRegularExpression(RegexEscape(authorName), "i"));
            return await _books.Find(filter).ToListAsync();
        }

        private static string RegexEscape(string input)
        {
            return System.Text.RegularExpressions.Regex.Escape(input ?? string.Empty);
        }
    }
}


