using EnrollmentSystem.Models;
using E_SysV0._01.Services; // ✅ Import your new MongoDBServices namespace
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EnrollmentSystem.Controllers
{
    public class managedocs : Controller
    {
        private readonly MongoDBServices _mongo; // ✅ Use the new service

        public managedocs(MongoDBServices mongo)
        {
            _mongo = mongo;
        }

        public IActionResult Index()
        {
            return View();
        }

        // =========================================================
        // 📄 GET: Fetch all required documents
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetRequiredDocuments()
        {
            var documents = await _mongo.DocumentsRequiredCollection.Find(_ => true).ToListAsync();
            return Json(documents);
        }

        // =========================================================
        // ➕ POST: Add a new required document (UI will confirm)
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> AddRequiredDocument([FromBody] RequiredDocument newDoc)
        {
            if (newDoc == null || string.IsNullOrWhiteSpace(newDoc.DocumentName))
                return BadRequest("Invalid document data.");

            // Prevent duplicate names (case-insensitive)
            var existing = await _mongo.DocumentsRequiredCollection
                .Find(d => d.DocumentName.ToLower() == newDoc.DocumentName.ToLower())
                .FirstOrDefaultAsync();

            if (existing != null)
                return Conflict("Document already exists.");

            // ✅ Get last document ID and increment properly
            var lastDoc = await _mongo.DocumentsRequiredCollection
                .Find(_ => true)
                .SortByDescending(d => d.DocumentId)
                .Limit(1)
                .FirstOrDefaultAsync();

            newDoc.DocumentId = (lastDoc?.DocumentId ?? 0) + 1;

            // ✅ Add timestamps safely (no structure change)
            newDoc.Id ??= ObjectId.GenerateNewId().ToString();
            newDoc.RequiredFor ??= new List<string>();
            if (newDoc.RequiredFor.Any(r => string.IsNullOrWhiteSpace(r)))
                newDoc.RequiredFor = newDoc.RequiredFor.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();

            // ✅ Insert into MongoDB
            await _mongo.DocumentsRequiredCollection.InsertOneAsync(newDoc);

            return Ok(new { message = "Document added successfully!" });
        }

        // =========================================================
        // ❌ DELETE: Remove a required document (UI confirms)
        // =========================================================
        [HttpDelete]
        public async Task<IActionResult> DeleteRequiredDocument(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("Invalid document ID.");

            if (!ObjectId.TryParse(id, out var objectId))
                return BadRequest("Invalid document ID format.");

            var filter = Builders<RequiredDocument>.Filter.Eq("_id", objectId);
            var result = await _mongo.DocumentsRequiredCollection.DeleteOneAsync(filter);

            if (result.DeletedCount == 0)
                return NotFound(new { success = false, message = "Document not found or already deleted." });

            return Ok(new { success = true, message = "Document deleted successfully!" });
        }

        // =========================================================
        // ✏️ PUT / POST: Update required document
        // =========================================================
        [HttpPut]
        [HttpPost] // allow POST as your frontend uses POST for update
        public async Task<IActionResult> UpdateRequiredDocument(string id, [FromBody] RequiredDocument updatedDoc)
        {
            if (updatedDoc == null)
                return BadRequest("Invalid document data.");

            // determine id source: route/query 'id' has priority, otherwise try body.Id
            var idCandidate = id;
            if (string.IsNullOrWhiteSpace(idCandidate) && !string.IsNullOrWhiteSpace(updatedDoc.Id))
                idCandidate = updatedDoc.Id;

            if (string.IsNullOrWhiteSpace(idCandidate))
                return BadRequest("Invalid document ID format.");

            // Build a filter that tries ObjectId -> numeric DocumentId -> string Id
            FilterDefinition<RequiredDocument> filter = null;

            if (ObjectId.TryParse(idCandidate, out var objectId))
            {
                filter = Builders<RequiredDocument>.Filter.Eq("_id", objectId);
            }
            else if (int.TryParse(idCandidate, out var numericId))
            {
                filter = Builders<RequiredDocument>.Filter.Eq(d => d.DocumentId, numericId);
            }
            else
            {
                filter = Builders<RequiredDocument>.Filter.Eq(d => d.Id, idCandidate);
            }

            // Normalize/clean incoming fields
            var cleanedName = updatedDoc.DocumentName?.Trim();
            var cleanedRequiredFor = updatedDoc.RequiredFor != null
                ? updatedDoc.RequiredFor.Select(r => (r ?? string.Empty).Trim()).Where(r => !string.IsNullOrEmpty(r)).ToList()
                : new List<string>();

            // ✅ Add updated timestamp and cleaned data
            var update = Builders<RequiredDocument>.Update
                .Set(d => d.DocumentName, cleanedName)
                .Set(d => d.RequiredFor, cleanedRequiredFor)
                .Set("updated_at", DateTime.UtcNow);

            var result = await _mongo.DocumentsRequiredCollection.UpdateOneAsync(filter, update);

            if (result.MatchedCount == 0)
                return NotFound(new { success = false, message = "Document not found." });

            if (result.ModifiedCount == 0)
                return Ok(new { success = false, message = "No changes were made." });

            return Ok(new { success = true, message = "Document updated successfully!" });
        }
    }
}
