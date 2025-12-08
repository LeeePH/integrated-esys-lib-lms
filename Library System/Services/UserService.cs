using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;
using BCrypt.Net;
using System;

namespace SystemLibrary.Services
{
    public class UserService : IUserService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<StudentProfile> _studentProfiles;
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IMongoCollection<PasswordResetToken> _resetTokens;
        private readonly IMongoCollection<PenaltyRecord> _penaltyRecords;
        private readonly IMongoDbService _mongoDbService;
        private readonly IEnrollmentSystemService _enrollmentSystemService;

        public UserService(IMongoDbService mongoDbService, IEnrollmentSystemService enrollmentSystemService)
        {
            _mongoDbService = mongoDbService;
            _enrollmentSystemService = enrollmentSystemService;
            _users = mongoDbService.GetCollection<User>("Users");
            _studentProfiles = mongoDbService.GetCollection<StudentProfile>("StudentProfiles");
            _reservations = mongoDbService.GetCollection<Reservation>("Reservations");

            _resetTokens = mongoDbService.GetCollection<PasswordResetToken>("PasswordResetTokens");
            _penaltyRecords = mongoDbService.GetCollection<PenaltyRecord>("PenaltyRecords");
        }

        public async Task<User> AuthenticateAsync(LoginViewModel loginModel)
        {
            // Normalize email
            var normalizedEmail = (loginModel.Email ?? string.Empty).Trim().ToLowerInvariant();
            
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                Console.WriteLine("[AuthenticateAsync] Email is empty");
                return null;
            }

            Console.WriteLine($"[AuthenticateAsync] Attempting authentication for email: {normalizedEmail}");

            // ALWAYS check enrollment system FIRST for student authentication
            // This ensures enrollment system is the source of truth
            EnrollmentStudent? enrollmentStudent = await _enrollmentSystemService.GetStudentByEmailAsync(normalizedEmail);
            
            if (enrollmentStudent != null)
            {
                Console.WriteLine($"[AuthenticateAsync] Found student in enrollment system: {enrollmentStudent.Username}");
                
                // Verify password against enrollment system
                bool passwordValid = false;
                try
                {
                    passwordValid = BCrypt.Net.BCrypt.Verify(loginModel.Password, enrollmentStudent.PasswordHash);
                    Console.WriteLine($"[AuthenticateAsync] Password verification result: {passwordValid}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AuthenticateAsync] Password verification error: {ex.Message}");
                    return null;
                }

                if (!passwordValid)
                {
                    Console.WriteLine($"[AuthenticateAsync] Password invalid for enrollment student: {normalizedEmail}");
                    
                    // Check if user exists in library system to track failed attempts
                    var libraryUser = await _users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
                    if (libraryUser != null)
                    {
                        libraryUser.FailedLoginAttempts++;
                        if (libraryUser.FailedLoginAttempts >= 3)
                        {
                            libraryUser.LockoutEndTime = DateTime.UtcNow.AddMinutes(3);
                            libraryUser.FailedLoginAttempts = 0;
                        }
                        await UpdateUserAsync(libraryUser);
                    }
                    
                    return null;
                }

                // Password is valid - sync user from enrollment to library system
                Console.WriteLine($"[AuthenticateAsync] Password valid, syncing user to library system...");
                var user = await SyncUserFromEnrollmentAsync(enrollmentStudent);
                
                if (user == null)
                {
                    Console.WriteLine($"[AuthenticateAsync] Failed to sync user from enrollment system");
                    return null;
                }

                // Check if account is active
                if (!user.IsActive)
                {
                    Console.WriteLine($"[AuthenticateAsync] User account is not active: {normalizedEmail}");
                    return null;
                }

                // Check if account is locked - use UTC consistently
                if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value > DateTime.UtcNow)
                {
                    Console.WriteLine($"[AuthenticateAsync] Account is locked until {user.LockoutEndTime}");
                    return null;
                }

                // Clear lockout if time has passed
                if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value <= DateTime.UtcNow)
                {
                    user.LockoutEndTime = null;
                    user.FailedLoginAttempts = 0;
                    await UpdateUserAsync(user);
                }

                // Password correct - reset security fields and sync password hash
                user.FailedLoginAttempts = 0;
                user.LockoutEndTime = null;
                user.PasswordHash = enrollmentStudent.PasswordHash; // Always sync password hash
                user.IsActive = true; // Ensure user is active
                user.Role = "student"; // Ensure role is student
                
                await UpdateUserAsync(user);
                
                Console.WriteLine($"[AuthenticateAsync] Authentication successful for: {normalizedEmail}");
                return user;
            }

            // Student not found in enrollment system - check library system for non-student users (admin/librarian)
            Console.WriteLine($"[AuthenticateAsync] Student not found in enrollment system, checking library system...");
            var libraryUserOnly = await _users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
            
            // Backward compatibility: some older accounts may have been created with username-only (stored in StudentId)
            if (libraryUserOnly == null)
            {
                Console.WriteLine($"[AuthenticateAsync] No library user found by email. Trying legacy username lookup using: {normalizedEmail}");
                libraryUserOnly = await _users.Find(u => u.StudentId == normalizedEmail).FirstOrDefaultAsync();
            }
            
            if (libraryUserOnly == null)
            {
                Console.WriteLine($"[AuthenticateAsync] User not found in either system: {normalizedEmail}");
                return null;
            }

            // For any library-only user (admin, librarian, or student), verify password normally
            Console.WriteLine($"[AuthenticateAsync] Authenticating library user: {libraryUserOnly.Role}");
            
            // Check if account is active
            if (!libraryUserOnly.IsActive)
                return null;

            // Check if account is locked
            if (libraryUserOnly.LockoutEndTime.HasValue && libraryUserOnly.LockoutEndTime.Value > DateTime.UtcNow)
            {
                Console.WriteLine($"Account is locked until {libraryUserOnly.LockoutEndTime}");
                return null;
            }

            // Clear lockout if time has passed
            if (libraryUserOnly.LockoutEndTime.HasValue && libraryUserOnly.LockoutEndTime.Value <= DateTime.UtcNow)
            {
                libraryUserOnly.LockoutEndTime = null;
                libraryUserOnly.FailedLoginAttempts = 0;
                await UpdateUserAsync(libraryUserOnly);
            }

            // Verify password
            bool libraryPasswordValid = BCrypt.Net.BCrypt.Verify(loginModel.Password, libraryUserOnly.PasswordHash);

            if (!libraryPasswordValid)
            {
                libraryUserOnly.FailedLoginAttempts++;
                if (libraryUserOnly.FailedLoginAttempts >= 3)
                {
                    libraryUserOnly.LockoutEndTime = DateTime.UtcNow.AddMinutes(3);
                    libraryUserOnly.FailedLoginAttempts = 0;
                }
                await UpdateUserAsync(libraryUserOnly);
                return null;
            }

            // Password correct - reset security fields
            libraryUserOnly.FailedLoginAttempts = 0;
            libraryUserOnly.LockoutEndTime = null;
            await UpdateUserAsync(libraryUserOnly);

            return libraryUserOnly;
        }

        private async Task<User?> SyncUserFromEnrollmentAsync(EnrollmentStudent enrollmentStudent)
        {
            try
            {
                // Fetch enrollment request to get detailed student information
                var enrollmentRequest = await _enrollmentSystemService.GetLatestEnrollmentRequestByEmailAsync(enrollmentStudent.Email);
                
                // Extract student details from enrollment request
                string firstName = string.Empty;
                string lastName = string.Empty;
                string middleName = string.Empty;
                string contactNumber = string.Empty;
                string fullName = enrollmentRequest?.FullName ?? string.Empty;

                if (enrollmentRequest?.ExtraFields != null)
                {
                    enrollmentRequest.ExtraFields.TryGetValue("Student.FirstName", out firstName);
                    enrollmentRequest.ExtraFields.TryGetValue("Student.LastName", out lastName);
                    enrollmentRequest.ExtraFields.TryGetValue("Student.MiddleName", out middleName);
                    enrollmentRequest.ExtraFields.TryGetValue("Student.ContactNumber", out contactNumber);
                }

                // If full name is available but not split, try to parse it
                if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(fullName))
                {
                    var nameParts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (nameParts.Length >= 1) firstName = nameParts[0];
                    if (nameParts.Length >= 2) lastName = nameParts[nameParts.Length - 1];
                    if (nameParts.Length > 2) middleName = string.Join(" ", nameParts.Skip(1).Take(nameParts.Length - 2));
                }

                // Check if user already exists with different email case or by username
                var existingUser = await _users.Find(u => u.StudentId == enrollmentStudent.Username || u.Email == enrollmentStudent.Email).FirstOrDefaultAsync();
                
                if (existingUser != null)
                {
                    // Update existing user with enrollment data
                    existingUser.Email = enrollmentStudent.Email.Trim().ToLowerInvariant();
                    existingUser.PasswordHash = enrollmentStudent.PasswordHash;
                    existingUser.StudentId = enrollmentStudent.Username;
                    existingUser.FirstName = !string.IsNullOrWhiteSpace(firstName) ? firstName : existingUser.FirstName;
                    existingUser.LastName = !string.IsNullOrWhiteSpace(lastName) ? lastName : existingUser.LastName;
                    existingUser.MiddleName = !string.IsNullOrWhiteSpace(middleName) ? middleName : existingUser.MiddleName;
                    existingUser.Role = "student";
                    existingUser.IsActive = true;
                    existingUser.UpdatedAt = DateTime.UtcNow;
                    
                    await UpdateUserAsync(existingUser);

                    // Update or create student profile
                    await SyncStudentProfileAsync(existingUser._id, enrollmentStudent.Username, contactNumber, fullName);
                    
                    Console.WriteLine($"✅ Updated user from enrollment system: {existingUser.Email}");
                    return existingUser;
                }

                // Create new user from enrollment data
                var newUser = new User
                {
                    _id = ObjectId.GenerateNewId(),
                    StudentId = enrollmentStudent.Username,
                    Email = enrollmentStudent.Email.Trim().ToLowerInvariant(),
                    PasswordHash = enrollmentStudent.PasswordHash,
                    FirstName = firstName,
                    LastName = lastName,
                    MiddleName = middleName,
                    Role = "student",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    FailedLoginAttempts = 0,
                    LockoutEndTime = null,
                    IsRestricted = false
                };

                await _users.InsertOneAsync(newUser);

                // Create student profile
                await SyncStudentProfileAsync(newUser._id, enrollmentStudent.Username, contactNumber, fullName);
                
                Console.WriteLine($"✅ Synced user from enrollment system: {newUser.Email}");
                
                return newUser;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing user from enrollment system: {ex.Message}");
                return null;
            }
        }

        private async Task SyncStudentProfileAsync(ObjectId userId, string studentNumber, string contactNumber, string fullName)
        {
            try
            {
                var existingProfile = await _studentProfiles.Find(sp => sp.UserId == userId).FirstOrDefaultAsync();
                
                if (existingProfile != null)
                {
                    // Update existing profile
                    var update = Builders<StudentProfile>.Update
                        .Set(sp => sp.StudentNumber, studentNumber)
                        .Set(sp => sp.ContactNumber, !string.IsNullOrWhiteSpace(contactNumber) ? contactNumber : existingProfile.ContactNumber)
                        .Set(sp => sp.FullName, !string.IsNullOrWhiteSpace(fullName) ? fullName : existingProfile.FullName)
                        .Set(sp => sp.UpdatedAt, DateTime.UtcNow);
                    
                    await _studentProfiles.UpdateOneAsync(sp => sp.UserId == userId, update);
                }
                else
                {
                    // Create new profile
                    var newProfile = new StudentProfile
                    {
                        _id = ObjectId.GenerateNewId(),
                        UserId = userId,
                        StudentNumber = studentNumber,
                        ContactNumber = contactNumber ?? string.Empty,
                        FullName = fullName,
                        IsEnrolled = true,
                        IsFlagged = false,
                        BorrowingLimit = 3,
                        TotalPenalties = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    await _studentProfiles.InsertOneAsync(newProfile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing student profile: {ex.Message}");
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
                return await _users.Find(u => u.Email == normalized).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user by email: {ex.Message}");
                return null;
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _users.Find(_ => true).ToListAsync();
        }

        public async Task<User> GetUserByIdAsync(string id)
        {
            if (ObjectId.TryParse(id, out ObjectId objectId))
            {
                return await _users.Find(u => u._id == objectId).FirstOrDefaultAsync();
            }
            return null;
        }

        public async Task<StudentProfile> GetStudentProfileAsync(string userId)
        {
            if (ObjectId.TryParse(userId, out ObjectId objectId))
            {
                return await _studentProfiles.Find(sp => sp.UserId == objectId).FirstOrDefaultAsync();
            }
            return null;
        }

        public async Task<StudentAccountViewModel> GetStudentAccountAsync(string userId)
        {
            if (!ObjectId.TryParse(userId, out ObjectId objectId))
            {
                return null;
            }

            var user = await _users.Find(u => u._id == objectId).FirstOrDefaultAsync();
            if (user == null)
            {
                return null;
            }

            var studentProfile = await _studentProfiles.Find(sp => sp.UserId == objectId).FirstOrDefaultAsync();

            // Always try to sync from enrollment system to ensure data is up-to-date
            var enrollmentRequest = await _enrollmentSystemService.GetLatestEnrollmentRequestByEmailAsync(user.Email);
            
            string contactNumber = string.Empty;
            string fullName = string.Empty;
            string firstName = string.Empty;
            string lastName = string.Empty;
            
            if (enrollmentRequest != null)
            {
                fullName = enrollmentRequest.FullName ?? string.Empty;
                
                if (enrollmentRequest.ExtraFields != null)
                {
                    enrollmentRequest.ExtraFields.TryGetValue("Student.ContactNumber", out contactNumber);
                    enrollmentRequest.ExtraFields.TryGetValue("Student.FirstName", out firstName);
                    enrollmentRequest.ExtraFields.TryGetValue("Student.LastName", out lastName);
                }
                
                // If full name is available but not split, try to parse it
                if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(fullName))
                {
                    var nameParts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (nameParts.Length >= 1) firstName = nameParts[0];
                    if (nameParts.Length >= 2) lastName = nameParts[nameParts.Length - 1];
                }

                // Update user's name from enrollment system
                if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                {
                    if (!string.IsNullOrWhiteSpace(firstName) && user.FirstName != firstName)
                    {
                        user.FirstName = firstName;
                    }
                    if (!string.IsNullOrWhiteSpace(lastName) && user.LastName != lastName)
                    {
                        user.LastName = lastName;
                    }
                    await UpdateUserAsync(user);
                }

                // Sync student profile with enrollment data
                if (studentProfile == null)
                {
                    var newProfile = new StudentProfile
                    {
                        _id = ObjectId.GenerateNewId(),
                        UserId = objectId,
                        StudentNumber = !string.IsNullOrWhiteSpace(user.StudentId) ? user.StudentId : string.Empty,
                        ContactNumber = contactNumber ?? string.Empty,
                        FullName = !string.IsNullOrWhiteSpace(fullName) ? fullName : user.FullName,
                        IsEnrolled = true,
                        IsFlagged = false,
                        BorrowingLimit = 3,
                        TotalPenalties = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _studentProfiles.InsertOneAsync(newProfile);
                    studentProfile = newProfile;
                    Console.WriteLine($"✅ Created student profile with contact number: {contactNumber}");
                }
                else
                {
                    // Always update contact number from enrollment system if available
                    var update = Builders<StudentProfile>.Update
                        .Set(sp => sp.StudentNumber, !string.IsNullOrWhiteSpace(user.StudentId) ? user.StudentId : studentProfile.StudentNumber)
                        .Set(sp => sp.FullName, !string.IsNullOrWhiteSpace(fullName) ? fullName : studentProfile.FullName)
                        .Set(sp => sp.UpdatedAt, DateTime.UtcNow);
                    
                    // Update contact number if we have it from enrollment system
                    if (!string.IsNullOrWhiteSpace(contactNumber))
                    {
                        update = update.Set(sp => sp.ContactNumber, contactNumber);
                        Console.WriteLine($"✅ Updating student profile contact number: {contactNumber}");
                    }
                    
                    await _studentProfiles.UpdateOneAsync(sp => sp.UserId == objectId, update);
                    
                    // Refresh profile
                    studentProfile = await _studentProfiles.Find(sp => sp.UserId == objectId).FirstOrDefaultAsync();
                }
            }

            // Get penalty history using unified penalty system
            var penaltyService = new PenaltyService(_mongoDbService);
            var penalties = await penaltyService.GetUserPenaltiesAsync(userId);
            var unpaidPenalties = penalties.Where(p => !p.IsPaid).Sum(p => p.Amount);
            var totalPenalties = penalties.Sum(p => p.Amount);

            // Get overdue books count (only Borrowed status can be overdue since DueDate is set when book is picked up)
            var reservations = _mongoDbService.GetCollection<Reservation>("Reservations");
            var overdueBooksCount = await reservations.CountDocumentsAsync(r => 
                r.UserId == userId && 
                r.Status == "Borrowed" &&
                r.DueDate.HasValue && 
                r.DueDate.Value < DateTime.UtcNow);

            // Convert Penalty to PenaltyRecord for backward compatibility
            var penaltyHistory = penalties.Select(p => new PenaltyRecord
            {
                _id = p._id,
                UserId = p.UserId,
                BookTitle = p.BookTitle,
                PenaltyAmount = p.Amount,
                Reason = p.Description,
                Condition = p.PenaltyType == "Damage" ? "Damage" : p.PenaltyType == "Lost" ? "Lost" : "Good",
                DaysLate = p.PenaltyType == "Late" ? 1 : 0, // Simplified for display
                IsPaid = p.IsPaid,
                PaidAt = p.PaymentDate,
                CreatedAt = p.CreatedDate
            }).ToList();

            var viewModel = new StudentAccountViewModel
            {
                UserId = user._id.ToString(),
                FullName = user.FullName,
                Email = user.Email,
                ContactNumber = !string.IsNullOrWhiteSpace(studentProfile?.ContactNumber) ? studentProfile.ContactNumber : (!string.IsNullOrWhiteSpace(contactNumber) ? contactNumber : "N/A"),
                StudentNumber = !string.IsNullOrWhiteSpace(studentProfile?.StudentNumber) ? studentProfile.StudentNumber : (!string.IsNullOrWhiteSpace(user.StudentId) ? user.StudentId : "N/A"),
                TotalPenalties = totalPenalties,
                PenaltyHistory = penaltyHistory,
                UnpaidPenalties = unpaidPenalties,
                PenaltyAmount = unpaidPenalties, // Legacy field
                OverdueBookTitle = penaltyHistory.FirstOrDefault()?.BookTitle ?? "None",
                PenaltyDate = penaltyHistory.FirstOrDefault()?.CreatedAt,
                OverdueBooksCount = (int)overdueBooksCount
            };

            return viewModel;
        }

        public async Task<bool> AddPenaltyToStudentAsync(string userId, decimal penaltyAmount, string bookTitle, string condition, int daysLate)
        {
            try
            {
                Console.WriteLine($"[UserService] Adding penalty of ₱{penaltyAmount} to user {userId}");

                if (!ObjectId.TryParse(userId, out ObjectId userObjectId))
                {
                    Console.WriteLine($"[UserService] Invalid userId format");
                    return false;
                }

                // Use the unified PenaltyService instead of creating separate PenaltyRecord
                var penaltyService = new PenaltyService(_mongoDbService);
                
                // Get user and student profile for penalty creation
                var user = await _users.Find(u => u._id == userObjectId).FirstOrDefaultAsync();
                var studentProfile = await _studentProfiles.Find(sp => sp.UserId == userObjectId).FirstOrDefaultAsync();
                
                if (user == null || studentProfile == null)
                {
                    Console.WriteLine($"[UserService] User or student profile not found");
                    return false;
                }

                // Create penalty using the unified system
                var penalty = new Penalty
                {
                    _id = ObjectId.GenerateNewId(),
                    UserId = userObjectId,
                    StudentNumber = studentProfile.StudentNumber,
                    StudentName = user.FullName,
                    BookTitle = bookTitle,
                    PenaltyType = GetPenaltyType(condition, daysLate),
                    Amount = penaltyAmount,
                    Description = GetPenaltyReason(condition, daysLate),
                    IsPaid = false,
                    CreatedDate = DateTime.UtcNow,
                    Remarks = $"Condition: {condition}, Days Late: {daysLate}"
                };

                var success = await penaltyService.CreatePenaltyAsync(penalty);
                
                if (success)
                {
                    Console.WriteLine($"[UserService] Penalty created successfully: {penalty._id}");
                    
                    var profileUpdate = Builders<StudentProfile>.Update
                        .Inc(sp => sp.TotalPenalties, penaltyAmount)
                        .Set(sp => sp.UpdatedAt, DateTime.UtcNow);

                    var profileResult = await _studentProfiles.UpdateOneAsync(
                        sp => sp.UserId == userObjectId,
                        profileUpdate
                    );

                    Console.WriteLine($"[UserService] Profile updated. Matched: {profileResult.MatchedCount}, Modified: {profileResult.ModifiedCount}");
                    return profileResult.MatchedCount > 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService] Error adding penalty: {ex.Message}");
                return false;
            }
        }

        private string GetPenaltyReason(string condition, int daysLate)
        {
            var reasons = new List<string>();

            if (daysLate > 0)
            {
                reasons.Add($"Late return ({daysLate} days)");
            }

            if (condition == "Damage")
            {
                reasons.Add("Book damaged");
            }
            else if (condition == "Lost")
            {
                reasons.Add("Book lost");
            }

            return reasons.Count > 0 ? string.Join(", ", reasons) : "Penalty applied";
        }

        private string GetPenaltyType(string condition, int daysLate)
        {
            if (condition == "Lost")
                return "Lost";
            else if (condition.StartsWith("Damaged-"))
                return "Damage";
            else if (daysLate > 0)
                return "Late";
            else
                return "Other";
        }

        public async Task<List<PenaltyRecord>> GetStudentPenaltyHistoryAsync(string userId)
        {
            try
            {
                // Use unified penalty system
                var penaltyService = new PenaltyService(_mongoDbService);
                var penalties = await penaltyService.GetUserPenaltiesAsync(userId);
                
                // Convert Penalty to PenaltyRecord for backward compatibility
                return penalties.Select(p => new PenaltyRecord
                {
                    _id = p._id,
                    UserId = p.UserId,
                    BookTitle = p.BookTitle,
                    PenaltyAmount = p.Amount,
                    Reason = p.Description,
                    Condition = p.PenaltyType == "Damage" ? "Damage" : p.PenaltyType == "Lost" ? "Lost" : "Good",
                    DaysLate = p.PenaltyType == "Late" ? 1 : 0, // Simplified for display
                    IsPaid = p.IsPaid,
                    PaidAt = p.PaymentDate,
                    CreatedAt = p.CreatedDate
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService] Error getting penalty history: {ex.Message}");
                return new List<PenaltyRecord>();
            }
        }

        public async Task<decimal> GetStudentTotalPenaltiesAsync(string userId)
        {
            try
            {
                // Use unified penalty system
                var penaltyService = new PenaltyService(_mongoDbService);
                return await penaltyService.GetUserTotalPendingPenaltiesAsync(userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService] Error getting total penalties: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> UpdatePasswordAsync(string userId, string newPassword)
        {
            if (!ObjectId.TryParse(userId, out ObjectId objectId))
            {
                return false;
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
            var update = Builders<User>.Update.Set(u => u.PasswordHash, hashedPassword);
            var result = await _users.UpdateOneAsync(u => u._id == objectId, update);

            return result.ModifiedCount > 0;
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            return await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
        }

        public async Task UpdateUserAsync(User user)
        {
            var filter = Builders<User>.Filter.Eq(u => u._id, user._id);
            await _users.ReplaceOneAsync(filter, user);
        }

        // ====== FORGOT PASSWORD METHODS ======

        public async Task<User> GetUserByUsernameAndEmailAsync(string username, string email)
        {
            return await _users.Find(u => u.Username == username && u.Email == email).FirstOrDefaultAsync();
        }

        public async Task<string> CreatePasswordResetTokenAsync(string userId)
        {
            if (!ObjectId.TryParse(userId, out ObjectId objectId))
            {
                return null;
            }

            // Generate a secure random token (64 characters)
            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

            var resetToken = new PasswordResetToken
            {
                UserId = objectId,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // Token valid for 1 hour
                IsUsed = false
            };

            await _resetTokens.InsertOneAsync(resetToken);
            return token;
        }

        public async Task<PasswordResetToken> ValidateResetTokenAsync(string token)
        {
            var resetToken = await _resetTokens
                .Find(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            return resetToken;
        }

        public async Task<bool> ResetPasswordWithTokenAsync(string token, string newPassword)
        {
            var resetToken = await ValidateResetTokenAsync(token);
            if (resetToken == null)
            {
                return false;
            }

            // Hash the new password
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // Update user password
            var update = Builders<User>.Update.Set(u => u.PasswordHash, hashedPassword);
            var result = await _users.UpdateOneAsync(u => u._id == resetToken.UserId, update);

            if (result.ModifiedCount > 0)
            {
                // Mark token as used
                var tokenUpdate = Builders<PasswordResetToken>.Update.Set(t => t.IsUsed, true);
                await _resetTokens.UpdateOneAsync(t => t._id == resetToken._id, tokenUpdate);
                return true;
            }

            return false;
        }

        public async Task<bool> RestrictUserAsync(string userId, string reason)
        {
            try
            {
                if (ObjectId.TryParse(userId, out ObjectId objectId))
                {
                    var update = Builders<User>.Update
                        .Set(u => u.IsRestricted, true)
                        .Set(u => u.RestrictionReason, reason)
                        .Set(u => u.RestrictionDate, DateTime.UtcNow);

                    var result = await _users.UpdateOneAsync(u => u._id == objectId, update);
                    return result.ModifiedCount > 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RestrictUserAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UnrestrictUserAsync(string userId)
        {
            try
            {
                if (ObjectId.TryParse(userId, out ObjectId objectId))
                {
                    var update = Builders<User>.Update
                        .Set(u => u.IsRestricted, false)
                        .Unset(u => u.RestrictionReason)
                        .Unset(u => u.RestrictionDate);

                    var result = await _users.UpdateOneAsync(u => u._id == objectId, update);
                    return result.ModifiedCount > 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UnrestrictUserAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<User>> GetAllStudentsAsync()
        {
            var filter = Builders<User>.Filter.Eq(u => u.Role, "student");
            return await _users.Find(filter)
                .SortBy(u => u.FullName)
                .ToListAsync();
        }
    }
}