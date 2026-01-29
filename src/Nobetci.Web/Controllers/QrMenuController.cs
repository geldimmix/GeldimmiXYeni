using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Services;
using System.Text.Json;
using QRCoder;

namespace Nobetci.Web.Controllers;

/// <summary>
/// QR Menü Controller - Restoran menü yönetimi
/// </summary>
public class QrMenuController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemSettingsService _settingsService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<QrMenuController> _logger;

    public QrMenuController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ISystemSettingsService settingsService,
        IWebHostEnvironment environment,
        ILogger<QrMenuController> logger)
    {
        _context = context;
        _userManager = userManager;
        _settingsService = settingsService;
        _environment = environment;
        _logger = logger;
    }

    #region Helper Methods

    private string? GetSessionId()
    {
        if (!HttpContext.Session.Keys.Contains("QrMenuSessionId"))
        {
            HttpContext.Session.SetString("QrMenuSessionId", Guid.NewGuid().ToString("N"));
        }
        return HttpContext.Session.GetString("QrMenuSessionId");
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        return User.Identity?.IsAuthenticated == true
            ? await _userManager.GetUserAsync(User)
            : null;
    }

    private async Task<QrMenuLimits> GetLimitsAsync(ApplicationUser? user)
    {
        if (user == null)
        {
            return new QrMenuLimits
            {
                MenuLimit = await _settingsService.GetQrMenuUnregisteredMenuLimitAsync(),
                TableLimit = 0,
                DailyAccessLimit = await _settingsService.GetQrMenuUnregisteredDailyAccessLimitAsync(),
                CategoryLimit = await _settingsService.GetQrMenuUnregisteredCategoryLimitAsync(),
                ItemLimit = await _settingsService.GetQrMenuUnregisteredItemLimitAsync(),
                CanUploadImages = false,
                CanManageTables = false,
                CanManageOrders = false
            };
        }

        if (user.Plan == UserPlan.Premium)
        {
            return new QrMenuLimits
            {
                MenuLimit = await _settingsService.GetQrMenuPremiumMenuLimitAsync(),
                TableLimit = await _settingsService.GetQrMenuPremiumTableLimitAsync(),
                DailyAccessLimit = await _settingsService.GetQrMenuPremiumDailyAccessLimitAsync(),
                CategoryLimit = await _settingsService.GetQrMenuPremiumCategoryLimitAsync(),
                ItemLimit = await _settingsService.GetQrMenuPremiumItemLimitAsync(),
                CanUploadImages = await _settingsService.GetQrMenuPremiumImageUploadEnabledAsync(),
                MaxImageSizeKB = await _settingsService.GetQrMenuPremiumMaxImageSizeKBAsync(),
                CanManageTables = true,
                CanManageOrders = true
            };
        }

        // Registered (Free)
        return new QrMenuLimits
        {
            MenuLimit = await _settingsService.GetQrMenuRegisteredMenuLimitAsync(),
            TableLimit = await _settingsService.GetQrMenuRegisteredTableLimitAsync(),
            DailyAccessLimit = await _settingsService.GetQrMenuRegisteredDailyAccessLimitAsync(),
            CategoryLimit = await _settingsService.GetQrMenuRegisteredCategoryLimitAsync(),
            ItemLimit = await _settingsService.GetQrMenuRegisteredItemLimitAsync(),
            CanUploadImages = false,
            CanManageTables = true,
            CanManageOrders = true
        };
    }

    private async Task<QrMenu?> GetUserMenuAsync(ApplicationUser? user, string? sessionId)
    {
        if (user != null)
        {
            return await _context.QrMenus
                .Include(m => m.Categories.OrderBy(c => c.DisplayOrder))
                    .ThenInclude(c => c.Items.OrderBy(i => i.DisplayOrder))
                .Include(m => m.Categories)
                    .ThenInclude(c => c.SubCategories.OrderBy(sc => sc.DisplayOrder))
                        .ThenInclude(sc => sc.Items.OrderBy(i => i.DisplayOrder))
                .Include(m => m.Tables.OrderBy(t => t.DisplayOrder))
                .FirstOrDefaultAsync(m => m.UserId == user.Id);
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            return await _context.QrMenus
                .Include(m => m.Categories.OrderBy(c => c.DisplayOrder))
                    .ThenInclude(c => c.Items.OrderBy(i => i.DisplayOrder))
                .FirstOrDefaultAsync(m => m.SessionId == sessionId && m.UserId == null);
        }

        return null;
    }

    private async Task<int> GetTodayAccessCountAsync(int menuId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _context.QrMenuAccesses
            .Where(a => a.MenuId == menuId && a.AccessDate == today)
            .CountAsync();
    }

    private string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace("ş", "s").Replace("ı", "i").Replace("ğ", "g")
            .Replace("ü", "u").Replace("ö", "o").Replace("ç", "c")
            .Replace(" ", "-");
        
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-").Trim('-');
        
        return $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
    }

    private string GenerateQrCode()
    {
        return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    private string GenerateOrderNumber()
    {
        return $"S{DateTime.UtcNow:yyMMdd}{new Random().Next(1000, 9999)}";
    }

    #endregion

    #region Menu Management

    /// <summary>
    /// Ana QR Menü sayfası
    /// </summary>
    [Route("qrmenu")]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);
        var limits = await GetLimitsAsync(user);

        var model = new QrMenuViewModel
        {
            Menu = menu,
            Limits = limits,
            IsAuthenticated = user != null,
            IsPremium = user?.Plan == UserPlan.Premium,
            Currencies = MenuCurrencies.All,
            Languages = MenuLanguages.All
        };

        if (menu != null)
        {
            model.CategoryCount = await _context.QrMenuCategories.CountAsync(c => c.MenuId == menu.Id);
            model.ItemCount = await _context.QrMenuItems
                .Where(i => i.Category.MenuId == menu.Id)
                .CountAsync();
            model.TodayAccessCount = await GetTodayAccessCountAsync(menu.Id);
        }

        return View(model);
    }

    /// <summary>
    /// Yeni menü oluştur
    /// </summary>
    [HttpPost]
    [Route("qrmenu/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMenu([FromForm] CreateMenuRequest request)
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var limits = await GetLimitsAsync(user);

        // Check existing menu count
        int existingCount = 0;
        if (user != null)
        {
            existingCount = await _context.QrMenus.CountAsync(m => m.UserId == user.Id);
        }
        else if (!string.IsNullOrEmpty(sessionId))
        {
            existingCount = await _context.QrMenus.CountAsync(m => m.SessionId == sessionId && m.UserId == null);
        }

        if (existingCount >= limits.MenuLimit)
        {
            TempData["Error"] = $"Maksimum {limits.MenuLimit} menü oluşturabilirsiniz.";
            return RedirectToAction(nameof(Index));
        }

        var menu = new QrMenu
        {
            Name = request.Name,
            Description = request.Description,
            Currency = request.Currency ?? "TRY",
            Language = request.Language ?? "tr",
            RestaurantName = request.RestaurantName,
            RestaurantPhone = request.RestaurantPhone,
            Slug = GenerateSlug(request.Name),
            UserId = user?.Id,
            SessionId = user == null ? sessionId : null,
            AcceptOrders = user != null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.QrMenus.Add(menu);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Menü başarıyla oluşturuldu!";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Menü güncelle
    /// </summary>
    [HttpPost]
    [Route("qrmenu/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMenu([FromForm] UpdateMenuRequest request)
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);

        if (menu == null)
        {
            TempData["Error"] = "Menü bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        menu.Name = request.Name;
        menu.Description = request.Description;
        menu.Currency = request.Currency ?? menu.Currency;
        menu.Language = request.Language ?? menu.Language;
        menu.RestaurantName = request.RestaurantName;
        menu.RestaurantPhone = request.RestaurantPhone;
        menu.RestaurantAddress = request.RestaurantAddress;
        menu.PrimaryColor = request.PrimaryColor ?? menu.PrimaryColor;
        menu.SecondaryColor = request.SecondaryColor ?? menu.SecondaryColor;
        menu.AcceptOrders = request.AcceptOrders;
        menu.IsActive = request.IsActive;
        menu.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Menü güncellendi!";
        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Category Management

    /// <summary>
    /// Kategori ekle
    /// </summary>
    [HttpPost]
    [Route("qrmenu/category/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory([FromForm] AddCategoryRequest request)
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);
        var limits = await GetLimitsAsync(user);

        if (menu == null)
        {
            return Json(new { success = false, message = "Önce menü oluşturmalısınız." });
        }

        var categoryCount = await _context.QrMenuCategories.CountAsync(c => c.MenuId == menu.Id);
        if (categoryCount >= limits.CategoryLimit)
        {
            return Json(new { success = false, message = $"Maksimum {limits.CategoryLimit} kategori oluşturabilirsiniz." });
        }

        var category = new QrMenuCategory
        {
            MenuId = menu.Id,
            ParentCategoryId = request.ParentCategoryId,
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            DisplayOrder = categoryCount + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.QrMenuCategories.Add(category);
        await _context.SaveChangesAsync();

        return Json(new { success = true, categoryId = category.Id, message = "Kategori eklendi!" });
    }

    /// <summary>
    /// Kategori güncelle
    /// </summary>
    [HttpPost]
    [Route("qrmenu/category/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCategory([FromForm] UpdateCategoryRequest request)
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);

        if (menu == null)
        {
            return Json(new { success = false, message = "Menü bulunamadı." });
        }

        var category = await _context.QrMenuCategories
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.MenuId == menu.Id);

        if (category == null)
        {
            return Json(new { success = false, message = "Kategori bulunamadı." });
        }

        category.Name = request.Name;
        category.Description = request.Description;
        category.Icon = request.Icon;
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Kategori güncellendi!" });
    }

    /// <summary>
    /// Kategori sil
    /// </summary>
    [HttpPost]
    [Route("qrmenu/category/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory([FromForm] int id)
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);

        if (menu == null)
        {
            return Json(new { success = false, message = "Menü bulunamadı." });
        }

        var category = await _context.QrMenuCategories
            .Include(c => c.Items)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id && c.MenuId == menu.Id);

        if (category == null)
        {
            return Json(new { success = false, message = "Kategori bulunamadı." });
        }

        // Check if has sub-categories
        if (category.SubCategories.Any())
        {
            return Json(new { success = false, message = "Alt kategorileri olan kategori silinemez." });
        }

        _context.QrMenuCategories.Remove(category);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Kategori silindi!" });
    }

    #endregion

    #region Item Management

    /// <summary>
    /// Ürün ekle
    /// </summary>
    [HttpPost]
    [Route("qrmenu/item/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem([FromForm] AddItemRequest request)
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);
        var limits = await GetLimitsAsync(user);

        if (menu == null)
        {
            return Json(new { success = false, message = "Önce menü oluşturmalısınız." });
        }

        var itemCount = await _context.QrMenuItems
            .Where(i => i.Category.MenuId == menu.Id)
            .CountAsync();

        if (itemCount >= limits.ItemLimit)
        {
            return Json(new { success = false, message = $"Maksimum {limits.ItemLimit} ürün ekleyebilirsiniz." });
        }

        var category = await _context.QrMenuCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.MenuId == menu.Id);

        if (category == null)
        {
            return Json(new { success = false, message = "Kategori bulunamadı." });
        }

        var item = new QrMenuItem
        {
            CategoryId = request.CategoryId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            DiscountedPrice = request.DiscountedPrice,
            Calories = request.Calories,
            PrepTimeMinutes = request.PrepTimeMinutes,
            Allergens = request.Allergens,
            Tags = request.Tags,
            PortionSize = request.PortionSize,
            DisplayOrder = itemCount + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.QrMenuItems.Add(item);
        await _context.SaveChangesAsync();

        return Json(new { success = true, itemId = item.Id, message = "Ürün eklendi!" });
    }

    /// <summary>
    /// Ürün güncelle
    /// </summary>
    [HttpPost]
    [Route("qrmenu/item/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem([FromForm] UpdateItemRequest request)
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);

        if (menu == null)
        {
            return Json(new { success = false, message = "Menü bulunamadı." });
        }

        var item = await _context.QrMenuItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == request.Id && i.Category.MenuId == menu.Id);

        if (item == null)
        {
            return Json(new { success = false, message = "Ürün bulunamadı." });
        }

        item.Name = request.Name;
        item.Description = request.Description;
        item.Price = request.Price;
        item.DiscountedPrice = request.DiscountedPrice;
        item.Calories = request.Calories;
        item.PrepTimeMinutes = request.PrepTimeMinutes;
        item.Allergens = request.Allergens;
        item.Tags = request.Tags;
        item.PortionSize = request.PortionSize;
        item.DisplayOrder = request.DisplayOrder;
        item.IsActive = request.IsActive;
        item.InStock = request.InStock;
        item.IsFeatured = request.IsFeatured;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Ürün güncellendi!" });
    }

    /// <summary>
    /// Ürün sil
    /// </summary>
    [HttpPost]
    [Route("qrmenu/item/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem([FromForm] int id)
    {
        var user = await GetCurrentUserAsync();
        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);

        if (menu == null)
        {
            return Json(new { success = false, message = "Menü bulunamadı." });
        }

        var item = await _context.QrMenuItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id && i.Category.MenuId == menu.Id);

        if (item == null)
        {
            return Json(new { success = false, message = "Ürün bulunamadı." });
        }

        _context.QrMenuItems.Remove(item);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Ürün silindi!" });
    }

    /// <summary>
    /// Ürün resmi yükle (Premium)
    /// </summary>
    [HttpPost]
    [Route("qrmenu/item/upload-image")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadItemImage(int itemId, IFormFile image)
    {
        var user = await GetCurrentUserAsync();
        if (user?.Plan != UserPlan.Premium)
        {
            return Json(new { success = false, message = "Bu özellik sadece Premium kullanıcılar içindir." });
        }

        var limits = await GetLimitsAsync(user);
        if (!limits.CanUploadImages)
        {
            return Json(new { success = false, message = "Resim yükleme özelliği aktif değil." });
        }

        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);

        if (menu == null)
        {
            return Json(new { success = false, message = "Menü bulunamadı." });
        }

        var item = await _context.QrMenuItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Category.MenuId == menu.Id);

        if (item == null)
        {
            return Json(new { success = false, message = "Ürün bulunamadı." });
        }

        if (image == null || image.Length == 0)
        {
            return Json(new { success = false, message = "Resim seçilmedi." });
        }

        // Check file size
        var maxSizeBytes = limits.MaxImageSizeKB * 1024;
        if (image.Length > maxSizeBytes)
        {
            return Json(new { success = false, message = $"Resim boyutu maksimum {limits.MaxImageSizeKB}KB olmalıdır." });
        }

        // Check file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(image.ContentType.ToLower()))
        {
            return Json(new { success = false, message = "Sadece JPEG, PNG ve WebP formatları desteklenir." });
        }

        try
        {
            // Create directory if not exists
            var uploadsDir = Path.Combine(_environment.ContentRootPath, "AppData", "QrMenuImages", menu.Id.ToString());
            Directory.CreateDirectory(uploadsDir);

            // Delete old image if exists
            if (!string.IsNullOrEmpty(item.ImageUrl))
            {
                var oldPath = Path.Combine(_environment.ContentRootPath, "AppData", item.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            // Save new image
            var extension = Path.GetExtension(image.FileName).ToLower();
            var fileName = $"{item.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            item.ImageUrl = $"/QrMenuImages/{menu.Id}/{fileName}";
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, imageUrl = item.ImageUrl, message = "Resim yüklendi!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading item image");
            return Json(new { success = false, message = "Resim yüklenirken bir hata oluştu." });
        }
    }

    #endregion

    #region Table Management

    /// <summary>
    /// Masa ekle
    /// </summary>
    [HttpPost]
    [Route("qrmenu/table/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTable([FromForm] AddTableRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Json(new { success = false, message = "Masa yönetimi için giriş yapmalısınız." });
        }

        var sessionId = GetSessionId();
        var menu = await GetUserMenuAsync(user, sessionId);
        var limits = await GetLimitsAsync(user);

        if (menu == null)
        {
            return Json(new { success = false, message = "Önce menü oluşturmalısınız." });
        }

        if (!limits.CanManageTables)
        {
            return Json(new { success = false, message = "Masa yönetimi için kayıt olmalısınız." });
        }

        var tableCount = await _context.QrMenuTables.CountAsync(t => t.MenuId == menu.Id);
        if (tableCount >= limits.TableLimit)
        {
            return Json(new { success = false, message = $"Maksimum {limits.TableLimit} masa ekleyebilirsiniz." });
        }

        var table = new QrMenuTable
        {
            MenuId = menu.Id,
            TableNumber = request.TableNumber,
            Description = request.Description,
            Capacity = request.Capacity,
            QrCode = GenerateQrCode(),
            DisplayOrder = tableCount + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.QrMenuTables.Add(table);
        await _context.SaveChangesAsync();

        return Json(new { success = true, tableId = table.Id, qrCode = table.QrCode, message = "Masa eklendi!" });
    }

    /// <summary>
    /// Masa sil
    /// </summary>
    [HttpPost]
    [Route("qrmenu/table/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTable([FromForm] int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Json(new { success = false, message = "Giriş yapmalısınız." });
        }

        var menu = await GetUserMenuAsync(user, null);

        if (menu == null)
        {
            return Json(new { success = false, message = "Menü bulunamadı." });
        }

        var table = await _context.QrMenuTables
            .FirstOrDefaultAsync(t => t.Id == id && t.MenuId == menu.Id);

        if (table == null)
        {
            return Json(new { success = false, message = "Masa bulunamadı." });
        }

        _context.QrMenuTables.Remove(table);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Masa silindi!" });
    }

    /// <summary>
    /// Tüm masaların QR kodlarını yazdır
    /// </summary>
    [Route("qrmenu/tables/print")]
    public async Task<IActionResult> PrintTableQrCodes()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var menu = await _context.QrMenus
            .Include(m => m.Tables.OrderBy(t => t.DisplayOrder))
            .FirstOrDefaultAsync(m => m.UserId == user.Id);

        if (menu == null || !menu.Tables.Any())
        {
            TempData["Error"] = "Yazdırılacak masa bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        var model = new PrintTableQrCodesViewModel
        {
            Menu = menu,
            BaseUrl = $"{Request.Scheme}://{Request.Host}"
        };

        return View(model);
    }

    #endregion

    #region Public Menu View (Customer Facing)

    /// <summary>
    /// Müşteri tarafı menü görüntüleme
    /// </summary>
    [Route("m/{slug}")]
    [Route("m/{slug}/{tableQr?}")]
    public async Task<IActionResult> ViewMenu(string slug, string? tableQr = null)
    {
        var menu = await _context.QrMenus
            .Include(m => m.Categories.Where(c => c.IsActive))
                .ThenInclude(c => c.Items.Where(i => i.IsActive))
            .Include(m => m.Categories.Where(c => c.IsActive))
                .ThenInclude(c => c.SubCategories.Where(sc => sc.IsActive))
                    .ThenInclude(sc => sc.Items.Where(i => i.IsActive))
            .FirstOrDefaultAsync(m => m.Slug == slug && m.IsActive);

        if (menu == null)
        {
            return NotFound();
        }

        // Check access limit
        QrMenuTable? table = null;
        if (!string.IsNullOrEmpty(tableQr))
        {
            table = await _context.QrMenuTables
                .FirstOrDefaultAsync(t => t.MenuId == menu.Id && t.QrCode == tableQr && t.IsActive);
        }

        // Get limits for this menu's owner
        var menuOwner = menu.UserId != null 
            ? await _userManager.FindByIdAsync(menu.UserId)
            : null;
        var limits = await GetLimitsAsync(menuOwner);
        
        var todayAccess = await GetTodayAccessCountAsync(menu.Id);
        if (todayAccess >= limits.DailyAccessLimit)
        {
            return View("AccessLimitReached", new AccessLimitReachedViewModel
            {
                MenuName = menu.Name,
                DailyLimit = limits.DailyAccessLimit,
                IsOwnerRegistered = menuOwner != null
            });
        }

        // Log access
        var access = new QrMenuAccess
        {
            MenuId = menu.Id,
            TableId = table?.Id,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].ToString(),
            SessionId = HttpContext.Session.Id,
            AccessedAt = DateTime.UtcNow,
            AccessDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _context.QrMenuAccesses.Add(access);
        await _context.SaveChangesAsync();

        var model = new PublicMenuViewModel
        {
            Menu = menu,
            Table = table,
            CurrencySymbol = MenuCurrencies.GetSymbol(menu.Currency),
            CanOrder = menu.AcceptOrders && menuOwner != null
        };

        return View(model);
    }

    #endregion

    #region Orders

    /// <summary>
    /// Sipariş oluştur (müşteri tarafı)
    /// </summary>
    [HttpPost]
    [Route("m/{slug}/order")]
    public async Task<IActionResult> CreateOrder(string slug, [FromBody] CreateOrderRequest request)
    {
        var menu = await _context.QrMenus
            .FirstOrDefaultAsync(m => m.Slug == slug && m.IsActive && m.AcceptOrders);

        if (menu == null)
        {
            return Json(new { success = false, message = "Menü bulunamadı veya sipariş kabul etmiyor." });
        }

        QrMenuTable? table = null;
        if (!string.IsNullOrEmpty(request.TableQrCode))
        {
            table = await _context.QrMenuTables
                .FirstOrDefaultAsync(t => t.MenuId == menu.Id && t.QrCode == request.TableQrCode);
        }

        if (request.Items == null || !request.Items.Any())
        {
            return Json(new { success = false, message = "Sipariş için ürün seçilmedi." });
        }

        // Validate items and calculate total
        decimal total = 0;
        var orderItems = new List<QrMenuOrderItem>();

        foreach (var item in request.Items)
        {
            var menuItem = await _context.QrMenuItems
                .Include(i => i.Category)
                .FirstOrDefaultAsync(i => i.Id == item.ItemId && i.Category.MenuId == menu.Id && i.IsActive && i.InStock);

            if (menuItem == null)
            {
                return Json(new { success = false, message = $"Ürün bulunamadı veya stokta yok: {item.ItemId}" });
            }

            var unitPrice = menuItem.DiscountedPrice ?? menuItem.Price;
            var itemTotal = unitPrice * item.Quantity;
            total += itemTotal;

            orderItems.Add(new QrMenuOrderItem
            {
                MenuItemId = menuItem.Id,
                ItemName = menuItem.Name,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = itemTotal,
                Note = item.Note
            });
        }

        var order = new QrMenuOrder
        {
            MenuId = menu.Id,
            TableId = table?.Id,
            OrderNumber = GenerateOrderNumber(),
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            Note = request.Note,
            TotalAmount = total,
            Currency = menu.Currency,
            Status = OrderStatus.Pending,
            OrderedAt = DateTime.UtcNow,
            Items = orderItems
        };

        _context.QrMenuOrders.Add(order);
        await _context.SaveChangesAsync();

        return Json(new { 
            success = true, 
            orderNumber = order.OrderNumber, 
            total = total,
            message = "Siparişiniz alındı!" 
        });
    }

    /// <summary>
    /// Siparişleri listele (restoran sahibi)
    /// </summary>
    [Authorize]
    [Route("qrmenu/orders")]
    public async Task<IActionResult> Orders(DateTime? date = null, OrderStatus? status = null)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var menu = await _context.QrMenus.FirstOrDefaultAsync(m => m.UserId == user.Id);
        if (menu == null)
        {
            TempData["Error"] = "Önce menü oluşturmalısınız.";
            return RedirectToAction(nameof(Index));
        }

        var filterDate = date?.Date ?? DateTime.UtcNow.Date;
        var filterDateStart = DateTime.SpecifyKind(filterDate, DateTimeKind.Utc);
        var filterDateEnd = filterDateStart.AddDays(1);

        var query = _context.QrMenuOrders
            .Include(o => o.Table)
            .Include(o => o.Items)
            .Where(o => o.MenuId == menu.Id && o.OrderedAt >= filterDateStart && o.OrderedAt < filterDateEnd);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.OrderedAt)
            .ToListAsync();

        var model = new OrdersViewModel
        {
            Menu = menu,
            Orders = orders,
            FilterDate = filterDate,
            FilterStatus = status,
            TodayTotal = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount),
            PendingCount = orders.Count(o => o.Status == OrderStatus.Pending)
        };

        return View(model);
    }

    /// <summary>
    /// Sipariş durumu güncelle
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("qrmenu/order/update-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus([FromForm] int orderId, [FromForm] OrderStatus status)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Json(new { success = false, message = "Giriş yapmalısınız." });
        }

        var order = await _context.QrMenuOrders
            .Include(o => o.Menu)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.Menu.UserId == user.Id);

        if (order == null)
        {
            return Json(new { success = false, message = "Sipariş bulunamadı." });
        }

        order.Status = status;
        
        switch (status)
        {
            case OrderStatus.Confirmed:
                order.ConfirmedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Completed:
                order.CompletedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Cancelled:
                order.CancelledAt = DateTime.UtcNow;
                break;
        }

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Sipariş durumu güncellendi." });
    }

    /// <summary>
    /// Gün sonu raporu
    /// </summary>
    [Authorize]
    [Route("qrmenu/report")]
    public async Task<IActionResult> DailyReport(DateTime? date = null)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var menu = await _context.QrMenus.FirstOrDefaultAsync(m => m.UserId == user.Id);
        if (menu == null)
        {
            TempData["Error"] = "Önce menü oluşturmalısınız.";
            return RedirectToAction(nameof(Index));
        }

        var reportDate = date?.Date ?? DateTime.UtcNow.Date;
        var reportDateStart = DateTime.SpecifyKind(reportDate, DateTimeKind.Utc);
        var reportDateEnd = reportDateStart.AddDays(1);

        var orders = await _context.QrMenuOrders
            .Include(o => o.Table)
            .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
            .Where(o => o.MenuId == menu.Id && o.OrderedAt >= reportDateStart && o.OrderedAt < reportDateEnd)
            .ToListAsync();

        var completedOrders = orders.Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Delivered).ToList();
        var cancelledOrders = orders.Where(o => o.Status == OrderStatus.Cancelled).ToList();

        // Top selling items
        var topItems = orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.MenuItemId, i.ItemName })
            .Select(g => new TopSellingItem
            {
                ItemName = g.Key.ItemName,
                Quantity = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.TotalPrice)
            })
            .OrderByDescending(x => x.Quantity)
            .Take(10)
            .ToList();

        var model = new DailyReportViewModel
        {
            Menu = menu,
            ReportDate = reportDate,
            TotalOrders = orders.Count,
            CompletedOrders = completedOrders.Count,
            CancelledOrders = cancelledOrders.Count,
            TotalRevenue = completedOrders.Sum(o => o.TotalAmount),
            CancelledRevenue = cancelledOrders.Sum(o => o.TotalAmount),
            AverageOrderValue = completedOrders.Any() ? completedOrders.Average(o => o.TotalAmount) : 0,
            TopSellingItems = topItems,
            CurrencySymbol = MenuCurrencies.GetSymbol(menu.Currency)
        };

        return View(model);
    }

    #endregion

    #region QR Code Generation

    /// <summary>
    /// QR kod görseli oluştur
    /// </summary>
    [Route("qrmenu/qr/{menuSlug}")]
    [Route("qrmenu/qr/{menuSlug}/{tableQr}")]
    public IActionResult GenerateQrCodeImage(string menuSlug, string? tableQr = null)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var menuUrl = string.IsNullOrEmpty(tableQr) 
            ? $"{baseUrl}/m/{menuSlug}"
            : $"{baseUrl}/m/{menuSlug}/{tableQr}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(menuUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(10);

        return File(qrCodeBytes, "image/png");
    }

    #endregion
}

#region ViewModels and Request Models

public class QrMenuLimits
{
    public int MenuLimit { get; set; }
    public int TableLimit { get; set; }
    public int DailyAccessLimit { get; set; }
    public int CategoryLimit { get; set; }
    public int ItemLimit { get; set; }
    public bool CanUploadImages { get; set; }
    public int MaxImageSizeKB { get; set; } = 500;
    public bool CanManageTables { get; set; }
    public bool CanManageOrders { get; set; }
}

public class QrMenuViewModel
{
    public QrMenu? Menu { get; set; }
    public QrMenuLimits Limits { get; set; } = new();
    public bool IsAuthenticated { get; set; }
    public bool IsPremium { get; set; }
    public int CategoryCount { get; set; }
    public int ItemCount { get; set; }
    public int TodayAccessCount { get; set; }
    public Dictionary<string, string> Currencies { get; set; } = new();
    public Dictionary<string, string> Languages { get; set; } = new();
}

public class CreateMenuRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Currency { get; set; }
    public string? Language { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantPhone { get; set; }
}

public class UpdateMenuRequest : CreateMenuRequest
{
    public string? RestaurantAddress { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public bool AcceptOrders { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AddCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int? ParentCategoryId { get; set; }
}

public class UpdateCategoryRequest : AddCategoryRequest
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AddItemRequest
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public int? Calories { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public string? Allergens { get; set; }
    public string? Tags { get; set; }
    public string? PortionSize { get; set; }
}

public class UpdateItemRequest : AddItemRequest
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool InStock { get; set; } = true;
    public bool IsFeatured { get; set; }
}

public class AddTableRequest
{
    public string TableNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; } = 4;
}

public class PrintTableQrCodesViewModel
{
    public QrMenu Menu { get; set; } = null!;
    public string BaseUrl { get; set; } = string.Empty;
}

public class PublicMenuViewModel
{
    public QrMenu Menu { get; set; } = null!;
    public QrMenuTable? Table { get; set; }
    public string CurrencySymbol { get; set; } = "₺";
    public bool CanOrder { get; set; }
}

public class AccessLimitReachedViewModel
{
    public string MenuName { get; set; } = string.Empty;
    public int DailyLimit { get; set; }
    public bool IsOwnerRegistered { get; set; }
}

public class CreateOrderRequest
{
    public string? TableQrCode { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Note { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
}

public class OrdersViewModel
{
    public QrMenu Menu { get; set; } = null!;
    public List<QrMenuOrder> Orders { get; set; } = new();
    public DateTime FilterDate { get; set; }
    public OrderStatus? FilterStatus { get; set; }
    public decimal TodayTotal { get; set; }
    public int PendingCount { get; set; }
}

public class DailyReportViewModel
{
    public QrMenu Menu { get; set; } = null!;
    public DateTime ReportDate { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal CancelledRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public List<TopSellingItem> TopSellingItems { get; set; } = new();
    public string CurrencySymbol { get; set; } = "₺";
}

public class TopSellingItem
{
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

#endregion

