using Microsoft.AspNetCore.Html;

namespace UretimPro.Services;

// Çizgi ikonlar (inline SVG, internet gerektirmez)
public static class Ikon
{
    private static readonly Dictionary<string, string> Yollar = new()
    {
        ["panel"] = "<rect x='3' y='3' width='7' height='9'/><rect x='14' y='3' width='7' height='5'/><rect x='14' y='12' width='7' height='9'/><rect x='3' y='16' width='7' height='5'/>",
        ["uretim"] = "<path d='M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2'/><rect x='9' y='3' width='6' height='4' rx='1'/><path d='M9 12h6M9 16h4'/>",
        ["istasyon"] = "<path d='M3 7V5a2 2 0 0 1 2-2h2M17 3h2a2 2 0 0 1 2 2v2M21 17v2a2 2 0 0 1-2 2h-2M7 21H5a2 2 0 0 1-2-2v-2'/><path d='M7 8v8M10 8v8M13 8v8M16 8v8'/>",
        ["spc"] = "<path d='M3 3v18h18'/><path d='M7 15l4-4 3 3 5-6'/>",
        ["ncr"] = "<path d='M12 9v4M12 17h.01'/><path d='M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z'/>",
        ["depo"] = "<path d='M21 8 12 3 3 8v8l9 5 9-5z'/><path d='M3 8l9 5 9-5M12 13v8'/>",
        ["bakim"] = "<path d='M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18v3h3l6.3-6.3a4 4 0 0 0 5.4-5.4l-2.6 2.6-2.4-.6-.6-2.4z'/>",
        ["ara"] = "<circle cx='11' cy='11' r='7'/><path d='m21 21-4.3-4.3'/>",
        ["kalkan"] = "<path d='M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z'/>",
        ["kullanicilar"] = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8'/>",
        ["yetkinlik"] = "<circle cx='12' cy='8' r='6'/><path d='M15.5 13.5 17 22l-5-3-5 3 1.5-8.5'/>",
        ["urun"] = "<circle cx='12' cy='12' r='9'/><circle cx='12' cy='12' r='3'/>",
        ["kalibrasyon"] = "<path d='M12 2v4M12 18v4M4.9 4.9l2.8 2.8M16.3 16.3l2.8 2.8M2 12h4M18 12h4M4.9 19.1l2.8-2.8M16.3 7.7l2.8-2.8'/><circle cx='12' cy='12' r='3'/>",
        ["audit"] = "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><path d='M14 2v6h6M8 13h8M8 17h5'/>",
        ["zil"] = "<path d='M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9'/><path d='M10.3 21a1.9 1.9 0 0 0 3.4 0'/>",
        ["ay"] = "<path d='M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z'/>",
        ["gunes"] = "<circle cx='12' cy='12' r='4'/><path d='M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4'/>",
        ["cikis"] = "<path d='M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4'/><path d='m16 17 5-5-5-5M21 12H9'/>",
        ["arti"] = "<path d='M12 5v14M5 12h14'/>",
        ["qr"] = "<rect x='3' y='3' width='7' height='7'/><rect x='14' y='3' width='7' height='7'/><rect x='3' y='14' width='7' height='7'/><path d='M14 14h3v3h-3zM18 18h3v3h-3zM14 20h2M20 14v2'/>",
        ["yazici"] = "<path d='M6 9V2h12v7'/><path d='M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2'/><rect x='6' y='14' width='12' height='8'/>",
        ["oynat"] = "<path d='m6 3 14 9-14 9z'/>",
        ["tik"] = "<path d='M20 6 9 17l-5-5'/>",
        ["carpi"] = "<path d='M18 6 6 18M6 6l12 12'/>",
        ["saat"] = "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>",
        ["fabrika"] = "<path d='M3 21V9l6 4V9l6 4V5l6 4v12z'/><path d='M3 21h18'/>",
        ["menu"] = "<path d='M3 6h18M3 12h18M3 18h18'/>",
        ["ok-sag"] = "<path d='M5 12h14M13 6l6 6-6 6'/>",
        ["agac"] = "<circle cx='12' cy='5' r='2'/><circle cx='5' cy='19' r='2'/><circle cx='19' cy='19' r='2'/><path d='M12 7v4M12 11H5v6M12 11h7v6'/>",
        ["bilgi"] = "<circle cx='12' cy='12' r='9'/><path d='M12 16v-4M12 8h.01'/>",
        ["harita"] = "<path d='M9 3 3 6v15l6-3 6 3 6-3V3l-6 3z'/><path d='M9 3v15M15 6v15'/>",
        ["rapor"] = "<path d='M3 3v18h18'/><rect x='7' y='12' width='3' height='6'/><rect x='12' y='8' width='3' height='10'/><rect x='17' y='5' width='3' height='13'/>",
        ["durus"] = "<circle cx='12' cy='12' r='9'/><path d='M10 9v6M14 9v6'/>",
        ["pdf"] = "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><path d='M14 2v6h6'/><path d='M9 15h6M12 12v6'/>",
        ["excel"] = "<rect x='3' y='3' width='18' height='18' rx='2'/><path d='M3 9h18M3 15h18M9 3v18'/>",
        ["imza"] = "<path d='M3 17c3-1 4-8 6-8s0 7 2 7 2-4 4-4 1 3 3 3 3-1 3-1'/><path d='M3 21h18'/>",
        ["api"] = "<path d='m8 8-4 4 4 4M16 8l4 4-4 4M14 4l-4 16'/>",
        ["komut"] = "<path d='M18 3a3 3 0 0 0-3 3v12a3 3 0 1 0 3-3H6a3 3 0 1 0 3 3V6a3 3 0 1 0-3 3h12a3 3 0 0 0 0-6z'/>",
        ["duzenle"] = "<path d='M12 20h9'/><path d='M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z'/>",
        ["goz"] = "<path d='M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z'/><circle cx='12' cy='12' r='3'/>",
        ["tasi"] = "<path d='M9 5h.01M9 12h.01M9 19h.01M15 5h.01M15 12h.01M15 19h.01'/>",
        ["indir"] = "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><path d='m7 10 5 5 5-5M12 15V3'/>",
        ["kilit"] = "<rect x='4' y='11' width='16' height='10' rx='2'/><path d='M8 11V7a4 4 0 0 1 8 0v4'/>"
    };

    public static IHtmlContent Ciz(string ad, int boyut = 18, string? sinif = null)
    {
        var yol = Yollar.TryGetValue(ad, out var y) ? y : Yollar["bilgi"];
        var cls = sinif == null ? "" : $" class='{sinif}'";
        return new HtmlString(
            $"<svg{cls} width='{boyut}' height='{boyut}' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>{yol}</svg>");
    }
}
