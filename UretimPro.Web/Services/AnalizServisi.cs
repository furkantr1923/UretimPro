using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;

namespace UretimPro.Services;

public record MakineOee(Makine Makine, OeeGirdi Girdi, OeeSonuc Sonuc, int DurusSayisi)
{
    public bool Planli => Girdi.PlanliDakika > 0;
}

public record AramaOnerisi(string Tur, string Baslik, string Alt, string Link);

// Soy ağacındaki bir düğüm (hammadde → lot → rota → parça → NCR)
public class AgacDugum
{
    public string Ikon { get; set; } = "bilgi";
    public string Baslik { get; set; } = "";
    public string? Alt { get; set; }
    public string? Rozet { get; set; }
    public string RozetSinif { get; set; } = "gri";
    public string? Link { get; set; }
    public bool Mono { get; set; }
    public int Derinlik { get; set; }
    public List<AgacDugum> Cocuklar { get; set; } = new();

    public int ToplamDugum => 1 + Cocuklar.Sum(c => c.ToplamDugum);
}

// OEE, duruş, Pareto, soy ağacı ve arama önerileri
public class AnalizServisi
{
    private readonly UretimDbContext _db;
    public AnalizServisi(UretimDbContext db) => _db = db;

    // ---------- OEE ----------

    // Planlı süre: makinenin aralıktaki ilk faaliyetinden son faaliyetine kadar (duruşlar dahil)
    public async Task<List<MakineOee>> OeeAsync(DateTime bas, DateTime bit)
    {
        var simdi = DateTime.Now;
        var son = bit < simdi ? bit : simdi;
        var makineler = await _db.Makineler.OrderBy(m => m.Kod).ToListAsync();
        if (son <= bas)
            return makineler.Select(m => new MakineOee(m, new OeeGirdi(0, 0, 0, 0, m.IdealCevrimSn), OeeSonuc.Bos, 0)).ToList();

        var islemler = await _db.IslemKayitlari.Where(i => i.Zaman >= bas && i.Zaman < son)
            .Select(i => new { i.MakineId, i.Zaman }).ToListAsync();
        var redler = await _db.Olcumler.Where(o => !o.Uygun && o.Zaman >= bas && o.Zaman < son)
            .Select(o => new { o.MakineId, o.ParcaId }).Distinct().ToListAsync();
        var duruslar = await _db.DurusKayitlari.Where(d => d.Baslangic < son && (d.Bitis == null || d.Bitis > bas)).ToListAsync();

        var sonuc = new List<MakineOee>();
        foreach (var m in makineler)
        {
            var ops = islemler.Where(i => i.MakineId == m.Id).Select(i => i.Zaman).ToList();
            var ds = duruslar.Where(d => d.MakineId == m.Id).ToList();
            var durusDk = ds.Sum(d => OeeHesaplayici.Cakisma(d.Baslangic, d.Bitis ?? simdi, bas, son));

            if (ops.Count == 0 && durusDk <= 0)
            {
                sonuc.Add(new MakineOee(m, new OeeGirdi(0, 0, 0, 0, m.IdealCevrimSn), OeeSonuc.Bos, 0));
                continue;
            }

            var baslangiclar = ops.Concat(ds.Select(d => d.Baslangic > bas ? d.Baslangic : bas)).ToList();
            var bitisler = ops.Select(t => t.AddSeconds(m.IdealCevrimSn))
                .Concat(ds.Select(d => (d.Bitis ?? simdi) < son ? (d.Bitis ?? simdi) : son)).ToList();
            var ilk = baslangiclar.Min();
            var enSon = bitisler.Max();
            if (enSon > son) enSon = son;

            var planli = Math.Max(durusDk, (enSon - ilk).TotalMinutes);
            var red = redler.Count(r => r.MakineId == m.Id);
            var girdi = new OeeGirdi(planli, durusDk, ops.Count + red, ops.Count, m.IdealCevrimSn);
            sonuc.Add(new MakineOee(m, girdi, OeeHesaplayici.Hesapla(girdi), ds.Count));
        }
        return sonuc;
    }

    public async Task<OeeSonuc> ToplamOeeAsync(DateTime bas, DateTime bit) =>
        OeeHesaplayici.Topla((await OeeAsync(bas, bit)).Select(x => x.Girdi));

    // Son n günün günlük toplam OEE'si (%)
    public async Task<List<GunlukDeger>> GunlukOeeAsync(int gun)
    {
        var liste = new List<GunlukDeger>();
        var bugun = DateTime.Today;
        for (var i = gun - 1; i >= 0; i--)
        {
            var g = bugun.AddDays(-i);
            if (g.DayOfWeek == DayOfWeek.Sunday && i > 0) continue;
            var oee = await ToplamOeeAsync(g, g.AddDays(1));
            liste.Add(new GunlukDeger(g.ToString("dd MMM"), Math.Round(oee.Oee * 100, 1)));
        }
        return liste;
    }

    // ---------- Duruş / Pareto ----------

    public async Task<List<DurusKaydi>> DuruslarAsync(DateTime bas, DateTime bit) =>
        await _db.DurusKayitlari.Include(d => d.Makine)
            .Where(d => d.Baslangic < bit && (d.Bitis == null || d.Bitis > bas))
            .OrderByDescending(d => d.Baslangic).ToListAsync();

    public async Task<List<ParetoSatiri>> ParetoAsync(DateTime bas, DateTime bit)
    {
        var simdi = DateTime.Now;
        var son = bit < simdi ? bit : simdi;
        var duruslar = await DuruslarAsync(bas, son);
        return ParetoHesaplayici.Hesapla(duruslar.Select(d => (d.Neden, Math.Round(OeeHesaplayici.Cakisma(d.Baslangic, d.Bitis ?? simdi, bas, son)))));
    }

    // ---------- Soy ağacı ----------

    public async Task<AgacDugum?> LotAgaciAsync(string lotNo)
    {
        var emir = await _db.UretimEmirleri.Include(e => e.Urun).Include(e => e.Parcalar)
            .FirstOrDefaultAsync(e => e.LotNo == lotNo || e.EmirNo == lotNo);
        if (emir == null) return null;

        var kok = new AgacDugum
        {
            Ikon = "uretim", Baslik = emir.LotNo, Mono = true,
            Alt = $"{emir.Urun?.Ad} · {emir.Miktar} adet · {emir.EmirNo}",
            Rozet = emir.Durum, RozetSinif = Gorunum.EmirSinif(emir.Durum),
            Link = $"/izlenebilirlik/lot?lotNo={emir.LotNo}"
        };

        // Hammadde ve tedarikçi
        var tuketimler = await _db.HammaddeTuketimleri.Include(t => t.HammaddeLotu!).ThenInclude(l => l.Malzeme)
            .Where(t => t.UretimEmriId == emir.Id).ToListAsync();
        var hammadde = new AgacDugum { Ikon = "depo", Baslik = "Hammadde", Alt = $"{tuketimler.Count} lot · {tuketimler.Sum(t => t.Miktar):N1} kg" };
        foreach (var t in tuketimler)
        {
            var l = t.HammaddeLotu!;
            hammadde.Cocuklar.Add(new AgacDugum
            {
                Ikon = "depo", Baslik = l.LotNo, Mono = true, Alt = $"{l.Malzeme?.Ad} · {t.Miktar:N1} kg",
                Link = $"/izlenebilirlik/agac?hammadde={l.LotNo}",
                Cocuklar = { new AgacDugum { Ikon = "fabrika", Baslik = l.Tedarikci, Alt = $"Sertifika {l.SertifikaNo} · {l.GirisTarihi:dd.MM.yyyy}" } }
            });
        }
        kok.Cocuklar.Add(hammadde);

        // Rota: her adımda hangi makine, hangi operatörler
        var adimlar = await _db.RotaAdimlari.Include(a => a.Makine).Where(a => a.UrunId == emir.UrunId).OrderBy(a => a.Sira).ToListAsync();
        var parcaIdleri = emir.Parcalar.Select(p => p.Id).ToList();
        var islemler = await _db.IslemKayitlari.Include(i => i.Operator)
            .Where(i => parcaIdleri.Contains(i.ParcaId)).Select(i => new { i.RotaAdimiId, Ad = i.Operator!.AdSoyad }).ToListAsync();
        var rota = new AgacDugum { Ikon = "istasyon", Baslik = "Rota", Alt = $"{adimlar.Count} adım" };
        foreach (var a in adimlar)
        {
            var adimIslem = islemler.Where(i => i.RotaAdimiId == a.Id).ToList();
            var dugum = new AgacDugum
            {
                Ikon = "bakim", Baslik = $"{a.Sira}. {a.Ad}", Alt = $"{a.Makine?.Kod} · {adimIslem.Count} işlem",
                Rozet = adimIslem.Count == 0 ? "Bekliyor" : adimIslem.Count >= emir.Miktar ? "Tamam" : "Sürüyor",
                RozetSinif = adimIslem.Count == 0 ? "gri" : adimIslem.Count >= emir.Miktar ? "yesil" : "mavi"
            };
            foreach (var op in adimIslem.GroupBy(i => i.Ad).OrderByDescending(g => g.Count()))
                dugum.Cocuklar.Add(new AgacDugum { Ikon = "kullanicilar", Baslik = op.Key, Alt = $"{op.Count()} parça" });
            rota.Cocuklar.Add(dugum);
        }
        kok.Cocuklar.Add(rota);

        // Parçalar durumlarına göre
        var parcalar = new AgacDugum { Ikon = "urun", Baslik = "Parçalar", Alt = $"{emir.Parcalar.Count} seri no" };
        foreach (var g in emir.Parcalar.GroupBy(p => p.Durum).OrderByDescending(g => g.Count()))
        {
            var grup = new AgacDugum { Ikon = "urun", Baslik = g.Key, Rozet = g.Count().ToString(), RozetSinif = Gorunum.ParcaSinif(g.Key) };
            var sirali = g.OrderBy(p => p.SeriNo).ToList();
            foreach (var p in sirali.Take(5))
                grup.Cocuklar.Add(new AgacDugum { Ikon = "qr", Baslik = p.SeriNo, Mono = true, Link = $"/izlenebilirlik/parca?seriNo={p.SeriNo}" });
            if (sirali.Count > 5)
                grup.Cocuklar.Add(new AgacDugum { Ikon = "arti", Baslik = $"+{sirali.Count - 5} parça daha", Link = $"/izlenebilirlik/lot?lotNo={emir.LotNo}" });
            parcalar.Cocuklar.Add(grup);
        }
        kok.Cocuklar.Add(parcalar);

        // Uygunsuzluklar
        var ncrler = await _db.Ncrler.Include(n => n.Parca).Where(n => n.UretimEmriId == emir.Id).ToListAsync();
        var ncrDugum = new AgacDugum { Ikon = "ncr", Baslik = "Uygunsuzluk", Alt = ncrler.Count == 0 ? "Kayıt yok" : $"{ncrler.Count} NCR" };
        if (ncrler.Count == 0) ncrDugum.Rozet = "Temiz";
        ncrDugum.RozetSinif = ncrler.Count == 0 ? "yesil" : "kirmizi";
        foreach (var n in ncrler)
            ncrDugum.Cocuklar.Add(new AgacDugum
            {
                Ikon = "ncr", Baslik = n.NcrNo, Mono = true, Alt = n.Karar ?? n.Parca?.SeriNo,
                Rozet = n.Durum, RozetSinif = Gorunum.NcrSinif(n.Durum), Link = $"/ncr/detay/{n.Id}"
            });
        kok.Cocuklar.Add(ncrDugum);

        DerinlikVer(kok, 0);
        return kok;
    }

    // Geri çağırma: bir hammadde lotundan çıkan tüm üretim lotları
    public async Task<AgacDugum?> HammaddeAgaciAsync(string lotNo)
    {
        var lot = await _db.HammaddeLotlari.Include(l => l.Malzeme).FirstOrDefaultAsync(l => l.LotNo == lotNo);
        if (lot == null) return null;

        var kok = new AgacDugum
        {
            Ikon = "depo", Baslik = lot.LotNo, Mono = true,
            Alt = $"{lot.Malzeme?.Ad} · {lot.Tedarikci} · {lot.BaslangicMiktar:N0} kg",
            Link = $"/izlenebilirlik/hammadde?lotNo={lot.LotNo}"
        };

        var tuketimler = await _db.HammaddeTuketimleri.Include(t => t.UretimEmri!).ThenInclude(e => e.Urun)
            .Where(t => t.HammaddeLotuId == lot.Id).OrderBy(t => t.Zaman).ToListAsync();
        var emirIdleri = tuketimler.Select(t => t.UretimEmriId).Distinct().ToList();
        var durumlar = await _db.Parcalar.Where(p => emirIdleri.Contains(p.UretimEmriId))
            .GroupBy(p => new { p.UretimEmriId, p.Durum }).Select(g => new { g.Key.UretimEmriId, g.Key.Durum, Adet = g.Count() }).ToListAsync();
        var ncrler = await _db.Ncrler.Where(n => emirIdleri.Contains(n.UretimEmriId)).ToListAsync();

        foreach (var t in tuketimler)
        {
            var e = t.UretimEmri!;
            var dugum = new AgacDugum
            {
                Ikon = "uretim", Baslik = e.LotNo, Mono = true, Alt = $"{e.Urun?.Ad} · {t.Miktar:N1} kg",
                Rozet = e.Durum, RozetSinif = Gorunum.EmirSinif(e.Durum), Link = $"/izlenebilirlik/agac?lot={e.LotNo}"
            };
            foreach (var d in durumlar.Where(x => x.UretimEmriId == e.Id).OrderByDescending(x => x.Adet))
                dugum.Cocuklar.Add(new AgacDugum { Ikon = "urun", Baslik = d.Durum, Rozet = d.Adet.ToString(), RozetSinif = Gorunum.ParcaSinif(d.Durum) });
            foreach (var n in ncrler.Where(n => n.UretimEmriId == e.Id))
                dugum.Cocuklar.Add(new AgacDugum { Ikon = "ncr", Baslik = n.NcrNo, Mono = true, Rozet = n.Durum, RozetSinif = Gorunum.NcrSinif(n.Durum), Link = $"/ncr/detay/{n.Id}" });
            kok.Cocuklar.Add(dugum);
        }

        DerinlikVer(kok, 0);
        return kok;
    }

    private static void DerinlikVer(AgacDugum d, int derinlik)
    {
        d.Derinlik = derinlik;
        foreach (var c in d.Cocuklar) DerinlikVer(c, derinlik + 1);
    }

    // ---------- Ctrl+K arama önerileri ----------

    public async Task<List<AramaOnerisi>> OnerilerAsync(string q, string rol)
    {
        var a = (q ?? "").Trim().ToUpperInvariant();
        var liste = new List<AramaOnerisi>();
        if (a.Length < 2) return liste;

        liste.AddRange(await _db.Parcalar.Where(p => p.SeriNo.StartsWith(a)).OrderBy(p => p.SeriNo).Take(4)
            .Select(p => new AramaOnerisi("Parça", p.SeriNo, p.Durum, "/izlenebilirlik/parca?seriNo=" + p.SeriNo)).ToListAsync());
        liste.AddRange(await _db.UretimEmirleri.Include(e => e.Urun).Where(e => e.LotNo.Contains(a) || e.EmirNo.Contains(a))
            .OrderByDescending(e => e.Id).Take(4)
            .Select(e => new AramaOnerisi("Lot", e.LotNo, e.EmirNo + " · " + e.Urun!.Ad + " · " + e.Durum, "/izlenebilirlik/lot?lotNo=" + e.LotNo)).ToListAsync());
        liste.AddRange(await _db.HammaddeLotlari.Include(l => l.Malzeme).Where(l => l.LotNo.Contains(a))
            .OrderByDescending(l => l.Id).Take(3)
            .Select(l => new AramaOnerisi("Hammadde", l.LotNo, l.Malzeme!.Ad + " · " + l.Tedarikci, "/izlenebilirlik/hammadde?lotNo=" + l.LotNo)).ToListAsync());
        liste.AddRange(await _db.Ncrler.Where(n => n.NcrNo.Contains(a)).OrderByDescending(n => n.Id).Take(3)
            .Select(n => new AramaOnerisi("NCR", n.NcrNo, n.Durum, "/ncr/detay/" + n.Id)).ToListAsync());

        var makineler = await _db.Makineler.ToListAsync();
        liste.AddRange(makineler
            .Where(m => m.Kod.Contains(a) || m.Ad.ToUpperInvariant().Contains(a))
            .Take(3)
            .Select(m => new AramaOnerisi("Makine", m.Kod, m.Ad + " · " + m.Durum,
                rol == Roller.Operator ? "/istasyon/ekran/" + m.Id : "/fabrika?makine=" + m.Kod)));
        return liste;
    }
}
