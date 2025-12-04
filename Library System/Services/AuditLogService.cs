using MongoDB.Driver;
using MongoDB.Bson;
using System.Security.AccessControl;
using SystemLibrary.Models;
using System.Text.RegularExpressions;

namespace SystemLibrary.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IMongoCollection<AuditLog> _auditLogs;

        public AuditLogService(IMongoDbService mongoDbService)
        {
            _auditLogs = mongoDbService.GetCollection<AuditLog>("AuditLogs");
        }

        public async Task CreateAuditLogAsync(string userId, string username, string userRole, string action, string details, 
            string entityType = null, string entityId = null, string actionCategory = "", string entityName = "", 
            string ipAddress = "", string userAgent = "", string sessionId = "", string oldValues = "", 
            string newValues = "", bool success = true, string errorMessage = "", long durationMs = 0)
        {
            Console.WriteLine($"🔍 AUDIT SERVICE: Creating audit log - User: {username} ({userRole}), Action: {action}");
            
            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                UserRole = userRole,
                Action = action,
                ActionCategory = actionCategory,
                Details = details,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                SessionId = sessionId,
                OldValues = oldValues,
                NewValues = newValues,
                Success = success,
                ErrorMessage = errorMessage,
                DurationMs = durationMs,
                Timestamp = DateTime.UtcNow
            };

            await _auditLogs.InsertOneAsync(auditLog);
            
            Console.WriteLine($"✅ AUDIT SERVICE: Successfully saved audit log to MongoDB - User: {username}, Action: {action}, ID: {auditLog._id}");
        }

        public async Task<List<AuditLog>> GetAllAuditLogsAsync(int skip = 0, int limit = 100)
        {
            return await _auditLogs
                .Find(_ => true)
                .SortByDescending(a => a.Timestamp)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAuditLogsByUserAsync(string userId)
        {
            return await _auditLogs
                .Find(a => a.UserId == userId)
                .SortByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAuditLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _auditLogs
                .Find(a => a.Timestamp >= startDate && a.Timestamp <= endDate)
                .SortByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAuditLogsByActionAsync(string action)
        {
            return await _auditLogs
                .Find(a => a.Action == action)
                .SortByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAuditLogsByCategoryAsync(string category)
        {
            return await _auditLogs
                .Find(a => a.ActionCategory == category)
                .SortByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAuditLogsByUserRoleAsync(string userRole)
        {
            return await _auditLogs
                .Find(a => a.UserRole == userRole)
                .SortByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAuditLogsByEntityTypeAsync(string entityType)
        {
            return await _auditLogs
                .Find(a => a.EntityType == entityType)
                .SortByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetFilteredAuditLogsAsync(AuditLogFilter filter)
        {
            var filterBuilder = Builders<AuditLog>.Filter;
            var filters = new List<FilterDefinition<AuditLog>>();

            if (!string.IsNullOrEmpty(filter.UserId))
                filters.Add(filterBuilder.Eq(a => a.UserId, filter.UserId));

            if (!string.IsNullOrEmpty(filter.Username))
                filters.Add(filterBuilder.Regex(a => a.Username, new BsonRegularExpression(filter.Username, "i")));

            if (!string.IsNullOrEmpty(filter.UserRole))
                filters.Add(filterBuilder.Eq(a => a.UserRole, filter.UserRole));

            if (!string.IsNullOrEmpty(filter.Action))
                filters.Add(filterBuilder.Regex(a => a.Action, new BsonRegularExpression(filter.Action, "i")));

            if (!string.IsNullOrEmpty(filter.ActionCategory))
                filters.Add(filterBuilder.Eq(a => a.ActionCategory, filter.ActionCategory));

            if (!string.IsNullOrEmpty(filter.EntityType))
                filters.Add(filterBuilder.Eq(a => a.EntityType, filter.EntityType));

            if (!string.IsNullOrEmpty(filter.EntityId))
                filters.Add(filterBuilder.Eq(a => a.EntityId, filter.EntityId));

            if (filter.StartDate.HasValue)
                filters.Add(filterBuilder.Gte(a => a.Timestamp, filter.StartDate.Value));

            if (filter.EndDate.HasValue)
                filters.Add(filterBuilder.Lte(a => a.Timestamp, filter.EndDate.Value));

            if (filter.Success.HasValue)
                filters.Add(filterBuilder.Eq(a => a.Success, filter.Success.Value));

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                var searchRegex = new BsonRegularExpression(Regex.Escape(filter.SearchTerm), "i");
                filters.Add(filterBuilder.Or(
                    filterBuilder.Regex(a => a.Action, searchRegex),
                    filterBuilder.Regex(a => a.Details, searchRegex),
                    filterBuilder.Regex(a => a.Username, searchRegex),
                    filterBuilder.Regex(a => a.EntityName, searchRegex)
                ));
            }

            var finalFilter = filters.Any() ? filterBuilder.And(filters) : filterBuilder.Empty;

            return await _auditLogs
                .Find(finalFilter)
                .SortByDescending(a => a.Timestamp)
                .Skip(filter.Skip)
                .Limit(filter.Limit)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> SearchAuditLogsAsync(string searchTerm, int skip = 0, int limit = 100)
        {
            var searchRegex = new BsonRegularExpression(Regex.Escape(searchTerm), "i");
            var filter = Builders<AuditLog>.Filter.Or(
                Builders<AuditLog>.Filter.Regex(a => a.Action, searchRegex),
                Builders<AuditLog>.Filter.Regex(a => a.Details, searchRegex),
                Builders<AuditLog>.Filter.Regex(a => a.Username, searchRegex),
                Builders<AuditLog>.Filter.Regex(a => a.EntityName, searchRegex)
            );

            return await _auditLogs
                .Find(filter)
                .SortByDescending(a => a.Timestamp)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<long> GetTotalCountAsync()
        {
            return await _auditLogs.CountDocumentsAsync(_ => true);
        }

        public async Task<long> GetFilteredCountAsync(AuditLogFilter filter)
        {
            var filterBuilder = Builders<AuditLog>.Filter;
            var filters = new List<FilterDefinition<AuditLog>>();

            if (!string.IsNullOrEmpty(filter.UserId))
                filters.Add(filterBuilder.Eq(a => a.UserId, filter.UserId));

            if (!string.IsNullOrEmpty(filter.Username))
                filters.Add(filterBuilder.Regex(a => a.Username, new BsonRegularExpression(filter.Username, "i")));

            if (!string.IsNullOrEmpty(filter.UserRole))
                filters.Add(filterBuilder.Eq(a => a.UserRole, filter.UserRole));

            if (!string.IsNullOrEmpty(filter.Action))
                filters.Add(filterBuilder.Regex(a => a.Action, new BsonRegularExpression(filter.Action, "i")));

            if (!string.IsNullOrEmpty(filter.ActionCategory))
                filters.Add(filterBuilder.Eq(a => a.ActionCategory, filter.ActionCategory));

            if (!string.IsNullOrEmpty(filter.EntityType))
                filters.Add(filterBuilder.Eq(a => a.EntityType, filter.EntityType));

            if (!string.IsNullOrEmpty(filter.EntityId))
                filters.Add(filterBuilder.Eq(a => a.EntityId, filter.EntityId));

            if (filter.StartDate.HasValue)
                filters.Add(filterBuilder.Gte(a => a.Timestamp, filter.StartDate.Value));

            if (filter.EndDate.HasValue)
                filters.Add(filterBuilder.Lte(a => a.Timestamp, filter.EndDate.Value));

            if (filter.Success.HasValue)
                filters.Add(filterBuilder.Eq(a => a.Success, filter.Success.Value));

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                var searchRegex = new BsonRegularExpression(Regex.Escape(filter.SearchTerm), "i");
                filters.Add(filterBuilder.Or(
                    filterBuilder.Regex(a => a.Action, searchRegex),
                    filterBuilder.Regex(a => a.Details, searchRegex),
                    filterBuilder.Regex(a => a.Username, searchRegex),
                    filterBuilder.Regex(a => a.EntityName, searchRegex)
                ));
            }

            var finalFilter = filters.Any() ? filterBuilder.And(filters) : filterBuilder.Empty;

            return await _auditLogs.CountDocumentsAsync(finalFilter);
        }

        public async Task<AuditLogStatistics> GetAuditStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var filterBuilder = Builders<AuditLog>.Filter;
            var baseFilter = filterBuilder.Empty;

            if (startDate.HasValue && endDate.HasValue)
            {
                baseFilter = filterBuilder.And(
                    filterBuilder.Gte(a => a.Timestamp, startDate.Value),
                    filterBuilder.Lte(a => a.Timestamp, endDate.Value)
                );
            }

            var stats = new AuditLogStatistics
            {
                TotalLogs = await _auditLogs.CountDocumentsAsync(baseFilter),
                TodayLogs = await _auditLogs.CountDocumentsAsync(filterBuilder.And(baseFilter, filterBuilder.Gte(a => a.Timestamp, today))),
                ThisWeekLogs = await _auditLogs.CountDocumentsAsync(filterBuilder.And(baseFilter, filterBuilder.Gte(a => a.Timestamp, weekStart))),
                ThisMonthLogs = await _auditLogs.CountDocumentsAsync(filterBuilder.And(baseFilter, filterBuilder.Gte(a => a.Timestamp, monthStart)))
            };

            // Get logs by category using simple queries
            var allLogs = await _auditLogs.Find(baseFilter).ToListAsync();
            
            // Group by category
            var categoryGroups = allLogs.GroupBy(log => log.ActionCategory ?? "Unknown")
                .ToDictionary(g => g.Key, g => (long)g.Count());
            stats.LogsByCategory = categoryGroups;

            // Group by user role
            var roleGroups = allLogs.GroupBy(log => log.UserRole ?? "Unknown")
                .ToDictionary(g => g.Key, g => (long)g.Count());
            stats.LogsByUserRole = roleGroups;

            // Get top users
            var userGroups = allLogs.GroupBy(log => log.Username ?? "Unknown")
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();
            stats.TopUsers = userGroups;

            // Get recent actions
            var recentActions = allLogs
                .OrderByDescending(log => log.Timestamp)
                .Take(10)
                .Select(log => log.Action)
                .Distinct()
                .ToList();
            stats.RecentActions = recentActions;

            return stats;
        }
    }
}