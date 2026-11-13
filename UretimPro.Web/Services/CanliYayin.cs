using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;

namespace UretimPro.Services;

// Server-Sent Events: tarayıcılara canlı bildirim ve istasyon akışı gönderir (ek kütüphane gerektirmez)
public class CanliYayin
{
    public record Abone(Guid Id, string Rol, Channel<string> Kanal);

    private readonly ConcurrentDictionary<Guid, Abone> _aboneler = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Abone AboneOl(string rol)
    {
        var abone = new Abone(Guid.NewGuid(), rol, Channel.CreateUnbounded<string>());
        _aboneler[abone.Id] = abone;
        return abone;
    }

    public void AboneligiBitir(Abone abone)
    {
        _aboneler.TryRemove(abone.Id, out _);
        abone.Kanal.Writer.TryComplete();
    }

    // roller null ise herkese gider
    public void Yayinla(object mesaj, IEnumerable<string>? roller = null)
    {
        var metin = JsonSerializer.Serialize(mesaj, Json);
        var hedef = roller?.ToHashSet();
        foreach (var a in _aboneler.Values)
        {
            if (hedef == null || hedef.Contains(a.Rol)) a.Kanal.Writer.TryWrite(metin);
        }
    }
}

public class BildirimServisi
{
    private readonly UretimDbContext _db;
    private readonly CanliYayin _yayin;

    public BildirimServisi(UretimDbContext db, CanliYayin yayin)
    {
        _db = db;
        _yayin = yayin;
    }

    // Bildirimi kaydeder ve ilgili rollerin ekranına anında düşürür
    public async Task GonderAsync(string baslik, string mesaj, string seviye, string? link, params string[] roller)
    {
        var hedef = roller.Length == 0 ? Roller.Tumu : roller;
        var bildirim = new Bildirim
        {
            Baslik = baslik,
            Mesaj = mesaj,
            Seviye = seviye,
            Link = link,
            HedefRoller = "," + string.Join(",", hedef) + ",",
            Zaman = DateTime.Now
        };
        _db.Bildirimler.Add(bildirim);
        await _db.SaveChangesAsync();

        _yayin.Yayinla(new
        {
            tur = "bildirim",
            id = bildirim.Id,
            baslik,
            mesaj,
            seviye,
            link,
            zaman = bildirim.Zaman.ToString("HH:mm")
        }, hedef);
    }

    public static IQueryable<Bildirim> RolIcin(IQueryable<Bildirim> sorgu, string rol)
    {
        var anahtar = "," + rol + ",";
        return sorgu.Where(b => b.HedefRoller.Contains(anahtar));
    }
}

public static class KimlikUzantilari
{
    public static int KullaniciId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string Rol(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? "";

    public static string AdSoyad(this ClaimsPrincipal user) =>
        user.FindFirstValue("AdSoyad") ?? user.Identity?.Name ?? "";

    public static bool RoldeMi(this ClaimsPrincipal user, params string[] roller) =>
        roller.Any(user.IsInRole);
}
