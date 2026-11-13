using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Controllers;

public class IzlenebilirlikController : Controller
{
    private readonly UretimDbContext _db;
    private readonly AnalizServisi _analiz;

    public IzlenebilirlikController(UretimDbContext db, AnalizServisi analiz)
    {
        _db = db;
        _analiz = analiz;
    }

    // Soy ağacı: üretim lotundan veya hammadde lotundan
    public async Task<IActionResult> Agac(string? lot, string? hammadde)
    {
        if (!string.IsNullOrWhiteSpace(hammadde))
        {
            var kok = await _analiz.HammaddeAgaciAsync(hammadde.Trim().ToUpperInvariant());
            if (kok == null) return NotFound();
            return View(new AgacModel { Kok = kok, Tur = "hammadde" });
        }
        if (!string.IsNullOrWhiteSpace(lot))
        {
            var kok = await _analiz.LotAgaciAsync(lot.Trim().ToUpperInvariant());
            if (kok == null) return NotFound();
            return View(new AgacModel { Kok = kok, Tur = "lot" });
        }
        // Parametre yoksa en son üretimdeki lot gösterilir
        var son = await _db.UretimEmirleri.Where(e => e.Durum != EmirDurum.Planlandi)
            .OrderByDescending(e => e.BaslamaTarihi).Select(e => e.LotNo).FirstOrDefaultAsync();
        if (son == null) return RedirectToAction(nameof(Index));
        return RedirectToAction(nameof(Agac), new { lot = son });
    }

    // Ctrl+K komut paleti için canlı öneriler
    [HttpGet("/ara/oneri")]
    public async Task<IActionResult> Oneri(string? q) => Json(await _analiz.OnerilerAsync(q ?? "", User.Rol()));

    // Tek arama kutusu: seri no, üretim lotu, emir no veya hammadde lotu
    public async Task<IActionResult> Index(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return View();
        var a = q.Trim().ToUpperInvariant();

        if (await _db.Parcalar.AnyAsync(p => p.SeriNo == a)) return RedirectToAction(nameof(Parca), new { seriNo = a });
        var emir = await _db.UretimEmirleri.FirstOrDefaultAsync(e => e.LotNo == a || e.EmirNo == a);
        if (emir != null) return RedirectToAction(nameof(Lot), new { lotNo = emir.LotNo });
        if (await _db.HammaddeLotlari.AnyAsync(l => l.LotNo == a)) return RedirectToAction(nameof(Hammadde), new { lotNo = a });
        var ncr = await _db.Ncrler.FirstOrDefaultAsync(n => n.NcrNo == a);
        if (ncr != null) return RedirectToAction("Detay", "Ncr", new { id = ncr.Id });

        ViewBag.Arama = q;
        ViewBag.Bulunamadi = true;
        return View();
    }

    public async Task<IActionResult> Parca(string seriNo)
    {
        var parca = await _db.Parcalar.Include(p => p.Urun).Include(p => p.UretimEmri)
            .FirstOrDefaultAsync(p => p.SeriNo == seriNo.ToUpperInvariant());
        if (parca == null) return NotFound();

        var model = new ParcaIzModel
        {
            Parca = parca,
            Adimlar = await _db.RotaAdimlari.Include(a => a.Makine).Where(a => a.UrunId == parca.UrunId).OrderBy(a => a.Sira).ToListAsync(),
            Islemler = await _db.IslemKayitlari.Include(i => i.RotaAdimi).Include(i => i.Makine).Include(i => i.Operator)
                .Where(i => i.ParcaId == parca.Id).OrderBy(i => i.Zaman).ToListAsync(),
            Olcumler = await _db.Olcumler.Include(o => o.Karakteristik).Include(o => o.OlcumAleti).Include(o => o.Kullanici)
                .Where(o => o.ParcaId == parca.Id).OrderBy(o => o.Zaman).ToListAsync(),
            Hammaddeler = await _db.HammaddeTuketimleri.Include(t => t.HammaddeLotu!).ThenInclude(l => l.Malzeme)
                .Where(t => t.UretimEmriId == parca.UretimEmriId).ToListAsync(),
            Ncrler = await _db.Ncrler.Where(n => n.ParcaId == parca.Id).ToListAsync()
        };
        return View(model);
    }

    public async Task<IActionResult> Lot(string lotNo)
    {
        var emir = await _db.UretimEmirleri.Include(e => e.Urun).Include(e => e.Parcalar)
            .FirstOrDefaultAsync(e => e.LotNo == lotNo.ToUpperInvariant());
        if (emir == null) return NotFound();

        var model = new LotIzModel
        {
            Emir = emir,
            Hammaddeler = await _db.HammaddeTuketimleri.Include(t => t.HammaddeLotu!).ThenInclude(l => l.Malzeme)
                .Where(t => t.UretimEmriId == emir.Id).ToListAsync(),
            DurumSayilari = emir.Parcalar.GroupBy(p => p.Durum).ToDictionary(g => g.Key, g => g.Count()),
            Ncrler = await _db.Ncrler.Where(n => n.UretimEmriId == emir.Id).ToListAsync()
        };
        return View(model);
    }

    public async Task<IActionResult> Hammadde(string lotNo)
    {
        var lot = await _db.HammaddeLotlari.Include(l => l.Malzeme).FirstOrDefaultAsync(l => l.LotNo == lotNo.ToUpperInvariant());
        if (lot == null) return NotFound();
        var model = new HammaddeIzModel
        {
            Lot = lot,
            Tuketimler = await _db.HammaddeTuketimleri.Include(t => t.UretimEmri!).ThenInclude(e => e.Urun)
                .Where(t => t.HammaddeLotuId == lot.Id).OrderBy(t => t.Zaman).ToListAsync()
        };
        return View(model);
    }
}

public class YonetimController : Controller
{
    private readonly UretimDbContext _db;
    private readonly PasswordHasher<Kullanici> _hasher = new();

    public YonetimController(UretimDbContext db) => _db = db;

    // ---------- Kullanıcılar ----------

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Kullanicilar()
    {
        return View(new KullaniciListeModel
        {
            Kullanicilar = await _db.Kullanicilar.OrderBy(k => k.Rol).ThenBy(k => k.AdSoyad).ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> KullaniciEkle(string kullaniciAdi, string adSoyad, string rol, string sifre)
    {
        var kAdi = (kullaniciAdi ?? "").Trim().ToLowerInvariant();
        if (kAdi.Length < 3 || string.IsNullOrWhiteSpace(adSoyad) || !Roller.Tumu.Contains(rol))
            TempData["Hata"] = "Kullanıcı adı (en az 3 karakter), ad soyad ve rol zorunlu.";
        else if (!SifreGucluMu(sifre))
            TempData["Hata"] = "Şifre en az 8 karakter olmalı; büyük harf, küçük harf ve rakam içermeli.";
        else if (await _db.Kullanicilar.AnyAsync(k => k.KullaniciAdi == kAdi))
            TempData["Hata"] = "Bu kullanıcı adı zaten var.";
        else
        {
            var k = new Kullanici { KullaniciAdi = kAdi, AdSoyad = adSoyad.Trim(), Rol = rol };
            k.SifreHash = _hasher.HashPassword(k, sifre);
            _db.Kullanicilar.Add(k);
            await _db.SaveChangesAsync();
            TempData["Mesaj"] = $"{k.AdSoyad} eklendi ({Roller.Ad(rol)}).";
            TempData["Efekt"] = "basari";
        }
        return RedirectToAction(nameof(Kullanicilar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RolDegistir(int id, string rol)
    {
        var k = await _db.Kullanicilar.FirstOrDefaultAsync(x => x.Id == id);
        if (k == null || !Roller.Tumu.Contains(rol)) return BadRequest();
        if (k.Id == User.KullaniciId())
        {
            TempData["Hata"] = "Kendi rolünü değiştiremezsin.";
            return RedirectToAction(nameof(Kullanicilar));
        }
        k.Rol = rol;
        await _db.SaveChangesAsync();
        TempData["Mesaj"] = $"{k.AdSoyad} artık {Roller.Ad(rol)}. Değişiklik bir sonraki girişte geçerli olur.";
        return RedirectToAction(nameof(Kullanicilar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AktifDegistir(int id)
    {
        var k = await _db.Kullanicilar.FirstOrDefaultAsync(x => x.Id == id);
        if (k == null) return NotFound();
        if (k.Id == User.KullaniciId())
        {
            TempData["Hata"] = "Kendi hesabını pasif yapamazsın.";
            return RedirectToAction(nameof(Kullanicilar));
        }
        k.Aktif = !k.Aktif;
        k.KilitBitis = null;
        k.HataliDeneme = 0;
        await _db.SaveChangesAsync();
        TempData["Mesaj"] = $"{k.AdSoyad} {(k.Aktif ? "aktif edildi" : "pasif yapıldı")}.";
        return RedirectToAction(nameof(Kullanicilar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SifreSifirla(int id, string sifre)
    {
        var k = await _db.Kullanicilar.FirstOrDefaultAsync(x => x.Id == id);
        if (k == null) return NotFound();
        if (!SifreGucluMu(sifre))
        {
            TempData["Hata"] = "Şifre en az 8 karakter olmalı; büyük harf, küçük harf ve rakam içermeli.";
            return RedirectToAction(nameof(Kullanicilar));
        }
        k.SifreHash = _hasher.HashPassword(k, sifre);
        k.KilitBitis = null;
        k.HataliDeneme = 0;
        await _db.SaveChangesAsync();
        TempData["Mesaj"] = $"{k.AdSoyad} için şifre güncellendi.";
        return RedirectToAction(nameof(Kullanicilar));
    }

    public static bool SifreGucluMu(string? s) =>
        !string.IsNullOrEmpty(s) && s.Length >= 8 && s.Any(char.IsUpper) && s.Any(char.IsLower) && s.Any(char.IsDigit);

    // ---------- Yetkinlik matrisi ----------

    [Authorize(Roles = "Admin,Yonetici")]
    public async Task<IActionResult> Yetkinlik()
    {
        var yetkinlikler = await _db.Yetkinlikler.ToListAsync();
        return View(new YetkinlikModel
        {
            Kullanicilar = await _db.Kullanicilar.Where(k => k.Aktif && (k.Rol == Roller.Operator || k.Rol == Roller.Kalite))
                .OrderBy(k => k.AdSoyad).ToListAsync(),
            Makineler = await _db.Makineler.OrderBy(m => m.Kod).ToListAsync(),
            Hucreler = yetkinlikler.ToDictionary(y => (y.KullaniciId, y.MakineId))
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Yonetici")]
    public async Task<IActionResult> YetkinlikDegistir(int kullaniciId, int makineId, string islem)
    {
        var y = await _db.Yetkinlikler.FirstOrDefaultAsync(x => x.KullaniciId == kullaniciId && x.MakineId == makineId);
        if (islem == "kaldir")
        {
            if (y != null) _db.Yetkinlikler.Remove(y);
        }
        else
        {
            // Sertifika verilir ya da yenilenir: 1 yıl geçerli
            if (y == null) _db.Yetkinlikler.Add(new Yetkinlik { KullaniciId = kullaniciId, MakineId = makineId, GecerlilikBitis = DateTime.Today.AddYears(1) });
            else y.GecerlilikBitis = DateTime.Today.AddYears(1);
        }
        await _db.SaveChangesAsync();
        TempData["Efekt"] = "tik";
        return RedirectToAction(nameof(Yetkinlik));
    }

    // ---------- Ürünler ve toleranslar ----------

    [Authorize(Roles = "Admin,Yonetici,Kalite")]
    public async Task<IActionResult> Urunler()
    {
        return View(new UrunListeModel
        {
            Urunler = await _db.Urunler.Include(u => u.Malzeme)
                .Include(u => u.RotaAdimlari).ThenInclude(a => a.Makine)
                .Include(u => u.Karakteristikler)
                .OrderBy(u => u.Kod).ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Yonetici,Kalite")]
    public async Task<IActionResult> ToleransGuncelle(int id, string? alt, string? ust)
    {
        var k = await _db.Karakteristikler.Include(x => x.Urun).FirstOrDefaultAsync(x => x.Id == id);
        if (k == null) return NotFound();
        if (!Sayi.Coz(alt, out var a) || !Sayi.Coz(ust, out var u) || a >= u || k.Nominal < a || k.Nominal > u)
        {
            TempData["Hata"] = "Alt sınır üst sınırdan küçük olmalı ve nominal değer aralıkta kalmalı.";
            return RedirectToAction(nameof(Urunler));
        }
        k.AltSinir = a;
        k.UstSinir = u;
        await _db.SaveChangesAsync();
        TempData["Mesaj"] = $"{k.Urun?.Ad} · {k.Ad} toleransı güncellendi. Değişiklik audit log'a yazıldı.";
        return RedirectToAction(nameof(Urunler));
    }
}

[Authorize(Roles = "Admin,Yonetici")]
public class AuditController : Controller
{
    private readonly UretimDbContext _db;
    private readonly RaporServisi _rapor;

    public AuditController(UretimDbContext db, RaporServisi rapor)
    {
        _db = db;
        _rapor = rapor;
    }

    public async Task<IActionResult> Excel(string? kullanici, string? tablo)
    {
        var sorgu = _db.AuditKayitlari.AsQueryable();
        if (!string.IsNullOrEmpty(kullanici)) sorgu = sorgu.Where(a => a.KullaniciAdi == kullanici);
        if (!string.IsNullOrEmpty(tablo)) sorgu = sorgu.Where(a => a.Tablo == tablo);
        var kayitlar = await sorgu.OrderByDescending(a => a.Zaman).Take(5000).ToListAsync();
        var satirlar = kayitlar.Select(k => new AuditSatiri(k, Coz(k.Degisiklikler))).ToList();
        return File(_rapor.AuditExcel(satirlar), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"AuditLog-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    public async Task<IActionResult> Index(string? kullanici, string? tablo)
    {
        var sorgu = _db.AuditKayitlari.AsQueryable();
        if (!string.IsNullOrEmpty(kullanici)) sorgu = sorgu.Where(a => a.KullaniciAdi == kullanici);
        if (!string.IsNullOrEmpty(tablo)) sorgu = sorgu.Where(a => a.Tablo == tablo);

        var kayitlar = await sorgu.OrderByDescending(a => a.Zaman).Take(300).ToListAsync();
        var model = new AuditModel
        {
            Kullanici = kullanici,
            Tablo = tablo,
            Tablolar = await _db.AuditKayitlari.Select(a => a.Tablo).Distinct().OrderBy(t => t).ToListAsync(),
            KullaniciAdlari = await _db.AuditKayitlari.Select(a => a.KullaniciAdi).Distinct().OrderBy(t => t).ToListAsync(),
            Kayitlar = kayitlar.Select(k => new AuditSatiri(k, Coz(k.Degisiklikler))).ToList()
        };
        return View(model);
    }

    private static List<UretimDbContext.Degisiklik> Coz(string json)
    {
        try { return JsonSerializer.Deserialize<List<UretimDbContext.Degisiklik>>(json) ?? new(); }
        catch { return new(); }
    }
}

public class BildirimController : Controller
{
    private readonly UretimDbContext _db;
    private readonly CanliYayin _yayin;

    public BildirimController(UretimDbContext db, CanliYayin yayin)
    {
        _db = db;
        _yayin = yayin;
    }

    public async Task<IActionResult> Index()
    {
        var kimlik = User.KullaniciId();
        var kullanici = await _db.Kullanicilar.FirstAsync(k => k.Id == kimlik);
        var model = new BildirimListeModel
        {
            SonOkuma = kullanici.SonBildirimOkuma,
            Bildirimler = await BildirimServisi.RolIcin(_db.Bildirimler, User.Rol())
                .OrderByDescending(b => b.Zaman).Take(100).ToListAsync()
        };
        kullanici.SonBildirimOkuma = DateTime.Now;
        await _db.SaveChangesAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TumunuOku()
    {
        var kimlik = User.KullaniciId();
        var kullanici = await _db.Kullanicilar.FirstAsync(k => k.Id == kimlik);
        kullanici.SonBildirimOkuma = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // Server-Sent Events akışı: yeni bildirim ve istasyon işlemleri anında gelir
    [HttpGet]
    public async Task Akis(CancellationToken iptal)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var abone = _yayin.AboneOl(User.Rol());
        try
        {
            await Response.WriteAsync(": baglandi\n\n", iptal);
            await Response.Body.FlushAsync(iptal);

            while (!iptal.IsCancellationRequested)
            {
                // 25 sn'de bir boş mesaj: bağlantı canlı kalsın
                var bekleme = Task.Delay(TimeSpan.FromSeconds(25), iptal);
                var okuma = abone.Kanal.Reader.WaitToReadAsync(iptal).AsTask();
                var biten = await Task.WhenAny(okuma, bekleme);

                if (biten == bekleme)
                {
                    await Response.WriteAsync(": nabiz\n\n", iptal);
                }
                else
                {
                    if (!await okuma) break;
                    while (abone.Kanal.Reader.TryRead(out var mesaj))
                        await Response.WriteAsync($"data: {mesaj}\n\n", iptal);
                }
                await Response.Body.FlushAsync(iptal);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _yayin.AboneligiBitir(abone);
        }
    }
}
