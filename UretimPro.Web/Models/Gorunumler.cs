using UretimPro.Services;

namespace UretimPro.Models;

// Sayfaların kullandığı görünüm modelleri

public class GirisModel
{
    public string KullaniciAdi { get; set; } = "";
    public string Sifre { get; set; } = "";
    public string? ReturnUrl { get; set; }
    public string? Hata { get; set; }
    public int BugunUretilen { get; set; }
    public int AktifEmir { get; set; }
    public int CalisanMakine { get; set; }
}

public record GunlukDeger(string Etiket, double Deger);
public record CanliIslem(string SeriNo, string Adim, string Makine, string Kullanici, DateTime Zaman);

public class PanelModel
{
    public int BugunUretilen { get; set; }
    public double DunkuUretim { get; set; }
    public double FireOrani { get; set; }
    public int AcikNcr { get; set; }
    public int AktifEmir { get; set; }
    public int CalisanMakine { get; set; }
    public int ToplamMakine { get; set; }
    public List<UretimEmri> AktifEmirler { get; set; } = new();
    public Dictionary<int, int> EmirTamamlanan { get; set; } = new();
    public List<Uyari> Uyarilar { get; set; } = new();
    public List<CanliIslem> SonIslemler { get; set; } = new();
    public List<GunlukDeger> GunlukUretim { get; set; } = new();
    public List<GunlukDeger> UrunDagilimi { get; set; } = new();
    public List<MakineBakimSatiri> Makineler { get; set; } = new();
    public OeeSonuc Oee { get; set; } = OeeSonuc.Bos;
    public List<ParetoSatiri> Pareto { get; set; } = new();
    public List<GunlukDeger> OeeTrend { get; set; } = new();
    public string? Duzen { get; set; }
}

public class EmirListeModel
{
    public List<UretimEmri> Emirler { get; set; } = new();
    public Dictionary<int, int> Tamamlanan { get; set; } = new();
    public string? Durum { get; set; }
    public string? Arama { get; set; }
}

public class EmirDetayModel
{
    public UretimEmri Emir { get; set; } = null!;
    public List<RotaAdimi> Adimlar { get; set; } = new();
    public Dictionary<int, int> AdimdaBekleyen { get; set; } = new();   // TamamlananAdim -> adet
    public List<Ncr> Ncrler { get; set; } = new();
    public List<HammaddeLotu> UygunLotlar { get; set; } = new();
    public List<LotPayi> FifoPaylari { get; set; } = new();
    public double FifoEksik { get; set; }
    public double Ihtiyac { get; set; }
    public Dictionary<int, string> LotNolari { get; set; } = new();
}

public class YeniEmirModel
{
    public int UrunId { get; set; }
    public int Miktar { get; set; } = 50;
    public DateTime PlanTarihi { get; set; } = DateTime.Today.AddDays(1);
    public List<Urun> Urunler { get; set; } = new();
    public Dictionary<int, double> MalzemeStoklari { get; set; } = new();
}

public class IstasyonSecimModel
{
    public List<Makine> Makineler { get; set; } = new();
    public HashSet<int> Yetkili { get; set; } = new();
    public Dictionary<int, int> BekleyenParca { get; set; } = new();
}

public class IstasyonEkranModel
{
    public Makine Makine { get; set; } = null!;
    public bool Yetkili { get; set; }
    public DateTime? YetkinlikBitis { get; set; }
    public List<CanliIslem> SonIslemler { get; set; } = new();
    public List<string> OrnekSeriNolar { get; set; } = new();
    public int BugunIslem { get; set; }
}

public class SpcModel
{
    public List<KaliteKarakteristigi> Karakteristikler { get; set; } = new();
    public KaliteKarakteristigi? Secili { get; set; }
    public Makine? Makine { get; set; }
    public List<Olcum> Olcumler { get; set; } = new();
    public SpcSonuc? Sonuc { get; set; }
    public List<SpcOzet> Ozetler { get; set; } = new();
    public int Uygunsuz { get; set; }
}

public class NcrListeModel
{
    public List<Ncr> Ncrler { get; set; } = new();
    public string? Durum { get; set; }
    public Dictionary<string, int> Sayilar { get; set; } = new();
}

public class NcrDetayModel
{
    public Ncr Ncr { get; set; } = null!;
    public List<Olcum> Olcumler { get; set; } = new();
    public List<AuditKaydi> Gecmis { get; set; } = new();
    public List<ImzaSatiri> Imzalar { get; set; } = new();
}

public record StokSatiri(Malzeme Malzeme, double Kalan, double Tuketim30Gun, int LotSayisi);

public class DepoModel
{
    public List<StokSatiri> Stoklar { get; set; } = new();
    public List<HammaddeLotu> Lotlar { get; set; } = new();
    public List<HammaddeTuketimi> SonHareketler { get; set; } = new();
    public List<Malzeme> Malzemeler { get; set; } = new();
}

public class BakimModel
{
    public List<MakineBakimSatiri> Makineler { get; set; } = new();
    public List<BakimKaydi> SonKayitlar { get; set; } = new();
    public List<SpcOzet> SpcUyarilari { get; set; } = new();
}

public class ParcaIzModel
{
    public Parca Parca { get; set; } = null!;
    public List<RotaAdimi> Adimlar { get; set; } = new();
    public List<IslemKaydi> Islemler { get; set; } = new();
    public List<Olcum> Olcumler { get; set; } = new();
    public List<HammaddeTuketimi> Hammaddeler { get; set; } = new();
    public List<Ncr> Ncrler { get; set; } = new();
}

public class LotIzModel
{
    public UretimEmri Emir { get; set; } = null!;
    public List<HammaddeTuketimi> Hammaddeler { get; set; } = new();
    public Dictionary<string, int> DurumSayilari { get; set; } = new();
    public List<Ncr> Ncrler { get; set; } = new();
}

public class HammaddeIzModel
{
    public HammaddeLotu Lot { get; set; } = null!;
    public List<HammaddeTuketimi> Tuketimler { get; set; } = new();
}

public class KullaniciListeModel
{
    public List<Kullanici> Kullanicilar { get; set; } = new();
}

public class YetkinlikModel
{
    public List<Kullanici> Kullanicilar { get; set; } = new();
    public List<Makine> Makineler { get; set; } = new();
    public Dictionary<(int, int), Yetkinlik> Hucreler { get; set; } = new();
}

public class UrunListeModel
{
    public List<Urun> Urunler { get; set; } = new();
}

public record AuditSatiri(AuditKaydi Kayit, List<UretimPro.Data.UretimDbContext.Degisiklik> Degisiklikler);

public class AuditModel
{
    public List<AuditSatiri> Kayitlar { get; set; } = new();
    public string? Kullanici { get; set; }
    public string? Tablo { get; set; }
    public List<string> Tablolar { get; set; } = new();
    public List<string> KullaniciAdlari { get; set; } = new();
}

public class BildirimListeModel
{
    public List<Bildirim> Bildirimler { get; set; } = new();
    public DateTime SonOkuma { get; set; }
}

// ---------- Paket 2 ----------

public class FabrikaModel
{
    public List<MakineOee> Makineler { get; set; } = new();
    public Dictionary<int, int> BugunAdet { get; set; } = new();
    public Dictionary<int, string> SonOperator { get; set; } = new();
    public Dictionary<int, DurusKaydi> AcikDuruslar { get; set; } = new();
    public Dictionary<int, BakimDurumu> Bakim { get; set; } = new();
    public List<(int Kaynak, int Hedef, int Adet)> Akislar { get; set; } = new();
    public OeeSonuc Toplam { get; set; } = OeeSonuc.Bos;
    public string? SeciliKod { get; set; }
}

public class AgacModel
{
    public AgacDugum Kok { get; set; } = null!;
    public string Tur { get; set; } = "lot";   // lot / hammadde
}

public class VardiyaRaporModel
{
    public DateTime Tarih { get; set; }
    public string Vardiya { get; set; } = "A";
    public DateTime Baslangic { get; set; }
    public DateTime Bitis { get; set; }
    public List<MakineOee> Makineler { get; set; } = new();
    public OeeSonuc Toplam { get; set; } = OeeSonuc.Bos;
    public List<DurusKaydi> Duruslar { get; set; } = new();
    public List<ParetoSatiri> Pareto { get; set; } = new();
    public List<GunlukDeger> UrunUretim { get; set; } = new();
    public List<GunlukDeger> OperatorIslem { get; set; } = new();
    public List<Ncr> Ncrler { get; set; } = new();
    public List<GunlukDeger> OeeTrend { get; set; } = new();
    public int TamamlananParca => (int)UrunUretim.Sum(u => u.Deger);
    public double DurusDakika => Makineler.Sum(m => m.Girdi.DurusDakika);
}

public class DurusModel
{
    public int Gun { get; set; } = 30;
    public List<DurusKaydi> Duruslar { get; set; } = new();
    public List<DurusKaydi> Acik { get; set; } = new();
    public List<ParetoSatiri> Pareto { get; set; } = new();
    public List<GunlukDeger> MakineBazli { get; set; } = new();
    public List<Makine> Makineler { get; set; } = new();
}

public record ImzaSatiri(ElektronikImza Imza, bool Gecerli);
