using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using System;
using MongoDB.Bson;

namespace SystemLibrary.Services
{
    public class MongoDbService : IMongoDbService
    {
        private readonly IMongoDatabase _database;

        public MongoDbService(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoDB");
            var mongoClient = new MongoClient(connectionString);

            try
            {
                _database = mongoClient.GetDatabase("LibraDB");

                mongoClient.GetDatabase("admin")
                           .RunCommandAsync((Command<BsonDocument>)"{ping:1}")
                           .Wait();

                Console.WriteLine("✅ MongoDB connection successful!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MongoDB connection failed: {ex.Message}");
                throw;
            }
        }

        public IMongoDatabase Database => _database;

        public IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            return _database.GetCollection<T>(collectionName);
        }
    }
}
