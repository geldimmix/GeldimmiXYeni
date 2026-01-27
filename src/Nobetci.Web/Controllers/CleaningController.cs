using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Services;

namespace Nobetci.Web.Controllers;

/// <summary>
/// Temizlik Çizelgesi Modülü Controller
/// </summary>
public class CleaningController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CleaningController> _logger;
    private readonly IActivityLogService _activityLog;
    private readonly ISystemSettingsService _systemSettings;

    public CleaningController(
        ApplicationDbContext context, 
        UserManager<ApplicationUser> userManager,
        ILogger<CleaningController> logger,
        IActivityLogService activityLog,
        ISystemSettingsService systemSettings)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _activityLog = activityLog;
        _systemSettings = systemSettings;
    }

    #region Main Page

    /// <summary>
    /// Ana temizlik çizelgesi yönetim sayfası
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        var isRegistered = User.Identity?.IsAuthenticated == true;
        var isPremium = user?.Plan == UserPlan.Premium;
        
        // Use CultureInfo for language detection (respects cookie/browser settings)
        var isTurkish = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
        
        // Get cleaning limits
        var limits = await GetCleaningLimitsAsync(user, isRegistered, isPremium);
        
        // Get schedules
        var schedules = await _context.CleaningSchedules
            .Include(s => s.Items.Where(i => i.IsActive))
            .Include(s => s.Group)
            .Where(s => s.OrganizationId == organization.Id && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
        
        // Get groups (for premium)
        var groups = isPremium ? await _context.CleaningScheduleGroups
            .Where(g => g.OrganizationId == organization.Id)
            .OrderBy(g => g.Name)
            .ToListAsync() : new List<CleaningScheduleGroup>();
        
        // Get pending records count
        var pendingCount = await _context.CleaningRecords
            .Where(r => r.Item.Schedule.OrganizationId == organization.Id && r.Status == CleaningRecordStatus.Pending)
            .CountAsync();
        
        // Get this month's QR access count
        var monthKey = DateTime.UtcNow.ToString("yyyy-MM");
        var monthlyAccessCount = await _context.CleaningQrAccesses
            .Where(a => a.Schedule.OrganizationId == organization.Id && a.MonthKey == monthKey)
            .CountAsync();
        
        var viewModel = new CleaningViewModel
        {
            Schedules = schedules,
            Groups = groups,
            Limits = limits,
            PendingRecordsCount = pendingCount,
            MonthlyQrAccessCount = monthlyAccessCount,
            IsRegistered = isRegistered,
            IsPremium = isPremium,
            IsTurkish = isTurkish
        };
        
        return View(viewModel);
    }

    #endregion

    #region Schedule API

    /// <summary>
    /// Yeni çizelge oluştur
    /// </summary>
    [HttpPost]
    [Route("api/cleaning/schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleRequest request)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        var isRegistered = User.Identity?.IsAuthenticated == true;
        var isPremium = user?.Plan == UserPlan.Premium;
        var isTr = await IsTurkishAsync();
        
        var limits = await GetCleaningLimitsAsync(user, isRegistered, isPremium);
        
        // Check schedule limit
        var currentCount = await _context.CleaningSchedules
            .CountAsync(s => s.OrganizationId == organization.Id && s.IsActive);
        
        if (currentCount >= limits.MaxSchedules)
        {
            return BadRequest(new { error = T(isTr, $"Maksimum çizelge sayısına ulaştınız ({limits.MaxSchedules})", $"Maximum schedule limit reached ({limits.MaxSchedules})") });
        }
        
        var schedule = new CleaningSchedule
        {
            OrganizationId = organization.Id,
            Name = request.Name,
            Location = request.Location,
            AccessCode = request.AccessCode,
            CleanerName = request.CleanerName,
            CleanerPhone = request.CleanerPhone,
            GroupId = isPremium ? request.GroupId : null
        };
        
        _context.CleaningSchedules.Add(schedule);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.CleaningScheduleCreated, 
            $"Temizlik çizelgesi eklendi: {schedule.Name}", 
            "CleaningSchedule", schedule.Id,
            new { schedule.Name, schedule.Location });
        
        return Json(new { 
            success = true, 
            schedule = new {
                schedule.Id,
                schedule.Name,
                schedule.Location,
                schedule.QrAccessCode,
                schedule.CleanerName,
                schedule.CleanerPhone,
                schedule.GroupId,
                ItemCount = 0
            }
        });
    }

    /// <summary>
    /// Çizelge güncelle
    /// </summary>
    [HttpPut]
    [Route("api/cleaning/schedules/{id}")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] CreateScheduleRequest request)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        var isPremium = user?.Plan == UserPlan.Premium;
        var isTr = await IsTurkishAsync();
        
        var schedule = await _context.CleaningSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
        
        if (schedule == null)
            return NotFound(new { error = T(isTr, "Çizelge bulunamadı", "Schedule not found") });
        
        schedule.Name = request.Name;
        schedule.Location = request.Location;
        schedule.AccessCode = request.AccessCode;
        schedule.CleanerName = request.CleanerName;
        schedule.CleanerPhone = request.CleanerPhone;
        schedule.GroupId = isPremium ? request.GroupId : null;
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.CleaningScheduleUpdated, 
            $"Temizlik çizelgesi güncellendi: {schedule.Name}", 
            "CleaningSchedule", schedule.Id,
            new { schedule.Name, schedule.Location });
        
        return Json(new { success = true });
    }

    /// <summary>
    /// Çizelge sil
    /// </summary>
    [HttpDelete]
    [Route("api/cleaning/schedules/{id}")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var isTr = await IsTurkishAsync();
        
        var schedule = await _context.CleaningSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
        
        if (schedule == null)
            return NotFound(new { error = T(isTr, "Çizelge bulunamadı", "Schedule not found") });
        
        var scheduleName = schedule.Name;
        schedule.IsActive = false;
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.CleaningScheduleDeleted, 
            $"Temizlik çizelgesi silindi: {scheduleName}", 
            "CleaningSchedule", id,
            new { Name = scheduleName });
        
        return Json(new { success = true });
    }

    /// <summary>
    /// Çizelge detayını getir
    /// </summary>
    [HttpGet]
    [Route("api/cleaning/schedules/{id}")]
    public async Task<IActionResult> GetSchedule(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var isTr = await IsTurkishAsync();
        
        var schedule = await _context.CleaningSchedules
            .Include(s => s.Items.Where(i => i.IsActive).OrderBy(i => i.SortOrder))
            .Include(s => s.Group)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
        
        if (schedule == null)
            return NotFound(new { error = T(isTr, "Çizelge bulunamadı", "Schedule not found") });
        
        return Json(new {
            schedule.Id,
            schedule.Name,
            schedule.Location,
            schedule.AccessCode,
            schedule.QrAccessCode,
            schedule.CleanerName,
            schedule.CleanerPhone,
            schedule.GroupId,
            GroupName = schedule.Group?.Name,
            Items = schedule.Items.Select(i => new {
                i.Id,
                i.Name,
                i.Description,
                i.Frequency,
                i.FrequencyDays,
                i.SortOrder
            })
        });
    }

    #endregion

    #region Items API

    /// <summary>
    /// Madde ekle
    /// </summary>
    [HttpPost]
    [Route("api/cleaning/items")]
    public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest request)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        var isRegistered = User.Identity?.IsAuthenticated == true;
        var isPremium = user?.Plan == UserPlan.Premium;
        var isTr = await IsTurkishAsync();
        
        var limits = await GetCleaningLimitsAsync(user, isRegistered, isPremium);
        
        // Get schedule
        var schedule = await _context.CleaningSchedules
            .Include(s => s.Items.Where(i => i.IsActive))
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId && s.OrganizationId == organization.Id);
        
        if (schedule == null)
            return NotFound(new { error = T(isTr, "Çizelge bulunamadı", "Schedule not found") });
        
        // Check item limit
        if (schedule.Items.Count >= limits.MaxItemsPerSchedule)
        {
            return BadRequest(new { error = T(isTr, $"Maksimum madde sayısına ulaştınız ({limits.MaxItemsPerSchedule})", $"Maximum item limit reached ({limits.MaxItemsPerSchedule})") });
        }
        
        // Check frequency permission
        var frequency = limits.CanSelectFrequency ? request.Frequency : CleaningFrequency.Daily;
        
        var maxOrder = schedule.Items.Any() ? schedule.Items.Max(i => i.SortOrder) : 0;
        
        var item = new CleaningItem
        {
            ScheduleId = request.ScheduleId,
            Name = request.Name,
            Description = request.Description,
            Frequency = frequency,
            FrequencyDays = frequency == CleaningFrequency.Custom ? request.FrequencyDays : null,
            SortOrder = maxOrder + 1
        };
        
        _context.CleaningItems.Add(item);
        await _context.SaveChangesAsync();
        
        return Json(new { 
            success = true, 
            item = new {
                item.Id,
                item.Name,
                item.Description,
                item.Frequency,
                item.FrequencyDays,
                item.SortOrder
            }
        });
    }

    /// <summary>
    /// Madde güncelle
    /// </summary>
    [HttpPut]
    [Route("api/cleaning/items/{id}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] CreateItemRequest request)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        var isRegistered = User.Identity?.IsAuthenticated == true;
        var isPremium = user?.Plan == UserPlan.Premium;
        var isTr = await IsTurkishAsync();
        
        var limits = await GetCleaningLimitsAsync(user, isRegistered, isPremium);
        
        var item = await _context.CleaningItems
            .Include(i => i.Schedule)
            .FirstOrDefaultAsync(i => i.Id == id && i.Schedule.OrganizationId == organization.Id);
        
        if (item == null)
            return NotFound(new { error = T(isTr, "Madde bulunamadı", "Item not found") });
        
        item.Name = request.Name;
        item.Description = request.Description;
        item.Frequency = limits.CanSelectFrequency ? request.Frequency : CleaningFrequency.Daily;
        item.FrequencyDays = item.Frequency == CleaningFrequency.Custom ? request.FrequencyDays : null;
        
        await _context.SaveChangesAsync();
        
        return Json(new { success = true });
    }

    /// <summary>
    /// Madde sil
    /// </summary>
    [HttpDelete]
    [Route("api/cleaning/items/{id}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var isTr = await IsTurkishAsync();
        
        var item = await _context.CleaningItems
            .Include(i => i.Schedule)
            .FirstOrDefaultAsync(i => i.Id == id && i.Schedule.OrganizationId == organization.Id);
        
        if (item == null)
            return NotFound(new { error = T(isTr, "Madde bulunamadı", "Item not found") });
        
        item.IsActive = false;
        await _context.SaveChangesAsync();
        
        return Json(new { success = true });
    }

    #endregion

    #region Groups API (Premium)

    /// <summary>
    /// Grup oluştur
    /// </summary>
    [HttpPost]
    [Route("api/cleaning/groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        var isTr = await IsTurkishAsync();
        if (user?.Plan != UserPlan.Premium || !user.CanGroupCleaningSchedules)
            return BadRequest(new { error = T(isTr, "Bu özellik premium üyelik gerektirir", "This feature requires premium membership") });
        
        var organization = await GetOrCreateOrganizationAsync();
        
        var group = new CleaningScheduleGroup
        {
            OrganizationId = organization.Id,
            Name = request.Name
        };
        
        _context.CleaningScheduleGroups.Add(group);
        await _context.SaveChangesAsync();
        
        return Json(new { success = true, group = new { group.Id, group.Name } });
    }

    /// <summary>
    /// Grup sil
    /// </summary>
    [HttpDelete]
    [Route("api/cleaning/groups/{id}")]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var isTr = await IsTurkishAsync();
        if (user?.Plan != UserPlan.Premium)
            return BadRequest(new { error = T(isTr, "Bu özellik premium üyelik gerektirir", "This feature requires premium membership") });
        
        var organization = await GetOrCreateOrganizationAsync();
        
        var group = await _context.CleaningScheduleGroups
            .FirstOrDefaultAsync(g => g.Id == id && g.OrganizationId == organization.Id);
        
        if (group == null)
            return NotFound(new { error = T(isTr, "Grup bulunamadı", "Group not found") });
        
        // Remove group from schedules
        var schedules = await _context.CleaningSchedules
            .Where(s => s.GroupId == id)
            .ToListAsync();
        
        foreach (var s in schedules)
            s.GroupId = null;
        
        _context.CleaningScheduleGroups.Remove(group);
        await _context.SaveChangesAsync();
        
        return Json(new { success = true });
    }

    #endregion

    #region Records API (Onay/Red)

    /// <summary>
    /// Günlük dashboard verisi
    /// </summary>
    [HttpGet]
    [Route("api/cleaning/dashboard/daily")]
    public async Task<IActionResult> GetDailyDashboard()
    {
        var organization = await GetOrCreateOrganizationAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var schedules = await _context.CleaningSchedules
            .Include(s => s.Items.Where(i => i.IsActive))
            .Where(s => s.OrganizationId == organization.Id && s.IsActive)
            .ToListAsync();
        
        var allItemIds = schedules.SelectMany(s => s.Items).Select(i => i.Id).ToList();
        
        // Get today's records
        var todayRecords = await _context.CleaningRecords
            .Where(r => allItemIds.Contains(r.ItemId) && DateOnly.FromDateTime(r.CompletedAt) == today)
            .ToListAsync();
        
        var recordsByItem = todayRecords.GroupBy(r => r.ItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CompletedAt).First());
        
        int totalItems = 0;
        int completedItems = 0;
        int pendingItems = 0;
        int rejectedItems = 0;
        int notDoneItems = 0;
        
        var scheduleData = new List<object>();
        
        foreach (var schedule in schedules)
        {
            var items = schedule.Items.Where(i => i.IsActive).ToList();
            int scheduleCompleted = 0;
            int schedulePending = 0;
            int scheduleNotDone = 0;
            int scheduleRejected = 0;
            
            var itemData = new List<object>();
            
            foreach (var item in items)
            {
                totalItems++;
                string status = "notdone";
                
                if (recordsByItem.TryGetValue(item.Id, out var record))
                {
                    if (record.Status == CleaningRecordStatus.Approved)
                    {
                        status = "completed";
                        completedItems++;
                        scheduleCompleted++;
                    }
                    else if (record.Status == CleaningRecordStatus.Pending)
                    {
                        status = "pending";
                        pendingItems++;
                        schedulePending++;
                    }
                    else if (record.Status == CleaningRecordStatus.Rejected)
                    {
                        status = "rejected";
                        rejectedItems++;
                        scheduleRejected++;
                    }
                }
                else
                {
                    notDoneItems++;
                    scheduleNotDone++;
                }
                
                itemData.Add(new {
                    id = item.Id,
                    name = item.Name,
                    status = status
                });
            }
            
            scheduleData.Add(new {
                id = schedule.Id,
                name = schedule.Name,
                location = schedule.Location,
                completedCount = scheduleCompleted,
                pendingCount = schedulePending,
                notDoneCount = scheduleNotDone,
                rejectedCount = scheduleRejected,
                items = itemData
            });
        }
        
        return Json(new {
            totalItems,
            completedItems,
            pendingItems,
            rejectedItems,
            notDoneItems,
            schedules = scheduleData
        });
    }

    /// <summary>
    /// Bekleyen kayıtları getir
    /// </summary>
    [HttpGet]
    [Route("api/cleaning/records/pending")]
    public async Task<IActionResult> GetPendingRecords()
    {
        var organization = await GetOrCreateOrganizationAsync();
        
        var records = await _context.CleaningRecords
            .Include(r => r.Item)
            .ThenInclude(i => i.Schedule)
            .Where(r => r.Item.Schedule.OrganizationId == organization.Id && r.Status == CleaningRecordStatus.Pending)
            .OrderByDescending(r => r.CompletedAt)
            .Take(100)
            .ToListAsync();
        
        return Json(records.Select(r => new {
            r.Id,
            r.CompletedAt,
            r.CompletedByName,
            ItemName = r.Item.Name,
            ScheduleName = r.Item.Schedule.Name,
            ScheduleLocation = r.Item.Schedule.Location
        }));
    }

    /// <summary>
    /// Kaydı onayla
    /// </summary>
    [HttpPost]
    [Route("api/cleaning/records/{id}/approve")]
    public async Task<IActionResult> ApproveRecord(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        var isTr = await IsTurkishAsync();
        
        var record = await _context.CleaningRecords
            .Include(r => r.Item)
            .ThenInclude(i => i.Schedule)
            .FirstOrDefaultAsync(r => r.Id == id && r.Item.Schedule.OrganizationId == organization.Id);
        
        if (record == null)
            return NotFound(new { error = T(isTr, "Kayıt bulunamadı", "Record not found") });
        
        record.Status = CleaningRecordStatus.Approved;
        record.ReviewedAt = DateTime.UtcNow;
        record.ReviewedById = user?.Id;
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.CleaningRecordApproved, 
            $"Temizlik kaydı onaylandı: {record.Item?.Name}", 
            "CleaningRecord", record.Id,
            new { ItemName = record.Item?.Name, ScheduleName = record.Item?.Schedule?.Name });
        
        return Json(new { success = true });
    }

    /// <summary>
    /// Kaydı reddet
    /// </summary>
    [HttpPost]
    [Route("api/cleaning/records/{id}/reject")]
    public async Task<IActionResult> RejectRecord(int id, [FromBody] RejectRecordRequest request)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        var isTr = await IsTurkishAsync();
        
        var record = await _context.CleaningRecords
            .Include(r => r.Item)
            .ThenInclude(i => i.Schedule)
            .FirstOrDefaultAsync(r => r.Id == id && r.Item.Schedule.OrganizationId == organization.Id);
        
        if (record == null)
            return NotFound(new { error = T(isTr, "Kayıt bulunamadı", "Record not found") });
        
        record.Status = CleaningRecordStatus.Rejected;
        record.ReviewedAt = DateTime.UtcNow;
        record.ReviewedById = user?.Id;
        record.Note = request.Note;
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.CleaningRecordRejected, 
            $"Temizlik kaydı reddedildi: {record.Item?.Name}", 
            "CleaningRecord", record.Id,
            new { ItemName = record.Item?.Name, ScheduleName = record.Item?.Schedule?.Name, Note = request.Note });
        
        return Json(new { success = true });
    }

    #endregion

    #region QR Access Page (Public)

    /// <summary>
    /// QR ile erişilen sayfa (şifre girişi)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [Route("c/{qrCode}")]
    public async Task<IActionResult> QrAccess(string qrCode)
    {
        var schedule = await _context.CleaningSchedules
            .Include(s => s.Organization)
            .FirstOrDefaultAsync(s => s.QrAccessCode == qrCode && s.IsActive);
        
        if (schedule == null)
            return View("QrNotFound");
        
        // Check QR access limit
        var user = await GetOrganizationOwnerAsync(schedule.OrganizationId);
        var isRegistered = user != null;
        var isPremium = user?.Plan == UserPlan.Premium;
        var limits = await GetCleaningLimitsAsync(user, isRegistered, isPremium);
        
        var monthKey = DateTime.UtcNow.ToString("yyyy-MM");
        var monthlyAccessCount = await _context.CleaningQrAccesses
            .CountAsync(a => a.Schedule.OrganizationId == schedule.OrganizationId && a.MonthKey == monthKey);
        
        if (monthlyAccessCount >= limits.MaxQrAccessPerMonth)
        {
            return View("QrLimitReached", new QrLimitViewModel { 
                ScheduleName = schedule.Name,
                Limit = limits.MaxQrAccessPerMonth 
            });
        }
        
        var viewModel = new QrAccessViewModel
        {
            QrCode = qrCode,
            ScheduleName = schedule.Name,
            Location = schedule.Location,
            HasAccessCode = !string.IsNullOrEmpty(schedule.AccessCode),
            IsTurkish = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr"
        };
        
        return View("QrAccess", viewModel);
    }

    /// <summary>
    /// QR şifre doğrulama
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [Route("api/cleaning/qr/verify")]
    public async Task<IActionResult> VerifyQrAccess([FromBody] VerifyQrRequest request)
    {
        var isTr = await IsTurkishAsync();
        
        var schedule = await _context.CleaningSchedules
            .Include(s => s.Items.Where(i => i.IsActive).OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(s => s.QrAccessCode == request.QrCode && s.IsActive);
        
        if (schedule == null)
            return NotFound(new { error = T(isTr, "Çizelge bulunamadı", "Schedule not found") });
        
        // Verify access code if set
        if (!string.IsNullOrEmpty(schedule.AccessCode))
        {
            if (request.AccessCode != schedule.AccessCode)
                return BadRequest(new { error = T(isTr, "Yanlış şifre", "Wrong password") });
        }
        
        // Check and log QR access
        var user = await GetOrganizationOwnerAsync(schedule.OrganizationId);
        var isRegistered = user != null;
        var isPremium = user?.Plan == UserPlan.Premium;
        var limits = await GetCleaningLimitsAsync(user, isRegistered, isPremium);
        
        var monthKey = DateTime.UtcNow.ToString("yyyy-MM");
        var monthlyAccessCount = await _context.CleaningQrAccesses
            .CountAsync(a => a.Schedule.OrganizationId == schedule.OrganizationId && a.MonthKey == monthKey);
        
        if (monthlyAccessCount >= limits.MaxQrAccessPerMonth)
        {
            return BadRequest(new { error = T(isTr, "Aylık erişim limitine ulaşıldı", "Monthly access limit reached") });
        }
        
        // Log access
        var access = new CleaningQrAccess
        {
            ScheduleId = schedule.Id,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            MonthKey = monthKey
        };
        _context.CleaningQrAccesses.Add(access);
        await _context.SaveChangesAsync();
        
        // Get today's records for items
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var todayRecords = await _context.CleaningRecords
            .Where(r => r.Item.ScheduleId == schedule.Id && 
                       DateOnly.FromDateTime(r.CompletedAt) == today)
            .ToListAsync();
        
        // Get last approved records for each item (for frequency check display)
        var itemIds = schedule.Items.Select(i => i.Id).ToList();
        var lastApprovedRecords = await _context.CleaningRecords
            .Where(r => itemIds.Contains(r.ItemId) && r.Status == CleaningRecordStatus.Approved)
            .GroupBy(r => r.ItemId)
            .Select(g => g.OrderByDescending(r => r.CompletedAt).First())
            .ToDictionaryAsync(r => r.ItemId, r => r);
        
        return Json(new {
            success = true,
            schedule = new {
                schedule.Id,
                schedule.Name,
                schedule.Location,
                schedule.CleanerName,
                Items = schedule.Items.Select(i => {
                    var todayRec = todayRecords.FirstOrDefault(r => r.ItemId == i.Id);
                    var lastApproved = lastApprovedRecords.GetValueOrDefault(i.Id);
                    
                    // Calculate next available date based on frequency
                    var requiredDays = i.Frequency switch
                    {
                        CleaningFrequency.Daily => 1,
                        CleaningFrequency.Weekly => 7,
                        CleaningFrequency.Monthly => 30,
                        CleaningFrequency.Yearly => 365,
                        CleaningFrequency.Custom => i.FrequencyDays ?? 1,
                        _ => 1
                    };
                    
                    DateTime? nextAvailableDate = lastApproved != null 
                        ? lastApproved.CompletedAt.AddDays(requiredDays) 
                        : null;
                    
                    bool canComplete = lastApproved == null || 
                                       (now - lastApproved.CompletedAt).TotalDays >= requiredDays;
                    
                    return new {
                        i.Id,
                        i.Name,
                        i.Description,
                        i.Frequency,
                        i.FrequencyDays,
                        TodayRecord = todayRec != null 
                            ? new { todayRec.Id, todayRec.CompletedAt, todayRec.Status, todayRec.Note } 
                            : null,
                        LastApprovedAt = lastApproved?.CompletedAt,
                        NextAvailableAt = nextAvailableDate,
                        CanComplete = canComplete
                    };
                })
            }
        });
    }

    /// <summary>
    /// Temizlik işareti koy (QR üzerinden)
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [Route("api/cleaning/qr/complete")]
    public async Task<IActionResult> CompleteItem([FromBody] CompleteItemRequest request)
    {
        var isTr = await IsTurkishAsync();
        
        var schedule = await _context.CleaningSchedules
            .FirstOrDefaultAsync(s => s.QrAccessCode == request.QrCode && s.IsActive);
        
        if (schedule == null)
            return NotFound(new { error = T(isTr, "Çizelge bulunamadı", "Schedule not found") });
        
        // Verify access code if set
        if (!string.IsNullOrEmpty(schedule.AccessCode) && request.AccessCode != schedule.AccessCode)
            return BadRequest(new { error = T(isTr, "Yanlış şifre", "Wrong password") });
        
        var item = await _context.CleaningItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.ScheduleId == schedule.Id && i.IsActive);
        
        if (item == null)
            return NotFound(new { error = T(isTr, "Madde bulunamadı", "Item not found") });
        
        // Get the last APPROVED record for this item
        var lastApprovedRecord = await _context.CleaningRecords
            .Where(r => r.ItemId == request.ItemId && r.Status == CleaningRecordStatus.Approved)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync();
        
        // Check frequency-based completion
        var now = DateTime.UtcNow;
        if (lastApprovedRecord != null)
        {
            var daysSinceLastApproved = (now - lastApprovedRecord.CompletedAt).TotalDays;
            var requiredDays = item.Frequency switch
            {
                CleaningFrequency.Daily => 1,
                CleaningFrequency.Weekly => 7,
                CleaningFrequency.Monthly => 30,
                CleaningFrequency.Yearly => 365,
                CleaningFrequency.Custom => item.FrequencyDays ?? 1,
                _ => 1
            };
            
            if (daysSinceLastApproved < requiredDays)
            {
                var nextDate = lastApprovedRecord.CompletedAt.AddDays(requiredDays);
                var nextDateStr = nextDate.ToString("dd.MM.yyyy");
                return BadRequest(new { 
                    error = T(isTr, 
                        $"Bu madde {requiredDays} günde bir yapılabilir. Sonraki tarih: {nextDateStr}", 
                        $"This item can be done every {requiredDays} days. Next date: {nextDateStr}") 
                });
            }
        }
        
        // Check if there's already a PENDING record today (don't allow duplicate pending)
        var today = DateOnly.FromDateTime(now);
        var existingTodayRecord = await _context.CleaningRecords
            .FirstOrDefaultAsync(r => r.ItemId == request.ItemId && 
                                     DateOnly.FromDateTime(r.CompletedAt) == today);
        
        if (existingTodayRecord != null)
        {
            if (existingTodayRecord.Status == CleaningRecordStatus.Pending)
            {
                return BadRequest(new { error = T(isTr, "Bu madde bugün zaten işaretlenmiş ve onay bekliyor", "This item is already marked today and pending approval") });
            }
            else if (existingTodayRecord.Status == CleaningRecordStatus.Approved)
            {
                return BadRequest(new { error = T(isTr, "Bu madde bugün zaten onaylanmış", "This item is already approved today") });
            }
            else if (existingTodayRecord.Status == CleaningRecordStatus.Rejected)
            {
                // Reset the rejected record to pending for retry
                existingTodayRecord.Status = CleaningRecordStatus.Pending;
                existingTodayRecord.CompletedAt = DateTime.UtcNow;
                existingTodayRecord.CompletedByName = request.CompletedByName ?? schedule.CleanerName;
                existingTodayRecord.Note = null; // Clear rejection note
                await _context.SaveChangesAsync();
                
                return Json(new { 
                    success = true, 
                    record = new {
                        existingTodayRecord.Id,
                        existingTodayRecord.CompletedAt,
                        existingTodayRecord.Status
                    }
                });
            }
        }
        
        var record = new CleaningRecord
        {
            ItemId = request.ItemId,
            CompletedByName = request.CompletedByName ?? schedule.CleanerName
        };
        
        _context.CleaningRecords.Add(record);
        await _context.SaveChangesAsync();
        
        return Json(new { 
            success = true, 
            record = new {
                record.Id,
                record.CompletedAt,
                record.Status
            }
        });
    }

    /// <summary>
    /// Temizlik görevlisi için bugünün durumu
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [Route("api/cleaning/qr/{qrCode}/status")]
    public async Task<IActionResult> GetCleanerStatus(string qrCode, [FromQuery] string? accessCode)
    {
        var isTr = await IsTurkishAsync();
        
        var schedule = await _context.CleaningSchedules
            .Include(s => s.Items.Where(i => i.IsActive))
            .FirstOrDefaultAsync(s => s.QrAccessCode == qrCode && s.IsActive);
        
        if (schedule == null)
            return NotFound(new { error = T(isTr, "Çizelge bulunamadı", "Schedule not found") });
        
        if (!string.IsNullOrEmpty(schedule.AccessCode) && accessCode != schedule.AccessCode)
            return BadRequest(new { error = T(isTr, "Yanlış şifre", "Wrong password") });
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayRecords = await _context.CleaningRecords
            .Where(r => r.Item.ScheduleId == schedule.Id && 
                       DateOnly.FromDateTime(r.CompletedAt) == today)
            .ToListAsync();
        
        return Json(new {
            schedule.Name,
            schedule.Location,
            Items = schedule.Items.OrderBy(i => i.SortOrder).Select(i => {
                var rec = todayRecords.FirstOrDefault(r => r.ItemId == i.Id);
                return new {
                    i.Id,
                    i.Name,
                    IsCompleted = rec != null,
                    Status = rec?.Status,
                    StatusNote = rec?.Note,
                    CompletedAt = rec?.CompletedAt
                };
            })
        });
    }

    #endregion

    #region Helpers

    private async Task<Organization> GetOrCreateOrganizationAsync()
    {
        var sessionId = HttpContext.Session.GetString("OrganizationId");
        
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            var org = await _context.Organizations
                .FirstOrDefaultAsync(o => o.UserId == user!.Id);
            
            if (org != null)
            {
                HttpContext.Session.SetString("OrganizationId", org.Id.ToString());
                return org;
            }
            
            // Create for authenticated user
            org = new Organization
            {
                Name = user!.FullName ?? user.Email ?? "Organizasyonum",
                UserId = user.Id
            };
            _context.Organizations.Add(org);
            await _context.SaveChangesAsync();
            HttpContext.Session.SetString("OrganizationId", org.Id.ToString());
            return org;
        }
        
        // Anonymous user - session based
        if (!string.IsNullOrEmpty(sessionId) && int.TryParse(sessionId, out var orgId))
        {
            var existing = await _context.Organizations.FindAsync(orgId);
            if (existing != null) return existing;
        }
        
        var newOrg = new Organization { Name = "Misafir Organizasyon" };
        _context.Organizations.Add(newOrg);
        await _context.SaveChangesAsync();
        HttpContext.Session.SetString("OrganizationId", newOrg.Id.ToString());
        return newOrg;
    }

    private async Task<ApplicationUser?> GetOrganizationOwnerAsync(int organizationId)
    {
        var org = await _context.Organizations.FindAsync(organizationId);
        if (org?.UserId == null) return null;
        return await _userManager.FindByIdAsync(org.UserId);
    }

    private async Task<CleaningLimits> GetCleaningLimitsAsync(ApplicationUser? user, bool isRegistered, bool isPremium)
    {
        if (!isRegistered)
        {
            // Kayıtsız kullanıcılar için sistem ayarlarından limitleri al
            return new CleaningLimits
            {
                MaxSchedules = await _systemSettings.GetUnregisteredMaxSchedulesAsync(),
                MaxItemsPerSchedule = await _systemSettings.GetUnregisteredMaxItemsPerScheduleAsync(),
                MaxQrAccessPerMonth = await _systemSettings.GetUnregisteredMaxQrAccessPerMonthAsync(),
                CanSelectFrequency = false,
                CanGroupSchedules = false
            };
        }
        
        if (isPremium)
        {
            // Premium kullanıcılar için: Kullanıcı özel limiti varsa onu kullan, yoksa sistem varsayılanını
            var defaultScheduleLimit = await _systemSettings.GetPremiumDefaultScheduleLimitAsync();
            var defaultItemLimit = await _systemSettings.GetPremiumDefaultItemLimitAsync();
            var defaultQrLimit = await _systemSettings.GetPremiumDefaultQrAccessLimitAsync();
            
            return new CleaningLimits
            {
                MaxSchedules = user?.CleaningScheduleLimit ?? defaultScheduleLimit,
                MaxItemsPerSchedule = user?.CleaningItemLimit ?? defaultItemLimit,
                MaxQrAccessPerMonth = user?.CleaningQrAccessLimit ?? defaultQrLimit,
                CanSelectFrequency = user?.CanSelectCleaningFrequency ?? true,
                CanGroupSchedules = user?.CanGroupCleaningSchedules ?? true
            };
        }
        
        // Kayıtlı (Free) kullanıcılar için: Kullanıcı özel limiti varsa onu kullan, yoksa sistem varsayılanını
        var regScheduleLimit = await _systemSettings.GetRegisteredDefaultScheduleLimitAsync();
        var regItemLimit = await _systemSettings.GetRegisteredDefaultItemLimitAsync();
        var regQrLimit = await _systemSettings.GetRegisteredDefaultQrAccessLimitAsync();
        
        return new CleaningLimits
        {
            MaxSchedules = user?.CleaningScheduleLimit ?? regScheduleLimit,
            MaxItemsPerSchedule = user?.CleaningItemLimit ?? regItemLimit,
            MaxQrAccessPerMonth = user?.CleaningQrAccessLimit ?? regQrLimit,
            CanSelectFrequency = user?.CanSelectCleaningFrequency ?? true,
            CanGroupSchedules = false
        };
    }

    /// <summary>
    /// Check if current user prefers Turkish (uses CultureInfo which respects cookie settings)
    /// </summary>
    private Task<bool> IsTurkishAsync()
    {
        // Use CultureInfo which is set by the localization middleware (respects cookie)
        var isTurkish = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
        return Task.FromResult(isTurkish);
    }

    /// <summary>
    /// Translate message based on user language
    /// </summary>
    private string T(bool isTurkish, string tr, string en) => isTurkish ? tr : en;

    #endregion
}

#region Request/Response Models

public class CreateScheduleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? AccessCode { get; set; }
    public string? CleanerName { get; set; }
    public string? CleanerPhone { get; set; }
    public int? GroupId { get; set; }
}

public class CreateItemRequest
{
    public int ScheduleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CleaningFrequency Frequency { get; set; } = CleaningFrequency.Daily;
    public int? FrequencyDays { get; set; }
}

public class CreateGroupRequest
{
    public string Name { get; set; } = string.Empty;
}

public class RejectRecordRequest
{
    public string? Note { get; set; }
}

public class VerifyQrRequest
{
    public string QrCode { get; set; } = string.Empty;
    public string? AccessCode { get; set; }
}

public class CompleteItemRequest
{
    public string QrCode { get; set; } = string.Empty;
    public string? AccessCode { get; set; }
    public int ItemId { get; set; }
    public string? CompletedByName { get; set; }
}

#endregion

