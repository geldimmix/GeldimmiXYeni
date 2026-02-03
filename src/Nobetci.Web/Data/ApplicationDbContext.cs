using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<UnitTypeTemplate> UnitTypeTemplates => Set<UnitTypeTemplate>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ShiftTemplate> ShiftTemplates => Set<ShiftTemplate>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<EmployeeAvailability> EmployeeAvailabilities => Set<EmployeeAvailability>();
    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<VisitorLog> VisitorLogs => Set<VisitorLog>();
    public DbSet<SavedSchedule> SavedSchedules => Set<SavedSchedule>();
    public DbSet<TimeAttendance> TimeAttendances => Set<TimeAttendance>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SavedPayroll> SavedPayrolls => Set<SavedPayroll>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<BordroSabitleri> BordroSabitleri => Set<BordroSabitleri>();
    public DbSet<BordroSabitleriGecmis> BordroSabitleriGecmis => Set<BordroSabitleriGecmis>();
    public DbSet<BordroSabitleriTemplate> BordroSabitleriTemplates => Set<BordroSabitleriTemplate>();
    public DbSet<BordroResult4A> BordroResults4A => Set<BordroResult4A>();
    public DbSet<BordroResult4B> BordroResults4B => Set<BordroResult4B>();
    public DbSet<BordroYetkileri> BordroYetkileri => Set<BordroYetkileri>();
    public DbSet<PersonelNobetPuan> PersonelNobetPuan => Set<PersonelNobetPuan>();
    
    // Module System
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<SubModule> SubModules => Set<SubModule>();
    public DbSet<UserModuleAccess> UserModuleAccesses => Set<UserModuleAccess>();
    
    // User API Credentials
    public DbSet<UserApiCredential> UserApiCredentials => Set<UserApiCredential>();
    
    // Cleaning Module
    public DbSet<CleaningSchedule> CleaningSchedules => Set<CleaningSchedule>();
    public DbSet<CleaningScheduleGroup> CleaningScheduleGroups => Set<CleaningScheduleGroup>();
    public DbSet<CleaningItem> CleaningItems => Set<CleaningItem>();
    public DbSet<CleaningRecord> CleaningRecords => Set<CleaningRecord>();
    public DbSet<CleaningQrAccess> CleaningQrAccesses => Set<CleaningQrAccess>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    
    // Contact form submissions
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();

    // Blog System
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    
    // QR Menu Module
    public DbSet<QrMenu> QrMenus => Set<QrMenu>();
    public DbSet<QrMenuCategory> QrMenuCategories => Set<QrMenuCategory>();
    public DbSet<QrMenuItem> QrMenuItems => Set<QrMenuItem>();
    public DbSet<QrMenuTable> QrMenuTables => Set<QrMenuTable>();
    public DbSet<QrMenuOrder> QrMenuOrders => Set<QrMenuOrder>();
    public DbSet<QrMenuOrderItem> QrMenuOrderItems => Set<QrMenuOrderItem>();
    public DbSet<QrMenuAccess> QrMenuAccesses => Set<QrMenuAccess>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser configuration
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.Language).HasMaxLength(5).HasDefaultValue("tr");
        });

        // Organization configuration
        builder.Entity<Organization>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.GuestSessionId);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Organizations)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UnitType configuration
        builder.Entity<UnitType>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Name }).IsUnique();
            
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.UnitTypes)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UnitTypeTemplate configuration
        builder.Entity<UnitTypeTemplate>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Unit configuration
        builder.Entity<Unit>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Name }).IsUnique();
            
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Units)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.UnitType)
                .WithMany(ut => ut.Units)
                .HasForeignKey(e => e.UnitTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Employee configuration
        builder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.IdentityNo });
            
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Employees)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Unit relationship temporarily disabled - requires DB migration
            // entity.HasOne(e => e.Unit)
            //     .WithMany(u => u.Employees)
            //     .HasForeignKey(e => e.UnitId)
            //     .OnDelete(DeleteBehavior.SetNull);
        });

        // ShiftTemplate configuration
        builder.Entity<ShiftTemplate>(entity =>
        {
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.IsGlobal);
            
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.ShiftTemplates)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Shift configuration
        builder.Entity<Shift>(entity =>
        {
            entity.HasIndex(e => new { e.EmployeeId, e.Date });
            entity.HasIndex(e => e.Date);
            
            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.Shifts)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.ShiftTemplate)
                .WithMany()
                .HasForeignKey(e => e.ShiftTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Holiday configuration
        builder.Entity<Holiday>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Date }).IsUnique();
            
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Holidays)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LeaveType configuration
        builder.Entity<LeaveType>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Code });
            entity.HasIndex(e => e.IsSystem);
            
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Leave configuration
        builder.Entity<Leave>(entity =>
        {
            entity.HasIndex(e => new { e.EmployeeId, e.Date });
            entity.HasIndex(e => e.Date);
            
            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.Leaves)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.LeaveType)
                .WithMany()
                .HasForeignKey(e => e.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BordroSabitleri>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Key });
            entity.HasIndex(e => new { e.OrganizationId, e.CadreType });
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BordroSabitleriGecmis>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.SabitId });
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BordroSabitleriTemplate>(entity =>
        {
            entity.HasIndex(e => e.Key);
            entity.HasIndex(e => new { e.Key, e.CadreType });
        });

        builder.Entity<BordroResult4A>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.EmployeeId, e.Year, e.Month, e.YogunBakimVar });
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BordroResult4B>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.EmployeeId, e.Year, e.Month, e.YogunBakimVar });
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BordroYetkileri>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.UnitId, e.TcKimlik });
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Unit)
                .WithMany()
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PersonelNobetPuan>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.TcKimlik });
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EmployeeAvailability configuration
        builder.Entity<EmployeeAvailability>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            
            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.Availabilities)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ContentPage configuration
        builder.Entity<ContentPage>(entity =>
        {
            entity.HasIndex(e => new { e.Slug, e.Language }).IsUnique();
            entity.HasIndex(e => e.PageType);
        });

        // VisitorLog configuration
        builder.Entity<VisitorLog>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IpAddress);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.PagePath);
        });

        // SavedSchedule configuration
        builder.Entity<SavedSchedule>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Year, e.Month });
            entity.HasIndex(e => e.OrganizationId);
            
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TimeAttendance configuration
        builder.Entity<TimeAttendance>(entity =>
        {
            entity.HasIndex(e => new { e.EmployeeId, e.Date });
            entity.HasIndex(e => e.Date);
            
            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ApiKey configuration
        builder.Entity<ApiKey>(entity =>
        {
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => e.OrganizationId);
            
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SavedPayroll configuration
        builder.Entity<SavedPayroll>(entity =>
        {
            entity.HasIndex(e => new { e.OrganizationId, e.Year, e.Month });
            entity.HasIndex(e => e.OrganizationId);
            
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SystemSettings configuration
        builder.Entity<SystemSettings>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // AdminUser configuration
        builder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // ContactSubmission configuration
        builder.Entity<ContactSubmission>(entity =>
        {
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        // Module configuration
        builder.Entity<Module>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // SubModule configuration
        builder.Entity<SubModule>(entity =>
        {
            entity.HasIndex(e => new { e.ModuleId, e.Code }).IsUnique();
            
            entity.HasOne(e => e.Module)
                .WithMany(m => m.SubModules)
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserModuleAccess configuration
        builder.Entity<UserModuleAccess>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ModuleId }).IsUnique();
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Module)
                .WithMany(m => m.UserAccesses)
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserApiCredential configuration
        builder.Entity<UserApiCredential>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.OrganizationId }).IsUnique();
            entity.HasIndex(e => e.ApiUsername).IsUnique();
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ActivityLog configuration
        builder.Entity<ActivityLog>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ActivityType);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // BlogPost configuration
        builder.Entity<BlogPost>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.IsPublished);
            entity.HasIndex(e => e.PublishedAt);
            entity.HasIndex(e => e.IsFeatured);
        });
        
        // QrMenu configuration
        builder.Entity<QrMenu>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.IsActive);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // QrMenuCategory configuration
        builder.Entity<QrMenuCategory>(entity =>
        {
            entity.HasIndex(e => new { e.MenuId, e.DisplayOrder });
            
            entity.HasOne(e => e.Menu)
                .WithMany(m => m.Categories)
                .HasForeignKey(e => e.MenuId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // QrMenuItem configuration
        builder.Entity<QrMenuItem>(entity =>
        {
            entity.HasIndex(e => new { e.CategoryId, e.DisplayOrder });
            entity.HasIndex(e => e.IsActive);
            
            entity.HasOne(e => e.Category)
                .WithMany(c => c.Items)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // QrMenuTable configuration
        builder.Entity<QrMenuTable>(entity =>
        {
            entity.HasIndex(e => new { e.MenuId, e.QrCode }).IsUnique();
            entity.HasIndex(e => e.IsActive);
            
            entity.HasOne(e => e.Menu)
                .WithMany(m => m.Tables)
                .HasForeignKey(e => e.MenuId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // QrMenuOrder configuration
        builder.Entity<QrMenuOrder>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderedAt);
            entity.HasIndex(e => new { e.MenuId, e.OrderedAt });
            
            entity.HasOne(e => e.Menu)
                .WithMany(m => m.Orders)
                .HasForeignKey(e => e.MenuId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Table)
                .WithMany(t => t.Orders)
                .HasForeignKey(e => e.TableId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // QrMenuOrderItem configuration
        builder.Entity<QrMenuOrderItem>(entity =>
        {
            entity.HasOne(e => e.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.MenuItem)
                .WithMany()
                .HasForeignKey(e => e.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // QrMenuAccess configuration
        builder.Entity<QrMenuAccess>(entity =>
        {
            entity.HasIndex(e => new { e.MenuId, e.AccessDate });
            entity.HasIndex(e => e.AccessDate);
            
            entity.HasOne(e => e.Menu)
                .WithMany(m => m.Accesses)
                .HasForeignKey(e => e.MenuId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Table)
                .WithMany()
                .HasForeignKey(e => e.TableId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Seed global shift templates
        SeedGlobalShiftTemplates(builder);
        
        // NOTE: ContentPages are seeded in Program.cs at runtime, not via migration
        // This allows dynamic updates without requiring new migrations
    }

    private static void SeedGlobalShiftTemplates(ModelBuilder builder)
    {
        // Fixed date for seed data (EF Core requires static values)
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        builder.Entity<ShiftTemplate>().HasData(
            new ShiftTemplate
            {
                Id = 1,
                Name = "Morning Shift",
                NameKey = "shift.morning",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(16, 0),
                SpansNextDay = false,
                Color = "#3B82F6",
                IsGlobal = true,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAt = seedDate
            },
            new ShiftTemplate
            {
                Id = 2,
                Name = "Evening Shift",
                NameKey = "shift.evening",
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(0, 0),
                SpansNextDay = false,
                Color = "#F97316",
                IsGlobal = true,
                IsActive = true,
                DisplayOrder = 2,
                CreatedAt = seedDate
            },
            new ShiftTemplate
            {
                Id = 3,
                Name = "Night Shift",
                NameKey = "shift.night",
                StartTime = new TimeOnly(0, 0),
                EndTime = new TimeOnly(8, 0),
                SpansNextDay = false,
                Color = "#8B5CF6",
                IsGlobal = true,
                IsActive = true,
                DisplayOrder = 3,
                CreatedAt = seedDate
            },
            new ShiftTemplate
            {
                Id = 4,
                Name = "Full Day",
                NameKey = "shift.fullday",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 0),
                SpansNextDay = false,
                BreakMinutes = 60,
                Color = "#22C55E",
                IsGlobal = true,
                IsActive = true,
                DisplayOrder = 4,
                CreatedAt = seedDate
            },
            new ShiftTemplate
            {
                Id = 5,
                Name = "Nurse Duty (16h)",
                NameKey = "shift.nurseduty",
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(8, 0),
                SpansNextDay = true,
                Color = "#EF4444",
                IsGlobal = true,
                IsActive = true,
                DisplayOrder = 5,
                CreatedAt = seedDate
            },
            new ShiftTemplate
            {
                Id = 6,
                Name = "24h Duty",
                NameKey = "shift.24h",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(8, 0),
                SpansNextDay = true,
                Color = "#DC2626",
                IsGlobal = true,
                IsActive = true,
                DisplayOrder = 6,
                CreatedAt = seedDate
            }
        );
    }

    private static void SeedContentPages(ModelBuilder builder)
    {
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.Entity<ContentPage>().HasData(
            // ==================== TURKISH PAGES ====================
            
            // 1. Nöbet Listesi Oluşturma
            new ContentPage
            {
                Id = 1,
                Slug = "nobet-listesi-olusturma",
                Language = "tr",
                Title = "Online Nöbet Listesi Oluşturma",
                MetaDescription = "Ücretsiz online nöbet listesi oluşturun. Hastane, fabrika, güvenlik ve tüm sektörler için akıllı nöbet planlama sistemi. Kayıt olmadan hemen başlayın.",
                MetaKeywords = "nöbet listesi, nöbet programı, vardiya planlama, nöbet çizelgesi, online nöbet, ücretsiz nöbet programı",
                Subtitle = "Saniyeler içinde profesyonel nöbet listeleri oluşturun",
                CtaText = "Hemen Ücretsiz Başla",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>Nöbet Listesi Nedir?</h2>
<p>Nöbet listesi, bir kurumdaki personelin hangi gün ve saatlerde çalışacağını gösteren planlama aracıdır. Hastaneler, fabrikalar, güvenlik şirketleri ve 7/24 hizmet veren tüm işletmeler için vazgeçilmezdir.</p>

<h2>Geldimmi ile Nöbet Listesi Oluşturma</h2>
<p>Geldimmi, nöbet listesi oluşturmayı son derece kolay hale getirir:</p>
<ul>
    <li><strong>Hızlı Personel Ekleme:</strong> Excel'den kopyala-yapıştır ile anında personel ekleyin</li>
    <li><strong>Esnek Vardiya Şablonları:</strong> Sabah, akşam, gece veya özel vardiyalar tanımlayın</li>
    <li><strong>Sürükle-Bırak Atama:</strong> Takvim üzerinde kolayca nöbet atayın</li>
    <li><strong>Akıllı Dağıtım:</strong> Algoritmamız nöbetleri adil şekilde dağıtır</li>
</ul>

<h2>Özellikler</h2>
<h3>📅 Aylık Takvim Görünümü</h3>
<p>Tüm ayı tek bakışta görün. Kimin ne zaman çalıştığını anında takip edin.</p>

<h3>🎨 Renk Kodlama</h3>
<p>Farklı vardiya türlerini renklerle ayırt edin. Sabah mavisi, gece moru gibi.</p>

<h3>📱 Mobil Uyumlu</h3>
<p>Telefonunuzdan veya tabletinizden nöbet listesi oluşturun ve paylaşın.</p>

<h3>📥 Excel Export</h3>
<p>Oluşturduğunuz nöbet listesini tek tıkla Excel'e aktarın.</p>

<h2>Kimler İçin?</h2>
<ul>
    <li>Hastane ve sağlık kuruluşları</li>
    <li>Fabrika ve üretim tesisleri</li>
    <li>Güvenlik şirketleri</li>
    <li>Çağrı merkezleri</li>
    <li>Otel ve turizm işletmeleri</li>
    <li>Market ve perakende zincirleri</li>
</ul>
</div>",
                IsPublished = true,
                DisplayOrder = 1,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // 2. Hemşire Nöbet Programı
            new ContentPage
            {
                Id = 2,
                Slug = "hemsire-nobet-programi",
                Language = "tr",
                Title = "Hemşire Nöbet Programı",
                MetaDescription = "Hastaneler için özel hemşire nöbet planlama sistemi. Adil dağıtım algoritması, gece nöbeti takibi ve otomatik puantaj. Ücretsiz deneyin.",
                MetaKeywords = "hemşire nöbet programı, hastane nöbet listesi, hemşire vardiya, sağlık personeli nöbet, hemşire çalışma saatleri",
                Subtitle = "Hastaneler için özel tasarlanmış akıllı nöbet sistemi",
                CtaText = "Ücretsiz Dene",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>Hemşireler İçin Özel Çözüm</h2>
<p>Hemşire nöbet planlaması, sağlık sektörünün en zorlu konularından biridir. Geldimmi, hemşirelerin iş yükünü dengelemek ve adil bir çalışma ortamı sağlamak için özel olarak tasarlanmıştır.</p>

<h2>Hemşire Nöbet Planlamasının Zorlukları</h2>
<ul>
    <li>Gece nöbetlerinin adil dağıtılması</li>
    <li>Hafta sonu çalışmalarının dengelenmesi</li>
    <li>Ardışık nöbet kontrolü</li>
    <li>Yasal dinlenme sürelerine uyum</li>
    <li>Acil durum ve izin yönetimi</li>
</ul>

<h2>Geldimmi Nasıl Yardımcı Olur?</h2>
<h3>⚖️ Adil Dağıtım Algoritması</h3>
<p>Akıllı algoritmamız, gece nöbetlerini ve hafta sonu çalışmalarını tüm hemşireler arasında eşit dağıtır.</p>

<h3>🌙 Gece Nöbeti Takibi</h3>
<p>Her hemşirenin kaç gece nöbeti tuttuğunu otomatik hesaplar ve puantaja yansıtır.</p>

<h3>📊 Detaylı Puantaj</h3>
<p>Normal çalışma, gece çalışması, hafta sonu ve fazla mesai saatlerini ayrı ayrı hesaplar.</p>

<h3>🔄 16 Saatlik Nöbet Desteği</h3>
<p>Hemşire nöbetlerinde sık kullanılan 16:00-08:00 gibi ertesi güne sarkan vardiyaları destekler.</p>

<h2>Örnek Hemşire Nöbet Planı</h2>
<p>10 hemşireli bir serviste, sistemimiz otomatik olarak:</p>
<ul>
    <li>Her hemşireye ayda ortalama 4-5 gece nöbeti atar</li>
    <li>Hafta sonu çalışmalarını dengeler</li>
    <li>Ardışık gece nöbeti oluşmasını engeller</li>
    <li>İzin ve raporları dikkate alır</li>
</ul>
</div>",
                IsPublished = true,
                DisplayOrder = 2,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // 3. Adil Nöbet Dağıtımı
            new ContentPage
            {
                Id = 3,
                Slug = "adil-nobet-dagitimi",
                Language = "tr",
                Title = "Adil Nöbet Dağıtım Sistemi",
                MetaDescription = "Akıllı algoritma ile adil nöbet dağıtımı. Gece, hafta sonu ve tatil nöbetlerini dengeli şekilde planlayın. Çalışan memnuniyetini artırın.",
                MetaKeywords = "adil nöbet dağıtımı, nöbet algoritması, dengeli vardiya, eşit nöbet, nöbet adaleti",
                Subtitle = "Akıllı algoritma ile dengeli ve adil nöbet planlaması",
                CtaText = "Şimdi Dene",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>Nöbet Dağıtımında Adalet Neden Önemli?</h2>
<p>Adaletsiz nöbet dağıtımı, çalışan memnuniyetsizliği, motivasyon kaybı ve hatta işten ayrılmalara neden olabilir. Geldimmi'nin akıllı algoritması bu sorunu çözer.</p>

<h2>Adil Dağıtım Kriterleri</h2>
<ul>
    <li><strong>Gece Nöbetleri:</strong> Her çalışana eşit sayıda gece nöbeti</li>
    <li><strong>Hafta Sonu:</strong> Cumartesi ve Pazar çalışmalarının dengeli dağıtımı</li>
    <li><strong>Resmi Tatiller:</strong> Bayram ve tatil günlerinin adil paylaşımı</li>
    <li><strong>Toplam Çalışma Saati:</strong> Aylık çalışma sürelerinin dengelenmesi</li>
</ul>

<h2>Algoritmamız Nasıl Çalışır?</h2>
<h3>1. Veri Toplama</h3>
<p>Geçmiş nöbet verilerini ve çalışan tercihlerini analiz eder.</p>

<h3>2. Kısıtları Belirleme</h3>
<p>İzinler, raporlar ve yasal dinlenme süreleri hesaba katılır.</p>

<h3>3. Optimizasyon</h3>
<p>En adil dağıtımı bulmak için matematiksel optimizasyon uygulanır.</p>

<h3>4. Dengeleme</h3>
<p>Gece, hafta sonu ve tatil nöbetleri tüm personel arasında eşitlenir.</p>

<h2>Sonuçlar</h2>
<ul>
    <li>✅ %95 daha az nöbet şikayeti</li>
    <li>✅ Çalışan memnuniyetinde artış</li>
    <li>✅ Yönetici iş yükünde azalma</li>
    <li>✅ Şeffaf ve ölçülebilir dağıtım</li>
</ul>
</div>",
                IsPublished = true,
                DisplayOrder = 3,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // 4. Puantaj Hesaplama
            new ContentPage
            {
                Id = 4,
                Slug = "puantaj-hesaplama",
                Language = "tr",
                Title = "Online Puantaj Hesaplama",
                MetaDescription = "Nöbet listesinden otomatik puantaj oluşturun. Fazla mesai, gece çalışması, hafta sonu ve tatil saatlerini ayrı ayrı hesaplayın. Excel export.",
                MetaKeywords = "puantaj hesaplama, puantaj oluşturma, mesai hesabı, çalışma saati hesaplama, otomatik puantaj",
                Subtitle = "Nöbet listesinden otomatik puantaj ve mesai hesabı",
                CtaText = "Puantaj Oluştur",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>Puantaj Nedir?</h2>
<p>Puantaj, personelin aylık çalışma saatlerini ve türlerini gösteren dokümandır. Bordro hesaplamasının temelini oluşturur ve yasal bir zorunluluktur.</p>

<h2>Geldimmi ile Otomatik Puantaj</h2>
<p>Nöbet listesi oluşturduktan sonra, tek tıkla detaylı puantaj raporunuzu alın:</p>

<h3>📊 Hesaplanan Değerler</h3>
<ul>
    <li><strong>Normal Çalışma:</strong> Standart mesai saatleri içindeki çalışma</li>
    <li><strong>Gece Çalışması:</strong> 20:00-06:00 arası çalışma saatleri</li>
    <li><strong>Hafta Sonu:</strong> Cumartesi ve Pazar günleri çalışma</li>
    <li><strong>Resmi Tatil:</strong> Bayram ve resmi tatil günleri çalışma</li>
    <li><strong>Fazla Mesai:</strong> Günlük veya aylık limite göre hesaplanan ek çalışma</li>
</ul>

<h3>⚙️ Hesaplama Modları</h3>
<p><strong>Günlük Mod:</strong> Her gün için ayrı fazla mesai hesabı. Örneğin, günlük 8 saat çalışması gereken biri 10 saat çalıştıysa, o gün 2 saat fazla mesai yazılır.</p>
<p><strong>Aylık Mod:</strong> Ay sonunda toplam çalışma saatine bakılır. Aylık hedef 176 saat, toplam çalışma 180 saat ise, 4 saat fazla mesai hesaplanır.</p>

<h3>🔧 Ayarlanabilir Parametreler</h3>
<ul>
    <li>Gece başlangıç/bitiş saatleri (örn: 20:00-06:00)</li>
    <li>Günlük çalışma hedefi (örn: 8 saat)</li>
    <li>Aylık çalışma hedefi (örn: 176 saat)</li>
    <li>Mola süreleri</li>
</ul>

<h2>Excel Export</h2>
<p>Oluşturulan puantajı tek tıkla Excel'e aktarın. Bordro sistemlerinize kolayca entegre edin.</p>
</div>",
                IsPublished = true,
                DisplayOrder = 4,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // 5. Fazla Mesai Hesaplama
            new ContentPage
            {
                Id = 5,
                Slug = "fazla-mesai-hesaplama",
                Language = "tr",
                Title = "Fazla Mesai Hesaplama Sistemi",
                MetaDescription = "Günlük ve aylık fazla mesai hesaplama. Otomatik overtime takibi, yasal sınırlar ve raporlama. İş Kanunu'na uygun hesaplama.",
                MetaKeywords = "fazla mesai hesaplama, overtime hesabı, ek mesai, mesai ücreti, fazla çalışma",
                Subtitle = "Günlük veya aylık modda otomatik fazla mesai hesabı",
                CtaText = "Hesaplamaya Başla",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>Fazla Mesai Nedir?</h2>
<p>Fazla mesai (overtime), çalışanın yasal veya sözleşmesel çalışma süresini aşan çalışmasıdır. Türkiye'de İş Kanunu'na göre haftalık 45 saati aşan çalışmalar fazla mesai sayılır.</p>

<h2>İki Farklı Hesaplama Modu</h2>
<h3>📅 Günlük Hesaplama</h3>
<p>Her gün için ayrı ayrı fazla mesai hesaplanır:</p>
<ul>
    <li>Günlük çalışma hedefi: 8 saat</li>
    <li>Bugün çalışılan: 11 saat</li>
    <li>Fazla mesai: 3 saat</li>
</ul>
<p><em>Avantajı: Her günün fazla mesaisi net görülür</em></p>

<h3>📆 Aylık Hesaplama</h3>
<p>Ay sonunda toplam saat üzerinden hesaplanır:</p>
<ul>
    <li>Aylık çalışma hedefi: 176 saat</li>
    <li>Toplam çalışılan: 184 saat</li>
    <li>Fazla mesai: 8 saat</li>
</ul>
<p><em>Avantajı: Bazı günler fazla, bazı günler eksik çalışma dengelenir</em></p>

<h2>Gece Çalışması ve Fazla Mesai</h2>
<p>Örnek: Bir hemşire 16:00-08:00 (16 saat) nöbet tutmuş.</p>
<ul>
    <li>Normal çalışma hedefi: 8 saat/gün × 2 gün = 16 saat</li>
    <li>Çalışılan: 16 saat</li>
    <li>Fazla mesai: 0 saat</li>
    <li>Gece çalışması (20:00-06:00): 10 saat</li>
</ul>
<p>Bu durumda fazla mesai yoktur, ancak 10 saat gece çalışması tazminatı uygulanabilir.</p>

<h2>Yasal Sınırlar</h2>
<ul>
    <li>Günlük fazla mesai: Maksimum 3 saat</li>
    <li>Yıllık fazla mesai: Maksimum 270 saat</li>
    <li>Fazla mesai ücreti: Normal ücretin %50 fazlası</li>
</ul>
</div>",
                IsPublished = true,
                DisplayOrder = 5,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // ==================== ENGLISH PAGES ====================

            // 1. Shift Scheduling
            new ContentPage
            {
                Id = 6,
                Slug = "shift-scheduling",
                Language = "en",
                Title = "Online Shift Scheduling Software",
                MetaDescription = "Free online shift scheduling tool. Create employee schedules for hospitals, factories, and businesses. Smart algorithm for fair distribution. Start without registration.",
                MetaKeywords = "shift scheduling, employee scheduling, work schedule maker, duty roster, shift planner, free scheduling software",
                Subtitle = "Create professional shift schedules in seconds",
                CtaText = "Start Free Now",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>What is Shift Scheduling?</h2>
<p>Shift scheduling is the process of creating work schedules that assign employees to specific shifts. It's essential for hospitals, factories, security companies, and any business operating 24/7.</p>

<h2>Shift Scheduling with Geldimmi</h2>
<p>Geldimmi makes shift scheduling incredibly easy:</p>
<ul>
    <li><strong>Quick Employee Import:</strong> Copy-paste from Excel to add employees instantly</li>
    <li><strong>Flexible Shift Templates:</strong> Define morning, evening, night, or custom shifts</li>
    <li><strong>Drag-and-Drop Assignment:</strong> Easily assign shifts on the calendar</li>
    <li><strong>Smart Distribution:</strong> Our algorithm distributes shifts fairly</li>
</ul>

<h2>Features</h2>
<h3>📅 Monthly Calendar View</h3>
<p>See the entire month at a glance. Track who's working when instantly.</p>

<h3>🎨 Color Coding</h3>
<p>Distinguish different shift types with colors. Morning blue, night purple, etc.</p>

<h3>📱 Mobile Friendly</h3>
<p>Create and share shift schedules from your phone or tablet.</p>

<h3>📥 Excel Export</h3>
<p>Export your shift schedule to Excel with one click.</p>

<h2>Who Is It For?</h2>
<ul>
    <li>Hospitals and healthcare facilities</li>
    <li>Factories and manufacturing plants</li>
    <li>Security companies</li>
    <li>Call centers</li>
    <li>Hotels and tourism businesses</li>
    <li>Retail stores and chains</li>
</ul>
</div>",
                IsPublished = true,
                DisplayOrder = 1,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // 2. Nurse Shift Planner
            new ContentPage
            {
                Id = 7,
                Slug = "nurse-shift-planner",
                Language = "en",
                Title = "Nurse Shift Planner",
                MetaDescription = "Specialized nurse scheduling software for hospitals. Fair distribution algorithm, night shift tracking, and automatic timesheet. Try free.",
                MetaKeywords = "nurse shift planner, hospital scheduling, nurse roster, healthcare scheduling, nurse duty schedule, nursing shifts",
                Subtitle = "Smart scheduling system designed for hospitals",
                CtaText = "Try Free",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>A Solution Built for Nurses</h2>
<p>Nurse shift planning is one of the most challenging aspects of healthcare management. Geldimmi is specifically designed to balance nurse workloads and create a fair working environment.</p>

<h2>Challenges in Nurse Scheduling</h2>
<ul>
    <li>Fair distribution of night shifts</li>
    <li>Balancing weekend work</li>
    <li>Preventing consecutive night shifts</li>
    <li>Compliance with legal rest periods</li>
    <li>Emergency and leave management</li>
</ul>

<h2>How Geldimmi Helps</h2>
<h3>⚖️ Fair Distribution Algorithm</h3>
<p>Our smart algorithm distributes night shifts and weekend work equally among all nurses.</p>

<h3>🌙 Night Shift Tracking</h3>
<p>Automatically calculates how many night shifts each nurse has worked and reflects it in the timesheet.</p>

<h3>📊 Detailed Timesheet</h3>
<p>Calculates regular work, night work, weekends, and overtime separately.</p>

<h3>🔄 16-Hour Shift Support</h3>
<p>Supports overnight shifts like 4 PM to 8 AM commonly used in nursing.</p>

<h2>Example Nurse Schedule</h2>
<p>In a ward with 10 nurses, our system automatically:</p>
<ul>
    <li>Assigns each nurse an average of 4-5 night shifts per month</li>
    <li>Balances weekend work</li>
    <li>Prevents consecutive night shifts</li>
    <li>Considers leaves and sick days</li>
</ul>
</div>",
                IsPublished = true,
                DisplayOrder = 2,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // 3. Fair Shift Distribution
            new ContentPage
            {
                Id = 8,
                Slug = "fair-shift-distribution",
                Language = "en",
                Title = "Fair Shift Distribution System",
                MetaDescription = "Smart algorithm for fair shift distribution. Balance night, weekend, and holiday shifts. Increase employee satisfaction.",
                MetaKeywords = "fair shift distribution, shift algorithm, balanced scheduling, equal shifts, shift fairness",
                Subtitle = "Balanced and fair scheduling with smart algorithm",
                CtaText = "Try Now",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>Why Is Fairness Important in Shift Distribution?</h2>
<p>Unfair shift distribution can lead to employee dissatisfaction, motivation loss, and even resignations. Geldimmi's smart algorithm solves this problem.</p>

<h2>Fair Distribution Criteria</h2>
<ul>
    <li><strong>Night Shifts:</strong> Equal number of night shifts for each employee</li>
    <li><strong>Weekends:</strong> Balanced distribution of Saturday and Sunday work</li>
    <li><strong>Holidays:</strong> Fair sharing of public holiday duties</li>
    <li><strong>Total Hours:</strong> Balancing monthly work hours</li>
</ul>

<h2>How Our Algorithm Works</h2>
<h3>1. Data Collection</h3>
<p>Analyzes past shift data and employee preferences.</p>

<h3>2. Constraint Definition</h3>
<p>Considers leaves, sick days, and legal rest periods.</p>

<h3>3. Optimization</h3>
<p>Mathematical optimization is applied to find the fairest distribution.</p>

<h3>4. Balancing</h3>
<p>Night, weekend, and holiday shifts are equalized among all staff.</p>

<h2>Results</h2>
<ul>
    <li>✅ 95% fewer shift complaints</li>
    <li>✅ Increased employee satisfaction</li>
    <li>✅ Reduced manager workload</li>
    <li>✅ Transparent and measurable distribution</li>
</ul>
</div>",
                IsPublished = true,
                DisplayOrder = 3,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // 4. Timesheet Calculation
            new ContentPage
            {
                Id = 9,
                Slug = "timesheet-calculation",
                Language = "en",
                Title = "Online Timesheet Calculation",
                MetaDescription = "Generate automatic timesheets from shift schedules. Calculate overtime, night work, weekends, and holidays separately. Excel export.",
                MetaKeywords = "timesheet calculation, timesheet generator, hours calculation, work hours tracking, automatic timesheet",
                Subtitle = "Automatic timesheet and work hours calculation from shift schedules",
                CtaText = "Create Timesheet",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>What is a Timesheet?</h2>
<p>A timesheet is a document that shows the monthly working hours and types for each employee. It forms the basis of payroll calculation and is a legal requirement.</p>

<h2>Automatic Timesheet with Geldimmi</h2>
<p>After creating a shift schedule, get your detailed timesheet report with one click:</p>

<h3>📊 Calculated Values</h3>
<ul>
    <li><strong>Regular Work:</strong> Work within standard working hours</li>
    <li><strong>Night Work:</strong> Working hours between 8 PM and 6 AM</li>
    <li><strong>Weekend:</strong> Saturday and Sunday work</li>
    <li><strong>Public Holiday:</strong> Work on public holidays</li>
    <li><strong>Overtime:</strong> Extra work calculated daily or monthly</li>
</ul>

<h3>⚙️ Calculation Modes</h3>
<p><strong>Daily Mode:</strong> Overtime calculated separately for each day. For example, if someone who should work 8 hours works 10 hours, 2 hours overtime is recorded for that day.</p>
<p><strong>Monthly Mode:</strong> Based on total hours at month end. If monthly target is 176 hours and total work is 180 hours, 4 hours overtime is calculated.</p>

<h3>🔧 Adjustable Parameters</h3>
<ul>
    <li>Night start/end times (e.g., 8 PM - 6 AM)</li>
    <li>Daily work target (e.g., 8 hours)</li>
    <li>Monthly work target (e.g., 176 hours)</li>
    <li>Break times</li>
</ul>

<h2>Excel Export</h2>
<p>Export the generated timesheet to Excel with one click. Easily integrate with your payroll systems.</p>
</div>",
                IsPublished = true,
                DisplayOrder = 4,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },

            // 5. Overtime Calculation
            new ContentPage
            {
                Id = 10,
                Slug = "overtime-calculation",
                Language = "en",
                Title = "Overtime Calculation System",
                MetaDescription = "Daily and monthly overtime calculation. Automatic overtime tracking, legal limits, and reporting. Labor law compliant calculation.",
                MetaKeywords = "overtime calculation, overtime tracking, extra hours, overtime pay, work hours",
                Subtitle = "Automatic overtime calculation in daily or monthly mode",
                CtaText = "Start Calculating",
                CtaUrl = "/app",
                Content = @"<div class='feature-content'>
<h2>What is Overtime?</h2>
<p>Overtime is work that exceeds an employee's legal or contractual working hours. In most countries, work exceeding 40-45 hours per week is considered overtime.</p>

<h2>Two Different Calculation Modes</h2>
<h3>📅 Daily Calculation</h3>
<p>Overtime is calculated separately for each day:</p>
<ul>
    <li>Daily work target: 8 hours</li>
    <li>Worked today: 11 hours</li>
    <li>Overtime: 3 hours</li>
</ul>
<p><em>Advantage: Each day's overtime is clearly visible</em></p>

<h3>📆 Monthly Calculation</h3>
<p>Calculated based on total hours at month end:</p>
<ul>
    <li>Monthly work target: 176 hours</li>
    <li>Total worked: 184 hours</li>
    <li>Overtime: 8 hours</li>
</ul>
<p><em>Advantage: Extra work on some days can be balanced by less work on others</em></p>

<h2>Night Work and Overtime</h2>
<p>Example: A nurse worked a 4 PM to 8 AM shift (16 hours).</p>
<ul>
    <li>Normal work target: 8 hours/day × 2 days = 16 hours</li>
    <li>Worked: 16 hours</li>
    <li>Overtime: 0 hours</li>
    <li>Night work (8 PM - 6 AM): 10 hours</li>
</ul>
<p>In this case, there's no overtime, but 10 hours of night work premium may apply.</p>

<h2>Legal Limits</h2>
<ul>
    <li>Daily overtime: Maximum 3 hours</li>
    <li>Annual overtime: Maximum 270 hours</li>
    <li>Overtime pay: 50% more than regular rate</li>
</ul>
</div>",
                IsPublished = true,
                DisplayOrder = 5,
                PageType = PageType.Feature,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            }
        );
    }
}

