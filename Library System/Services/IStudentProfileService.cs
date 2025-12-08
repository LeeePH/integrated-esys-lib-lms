using System.Collections.Generic;
using System.Threading.Tasks;
using SystemLibrary.Models;

public interface IStudentProfileService
{
    Task<List<StudentProfile>> GetAllStudentProfilesAsync();
}
