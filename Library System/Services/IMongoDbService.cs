using MongoDB.Driver;

namespace SystemLibrary.Services
{
    public interface IMongoDbService
    {
        IMongoDatabase Database { get; }
        IMongoCollection<T> GetCollection<T>(string collectionName);
    }
}