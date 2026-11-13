using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Controllers;

[Authorize(Roles = "Kalite,Yonetici,Admin,DepoBakim")]
public class KaliteController : Controller
{
    private readonly UretimDbContext _db;
    private readonly UyariServisi _uyari;

    public KaliteController(UretimDbContext db, UyariServisi uyari)
    {
        _db = db;
        _uyari = uyari;
    }

    // SPC kontrol grafiği
    public async Task<IActionResult> Index(int? karakteristikId, int? makineId)
    {
        var model = new SpcModel
        {
            Karakteristikler = await _db.Karakteristikler.Include(k => k.Urun).Include(k => k.RotaAdimi!).ThenInclude(a => a.Makine)
                .OrderBy(k => k.Urun!.Kod).ThenBy(k => k.Ad).ToListAsync(),
            Ozetler = await _uyari.SpcOzetleriAsync()
        };

        // Seçim yoksa ihlali olan ilk karakteristik açılır
        var varsayilan = model.Ozetler.FirstOrDefault(o => o.Sonuc.Ihlaller.Any(i => i.Indeks >= o.Degerler.Count - 3)) ?? model.Ozetler.FirstOrDefault();
        var hedefKar = karakteristikId ?? varsayilan?.Karakteristik.Id;
        model.Secili = model.Karakteristikler.FirstOrDefault(k => k.Id == hedefKar);
        if (model.Secili == null) return View(model);

        var hedefMakine = makineId ?? model.Secili.RotaAdimi!.MakineId;
        model.Makine = await _db.Makineler.FirstOrDefaultAsync(m => m.Id == hedefMakine);
        if (model.Makine == null) return View(model);

        model.Olcumler = await _db.Olcumler.Include(o => o.Parca).Include(o => o.OlcumAleti).Include(o => o.Kullanici)
            .Where(o => o.KarakteristikId == model.Secili.Id && o.MakineId == model.Makine.Id)
            .OrderByDescending(o => o.Zaman).Take(40).ToListAsync();
        model.Olcumler.Reverse();
        model.Uygunsuz = model.Olcumler.Count(o => !o.Uygun);
        if (model.Olcumler.Count >= 2)
            model.Sonuc = SpcHesaplayici.Analiz(model.Olcumler.Select(o => o.Deger).ToList(), model.Secili.AltSinir, model.Secili.UstSinir);

        return View(model);
    }

    public async Task<IActionResult> Kalibrasyon()
    {
        return View(await _db.OlcumAletleri.OrderBy(a => a.Kod).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Kalite,Admin")]
    public async Task<IActionResult> Kalibre(int id)
    {
        var alet = await _db.OlcumAletleri.FirstOrDefaultAsync(a => a.Id == id);
        if (alet == null) return NotFound();
        alet.SonKalibrasyon = DateTime.Today;
        await _db.SaveChangesAsync();
        TempData["Mesaj"] = $"{alet.Kod} kalibre edildi. Sonraki kalibrasyon: {alet.SonrakiKalibrasyon:dd.MM.yyyy}";
        TempData["Efekt"] = "basari";
        return RedirectToAction(nameof(Kalibrasyon));
    }
}

public class NcrController : Controller
{
    private readonly UretimDbContext _db;
    private readonly UretimServisi _uretim;
    private readonly BildirimServisi _bildirim;

    public NcrController(UretimDbContext db, UretimServisi uretim, BildirimServisi bildirim)
    {
        _db = db;
        _uretim = uretim;
        _bildirim = bildirim;
    }

    public async Task<IActionResult> Index(string? durum)
    {
        var tumu = await _db.Ncrler.Include(n => n.UretimEmri!).ThenInclude(e => e.Urun).Include(n => n.Parca)
            .OrderByDescending(n => n.AcilisTarihi).ToListAsync();
        var model = new NcrListeModel
        {
            Durum = durum,
            Ncrler = string.IsNullOrEmpty(durum) ? tumu : tumu.Where(n => n.Durum == durum).ToList(),
            Sayilar = tumu.GroupBy(n => n.Durum).ToDictionary(g => g.Key, g => g.Count())
        };
        return View(model);
    }

    public async Task<IActionResult> Detay(int id)
    {
        var ncr = await _db.Ncrler.Include(n => n.UretimEmri!).ThenInclude(e => e.Urun).Include(n => n.Parca)
            .FirstOrDefaultAsync(n => n.Id == id);
        if (ncr == null) return NotFound();

        var model = new NcrDetayModel
        {
            Ncr = ncr,
            Olcumler = ncr.ParcaId == null ? new() : await _db.Olcumler.Include(o => o.Karakteristik).Include(o => o.OlcumAleti).Include(o => o.Kullanici)
                .Where(o => o.ParcaId == ncr.ParcaId).OrderBy(o => o.Zaman).ToListAsync(),
            Gecmis = await _db.AuditKayitlari.Where(a => a.Tablo == "Ncr" && a.KayitId == id.ToString())
                .OrderBy(a => a.Zaman).ToListAsync(),
            Imzalar = (await _db.Imzalar.Where(i => i.Belge == "NCR" && i.BelgeId == id).OrderBy(i => i.Zaman).ToListAsync())
                .Select(i => new ImzaSatiri(i, NcrImza.Gecerli(i, ncr))).ToList()
        };
        return View(model);
    }

    // İmza öncesi kimlik doğrulama: şifre yeniden istenir
    private async Task<(Kullanici? Kullanici, string? Hata)> ImzaDogrulaAsync(string? imza, string? sifre)
    {
        if (!ImzaHesaplayici.YolGecerliMi(imza)) return (null, "İmza alanına imzanızı çizin.");
        var kimlik = User.KullaniciId();
        var k = await _db.Kullanicilar.FirstOrDefaultAsync(x => x.Id == kimlik);
        if (k == null || string.IsNullOrEmpty(sifre)) return (null, "İmzalamak için şifrenizi girin.");
        var sonuc = new PasswordHasher<Kullanici>().VerifyHashedPassword(k, k.SifreHash, sifre);
        if (sonuc == PasswordVerificationResult.Failed) return (null, "Şifre hatalı, imza atılmadı.");
        return (k, null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Kalite,Yonetici,Admin")]
    public async Task<IActionResult> Karar(int id, string karar)
    {
        var hata = await _uretim.NcrKararAsync(id, karar, User.AdSoyad());
        if (hata != null)
        {
            TempData["Hata"] = hata;
            TempData["Efekt"] = "hata";
        }
        else
        {
            TempData["Mesaj"] = $"Karar kaydedildi: {karar}.";
            TempData["Efekt"] = "basari";
        }
        return RedirectToAction(nameof(Detay), new { id });
    }

    // 1. imza: kalite düzeltici faaliyeti yazar, imzalar ve onaya gönderir
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Kalite,Admin")]
    public async Task<IActionResult> Kapat(int id, string? duzelticiFaaliyet, string? imza, string? sifre)
    {
        var ncr = await _db.Ncrler.FirstOrDefaultAsync(n => n.Id == id);
        if (ncr == null) return NotFound();
        if (ncr.Durum != NcrDurum.KararVerildi)
        {
            TempData["Hata"] = "NCR kapatılmadan önce karar verilmeli.";
            return RedirectToAction(nameof(Detay), new { id });
        }
        if (string.IsNullOrWhiteSpace(duzelticiFaaliyet) || duzelticiFaaliyet.Trim().Length < 10)
        {
            TempData["Hata"] = "Düzeltici faaliyeti açıklayın (en az 10 karakter).";
            return RedirectToAction(nameof(Detay), new { id });
        }
        var (kullanici, hata) = await ImzaDogrulaAsync(imza, sifre);
        if (kullanici == null)
        {
            TempData["Hata"] = hata;
            TempData["Efekt"] = "hata";
            return RedirectToAction(nameof(Detay), new { id });
        }

        ncr.DuzelticiFaaliyet = duzelticiFaaliyet.Trim();
        ncr.Durum = NcrDurum.OnayBekliyor;
        await _db.SaveChangesAsync();
        _db.Imzalar.Add(NcrImza.Olustur(ncr, "Hazırlayan", kullanici, imza!, DateTime.Now));
        await _db.SaveChangesAsync();

        await _bildirim.GonderAsync($"{ncr.NcrNo} onayınızı bekliyor", $"{kullanici.AdSoyad} düzeltici faaliyeti imzaladı. Yönetici onayı gerekli.",
            "uyari", $"/ncr/detay/{ncr.Id}", Roller.Yonetici, Roller.Admin);
        TempData["Mesaj"] = $"{ncr.NcrNo} imzalandı ve yönetici onayına gönderildi.";
        TempData["Efekt"] = "basari";
        return RedirectToAction(nameof(Detay), new { id });
    }

    // 2. imza: yönetici onaylar, NCR kapanır (dört göz ilkesi: hazırlayan onaylayamaz)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Yonetici,Admin")]
    public async Task<IActionResult> Onayla(int id, string? imza, string? sifre)
    {
        var ncr = await _db.Ncrler.FirstOrDefaultAsync(n => n.Id == id);
        if (ncr == null) return NotFound();
        if (ncr.Durum != NcrDurum.OnayBekliyor)
        {
            TempData["Hata"] = "Bu NCR onay beklemiyor.";
            return RedirectToAction(nameof(Detay), new { id });
        }
        var hazirlayanId = await _db.Imzalar.Where(i => i.Belge == "NCR" && i.BelgeId == id && i.Rol == "Hazırlayan")
            .Select(i => (int?)i.KullaniciId).FirstOrDefaultAsync();
        if (hazirlayanId == User.KullaniciId())
        {
            TempData["Hata"] = "Dört göz ilkesi: NCR'ı hazırlayan kişi onaylayamaz.";
            return RedirectToAction(nameof(Detay), new { id });
        }
        var (kullanici, hata) = await ImzaDogrulaAsync(imza, sifre);
        if (kullanici == null)
        {
            TempData["Hata"] = hata;
            TempData["Efekt"] = "hata";
            return RedirectToAction(nameof(Detay), new { id });
        }

        ncr.Durum = NcrDurum.Kapali;
        ncr.KapanisTarihi = DateTime.Now;
        await _db.SaveChangesAsync();
        _db.Imzalar.Add(NcrImza.Olustur(ncr, "Onaylayan", kullanici, imza!, DateTime.Now));
        await _db.SaveChangesAsync();

        TempData["Mesaj"] = $"{ncr.NcrNo} onaylandı ve kapatıldı. PDF raporunu indirebilirsiniz.";
        TempData["Efekt"] = "konfeti";
        return RedirectToAction(nameof(Detay), new { id });
    }
}

[Authorize(Roles = "DepoBakim,Yonetici,Admin")]
public class DepoController : Controller
{
    private readonly UretimDbContext _db;
    private readonly BildirimServisi _bildirim;

    public DepoController(UretimDbContext db, BildirimServisi bildirim)
    {
        _db = db;
        _bildirim = bildirim;
    }

    public async Task<IActionResult> Index()
    {
        var malzemeler = await _db.Malzemeler.OrderBy(m => m.Ad).ToListAsync();
        var lotlar = await _db.HammaddeLotlari.Include(l => l.Malzeme).OrderByDescending(l => l.GirisTarihi).ToListAsync();
        var otuzGun = DateTime.Today.AddDays(-30);
        var tuketimler = await _db.HammaddeTuketimleri.Include(t => t.HammaddeLotu)
            .Where(t => t.Zaman >= otuzGun).ToListAsync();

        var model = new DepoModel
        {
            Malzemeler = malzemeler,
            Lotlar = lotlar,
            Stoklar = malzemeler.Select(m => new StokSatiri(
                m,
                lotlar.Where(l => l.MalzemeId == m.Id).Sum(l => l.KalanMiktar),
                tuketimler.Where(t => t.HammaddeLotu!.MalzemeId == m.Id).Sum(t => t.Miktar),
                lotlar.Count(l => l.MalzemeId == m.Id && l.KalanMiktar > 0))).ToList(),
            SonHareketler = await _db.HammaddeTuketimleri.Include(t => t.HammaddeLotu).Include(t => t.UretimEmri)
                .OrderByDescending(t => t.Zaman).Take(10).ToListAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "DepoBakim,Admin")]
    public async Task<IActionResult> Giris(int malzemeId, string? miktar, string? tedarikci, string? sertifikaNo)
    {
        if (!Sayi.Coz(miktar, out var m) || m <= 0 || m > 100000)
        {
            TempData["Hata"] = "Geçerli bir miktar girin.";
            return RedirectToAction(nameof(Index));
        }
        var malzeme = await _db.Malzemeler.FirstOrDefaultAsync(x => x.Id == malzemeId);
        if (malzeme == null || string.IsNullOrWhiteSpace(tedarikci))
        {
            TempData["Hata"] = "Malzeme ve tedarikçi zorunlu.";
            return RedirectToAction(nameof(Index));
        }

        var simdi = DateTime.Now;
        var nolar = await _db.HammaddeLotlari.Select(l => l.LotNo).ToListAsync();
        var lot = new HammaddeLotu
        {
            LotNo = Numara.HammaddeLotNo(simdi, Numara.Siradaki(nolar)),
            MalzemeId = malzemeId,
            Tedarikci = tedarikci.Trim(),
            SertifikaNo = string.IsNullOrWhiteSpace(sertifikaNo) ? "-" : sertifikaNo.Trim(),
            GirisTarihi = simdi,
            BaslangicMiktar = Math.Round(m, 3),
            KalanMiktar = Math.Round(m, 3)
        };
        _db.HammaddeLotlari.Add(lot);
        await _db.SaveChangesAsync();
        await _bildirim.GonderAsync("Hammadde girişi", $"{lot.LotNo} · {malzeme.Ad} {lot.BaslangicMiktar:N0} kg depoya alındı.",
            "bilgi", "/depo", Roller.DepoBakim, Roller.Yonetici);

        TempData["Mesaj"] = $"{lot.LotNo} depoya alındı. Etiketi yazdırabilirsiniz.";
        TempData["Efekt"] = "basari";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Etiket(int id)
    {
        var lot = await _db.HammaddeLotlari.Include(l => l.Malzeme).FirstOrDefaultAsync(l => l.Id == id);
        if (lot == null) return NotFound();
        return View(lot);
    }
}

public class BakimController : Controller
{
    private readonly UretimDbContext _db;
    private readonly UyariServisi _uyari;
    private readonly BildirimServisi _bildirim;

    public BakimController(UretimDbContext db, UyariServisi uyari, BildirimServisi bildirim)
    {
        _db = db;
        _uyari = uyari;
        _bildirim = bildirim;
    }

    public async Task<IActionResult> Index()
    {
        var spc = await _uyari.SpcOzetleriAsync();
        var model = new BakimModel
        {
            Makineler = await _uyari.BakimDurumlariAsync(),
            SonKayitlar = await _db.BakimKayitlari.Include(b => b.Makine).OrderByDescending(b => b.Tarih).Take(12).ToListAsync(),
            SpcUyarilari = spc.Where(s => s.Sonuc.Ihlaller.Any(i => i.Indeks >= s.Degerler.Count - 3)).ToList()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "DepoBakim,Admin")]
    public async Task<IActionResult> Kaydet(int makineId, string tur, string? aciklama, int sureDakika)
    {
        var makine = await _db.Makineler.FirstOrDefaultAsync(m => m.Id == makineId);
        if (makine == null) return NotFound();

        _db.BakimKayitlari.Add(new BakimKaydi
        {
            MakineId = makineId,
            Tur = tur == "Arıza onarımı" ? "Arıza onarımı" : "Periyodik",
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? "Bakım yapıldı" : aciklama.Trim(),
            Tarih = DateTime.Now,
            Yapan = User.AdSoyad(),
            SureDakika = Math.Clamp(sureDakika, 0, 1440)
        });
        makine.SonBakimTarihi = DateTime.Now;
        makine.Durum = MakineDurum.Bekliyor;
        foreach (var d in await _db.DurusKayitlari.Where(d => d.MakineId == makineId && d.Bitis == null).ToListAsync())
            d.Bitis = DateTime.Now;
        await _db.SaveChangesAsync();

        await _bildirim.GonderAsync($"{makine.Kod} bakımı tamamlandı", $"{makine.Ad} yeniden üretime hazır.", "bilgi", "/bakim",
            Roller.Operator, Roller.Yonetici, Roller.DepoBakim);
        TempData["Mesaj"] = $"{makine.Kod} bakımı kaydedildi, sayaçlar sıfırlandı.";
        TempData["Efekt"] = "basari";
        return RedirectToAction(nameof(Index));
    }

    // Operatörler de arıza bildirebilir
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArizaBildir(int makineId, string? aciklama, string? donus)
    {
        var makine = await _db.Makineler.FirstOrDefaultAsync(m => m.Id == makineId);
        if (makine == null) return NotFound();

        makine.Durum = MakineDurum.Arizali;
        if (!await _db.DurusKayitlari.AnyAsync(d => d.MakineId == makineId && d.Bitis == null))
            _db.DurusKayitlari.Add(new DurusKaydi
            {
                MakineId = makineId, Neden = DurusNedeni.Ariza, Baslangic = DateTime.Now, BildirenKisi = User.AdSoyad(),
                Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
            });
        await _db.SaveChangesAsync();
        await _bildirim.GonderAsync($"{makine.Kod} arıza bildirimi",
            $"{makine.Ad}: {(string.IsNullOrWhiteSpace(aciklama) ? "Arıza" : aciklama.Trim())} ({User.AdSoyad()})",
            "kritik", "/bakim", Roller.DepoBakim, Roller.Yonetici, Roller.Admin);

        TempData["Mesaj"] = $"{makine.Kod} arızası bakım ekibine bildirildi.";
        TempData["Efekt"] = "alarm";
        if (!string.IsNullOrEmpty(donus) && Url.IsLocalUrl(donus)) return Redirect(donus);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "DepoBakim,Admin")]
    public async Task<IActionResult> DurumDegistir(int makineId, string durum)
    {
        var makine = await _db.Makineler.FirstOrDefaultAsync(m => m.Id == makineId);
        if (makine == null) return NotFound();
        var gecerli = new[] { MakineDurum.Bekliyor, MakineDurum.Bakimda, MakineDurum.Arizali, MakineDurum.Calisiyor };
        if (gecerli.Contains(durum))
        {
            makine.Durum = durum;
            await _db.SaveChangesAsync();
            TempData["Mesaj"] = $"{makine.Kod} durumu: {durum}";
        }
        return RedirectToAction(nameof(Index));
    }
}
