using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Controllers;

public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private const string AdminUsername = "Geldimmix";
    private const string AdminPassword = "Liberemall423445!!";
    private const string AdminSessionKey = "IsAdmin";

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
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


