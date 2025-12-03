using System;
using System.Linq;
using System.Security.Cryptography;
using MongoDB.Driver;

namespace ProfessorAccountCreation.Models
{
    public static class DbSeeder
    {
        public static void SeedSuperAdmin(MongoDbContext db)
        {
            if (db is null) throw new ArgumentNullException(nameof(db));

            // Ensure the collection exists (create if missing)
            var collectionName = db.ProfAdmins.CollectionNamespace.CollectionName;
            var database = db.ProfAdmins.Database;
            var collectionNames = database.ListCollectionNames().ToList();
            if (!collectionNames.Contains(collectionName))
            {
                database.CreateCollection(collectionName);
            }

            // Accounts to ensure exist (email, plaintext password)
            var accounts = new (string Email, string Password)[]
            {
                ("tolentino.daniel.ochoa@gmail.com", "Daniel_2004"),
                ("calderon.anjelo.salvador@gmail.com", "71147114Admin")
            };

            foreach (var (email, password) in accounts)
            {
                var passwordHash = HashPassword(password);

                var update = Builders<ProfAdmin>.Update
                    .Set(p => p.PasswordHash, passwordHash)
                    .Set(p => p.OTP, string.Empty)
                    .Set(p => p.OTPExpiresAt, DateTime.MinValue)
                    .SetOnInsert(p => p.Email, email);

                db.ProfAdmins.UpdateOne(
                    Builders<ProfAdmin>.Filter.Eq(p => p.Email, email),
                    update,
                    new UpdateOptions { IsUpsert = true }
                );
            }
        }

        // Format: {iterations}.{saltBase64}.{hashBase64}
        private static string HashPassword(string password)
        {
            const int saltSize = 16;
            const int hashSize = 32;
            const int iterations = 100_000;

            byte[] salt = new byte[saltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(hashSize);

            return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }
    }
}