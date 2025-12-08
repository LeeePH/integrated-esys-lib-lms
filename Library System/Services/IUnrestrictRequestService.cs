using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IUnrestrictRequestService
    {
        Task<bool> CreateRequestAsync(UnrestrictRequest request);
        Task<List<UnrestrictRequest>> GetPendingRequestsAsync();
        Task<List<UnrestrictRequest>> GetAllRequestsAsync();
        Task<UnrestrictRequest> GetRequestByIdAsync(string requestId);
        Task<bool> ProcessRequestAsync(string requestId, string adminId, string adminName, string status, string adminNotes = null);
        Task<bool> DeleteRequestAsync(string requestId);
    }
}
