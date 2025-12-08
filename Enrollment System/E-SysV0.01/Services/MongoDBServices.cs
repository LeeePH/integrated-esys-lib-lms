using E_SysV0._01.Models;
using E_SysV0._01.Models.BSITSubjectModels._1stYear;
using EnrollmentSystem.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace E_SysV0._01.Services
{
    public class MongoDBServices
    {
        private readonly MongoClient _client;
        private readonly IMongoDatabase _database;
        private readonly IConfiguration _config;

        private readonly IMongoCollection<EnrollmentRequest> _enrollments;
        private readonly IMongoCollection<EnrollmentArchive> _enrollmentArchives;
        private readonly IMongoCollection<Student> _students;
        private readonly IMongoCollection<Counter> _counters;

        private readonly IMongoCollection<Room> _rooms;
        private readonly IMongoCollection<CourseSection> _sections;
        private readonly IMongoCollection<ClassMeeting> _meetings;
        private readonly IMongoCollection<StudentSectionEnrollment> _studentSectionEnrollments;
        private readonly IMongoCollection<EnrollmentSettings> _enrollmentSettings;

        private readonly IMongoCollection<Announcement> _announcements;

        private readonly IMongoCollection<ShifterEnrollmentRequest> _shifterEnrollments;
        private readonly IMongoCollection<StudentSubjectRemarks> _studentSubjectRemarks;

        private static readonly string[] CountedEnrolledStatuses = new[] { "Enrolled", "Enrolled - Regular", "Enrolled - Irregular" };

        // =========================================================
        // ✅ Newly Injected MongoDB Collections for Required Documents
        // =========================================================
        private readonly IMongoCollection<RequiredDocument> _documentsRequired;
        private readonly IMongoCollection<RequiredDocument> _requiredDocuments;

        public MongoDBServices(IConfiguration config)
        {
            _config = config;
            // Using ESys MongoDB Atlas connection string
            var connString = config["Mongo:ConnectionString"] ??
                "mongodb+srv://villalinojohnalwynlebadisos_db_user:THJRpJjegAhOmRJv@esys.vi1regz.mongodb.net/?retryWrites=true&w=majority&appName=ESys";
        
            var databaseName = config["Mongo:Database"] ?? "ESys";
            _client = new MongoClient(connString);
            _database = _client.GetDatabase(databaseName);

            // Original collections
            _enrollments = _database.GetCollection<EnrollmentRequest>("enrollmentRequests");
            _enrollmentArchives = _database.GetCollection<EnrollmentArchive>("enrollmentArchives");
            _students = _database.GetCollection<Student>("students");
            _counters = _database.GetCollection<Counter>("counters");

            _rooms = _database.GetCollection<Room>("rooms");
            _sections = _database.GetCollection<CourseSection>("sections");
            _meetings = _database.GetCollection<ClassMeeting>("classMeetings");
            _studentSectionEnrollments = _database.GetCollection<StudentSectionEnrollment>("studentSectionEnrollments");
            _enrollmentSettings = _database.GetCollection<EnrollmentSettings>("enrollmentSettings");

            _announcements = _database.GetCollection<Announcement>("announcements");

            // =========================================================
            // ✅ Added: Initialize Required Document Collections
            // =========================================================
            _documentsRequired = _database.GetCollection<RequiredDocument>("documents_required");
            _requiredDocuments = _database.GetCollection<RequiredDocument>("required_documents");


            _shifterEnrollments = _database.GetCollection<ShifterEnrollmentRequest>("shifterEnrollmentRequests");
            _studentSubjectRemarks = _database.GetCollection<StudentSubjectRemarks>("studentSubjectRemarks");

            try
            {
                // Optional Ping Test
                _database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                Console.WriteLine($"✅ Connected to MongoDB: {databaseName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MongoDB connection failed: {ex.Message}");
                throw;
            }

            EnsureIndexesAndCleanupAsync().GetAwaiter().GetResult();
        }



        // =========================================================
        // ✅ Public accessors for the newly added collections
        // =========================================================
        public IMongoCollection<RequiredDocument> DocumentsRequiredCollection => _documentsRequired;
        public IMongoCollection<RequiredDocument> RequiredDocumentsCollection => _requiredDocuments;


        public async Task UpsertStudentSubjectRemarkAsync(StudentSubjectRemarks remark)
        {
            if (string.IsNullOrWhiteSpace(remark.Id))
                remark.Id = $"{remark.StudentUsername}:{remark.SubjectCode}";

            remark.UpdatedAt = DateTime.UtcNow;

            var filter = Builders<StudentSubjectRemarks>.Filter.Eq(r => r.Id, remark.Id);
            await _studentSubjectRemarks.ReplaceOneAsync(filter, remark, new ReplaceOptions { IsUpsert = true });
        }
        /// <summary>
        /// Get all subject remarks for a student
        /// </summary>
        public async Task<List<StudentSubjectRemarks>> GetStudentSubjectRemarksAsync(string studentUsername)
        {
            if (string.IsNullOrWhiteSpace(studentUsername))
                return new List<StudentSubjectRemarks>();

            var collection = _database.GetCollection<StudentSubjectRemarks>("student_subject_remarks");

            var filter = Builders<StudentSubjectRemarks>.Filter.Eq(r => r.StudentUsername, studentUsername);

            return await collection.Find(filter).ToListAsync();
        }


        public async Task SubmitShifterEnrollmentRequestAsync(ShifterEnrollmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
                request.Id = Guid.NewGuid().ToString("N");

            request.SubmittedDate = DateTime.UtcNow;

            var filter = Builders<ShifterEnrollmentRequest>.Filter.Eq(r => r.Id, request.Id);
            await _shifterEnrollments.ReplaceOneAsync(filter, request, new ReplaceOptions { IsUpsert = true });
        }


        /// <summary>
        /// Get shifter request by ID
        /// </summary>
        public Task<ShifterEnrollmentRequest?> GetShifterEnrollmentRequestByIdAsync(string id)
            => _shifterEnrollments.Find(r => r.Id == id).FirstOrDefaultAsync();

        /// <summary>
        /// Get shifter request by access token
        /// </summary>
        public Task<ShifterEnrollmentRequest?> GetShifterEnrollmentRequestByTokenAsync(string token)
            => _shifterEnrollments.Find(r => r.AccessToken == token).FirstOrDefaultAsync();

        /// <summary>
        /// Get all shifter requests by status
        /// </summary>
        public async Task<List<ShifterEnrollmentRequest>> GetShifterEnrollmentRequestsByStatusAsync(string status)
        {
            return await _shifterEnrollments
                .Find(r => r.Status == status)
                .SortByDescending(r => r.SubmittedDate)
                .ToListAsync();
        }

        /// <summary>
        /// Update shifter enrollment request
        /// </summary>
        public Task UpdateShifterEnrollmentRequestAsync(ShifterEnrollmentRequest request)
        {
            var filter = Builders<ShifterEnrollmentRequest>.Filter.Eq(r => r.Id, request.Id);
            return _shifterEnrollments.ReplaceOneAsync(filter, request, new ReplaceOptions { IsUpsert = false });
        }
      
        /// <summary>
        /// Get shifter request for a student (latest)
        /// </summary>
        public Task<ShifterEnrollmentRequest?> GetLatestShifterRequestByUsernameAsync(string studentUsername)
        {
            return _shifterEnrollments
                .Find(r => r.StudentUsername == studentUsername)
                .SortByDescending(r => r.SubmittedDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<EnrollmentRequest>> SearchEnrollmentRequestsByNameAsync(string term, int limit = 10)
        {
            var t = (term ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t))
                return new List<EnrollmentRequest>();

            var escaped = Regex.Escape(t);
            var regex = new BsonRegularExpression(escaped, "i");
            var filter = Builders<EnrollmentRequest>.Filter.Regex(e => e.FullName, regex);

            return await _enrollments.Find(filter)
                                     .SortByDescending(e => e.SubmittedAt)
                                     .Limit(Math.Max(1, limit))
                                     .ToListAsync();
        }

        public Task<StudentSectionEnrollment?> GetStudentSectionEnrollmentAsync(string studentUsername)
            => string.IsNullOrWhiteSpace(studentUsername)
                ? Task.FromResult<StudentSectionEnrollment?>(null)
                : _studentSectionEnrollments.Find(e => e.StudentUsername == studentUsername).FirstOrDefaultAsync();
        public async Task<List<RequiredDocument>> GetRequiredDocumentsAsync()
        {
            try
            {
                // ✅ Use the existing _database field (correct context)
                var collection = _database.GetCollection<RequiredDocument>("documents_required");

                var docs = await collection.Find(Builders<RequiredDocument>.Filter.Empty).ToListAsync();
                return docs ?? new List<RequiredDocument>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error fetching documents_required: {ex.Message}");
                return new List<RequiredDocument>();
            }
        }

        public Task<CourseSection?> GetSectionByIdAsync(string sectionId)
            => string.IsNullOrWhiteSpace(sectionId)
                ? Task.FromResult<CourseSection?>(null)
                : _sections.Find(s => s.Id == sectionId).FirstOrDefaultAsync();

        public async Task<Dictionary<string, string>> GetRoomNamesByIdsAsync(IEnumerable<string> ids)
        {
            var list = (ids ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToArray();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (list.Length == 0) return dict;

            var rooms = await _rooms.Find(r => list.Contains(r.Id)).ToListAsync();
            foreach (var r in rooms)
                dict[r.Id] = string.IsNullOrWhiteSpace(r.Name) ? r.Id : r.Name;
            return dict;
        }

        public async Task<bool> DeleteStudentByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            var result = await _students.DeleteOneAsync(s => s.Username == username);
            return result.DeletedCount > 0;
        }

        public async Task<bool> ExistsEnrolledByNameOrEmailAsync(string? fullName, string? email)
        {
            var normEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            var name = (fullName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normEmail) && string.IsNullOrWhiteSpace(name))
                return false;

            var statusFilter = Builders<EnrollmentRequest>.Filter.In(e => e.Status, CountedEnrolledStatuses);

            if (!string.IsNullOrWhiteSpace(normEmail))
            {
                var emailFilter = Builders<EnrollmentRequest>.Filter.And(
                    statusFilter,
                    Builders<EnrollmentRequest>.Filter.Eq(e => e.Email, normEmail)
                );

                if (await _enrollments.Find(emailFilter).Limit(1).AnyAsync())
                    return true;

                if (await _students.Find(s => s.Email == normEmail).Limit(1).AnyAsync())
                    return true;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var nameFilter = Builders<EnrollmentRequest>.Filter.And(
                    statusFilter,
                    Builders<EnrollmentRequest>.Filter.Eq(e => e.FullName, name)
                );

                if (await _enrollments.Find(nameFilter).Limit(1).AnyAsync())
                    return true;

                var archiveNameFilter = Builders<EnrollmentArchive>.Filter.Eq(a => a.FullName, name);
                if (await _enrollmentArchives.Find(archiveNameFilter).Limit(1).AnyAsync())
                    return true;
            }

            return false;
        }

        public async Task<bool> RollbackSectionAssignmentAsync(string studentUsername)
        {
            if (string.IsNullOrWhiteSpace(studentUsername)) return false;

            var sse = await _studentSectionEnrollments.FindOneAndDeleteAsync(e => e.StudentUsername == studentUsername);
            if (sse is null) return false;

            await _sections.UpdateOneAsync(
                s => s.Id == sse.SectionId && s.CurrentCount > 0,
                Builders<CourseSection>.Update.Inc(s => s.CurrentCount, -1));

            return true;
        }

        public async Task ArchiveEnrollmentRequestAsync(EnrollmentRequest request, string reason)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                throw new ArgumentException("Invalid enrollment request");
            }

            try
            {
                // ✅ Use the INSTANCE FIELD, not a new collection reference
                // var archivesCollection = _database.GetCollection<EnrollmentArchive>("enrollment_archives"); // ❌ WRONG

                // Create archive record with ALL fields
                var archive = new EnrollmentArchive
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OriginalRequestId = request.Id,
                    Email = request.Email,
                    FullName = request.FullName,
                    Program = request.Program,
                    Type = request.Type,
                    Status = request.Status,
                    StatusAtArchive = request.Status,
                    Reason = request.Reason,
                    Notes = request.Notes,
                    SubmittedAt = request.SubmittedAt,
                    LastUpdatedAt = request.LastUpdatedAt ?? DateTime.UtcNow,
                    ArchivedAt = DateTime.UtcNow,
                    ArchiveReason = reason,
                    DocumentFlags = request.DocumentFlags,
                    SecondSemesterEligibility = request.SecondSemesterEligibility,
                    ExtraFields = request.ExtraFields,
                    EmergencyContactName = request.EmergencyContactName,
                    EmergencyContactPhone = request.EmergencyContactPhone
                };

                // Determine academic year from extra fields
                if (request.ExtraFields?.TryGetValue("Academic.AcademicYear", out var ay) == true)
                {
                    archive.AcademicYear = ay;
                }

                // Determine semester from extra fields
                if (request.ExtraFields?.TryGetValue("Academic.Semester", out var sem) == true)
                {
                    archive.Semester = sem;
                }

                // ✅ Use the INSTANCE FIELD
                await _enrollmentArchives.InsertOneAsync(archive);

                Console.WriteLine($"[ArchiveEnrollmentRequestAsync] Archived request {request.Id} → archive {archive.Id}");
                Console.WriteLine($"  - AcademicYear: '{archive.AcademicYear}'");
                Console.WriteLine($"  - Semester: '{archive.Semester}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ArchiveEnrollmentRequestAsync] Error: {ex.Message}");
                throw;
            }
        }

    
        public async Task CreateEnrollmentRequestAsync(EnrollmentRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Id))
                request.Id = Guid.NewGuid().ToString("N");

            request.SubmittedAt = DateTime.UtcNow;
            request.LastUpdatedAt = DateTime.UtcNow;

            await _enrollments.InsertOneAsync(request);

            Console.WriteLine($"[CreateEnrollmentRequestAsync] Created enrollment request {request.Id} for {request.Email}");
        }

        /// <summary>
        /// Delete an enrollment request by ID
        /// </summary>
        public async Task DeleteEnrollmentRequestAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Request ID cannot be empty", nameof(id));

            var filter = Builders<EnrollmentRequest>.Filter.Eq(e => e.Id, id);
            var result = await _enrollments.DeleteOneAsync(filter);

            Console.WriteLine($"[DeleteEnrollmentRequestAsync] Deleted enrollment request {id} (deleted count: {result.DeletedCount})");
        }

        /// <summary>
        /// Save or update student subject remarks (creates collection if needed)
        /// </summary>
        public async Task SaveStudentSubjectRemarksAsync(string studentUsername, Dictionary<string, string> subjectRemarks, string program, string semester, string yearLevel)
        {
            if (string.IsNullOrWhiteSpace(studentUsername) || subjectRemarks == null || !subjectRemarks.Any())
                return;

            var collection = _database.GetCollection<StudentSubjectRemarks>("student_subject_remarks");

            foreach (var remark in subjectRemarks)
            {
                var subjectCode = remark.Key;
                var remarkValue = remark.Value; // "pass", "fail", "ongoing"

                if (string.IsNullOrWhiteSpace(subjectCode) || string.IsNullOrWhiteSpace(remarkValue))
                    continue;

                // Find existing remark or create new
                var filter = Builders<StudentSubjectRemarks>.Filter.And(
                    Builders<StudentSubjectRemarks>.Filter.Eq(r => r.StudentUsername, studentUsername),
                    Builders<StudentSubjectRemarks>.Filter.Eq(r => r.SubjectCode, subjectCode),
                    Builders<StudentSubjectRemarks>.Filter.Eq(r => r.Program, program),
                    Builders<StudentSubjectRemarks>.Filter.Eq(r => r.SemesterTaken, semester)
                );

                var existing = await collection.Find(filter).FirstOrDefaultAsync();

                if (existing != null)
                {
                    // Update existing remark
                    var update = Builders<StudentSubjectRemarks>.Update
                        .Set(r => r.Remark, remarkValue)
                        .Set(r => r.YearLevelTaken, yearLevel)
                        .Set(r => r.UpdatedAt, DateTime.UtcNow);

                    await collection.UpdateOneAsync(filter, update);
                }
                else
                {
                    // Create new remark record
                    // Get subject details from program subjects
                    var subjectTitle = "";
                    var units = 0;

                    // Look up subject info from canonical models
                    try
                    {
                        if (program.Equals("BSIT", StringComparison.OrdinalIgnoreCase))
                        {
                            var allSubjects = E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects
                                .Concat(E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects)
                                .Concat(E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                                .Concat(E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects);

                            var subject = allSubjects.FirstOrDefault(s =>
                                s.Code.Equals(subjectCode, StringComparison.OrdinalIgnoreCase));

                            if (subject != null)
                            {
                                subjectTitle = subject.Title;
                                units = subject.Units;
                            }
                        }
                        else if (program.Equals("BSENT", StringComparison.OrdinalIgnoreCase))
                        {
                            var allSubjects = E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects
                                .Concat(E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects)
                                .Concat(E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                                .Concat(E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects);

                            var subject = allSubjects.FirstOrDefault(s =>
                                s.Code.Equals(subjectCode, StringComparison.OrdinalIgnoreCase));

                            if (subject != null)
                            {
                                subjectTitle = subject.Title;
                                units = subject.Units;
                            }
                        }
                    }
                    catch
                    {
                        // Fallback: use subject code as title
                        subjectTitle = subjectCode;
                        units = 3; // Default
                    }

                    var newRemark = new StudentSubjectRemarks
                    {
                        StudentUsername = studentUsername,
                        SubjectCode = subjectCode,
                        SubjectTitle = subjectTitle,
                        Units = units,
                        SemesterTaken = semester,
                        YearLevelTaken = yearLevel,
                        Program = program,
                        Remark = remarkValue,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await collection.InsertOneAsync(newRemark);
                }
            }

            Console.WriteLine($"[SaveStudentSubjectRemarksAsync] Saved {subjectRemarks.Count} remarks for {studentUsername}");
        }
        private async Task EnsureIndexesAndCleanupAsync()
        {
            var enrollmentIndexes = new List<CreateIndexModel<EnrollmentRequest>>
            {
                new CreateIndexModel<EnrollmentRequest>(Builders<EnrollmentRequest>.IndexKeys.Ascending(e => e.Status)),
                new CreateIndexModel<EnrollmentRequest>(Builders<EnrollmentRequest>.IndexKeys.Ascending(e => e.Email).Descending(e => e.SubmittedAt)),
                new CreateIndexModel<EnrollmentRequest>(Builders<EnrollmentRequest>.IndexKeys.Ascending(e => e.EditToken)),
                new CreateIndexModel<EnrollmentRequest>(
                    Builders<EnrollmentRequest>.IndexKeys.Combine(
                        Builders<EnrollmentRequest>.IndexKeys.Ascending(e => e.Type),
                        Builders<EnrollmentRequest>.IndexKeys.Ascending(e => e.FullName),
                        Builders<EnrollmentRequest>.IndexKeys.Descending(e => e.SubmittedAt)
                    ))
            };
            await _enrollments.Indexes.CreateManyAsync(enrollmentIndexes);

            try
            {
                var annIndex = new CreateIndexModel<Announcement>(Builders<Announcement>.IndexKeys.Descending(a => a.PostedAtUtc));
                await _announcements.Indexes.CreateOneAsync(annIndex);
            }
            catch { }

            var archiveIndexes = new List<CreateIndexModel<EnrollmentArchive>>
            {
                new CreateIndexModel<EnrollmentArchive>(Builders<EnrollmentArchive>.IndexKeys.Ascending(a => a.Email).Descending(a => a.ArchivedAt)),
                new CreateIndexModel<EnrollmentArchive>(Builders<EnrollmentArchive>.IndexKeys.Ascending(a => a.AcademicYear).Descending(a => a.ArchivedAt)),
                new CreateIndexModel<EnrollmentArchive>(Builders<EnrollmentArchive>.IndexKeys.Ascending(a => a.StatusAtArchive)),
                new CreateIndexModel<EnrollmentArchive>(Builders<EnrollmentArchive>.IndexKeys.Ascending(a => a.OriginalRequestId), new CreateIndexOptions { Unique = true })
            };
            await _enrollmentArchives.Indexes.CreateManyAsync(archiveIndexes);

            var studentIndexes = new List<CreateIndexModel<Student>>
            {
                new CreateIndexModel<Student>(Builders<Student>.IndexKeys.Ascending(s => s.Username), new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<Student>(Builders<Student>.IndexKeys.Ascending(s => s.Type)),
                new CreateIndexModel<Student>(Builders<Student>.IndexKeys.Ascending(s => s.Email))
            };
            await _students.Indexes.CreateManyAsync(studentIndexes);

            var roomIndexes = new List<CreateIndexModel<Room>>
            {
                new CreateIndexModel<Room>(Builders<Room>.IndexKeys.Ascending(r => r.Name), new CreateIndexOptions { Unique = true })
            };
            await _rooms.Indexes.CreateManyAsync(roomIndexes);

            var sectionIndexes = new List<CreateIndexModel<CourseSection>>
            {
                new CreateIndexModel<CourseSection>(Builders<CourseSection>.IndexKeys.Ascending(s => s.Program).Ascending(s => s.Name), new CreateIndexOptions { Unique = true })
            };
            await _sections.Indexes.CreateManyAsync(sectionIndexes);

            try { await _meetings.Indexes.DropOneAsync("uniq_instructor_day_slot"); } catch { }

            var meetingIndexes = new List<CreateIndexModel<ClassMeeting>>
            {
                new CreateIndexModel<ClassMeeting>(
                    Builders<ClassMeeting>.IndexKeys
                        .Ascending(m => m.RoomId)
                        .Ascending(m => m.DayOfWeek)
                        .Ascending(m => m.Slot),
                    new CreateIndexOptions { Unique = true, Name = "uniq_room_day_slot" }),
                new CreateIndexModel<ClassMeeting>(
                    Builders<ClassMeeting>.IndexKeys
                        .Ascending(m => m.SectionId)
                        .Ascending(m => m.DayOfWeek)
                        .Ascending(m => m.Slot),
                    new CreateIndexOptions { Unique = true, Name = "uniq_section_day_slot" }),
                new CreateIndexModel<ClassMeeting>(Builders<ClassMeeting>.IndexKeys.Ascending(m => m.SectionId))
            };

            try { await _meetings.Indexes.CreateManyAsync(meetingIndexes); }
            catch (MongoCommandException) { }

            var sseIndexes = new List<CreateIndexModel<StudentSectionEnrollment>>
            {
                new CreateIndexModel<StudentSectionEnrollment>(
                    Builders<StudentSectionEnrollment>.IndexKeys.Ascending(e => e.StudentUsername),
                    new CreateIndexOptions { Unique = true })
            };
            await _studentSectionEnrollments.Indexes.CreateManyAsync(sseIndexes);

            try
            {
                await _enrollmentSettings.Indexes.CreateOneAsync(
                    new CreateIndexModel<EnrollmentSettings>(
                        Builders<EnrollmentSettings>.IndexKeys.Ascending(s => s.Id),
                        new CreateIndexOptions { Unique = true }));
            }
            catch { }

            try { await _database.DropCollectionAsync("instructors"); } catch { }
        }
        // --- Announcement helpers (NEW) ---
        public async Task InsertAnnouncementAsync(Announcement announcement)
        {
            if (announcement == null) throw new ArgumentNullException(nameof(announcement));
            if (string.IsNullOrWhiteSpace(announcement.Id)) announcement.Id = Guid.NewGuid().ToString("N");
            announcement.PostedAtUtc = DateTime.UtcNow;
            await _announcements.InsertOneAsync(announcement);
        }

        public async Task<List<Announcement>> GetRecentAnnouncementsAsync(int take = 5)
        {
            return await _announcements.Find(Builders<Announcement>.Filter.Empty)
                                       .SortByDescending(a => a.PostedAtUtc)
                                       .Limit(Math.Max(1, take))
                                       .ToListAsync();
        }

        public async Task<Announcement?> GetLatestAnnouncementAsync()
        {
            return await _announcements.Find(Builders<Announcement>.Filter.Empty)
                                       .SortByDescending(a => a.PostedAtUtc)
                                       .Limit(1)
                                       .FirstOrDefaultAsync();
        }

        // --- Enrollment settings ---
        public async Task<EnrollmentSettings> GetEnrollmentSettingsAsync()
        {
            const string SettingsId = "enrollment-settings";

            var settings = await _enrollmentSettings
                .Find(s => s.Id == SettingsId)
                .Limit(1)
                .FirstOrDefaultAsync();

            if (settings != null)
                return settings;

            // Seed durable default when collection is empty (e.g., after drop)
            settings = new EnrollmentSettings
            {
                Id = SettingsId,
                IsOpen = false,
                Semester = "1st Semester",
                AcademicYear = "" // keep non-null
            };

            await _enrollmentSettings.ReplaceOneAsync(
                s => s.Id == SettingsId,
                settings,
                new ReplaceOptions { IsUpsert = true });

            return settings;
        }

        // NEW: lookup latest request by normalized email
        public async Task<EnrollmentRequest?> GetLatestRequestByEmailAsync(string email)
        {
            var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
            return await _enrollments.Find(e => e.Email == normalized)
                                     .SortByDescending(e => e.SubmittedAt)
                                     .Limit(1)
                                     .FirstOrDefaultAsync();
        }
        public async Task<List<EnrollmentRequest>> GetRequestsByEmailAsync(string email)
        {
            var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
            return await _enrollments.Find(e => e.Email == normalized)
                                     .SortByDescending(e => e.SubmittedAt)
                                     .ToListAsync();
        }
        // NEW: lookup latest request by FullName and optional Type
        public async Task<EnrollmentRequest?> GetLatestRequestByFullNameAsync(string fullName, string? type = null)
        {
            var normalized = (fullName ?? string.Empty).Trim();
            var filter = Builders<EnrollmentRequest>.Filter.Eq(e => e.FullName, normalized);
            if (!string.IsNullOrWhiteSpace(type))
                filter &= Builders<EnrollmentRequest>.Filter.Eq(e => e.Type, type);

            return await _enrollments.Find(filter)
                                     .SortByDescending(e => e.SubmittedAt)
                                     .Limit(1)
                                     .FirstOrDefaultAsync();
        }
        public async Task<List<EnrollmentRequest>> GetOnHoldByProgramAndReasonAsync(string program, string reason, int limit = 10)
        {
            var p = (program ?? string.Empty).Trim();
            var r = (reason ?? string.Empty).Trim();

            var filter = Builders<EnrollmentRequest>.Filter.And(
                Builders<EnrollmentRequest>.Filter.Eq(e => e.Status, "On Hold"),
                Builders<EnrollmentRequest>.Filter.Eq(e => e.Program, p),
                Builders<EnrollmentRequest>.Filter.Eq(e => e.Reason, r)
            );

            return await _enrollments.Find(filter)
                                     .SortBy(e => e.SubmittedAt)
                                     .Limit(Math.Max(1, limit))
                                     .ToListAsync();
        }



        public Task<long> CountEnrolledByProgramAsync(string program)
        {
            var p = (program ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(p)) return Task.FromResult(0L);

            var filter = Builders<EnrollmentRequest>.Filter.And(
                Builders<EnrollmentRequest>.Filter.In(e => e.Status, CountedEnrolledStatuses),
                Builders<EnrollmentRequest>.Filter.Eq(e => e.Program, p)
            );

            return _enrollments.CountDocumentsAsync(filter);
        }
        public async Task<List<EnrollmentRequest>> SearchRequestsByStatusAsync(string status, string? q, string? program, int take = 200)
        {
            var filter = Builders<EnrollmentRequest>.Filter.Eq(e => e.Status, status);

            if (!string.IsNullOrWhiteSpace(program))
            {
                var p = program.Trim();
                filter &= Builders<EnrollmentRequest>.Filter.Eq(e => e.Program, p);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                var emailNorm = t.ToLowerInvariant();
                var emailFilter = Builders<EnrollmentRequest>.Filter.Eq(e => e.Email, emailNorm);
                var nameFilter = Builders<EnrollmentRequest>.Filter.Regex(e => e.FullName,
                    new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(t), "i"));
                filter &= Builders<EnrollmentRequest>.Filter.Or(emailFilter, nameFilter);
            }

            return await _enrollments.Find(filter)
                                     .SortByDescending(e => e.SubmittedAt)
                                     .Limit(Math.Max(1, take))
                                     .ToListAsync();
        }
        public async Task<Dictionary<string, long>> GetEnrolledCountsByProgramAsync(IEnumerable<string> programs)
        {
            var list = (programs ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToArray();
            if (list.Length == 0) return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            var filter = Builders<EnrollmentRequest>.Filter.And(
                Builders<EnrollmentRequest>.Filter.In(e => e.Status, CountedEnrolledStatuses),
                Builders<EnrollmentRequest>.Filter.In(e => e.Program, list)
            );

            var results = await _enrollments.Aggregate()
                .Match(filter)
                .Group(e => e.Program, g => new { Program = g.Key, Count = g.Count() })
                .ToListAsync();

            var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in results)
                dict[r.Program ?? ""] = r.Count;

            foreach (var p in list)
                if (!dict.ContainsKey(p)) dict[p] = 0;

            return dict;
        }
        public async Task UpsertEnrollmentSettingsAsync(EnrollmentSettings settings)
        {
            settings.Id = "enrollment-settings";
            var filter = Builders<EnrollmentSettings>.Filter.Eq(s => s.Id, settings.Id);
            await _enrollmentSettings.ReplaceOneAsync(filter, settings, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<bool> CanConnectAsync()
        {
            try { _ = await _enrollments.EstimatedDocumentCountAsync(); return true; }
            catch { return false; }
        }

        public async Task SubmitEnrollmentRequestAsync(EnrollmentRequest request)
        {
            var filter = Builders<EnrollmentRequest>.Filter.Eq(e => e.Id, request.Id);
            await _enrollments.ReplaceOneAsync(filter, request, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<List<EnrollmentRequest>> GetEnrollmentRequestsByStatusAsync(string status)
        {
            return await _enrollments.Find(e => e.Status == status)
                                     .SortByDescending(e => e.SubmittedAt)
                                     .ToListAsync();
        }

        public Task<EnrollmentRequest?> GetEnrollmentRequestByIdAsync(string id)
            => _enrollments.Find(e => e.Id == id).FirstOrDefaultAsync();

        public Task UpdateEnrollmentRequestAsync(EnrollmentRequest request)
        {
            var filter = Builders<EnrollmentRequest>.Filter.Eq(e => e.Id, request.Id);
            return _enrollments.ReplaceOneAsync(filter, request, new ReplaceOptions { IsUpsert = false });
        }

        public Task<EnrollmentRequest?> GetEnrollmentRequestByEditTokenAsync(string token)
            => _enrollments.Find(e => e.EditToken == token).Limit(1).FirstOrDefaultAsync();

        public async Task CreateStudentAsync(Student student)
        {
            if (string.IsNullOrWhiteSpace(student.Id))
                student.Id = student.Username;
            await _students.InsertOneAsync(student);
        }

        public Task UpdateStudentAsync(Student student)
        {
            var filter = Builders<Student>.Filter.Eq(s => s.Username, student.Username);
            return _students.ReplaceOneAsync(filter, student, new ReplaceOptions { IsUpsert = false });
        }

        public Task<Student?> GetStudentByUsernameAsync(string username)
            => string.IsNullOrWhiteSpace(username) ? Task.FromResult<Student?>(null)
               : _students.Find(s => s.Username == username).FirstOrDefaultAsync();

        // NEW: lookup by normalized email (Account approval used normalized email too)
        public Task<Student?> GetStudentByEmailAsync(string email)
        {
            var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
            return _students.Find(s => s.Email == normalized).FirstOrDefaultAsync();
        }

        public Task<long> CountStudentsByTypeAsync(string type)
            => _students.CountDocumentsAsync(s => s.Type == type);

        public async Task<Dictionary<string, long>> GetStudentCountsByTypeAsync()
        {
            var results = await _students.Aggregate()
                .Group(s => s.Type, g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            return results.ToDictionary(x => string.IsNullOrWhiteSpace(x.Type) ? "Unknown" : x.Type, x => (long)x.Count);
        }

        public async Task<string> GenerateNextStudentUsernameAsync(int year)
        {
            var id = $"student-{year}";
            var filter = Builders<Counter>.Filter.Eq(c => c.Id, id);
            var update = Builders<Counter>.Update
                .Inc(c => c.Seq, 1)
                .SetOnInsert(c => c.Id, id);

            var options = new FindOneAndUpdateOptions<Counter> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            var counter = await _counters.FindOneAndUpdateAsync(filter, update, options);
            return $"{year}-{counter.Seq:0000}";
        }

        private class Counter
        {
            [BsonId]
            [BsonRepresentation(BsonType.String)]
            public string Id { get; set; } = string.Empty;
            public int Seq { get; set; }
        }

        // ===== ARCHIVE HELPERS =====

        public async Task<int> ArchiveFirstSemEnrollmentForStudentAsync(
            string email,
            string academicYear,
            string reason = "Superseded by 2nd Semester Enrollment")
        {
            var norm = (email ?? string.Empty).Trim().ToLowerInvariant();
            var ay = (academicYear ?? string.Empty).Trim();

            Console.WriteLine($"[ArchiveFirstSemEnrollmentForStudentAsync] Starting archive for {email}, AY: {ay}");

            // Get candidate 1st-sem enrolled requests
            var candidates = await _enrollments
                .Find(e => e.Email == norm && e.Status == "Enrolled")
                .SortByDescending(e => e.SubmittedAt)
                .ToListAsync();

            Console.WriteLine($"[ArchiveFirstSemEnrollmentForStudentAsync] Found {candidates.Count} enrolled candidates");

            int archived = 0;
            foreach (var r in candidates)
            {
                var ef = r.ExtraFields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var sem = ef.TryGetValue("Academic.Semester", out var s) ? (s ?? "") : "";
                var reqAy = ef.TryGetValue("Academic.AcademicYear", out var a) ? (a ?? "") : "";
                var yearLevel = ef.TryGetValue("Academic.YearLevel", out var yl) ? (yl ?? "") : "";

                Console.WriteLine($"[ArchiveFirstSemEnrollmentForStudentAsync] Checking record {r.Id}:");
                Console.WriteLine($"  - Semester: '{sem}'");
                Console.WriteLine($"  - Year Level: '{yearLevel}'");
                Console.WriteLine($"  - Academic Year: '{reqAy}'");

                // ✅ Skip if academic year doesn't match
                if (!reqAy.Equals(ay, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  - SKIP: AY doesn't match ('{reqAy}' != '{ay}')");
                    continue;
                }

                // ✅ Skip if not 1st semester
                if (!sem.Contains("1st", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  - SKIP: Not 1st semester ('{sem}')");
                    continue;
                }

                // Already archived?
                var exists = await _enrollmentArchives.Find(ae => ae.OriginalRequestId == r.Id).AnyAsync();
                if (exists)
                {
                    Console.WriteLine($"  - SKIP: Already archived");
                    continue;
                }

                // ✅ CREATE ARCHIVE RECORD
                var archive = new EnrollmentArchive
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OriginalRequestId = r.Id,
                    Email = r.Email,
                    FullName = r.FullName,
                    Program = r.Program,
                    Type = r.Type,
                    Status = r.Status,
                    StatusAtArchive = r.Status,
                    Reason = reason,
                    Notes = r.Notes,
                    SubmittedAt = r.SubmittedAt,
                    LastUpdatedAt = r.LastUpdatedAt,
                    ArchivedAt = DateTime.UtcNow,
                    ArchiveReason = reason,
                    AcademicYear = reqAy, // ✅ From ExtraFields
                    Semester = sem,        // ✅ From ExtraFields
                    ExtraFields = r.ExtraFields,
                    DocumentFlags = r.DocumentFlags,
                    SecondSemesterEligibility = r.SecondSemesterEligibility,
                    EmergencyContactName = r.EmergencyContactName,
                    EmergencyContactPhone = r.EmergencyContactPhone
                };

                Console.WriteLine($"  ✅ ARCHIVING: {r.Id}");
                Console.WriteLine($"     - Archive ID: {archive.Id}");
                Console.WriteLine($"     - Collection: enrollmentArchives");
                Console.WriteLine($"     - AcademicYear: '{archive.AcademicYear}'");
                Console.WriteLine($"     - Semester: '{archive.Semester}'");

                // ✅ Insert into the CORRECT collection
                await _enrollmentArchives.InsertOneAsync(archive);

                // ✅ Verify it was inserted
                var verify = await _enrollmentArchives.Find(a => a.Id == archive.Id).FirstOrDefaultAsync();
                if (verify != null)
                {
                    Console.WriteLine($"     ✅ VERIFIED: Archive record exists in database");
                    Console.WriteLine($"        - Semester in DB: '{verify.Semester}'");
                    Console.WriteLine($"        - AY in DB: '{verify.AcademicYear}'");
                }
                else
                {
                    Console.WriteLine($"     ❌ ERROR: Archive record NOT found after insert!");
                }

                // Delete from active enrollments
                await _enrollments.DeleteOneAsync(e => e.Id == r.Id);
                archived++;
            }

            Console.WriteLine($"[ArchiveFirstSemEnrollmentForStudentAsync] ✅ Archived {archived} record(s)");
            return archived;
        }

        public async Task<List<EnrollmentArchive>> GetArchivesAsync(string? academicYear = null, int take = 200)
        {
            Console.WriteLine($"[GetArchivesAsync] Fetching archives, AY filter: '{academicYear ?? "NONE"}'");

            var filter = string.IsNullOrWhiteSpace(academicYear)
                ? Builders<EnrollmentArchive>.Filter.Empty
                : Builders<EnrollmentArchive>.Filter.Eq(a => a.AcademicYear, academicYear!.Trim());

            var results = await _enrollmentArchives.Find(filter)
                                                   .SortByDescending(a => a.ArchivedAt)
                                                   .Limit(Math.Max(1, take))
                                                   .ToListAsync();

            Console.WriteLine($"[GetArchivesAsync] Found {results.Count} archived records");

            // ✅ Debug first 3 records
            foreach (var r in results.Take(3))
            {
                Console.WriteLine($"  - {r.FullName}: AY='{r.AcademicYear}', Sem='{r.Semester}', Status='{r.StatusAtArchive}'");
            }

            return results;
        }

        public Task<EnrollmentArchive?> GetArchiveByIdAsync(string id)
            => _enrollmentArchives.Find(a => a.Id == id).FirstOrDefaultAsync();
        private static readonly (string Display, int Slot)[] DefaultSlots =
        {
    ("08:00-09:30", 1),
    ("10:30-12:00", 2),
    ("13:00-14:30", 3),
    ("14:30-16:00", 4),
    ("16:30-18:00", 5) // optional: push later slots if you want larger breaks
};

        // Split first-year arrays by program so we can pick BSIT or BSENT accordingly
        private static readonly string[] FirstYearFirstSemCourseCodes_BSIT =
            _1stYear1stSem.Subjects.Select(s => s.Code).ToArray();

        private static readonly string[] FirstYearSecondSemCourseCodes_BSIT =
            _1stYear2ndSem.Subjects.Select(s => s.Code).ToArray();

        private static readonly string[] FirstYearFirstSemCourseCodes_BSENT =
            E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects.Select(s => s.Code).ToArray();

        private static readonly string[] FirstYearSecondSemCourseCodes_BSENT =
            E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.Select(s => s.Code).ToArray();

        private static string[] GetFirstYearCourseCodesForSemester(string program, string semester)
        {
            if (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(semester, "2nd Semester", StringComparison.OrdinalIgnoreCase) &&
                    FirstYearSecondSemCourseCodes_BSENT.Length > 0)
                {
                    return FirstYearSecondSemCourseCodes_BSENT;
                }
                return FirstYearFirstSemCourseCodes_BSENT;
            }

            // Default to BSIT sets
            if (string.Equals(semester, "2nd Semester", StringComparison.OrdinalIgnoreCase) &&
                FirstYearSecondSemCourseCodes_BSIT.Length > 0)
            {
                return FirstYearSecondSemCourseCodes_BSIT;
            }

            return FirstYearFirstSemCourseCodes_BSIT; // default/fallback
        }

        public async Task SeedSchedulingDataIfEmptyAsync(int year = 2025, string program = "BSIT", int sectionCapacity = 1)
        {
            if (await _rooms.EstimatedDocumentCountAsync() == 0)
            {
                var avail = Enumerable.Range(1, 5) // Mon-Fri
                    .SelectMany(d => DefaultSlots.Select(slot => new RoomSlotAvailability
                    {
                        DayOfWeek = d,
                        Slot = slot.Slot
                    }))
                    .ToList();

                var rooms = Enumerable.Range(101, 10)
                    .Select(i => new Room
                    {
                        Id = $"R{i}",
                        Name = $"Room {i}",
                        Capacity = 1,
                        Building = "Main",
                        Availability = avail
                    }).ToList();
                await _rooms.InsertManyAsync(rooms);
            }

            var section = await _sections.Find(s => s.Program == program && s.Year == year && s.Name == "Freshmen-Block-1").FirstOrDefaultAsync();
            if (section is null)
            {
                section = new CourseSection
                {
                    Id = $"{program}-{year}-Block-1",
                    Program = program,
                    Name = "Freshmen-Block-1",
                    Capacity = sectionCapacity,
                    CurrentCount = 0,
                    Year = year
                };
                await _sections.InsertOneAsync(section);
            }

            var existingMeetings = await _meetings.Find(m => m.SectionId == section.Id).Limit(1).FirstOrDefaultAsync();
            if (existingMeetings is null)
            {
                var settings = await GetEnrollmentSettingsAsync();
                var codes = GetFirstYearCourseCodesForSemester(program, settings.Semester);
                await GenerateSectionScheduleAsync(section.Id, codes);
            }
        }

        public async Task GenerateSectionScheduleAsync(string sectionId, IEnumerable<string> courseCodes)
        {
            var rooms = await _rooms.Find(Builders<Room>.Filter.Empty).ToListAsync();
            if (rooms.Count == 0) return;

            var daySlots = new List<(int Day, string Display, int Slot)>();
            foreach (var slot in DefaultSlots)
            {
                for (int day = 1; day <= 5; day++)
                {
                    daySlots.Add((day, slot.Display, slot.Slot));
                }
            }

            int startIndex = 0;
            foreach (var course in courseCodes)
            {
                var placed = false;

                for (int pass = 0; pass < daySlots.Count && !placed; pass++)
                {
                    var idx = (startIndex + pass) % daySlots.Count;
                    var (day, display, slot) = daySlots[idx];

                    var room = await FindFreeRoomAsync(day, slot, rooms);
                    if (room is null) continue;

                    var meeting = new ClassMeeting
                    {
                        Id = $"{sectionId}:{course}:{day}:{slot}",
                        SectionId = sectionId,
                        CourseCode = course,
                        RoomId = room.Id,
                        DayOfWeek = day,
                        Slot = slot,
                        DisplayTime = display
                    };

                    try
                    {
                        await _meetings.InsertOneAsync(meeting);
                        placed = true;
                    }
                    catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                    {
                        continue;
                    }
                }

                startIndex = (startIndex + 1) % daySlots.Count;
            }
        }

        private async Task<Room?> FindFreeRoomAsync(int day, int slot, List<Room> candidateRooms)
        {
            var occupied = (await _meetings.Find(m => m.DayOfWeek == day && m.Slot == slot)
                                          .Project(m => m.RoomId)
                                          .ToListAsync())
                                          .ToHashSet();

            foreach (var r in candidateRooms)
            {
                var hasAvail = r.Availability != null && r.Availability.Count > 0;
                var availableThisSlot = !hasAvail || r.Availability.Any(a => a.DayOfWeek == day && a.Slot == slot);
                if (availableThisSlot && !occupied.Contains(r.Id))
                    return r;
            }
            return null;
        }

        public async Task<string> AssignFreshmanToAnyAvailableSectionAsync(string studentUsername, string program, int year, string semester, int sectionCapacity = 1)
        {
            using var session = await _client.StartSessionAsync();
            session.StartTransaction();

            try
            {
                if (string.IsNullOrWhiteSpace(program))
                    program = "BSIT";

                var isS2 = string.Equals(semester, "2nd Semester", StringComparison.OrdinalIgnoreCase);
                var semTag = isS2 ? "S2" : "S1";

                // Ensure infra exists
                await SeedSchedulingDataIfEmptyAsync(year: year, program: program, sectionCapacity: sectionCapacity);

                // Find viable sections by program/year/capacity, then filter by semester tag in Name
                var viable = await _sections.Find(s => s.Program == program && s.Year == year && s.CurrentCount < s.Capacity)
                                            .ToListAsync();

                CourseSection? section = null;
                if (isS2)
                {
                    section = viable.FirstOrDefault(s => (s.Name ?? "").EndsWith("-S2", StringComparison.OrdinalIgnoreCase));
                    // For 2nd sem do NOT fallback to S1 section
                }
                else
                {
                    section = viable.FirstOrDefault(s => !(s.Name ?? "").EndsWith("-S2", StringComparison.OrdinalIgnoreCase));
                    // Fallback to any if no tagged naming (legacy)
                    section ??= viable.FirstOrDefault();
                }

                if (section is null)
                {
                    // Create a dedicated section for the active semester
                    var countSameSem = await _sections.CountDocumentsAsync(s => s.Program == program && s.Year == year && (isS2 ? (s.Name ?? "").EndsWith("-S2") : !(s.Name ?? "").EndsWith("-S2")));
                    var blockNumber = (int)countSameSem + 1;

                    section = new CourseSection
                    {
                        Id = $"{program}-{year}-{semTag}-Block-{blockNumber}",
                        Program = program,
                        Name = $"Freshmen-Block-{blockNumber}-{semTag}",
                        Capacity = sectionCapacity,
                        CurrentCount = 0,
                        Year = year
                    };
                    await _sections.InsertOneAsync(session, section);

                    var codesNew = GetFirstYearCourseCodesForSemester(program, semester);
                    await GenerateSectionScheduleAsync(section.Id, codesNew);
                }
                else
                {
                    // Ensure this section has schedule; if empty, generate only for the active semester
                    var hasMeetings = await _meetings.Find(m => m.SectionId == section.Id).AnyAsync();
                    if (!hasMeetings)
                    {
                        var codes = GetFirstYearCourseCodesForSemester(program, semester);
                        await GenerateSectionScheduleAsync(section.Id, codes);
                        hasMeetings = await _meetings.Find(m => m.SectionId == section.Id).AnyAsync();
                        if (!hasMeetings)
                            throw new InvalidOperationException("No schedule could be generated for the selected section.");
                    }
                }

                // Reassign or insert the student's section enrollment (unique per student)
                var existingSse = await _studentSectionEnrollments.Find(e => e.StudentUsername == studentUsername).FirstOrDefaultAsync();

                if (existingSse is null)
                {
                    var sse = new StudentSectionEnrollment
                    {
                        Id = $"{studentUsername}:{section.Id}",
                        StudentUsername = studentUsername,
                        SectionId = section.Id,
                        EnrolledAt = DateTime.UtcNow
                    };
                    await _studentSectionEnrollments.InsertOneAsync(session, sse);

                    var filter = Builders<CourseSection>.Filter.Where(s => s.Id == section.Id && s.CurrentCount < s.Capacity);
                    var update = Builders<CourseSection>.Update.Inc(s => s.CurrentCount, 1);
                    var result = await _sections.UpdateOneAsync(session, filter, update);
                    if (result.ModifiedCount == 0)
                        throw new InvalidOperationException("Section reached capacity during enrollment. Please retry.");
                }
                else
                {
                    var oldSectionId = existingSse.SectionId;

                    if (!string.Equals(oldSectionId, section.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        // increment new
                        var incNew = await _sections.UpdateOneAsync(
                            session,
                            s => s.Id == section.Id && s.CurrentCount < s.Capacity,
                            Builders<CourseSection>.Update.Inc(s => s.CurrentCount, 1));
                        if (incNew.ModifiedCount == 0)
                            throw new InvalidOperationException("Section reached capacity during enrollment. Please retry.");

                        // decrement old
                        if (!string.IsNullOrWhiteSpace(oldSectionId))
                        {
                            await _sections.UpdateOneAsync(
                                session,
                                s => s.Id == oldSectionId && s.CurrentCount > 0,
                                Builders<CourseSection>.Update.Inc(s => s.CurrentCount, -1));
                        }
                    }

                    // point SSE to the semester-appropriate section
                    var sseUpdate = Builders<StudentSectionEnrollment>.Update
                        .Set(e => e.SectionId, section.Id)
                        .Set(e => e.EnrolledAt, DateTime.UtcNow);
                    await _studentSectionEnrollments.UpdateOneAsync(session, e => e.Id == existingSse.Id, sseUpdate);
                }

                await session.CommitTransactionAsync();
                return section.Id;
            }
            catch
            {
                await session.AbortTransactionAsync();
                throw;
            }
        }
        public async Task<List<ClassMeeting>> GetStudentScheduleAsync(string studentUsername)
        {
            var sse = await _studentSectionEnrollments.Find(e => e.StudentUsername == studentUsername).FirstOrDefaultAsync();
            if (sse is null) return new List<ClassMeeting>();
            return await _meetings.Find(m => m.SectionId == sse.SectionId)
                                  .SortBy(m => m.DayOfWeek).ThenBy(m => m.Slot)
                                  .ToListAsync();
        }

        public async Task ResetDatabaseAsync(bool reseed = false, int year = 0, string program = "BSIT", int sectionCapacity = 1)
        {
            var dbName = _database.DatabaseNamespace.DatabaseName;

            try
            {
                await _client.DropDatabaseAsync(dbName);
            }
            catch (MongoCommandException)
            {
                await DropAllCollectionsIndividuallyAsync();
            }
            catch
            {
                await DropAllCollectionsIndividuallyAsync();
            }

            // Recreate indexes/collections scaffolding
            await EnsureIndexesAndCleanupAsync();

            // GUARANTEE: settings singleton exists after any reset
            await GetEnrollmentSettingsAsync();

            if (reseed)
            {
                if (year <= 0) year = DateTime.UtcNow.Year;
                await SeedSchedulingDataIfEmptyAsync(year: year, program: program, sectionCapacity: sectionCapacity);
            }
        }
        private async Task DropAllCollectionsIndividuallyAsync()
        {
            try
            {
                using var cursor = await _database.ListCollectionNamesAsync();
                var names = await cursor.ToListAsync();
                foreach (var name in names)
                {
                    try { await _database.DropCollectionAsync(name); } catch { /* ignore per collection */ }
                }
            }
            catch
            {
                var known = new[]
                {
                    "enrollmentRequests", "students", "counters",
                    "rooms", "sections", "classMeetings",
                    "studentSectionEnrollments", "enrollmentSettings", "instructors"
                };
                foreach (var name in known)
                {
                    try { await _database.DropCollectionAsync(name); } catch { /* ignore */ }
                }
            }
        }

        // Clear student's existing schedule
        public async Task ClearStudentScheduleAsync(string studentUsername)
        {
            // Use the correct property name: StudentUsername (not Username)
            var filter = Builders<StudentSectionEnrollment>.Filter.Eq(x => x.StudentUsername, studentUsername);
            await _studentSectionEnrollments.DeleteManyAsync(filter);
        }

        // Update student schedule with filtered meetings
        public async Task UpdateStudentScheduleAsync(string studentUsername, List<ClassMeeting> meetings)
        {
            // First, remove old enrollments
            await ClearStudentScheduleAsync(studentUsername);

            if (meetings == null || meetings.Count == 0)
                return;

            // Get the section ID from the first meeting (all should have the same section)
            var sectionId = meetings.FirstOrDefault()?.SectionId;
            if (string.IsNullOrWhiteSpace(sectionId))
                return;

            // Create a single enrollment record pointing to the section
            // The section itself has the meetings, so we just need to link the student to the section
            var enrollment = new StudentSectionEnrollment
            {
                Id = $"{studentUsername}:{sectionId}",
                StudentUsername = studentUsername,
                SectionId = sectionId,
                EnrolledAt = DateTime.UtcNow
            };

            await _studentSectionEnrollments.InsertOneAsync(enrollment);
        }

        public async Task CreateCustomSectionAsync(CourseSection section)
        {
            await _sections.InsertOneAsync(section);
        }

        public async Task<List<ClassMeeting>> GetSectionScheduleAsync(string sectionId)
        {
            return await _meetings.Find(m => m.SectionId == sectionId)
                                  .SortBy(m => m.DayOfWeek)
                                  .ThenBy(m => m.Slot)
                                  .ToListAsync();
        }

        public async Task DeleteSectionAsync(string sectionId)
        {
            await _sections.DeleteOneAsync(s => s.Id == sectionId);
            await _meetings.DeleteManyAsync(m => m.SectionId == sectionId);
        }

        public async Task EnrollStudentInSectionAsync(string studentUsername, string sectionId)
        {
            // Remove any existing enrollment
            await _studentSectionEnrollments.DeleteManyAsync(e => e.StudentUsername == studentUsername);

            // Create new enrollment
            var enrollment = new StudentSectionEnrollment
            {
                Id = $"{studentUsername}:{sectionId}",
                StudentUsername = studentUsername,
                SectionId = sectionId,
                EnrolledAt = DateTime.UtcNow
            };

            await _studentSectionEnrollments.InsertOneAsync(enrollment);

            // Increment section count
            await _sections.UpdateOneAsync(
                s => s.Id == sectionId,
                Builders<CourseSection>.Update.Inc(s => s.CurrentCount, 1)
            );
        }

        // Get archived 1st semester enrollment for a student
        public async Task<EnrollmentArchive?> GetArchivedFirstSemesterEnrollmentAsync(string email, string academicYear)
        {
            var norm = (email ?? string.Empty).Trim().ToLowerInvariant();
            var ay = (academicYear ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(norm) || string.IsNullOrWhiteSpace(ay))
                return null;

            var filter = Builders<EnrollmentArchive>.Filter.And(
                Builders<EnrollmentArchive>.Filter.Eq(a => a.Email, norm),
                Builders<EnrollmentArchive>.Filter.Eq(a => a.AcademicYear, ay),
                Builders<EnrollmentArchive>.Filter.Regex(a => a.Semester, new BsonRegularExpression("^1", "i"))
            );

            return await _enrollmentArchives.Find(filter)
                                            .SortByDescending(a => a.ArchivedAt)
                                            .FirstOrDefaultAsync();
        }

        // =========================================================
        // ✅ Added: Method to fetch penalties from library database
        // =========================================================
        public async Task<List<Penalty>> GetStudentPenaltiesFromLibraryAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return new List<Penalty>();
                }

                // Connect to library database (LibraDB)
                // Try to get library connection string from config, fallback to hardcoded
                var libraryConnString = _config["Library:Mongo:ConnectionString"] ??
                    "mongodb+srv://leedb:aO8d4Xmm0fPuInDf@librarydb.8n8yjwr.mongodb.net/?retryWrites=true&w=majority";
                var libraryDbName = _config["Library:Mongo:Database"] ?? "LibraDB";
                
                var libraryClient = new MongoClient(libraryConnString);
                var libraryDb = libraryClient.GetDatabase(libraryDbName);

                // Get User and Penalty collections from library database
                var usersCollection = libraryDb.GetCollection<BsonDocument>("Users");
                var penaltiesCollection = libraryDb.GetCollection<Penalty>("Penalties");

                // Find user by email in library system
                var normalizedEmail = email.Trim().ToLowerInvariant();
                var userFilter = Builders<BsonDocument>.Filter.Eq("email", normalizedEmail);
                var user = await usersCollection.Find(userFilter).FirstOrDefaultAsync();

                if (user == null)
                {
                    // User not found in library system, return empty list
                    return new List<Penalty>();
                }

                // Get user ID
                var userId = user["_id"].AsObjectId;

                // Fetch penalties for this user
                var penaltyFilter = Builders<Penalty>.Filter.Eq(p => p.UserId, userId);
                var penalties = await penaltiesCollection.Find(penaltyFilter)
                    .SortByDescending(p => p.CreatedDate)
                    .ToListAsync();

                return penalties;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetStudentPenaltiesFromLibraryAsync] Error: {ex.Message}");
                Console.WriteLine($"[GetStudentPenaltiesFromLibraryAsync] StackTrace: {ex.StackTrace}");
                return new List<Penalty>();
            }
        }

        public async Task<List<EnrollmentRequest>> GetEnrollmentRequestsByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return new List<EnrollmentRequest>();

            var normalizedEmail = email.Trim().ToLowerInvariant();

            var filter = Builders<EnrollmentRequest>.Filter.Eq(r => r.Email, normalizedEmail);
            var results = await _enrollments  
                .Find(filter)
                .SortByDescending(r => r.SubmittedAt)
                .ToListAsync();

            return results ?? new List<EnrollmentRequest>();
        }
        public class InstructorSectionSummary { }
    }
}