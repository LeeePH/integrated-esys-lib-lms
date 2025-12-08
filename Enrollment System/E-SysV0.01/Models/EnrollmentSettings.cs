using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
public class EnrollmentSettings
{
    [BsonId][BsonRepresentation(BsonType.String)] public string Id { get; set; } = "enrollment-settings";
    // Active semester + AY
    public bool IsOpen { get; set; } = false;
    public string Semester { get; set; } = "1st Semester";
    public string AcademicYear { get; set; } = "";

    // Enrollment window (for the active semester)
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? OpenedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ClosesAtUtc { get; set; }
    public long? OpenDurationSeconds { get; set; }

    // Program capacities
    public Dictionary<string, int> ProgramCapacities { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Planned semester durations
    public int? Semester1PlannedMonths { get; set; }
    public long? Semester1PlannedDurationSeconds { get; set; } // d/h/m/s
    public int? Semester2PlannedMonths { get; set; }
    public long? Semester2PlannedDurationSeconds { get; set; } // d/h/m/s

    // Runtime semester windows
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? Semester1StartedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? Semester1EndsAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? Semester2StartedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? Semester2EndsAtUtc { get; set; }
}