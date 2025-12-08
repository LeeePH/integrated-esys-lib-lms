using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using StudentPortal.Models;
using StudentPortal.Models.AdminAssessment;
using StudentPortal.Models.AdminClass;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models.AdminMaterial;
using StudentPortal.Models.AdminTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Services
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoDatabase _enrollmentDatabase;
        private readonly IMongoDatabase _professorDatabase;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<ClassItem> _classes;
        private readonly IMongoCollection<StudentRecord> _students;
        private readonly IMongoCollection<UploadItem> _uploadCollection;
        private readonly IMongoCollection<ContentItem> _contentCollection;
        private readonly IMongoCollection<JoinRequest> _joinRequestsCollection;
        private readonly IMongoCollection<TaskItem> _taskCollection;
        private readonly IMongoCollection<StudentPortal.Models.AdminDb.AttendanceRecord> _attendanceCollection;
        private readonly IMongoCollection<BsonDocument> _attendanceCopyCollection;
        private readonly IMongoCollection<Submission> _submissionsCollection;
        private readonly IMongoCollection<StudentPortal.Models.AdminTask.TaskCommentItem> _taskCommentsCollection;
        private readonly IMongoCollection<StudentPortal.Models.AdminDb.AntiCheatLog> _antiCheatLogsCollection;
        private readonly IMongoCollection<StudentPortal.Models.StudentDb.AssessmentResult> _assessmentResultsCollection;
        private readonly IMongoCollection<EnrollmentStudent> _enrollmentStudents;
        private readonly IMongoCollection<Professor> _professors;
        private readonly string _professorCollectionName;

        public IMongoDatabase Database => _database;

        public MongoDbService(IConfiguration config)
        {
            // Student Portal database connection
            var client = new MongoClient(config["MongoDb:ConnectionString"]);
            _database = client.GetDatabase(config["MongoDb:Database"]);

            // Enrollment database connection (ESys)
            var enrollmentConnectionString = config["EnrollmentDb:ConnectionString"] ?? config["MongoDb:ConnectionString"];
            var enrollmentDatabaseName = config["EnrollmentDb:Database"] ?? "ESys";
            var enrollmentClient = new MongoClient(enrollmentConnectionString);
            
            // Test enrollment database connection and find the right database
            try
            {
                var databases = enrollmentClient.ListDatabaseNames().ToList();
                
                // Try different case variations of the database name
                // Note: MongoDB database names are case-sensitive
                var dbNameVariations = new[] { enrollmentDatabaseName, "ESys", "Esys", "esys", "ESYS", "EsysDB" };
                IMongoDatabase? foundDatabase = null;
                string? foundDbName = null;
                
                foreach (var dbName in dbNameVariations)
                {
                    // Check if database exists (case-sensitive check)
                    if (databases.Contains(dbName))
                    {
                        var testDb = enrollmentClient.GetDatabase(dbName);
                        
                        try
                        {
                        var collections = testDb.ListCollectionNames().ToList();
                            
                            // Check for students collection (case-sensitive)
                        if (collections.Contains("students"))
                        {
                            var studentsCollection = testDb.GetCollection<BsonDocument>("students");
                            var studentCount = studentsCollection.CountDocuments(FilterDefinition<BsonDocument>.Empty);
                                
                                // Use this database if it has students
                                foundDatabase = testDb;
                                foundDbName = dbName;
                                break;
                            }
                            else
                            {
                                // Also try case variations of collection name
                                var collectionVariations = new[] { "students", "Students", "STUDENTS" };
                                foreach (var collName in collectionVariations)
                                {
                                    if (collections.Contains(collName))
                                    {
                                        var studentsCollection = testDb.GetCollection<BsonDocument>(collName);
                                        var studentCount = studentsCollection.CountDocuments(FilterDefinition<BsonDocument>.Empty);
                                        
                                foundDatabase = testDb;
                                foundDbName = dbName;
                                        // Update the collection reference to use the correct case
                                        _enrollmentStudents = foundDatabase.GetCollection<EnrollmentStudent>(collName);
                                break;
                            }
                                }
                                
                                if (foundDatabase != null) break;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                
                if (foundDatabase == null)
                {
                    // Use the configured database name directly
                    foundDatabase = enrollmentClient.GetDatabase(enrollmentDatabaseName);
                    foundDbName = enrollmentDatabaseName;
                }
                
                _enrollmentDatabase = foundDatabase;
                
                // Verify connection
                _enrollmentDatabase.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            }
            catch (Exception)
            {
                // Fallback to configured database name
                _enrollmentDatabase = enrollmentClient.GetDatabase(enrollmentDatabaseName);
            }

            _users = _database.GetCollection<User>("Users");
            _classes = _database.GetCollection<ClassItem>("Classes");
            _students = _database.GetCollection<StudentRecord>("Students");
            _uploadCollection = _database.GetCollection<UploadItem>("Uploads");
            _contentCollection = _database.GetCollection<ContentItem>("Contents");
            _joinRequestsCollection = _database.GetCollection<JoinRequest>("JoinRequests");
            _taskCollection = _database.GetCollection<TaskItem>("Tasks");
            _attendanceCollection = _database.GetCollection<StudentPortal.Models.AdminDb.AttendanceRecord>("AttendanceRecords");
            _attendanceCopyCollection = _database.GetCollection<BsonDocument>("AttendanceCopy");
            _submissionsCollection = _database.GetCollection<Submission>("Submissions");
            _taskCommentsCollection = _database.GetCollection<StudentPortal.Models.AdminTask.TaskCommentItem>("TaskComments");
            _antiCheatLogsCollection = _database.GetCollection<StudentPortal.Models.AdminDb.AntiCheatLog>("AntiCheatLogs");
            _assessmentResultsCollection = _database.GetCollection<StudentPortal.Models.StudentDb.AssessmentResult>("AssessmentResults");
            
            // Initialize enrollment students collection - try different case variations
            try
            {
                var collectionNames = new[] { "students", "Students", "STUDENTS" };
                bool collectionFound = false;
                
                foreach (var collName in collectionNames)
                {
            try
            {
                        var testCollection = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var count = testCollection.CountDocuments(FilterDefinition<BsonDocument>.Empty);
                        if (count >= 0) // Collection exists (even if empty)
                        {
                            _enrollmentStudents = _enrollmentDatabase.GetCollection<EnrollmentStudent>(collName);
                            collectionFound = true;
                            break;
                        }
            }
                    catch
                    {
                        continue;
                    }
                }
                
                if (!collectionFound)
            {
                    // Default to "students" (lowercase)
                    _enrollmentStudents = _enrollmentDatabase.GetCollection<EnrollmentStudent>("students");
                }
            }
            catch
            {
                _enrollmentStudents = _enrollmentDatabase.GetCollection<EnrollmentStudent>("students");
            }

            // Professor database connection (ProfessorDB)
            var professorConnectionString = config["ProfessorDb:ConnectionString"] ?? config["MongoDb:ConnectionString"];
            var professorDatabaseName = config["ProfessorDb:Database"] ?? "ProfessorDB";
            var professorClient = new MongoClient(professorConnectionString);
            
            try
            {
                var databases = professorClient.ListDatabaseNames().ToList();
                var dbNameVariations = new[] { professorDatabaseName, "ProfessorDB", "professordb", "PROFESSORDB" };
                IMongoDatabase? foundProfessorDatabase = null;
                
                foreach (var dbName in dbNameVariations)
                {
                    if (databases.Contains(dbName))
                    {
                        var testDb = professorClient.GetDatabase(dbName);
                        try
                        {
                            var collections = testDb.ListCollectionNames().ToList();
                            Console.WriteLine($"[MongoDbService] Available collections in {dbName}: {string.Join(", ", collections)}");
                            
                            // Try common collection names for professors - prioritize "Professors" first
                            var collectionVariations = new[] { "Professors", "professors", "PROFESSORS", "Users", "users", "USERS" };
                            string? foundCollectionName = null;
                            foreach (var collName in collectionVariations)
                            {
                                if (collections.Contains(collName))
                                {
                                    foundProfessorDatabase = testDb;
                                    foundCollectionName = collName;
                                    _professors = foundProfessorDatabase.GetCollection<Professor>(collName);
                                    Console.WriteLine($"[MongoDbService] Found professor collection: {collName}");
                                    break;
                                }
                            }
                            if (foundCollectionName != null)
                            {
                                _professorCollectionName = foundCollectionName;
                                break;
                            }
                            if (foundProfessorDatabase != null) break;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                
                if (foundProfessorDatabase == null)
                {
                    _professorDatabase = professorClient.GetDatabase(professorDatabaseName);
                    // Try to find the collection in the database
                    try
                    {
                        var collections = _professorDatabase.ListCollectionNames().ToList();
                        Console.WriteLine($"[MongoDbService] Collections in {professorDatabaseName}: {string.Join(", ", collections)}");
                        
                        // Try "Professors" first, then "Users"
                        if (collections.Contains("Professors"))
                        {
                            _professorCollectionName = "Professors";
                            Console.WriteLine($"[MongoDbService] Using collection: Professors");
                        }
                        else if (collections.Contains("professors"))
                        {
                            _professorCollectionName = "professors";
                            Console.WriteLine($"[MongoDbService] Using collection: professors");
                        }
                        else if (collections.Contains("Users"))
                        {
                            _professorCollectionName = "Users";
                            Console.WriteLine($"[MongoDbService] Using collection: Users");
                        }
                        else
                        {
                            // Default to "Professors" (most likely for ProfessorDB)
                            _professorCollectionName = "Professors";
                            Console.WriteLine($"[MongoDbService] Defaulting to collection: Professors");
                        }
                    }
                    catch
                    {
                        // Default to "Professors" (most likely for ProfessorDB)
                        _professorCollectionName = "Professors";
                        Console.WriteLine($"[MongoDbService] Exception occurred, defaulting to collection: Professors");
                    }
                    _professors = _professorDatabase.GetCollection<Professor>(_professorCollectionName);
                }
                else
                {
                    _professorDatabase = foundProfessorDatabase;
                    Console.WriteLine($"[MongoDbService] Using found database with collection: {_professorCollectionName}");
                }
                
                // Verify connection
                _professorDatabase.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                Console.WriteLine($"[MongoDbService] ProfessorDB connection verified. Using collection: {_professorCollectionName}");
            }
            catch (Exception ex)
            {
                // Fallback to configured database name
                Console.WriteLine($"[MongoDbService] Exception connecting to ProfessorDB: {ex.Message}");
                _professorDatabase = professorClient.GetDatabase(professorDatabaseName);
                // Default to "Professors" (most likely for ProfessorDB)
                _professorCollectionName = "Professors";
                _professors = _professorDatabase.GetCollection<Professor>(_professorCollectionName);
                Console.WriteLine($"[MongoDbService] Fallback: Using collection: {_professorCollectionName}");
            }
        }

        // ---------------- PROFESSORS ----------------
        public async Task<Professor?> GetProfessorByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var originalEmail = email.Trim();
            
            Console.WriteLine($"[GetProfessorByEmailAsync] Searching for professor with email: {originalEmail}");
            Console.WriteLine($"[GetProfessorByEmailAsync] Using collection: {_professorCollectionName}");
            
            try
            {
                // Use BsonDocument collection directly for more reliable querying
                var bsonCollection = _professorDatabase.GetCollection<BsonDocument>(_professorCollectionName);
                
                // First, let's check if the collection exists and has documents
                var documentCount = await bsonCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
                Console.WriteLine($"[GetProfessorByEmailAsync] Collection '{_professorCollectionName}' has {documentCount} documents");
                
                // Try multiple field name variations: "email" (actual DB field) first, then "Email" (legacy)
                var emailFieldVariations = new[] { "email", "Email" };
                
                foreach (var emailField in emailFieldVariations)
                {
                    // Try case-insensitive regex search first (most reliable)
                    var bsonFilter = Builders<BsonDocument>.Filter.Regex(emailField, new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i"));
                    var bsonProfessor = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                    
                    if (bsonProfessor != null)
                    {
                        try
                        {
                            // Convert BsonDocument to Professor
                            var professor = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Professor>(bsonProfessor);
                            
                            // Ensure email is populated from any field variation
                            if (string.IsNullOrEmpty(professor.Email) && string.IsNullOrEmpty(professor.EmailLegacy))
                            {
                                // Try to get email from BsonDocument directly
                                if (bsonProfessor.Contains("email"))
                                    professor.Email = bsonProfessor["email"].AsString;
                                else if (bsonProfessor.Contains("Email"))
                                    professor.EmailLegacy = bsonProfessor["Email"].AsString;
                            }
                            
                            // Ensure passwordHash is populated
                            if (string.IsNullOrEmpty(professor.PasswordHash) && string.IsNullOrEmpty(professor.PasswordHashLegacy) && string.IsNullOrEmpty(professor.Password))
                            {
                                if (bsonProfessor.Contains("passwordHash"))
                                    professor.PasswordHash = bsonProfessor["passwordHash"].AsString;
                                else if (bsonProfessor.Contains("PasswordHash"))
                                    professor.PasswordHashLegacy = bsonProfessor["PasswordHash"].AsString;
                                else if (bsonProfessor.Contains("Password"))
                                    professor.Password = bsonProfessor["Password"].AsString;
                            }
                            
                            Console.WriteLine($"[GetProfessorByEmailAsync] Found professor: Email={professor.GetEmail()}, HasPasswordHash={!string.IsNullOrEmpty(professor.GetPasswordHash())}, FullName={professor.GetFullName()}");
                            return professor;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GetProfessorByEmailAsync] Error deserializing professor: {ex.Message}");
                            // Continue to try other methods
                        }
                    }
                    
                    // Fallback: Try exact match with original email (case-sensitive)
                    bsonFilter = Builders<BsonDocument>.Filter.Eq(emailField, originalEmail);
                    bsonProfessor = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                    
                    if (bsonProfessor != null)
                    {
                        try
                        {
                            var professor = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Professor>(bsonProfessor);
                            
                            // Ensure email is populated
                            if (string.IsNullOrEmpty(professor.Email) && string.IsNullOrEmpty(professor.EmailLegacy))
                            {
                                if (bsonProfessor.Contains("email"))
                                    professor.Email = bsonProfessor["email"].AsString;
                                else if (bsonProfessor.Contains("Email"))
                                    professor.EmailLegacy = bsonProfessor["Email"].AsString;
                            }
                            
                            // Ensure passwordHash is populated
                            if (string.IsNullOrEmpty(professor.PasswordHash) && string.IsNullOrEmpty(professor.PasswordHashLegacy) && string.IsNullOrEmpty(professor.Password))
                            {
                                if (bsonProfessor.Contains("passwordHash"))
                                    professor.PasswordHash = bsonProfessor["passwordHash"].AsString;
                                else if (bsonProfessor.Contains("PasswordHash"))
                                    professor.PasswordHashLegacy = bsonProfessor["PasswordHash"].AsString;
                            }
                            
                            Console.WriteLine($"[GetProfessorByEmailAsync] Found professor (exact match): Email={professor.GetEmail()}");
                            return professor;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GetProfessorByEmailAsync] Error deserializing professor (exact match): {ex.Message}");
                        }
                    }
                    
                    // Fallback: Try normalized lowercase
                    bsonFilter = Builders<BsonDocument>.Filter.Eq(emailField, normalizedEmail);
                    bsonProfessor = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                    
                    if (bsonProfessor != null)
                    {
                        try
                        {
                            var professor = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Professor>(bsonProfessor);
                            
                            // Ensure email is populated
                            if (string.IsNullOrEmpty(professor.Email) && string.IsNullOrEmpty(professor.EmailLegacy))
                            {
                                if (bsonProfessor.Contains("email"))
                                    professor.Email = bsonProfessor["email"].AsString;
                                else if (bsonProfessor.Contains("Email"))
                                    professor.EmailLegacy = bsonProfessor["Email"].AsString;
                            }
                            
                            // Ensure passwordHash is populated
                            if (string.IsNullOrEmpty(professor.PasswordHash) && string.IsNullOrEmpty(professor.PasswordHashLegacy) && string.IsNullOrEmpty(professor.Password))
                            {
                                if (bsonProfessor.Contains("passwordHash"))
                                    professor.PasswordHash = bsonProfessor["passwordHash"].AsString;
                                else if (bsonProfessor.Contains("PasswordHash"))
                                    professor.PasswordHashLegacy = bsonProfessor["PasswordHash"].AsString;
                            }
                            
                            Console.WriteLine($"[GetProfessorByEmailAsync] Found professor (normalized): Email={professor.GetEmail()}");
                            return professor;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GetProfessorByEmailAsync] Error deserializing professor (normalized): {ex.Message}");
                        }
                    }
                }
                
                Console.WriteLine($"[GetProfessorByEmailAsync] Professor not found for email: {originalEmail} in collection: {_professorCollectionName}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProfessorByEmailAsync] Exception searching for professor: {ex.Message}");
                Console.WriteLine($"[GetProfessorByEmailAsync] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // ---------------- ENROLLMENT STUDENTS ----------------
        public async Task<EnrollmentStudent?> GetEnrollmentStudentByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            var normalizedEmail = email.Trim().ToLowerInvariant();
            
            try
            {
                // Use BsonDocument collection directly for more reliable querying
                var bsonCollection = _enrollmentDatabase.GetCollection<BsonDocument>("students");
                
                // Try case-insensitive regex search first (most reliable)
                var bsonFilter = Builders<BsonDocument>.Filter.Regex("Email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i"));
                var bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonStudent != null)
                {
                    // Convert BsonDocument to EnrollmentStudent
                    var student = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<EnrollmentStudent>(bsonStudent);
                    return student;
                }
                
                // Fallback: Try exact match with original email (case-sensitive)
                bsonFilter = Builders<BsonDocument>.Filter.Eq("Email", email.Trim());
                bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonStudent != null)
                {
                    return MongoDB.Bson.Serialization.BsonSerializer.Deserialize<EnrollmentStudent>(bsonStudent);
                }
                
                // Fallback: Try normalized lowercase
                bsonFilter = Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail);
                bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonStudent != null)
                {
                    return MongoDB.Bson.Serialization.BsonSerializer.Deserialize<EnrollmentStudent>(bsonStudent);
                }
                
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Get student ExtraFields from ESys students collection or enrollmentRequests collection
        public async Task<Dictionary<string, string>?> GetStudentExtraFieldsByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            var normalizedEmail = email.Trim().ToLowerInvariant();
            
            try
            {
                // First, try the "students" collection
                var bsonCollection = _enrollmentDatabase.GetCollection<BsonDocument>("students");
                
                // Try case-insensitive regex search
                var bsonFilter = Builders<BsonDocument>.Filter.Regex("Email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i"));
                var bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonStudent == null)
                {
                    // Fallback: Try exact match
                    bsonFilter = Builders<BsonDocument>.Filter.Eq("Email", email.Trim());
                    bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                }
                
                if (bsonStudent == null)
                {
                    // Fallback: Try normalized lowercase
                    bsonFilter = Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail);
                    bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                }
                
                if (bsonStudent != null && bsonStudent.Contains("ExtraFields"))
                {
                    var extraFields = bsonStudent["ExtraFields"].AsBsonDocument;
                    var result = new Dictionary<string, string>();
                    foreach (var field in extraFields)
                    {
                        result[field.Name] = field.Value?.ToString() ?? string.Empty;
                    }
                    return result;
                }
                
                // If not found in "students" collection, try "enrollmentRequests" collection
                var enrollmentCollection = _enrollmentDatabase.GetCollection<BsonDocument>("enrollmentRequests");
                
                // Try case-insensitive regex search
                bsonFilter = Builders<BsonDocument>.Filter.Regex("Email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i"));
                var bsonEnrollment = await enrollmentCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonEnrollment == null)
                {
                    // Fallback: Try exact match
                    bsonFilter = Builders<BsonDocument>.Filter.Eq("Email", email.Trim());
                    bsonEnrollment = await enrollmentCollection.Find(bsonFilter).FirstOrDefaultAsync();
                }
                
                if (bsonEnrollment == null)
                {
                    // Fallback: Try normalized lowercase
                    bsonFilter = Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail);
                    bsonEnrollment = await enrollmentCollection.Find(bsonFilter).FirstOrDefaultAsync();
                }
                
                if (bsonEnrollment != null && bsonEnrollment.Contains("ExtraFields"))
                {
                    var extraFields = bsonEnrollment["ExtraFields"].AsBsonDocument;
                    var result = new Dictionary<string, string>();
                    foreach (var field in extraFields)
                    {
                        result[field.Name] = field.Value?.ToString() ?? string.Empty;
                    }
                    return result;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetStudentExtraFieldsByEmailAsync] Error: {ex.Message}");
                return null;
            }
        }

        // ---------------- USERS ----------------
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            var normalizedEmail = email.Trim().ToLowerInvariant();
            
            // Try exact match first
            var user = await _users.Find(u => u.Email == email.Trim()).FirstOrDefaultAsync();
            
            // If not found, try normalized lowercase
            if (user == null)
            {
                user = await _users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
            }
            
            // If still not found, try case-insensitive regex
            if (user == null)
            {
                var filter = Builders<User>.Filter.Regex(
                    u => u.Email,
                    new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i"));
                user = await _users.Find(filter).FirstOrDefaultAsync();
            }
            
            return user;
        }

        private static User MapUserFromBson(BsonDocument doc)
        {
            string GetString(string k)
            {
                return doc.TryGetValue(k, out var v) && v.BsonType != BsonType.Null ? v.ToString() : string.Empty;
            }
            int? GetInt(string k)
            {
                return doc.TryGetValue(k, out var v) && v.IsInt32 ? (int?)v.AsInt32 : (doc.TryGetValue(k, out var v2) && v2.IsInt64 ? (int?)(int)v2.AsInt64 : null);
            }
            DateTime? GetDate(string k)
            {
                return doc.TryGetValue(k, out var v) && v.IsValidDateTime ? (DateTime?)v.ToUniversalTime() : null;
            }
            bool GetBool(string k)
            {
                return doc.TryGetValue(k, out var v) && v.IsBoolean && v.AsBoolean;
            }
            List<string> GetStringList(string k)
            {
                if (doc.TryGetValue(k, out var v) && v.IsBsonArray)
                {
                    return v.AsBsonArray.Select(x => x.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                }
                return new List<string>();
            }

            var id = doc.TryGetValue("_id", out var idVal) ? idVal.ToString() : null;

            return new User
            {
                Id = id,
                Email = string.IsNullOrEmpty(GetString("Email")) ? GetString("email") : GetString("Email"),
                Password = GetString("Password"),
                OTP = GetString("OTP"),
                IsVerified = GetBool("IsVerified"),
                FullName = GetString("FullName"),
                LastName = GetString("LastName"),
                FirstName = GetString("FirstName"),
                MiddleName = GetString("MiddleName"),
                Role = string.IsNullOrEmpty(GetString("Role")) ? "Student" : GetString("Role"),
                FailedLoginAttempts = GetInt("FailedLoginAttempts"),
                LockoutEndTime = GetDate("LockoutEndTime"),
                JoinedClasses = GetStringList("JoinedClasses")
            };
        }

        public async Task<User> CreateUserFromEnrollmentStudentAsync(EnrollmentStudent enrollmentStudent)
        {
            // Get ExtraFields from ESys students collection to get name fields
            var extraFields = await GetStudentExtraFieldsByEmailAsync(enrollmentStudent.Email);
            
            string lastName = string.Empty;
            string firstName = string.Empty;
            string middleName = string.Empty;
            string fullName = string.Empty;
            
            if (extraFields != null)
            {
                extraFields.TryGetValue("Student.LastName", out lastName);
                extraFields.TryGetValue("Student.FirstName", out firstName);
                extraFields.TryGetValue("Student.MiddleName", out middleName);
                
                // Build FullName from parts
                var nameParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(firstName)) nameParts.Add(firstName);
                if (!string.IsNullOrWhiteSpace(middleName)) nameParts.Add(middleName);
                if (!string.IsNullOrWhiteSpace(lastName)) nameParts.Add(lastName);
                fullName = string.Join(" ", nameParts);
            }
            
            // Check if user already exists (case-insensitive)
            var normalizedEmail = enrollmentStudent.Email.Trim().ToLowerInvariant();
            var existingUser = await GetUserByEmailAsync(enrollmentStudent.Email);
            
            if (existingUser != null)
            {
                // User already exists, always sync password hash and ensure correct role/status
                var filter = Builders<User>.Filter.Eq(u => u.Email, existingUser.Email);
                var update = Builders<User>.Update
                    .Set(u => u.Password, enrollmentStudent.PasswordHash) // Always sync password hash from enrollment
                    .Set(u => u.IsVerified, true) // Ensure verified status
                    .Set(u => u.Role, "Student") // Ensure role is Student
                    .Set(u => u.EnrollmentId, enrollmentStudent.Id)
                    .Set(u => u.EnrollmentUsername, enrollmentStudent.Username);
                
                // Update name fields if available
                if (!string.IsNullOrWhiteSpace(lastName))
                    update = update.Set(u => u.LastName, lastName);
                if (!string.IsNullOrWhiteSpace(firstName))
                    update = update.Set(u => u.FirstName, firstName);
                if (!string.IsNullOrWhiteSpace(middleName))
                    update = update.Set(u => u.MiddleName, middleName);
                if (!string.IsNullOrWhiteSpace(fullName))
                    update = update.Set(u => u.FullName, fullName);
                
                await _users.UpdateOneAsync(filter, update);
                
                // Update local object for return
                existingUser.Password = enrollmentStudent.PasswordHash;
                existingUser.IsVerified = true;
                existingUser.Role = "Student";
                existingUser.EnrollmentId = enrollmentStudent.Id ?? string.Empty;
                existingUser.EnrollmentUsername = enrollmentStudent.Username ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(lastName)) existingUser.LastName = lastName;
                if (!string.IsNullOrWhiteSpace(firstName)) existingUser.FirstName = firstName;
                if (!string.IsNullOrWhiteSpace(middleName)) existingUser.MiddleName = middleName;
                if (!string.IsNullOrWhiteSpace(fullName)) existingUser.FullName = fullName;
                
                return existingUser;
            }

            // Create new user from enrollment student data
            var newUser = new User
            {
                Email = enrollmentStudent.Email,
                Password = enrollmentStudent.PasswordHash, // Use the same password hash
                OTP = "",
                IsVerified = true, // Enrollment students are already verified
                LastName = lastName ?? string.Empty,
                FirstName = firstName ?? string.Empty,
                MiddleName = middleName ?? string.Empty,
                FullName = fullName,
                Role = "Student",
                FailedLoginAttempts = 0,
                LockoutEndTime = null,
                JoinedClasses = new List<string>(),
                EnrollmentId = enrollmentStudent.Id ?? string.Empty,
                EnrollmentUsername = enrollmentStudent.Username ?? string.Empty
            };

            await _users.InsertOneAsync(newUser);
            return newUser;
        }

        public async Task<bool> LinkEnrollmentToUserByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var enrollment = await GetEnrollmentStudentByEmailAsync(email);
            if (enrollment == null) return false;
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update
                .Set(u => u.EnrollmentId, enrollment.Id)
                .Set(u => u.EnrollmentUsername, enrollment.Username)
                .Set(u => u.IsVerified, true)
                .Set(u => u.Role, "Student");
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<User> CreateUserFromProfessorAsync(Professor professor, string passwordHash)
        {
            // Get professor email and name using helper methods
            var professorEmail = professor.GetEmail();
            var professorName = professor.GetFullName();
            
            if (string.IsNullOrEmpty(professorEmail))
            {
                throw new ArgumentException("Professor email is required.");
            }

            // Check if user already exists (case-insensitive)
            var existingUser = await GetUserByEmailAsync(professorEmail);
            
            if (existingUser != null)
            {
                // User already exists, always sync password hash and ensure correct role/status
                var filter = Builders<User>.Filter.Eq(u => u.Email, existingUser.Email);
                var update = Builders<User>.Update
                    .Set(u => u.Password, passwordHash) // Always sync password hash from ProfessorDB
                    .Set(u => u.IsVerified, true) // Ensure verified status
                    .Set(u => u.Role, "Professor"); // Ensure role is Professor
                
                // Update FullName if available
                if (!string.IsNullOrEmpty(professorName))
                {
                    update = update.Set(u => u.FullName, professorName);
                }
                
                await _users.UpdateOneAsync(filter, update);
                
                // Update local object for return
                existingUser.Password = passwordHash;
                existingUser.IsVerified = true;
                existingUser.Role = "Professor";
                if (!string.IsNullOrEmpty(professorName))
                {
                    existingUser.FullName = professorName;
                }
                
                return existingUser;
            }

            // Create new user from professor data in StudentDB Users collection
            var newUser = new User
            {
                Email = professorEmail,
                Password = passwordHash, // Use the password hash (hashed if needed)
                OTP = "",
                IsVerified = true, // Professors from ProfessorDB are already verified
                FullName = !string.IsNullOrEmpty(professorName) ? professorName : "",
                Role = "Professor", // Set role as Professor
                FailedLoginAttempts = 0,
                LockoutEndTime = null,
                JoinedClasses = new List<string>()
            };

            await _users.InsertOneAsync(newUser);
            return newUser;
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            return await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        }

        public async Task<User?> GetFirstStudentAsync()
        {
            return await _users.Find(u => u.Role == "Student").FirstOrDefaultAsync();
        }

        public async Task<bool> PushJoinedClassAsync(string email, string classCode)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update.Push(u => u.JoinedClasses, classCode);
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, user.Email);
            var result = await _users.ReplaceOneAsync(filter, user);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        public async Task CreateUserAsync(string email, string hashedPassword, string otp, string role = "Student", bool markVerified = false)
        {
            var newUser = new User
            {
                Email = email,
                Password = hashedPassword,
                OTP = otp,
                IsVerified = markVerified,
                Role = role,
                JoinedClasses = new List<string>()
            };
            await _users.InsertOneAsync(newUser);
        }

        public async Task<bool> VerifyOtpAsync(string email, string otp)
        {
            var user = await GetUserByEmailAsync(email);
            return user != null && user.OTP == otp;
        }

        public async Task UpdateOtpAsync(string email, string newOtp)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update.Set(u => u.OTP, newOtp);
            await _users.UpdateOneAsync(filter, update);
        }

        public async Task UpdateUserPasswordAsync(string email, string hashedPassword)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update.Set(u => u.Password, hashedPassword);
            await _users.UpdateOneAsync(filter, update);
        }

        public async Task UpdateUserLoginStatusAsync(string email, int? failedAttempts, DateTime? lockoutEndTime)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update
                .Set(u => u.FailedLoginAttempts, failedAttempts)
                .Set(u => u.LockoutEndTime, lockoutEndTime);
            await _users.UpdateOneAsync(filter, update);
        }

        public async Task MarkUserAsVerifiedAsync(string email)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update.Set(u => u.IsVerified, true).Set(u => u.OTP, "");
            await _users.UpdateOneAsync(filter, update);
        }

        // ---------------- CLASSES ----------------
        public async Task<List<ClassItem>> GetAllClassesAsync()
        {
            return await _classes.Find(_ => true).ToListAsync();
        }

        /// <summary>
        /// Get classes owned by a specific professor (by email).
        /// </summary>
        public async Task<List<ClassItem>> GetClassesByOwnerEmailAsync(string ownerEmail)
        {
            if (string.IsNullOrWhiteSpace(ownerEmail))
                return new List<ClassItem>();

            var normalized = ownerEmail.Trim().ToLowerInvariant();
            return await _classes.Find(c => c.OwnerEmail.ToLower() == normalized).ToListAsync();
        }

        public async Task<ClassItem?> GetClassByCodeAsync(string code)
        {
            return await _classes.Find(c => c.ClassCode == code).FirstOrDefaultAsync();
        }

        public async Task<ClassItem?> GetClassByIdAsync(string classId)
        {
            return await _classes.Find(c => c.Id == classId).FirstOrDefaultAsync();
        }

        public async Task<bool> ClassExistsAsync(string subjectName, string section, string year, string course, string semester)
        {
            return await _classes.Find(c =>
                c.SubjectName.ToLower() == subjectName.ToLower() &&
                c.Section.ToLower() == section.ToLower() &&
                c.Year.ToLower() == year.ToLower() &&
                c.Course.ToLower() == course.ToLower() &&
                c.Semester.ToLower() == semester.ToLower()).AnyAsync();
        }

        public async Task<List<ClassItem>> GetClassesByCodesAsync(List<string> classCodes)
        {
            if (classCodes == null || classCodes.Count == 0)
                return new List<ClassItem>();

            return await _classes.Find(c => classCodes.Contains(c.ClassCode)).ToListAsync();
        }

        public async Task<List<ClassItem>> GetClassesByIdsAsync(List<string> ids)
        {
            return await _classes.Find(c => ids.Contains(c.Id)).ToListAsync();
        }

        public async Task AddClassToStudentAsync(string studentEmail, string classCode)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, studentEmail);
            var update = Builders<User>.Update.AddToSet(u => u.JoinedClasses, classCode);
            await _users.UpdateOneAsync(filter, update);
        }

        public async Task CreateClassAsync(ClassItem newClass)
        {
            if (string.IsNullOrEmpty(newClass.Id))
                newClass.Id = ObjectId.GenerateNewId().ToString();

            await _classes.InsertOneAsync(newClass);
        }

        public string GenerateClassCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            string code;
            do
            {
                code = new string(Enumerable.Repeat(chars, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            } while (_classes.Find(c => c.ClassCode == code).AnyAsync().Result);
            return code;
        }

        // ---------------- STUDENT MANAGEMENT ----------------
        public async Task<List<Student>> GetStudentsByClassIdAsync(string classId)
        {
            var classItem = await _classes.Find(c => c.Id == classId).FirstOrDefaultAsync();
            if (classItem == null) return new List<Student>();

            // Get students from Users collection who have this class in their JoinedClasses
            var usersInClass = await _users
                .Find(u => u.JoinedClasses.Contains(classItem.ClassCode) && u.Role == "Student")
                .ToListAsync();

            return usersInClass.Select(u => new Student
            {
                Id = u.Id ?? string.Empty,
                FullName = u.FullName ?? "Unknown Student",
                Email = u.Email ?? string.Empty
            }).ToList();
        }

        public async Task<List<StudentRecord>> GetStudentsByClassCodeAsync(string classCode)
        {
            if (string.IsNullOrEmpty(classCode)) return new List<StudentRecord>();
            var classItem = await _classes.Find(c => c.ClassCode == classCode).FirstOrDefaultAsync();
            if (classItem == null) return new List<StudentRecord>();

            var students = await GetStudentsByClassIdAsync(classItem.Id);

            return students.Select(s => new StudentRecord
            {
                Id = s.Id,
                ClassId = classItem.Id,
                StudentName = s.FullName,
                StudentEmail = s.Email,
                Status = "Active",
                Grade = 0.0
            }).ToList();
        }

        // ---------------- CONTENT ----------------
        public async Task InsertContentAsync(ContentItem content)
        {
            Console.WriteLine($"Inserting content - Type: {content.Type}, ClassId: {content.ClassId}, Title: {content.Title}");

            if (string.IsNullOrEmpty(content.Id))
                content.Id = ObjectId.GenerateNewId().ToString();

            await _contentCollection.InsertOneAsync(content);
        }

        public async Task<List<UploadItem>> GetRecentUploadsByClassIdAsync(string classId)
        {
            return await _uploadCollection.Find(u => u.ClassId == classId)
                .SortByDescending(u => u.UploadedAt)
                .Limit(5)
                .ToListAsync();
        }

        public async Task<List<ContentItem>> GetContentsByClassIdAsync(string classId)
        {
            return await _contentCollection.Find(c => c.ClassId == classId)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ContentItem>> GetContentsByClassCodeAsync(string classCode)
        {
            var classItem = await GetClassByCodeAsync(classCode);
            if (classItem == null)
                return new List<ContentItem>();

            return await _contentCollection.Find(c => c.ClassId == classItem.Id)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        // ---------------- CONTENT MANAGEMENT ----------------
        public async Task<ContentItem?> GetContentByIdAsync(string contentId)
        {
            return await _contentCollection.Find(c => c.Id == contentId).FirstOrDefaultAsync();
        }

        public async Task UpdateContentAsync(ContentItem content)
        {
            var filter = Builders<ContentItem>.Filter.Eq(c => c.Id, content.Id);
            var update = Builders<ContentItem>.Update
                .Set(c => c.Title, content.Title)
                .Set(c => c.Description, content.Description)
                .Set(c => c.LinkUrl, content.LinkUrl)
                .Set(c => c.Deadline, content.Deadline)
                .Set(c => c.Attachments, content.Attachments)
                .Set(c => c.MetaText, content.MetaText)
                .Set(c => c.MaxGrade, content.MaxGrade)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var result = await _contentCollection.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 0)
                throw new Exception("Could not save content");
        }

        public async Task DeleteContentAsync(string contentId)
        {
            await _contentCollection.DeleteOneAsync(c => c.Id == contentId);
        }

        // ---------------- FILE/UPLOAD MANAGEMENT ----------------
        public async Task<List<UploadItem>> GetUploadsByContentIdAsync(string contentId)
        {
            return await _uploadCollection
                .Find(u => u.ContentId == contentId)
                .SortByDescending(u => u.UploadedAt)
                .ToListAsync();
        }

        public async Task<UploadItem?> GetUploadByFileNameAsync(string fileName, string contentId)
        {
            return await _uploadCollection
                .Find(u => u.FileName == fileName && u.ContentId == contentId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<UploadItem>> GetUploadsByClassIdAsync(string classId)
        {
            return await _uploadCollection
                .Find(u => u.ClassId == classId)
                .SortByDescending(u => u.UploadedAt)
                .ToListAsync();
        }

        public async Task InsertUploadAsync(UploadItem upload)
        {
            if (string.IsNullOrEmpty(upload.Id))
                upload.Id = ObjectId.GenerateNewId().ToString();

            await _uploadCollection.InsertOneAsync(upload);
        }

        public async Task DeleteUploadAsync(string uploadId)
        {
            var filter = Builders<UploadItem>.Filter.Eq(u => u.Id, uploadId);
            await _uploadCollection.DeleteOneAsync(filter);
        }

        public async Task DeleteUploadsByContentIdAsync(string contentId)
        {
            var filter = Builders<UploadItem>.Filter.Eq(u => u.ContentId, contentId);
            await _uploadCollection.DeleteManyAsync(filter);
        }

        public async Task UpdateUploadAsync(UploadItem upload)
        {
            var filter = Builders<UploadItem>.Filter.Eq(u => u.Id, upload.Id);
            await _uploadCollection.ReplaceOneAsync(filter, upload);
        }

        // ---------------- MATERIAL MANAGEMENT ----------------
        public async Task<List<string>> GetRecentMaterialsByClassIdAsync(string classId)
        {
            var materials = await _contentCollection
                .Find(c => c.ClassId == classId && c.Type == "material")
                .SortByDescending(c => c.CreatedAt)
                .Limit(5)
                .ToListAsync();

            return materials.Select(m => m.Title).ToList();
        }

        public async Task<List<ContentItem>> GetMaterialsByClassIdAsync(string classId)
        {
            return await _contentCollection
                .Find(c => c.ClassId == classId && c.Type == "material")
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        // ---------------- TASKS ----------------
        public async Task<TaskItem?> GetTaskByIdAsync(string taskId)
        {
            return await _taskCollection.Find(t => t.Id == taskId).FirstOrDefaultAsync();
        }

        public async Task<List<TaskItem>> GetTasksByClassIdAsync(string classId)
        {
            return await _taskCollection.Find(t => t.ClassId == classId)
                .SortByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateTaskAsync(TaskItem task)
        {
            var filter = Builders<TaskItem>.Filter.Eq(t => t.Id, task.Id);
            await _taskCollection.ReplaceOneAsync(filter, task);
        }

        public async Task DeleteTaskAsync(string taskId)
        {
            var filter = Builders<TaskItem>.Filter.Eq(t => t.Id, taskId);
            await _taskCollection.DeleteOneAsync(filter);
        }

        public async Task InsertTaskAsync(TaskItem task)
        {
            if (string.IsNullOrEmpty(task.Id))
                task.Id = ObjectId.GenerateNewId().ToString();

            await _taskCollection.InsertOneAsync(task);
        }

        // ---------------- TASK SUBMISSIONS ----------------
        public async Task<List<Submission>> GetTaskSubmissionsAsync(string taskId)
        {
            try
            {
                return await _submissionsCollection
                    .Find(s => s.TaskId == taskId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting task submissions: {ex.Message}");
                return new List<Submission>();
            }
        }

        public async Task<Submission?> GetSubmissionByStudentAndTaskAsync(string studentId, string taskId)
        {
            return await _submissionsCollection
                .Find(s => s.StudentId == studentId && s.TaskId == taskId)
                .FirstOrDefaultAsync();
        }

        public async Task<Submission?> GetSubmissionByIdAsync(string submissionId)
        {
            try
            {
                return await _submissionsCollection
                    .Find(s => s.Id == submissionId)
                    .FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<Submission> CreateOrUpdateSubmissionAsync(Submission submission)
        {
            if (string.IsNullOrEmpty(submission.Id))
            {
                // Create new submission
                submission.Id = ObjectId.GenerateNewId().ToString();
                submission.CreatedAt = DateTime.UtcNow;
                submission.UpdatedAt = DateTime.UtcNow;
                await _submissionsCollection.InsertOneAsync(submission);
            }
            else
            {
                // Update existing submission
                submission.UpdatedAt = DateTime.UtcNow;
                var filter = Builders<Submission>.Filter.Eq(s => s.Id, submission.Id);
                await _submissionsCollection.ReplaceOneAsync(filter, submission);
            }

            return submission;
        }

        public async Task<bool> UpdateSubmissionStatusAsync(string submissionId, bool isApproved, bool hasPassed, string grade, string feedback)
        {
            var filter = Builders<Submission>.Filter.Eq(s => s.Id, submissionId);
            var update = Builders<Submission>.Update
                .Set(s => s.IsApproved, isApproved)
                .Set(s => s.HasPassed, hasPassed)
                .Set(s => s.Grade, grade)
                .Set(s => s.Feedback, feedback)
                .Set(s => s.ApprovedDate, isApproved ? DateTime.UtcNow : (DateTime?)null)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);

            var result = await _submissionsCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<int> GetSubmissionCountAsync(string taskId, bool submittedOnly = true)
        {
            var filter = submittedOnly
                ? Builders<Submission>.Filter.Eq(s => s.TaskId, taskId) & Builders<Submission>.Filter.Eq(s => s.Submitted, true)
                : Builders<Submission>.Filter.Eq(s => s.TaskId, taskId);

            return (int)await _submissionsCollection.CountDocumentsAsync(filter);
        }

        public async Task<int> GetApprovedSubmissionCountAsync(string taskId)
        {
            return (int)await _submissionsCollection
                .CountDocumentsAsync(s => s.TaskId == taskId && s.IsApproved == true);
        }

        public async Task<List<StudentPortal.Models.AdminTask.TaskCommentItem>> GetTaskCommentsAsync(string taskId)
        {
            try
            {
                return await _taskCommentsCollection
                    .Find(c => c.TaskId == taskId)
                    .SortByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch
            {
                return new List<StudentPortal.Models.AdminTask.TaskCommentItem>();
            }
        }

        public async Task<StudentPortal.Models.AdminTask.TaskCommentItem?> AddTaskCommentAsync(string taskId, string classId, string authorEmail, string authorName, string role, string text)
        {
            var item = new StudentPortal.Models.AdminTask.TaskCommentItem
            {
                TaskId = taskId,
                ClassId = classId,
                AuthorEmail = authorEmail,
                AuthorName = authorName,
                Role = role,
                Text = text,
                CreatedAt = DateTime.UtcNow,
                Replies = new List<StudentPortal.Models.AdminTask.TaskReplyItem>()
            };
            await _taskCommentsCollection.InsertOneAsync(item);
            return item;
        }

        public async Task<StudentPortal.Models.AdminTask.TaskCommentItem?> AddTaskReplyAsync(string commentId, string authorEmail, string authorName, string role, string text)
        {
            var reply = new StudentPortal.Models.AdminTask.TaskReplyItem
            {
                AuthorEmail = authorEmail,
                AuthorName = authorName,
                Role = role,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };

            var filter = Builders<StudentPortal.Models.AdminTask.TaskCommentItem>.Filter.Eq(c => c.Id, commentId);
            var update = Builders<StudentPortal.Models.AdminTask.TaskCommentItem>.Update.Push(c => c.Replies, reply);
            await _taskCommentsCollection.UpdateOneAsync(filter, update);
            return await _taskCommentsCollection.Find(filter).FirstOrDefaultAsync();
        }

        // ---------------- JOIN REQUESTS ----------------
        public async Task<List<JoinRequest>> GetJoinRequestsByClassCodeAsync(string classCode)
        {
            return await _joinRequestsCollection.Find(j => j.ClassCode == classCode && j.Status == "Pending")
                .SortByDescending(j => j.RequestedAt)
                .ToListAsync();
        }

        public async Task<JoinRequest?> GetJoinRequestByIdAsync(string requestId)
        {
            return await _joinRequestsCollection.Find(j => j.Id == requestId).FirstOrDefaultAsync();
        }

        public async Task CreateJoinRequestAsync(JoinRequest req)
        {
            await _joinRequestsCollection.InsertOneAsync(req);
        }

        public async Task UpdateJoinRequestAsync(JoinRequest joinRequest)
        {
            var filter = Builders<JoinRequest>.Filter.Eq(r => r.Id, joinRequest.Id);
            await _joinRequestsCollection.ReplaceOneAsync(filter, joinRequest);
        }

        public async Task RemoveJoinRequest(string requestId)
        {
            var filter = Builders<JoinRequest>.Filter.Eq(r => r.Id, requestId);
            await _joinRequestsCollection.DeleteOneAsync(filter);
        }

        public async Task<bool> JoinRequestExistsAsync(string email, string classCode)
        {
            var count = await _joinRequestsCollection
                .Find(j => j.StudentEmail == email && j.ClassCode == classCode && j.Status == "Pending")
                .CountDocumentsAsync();

            return count > 0;
        }

        public async Task<List<JoinRequest>> GetJoinRequestsByEmailAsync(string email)
        {
            return await _joinRequestsCollection
                .Find(j => j.StudentEmail == email && j.Status == "Pending")
                .SortByDescending(j => j.RequestedAt)
                .ToListAsync();
        }

        // ---------------- ATTENDANCE ----------------
        public async Task UpsertAttendanceRecordAsync(string classCode, string studentId, string status)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(status))
                throw new ArgumentException("classCode, studentId, and status are required");

            var classItem = await GetClassByCodeAsync(classCode);
            if (classItem == null)
                throw new KeyNotFoundException($"Class with code {classCode} not found.");

            var user = await _users.Find(u => u.Id == studentId).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with id {studentId} not found.");

            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            var filter = Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.And(
                Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.Eq(a => a.ClassId, classItem.Id),
                Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.Eq(a => a.StudentId, studentId),
                Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.Gte(a => a.Date, todayStart),
                Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.Lt(a => a.Date, todayEnd)
            );

            var update = Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Update
                .Set(a => a.ClassId, classItem.Id)
                .Set(a => a.ClassCode, classCode)
                .Set(a => a.StudentId, studentId)
                .Set(a => a.StudentName, user.FullName ?? string.Empty)
                .Set(a => a.Date, DateTime.UtcNow)
                .Set(a => a.Status, status);

            var options = new UpdateOptions { IsUpsert = true };
            await _attendanceCollection.UpdateOneAsync(filter, update, options);
        }

        /// <summary>
        /// Bulk-insert generic attendance rows coming from an Excel import
        /// into the AttendanceCopy collection. The collection is created
        /// automatically by MongoDB on first insert if it does not exist.
        /// </summary>
        public async Task InsertAttendanceCopyRowsAsync(List<Dictionary<string, string>> rows)
        {
            if (rows == null || rows.Count == 0) return;

            var docs = new List<BsonDocument>();
            foreach (var row in rows)
            {
                // Convert the row key/value pairs into a BsonDocument
                var doc = new BsonDocument();
                foreach (var kvp in row)
                {
                    doc[kvp.Key] = kvp.Value ?? string.Empty;
                }
                docs.Add(doc);
            }

            await _attendanceCopyCollection.InsertManyAsync(docs);
        }

        public async Task<List<StudentPortal.Models.AdminDb.AttendanceRecord>> GetAttendanceByClassCodeAsync(string classCode)
        {
            var classItem = await GetClassByCodeAsync(classCode);
            if (classItem == null) return new List<StudentPortal.Models.AdminDb.AttendanceRecord>();
            return await _attendanceCollection
                .Find(a => a.ClassId == classItem.Id)
                .SortByDescending(a => a.Date)
                .ToListAsync();
        }

        public async Task<List<StudentPortal.Models.AdminDb.AttendanceRecord>> GetAttendanceByStudentAsync(string classCode, string studentId)
        {
            var classItem = await GetClassByCodeAsync(classCode);
            if (classItem == null) return new List<StudentPortal.Models.AdminDb.AttendanceRecord>();
            return await _attendanceCollection
                .Find(a => a.ClassId == classItem.Id && a.StudentId == studentId)
                .SortByDescending(a => a.Date)
                .ToListAsync();
        }

        public async Task AddStudentToClass(string studentEmail, string classCode)
        {
            if (string.IsNullOrEmpty(studentEmail) || string.IsNullOrEmpty(classCode))
                throw new ArgumentException("Email and ClassCode are required.");

            var user = await _users.Find(u => u.Email == studentEmail).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with email {studentEmail} not found.");

            if (user.JoinedClasses == null)
                user.JoinedClasses = new List<string>();

            if (!user.JoinedClasses.Contains(classCode))
                user.JoinedClasses.Add(classCode);

            await UpdateUserAsync(user);
        }

        public async Task<bool> RemoveStudentFromClassById(string studentId, string classId)
        {
            if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(classId))
                throw new ArgumentException("StudentId and ClassId are required.");

            // Get the class to find the class code
            var classItem = await GetClassByIdAsync(classId);
            if (classItem == null)
                throw new KeyNotFoundException($"Class with id {classId} not found.");

            // Get the user by student ID
            var user = await _users.Find(u => u.Id == studentId).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with id {studentId} not found.");

            // Remove the class code from JoinedClasses
            if (user.JoinedClasses != null && user.JoinedClasses.Contains(classItem.ClassCode))
            {
                var filter = Builders<User>.Filter.Eq(u => u.Id, studentId);
                var update = Builders<User>.Update.Pull(u => u.JoinedClasses, classItem.ClassCode);
                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }

            return false;
        }

        public async Task<bool> RemoveStudentFromClassByEmail(string studentEmail, string classCode)
        {
            if (string.IsNullOrEmpty(studentEmail) || string.IsNullOrEmpty(classCode))
                throw new ArgumentException("Email and ClassCode are required.");

            var user = await _users.Find(u => u.Email == studentEmail).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with email {studentEmail} not found.");

            // Remove the class code from JoinedClasses
            if (user.JoinedClasses != null && user.JoinedClasses.Contains(classCode))
            {
                var filter = Builders<User>.Filter.Eq(u => u.Email, studentEmail);
                var update = Builders<User>.Update.Pull(u => u.JoinedClasses, classCode);
                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }

            return false;
        }

        // ---------------- ASSESSMENT MANAGEMENT ----------------
        public async Task<AdminAssessment?> GetAssessmentByIdAsync(string assessmentId)
        {
            return await _database.GetCollection<AdminAssessment>("Assessments")
                .Find(a => a.Id == assessmentId)
                .FirstOrDefaultAsync();
        }

        public async Task<AdminAssessment?> GetAssessmentByClassIdAsync(string classId)
        {
            return await _database.GetCollection<AdminAssessment>("Assessments")
                .Find(a => a.ClassId == classId && a.Status == "Active")
                .SortByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<AdminAssessment>> GetAssessmentsByClassIdAsync(string classId)
        {
            return await _database.GetCollection<AdminAssessment>("Assessments")
                .Find(a => a.ClassId == classId)
                .SortByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<AdminAssessment> CreateAssessmentAsync(AdminAssessment assessment)
        {
            if (string.IsNullOrEmpty(assessment.Id))
                assessment.Id = ObjectId.GenerateNewId().ToString();

            assessment.CreatedAt = DateTime.UtcNow;
            assessment.UpdatedAt = DateTime.UtcNow;

            await _database.GetCollection<AdminAssessment>("Assessments")
                .InsertOneAsync(assessment);

            return assessment;
        }

        public async Task<AdminAssessment> UpdateAssessmentAsync(AdminAssessment assessment)
        {
            assessment.UpdatedAt = DateTime.UtcNow;

            var filter = Builders<AdminAssessment>.Filter.Eq(a => a.Id, assessment.Id);
            await _database.GetCollection<AdminAssessment>("Assessments")
                .ReplaceOneAsync(filter, assessment);

            return assessment;
        }

        public async Task<bool> DeleteAssessmentAsync(string assessmentId)
        {
            var result = await _database.GetCollection<AdminAssessment>("Assessments")
                .DeleteOneAsync(a => a.Id == assessmentId);

            return result.DeletedCount > 0;
        }

        // ---------------- ASSESSMENT SUBMISSIONS ----------------
        public async Task<List<AssessmentSubmission>> GetSubmissionsByAssessmentIdAsync(string assessmentId)
        {
            return await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                .Find(s => s.AssessmentId == assessmentId)
                .SortBy(s => s.StudentName)
                .ToListAsync();
        }

        public async Task<AssessmentSubmission?> GetSubmissionByStudentAsync(string assessmentId, string studentId)
        {
            return await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                .Find(s => s.AssessmentId == assessmentId && s.StudentId == studentId)
                .FirstOrDefaultAsync();
        }

        public async Task<AssessmentSubmission> CreateOrUpdateSubmissionAsync(AssessmentSubmission submission)
        {
            if (string.IsNullOrEmpty(submission.Id))
            {
                submission.Id = ObjectId.GenerateNewId().ToString();
                submission.CreatedAt = DateTime.UtcNow;
                await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                    .InsertOneAsync(submission);
            }
            else
            {
                submission.UpdatedAt = DateTime.UtcNow;
                var filter = Builders<AssessmentSubmission>.Filter.Eq(s => s.Id, submission.Id);
                await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                    .ReplaceOneAsync(filter, submission);
            }

            return submission;
        }

        public async Task<int> GetAssessmentSubmissionCountAsync(string assessmentId, string status = "Submitted")
        {
            return (int)await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                .Find(s => s.AssessmentId == assessmentId && s.Status == status)
                .CountDocumentsAsync();
        }

        // ---------------- DEBUG METHODS ----------------
        public async Task<List<ClassItem>> DebugGetAllClassesAsync()
        {
            var classes = await _classes.Find(_ => true).ToListAsync();
            Console.WriteLine($"Total classes in database: {classes.Count}");
            foreach (var cls in classes)
            {
                Console.WriteLine($"Class: {cls.ClassCode} - {cls.SubjectName} - ID: {cls.Id}");
            }
            return classes;
        }

        // Add this method to your MongoDbService class
        public async Task DebugSubmissions(string taskId)
        {
            try
            {
                Console.WriteLine($"=== DEBUG SUBMISSIONS FOR TASK: {taskId} ===");

                // Get all submissions for this task
                var submissions = await _submissionsCollection
                    .Find(s => s.TaskId == taskId)
                    .ToListAsync();

                Console.WriteLine($"Found {submissions.Count} submissions for this task:");

                foreach (var sub in submissions)
                {
                    Console.WriteLine($"- Submission ID: {sub.Id}");
                    Console.WriteLine($"  Student: {sub.StudentName} ({sub.StudentId})");
                    Console.WriteLine($"  Email: {sub.StudentEmail}");
                    Console.WriteLine($"  Submitted: {sub.Submitted}");
                    Console.WriteLine($"  SubmittedAt: {sub.SubmittedAt}");
                    Console.WriteLine($"  File: {sub.FileName} (Size: {sub.FileSize})");
                    Console.WriteLine($"  FileUrl: {sub.FileUrl}");
                    Console.WriteLine($"  Approved: {sub.IsApproved}");
                    Console.WriteLine($"  Passed: {sub.HasPassed}");
                    Console.WriteLine($"  Grade: {sub.Grade}");
                    Console.WriteLine($"  Feedback: {sub.Feedback}");
                    Console.WriteLine($"  Created: {sub.CreatedAt}");
                    Console.WriteLine($"  Updated: {sub.UpdatedAt}");
                    Console.WriteLine($"  ---");
                }

                if (submissions.Count == 0)
                {
                    Console.WriteLine("No submissions found for this task.");
                }

                Console.WriteLine("=== END DEBUG ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== DEBUG ERROR ===");
                Console.WriteLine($"Error in DebugSubmissions: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                Console.WriteLine($"=== END DEBUG ERROR ===");
            }
        }

        public async Task<bool> UpdateSubmissionStatusAsync(string studentId, string taskId, bool isApproved, bool hasPassed, string grade, string feedback)
        {
            try
            {
                var submission = await GetSubmissionByStudentAndTaskAsync(studentId, taskId);
                if (submission == null)
                    return false;

                submission.IsApproved = isApproved;
                submission.HasPassed = hasPassed;
                submission.Grade = grade;
                submission.Feedback = feedback;
                submission.ApprovedDate = isApproved ? DateTime.UtcNow : (DateTime?)null;
                submission.UpdatedAt = DateTime.UtcNow;

                await CreateOrUpdateSubmissionAsync(submission);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateSubmissionStatusAsync: {ex.Message}");
                return false;
            }
        }
        public async Task AddAntiCheatLogAsync(StudentPortal.Models.AdminDb.AntiCheatLog log)
        {
            await _antiCheatLogsCollection.InsertOneAsync(log);
        }

        public async Task<List<StudentPortal.Models.AdminDb.AntiCheatLog>> GetAntiCheatLogsAsync(string classId, string contentId)
        {
            var filter = Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.ClassId, classId)
                       & Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.ContentId, contentId);
            return await _antiCheatLogsCollection.Find(filter).ToListAsync();
        }

        public async Task<StudentPortal.Models.StudentDb.AssessmentResult?> GetAssessmentResultAsync(string classId, string contentId, string studentId)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(contentId) || string.IsNullOrEmpty(studentId)) return null;
            return await _assessmentResultsCollection
                .Find(r => r.ClassId == classId && r.ContentId == contentId && r.StudentId == studentId)
                .FirstOrDefaultAsync();
        }

        public async Task UpsertAssessmentSubmittedAsync(string classId, string classCode, string contentId, string studentId, string studentEmail)
        {
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.StudentId, studentId)
            );
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .SetOnInsert(r => r.Id, ObjectId.GenerateNewId().ToString())
                .Set(r => r.ClassId, classId)
                .Set(r => r.ClassCode, classCode)
                .Set(r => r.ContentId, contentId)
                .Set(r => r.StudentId, studentId)
                .Set(r => r.StudentEmail, studentEmail)
                .Set(r => r.SubmittedAt, DateTime.UtcNow)
                .Set(r => r.Status, "submitted");
            var options = new UpdateOptions { IsUpsert = true };
            await _assessmentResultsCollection.UpdateOneAsync(filter, update, options);
        }

        public async Task UpdateAssessmentScoreAsync(string classId, string contentId, string studentId, double? score, double? maxScore)
        {
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.StudentId, studentId)
            );
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .Set(r => r.Score, score)
                .Set(r => r.MaxScore, maxScore)
                .Set(r => r.Status, score.HasValue ? "scored" : "submitted");
            await _assessmentResultsCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }
    }
}
