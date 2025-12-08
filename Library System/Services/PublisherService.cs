using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class PublisherService : IPublisherService
    {
        private readonly IMongoCollection<Publisher> _publishers;
        private readonly IMongoCollection<Book> _books;

        public PublisherService(IMongoDbService mongoDbService)
        {
            _publishers = mongoDbService.GetCollection<Publisher>("Publishers");
            _books = mongoDbService.GetCollection<Book>("Books");
        }

        public async Task<List<Publisher>> GetAllAsync()
        {
            return await _publishers.Find(_ => true).ToListAsync();
        }

        public async Task<Publisher?> GetByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return null;
            return await _publishers.Find(p => p._id == oid).FirstOrDefaultAsync();
        }

        public async Task<Publisher?> GetByNameAsync(string name)
        {
            var filter = Builders<Publisher>.Filter.Regex(p => p.Name, new BsonRegularExpression($"^{RegexEscape(name)}$", "i"));
            return await _publishers.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<bool> CreateAsync(Publisher publisher)
        {
            publisher._id = ObjectId.GenerateNewId();
            publisher.CreatedAt = DateTime.UtcNow;
            await _publishers.InsertOneAsync(publisher);
            return true;
        }

        public async Task<bool> UpdateAsync(string id, Publisher publisher)
        {
            if (!ObjectId.TryParse(id, out var oid)) return false;
            publisher._id = oid;
            publisher.UpdatedAt = DateTime.UtcNow;
            var result = await _publishers.ReplaceOneAsync(p => p._id == oid, publisher);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return false;
            var result = await _publishers.DeleteOneAsync(p => p._id == oid);
            return result.DeletedCount > 0;
        }

        public async Task<List<Book>> GetBooksByPublisherAsync(string publisherName)
        {
            var filter = Builders<Book>.Filter.Regex(b => b.Publisher, new BsonRegularExpression(RegexEscape(publisherName), "i"));
            return await _books.Find(filter).ToListAsync();
        }

        private static string RegexEscape(string input)
        {
            return System.Text.RegularExpressions.Regex.Escape(input ?? string.Empty);
        }
    }
}


