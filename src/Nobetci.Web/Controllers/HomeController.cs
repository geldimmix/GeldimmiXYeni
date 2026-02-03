using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Resources;
using Nobetci.Web.Services;

namespace Nobetci.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public HomeController(
        ILogger<HomeController> logger,
        IStringLocalizer<SharedResource> localizer,
        ApplicationDbContext context,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _logger = logger;
        _localizer = localizer;
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        // Always show landing page so users can go from App (logo) to root and use contact form
        return View();
    }

    /// <summary>
    /// SEO content pages - Turkish
    /// </summary>
    [Route("rehber/{slug}")]
    public async Task<IActionResult> PageTurkish(string slug)
    {
        return await RenderContentPage(slug, "tr");
    }

    /// <summary>
    /// SEO content pages - English
    /// </summary>
    [Route("guide/{slug}")]
    public async Task<IActionResult> PageEnglish(string slug)
    {
        return await RenderContentPage(slug, "en");
    }

    private async Task<IActionResult> RenderContentPage(string slug, string language)
    {
        var page = await _context.ContentPages
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Language == language && p.IsPublished);
        
        if (page == null)
        {
            return NotFound();
        }
        
        ViewData["Title"] = page.Title;
        ViewData["MetaDescription"] = page.MetaDescription;
        ViewData["MetaKeywords"] = page.MetaKeywords;
        
        return View("ContentPage", page);
    }

    /// <summary>
    /// Change language
    /// </summary>
    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions 
            { 
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            }
        );

        return LocalRedirect(returnUrl ?? "/");
    }

    /// <summary>
    /// Get current language from culture
    /// </summary>
    [HttpGet]
    [Route("api/language")]
    public IActionResult GetLanguage()
    {
        var language = GetCurrentLanguage();
        return Json(new { language });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Contact form - saves to DB and sends email via Resend; admin can view in /admin/contacts
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactViewModel model)
    {
        var isTurkish = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
        if (!ModelState.IsValid)
        {
            TempData["ContactError"] = isTurkish ? "Lütfen tüm alanları doğru doldurun." : "Please fill all fields correctly.";
            return RedirectToAction(nameof(Index), new { showContact = 1 });
        }

        var submission = new ContactSubmission
        {
            Name = model.Name.Trim(),
            Email = model.Email.Trim(),
            Message = model.Message.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.ContactSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        var toEmail = _configuration["Contact:ToEmail"] ?? _configuration["Email:ContactTo"] ?? "geldimmix@gmail.com";
        var subject = isTurkish
            ? $"İletişim Formu: {model.Name}"
            : $"Contact Form: {model.Name}";
        var body = $@"
<h3>{(isTurkish ? "Yeni iletişim formu mesajı" : "New contact form message")}</h3>
<p><strong>{(isTurkish ? "Ad Soyad" : "Name")}:</strong> {System.Net.WebUtility.HtmlEncode(model.Name)}</p>
<p><strong>E-mail:</strong> {System.Net.WebUtility.HtmlEncode(model.Email)}</p>
<p><strong>{(isTurkish ? "Mesaj" : "Message")}:</strong></p>
<pre style=""white-space:pre-wrap;font-family:inherit;"">{System.Net.WebUtility.HtmlEncode(model.Message)}</pre>
";

        try
        {
            await _emailSender.SendEmailAsync(toEmail, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contact form email failed to {To}", toEmail);
        }

        TempData["ContactSuccess"] = isTurkish
            ? "Mesajınız alındı, en kısa sürede dönüş yapacağız."
            : "Your message has been received. We will get back to you soon.";
        return RedirectToAction(nameof(Index), new { showContact = 1 });
    }

    /// <summary>
    /// Admin: Seed content pages (temporary endpoint)
    /// </summary>
    [Route("admin/seed-pages")]
    public async Task<IActionResult> SeedPages()
    {
        try
        {
            // Check if pages already exist
            var hasPages = await _context.ContentPages.AnyAsync(p => p.Slug == "nobet-listesi-olusturma");
            if (hasPages)
            {
                return Content($"Pages already exist. Total: {await _context.ContentPages.CountAsync()}");
            }

            // Clear existing pages
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"ContentPages\"");

            var pages = GetSeedContentPages();
            await _context.ContentPages.AddRangeAsync(pages);
            await _context.SaveChangesAsync();

            return Content($"Seeded {pages.Count} pages successfully!");
        }
        catch (Exception ex)
        {
            return Content($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Debug: List all content pages
    /// </summary>
    [Route("admin/list-pages")]
    public async Task<IActionResult> ListPages()
    {
        var pages = await _context.ContentPages.Select(p => new { p.Id, p.Slug, p.Language, p.Title, p.IsPublished }).ToListAsync();
        return Json(pages);
    }

    private static List<Data.Entities.ContentPage> GetSeedContentPages()
    {
        return new List<Data.Entities.ContentPage>
        {
            // TURKISH PAGES
            new Data.Entities.ContentPage
            {
                Slug = "nobet-listesi-olusturma",
                Language = "tr",
                Title = "Online Nöbet Listesi Oluşturma",
                MetaDescription = "Ücretsiz online nöbet listesi oluşturun. Hastane, fabrika, güvenlik ve tüm sektörler için akıllı nöbet planlama sistemi.",
                MetaKeywords = "nöbet listesi, nöbet programı, vardiya planlama, nöbet çizelgesi",
                Subtitle = "Saniyeler içinde profesyonel nöbet listeleri oluşturun",
                CtaText = "Hemen Ücretsiz Başla",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>Nöbet Listesi Nedir?</h2>
<p>Nöbet listesi, bir kurumdaki personelin hangi gün ve saatlerde çalışacağını gösteren planlama aracıdır. Hastaneler, fabrikalar, güvenlik şirketleri ve 7/24 hizmet veren tüm işletmeler için vazgeçilmezdir.</p>

<h2>Geldimmi ile Nöbet Listesi Oluşturma</h2>
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
<p>Farklı vardiya türlerini renklerle ayırt edin.</p>

<h3>📥 Excel Export</h3>
<p>Oluşturduğunuz nöbet listesini tek tıkla Excel'e aktarın.</p>

<h2>Kimler İçin?</h2>
<ul>
    <li>Hastane ve sağlık kuruluşları</li>
    <li>Fabrika ve üretim tesisleri</li>
    <li>Güvenlik şirketleri</li>
    <li>Çağrı merkezleri</li>
    <li>Otel ve turizm işletmeleri</li>
</ul>
</div>",
                DisplayOrder = 1,
                IsPublished = true
            },
            new Data.Entities.ContentPage
            {
                Slug = "hemsire-nobet-programi",
                Language = "tr",
                Title = "Hemşire Nöbet Programı",
                MetaDescription = "Hastaneler için özel hemşire nöbet planlama sistemi. Adil dağıtım algoritması, gece nöbeti takibi ve otomatik puantaj.",
                MetaKeywords = "hemşire nöbet programı, hastane nöbet listesi, hemşire vardiya",
                Subtitle = "Hastaneler için özel tasarlanmış akıllı nöbet sistemi",
                CtaText = "Ücretsiz Dene",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>Hemşireler İçin Özel Çözüm</h2>
<p>Hemşire nöbet planlaması, sağlık sektörünün en zorlu konularından biridir. Geldimmi, hemşirelerin iş yükünü dengelemek için özel olarak tasarlanmıştır.</p>

<h2>Hemşire Nöbet Planlamasının Zorlukları</h2>
<ul>
    <li>Gece nöbetlerinin adil dağıtılması</li>
    <li>Hafta sonu çalışmalarının dengelenmesi</li>
    <li>Ardışık nöbet kontrolü</li>
    <li>Yasal dinlenme sürelerine uyum</li>
</ul>

<h2>Geldimmi Nasıl Yardımcı Olur?</h2>
<h3>⚖️ Adil Dağıtım Algoritması</h3>
<p>Akıllı algoritmamız, gece nöbetlerini ve hafta sonu çalışmalarını tüm hemşireler arasında eşit dağıtır.</p>

<h3>🌙 Gece Nöbeti Takibi</h3>
<p>Her hemşirenin kaç gece nöbeti tuttuğunu otomatik hesaplar.</p>

<h3>📊 Detaylı Puantaj</h3>
<p>Normal çalışma, gece çalışması, hafta sonu ve fazla mesai saatlerini ayrı ayrı hesaplar.</p>

<h3>🔄 16 Saatlik Nöbet Desteği</h3>
<p>16:00-08:00 gibi ertesi güne sarkan vardiyaları destekler.</p>
</div>",
                DisplayOrder = 2,
                IsPublished = true
            },
            new Data.Entities.ContentPage
            {
                Slug = "adil-nobet-dagitimi",
                Language = "tr",
                Title = "Adil Nöbet Dağıtım Sistemi",
                MetaDescription = "Akıllı algoritma ile adil nöbet dağıtımı. Gece, hafta sonu ve tatil nöbetlerini dengeli şekilde planlayın.",
                MetaKeywords = "adil nöbet dağıtımı, nöbet algoritması, dengeli vardiya",
                Subtitle = "Akıllı algoritma ile dengeli ve adil nöbet planlaması",
                CtaText = "Şimdi Dene",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>Nöbet Dağıtımında Adalet Neden Önemli?</h2>
<p>Adaletsiz nöbet dağıtımı, çalışan memnuniyetsizliği ve motivasyon kaybına neden olabilir. Geldimmi'nin akıllı algoritması bu sorunu çözer.</p>

<h2>Adil Dağıtım Kriterleri</h2>
<ul>
    <li><strong>Gece Nöbetleri:</strong> Her çalışana eşit sayıda gece nöbeti</li>
    <li><strong>Hafta Sonu:</strong> Cumartesi ve Pazar çalışmalarının dengeli dağıtımı</li>
    <li><strong>Resmi Tatiller:</strong> Bayram günlerinin adil paylaşımı</li>
    <li><strong>Toplam Çalışma Saati:</strong> Aylık çalışma sürelerinin dengelenmesi</li>
</ul>

<h2>Sonuçlar</h2>
<ul>
    <li>✅ %95 daha az nöbet şikayeti</li>
    <li>✅ Çalışan memnuniyetinde artış</li>
    <li>✅ Yönetici iş yükünde azalma</li>
</ul>
</div>",
                DisplayOrder = 3,
                IsPublished = true
            },
            new Data.Entities.ContentPage
            {
                Slug = "puantaj-hesaplama",
                Language = "tr",
                Title = "Online Puantaj Hesaplama",
                MetaDescription = "Nöbet listesinden otomatik puantaj oluşturun. Fazla mesai, gece çalışması, hafta sonu saatlerini hesaplayın.",
                MetaKeywords = "puantaj hesaplama, puantaj oluşturma, mesai hesabı",
                Subtitle = "Nöbet listesinden otomatik puantaj ve mesai hesabı",
                CtaText = "Puantaj Oluştur",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>Puantaj Nedir?</h2>
<p>Puantaj, personelin aylık çalışma saatlerini gösteren dokümandır. Bordro hesaplamasının temelini oluşturur.</p>

<h2>Hesaplanan Değerler</h2>
<ul>
    <li><strong>Normal Çalışma:</strong> Standart mesai saatleri</li>
    <li><strong>Gece Çalışması:</strong> 20:00-06:00 arası</li>
    <li><strong>Hafta Sonu:</strong> Cumartesi ve Pazar günleri</li>
    <li><strong>Fazla Mesai:</strong> Günlük veya aylık hesaplama</li>
</ul>

<h2>Hesaplama Modları</h2>
<p><strong>Günlük Mod:</strong> Her gün için ayrı fazla mesai hesabı.</p>
<p><strong>Aylık Mod:</strong> Ay sonunda toplam saate bakılır.</p>

<h2>Excel Export</h2>
<p>Oluşturulan puantajı tek tıkla Excel'e aktarın.</p>
</div>",
                DisplayOrder = 4,
                IsPublished = true
            },
            new Data.Entities.ContentPage
            {
                Slug = "fazla-mesai-hesaplama",
                Language = "tr",
                Title = "Fazla Mesai Hesaplama",
                MetaDescription = "Günlük ve aylık fazla mesai hesaplama. Otomatik overtime takibi ve raporlama.",
                MetaKeywords = "fazla mesai hesaplama, overtime hesabı, ek mesai",
                Subtitle = "Günlük veya aylık modda otomatik fazla mesai hesabı",
                CtaText = "Hesaplamaya Başla",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>Fazla Mesai Nedir?</h2>
<p>Fazla mesai, çalışanın yasal çalışma süresini aşan çalışmasıdır. Haftalık 45 saati aşan çalışmalar fazla mesai sayılır.</p>

<h2>Hesaplama Modları</h2>
<h3>📅 Günlük Hesaplama</h3>
<p>Her gün için ayrı hesaplanır. Günlük 8 saat hedefli biri 11 saat çalıştıysa, 3 saat fazla mesai.</p>

<h3>📆 Aylık Hesaplama</h3>
<p>Ay sonunda toplam saate bakılır. Aylık hedef 176 saat, çalışılan 184 saat ise, 8 saat fazla mesai.</p>

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

            // ENGLISH PAGES
            new Data.Entities.ContentPage
            {
                Slug = "shift-scheduling",
                Language = "en",
                Title = "Online Shift Scheduling Software",
                MetaDescription = "Free online shift scheduling tool. Create employee schedules for hospitals, factories, and businesses.",
                MetaKeywords = "shift scheduling, employee scheduling, work schedule maker, duty roster",
                Subtitle = "Create professional shift schedules in seconds",
                CtaText = "Start Free Now",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>What is Shift Scheduling?</h2>
<p>Shift scheduling is the process of creating work schedules that assign employees to specific shifts. Essential for hospitals, factories, and 24/7 businesses.</p>

<h2>Features</h2>
<ul>
    <li><strong>Quick Import:</strong> Copy-paste from Excel</li>
    <li><strong>Flexible Templates:</strong> Morning, evening, night shifts</li>
    <li><strong>Drag-and-Drop:</strong> Easy calendar assignment</li>
    <li><strong>Smart Distribution:</strong> Fair shift allocation</li>
</ul>

<h2>Who Is It For?</h2>
<ul>
    <li>Hospitals and healthcare</li>
    <li>Factories and manufacturing</li>
    <li>Security companies</li>
    <li>Call centers</li>
    <li>Hotels and tourism</li>
</ul>
</div>",
                DisplayOrder = 1,
                IsPublished = true
            },
            new Data.Entities.ContentPage
            {
                Slug = "nurse-shift-planner",
                Language = "en",
                Title = "Nurse Shift Planner",
                MetaDescription = "Specialized nurse scheduling software for hospitals. Fair distribution algorithm and automatic timesheet.",
                MetaKeywords = "nurse shift planner, hospital scheduling, nurse roster",
                Subtitle = "Smart scheduling system designed for hospitals",
                CtaText = "Try Free",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>Built for Nurses</h2>
<p>Nurse shift planning is challenging. Geldimmi balances workloads and creates fair schedules.</p>

<h2>Challenges We Solve</h2>
<ul>
    <li>Fair night shift distribution</li>
    <li>Weekend work balancing</li>
    <li>Consecutive shift prevention</li>
    <li>Legal rest compliance</li>
</ul>

<h2>Features</h2>
<ul>
    <li>⚖️ Fair Distribution Algorithm</li>
    <li>🌙 Night Shift Tracking</li>
    <li>📊 Detailed Timesheet</li>
    <li>🔄 16-Hour Shift Support</li>
</ul>
</div>",
                DisplayOrder = 2,
                IsPublished = true
            },
            new Data.Entities.ContentPage
            {
                Slug = "fair-shift-distribution",
                Language = "en",
                Title = "Fair Shift Distribution System",
                MetaDescription = "Smart algorithm for fair shift distribution. Balance night, weekend, and holiday shifts.",
                MetaKeywords = "fair shift distribution, shift algorithm, balanced scheduling",
                Subtitle = "Balanced and fair scheduling with smart algorithm",
                CtaText = "Try Now",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>Why Fairness Matters</h2>
<p>Unfair distribution causes dissatisfaction and resignations. Our algorithm solves this.</p>

<h2>What We Balance</h2>
<ul>
    <li><strong>Night Shifts:</strong> Equal distribution</li>
    <li><strong>Weekends:</strong> Balanced Saturday/Sunday</li>
    <li><strong>Holidays:</strong> Fair sharing</li>
    <li><strong>Total Hours:</strong> Monthly balancing</li>
</ul>

<h2>Results</h2>
<ul>
    <li>✅ 95% fewer complaints</li>
    <li>✅ Higher satisfaction</li>
    <li>✅ Less manager workload</li>
</ul>
</div>",
                DisplayOrder = 3,
                IsPublished = true
            },
            new Data.Entities.ContentPage
            {
                Slug = "timesheet-calculation",
                Language = "en",
                Title = "Online Timesheet Calculation",
                MetaDescription = "Generate automatic timesheets from shift schedules. Calculate overtime, night work, weekends.",
                MetaKeywords = "timesheet calculation, timesheet generator, hours calculation",
                Subtitle = "Automatic timesheet from shift schedules",
                CtaText = "Create Timesheet",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>What is a Timesheet?</h2>
<p>A timesheet shows monthly working hours. It's the basis for payroll calculation.</p>

<h2>Calculated Values</h2>
<ul>
    <li><strong>Regular Work:</strong> Standard hours</li>
    <li><strong>Night Work:</strong> 8 PM - 6 AM</li>
    <li><strong>Weekend:</strong> Saturday/Sunday</li>
    <li><strong>Overtime:</strong> Daily or monthly</li>
</ul>

<h2>Calculation Modes</h2>
<p><strong>Daily Mode:</strong> Overtime per day.</p>
<p><strong>Monthly Mode:</strong> Total hours at month end.</p>
</div>",
                DisplayOrder = 4,
                IsPublished = true
            },
            new Data.Entities.ContentPage
            {
                Slug = "overtime-calculation",
                Language = "en",
                Title = "Overtime Calculation System",
                MetaDescription = "Daily and monthly overtime calculation. Automatic tracking and reporting.",
                MetaKeywords = "overtime calculation, overtime tracking, extra hours",
                Subtitle = "Automatic overtime calculation",
                CtaText = "Start Calculating",
                CtaUrl = "/app",
                PageType = Data.Entities.PageType.Feature,
                Content = @"<div class='feature-content'>
<h2>What is Overtime?</h2>
<p>Work exceeding legal hours. Usually over 40-45 hours per week.</p>

<h2>Calculation Modes</h2>
<h3>📅 Daily</h3>
<p>8 hour target, worked 11 hours = 3 hours overtime.</p>

<h3>📆 Monthly</h3>
<p>176 hour target, worked 184 = 8 hours overtime.</p>

<h2>Legal Limits</h2>
<ul>
    <li>Daily: Max 3 hours</li>
    <li>Annual: Max 270 hours</li>
    <li>Pay: 50% premium</li>
</ul>
</div>",
                DisplayOrder = 5,
                IsPublished = true
            }
        };
    }

    /// <summary>
    /// Dynamic robots.txt
    /// </summary>
    [Route("robots.txt")]
    public ContentResult RobotsTxt()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var robotsTxt = $@"User-agent: *
Allow: /
Allow: /blog/
Allow: /rehber/
Allow: /guide/

Disallow: /app/
Disallow: /admin/
Disallow: /Account/
Disallow: /api/

Sitemap: {baseUrl}/sitemap.xml
";
        return Content(robotsTxt, "text/plain");
    }

    /// <summary>
    /// Dynamic sitemap.xml - automatically generated from ContentPages and blog posts
    /// </summary>
    [Route("sitemap.xml")]
    public async Task<ContentResult> SitemapXml()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Get all published content pages from database
        var contentPages = await _context.ContentPages
            .Where(p => p.IsPublished)
            .OrderBy(p => p.Language)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync();

        // Build sitemap URLs
        var sitemapUrls = new List<string>
        {
            // Homepage
            $"  <url><loc>{baseUrl}/</loc><lastmod>{currentDate}</lastmod><changefreq>daily</changefreq><priority>1.0</priority></url>",
            
            // App page
            $"  <url><loc>{baseUrl}/app</loc><lastmod>{currentDate}</lastmod><changefreq>weekly</changefreq><priority>0.9</priority></url>",
            
            // Blog index
            $"  <url><loc>{baseUrl}/blog</loc><lastmod>{currentDate}</lastmod><changefreq>daily</changefreq><priority>0.8</priority></url>"
        };

        // Add content pages (rehber/guide pages)
        foreach (var page in contentPages)
        {
            var path = page.Language == "tr" ? $"/rehber/{page.Slug}" : $"/guide/{page.Slug}";
            var lastmod = page.UpdatedAt.ToString("yyyy-MM-dd");
            sitemapUrls.Add($"  <url><loc>{baseUrl}{path}</loc><lastmod>{lastmod}</lastmod><changefreq>weekly</changefreq><priority>0.7</priority></url>");
        }

        // Add blog posts (automatically from BlogController)
        // Blog posts are static in BlogController, automatically retrieved via AllSlugs property
        var blogSlugs = Nobetci.Web.Controllers.BlogController.AllSlugs;

        foreach (var slug in blogSlugs)
        {
            sitemapUrls.Add($"  <url><loc>{baseUrl}/blog/{slug}</loc><lastmod>{currentDate}</lastmod><changefreq>monthly</changefreq><priority>0.6</priority></url>");
        }

        // Build XML
        var sitemapXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
{string.Join("\n", sitemapUrls)}
</urlset>";

        return Content(sitemapXml, "application/xml");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private string GetCurrentLanguage()
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return culture == "tr" ? "tr" : "en";
    }
}
