using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Services;

// Exe olarak çalışırken dosyalar exe'nin yanındaki klasörden okunur (USB'den çalıştırma)
var gelistirme = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = gelistirme ? null : AppContext.BaseDirectory
});

var kultur = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = kultur;
CultureInfo.DefaultThreadCurrentUICulture = kultur;

// Veritabanı: tek dosya SQLite (kurulum gerektirmez). Docker'da VeriKlasoru ile kalıcı klasör verilir.
var veriKlasoru = builder.Configuration["VeriKlasoru"] is { Length: > 0 } vk
    ? vk
    : (gelistirme ? builder.Environment.ContentRootPath : AppContext.BaseDirectory);
Directory.CreateDirectory(veriKlasoru);
var dbYolu = Path.Combine(veriKlasoru, "uretimpro.db");
var tarayiciAc = builder.Configuration.GetValue("TarayiciAc", true);

// PDF raporları (QuestPDF topluluk lisansı: bireysel ve küçük ölçekli kullanım için ücretsiz)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

builder.Services.AddControllersWithViews(o => o.Filters.Add(new AuthorizeFilter()));
builder.Services.AddRouting(o => o.LowercaseUrls = true);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<UretimDbContext>(o => o.UseSqlite($"Data Source={dbYolu}"));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/hesap/giris";
        o.LogoutPath = "/hesap/cikis";
        o.AccessDeniedPath = "/hesap/erisim-yok";
        o.ExpireTimeSpan = TimeSpan.FromHours(10);
        o.SlidingExpiration = true;
        o.Cookie.Name = "UretimPro.Oturum";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<CanliYayin>();
builder.Services.AddScoped<BildirimServisi>();
builder.Services.AddScoped<UretimServisi>();
builder.Services.AddScoped<UyariServisi>();
builder.Services.AddScoped<AnalizServisi>();
builder.Services.AddScoped<RaporServisi>();

// REST API dokümantasyonu (/swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ÜretimPro API",
        Version = "v1",
        Description = "Üretim, izlenebilirlik, OEE ve kalite verileri. Önce uygulamaya giriş yapın (çerez ile kimlik doğrulama)."
    });
    c.DocInclusionPredicate((_, api) => api.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
    var xml = Path.Combine(AppContext.BaseDirectory, "UretimPro.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/hata");
}

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(kultur),
    SupportedCultures = new List<CultureInfo> { kultur },
    SupportedUICultures = new List<CultureInfo> { kultur }
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ÜretimPro API v1");
    c.DocumentTitle = "ÜretimPro API";
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Panel}/{action=Index}/{id?}")
    .WithStaticAssets();

// İlk çalıştırmada veritabanı ve 30 günlük örnek veri.
// Şema değiştiyse (yeni sürüm) eski demo veritabanı silinip yeniden oluşturulur.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UretimDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var surumDosyasi = dbYolu + ".surum";
    var sema = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(db.Database.GenerateCreateScript())))[..16];
    var eskiSema = File.Exists(surumDosyasi) ? File.ReadAllText(surumDosyasi).Trim() : null;
    if (eskiSema != sema && File.Exists(dbYolu))
    {
        log.LogWarning("Veritabanı şeması değişti; demo verisi yeni sürüm için yeniden oluşturuluyor.");
        db.Database.EnsureDeleted();
    }
    db.Database.EnsureCreated();
    if (!db.Kullanicilar.Any())
    {
        log.LogInformation("Örnek veri oluşturuluyor (ilk açılış, birkaç saniye sürebilir)...");
        OrnekVeri.Olustur(db);
    }
    File.WriteAllText(surumDosyasi, sema);
}

// Exe olarak açılınca tarayıcıyı kendiliğinden aç
if (!app.Environment.IsDevelopment() && tarayiciAc)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var adres = app.Urls.FirstOrDefault() ?? "http://localhost:5080";
        Console.WriteLine();
        Console.WriteLine("  ================================================");
        Console.WriteLine("   ÜretimPro · Üretim İzlenebilirlik Sistemi");
        Console.WriteLine($"   Adres : {adres}");
        Console.WriteLine($"   Veri  : {dbYolu}");
        Console.WriteLine("   Kapatmak için bu pencereyi kapatın.");
        Console.WriteLine("  ================================================");
        try { Process.Start(new ProcessStartInfo(adres) { UseShellExecute = true }); } catch { }
    });
}

app.Run();

public partial class Program { }
