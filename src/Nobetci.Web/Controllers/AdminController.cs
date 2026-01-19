using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Controllers;

public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private const string AdminUsername = "Geldimmix";
    private const string AdminPassword = "Liberemall423445";
    private const string AdminSessionKey = "IsAdmin";

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
    }

    private bool IsAdminLoggedIn()
    {
        return HttpContext.Session.GetString(AdminSessionKey) == "true";
    }

    // GET: /admin
    public IActionResult Index()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

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
    public IActionResult Login(string username, string password)
    {
        if (username == AdminUsername && password == AdminPassword)
        {
            HttpContext.Session.SetString(AdminSessionKey, "true");
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Error = "Geçersiz kullanıcı adı veya parola";
        return View();
    }

    // GET: /admin/logout
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(AdminSessionKey);
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

    // GET: /admin/settings
    public IActionResult Settings()
    {
        if (!IsAdminLoggedIn())
            return RedirectToAction(nameof(Login));

        return View();
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

        // Get default limits from config
        var guestLimit = _configuration.GetValue<int>("AppSettings:GuestEmployeeLimit", 5);
        var registeredLimit = _configuration.GetValue<int>("AppSettings:RegisteredEmployeeLimit", 10);
        var premiumLimit = _configuration.GetValue<int>("AppSettings:PremiumEmployeeLimit", 100);

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.Search = search;
        ViewBag.GuestLimit = guestLimit;
        ViewBag.RegisteredLimit = registeredLimit;
        ViewBag.PremiumLimit = premiumLimit;

        return View(users);
    }
    
    // GET: /admin/users/edit/{id}
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
        
        // Get default limits from config
        ViewBag.GuestLimit = _configuration.GetValue<int>("AppSettings:GuestEmployeeLimit", 5);
        ViewBag.RegisteredLimit = _configuration.GetValue<int>("AppSettings:RegisteredEmployeeLimit", 10);
        ViewBag.PremiumLimit = _configuration.GetValue<int>("AppSettings:PremiumEmployeeLimit", 100);

        return View(viewModel);
    }
    
    // POST: /admin/users/edit/{id}
    [HttpPost]
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
}

public class AdminStatsViewModel
{
    public int TotalVisits { get; set; }
    public int TodayVisits { get; set; }
    public int WeekVisits { get; set; }
    public int MonthVisits { get; set; }
    public int UniqueIPs { get; set; }
    public List<PageStat> TopPages { get; set; } = new();
    public List<DeviceStat> DeviceStats { get; set; } = new();
    public List<BrowserStat> BrowserStats { get; set; } = new();
    public int TotalPages { get; set; }
    public int PublishedPages { get; set; }
    public int TotalEmployees { get; set; }
    public int TotalOrganizations { get; set; }
}

public class PageStat
{
    public string Path { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DeviceStat
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class BrowserStat
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class UserListItem
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserPlan Plan { get; set; }
    public int? CustomEmployeeLimit { get; set; }
    public bool CanAccessAttendance { get; set; }
    public bool CanAccessPayroll { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? AdminNotes { get; set; }
    public int OrganizationCount { get; set; }
    public int EmployeeCount { get; set; }
    
    public int GetEffectiveLimit(int registeredLimit, int premiumLimit)
    {
        if (CustomEmployeeLimit.HasValue)
            return CustomEmployeeLimit.Value;
        
        return Plan switch
        {
            UserPlan.Premium => premiumLimit,
            _ => registeredLimit
        };
    }
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserPlan Plan { get; set; }
    public int? CustomEmployeeLimit { get; set; }
    public bool CanAccessAttendance { get; set; } = true;
    public bool CanAccessPayroll { get; set; } = true;
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<OrganizationSummary> Organizations { get; set; } = new();
}

public class OrganizationSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
}

public class QuickUpdateUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public int? IntValue { get; set; }
    public bool? BoolValue { get; set; }
    public string? StringValue { get; set; }
}


