using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class UnrestrictRequestService : IUnrestrictRequestService
    {
        private readonly IMongoCollection<UnrestrictRequest> _requests;

        public UnrestrictRequestService(IMongoDbService mongoDbService)
        {
            _requests = mongoDbService.GetCollection<UnrestrictRequest>("UnrestrictRequests");
        }

        public async Task<bool> CreateRequestAsync(UnrestrictRequest request)
        {
            try
            {
                await _requests.InsertOneAsync(request);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<UnrestrictRequest>> GetPendingRequestsAsync()
        {
            return await _requests
                .Find(r => r.Status == "Pending")
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<UnrestrictRequest>> GetAllRequestsAsync()
        {
            return await _requests
                .Find(_ => true)
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<UnrestrictRequest> GetRequestByIdAsync(string requestId)
        {
            try
            {
                return await _requests
                    .Find(r => r._id == requestId)
                    .FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ProcessRequestAsync(string requestId, string adminId, string adminName, string status, string adminNotes = null)
        {
            try
            {
                var filter = Builders<UnrestrictRequest>.Filter.Eq(r => r._id, requestId);
                var update = Builders<UnrestrictRequest>.Update
                    .Set(r => r.Status, status)
                    .Set(r => r.ProcessedBy, adminId)
                    .Set(r => r.ProcessedByName, adminName)
                    .Set(r => r.ProcessedAt, DateTime.UtcNow)
                    .Set(r => r.UpdatedAt, DateTime.UtcNow);

                if (!string.IsNullOrEmpty(adminNotes))
                {
                    update = update.Set(r => r.AdminNotes, adminNotes);
                }

                var result = await _requests.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteRequestAsync(string requestId)
        {
            try
            {
                var result = await _requests.DeleteOneAsync(r => r._id == requestId);
                return result.DeletedCount > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
