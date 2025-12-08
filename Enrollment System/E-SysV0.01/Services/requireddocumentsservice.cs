using EnrollmentSystem.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using E_SysV0._01.Services; // ✅ Add this for MongoDBServices

namespace EnrollmentSystem.Services
{
    public class RequiredDocumentsService
    {
        private readonly MongoDBServices _mongo; // ✅ Replaced MongoDBContext with MongoDBServices

        public RequiredDocumentsService(MongoDBServices mongo)
        {
            _mongo = mongo;
        }

        // ✅ Get all required documents
        public async Task<List<RequiredDocument>> GetAllAsync()
        {
            return await _mongo.RequiredDocumentsCollection.Find(_ => true).ToListAsync();
        }

        // ✅ Add a required document
        public async Task AddAsync(RequiredDocument doc)
        {
            await _mongo.RequiredDocumentsCollection.InsertOneAsync(doc);
        }

        // ✅ Delete a required document by ID
        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _mongo.RequiredDocumentsCollection.DeleteOneAsync(d => d.Id == id);
            return result.DeletedCount > 0;
        }
    }
}
