using SystemLibrary.Models;
using SystemLibrary.Services;
using MongoDB.Driver;

namespace SystemLibrary.Data
{
    public class MOCKDataSeeder
    {
        private readonly IMOCKDataService _MOCKDataService;

        public MOCKDataSeeder(IMOCKDataService MOCKDataService)
        {
            _MOCKDataService = MOCKDataService;
        }

        public async Task SeedSampleDataAsync()
        {
            await SeedStudentDataAsync();
            await SeedStaffDataAsync();
            await SeedAuthorDataAsync();
            await SeedBookMOCKDataAsync();
        }

        private async Task SeedStudentDataAsync()
        {
            var students = new List<StudentMOCKData>
            {
                new StudentMOCKData
                {
                    StudentNumber = "2024-0001",
                    FullName = "Juan Carlos Dela Cruz",
                    Course = "BSIT",
                    YearLevel = "3rd Year",
                    Program = "Bachelor of Science in Information Technology",
                    Department = "College of Computer Studies",
                    ContactNumber = "09123456789",
                    Email = "juan.delacruz@student.mac.edu.ph",
                    IsEnrolled = true,
                    EnrollmentDate = new DateTime(2024, 6, 15),
                    IsActive = true
                },
                new StudentMOCKData
                {
                    StudentNumber = "2024-0002",
                    FullName = "Maria Santos Rodriguez",
                    Course = "BSCS",
                    YearLevel = "2nd Year",
                    Program = "Bachelor of Science in Computer Science",
                    Department = "College of Computer Studies",
                    ContactNumber = "09123456790",
                    Email = "maria.rodriguez@student.mac.edu.ph",
                    IsEnrolled = true,
                    EnrollmentDate = new DateTime(2024, 6, 15),
                    IsActive = true
                },
                new StudentMOCKData
                {
                    StudentNumber = "2024-0003",
                    FullName = "Jose Miguel Torres",
                    Course = "BSIT",
                    YearLevel = "4th Year",
                    Program = "Bachelor of Science in Information Technology",
                    Department = "College of Computer Studies",
                    ContactNumber = "09123456791",
                    Email = "jose.torres@student.mac.edu.ph",
                    IsEnrolled = true,
                    EnrollmentDate = new DateTime(2021, 6, 15),
                    IsActive = true
                },
                new StudentMOCKData
                {
                    StudentNumber = "2024-0004",
                    FullName = "Ana Patricia Garcia",
                    Course = "BSCS",
                    YearLevel = "1st Year",
                    Program = "Bachelor of Science in Computer Science",
                    Department = "College of Computer Studies",
                    ContactNumber = "09123456792",
                    Email = "ana.garcia@student.mac.edu.ph",
                    IsEnrolled = true,
                    EnrollmentDate = new DateTime(2024, 6, 15),
                    IsActive = true
                },
                new StudentMOCKData
                {
                    StudentNumber = "2024-0005",
                    FullName = "Roberto Luis Martinez",
                    Course = "BSIT",
                    YearLevel = "2nd Year",
                    Program = "Bachelor of Science in Information Technology",
                    Department = "College of Computer Studies",
                    ContactNumber = "09123456793",
                    Email = "roberto.martinez@student.mac.edu.ph",
                    IsEnrolled = true,
                    EnrollmentDate = new DateTime(2023, 6, 15),
                    IsActive = true
                },
                new StudentMOCKData
                {
                    StudentNumber = "2023-0099",
                    FullName = "Carlos Eduardo Lopez",
                    Course = "BSCS",
                    YearLevel = "5th Year",
                    Program = "Bachelor of Science in Computer Science",
                    Department = "College of Computer Studies",
                    ContactNumber = "09123456794",
                    Email = "carlos.lopez@student.mac.edu.ph",
                    IsEnrolled = false, // Graduated
                    EnrollmentDate = new DateTime(2020, 6, 15),
                    GraduationDate = new DateTime(2024, 5, 30),
                    IsActive = false
                }
            };

            await _MOCKDataService.BulkInsertStudentsAsync(students);
        }

        private async Task SeedStaffDataAsync()
        {
            var staff = new List<StaffMOCKData>
            {
                new StaffMOCKData
                {
                    EmployeeId = "EMP-001",
                    FullName = "Dr. Sarah Johnson",
                    Department = "College of Computer Studies",
                    Position = "Dean",
                    Email = "sarah.johnson@mac.edu.ph",
                    ContactNumber = "09123456795",
                    EmploymentType = "Full-time",
                    HireDate = new DateTime(2020, 1, 15),
                    IsActive = true
                },
                new StaffMOCKData
                {
                    EmployeeId = "EMP-002",
                    FullName = "Prof. Michael Chen",
                    Department = "College of Computer Studies",
                    Position = "Professor",
                    Email = "michael.chen@mac.edu.ph",
                    ContactNumber = "09123456796",
                    EmploymentType = "Full-time",
                    HireDate = new DateTime(2019, 8, 1),
                    IsActive = true
                },
                new StaffMOCKData
                {
                    EmployeeId = "EMP-003",
                    FullName = "Ms. Jennifer Lee",
                    Department = "Library Services",
                    Position = "Head Librarian",
                    Email = "jennifer.lee@mac.edu.ph",
                    ContactNumber = "09123456797",
                    EmploymentType = "Full-time",
                    HireDate = new DateTime(2021, 3, 1),
                    IsActive = true
                },
                new StaffMOCKData
                {
                    EmployeeId = "EMP-004",
                    FullName = "Mr. David Wilson",
                    Department = "Library Services",
                    Position = "Assistant Librarian",
                    Email = "david.wilson@mac.edu.ph",
                    ContactNumber = "09123456798",
                    EmploymentType = "Full-time",
                    HireDate = new DateTime(2022, 6, 1),
                    IsActive = true
                },
                new StaffMOCKData
                {
                    EmployeeId = "EMP-005",
                    FullName = "Ms. Lisa Anderson",
                    Department = "Library Services",
                    Position = "Library Assistant",
                    Email = "lisa.anderson@mac.edu.ph",
                    ContactNumber = "09123456799",
                    EmploymentType = "Part-time",
                    HireDate = new DateTime(2023, 1, 15),
                    IsActive = true
                },
                new StaffMOCKData
                {
                    EmployeeId = "EMP-006",
                    FullName = "Dr. Robert Brown",
                    Department = "College of Computer Studies",
                    Position = "Professor",
                    Email = "robert.brown@mac.edu.ph",
                    ContactNumber = "09123456800",
                    EmploymentType = "Full-time",
                    HireDate = new DateTime(2018, 9, 1),
                    TerminationDate = new DateTime(2024, 6, 30),
                    IsActive = false // Retired
                }
            };

            await _MOCKDataService.BulkInsertStaffAsync(staff);
        }

        private async Task SeedAuthorDataAsync()
        {
            try
            {
                var db = _MOCKDataService.GetDatabase();
                var authors = db.GetCollection<Author>("Authors");
                var count = await authors.CountDocumentsAsync(Builders<Author>.Filter.Empty);
                if (count > 0) return;

                var sampleAuthors = new List<Author>
                {
                    new Author { Name = "Robert C. Martin", AlternateNames = new List<string>{ "Uncle Bob" }, Bio = "Software engineer and author of Clean Code.", FirstPublicationDate = new DateTime(1990,1,1), LastPublicationDate = new DateTime(2020,1,1) },
                    new Author { Name = "Andrew Hunt", AlternateNames = new List<string>{ "Andy Hunt" }, FirstPublicationDate = new DateTime(1999,10,30), LastPublicationDate = new DateTime(2008,1,1) },
                    new Author { Name = "David Thomas", AlternateNames = new List<string>{ "Dave Thomas" }, FirstPublicationDate = new DateTime(1999,10,30), LastPublicationDate = new DateTime(2008,1,1) },
                    new Author { Name = "Erich Gamma", FirstPublicationDate = new DateTime(1994,10,31), LastPublicationDate = new DateTime(1994,10,31) },
                    new Author { Name = "Martin Fowler", FirstPublicationDate = new DateTime(2002,11,5), LastPublicationDate = new DateTime(2010,1,1) }
                };

                await authors.InsertManyAsync(sampleAuthors);
            }
            catch
            {
                // ignore seeding failures for optional data
            }
        }

        private async Task SeedBookMOCKDataAsync()
        {
            try
            {
                var db = _MOCKDataService.GetDatabase();
                var bookMac = db.GetCollection<BookMOCKData>("BookMOCKData");
                var count = await bookMac.CountDocumentsAsync(Builders<BookMOCKData>.Filter.Empty);
                if (count > 0) return;

                var samples = new List<BookMOCKData>
                {
                    new BookMOCKData { ISBN = "978-0132350884", Title = "Clean Code", Author = "Robert C. Martin", Publisher = "Prentice Hall", PublicationDate = "2008-08-11", Subject = "Computer Science", ClassificationNo = "CS-001" },
                    new BookMOCKData { ISBN = "978-0201633610", Title = "Design Patterns", Author = "Erich Gamma", Publisher = "Addison-Wesley", PublicationDate = "1994-10-31", Subject = "Computer Science", ClassificationNo = "CS-002" },
                    new BookMOCKData { ISBN = "978-0134494166", Title = "Clean Architecture", Author = "Robert C. Martin", Publisher = "Pearson", PublicationDate = "2017-09-20", Subject = "Computer Science", ClassificationNo = "CS-003" },
                    new BookMOCKData { ISBN = "978-0201616224", Title = "The Pragmatic Programmer", Author = "Andrew Hunt, David Thomas", Publisher = "Addison-Wesley", PublicationDate = "1999-10-30", Subject = "Computer Science", ClassificationNo = "CS-004" },
                    new BookMOCKData { ISBN = "978-0321127426", Title = "Patterns of Enterprise Application Architecture", Author = "Martin Fowler", Publisher = "Addison-Wesley", PublicationDate = "2002-11-05", Subject = "Computer Science", ClassificationNo = "CS-005" }
                };

                await bookMac.InsertManyAsync(samples);
            }
            catch
            {
                // ignore seeding failures for optional data
            }
        }
    }
}
