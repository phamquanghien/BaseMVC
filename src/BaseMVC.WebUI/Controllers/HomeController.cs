using System.Diagnostics;
using System.Globalization;
using BaseMVC.Application.Exams;
using BaseMVC.WebUI.Models;
using BaseMVC.WebUI.Models.Home;
using BaseMVC.WebUI.Security;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace BaseMVC.WebUI.Controllers;

public class HomeController(ISender sender, IConfiguration configuration, IRecaptchaV3Verifier recaptchaVerifier) : Controller
{
    private const string LookupAction = "exam_lookup";

    public IActionResult Index()
    {
        return View(new HomeIndexViewModel(configuration["RECAPTCHA_SITE_KEY"]));
    }

    [HttpGet]
    public async Task<IActionResult> ExamTypes([FromQuery] string kind, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PublicExamLookupKind>(kind, ignoreCase: true, out var lookupKind) || !Enum.IsDefined(lookupKind))
            return BadRequest(new { error = "Vui lòng chọn loại thông tin cần tra cứu." });

        var items = await sender.Send(new ListPublicExamTypesQuery(lookupKind), cancellationToken);
        return Ok(new { items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lookup([FromForm] PublicExamLookupRequest request, CancellationToken cancellationToken)
    {
        if (request.ExamTypeId <= 0 ||
            !Enum.TryParse<PublicExamLookupKind>(request.Kind, ignoreCase: true, out var lookupKind) ||
            !Enum.IsDefined(lookupKind) ||
            string.IsNullOrWhiteSpace(request.StudentCode) ||
            request.StudentCode.Trim().Length > 15 ||
            !DateOnly.TryParseExact(request.DateOfBirth.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOfBirth))
        {
            return BadRequest(new { error = "Kiểm tra lại loại tra cứu, mã sinh viên và ngày sinh (dd/mm/yyyy)." });
        }

        if (!await recaptchaVerifier.VerifyAsync(request.RecaptchaToken, LookupAction, cancellationToken))
            return BadRequest(new { error = "Thao tác người dùng không hợp lệ, vui lòng tắt trình duyệt web và thử lại." });

        var result = await sender.Send(
            new LookupPublicExamDataQuery(request.ExamTypeId, lookupKind, request.StudentCode, dateOfBirth),
            cancellationToken);
        var items = result?.Items ?? [];
        return Ok(new
        {
            columns = result?.Columns ?? [],
            items,
            message = items.Count == 0
                ? lookupKind == PublicExamLookupKind.Schedule
                    ? "Chưa có lịch thi tương ứng."
                    : "Chưa có kết quả thi tương ứng."
                : null
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
