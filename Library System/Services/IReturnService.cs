using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IReturnService
    {
        Task<bool> ProcessReturnAsync(ReturnTransaction returnTransaction);
        Task<List<ReturnTransaction>> GetAllReturnsAsync();
        Task<ReturnTransaction?> GetReturnByIdAsync(string returnId);
        Task<ReturnTransaction?> SearchReturnAsync(string searchTerm);
        Task<List<ReturnTransaction>> GetUserReturnsAsync(string userId);
        Task<bool> UpdatePaymentStatusAsync(string returnId, string status);
    }


}