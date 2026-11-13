using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Controllers;

// Canlı fabrika yerleşim planı
public class FabrikaController : Controller
{
    private readonly UretimDbContext _db;
    private readonly AnalizServisi _analiz;
    private readonly UyariServisi _uyari;

    public FabrikaController(UretimDbContext db, AnalizServisi analiz, UyariServisi uyari)
    {
        _db = db;
        _analiz = analiz;
        _uyari = uyari;
    }

    public async Task<IActionResult> Index(string? makine)
    {
        var simdi = DateTime.Now;
        var bugun = DateTime.Today;
        var model = new FabrikaModel { SeciliKod = makine };

        model.Makineler = await _analiz.OeeAsync(bugun, simdi);
        model.Toplam = OeeHesaplayici.Topla(model.Makineler.Select(m => m.Girdi));
        model.Bakim = (await _uyari.BakimDurumlariAsync()).ToDictionary(x => x.Makine.Id, x => x.Durum);
        model.AcikDuruslar = (await _db.DurusKayitlari.Where(d => d.Bitis == null).ToListAsync())
            .GroupBy(d => d.MakineId).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Baslangic).First());

        var bugunIslem = await _db.IslemKayitlari.Where(i => i.Zaman >= bugun)
            .Select(i => new { i.RotaAdimiId, i.MakineId, i.Zaman, Ad = i.Operator!.AdSoyad }).ToListAsync();
        model.BugunAdet = bugunIslem.GroupBy(i => i.MakineId).ToDictionary(g => g.Key, g => g.Count());
        model.SonOperator = bugunIslem.Where(i => i.Zaman >= simdi.AddMinutes(-45))
            .GroupBy(i => i.MakineId).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Zaman).First().Ad);

        // Rota adımlarından makineler arası malzeme akışı
        var adimlar = await _db.RotaAdimlari.ToListAsync();
        var oncekiMakine = adimlar.ToDictionary(a => a.Id,
            a => adimlar.Where(x => x.UrunId == a.UrunId && x.Sira == a.Sira - 1).Select(x => (int?)x.MakineId).FirstOrDefault());
        var kenarlar = new Dictionary<(int, int), int>();
        foreach (var a in adimlar)
            if (oncekiMakine[a.Id] is int kaynak && kaynak != a.MakineId) kenarlar.TryAdd((kaynak, a.MakineId), 0);
        foreach (var i in bugunIslem)
            if (oncekiMakine.GetValueOrDefault(i.RotaAdimiId) is int kaynak && kaynak != i.MakineId)
                kenarlar[(kaynak, i.MakineId)] = kenarlar.GetValueOrDefault((kaynak, i.MakineId)) + 1;
        model.Akislar = kenarlar.Select(x => (x.Key.Item1, x.Key.Item2, x.Value)).ToList();

        return View(model);
    }
}

// Vardiya raporu, OEE ve PDF / Excel çıktıları
public class RaporController : Controller
{
    private readonly RaporServisi _rapor;
    private readonly AnalizServisi _analiz;

    public RaporController(RaporServisi rapor, AnalizServisi analiz)
    {
        _rapor = rapor;
        _analiz = analiz;
    }

    private static (DateTime Tarih, string Kod) Secim(DateTime? tarih, string? vardiya)
    {
        var t = (tarih ?? DateTime.Today).Date;
        if (t > DateTime.Today) t = DateTime.Today;
        var kod = string.IsNullOrEmpty(vardiya) ? (tarih == null ? VardiyaPlani.Kod(DateTime.Now) : "A") : vardiya.ToUpperInvariant();
        return (t, VardiyaPlani.Kodlar.Contains(kod) ? kod : "A");
    }

    [Authorize(Roles = "Admin,Yonetici,Kalite,DepoBakim")]
    public async Task<IActionResult> Vardiya(DateTime? tarih, string? vardiya)
    {
        var (t, kod) = Secim(tarih, vardiya);
        var model = await _rapor.VardiyaAsync(t, kod);
        model.OeeTrend = await _analiz.GunlukOeeAsync(14);
        return View(model);
    }

    [Authorize(Roles = "Admin,Yonetici,Kalite,DepoBakim")]
    public async Task<IActionResult> VardiyaPdf(DateTime? tarih, string? vardiya)
    {
        var (t, kod) = Secim(tarih, vardiya);
        var model = await _rapor.VardiyaAsync(t, kod);
        return File(_rapor.VardiyaPdf(model), "application/pdf", $"Vardiya-{t:yyyyMMdd}-{kod}.pdf");
    }

    [Authorize(Roles = "Admin,Yonetici,Kalite,DepoBakim")]
    public async Task<IActionResult> VardiyaExcel(DateTime? tarih, string? vardiya)
    {
        var (t, kod) = Secim(tarih, vardiya);
        var model = await _rapor.VardiyaAsync(t, kod);
        return File(_rapor.VardiyaExcel(model), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Vardiya-{t:yyyyMMdd}-{kod}.xlsx");
    }

    public async Task<IActionResult> NcrPdf(int id)
    {
        var sonuc = await _rapor.NcrPdfAsync(id);
        if (sonuc is not { } r) return NotFound();
        return File(r.Dosya, "application/pdf", r.Ad);
    }

    public async Task<IActionResult> LotSertifika(string lotNo)
    {
        var sonuc = await _rapor.LotSertifikaPdfAsync((lotNo ?? "").ToUpperInvariant());
        if (sonuc is not { } r) return NotFound();
        return File(r.Dosya, "application/pdf", r.Ad);
    }

    public async Task<IActionResult> LotOlcumler(string lotNo)
    {
        var sonuc = await _rapor.LotOlcumExcelAsync((lotNo ?? "").ToUpperInvariant());
        if (sonuc is not { } r) return NotFound();
        return File(r.Dosya, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", r.Ad);
    }

    public async Task<IActionResult> DurusExcel(int gun = 30)
    {
        gun = Math.Clamp(gun, 1, 90);
        var bas = DateTime.Today.AddDays(-gun + 1);
        var bit = DateTime.Now;
        var duruslar = await _analiz.DuruslarAsync(bas, bit);
        var pareto = await _analiz.ParetoAsync(bas, bit);
        return File(_rapor.DurusExcel(duruslar, pareto), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Duruslar-{gun}gun.xlsx");
    }
}

// Duruş kayıtları ve Pareto analizi
public class DurusController : Controller
{
    private readonly UretimDbContext _db;
    private readonly AnalizServisi _analiz;
    private readonly BildirimServisi _bildirim;

    public DurusController(UretimDbContext db, AnalizServisi analiz, BildirimServisi bildirim)
    {
        _db = db;
        _analiz = analiz;
        _bildirim = bildirim;
    }

    public async Task<IActionResult> Index(int gun = 30)
    {
        gun = gun is 7 or 14 or 30 ? gun : 30;
        var simdi = DateTime.Now;
        var bas = DateTime.Today.AddDays(-gun + 1);
        var duruslar = await _analiz.DuruslarAsync(bas, simdi);

        var model = new DurusModel
        {
            Gun = gun,
            Duruslar = duruslar.Take(60).ToList(),
            Acik = duruslar.Where(d => d.Bitis == null).ToList(),
            Pareto = await _analiz.ParetoAsync(bas, simdi),
            Makineler = await _db.Makineler.OrderBy(m => m.Kod).ToListAsync(),
            MakineBazli = duruslar.GroupBy(d => d.Makine?.Kod ?? "?")
                .Select(g => new GunlukDeger(g.Key, Math.Round(g.Sum(d => OeeHesaplayici.Cakisma(d.Baslangic, d.Bitis ?? simdi, bas, simdi)))))
                .OrderByDescending(x => x.Deger).ToList()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Baslat(int makineId, string neden, string? aciklama, string? donus)
    {
        var makine = await _db.Makineler.FirstOrDefaultAsync(m => m.Id == makineId);
        if (makine == null || !DurusNedeni.Tumu.Contains(neden))
        {
            TempData["Hata"] = "Makine ve duruş nedeni seçin.";
            return Geri(donus);
        }
        if (await _db.DurusKayitlari.AnyAsync(d => d.MakineId == makineId && d.Bitis == null))
        {
            TempData["Hata"] = $"{makine.Kod} için zaten açık bir duruş var. Önce onu bitirin.";
            return Geri(donus);
        }

        _db.DurusKayitlari.Add(new DurusKaydi
        {
            MakineId = makineId,
            Neden = neden,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim(),
            Baslangic = DateTime.Now,
            BildirenKisi = User.AdSoyad()
        });
        makine.Durum = neden == DurusNedeni.Ariza ? MakineDurum.Arizali
                     : neden == DurusNedeni.PlanliBakim ? MakineDurum.Bakimda
                     : MakineDurum.Bekliyor;
        await _db.SaveChangesAsync();

        var kritik = neden == DurusNedeni.Ariza;
        await _bildirim.GonderAsync($"{makine.Kod} durdu · {neden}",
            $"{makine.Ad}{(string.IsNullOrWhiteSpace(aciklama) ? "" : ": " + aciklama.Trim())} ({User.AdSoyad()})",
            kritik ? "kritik" : "uyari", "/durus",
            kritik ? new[] { Roller.DepoBakim, Roller.Yonetici, Roller.Admin } : new[] { Roller.Yonetici, Roller.Admin, Roller.DepoBakim });

        TempData["Mesaj"] = $"{makine.Kod} duruşu başladı: {neden}. Süre OEE'ye otomatik yansır.";
        TempData["Efekt"] = kritik ? "alarm" : "basari";
        return Geri(donus);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bitir(int id, string? donus)
    {
        var durus = await _db.DurusKayitlari.Include(d => d.Makine).FirstOrDefaultAsync(d => d.Id == id);
        if (durus == null) return NotFound();
        if (durus.Bitis == null)
        {
            durus.Bitis = DateTime.Now;
            if (durus.Makine is Makine m && m.Durum != MakineDurum.Calisiyor) m.Durum = MakineDurum.Bekliyor;
            await _db.SaveChangesAsync();
            TempData["Mesaj"] = $"{durus.Makine?.Kod} duruşu bitti · {Gorunum.Sure(durus.Bitis.Value - durus.Baslangic)}. Makine üretime hazır.";
            TempData["Efekt"] = "basari";
        }
        return Geri(donus);
    }

    private IActionResult Geri(string? donus) =>
        !string.IsNullOrEmpty(donus) && Url.IsLocalUrl(donus) ? Redirect(donus) : RedirectToAction(nameof(Index));
}
