using BaseMVC.Application.Accounts;
using MediatR;
using BaseMVC.Infrastructure.Identity;
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
    ISender sender,
    UserManager<ApplicationUser> userManager,
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

        var result = await sender.Send(new LoginCommand(model.Email, model.Password, model.RememberMe), HttpContext.RequestAborted);
        if (result == LoginOutcome.Succeeded)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result == LoginOutcome.LockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "Tài khoản tạm khóa do có nhiều lần đăng nhập không thành công. Vui lòng thử lại sau.");
        }
        else
        {
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

        var preparation = await sender.Send(new PreparePasswordResetCommand(model.Email), cancellationToken);
        if (!preparation.HasConfirmedUser)
        {
            return View("ForgotPasswordConfirmation");
        }

        var callbackPath = Url.Action(
            nameof(ResetPassword),
            "Account",
            new { email = preparation.Email, code = preparation.Token });
        if (string.IsNullOrWhiteSpace(callbackPath))
        {
            throw new InvalidOperationException("Could not generate the password-reset callback path.");
        }

        var resetUrl = new Uri(publicBaseUri, callbackPath).AbsoluteUri;
        await sender.Send(new DeliverPasswordResetEmailCommand(preparation.UserId!, preparation.Email!, resetUrl), cancellationToken);

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

        var result = await sender.Send(new ResetPasswordCommand(model.Email, model.Code, model.Password), cancellationToken);
        if (!result.Succeeded && result.Errors.Count == 0)
        {
            return View("ResetPasswordInvalid");
        }
        if (!result.Succeeded)
        {
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

        var userId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await sender.Send(new ChangePasswordCommand(userId, model.CurrentPassword, model.NewPassword), HttpContext.RequestAborted);
        if (!result.Succeeded)
        {
            if (result.Errors.Count == 0)
            {
                return Challenge();
            }

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

        TempData["PasswordChanged"] = true;
        return RedirectToAction(nameof(ChangePassword));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await sender.Send(new LogoutCommand(userManager.GetUserId(User)), HttpContext.RequestAborted);
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
