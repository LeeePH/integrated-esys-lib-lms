using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class SubjectService : ISubjectService
    {
        private readonly IMongoCollection<Subject> _subjects;
        private readonly IMongoCollection<Book> _books;

        public SubjectService(IMongoDbService mongoDbService)
        {
            _subjects = mongoDbService.GetCollection<Subject>("Subjects");
            _books = mongoDbService.GetCollection<Book>("Books");
        }

        public async Task<List<Subject>> GetAllAsync()
        {
            return await _subjects.Find(_ => true).ToListAsync();
        }

        public async Task<Subject?> GetByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return null;
            return await _subjects.Find(s => s._id == oid).FirstOrDefaultAsync();
        }

        public async Task<Subject?> GetByNameAsync(string name)
        {
            var filter = Builders<Subject>.Filter.Regex(s => s.Name, new BsonRegularExpression($"^{RegexEscape(name)}$", "i"));
            return await _subjects.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<bool> CreateAsync(Subject subject)
        {
            subject._id = ObjectId.GenerateNewId();
            subject.CreatedAt = DateTime.UtcNow;
            await _subjects.InsertOneAsync(subject);
            return true;
        }

        public async Task<bool> UpdateAsync(string id, Subject subject)
        {
            if (!ObjectId.TryParse(id, out var oid)) return false;
            subject._id = oid;
            subject.UpdatedAt = DateTime.UtcNow;
            var result = await _subjects.ReplaceOneAsync(s => s._id == oid, subject);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return false;
            var result = await _subjects.DeleteOneAsync(s => s._id == oid);
            return result.DeletedCount > 0;
        }

        public async Task<List<Book>> GetBooksBySubjectAsync(string subjectName)
        {
            var filter = Builders<Book>.Filter.Regex(b => b.Subject, new BsonRegularExpression(RegexEscape(subjectName), "i"));
            return await _books.Find(filter).ToListAsync();
        }

        private static string RegexEscape(string input)
        {
            return System.Text.RegularExpressions.Regex.Escape(input ?? string.Empty);
        }
    }
}


