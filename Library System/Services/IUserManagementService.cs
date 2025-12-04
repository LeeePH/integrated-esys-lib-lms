using SystemLibrary.Models;
using SystemLibrary.ViewModels;

namespace SystemLibrary.Services
{
    public interface IUserManagementService
    {
        // Get users by role
        Task<List<User>> GetLibraryStaffAsync();
        Task<List<User>> GetStudentsAsync();

        // Add new user
        Task<bool> AddUserAsync(CreateUserViewModel model);

        // MOCK data methods removed - now using enrollment system integration
        // Students are automatically synced from enrollment system during login

        // Update password
        Task<bool> UpdatePasswordAsync(string userId, string oldPassword, string newPassword);

        // Deactivate/Activate user (for staff)
        Task<bool> DeactivateUserAsync(string userId);
        Task<bool> ActivateUserAsync(string userId);

        // Restrict/Unrestrict user (for students)
        Task<bool> RestrictUserAsync(string userId);
        Task<bool> UnrestrictUserAsync(string userId);

        // Delete user
        Task<bool> DeleteUserAsync(string userId);

        // Get user by ID
        Task<User> GetUserByIdAsync(string userId);

        // Check if user can be deleted (no active reservations)
        Task<bool> CanDeleteUserAsync(string userId);
        Task<bool> AdminResetPasswordAsync(string userId, string newPassword);

    }
}

