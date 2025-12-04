using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using E_SysV0._01.Services;

namespace E_SysV0._01.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class RegistrationSlipController : Controller
    {
        private readonly RegistrationSlipPdfService _pdfService;

        public RegistrationSlipController(RegistrationSlipPdfService pdfService)
        {
            _pdfService = pdfService;
        }

        // GET: /Admin/RegistrationSlip/Print?requestId=abc123
        [HttpGet]
        public async Task<IActionResult> Print(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return BadRequest("requestId is required.");

            // For student/print copies we omit the generated timestamp.
            // Keep service default true for other callers (emails/attachments) but explicitly request false here.
            var bytes = await _pdfService.GenerateForRequestAsync(requestId, includeGeneratedFooter: false);
            return File(bytes, "application/pdf", $"RegistrationSlip_{requestId}.pdf");
        }
    }
}