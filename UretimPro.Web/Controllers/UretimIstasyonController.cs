using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Controllers;

public class UretimController : Controller
{
    private readonly UretimDbContext _db;
    private readonly UretimServisi _uretim;

    public UretimController(UretimDbContext db, UretimServisi uretim)
    {
        _db = db;
        _uretim = uretim;
    }

    public async Task<IActionResult> Index(string? durum, string? q)
    {
        var sorgu = _db.UretimEmirleri.Include(e => e.Urun).AsQueryable();
        if (!string.IsNullOrEmpty(durum)) sorgu = sorgu.Where(e => e.Durum == durum);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var a = q.Trim().ToUpperInvariant();
            sorgu = sorgu.Where(e => e.EmirNo.Contains(a) || e.LotNo.Contains(a) || e.Urun!.Kod.Contains(a));
        }

        var emirler = await sorgu.OrderByDescending(e => e.PlanTarihi).ThenByDescending(e => e.Id).Take(200).ToListAsync();
        var idler = emirler.Select(e => e.Id).ToList();
        var tamamlanan = await _db.Parcalar
            .Where(p => idler.Contains(p.UretimEmriId) && p.Durum == ParcaDurum.Tamamlandi)
            .GroupBy(p => p.UretimEmriId).Select(g => new { g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Adet);

        return View(new EmirListeModel { Emirler = emirler, Tamamlanan = tamamlanan, Durum = durum, Arama = q });
    }

    public async Task<IActionResult> Detay(int id)
    {
        var emir = await _db.UretimEmirleri.Include(e => e.Urun!).ThenInclude(u => u.Malzeme)
            .Include(e => e.Parcalar)
            .Include(e => e.Tuketimler).ThenInclude(t => t.HammaddeLotu)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (emir == null) return NotFound();

        var model = new EmirDetayModel
        {
            Emir = emir,
            Adimlar = await _db.RotaAdimlari.Include(a => a.Makine).Where(a => a.UrunId == emir.UrunId).OrderBy(a => a.Sira).ToListAsync(),
            AdimdaBekleyen = emir.Parcalar.Where(p => p.Durum == ParcaDurum.Uretimde || p.Durum == ParcaDurum.Bekliyor)
                .GroupBy(p => p.TamamlananAdim).ToDictionary(g => g.Key, g => g.Count()),
            Ncrler = await _db.Ncrler.Where(n => n.UretimEmriId == id).OrderByDescending(n => n.AcilisTarihi).ToListAsync()
        };

        if (emir.Durum == EmirDurum.Planlandi)
        {
            model.UygunLotlar = await _db.HammaddeLotlari
                .Where(l => l.MalzemeId == emir.Urun!.MalzemeId && l.KalanMiktar > 0)
                .OrderBy(l => l.GirisTarihi).ToListAsync();
            var (paylar, eksik, ihtiyac) = await _uretim.FifoOnerisiAsync(id);
            model.FifoPaylari = paylar;
            model.FifoEksik = eksik;
            model.Ihtiyac = ihtiyac;
            model.LotNolari = model.UygunLotlar.ToDictionary(l => l.Id, l => l.LotNo);
        }
        return View(model);
    }

    [Authorize(Roles = "Admin,Yonetici")]
    public async Task<IActionResult> Yeni()
    {
        return View(await YeniModelAsync(new YeniEmirModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Yonetici")]
    public async Task<IActionResult> Yeni(YeniEmirModel model)
    {
        if (model.Miktar < 1 || model.Miktar > 5000)
            ModelState.AddModelError(nameof(model.Miktar), "Miktar 1 ile 5.000 arasında olmalı.");
        if (!await _db.Urunler.AnyAsync(u => u.Id == model.UrunId && u.Aktif))
            ModelState.AddModelError(nameof(model.UrunId), "Ürün seçin.");
        if (model.PlanTarihi.Date < DateTime.Today.AddDays(-1))
            ModelState.AddModelError(nameof(model.PlanTarihi), "Plan tarihi geçmişte olamaz.");

        if (!ModelState.IsValid) return View(await YeniModelAsync(model));

        var emir = await _uretim.EmirOlusturAsync(model.UrunId, model.Miktar, model.PlanTarihi.Date);
        TempData["Mesaj"] = $"{emir.EmirNo} oluşturuldu · {emir.Miktar} seri numarası atandı (Lot {emir.LotNo}).";
        TempData["Efekt"] = "basari";
        return RedirectToAction(nameof(Detay), new { id = emir.Id });
    }

    private async Task<YeniEmirModel> YeniModelAsync(YeniEmirModel model)
    {
        model.Urunler = await _db.Urunler.Include(u => u.Malzeme).Include(u => u.RotaAdimlari)
            .Where(u => u.Aktif).OrderBy(u => u.Kod).ToListAsync();
        model.MalzemeStoklari = await _db.HammaddeLotlari.GroupBy(l => l.MalzemeId)
            .Select(g => new { g.Key, Toplam = g.Sum(l => l.KalanMiktar) })
            .ToDictionaryAsync(x => x.Key, x => x.Toplam);
        if (model.UrunId == 0 && model.Urunler.Count > 0) model.UrunId = model.Urunler[0].Id;
        return model;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Yonetici")]
    public async Task<IActionResult> Baslat(int id, int? lotId)
    {
        var hata = await _uretim.EmirBaslatAsync(id, lotId);
        if (hata != null)
        {
            TempData["Hata"] = hata;
            TempData["Efekt"] = "hata";
        }
        else
        {
            TempData["Mesaj"] = "Üretim başladı. Hammadde stoktan düşüldü, parçalar istasyonlarda okutulabilir.";
            TempData["Efekt"] = "basari";
        }
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Yonetici")]
    public async Task<IActionResult> Iptal(int id)
    {
        var emir = await _db.UretimEmirleri.FirstOrDefaultAsync(e => e.Id == id);
        if (emir == null) return NotFound();
        if (emir.Durum != EmirDurum.Planlandi)
        {
            TempData["Hata"] = "Sadece başlamamış emirler iptal edilebilir.";
            return RedirectToAction(nameof(Detay), new { id });
        }
        emir.Durum = EmirDurum.Iptal;
        await _db.SaveChangesAsync();
        TempData["Mesaj"] = $"{emir.EmirNo} iptal edildi.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    // Yazdırılabilir QR etiketler
    public async Task<IActionResult> Etiketler(int id)
    {
        var emir = await _db.UretimEmirleri.Include(e => e.Urun).Include(e => e.Parcalar)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (emir == null) return NotFound();
        return View(emir);
    }
}

[Authorize(Roles = "Operator,Kalite,Admin,Yonetici")]
public class IstasyonController : Controller
{
    private readonly UretimDbContext _db;
    private readonly UretimServisi _uretim;

    public IstasyonController(UretimDbContext db, UretimServisi uretim)
    {
        _db = db;
        _uretim = uretim;
    }

    public async Task<IActionResult> Index()
    {
        var simdi = DateTime.Now;
        var kullaniciId = User.KullaniciId();
        var makineler = await _db.Makineler.OrderBy(m => m.Kod).ToListAsync();
        var yetkili = await _db.Yetkinlikler.Where(y => y.KullaniciId == kullaniciId && y.GecerlilikBitis >= simdi)
            .Select(y => y.MakineId).ToListAsync();

        // Her istasyonda sırası gelmiş parça sayısı
        var bekleyen = new Dictionary<int, int>();
        var aktifParcalar = await _db.Parcalar
            .Where(p => p.Durum == ParcaDurum.Uretimde && p.UretimEmri!.Durum == EmirDurum.Uretimde)
            .Select(p => new { p.UrunId, p.TamamlananAdim }).ToListAsync();
        var adimlar = await _db.RotaAdimlari.ToListAsync();
        foreach (var p in aktifParcalar)
        {
            var adim = adimlar.FirstOrDefault(a => a.UrunId == p.UrunId && a.Sira == p.TamamlananAdim + 1);
            if (adim != null) bekleyen[adim.MakineId] = bekleyen.GetValueOrDefault(adim.MakineId) + 1;
        }

        return View(new IstasyonSecimModel { Makineler = makineler, Yetkili = yetkili.ToHashSet(), BekleyenParca = bekleyen });
    }

    public async Task<IActionResult> Ekran(int id)
    {
        var makine = await _db.Makineler.FirstOrDefaultAsync(m => m.Id == id);
        if (makine == null) return NotFound();

        var simdi = DateTime.Now;
        var kullaniciId = User.KullaniciId();
        var yetkinlik = await _db.Yetkinlikler.FirstOrDefaultAsync(y => y.KullaniciId == kullaniciId && y.MakineId == id);

        // Demo kolaylığı: bu istasyonda sırası gelmiş birkaç seri no
        var adimlar = await _db.RotaAdimlari.Where(a => a.MakineId == id).ToListAsync();
        var ornekler = new List<string>();
        foreach (var a in adimlar)
        {
            var seriler = await _db.Parcalar
                .Where(p => p.UrunId == a.UrunId && p.TamamlananAdim == a.Sira - 1 && p.Durum == ParcaDurum.Uretimde && p.UretimEmri!.Durum == EmirDurum.Uretimde)
                .OrderBy(p => p.Id).Take(4).Select(p => p.SeriNo).ToListAsync();
            ornekler.AddRange(seriler);
        }

        var model = new IstasyonEkranModel
        {
            Makine = makine,
            Yetkili = yetkinlik != null && yetkinlik.GecerlilikBitis >= simdi,
            YetkinlikBitis = yetkinlik?.GecerlilikBitis,
            OrnekSeriNolar = ornekler.Take(6).ToList(),
            BugunIslem = await _db.IslemKayitlari.CountAsync(i => i.MakineId == id && i.Zaman >= DateTime.Today),
            SonIslemler = await _db.IslemKayitlari.Where(i => i.MakineId == id)
                .OrderByDescending(i => i.Zaman).Take(6)
                .Select(i => new CanliIslem(i.Parca!.SeriNo, i.RotaAdimi!.Ad, i.Makine!.Kod, i.Operator!.AdSoyad, i.Zaman))
                .ToListAsync()
        };
        return View(model);
    }

    public record OkutIstegi(int MakineId, string SeriNo);
    public record OlcumIstegi(int MakineId, string SeriNo, List<OlcumGirdisi> Olcumler);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Okut([FromBody] OkutIstegi istek)
    {
        var cevap = await _uretim.OkutAsync(istek.MakineId, istek.SeriNo, User.KullaniciId());
        return Json(cevap);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Olcum([FromBody] OlcumIstegi istek)
    {
        var cevap = await _uretim.OlcumKaydetAsync(istek.MakineId, istek.SeriNo, User.KullaniciId(), istek.Olcumler ?? new());
        return Json(cevap);
    }
}
