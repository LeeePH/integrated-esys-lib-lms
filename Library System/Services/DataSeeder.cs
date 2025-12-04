//using MongoDB.Driver;
//using SystemLibrary.Models;

//namespace SystemLibrary.Services
//{
//    public class DataSeeder
//    {
//        private readonly IMongoDbService _mongoDbService;

//        public DataSeeder(IMongoDbService mongoDbService)
//        {
//            _mongoDbService = mongoDbService;
//        }

//        public async Task SeedDataAsync()
//        {
//            await SeedUsersAsync();
//            await SeedStudentProfilesAsync();a

//            // Fix any existing books missing or with invalid AvailableCopies before seeding
//            await FixExistingBooksAsync();

//            await SeedBooksAsync();
//        }

//        private async Task SeedUsersAsync()
//        {
//            var usersCollection = _mongoDbService.GetCollection<User>("Users");

//            var existingUsersCount = await usersCollection.CountDocumentsAsync(_ => true);
//            if (existingUsersCount > 0) return;

//            var users = new List<User>
//            {
//                new User
//                {
//                    Username = "student1",
//                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("hashedpw123"),
//                    Email = "student1@example.com",
//                    FullName = "John Doe",
//                    Role = "student",
//                    IsActive = true,
//                    CreatedAt = DateTime.UtcNow
//                },
//                new User
//                {
//                    Username = "librarian1",
//                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("hashedlib123"),
//                    Email = "librarian1@example.com",
//                    FullName = "Libby Admin",
//                    Role = "librarian",
//                    IsActive = true,
//                    CreatedAt = DateTime.UtcNow
//                },
//                new User
//                {
//                    Username = "student2",
//                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
//                    Email = "student2@example.com",
//                    FullName = "Jane Smith",
//                    Role = "student",
//                    IsActive = true,
//                    CreatedAt = DateTime.UtcNow
//                },
//                new User
//                {
//                    Username = "student6",
//                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
//                    Email = "student6@example.com",
//                    FullName = "Student Six",
//                    Role = "student",
//                    IsActive = true,
//                    CreatedAt = DateTime.UtcNow
//                }
//            };

//            await usersCollection.InsertManyAsync(users);
//        }

//        private async Task SeedStudentProfilesAsync()
//        {
//            var studentProfilesCollection = _mongoDbService.GetCollection<StudentProfile>("StudentProfiles");
//            var usersCollection = _mongoDbService.GetCollection<User>("Users");

//            var existingProfilesCount = await studentProfilesCollection.CountDocumentsAsync(_ => true);
//            if (existingProfilesCount > 0) return;

//            var studentUsers = await usersCollection.Find(u => u.Role == "student").ToListAsync();

//            var studentProfiles = new List<StudentProfile>();

//            foreach (var user in studentUsers)
//            {
//                var profile = new StudentProfile
//                {
//                    UserId = user._id,
//                    IsEnrolled = true,
//                    IsFlagged = false,
//                    BorrowingLimit = 5,
//                    YearLevel = user.Username == "student1" ? "1st" : "2nd",
//                    Program = user.Username == "student1" ? "BSIT" : "BSCS",
//                    Department = "CCS"
//                };
//                studentProfiles.Add(profile);
//            }

//            if (studentProfiles.Any())
//            {
//                await studentProfilesCollection.InsertManyAsync(studentProfiles);
//            }
//        }

//        private async Task SeedBooksAsync()
//        {
//            var booksCollection = _mongoDbService.GetCollection<Book>("Books");

//            var existingBooksCount = await booksCollection.CountDocumentsAsync(_ => true);
//            if (existingBooksCount > 0) return;

//            var books = new List<Book>
//            {
//                new Book
//                {
//                    Title = "Intro to Programming",
//                    Author = "Smith",
//                    ISBN = "978-0001",
//                    Subject = "Computer Science",
//                    TotalCopies = 2,
//                    AvailableCopies = 2,    // Initialize AvailableCopies equal to TotalCopies
//                    CreatedAt = DateTime.UtcNow
//                },
//                new Book
//                {
//                    Title = "Data Structures and Algorithms",
//                    Author = "Johnson",
//                    ISBN = "978-0002",
//                    Subject = "Computer Science",
//                    TotalCopies = 3,
//                    AvailableCopies = 3,
//                    CreatedAt = DateTime.UtcNow
//                },
//                new Book
//                {
//                    Title = "Web Development Fundamentals",
//                    Author = "Brown",
//                    ISBN = "978-0003",
//                    Subject = "Computer Science",
//                    TotalCopies = 4,
//                    AvailableCopies = 4,
//                    CreatedAt = DateTime.UtcNow
//                },
//                new Book
//                {
//                    Title = "Database Management Systems",
//                    Author = "Davis",
//                    ISBN = "978-0004",
//                    Subject = "Computer Science",
//                    TotalCopies = 2,
//                    AvailableCopies = 2,
//                    CreatedAt = DateTime.UtcNow
//                },
//                new Book
//                {
//                    Title = "Software Engineering Principles",
//                    Author = "Wilson",
//                    ISBN = "978-0005",
//                    Subject = "Computer Science",
//                    TotalCopies = 3,
//                    AvailableCopies = 3,
//                    CreatedAt = DateTime.UtcNow
//                }
//            };

//            await booksCollection.InsertManyAsync(books);
//        }

//        // This method fixes existing books that have missing or invalid AvailableCopies values
//        public async Task FixExistingBooksAsync()
//        {
//            var booksCollection = _mongoDbService.GetCollection<Book>("Books");

//            // Filter books where AvailableCopies is missing or less than zero
//            var filter = Builders<Book>.Filter.Or(
//                Builders<Book>.Filter.Exists("available_copies", false),
//                Builders<Book>.Filter.Lt("available_copies", 0)
//            );

//            // Find books to fix
//            var booksToFix = await booksCollection.Find(filter).ToListAsync();

//            foreach (var book in booksToFix)
//            {
//                // Set AvailableCopies = TotalCopies
//                var update = Builders<Book>.Update.Set("available_copies", book.TotalCopies);
//                await booksCollection.UpdateOneAsync(
//                    Builders<Book>.Filter.Eq("_id", book._id),
//                    update
//                );
//            }
//        }
//    }
//}
