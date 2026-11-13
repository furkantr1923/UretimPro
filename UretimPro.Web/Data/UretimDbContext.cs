using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using UretimPro.Models;

namespace UretimPro.Data;

public class UretimDbContext : DbContext
{
    private readonly IHttpContextAccessor? _http;

    public UretimDbContext(DbContextOptions<UretimDbContext> options, IHttpContextAccessor? http = null) : base(options)
    {
        _http = http;
    }

    // Örnek veri yüklenirken audit kaydı tutulmaz
    public bool DenetimKapali { get; set; }

    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<Makine> Makineler => Set<Makine>();
    public DbSet<Malzeme> Malzemeler => Set<Malzeme>();
    public DbSet<Urun> Urunler => Set<Urun>();
    public DbSet<RotaAdimi> RotaAdimlari => Set<RotaAdimi>();
    public DbSet<KaliteKarakteristigi> Karakteristikler => Set<KaliteKarakteristigi>();
    public DbSet<OlcumAleti> OlcumAletleri => Set<OlcumAleti>();
    public DbSet<Yetkinlik> Yetkinlikler => Set<Yetkinlik>();
    public DbSet<HammaddeLotu> HammaddeLotlari => Set<HammaddeLotu>();
    public DbSet<UretimEmri> UretimEmirleri => Set<UretimEmri>();
    public DbSet<HammaddeTuketimi> HammaddeTuketimleri => Set<HammaddeTuketimi>();
    public DbSet<Parca> Parcalar => Set<Parca>();
    public DbSet<IslemKaydi> IslemKayitlari => Set<IslemKaydi>();
    public DbSet<Olcum> Olcumler => Set<Olcum>();
    public DbSet<Ncr> Ncrler => Set<Ncr>();
    public DbSet<BakimKaydi> BakimKayitlari => Set<BakimKaydi>();
    public DbSet<Bildirim> Bildirimler => Set<Bildirim>();
    public DbSet<AuditKaydi> AuditKayitlari => Set<AuditKaydi>();
    public DbSet<DurusKaydi> DurusKayitlari => Set<DurusKaydi>();
    public DbSet<ElektronikImza> Imzalar => Set<ElektronikImza>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Kullanici>().HasIndex(x => x.KullaniciAdi).IsUnique();
        b.Entity<Makine>().HasIndex(x => x.Kod).IsUnique();
        b.Entity<Urun>().HasIndex(x => x.Kod).IsUnique();
        b.Entity<OlcumAleti>().HasIndex(x => x.Kod).IsUnique();
        b.Entity<HammaddeLotu>().HasIndex(x => x.LotNo).IsUnique();
        b.Entity<UretimEmri>().HasIndex(x => x.EmirNo).IsUnique();
        b.Entity<UretimEmri>().HasIndex(x => x.LotNo).IsUnique();
        b.Entity<Parca>().HasIndex(x => x.SeriNo).IsUnique();
        b.Entity<Ncr>().HasIndex(x => x.NcrNo).IsUnique();
        b.Entity<IslemKaydi>().HasIndex(x => x.Zaman);
        b.Entity<Olcum>().HasIndex(x => new { x.KarakteristikId, x.MakineId, x.Zaman });
        b.Entity<Bildirim>().HasIndex(x => x.Zaman);
        b.Entity<AuditKaydi>().HasIndex(x => x.Zaman);
        b.Entity<DurusKaydi>().HasIndex(x => new { x.MakineId, x.Baslangic });
        b.Entity<ElektronikImza>().HasIndex(x => new { x.Belge, x.BelgeId });

        // Silme zincirleri: geçmiş kayıtlar kaybolmasın
        b.Entity<IslemKaydi>().HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IslemKaydi>().HasOne(x => x.Makine).WithMany().HasForeignKey(x => x.MakineId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<IslemKaydi>().HasOne(x => x.RotaAdimi).WithMany().HasForeignKey(x => x.RotaAdimiId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Olcum>().HasOne(x => x.Kullanici).WithMany().HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Olcum>().HasOne(x => x.OlcumAleti).WithMany().HasForeignKey(x => x.OlcumAletiId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Olcum>().HasOne(x => x.Karakteristik).WithMany().HasForeignKey(x => x.KarakteristikId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<KaliteKarakteristigi>().HasOne(x => x.RotaAdimi).WithMany().HasForeignKey(x => x.RotaAdimiId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Ncr>().HasOne(x => x.Parca).WithMany().HasForeignKey(x => x.ParcaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Parca>().HasOne(x => x.Urun).WithMany().HasForeignKey(x => x.UrunId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<HammaddeTuketimi>().HasOne(x => x.HammaddeLotu).WithMany().HasForeignKey(x => x.HammaddeLotuId).OnDelete(DeleteBehavior.Restrict);
    }

    // ---------- Audit log ----------

    private record BekleyenDenetim(EntityEntry Kayit, string Islem, List<Degisiklik> Degisiklikler);
    public record Degisiklik(string Alan, string? Eski, string? Yeni);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (DenetimKapali) return base.SaveChanges(acceptAllChangesOnSuccess);

        AuditKorumasi();
        var bekleyen = DenetimHazirla();
        var sonuc = base.SaveChanges(acceptAllChangesOnSuccess);
        if (DenetimYaz(bekleyen)) base.SaveChanges(acceptAllChangesOnSuccess);
        return sonuc;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (DenetimKapali) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        AuditKorumasi();
        var bekleyen = DenetimHazirla();
        var sonuc = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (DenetimYaz(bekleyen)) await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        return sonuc;
    }

    // Audit kayıtları ve elektronik imzalar değiştirilemez ve silinemez
    private void AuditKorumasi()
    {
        var degisen = ChangeTracker.Entries<AuditKaydi>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (degisen) throw new InvalidOperationException("Audit kayıtları değiştirilemez veya silinemez.");
        var imza = ChangeTracker.Entries<ElektronikImza>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (imza) throw new InvalidOperationException("Elektronik imzalar değiştirilemez veya silinemez.");
    }

    // Oturum bilgisi gibi sık değişen alanlar audit'e yazılmaz
    private static readonly HashSet<string> DenetimDisi = new()
    {
        nameof(Kullanici.SonGiris), nameof(Kullanici.HataliDeneme), nameof(Kullanici.KilitBitis), nameof(Kullanici.SonBildirimOkuma),
        nameof(Kullanici.PanelDuzeni)
    };

    private List<BekleyenDenetim> DenetimHazirla()
    {
        var liste = new List<BekleyenDenetim>();
        foreach (var e in ChangeTracker.Entries().Where(e => e.Entity is IDenetlenebilir))
        {
            if (e.State == EntityState.Modified)
            {
                var degisiklikler = e.Properties
                    .Where(p => p.IsModified && !DenetimDisi.Contains(p.Metadata.Name) && !Equals(p.OriginalValue, p.CurrentValue))
                    .Select(p => new Degisiklik(p.Metadata.Name, Yaz(p.Metadata.Name, p.OriginalValue), Yaz(p.Metadata.Name, p.CurrentValue)))
                    .ToList();
                if (degisiklikler.Count > 0) liste.Add(new BekleyenDenetim(e, "Güncelleme", degisiklikler));
            }
            else if (e.State == EntityState.Added)
            {
                liste.Add(new BekleyenDenetim(e, "Ekleme", new List<Degisiklik>()));
            }
            else if (e.State == EntityState.Deleted)
            {
                var degerler = e.Properties
                    .Select(p => new Degisiklik(p.Metadata.Name, Yaz(p.Metadata.Name, p.OriginalValue), null))
                    .ToList();
                liste.Add(new BekleyenDenetim(e, "Silme", degerler));
            }
        }
        return liste;
    }

    private bool DenetimYaz(List<BekleyenDenetim> bekleyen)
    {
        if (bekleyen.Count == 0) return false;

        var kullanici = _http?.HttpContext?.User?.Identity?.Name ?? "sistem";
        var zaman = DateTime.Now;

        foreach (var b in bekleyen)
        {
            var degisiklikler = b.Degisiklikler;
            if (b.Islem == "Ekleme")
            {
                // Eklenen kaydın kimliği kaydetmeden sonra belli olur
                degisiklikler = b.Kayit.Properties
                    .Where(p => p.Metadata.Name != "Id" && p.CurrentValue != null)
                    .Select(p => new Degisiklik(p.Metadata.Name, null, Yaz(p.Metadata.Name, p.CurrentValue)))
                    .ToList();
            }

            AuditKayitlari.Add(new AuditKaydi
            {
                Zaman = zaman,
                KullaniciAdi = kullanici,
                Tablo = b.Kayit.Metadata.ClrType.Name,
                KayitId = b.Kayit.Property("Id").CurrentValue?.ToString() ?? "",
                Islem = b.Islem,
                Degisiklikler = JsonSerializer.Serialize(degisiklikler)
            });
        }
        return true;
    }

    private static string? Yaz(string alan, object? deger)
    {
        if (deger == null) return null;
        if (alan == nameof(Kullanici.SifreHash)) return "••••••";
        return deger switch
        {
            DateTime t => t.ToString("dd.MM.yyyy HH:mm"),
            double d => d.ToString("0.####"),
            bool b => b ? "Evet" : "Hayır",
            _ => deger.ToString()
        };
    }
}
