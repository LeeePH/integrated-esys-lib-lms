using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class DuplicateAccountDetectionService : IDuplicateAccountDetectionService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<StudentProfile> _studentProfiles;
        private readonly IMongoCollection<StaffMOCKData> _staffData;
        private readonly IMongoCollection<DuplicateAccountConflict> _conflicts;
        private readonly INotificationService _notificationService;

        public DuplicateAccountDetectionService(
            IMongoDbService mongoDbService,
            INotificationService notificationService)
        {
            _users = mongoDbService.GetCollection<User>("Users");
            _studentProfiles = mongoDbService.GetCollection<StudentProfile>("StudentProfiles");
            _staffData = mongoDbService.GetCollection<StaffMOCKData>("StaffMOCKData");
            _conflicts = mongoDbService.GetCollection<DuplicateAccountConflict>("DuplicateAccountConflicts");
            _notificationService = notificationService;
        }

        public async Task<List<DuplicateAccountConflict>> DetectDuplicateAccountsAsync()
        {
            var conflicts = new List<DuplicateAccountConflict>();
            var processedStudentNumbers = new HashSet<string>();

            // Get all student profiles
            var allProfiles = await _studentProfiles.Find(_ => true).ToListAsync();

            foreach (var profile in allProfiles)
            {
                if (string.IsNullOrWhiteSpace(profile.StudentNumber) || processedStudentNumbers.Contains(profile.StudentNumber))
                    continue;

                // Find all profiles with the same student number
                var duplicates = allProfiles
                    .Where(p => p.StudentNumber == profile.StudentNumber && p._id != profile._id)
                    .ToList();

                if (duplicates.Any())
                {
                    var duplicateUserIds = duplicates.Select(d => d.UserId).ToList();
                    duplicateUserIds.Add(profile.UserId);

                    // Check if conflict already exists
                    var existingConflict = await _conflicts.Find(c =>
                        c.ConflictType == "DuplicateStudent" &&
                        c.StudentNumber == profile.StudentNumber &&
                        !c.IsResolved
                    ).FirstOrDefaultAsync();

                    if (existingConflict == null)
                    {
                        var user = await _users.Find(u => u._id == profile.UserId).FirstOrDefaultAsync();
                        var conflict = new DuplicateAccountConflict
                        {
                            _id = ObjectId.GenerateNewId(),
                            ConflictType = "DuplicateStudent",
                            UserId = profile.UserId,
                            StudentNumber = profile.StudentNumber,
                            FullName = user?.FullName ?? profile.FullName ?? "Unknown",
                            Email = user?.Email ?? string.Empty,
                            DuplicateUserIds = duplicateUserIds,
                            ConflictDetails = $"Found {duplicateUserIds.Count} accounts with student number {profile.StudentNumber}",
                            DetectedAt = DateTime.UtcNow,
                            IsResolved = false
                        };

                        await _conflicts.InsertOneAsync(conflict);
                        conflicts.Add(conflict);

                        // Notify admins
                        await NotifyAdminsAboutConflictAsync(conflict);
                    }

                    processedStudentNumbers.Add(profile.StudentNumber);
                }
            }

            return conflicts;
        }

        public async Task<List<DuplicateAccountConflict>> DetectStaffAsStudentAsync()
        {
            var conflicts = new List<DuplicateAccountConflict>();

            // Get all staff
            var allStaff = await _staffData.Find(_ => true).ToListAsync();

            foreach (var staff in allStaff)
            {
                // Check if staff email exists as a student user
                var studentUser = await _users.Find(u =>
                    u.Email == staff.Email &&
                    u.Role == "student"
                ).FirstOrDefaultAsync();

                if (studentUser != null)
                {
                    var studentProfile = await _studentProfiles.Find(sp => sp.UserId == studentUser._id).FirstOrDefaultAsync();

                    // Check if conflict already exists
                    var existingConflict = await _conflicts.Find(c =>
                        c.ConflictType == "StaffAsStudent" &&
                        c.Email == staff.Email &&
                        !c.IsResolved
                    ).FirstOrDefaultAsync();

                    if (existingConflict == null)
                    {
                        var conflict = new DuplicateAccountConflict
                        {
                            _id = ObjectId.GenerateNewId(),
                            ConflictType = "StaffAsStudent",
                            UserId = studentUser._id,
                            StudentNumber = studentProfile?.StudentNumber ?? "N/A",
                            FullName = studentUser.FullName ?? staff.FullName,
                            Email = staff.Email,
                            EmployeeId = staff.EmployeeId,
                            ConflictDetails = $"Staff member {staff.FullName} (Employee ID: {staff.EmployeeId}) is enrolled as a student with email {staff.Email}",
                            DetectedAt = DateTime.UtcNow,
                            IsResolved = false
                        };

                        await _conflicts.InsertOneAsync(conflict);
                        conflicts.Add(conflict);

                        // Notify admins
                        await NotifyAdminsAboutConflictAsync(conflict);
                    }
                }
            }

            return conflicts;
        }

        public async Task<List<DuplicateAccountConflict>> GetAllConflictsAsync(bool includeResolved = false)
        {
            var filter = includeResolved
                ? Builders<DuplicateAccountConflict>.Filter.Empty
                : Builders<DuplicateAccountConflict>.Filter.Eq(c => c.IsResolved, false);

            return await _conflicts.Find(filter)
                .SortByDescending(c => c.DetectedAt)
                .ToListAsync();
        }

        public async Task<bool> ResolveConflictAsync(string conflictId, string adminId, string resolutionNotes)
        {
            if (!ObjectId.TryParse(conflictId, out var conflictObjectId) ||
                !ObjectId.TryParse(adminId, out var adminObjectId))
                return false;

            var update = Builders<DuplicateAccountConflict>.Update
                .Set(c => c.IsResolved, true)
                .Set(c => c.ResolvedAt, DateTime.UtcNow)
                .Set(c => c.ResolvedBy, adminObjectId)
                .Set(c => c.ResolutionNotes, resolutionNotes);

            var result = await _conflicts.UpdateOneAsync(
                c => c._id == conflictObjectId,
                update
            );

            return result.ModifiedCount > 0;
        }

        public async Task<int> RunDetectionAsync()
        {
            Console.WriteLine("🔍 [DUPLICATE DETECTION] Starting duplicate account detection...");
            
            var duplicateConflicts = await DetectDuplicateAccountsAsync();
            var staffAsStudentConflicts = await DetectStaffAsStudentAsync();

            var totalConflicts = duplicateConflicts.Count + staffAsStudentConflicts.Count;
            Console.WriteLine($"✅ [DUPLICATE DETECTION] Detection complete: {duplicateConflicts.Count} duplicate students, {staffAsStudentConflicts.Count} staff-as-student conflicts");

            return totalConflicts;
        }

        private async Task NotifyAdminsAboutConflictAsync(DuplicateAccountConflict conflict)
        {
            try
            {
                var adminUsers = await _users.Find(u => u.Role == "admin").ToListAsync();
                
                foreach (var admin in adminUsers)
                {
                    var notification = new Notification
                    {
                        UserId = admin._id.ToString(),
                        Type = "DUPLICATE_ACCOUNT_DETECTED",
                        Title = "Duplicate Account Detected",
                        Message = $"{conflict.ConflictType}: {conflict.FullName} ({conflict.StudentNumber}) - {conflict.ConflictDetails}",
                        BookTitle = string.Empty,
                        ReservationId = string.Empty
                    };

                    await _notificationService.CreateNotificationAsync(notification);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DuplicateAccountDetectionService] Error notifying admins: {ex.Message}");
            }
        }
    }
}

