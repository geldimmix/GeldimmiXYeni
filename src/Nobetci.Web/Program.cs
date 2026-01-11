using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Middleware;
using Nobetci.Web.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    
    // User settings
    options.User.RequireUniqueEmail = true;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("tr"),
        new CultureInfo("en")
    };
    
    options.DefaultRequestCulture = new RequestCulture("tr");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    
    // Add cookie provider for language selection
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider
    {
        CookieName = ".Nobetci.Culture"
    });
});

// Custom services
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddHttpClient<ITranslationService, TranslationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IVisitorLogService, VisitorLogService>();

// Session for guest users
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Nobetci.Session";
});

// MVC
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Localization middleware
app.UseRequestLocalization();

// Session
app.UseSession();

// Visitor tracking - DISABLED: VisitorLogs table doesn't exist yet
// app.UseVisitorTracking();

app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "localized",
    pattern: "{lang:regex(^(tr|en)$)}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Apply migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // Apply pending column additions manually before migration
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='SaturdayWorkHours') THEN
                        ALTER TABLE ""Employees"" ADD COLUMN ""SaturdayWorkHours"" DECIMAL NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Employees' AND column_name='WeekendWorkMode') THEN
                        ALTER TABLE ""Employees"" ADD COLUMN ""WeekendWorkMode"" INTEGER DEFAULT 0 NOT NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Shifts' AND column_name='IsDayOff') THEN
                        ALTER TABLE ""Shifts"" ADD COLUMN ""IsDayOff"" BOOLEAN DEFAULT FALSE NOT NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='Shifts' AND column_name='OvernightHoursMode') THEN
                        ALTER TABLE ""Shifts"" ADD COLUMN ""OvernightHoursMode"" INTEGER DEFAULT 0 NOT NULL;
                    END IF;
                END $$;
            ");
        }
        catch { /* Columns may already exist */ }
        
        await context.Database.MigrateAsync();
        
        // Seed initial content pages
        await SeedContentPages(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.Run();

// Seed method for content pages
static async Task SeedContentPages(ApplicationDbContext context)
{
    // Check if we already have the correct pages
    var hasCorrectPages = await context.ContentPages.AnyAsync(p => p.Slug == "nobet-listesi-olusturma" && p.Language == "tr");
    if (hasCorrectPages) return;
    
    // Clear ALL existing content pages first
    await context.Database.ExecuteSqlRawAsync("DELETE FROM \"ContentPages\"");
    
    // Reset the sequence for PostgreSQL
    try 
    {
        await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"ContentPages_Id_seq\" RESTART WITH 1");
    }
    catch { /* Ignore if sequence doesn't exist */ }

    var contentPages = new List<ContentPage>
    {
        // ==================== TURKISH PAGES ====================
        
        // 1. Nöbet Listesi Oluşturma
        new ContentPage
        {
            Slug = "nobet-listesi-olusturma",
            Language = "tr",
            Title = "Online Nöbet Listesi Oluşturma",
            MetaDescription = "Ücretsiz online nöbet listesi oluşturun. Hastane, fabrika, güvenlik ve tüm sektörler için akıllı nöbet planlama sistemi. Kayıt olmadan hemen başlayın.",
            MetaKeywords = "nöbet listesi, nöbet programı, vardiya planlama, nöbet çizelgesi, online nöbet, ücretsiz nöbet programı",
            Subtitle = "Saniyeler içinde profesyonel nöbet listeleri oluşturun",
            CtaText = "Hemen Ücretsiz Başla",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 1,
            IsPublished = true
        },

        // 2. Hemşire Nöbet Programı
        new ContentPage
        {
            Slug = "hemsire-nobet-programi",
            Language = "tr",
            Title = "Hemşire Nöbet Programı",
            MetaDescription = "Hastaneler için özel hemşire nöbet planlama sistemi. Adil dağıtım algoritması, gece nöbeti takibi ve otomatik puantaj. Ücretsiz deneyin.",
            MetaKeywords = "hemşire nöbet programı, hastane nöbet listesi, hemşire vardiya, sağlık personeli nöbet, hemşire çalışma saatleri",
            Subtitle = "Hastaneler için özel tasarlanmış akıllı nöbet sistemi",
            CtaText = "Ücretsiz Dene",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 2,
            IsPublished = true
        },

        // 3. Adil Nöbet Dağıtımı
        new ContentPage
        {
            Slug = "adil-nobet-dagitimi",
            Language = "tr",
            Title = "Adil Nöbet Dağıtım Sistemi",
            MetaDescription = "Akıllı algoritma ile adil nöbet dağıtımı. Gece, hafta sonu ve tatil nöbetlerini dengeli şekilde planlayın. Çalışan memnuniyetini artırın.",
            MetaKeywords = "adil nöbet dağıtımı, nöbet algoritması, dengeli vardiya, eşit nöbet, nöbet adaleti",
            Subtitle = "Akıllı algoritma ile dengeli ve adil nöbet planlaması",
            CtaText = "Şimdi Dene",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 3,
            IsPublished = true
        },

        // 4. Puantaj Hesaplama
        new ContentPage
        {
            Slug = "puantaj-hesaplama",
            Language = "tr",
            Title = "Online Puantaj Hesaplama",
            MetaDescription = "Nöbet listesinden otomatik puantaj oluşturun. Fazla mesai, gece çalışması, hafta sonu ve tatil saatlerini ayrı ayrı hesaplayın. Excel export.",
            MetaKeywords = "puantaj hesaplama, puantaj oluşturma, mesai hesabı, çalışma saati hesaplama, otomatik puantaj",
            Subtitle = "Nöbet listesinden otomatik puantaj ve mesai hesabı",
            CtaText = "Puantaj Oluştur",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 4,
            IsPublished = true
        },

        // 5. Fazla Mesai Hesaplama
        new ContentPage
        {
            Slug = "fazla-mesai-hesaplama",
            Language = "tr",
            Title = "Fazla Mesai Hesaplama Sistemi",
            MetaDescription = "Günlük ve aylık fazla mesai hesaplama. Otomatik overtime takibi, yasal sınırlar ve raporlama. İş Kanunu'na uygun hesaplama.",
            MetaKeywords = "fazla mesai hesaplama, overtime hesabı, ek mesai, mesai ücreti, fazla çalışma",
            Subtitle = "Günlük veya aylık modda otomatik fazla mesai hesabı",
            CtaText = "Hesaplamaya Başla",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 5,
            IsPublished = true
        },

        // ==================== ENGLISH PAGES ====================

        // 1. Shift Scheduling
        new ContentPage
        {
            Slug = "shift-scheduling",
            Language = "en",
            Title = "Online Shift Scheduling Software",
            MetaDescription = "Free online shift scheduling tool. Create employee schedules for hospitals, factories, and businesses. Smart algorithm for fair distribution. Start without registration.",
            MetaKeywords = "shift scheduling, employee scheduling, work schedule maker, duty roster, shift planner, free scheduling software",
            Subtitle = "Create professional shift schedules in seconds",
            CtaText = "Start Free Now",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 1,
            IsPublished = true
        },

        // 2. Nurse Shift Planner
        new ContentPage
        {
            Slug = "nurse-shift-planner",
            Language = "en",
            Title = "Nurse Shift Planner",
            MetaDescription = "Specialized nurse scheduling software for hospitals. Fair distribution algorithm, night shift tracking, and automatic timesheet. Try free.",
            MetaKeywords = "nurse shift planner, hospital scheduling, nurse roster, healthcare scheduling, nurse duty schedule, nursing shifts",
            Subtitle = "Smart scheduling system designed for hospitals",
            CtaText = "Try Free",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 2,
            IsPublished = true
        },

        // 3. Fair Shift Distribution
        new ContentPage
        {
            Slug = "fair-shift-distribution",
            Language = "en",
            Title = "Fair Shift Distribution System",
            MetaDescription = "Smart algorithm for fair shift distribution. Balance night, weekend, and holiday shifts. Increase employee satisfaction.",
            MetaKeywords = "fair shift distribution, shift algorithm, balanced scheduling, equal shifts, shift fairness",
            Subtitle = "Balanced and fair scheduling with smart algorithm",
            CtaText = "Try Now",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 3,
            IsPublished = true
        },

        // 4. Timesheet Calculation
        new ContentPage
        {
            Slug = "timesheet-calculation",
            Language = "en",
            Title = "Online Timesheet Calculation",
            MetaDescription = "Generate automatic timesheets from shift schedules. Calculate overtime, night work, weekends, and holidays separately. Excel export.",
            MetaKeywords = "timesheet calculation, timesheet generator, hours calculation, work hours tracking, automatic timesheet",
            Subtitle = "Automatic timesheet and work hours calculation from shift schedules",
            CtaText = "Create Timesheet",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 4,
            IsPublished = true
        },

        // 5. Overtime Calculation
        new ContentPage
        {
            Slug = "overtime-calculation",
            Language = "en",
            Title = "Overtime Calculation System",
            MetaDescription = "Daily and monthly overtime calculation. Automatic overtime tracking, legal limits, and reporting. Labor law compliant calculation.",
            MetaKeywords = "overtime calculation, overtime tracking, extra hours, overtime pay, work hours",
            Subtitle = "Automatic overtime calculation in daily or monthly mode",
            CtaText = "Start Calculating",
            CtaUrl = "/app",
            PageType = PageType.Feature,
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
            DisplayOrder = 5,
            IsPublished = true
        }
    };

    await context.ContentPages.AddRangeAsync(contentPages);
    await context.SaveChangesAsync();
}
