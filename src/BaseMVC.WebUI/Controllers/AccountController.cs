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
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
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
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
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

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            return View("ForgotPasswordConfirmation");
        }

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
            return View("ResetPasswordInvalid");
        }

        var result = await userManager.ResetPasswordAsync(user, model.Code, model.Password);
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

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);
        if (!result.Succeeded)
        {
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

        await signInManager.RefreshSignInAsync(user);
        TempData["PasswordChanged"] = true;
        return RedirectToAction(nameof(ChangePassword));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await signInManager.SignOutAsync();
        return RedirectToLocal(returnUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
