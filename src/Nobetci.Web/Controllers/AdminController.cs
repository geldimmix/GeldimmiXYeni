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
    private readonly IActivityLogService _activityLog;
    private const string AdminSessionKey = "IsAdmin";
    private const string AdminUserIdKey = "AdminUserId";
    private const string AdminRoleKey = "AdminRole";

    public AdminController(
        ApplicationDbContext context, 
        UserManager<ApplicationUser> userManager, 
        IConfiguration configuration,
        ISystemSettingsService settingsService,
        IActivityLogService activityLog)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
        _settingsService = settingsService;
        _activityLog = activityLog;
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
                CanManageUnits = u.CanManageUnits,
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
            UnitLimit = user.UnitLimit,
            UnitEmployeeLimit = user.UnitEmployeeLimit,
            CanAccessAttendance = user.CanAccessAttendance,
            CanAccessPayroll = user.CanAccessPayroll,
            CanManageUnits = user.CanManageUnits,
            // Temizlik modülü limitleri
            CanAccessCleaning = user.CanAccessCleaning,
            CleaningScheduleLimit = user.CleaningScheduleLimit,
            CleaningItemLimit = user.CleaningItemLimit,
            CleaningQrAccessLimit = user.CleaningQrAccessLimit,
            CanSelectCleaningFrequency = user.CanSelectCleaningFrequency,
            CanGroupCleaningSchedules = user.CanGroupCleaningSchedules,
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
        user.UnitLimit = model.UnitLimit;
        user.UnitEmployeeLimit = model.UnitEmployeeLimit;
        user.CanAccessAttendance = model.CanAccessAttendance;
        user.CanAccessPayroll = model.CanAccessPayroll;
        user.CanManageUnits = model.CanManageUnits;
        // Temizlik modülü limitleri
        user.CanAccessCleaning = model.CanAccessCleaning;
        user.CleaningScheduleLimit = model.CleaningScheduleLimit;
        user.CleaningItemLimit = model.CleaningItemLimit;
        user.CleaningQrAccessLimit = model.CleaningQrAccessLimit;
        user.CanSelectCleaningFrequency = model.CanSelectCleaningFrequency;
        user.CanGroupCleaningSchedules = model.CanGroupCleaningSchedules;
        user.AdminNotes = model.AdminNotes;

        var result = await _userManager.UpdateAsync(user);
        
        if (result.Succeeded)
        {
            await _activityLog.LogAsync(ActivityType.AdminUserUpdated, 
                $"Kullanıcı güncellendi (Admin): {user.Email}", 
                "User", null,
                new { user.Email, user.Plan, user.CustomEmployeeLimit, user.CanManageUnits });
            
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
        else if (dto.Field == "canManageUnits")
        {
            user.CanManageUnits = dto.BoolValue ?? false;
        }

        var result = await _userManager.UpdateAsync(user);
        
        if (result.Succeeded)
        {
            await _activityLog.LogAsync(ActivityType.AdminUserUpdated, 
                $"Kullanıcı hızlı güncelleme (Admin): {user.Email} - {dto.Field}", 
                "User", null,
                new { user.Email, Field = dto.Field, dto.IntValue, dto.BoolValue, dto.StringValue });
        }
        
        return Json(new { success = result.Succeeded });
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
    
    #region Activity Logs
    
    [HttpGet]
    public async Task<IActionResult> ActivityLogs(string? userId, int? activityType, string? fromDate, string? toDate, int page = 1)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));
        
        var pageSize = 50;
        DateTime? from = null;
        DateTime? to = null;
        
        if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var parsedFrom))
            from = parsedFrom;
        
        if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var parsedTo))
            to = parsedTo.AddDays(1); // Include the whole day
        
        ActivityType? type = activityType.HasValue ? (ActivityType)activityType.Value : null;
        
        var logs = await _activityLog.GetLogsAsync(
            organizationId: null, 
            userId: userId, 
            type: type, 
            from: from, 
            to: to, 
            page: page, 
            pageSize: pageSize);
        
        var totalCount = await _activityLog.GetLogCountAsync(
            organizationId: null, 
            userId: userId, 
            type: type, 
            from: from, 
            to: to);
        
        // Get users for filter dropdown
        var users = await _context.Users
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToListAsync();
        
        var model = new ActivityLogsViewModel
        {
            Logs = logs.Select(l => new ActivityLogItem
            {
                Id = l.Id,
                UserId = l.UserId,
                UserName = l.User?.FullName ?? l.User?.Email ?? "Sistem",
                ActivityType = l.ActivityType,
                ActivityTypeName = l.ActivityType.GetDisplayName(true),
                ActivityTypeIcon = l.ActivityType.GetIcon(),
                ActivityTypeColor = l.ActivityType.GetColor(),
                EntityType = l.EntityType,
                EntityId = l.EntityId,
                Description = l.Description,
                Details = l.Details,
                IpAddress = l.IpAddress,
                CreatedAt = l.CreatedAt
            }).ToList(),
            Users = users.Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})" }).ToList(),
            ActivityTypes = Enum.GetValues<ActivityType>()
                .Select(t => new SelectListItem { Value = ((int)t).ToString(), Text = t.GetDisplayName(true) })
                .ToList(),
            SelectedUserId = userId,
            SelectedActivityType = activityType,
            FromDate = fromDate,
            ToDate = toDate,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            TotalCount = totalCount
        };
        
        return View(model);
    }
    
    [HttpGet]
    [Route("admin/api/activity-logs/{id}")]
    public async Task<IActionResult> GetActivityLogDetails(long id)
    {
        if (!IsAdminLoggedIn())
            return Unauthorized();
        
        var log = await _context.ActivityLogs
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == id);
        
        if (log == null)
            return NotFound();
        
        return Json(new {
            log.Id,
            UserName = log.User?.FullName ?? log.User?.Email ?? "Sistem",
            UserEmail = log.User?.Email,
            ActivityType = log.ActivityType.GetDisplayName(true),
            log.EntityType,
            log.EntityId,
            log.Description,
            log.Details,
            log.IpAddress,
            log.UserAgent,
            CreatedAt = log.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss")
        });
    }
    
    #endregion
    
    #region Blog Management
    
    [HttpGet]
    [Route("admin/blog")]
    public async Task<IActionResult> BlogPosts(int page = 1)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction("Login");
        
        ViewBag.IsSuperAdmin = IsSuperAdmin();
        
        const int pageSize = 20;
        var query = _context.BlogPosts.OrderByDescending(b => b.PublishedAt);
        
        var totalCount = await query.CountAsync();
        var posts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        var model = new BlogPostsViewModel
        {
            Posts = posts,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            TotalCount = totalCount
        };
        
        return View(model);
    }
    
    [HttpGet]
    [Route("admin/blog/create")]
    public IActionResult CreateBlog()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction("Login");
        
        ViewBag.IsSuperAdmin = IsSuperAdmin();
        return View(new BlogPostEditViewModel());
    }
    
    [HttpPost]
    [Route("admin/blog/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBlog(BlogPostEditViewModel model)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction("Login");
        
        ViewBag.IsSuperAdmin = IsSuperAdmin();
        
        if (!ModelState.IsValid)
            return View(model);
        
        // Check slug uniqueness
        var existingSlug = await _context.BlogPosts.AnyAsync(b => b.Slug == model.Slug);
        if (existingSlug)
        {
            ModelState.AddModelError("Slug", "Bu URL slug zaten kullanılıyor");
            return View(model);
        }
        
        var post = new BlogPost
        {
            Slug = model.Slug,
            TitleTr = model.TitleTr,
            TitleEn = model.TitleEn,
            ExcerptTr = model.ExcerptTr,
            ExcerptEn = model.ExcerptEn,
            ContentTr = model.ContentTr ?? "",
            ContentEn = model.ContentEn,
            KeywordsTr = model.KeywordsTr,
            KeywordsEn = model.KeywordsEn,
            MetaDescriptionTr = model.MetaDescriptionTr,
            MetaDescriptionEn = model.MetaDescriptionEn,
            OgImageUrl = model.OgImageUrl,
            CanonicalUrl = model.CanonicalUrl,
            RobotsMeta = model.RobotsMeta,
            IsPublished = model.IsPublished,
            IsFeatured = model.IsFeatured,
            AuthorName = model.AuthorName,
            PublishedAt = model.PublishedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.BlogPosts.Add(post);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.ContentCreated, 
            $"Blog yazısı oluşturuldu: {post.TitleTr}", 
            "BlogPost", post.Id);
        
        TempData["Success"] = "Blog yazısı başarıyla oluşturuldu";
        return RedirectToAction("BlogPosts");
    }
    
    [HttpGet]
    [Route("admin/blog/edit/{id}")]
    public async Task<IActionResult> EditBlog(int id)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction("Login");
        
        ViewBag.IsSuperAdmin = IsSuperAdmin();
        
        var post = await _context.BlogPosts.FindAsync(id);
        if (post == null)
            return NotFound();
        
        var model = new BlogPostEditViewModel
        {
            Id = post.Id,
            Slug = post.Slug,
            TitleTr = post.TitleTr,
            TitleEn = post.TitleEn,
            ExcerptTr = post.ExcerptTr,
            ExcerptEn = post.ExcerptEn,
            ContentTr = post.ContentTr,
            ContentEn = post.ContentEn,
            KeywordsTr = post.KeywordsTr,
            KeywordsEn = post.KeywordsEn,
            MetaDescriptionTr = post.MetaDescriptionTr,
            MetaDescriptionEn = post.MetaDescriptionEn,
            OgImageUrl = post.OgImageUrl,
            CanonicalUrl = post.CanonicalUrl,
            RobotsMeta = post.RobotsMeta,
            IsPublished = post.IsPublished,
            IsFeatured = post.IsFeatured,
            AuthorName = post.AuthorName,
            PublishedAt = post.PublishedAt,
            ViewCount = post.ViewCount
        };
        
        return View(model);
    }
    
    [HttpPost]
    [Route("admin/blog/edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBlog(int id, BlogPostEditViewModel model)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction("Login");
        
        ViewBag.IsSuperAdmin = IsSuperAdmin();
        
        if (!ModelState.IsValid)
            return View(model);
        
        var post = await _context.BlogPosts.FindAsync(id);
        if (post == null)
            return NotFound();
        
        // Check slug uniqueness (excluding current)
        var existingSlug = await _context.BlogPosts.AnyAsync(b => b.Slug == model.Slug && b.Id != id);
        if (existingSlug)
        {
            ModelState.AddModelError("Slug", "Bu URL slug zaten kullanılıyor");
            return View(model);
        }
        
        post.Slug = model.Slug;
        post.TitleTr = model.TitleTr;
        post.TitleEn = model.TitleEn;
        post.ExcerptTr = model.ExcerptTr;
        post.ExcerptEn = model.ExcerptEn;
        post.ContentTr = model.ContentTr ?? "";
        post.ContentEn = model.ContentEn;
        post.KeywordsTr = model.KeywordsTr;
        post.KeywordsEn = model.KeywordsEn;
        post.MetaDescriptionTr = model.MetaDescriptionTr;
        post.MetaDescriptionEn = model.MetaDescriptionEn;
        post.OgImageUrl = model.OgImageUrl;
        post.CanonicalUrl = model.CanonicalUrl;
        post.RobotsMeta = model.RobotsMeta;
        post.IsPublished = model.IsPublished;
        post.IsFeatured = model.IsFeatured;
        post.AuthorName = model.AuthorName;
        post.PublishedAt = model.PublishedAt ?? post.PublishedAt;
        post.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.ContentUpdated, 
            $"Blog yazısı güncellendi: {post.TitleTr}", 
            "BlogPost", post.Id);
        
        TempData["Success"] = "Blog yazısı başarıyla güncellendi";
        return RedirectToAction("BlogPosts");
    }
    
    [HttpPost]
    [Route("admin/blog/delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlog(int id)
    {
        if (!IsAdminLoggedIn())
            return Unauthorized();
        
        var post = await _context.BlogPosts.FindAsync(id);
        if (post == null)
            return NotFound();
        
        var title = post.TitleTr;
        _context.BlogPosts.Remove(post);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.ContentDeleted, 
            $"Blog yazısı silindi: {title}", 
            "BlogPost", id);
        
        TempData["Success"] = "Blog yazısı silindi";
        return RedirectToAction("BlogPosts");
    }
    
    [HttpPost]
    [Route("admin/blog/toggle-publish/{id}")]
    public async Task<IActionResult> ToggleBlogPublish(int id)
    {
        if (!IsAdminLoggedIn())
            return Unauthorized();
        
        var post = await _context.BlogPosts.FindAsync(id);
        if (post == null)
            return NotFound();
        
        post.IsPublished = !post.IsPublished;
        post.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return Json(new { success = true, isPublished = post.IsPublished });
    }
    
    #endregion
    
    #region System Settings Management
    
    /// <summary>
    /// Sistem ayarları listesi
    /// </summary>
    [HttpGet]
    [Route("admin/settings")]
    public async Task<IActionResult> SystemSettings()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction("Login");
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu sayfaya erişim yetkiniz yok";
            return RedirectToAction("Index");
        }
        
        ViewBag.IsSuperAdmin = IsSuperAdmin();
        
        var settings = await _settingsService.GetAllSettingsAsync();
        
        // Kategorilere göre grupla
        var grouped = settings
            .GroupBy(s => s.Category)
            .OrderBy(g => GetCategoryOrder(g.Key))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SortOrder).ToList());
        
        var model = new SystemSettingsViewModel
        {
            SettingsByCategory = grouped,
            CategoryNames = GetCategoryNames()
        };
        
        return View(model);
    }
    
    /// <summary>
    /// Tek bir ayarı güncelle (AJAX)
    /// </summary>
    [HttpPost]
    [Route("admin/settings/update")]
    public async Task<IActionResult> UpdateSetting([FromBody] UpdateSettingRequest request)
    {
        if (!IsAdminLoggedIn())
            return Unauthorized();
        
        if (!IsSuperAdmin())
            return Forbid();
        
        if (string.IsNullOrEmpty(request.Key) || request.Value == null)
            return BadRequest(new { error = "Key ve Value gereklidir" });
        
        try
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == request.Key);
            if (setting == null)
                return NotFound(new { error = "Ayar bulunamadı" });
            
            var oldValue = setting.Value;
            
            // Veri tipi doğrulaması
            if (!ValidateSettingValue(setting.DataType, request.Value))
                return BadRequest(new { error = $"Geçersiz değer formatı. Beklenen tip: {setting.DataType}" });
            
            setting.Value = request.Value;
            setting.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            // Cache temizle
            _settingsService.ClearCache();
            
            // Log kaydet
            await _activityLog.LogAsync(ActivityType.UserSettingsUpdated, 
                $"Sistem ayarı güncellendi: {setting.Key} = {setting.Value} (eski: {oldValue})", 
                "SystemSettings", setting.Id);
            
            return Json(new { success = true, message = "Ayar güncellendi" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// Tüm ayarları toplu güncelle
    /// </summary>
    [HttpPost]
    [Route("admin/settings/save-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAllSettings(Dictionary<string, string> settings)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction("Login");
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu işlem için yetkiniz yok";
            return RedirectToAction("Index");
        }
        
        try
        {
            var dbSettings = await _context.SystemSettings.ToListAsync();
            var changedCount = 0;
            
            foreach (var kvp in settings)
            {
                var setting = dbSettings.FirstOrDefault(s => s.Key == kvp.Key);
                if (setting != null && setting.Value != kvp.Value)
                {
                    if (!ValidateSettingValue(setting.DataType, kvp.Value))
                        continue;
                    
                    setting.Value = kvp.Value;
                    setting.UpdatedAt = DateTime.UtcNow;
                    changedCount++;
                }
            }
            
            if (changedCount > 0)
            {
                await _context.SaveChangesAsync();
                _settingsService.ClearCache();
                
                await _activityLog.LogAsync(ActivityType.UserSettingsUpdated, 
                    $"{changedCount} sistem ayarı güncellendi");
            }
            
            TempData["Success"] = $"{changedCount} ayar başarıyla güncellendi";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Hata: {ex.Message}";
        }
        
        return RedirectToAction("SystemSettings");
    }
    
    /// <summary>
    /// Yeni ayar ekle
    /// </summary>
    [HttpPost]
    [Route("admin/settings/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSetting(CreateSettingRequest request)
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction("Login");
        
        if (!IsSuperAdmin())
        {
            TempData["Error"] = "Bu işlem için yetkiniz yok";
            return RedirectToAction("Index");
        }
        
        if (string.IsNullOrEmpty(request.Key))
        {
            TempData["Error"] = "Ayar anahtarı gereklidir";
            return RedirectToAction("SystemSettings");
        }
        
        // Anahtar benzersizliği kontrolü
        var exists = await _context.SystemSettings.AnyAsync(s => s.Key == request.Key);
        if (exists)
        {
            TempData["Error"] = "Bu anahtar zaten mevcut";
            return RedirectToAction("SystemSettings");
        }
        
        var maxSortOrder = await _context.SystemSettings
            .Where(s => s.Category == request.Category)
            .MaxAsync(s => (int?)s.SortOrder) ?? 0;
        
        var setting = new Data.Entities.SystemSettings
        {
            Key = request.Key,
            Value = request.Value ?? "",
            Description = request.Description,
            Category = request.Category ?? Data.Entities.SystemSettings.Categories.General,
            DataType = request.DataType ?? "string",
            SortOrder = maxSortOrder + 1,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.SystemSettings.Add(setting);
        await _context.SaveChangesAsync();
        
        _settingsService.ClearCache();
        
        await _activityLog.LogAsync(ActivityType.UserSettingsUpdated, 
            $"Yeni sistem ayarı oluşturuldu: {setting.Key}", 
            "SystemSettings", setting.Id);
        
        TempData["Success"] = "Yeni ayar oluşturuldu";
        return RedirectToAction("SystemSettings");
    }
    
    /// <summary>
    /// Ayarı sil (sadece özel eklenen ayarlar silinebilir)
    /// </summary>
    [HttpPost]
    [Route("admin/settings/delete/{id}")]
    public async Task<IActionResult> DeleteSetting(int id)
    {
        if (!IsAdminLoggedIn())
            return Unauthorized();
        
        if (!IsSuperAdmin())
            return Forbid();
        
        var setting = await _context.SystemSettings.FindAsync(id);
        if (setting == null)
            return NotFound(new { error = "Ayar bulunamadı" });
        
        // Sistem ayarlarını silmeye izin verme
        var systemKeys = typeof(Data.Entities.SystemSettings.Keys)
            .GetFields()
            .Select(f => f.GetValue(null)?.ToString())
            .ToHashSet();
        
        if (systemKeys.Contains(setting.Key))
            return BadRequest(new { error = "Sistem ayarları silinemez" });
        
        var key = setting.Key;
        _context.SystemSettings.Remove(setting);
        await _context.SaveChangesAsync();
        
        _settingsService.ClearCache();
        
        await _activityLog.LogAsync(ActivityType.UserSettingsUpdated, 
            $"Sistem ayarı silindi: {key}");
        
        return Json(new { success = true });
    }
    
    private bool ValidateSettingValue(string dataType, string value)
    {
        return dataType switch
        {
            "int" => int.TryParse(value, out _),
            "decimal" => decimal.TryParse(value, out _),
            "bool" => bool.TryParse(value, out _) || value == "0" || value == "1",
            _ => true // string her zaman geçerli
        };
    }
    
    private int GetCategoryOrder(string category)
    {
        return category switch
        {
            Data.Entities.SystemSettings.Categories.General => 1,
            Data.Entities.SystemSettings.Categories.EmployeeLimits => 2,
            Data.Entities.SystemSettings.Categories.WorkSettings => 3,
            Data.Entities.SystemSettings.Categories.CleaningLimits => 4,
            Data.Entities.SystemSettings.Categories.QrMenuLimits => 5,
            Data.Entities.SystemSettings.Categories.UnitLimits => 6,
            Data.Entities.SystemSettings.Categories.Security => 7,
            _ => 99
        };
    }
    
    private Dictionary<string, string> GetCategoryNames()
    {
        return new Dictionary<string, string>
        {
            { Data.Entities.SystemSettings.Categories.General, "🌐 Genel Ayarlar" },
            { Data.Entities.SystemSettings.Categories.EmployeeLimits, "👥 Personel Limitleri" },
            { Data.Entities.SystemSettings.Categories.WorkSettings, "⏰ Çalışma Ayarları" },
            { Data.Entities.SystemSettings.Categories.CleaningLimits, "🧹 Temizlik Modülü Limitleri" },
            { Data.Entities.SystemSettings.Categories.UnitLimits, "🏢 Birim Limitleri" },
            { Data.Entities.SystemSettings.Categories.Security, "🔒 Güvenlik Ayarları" },
            { Data.Entities.SystemSettings.Categories.QrMenuLimits, "🍽️ QR Menü Limitleri" }
        };
    }
    
    #endregion
}


