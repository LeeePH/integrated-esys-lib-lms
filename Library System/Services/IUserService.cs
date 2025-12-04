using SystemLibrary.Models;
using SystemLibrary.ViewModels;

namespace SystemLibrary.Services
{
    public interface IUserService
    {
        Task<User> AuthenticateAsync(LoginViewModel loginModel);
        Task<User?> GetUserByEmailAsync(string email);
        Task<List<User>> GetAllUsersAsync();
        Task<User> GetUserByIdAsync(string id);
        Task<StudentProfile> GetStudentProfileAsync(string userId);
        Task<StudentAccountViewModel> GetStudentAccountAsync(string userId);
        Task<bool> UpdatePasswordAsync(string userId, string newPassword);
        Task<User> GetUserByUsernameAsync(string username);
        Task UpdateUserAsync(User user);
        Task<bool> AddPenaltyToStudentAsync(string userId, decimal penaltyAmount, string bookTitle, string condition, int daysLate);
        Task<List<PenaltyRecord>> GetStudentPenaltyHistoryAsync(string userId);
        Task<decimal> GetStudentTotalPenaltiesAsync(string userId);

        Task<User> GetUserByUsernameAndEmailAsync(string username, string email);
        Task<string> CreatePasswordResetTokenAsync(string userId);
        Task<PasswordResetToken> ValidateResetTokenAsync(string token);
        Task<bool> ResetPasswordWithTokenAsync(string token, string newPassword);
        Task<bool> RestrictUserAsync(string userId, string reason);
        Task<bool> UnrestrictUserAsync(string userId);
        Task<List<User>> GetAllStudentsAsync();
    }
}