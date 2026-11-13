using Microsoft.AspNetCore.Html;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using UretimPro.Data;
using UretimPro.Models;

namespace UretimPro.Services;

public record Uyari(string Seviye, string Baslik, string Mesaj, string? Link, string Ikon);
public record MakineBakimSatiri(Makine Makine, BakimDurumu Durum, int Adet);
public record SpcOzet(KaliteKarakteristigi Karakteristik, Makine Makine, SpcSonuc Sonuc, List<double> Degerler);

// Kontrol merkezindeki uyarıları ve özetleri hesaplar
public class UyariServisi
{
    private readonly UretimDbContext _db;
    public UyariServisi(UretimDbContext db) => _db = db;

    public async Task<List<MakineBakimSatiri>> BakimDurumlariAsync()
    {
        var simdi = DateTime.Now;
        var makineler = await _db.Makineler.OrderBy(m => m.Kod).ToListAsync();
        var liste = new List<MakineBakimSatiri>();
        foreach (var m in makineler)
        {
            var adet = await _db.IslemKayitlari.CountAsync(i => i.MakineId == m.Id && i.Zaman > m.SonBakimTarihi);
            liste.Add(new MakineBakimSatiri(m, BakimHesaplayici.Hesapla(m.SonBakimTarihi, m.BakimPeriyotGun, m.BakimPeriyotAdet, adet, simdi), adet));
        }
        return liste;
    }

    public async Task<List<SpcOzet>> SpcOzetleriAsync(int sonKac = 30)
    {
        var karakteristikler = await _db.Karakteristikler
            .Include(k => k.Urun).Include(k => k.RotaAdimi!).ThenInclude(a => a.Makine)
            .ToListAsync();

        var liste = new List<SpcOzet>();
        foreach (var k in karakteristikler)
        {
            var makine = k.RotaAdimi?.Makine;
            if (makine == null) continue;
            var degerler = await _db.Olcumler
                .Where(o => o.KarakteristikId == k.Id && o.MakineId == makine.Id)
                .OrderByDescending(o => o.Zaman).Take(sonKac)
                .Select(o => o.Deger).ToListAsync();
            degerler.Reverse();
            if (degerler.Count < 2) continue;
            liste.Add(new SpcOzet(k, makine, SpcHesaplayici.Analiz(degerler, k.AltSinir, k.UstSinir), degerler));
        }
        return liste;
    }

    public async Task<List<Uyari>> HepsiAsync()
    {
        var simdi = DateTime.Now;
        var uyarilar = new List<Uyari>();

        // Açık uygunsuzluklar
        var acikNcrler = await _db.Ncrler.Include(n => n.UretimEmri)
            .Where(n => n.Durum == NcrDurum.Acik).OrderBy(n => n.AcilisTarihi).ToListAsync();
        foreach (var n in acikNcrler)
        {
            var saat = (int)(simdi - n.AcilisTarihi).TotalHours;
            uyarilar.Add(new Uyari(saat >= 48 ? "kritik" : "uyari", $"{n.NcrNo} açık · {saat} saat",
                $"Lot {n.UretimEmri?.LotNo} bloke, karar bekliyor", $"/ncr/detay/{n.Id}", "ncr"));
        }

        // SPC erken uyarıları (son 3 ölçümde ihlal)
        foreach (var s in await SpcOzetleriAsync())
        {
            var ihlal = s.Sonuc.Ihlaller.Where(i => i.Indeks >= s.Degerler.Count - 3).OrderBy(i => i.Kural).FirstOrDefault();
            if (ihlal == null) continue;
            uyarilar.Add(new Uyari("uyari", $"{s.Makine.Kod} ayarı kayıyor",
                $"{s.Karakteristik.Urun?.Ad} · {s.Karakteristik.Ad}: {ihlal.Aciklama}",
                $"/kalite?karakteristikId={s.Karakteristik.Id}&makineId={s.Makine.Id}", "spc"));
        }

        // Arızalı makineler ve bakım planı
        foreach (var b in await BakimDurumlariAsync())
        {
            if (b.Makine.Durum == MakineDurum.Arizali)
                uyarilar.Add(new Uyari("kritik", $"{b.Makine.Kod} arızalı", $"{b.Makine.Ad} üretim dışı", "/bakim", "bakim"));
            else if (b.Durum.Seviye == BakimSeviye.Gecikti)
                uyarilar.Add(new Uyari("kritik", $"{b.Makine.Kod} bakımı gecikti", $"Periyodik bakım süresi doldu", "/bakim", "bakim"));
            else if (b.Durum.Seviye == BakimSeviye.Yaklasiyor)
                uyarilar.Add(new Uyari("uyari", $"{b.Makine.Kod} bakımı yaklaşıyor",
                    $"{Math.Max(0, b.Durum.KalanGun)} gün ya da {Math.Max(0, b.Durum.KalanAdet)} işlem kaldı", "/bakim", "bakim"));
        }

        // Kritik stok
        var malzemeler = await _db.Malzemeler.ToListAsync();
        foreach (var m in malzemeler)
        {
            var kalan = await _db.HammaddeLotlari.Where(l => l.MalzemeId == m.Id).SumAsync(l => l.KalanMiktar);
            if (kalan < m.MinimumStok)
                uyarilar.Add(new Uyari("uyari", $"Kritik stok: {m.Ad}", $"Kalan {kalan:N0} {m.Birim} · minimum {m.MinimumStok:N0} {m.Birim}", "/depo", "depo"));
        }

        // Kalibrasyon
        foreach (var a in await _db.OlcumAletleri.ToListAsync())
        {
            var kalan = (a.SonrakiKalibrasyon - simdi).TotalDays;
            if (kalan < 0)
                uyarilar.Add(new Uyari("uyari", $"{a.Kod} kalibrasyonu geçti", $"{a.Ad} ölçümde kullanılamaz", "/kalite/kalibrasyon", "kalibrasyon"));
            else if (kalan <= 14)
                uyarilar.Add(new Uyari("bilgi", $"{a.Kod} kalibrasyonu yaklaşıyor", $"{a.Ad} · {(int)Math.Ceiling(kalan)} gün kaldı", "/kalite/kalibrasyon", "kalibrasyon"));
        }

        var sira = new Dictionary<string, int> { ["kritik"] = 0, ["uyari"] = 1, ["bilgi"] = 2 };
        return uyarilar.OrderBy(u => sira[u.Seviye]).ToList();
    }
}

public static class QrServisi
{
    // QR kodu SVG olarak üretir (çevrimdışı çalışır)
    public static IHtmlContent Svg(string metin, int piksel = 4)
    {
        using var uretici = new QRCodeGenerator();
        using var veri = uretici.CreateQrCode(metin, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(veri).GetGraphic(piksel);
        return new HtmlString(svg);
    }
}
