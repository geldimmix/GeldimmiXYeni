using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Resources;

namespace Nobetci.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        IStringLocalizer<SharedResource> localizer,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _localizer = localizer;
        _logger = logger;
    }

    #region External Login (Google)

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, 
            model.Password, 
            model.RememberMe, 
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
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
            ModelState.AddModelError(string.Empty, "Account locked. Please try again later.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
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
            Plan = UserPlan.Freemium,
            Language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        
        if (result.Succeeded)
        {
            _logger.LogInformation("User created a new account with password.");
            
            // Sign in the user
            await _signInManager.SignInAsync(user, isPersistent: false);
            
            // Transfer guest data to user account
            await TransferGuestDataToUser(user);
            
            return LocalRedirect(returnUrl ?? "/app");
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
    public IActionResult AccessDenied()
    {
        return View();
    }

    #endregion

    #region External Login Methods

    /// <summary>
    /// Initiates external login (Google)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }

    /// <summary>
    /// Handles callback from external login provider
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/app");

        if (remoteError != null)
        {
            ModelState.AddModelError(string.Empty, $"External login error: {remoteError}");
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return RedirectToAction(nameof(Login));
        }

        // Try to sign in with external login
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
        
        if (result.Succeeded)
        {
            _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
            
            // Get user and transfer guest data
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrEmpty(email))
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user != null)
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                    await TransferGuestDataToUser(user);
                }
            }
            
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            return RedirectToAction(nameof(AccessDenied));
        }
        
        // If user doesn't exist, create one
        var userEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
        var userName = info.Principal.FindFirstValue(ClaimTypes.Name);
        
        if (string.IsNullOrEmpty(userEmail))
        {
            ModelState.AddModelError(string.Empty, "Email not received from external provider.");
            return RedirectToAction(nameof(Login));
        }

        // Check if user with this email already exists
        var existingUser = await _userManager.FindByEmailAsync(userEmail);
        if (existingUser != null)
        {
            // Link external login to existing user
            var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
            if (addLoginResult.Succeeded)
            {
                await _signInManager.SignInAsync(existingUser, isPersistent: true);
                existingUser.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(existingUser);
                await TransferGuestDataToUser(existingUser);
                return LocalRedirect(returnUrl);
            }
        }

        // Create new user
        var newUser = new ApplicationUser
        {
            UserName = userEmail,
            Email = userEmail,
            FullName = userName ?? userEmail.Split('@')[0],
            EmailConfirmed = true, // Email from Google is verified
            Plan = UserPlan.Freemium,
            Language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
        };

        var createResult = await _userManager.CreateAsync(newUser);
        if (createResult.Succeeded)
        {
            createResult = await _userManager.AddLoginAsync(newUser, info);
            if (createResult.Succeeded)
            {
                _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                await _signInManager.SignInAsync(newUser, isPersistent: true);
                await TransferGuestDataToUser(newUser);
                return LocalRedirect(returnUrl);
            }
        }

        foreach (var error in createResult.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return RedirectToAction(nameof(Login));
    }

    #endregion

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

            // Move shifts
            var guestShifts = await _context.Shifts
                .Where(s => s.Employee.OrganizationId == guestOrg.Id)
                .ToListAsync();

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


