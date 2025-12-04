using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SystemLibrary.Services
{
    public interface IPenaltyService
    {
        Task<bool> CreatePenaltyAsync(Penalty penalty);
        Task<List<Penalty>> GetUserPenaltiesAsync(string userId);
        Task<List<Penalty>> GetAllPenaltiesAsync();
        Task<bool> MarkPenaltyAsPaidAsync(string penaltyId, string processedBy);
        Task<bool> RemovePenaltyAsync(string penaltyId);
        Task<bool> UpdateUserPenaltyStatusAsync(string userId);
        Task<decimal> GetUserTotalPendingPenaltiesAsync(string userId);
        Task ProcessUserOverduePenaltiesAsync(string userId);
    }

    public class PenaltyService : IPenaltyService
    {
        private readonly IMongoCollection<Penalty> _penalties;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<StudentProfile> _studentProfiles;
        private readonly IMongoDbService _mongoDbService;

        public PenaltyService(IMongoDbService mongoDbService)
        {
            _mongoDbService = mongoDbService;
            _penalties = mongoDbService.GetCollection<Penalty>("Penalties");
            _users = mongoDbService.GetCollection<User>("Users");
            _studentProfiles = mongoDbService.GetCollection<StudentProfile>("StudentProfiles");
        }

        public async Task<bool> CreatePenaltyAsync(Penalty penalty)
        {
            try
            {
                await _penalties.InsertOneAsync(penalty);
                await UpdateUserPenaltyStatusAsync(penalty.UserId.ToString());
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating penalty: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Penalty>> GetUserPenaltiesAsync(string userId)
        {
            try
            {
                var objectId = ObjectId.Parse(userId);
                return await _penalties.Find(p => p.UserId == objectId)
                    .SortByDescending(p => p.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user penalties: {ex.Message}");
                return new List<Penalty>();
            }
        }

        public async Task<List<Penalty>> GetAllPenaltiesAsync()
        {
            try
            {
                return await _penalties.Find(_ => true)
                    .SortByDescending(p => p.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting all penalties: {ex.Message}");
                return new List<Penalty>();
            }
        }

        public async Task<bool> MarkPenaltyAsPaidAsync(string penaltyId, string processedBy)
        {
            try
            {
                var objectId = ObjectId.Parse(penaltyId);
                var update = Builders<Penalty>.Update
                    .Set(p => p.IsPaid, true)
                    .Set(p => p.PaymentDate, DateTime.UtcNow);

                var result = await _penalties.UpdateOneAsync(p => p._id == objectId, update);
                
                if (result.ModifiedCount > 0)
                {
                    // Get the penalty to update user status
                    var penalty = await _penalties.Find(p => p._id == objectId).FirstOrDefaultAsync();
                    if (penalty != null)
                    {
                        await UpdateUserPenaltyStatusAsync(penalty.UserId.ToString());
                        
                        // Create notification for penalty paid
                        var notificationService = new NotificationService(_mongoDbService);
                        await notificationService.CreatePenaltyPaidNotificationAsync(
                            penalty.UserId.ToString(), 
                            penalty.BookTitle, 
                            penalty.Amount);
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking penalty as paid: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemovePenaltyAsync(string penaltyId)
        {
            try
            {
                var objectId = ObjectId.Parse(penaltyId);
                var penalty = await _penalties.Find(p => p._id == objectId).FirstOrDefaultAsync();
                
                if (penalty != null)
                {
                    var result = await _penalties.DeleteOneAsync(p => p._id == objectId);
                    if (result.DeletedCount > 0)
                    {
                        await UpdateUserPenaltyStatusAsync(penalty.UserId.ToString());
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing penalty: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateUserPenaltyStatusAsync(string userId)
        {
            try
            {
                var objectId = ObjectId.Parse(userId);
                var totalPending = await GetUserTotalPendingPenaltiesAsync(userId);
                var hasPending = totalPending > 0;

                var update = Builders<User>.Update
                    .Set(u => u.HasPendingPenalties, hasPending)
                    .Set(u => u.TotalPendingPenalties, totalPending)
                    .Set(u => u.PenaltyRestrictionDate, hasPending ? DateTime.UtcNow : (DateTime?)null);

                var result = await _users.UpdateOneAsync(u => u._id == objectId, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user penalty status: {ex.Message}");
                return false;
            }
        }

        public async Task<decimal> GetUserTotalPendingPenaltiesAsync(string userId)
        {
            try
            {
                var objectId = ObjectId.Parse(userId);
                var pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match", new BsonDocument("user_id", objectId)),
                    new BsonDocument("$match", new BsonDocument("is_paid", false)),
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", BsonNull.Value },
                        { "total", new BsonDocument("$sum", "$amount") }
                    })
                };

                var result = await _penalties.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
                return result != null ? result["total"].AsDecimal : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user total pending penalties: {ex.Message}");
                return 0;
            }
        }

        public async Task ProcessUserOverduePenaltiesAsync(string userId)
        {
            try
            {
                var reservations = _mongoDbService.GetCollection<Reservation>("Reservations");
                var penalties = _mongoDbService.GetCollection<Penalty>("Penalties");
                var users = _mongoDbService.GetCollection<User>("Users");
                var studentProfiles = _mongoDbService.GetCollection<StudentProfile>("StudentProfiles");

                var userObjectId = ObjectId.Parse(userId);
                decimal penaltyPerMinute = 10m;

                // Find overdue reservations for this user
                // Only "Borrowed" status is tracked - overdue penalties start when the student picks up the book
                var overdueReservations = await reservations
                    .Find(r => r.UserId == userId && 
                               r.Status == "Borrowed" &&
                               r.DueDate.HasValue && 
                               r.DueDate.Value < DateTime.UtcNow)
                    .ToListAsync();

                foreach (var reservation in overdueReservations)
                {
                    if (!reservation.DueDate.HasValue) continue;

                    var minutesOverdue = (int)(DateTime.UtcNow - reservation.DueDate.Value).TotalMinutes;
                    if (minutesOverdue <= 0) continue;

                    var reservationObjectId = ObjectId.Parse(reservation._id);
                    var newPenaltyAmount = minutesOverdue * penaltyPerMinute;

                    // Check if penalty exists
                    var existingPenalty = await penalties
                        .Find(p => p.ReservationId == reservationObjectId && p.PenaltyType == "Overdue")
                        .FirstOrDefaultAsync();

                    if (existingPenalty != null)
                    {
                        // Update existing penalty
                        var update = Builders<Penalty>.Update
                            .Set(p => p.Amount, newPenaltyAmount)
                            .Set(p => p.Description, $"{minutesOverdue} min(s) = ₱{newPenaltyAmount}");

                        await penalties.UpdateOneAsync(p => p._id == existingPenalty._id, update);
                    }
                    else
                    {
                        // Create new penalty
                        var user = await users.Find(u => u._id == userObjectId).FirstOrDefaultAsync();
                        var studentProfile = await studentProfiles
                            .Find(sp => sp.UserId == userObjectId)
                            .FirstOrDefaultAsync();

                        if (user == null || studentProfile == null) continue;

                        var overduePenalty = new Penalty
                        {
                            _id = ObjectId.GenerateNewId(),
                            UserId = userObjectId,
                            StudentNumber = studentProfile.StudentNumber,
                            StudentName = user.FullName,
                            BookTitle = reservation.BookTitle,
                            ReservationId = reservationObjectId,
                            PenaltyType = "Overdue",
                            Amount = newPenaltyAmount,
                            Description = $"{minutesOverdue} min(s) = ₱{newPenaltyAmount}",
                            IsPaid = false,
                            CreatedDate = DateTime.UtcNow,
                            Remarks = $"{minutesOverdue} minute(s) overdue = ₱{newPenaltyAmount}"
                        };

                        await penalties.InsertOneAsync(overduePenalty);
                    }
                }

                // Update user penalty status
                await UpdateUserPenaltyStatusAsync(userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing user overdue penalties: {ex.Message}");
            }
        }
    }
}
