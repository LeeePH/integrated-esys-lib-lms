using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProfessorAccountCreation.Models;

namespace ProfessorAccountCreation.Models
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IOptions<MongoDBSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            _database = client.GetDatabase(settings.Value.DatabaseName);
        }

        // Existing collection
        public IMongoCollection<Professor> Professors => _database.GetCollection<Professor>("Professors");

        // Add SuperAdmins collection
        public IMongoCollection<ProfAdmin> ProfAdmins => _database.GetCollection<ProfAdmin>("ProfAdmins");

        public IMongoCollection<ProfessorAssignment> ProfessorAssignments
    => _database.GetCollection<ProfessorAssignment>("professorAssignments");

    }
}
