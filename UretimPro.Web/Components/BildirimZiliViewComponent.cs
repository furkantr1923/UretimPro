using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPro.Data;
using UretimPro.Models;
using UretimPro.Services;

namespace UretimPro.Components;

public record BildirimZiliModel(int Okunmamis, List<Bildirim> Son, DateTime SonOkuma);

// Üst çubuktaki zil: okunmamış bildirim sayısı ve son bildirimler
public class BildirimZiliViewComponent : ViewComponent
{
    private readonly UretimDbContext _db;
    public BildirimZiliViewComponent(UretimDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var id = UserClaimsPrincipal.KullaniciId();
        var kullanici = await _db.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (kullanici == null) return View(new BildirimZiliModel(0, new(), DateTime.Now));

        var sorgu = BildirimServisi.RolIcin(_db.Bildirimler, kullanici.Rol);
        var okunmamis = await sorgu.CountAsync(b => b.Zaman > kullanici.SonBildirimOkuma);
        var son = await sorgu.OrderByDescending(b => b.Zaman).Take(6).ToListAsync();
        return View(new BildirimZiliModel(okunmamis, son, kullanici.SonBildirimOkuma));
    }
}
