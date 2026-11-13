using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Controllers;

public record OzetDto(int BugunUretilen, int AktifEmir, int AcikNcr, int CalisanMakine, int ToplamMakine, double OeeYuzde, int AcikDurus);
public record MakineDto(int Id, string Kod, string Ad, string Tur, string Durum, int BugunIslem, double OeeYuzde, double KullanilabilirlikYuzde, double PerformansYuzde, double KaliteYuzde);
public record EmirDto(int Id, string EmirNo, string LotNo, string Urun, int Miktar, int Tamamlanan, string Durum, DateTime PlanTarihi);
public record IslemDto(DateTime Zaman, int Adim, string AdimAdi, string Makine, string Operator);
public record OlcumDto(DateTime Zaman, string Karakteristik, double Deger, double AltSinir, double UstSinir, bool Uygun, string Alet);
public record ParcaGecmisiDto(string SeriNo, string Urun, string LotNo, string Durum, List<string> HammaddeLotlari, List<IslemDto> Islemler, List<OlcumDto> Olcumler, List<string> Ncrler);
public record NcrDto(int Id, string NcrNo, string LotNo, string? SeriNo, string Tanim, string Durum, string? Karar, DateTime AcilisTarihi, DateTime? KapanisTarihi);
public record ParetoDto(string Neden, double Dakika, double Yuzde, double KumulatifYuzde);
public record AgacDto(string Baslik, string? Alt, string? Rozet, string? Link, List<AgacDto> Cocuklar);

/// <summary>
/// ÜretimPro REST API (sürüm 1). Tüm uçlar oturum açmış kullanıcı ister.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ApiV1Controller : ControllerBase
{
    private readonly UretimDbContext _db;
    private readonly AnalizServisi _analiz;

    public ApiV1Controller(UretimDbContext db, AnalizServisi analiz)
    {
        _db = db;
        _analiz = analiz;
    }

    private static double Y(double oran) => Math.Round(oran * 100, 1);

    /// <summary>Kontrol merkezi özeti: bugünkü üretim, açık NCR, OEE.</summary>
    [HttpGet("ozet")]
    public async Task<ActionResult<OzetDto>> Ozet()
    {
        var bugun = DateTime.Today;
        var oee = await _analiz.ToplamOeeAsync(bugun, DateTime.Now);
        return new OzetDto(
            await _db.Parcalar.CountAsync(p => p.TamamlanmaTarihi >= bugun),
            await _db.UretimEmirleri.CountAsync(e => e.Durum == EmirDurum.Uretimde || e.Durum == EmirDurum.Bloke),
            await _db.Ncrler.CountAsync(n => n.Durum == NcrDurum.Acik),
            await _db.Makineler.CountAsync(m => m.Durum == MakineDurum.Calisiyor),
            await _db.Makineler.CountAsync(),
            Y(oee.Oee),
            await _db.DurusKayitlari.CountAsync(d => d.Bitis == null));
    }

    /// <summary>Makineler, anlık durum ve bugünkü OEE.</summary>
    [HttpGet("makineler")]
    public async Task<ActionResult<List<MakineDto>>> Makineler()
    {
        var bugun = DateTime.Today;
        var oee = await _analiz.OeeAsync(bugun, DateTime.Now);
        return oee.Select(x => new MakineDto(x.Makine.Id, x.Makine.Kod, x.Makine.Ad, x.Makine.Tur, x.Makine.Durum, x.Girdi.UygunAdet,
            Y(x.Sonuc.Oee), Y(x.Sonuc.Kullanilabilirlik), Y(x.Sonuc.Performans), Y(x.Sonuc.Kalite))).ToList();
    }

    /// <summary>Üretim emirleri (isteğe bağlı durum filtresi: Planlandı, Üretimde, Tamamlandı, Bloke).</summary>
    [HttpGet("emirler")]
    public async Task<ActionResult<List<EmirDto>>> Emirler(string? durum, int adet = 50)
    {
        var sorgu = _db.UretimEmirleri.Include(e => e.Urun).AsQueryable();
        if (!string.IsNullOrEmpty(durum)) sorgu = sorgu.Where(e => e.Durum == durum);
        return await sorgu.OrderByDescending(e => e.Id).Take(Math.Clamp(adet, 1, 500))
            .Select(e => new EmirDto(e.Id, e.EmirNo, e.LotNo, e.Urun!.Ad, e.Miktar,
                e.Parcalar.Count(p => p.Durum == ParcaDurum.Tamamlandi), e.Durum, e.PlanTarihi))
            .ToListAsync();
    }

    /// <summary>Bir parçanın tam geçmişi: hammadde, rota adımları, ölçümler, NCR.</summary>
    [HttpGet("parcalar/{seriNo}")]
    public async Task<ActionResult<ParcaGecmisiDto>> Parca(string seriNo)
    {
        var no = seriNo.ToUpperInvariant();
        var p = await _db.Parcalar.Include(x => x.Urun).Include(x => x.UretimEmri).FirstOrDefaultAsync(x => x.SeriNo == no);
        if (p == null) return NotFound();

        var hammadde = await _db.HammaddeTuketimleri.Where(t => t.UretimEmriId == p.UretimEmriId).Select(t => t.HammaddeLotu!.LotNo).ToListAsync();
        var islemler = await _db.IslemKayitlari.Where(i => i.ParcaId == p.Id).OrderBy(i => i.Zaman)
            .Select(i => new IslemDto(i.Zaman, i.RotaAdimi!.Sira, i.RotaAdimi.Ad, i.Makine!.Kod, i.Operator!.AdSoyad)).ToListAsync();
        var olcumler = await _db.Olcumler.Where(o => o.ParcaId == p.Id).OrderBy(o => o.Zaman)
            .Select(o => new OlcumDto(o.Zaman, o.Karakteristik!.Ad, o.Deger, o.Karakteristik.AltSinir, o.Karakteristik.UstSinir, o.Uygun, o.OlcumAleti!.Kod))
            .ToListAsync();
        var ncrler = await _db.Ncrler.Where(n => n.ParcaId == p.Id).Select(n => n.NcrNo).ToListAsync();
        return new ParcaGecmisiDto(p.SeriNo, p.Urun?.Ad ?? "", p.UretimEmri?.LotNo ?? "", p.Durum, hammadde, islemler, olcumler, ncrler);
    }

    /// <summary>Üretim lotunun soy ağacı (hammadde, rota, parçalar, NCR).</summary>
    [HttpGet("lotlar/{lotNo}/agac")]
    public async Task<ActionResult<AgacDto>> LotAgaci(string lotNo)
    {
        var kok = await _analiz.LotAgaciAsync(lotNo.ToUpperInvariant());
        if (kok == null) return NotFound();
        return Donustur(kok);
    }

    /// <summary>Hammadde lotundan etkilenen tüm üretim lotları (geri çağırma).</summary>
    [HttpGet("hammadde/{lotNo}/geri-cagirma")]
    public async Task<ActionResult<AgacDto>> GeriCagirma(string lotNo)
    {
        var kok = await _analiz.HammaddeAgaciAsync(lotNo.ToUpperInvariant());
        if (kok == null) return NotFound();
        return Donustur(kok);
    }

    private static AgacDto Donustur(AgacDugum d) =>
        new(d.Baslik, d.Alt, d.Rozet, d.Link, d.Cocuklar.Select(Donustur).ToList());

    /// <summary>Uygunsuzluk raporları.</summary>
    [HttpGet("ncr")]
    public async Task<ActionResult<List<NcrDto>>> Ncrler(string? durum)
    {
        var sorgu = _db.Ncrler.AsQueryable();
        if (!string.IsNullOrEmpty(durum)) sorgu = sorgu.Where(n => n.Durum == durum);
        return await sorgu.OrderByDescending(n => n.AcilisTarihi)
            .Select(n => new NcrDto(n.Id, n.NcrNo, n.UretimEmri!.LotNo, n.Parca != null ? n.Parca.SeriNo : null, n.Tanim, n.Durum, n.Karar, n.AcilisTarihi, n.KapanisTarihi))
            .ToListAsync();
    }

    /// <summary>Vardiya veya gün için makine bazında OEE. vardiya: A, B, C (boşsa tüm gün).</summary>
    [HttpGet("oee")]
    public async Task<ActionResult<List<MakineDto>>> Oee(DateTime? tarih, string? vardiya)
    {
        var t = (tarih ?? DateTime.Today).Date;
        var (bas, bit) = string.IsNullOrEmpty(vardiya) ? (t, t.AddDays(1)) : VardiyaPlani.Aralik(t, vardiya.ToUpperInvariant());
        var oee = await _analiz.OeeAsync(bas, bit);
        return oee.Where(x => x.Planli).Select(x => new MakineDto(x.Makine.Id, x.Makine.Kod, x.Makine.Ad, x.Makine.Tur, x.Makine.Durum, x.Girdi.UygunAdet,
            Y(x.Sonuc.Oee), Y(x.Sonuc.Kullanilabilirlik), Y(x.Sonuc.Performans), Y(x.Sonuc.Kalite))).ToList();
    }

    /// <summary>Duruş nedenlerinin Pareto analizi (son n gün).</summary>
    [HttpGet("duruslar/pareto")]
    public async Task<ActionResult<List<ParetoDto>>> Pareto(int gun = 30)
    {
        var bas = DateTime.Today.AddDays(-Math.Clamp(gun, 1, 90) + 1);
        var pareto = await _analiz.ParetoAsync(bas, DateTime.Now);
        return pareto.Select(p => new ParetoDto(p.Etiket, p.Deger, Math.Round(p.Yuzde, 1), Math.Round(p.KumulatifYuzde, 1))).ToList();
    }
}
