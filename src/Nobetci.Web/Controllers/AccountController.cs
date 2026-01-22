using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Resources;
using Nobetci.Web.Services;

namespace Nobetci.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AccountController> logger,
        IConfiguration configuration,
        IEmailSender emailSender,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _localizer = localizer;
        _logger = logger;
        _configuration = configuration;
        _emailSender = emailSender;
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // If user is already logged in, redirect to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl ?? "/app");
        }
        
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["RecaptchaSiteKey"] = _configuration["ReCaptcha:SiteKey"] ?? "";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        
        if (!ModelState.IsValid)
            return View(model);

        // Validate reCAPTCHA v2 (required for v2 Checkbox)
        var recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
        var recaptchaSecretKey = _configuration["ReCaptcha:SecretKey"];
        
        if (!string.IsNullOrEmpty(recaptchaSecretKey) && 
            recaptchaSecretKey != "YOUR_RECAPTCHA_SECRET_KEY" && 
            !recaptchaSecretKey.Contains("YOUR_"))
        {
            // reCAPTCHA is configured, so it's required
            if (string.IsNullOrEmpty(recaptchaToken))
            {
                ModelState.AddModelError(string.Empty, _localizer["PleaseCompleteReCaptcha"]);
                ViewData["RecaptchaSiteKey"] = _configuration["ReCaptcha:SiteKey"] ?? "";
                return View(model);
            }
            
            var isValid = await ValidateReCaptchaAsync(recaptchaToken);
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, _localizer["ReCaptchaVerificationFailed"]);
                ViewData["RecaptchaSiteKey"] = _configuration["ReCaptcha:SiteKey"] ?? "";
                return View(model);
            }
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        
        // Check if user exists and email is confirmed
        if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
        {
            ModelState.AddModelError(string.Empty, _localizer["EmailNotConfirmed"]);
            ViewData["ShowResendLink"] = true;
            ViewData["UserEmail"] = model.Email;
            ViewData["RecaptchaSiteKey"] = _configuration["ReCaptcha:SiteKey"] ?? "";
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, 
            model.Password, 
            model.RememberMe, 
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                
                // Transfer guest data to user account
                await TransferGuestDataToUser(user);
            }
            
            _logger.LogInformation("User logged in.");
            return LocalRedirect(returnUrl ?? "/app");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, _localizer["AccountLocked"]);
            ViewData["RecaptchaSiteKey"] = _configuration["ReCaptcha:SiteKey"] ?? "";
            return View(model);
        }

        ModelState.AddModelError(string.Empty, _localizer["InvalidLoginAttempt"]);
        ViewData["RecaptchaSiteKey"] = _configuration["ReCaptcha:SiteKey"] ?? "";
        return View(model);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        // If user is already logged in, redirect to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl ?? "/app");
        }
        
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            Plan = UserPlan.Free,
            Language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            EmailConfirmed = false // Email confirmation required
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        
        if (result.Succeeded)
        {
            _logger.LogInformation("User created a new account with password.");
            
            // Generate email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Action(
                nameof(ConfirmEmail),
                "Account",
                new { userId = user.Id, token = token, returnUrl = returnUrl },
                protocol: Request.Scheme);

            // Send confirmation email
            var subject = _localizer["EmailVerificationSubject"];
            var emailBody = string.Format(_localizer["EmailWelcomeBody"].Value, user.FullName, callbackUrl);

            await _emailSender.SendEmailAsync(user.Email, subject, emailBody);
            
            // Don't sign in - user must confirm email first
            // Show success message with email confirmation notice
            TempData["EmailConfirmationSent"] = true;
            TempData["UserEmail"] = user.Email;
            
            return RedirectToAction(nameof(RegisterConfirmation), new { email = user.Email, returnUrl = returnUrl });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Index", "Home");
    }
    
    [HttpGet]
    [Route("Account/Logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult RegisterConfirmation(string? email, string? returnUrl = null)
    {
        ViewData["Email"] = email;
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token, string? returnUrl = null)
    {
        if (userId == null || token == null)
        {
            TempData["Error"] = _localizer["InvalidEmailConfirmationLink"];
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = _localizer["UserNotFound"];
            return RedirectToAction(nameof(Login));
        }

        // Token may be URL-encoded, decode it if needed
        token = Uri.UnescapeDataString(token);
        
        var result = await _userManager.ConfirmEmailAsync(user, token);
        
        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} confirmed their email.", userId);
            
            // Transfer guest data to user account
            await TransferGuestDataToUser(user);
            
            TempData["Success"] = _localizer["EmailConfirmedSuccessfully"];
            
            return RedirectToAction(nameof(Login), new { returnUrl = returnUrl });
        }

        TempData["Error"] = _localizer["EmailConfirmationFailed"];
        
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ResendConfirmationEmail(string? email = null)
    {
        ViewData["Email"] = email;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("ResendConfirmationEmail")]
    public async Task<IActionResult> ResendConfirmationEmailPost(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, _localizer["EmailAddressRequired"]);
            return View();
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Don't reveal if user exists or not (security best practice)
            TempData["Success"] = _localizer["ConfirmationEmailSentIfRegistered"];
            return RedirectToAction(nameof(ResendConfirmationEmail));
        }

        if (user.EmailConfirmed)
        {
            TempData["Info"] = _localizer["EmailAlreadyConfirmed"];
            return RedirectToAction(nameof(Login));
        }

        // Generate new email confirmation token
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var callbackUrl = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { userId = user.Id, token = token },
            protocol: Request.Scheme);

        // Send confirmation email
        var subject = _localizer["EmailVerificationSubject"];
        var emailBody = string.Format(_localizer["EmailVerificationBody"].Value, user.FullName, callbackUrl);

        if (!string.IsNullOrEmpty(user.Email))
        {
            await _emailSender.SendEmailAsync(user.Email, subject, emailBody);
        }
        
        TempData["Success"] = _localizer["ConfirmationEmailSent"];
        
        return RedirectToAction(nameof(ResendConfirmationEmail), new { email = user.Email });
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // Google OAuth External Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError != null)
        {
            _logger.LogError("Error from external provider: {RemoteError}", remoteError);
            ModelState.AddModelError(string.Empty, _localizer["GoogleSignInError"]);
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ModelState.AddModelError(string.Empty, _localizer["FailedToRetrieveGoogleLogin"]);
            return RedirectToAction(nameof(Login));
        }

        // Sign in the user with this external login provider if the user already has a login
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        
        if (result.Succeeded)
        {
            // User already exists, transfer guest data and redirect
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                await TransferGuestDataToUser(user);
            }
            
            _logger.LogInformation("User logged in with {Name} provider.", info.LoginProvider);
            return LocalRedirect(returnUrl ?? "/app");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, _localizer["AccountLocked"]);
            return RedirectToAction(nameof(Login));
        }

        // If the user does not have an account, then create one
        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var name = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, _localizer["CouldNotRetrieveEmailFromGoogle"]);
            return RedirectToAction(nameof(Login));
        }

        // Check if user with this email already exists
        var existingUser = await _userManager.FindByEmailAsync(email);
        
        if (existingUser != null)
        {
            // User exists but doesn't have this external login, add it
            var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
            if (addLoginResult.Succeeded)
            {
                await _signInManager.SignInAsync(existingUser, isPersistent: false);
                existingUser.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(existingUser);
                await TransferGuestDataToUser(existingUser);
                return LocalRedirect(returnUrl ?? "/app");
            }
        }
        else
        {
            // Create new user
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true, // Google email is verified
                FullName = name ?? email.Split('@')[0],
                Plan = UserPlan.Free,
                Language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            };

            var createResult = await _userManager.CreateAsync(user);
            if (createResult.Succeeded)
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (addLoginResult.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    await TransferGuestDataToUser(user);
                    _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
                    return LocalRedirect(returnUrl ?? "/app");
                }
            }

            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return RedirectToAction(nameof(Login));
    }

    // reCAPTCHA Validation (v3 - score-based)
    private async Task<bool> ValidateReCaptchaAsync(string token)
    {
        var secretKey = _configuration["ReCaptcha:SecretKey"];
        if (string.IsNullOrEmpty(secretKey) || secretKey == "YOUR_RECAPTCHA_SECRET_KEY")
        {
            return true; // If not configured, skip validation
        }

        try
        {
            using var client = new HttpClient();
            var response = await client.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}",
                null);
            
            var json = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
            
            if (!result.GetProperty("success").GetBoolean())
            {
                return false;
            }

            // reCAPTCHA v3: Check score (0.0 = bot, 1.0 = human)
            // v2 Checkbox doesn't have score, so this check is for v3 compatibility
            if (result.TryGetProperty("score", out var scoreElement))
            {
                var score = scoreElement.GetDouble();
                _logger.LogInformation("reCAPTCHA v3 score: {Score}", score);
                
                // Threshold: 0.5 (adjust based on your needs: 0.5 = lenient, 0.7 = strict)
                return score >= 0.5;
            }

            // reCAPTCHA v2 Checkbox: success = true means user completed the challenge
            _logger.LogInformation("reCAPTCHA v2 validation: Success");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "reCAPTCHA validation error");
            return false;
        }
    }

    /// <summary>
    /// Transfer guest session data to registered user
    /// </summary>
    private async Task TransferGuestDataToUser(ApplicationUser user)
    {
        var sessionId = HttpContext.Session.GetString("GuestSessionId");
        if (string.IsNullOrEmpty(sessionId))
            return;

        // Find guest organization
        var guestOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.GuestSessionId == sessionId);

        if (guestOrg == null)
            return;

        // Check if user already has an organization
        var userOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.UserId == user.Id);

        if (userOrg == null)
        {
            // Transfer guest org to user
            guestOrg.UserId = user.Id;
            guestOrg.GuestSessionId = null;
            guestOrg.Name = user.FullName ?? "My Organization";
        }
        else
        {
            // Merge: Move employees from guest org to user org
            var guestEmployees = await _context.Employees
                .Where(e => e.OrganizationId == guestOrg.Id)
                .ToListAsync();

            foreach (var emp in guestEmployees)
            {
                emp.OrganizationId = userOrg.Id;
            }

            // Move holidays
            var guestHolidays = await _context.Holidays
                .Where(h => h.OrganizationId == guestOrg.Id)
                .ToListAsync();

            foreach (var holiday in guestHolidays)
            {
                holiday.OrganizationId = userOrg.Id;
            }

            // Delete guest organization
            _context.Organizations.Remove(guestOrg);
        }

        await _context.SaveChangesAsync();
        
        // Clear session
        HttpContext.Session.Remove("GuestSessionId");
    }
}
