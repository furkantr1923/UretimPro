using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Tests;

public class OeeTestleri
{
    [Fact]
    public void Oee_uc_carpanin_carpimidir()
    {
        // 480 dk planlı, 48 dk duruş, 100 adet, 95 uygun, ideal çevrim 240 sn
        var s = OeeHesaplayici.Hesapla(new OeeGirdi(480, 48, 100, 95, 240));
        Assert.Equal(0.90, s.Kullanilabilirlik, 3);          // 432 / 480
        Assert.Equal(400.0 / 432, s.Performans, 3);          // 100 x 4 dk / 432 dk
        Assert.Equal(0.95, s.Kalite, 3);
        Assert.Equal(s.Kullanilabilirlik * s.Performans * s.Kalite, s.Oee, 6);
    }

    [Fact]
    public void Performans_yuzde_yuzu_gecmez()
    {
        var s = OeeHesaplayici.Hesapla(new OeeGirdi(60, 0, 100, 100, 240));
        Assert.Equal(1, s.Performans, 6);
    }

    [Fact]
    public void Planli_sure_yoksa_oee_sifir()
    {
        Assert.Equal(OeeSonuc.Bos, OeeHesaplayici.Hesapla(new OeeGirdi(0, 0, 0, 0, 240)));
    }

    [Fact]
    public void Tamamen_duran_makinenin_kullanilabilirligi_sifir()
    {
        var s = OeeHesaplayici.Hesapla(new OeeGirdi(180, 180, 0, 0, 300));
        Assert.Equal(0, s.Kullanilabilirlik, 6);
        Assert.Equal(0, s.Oee, 6);
    }

    [Fact]
    public void Toplam_oee_sure_agirliklidir()
    {
        var girdiler = new[]
        {
            new OeeGirdi(400, 40, 80, 80, 240),
            new OeeGirdi(100, 50, 10, 9, 240)
        };
        var t = OeeHesaplayici.Topla(girdiler);
        Assert.Equal(410.0 / 500, t.Kullanilabilirlik, 3);
        Assert.Equal(360.0 / 410, t.Performans, 3);           // (80 + 10) x 4 dk / 410 dk
        Assert.Equal(89.0 / 90, t.Kalite, 3);
    }

    [Fact]
    public void Cakisma_dakika_olarak_hesaplanir()
    {
        var gun = new DateTime(2026, 11, 13);
        Assert.Equal(30, OeeHesaplayici.Cakisma(gun.AddHours(15).AddMinutes(30), gun.AddHours(17), gun.AddHours(8), gun.AddHours(16)), 6);
        Assert.Equal(0, OeeHesaplayici.Cakisma(gun.AddHours(17), gun.AddHours(18), gun.AddHours(8), gun.AddHours(16)), 6);
    }

    [Theory]
    [InlineData(0.86, "yesil")]
    [InlineData(0.70, "sari")]
    [InlineData(0.40, "kirmizi")]
    public void Oee_siniflandirmasi(double oee, string beklenen) => Assert.Equal(beklenen, OeeHesaplayici.Sinif(oee));
}

public class VardiyaTestleri
{
    [Theory]
    [InlineData(7, "C")]
    [InlineData(8, "A")]
    [InlineData(15, "A")]
    [InlineData(16, "B")]
    [InlineData(23, "B")]
    public void Saate_gore_vardiya(int saat, string beklenen) =>
        Assert.Equal(beklenen, VardiyaPlani.Kod(new DateTime(2026, 11, 13, saat, 30, 0)));

    [Fact]
    public void B_vardiyasi_gece_yarisi_biter()
    {
        var (bas, bit) = VardiyaPlani.Aralik(new DateTime(2026, 11, 13), "B");
        Assert.Equal(new DateTime(2026, 11, 13, 16, 0, 0), bas);
        Assert.Equal(new DateTime(2026, 11, 14), bit);
    }
}

public class ParetoTestleri
{
    [Fact]
    public void Buyukten_kucuge_siralar_ve_kumulatif_hesaplar()
    {
        var p = ParetoHesaplayici.Hesapla(new[] { ("Ayar", 30.0), ("Arıza", 50.0), ("Malzeme", 20.0), ("Ayar", 0.0) });
        Assert.Equal(new[] { "Arıza", "Ayar", "Malzeme" }, p.Select(x => x.Etiket));
        Assert.Equal(50, p[0].Yuzde, 6);
        Assert.Equal(80, p[1].KumulatifYuzde, 6);
        Assert.Equal(100, p[2].KumulatifYuzde, 6);
    }

    [Fact]
    public void Ayni_neden_birlestirilir()
    {
        var p = ParetoHesaplayici.Hesapla(new[] { ("Ayar", 10.0), ("Ayar", 15.0) });
        Assert.Single(p);
        Assert.Equal(25, p[0].Deger, 6);
    }

    [Fact]
    public void Hayati_azlar_yuzde_sekseni_kapsar()
    {
        var p = ParetoHesaplayici.Hesapla(new[] { ("A", 60.0), ("B", 25.0), ("C", 10.0), ("D", 5.0) });
        Assert.Equal(new[] { "A", "B" }, ParetoHesaplayici.HayatiAzlar(p).Select(x => x.Etiket));
    }
}

public class ImzaTestleri
{
    private const string Yol = "M 10 10 L 20 20 L 30 25 L 40 22 L 50 30 L 60 28 L 70 35 L 80 30 L 90 40";

    private static Ncr OrnekNcr() => new()
    {
        Id = 1, NcrNo = "NCR-0031", Tanim = "FL120-00681 tolerans dışı", Karar = NcrKarar.YenidenIsleme,
        DuzelticiFaaliyet = "Takım ofseti güncellendi."
    };

    [Fact]
    public void Imza_dogrulanir()
    {
        var ncr = OrnekNcr();
        var k = new Kullanici { Id = 3, KullaniciAdi = "kalite", AdSoyad = "Murat Demir" };
        var imza = NcrImza.Olustur(ncr, "Hazırlayan", k, Yol, new DateTime(2026, 11, 13, 10, 15, 42, 123));
        Assert.True(NcrImza.Gecerli(imza, ncr));
        Assert.Equal(64, imza.Ozet.Length);
    }

    [Fact]
    public void Belge_degisirse_imza_gecersiz_olur()
    {
        var ncr = OrnekNcr();
        var imza = NcrImza.Olustur(ncr, "Hazırlayan", new Kullanici { Id = 3, KullaniciAdi = "kalite" }, Yol, DateTime.Now);
        ncr.DuzelticiFaaliyet = "Sonradan değiştirilmiş metin";
        Assert.False(NcrImza.Gecerli(imza, ncr));
    }

    [Theory]
    [InlineData(Yol, true)]
    [InlineData("M 10 10 L 20 20", false)]
    [InlineData("M 10 10 L 20 20\"/><script>alert(1)</script>", false)]
    [InlineData("", false)]
    public void Imza_yolu_dogrulamasi(string yol, bool beklenen) => Assert.Equal(beklenen, ImzaHesaplayici.YolGecerliMi(yol));
}
