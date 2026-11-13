using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Controllers;

[AllowAnonymous]
public class HesapController : Controller
{
    private const int MaksimumDeneme = 5;
    private static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);

    private readonly UretimDbContext _db;
    private readonly PasswordHasher<Kullanici> _hasher = new();

    public HesapController(UretimDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Giris(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Panel");
        return View(await GirisModeliAsync(new GirisModel { ReturnUrl = returnUrl }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Giris(GirisModel model)
    {
        var kAdi = (model.KullaniciAdi ?? "").Trim().ToLowerInvariant();
        var kullanici = await _db.Kullanicilar.FirstOrDefaultAsync(k => k.KullaniciAdi == kAdi);
        var simdi = DateTime.Now;

        if (kullanici == null || !kullanici.Aktif)
        {
            // Kullanıcı yokken de aynı süre harcansın (kullanıcı adı tahminini zorlaştırır)
            _hasher.VerifyHashedPassword(new Kullanici(), SahteHash, model.Sifre ?? "");
            model.Hata = "Kullanıcı adı veya şifre hatalı.";
            return View(await GirisModeliAsync(model));
        }

        if (kullanici.KilitBitis > simdi)
        {
            var dk = (int)Math.Ceiling((kullanici.KilitBitis.Value - simdi).TotalMinutes);
            model.Hata = $"Çok fazla hatalı deneme. Hesap {dk} dakika kilitli.";
            return View(await GirisModeliAsync(model));
        }

        var sonuc = _hasher.VerifyHashedPassword(kullanici, kullanici.SifreHash, model.Sifre ?? "");
        if (sonuc == PasswordVerificationResult.Failed)
        {
            kullanici.HataliDeneme++;
            if (kullanici.HataliDeneme >= MaksimumDeneme)
            {
                kullanici.KilitBitis = simdi.Add(KilitSuresi);
                kullanici.HataliDeneme = 0;
                model.Hata = $"{MaksimumDeneme} hatalı deneme. Hesap {KilitSuresi.TotalMinutes:0} dakika kilitlendi.";
            }
            else
            {
                model.Hata = $"Kullanıcı adı veya şifre hatalı. Kalan deneme: {MaksimumDeneme - kullanici.HataliDeneme}";
            }
            await _db.SaveChangesAsync();
            return View(await GirisModeliAsync(model));
        }

        kullanici.HataliDeneme = 0;
        kullanici.KilitBitis = null;
        kullanici.SonGiris = simdi;
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, kullanici.Id.ToString()),
            new(ClaimTypes.Name, kullanici.KullaniciAdi),
            new(ClaimTypes.Role, kullanici.Rol),
            new("AdSoyad", kullanici.AdSoyad)
        };
        var kimlik = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(kimlik));

        TempData["Efekt"] = "giris";
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)) return Redirect(model.ReturnUrl);
        return kullanici.Rol == Roller.Operator
            ? RedirectToAction("Index", "Istasyon")
            : RedirectToAction("Index", "Panel");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cikis()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Giris));
    }

    [HttpGet("/hesap/erisim-yok")]
    public IActionResult ErisimYok() => View();

    private async Task<GirisModel> GirisModeliAsync(GirisModel model)
    {
        var bugun = DateTime.Today;
        model.BugunUretilen = await _db.Parcalar.CountAsync(p => p.TamamlanmaTarihi >= bugun);
        model.AktifEmir = await _db.UretimEmirleri.CountAsync(e => e.Durum == EmirDurum.Uretimde || e.Durum == EmirDurum.Bloke);
        model.CalisanMakine = await _db.Makineler.CountAsync(m => m.Durum == MakineDurum.Calisiyor);
        return model;
    }

    private static readonly string SahteHash = new PasswordHasher<Kullanici>().HashPassword(new Kullanici(), "sahte-sifre-zamanlama");
}

public record PanelDuzenIstegi(List<string>? Genis, List<string>? Dar, List<string>? Gizli);

public class PanelController : Controller
{
    private readonly UretimDbContext _db;
    private readonly UyariServisi _uyari;
    private readonly AnalizServisi _analiz;

    public PanelController(UretimDbContext db, UyariServisi uyari, AnalizServisi analiz)
    {
        _db = db;
        _uyari = uyari;
        _analiz = analiz;
    }

    // Kişiselleştirilmiş kontrol merkezi düzenini kaydeder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzen([FromBody] PanelDuzenIstegi? istek)
    {
        var kimlik = User.KullaniciId();
        var k = await _db.Kullanicilar.FirstOrDefaultAsync(x => x.Id == kimlik);
        if (k == null) return NotFound();

        static List<string> Temizle(List<string>? l) =>
            (l ?? new()).Where(x => x.Length <= 30 && x.All(c => char.IsAsciiLetterLower(c) || c == '-')).Distinct().Take(20).ToList();

        k.PanelDuzeni = istek == null ? null : System.Text.Json.JsonSerializer.Serialize(new
        {
            genis = Temizle(istek.Genis),
            dar = Temizle(istek.Dar),
            gizli = Temizle(istek.Gizli)
        });
        await _db.SaveChangesAsync();
        return Ok(new { kaydedildi = true });
    }

    public async Task<IActionResult> Index()
    {
        var simdi = DateTime.Now;
        var bugun = DateTime.Today;
        var model = new PanelModel();

        var tamamlananlar = await _db.Parcalar
            .Where(p => p.TamamlanmaTarihi >= bugun.AddDays(-30))
            .Select(p => new { p.TamamlanmaTarihi, p.UrunId })
            .ToListAsync();

        model.BugunUretilen = tamamlananlar.Count(p => p.TamamlanmaTarihi >= bugun);
        model.DunkuUretim = tamamlananlar.Count(p => p.TamamlanmaTarihi >= bugun.AddDays(-1) && p.TamamlanmaTarihi < bugun);

        // Fire oranı: son 7 günde hurdaya ayrılan ve bloke parçalar / işlenen parçalar
        var hafta = bugun.AddDays(-7);
        var haftaTamam = tamamlananlar.Count(p => p.TamamlanmaTarihi >= hafta);
        var haftaUygunsuz = await _db.Ncrler.CountAsync(n => n.AcilisTarihi >= hafta && n.ParcaId != null);
        model.FireOrani = haftaTamam + haftaUygunsuz == 0 ? 0 : 100.0 * haftaUygunsuz / (haftaTamam + haftaUygunsuz);

        model.AcikNcr = await _db.Ncrler.CountAsync(n => n.Durum == NcrDurum.Acik);
        model.AktifEmirler = await _db.UretimEmirleri.Include(e => e.Urun)
            .Where(e => e.Durum == EmirDurum.Uretimde || e.Durum == EmirDurum.Bloke || e.Durum == EmirDurum.Planlandi)
            .OrderBy(e => e.Durum == EmirDurum.Planlandi).ThenByDescending(e => e.BaslamaTarihi)
            .Take(8).ToListAsync();
        model.AktifEmir = model.AktifEmirler.Count(e => e.Durum != EmirDurum.Planlandi);

        var emirIdleri = model.AktifEmirler.Select(e => e.Id).ToList();
        model.EmirTamamlanan = await _db.Parcalar
            .Where(p => emirIdleri.Contains(p.UretimEmriId) && p.Durum == ParcaDurum.Tamamlandi)
            .GroupBy(p => p.UretimEmriId)
            .Select(g => new { g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Adet);

        model.Makineler = await _uyari.BakimDurumlariAsync();
        model.CalisanMakine = model.Makineler.Count(m => m.Makine.Durum == MakineDurum.Calisiyor);
        model.ToplamMakine = model.Makineler.Count;
        model.Uyarilar = await _uyari.HepsiAsync();

        model.SonIslemler = await _db.IslemKayitlari
            .OrderByDescending(i => i.Zaman).Take(8)
            .Select(i => new CanliIslem(i.Parca!.SeriNo, i.RotaAdimi!.Ad, i.Makine!.Kod, i.Operator!.AdSoyad, i.Zaman))
            .ToListAsync();

        model.GunlukUretim = Enumerable.Range(0, 14)
            .Select(i => bugun.AddDays(i - 13))
            .Select(g => new GunlukDeger(g.ToString("dd MMM"), tamamlananlar.Count(p => p.TamamlanmaTarihi >= g && p.TamamlanmaTarihi < g.AddDays(1))))
            .ToList();

        var urunler = await _db.Urunler.ToDictionaryAsync(u => u.Id, u => u.Ad);
        model.UrunDagilimi = tamamlananlar.GroupBy(p => p.UrunId)
            .Select(g => new GunlukDeger(urunler.GetValueOrDefault(g.Key, "?"), g.Count()))
            .OrderByDescending(x => x.Deger).ToList();

        model.Oee = await _analiz.ToplamOeeAsync(bugun, simdi);
        model.OeeTrend = await _analiz.GunlukOeeAsync(14);
        model.Pareto = (await _analiz.ParetoAsync(bugun.AddDays(-6), simdi)).Take(6).ToList();
        var kimlik = User.KullaniciId();
        model.Duzen = await _db.Kullanicilar.Where(k => k.Id == kimlik).Select(k => k.PanelDuzeni).FirstOrDefaultAsync();

        return View(model);
    }

    [AllowAnonymous]
    [Route("/hata")]
    public IActionResult Hata() => View();
}
