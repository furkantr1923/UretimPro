namespace UretimPro.Models;

// Değişiklikleri audit log'a yazılacak varlıkları işaretler
public interface IDenetlenebilir { }

public static class Roller
{
    public const string Admin = "Admin";
    public const string Yonetici = "Yonetici";
    public const string Kalite = "Kalite";
    public const string Operator = "Operator";
    public const string DepoBakim = "DepoBakim";

    public static readonly string[] Tumu = { Admin, Yonetici, Kalite, Operator, DepoBakim };

    public static string Ad(string rol) => rol switch
    {
        Admin => "Admin",
        Yonetici => "Yönetici",
        Kalite => "Kalite",
        Operator => "Operatör",
        DepoBakim => "Depo / Bakım",
        _ => rol
    };
}

public static class MakineDurum
{
    public const string Calisiyor = "Çalışıyor";
    public const string Bekliyor = "Bekliyor";
    public const string Arizali = "Arızalı";
    public const string Bakimda = "Bakımda";
}

public static class EmirDurum
{
    public const string Planlandi = "Planlandı";
    public const string Uretimde = "Üretimde";
    public const string Tamamlandi = "Tamamlandı";
    public const string Bloke = "Bloke";
    public const string Iptal = "İptal";
}

public static class ParcaDurum
{
    public const string Bekliyor = "Bekliyor";
    public const string Uretimde = "Üretimde";
    public const string Tamamlandi = "Tamamlandı";
    public const string Bloke = "Bloke";
    public const string Hurda = "Hurda";
}

public static class NcrDurum
{
    public const string Acik = "Açık";
    public const string KararVerildi = "Karar verildi";
    public const string OnayBekliyor = "Onay bekliyor";
    public const string Kapali = "Kapalı";

    public static readonly string[] Tumu = { Acik, KararVerildi, OnayBekliyor, Kapali };
}

public static class DurusNedeni
{
    public const string Ariza = "Arıza";
    public const string Ayar = "Ayar / set-up";
    public const string Malzeme = "Malzeme bekleme";
    public const string Takim = "Takım değişimi";
    public const string Operator = "Operatör yok";
    public const string KaliteOnayi = "Kalite onayı bekleme";
    public const string PlanliBakim = "Planlı bakım";

    public static readonly string[] Tumu = { Ariza, Ayar, Malzeme, Takim, Operator, KaliteOnayi, PlanliBakim };
}

public static class NcrKarar
{
    public const string Hurda = "Hurda";
    public const string YenidenIsleme = "Yeniden işleme";
    public const string SartliKabul = "Şartlı kabul";

    public static readonly string[] Tumu = { Hurda, YenidenIsleme, SartliKabul };
}

public class Kullanici : IDenetlenebilir
{
    public int Id { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string Rol { get; set; } = Roller.Operator;
    public string SifreHash { get; set; } = "";
    public bool Aktif { get; set; } = true;
    public int HataliDeneme { get; set; }
    public DateTime? KilitBitis { get; set; }
    public DateTime? SonGiris { get; set; }
    public DateTime SonBildirimOkuma { get; set; } = DateTime.Now;
    public string? PanelDuzeni { get; set; }   // kişiselleştirilmiş kontrol merkezi (JSON)
}

public class Makine : IDenetlenebilir
{
    public int Id { get; set; }
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public string Tur { get; set; } = "";
    public string Durum { get; set; } = MakineDurum.Bekliyor;
    public int BakimPeriyotGun { get; set; } = 30;
    public int BakimPeriyotAdet { get; set; } = 5000;
    public DateTime SonBakimTarihi { get; set; } = DateTime.Now;
    public int KonumX { get; set; }
    public int KonumY { get; set; }
    public int IdealCevrimSn { get; set; } = 240;   // OEE performans hesabı için ideal çevrim süresi
}

public class Malzeme : IDenetlenebilir
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string Birim { get; set; } = "kg";
    public double MinimumStok { get; set; }
}

public class Urun : IDenetlenebilir
{
    public int Id { get; set; }
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public int MalzemeId { get; set; }
    public Malzeme? Malzeme { get; set; }
    public double BirimTuketim { get; set; }   // adet başına hammadde (kg)
    public int SonSeriNo { get; set; }
    public bool Aktif { get; set; } = true;
    public List<RotaAdimi> RotaAdimlari { get; set; } = new();
    public List<KaliteKarakteristigi> Karakteristikler { get; set; } = new();
}

public class RotaAdimi
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }
    public int Sira { get; set; }
    public string Ad { get; set; } = "";
    public int MakineId { get; set; }
    public Makine? Makine { get; set; }
}

public class KaliteKarakteristigi : IDenetlenebilir
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }
    public int RotaAdimiId { get; set; }
    public RotaAdimi? RotaAdimi { get; set; }
    public string Ad { get; set; } = "";
    public string Birim { get; set; } = "mm";
    public double Nominal { get; set; }
    public double AltSinir { get; set; }
    public double UstSinir { get; set; }
    public string AletTuru { get; set; } = "";   // hangi ölçüm aleti türüyle ölçülür
}

public class OlcumAleti : IDenetlenebilir
{
    public int Id { get; set; }
    public string Kod { get; set; } = "";
    public string Ad { get; set; } = "";
    public string Tur { get; set; } = "";
    public DateTime SonKalibrasyon { get; set; }
    public int PeriyotGun { get; set; } = 180;

    public DateTime SonrakiKalibrasyon => SonKalibrasyon.AddDays(PeriyotGun);
    public bool GecerliMi(DateTime zaman) => zaman <= SonrakiKalibrasyon;
}

public class Yetkinlik : IDenetlenebilir
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public Kullanici? Kullanici { get; set; }
    public int MakineId { get; set; }
    public Makine? Makine { get; set; }
    public DateTime GecerlilikBitis { get; set; }
}

public class HammaddeLotu : IDenetlenebilir
{
    public int Id { get; set; }
    public string LotNo { get; set; } = "";
    public int MalzemeId { get; set; }
    public Malzeme? Malzeme { get; set; }
    public string Tedarikci { get; set; } = "";
    public string SertifikaNo { get; set; } = "";
    public DateTime GirisTarihi { get; set; }
    public double BaslangicMiktar { get; set; }
    public double KalanMiktar { get; set; }
}

public class UretimEmri : IDenetlenebilir
{
    public int Id { get; set; }
    public string EmirNo { get; set; } = "";
    public string LotNo { get; set; } = "";
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }
    public int Miktar { get; set; }
    public string Durum { get; set; } = EmirDurum.Planlandi;
    public DateTime PlanTarihi { get; set; }
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    public DateTime? BaslamaTarihi { get; set; }
    public DateTime? TamamlanmaTarihi { get; set; }
    public List<Parca> Parcalar { get; set; } = new();
    public List<HammaddeTuketimi> Tuketimler { get; set; } = new();
}

public class HammaddeTuketimi
{
    public int Id { get; set; }
    public int UretimEmriId { get; set; }
    public UretimEmri? UretimEmri { get; set; }
    public int HammaddeLotuId { get; set; }
    public HammaddeLotu? HammaddeLotu { get; set; }
    public double Miktar { get; set; }
    public DateTime Zaman { get; set; }
}

public class Parca
{
    public int Id { get; set; }
    public string SeriNo { get; set; } = "";
    public int UretimEmriId { get; set; }
    public UretimEmri? UretimEmri { get; set; }
    public int UrunId { get; set; }
    public Urun? Urun { get; set; }
    public string Durum { get; set; } = ParcaDurum.Bekliyor;
    public int TamamlananAdim { get; set; }
    public DateTime? TamamlanmaTarihi { get; set; }
}

public class IslemKaydi
{
    public int Id { get; set; }
    public int ParcaId { get; set; }
    public Parca? Parca { get; set; }
    public int RotaAdimiId { get; set; }
    public RotaAdimi? RotaAdimi { get; set; }
    public int MakineId { get; set; }
    public Makine? Makine { get; set; }
    public int OperatorId { get; set; }
    public Kullanici? Operator { get; set; }
    public DateTime Zaman { get; set; }
}

public class Olcum
{
    public int Id { get; set; }
    public int ParcaId { get; set; }
    public Parca? Parca { get; set; }
    public int KarakteristikId { get; set; }
    public KaliteKarakteristigi? Karakteristik { get; set; }
    public int MakineId { get; set; }
    public double Deger { get; set; }
    public bool Uygun { get; set; }
    public int OlcumAletiId { get; set; }
    public OlcumAleti? OlcumAleti { get; set; }
    public int KullaniciId { get; set; }
    public Kullanici? Kullanici { get; set; }
    public DateTime Zaman { get; set; }
}

public class Ncr : IDenetlenebilir
{
    public int Id { get; set; }
    public string NcrNo { get; set; } = "";
    public int UretimEmriId { get; set; }
    public UretimEmri? UretimEmri { get; set; }
    public int? ParcaId { get; set; }
    public Parca? Parca { get; set; }
    public string Kaynak { get; set; } = "";
    public string Tanim { get; set; } = "";
    public string Durum { get; set; } = NcrDurum.Acik;
    public string? Karar { get; set; }
    public string? DuzelticiFaaliyet { get; set; }
    public string AcanKisi { get; set; } = "";
    public string? KararVeren { get; set; }
    public DateTime AcilisTarihi { get; set; }
    public DateTime? KararTarihi { get; set; }
    public DateTime? KapanisTarihi { get; set; }
}

public class BakimKaydi
{
    public int Id { get; set; }
    public int MakineId { get; set; }
    public Makine? Makine { get; set; }
    public string Tur { get; set; } = "Periyodik";
    public string Aciklama { get; set; } = "";
    public DateTime Tarih { get; set; }
    public string Yapan { get; set; } = "";
    public int SureDakika { get; set; }
}

// Makine duruşu: OEE kullanılabilirlik hesabı ve Pareto analizi için
public class DurusKaydi
{
    public int Id { get; set; }
    public int MakineId { get; set; }
    public Makine? Makine { get; set; }
    public string Neden { get; set; } = DurusNedeni.Ayar;
    public string? Aciklama { get; set; }
    public DateTime Baslangic { get; set; }
    public DateTime? Bitis { get; set; }   // null: duruş devam ediyor
    public string BildirenKisi { get; set; } = "";

    public double Dakika(DateTime simdi) => ((Bitis ?? simdi) - Baslangic).TotalMinutes;
}

// Elektronik imza: değiştirilemez; belge içeriğinin SHA-256 özeti ile birlikte saklanır
public class ElektronikImza
{
    public int Id { get; set; }
    public string Belge { get; set; } = "NCR";
    public int BelgeId { get; set; }
    public string Rol { get; set; } = "";          // Hazırlayan / Onaylayan
    public int KullaniciId { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public DateTime Zaman { get; set; }
    public string ImzaYolu { get; set; } = "";     // imza çizimi (SVG path, 400x140)
    public string Ozet { get; set; } = "";         // SHA-256
}

public class Bildirim
{
    public int Id { get; set; }
    public string HedefRoller { get; set; } = "";   // ",Kalite,Yonetici," biçiminde
    public string Baslik { get; set; } = "";
    public string Mesaj { get; set; } = "";
    public string Seviye { get; set; } = "bilgi";   // bilgi / uyari / kritik
    public string? Link { get; set; }
    public DateTime Zaman { get; set; } = DateTime.Now;
}

public class AuditKaydi
{
    public int Id { get; set; }
    public DateTime Zaman { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string Tablo { get; set; } = "";
    public string KayitId { get; set; } = "";
    public string Islem { get; set; } = "";
    public string Degisiklikler { get; set; } = "[]";
}
