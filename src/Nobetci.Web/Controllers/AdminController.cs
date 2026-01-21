using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Services;

namespace Nobetci.Web.Controllers;

public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ISystemSettingsService _settingsService;
    private const string AdminSessionKey = "IsAdmin";
    private const string AdminUserIdKey = "AdminUserId";
    private const string AdminRoleKey = "AdminRole";

    public AdminController(
        ApplicationDbContext context, 
        UserManager<ApplicationUser> userManager, 
        IConfiguration configuration,
        ISystemSettingsService settingsService)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
        _settingsService = settingsService;
    }

    private bool IsAdminLoggedIn()
    {
        return HttpContext.Session.GetString(AdminSessionKey) == "true";
    }
    
    private bool IsSuperAdmin()
    {
        return HttpContext.Session.GetString(AdminRoleKey) == AdminRoles.SuperAdmin;
    }
    
    private int? GetCurrentAdminId()
    {
        var idStr = HttpContext.Session.GetString(AdminUserIdKey);
        return int.TryParse(idStr, out var id) ? id : null;
    }

    // GET: /admin
    public IActionResult Index()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        ViewBag.IsSuperAdmin = IsSuperAdmin();
        return View();
    }

    // GET: /admin/login
    public IActionResult Login()
    {
        if (IsAdminLoggedIn())
            return RedirectToAction(nameof(Index));

        return View();
    }

    // POST: /admin/login
    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
        var adminUser = await _context.AdminUsers
            .FirstOrDefaultAsync(a => a.Username == username && a.IsActive);
        
        if (adminUser != null && adminUser.VerifyPassword(password))
        {
            // Update last login
            adminUser.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            HttpContext.Session.SetString(AdminSessionKey, "true");
            HttpContext.Session.SetString(AdminUserIdKey, adminUser.Id.ToString());
            HttpContext.Session.SetString(AdminRoleKey, adminUser.Role);
            
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Error = "Geçersiz kullanıcı adı veya parola";
        return View();
    }

    // GET: /admin/logout
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(AdminSessionKey);
        HttpContext.Session.Remove(AdminUserIdKey);
        HttpContext.Session.Remove(AdminRoleKey);
        return RedirectToAction(nameof(Login));
    }

    // GET: /admin/pages
    public async Task<IActionResult> Pages()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        var pages = await _context.ContentPages
            .OrderBy(p => p.Language)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync();

        return View(pages);
    }

    // GET: /admin/pages/create
    public IActionResult CreatePage()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        return View(new ContentPage());
    }

    // POST: /admin/pages/create
    [HttpPost]
    public async Task<IActionResult> CreatePage(ContentPage page)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        if (ModelState.IsValid)
        {
            page.CreatedAt = DateTime.UtcNow;
            page.UpdatedAt = DateTime.UtcNow;
            _context.ContentPages.Add(page);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Sayfa oluşturuldu";
            return RedirectToAction(nameof(Pages));
        }

        return View(page);
    }

    // GET: /admin/pages/edit/5
    public async Task<IActionResult> EditPage(int id)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        var page = await _context.ContentPages.FindAsync(id);
        if (page == null)
            return NotFound();

        return View(page);
    }

    // POST: /admin/pages/edit/5
    [HttpPost]
    public async Task<IActionResult> EditPage(int id, ContentPage page)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        if (id != page.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            page.UpdatedAt = DateTime.UtcNow;
            _context.Update(page);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Sayfa güncellendi";
            return RedirectToAction(nameof(Pages));
        }

        return View(page);
    }

    // POST: /admin/pages/delete/5
    [HttpPost]
    public async Task<IActionResult> DeletePage(int id)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        var page = await _context.ContentPages.FindAsync(id);
        if (page != null)
        {
            _context.ContentPages.Remove(page);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Sayfa silindi";
        }

        return RedirectToAction(nameof(Pages));
    }

    // GET: /admin/logs
    public async Task<IActionResult> Logs(int page = 1, string? search = null, string? deviceType = null, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        const int pageSize = 50;

        var query = _context.VisitorLogs.AsQueryable();

        // Filters
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(l => 
                (l.PagePath != null && l.PagePath.Contains(search)) ||
                (l.IpAddress != null && l.IpAddress.Contains(search)) ||
                (l.Browser != null && l.Browser.Contains(search)));
        }

        if (!string.IsNullOrEmpty(deviceType))
        {
            query = query.Where(l => l.DeviceType == deviceType);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= dateTo.Value.AddDays(1));
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.Search = search;
        ViewBag.DeviceType = deviceType;
        ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
        ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

        return View(logs);
    }

    // GET: /admin/stats
    public async Task<IActionResult> Stats()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);
        var monthAgo = today.AddDays(-30);

        var stats = new AdminStatsViewModel
        {
            TotalVisits = await _context.VisitorLogs.CountAsync(),
            TodayVisits = await _context.VisitorLogs.CountAsync(l => l.CreatedAt >= today),
            WeekVisits = await _context.VisitorLogs.CountAsync(l => l.CreatedAt >= weekAgo),
            MonthVisits = await _context.VisitorLogs.CountAsync(l => l.CreatedAt >= monthAgo),
            
            UniqueIPs = await _context.VisitorLogs.Select(l => l.IpAddress).Distinct().CountAsync(),
            
            TopPages = await _context.VisitorLogs
                .Where(l => l.PagePath != null)
                .GroupBy(l => l.PagePath)
                .Select(g => new PageStat { Path = g.Key!, Count = g.Count() })
                .OrderByDescending(p => p.Count)
                .Take(10)
                .ToListAsync(),
            
            DeviceStats = await _context.VisitorLogs
                .Where(l => l.DeviceType != null)
                .GroupBy(l => l.DeviceType)
                .Select(g => new DeviceStat { Type = g.Key!, Count = g.Count() })
                .OrderByDescending(d => d.Count)
                .ToListAsync(),
            
            BrowserStats = await _context.VisitorLogs
                .Where(l => l.Browser != null)
                .GroupBy(l => l.Browser)
                .Select(g => new BrowserStat { Name = g.Key!, Count = g.Count() })
                .OrderByDescending(b => b.Count)
                .Take(5)
                .ToListAsync(),
            
            TotalPages = await _context.ContentPages.CountAsync(),
            PublishedPages = await _context.ContentPages.CountAsync(p => p.IsPublished),
            TotalEmployees = await _context.Employees.CountAsync(),
            TotalOrganizations = await _context.Organizations.CountAsync()
        };

        return View(stats);
    }
    
    #region User Management
    
    // GET: /admin/users
    public async Task<IActionResult> Users(int page = 1, string? search = null)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        const int pageSize = 25;

        var query = _context.Users.AsQueryable();

        // Search filter
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u => 
                (u.Email != null && u.Email.Contains(search)) ||
                (u.FullName != null && u.FullName.Contains(search)));
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItem
            {
                Id = u.Id,
                Email = u.Email ?? "",
                FullName = u.FullName ?? "",
                Plan = u.Plan,
                CustomEmployeeLimit = u.CustomEmployeeLimit,
                CanAccessAttendance = u.CanAccessAttendance,
                CanAccessPayroll = u.CanAccessPayroll,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                AdminNotes = u.AdminNotes,
                OrganizationCount = u.Organizations.Count,
                EmployeeCount = u.Organizations.SelectMany(o => o.Employees).Count(e => e.IsActive)
            })
            .ToListAsync();

        // Get default limits from database
        var guestLimit = await _settingsService.GetGuestEmployeeLimitAsync();
        var registeredLimit = await _settingsService.GetRegisteredEmployeeLimitAsync();
        var premiumLimit = await _settingsService.GetPremiumEmployeeLimitAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.Search = search;
        ViewBag.GuestLimit = guestLimit;
        ViewBag.RegisteredLimit = registeredLimit;
        ViewBag.PremiumLimit = premiumLimit;
        ViewBag.IsSuperAdmin = IsSuperAdmin();

        return View(users);
    }
    
    // GET: /admin/users/edit/{id}
    [Route("admin/users/edit/{id}")]
    public async Task<IActionResult> EditUser(string id)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        var user = await _context.Users
            .Include(u => u.Organizations)
            .ThenInclude(o => o.Employees)
            .FirstOrDefaultAsync(u => u.Id == id);
            
        if (user == null)
            return NotFound();

        var viewModel = new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName ?? "",
            Plan = user.Plan,
            CustomEmployeeLimit = user.CustomEmployeeLimit,
            CanAccessAttendance = user.CanAccessAttendance,
            CanAccessPayroll = user.CanAccessPayroll,
            AdminNotes = user.AdminNotes,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Organizations = user.Organizations.Select(o => new OrganizationSummary
            {
                Id = o.Id,
                Name = o.Name,
                EmployeeCount = o.Employees.Count(e => e.IsActive)
            }).ToList()
        };
        
        // Get default limits from database
        ViewBag.GuestLimit = await _settingsService.GetGuestEmployeeLimitAsync();
        ViewBag.RegisteredLimit = await _settingsService.GetRegisteredEmployeeLimitAsync();
        ViewBag.PremiumLimit = await _settingsService.GetPremiumEmployeeLimitAsync();

        return View(viewModel);
    }
    
    // POST: /admin/users/edit/{id}
    [HttpPost]
    [Route("admin/users/edit/{id}")]
    public async Task<IActionResult> EditUser(string id, EditUserViewModel model)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        // Update user properties
        user.Plan = model.Plan;
        user.CustomEmployeeLimit = model.CustomEmployeeLimit;
        user.CanAccessAttendance = model.CanAccessAttendance;
        user.CanAccessPayroll = model.CanAccessPayroll;
        user.AdminNotes = model.AdminNotes;

        var result = await _userManager.UpdateAsync(user);
        
        if (result.Succeeded)
        {
            TempData["Success"] = "Kullanıcı ayarları güncellendi";
            return RedirectToAction(nameof(Users));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        
        return View(model);
    }
    
    // POST: /admin/users/quick-update
    [HttpPost]
    public async Task<IActionResult> QuickUpdateUser([FromBody] QuickUpdateUserDto dto)
    {
        if (!IsAdminLoggedIn())
            return Json(new { success = false, error = "Unauthorized" });

        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user == null)
            return Json(new { success = false, error = "User not found" });

        if (dto.Field == "customLimit")
        {
            user.CustomEmployeeLimit = dto.IntValue;
        }
        else if (dto.Field == "canAccessAttendance")
        {
            user.CanAccessAttendance = dto.BoolValue ?? true;
        }
        else if (dto.Field == "canAccessPayroll")
        {
            user.CanAccessPayroll = dto.BoolValue ?? true;
        }
        else if (dto.Field == "plan")
        {
            if (Enum.TryParse<UserPlan>(dto.StringValue, out var plan))
            {
                user.Plan = plan;
            }
        }

        var result = await _userManager.UpdateAsync(user);
        
        return Json(new { success = result.Succeeded });
    }
    
    #endregion
    
    #region System Settings
    
    // GET: /admin/settings
    public async Task<IActionResult> Settings()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu sayfaya erişim yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }
        
        var settings = await _context.SystemSettings.ToListAsync();
        
        var viewModel = new SystemSettingsViewModel
        {
            GuestEmployeeLimit = int.Parse(settings.FirstOrDefault(s => s.Key == SystemSettings.Keys.GuestEmployeeLimit)?.Value ?? "5"),
            RegisteredEmployeeLimit = int.Parse(settings.FirstOrDefault(s => s.Key == SystemSettings.Keys.RegisteredEmployeeLimit)?.Value ?? "10"),
            PremiumEmployeeLimit = int.Parse(settings.FirstOrDefault(s => s.Key == SystemSettings.Keys.PremiumEmployeeLimit)?.Value ?? "100"),
            SiteName = settings.FirstOrDefault(s => s.Key == SystemSettings.Keys.SiteName)?.Value ?? "Geldimmi",
            MaintenanceMode = bool.Parse(settings.FirstOrDefault(s => s.Key == SystemSettings.Keys.MaintenanceMode)?.Value ?? "false")
        };
        
        return View(viewModel);
    }
    
    // POST: /admin/settings
    [HttpPost]
    public async Task<IActionResult> Settings(SystemSettingsViewModel model)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu işlemi yapmaya yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }
        
        await _settingsService.SetSettingAsync(SystemSettings.Keys.GuestEmployeeLimit, model.GuestEmployeeLimit.ToString());
        await _settingsService.SetSettingAsync(SystemSettings.Keys.RegisteredEmployeeLimit, model.RegisteredEmployeeLimit.ToString());
        await _settingsService.SetSettingAsync(SystemSettings.Keys.PremiumEmployeeLimit, model.PremiumEmployeeLimit.ToString());
        await _settingsService.SetSettingAsync(SystemSettings.Keys.SiteName, model.SiteName ?? "Geldimmi");
        await _settingsService.SetSettingAsync(SystemSettings.Keys.MaintenanceMode, model.MaintenanceMode.ToString().ToLower());
        
        _settingsService.ClearCache();
        
        TempData["Success"] = "Ayarlar kaydedildi.";
        return RedirectToAction(nameof(Settings));
    }
    
    #endregion
    
    #region Admin User Management
    
    // GET: /admin/admins
    public async Task<IActionResult> Admins()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu sayfaya erişim yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }
        
        var admins = await _context.AdminUsers
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        
        return View(admins);
    }
    
    // GET: /admin/admins/create
    public IActionResult CreateAdmin()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu işlemi yapmaya yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }
        
        return View(new AdminUserViewModel());
    }
    
    // POST: /admin/admins/create
    [HttpPost]
    public async Task<IActionResult> CreateAdmin(AdminUserViewModel model)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu işlemi yapmaya yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }
        
        // Check if username already exists
        if (await _context.AdminUsers.AnyAsync(a => a.Username == model.Username))
        {
            ModelState.AddModelError("Username", "Bu kullanıcı adı zaten kullanılıyor.");
            return View(model);
        }
        
        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError("Password", "Şifre gereklidir.");
            return View(model);
        }
        
        var admin = new AdminUser
        {
            Username = model.Username,
            PasswordHash = AdminUser.HashPassword(model.Password),
            FullName = model.FullName,
            Email = model.Email,
            Role = model.Role,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.AdminUsers.Add(admin);
        await _context.SaveChangesAsync();
        
        TempData["Success"] = "Admin kullanıcı oluşturuldu.";
        return RedirectToAction(nameof(Admins));
    }
    
    // GET: /admin/admins/edit/{id}
    public async Task<IActionResult> EditAdmin(int id)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu işlemi yapmaya yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }
        
        var admin = await _context.AdminUsers.FindAsync(id);
        if (admin == null)
            return NotFound();
        
        var viewModel = new AdminUserViewModel
        {
            Id = admin.Id,
            Username = admin.Username,
            FullName = admin.FullName,
            Email = admin.Email,
            Role = admin.Role,
            IsActive = admin.IsActive
        };
        
        return View(viewModel);
    }
    
    // POST: /admin/admins/edit/{id}
    [HttpPost]
    public async Task<IActionResult> EditAdmin(int id, AdminUserViewModel model)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu işlemi yapmaya yetkiniz yok.";
            return RedirectToAction(nameof(Index));
        }
        
        var admin = await _context.AdminUsers.FindAsync(id);
        if (admin == null)
            return NotFound();
        
        // Check if username already exists (for another user)
        if (await _context.AdminUsers.AnyAsync(a => a.Username == model.Username && a.Id != id))
        {
            ModelState.AddModelError("Username", "Bu kullanıcı adı zaten kullanılıyor.");
            return View(model);
        }
        
        admin.Username = model.Username;
        admin.FullName = model.FullName;
        admin.Email = model.Email;
        admin.Role = model.Role;
        admin.IsActive = model.IsActive;
        
        // Update password if provided
        if (!string.IsNullOrEmpty(model.Password))
        {
            admin.PasswordHash = AdminUser.HashPassword(model.Password);
        }
        
        await _context.SaveChangesAsync();
        
        TempData["Success"] = "Admin kullanıcı güncellendi.";
        return RedirectToAction(nameof(Admins));
    }
    
    // POST: /admin/admins/delete/{id}
    [HttpPost]
    public async Task<IActionResult> DeleteAdmin(int id)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        if (!IsSuperAdmin())
            return Json(new { success = false, error = "Yetkiniz yok." });
        
        var currentAdminId = GetCurrentAdminId();
        if (currentAdminId == id)
            return Json(new { success = false, error = "Kendi hesabınızı silemezsiniz." });
        
        var admin = await _context.AdminUsers.FindAsync(id);
        if (admin == null)
            return Json(new { success = false, error = "Admin bulunamadı." });
        
        _context.AdminUsers.Remove(admin);
        await _context.SaveChangesAsync();
        
        return Json(new { success = true });
    }
    
    #endregion
}

