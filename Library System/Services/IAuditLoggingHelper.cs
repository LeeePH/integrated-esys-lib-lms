using System.Security.Claims;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IAuditLoggingHelper
    {
        Task LogBookActionAsync(string action, string bookId, string bookTitle, string details, bool success = true, string errorMessage = "");
        Task LogUserActionAsync(string action, string userId, string username, string details, bool success = true, string errorMessage = "");
        Task LogBorrowingActionAsync(string action, string reservationId, string bookTitle, string studentName, string details, bool success = true, string errorMessage = "");
        Task LogSystemActionAsync(string action, string details, bool success = true, string errorMessage = "");
        Task LogSecurityActionAsync(string action, string details, bool success = true, string errorMessage = "");
        Task LogReportActionAsync(string action, string reportType, string details, bool success = true, string errorMessage = "");
        Task LogPenaltyActionAsync(string action, string penaltyId, string studentName, string bookTitle, string details, bool success = true, string errorMessage = "");
    }

    public class AuditLoggingHelper : IAuditLoggingHelper
    {
        private readonly IAuditLogService _auditLogService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLoggingHelper(IAuditLogService auditLogService, IHttpContextAccessor httpContextAccessor)
        {
            _auditLogService = auditLogService;
            _httpContextAccessor = httpContextAccessor;
        }

        private async Task LogActionAsync(string action, string actionCategory, string details, 
            string entityType = null, string entityId = null, string entityName = "", 
            bool success = true, string errorMessage = "")
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var user = httpContext?.User;

                Console.WriteLine($"ðŸ” AUDIT CONTEXT: HttpContext exists: {httpContext != null}");
                Console.WriteLine($"ðŸ” AUDIT CONTEXT: User exists: {user != null}");
                Console.WriteLine($"ðŸ” AUDIT CONTEXT: User Identity: {user?.Identity?.Name ?? "NULL"}");
                Console.WriteLine($"ðŸ” AUDIT CONTEXT: Is Authenticated: {user?.Identity?.IsAuthenticated ?? false}");

                var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                            user?.FindFirst("UserId")?.Value ?? 
                            user?.FindFirst("sub")?.Value ?? "Unknown";

                var username = user?.FindFirst(ClaimTypes.Name)?.Value ?? 
                              user?.FindFirst("username")?.Value ?? 
                              user?.Identity?.Name ?? "Unknown";

                var userRole = user?.FindFirst(ClaimTypes.Role)?.Value ?? 
                              user?.FindFirst("role")?.Value ?? "Unknown";
                              
                Console.WriteLine($"ðŸ” AUDIT CONTEXT: Extracted - UserId: {userId}, Username: {username}, Role: {userRole}");

                var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "";
                
                // Try to get session ID, but don't fail if sessions aren't configured
                string sessionId = "";
                try
                {
                    sessionId = httpContext?.Session?.Id ?? "";
                }
                catch (Exception)
                {
                    // Sessions not configured, use empty string
                    sessionId = "";
                }

                Console.WriteLine($"ðŸ” AUDIT LOG: Creating audit log - User: {username} ({userRole}), Action: {action}, Category: {actionCategory}");
                
                await _auditLogService.CreateAuditLogAsync(
                    userId: userId,
                    username: username,
                    userRole: userRole,
                    action: action,
                    details: details,
                    entityType: entityType,
                    entityId: entityId,
                    actionCategory: actionCategory,
                    entityName: entityName,
                    ipAddress: ipAddress,
                    userAgent: userAgent,
                    sessionId: sessionId,
                    success: success,
                    errorMessage: errorMessage
                );
                
                Console.WriteLine($"âœ… AUDIT LOG: Successfully created audit log - User: {username}, Action: {action}");
            }
            catch (Exception ex)
            {
                // Log the error but don't throw to avoid breaking the main operation
                Console.WriteLine($"Error creating audit log: {ex.Message}");
            }
        }

        public async Task LogBookActionAsync(string action, string bookId, string bookTitle, string details, bool success = true, string errorMessage = "")
        {
            Console.WriteLine($"ðŸ” AUDIT LOG: LogBookActionAsync called - Action: {action}, Book: {bookTitle}, Success: {success}");
            
            await LogActionAsync(
                action: action,
                actionCategory: "BOOK_MANAGEMENT",
                details: details,
                entityType: "Book",
                entityId: bookId,
                entityName: bookTitle,
                success: success,
                errorMessage: errorMessage
            );
            
            Console.WriteLine($"âœ… AUDIT LOG: LogBookActionAsync completed - Action: {action}, Book: {bookTitle}");
        }

        public async Task LogUserActionAsync(string action, string userId, string username, string details, bool success = true, string errorMessage = "")
        {
            await LogActionAsync(
                action: action,
                actionCategory: "USER_MANAGEMENT",
                details: details,
                entityType: "User",
                entityId: userId,
                entityName: username,
                success: success,
                errorMessage: errorMessage
            );
        }

        public async Task LogBorrowingActionAsync(string action, string reservationId, string bookTitle, string studentName, string details, bool success = true, string errorMessage = "")
        {
            await LogActionAsync(
                action: action,
                actionCategory: "BORROWING",
                details: details,
                entityType: "Reservation",
                entityId: reservationId,
                entityName: $"{bookTitle} - {studentName}",
                success: success,
                errorMessage: errorMessage
            );
        }

        public async Task LogSystemActionAsync(string action, string details, bool success = true, string errorMessage = "")
        {
            await LogActionAsync(
                action: action,
                actionCategory: "SYSTEM",
                details: details,
                success: success,
                errorMessage: errorMessage
            );
        }

        public async Task LogSecurityActionAsync(string action, string details, bool success = true, string errorMessage = "")
        {
            await LogActionAsync(
                action: action,
                actionCategory: "SECURITY",
                details: details,
                success: success,
                errorMessage: errorMessage
            );
        }

        public async Task LogReportActionAsync(string action, string reportType, string details, bool success = true, string errorMessage = "")
        {
            await LogActionAsync(
                action: action,
                actionCategory: "REPORT",
                details: $"{reportType}: {details}",
                success: success,
                errorMessage: errorMessage
            );
        }

        public async Task LogPenaltyActionAsync(string action, string penaltyId, string studentName, string bookTitle, string details, bool success = true, string errorMessage = "")
        {
            await LogActionAsync(
                action: action,
                actionCategory: "PENALTY",
                details: details,
                entityType: "Penalty",
                entityId: penaltyId,
                entityName: $"{bookTitle} - {studentName}",
                success: success,
                errorMessage: errorMessage
            );
        }
    }
}
