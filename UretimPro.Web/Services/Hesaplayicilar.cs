using System.Globalization;

namespace UretimPro.Services;

// Veritabanından bağımsız, birim testli iş kuralları

// ---------- SPC (istatistiksel proses kontrolü) ----------

public record SpcIhlal(int Indeks, int Kural, string Aciklama);

public record SpcSonuc(
    double Ortalama,
    double Sigma,
    double UstKontrolSiniri,
    double AltKontrolSiniri,
    double? Cpk,
    IReadOnlyList<SpcIhlal> Ihlaller)
{
    // Son noktayı etkileyen bir ihlal var mı (yeni uyarı üretmek için)
    public bool SonNoktadaIhlalVar(int adet) => Ihlaller.Any(i => i.Indeks == adet - 1);
}

public static class SpcHesaplayici
{
    public static readonly Dictionary<int, string> KuralAdlari = new()
    {
        [1] = "Kontrol sınırı dışında nokta",
        [2] = "3 noktadan 2'si 2σ bölgesinde",
        [3] = "5 noktadan 4'ü 1σ bölgesinde",
        [4] = "8 nokta art arda ortalamanın aynı tarafında",
        [5] = "6 nokta art arda yükseliyor veya düşüyor"
    };

    // Western Electric kuralları (1-4) + trend kuralı (5)
    public static SpcSonuc Analiz(IReadOnlyList<double> degerler, double altTolerans, double ustTolerans)
    {
        if (degerler.Count == 0)
            return new SpcSonuc(0, 0, 0, 0, null, Array.Empty<SpcIhlal>());

        var ortalama = degerler.Average();
        var sigma = degerler.Count > 1
            ? Math.Sqrt(degerler.Sum(d => (d - ortalama) * (d - ortalama)) / (degerler.Count - 1))
            : 0;

        double? cpk = sigma > 0
            ? Math.Min(ustTolerans - ortalama, ortalama - altTolerans) / (3 * sigma)
            : null;

        var ihlaller = new List<SpcIhlal>();
        if (sigma <= 0)
            return new SpcSonuc(ortalama, 0, ortalama, ortalama, cpk, ihlaller);

        double Z(int i) => (degerler[i] - ortalama) / sigma;
        var eklenen = new HashSet<(int, int)>();
        void Ekle(int i, int kural)
        {
            if (eklenen.Add((i, kural))) ihlaller.Add(new SpcIhlal(i, kural, KuralAdlari[kural]));
        }

        for (var i = 0; i < degerler.Count; i++)
        {
            // Kural 1: 3σ dışı
            if (Math.Abs(Z(i)) > 3) Ekle(i, 1);

            // Kural 2: son 3 noktanın 2'si aynı tarafta 2σ dışı
            if (i >= 2)
            {
                foreach (var yon in new[] { 1, -1 })
                {
                    var say = Enumerable.Range(i - 2, 3).Count(j => yon * Z(j) > 2);
                    if (say >= 2 && yon * Z(i) > 2) Ekle(i, 2);
                }
            }

            // Kural 3: son 5 noktanın 4'ü aynı tarafta 1σ dışı
            if (i >= 4)
            {
                foreach (var yon in new[] { 1, -1 })
                {
                    var say = Enumerable.Range(i - 4, 5).Count(j => yon * Z(j) > 1);
                    if (say >= 4 && yon * Z(i) > 1) Ekle(i, 3);
                }
            }

            // Kural 4: 8 nokta art arda aynı tarafta
            if (i >= 7)
            {
                var pencere = Enumerable.Range(i - 7, 8).Select(Z).ToList();
                if (pencere.All(z => z > 0) || pencere.All(z => z < 0)) Ekle(i, 4);
            }

            // Kural 5: 6 nokta art arda artan veya azalan (trend)
            if (i >= 5)
            {
                var artan = true;
                var azalan = true;
                for (var j = i - 4; j <= i; j++)
                {
                    if (degerler[j] <= degerler[j - 1]) artan = false;
                    if (degerler[j] >= degerler[j - 1]) azalan = false;
                }
                if (artan || azalan) Ekle(i, 5);
            }
        }

        return new SpcSonuc(ortalama, sigma, ortalama + 3 * sigma, ortalama - 3 * sigma, cpk, ihlaller);
    }
}

// ---------- Bakım planı ----------

public enum BakimSeviye { Normal, Yaklasiyor, Gecikti }

public record BakimDurumu(BakimSeviye Seviye, double GunOrani, double AdetOrani, int KalanGun, int KalanAdet)
{
    public double EnYuksekOran => Math.Max(GunOrani, AdetOrani);
}

public static class BakimHesaplayici
{
    // Süre veya adet: hangisi önce dolarsa
    public static BakimDurumu Hesapla(DateTime sonBakim, int periyotGun, int periyotAdet, int sonBakimdanBeriAdet, DateTime simdi)
    {
        var gecenGun = (simdi - sonBakim).TotalDays;
        var gunOrani = periyotGun > 0 ? gecenGun / periyotGun : 0;
        var adetOrani = periyotAdet > 0 ? (double)sonBakimdanBeriAdet / periyotAdet : 0;
        var oran = Math.Max(gunOrani, adetOrani);

        var seviye = oran >= 1 ? BakimSeviye.Gecikti
                   : oran >= 0.85 ? BakimSeviye.Yaklasiyor
                   : BakimSeviye.Normal;

        var kalanGun = (int)Math.Ceiling(periyotGun - gecenGun);
        var kalanAdet = periyotAdet - sonBakimdanBeriAdet;
        return new BakimDurumu(seviye, gunOrani, adetOrani, kalanGun, kalanAdet);
    }
}

// ---------- FIFO hammadde seçimi ----------

public record LotStok(int LotId, DateTime GirisTarihi, double KalanMiktar);
public record LotPayi(int LotId, double Miktar);

public static class FifoSecici
{
    // İlk giren ilk çıkar: en eski lottan başlayarak ihtiyacı karşılar
    public static (List<LotPayi> Paylar, double Eksik) Sec(IEnumerable<LotStok> lotlar, double ihtiyac)
    {
        var paylar = new List<LotPayi>();
        var kalan = ihtiyac;
        foreach (var lot in lotlar.Where(l => l.KalanMiktar > 0).OrderBy(l => l.GirisTarihi).ThenBy(l => l.LotId))
        {
            if (kalan <= 0.0001) break;
            var al = Math.Min(lot.KalanMiktar, kalan);
            paylar.Add(new LotPayi(lot.LotId, Math.Round(al, 3)));
            kalan -= al;
        }
        return (paylar, Math.Max(0, Math.Round(kalan, 3)));
    }
}

// ---------- Numaralar ve sayılar ----------

public static class Numara
{
    public static string SeriNo(string urunKodu, int sira) => $"{urunKodu}-{sira:00000}";
    public static string EmirNo(int sira) => $"UE-{sira:0000}";
    public static string LotNo(DateTime tarih, int sira) => $"UL-{tarih:yyMM}-{sira:00}";
    public static string NcrNo(int sira) => $"NCR-{sira:0000}";
    public static string HammaddeLotNo(DateTime tarih, int sira) => $"HM-{tarih:yyyy}-{sira:000}";

    // "UE-0457" gibi numaralardan en büyüğünü bulup bir sonrakini verir
    public static int Siradaki(IEnumerable<string> numaralar)
    {
        var enBuyuk = 0;
        foreach (var no in numaralar)
        {
            var parca = no.Split('-').LastOrDefault();
            if (int.TryParse(parca, out var n) && n > enBuyuk) enBuyuk = n;
        }
        return enBuyuk + 1;
    }
}

public static class Sayi
{
    // "25,044" ve "25.044" ikisini de kabul eder (Türkçe klavye ve barkod okuyucu)
    public static bool Coz(string? metin, out double deger)
    {
        deger = 0;
        if (string.IsNullOrWhiteSpace(metin)) return false;
        var temiz = metin.Trim().Replace(" ", "").Replace(',', '.');
        if (temiz.Count(c => c == '.') > 1) return false;
        return double.TryParse(temiz, NumberStyles.Float, CultureInfo.InvariantCulture, out deger);
    }

    // CSS ve SVG içinde her zaman nokta kullanılır
    public static string Css(double deger) => deger.ToString("0.###", CultureInfo.InvariantCulture);
}

// ---------- OEE (Toplam Ekipman Etkinliği) ----------

public record OeeGirdi(double PlanliDakika, double DurusDakika, int UretilenAdet, int UygunAdet, double IdealCevrimSn);

public record OeeSonuc(double Kullanilabilirlik, double Performans, double Kalite, double Oee, double CalismaDakika)
{
    public static readonly OeeSonuc Bos = new(0, 0, 0, 0, 0);
}

public static class OeeHesaplayici
{
    // OEE = Kullanılabilirlik × Performans × Kalite
    public static OeeSonuc Hesapla(OeeGirdi g)
    {
        if (g.PlanliDakika <= 0) return OeeSonuc.Bos;
        var calisma = Math.Max(0, g.PlanliDakika - g.DurusDakika);
        var kullanilabilirlik = calisma / g.PlanliDakika;
        var performans = calisma > 0 ? Math.Min(1, g.UretilenAdet * g.IdealCevrimSn / 60.0 / calisma) : 0;
        var kalite = g.UretilenAdet > 0 ? Math.Clamp((double)g.UygunAdet / g.UretilenAdet, 0, 1) : 1;
        return new OeeSonuc(kullanilabilirlik, performans, kalite, kullanilabilirlik * performans * kalite, calisma);
    }

    // Birden fazla makinenin toplam OEE'si (süre ağırlıklı)
    public static OeeSonuc Topla(IEnumerable<OeeGirdi> girdiler)
    {
        var liste = girdiler.Where(g => g.PlanliDakika > 0).ToList();
        if (liste.Count == 0) return OeeSonuc.Bos;
        var planli = liste.Sum(g => g.PlanliDakika);
        var calisma = liste.Sum(g => Math.Max(0, g.PlanliDakika - g.DurusDakika));
        var idealDakika = liste.Sum(g => g.UretilenAdet * g.IdealCevrimSn / 60.0);
        var uretilen = liste.Sum(g => g.UretilenAdet);
        var uygun = liste.Sum(g => g.UygunAdet);

        var kullanilabilirlik = calisma / planli;
        var performans = calisma > 0 ? Math.Min(1, idealDakika / calisma) : 0;
        var kalite = uretilen > 0 ? Math.Clamp((double)uygun / uretilen, 0, 1) : 1;
        return new OeeSonuc(kullanilabilirlik, performans, kalite, kullanilabilirlik * performans * kalite, calisma);
    }

    // İki zaman aralığının kesişimi (dakika)
    public static double Cakisma(DateTime aBas, DateTime aBit, DateTime bBas, DateTime bBit)
    {
        var bas = aBas > bBas ? aBas : bBas;
        var bit = aBit < bBit ? aBit : bBit;
        return bit > bas ? (bit - bas).TotalMinutes : 0;
    }

    // Dünya standardı %85, tipik %60
    public static string Sinif(double oee) => oee >= 0.85 ? "yesil" : oee >= 0.60 ? "sari" : "kirmizi";
}

public static class VardiyaPlani
{
    public static readonly string[] Kodlar = { "A", "B", "C" };

    public static string Kod(DateTime t) => t.Hour switch
    {
        >= 8 and < 16 => "A",
        >= 16 => "B",
        _ => "C"
    };

    // A 08-16, B 16-24, C 00-08 (aynı takvim günü)
    public static (DateTime Baslangic, DateTime Bitis) Aralik(DateTime tarih, string kod)
    {
        var g = tarih.Date;
        return kod switch
        {
            "B" => (g.AddHours(16), g.AddDays(1)),
            "C" => (g, g.AddHours(8)),
            _ => (g.AddHours(8), g.AddHours(16))
        };
    }

    public static string Ad(string kod) => kod switch
    {
        "B" => "B · 16:00–24:00",
        "C" => "C · 00:00–08:00",
        _ => "A · 08:00–16:00"
    };
}

// ---------- Pareto analizi ----------

public record ParetoSatiri(string Etiket, double Deger, double Yuzde, double KumulatifYuzde);

public static class ParetoHesaplayici
{
    // Büyükten küçüğe sıralar, kümülatif yüzdeyi hesaplar (80/20 kuralı)
    public static List<ParetoSatiri> Hesapla(IEnumerable<(string Etiket, double Deger)> veriler)
    {
        var sirali = veriler.Where(v => v.Deger > 0)
            .GroupBy(v => v.Etiket).Select(g => (Etiket: g.Key, Deger: g.Sum(x => x.Deger)))
            .OrderByDescending(v => v.Deger).ThenBy(v => v.Etiket).ToList();
        var toplam = sirali.Sum(v => v.Deger);
        if (toplam <= 0) return new List<ParetoSatiri>();

        var kumulatif = 0.0;
        var sonuc = new List<ParetoSatiri>();
        foreach (var v in sirali)
        {
            kumulatif += v.Deger;
            sonuc.Add(new ParetoSatiri(v.Etiket, v.Deger, 100 * v.Deger / toplam, Math.Min(100, 100 * kumulatif / toplam)));
        }
        return sonuc;
    }

    // Toplamın %80'ini oluşturan "hayati az sayıdaki" nedenler
    public static List<ParetoSatiri> HayatiAzlar(List<ParetoSatiri> satirlar)
    {
        var liste = new List<ParetoSatiri>();
        foreach (var s in satirlar)
        {
            liste.Add(s);
            if (s.KumulatifYuzde >= 80) break;
        }
        return liste;
    }
}

// ---------- Elektronik imza ----------

public static class ImzaHesaplayici
{
    // Belge içeriği + imzalayan + zaman + imza çizimi -> SHA-256.
    // Belge imzadan sonra değişirse özet tutmaz ve imza "geçersiz" görünür.
    public static string Ozet(string belgeIcerigi, string rol, string kullaniciAdi, DateTime zaman, string imzaYolu)
    {
        var metin = string.Join("|", belgeIcerigi, rol, kullaniciAdi, zaman.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture), imzaYolu);
        var bayt = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(metin));
        return Convert.ToHexString(bayt).ToLowerInvariant();
    }

    // İmza çizimi yalnızca "M x y L x y" komutlarından oluşabilir (XSS'e karşı)
    public static bool YolGecerliMi(string? yol)
    {
        if (string.IsNullOrWhiteSpace(yol) || yol.Length > 60000) return false;
        if (!yol.All(c => c is 'M' or 'L' or ' ' or '.' or '-' || char.IsAsciiDigit(c))) return false;
        return yol.Count(c => c == 'L') >= 8;   // boş / tek nokta imza kabul edilmez
    }
}
