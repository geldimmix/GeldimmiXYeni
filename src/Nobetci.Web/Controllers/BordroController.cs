using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Services;
using System.Security.Claims;
using ClosedXML.Excel;
using System.Globalization;

namespace Nobetci.Web.Controllers;

public class BordroController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBordroHesaplamaService _bordroHesaplamaService;
    private readonly ILogger<BordroController> _logger;
    private const string AdminTc = "29467790262";

    public BordroController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IBordroHesaplamaService bordroHesaplamaService, ILogger<BordroController> logger)
    {
        _context = context;
        _userManager = userManager;
        _bordroHesaplamaService = bordroHesaplamaService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Sabitler(int? editId)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        await _bordroHesaplamaService.EnsureBordroSabitleriAsync(organization.Id);
        try
        {
            await _bordroHesaplamaService.EnsureDefaultUnitTypesAsync(organization.Id);
            await _bordroHesaplamaService.EnsureDefaultUnitAsync(organization.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bordro Sabitler: unit init failed for OrgId={OrgId}, user can still edit sabitleri", organization.Id);
        }

        var sabitler = await _context.BordroSabitleri
            .Where(s => s.OrganizationId == organization.Id)
            .OrderByDescending(s => s.ValidFrom)
            .ThenBy(s => s.Key)
            .ToListAsync();

        var unitTypes = await _context.UnitTypes
            .Where(ut => ut.OrganizationId == organization.Id)
            .OrderBy(ut => ut.SortOrder)
            .ToListAsync();

        var sabitTemplates = await _context.BordroSabitleriTemplates
            .Where(t => t.IsActive)
            .ToListAsync();

        var unitTypeTemplates = await _context.UnitTypeTemplates
            .Where(t => t.IsActive)
            .ToListAsync();

        var viewModel = new BordroSabitlerViewModel
        {
            Sabitler = sabitler,
            UnitTypes = unitTypes,
            SabitTemplateUpdateCount = CountPendingSabitTemplateUpdates(sabitler, sabitTemplates),
            UnitTypeTemplateUpdateCount = CountPendingUnitTypeTemplateUpdates(unitTypes, unitTypeTemplates),
            SabitTemplateUpdateDetails = GetPendingSabitTemplateUpdateDetails(sabitler, sabitTemplates),
            UnitTypeTemplateUpdateDetails = GetPendingUnitTypeTemplateUpdateDetails(unitTypes, unitTypeTemplates)
        };
        viewModel.HasSabitTemplateUpdates = viewModel.SabitTemplateUpdateCount > 0;
        viewModel.HasUnitTypeTemplateUpdates = viewModel.UnitTypeTemplateUpdateCount > 0;

        if (editId.HasValue)
        {
            var editItem = sabitler.FirstOrDefault(s => s.Id == editId.Value);
            if (editItem != null)
            {
                viewModel.NewSabit = new BordroSabitInputModel
                {
                    Id = editItem.Id,
                    Key = editItem.Key,
                    Value = editItem.Value,
                    ValueType = editItem.ValueType,
                    Description = editItem.Description,
                    CadreType = editItem.CadreType,
                    ValidFrom = editItem.ValidFrom,
                    ValidTo = editItem.ValidTo,
                    IsActive = editItem.IsActive,
                    WorkingUnitIds = editItem.WorkingUnitIds
                };
            }
        }

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> YetkiliYonetimi()
    {
        if (!IsBordroAdmin())
            return Unauthorized("Bu sayfaya erişim yetkiniz bulunmamaktadır");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        try
        {
            await _bordroHesaplamaService.EnsureBordroSabitleriAsync(organization.Id);
            await _bordroHesaplamaService.EnsureDefaultUnitTypesAsync(organization.Id);
            await _bordroHesaplamaService.EnsureDefaultUnitAsync(organization.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yetkili Yonetimi: ensure defaults failed for OrgId={OrgId}", organization.Id);
        }

        var yetkiler = await _context.BordroYetkileri
            .Where(y => y.OrganizationId == organization.Id && y.IsActive)
            .Include(y => y.Unit)
            .OrderBy(y => y.Unit.Name)
            .ThenBy(y => y.TcKimlik)
            .ToListAsync();

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .ToListAsync();

        ViewBag.Units = await _context.Units
            .Where(u => u.OrganizationId == organization.Id && u.IsActive)
            .OrderBy(u => u.SortOrder)
            .ToListAsync();

        ViewBag.EmployeeLookup = employees.ToDictionary(e => e.IdentityNo ?? string.Empty, e => new { e.FullName, e.Title });

        return View(yetkiler);
    }

    [HttpPost]
    public async Task<IActionResult> YetkiliEkle(int unitId, string tcKimlik, string kadroTipiYetkisi)
    {
        if (!IsBordroAdmin())
            return Json(new { success = false, message = "Bu işlem için yetkiniz bulunmamaktadır" });

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return Json(new { success = false, message = "Organizasyon bulunamadı" });

        if (string.IsNullOrWhiteSpace(tcKimlik) || unitId <= 0)
            return Json(new { success = false, message = "Gerekli alanları doldurunuz" });

        var existing = await _context.BordroYetkileri
            .FirstOrDefaultAsync(y => y.OrganizationId == organization.Id && y.UnitId == unitId && y.TcKimlik == tcKimlik && y.KadroTipiYetkisi == kadroTipiYetkisi);

        if (existing != null)
            return Json(new { success = false, message = "Yetki zaten mevcut" });

        _context.BordroYetkileri.Add(new BordroYetkileri
        {
            OrganizationId = organization.Id,
            UnitId = unitId,
            TcKimlik = tcKimlik,
            KadroTipiYetkisi = kadroTipiYetkisi,
            CreatedBy = _userManager.GetUserId(User),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Yetki eklendi" });
    }

    [HttpPost]
    public async Task<IActionResult> YetkiliSil(int id)
    {
        if (!IsBordroAdmin())
            return Json(new { success = false, message = "Bu işlem için yetkiniz bulunmamaktadır" });

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return Json(new { success = false, message = "Organizasyon bulunamadı" });

        var yetki = await _context.BordroYetkileri
            .FirstOrDefaultAsync(y => y.Id == id && y.OrganizationId == organization.Id);

        if (yetki == null)
            return Json(new { success = false, message = "Yetki bulunamadı" });

        yetki.IsActive = false;
        yetki.UpdatedBy = _userManager.GetUserId(User);
        yetki.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Yetki kaldırıldı" });
    }

    [HttpGet]
    public IActionResult PersonelPuanImport()
    {
        if (!IsBordroAdmin())
            return Unauthorized("Bu sayfaya erişim yetkiniz bulunmamaktadır");

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> PersonelPuanYonetimi(int? editId)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        if (!await BordroYetkisiKontrol(organization.Id) && !IsBordroAdmin())
            return Unauthorized("Bu sayfaya erişim yetkiniz bulunmamaktadır");

        var personeller = await _context.PersonelNobetPuan
            .Where(p => p.OrganizationId == organization.Id)
            .OrderBy(p => p.AdiSoyadi)
            .ToListAsync();

        var viewModel = new PersonelPuanYonetimViewModel
        {
            Personeller = personeller
        };

        if (editId.HasValue)
        {
            var editItem = personeller.FirstOrDefault(p => p.Id == editId.Value);
            if (editItem != null)
            {
                viewModel.NewPersonel = new PersonelPuanInputModel
                {
                    Id = editItem.Id,
                    TcKimlik = editItem.TcKimlik,
                    AdiSoyadi = editItem.AdiSoyadi ?? string.Empty,
                    Unvan = editItem.Unvan,
                    Mezuniyet = editItem.Mezuniyet,
                    YPuan = editItem.YPuan,
                    NormalSaatUcreti = editItem.NormalSaatUcreti,
                    YogunBakimSaatUcreti = editItem.YogunBakimSaatUcreti,
                    IcapSaatUcreti = editItem.IcapSaatUcreti,
                    Iban = editItem.Iban,
                    OncekiSoyadi = editItem.OncekiSoyadi,
                    Description = editItem.Description,
                    IsActive = editItem.IsActive
                };
            }
        }

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> PersonelPuanImportExcel(IFormFile excelFile)
    {
        if (!IsBordroAdmin())
            return Json(new { success = false, message = "Bu işlem için yetkiniz bulunmamaktadır" });

        if (excelFile == null || excelFile.Length == 0)
            return Json(new { success = false, message = "Lütfen geçerli bir Excel dosyası seçin" });

        try
        {
            var organization = await GetOrganizationAsync();
            if (organization == null)
                return Json(new { success = false, message = "Organizasyon bulunamadı" });

            var result = await ProcessExcelImport(excelFile, organization.Id);
            return Json(new
            {
                success = true,
                message = $"Import işlemi tamamlandı. {result.EklenenSayisi} yeni kayıt eklendi, {result.GuncellenenSayisi} kayıt güncellendi.",
                detay = result
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Import hatası: {ex.Message}" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PersonelPuanKaydet(PersonelPuanInputModel model)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        if (!await BordroYetkisiKontrol(organization.Id) && !IsBordroAdmin())
            return Unauthorized("Bu işlem için yetkiniz bulunmamaktadır");

        if (!ModelState.IsValid)
            return RedirectToAction(nameof(PersonelPuanYonetimi));

        if (string.IsNullOrWhiteSpace(model.TcKimlik) || model.TcKimlik.Length != 11 || !model.TcKimlik.All(char.IsDigit))
            return RedirectToAction(nameof(PersonelPuanYonetimi));

        var userId = _userManager.GetUserId(User);

        var portalPersonel = await GetPersonelFromBirimPortal(model.TcKimlik);
        if (portalPersonel != null)
        {
            if (string.IsNullOrWhiteSpace(model.AdiSoyadi))
                model.AdiSoyadi = portalPersonel.FullName;
            if (string.IsNullOrWhiteSpace(model.Unvan))
                model.Unvan = portalPersonel.Title;
        }

        var otomatikDegerler = HesaplaOtomatikDegerler(model.Mezuniyet, model.Unvan ?? string.Empty);

        if (model.Id.HasValue)
        {
            var existing = await _context.PersonelNobetPuan
                .FirstOrDefaultAsync(p => p.Id == model.Id.Value && p.OrganizationId == organization.Id);
            if (existing == null)
                return NotFound();

            existing.TcKimlik = model.TcKimlik.Trim();
            existing.AdiSoyadi = model.AdiSoyadi.Trim();
            existing.Unvan = model.Unvan;
            existing.Mezuniyet = model.Mezuniyet;
            existing.YPuan = otomatikDegerler.puan;
            existing.NormalSaatUcreti = otomatikDegerler.normalUcret;
            existing.YogunBakimSaatUcreti = otomatikDegerler.ybUcret;
            existing.IcapSaatUcreti = otomatikDegerler.icapUcret;
            existing.Iban = model.Iban;
            existing.OncekiSoyadi = model.OncekiSoyadi;
            existing.Description = model.Description;
            existing.IsActive = model.IsActive;
            existing.UpdatedBy = userId;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var existing = await _context.PersonelNobetPuan
                .FirstOrDefaultAsync(p => p.OrganizationId == organization.Id && p.TcKimlik == model.TcKimlik);
            if (existing != null)
                return RedirectToAction(nameof(PersonelPuanYonetimi), new { editId = existing.Id });

            _context.PersonelNobetPuan.Add(new PersonelNobetPuan
            {
                OrganizationId = organization.Id,
                TcKimlik = model.TcKimlik.Trim(),
                AdiSoyadi = model.AdiSoyadi.Trim(),
                Unvan = model.Unvan,
                Mezuniyet = model.Mezuniyet,
                YPuan = otomatikDegerler.puan,
                NormalSaatUcreti = otomatikDegerler.normalUcret,
                YogunBakimSaatUcreti = otomatikDegerler.ybUcret,
                IcapSaatUcreti = otomatikDegerler.icapUcret,
                Iban = model.Iban,
                OncekiSoyadi = model.OncekiSoyadi,
                Description = model.Description,
                IsActive = model.IsActive,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(PersonelPuanYonetimi));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PersonelPuanPasiflestir(int id)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        if (!await BordroYetkisiKontrol(organization.Id) && !IsBordroAdmin())
            return Unauthorized("Bu işlem için yetkiniz bulunmamaktadır");

        var existing = await _context.PersonelNobetPuan
            .FirstOrDefaultAsync(p => p.OrganizationId == organization.Id && p.Id == id);
        if (existing == null)
            return NotFound();

        existing.IsActive = false;
        existing.UpdatedBy = _userManager.GetUserId(User);
        existing.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(PersonelPuanYonetimi));
    }

    [HttpGet]
    public async Task<IActionResult> GetPersonelBordroDetay(string personelTc, int yil, int ay)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(personelTc))
            return BadRequest("TC Kimlik gerekli");

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.OrganizationId == organization.Id && e.IdentityNo == personelTc);
        if (employee == null)
            return NotFound();

        var kadroTipi = employee.PositionType ?? string.Empty;
        var bordro4A = kadroTipi.Equals("4A", StringComparison.OrdinalIgnoreCase)
            ? await _bordroHesaplamaService.Get4ABordroDetay(personelTc, yil, ay)
            : null;
        var bordro4B = kadroTipi.Equals("4B", StringComparison.OrdinalIgnoreCase)
            ? await _bordroHesaplamaService.Get4BBordroDetay(personelTc, yil, ay)
            : null;

        return Json(new
        {
            success = true,
            kadroTipi,
            employee = new { employee.Id, employee.FullName, employee.Title, employee.IdentityNo },
            bordro4A,
            bordro4B
        });
    }

    [HttpGet]
    public async Task<IActionResult> PersonelAra(string? q)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        var query = _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e =>
                e.IdentityNo.Contains(term) ||
                e.FullName.Contains(term));
        }

        var results = await query
            .OrderBy(e => e.FullName)
            .Select(e => new
            {
                e.IdentityNo,
                e.FullName,
                e.Title,
                e.PositionType,
                e.UnitId
            })
            .Take(30)
            .ToListAsync();

        return Json(new { success = true, data = results });
    }

    [HttpGet]
    public async Task<IActionResult> TekPersonelBordroHesapla(string tcKimlik, int yil, int ay, bool yenidenHesapla = false)
    {
        if (!IsBordroAdmin())
            return Unauthorized("Bu işlem için yetkiniz bulunmamaktadır");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return Json(new { success = false, message = "Organizasyon bulunamadı" });

        if (string.IsNullOrWhiteSpace(tcKimlik) || tcKimlik.Length != 11)
            return Json(new { success = false, message = "Geçersiz TC Kimlik No" });

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.OrganizationId == organization.Id && e.IdentityNo == tcKimlik);
        if (employee == null)
            return Json(new { success = false, message = "Personel bulunamadı" });

        var kadroTipi = employee.PositionType ?? string.Empty;
        var bordro4A = await _bordroHesaplamaService.Hesapla4ABordro(tcKimlik, yil, ay, yenidenHesapla);
        var bordro4B = await _bordroHesaplamaService.Hesapla4BBordro(tcKimlik, yil, ay, yenidenHesapla);

        if (kadroTipi.Equals("4A", StringComparison.OrdinalIgnoreCase) || (!kadroTipi.Equals("4B", StringComparison.OrdinalIgnoreCase) && bordro4A != null))
        {
            if (bordro4A == null)
                return Json(new { success = false, message = "Bordro hesaplanamadı" });

            var summary = bordro4A;
            return Json(new
            {
                success = true,
                message = yenidenHesapla ? "4A bordrosu yeniden hesaplandı" : "4A bordrosu hesaplandı",
                data = new
                {
                    personelAd = employee.FullName,
                    kadroTipi = "4A",
                    donem = $"{GetAyAdi(ay)} {yil}",
                    brutToplam = $"{summary.GenelToplamTutar:N2} ₺",
                    damgaVergisi = $"{summary.DamgaVergisi:N2} ₺",
                    eleGecenToplam = $"{summary.EleGecenToplam:N2} ₺",
                    normalNobet = $"{summary.NormalServisNobetSaati + summary.YogunBakimNobetSaati:0.##} saat - {summary.NormalServisNobetToplamTutar + summary.YogunBakimNobetToplamTutar:N2} ₺",
                    bayramNobet = $"{summary.NormalServisBayramSaati + summary.YogunBakimBayramSaati:0.##} saat - {summary.NormalServisBayramToplamTutar + summary.YogunBakimBayramToplamTutar:N2} ₺",
                    bayramFarki = $"{summary.BayramFarkiNobetSaati:0.##} saat - {summary.BayramFarkiToplamTutar:N2} ₺",
                    yogunBakim = summary.YogunBakimVar ? "Evet" : "Hayır",
                    saatUcreti = $"{summary.SaatUcreti:N2} ₺",
                    nobetPuani = summary.NobetPuani
                }
            });
        }

        if (kadroTipi.Equals("4B", StringComparison.OrdinalIgnoreCase) || bordro4B != null)
        {
            if (bordro4B == null)
                return Json(new { success = false, message = "Bordro hesaplanamadı" });

            var summary = bordro4B;
            return Json(new
            {
                success = true,
                message = yenidenHesapla ? "4B bordrosu yeniden hesaplandı" : "4B bordrosu hesaplandı",
                data = new
                {
                    personelAd = employee.FullName,
                    kadroTipi = "4B",
                    donem = $"{GetAyAdi(ay)} {yil}",
                    brutToplam = $"{summary.GenelToplamTutarPek:N2} ₺",
                    gelirToplami = $"{summary.GelirToplami:N2} ₺",
                    damgaVergisi = $"{summary.DamgaVergisi:N2} ₺",
                    kesintiToplami = $"{summary.KesintiToplami:N2} ₺",
                    eleGecenToplam = $"{summary.EleGecenToplam:N2} ₺",
                    normalNobet = $"{summary.NormalServisNobetSaati + summary.YogunBakimNobetSaati:0.##} saat - {summary.NormalServisNobetToplamTutar + summary.YogunBakimNobetToplamTutar:N2} ₺",
                    bayramNobet = $"{summary.NormalServisBayramSaati + summary.YogunBakimBayramSaati:0.##} saat - {summary.NormalServisBayramToplamTutar + summary.YogunBakimBayramToplamTutar:N2} ₺",
                    bayramFarki = $"{summary.BayramFarkiNobetSaati:0.##} saat - {summary.BayramFarkiToplamTutar:N2} ₺",
                    yogunBakim = summary.YogunBakimVar ? "Evet" : "Hayır",
                    saatUcreti = $"{summary.SaatUcreti:N2} ₺",
                    nobetPuani = summary.NobetPuani,
                    sgkDetay = new
                    {
                        maluliyetDev = $"{summary.MaluliyetYaslilikEmeklilikDev:N2} ₺",
                        gssDev = $"{summary.GssDev:N2} ₺",
                        kisaVadSig = $"{summary.KisaVadSigKolPrim:N2} ₺",
                        maluliyetKisi = $"{summary.MaluliyetYaslilikEmeklilikKisi:N2} ₺",
                        gssKisi = $"{summary.GssKisi:N2} ₺"
                    }
                }
            });
        }

        return Json(new { success = false, message = "Bordro hesaplanamadı" });
    }

    [HttpPost]
    [Route("Bordro/Api/HesaplaBordro")]
    public async Task<IActionResult> ApiHesaplaBordro([FromBody] TekPersonelBordroRequestModel request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TcKimlik) || request.TcKimlik.Length != 11)
            return Json(new { success = false, message = "Geçersiz TC Kimlik No" });

        if (request.Yil < 2020 || request.Yil > DateTime.Now.Year + 1)
            return Json(new { success = false, message = "Geçersiz yıl" });

        if (request.Ay < 1 || request.Ay > 12)
            return Json(new { success = false, message = "Geçersiz ay (1-12 arası olmalı)" });

        return await TekPersonelBordroHesapla(request.TcKimlik, request.Yil, request.Ay, request.YenidenHesapla);
    }

    /// <summary>
    /// Harici API: Tek personel bordro sorgulama (hesaplama yapmadan mevcut kaydı getir)
    /// GET /Bordro/Api/GetBordro?tcKimlik=xxx&yil=2025&ay=11
    /// </summary>
    [HttpGet]
    [Route("Bordro/Api/GetBordro")]
    public async Task<IActionResult> ApiGetBordro(string tcKimlik, int yil, int ay)
    {
        try
        {
            if (string.IsNullOrEmpty(tcKimlik) || tcKimlik.Length != 11)
                return Json(new { success = false, message = "Geçersiz TC Kimlik No" });

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityNo == tcKimlik);
            if (employee == null)
                return Json(new { success = false, message = "Personel bulunamadı" });

            if (employee.PositionType?.Equals("4A", StringComparison.OrdinalIgnoreCase) == true)
            {
                var bordro4A = await _bordroHesaplamaService.Get4ABordroDetay(tcKimlik, yil, ay);
                if (bordro4A == null)
                    return Json(new { success = false, message = "4A bordro kaydı bulunamadı. Önce hesaplama yapılmalı." });
                return Json(new { success = true, kadroTipi = "4A", bordro = bordro4A });
            }

            if (employee.PositionType?.Equals("4B", StringComparison.OrdinalIgnoreCase) == true)
            {
                var bordro4B = await _bordroHesaplamaService.Get4BBordroDetay(tcKimlik, yil, ay);
                if (bordro4B == null)
                    return Json(new { success = false, message = "4B bordro kaydı bulunamadı. Önce hesaplama yapılmalı." });
                return Json(new { success = true, kadroTipi = "4B", bordro = bordro4B });
            }

            return Json(new { success = false, message = "Desteklenmeyen kadro tipi" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Hata: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetBordroDetayWithSteps(string personelTc, int yil, int ay)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(personelTc))
            return BadRequest("TC Kimlik gerekli");

        var detail = await _bordroHesaplamaService.GetBordroDetayWithSteps(personelTc, yil, ay);
        if (detail.Bordro4A == null && detail.Bordro4B == null)
            return Json(new { success = false, message = "Bordro kaydı bulunamadı" });

        return Json(new
        {
            success = true,
            kadroTipi = detail.Bordro4A != null ? "4A" : "4B",
            bordro = detail.Bordro4A ?? (object?)detail.Bordro4B,
            steps = detail.Steps
        });
    }

    [HttpGet]
    public async Task<IActionResult> TopluBordroHesaplama()
    {
        if (!IsBordroAdmin())
            return Unauthorized("Bu sayfaya erişim yetkiniz bulunmamaktadır");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        var months = new List<object>();
        var now = DateTime.Now;
        for (int i = 0; i < 3; i++)
        {
            var date = now.AddMonths(-i);
            var has4A = await _context.BordroResults4A
                .AnyAsync(b => b.OrganizationId == organization.Id && b.Year == date.Year && b.Month == date.Month);
            var has4B = await _context.BordroResults4B
                .AnyAsync(b => b.OrganizationId == organization.Id && b.Year == date.Year && b.Month == date.Month);

            months.Add(new
            {
                Yil = date.Year,
                Ay = date.Month,
                AyAdi = GetAyAdi(date.Month),
                PuantajVarMi = has4A || has4B,
                DonemText = $"{GetAyAdi(date.Month)} {date.Year}"
            });
        }

        ViewBag.UygunDonemler = months;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> TopluBordroHesaplamaBaslat(int yil, int ay, bool yenidenHesapla = false)
    {
        if (!IsBordroAdmin())
            return Json(new { success = false, message = "Bu işlem için yetkiniz bulunmamaktadır" });

        try
        {
            var sonuc = await TopluBordroHesapla(yil, ay, yenidenHesapla);
            return Json(new { success = true, message = "Toplu bordro hesaplama tamamlandı!", detay = sonuc });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Hesaplama hatası: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> BirimBordroHesaplama(int birimId, int yil, int ay)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        if (!await BordroYetkisiKontrol(organization.Id) && !IsBordroAdmin())
            return Unauthorized("Bu sayfaya erişim yetkiniz bulunmamaktadır");

        var unit = await _context.Units.FirstOrDefaultAsync(u => u.OrganizationId == organization.Id && u.Id == birimId);
        if (unit == null)
            return NotFound();

        var sonuc = await _bordroHesaplamaService.HesaplaBirimBordrolari(
            birimId,
            yil,
            ay,
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? AdminTc,
            false);

        return Json(new { success = true, message = "Birim bordro hesaplama tamamlandı", detay = sonuc });
    }

    private async Task<TopluBordroHesaplamaSonucu> TopluBordroHesapla(int yil, int ay, bool yenidenHesapla)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            throw new Exception("Organizasyon bulunamadı");

        var sonuc = new TopluBordroHesaplamaSonucu
        {
            Yil = yil,
            Ay = ay,
            AyAdi = GetAyAdi(ay),
            BaslangicZamani = DateTime.Now
        };

        var unitIds = await _context.Units
            .Where(u => u.OrganizationId == organization.Id && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var unitId in unitIds)
        {
            try
            {
                var birimSonuc = await _bordroHesaplamaService.HesaplaBirimBordrolari(
                    unitId,
                    yil,
                    ay,
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? AdminTc,
                    yenidenHesapla);

                sonuc.Hesaplanan4APersonelSayisi += birimSonuc.Bordro4APersonelSayisi;
                sonuc.Hesaplanan4BPersonelSayisi += birimSonuc.Bordro4BPersonelSayisi;
                sonuc.Toplam4ATutar += birimSonuc.Bordro4AToplamTutar;
                sonuc.Toplam4BTutar += birimSonuc.Bordro4BToplamTutar;

                var birim4A = await _bordroHesaplamaService.GetBirim4ABordrolari(unitId, yil, ay);
                foreach (var p in birim4A)
                    sonuc.Hesaplanan4APersoneller.Add($"{p.EmployeeName} - {p.EleGecenToplam:N2} ₺");

                var birim4B = await _bordroHesaplamaService.GetBirim4BBordrolari(unitId, yil, ay);
                foreach (var p in birim4B)
                    sonuc.Hesaplanan4BPersoneller.Add($"{p.EmployeeName} - {p.EleGecenToplam:N2} ₺");
            }
            catch (Exception ex)
            {
                sonuc.HataliPersoneller.Add($"Birim {unitId}: {ex.Message}");
            }
        }

        sonuc.ToplamHesaplananPersonel = sonuc.Hesaplanan4APersonelSayisi + sonuc.Hesaplanan4BPersonelSayisi;
        sonuc.ToplamBordroTutari = sonuc.Toplam4ATutar + sonuc.Toplam4BTutar;
        sonuc.BitisZamani = DateTime.Now;
        sonuc.IslemSuresi = sonuc.BitisZamani - sonuc.BaslangicZamani;

        return sonuc;
    }

    private async Task<object> GetBirimBordroOzet(int organizationId, int unitId, int yil, int ay)
    {
        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organizationId && e.UnitId == unitId && e.IsActive)
            .Select(e => e.Id)
            .ToListAsync();

        var results4A = await _context.BordroResults4A
            .Where(b => b.OrganizationId == organizationId && b.Year == yil && b.Month == ay && employees.Contains(b.EmployeeId))
            .ToListAsync();
        var results4B = await _context.BordroResults4B
            .Where(b => b.OrganizationId == organizationId && b.Year == yil && b.Month == ay && employees.Contains(b.EmployeeId))
            .ToListAsync();

        return new
        {
            Bordro4APersonelSayisi = results4A.Select(r => r.EmployeeId).Distinct().Count(),
            Bordro4BPersonelSayisi = results4B.Select(r => r.EmployeeId).Distinct().Count(),
            Bordro4AToplamTutar = results4A.Sum(r => r.EleGecenToplam),
            Bordro4BToplamTutar = results4B.Sum(r => r.EleGecenToplam)
        };
    }

    private async Task<bool> BordroYetkisiKontrol(int organizationId)
    {
        var tcKimlik = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(tcKimlik))
            return false;

        return await _context.BordroYetkileri.AnyAsync(y =>
            y.OrganizationId == organizationId && y.TcKimlik == tcKimlik && y.IsActive);
    }

    private async Task<Employee?> GetPersonelFromBirimPortal(string tcKimlik)
    {
        return await _context.Employees.FirstOrDefaultAsync(e => e.IdentityNo == tcKimlik && e.IsActive);
    }

    private static (int puan, decimal normalUcret, decimal ybUcret, decimal icapUcret) HesaplaOtomatikDegerler(string mezuniyet, string unvan)
    {
        if (!string.IsNullOrEmpty(unvan) && unvan.ToUpper().Contains("ECZACI") && mezuniyet == "Lisans")
        {
            return (130, 131.63m, 197.45m, 52.65m);
        }

        return mezuniyet switch
        {
            "SH Dışı" or "Vhki" or "Diğer" => (65, 65.82m, 98.72m, 26.33m),
            "Ortaöğretim" => (85, 86.07m, 129.10m, 34.43m),
            "Önlisans" or "Lisans" or "Yüksek Lisans" or "Doktora" => (100, 101.26m, 151.88m, 40.50m),
            _ => (100, 101.26m, 151.88m, 40.50m)
        };
    }

    private static string GetAyAdi(int ay)
    {
        return CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(ay);
    }

    private static BordroResult4A Aggregate4A(List<BordroResult4A> items)
    {
        return new BordroResult4A
        {
            NobetPuani = items.Max(i => i.NobetPuani),
            SaatUcreti = items.Max(i => i.SaatUcreti),
            YogunBakimVar = items.Any(i => i.YogunBakimVar),
            NormalServisNobetSaati = items.Sum(i => i.NormalServisNobetSaati),
            YogunBakimNobetSaati = items.Sum(i => i.YogunBakimNobetSaati),
            NormalServisBayramSaati = items.Sum(i => i.NormalServisBayramSaati),
            YogunBakimBayramSaati = items.Sum(i => i.YogunBakimBayramSaati),
            BayramFarkiNobetSaati = items.Sum(i => i.BayramFarkiNobetSaati),
            NormalServisNobetToplamTutar = items.Sum(i => i.NormalServisNobetToplamTutar),
            YogunBakimNobetToplamTutar = items.Sum(i => i.YogunBakimNobetToplamTutar),
            NormalServisBayramToplamTutar = items.Sum(i => i.NormalServisBayramToplamTutar),
            YogunBakimBayramToplamTutar = items.Sum(i => i.YogunBakimBayramToplamTutar),
            BayramFarkiToplamTutar = items.Sum(i => i.BayramFarkiToplamTutar),
            GenelToplamTutar = items.Sum(i => i.GenelToplamTutar),
            DamgaVergisi = items.Sum(i => i.DamgaVergisi),
            EleGecenToplam = items.Sum(i => i.EleGecenToplam)
        };
    }

    private static BordroResult4B Aggregate4B(List<BordroResult4B> items)
    {
        return new BordroResult4B
        {
            NobetPuani = items.Max(i => i.NobetPuani),
            SaatUcreti = items.Max(i => i.SaatUcreti),
            YogunBakimVar = items.Any(i => i.YogunBakimVar),
            NormalServisNobetSaati = items.Sum(i => i.NormalServisNobetSaati),
            YogunBakimNobetSaati = items.Sum(i => i.YogunBakimNobetSaati),
            NormalServisBayramSaati = items.Sum(i => i.NormalServisBayramSaati),
            YogunBakimBayramSaati = items.Sum(i => i.YogunBakimBayramSaati),
            BayramFarkiNobetSaati = items.Sum(i => i.BayramFarkiNobetSaati),
            NormalServisNobetToplamTutar = items.Sum(i => i.NormalServisNobetToplamTutar),
            YogunBakimNobetToplamTutar = items.Sum(i => i.YogunBakimNobetToplamTutar),
            NormalServisBayramToplamTutar = items.Sum(i => i.NormalServisBayramToplamTutar),
            YogunBakimBayramToplamTutar = items.Sum(i => i.YogunBakimBayramToplamTutar),
            BayramFarkiToplamTutar = items.Sum(i => i.BayramFarkiToplamTutar),
            GenelToplamTutarPek = items.Sum(i => i.GenelToplamTutarPek),
            MaluliyetYaslilikEmeklilikDev = items.Sum(i => i.MaluliyetYaslilikEmeklilikDev),
            GssDev = items.Sum(i => i.GssDev),
            KisaVadSigKolPrim = items.Sum(i => i.KisaVadSigKolPrim),
            GelirToplami = items.Sum(i => i.GelirToplami),
            DamgaVergisi = items.Sum(i => i.DamgaVergisi),
            MaluliyetYaslilikEmeklilikDevKesinti = items.Sum(i => i.MaluliyetYaslilikEmeklilikDevKesinti),
            GssDevKesinti = items.Sum(i => i.GssDevKesinti),
            KisaVadSigKolPrimKesinti = items.Sum(i => i.KisaVadSigKolPrimKesinti),
            MaluliyetYaslilikEmeklilikKisi = items.Sum(i => i.MaluliyetYaslilikEmeklilikKisi),
            GssKisi = items.Sum(i => i.GssKisi),
            KesintiToplami = items.Sum(i => i.KesintiToplami),
            EleGecenToplam = items.Sum(i => i.EleGecenToplam)
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SabitKaydet(BordroSabitInputModel model)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Key))
            return RedirectToAction(nameof(Sabitler));

        var userId = _userManager.GetUserId(User);
        var normalizedCadre = string.IsNullOrWhiteSpace(model.CadreType) ? null : model.CadreType;
        var template = await _context.BordroSabitleriTemplates
            .FirstOrDefaultAsync(t => t.Key == model.Key.Trim() && t.CadreType == normalizedCadre);

        if (model.Id.HasValue)
        {
            var existing = await _context.BordroSabitleri
                .FirstOrDefaultAsync(s => s.Id == model.Id.Value && s.OrganizationId == organization.Id);
            if (existing == null)
                return NotFound();

            _context.BordroSabitleriGecmis.Add(new BordroSabitleriGecmis
            {
                OrganizationId = organization.Id,
                SabitId = existing.Id,
                Key = existing.Key,
                OldValue = existing.Value,
                NewValue = model.Value,
                ValueType = existing.ValueType,
                Description = existing.Description,
                CadreType = existing.CadreType,
                OldValidFrom = existing.ValidFrom,
                OldValidTo = existing.ValidTo,
                NewValidFrom = model.ValidFrom,
                NewValidTo = model.ValidTo,
                OldWorkingUnitIds = existing.WorkingUnitIds,
                NewWorkingUnitIds = model.WorkingUnitIds,
                ActionType = "UPDATE",
                ActionBy = userId
            });

            existing.Key = model.Key.Trim();
            existing.Value = model.Value;
            existing.ValueType = model.ValueType;
            existing.Description = model.Description;
            existing.CadreType = normalizedCadre;
            existing.ValidFrom = model.ValidFrom.Date;
            existing.ValidTo = model.ValidTo?.Date;
            existing.IsActive = model.IsActive;
            existing.WorkingUnitIds = model.WorkingUnitIds;
            if (existing.TemplateId == null && template != null)
                existing.TemplateId = template.Id;
            existing.IsCustom = true;
            existing.UpdatedBy = userId;
            existing.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        }
        else
        {
            var sabit = new BordroSabitleri
            {
                OrganizationId = organization.Id,
                Key = model.Key.Trim(),
                Value = model.Value,
                ValueType = model.ValueType,
                Description = model.Description,
                CadreType = normalizedCadre,
                ValidFrom = model.ValidFrom.Date,
                ValidTo = model.ValidTo?.Date,
                IsActive = model.IsActive,
                WorkingUnitIds = model.WorkingUnitIds,
                TemplateId = template?.Id,
                IsCustom = true,
                CreatedBy = userId,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };

            _context.BordroSabitleri.Add(sabit);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Sabitler));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SabitPasiflestir(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        var existing = await _context.BordroSabitleri
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
        if (existing == null)
            return NotFound();

        existing.IsActive = false;
        existing.UpdatedBy = _userManager.GetUserId(User);
        existing.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Sabitler));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SabitAktiflestir(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        var existing = await _context.BordroSabitleri
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
        if (existing == null)
            return NotFound();

        existing.IsActive = true;
        existing.UpdatedBy = _userManager.GetUserId(User);
        existing.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Sabitler));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnitTypeKatsayiGuncelle(int id, decimal defaultCoefficient)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        var unitType = await _context.UnitTypes
            .FirstOrDefaultAsync(ut => ut.Id == id && ut.OrganizationId == organization.Id);
        if (unitType == null)
            return NotFound();

        unitType.DefaultCoefficient = defaultCoefficient > 0 ? defaultCoefficient : 1.0m;
        unitType.IsCustom = true;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Sabitler));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SabitTemplateSync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        await _bordroHesaplamaService.SyncBordroSabitleriFromTemplatesAsync(organization.Id);
        return RedirectToAction(nameof(Sabitler));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnitTypeTemplateSync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        await SyncUnitTypesFromTemplatesAsync(organization.Id);
        return RedirectToAction(nameof(Sabitler));
    }

    private static int CountPendingSabitTemplateUpdates(
        List<BordroSabitleri> orgItems,
        List<BordroSabitleriTemplate> templates)
    {
        var count = 0;
        foreach (var template in templates)
        {
            var match = orgItems.FirstOrDefault(s => s.TemplateId == template.Id)
                        ?? orgItems.FirstOrDefault(s =>
                            s.TemplateId == null &&
                            string.Equals(s.Key, template.Key, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(s.CadreType ?? string.Empty, template.CadreType ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                count++;
                continue;
            }

            if (!match.IsCustom)
            {
                if (match.Value != template.Value ||
                    !string.Equals(match.ValueType, template.ValueType, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.Description ?? string.Empty, template.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.CadreType ?? string.Empty, template.CadreType ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.WorkingUnitIds ?? string.Empty, template.WorkingUnitIds ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    match.IsActive != template.IsActive)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static List<string> GetPendingSabitTemplateUpdateDetails(
        List<BordroSabitleri> orgItems,
        List<BordroSabitleriTemplate> templates)
    {
        var details = new List<string>();
        foreach (var template in templates)
        {
            var match = orgItems.FirstOrDefault(s => s.TemplateId == template.Id)
                        ?? orgItems.FirstOrDefault(s =>
                            s.TemplateId == null &&
                            string.Equals(s.Key, template.Key, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(s.CadreType ?? string.Empty, template.CadreType ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                details.Add(FormatSabitKey(template.Key, template.CadreType));
                continue;
            }

            if (!match.IsCustom)
            {
                if (match.Value != template.Value ||
                    !string.Equals(match.ValueType, template.ValueType, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.Description ?? string.Empty, template.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.CadreType ?? string.Empty, template.CadreType ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.WorkingUnitIds ?? string.Empty, template.WorkingUnitIds ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    match.IsActive != template.IsActive)
                {
                    details.Add(FormatSabitKey(template.Key, template.CadreType));
                }
            }
        }

        return details;
    }

    private static List<string> GetPendingUnitTypeTemplateUpdateDetails(
        List<UnitType> orgItems,
        List<UnitTypeTemplate> templates)
    {
        var details = new List<string>();
        foreach (var template in templates)
        {
            var match = orgItems.FirstOrDefault(s => s.TemplateId == template.Id)
                        ?? orgItems.FirstOrDefault(s =>
                            s.TemplateId == null &&
                            string.Equals(s.Name, template.Name, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                details.Add(template.Name);
                continue;
            }

            if (!match.IsCustom)
            {
                if (match.DefaultCoefficient != template.DefaultCoefficient ||
                    !string.Equals(match.Name ?? string.Empty, template.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.NameEn ?? string.Empty, template.NameEn ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.Color ?? string.Empty, template.Color ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.Icon ?? string.Empty, template.Icon ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    match.SortOrder != template.SortOrder ||
                    match.IsActive != template.IsActive)
                {
                    details.Add(template.Name);
                }
            }
        }

        return details;
    }

    private static string FormatSabitKey(string key, string? cadreType)
    {
        if (string.IsNullOrWhiteSpace(cadreType))
            return $"{key} (GENEL)";

        return $"{key} ({cadreType})";
    }

    private static int CountPendingUnitTypeTemplateUpdates(
        List<UnitType> orgTypes,
        List<UnitTypeTemplate> templates)
    {
        var count = 0;
        foreach (var template in templates)
        {
            var match = orgTypes.FirstOrDefault(ut => ut.TemplateId == template.Id)
                        ?? orgTypes.FirstOrDefault(ut =>
                            ut.TemplateId == null &&
                            string.Equals(ut.Name, template.Name, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                count++;
                continue;
            }

            if (!match.IsCustom)
            {
                if (!string.Equals(match.Name, template.Name, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.NameEn ?? string.Empty, template.NameEn ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    match.DefaultCoefficient != template.DefaultCoefficient ||
                    !string.Equals(match.Color ?? string.Empty, template.Color ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(match.Icon ?? string.Empty, template.Icon ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    match.SortOrder != template.SortOrder ||
                    match.IsActive != template.IsActive)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private async Task SyncUnitTypesFromTemplatesAsync(int organizationId)
    {
        var templates = await _context.UnitTypeTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        if (!templates.Any())
            return;

        var orgTypes = await _context.UnitTypes
            .Where(ut => ut.OrganizationId == organizationId)
            .ToListAsync();

        var updated = false;
        foreach (var template in templates)
        {
            var match = orgTypes.FirstOrDefault(ut => ut.TemplateId == template.Id)
                        ?? orgTypes.FirstOrDefault(ut =>
                            ut.TemplateId == null &&
                            string.Equals(ut.Name, template.Name, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                _context.UnitTypes.Add(new UnitType
                {
                    OrganizationId = organizationId,
                    Name = template.Name,
                    NameEn = template.NameEn,
                    DefaultCoefficient = template.DefaultCoefficient,
                    Color = template.Color,
                    Icon = template.Icon,
                    SortOrder = template.SortOrder,
                    IsActive = template.IsActive,
                    IsSystem = true,
                    TemplateId = template.Id,
                    IsCustom = false
                });
                updated = true;
                continue;
            }

            if (match.TemplateId == null)
            {
                match.TemplateId = template.Id;
                updated = true;
            }

            if (!match.IsCustom)
            {
                match.Name = template.Name;
                match.NameEn = template.NameEn;
                match.DefaultCoefficient = template.DefaultCoefficient;
                match.Color = template.Color;
                match.Icon = template.Icon;
                match.SortOrder = template.SortOrder;
                match.IsActive = template.IsActive;
                match.IsSystem = true;
                updated = true;
            }
        }

        if (updated)
            await _context.SaveChangesAsync();
    }

    private async Task<Organization?> GetOrganizationAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                return await _context.Organizations
                    .FirstOrDefaultAsync(o => o.UserId == user.Id);
            }
        }

        var sessionId = HttpContext.Session.GetString("GuestSessionId");
        if (!string.IsNullOrEmpty(sessionId))
        {
            return await _context.Organizations
                .FirstOrDefaultAsync(o => o.GuestSessionId == sessionId);
        }

        return null;
    }

    private bool IsBordroAdmin()
    {
        var tcKimlik = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(tcKimlik) && tcKimlik == AdminTc)
            return true;

        return HttpContext.Session.GetString("IsAdmin") == "true";
    }

    private async Task<ExcelImportSonucu> ProcessExcelImport(IFormFile excelFile, int organizationId)
    {
        var sonuc = new ExcelImportSonucu();

        using var stream = new MemoryStream();
        await excelFile.CopyToAsync(stream);

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var rowCount = worksheet.LastRowUsed().RowNumber();

        for (int row = 2; row <= rowCount; row++)
        {
            try
            {
                var tcNo = worksheet.Cell(row, 3).GetString().Trim();
                if (string.IsNullOrEmpty(tcNo) || tcNo.Length != 11)
                {
                    sonuc.HataliSatirlar.Add($"Satır {row}: Geçersiz TC Kimlik No");
                    continue;
                }

                var adSoyad = worksheet.Cell(row, 2).GetString().Trim();
                var unvan = worksheet.Cell(row, 4).GetString().Trim();
                var mezuniyet = worksheet.Cell(row, 5).GetString().Trim();
                var yPuanText = worksheet.Cell(row, 6).GetString().Trim();
                var normalUcretText = worksheet.Cell(row, 7).GetString().Trim();
                var ybUcretText = worksheet.Cell(row, 8).GetString().Trim();
                var icapUcretText = worksheet.Cell(row, 9).GetString().Trim();
                var iban = worksheet.Cell(row, 10).GetString().Trim();
                var oncekiSoyad = worksheet.Cell(row, 11).GetString().Trim();

                if (string.IsNullOrEmpty(adSoyad) || string.IsNullOrEmpty(unvan))
                {
                    sonuc.HataliSatirlar.Add($"Satır {row}: Ad soyad veya ünvan eksik");
                    continue;
                }

                if (!int.TryParse(yPuanText, out int yPuan)) yPuan = 0;
                if (!decimal.TryParse(normalUcretText, out decimal normalUcret)) normalUcret = 0;
                if (!decimal.TryParse(ybUcretText, out decimal ybUcret)) ybUcret = 0;
                if (!decimal.TryParse(icapUcretText, out decimal icapUcret)) icapUcret = 0;

                if (yPuan == 0 || normalUcret == 0 || ybUcret == 0 || icapUcret == 0)
                {
                    var auto = HesaplaOtomatikDegerler(mezuniyet, unvan);
                    yPuan = auto.puan;
                    normalUcret = auto.normalUcret;
                    ybUcret = auto.ybUcret;
                    icapUcret = auto.icapUcret;
                }

                var mevcut = await _context.PersonelNobetPuan
                    .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.TcKimlik == tcNo);

                if (mevcut != null)
                {
                    mevcut.AdiSoyadi = adSoyad;
                    mevcut.Unvan = unvan;
                    mevcut.Mezuniyet = mezuniyet;
                    mevcut.YPuan = yPuan;
                    mevcut.NormalSaatUcreti = normalUcret;
                    mevcut.YogunBakimSaatUcreti = ybUcret;
                    mevcut.IcapSaatUcreti = icapUcret;
                    mevcut.Iban = iban;
                    mevcut.OncekiSoyadi = oncekiSoyad;
                    mevcut.UpdatedBy = _userManager.GetUserId(User);
                    mevcut.UpdatedAt = DateTime.UtcNow;

                    sonuc.GuncellenenSayisi++;
                    sonuc.GuncellenenPersoneller.Add($"{adSoyad} ({tcNo})");
                }
                else
                {
                    _context.PersonelNobetPuan.Add(new PersonelNobetPuan
                    {
                        OrganizationId = organizationId,
                        TcKimlik = tcNo,
                        AdiSoyadi = adSoyad,
                        Unvan = unvan,
                        Mezuniyet = mezuniyet,
                        YPuan = yPuan,
                        NormalSaatUcreti = normalUcret,
                        YogunBakimSaatUcreti = ybUcret,
                        IcapSaatUcreti = icapUcret,
                        Iban = iban,
                        OncekiSoyadi = oncekiSoyad,
                        CreatedBy = _userManager.GetUserId(User),
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    });

                    sonuc.EklenenSayisi++;
                    sonuc.EklenenPersoneller.Add($"{adSoyad} ({tcNo})");
                }
            }
            catch (Exception ex)
            {
                sonuc.HataliSatirlar.Add($"Satır {row}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return sonuc;
    }

    public class ExcelImportSonucu
    {
        public int EklenenSayisi { get; set; }
        public int GuncellenenSayisi { get; set; }
        public List<string> EklenenPersoneller { get; set; } = new();
        public List<string> GuncellenenPersoneller { get; set; } = new();
        public List<string> HataliSatirlar { get; set; } = new();
    }

    public class TopluBordroHesaplamaSonucu
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string AyAdi { get; set; } = string.Empty;
        public DateTime BaslangicZamani { get; set; }
        public DateTime BitisZamani { get; set; }
        public TimeSpan IslemSuresi { get; set; }
        public int Hesaplanan4APersonelSayisi { get; set; }
        public int Hesaplanan4BPersonelSayisi { get; set; }
        public int ToplamHesaplananPersonel { get; set; }
        public decimal Toplam4ATutar { get; set; }
        public decimal Toplam4BTutar { get; set; }
        public decimal ToplamBordroTutari { get; set; }
        public List<string> Hesaplanan4APersoneller { get; set; } = new();
        public List<string> Hesaplanan4BPersoneller { get; set; } = new();
        public List<string> HataliPersoneller { get; set; } = new();
    }
}
