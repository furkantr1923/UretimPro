using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;

namespace UretimPro.Services;

// İstasyon ekranının cevabı (JSON olarak tarayıcıya döner)
public record IstasyonCevabi
{
    public string Durum { get; init; } = "hata";          // tamam / olcum / hata / red
    public string Baslik { get; init; } = "";
    public string Mesaj { get; init; } = "";
    public string? SeriNo { get; init; }
    public string? Urun { get; init; }
    public string? EmirNo { get; init; }
    public string? Adim { get; init; }
    public int AdimSira { get; init; }
    public int AdimSayisi { get; init; }
    public string? NcrNo { get; init; }
    public bool EmirTamamlandi { get; init; }
    public int EmirTamamlanan { get; init; }
    public int EmirToplam { get; init; }
    public List<OlcumAlani> Olcumler { get; init; } = new();
    public List<AletSecenegi> Aletler { get; init; } = new();
}

public record OlcumAlani(int Id, string Ad, string Birim, double Nominal, double Alt, double Ust, string AletTuru);
public record AletSecenegi(int Id, string Kod, string Ad, string Tur, bool Gecerli, string SonrakiKalibrasyon);
public record OlcumGirdisi(int KarakteristikId, string? Deger, int AletId);

public class UretimServisi
{
    private readonly UretimDbContext _db;
    private readonly BildirimServisi _bildirim;
    private readonly CanliYayin _yayin;

    public UretimServisi(UretimDbContext db, BildirimServisi bildirim, CanliYayin yayin)
    {
        _db = db;
        _bildirim = bildirim;
        _yayin = yayin;
    }

    // ---------- Üretim emri ----------

    public async Task<UretimEmri> EmirOlusturAsync(int urunId, int miktar, DateTime planTarihi)
    {
        var urun = await _db.Urunler.FirstAsync(u => u.Id == urunId);
        var emirNolari = await _db.UretimEmirleri.Select(e => e.EmirNo).ToListAsync();
        var ayBasi = new DateTime(planTarihi.Year, planTarihi.Month, 1);
        var aydakiLot = await _db.UretimEmirleri.CountAsync(e => e.PlanTarihi >= ayBasi && e.PlanTarihi < ayBasi.AddMonths(1));

        var emir = new UretimEmri
        {
            EmirNo = Numara.EmirNo(Numara.Siradaki(emirNolari)),
            LotNo = await BenzersizLotNo(planTarihi, aydakiLot + 1),
            UrunId = urun.Id,
            Miktar = miktar,
            PlanTarihi = planTarihi,
            OlusturmaTarihi = DateTime.Now,
            Durum = EmirDurum.Planlandi
        };

        for (var i = 0; i < miktar; i++)
        {
            urun.SonSeriNo++;
            emir.Parcalar.Add(new Parca
            {
                SeriNo = Numara.SeriNo(urun.Kod, urun.SonSeriNo),
                UrunId = urun.Id,
                Durum = ParcaDurum.Bekliyor
            });
        }

        _db.UretimEmirleri.Add(emir);
        await _db.SaveChangesAsync();
        return emir;
    }

    private async Task<string> BenzersizLotNo(DateTime tarih, int sira)
    {
        while (true)
        {
            var lotNo = Numara.LotNo(tarih, sira);
            if (!await _db.UretimEmirleri.AnyAsync(e => e.LotNo == lotNo)) return lotNo;
            sira++;
        }
    }

    // FIFO önerisi: en eski hammadde lotlarından başlar
    public async Task<(List<LotPayi> Paylar, double Eksik, double Ihtiyac)> FifoOnerisiAsync(int emirId, int? secilenLotId = null)
    {
        var emir = await _db.UretimEmirleri.Include(e => e.Urun).FirstAsync(e => e.Id == emirId);
        var ihtiyac = Math.Round(emir.Miktar * emir.Urun!.BirimTuketim, 3);

        var lotlar = await _db.HammaddeLotlari
            .Where(l => l.MalzemeId == emir.Urun.MalzemeId && l.KalanMiktar > 0)
            .Select(l => new LotStok(l.Id, l.GirisTarihi, l.KalanMiktar))
            .ToListAsync();

        if (secilenLotId is int secilen)
        {
            // Kullanıcının seçtiği lot önce kullanılır, eksik kalırsa FIFO ile tamamlanır
            var oncelikli = lotlar.Where(l => l.LotId == secilen)
                .Select(l => l with { GirisTarihi = DateTime.MinValue });
            lotlar = oncelikli.Concat(lotlar.Where(l => l.LotId != secilen)).ToList();
        }

        var (paylar, eksik) = FifoSecici.Sec(lotlar, ihtiyac);
        return (paylar, eksik, ihtiyac);
    }

    public async Task<string?> EmirBaslatAsync(int emirId, int? secilenLotId)
    {
        var emir = await _db.UretimEmirleri.Include(e => e.Urun).FirstOrDefaultAsync(e => e.Id == emirId);
        if (emir == null) return "Üretim emri bulunamadı.";
        if (emir.Durum != EmirDurum.Planlandi) return "Sadece planlanmış emirler başlatılabilir.";

        var (paylar, eksik, _) = await FifoOnerisiAsync(emirId, secilenLotId);
        if (eksik > 0) return $"Yetersiz hammadde: {eksik:0.##} kg eksik. Önce depoya hammadde girişi yapın.";

        foreach (var pay in paylar)
        {
            var lot = await _db.HammaddeLotlari.FirstAsync(l => l.Id == pay.LotId);
            lot.KalanMiktar = Math.Round(lot.KalanMiktar - pay.Miktar, 3);
            _db.HammaddeTuketimleri.Add(new HammaddeTuketimi
            {
                UretimEmriId = emir.Id,
                HammaddeLotuId = lot.Id,
                Miktar = pay.Miktar,
                Zaman = DateTime.Now
            });
        }

        emir.Durum = EmirDurum.Uretimde;
        emir.BaslamaTarihi = DateTime.Now;
        await _db.Parcalar.Where(p => p.UretimEmriId == emir.Id && p.Durum == ParcaDurum.Bekliyor)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Durum, ParcaDurum.Uretimde));
        await _db.SaveChangesAsync();

        await KritikStokKontrolAsync(emir.Urun!.MalzemeId);
        return null;
    }

    private async Task KritikStokKontrolAsync(int malzemeId)
    {
        var malzeme = await _db.Malzemeler.FirstAsync(m => m.Id == malzemeId);
        var toplam = await _db.HammaddeLotlari.Where(l => l.MalzemeId == malzemeId).SumAsync(l => l.KalanMiktar);
        if (toplam < malzeme.MinimumStok)
        {
            await _bildirim.GonderAsync("Kritik stok", $"{malzeme.Ad}: kalan {toplam:0} kg, minimum {malzeme.MinimumStok:0} kg",
                "uyari", "/depo", Roller.DepoBakim, Roller.Yonetici);
        }
    }

    // ---------- İstasyon: QR / barkod okutma ----------

    public async Task<IstasyonCevabi> OkutAsync(int makineId, string seriNo, int kullaniciId)
    {
        var kontrol = await KontrolAsync(makineId, seriNo, kullaniciId);
        if (kontrol.Hata != null) return kontrol.Hata;

        var (parca, adim, adimlar, makine) = (kontrol.Parca!, kontrol.Adim!, kontrol.Adimlar!, kontrol.Makine!);
        var karakteristikler = await _db.Karakteristikler.Where(k => k.RotaAdimiId == adim.Id).ToListAsync();

        if (karakteristikler.Count > 0)
        {
            var aletler = await _db.OlcumAletleri.OrderBy(a => a.Kod).ToListAsync();
            var simdi = DateTime.Now;
            return Bilgili(parca, adim, adimlar) with
            {
                Durum = "olcum",
                Baslik = "Ölçüm gerekli",
                Mesaj = $"{adim.Ad} adımı için {karakteristikler.Count} ölçüm girin.",
                Olcumler = karakteristikler.Select(k => new OlcumAlani(k.Id, k.Ad, k.Birim, k.Nominal, k.AltSinir, k.UstSinir, k.AletTuru)).ToList(),
                Aletler = aletler.Select(a => new AletSecenegi(a.Id, a.Kod, a.Ad, a.Tur, a.GecerliMi(simdi), a.SonrakiKalibrasyon.ToString("dd.MM.yyyy"))).ToList()
            };
        }

        return await AdimiTamamlaAsync(parca, adim, adimlar, makine, kullaniciId);
    }

    public async Task<IstasyonCevabi> OlcumKaydetAsync(int makineId, string seriNo, int kullaniciId, List<OlcumGirdisi> girdiler)
    {
        var kontrol = await KontrolAsync(makineId, seriNo, kullaniciId);
        if (kontrol.Hata != null) return kontrol.Hata;

        var (parca, adim, adimlar, makine) = (kontrol.Parca!, kontrol.Adim!, kontrol.Adimlar!, kontrol.Makine!);
        var karakteristikler = await _db.Karakteristikler.Where(k => k.RotaAdimiId == adim.Id).ToListAsync();
        var aletler = await _db.OlcumAletleri.ToDictionaryAsync(a => a.Id);
        var simdi = DateTime.Now;

        // Önce tüm girdileri doğrula
        var olcumler = new List<Olcum>();
        foreach (var k in karakteristikler)
        {
            var girdi = girdiler.FirstOrDefault(g => g.KarakteristikId == k.Id);
            if (girdi == null || !Sayi.Coz(girdi.Deger, out var deger))
                return Hata("Eksik ölçüm", $"{k.Ad} için geçerli bir değer girin.", parca, adim, adimlar);

            if (!aletler.TryGetValue(girdi.AletId, out var alet))
                return Hata("Ölçüm aleti seçilmedi", $"{k.Ad} için ölçüm aleti seçin.", parca, adim, adimlar);

            if (!alet.GecerliMi(simdi))
                return Hata("Kalibrasyon süresi geçmiş",
                    $"{alet.Kod} {alet.Ad} aletinin kalibrasyonu {alet.SonrakiKalibrasyon:dd.MM.yyyy} tarihinde sona erdi. Bu aletle ölçüm kaydedilemez.",
                    parca, adim, adimlar);

            olcumler.Add(new Olcum
            {
                ParcaId = parca.Id,
                KarakteristikId = k.Id,
                MakineId = makine.Id,
                Deger = deger,
                Uygun = deger >= k.AltSinir && deger <= k.UstSinir,
                OlcumAletiId = alet.Id,
                KullaniciId = kullaniciId,
                Zaman = simdi
            });
        }

        _db.Olcumler.AddRange(olcumler);
        var uygunsuzlar = olcumler.Where(o => !o.Uygun).ToList();

        if (uygunsuzlar.Count > 0)
        {
            // Tolerans dışı: parça ve lot bloke, otomatik NCR
            var emir = parca.UretimEmri!;
            parca.Durum = ParcaDurum.Bloke;
            emir.Durum = EmirDurum.Bloke;

            var aciklama = string.Join("; ", uygunsuzlar.Select(o =>
            {
                var k = karakteristikler.First(x => x.Id == o.KarakteristikId);
                return $"{k.Ad} {o.Deger:0.###} {k.Birim} (tolerans {k.AltSinir:0.###}–{k.UstSinir:0.###})";
            }));

            var ncrNolari = await _db.Ncrler.Select(n => n.NcrNo).ToListAsync();
            var kullanici = await _db.Kullanicilar.FirstAsync(k => k.Id == kullaniciId);
            var ncr = new Ncr
            {
                NcrNo = Numara.NcrNo(Numara.Siradaki(ncrNolari)),
                UretimEmriId = emir.Id,
                ParcaId = parca.Id,
                Kaynak = "Otomatik · ölçüm",
                Tanim = $"{parca.SeriNo} tolerans dışı: {aciklama}",
                Durum = NcrDurum.Acik,
                AcanKisi = kullanici.AdSoyad,
                AcilisTarihi = simdi
            };
            _db.Ncrler.Add(ncr);
            await _db.SaveChangesAsync();

            await _bildirim.GonderAsync($"{ncr.NcrNo} · Tolerans dışı parça",
                $"{parca.SeriNo} ({makine.Kod}) bloke edildi. Lot {emir.LotNo} karar bekliyor.",
                "kritik", $"/ncr/detay/{ncr.Id}", Roller.Kalite, Roller.Yonetici, Roller.Admin);

            return Bilgili(parca, adim, adimlar) with
            {
                Durum = "red",
                Baslik = "TOLERANS DIŞI · PARÇA BLOKE",
                Mesaj = $"{aciklama}. {ncr.NcrNo} otomatik açıldı, lot {emir.LotNo} bloke edildi. Kalite ekibine bildirildi.",
                NcrNo = ncr.NcrNo
            };
        }

        var cevap = await AdimiTamamlaAsync(parca, adim, adimlar, makine, kullaniciId);
        await SpcKontrolAsync(karakteristikler, makine);
        return cevap;
    }

    // ---------- Ortak kontroller ----------

    private record KontrolSonucu(IstasyonCevabi? Hata, Parca? Parca, RotaAdimi? Adim, List<RotaAdimi>? Adimlar, Makine? Makine);

    private async Task<KontrolSonucu> KontrolAsync(int makineId, string seriNo, int kullaniciId)
    {
        var kod = (seriNo ?? "").Trim().ToUpperInvariant();
        var makine = await _db.Makineler.FirstOrDefaultAsync(m => m.Id == makineId);
        if (makine == null) return new(Hata("İstasyon bulunamadı", "Önce bir istasyon seçin."), null, null, null, null);

        if (makine.Durum is MakineDurum.Arizali or MakineDurum.Bakimda)
            return new(Hata("Makine kullanılamaz", $"{makine.Kod} şu anda {makine.Durum.ToLowerInvariant()}. Bakım ekibiyle görüşün."), null, null, null, null);

        var parca = await _db.Parcalar.Include(p => p.UretimEmri).Include(p => p.Urun)
            .FirstOrDefaultAsync(p => p.SeriNo == kod);
        if (parca == null) return new(Hata("Seri no bulunamadı", $"\"{kod}\" sistemde kayıtlı değil."), null, null, null, null);

        var adimlar = await _db.RotaAdimlari.Include(a => a.Makine)
            .Where(a => a.UrunId == parca.UrunId).OrderBy(a => a.Sira).ToListAsync();
        var emir = parca.UretimEmri!;

        if (emir.Durum == EmirDurum.Planlandi)
            return new(Hata("Emir başlatılmamış", $"{emir.EmirNo} henüz üretime alınmadı.", parca, null, adimlar), null, null, null, null);
        if (emir.Durum == EmirDurum.Iptal)
            return new(Hata("Emir iptal edildi", $"{emir.EmirNo} iptal edilmiş.", parca, null, adimlar), null, null, null, null);
        if (parca.Durum == ParcaDurum.Bloke || emir.Durum == EmirDurum.Bloke)
            return new(Hata("Lot bloke", $"Lot {emir.LotNo} açık bir uygunsuzluk nedeniyle bloke. Kalite kararı bekleniyor.", parca, null, adimlar), null, null, null, null);
        if (parca.Durum == ParcaDurum.Hurda)
            return new(Hata("Hurda parça", $"{parca.SeriNo} hurdaya ayrılmış.", parca, null, adimlar), null, null, null, null);
        if (parca.Durum == ParcaDurum.Tamamlandi || parca.TamamlananAdim >= adimlar.Count)
            return new(Hata("Parça tamamlanmış", $"{parca.SeriNo} tüm rota adımlarını tamamladı.", parca, null, adimlar), null, null, null, null);

        var adim = adimlar[parca.TamamlananAdim];
        if (adim.MakineId != makine.Id)
            return new(Hata("Yanlış istasyon · rota sırası",
                $"Bu parçanın sıradaki adımı {adim.Sira}. {adim.Ad} ({adim.Makine?.Kod}). Rota adımları atlanamaz.",
                parca, adim, adimlar), null, null, null, null);

        var simdi = DateTime.Now;
        var yetkin = await _db.Yetkinlikler.AnyAsync(y => y.KullaniciId == kullaniciId && y.MakineId == makine.Id && y.GecerlilikBitis >= simdi);
        if (!yetkin)
            return new(Hata("Yetkinlik yok",
                $"{makine.Kod} için geçerli bir yetkinlik belgen yok ya da süresi dolmuş. Bu istasyonda işlem yapamazsın.",
                parca, adim, adimlar), null, null, null, null);

        return new(null, parca, adim, adimlar, makine);
    }

    private async Task<IstasyonCevabi> AdimiTamamlaAsync(Parca parca, RotaAdimi adim, List<RotaAdimi> adimlar, Makine makine, int kullaniciId)
    {
        var simdi = DateTime.Now;
        _db.IslemKayitlari.Add(new IslemKaydi
        {
            ParcaId = parca.Id,
            RotaAdimiId = adim.Id,
            MakineId = makine.Id,
            OperatorId = kullaniciId,
            Zaman = simdi
        });

        parca.TamamlananAdim++;
        parca.Durum = ParcaDurum.Uretimde;
        var sonAdim = parca.TamamlananAdim >= adimlar.Count;
        if (sonAdim)
        {
            parca.Durum = ParcaDurum.Tamamlandi;
            parca.TamamlanmaTarihi = simdi;
        }
        makine.Durum = MakineDurum.Calisiyor;
        await _db.SaveChangesAsync();

        var emir = parca.UretimEmri!;
        var toplam = await _db.Parcalar.CountAsync(p => p.UretimEmriId == emir.Id);
        var bitti = await _db.Parcalar.CountAsync(p => p.UretimEmriId == emir.Id && (p.Durum == ParcaDurum.Tamamlandi || p.Durum == ParcaDurum.Hurda));
        var tamamlanan = await _db.Parcalar.CountAsync(p => p.UretimEmriId == emir.Id && p.Durum == ParcaDurum.Tamamlandi);
        var emirBitti = false;

        if (bitti == toplam && emir.Durum != EmirDurum.Tamamlandi)
        {
            emir.Durum = EmirDurum.Tamamlandi;
            emir.TamamlanmaTarihi = simdi;
            await _db.SaveChangesAsync();
            emirBitti = true;
            await _bildirim.GonderAsync($"{emir.EmirNo} tamamlandı",
                $"{parca.Urun?.Ad} · {tamamlanan} adet üretildi (Lot {emir.LotNo}).",
                "bilgi", $"/uretim/detay/{emir.Id}", Roller.Yonetici, Roller.Admin, Roller.DepoBakim);
        }

        var kullanici = await _db.Kullanicilar.FirstAsync(k => k.Id == kullaniciId);
        _yayin.Yayinla(new
        {
            tur = "islem",
            seriNo = parca.SeriNo,
            adim = adim.Ad,
            makine = makine.Kod,
            kullanici = kullanici.AdSoyad,
            zaman = simdi.ToString("HH:mm:ss")
        });

        return Bilgili(parca, adim, adimlar) with
        {
            Durum = "tamam",
            Baslik = sonAdim ? "Parça tamamlandı" : "Adım kaydedildi",
            Mesaj = sonAdim
                ? $"{parca.SeriNo} tüm rotayı tamamladı."
                : $"{adim.Ad} tamam. Sıradaki: {adimlar[parca.TamamlananAdim].Ad} ({adimlar[parca.TamamlananAdim].Makine?.Kod}).",
            EmirTamamlandi = emirBitti,
            EmirTamamlanan = tamamlanan,
            EmirToplam = toplam
        };
    }

    // Son ölçümler SPC kurallarını ihlal ediyorsa kaliteye erken uyarı
    private async Task SpcKontrolAsync(List<KaliteKarakteristigi> karakteristikler, Makine makine)
    {
        foreach (var k in karakteristikler)
        {
            var son = await _db.Olcumler
                .Where(o => o.KarakteristikId == k.Id && o.MakineId == makine.Id)
                .OrderByDescending(o => o.Zaman).Take(30)
                .Select(o => o.Deger).ToListAsync();
            son.Reverse();
            if (son.Count < 8) continue;

            var sonuc = SpcHesaplayici.Analiz(son, k.AltSinir, k.UstSinir);
            var ihlal = sonuc.Ihlaller.Where(i => i.Indeks == son.Count - 1).OrderBy(i => i.Kural).FirstOrDefault();
            if (ihlal == null) continue;

            await _bildirim.GonderAsync($"SPC erken uyarı · {makine.Kod}",
                $"{k.Ad}: {ihlal.Aciklama} (kural {ihlal.Kural}). Henüz tolerans içinde, makine ayarını kontrol edin.",
                "uyari", $"/kalite?karakteristikId={k.Id}&makineId={makine.Id}", Roller.Kalite, Roller.Yonetici, Roller.DepoBakim);
        }
    }

    private static IstasyonCevabi Bilgili(Parca parca, RotaAdimi? adim, List<RotaAdimi>? adimlar) => new()
    {
        SeriNo = parca.SeriNo,
        Urun = parca.Urun?.Ad,
        EmirNo = parca.UretimEmri?.EmirNo,
        Adim = adim?.Ad,
        AdimSira = adim?.Sira ?? 0,
        AdimSayisi = adimlar?.Count ?? 0
    };

    private static IstasyonCevabi Hata(string baslik, string mesaj, Parca? parca = null, RotaAdimi? adim = null, List<RotaAdimi>? adimlar = null)
    {
        var temel = parca != null ? Bilgili(parca, adim, adimlar) : new IstasyonCevabi();
        return temel with { Durum = "hata", Baslik = baslik, Mesaj = mesaj };
    }

    // ---------- NCR kararları ----------

    public async Task<string?> NcrKararAsync(int ncrId, string karar, string kararVeren)
    {
        var ncr = await _db.Ncrler.Include(n => n.Parca).Include(n => n.UretimEmri)
            .FirstOrDefaultAsync(n => n.Id == ncrId);
        if (ncr == null) return "NCR bulunamadı.";
        if (ncr.Durum != NcrDurum.Acik) return "Bu NCR için zaten karar verilmiş.";
        if (!NcrKarar.Tumu.Contains(karar)) return "Geçersiz karar.";

        ncr.Karar = karar;
        ncr.Durum = NcrDurum.KararVerildi;
        ncr.KararVeren = kararVeren;
        ncr.KararTarihi = DateTime.Now;

        if (ncr.Parca is Parca parca)
        {
            var adimSayisi = await _db.RotaAdimlari.CountAsync(a => a.UrunId == parca.UrunId);
            if (karar == NcrKarar.Hurda)
            {
                parca.Durum = ParcaDurum.Hurda;
            }
            else if (karar == NcrKarar.YenidenIsleme)
            {
                parca.Durum = ParcaDurum.Uretimde;   // aynı adımda yeniden ölçülür
            }
            else
            {
                parca.TamamlananAdim++;   // şartlı kabul: ölçüm adımı geçilmiş sayılır
                parca.Durum = parca.TamamlananAdim >= adimSayisi ? ParcaDurum.Tamamlandi : ParcaDurum.Uretimde;
                if (parca.Durum == ParcaDurum.Tamamlandi) parca.TamamlanmaTarihi = DateTime.Now;
            }
        }

        await _db.SaveChangesAsync();

        // Lotta açık NCR kalmadıysa bloke kalkar
        var emir = ncr.UretimEmri!;
        var acikVar = await _db.Ncrler.AnyAsync(n => n.UretimEmriId == emir.Id && n.Durum == NcrDurum.Acik);
        if (!acikVar && emir.Durum == EmirDurum.Bloke)
        {
            var kalan = await _db.Parcalar.AnyAsync(p => p.UretimEmriId == emir.Id && p.Durum != ParcaDurum.Tamamlandi && p.Durum != ParcaDurum.Hurda);
            emir.Durum = kalan ? EmirDurum.Uretimde : EmirDurum.Tamamlandi;
            if (!kalan) emir.TamamlanmaTarihi = DateTime.Now;
            await _db.SaveChangesAsync();
            await _bildirim.GonderAsync($"Lot {emir.LotNo} serbest bırakıldı",
                $"{ncr.NcrNo} kararı: {karar}. Üretim devam edebilir.", "bilgi", $"/uretim/detay/{emir.Id}",
                Roller.Operator, Roller.Yonetici, Roller.Kalite);
        }
        return null;
    }
}
