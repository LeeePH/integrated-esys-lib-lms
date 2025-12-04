using E_SysV0._01.Hubs;
using E_SysV0._01.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System;
using System.Threading.Tasks;
namespace E_SysV0._01.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Admin")]
    public class EnrollmentCycleController : Controller
    {
        private readonly EnrollmentCycleService _cycle;
        public EnrollmentCycleController(EnrollmentCycleService cycle)
        {
            _cycle = cycle;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSemesterCycle(
            string semester,
            int enrollDays = 0, int enrollHours = 0, int enrollMinutes = 0, int enrollSeconds = 0,
            int semMonths = 0, int semDays = 0, int semHours = 0, int semMinutes = 0, int semSeconds = 0)
        {
            try
            {
                await _cycle.NormalizeAsync();

                semester = (semester ?? "").Trim();
                if (semester != "1st Semester" && semester != "2nd Semester")
                    throw new InvalidOperationException("Invalid semester.");

                long enrollTotalSeconds =
                    (long)Math.Max(0, enrollDays) * 86400L +
                    (long)Math.Max(0, enrollHours) * 3600L +
                    (long)Math.Max(0, enrollMinutes) * 60L +
                    (long)Math.Max(0, enrollSeconds);

                long semPlannedSeconds =
                    (long)Math.Max(0, semDays) * 86400L +
                    (long)Math.Max(0, semHours) * 3600L +
                    (long)Math.Max(0, semMinutes) * 60L +
                    (long)Math.Max(0, semSeconds);
                int semPlannedMonths = Math.Max(0, semMonths);

                await _cycle.OpenEnrollmentAndPlanSemesterAsync(semester, enrollTotalSeconds, semPlannedMonths, semPlannedSeconds);

                TempData["Info"] = $"{semester}: Enrollment opened and will auto-close. Semester auto-starts after close.";
                return RedirectToAction("AdminSettings", "Admin");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("AdminSettings", "Admin");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetSemesterCycle()
        {
            await _cycle.ResetCycleAsync();
            TempData["Info"] = "Cycle reset. Configure 1st Semester to start again.";
            return RedirectToAction("AdminSettings", "Admin");
        }
    }
}