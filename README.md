# 🏭 ÜretimPro · Üretim İzlenebilirlik ve Kalite Yönetim Sistemi

![CI](https://github.com/furkantr1923/UretimPro/actions/workflows/ci.yml/badge.svg)

Hammaddeden sevkiyata kadar **her parçanın geçmişini** kayıt altına alan, **SPC ile erken uyarı** veren, **OEE ve duruş analizi** yapan ve **uygunsuzlukları elektronik imzayla yöneten** bir fabrika yazılımı (MES).

> ⚠️ **Yasal bilgilendirme:** Bu proje, kişisel portfolyo amacıyla geliştirilmiş **bağımsız bir eğitim / demo çalışmasıdır**. Herhangi bir şirketin, kurumun veya markanın **resmî ürünü değildir**, onlarla bir bağı, ortaklığı ya da onayı yoktur. Hiçbir şirketin adı, logosu, kodu veya gerçek verisi kullanılmamıştır. Uygulamadaki tüm kişi, makine, ürün ve üretim verileri **kurgusaldır**. Ürünler genel metal parçalardır (flanş, mil, burç, kapak); proje silah veya askerî ürün içermez. Ticari amaç taşımaz ve ücretsiz olarak paylaşılmaktadır.

## ✨ Özellikler

**Üretim ve izlenebilirlik**
- 🏷️ Üretim emri = üretim lotu; her parçaya otomatik seri no (`FL120-00681`)
- 🛣️ Ürüne özel rota: adımlar sırayla yapılır, **adım atlanamaz**
- 📷 İstasyon ekranı: QR / barkod okutma (el tipi okuyucu klavye gibi çalışır)
- 🗺️ **Canlı fabrika yerleşim planı**: makine durumları, akan malzeme, makine bazında OEE
- 🌳 **Lot soy ağacı**: tedarikçi → hammadde → rota → operatör → parça → NCR; hammaddeden **geri çağırma ağacı**
- 🖨️ Parça, lot ve hammadde için yazdırılabilir **QR etiketler**

**Kalite**
- 📏 İstasyonda ölçüm girişi, tolerans kontrolü
- 🔴 Tolerans dışı ölçüm → **parça ve lot anında bloke + otomatik NCR + canlı bildirim**
- 📈 **SPC kontrol grafiği**: 4 Western Electric kuralı + trend kuralı, Cpk
- ✍️ **NCR onay akışı ve elektronik imza**: kalite hazırlar ve imzalar → yönetici onaylar (dört göz ilkesi). İmzalar şifreyle doğrulanır, **SHA-256** ile mühürlenir; belge sonradan değişirse imza "geçersiz" görünür
- 🎯 **Kalibrasyon takibi**: süresi geçmiş aletle ölçüm kaydedilemez
- 📄 **Uygunluk sertifikası (CoC)** PDF: hammadde sertifikaları, boyutsal özet, Cpk, QR

**Verimlilik**
- ⚙️ **OEE** = Kullanılabilirlik × Performans × Kalite (makine, vardiya, gün bazında)
- ⏸️ **Duruş kaydı** ve **Pareto analizi** (80/20: kayıpların çoğunu oluşturan nedenler)
- 🕗 **Vardiya raporu** (A / B / C), 14 günlük OEE trendi
- 📦 Hammadde lotları, **FIFO** önerisi, kritik stok uyarısı
- 🔧 Bakım planı: **süre veya işlem adedi** (hangisi önce dolarsa), arıza bildirimi
- 👷 **Operatör yetkinlik matrisi**: sertifikası olmayan operatör o istasyonda işlem yapamaz

**Raporlar ve entegrasyon**
- 📑 **PDF** (QuestPDF): vardiya raporu, NCR raporu, uygunluk sertifikası
- 📊 **Excel** (ClosedXML): vardiya, duruşlar, lot ölçümleri, audit log
- 🔌 **REST API** + **Swagger** dokümantasyonu (`/swagger`)
- 🐳 **Docker** ve **GitHub Actions** (derleme, test, Windows exe, Docker imajı)

**Güvenlik ve kayıt**
- 👥 5 rol: Admin, Yönetici, Kalite, Operatör, Depo / Bakım
- 🔐 Hash'lenmiş şifreler, 5 hatalı denemede 15 dk kilit, CSRF koruması
- 📜 **Audit log**: eski → yeni değer, otomatik, değiştirilemez ve silinemez
- 🔔 Canlı bildirimler (Server-Sent Events) ve zil

**Arayüz**
- 🎨 Koyu yeşil + bakır endüstriyel tema, 🌙 karanlık mod
- ⌨️ **Ctrl+K komut paleti**: sayfa, komut, seri no, lot, NCR, makine
- 🧩 **Kişiselleştirilebilir kontrol merkezi**: kartları sürükle-bırak, gizle; düzen kullanıcıya kaydedilir
- ✨ Animasyonlu sayaçlar, çizilerek gelen grafikler (OEE göstergesi, Pareto), konfeti, ses efektleri
- 📱 Telefon ve tablette çalışır, internet gerektirmez (grafikler, QR ve imza alanı kendi kodumuzla)

## 🛠️ Teknolojiler

| Katman | Teknoloji |
|---|---|
| Backend | C#, ASP.NET Core MVC (.NET 10) |
| Veri | Entity Framework Core, SQLite (tek dosya, kurulum yok) |
| Canlı veri | Server-Sent Events |
| Ön yüz | Razor, özel CSS tasarım sistemi, vanilla JavaScript |
| Grafik | Kendi yazdığım animasyonlu SVG grafik kütüphanesi (alan, halka, SPC, Pareto, gösterge) |
| Rapor | QuestPDF (PDF), ClosedXML (Excel), QRCoder |
| API | REST, Swagger / OpenAPI |
| DevOps | Docker, Docker Compose, GitHub Actions |
| Test | xUnit (SPC, OEE, Pareto, imza, FIFO, bakım, numaralandırma) |

## 🚀 Çalıştırma

```bash
git clone https://github.com/furkantr1923/UretimPro.git
cd UretimPro
dotnet run --project UretimPro.Web
```

Tarayıcıda `http://localhost:5080` açılır. İlk çalıştırmada veritabanı ve **30 günlük örnek fabrika verisi** (üretim, ölçüm, duruş, NCR, imza) otomatik oluşur.

**Demo hesapları** (şifre: `Demo123!`): `admin`, `yonetici`, `kalite`, `operator`, `depo`, `burak` (yetkinliği sınırlı operatör)

## 💾 USB'den çalışan exe

```bash
dotnet publish UretimPro.Web -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

`publish` klasörünü USB'ye kopyala, `UretimPro.exe`'ye çift tıkla. .NET kurulu olmayan bilgisayarda da çalışır; veritabanı exe'nin yanında oluşur.

## 🐳 Docker

```bash
docker compose up -d --build
```

`http://localhost:5080` adresinden açılır. Veritabanı `uretimpro-veri` biriminde kalıcıdır.

## 🔌 REST API

Uygulamaya giriş yaptıktan sonra `http://localhost:5080/swagger` adresinden denenebilir.

| Uç | Açıklama |
|---|---|
| `GET /api/v1/ozet` | Bugünkü üretim, açık NCR, OEE |
| `GET /api/v1/makineler` | Makine durumu ve bugünkü OEE |
| `GET /api/v1/emirler` | Üretim emirleri |
| `GET /api/v1/parcalar/{seriNo}` | Parçanın tam geçmişi |
| `GET /api/v1/lotlar/{lotNo}/agac` | Lot soy ağacı |
| `GET /api/v1/hammadde/{lotNo}/geri-cagirma` | Geri çağırma ağacı |
| `GET /api/v1/oee?tarih=&vardiya=` | Makine bazında OEE |
| `GET /api/v1/duruslar/pareto?gun=30` | Duruş Pareto analizi |
| `GET /api/v1/ncr` | Uygunsuzluk raporları |

## 🧪 Testler

```bash
dotnet test
```

## 🎬 Denemek için senaryolar

1. **operator** ile gir → İstasyon → **CNC-3** → sırası gelen bir parçayı okut → tolerans dışı değer gir: alarm, bloke, otomatik NCR
2. **burak** ile gir → CNC-3'te okutmayı dene → *yetkinlik yok* engeli
3. **kalite** ile gir → SPC → CNC-3 dış çapında **trend uyarısı**
4. **yonetici** ile gir → bildirimdeki NCR → **imzala ve onayla** → PDF raporu indir
5. **Fabrika yerleşimi** → arızalı TSL-1'e tıkla → duruş ve OEE ayrıntısı
6. **Lot soy ağacı** → dalları aç; hammadde lotundan **geri çağırma**
7. **Duruşlar** → Pareto: kayıpların %80'i hangi nedenlerde?
8. Herhangi bir sayfada **Ctrl+K** → `FL120` yaz
9. Kontrol merkezi → **Paneli düzenle** → kartları sürükle

## 📁 Proje yapısı

```
UretimPro.Web/
  Controllers/   Sayfalar, JSON uçları ve REST API (api/v1)
  Data/          DbContext (audit log, imza koruması) ve 30 günlük örnek veri
  Models/        Varlıklar ve görünüm modelleri
  Services/      SPC, OEE, Pareto, FIFO, bakım, soy ağacı, PDF/Excel, canlı yayın
  Views/         Razor sayfaları
  wwwroot/       CSS tasarım sistemi; grafik, fabrika planı, komut paleti, imza scriptleri
UretimPro.Tests/ xUnit testleri
Dockerfile, docker-compose.yml, .github/workflows/ci.yml
```

## 👤 Geliştirici

**Furkan Baykal** · Yazılım Mühendisi · [@furkantr1923](https://github.com/furkantr1923)
