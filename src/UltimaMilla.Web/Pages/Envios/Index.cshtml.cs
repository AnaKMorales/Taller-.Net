using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Web.Pages.Envios;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public List<Envio> Envios { get; set; } = new();

    public async Task OnGetAsync()
    {
        Envios = await _db.Envios.AsNoTracking().Include(e => e.Destinatario).ThenInclude(d => d.Direccion)
            .OrderByDescending(e => e.FechaAdmision).ToListAsync();
    }
}
