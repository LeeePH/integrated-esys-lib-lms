// MongoDB Commands to Test the Overdue Penalty System

// ============================================
// 1. VIEW ALL OVERDUE RESERVATIONS
// ============================================
db.Reservations.find({
    Status: "Approved",
    DueDate: { $lt: new Date() }
}).pretty()


// ============================================
// 2. VIEW ALL OVERDUE PENALTIES
// ============================================
db.Penalties.find({
    PenaltyType: "Overdue"
}).pretty()


// ============================================
// 3. VIEW PENALTIES FOR A SPECIFIC STUDENT
// ============================================
// Replace STUDENT_ID with actual ObjectId
db.Penalties.find({
    UserId: ObjectId("STUDENT_ID")
}).pretty()


// ============================================
// 4. CREATE A TEST OVERDUE RESERVATION
// ============================================
// This creates a reservation that's 1 day overdue
db.Reservations.insertOne({
    _id: ObjectId(),
    UserId: ObjectId("STUDENT_USER_ID"),  // Replace with real ID
    BookId: ObjectId("BOOK_ID"),           // Replace with real ID
    BookTitle: "Test Overdue Book",
    ApprovalDate: ISODate("2025-11-09T10:00:00Z"),
    DueDate: ISODate("2025-11-10T10:00:00Z"),  // Yesterday = overdue
    ReservationDate: ISODate("2025-11-08T10:00:00Z"),
    Status: "Approved",
    StudentNumber: "2024-001",
    StudentName: "Test Student"
})


// ============================================
// 5. VIEW PENALTY PROGRESSION (Run every minute)
// ============================================
// Run this query every 60 seconds to see penalty amount increase
db.Penalties.find({
    PenaltyType: "Overdue",
    IsPaid: false
}, {
    BookTitle: 1,
    Amount: 1,
    CreatedDate: 1,
    Description: 1
}).sort({ CreatedDate: -1 })


// ============================================
// 6. CHECK USER PENALTY STATUS
// ============================================
// Replace STUDENT_ID with actual ObjectId
db.Users.findOne({ _id: ObjectId("STUDENT_ID") }, {
    FullName: 1,
    HasPendingPenalties: 1,
    TotalPendingPenalties: 1,
    PenaltyRestrictionDate: 1
})


// ============================================
// 7. GET TOTAL UNPAID PENALTIES FOR A STUDENT
// ============================================
db.Penalties.aggregate([
    { $match: { UserId: ObjectId("STUDENT_ID"), IsPaid: false } },
    { $group: { 
        _id: "$UserId", 
        TotalAmount: { $sum: "$Amount" },
        Count: { $sum: 1 }
    }}
])


// ============================================
// 8. CLEAR ALL TEST OVERDUE PENALTIES (FOR TESTING ONLY)
// ============================================
// This removes all "Overdue" type penalties - be careful!
db.Penalties.deleteMany({ PenaltyType: "Overdue" })


// ============================================
// 9. VIEW PENALTY HISTORY BY DATE
// ============================================
db.Penalties.find({
    PenaltyType: "Overdue"
}).sort({ CreatedDate: -1 }).limit(20).pretty()


// ============================================
// 10. GET STATISTICS ON OVERDUE PENALTIES
// ============================================
db.Penalties.aggregate([
    { $match: { PenaltyType: "Overdue" } },
    { $group: {
        _id: null,
        TotalPenalties: { $sum: 1 },
        TotalAmount: { $sum: "$Amount" },
        AverageAmount: { $avg: "$Amount" },
        MaxAmount: { $max: "$Amount" },
        MinAmount: { $min: "$Amount" }
    }}
])


// ============================================
// QUICK TEST WORKFLOW
// ============================================

// Step 1: Find a student
var student = db.Users.findOne({ Role: "Student" })
var studentId = student._id

// Step 2: Find a book
var book = db.Books.findOne()
var bookId = book._id

// Step 3: Create an overdue reservation
db.Reservations.insertOne({
    _id: ObjectId(),
    UserId: studentId,
    BookId: bookId,
    BookTitle: book.Title,
    ApprovalDate: ISODate("2025-11-09T10:00:00Z"),
    DueDate: new Date(new Date().getTime() - 24*60*60*1000),  // 1 day ago
    ReservationDate: ISODate("2025-11-08T10:00:00Z"),
    Status: "Approved",
    StudentNumber: "TEST-001",
    StudentName: student.FullName
})

// Step 4: Wait 60 seconds, then check penalties
// (Open a new Compass window or run this again)
db.Penalties.find({ 
    UserId: studentId,
    PenaltyType: "Overdue" 
}).pretty()

// Step 5: Wait another 60 seconds and check again
// The Amount should increase by 10 pesos
db.Penalties.find({ 
    UserId: studentId,
    PenaltyType: "Overdue" 
}).pretty()
