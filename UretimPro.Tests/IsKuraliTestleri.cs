using UretimPro.Controllers;
using UretimPro.Services;

namespace UretimPro.Tests;

public class SpcTestleri
{
    private static List<double> Dalgali(int adet) =>
        Enumerable.Range(0, adet).Select(i => i % 2 == 0 ? 10.00 : 10.02).ToList();

    [Fact]
    public void Kararli_surecte_ihlal_yok()
    {
        var sonuc = SpcHesaplayici.Analiz(Dalgali(20), 9.95, 10.05);
        Assert.DoesNotContain(sonuc.Ihlaller, i => i.Kural == 1);
        Assert.Equal(10.01, sonuc.Ortalama, 3);
    }

    [Fact]
    public void Kontrol_siniri_disindaki_nokta_kural_1()
    {
        var degerler = Dalgali(19);
        degerler.Add(10.5);
        var sonuc = SpcHesaplayici.Analiz(degerler, 9.95, 10.05);
        Assert.Contains(sonuc.Ihlaller, i => i.Kural == 1 && i.Indeks == 19);
    }

    [Fact]
    public void Art_arda_yukselen_alti_nokta_trend_kurali()
    {
        var degerler = Dalgali(10);
        degerler.AddRange(new[] { 10.000, 10.006, 10.012, 10.018, 10.024, 10.030 });
        var sonuc = SpcHesaplayici.Analiz(degerler, 9.95, 10.05);
        Assert.True(sonuc.SonNoktadaIhlalVar(degerler.Count));
        Assert.Contains(sonuc.Ihlaller, i => i.Kural == 5);
    }

    [Fact]
    public void Sekiz_nokta_ayni_tarafta_kural_4()
    {
        var degerler = Enumerable.Repeat(9.99, 10).Concat(Enumerable.Repeat(10.01, 8)).ToList();
        degerler[3] = 9.985;
        degerler[14] = 10.015;
        var sonuc = SpcHesaplayici.Analiz(degerler, 9.95, 10.05);
        Assert.Contains(sonuc.Ihlaller, i => i.Kural == 4 && i.Indeks == degerler.Count - 1);
    }

    [Fact]
    public void Cpk_merkezli_surecte_yeterli_kaymis_surecte_yetersiz()
    {
        var merkezli = Enumerable.Range(0, 30).Select(i => i % 2 == 0 ? 9.99 : 10.01).ToList();
        var kaymis = merkezli.Select(d => d + 0.035).ToList();

        Assert.True(SpcHesaplayici.Analiz(merkezli, 9.95, 10.05).Cpk > 1.33);
        Assert.True(SpcHesaplayici.Analiz(kaymis, 9.95, 10.05).Cpk < 1.0);
    }

    [Fact]
    public void Bos_listede_hata_vermez()
    {
        var sonuc = SpcHesaplayici.Analiz(new List<double>(), 0, 1);
        Assert.Empty(sonuc.Ihlaller);
        Assert.Null(sonuc.Cpk);
    }
}

public class BakimTestleri
{
    private static readonly DateTime Simdi = new(2026, 11, 13, 12, 0, 0);

    [Theory]
    [InlineData(10, 100, BakimSeviye.Normal)]
    [InlineData(28, 100, BakimSeviye.Yaklasiyor)]
    [InlineData(31, 100, BakimSeviye.Gecikti)]
    [InlineData(1, 5200, BakimSeviye.Gecikti)]   // adet sınırı süreden önce doldu
    [InlineData(1, 4500, BakimSeviye.Yaklasiyor)]
    public void Sure_veya_adet_hangisi_once_dolarsa(int gunOnce, int adet, BakimSeviye beklenen)
    {
        var durum = BakimHesaplayici.Hesapla(Simdi.AddDays(-gunOnce), 30, 5000, adet, Simdi);
        Assert.Equal(beklenen, durum.Seviye);
    }
}

public class FifoTestleri
{
    private static readonly List<LotStok> Lotlar = new()
    {
        new LotStok(2, new DateTime(2026, 10, 20), 200),
        new LotStok(1, new DateTime(2026, 10, 1), 100),
        new LotStok(3, new DateTime(2026, 9, 1), 0)   // boş lot atlanır
    };

    [Fact]
    public void En_eski_lottan_baslar()
    {
        var (paylar, eksik) = FifoSecici.Sec(Lotlar, 150);
        Assert.Equal(0, eksik);
        Assert.Equal(new[] { 1, 2 }, paylar.Select(p => p.LotId));
        Assert.Equal(100, paylar[0].Miktar);
        Assert.Equal(50, paylar[1].Miktar);
    }

    [Fact]
    public void Stok_yetmezse_eksik_miktar_doner()
    {
        var (_, eksik) = FifoSecici.Sec(Lotlar, 400);
        Assert.Equal(100, eksik);
    }
}

public class NumaraVeSayiTestleri
{
    [Fact]
    public void Seri_no_formati() => Assert.Equal("FL120-00681", Numara.SeriNo("FL120", 681));

    [Fact]
    public void Siradaki_numara_en_buyukten_devam_eder() =>
        Assert.Equal(458, Numara.Siradaki(new[] { "UE-0012", "UE-0457", "UE-0300" }));

    [Theory]
    [InlineData("25,044", 25.044)]
    [InlineData("25.044", 25.044)]
    [InlineData(" 120,05 ", 120.05)]
    public void Virgul_ve_nokta_kabul_edilir(string metin, double beklenen)
    {
        Assert.True(Sayi.Coz(metin, out var deger));
        Assert.Equal(beklenen, deger, 6);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("")]
    public void Gecersiz_sayi_reddedilir(string metin) => Assert.False(Sayi.Coz(metin, out _));

    [Theory]
    [InlineData("Demo123!", true)]
    [InlineData("demo1234", false)]
    [InlineData("Kisa1", false)]
    public void Sifre_kurali(string sifre, bool beklenen) => Assert.Equal(beklenen, YonetimController.SifreGucluMu(sifre));
}
