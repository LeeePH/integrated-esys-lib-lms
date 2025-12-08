using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class ReturnService : IReturnService
    {
        private readonly IMongoCollection<ReturnTransaction> _returns;
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IMongoCollection<Book> _books;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Penalty> _penalties;
        private readonly IMongoCollection<StudentProfile> _studentProfiles;
        private readonly INotificationService _notificationService;
        private readonly IReservationService _reservationService;

        public ReturnService(IMongoDbService mongoDbService, INotificationService notificationService, IReservationService reservationService)
        {
            _returns = mongoDbService.GetCollection<ReturnTransaction>("Returns");
            _reservations = mongoDbService.GetCollection<Reservation>("Reservations");
            _books = mongoDbService.GetCollection<Book>("Books");
            _users = mongoDbService.GetCollection<User>("Users");
            _penalties = mongoDbService.GetCollection<Penalty>("Penalties");
            _studentProfiles = mongoDbService.GetCollection<StudentProfile>("StudentProfiles");
            _notificationService = notificationService;
            _reservationService = reservationService;
        }

        public async Task<bool> ProcessReturnAsync(ReturnTransaction returnTransaction)
        {
            try
            {
                Console.WriteLine("[ReturnService] Starting ProcessReturnAsync...");
                Console.WriteLine($"[ReturnService] Received BookCondition: {returnTransaction.BookCondition}");
                Console.WriteLine($"[ReturnService] Received DamageType: {returnTransaction.DamageType}");
                Console.WriteLine($"[ReturnService] Received DamagePenalty: {returnTransaction.DamagePenalty}");
                Console.WriteLine($"[ReturnService] Received TotalPenalty: {returnTransaction.TotalPenalty}");

                // ✅ CALCULATE OVERDUE PENALTY (₱10 per minute if past due date)
                if (returnTransaction.DueDate < returnTransaction.ReturnDate)
                {
                    var minutesLate = (int)Math.Ceiling((returnTransaction.ReturnDate - returnTransaction.DueDate).TotalMinutes);
                    var calculatedLateFee = minutesLate * 10m; // ₱10 per minute

                    Console.WriteLine($"[ReturnService] Book is {minutesLate} minute(s) overdue");
                    Console.WriteLine($"[ReturnService] Calculated late fee: ₱{calculatedLateFee} ({minutesLate} minutes × ₱10)");
                    Console.WriteLine($"[ReturnService] Frontend sent late fee: ₱{returnTransaction.LateFees}");

                    // Use the calculated value if it's higher than what frontend sent
                    if (calculatedLateFee > returnTransaction.LateFees)
                    {
                        Console.WriteLine($"[ReturnService] Using calculated late fee: ₱{calculatedLateFee}");
                        returnTransaction.LateFees = calculatedLateFee;
                        returnTransaction.DaysLate = minutesLate; // Store minutes in DaysLate field for compatibility
                    }
                    else
                    {
                        // If frontend already provided DaysLate (which now represents minutes), ensure it's used
                        returnTransaction.DaysLate = returnTransaction.DaysLate > 0 ? returnTransaction.DaysLate : minutesLate;
                    }
                }

                // ✅ Use penalties calculated by frontend (they now use data-penalty attributes)
                // Only recalculate if damage penalty is 0 but damage type is specified
                if (returnTransaction.BookCondition.StartsWith("Damaged-") && returnTransaction.DamagePenalty == 0)
                {
                    returnTransaction.DamagePenalty = returnTransaction.BookCondition switch
                    {
                        "Damaged-Minor" => 50m,
                        "Damaged-Moderate" => 100m,
                        "Damaged-Major" => 200m,
                        _ => 0m
                    };
                    Console.WriteLine($"[ReturnService] Auto-calculated DamagePenalty: ₱{returnTransaction.DamagePenalty}");
                }
                
                // ✅ Reset PenaltyAmount based on condition
                if (returnTransaction.BookCondition == "Lost")
                {
                    if (returnTransaction.PenaltyAmount == 0)
                    {
                        returnTransaction.PenaltyAmount = 2000m;
                    }
                    Console.WriteLine($"[ReturnService] Set Lost book penalty: ₱{returnTransaction.PenaltyAmount}");
                }
                else
                {
                    // For non-lost conditions, PenaltyAmount should be 0
                    returnTransaction.PenaltyAmount = 0m;
                    Console.WriteLine($"[ReturnService] Reset PenaltyAmount to 0 for condition: {returnTransaction.BookCondition}");
                }

                // ✅ RECALCULATE TOTAL PENALTY (in case frontend calculation was wrong)
                returnTransaction.TotalPenalty = returnTransaction.LateFees +
                                                 returnTransaction.DamagePenalty +
                                                 returnTransaction.PenaltyAmount;

                Console.WriteLine($"[ReturnService] Final TotalPenalty: ₱{returnTransaction.TotalPenalty}");
                Console.WriteLine($"[ReturnService] Breakdown - Late: ₱{returnTransaction.LateFees}, Damage: ₱{returnTransaction.DamagePenalty}, Lost: ₱{returnTransaction.PenaltyAmount}");

                // Get student information for the return transaction
                var user = await _users.Find(u => u._id == returnTransaction.UserId).FirstOrDefaultAsync();
                var studentProfile = await _studentProfiles.Find(sp => sp.UserId == returnTransaction.UserId).FirstOrDefaultAsync();

                // Populate student information
                returnTransaction.StudentNumber = studentProfile?.StudentNumber ?? "N/A";
                returnTransaction.StudentName = user?.FullName ?? "Unknown Student";

                // 1. Insert return transaction
                returnTransaction._id = ObjectId.GenerateNewId();
                returnTransaction.CreatedAt = DateTime.UtcNow;

                Console.WriteLine($"[ReturnService] Inserting return transaction with ID: {returnTransaction._id}");
                await _returns.InsertOneAsync(returnTransaction);
                Console.WriteLine("[ReturnService] Return transaction inserted successfully");

                // 2. Update reservation status
                Console.WriteLine($"[ReturnService] Updating reservation: {returnTransaction.ReservationId}");
                // Determine status based on book condition
                string reservationStatus = returnTransaction.BookCondition switch
                {
                    "Good" => "Returned",
                    "Damaged-Minor" => "Damaged",
                    "Damaged-Moderate" => "Damaged",
                    "Damaged-Major" => "Damaged",
                    "Lost" => "Lost",
                    _ => "Returned"
                };

                var reservationUpdate = Builders<Reservation>.Update
                    .Set(r => r.Status, reservationStatus)  // ← NOW USES ACTUAL CONDITION
                    .Set(r => r.ReturnDate, DateTime.UtcNow);

                var reservationFilter = Builders<Reservation>.Filter.Eq("_id", returnTransaction.ReservationId.ToString());
                var existingReservation = await _reservations.Find(reservationFilter).FirstOrDefaultAsync();

                if (existingReservation == null)
                {
                    Console.WriteLine($"[ReturnService] ERROR: Reservation not found with ID: {returnTransaction.ReservationId}");
                    await _returns.DeleteOneAsync(r => r._id == returnTransaction._id);
                    return false;
                }

                Console.WriteLine($"[ReturnService] Found reservation with status: {existingReservation.Status}");

                // CRITICAL: Only allow returns for "Borrowed" books
                if (existingReservation.Status != "Borrowed")
                {
                    Console.WriteLine($"[ReturnService] ERROR: Cannot return book with status '{existingReservation.Status}'. Only 'Borrowed' books can be returned.");
                    await _returns.DeleteOneAsync(r => r._id == returnTransaction._id);
                    return false;
                }

                var reservationResult = await _reservations.UpdateOneAsync(
                    reservationFilter,
                    reservationUpdate
                );

                Console.WriteLine($"[ReturnService] Reservation update matched: {reservationResult.MatchedCount}, modified: {reservationResult.ModifiedCount}");

                if (reservationResult.ModifiedCount == 0)
                {
                    Console.WriteLine("[ReturnService] ERROR: Failed to update reservation status");
                    await _returns.DeleteOneAsync(r => r._id == returnTransaction._id);
                    return false;
                }

                // 3. Update book inventory based on condition
                Console.WriteLine($"[ReturnService] Updating book inventory. Condition: {returnTransaction.BookCondition}");

                var bookFilter = Builders<Book>.Filter.Eq(b => b._id, returnTransaction.BookId);
                var existingBook = await _books.Find(bookFilter).FirstOrDefaultAsync();

                if (existingBook == null)
                {
                    Console.WriteLine($"[ReturnService] ERROR: Book not found with ID: {returnTransaction.BookId}");
                    await _returns.DeleteOneAsync(r => r._id == returnTransaction._id);
                    return false;
                }

                Console.WriteLine($"[ReturnService] Found book: {existingBook.Title}, Available: {existingBook.AvailableCopies}, Total: {existingBook.TotalCopies}");

                if (returnTransaction.BookCondition != "Lost")
                {
                    // Increment available copies
                    var bookUpdate = Builders<Book>.Update
                        .Inc(b => b.AvailableCopies, 1)
                        .Set(b => b.UpdatedAt, DateTime.UtcNow);
                    
                    var bookResult = await _books.UpdateOneAsync(bookFilter, bookUpdate);
                    Console.WriteLine($"[ReturnService] Increased available copies. Matched: {bookResult.MatchedCount}, Modified: {bookResult.ModifiedCount}");
                    
                    if (bookResult.ModifiedCount == 0)
                    {
                        Console.WriteLine($"[ReturnService] WARNING: Failed to increment available copies for book {returnTransaction.BookId}");
                    }
                    else
                    {
                        // Verify the increment worked
                        var updatedBook = await _books.Find(bookFilter).FirstOrDefaultAsync();
                        Console.WriteLine($"[ReturnService] Book after increment - Available: {updatedBook?.AvailableCopies ?? -1}, Total: {updatedBook?.TotalCopies ?? -1}");

                        // If a copy became available, try to auto-approve the next student in queue and hold the copy
                        try
                        {
                            var bookIdStr = returnTransaction.BookId.ToString();
                            var autoApproved = await _reservationService.ApproveNextAndHoldAsync(bookIdStr);
                            Console.WriteLine($"[ReturnService] Auto-approval after return for book {bookIdStr}: {(autoApproved ? "Approved a pending reservation" : "No pending approvals or none available")}");
                            
                            // Verify final state
                            var finalBook = await _books.Find(bookFilter).FirstOrDefaultAsync();
                            Console.WriteLine($"[ReturnService] Book after auto-approval - Available: {finalBook?.AvailableCopies ?? -1}, Total: {finalBook?.TotalCopies ?? -1}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ReturnService] Error during auto-approve: {ex.Message}");
                            Console.WriteLine($"[ReturnService] Stack trace: {ex.StackTrace}");
                        }
                    }
                }
                else
                {
                    var bookUpdate = Builders<Book>.Update.Inc(b => b.TotalCopies, -1);
                    var bookResult = await _books.UpdateOneAsync(bookFilter, bookUpdate);
                    Console.WriteLine($"[ReturnService] Decreased total copies (lost). Matched: {bookResult.MatchedCount}, Modified: {bookResult.ModifiedCount}");
                }

                // 4. Create penalty records if there are penalties
                if (returnTransaction.TotalPenalty > 0)
                {
                    Console.WriteLine($"[ReturnService] Creating penalty records. Total: ₱{returnTransaction.TotalPenalty}");

                    // Late fee penalty
                    if (returnTransaction.LateFees > 0)
                    {
                        var latePenalty = new Penalty
                        {
                            _id = ObjectId.GenerateNewId(),
                            UserId = returnTransaction.UserId,
                            StudentNumber = returnTransaction.StudentNumber,
                            StudentName = returnTransaction.StudentName,
                            ReservationId = returnTransaction.ReservationId,
                            BookId = returnTransaction.BookId,
                            BookTitle = returnTransaction.BookTitle,
                            PenaltyType = "Late",
                            Amount = returnTransaction.LateFees,
                            Description = $"{returnTransaction.DaysLate} min(s) = ₱{returnTransaction.LateFees}",
                            IsPaid = false,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = returnTransaction.ProcessedBy,
                            Remarks = $"Late by {returnTransaction.DaysLate} minute(s) = ₱{returnTransaction.LateFees}"
                        };
                        await _penalties.InsertOneAsync(latePenalty);
                        await _notificationService.CreatePenaltyNotificationAsync(
                            returnTransaction.UserId.ToString(), 
                            returnTransaction.BookTitle, 
                            returnTransaction.LateFees, 
                            "Late");
                        Console.WriteLine($"[ReturnService] Created late fee penalty: ₱{returnTransaction.LateFees}");
                    }

                    // Damage penalty
                    if (returnTransaction.BookCondition.StartsWith("Damaged-") && returnTransaction.DamagePenalty > 0)
                    {
                        var damageReason = returnTransaction.BookCondition switch
                        {
                            "Damaged-Minor" => "Minor damage to book",
                            "Damaged-Moderate" => "Moderate damage to book",
                            "Damaged-Major" => "Major damage to book",
                            _ => "Book damage"
                        };

                        var damagePenalty = new Penalty
                        {
                            _id = ObjectId.GenerateNewId(),
                            UserId = returnTransaction.UserId,
                            StudentNumber = returnTransaction.StudentNumber,
                            StudentName = returnTransaction.StudentName,
                            ReservationId = returnTransaction.ReservationId,
                            BookId = returnTransaction.BookId,
                            BookTitle = returnTransaction.BookTitle,
                            PenaltyType = "Damage",
                            Amount = returnTransaction.DamagePenalty,
                            Description = damageReason,
                            IsPaid = false,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = returnTransaction.ProcessedBy,
                            Remarks = $"Damage type: {returnTransaction.BookCondition}"
                        };
                        await _penalties.InsertOneAsync(damagePenalty);
                        await _notificationService.CreatePenaltyNotificationAsync(
                            returnTransaction.UserId.ToString(), 
                            returnTransaction.BookTitle, 
                            returnTransaction.DamagePenalty, 
                            "Damage");
                        Console.WriteLine($"[ReturnService] Created damage penalty: ₱{returnTransaction.DamagePenalty} ({returnTransaction.BookCondition})");
                    }

                    // Lost book penalty
                    if (returnTransaction.BookCondition == "Lost" && returnTransaction.PenaltyAmount > 0)
                    {
                        var lostPenalty = new Penalty
                        {
                            _id = ObjectId.GenerateNewId(),
                            UserId = returnTransaction.UserId,
                            StudentNumber = returnTransaction.StudentNumber,
                            StudentName = returnTransaction.StudentName,
                            ReservationId = returnTransaction.ReservationId,
                            BookId = returnTransaction.BookId,
                            BookTitle = returnTransaction.BookTitle,
                            PenaltyType = "Lost",
                            Amount = returnTransaction.PenaltyAmount,
                            Description = "Lost book",
                            IsPaid = false,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = returnTransaction.ProcessedBy,
                            Remarks = "Book reported as lost"
                        };
                        await _penalties.InsertOneAsync(lostPenalty);
                        await _notificationService.CreatePenaltyNotificationAsync(
                            returnTransaction.UserId.ToString(), 
                            returnTransaction.BookTitle, 
                            returnTransaction.PenaltyAmount, 
                            "Lost");
                        Console.WriteLine($"[ReturnService] Created lost book penalty: ₱{returnTransaction.PenaltyAmount}");
                    }

                    Console.WriteLine($"[ReturnService] All penalty records created successfully");
                    
                    // Update student profile total penalties
                    var profileUpdate = Builders<StudentProfile>.Update
                        .Inc(sp => sp.TotalPenalties, returnTransaction.TotalPenalty)
                        .Set(sp => sp.UpdatedAt, DateTime.UtcNow);

                    var profileResult = await _studentProfiles.UpdateOneAsync(
                        sp => sp.UserId == returnTransaction.UserId,
                        profileUpdate
                    );

                    Console.WriteLine($"[ReturnService] Student profile updated. Matched: {profileResult.MatchedCount}, Modified: {profileResult.ModifiedCount}");

                    // Update user penalty status
                    var userUpdate = Builders<User>.Update
                        .Set(u => u.HasPendingPenalties, true)
                        .Set(u => u.TotalPendingPenalties, returnTransaction.TotalPenalty)
                        .Set(u => u.PenaltyRestrictionDate, DateTime.UtcNow);

                    var userResult = await _users.UpdateOneAsync(
                        u => u._id == returnTransaction.UserId,
                        userUpdate
                    );

                    Console.WriteLine($"[ReturnService] User penalty status updated. Matched: {userResult.MatchedCount}, Modified: {userResult.ModifiedCount}");
                }
                else
                {
                    Console.WriteLine("[ReturnService] No penalties to record");
                }

                Console.WriteLine("[ReturnService] ProcessReturnAsync completed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReturnService] EXCEPTION in ProcessReturnAsync: {ex.Message}");
                Console.WriteLine($"[ReturnService] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ReturnService] InnerException: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public async Task<List<ReturnTransaction>> GetAllReturnsAsync()
        {
            return await _returns.Find(_ => true).SortByDescending(r => r.CreatedAt).ToListAsync();
        }

        public async Task<ReturnTransaction?> GetReturnByIdAsync(string returnId)
        {
            if (ObjectId.TryParse(returnId, out ObjectId id))
                return await _returns.Find(r => r._id == id).FirstOrDefaultAsync();

            return null;
        }

        public async Task<ReturnTransaction?> SearchReturnAsync(string searchTerm)
        {
            var filter = Builders<ReturnTransaction>.Filter.Regex(
                r => r.BookTitle, new BsonRegularExpression(searchTerm, "i"));

            return await _returns.Find(filter).SortByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
        }

        public async Task<List<ReturnTransaction>> GetUserReturnsAsync(string userId)
        {
            if (!ObjectId.TryParse(userId, out ObjectId id))
                return new List<ReturnTransaction>();

            return await _returns.Find(r => r.UserId == id)
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdatePaymentStatusAsync(string returnId, string status)
        {
            if (!ObjectId.TryParse(returnId, out ObjectId id))
                return false;

            var update = Builders<ReturnTransaction>.Update.Set(r => r.PaymentStatus, status);
            var result = await _returns.UpdateOneAsync(r => r._id == id, update);
            return result.ModifiedCount > 0;
        }
    }
}