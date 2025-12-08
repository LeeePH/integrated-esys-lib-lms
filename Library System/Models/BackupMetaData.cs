using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class BackupMetadata
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        [BsonElement("backup_name")]
        public string BackupName { get; set; }

        [BsonElement("file_path")]
        public string FilePath { get; set; }

        [BsonElement("file_size")]
        public long FileSize { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("created_by")]
        public string CreatedBy { get; set; }

        [BsonElement("collections_included")]
        public List<string> CollectionsIncluded { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } // "Success", "Failed", "InProgress"

        [BsonElement("description")]
        public string Description { get; set; }

        [BsonIgnore]
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F2} KB";
                if (FileSize < 1024 * 1024 * 1024) return $"{FileSize / (1024.0 * 1024.0):F2} MB";
                return $"{FileSize / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }
    }
}

