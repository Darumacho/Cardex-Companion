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
    public DbSet<Tag> Tags { get; set; }
    public DbSet<CardTag> CardTags { get; set; }
    public DbSet<UnlockedAchievement> UnlockedAchievements { get; set; }
    public DbSet<Deck> Decks { get; set; }
    public DbSet<DeckCard> DeckCards { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeckCard>().HasKey(dc => new { dc.DeckId, dc.CardId });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cardex");
        Directory.CreateDirectory(folder);
        options.UseSqlite($"Data Source={Path.Combine(folder, "collection.db")}");
    }
}
