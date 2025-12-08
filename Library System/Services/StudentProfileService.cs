using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using SystemLibrary.Models;

public class StudentProfileService : IStudentProfileService
{
    private readonly IMongoCollection<StudentProfile> _studentProfiles;

    public StudentProfileService(IMongoClient mongoClient)
    {
        var database = mongoClient.GetDatabase("LibraDB");
        _studentProfiles = database.GetCollection<StudentProfile>("StudentProfiles");
    }

    public async Task<List<StudentProfile>> GetAllStudentProfilesAsync()
    {
        return await _studentProfiles.Find(_ => true).ToListAsync();
    }
}
