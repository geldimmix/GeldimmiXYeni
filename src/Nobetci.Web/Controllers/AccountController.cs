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
            var isTurkish = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
            ModelState.AddModelError(string.Empty, isTurkish ? "Hesap kilitlendi. Lütfen daha sonra tekrar deneyin." : "Account locked. Please try again later.");
            return View(model);
        }

        var isTr = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
        ModelState.AddModelError(string.Empty, isTr ? "Geçersiz giriş denemesi." : "Invalid login attempt.");
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
    [Route("Account/Logout")]
    public async Task<IActionResult> LogoutGet()
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
