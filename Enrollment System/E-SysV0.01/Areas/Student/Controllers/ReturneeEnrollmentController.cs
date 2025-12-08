using Microsoft.AspNetCore.Mvc;

namespace E_SysV0._01.Areas.Student.Controllers
{
    public class ReturneeEnrollmentController : Controller
    {
        public IActionResult ReturneeEnrollment()
        {
            return View("~/Areas/Student/Views/Student/Returnee/ReturneeEnrollment.cshtml");
        }
    }
}
