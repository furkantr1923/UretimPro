using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Data;

// İlk açılışta 30 günlük gerçekçi fabrika geçmişi oluşturur
public static class OrnekVeri
{
    public const string DemoSifre = "Demo123!";

    private record Senaryo(string Tur, string UrunKod, int ParcaIndeks, string KarakteristikAd);

    public static void Olustur(UretimDbContext db)
    {
        if (db.Kullanicilar.Any()) return;
        db.DenetimKapali = true;

        var rnd = new Random(2026);
        var simdi = DateTime.Now;
        var bugun = simdi.Date;
        var hasher = new PasswordHasher<Kullanici>();

        // ---------- Kullanıcılar ----------
        Kullanici K(string kAdi, string adSoyad, string rol)
        {
            var k = new Kullanici
            {
                KullaniciAdi = kAdi,
                AdSoyad = adSoyad,
                Rol = rol,
                SonGiris = simdi.AddHours(-rnd.Next(2, 40)),
                SonBildirimOkuma = simdi.AddHours(-6)
            };
            k.SifreHash = hasher.HashPassword(k, DemoSifre);
            return k;
        }

        var admin = K("admin", "Furkan Baykal", Roller.Admin);
        var yonetici = K("yonetici", "Selin Aksoy", Roller.Yonetici);
        var kalite = K("kalite", "Murat Demir", Roller.Kalite);
        var ali = K("operator", "Ali Yılmaz", Roller.Operator);
        var emre = K("emre", "Emre Kaya", Roller.Operator);
        var zeynep = K("zeynep", "Zeynep Şahin", Roller.Operator);
        var burak = K("burak", "Burak Çelik", Roller.Operator);
        var depo = K("depo", "Hakan Öztürk", Roller.DepoBakim);
        db.Kullanicilar.AddRange(admin, yonetici, kalite, ali, emre, zeynep, burak, depo);

        // ---------- Makineler ----------
        // Konum: fabrika yerleşim planındaki yeri (1280x560 plan), ideal çevrim: OEE performansı için
        Makine M(string kod, string ad, string tur, int gun, int adet, int sonBakimGunOnce, int x, int y, int idealSn) => new()
        {
            Kod = kod, Ad = ad, Tur = tur,
            BakimPeriyotGun = gun, BakimPeriyotAdet = adet,
            SonBakimTarihi = bugun.AddDays(-sonBakimGunOnce).AddHours(9),
            KonumX = x, KonumY = y, IdealCevrimSn = idealSn,
            Durum = MakineDurum.Bekliyor
        };

        var kesim = M("KSM-1", "Şerit testere", "Kesim", 30, 8000, 9, 190, 232, 180);
        var cnc1 = M("CNC-1", "Dik işleme merkezi", "CNC", 30, 5000, 14, 400, 90, 240);
        var cnc2 = M("CNC-2", "Dik işleme merkezi", "CNC", 30, 5000, 6, 400, 232, 240);
        var cnc3 = M("CNC-3", "Torna-freze merkezi", "CNC", 30, 5000, 28, 400, 374, 240);
        var trn1 = M("TRN-1", "CNC torna", "Torna", 45, 6000, 20, 600, 90, 240);
        var trn2 = M("TRN-2", "CNC torna", "Torna", 45, 6000, 11, 600, 232, 240);
        var pres = M("PRS-1", "Hidrolik pres", "Pres", 30, 8000, 33, 600, 374, 200);
        var taslama = M("TSL-1", "Silindirik taşlama", "Taşlama", 60, 4000, 18, 800, 160, 300);
        var olcumIst = M("KLT-1", "Ölçüm istasyonu", "Kalite", 90, 20000, 40, 800, 330, 180);
        var paket = M("PKT-1", "Paketleme hattı", "Paketleme", 90, 20000, 25, 1000, 232, 150);
        var makineler = new[] { kesim, cnc1, cnc2, cnc3, trn1, trn2, pres, taslama, olcumIst, paket };
        db.Makineler.AddRange(makineler);

        // ---------- Malzemeler ----------
        var s355 = new Malzeme { Ad = "Çelik S355", MinimumStok = 1500 };
        var c45 = new Malzeme { Ad = "Çelik C45", MinimumStok = 300 };
        var pirinc = new Malzeme { Ad = "Pirinç CuZn39", MinimumStok = 100 };
        var aluminyum = new Malzeme { Ad = "Alüminyum 6061", MinimumStok = 200 };
        db.Malzemeler.AddRange(s355, c45, pirinc, aluminyum);

        // ---------- Ölçüm aletleri ----------
        OlcumAleti A(string kod, string ad, string tur, int sonGunOnce, int periyot) => new()
        {
            Kod = kod, Ad = ad, Tur = tur, SonKalibrasyon = bugun.AddDays(-sonGunOnce), PeriyotGun = periyot
        };
        var aletler = new[]
        {
            A("MK-07", "Dış çap mikrometresi 100-125", "Mikrometre", 80, 90),
            A("MK-03", "Dış çap mikrometresi 25-50", "Mikrometre", 30, 180),
            A("KU-03", "Dijital kumpas 0-300", "Kumpas", 60, 180),
            A("KU-05", "Dijital kumpas 0-150", "Kumpas", 190, 180),
            A("IC-02", "İç çap mikrometresi", "İç çap mikrometresi", 45, 180),
            A("PR-01", "Yüzey pürüzlülük cihazı", "Pürüzlülük cihazı", 100, 365),
            A("CS-02", "Komparatör (çap saati)", "Çap saati", 200, 180)
        };
        db.OlcumAletleri.AddRange(aletler);
        db.SaveChanges();

        // ---------- Ürünler, rotalar, karakteristikler ----------
        Urun U(string kod, string ad, Malzeme mlz, double tuketim, int sonSeri) => new()
        {
            Kod = kod, Ad = ad, MalzemeId = mlz.Id, BirimTuketim = tuketim, SonSeriNo = sonSeri
        };
        var fl120 = U("FL120", "Flanş FL-120", s355, 2.4, 600);
        var fl90 = U("FL90", "Flanş FL-90", s355, 1.6, 350);
        var m40 = U("M40", "Mil M-40", c45, 1.1, 120);
        var b25 = U("B25", "Burç B-25", pirinc, 0.3, 1500);
        var k60 = U("K60", "Kapak K-60", aluminyum, 0.8, 400);
        var urunler = new[] { fl120, fl90, m40, b25, k60 };
        db.Urunler.AddRange(urunler);
        db.SaveChanges();

        var rotalar = new Dictionary<Urun, List<RotaAdimi>>();
        void Rota(Urun u, params (string Ad, Makine M)[] adimlar)
        {
            var liste = adimlar.Select((a, i) => new RotaAdimi { UrunId = u.Id, Sira = i + 1, Ad = a.Ad, MakineId = a.M.Id }).ToList();
            db.RotaAdimlari.AddRange(liste);
            rotalar[u] = liste;
        }
        Rota(fl120, ("Kesim", kesim), ("CNC işleme", cnc3), ("Son kontrol", olcumIst), ("Paketleme", paket));
        Rota(fl90, ("Kesim", kesim), ("CNC işleme", cnc1), ("Son kontrol", olcumIst), ("Paketleme", paket));
        Rota(m40, ("Kesim", kesim), ("Tornalama", trn2), ("Taşlama", taslama), ("Paketleme", paket));
        Rota(b25, ("Kesim", kesim), ("Tornalama", trn1), ("Son kontrol", olcumIst), ("Paketleme", paket));
        Rota(k60, ("Kesim", kesim), ("Presleme", pres), ("Delik delme", cnc2), ("Paketleme", paket));
        db.SaveChanges();

        var karakteristikler = new List<KaliteKarakteristigi>();
        void Kar(Urun u, int adimSira, string ad, string birim, double nominal, double alt, double ust, string aletTuru)
        {
            var k = new KaliteKarakteristigi
            {
                UrunId = u.Id, RotaAdimiId = rotalar[u][adimSira - 1].Id, Ad = ad, Birim = birim,
                Nominal = nominal, AltSinir = alt, UstSinir = ust, AletTuru = aletTuru
            };
            karakteristikler.Add(k);
        }
        Kar(fl120, 2, "Dış çap", "mm", 120.00, 119.95, 120.05, "Mikrometre");
        Kar(fl120, 3, "Kalınlık", "mm", 12.00, 11.90, 12.10, "Kumpas");
        Kar(fl90, 2, "Dış çap", "mm", 90.00, 89.95, 90.05, "Mikrometre");
        Kar(fl90, 3, "Kalınlık", "mm", 10.00, 9.90, 10.10, "Kumpas");
        Kar(m40, 2, "Çap", "mm", 40.000, 39.980, 40.020, "Mikrometre");
        Kar(m40, 2, "Boy", "mm", 250.0, 249.8, 250.2, "Kumpas");
        Kar(m40, 3, "Yüzey pürüzlülüğü Ra", "µm", 0.40, 0.00, 0.80, "Pürüzlülük cihazı");
        Kar(b25, 2, "İç çap", "mm", 25.000, 24.970, 25.030, "İç çap mikrometresi");
        Kar(b25, 3, "Dış çap", "mm", 32.00, 31.95, 32.05, "Mikrometre");
        Kar(k60, 3, "Delik çapı", "mm", 8.00, 7.95, 8.05, "İç çap mikrometresi");
        db.Karakteristikler.AddRange(karakteristikler);

        // ---------- Yetkinlik matrisi ----------
        void Y(Kullanici k, Makine m, int gecerlilikGun) =>
            db.Yetkinlikler.Add(new Yetkinlik { KullaniciId = k.Id, MakineId = m.Id, GecerlilikBitis = bugun.AddDays(gecerlilikGun) });
        foreach (var m in new[] { kesim, cnc3, cnc1, paket }) Y(ali, m, 210);
        foreach (var m in new[] { kesim, trn1, trn2, taslama, paket }) Y(emre, m, 180);
        foreach (var m in new[] { kesim, cnc1, pres, paket }) Y(zeynep, m, 150);
        Y(zeynep, cnc2, -1);   // süresi dün doldu
        foreach (var m in new[] { kesim, paket, trn1, pres }) Y(burak, m, 240);
        Y(kalite, olcumIst, 365);
        Y(kalite, cnc3, 365);
        db.SaveChanges();

        // ---------- Hammadde lotları ----------
        var lotlar = new List<HammaddeLotu>();
        var tedarikciler = new[] { "Ereğli Demir Çelik", "Kardemir", "Sarkuysan", "Assan Alüminyum" };
        void Lot(Malzeme m, int gunOnce, double miktar, string tedarikci) => lotlar.Add(new HammaddeLotu
        {
            MalzemeId = m.Id, Tedarikci = tedarikci, SertifikaNo = $"3.1-{rnd.Next(100000, 999999)}",
            GirisTarihi = bugun.AddDays(-gunOnce).AddHours(10), BaslangicMiktar = miktar, KalanMiktar = miktar
        });
        foreach (var g in new[] { 45, 30, 18, 8 }) Lot(s355, g, 900, tedarikciler[g % 2]);
        foreach (var g in new[] { 44, 26, 10 }) Lot(c45, g, 400, "Kardemir");
        foreach (var g in new[] { 43, 22, 9 }) Lot(pirinc, g, 150, "Sarkuysan");
        foreach (var g in new[] { 42, 24, 7 }) Lot(aluminyum, g, 300, "Assan Alüminyum");

        // ---------- 30 günlük üretim simülasyonu ----------
        int CalisilanGun(int d)
        {
            while (d > 0 && bugun.AddDays(-d).DayOfWeek == DayOfWeek.Sunday) d++;
            return d;
        }
        var senaryolar = new Dictionary<int, Senaryo>
        {
            [CalisilanGun(12)] = new("hurda", "B25", 17, "Dış çap"),
            [CalisilanGun(7)] = new("yeniden", "FL120", 30, "Kalınlık"),
            [CalisilanGun(4)] = new("sartli", "K60", 22, "Delik çapı"),
            [CalisilanGun(2)] = new("acik", "FL90", 25, "Kalınlık")
        };

        var operatorAdaylari = new Dictionary<int, List<Kullanici>>();
        foreach (var m in makineler)
        {
            var ids = db.Yetkinlikler.Local.Where(y => y.MakineId == m.Id).Select(y => y.KullaniciId).ToHashSet();
            operatorAdaylari[m.Id] = new[] { ali, emre, zeynep, burak, kalite }.Where(k => ids.Contains(k.Id)).ToList();
        }

        double Normal()
        {
            var u1 = 1.0 - rnd.NextDouble();
            var u2 = rnd.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        double IyiDeger(KaliteKarakteristigi k)
        {
            var genislik = k.UstSinir - k.AltSinir;
            var v = k.Nominal + Normal() * genislik / 8;
            var pay = genislik * 0.08;
            v = Math.Clamp(v, k.AltSinir + pay, k.UstSinir - pay);
            if (k.Birim == "µm") v = Math.Max(0.05, v);
            return Math.Round(v, k.Birim == "µm" ? 2 : 3);
        }

        OlcumAleti Alet(KaliteKarakteristigi k, DateTime t) =>
            aletler.Where(a => a.Tur == k.AletTuru && a.GecerliMi(t) && a.SonKalibrasyon <= t).FirstOrDefault()
            ?? aletler.First(a => a.Tur == k.AletTuru);

        var urunSecimi = new (Urun U, int Agirlik)[] { (fl120, 35), (fl90, 15), (m40, 15), (b25, 20), (k60, 15) };
        Urun RastgeleUrun(bool m40Haric)
        {
            var secenekler = urunSecimi.Where(x => !(m40Haric && x.U == m40)).ToList();
            var toplam = secenekler.Sum(x => x.Agirlik);
            var r = rnd.Next(toplam);
            foreach (var s in secenekler) { if (r < s.Agirlik) return s.U; r -= s.Agirlik; }
            return fl120;
        }

        var emirler = new List<UretimEmri>();
        var islemler = new List<IslemKaydi>();
        var olcumler = new List<Olcum>();
        var ncrler = new List<Ncr>();
        var tuketimler = new List<HammaddeTuketimi>();
        var lotSayaci = new Dictionary<string, int>();
        var emirSira = 431;
        var ncrSira = 28;

        string YeniLotNo(DateTime t)
        {
            var anahtar = t.ToString("yyMM");
            lotSayaci[anahtar] = lotSayaci.GetValueOrDefault(anahtar) + 1;
            return Numara.LotNo(t, lotSayaci[anahtar]);
        }

        void HammaddeTuket(UretimEmri emir, Urun urun, DateTime t)
        {
            var ihtiyac = emir.Miktar * urun.BirimTuketim;
            var uygun = lotlar.Where(l => l.MalzemeId == urun.MalzemeId && l.GirisTarihi <= t).ToList();
            var (paylar, eksik) = FifoSecici.Sec(uygun.Select((l, i) => new LotStok(i, l.GirisTarihi, l.KalanMiktar)), ihtiyac);
            foreach (var pay in paylar)
            {
                var lot = uygun[pay.LotId];
                lot.KalanMiktar = Math.Round(lot.KalanMiktar - pay.Miktar, 3);
                tuketimler.Add(new HammaddeTuketimi { UretimEmri = emir, HammaddeLotu = lot, Miktar = pay.Miktar, Zaman = t });
            }
            if (eksik > 0)
            {
                // Simülasyonda stok yetmezse acil tedarik lotu girilir
                var acil = new HammaddeLotu
                {
                    MalzemeId = urun.MalzemeId, Tedarikci = "Acil tedarik", SertifikaNo = $"3.1-{rnd.Next(100000, 999999)}",
                    GirisTarihi = t.AddHours(-3), BaslangicMiktar = Math.Ceiling(eksik + 300), KalanMiktar = Math.Ceiling(eksik + 300)
                };
                lotlar.Add(acil);
                acil.KalanMiktar = Math.Round(acil.KalanMiktar - eksik, 3);
                tuketimler.Add(new HammaddeTuketimi { UretimEmri = emir, HammaddeLotu = acil, Miktar = eksik, Zaman = t });
            }
        }

        for (var d = 29; d >= 0; d--)
        {
            var gun = bugun.AddDays(-d);
            if (d > 0 && gun.DayOfWeek == DayOfWeek.Sunday) continue;

            senaryolar.TryGetValue(d, out var senaryo);
            var emirAdedi = d == 0 ? 2 : (rnd.NextDouble() < 0.35 ? 2 : 1);

            for (var e = 0; e < emirAdedi; e++)
            {
                Urun urun;
                if (d == 0) urun = e == 0 ? fl120 : b25;
                else if (e == 0 && senaryo != null) urun = urunler.First(u => u.Kod == senaryo.UrunKod);
                else urun = RastgeleUrun(m40Haric: d <= 2);

                var miktar = d == 0 ? (e == 0 ? 80 : 60) : rnd.Next(4, 10) * 10;
                var baslangic = d == 0 ? simdi.AddHours(-5 + e * 1.5) : gun.AddHours(8 + e * 3.5).AddMinutes(rnd.Next(0, 40));
                var emirSenaryo = e == 0 && d > 0 ? senaryo : null;

                var emir = new UretimEmri
                {
                    EmirNo = Numara.EmirNo(emirSira++),
                    LotNo = YeniLotNo(gun),
                    UrunId = urun.Id,
                    Miktar = miktar,
                    PlanTarihi = gun,
                    OlusturmaTarihi = baslangic.AddDays(-1).AddHours(-2),
                    BaslamaTarihi = baslangic,
                    Durum = EmirDurum.Uretimde
                };
                emirler.Add(emir);
                HammaddeTuket(emir, urun, baslangic);

                var adimlar = rotalar[urun];
                var aralik = Math.Min(6.0, 420.0 / miktar);
                var durdu = false;

                for (var i = 0; i < miktar; i++)
                {
                    urun.SonSeriNo++;
                    var parca = new Parca { SeriNo = Numara.SeriNo(urun.Kod, urun.SonSeriNo), UrunId = urun.Id, Durum = ParcaDurum.Uretimde };
                    emir.Parcalar.Add(parca);
                    if (durdu) continue;

                    for (var a = 0; a < adimlar.Count; a++)
                    {
                        var adim = adimlar[a];
                        var t = baslangic.AddMinutes(i * aralik + a * 35 + rnd.Next(0, 4));
                        if (t > simdi) break;

                        var adaylar = operatorAdaylari[adim.MakineId];
                        var op = adaylar[rnd.Next(adaylar.Count)];
                        var adimKarlari = karakteristikler.Where(k => k.RotaAdimiId == adim.Id).ToList();
                        var hataliKar = emirSenaryo != null && i == emirSenaryo.ParcaIndeks
                            ? adimKarlari.FirstOrDefault(k => k.Ad == emirSenaryo.KarakteristikAd)
                            : null;

                        foreach (var k in adimKarlari)
                        {
                            var hatali = k == hataliKar;
                            var deger = hatali
                                ? Math.Round(k.UstSinir + (k.UstSinir - k.Nominal) * 0.35, 3)
                                : IyiDeger(k);
                            olcumler.Add(new Olcum
                            {
                                Parca = parca, KarakteristikId = k.Id, MakineId = adim.MakineId, Deger = deger,
                                Uygun = !hatali, OlcumAletiId = Alet(k, t).Id, KullaniciId = op.Id, Zaman = t
                            });
                        }

                        if (hataliKar != null)
                        {
                            var ncr = new Ncr
                            {
                                NcrNo = Numara.NcrNo(ncrSira++),
                                UretimEmri = emir,
                                Parca = parca,
                                Kaynak = "Otomatik · ölçüm",
                                Tanim = $"{parca.SeriNo} tolerans dışı: {hataliKar.Ad} (tolerans {hataliKar.AltSinir:0.###}–{hataliKar.UstSinir:0.###} {hataliKar.Birim})",
                                AcanKisi = op.AdSoyad,
                                AcilisTarihi = t
                            };
                            ncrler.Add(ncr);

                            if (emirSenaryo!.Tur == "acik")
                            {
                                parca.Durum = ParcaDurum.Bloke;
                                emir.Durum = EmirDurum.Bloke;
                                durdu = true;
                                break;
                            }
                            if (emirSenaryo.Tur == "hurda")
                            {
                                ncr.Durum = NcrDurum.Kapali; ncr.Karar = NcrKarar.Hurda; ncr.KararVeren = kalite.AdSoyad;
                                ncr.KararTarihi = t.AddHours(2); ncr.KapanisTarihi = t.AddDays(1);
                                ncr.DuzelticiFaaliyet = "Ölçüm istasyonunda referans parça kontrolü vardiya başına zorunlu hale getirildi.";
                                parca.Durum = ParcaDurum.Hurda;
                                break;
                            }
                            if (emirSenaryo.Tur == "yeniden")
                            {
                                // Kalite kapattı ve imzaladı, yönetici onayı bekliyor
                                ncr.Durum = NcrDurum.OnayBekliyor; ncr.Karar = NcrKarar.YenidenIsleme; ncr.KararVeren = kalite.AdSoyad;
                                ncr.KararTarihi = t.AddMinutes(40);
                                ncr.DuzelticiFaaliyet = "Yüzey tekrar işlendi; CNC-3 takım ofseti güncellendi.";
                                t = t.AddMinutes(50);
                                foreach (var k in adimKarlari)
                                {
                                    olcumler.Add(new Olcum
                                    {
                                        Parca = parca, KarakteristikId = k.Id, MakineId = adim.MakineId, Deger = IyiDeger(k),
                                        Uygun = true, OlcumAletiId = Alet(k, t).Id, KullaniciId = op.Id, Zaman = t
                                    });
                                }
                            }
                            else if (emirSenaryo.Tur == "sartli")
                            {
                                ncr.Durum = NcrDurum.KararVerildi; ncr.Karar = NcrKarar.SartliKabul; ncr.KararVeren = yonetici.AdSoyad;
                                ncr.KararTarihi = t.AddHours(5);
                            }
                        }

                        islemler.Add(new IslemKaydi { Parca = parca, RotaAdimiId = adim.Id, MakineId = adim.MakineId, OperatorId = op.Id, Zaman = t });
                        parca.TamamlananAdim = a + 1;
                        if (a == adimlar.Count - 1)
                        {
                            parca.Durum = ParcaDurum.Tamamlandi;
                            parca.TamamlanmaTarihi = t;
                        }
                    }
                }

                if (!durdu && emir.Parcalar.All(p => p.Durum is ParcaDurum.Tamamlandi or ParcaDurum.Hurda))
                {
                    emir.Durum = EmirDurum.Tamamlandi;
                    emir.TamamlanmaTarihi = emir.Parcalar.Where(p => p.TamamlanmaTarihi != null).Max(p => p.TamamlanmaTarihi);
                }
            }
        }

        // Yarın için planlanmış emir (henüz başlamadı)
        var planli = new UretimEmri
        {
            EmirNo = Numara.EmirNo(emirSira++),
            LotNo = YeniLotNo(bugun.AddDays(1)),
            UrunId = k60.Id, Miktar = 50, PlanTarihi = bugun.AddDays(1),
            OlusturmaTarihi = simdi.AddHours(-2), Durum = EmirDurum.Planlandi
        };
        for (var i = 0; i < planli.Miktar; i++)
        {
            k60.SonSeriNo++;
            planli.Parcalar.Add(new Parca { SeriNo = Numara.SeriNo(k60.Kod, k60.SonSeriNo), UrunId = k60.Id, Durum = ParcaDurum.Bekliyor });
        }
        emirler.Add(planli);

        // CNC-3'te son ölçümler yavaşça yukarı kayıyor (SPC erken uyarı senaryosu)
        var flCap = karakteristikler.First(k => k.UrunId == fl120.Id && k.Ad == "Dış çap");
        var sonFl = olcumler.Where(o => o.KarakteristikId == flCap.Id).OrderBy(o => o.Zaman).TakeLast(8).ToList();
        var yariTol = flCap.UstSinir - flCap.Nominal;
        for (var i = 0; i < sonFl.Count; i++)
            sonFl[i].Deger = Math.Round(flCap.Nominal + yariTol * (0.12 + 0.085 * i), 3);

        // Hammadde lot numaraları giriş sırasına göre
        var hmSira = 101;
        foreach (var l in lotlar.OrderBy(l => l.GirisTarihi)) l.LotNo = Numara.HammaddeLotNo(l.GirisTarihi, hmSira++);

        db.HammaddeLotlari.AddRange(lotlar);
        db.UretimEmirleri.AddRange(emirler);
        db.HammaddeTuketimleri.AddRange(tuketimler);
        db.IslemKayitlari.AddRange(islemler);
        db.Olcumler.AddRange(olcumler);
        db.Ncrler.AddRange(ncrler);
        db.SaveChanges();

        // Kritik stok eşikleri: S355 kritik seviyede başlasın
        double Kalan(Malzeme m) => lotlar.Where(l => l.MalzemeId == m.Id).Sum(l => l.KalanMiktar);
        s355.MinimumStok = Math.Ceiling((Kalan(s355) + 350) / 100) * 100;
        foreach (var m in new[] { c45, pirinc, aluminyum })
            m.MinimumStok = Math.Max(50, Math.Floor(Kalan(m) * 0.4 / 50) * 50);

        // Makine durumları
        var bugunCalisan = islemler.Where(i => i.Zaman > simdi.AddMinutes(-90)).Select(i => i.MakineId).ToHashSet();
        foreach (var m in makineler) m.Durum = bugunCalisan.Contains(m.Id) ? MakineDurum.Calisiyor : MakineDurum.Bekliyor;
        taslama.Durum = MakineDurum.Arizali;

        // Bakım geçmişi
        foreach (var m in makineler)
        {
            db.BakimKayitlari.Add(new BakimKaydi
            {
                MakineId = m.Id, Tur = "Periyodik", Tarih = m.SonBakimTarihi, Yapan = depo.AdSoyad, SureDakika = rnd.Next(60, 180),
                Aciklama = "Periyodik bakım: yağlama, filtre değişimi, eksen kontrolleri"
            });
            db.BakimKayitlari.Add(new BakimKaydi
            {
                MakineId = m.Id, Tur = "Periyodik", Tarih = m.SonBakimTarihi.AddDays(-m.BakimPeriyotGun), Yapan = depo.AdSoyad,
                SureDakika = rnd.Next(60, 180), Aciklama = "Periyodik bakım"
            });
        }
        db.BakimKayitlari.Add(new BakimKaydi
        {
            MakineId = cnc1.Id, Tur = "Arıza onarımı", Tarih = bugun.AddDays(-16).AddHours(14), Yapan = depo.AdSoyad, SureDakika = 210,
            Aciklama = "İş mili rulmanı değiştirildi"
        });

        // Bildirimler
        void B(DateTime zaman, string baslik, string mesaj, string seviye, string? link, params string[] roller) =>
            db.Bildirimler.Add(new Bildirim
            {
                Zaman = zaman, Baslik = baslik, Mesaj = mesaj, Seviye = seviye, Link = link,
                HedefRoller = "," + string.Join(",", roller) + ","
            });

        var acikNcr = ncrler.FirstOrDefault(n => n.Durum == NcrDurum.Acik);
        if (acikNcr != null)
            B(acikNcr.AcilisTarihi, $"{acikNcr.NcrNo} · Tolerans dışı parça", $"Lot {acikNcr.UretimEmri?.LotNo} bloke edildi, karar bekliyor.",
                "kritik", $"/ncr/detay/{acikNcr.Id}", Roller.Kalite, Roller.Yonetici, Roller.Admin);
        B(simdi.AddDays(-1).AddHours(-3), "Kritik stok", $"{s355.Ad} minimum stok seviyesinin altında.", "uyari", "/depo", Roller.DepoBakim, Roller.Yonetici, Roller.Admin);
        B(simdi.AddHours(-3), "TSL-1 arıza bildirimi", "Silindirik taşlama: iş mili titreşimi, makine durduruldu (Emre Kaya).", "kritik", "/bakim",
            Roller.DepoBakim, Roller.Yonetici, Roller.Admin);
        B(simdi.AddMinutes(-25), "SPC erken uyarı · CNC-3", "Flanş FL-120 dış çap: 6 nokta art arda yükseliyor. Makine ayarını kontrol edin.", "uyari",
            $"/kalite?karakteristikId={flCap.Id}&makineId={cnc3.Id}", Roller.Kalite, Roller.Yonetici, Roller.DepoBakim, Roller.Admin);
        var sonTamam = emirler.Where(e => e.Durum == EmirDurum.Tamamlandi).OrderByDescending(e => e.TamamlanmaTarihi).FirstOrDefault();
        if (sonTamam != null)
            B(sonTamam.TamamlanmaTarihi ?? simdi, $"{sonTamam.EmirNo} tamamlandı", $"Lot {sonTamam.LotNo} depoya sevk edilebilir.", "bilgi",
                $"/uretim/detay/{sonTamam.Id}", Roller.Yonetici, Roller.Admin, Roller.DepoBakim);

        // Örnek audit kayıtları
        string Json(params UretimDbContext.Degisiklik[] d) => JsonSerializer.Serialize(d);
        UretimDbContext.Degisiklik D(string alan, string? eski, string? yeni) => new UretimDbContext.Degisiklik(alan, eski, yeni);
        db.AuditKayitlari.AddRange(
            new AuditKaydi { Zaman = simdi.AddDays(-6), KullaniciAdi = "admin", Tablo = "Kullanici", KayitId = burak.Id.ToString(), Islem = "Ekleme",
                Degisiklikler = Json(D("KullaniciAdi", null, "burak"), D("AdSoyad", null, "Burak Çelik"), D("Rol", null, Roller.Operator)) },
            new AuditKaydi { Zaman = simdi.AddDays(-3), KullaniciAdi = "yonetici", Tablo = "KaliteKarakteristigi", KayitId = flCap.Id.ToString(), Islem = "Güncelleme",
                Degisiklikler = Json(D("UstSinir", "120,04", "120,05")) },
            new AuditKaydi { Zaman = simdi.AddDays(-1), KullaniciAdi = "kalite", Tablo = "OlcumAleti", KayitId = aletler[1].Id.ToString(), Islem = "Güncelleme",
                Degisiklikler = Json(D("SonKalibrasyon", aletler[1].SonKalibrasyon.AddDays(-180).ToString("dd.MM.yyyy HH:mm"), aletler[1].SonKalibrasyon.ToString("dd.MM.yyyy HH:mm"))) },
            new AuditKaydi { Zaman = simdi.AddHours(-3), KullaniciAdi = "emre", Tablo = "Makine", KayitId = taslama.Id.ToString(), Islem = "Güncelleme",
                Degisiklikler = Json(D("Durum", MakineDurum.Calisiyor, MakineDurum.Arizali)) });

        db.SaveChanges();

        // ---------- Duruş kayıtları (OEE ve Pareto için) ----------
        var nedenler = new (string Neden, int Agirlik, int Min, int Max, string[] Aciklamalar)[]
        {
            (DurusNedeni.Ayar, 30, 15, 40, new[] { "Yeni ürüne geçiş, bağlama aparatı değişimi", "İlk parça onayı için ayar", "Program ve sıfır noktası ayarı" }),
            (DurusNedeni.Malzeme, 20, 10, 45, new[] { "Kesimden parça gelmedi", "Forklift bekleniyor", "Hammadde depodan geç geldi" }),
            (DurusNedeni.Takim, 20, 5, 15, new[] { "Kesici uç aşındı", "Matkap kırıldı", "Planlı takım değişimi" }),
            (DurusNedeni.KaliteOnayi, 12, 10, 30, new[] { "İlk parça ölçüm onayı bekleniyor", "Kalite kontrol sırası" }),
            (DurusNedeni.Operator, 10, 10, 30, new[] { "Operatör başka istasyona destek verdi", "Vardiya devri gecikti" }),
            (DurusNedeni.Ariza, 8, 30, 110, new[] { "Hidrolik basınç düştü", "Soğutma sıvısı pompası arızası", "Eksen alarmı", "Kapı emniyet sensörü arızası" })
        };
        var agirlikToplam = nedenler.Sum(n => n.Agirlik);
        var bildirenler = new[] { ali, emre, zeynep, burak, depo };

        foreach (var g in islemler.GroupBy(i => new { i.MakineId, Gun = i.Zaman.Date }))
        {
            var ilk = g.Min(i => i.Zaman);
            var son = g.Max(i => i.Zaman);
            var pencere = (son - ilk).TotalMinutes;
            if (pencere < 90) continue;

            var r = rnd.NextDouble();
            var adet = r < 0.3 ? 0 : r < 0.75 ? 1 : 2;
            for (var k = 0; k < adet; k++)
            {
                var secim = rnd.Next(agirlikToplam);
                var neden = nedenler[0];
                foreach (var n in nedenler) { if (secim < n.Agirlik) { neden = n; break; } secim -= n.Agirlik; }

                var dk = rnd.Next(neden.Min, neden.Max + 1);
                var dilim = pencere / adet;
                var basDk = dilim * k + rnd.NextDouble() * Math.Max(1, dilim - dk);
                var bas = ilk.AddMinutes(basDk);
                var bit = bas.AddMinutes(dk);
                if (bit > simdi) continue;
                db.DurusKayitlari.Add(new DurusKaydi
                {
                    MakineId = g.Key.MakineId, Neden = neden.Neden, Aciklama = neden.Aciklamalar[rnd.Next(neden.Aciklamalar.Length)],
                    Baslangic = bas, Bitis = bit, BildirenKisi = bildirenler[rnd.Next(bildirenler.Length)].AdSoyad
                });
            }
        }

        // Planlı bakımlar ve bilinen arızalar
        foreach (var m in makineler.Where(x => x.SonBakimTarihi >= bugun.AddDays(-30)))
            db.DurusKayitlari.Add(new DurusKaydi
            {
                MakineId = m.Id, Neden = DurusNedeni.PlanliBakim, Aciklama = "Periyodik bakım",
                Baslangic = m.SonBakimTarihi, Bitis = m.SonBakimTarihi.AddMinutes(rnd.Next(60, 150)), BildirenKisi = depo.AdSoyad
            });
        db.DurusKayitlari.Add(new DurusKaydi
        {
            MakineId = cnc1.Id, Neden = DurusNedeni.Ariza, Aciklama = "İş mili rulmanı arızası",
            Baslangic = bugun.AddDays(-16).AddHours(14), Bitis = bugun.AddDays(-16).AddHours(17).AddMinutes(30), BildirenKisi = ali.AdSoyad
        });
        db.DurusKayitlari.Add(new DurusKaydi
        {
            MakineId = taslama.Id, Neden = DurusNedeni.Ariza, Aciklama = "İş mili titreşimi, makine durduruldu",
            Baslangic = simdi.AddHours(-3), Bitis = null, BildirenKisi = emre.AdSoyad
        });

        // ---------- Elektronik imzalar ----------
        var hurdaNcr = ncrler.FirstOrDefault(n => n.Karar == NcrKarar.Hurda && n.Durum == NcrDurum.Kapali);
        if (hurdaNcr?.KapanisTarihi is DateTime hk)
        {
            db.Imzalar.Add(NcrImza.Olustur(hurdaNcr, "Hazırlayan", kalite, ImzaCiz(kalite.AdSoyad, rnd), hk.AddHours(-2)));
            db.Imzalar.Add(NcrImza.Olustur(hurdaNcr, "Onaylayan", yonetici, ImzaCiz(yonetici.AdSoyad, rnd), hk));
        }
        var onayNcr = ncrler.FirstOrDefault(n => n.Durum == NcrDurum.OnayBekliyor);
        if (onayNcr != null)
        {
            onayNcr.DuzelticiFaaliyet = "Yüzey tekrar işlendi ve yeniden ölçüldü; CNC-3 takım ofseti güncellendi.";
            db.SaveChanges();
            db.Imzalar.Add(NcrImza.Olustur(onayNcr, "Hazırlayan", kalite, ImzaCiz(kalite.AdSoyad, rnd), (onayNcr.KararTarihi ?? simdi).AddHours(3)));
            B(simdi.AddHours(-4), $"{onayNcr.NcrNo} onayınızı bekliyor", "Kalite NCR'ı kapattı ve imzaladı. Yönetici onayı ve imzası gerekli.", "uyari",
                $"/ncr/detay/{onayNcr.Id}", Roller.Yonetici, Roller.Admin);
        }

        db.SaveChanges();
        db.DenetimKapali = false;
    }

    // Örnek veri için el yazısına benzeyen imza çizgisi üretir (400x140 alan)
    private static string ImzaCiz(string ad, Random rnd)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        var x = 28.0;
        var yTaban = 82.0;
        sb.Append("M ").Append(x.ToString("0.0", inv)).Append(' ').Append(yTaban.ToString("0.0", inv));
        foreach (var harf in ad.Replace(" ", "").Take(11))
        {
            var genislik = 18 + rnd.Next(0, 12);
            var yukseklik = char.IsUpper(harf) ? 46 + rnd.Next(0, 14) : 20 + rnd.Next(0, 16);
            for (var i = 1; i <= 8; i++)
            {
                var t = i / 8.0;
                var px = x + genislik * t + Math.Sin(t * Math.PI * 2) * 5;
                var py = yTaban - Math.Sin(t * Math.PI) * yukseklik + rnd.NextDouble() * 3;
                sb.Append(" L ").Append(Math.Clamp(px, 2, 398).ToString("0.0", inv)).Append(' ').Append(Math.Clamp(py, 4, 136).ToString("0.0", inv));
            }
            x += genislik * 0.85;
            if (x > 360) break;
        }
        var sonX = Math.Min(380, x + 14);
        sb.Append(" M 40.0 112.0 L ").Append((sonX * 0.6).ToString("0.0", inv)).Append(" 116.0 L ").Append(sonX.ToString("0.0", inv)).Append(" 106.0");
        return sb.ToString();
    }
}
