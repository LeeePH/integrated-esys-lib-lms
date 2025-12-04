using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IAuditLogService
    {
        // Create audit log with enhanced parameters
        Task CreateAuditLogAsync(string userId, string username, string userRole, string action, string details, 
            string entityType = null, string entityId = null, string actionCategory = "", string entityName = "", 
            string ipAddress = "", string userAgent = "", string sessionId = "", string oldValues = "", 
            string newValues = "", bool success = true, string errorMessage = "", long durationMs = 0);

        // Get all audit logs with pagination
        Task<List<AuditLog>> GetAllAuditLogsAsync(int skip = 0, int limit = 100);

        // Get audit logs by user
        Task<List<AuditLog>> GetAuditLogsByUserAsync(string userId);

        // Get audit logs by date range
        Task<List<AuditLog>> GetAuditLogsByDateRangeAsync(DateTime startDate, DateTime endDate);

        // Get audit logs by action type
        Task<List<AuditLog>> GetAuditLogsByActionAsync(string action);

        // Get audit logs by action category
        Task<List<AuditLog>> GetAuditLogsByCategoryAsync(string category);

        // Get audit logs by user role
        Task<List<AuditLog>> GetAuditLogsByUserRoleAsync(string userRole);

        // Get audit logs by entity type
        Task<List<AuditLog>> GetAuditLogsByEntityTypeAsync(string entityType);

        // Advanced filtering
        Task<List<AuditLog>> GetFilteredAuditLogsAsync(AuditLogFilter filter);

        // Get audit logs with search
        Task<List<AuditLog>> SearchAuditLogsAsync(string searchTerm, int skip = 0, int limit = 100);

        // Get total count
        Task<long> GetTotalCountAsync();

        // Get count by filter
        Task<long> GetFilteredCountAsync(AuditLogFilter filter);

        // Get audit statistics
        Task<AuditLogStatistics> GetAuditStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    }

    public class AuditLogFilter
    {
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? UserRole { get; set; }
        public string? Action { get; set; }
        public string? ActionCategory { get; set; }
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool? Success { get; set; }
        public string? SearchTerm { get; set; }
        public string? SuccessFilter { get; set; } // For frontend compatibility
        public int Skip { get; set; } = 0;
        public int Limit { get; set; } = 100;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public bool Export { get; set; } = false;
    }

    public class AuditLogStatistics
    {
        public long TotalLogs { get; set; }
        public long TodayLogs { get; set; }
        public long ThisWeekLogs { get; set; }
        public long ThisMonthLogs { get; set; }
        public Dictionary<string, long> LogsByCategory { get; set; } = new();
        public Dictionary<string, long> LogsByUserRole { get; set; } = new();
        public Dictionary<string, long> LogsByAction { get; set; } = new();
        public List<string> TopUsers { get; set; } = new();
        public List<string> RecentActions { get; set; } = new();
    }
}