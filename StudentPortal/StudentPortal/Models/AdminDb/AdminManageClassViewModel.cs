using System.Collections.Generic;
using StudentPortal.Models;

namespace StudentPortal.Models.AdminDb
{
    // AdminManageClassViewModel.cs
    public class AdminManageClassViewModel
    {
        public string ClassId { get; set; } = string.Empty; // <-- must be string
        public string SubjectName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;

        public List<StudentRecord> Students { get; set; } = new();
        public List<JoinRequest> JoinRequests { get; set; } = new();
    }
    public class ApproveJoinRequestModel
    {
        public string RequestId { get; set; }
        public string ClassCode { get; set; }
        public string StudentEmail { get; set; }
    }

}
