using System.IO;
using Cardex.Models;
using Microsoft.EntityFrameworkCore;

namespace Cardex.Data;

public class AppDbContext : DbContext
{
    public DbSet<OwnedCard> OwnedCards { get; set; }
    public DbSet<CachedSet> CachedSets { get; set; }
    public DbSet<CachedCard> CachedCards { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cardex");
        Directory.CreateDirectory(folder);
        options.UseSqlite($"Data Source={Path.Combine(folder, "collection.db")}");
    }
}
