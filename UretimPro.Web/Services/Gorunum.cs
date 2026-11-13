using UretimPro.Models;

namespace UretimPro.Services;

// Sayfalarda kullanılan küçük biçimlendirme yardımcıları
public static class Gorunum
{
    public static string EmirSinif(string durum) => durum switch
    {
        EmirDurum.Planlandi => "gri",
        EmirDurum.Uretimde => "mavi",
        EmirDurum.Tamamlandi => "yesil",
        EmirDurum.Bloke => "kirmizi",
        EmirDurum.Iptal => "gri",
        _ => "gri"
    };

    public static string ParcaSinif(string durum) => durum switch
    {
        ParcaDurum.Bekliyor => "gri",
        ParcaDurum.Uretimde => "mavi",
        ParcaDurum.Tamamlandi => "yesil",
        ParcaDurum.Bloke => "kirmizi",
        ParcaDurum.Hurda => "koyu",
        _ => "gri"
    };

    public static string NcrSinif(string durum) => durum switch
    {
        NcrDurum.Acik => "kirmizi",
        NcrDurum.KararVerildi => "sari",
        NcrDurum.OnayBekliyor => "mavi",
        NcrDurum.Kapali => "yesil",
        _ => "gri"
    };

    public static string MakineSinif(string durum) => durum switch
    {
        MakineDurum.Calisiyor => "yesil",
        MakineDurum.Bekliyor => "sari",
        MakineDurum.Arizali => "kirmizi",
        MakineDurum.Bakimda => "mavi",
        _ => "gri"
    };

    public static string SeviyeSinif(string seviye) => seviye switch
    {
        "kritik" => "kirmizi",
        "uyari" => "sari",
        _ => "mavi"
    };

    public static string BakimSinif(BakimSeviye s) => s switch
    {
        BakimSeviye.Gecikti => "kirmizi",
        BakimSeviye.Yaklasiyor => "sari",
        _ => "yesil"
    };

    public static string BakimMetin(BakimSeviye s) => s switch
    {
        BakimSeviye.Gecikti => "Gecikti",
        BakimSeviye.Yaklasiyor => "Yaklaşıyor",
        _ => "Normal"
    };

    public static string GecenSure(DateTime zaman)
    {
        var f = DateTime.Now - zaman;
        if (f.TotalMinutes < 1) return "az önce";
        if (f.TotalMinutes < 60) return $"{(int)f.TotalMinutes} dk önce";
        if (f.TotalHours < 24) return $"{(int)f.TotalHours} sa önce";
        if (f.TotalDays < 30) return $"{(int)f.TotalDays} gün önce";
        return zaman.ToString("dd.MM.yyyy");
    }

    public static string Sure(TimeSpan s)
    {
        if (s.TotalMinutes < 60) return $"{(int)s.TotalMinutes} dk";
        if (s.TotalHours < 24) return $"{(int)s.TotalHours} sa {s.Minutes} dk";
        return $"{(int)s.TotalDays} gün {s.Hours} sa";
    }

    public static string Yuzde(int kisim, int toplam) =>
        toplam == 0 ? "0" : Sayi.Css(Math.Min(100, 100.0 * kisim / toplam));

    public static string Vardiya(DateTime t) => t.Hour switch
    {
        >= 8 and < 16 => "A · 08:00–16:00",
        >= 16 => "B · 16:00–24:00",
        _ => "C · 00:00–08:00"
    };
}
