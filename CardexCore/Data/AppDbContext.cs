using System.IO;
using Cardex.Models;
using Microsoft.EntityFrameworkCore;

namespace Cardex.Data;

public class AppDbContext : DbContext
{
    public DbSet<OwnedCard> OwnedCards { get; set; }
    public DbSet<WantedCard> WantedCards { get; set; }
    public DbSet<FavoriteSet> FavoriteSets { get; set; }
    public DbSet<CachedSet> CachedSets { get; set; }
    public DbSet<CachedCard> CachedCards { get; set; }
    public DbSet<ExcludedCard> ExcludedCards { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={GetDatabasePath()}");

    protected virtual string GetDatabasePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cardex");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "collection.db");
    }
}
