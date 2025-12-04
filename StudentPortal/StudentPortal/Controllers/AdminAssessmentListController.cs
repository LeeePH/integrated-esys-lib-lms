using Microsoft.AspNetCore.Mvc;
using SIA_IPT.Models.AdminAssessmentList;
using StudentPortal.Services;
using System.Linq;

namespace SIA_IPT.Controllers
{
    public class AdminAssessmentListController : Controller
    {
        private readonly MongoDbService _mongoDb;
        public AdminAssessmentListController(MongoDbService mongoDb) { _mongoDb = mongoDb; }

        [HttpGet("/AdminAssessmentList")]
        public async Task<IActionResult> Index([FromQuery] string? classCode)
        {
            var items = new List<AssessmentItem>();
            if (!string.IsNullOrEmpty(classCode))
            {
                var contents = await _mongoDb.GetContentsByClassCodeAsync(classCode);
                items = contents.Where(c => c.Type == "assessment")
                                .Select(c => new AssessmentItem { Id = c.Id ?? string.Empty, Title = c.Title })
                                .ToList();
            }

            var model = new AdminAssessmentListViewModel { Assessments = items };
            return View("~/Views/AdminDb/AdminAssessmentList/Index.cshtml", model);
        }
    }
}
