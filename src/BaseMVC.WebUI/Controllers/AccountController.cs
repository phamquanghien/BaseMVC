using BaseMVC.Infrastructure.Identity;
using BaseMVC.Infrastructure.Auditing;
using BaseMVC.Infrastructure.Persistence;
using BaseMVC.WebUI.Email;
using BaseMVC.WebUI.Models.Account;
using BaseMVC.WebUI.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BaseMVC.WebUI.Controllers;

[Route("[controller]/[action]")]
public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions,
    ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);

        if (user is null)
        {
            await auditLogWriter.WriteAsync(
                AuditEventType.Login,
                AuditOutcome.Failed,
                cancellationToken: HttpContext.RequestAborted);
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        var result = await signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await auditLogWriter.WriteAsync(
                AuditEventType.Login,
                AuditOutcome.Succeeded,
                actorUserId: user.Id,
                targetUserId: user.Id,
                cancellationToken: HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            await auditLogWriter.WriteAsync(
                AuditEventType.Login,
                AuditOutcome.LockedOut,
                targetUserId: user.Id,
                cancellationToken: HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);
            ModelState.AddModelError(
                string.Empty,
                "Tài khoản tạm khóa do có nhiều lần đăng nhập không thành công. Vui lòng thử lại sau.");
        }
        else
        {
            await auditLogWriter.WriteAsync(
                AuditEventType.Login,
                AuditOutcome.Failed,
                targetUserId: user.Id,
                cancellationToken: HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Uri publicBaseUri;
        try
        {
            publicBaseUri = emailOptions.Value.Validate();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Password recovery email configuration is invalid.");
            ModelState.AddModelError(
                string.Empty,
                "Dịch vụ khôi phục mật khẩu chưa được cấu hình đầy đủ. Vui lòng liên hệ quản trị viên.");
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            await auditLogWriter.WriteAsync(
                AuditEventType.PasswordResetRequested,
                AuditOutcome.Accepted,
                cancellationToken: cancellationToken);
            return View("ForgotPasswordConfirmation");
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.PasswordResetRequested,
            AuditOutcome.Accepted,
            targetUserId: user.Id,
            cancellationToken: cancellationToken);

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var callbackPath = Url.Action(
            nameof(ResetPassword),
            "Account",
            new { email = user.Email, code = resetToken });
        if (string.IsNullOrWhiteSpace(callbackPath))
        {
            throw new InvalidOperationException("Could not generate the password-reset callback path.");
        }

        var resetUrl = new Uri(publicBaseUri, callbackPath).AbsoluteUri;
        try
        {
            await emailSender.SendPasswordResetAsync(user.Email!, resetUrl, cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            logger.LogError(exception, "Password recovery email delivery failed.");
            await auditLogWriter.WriteAsync(
                AuditEventType.PasswordResetEmailDelivery,
                AuditOutcome.Failed,
                targetUserId: user.Id,
                cancellationToken: cancellationToken);
        }

        return View("ForgotPasswordConfirmation");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? email, string? code)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return View("ResetPasswordInvalid");
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Code = code
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            await auditLogWriter.WriteAsync(
                AuditEventType.PasswordResetCompleted,
                AuditOutcome.Failed,
                cancellationToken: cancellationToken);
            return View("ResetPasswordInvalid");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.ResetPasswordAsync(user, model.Code, model.Password);
        if (!result.Succeeded)
        {
            await auditLogWriter.WriteAsync(
                AuditEventType.PasswordResetCompleted,
                AuditOutcome.Failed,
                targetUserId: user.Id,
                cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Code == "InvalidToken"
                        ? "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn."
                        : error.Description);
            }

            return View(model);
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.PasswordResetCompleted,
            AuditOutcome.Succeeded,
            actorUserId: user.Id,
            targetUserId: user.Id,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return View("ResetPasswordConfirmation");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        ViewData["PasswordChanged"] = TempData["PasswordChanged"];
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        var result = await userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);
        if (!result.Succeeded)
        {
            await auditLogWriter.WriteAsync(
                AuditEventType.PasswordChange,
                AuditOutcome.Failed,
                actorUserId: user.Id,
                targetUserId: user.Id,
                cancellationToken: HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Code == "PasswordMismatch"
                        ? "Mật khẩu hiện tại không chính xác."
                        : error.Description);
            }

            return View(model);
        }

        await auditLogWriter.WriteAsync(
            AuditEventType.PasswordChange,
            AuditOutcome.Succeeded,
            actorUserId: user.Id,
            targetUserId: user.Id,
            cancellationToken: HttpContext.RequestAborted);
        await transaction.CommitAsync(HttpContext.RequestAborted);
        await signInManager.RefreshSignInAsync(user);
        TempData["PasswordChanged"] = true;
        return RedirectToAction(nameof(ChangePassword));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        var userId = userManager.GetUserId(User);
        await signInManager.SignOutAsync();
        await auditLogWriter.WriteAsync(
            AuditEventType.Logout,
            AuditOutcome.Succeeded,
            actorUserId: userId,
            cancellationToken: HttpContext.RequestAborted);
        return RedirectToLocal(returnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        ViewData["ReturnUrl"] = "/";
        return View();
    }

    [AllowAnonymous]
    [Route("{statusCode:int}")]
    public IActionResult StatusCodePage(int statusCode, string? returnUrl = null)
    {
        if (statusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
        {
            return StatusCode(statusCode);
        }

        Response.StatusCode = statusCode;
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

        if (Request.GetTypedHeaders().Accept?.Any(mediaType =>
            string.Equals(mediaType.MediaType.Value, "text/html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType.MediaType.Value, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase)) != true)
        {
            return Problem(
                statusCode: statusCode,
                title: statusCode == StatusCodes.Status401Unauthorized
                    ? "Authentication required"
                    : "Access denied",
                detail: statusCode == StatusCodes.Status401Unauthorized
                    ? "Sign in to continue."
                    : "Your account does not have permission to access this resource.");
        }

        return statusCode == StatusCodes.Status401Unauthorized
            ? View("Unauthorized")
            : View("AccessDenied");
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
