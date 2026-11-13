using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UretimPro.Data;
using UretimPro.Models;

namespace UretimPro.Services;

public static class NcrImza
{
    // İmzalanan içerik: NCR numarası, tanım, karar ve düzeltici faaliyet
    public static string Icerik(Ncr n) => string.Join("|", n.NcrNo, n.Tanim, n.Karar ?? "", n.DuzelticiFaaliyet ?? "");

    public static bool Gecerli(ElektronikImza i, Ncr n) =>
        ImzaHesaplayici.Ozet(Icerik(n), i.Rol, i.KullaniciAdi, i.Zaman, i.ImzaYolu) == i.Ozet;

    public static ElektronikImza Olustur(Ncr n, string rol, Kullanici k, string imzaYolu, DateTime zaman)
    {
        zaman = new DateTime(zaman.Year, zaman.Month, zaman.Day, zaman.Hour, zaman.Minute, zaman.Second);
        return new ElektronikImza
        {
            Belge = "NCR", BelgeId = n.Id, Rol = rol, KullaniciId = k.Id, KullaniciAdi = k.KullaniciAdi, AdSoyad = k.AdSoyad,
            Zaman = zaman, ImzaYolu = imzaYolu, Ozet = ImzaHesaplayici.Ozet(Icerik(n), rol, k.KullaniciAdi, zaman, imzaYolu)
        };
    }
}

// Excel hücresine yüzde biçimiyle yazılacak değer
public readonly record struct Yuzde(double Deger);

// PDF (QuestPDF) ve Excel (ClosedXML) raporları
public class RaporServisi
{
    private readonly UretimDbContext _db;
    private readonly AnalizServisi _analiz;

    public RaporServisi(UretimDbContext db, AnalizServisi analiz)
    {
        _db = db;
        _analiz = analiz;
    }

    private const string Koyu = "#10241A";
    private const string Bakir = "#B8733A";
    private const string Cizgi = "#D9E2D6";
    private const string Soluk = "#5A6B60";
    private const string Zemin = "#F5F8F3";
    private const string Beyaz = "#FFFFFF";
    private const string Yesil = "#1E7A4C";
    private const string Sari = "#A86B12";
    private const string Kirmizi = "#B42318";

    private static string OeeRenk(double oee) => oee >= 0.85 ? Yesil : oee >= 0.60 ? Sari : Kirmizi;
    private static string Yzd(double oran) => (oran * 100).ToString("0.0") + "%";

    // ---------- Vardiya raporu verisi ----------

    public async Task<VardiyaRaporModel> VardiyaAsync(DateTime tarih, string kod)
    {
        if (!VardiyaPlani.Kodlar.Contains(kod)) kod = "A";
        var (bas, bit) = VardiyaPlani.Aralik(tarih, kod);
        var model = new VardiyaRaporModel { Tarih = tarih.Date, Vardiya = kod, Baslangic = bas, Bitis = bit };

        model.Makineler = await _analiz.OeeAsync(bas, bit);
        model.Toplam = OeeHesaplayici.Topla(model.Makineler.Select(m => m.Girdi));
        model.Duruslar = await _analiz.DuruslarAsync(bas, bit);
        model.Pareto = await _analiz.ParetoAsync(bas, bit);

        var urunler = await _db.Urunler.ToDictionaryAsync(u => u.Id, u => u.Ad);
        model.UrunUretim = (await _db.Parcalar.Where(p => p.TamamlanmaTarihi >= bas && p.TamamlanmaTarihi < bit)
                .GroupBy(p => p.UrunId).Select(g => new { g.Key, Adet = g.Count() }).ToListAsync())
            .Select(x => new GunlukDeger(urunler.GetValueOrDefault(x.Key, "?"), x.Adet))
            .OrderByDescending(x => x.Deger).ToList();
        model.OperatorIslem = (await _db.IslemKayitlari.Where(i => i.Zaman >= bas && i.Zaman < bit)
                .GroupBy(i => i.Operator!.AdSoyad).Select(g => new { g.Key, Adet = g.Count() }).ToListAsync())
            .Select(x => new GunlukDeger(x.Key, x.Adet)).OrderByDescending(x => x.Deger).ToList();
        model.Ncrler = await _db.Ncrler.Include(n => n.UretimEmri!).ThenInclude(e => e.Urun)
            .Where(n => n.AcilisTarihi >= bas && n.AcilisTarihi < bit).OrderBy(n => n.AcilisTarihi).ToListAsync();
        return model;
    }

    // ---------- PDF: ortak sayfa düzeni ----------

    private static byte[] Pdf(string baslik, string altBaslik, string belgeNo, Action<ColumnDescriptor> icerik, bool yatay = false)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(yatay ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(30);
                page.PageColor(Beyaz);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Koyu));
                page.Header().Element(h => Ust(h, baslik, altBaslik, belgeNo));
                page.Content().PaddingVertical(12).Column(icerik);
                page.Footer().Element(AltBilgi);
            });
        }).GeneratePdf();
    }

    private static void Ust(IContainer c, string baslik, string altBaslik, string belgeNo)
    {
        c.Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(x =>
                {
                    x.Item().Text(t =>
                    {
                        t.Span("ÜretimPro").FontSize(15).Bold().FontColor(Koyu);
                        t.Span("   İzlenebilirlik ve Kalite Yönetimi").FontSize(8).FontColor(Soluk);
                    });
                    x.Item().PaddingTop(6).Text(baslik).FontSize(14).Bold();
                    x.Item().Text(altBaslik).FontColor(Soluk);
                });
                r.ConstantItem(170).Column(x =>
                {
                    x.Item().AlignRight().Text(belgeNo).FontSize(11).Bold().FontColor(Bakir);
                    x.Item().AlignRight().Text("Oluşturma: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontSize(8).FontColor(Soluk);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(2).LineColor(Bakir);
        });
    }

    private static void AltBilgi(IContainer c)
    {
        c.PaddingTop(6).Row(r =>
        {
            r.RelativeItem().Text("Bu belge ÜretimPro tarafından elektronik olarak oluşturulmuştur. Örnek veridir.").FontSize(7).FontColor(Soluk);
            r.ConstantItem(90).AlignRight().Text(t =>
            {
                t.Span("Sayfa ").FontSize(7).FontColor(Soluk);
                t.CurrentPageNumber().FontSize(7).FontColor(Soluk);
                t.Span(" / ").FontSize(7).FontColor(Soluk);
                t.TotalPages().FontSize(7).FontColor(Soluk);
            });
        });
    }

    private static void Bolum(ColumnDescriptor col, string baslik)
    {
        col.Item().PaddingTop(12).PaddingBottom(4).Text(baslik).FontSize(11).Bold().FontColor(Koyu);
    }

    private static void Bilgiler(ColumnDescriptor col, params (string Etiket, string? Deger)[] satirlar)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.ConstantColumn(140);
                c.RelativeColumn();
            });
            foreach (var (etiket, deger) in satirlar)
            {
                t.Cell().BorderBottom(0.5f).BorderColor(Cizgi).PaddingVertical(4).Text(etiket).FontColor(Soluk);
                t.Cell().BorderBottom(0.5f).BorderColor(Cizgi).PaddingVertical(4).Text(string.IsNullOrWhiteSpace(deger) ? "-" : deger).SemiBold();
            }
        });
    }

    private static void Tablo(ColumnDescriptor col, string[] basliklar, float[] genislikler, IEnumerable<string[]> satirlar)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                foreach (var g in genislikler) c.RelativeColumn(g);
            });
            t.Header(h =>
            {
                foreach (var b in basliklar)
                    h.Cell().Background(Koyu).PaddingVertical(5).PaddingHorizontal(4).Text(b).FontSize(8).SemiBold().FontColor(Beyaz);
            });
            var sira = 0;
            foreach (var satir in satirlar)
            {
                var zemin = sira++ % 2 == 0 ? Beyaz : Zemin;
                foreach (var hucre in satir)
                    t.Cell().Background(zemin).BorderBottom(0.5f).BorderColor(Cizgi).PaddingVertical(4).PaddingHorizontal(4).Text(hucre);
            }
        });
    }

    private static void Kutular(ColumnDescriptor col, params (string Etiket, string Deger, string Renk)[] kutular)
    {
        col.Item().Row(r =>
        {
            foreach (var (etiket, deger, renk) in kutular)
            {
                r.RelativeItem().Padding(3).Border(0.5f).BorderColor(Cizgi).Background(Zemin).Padding(8).Column(x =>
                {
                    x.Item().Text(etiket).FontSize(8).FontColor(Soluk);
                    x.Item().Text(deger).FontSize(16).Bold().FontColor(renk);
                });
            }
        });
    }

    private static byte[] QrPng(string metin)
    {
        using var uretici = new QRCodeGenerator();
        using var veri = uretici.CreateQrCode(metin, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(veri).GetGraphic(8);
    }

    // ---------- PDF: Vardiya raporu ----------

    public byte[] VardiyaPdf(VardiyaRaporModel m)
    {
        var planli = m.Makineler.Where(x => x.Planli).ToList();
        return Pdf("Vardiya Raporu ve OEE", $"{m.Tarih:dd MMMM yyyy dddd} · Vardiya {VardiyaPlani.Ad(m.Vardiya)}",
            $"VR-{m.Tarih:yyyyMMdd}-{m.Vardiya}", col =>
            {
                Kutular(col,
                    ("OEE", Yzd(m.Toplam.Oee), OeeRenk(m.Toplam.Oee)),
                    ("Kullanılabilirlik", Yzd(m.Toplam.Kullanilabilirlik), Koyu),
                    ("Performans", Yzd(m.Toplam.Performans), Koyu),
                    ("Kalite", Yzd(m.Toplam.Kalite), Koyu),
                    ("Tamamlanan parça", m.TamamlananParca.ToString("N0"), Bakir));

                Bolum(col, "Makine bazında OEE");
                Tablo(col, new[] { "Makine", "Planlı (dk)", "Duruş (dk)", "Üretilen", "Uygun", "Kullanılabilirlik", "Performans", "Kalite", "OEE" },
                    new[] { 2.4f, 1, 1, 1, 1, 1.2f, 1.2f, 1, 1 },
                    planli.Select(x => new[]
                    {
                        $"{x.Makine.Kod} · {x.Makine.Ad}", x.Girdi.PlanliDakika.ToString("N0"), x.Girdi.DurusDakika.ToString("N0"),
                        x.Girdi.UretilenAdet.ToString(), x.Girdi.UygunAdet.ToString(), Yzd(x.Sonuc.Kullanilabilirlik),
                        Yzd(x.Sonuc.Performans), Yzd(x.Sonuc.Kalite), Yzd(x.Sonuc.Oee)
                    }));
                if (planli.Count == 0) col.Item().PaddingTop(4).Text("Bu vardiyada üretim kaydı yok.").FontColor(Soluk);

                Bolum(col, "Duruşlar");
                if (m.Duruslar.Count == 0) col.Item().Text("Bu vardiyada duruş kaydı yok.").FontColor(Soluk);
                else
                    Tablo(col, new[] { "Makine", "Neden", "Başlangıç", "Bitiş", "Süre (dk)", "Açıklama" }, new[] { 1f, 1.6f, 1, 1, 0.8f, 2.6f },
                        m.Duruslar.OrderBy(d => d.Baslangic).Select(d => new[]
                        {
                            d.Makine?.Kod ?? "", d.Neden, d.Baslangic.ToString("HH:mm"), d.Bitis?.ToString("HH:mm") ?? "devam ediyor",
                            d.Dakika(DateTime.Now).ToString("N0"), d.Aciklama ?? ""
                        }));

                if (m.Pareto.Count > 0)
                {
                    Bolum(col, "Duruş Pareto analizi");
                    Tablo(col, new[] { "Neden", "Süre (dk)", "Pay", "Kümülatif" }, new[] { 2f, 1, 1, 1 },
                        m.Pareto.Select(p => new[] { p.Etiket, p.Deger.ToString("N0"), p.Yuzde.ToString("0.0") + "%", p.KumulatifYuzde.ToString("0.0") + "%" }));
                }

                col.Item().Row(r =>
                {
                    r.RelativeItem().PaddingRight(8).Column(x =>
                    {
                        Bolum(x, "Ürün bazında üretim");
                        Tablo(x, new[] { "Ürün", "Adet" }, new[] { 2f, 1 }, m.UrunUretim.Select(u => new[] { u.Etiket, u.Deger.ToString("N0") }));
                    });
                    r.RelativeItem().PaddingLeft(8).Column(x =>
                    {
                        Bolum(x, "Operatör bazında işlem");
                        Tablo(x, new[] { "Operatör", "İşlem" }, new[] { 2f, 1 }, m.OperatorIslem.Select(u => new[] { u.Etiket, u.Deger.ToString("N0") }));
                    });
                });

                Bolum(col, "Uygunsuzluklar");
                if (m.Ncrler.Count == 0) col.Item().Text("Bu vardiyada uygunsuzluk açılmadı.").FontColor(Yesil);
                else
                    Tablo(col, new[] { "NCR", "Ürün / Lot", "Tanım", "Durum" }, new[] { 1f, 1.6f, 3.4f, 1 },
                        m.Ncrler.Select(n => new[] { n.NcrNo, $"{n.UretimEmri?.Urun?.Ad} · {n.UretimEmri?.LotNo}", n.Tanim, n.Durum }));

                col.Item().PaddingTop(24).Row(r =>
                {
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().LineHorizontal(0.5f).LineColor(Soluk);
                        x.Item().Text("Vardiya sorumlusu").FontSize(8).FontColor(Soluk);
                    });
                    r.ConstantItem(40);
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().LineHorizontal(0.5f).LineColor(Soluk);
                        x.Item().Text("Üretim müdürü").FontSize(8).FontColor(Soluk);
                    });
                });
            });
    }

    // ---------- PDF: NCR raporu ----------

    public async Task<(byte[] Dosya, string Ad)?> NcrPdfAsync(int id)
    {
        var n = await _db.Ncrler.Include(x => x.UretimEmri!).ThenInclude(e => e.Urun).Include(x => x.Parca)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (n == null) return null;
        var olcumler = n.ParcaId == null ? new List<Olcum>() : await _db.Olcumler.Include(o => o.Karakteristik).Include(o => o.OlcumAleti)
            .Include(o => o.Kullanici).Where(o => o.ParcaId == n.ParcaId).OrderBy(o => o.Zaman).ToListAsync();
        var imzalar = await _db.Imzalar.Where(i => i.Belge == "NCR" && i.BelgeId == id).OrderBy(i => i.Zaman).ToListAsync();

        var dosya = Pdf("Uygunsuzluk Raporu (NCR)", $"{n.UretimEmri?.Urun?.Ad} · Lot {n.UretimEmri?.LotNo}", n.NcrNo, col =>
        {
            Kutular(col,
                ("Durum", n.Durum, n.Durum == NcrDurum.Kapali ? Yesil : n.Durum == NcrDurum.Acik ? Kirmizi : Sari),
                ("Karar", n.Karar ?? "Bekliyor", Koyu),
                ("Kaynak", n.Kaynak, Koyu));

            Bolum(col, "1. Genel bilgiler");
            Bilgiler(col,
                ("NCR numarası", n.NcrNo),
                ("Ürün", n.UretimEmri?.Urun?.Ad),
                ("Üretim emri / lot", $"{n.UretimEmri?.EmirNo} / {n.UretimEmri?.LotNo}"),
                ("Parça seri no", n.Parca?.SeriNo),
                ("Açan", n.AcanKisi),
                ("Açılış tarihi", n.AcilisTarihi.ToString("dd.MM.yyyy HH:mm")));

            Bolum(col, "2. Uygunsuzluk tanımı");
            col.Item().Background(Zemin).Border(0.5f).BorderColor(Cizgi).Padding(8).Text(n.Tanim);

            if (olcumler.Count > 0)
            {
                Bolum(col, "3. Parçanın ölçüm kayıtları");
                Tablo(col, new[] { "Zaman", "Karakteristik", "Değer", "Tolerans", "Alet", "Ölçen", "Sonuç" }, new[] { 1.1f, 1.4f, 0.8f, 1.3f, 0.7f, 1.2f, 0.9f },
                    olcumler.Select(o => new[]
                    {
                        o.Zaman.ToString("dd.MM HH:mm"), o.Karakteristik?.Ad ?? "", o.Deger.ToString("0.000"),
                        $"{o.Karakteristik?.AltSinir:0.###} - {o.Karakteristik?.UstSinir:0.###} {o.Karakteristik?.Birim}",
                        o.OlcumAleti?.Kod ?? "", o.Kullanici?.AdSoyad ?? "", o.Uygun ? "Uygun" : "TOLERANS DIŞI"
                    }));
            }

            Bolum(col, "4. Karar ve düzeltici faaliyet");
            Bilgiler(col,
                ("Karar", n.Karar),
                ("Karar veren", n.KararVeren),
                ("Karar tarihi", n.KararTarihi?.ToString("dd.MM.yyyy HH:mm")),
                ("Kök neden / düzeltici faaliyet", n.DuzelticiFaaliyet),
                ("Kapanış tarihi", n.KapanisTarihi?.ToString("dd.MM.yyyy HH:mm")));

            Bolum(col, "5. Elektronik imzalar");
            if (imzalar.Count == 0) col.Item().Text("Henüz imza yok.").FontColor(Soluk);
            else
                Tablo(col, new[] { "Rol", "Ad soyad", "Zaman", "SHA-256 özeti", "Doğrulama" }, new[] { 1f, 1.4f, 1.1f, 3.2f, 1 },
                    imzalar.Select(i => new[]
                    {
                        i.Rol, i.AdSoyad, i.Zaman.ToString("dd.MM.yyyy HH:mm:ss"), i.Ozet, NcrImza.Gecerli(i, n) ? "Geçerli" : "GEÇERSİZ"
                    }));
            col.Item().PaddingTop(4).Text("İmza özeti; NCR numarası, tanım, karar, düzeltici faaliyet, imzalayan ve zaman bilgisinden hesaplanır. " +
                                          "Belge imzadan sonra değiştirilirse doğrulama \"GEÇERSİZ\" olur.").FontSize(7).FontColor(Soluk);
        });
        return (dosya, $"{n.NcrNo}.pdf");
    }

    // ---------- PDF: Lot uygunluk sertifikası (CoC) ----------

    public async Task<(byte[] Dosya, string Ad)?> LotSertifikaPdfAsync(string lotNo)
    {
        var emir = await _db.UretimEmirleri.Include(e => e.Urun).Include(e => e.Parcalar)
            .FirstOrDefaultAsync(e => e.LotNo == lotNo);
        if (emir == null) return null;

        var tuketimler = await _db.HammaddeTuketimleri.Include(t => t.HammaddeLotu!).ThenInclude(l => l.Malzeme)
            .Where(t => t.UretimEmriId == emir.Id).ToListAsync();
        var karakteristikler = await _db.Karakteristikler.Where(k => k.UrunId == emir.UrunId).OrderBy(k => k.Id).ToListAsync();
        var parcaIdleri = emir.Parcalar.Select(p => p.Id).ToList();
        var olcumler = await _db.Olcumler.Where(o => parcaIdleri.Contains(o.ParcaId))
            .Select(o => new { o.KarakteristikId, o.Deger, o.Uygun }).ToListAsync();
        var ncrler = await _db.Ncrler.Where(n => n.UretimEmriId == emir.Id).ToListAsync();

        var tamam = emir.Parcalar.Count(p => p.Durum == ParcaDurum.Tamamlandi);
        var hurda = emir.Parcalar.Count(p => p.Durum == ParcaDurum.Hurda);
        var kesin = emir.Durum == EmirDurum.Tamamlandi && ncrler.All(n => n.Durum == NcrDurum.Kapali || n.Karar == NcrKarar.SartliKabul);
        var qr = QrPng(emir.LotNo);

        var dosya = Pdf("Uygunluk Sertifikası (CoC)", $"{emir.Urun?.Ad} · Lot {emir.LotNo}", $"COC-{emir.LotNo}", col =>
        {
            if (!kesin)
                col.Item().PaddingBottom(8).Background("#FBEFD6").Border(0.5f).BorderColor(Sari).Padding(8)
                    .Text("ÖN RAPOR: Lot henüz tamamlanmadı veya açık uygunsuzluk var. Sevkiyat belgesi olarak kullanılamaz.").SemiBold().FontColor(Sari);

            col.Item().Row(r =>
            {
                r.RelativeItem().Column(x =>
                {
                    Bilgiler(x,
                        ("Ürün", $"{emir.Urun?.Kod} · {emir.Urun?.Ad}"),
                        ("Üretim lotu", emir.LotNo),
                        ("Üretim emri", emir.EmirNo),
                        ("Planlanan miktar", $"{emir.Miktar} adet"),
                        ("Sevke uygun", $"{tamam} adet"),
                        ("Hurda", $"{hurda} adet"),
                        ("Üretim tarihi", $"{emir.BaslamaTarihi:dd.MM.yyyy} - {emir.TamamlanmaTarihi:dd.MM.yyyy}"));
                });
                r.ConstantItem(20);
                r.ConstantItem(110).Column(x =>
                {
                    x.Item().Width(110).Image(qr);
                    x.Item().AlignCenter().Text(emir.LotNo).FontSize(8).SemiBold();
                });
            });

            Bolum(col, "Hammadde ve tedarikçi sertifikaları");
            Tablo(col, new[] { "Hammadde lotu", "Malzeme", "Tedarikçi", "Sertifika (EN 10204)", "Kullanılan" }, new[] { 1.2f, 1.4f, 1.6f, 1.3f, 0.9f },
                tuketimler.Select(t => new[]
                {
                    t.HammaddeLotu?.LotNo ?? "", t.HammaddeLotu?.Malzeme?.Ad ?? "", t.HammaddeLotu?.Tedarikci ?? "",
                    t.HammaddeLotu?.SertifikaNo ?? "", t.Miktar.ToString("N1") + " kg"
                }));

            Bolum(col, "Boyutsal kontrol özeti");
            Tablo(col, new[] { "Karakteristik", "Nominal", "Tolerans", "Ölçüm", "Min", "Maks", "Ortalama", "Cpk", "Sonuç" },
                new[] { 1.6f, 0.8f, 1.4f, 0.7f, 0.8f, 0.8f, 0.9f, 0.6f, 0.9f },
                karakteristikler.Select(k =>
                {
                    var d = olcumler.Where(o => o.KarakteristikId == k.Id).ToList();
                    if (d.Count == 0) return new[] { k.Ad, k.Nominal.ToString("0.###"), $"{k.AltSinir:0.###} - {k.UstSinir:0.###}", "0", "-", "-", "-", "-", "-" };
                    var spc = SpcHesaplayici.Analiz(d.Select(x => x.Deger).ToList(), k.AltSinir, k.UstSinir);
                    var red = d.Count(x => !x.Uygun);
                    return new[]
                    {
                        $"{k.Ad} ({k.Birim})", k.Nominal.ToString("0.###"), $"{k.AltSinir:0.###} - {k.UstSinir:0.###}", d.Count.ToString(),
                        d.Min(x => x.Deger).ToString("0.000"), d.Max(x => x.Deger).ToString("0.000"), spc.Ortalama.ToString("0.000"),
                        spc.Cpk?.ToString("0.00") ?? "-", red == 0 ? "Uygun" : $"{red} red"
                    };
                }));

            Bolum(col, "Uygunsuzluklar");
            if (ncrler.Count == 0) col.Item().Text("Bu lotta uygunsuzluk kaydı yoktur.").FontColor(Yesil);
            else
                Tablo(col, new[] { "NCR", "Tanım", "Karar", "Durum" }, new[] { 1f, 3.6f, 1.2f, 1 },
                    ncrler.Select(n => new[] { n.NcrNo, n.Tanim, n.Karar ?? "-", n.Durum }));

            col.Item().PaddingTop(14).Background(Zemin).Border(0.5f).BorderColor(Cizgi).Padding(10)
                .Text("Yukarıda belirtilen ürünlerin onaylı rota, kalibre edilmiş ölçüm aletleri ve yetkin operatörlerle üretildiğini; " +
                      "kayıtların tam izlenebilir olduğunu ve şartlı kabul kararları dışında teknik resim toleranslarına uygun olduğunu beyan ederiz.");

            col.Item().PaddingTop(30).Row(r =>
            {
                r.RelativeItem().Column(x =>
                {
                    x.Item().LineHorizontal(0.5f).LineColor(Soluk);
                    x.Item().Text("Kalite güvence").FontSize(8).FontColor(Soluk);
                });
                r.ConstantItem(40);
                r.RelativeItem().Column(x =>
                {
                    x.Item().LineHorizontal(0.5f).LineColor(Soluk);
                    x.Item().Text("Tarih / kaşe").FontSize(8).FontColor(Soluk);
                });
            });
        });
        return (dosya, $"CoC-{emir.LotNo}.pdf");
    }

    // ---------- Excel ----------

    private static void Sayfa(XLWorkbook wb, string ad, string[] basliklar, IEnumerable<object?[]> satirlar)
    {
        var ws = wb.Worksheets.Add(ad);
        for (var c = 0; c < basliklar.Length; c++) ws.Cell(1, c + 1).Value = basliklar[c];
        var baslik = ws.Range(1, 1, 1, basliklar.Length);
        baslik.Style.Font.Bold = true;
        baslik.Style.Font.FontColor = XLColor.White;
        baslik.Style.Fill.BackgroundColor = XLColor.FromHtml(Koyu);

        var r = 2;
        foreach (var satir in satirlar)
        {
            for (var c = 0; c < satir.Length; c++) Yaz(ws.Cell(r, c + 1), satir[c]);
            r++;
        }
        if (r > 2) ws.Range(1, 1, r - 1, basliklar.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void Yaz(IXLCell hucre, object? deger)
    {
        switch (deger)
        {
            case null:
                break;
            case string s:
                hucre.Value = s;
                break;
            case int i:
                hucre.Value = i;
                break;
            case double d:
                hucre.Value = Math.Round(d, 4);
                break;
            case DateTime t:
                hucre.Value = t;
                hucre.Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                break;
            case Yuzde y:
                hucre.Value = Math.Round(y.Deger, 4);
                hucre.Style.NumberFormat.Format = "0.0%";
                break;
            default:
                hucre.Value = deger.ToString();
                break;
        }
    }

    private static byte[] Kaydet(XLWorkbook wb)
    {
        using var akis = new MemoryStream();
        wb.SaveAs(akis);
        return akis.ToArray();
    }

    public byte[] VardiyaExcel(VardiyaRaporModel m)
    {
        using var wb = new XLWorkbook();
        Sayfa(wb, "OEE", new[] { "Makine", "Ad", "Planlı dk", "Duruş dk", "Üretilen", "Uygun", "Kullanılabilirlik", "Performans", "Kalite", "OEE" },
            m.Makineler.Where(x => x.Planli).Select(x => new object?[]
            {
                x.Makine.Kod, x.Makine.Ad, x.Girdi.PlanliDakika, x.Girdi.DurusDakika, x.Girdi.UretilenAdet, x.Girdi.UygunAdet,
                new Yuzde(x.Sonuc.Kullanilabilirlik), new Yuzde(x.Sonuc.Performans), new Yuzde(x.Sonuc.Kalite), new Yuzde(x.Sonuc.Oee)
            }));
        Sayfa(wb, "Duruşlar", new[] { "Makine", "Neden", "Başlangıç", "Bitiş", "Süre dk", "Açıklama", "Bildiren" },
            m.Duruslar.Select(d => new object?[] { d.Makine?.Kod, d.Neden, d.Baslangic, d.Bitis, Math.Round(d.Dakika(DateTime.Now)), d.Aciklama, d.BildirenKisi }));
        Sayfa(wb, "Pareto", new[] { "Neden", "Süre dk", "Pay", "Kümülatif" },
            m.Pareto.Select(p => new object?[] { p.Etiket, p.Deger, new Yuzde(p.Yuzde / 100), new Yuzde(p.KumulatifYuzde / 100) }));
        Sayfa(wb, "Üretim", new[] { "Ürün", "Tamamlanan" }, m.UrunUretim.Select(u => new object?[] { u.Etiket, u.Deger }));
        Sayfa(wb, "Operatörler", new[] { "Operatör", "İşlem" }, m.OperatorIslem.Select(u => new object?[] { u.Etiket, u.Deger }));
        Sayfa(wb, "NCR", new[] { "NCR", "Lot", "Ürün", "Tanım", "Durum", "Açılış" },
            m.Ncrler.Select(n => new object?[] { n.NcrNo, n.UretimEmri?.LotNo, n.UretimEmri?.Urun?.Ad, n.Tanim, n.Durum, n.AcilisTarihi }));
        return Kaydet(wb);
    }

    public byte[] DurusExcel(List<DurusKaydi> duruslar, List<ParetoSatiri> pareto)
    {
        using var wb = new XLWorkbook();
        Sayfa(wb, "Duruşlar", new[] { "Makine", "Neden", "Başlangıç", "Bitiş", "Süre dk", "Açıklama", "Bildiren" },
            duruslar.Select(d => new object?[] { d.Makine?.Kod, d.Neden, d.Baslangic, d.Bitis, Math.Round(d.Dakika(DateTime.Now)), d.Aciklama, d.BildirenKisi }));
        Sayfa(wb, "Pareto", new[] { "Neden", "Süre dk", "Pay", "Kümülatif" },
            pareto.Select(p => new object?[] { p.Etiket, p.Deger, new Yuzde(p.Yuzde / 100), new Yuzde(p.KumulatifYuzde / 100) }));
        return Kaydet(wb);
    }

    public async Task<(byte[] Dosya, string Ad)?> LotOlcumExcelAsync(string lotNo)
    {
        var emir = await _db.UretimEmirleri.FirstOrDefaultAsync(e => e.LotNo == lotNo);
        if (emir == null) return null;
        var olcumler = await _db.Olcumler.Include(o => o.Parca).Include(o => o.Karakteristik).Include(o => o.OlcumAleti).Include(o => o.Kullanici)
            .Where(o => o.Parca!.UretimEmriId == emir.Id).OrderBy(o => o.Zaman).ToListAsync();
        var makineler = await _db.Makineler.ToDictionaryAsync(m => m.Id, m => m.Kod);

        using var wb = new XLWorkbook();
        Sayfa(wb, "Ölçümler", new[] { "Zaman", "Seri no", "Karakteristik", "Birim", "Değer", "Alt sınır", "Üst sınır", "Sonuç", "Makine", "Alet", "Ölçen" },
            olcumler.Select(o => new object?[]
            {
                o.Zaman, o.Parca?.SeriNo, o.Karakteristik?.Ad, o.Karakteristik?.Birim, o.Deger, o.Karakteristik?.AltSinir, o.Karakteristik?.UstSinir,
                o.Uygun ? "Uygun" : "Tolerans dışı", makineler.GetValueOrDefault(o.MakineId), o.OlcumAleti?.Kod, o.Kullanici?.AdSoyad
            }));
        return (Kaydet(wb), $"Olcumler-{emir.LotNo}.xlsx");
    }

    public byte[] AuditExcel(List<AuditSatiri> kayitlar)
    {
        using var wb = new XLWorkbook();
        Sayfa(wb, "Audit log", new[] { "Zaman", "Kullanıcı", "Kayıt türü", "Kayıt no", "İşlem", "Alan", "Eski değer", "Yeni değer" },
            kayitlar.SelectMany(k => k.Degisiklikler.Count == 0
                ? new[] { new object?[] { k.Kayit.Zaman, k.Kayit.KullaniciAdi, k.Kayit.Tablo, k.Kayit.KayitId, k.Kayit.Islem, null, null, null } }
                : k.Degisiklikler.Select(d => new object?[] { k.Kayit.Zaman, k.Kayit.KullaniciAdi, k.Kayit.Tablo, k.Kayit.KayitId, k.Kayit.Islem, d.Alan, d.Eski, d.Yeni })));
        return Kaydet(wb);
    }
}
