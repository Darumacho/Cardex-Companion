using Cardex.Data;
using Cardex.Models;
using Cardex.Services;
using Cardex.ViewModels;
using Cardex.Views;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cardex;

public partial class App : Application
{
    public static MainViewModel MainVm { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var db = new AppDbContext();
        await db.Database.EnsureCreatedAsync();
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE OwnedCards ADD COLUMN Quantity INTEGER NOT NULL DEFAULT 1"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS CachedSets (
                SetId TEXT PRIMARY KEY NOT NULL, Name TEXT NOT NULL DEFAULT '',
                Series TEXT NOT NULL DEFAULT '', Total INTEGER NOT NULL DEFAULT 0,
                ReleaseDate TEXT NOT NULL DEFAULT '', LogoUrl TEXT NOT NULL DEFAULT '',
                SymbolUrl TEXT NOT NULL DEFAULT '', CachedAt TEXT NOT NULL DEFAULT '')"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS CachedCards (
                CardId TEXT PRIMARY KEY NOT NULL, SetId TEXT NOT NULL DEFAULT '',
                Name TEXT NOT NULL DEFAULT '', Number TEXT NOT NULL DEFAULT '',
                ImageSmall TEXT NOT NULL DEFAULT '', Rarity TEXT,
                SortOrder INTEGER NOT NULL DEFAULT 0)"); }
        catch { }

        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS WantedCards (
                CardId TEXT PRIMARY KEY NOT NULL, SetId TEXT NOT NULL DEFAULT '')"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS FavoriteSets (
                SetId TEXT PRIMARY KEY NOT NULL)"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN CmLow REAL"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN TcgLow REAL"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN PricesUpdatedAt TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN CmUrl TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN TcgUrl TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN ImageLarge TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedSets ADD COLUMN PtcgoCode TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedSets ADD COLUMN ShortCode TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ExcludedCards (
                CardId TEXT PRIMARY KEY NOT NULL, SetId TEXT NOT NULL DEFAULT '')"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Tags (
                Id    INTEGER PRIMARY KEY AUTOINCREMENT,
                Name  TEXT NOT NULL,
                Color TEXT NOT NULL DEFAULT '#8888aa')"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS CardTags (
                CardId TEXT NOT NULL PRIMARY KEY,
                TagId  INTEGER NOT NULL)"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS UnlockedAchievements (
                Id          TEXT NOT NULL PRIMARY KEY,
                UnlockedAt  TEXT NOT NULL DEFAULT '')"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN Supertype TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN Subtypes TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN Types TEXT"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedSets ADD COLUMN StandardLegal INTEGER NOT NULL DEFAULT 0"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedSets ADD COLUMN ExpandedLegal INTEGER NOT NULL DEFAULT 0"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedSets ADD COLUMN Source TEXT NOT NULL DEFAULT 'pokemontcgio'"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE CachedCards ADD COLUMN Source TEXT NOT NULL DEFAULT 'pokemontcgio'"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Decks (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Name      TEXT NOT NULL DEFAULT 'New Deck',
                CreatedAt TEXT NOT NULL DEFAULT '',
                UpdatedAt TEXT NOT NULL DEFAULT '')"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS DeckCards (
                DeckId   INTEGER NOT NULL,
                CardId   TEXT NOT NULL,
                Quantity INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (DeckId, CardId))"); }
        catch { }

        var settings = AppSettings.Load();
        var tcgService = new PokemonTcgService(settings.ApiKey);
        var tcgdexService = new TCGdexService();
        var imageCache = new ImageCacheService();

        MainVm = new MainViewModel(tcgService, tcgdexService, imageCache, db);

        var asm = Assembly.GetExecutingAssembly();

        using (var stream = asm.GetManifestResourceStream("Cardex.Logo.png"))
            if (stream != null)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                MainVm.AppLogo = bmp;
            }

        using (var stream = asm.GetManifestResourceStream("Cardex.Name.png"))
            if (stream != null)
                MainVm.AppName = LoadAndCrop(stream);

        Resources["MagnifierCursor"] = CursorHelper.CreateMagnifier();

        var window = new MainWindow { DataContext = MainVm };

        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.ico");
        if (System.IO.File.Exists(iconPath))
            window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri(iconPath, UriKind.Absolute),
                System.Windows.Media.Imaging.BitmapCreateOptions.None,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

        window.Show();

        await BackfillShortCodesAsync(db);
        await SeedDbFromEmbeddedAsync(db);
        await SeedTcgdexFallbackAsync(db);
        await SeedCompletionCardsAsync(db);
        await BackfillSupertypesAsync(db);
        await MainVm.LoadSetsAsync();
        _ = MainVm.CheckForUpdateAsync();
    }

    private static async Task BackfillSupertypesAsync(AppDbContext db)
    {
        try
        {
            // Skip entirely if all cards already have supertype data
            if (!await db.CachedCards.AnyAsync(c => c.Supertype == null)) return;

            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("Cardex.SeedData.supertypes.json");
            if (stream is null) return;

            var json = await new StreamReader(stream, detectEncodingFromByteOrderMarks: true).ReadToEndAsync();
            var map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (map is null || map.Count == 0) return;

            static string Expand(string code) => code switch
            {
                "T" => "Trainer",
                "E" => "Energy",
                _   => "Pokémon"
            };

            // Only fetch cards that are missing supertype and exist in the map
            var nullIds = await db.CachedCards
                .Where(c => c.Supertype == null)
                .Select(c => c.CardId)
                .ToListAsync();

            var toUpdate = nullIds
                .Where(id => map.ContainsKey(id))
                .Select(id => (Id: id, Supertype: Expand(map[id])))
                .ToList();

            if (toUpdate.Count == 0) return;

            // Batch updates in groups of 500 to avoid SQL parameter limits
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            foreach (var batch in toUpdate.Chunk(500))
            {
                var ids = batch.Select(x => x.Id).ToList();
                var cards = await db.CachedCards.Where(c => ids.Contains(c.CardId)).ToListAsync();
                foreach (var card in cards)
                {
                    var st = batch.First(x => x.Id == card.CardId).Supertype;
                    card.Supertype = st;
                }
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackfillSupertypes failed: {ex}");
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    private static async Task BackfillShortCodesAsync(AppDbContext db)
    {
        var sets = await db.CachedSets.ToListAsync();
        bool changed = false;
        foreach (var s in sets)
        {
            var expected = SetShortCodes.BySetId.TryGetValue(s.SetId, out var code) ? code : null;
            if (s.ShortCode != expected) { s.ShortCode = expected; changed = true; }
        }
        if (changed) await db.SaveChangesAsync();
    }

    private static async Task SeedDbFromEmbeddedAsync(AppDbContext db)
    {
        try
        {
            if (await db.CachedSets.AnyAsync()) return;

            var asm = Assembly.GetExecutingAssembly();
            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // StreamReader with BOM detection — PowerShell 5.1 adds UTF-8 BOM
            using var setsStream = asm.GetManifestResourceStream("Cardex.SeedData.sets.json");
            if (setsStream is null) return;
            var setsJson = await new StreamReader(setsStream, detectEncodingFromByteOrderMarks: true).ReadToEndAsync();
            var sets = JsonSerializer.Deserialize<List<SeedSetEntry>>(setsJson, jsonOpts);
            if (sets is null || sets.Count == 0) return;

            db.ChangeTracker.AutoDetectChangesEnabled = false;

            db.CachedSets.AddRange(sets.Select(s => new CachedSet
            {
                SetId = s.Id, Name = s.Name, Series = s.Series, Total = s.Total,
                ReleaseDate = s.ReleaseDate, LogoUrl = s.LogoUrl, SymbolUrl = s.SymbolUrl,
                CachedAt = DateTime.UtcNow,
                ShortCode = SetShortCodes.BySetId.TryGetValue(s.Id, out var sc) ? sc : null
            }));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            using var cardsStream = asm.GetManifestResourceStream("Cardex.SeedData.cards.json");
            if (cardsStream is not null)
            {
                var cardsJson = await new StreamReader(cardsStream, detectEncodingFromByteOrderMarks: true).ReadToEndAsync();
                var cards = JsonSerializer.Deserialize<List<SeedCardEntry>>(cardsJson, jsonOpts);
                if (cards is not null && cards.Count > 0)
                {
                    int sort = 0;
                    foreach (var batch in cards.Chunk(1000))
                    {
                        db.CachedCards.AddRange(batch.Select(c => new CachedCard
                        {
                            CardId = c.Id, SetId = c.SetId, Name = c.Name,
                            Number = c.Number, ImageSmall = c.ImageSmall,
                            Rarity = c.Rarity, SortOrder = sort++
                        }));
                        await db.SaveChangesAsync();
                        db.ChangeTracker.Clear();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Seed failed: {ex}");
            // Une entrée en échec (ex: doublon de CardId) laisse des entités "Added" non
            // sauvegardées dans le tracker EF — les nettoyer pour ne pas polluer les opérations
            // suivantes sur ce même AppDbContext (elles réapparaîtraient au prochain SaveChanges).
            db.ChangeTracker.Clear();
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    // Sets/cartes que pokemontcg.io n'a pas (promos Mega Evolution, Trainer Kits, McDonald's
    // récents, etc.), pré-générées depuis TCGdex (voir SeedData/tcgdex_sets.json /
    // tcgdex_cards.json). Contrairement à SeedDbFromEmbeddedAsync, tourne à chaque démarrage
    // (pas seulement sur une DB vide) et réconcilie la DB avec le seed embarqué à chaque fois :
    // ajoute les sets/cartes manquants, met à jour ceux dont les données ont changé (ex: image
    // retrouvée), et retire ceux qui ont été explicitement exclus d'une régénération du seed
    // (ex: doublons, sets à 0-1 carte). Tout est identifié par SetId, propre à TCGdex et jamais
    // en collision avec pokemontcg.io.
    private static async Task SeedTcgdexFallbackAsync(AppDbContext db)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            using var setsStream = asm.GetManifestResourceStream("Cardex.SeedData.tcgdex_sets.json");
            if (setsStream is null) return;
            var setsJson = await new StreamReader(setsStream, detectEncodingFromByteOrderMarks: true).ReadToEndAsync();
            var seedSets = JsonSerializer.Deserialize<List<TcgdexSeedSetEntry>>(setsJson, jsonOpts);
            if (seedSets is null || seedSets.Count == 0) return;

            using var cardsStream = asm.GetManifestResourceStream("Cardex.SeedData.tcgdex_cards.json");
            if (cardsStream is null) return;
            var cardsJson = await new StreamReader(cardsStream, detectEncodingFromByteOrderMarks: true).ReadToEndAsync();
            var seedCards = JsonSerializer.Deserialize<List<TcgdexSeedCardEntry>>(cardsJson, jsonOpts) ?? [];

            var seedSetIds = seedSets.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dbTcgdexSetIds = (await db.CachedSets.Where(s => s.Source == "tcgdex").Select(s => s.SetId).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Retire les sets tcgdex en base qui ne sont plus dans le seed (doublons, sets vides…).
            var removedSetIds = dbTcgdexSetIds.Except(seedSetIds).ToList();
            if (removedSetIds.Count > 0)
            {
                await db.CachedCards.Where(c => removedSetIds.Contains(c.SetId)).ExecuteDeleteAsync();
                await db.CachedSets.Where(s => removedSetIds.Contains(s.SetId)).ExecuteDeleteAsync();
                dbTcgdexSetIds.ExceptWith(removedSetIds);
            }

            var newSets = seedSets.Where(s => !dbTcgdexSetIds.Contains(s.Id)).ToList();
            if (newSets.Count > 0)
            {
                db.CachedSets.AddRange(newSets.Select(s => new CachedSet
                {
                    SetId = s.Id, Name = s.Name, Series = s.Series, Total = s.Total,
                    ReleaseDate = s.ReleaseDate, LogoUrl = s.LogoUrl, SymbolUrl = s.SymbolUrl,
                    CachedAt = DateTime.UtcNow,
                    StandardLegal = s.StandardLegal, ExpandedLegal = s.ExpandedLegal,
                    Source = "tcgdex"
                }));

                var newSetIds = newSets.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var sort = 0;
                db.CachedCards.AddRange(seedCards.Where(c => newSetIds.Contains(c.SetId)).Select(c => new CachedCard
                {
                    CardId = c.Id, SetId = c.SetId, Name = c.Name, Number = c.Number,
                    ImageSmall = c.ImageSmall, ImageLarge = c.ImageLarge, Rarity = c.Rarity,
                    SortOrder = sort++, Supertype = c.Supertype, Subtypes = c.Subtypes,
                    Types = c.Types, Source = "tcgdex"
                }));
                await db.SaveChangesAsync();
            }

            // Met à jour les sets/cartes déjà en base si le seed a été régénéré avec de
            // meilleures données (ex: catégorie, symbole ou image retrouvés).
            var existingIds = dbTcgdexSetIds;
            if (existingIds.Count > 0)
            {
                var seedSetById = seedSets.Where(s => existingIds.Contains(s.Id)).ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
                var setsToRefresh = await db.CachedSets.Where(s => existingIds.Contains(s.SetId)).ToListAsync();
                var setsChanged = false;
                foreach (var row in setsToRefresh)
                {
                    if (!seedSetById.TryGetValue(row.SetId, out var seed)) continue;
                    if (row.Series == seed.Series && row.LogoUrl == seed.LogoUrl && row.SymbolUrl == seed.SymbolUrl
                        && row.StandardLegal == seed.StandardLegal && row.ExpandedLegal == seed.ExpandedLegal) continue;
                    row.Series = seed.Series;
                    row.LogoUrl = seed.LogoUrl;
                    row.SymbolUrl = seed.SymbolUrl;
                    row.StandardLegal = seed.StandardLegal;
                    row.ExpandedLegal = seed.ExpandedLegal;
                    setsChanged = true;
                }
                if (setsChanged) await db.SaveChangesAsync();

                var seedCardById = seedCards.Where(c => existingIds.Contains(c.SetId)).ToDictionary(c => c.Id);
                var cardsToRefresh = await db.CachedCards.Where(c => c.Source == "tcgdex" && existingIds.Contains(c.SetId)).ToListAsync();
                var cardsChanged = false;
                foreach (var row in cardsToRefresh)
                {
                    if (!seedCardById.TryGetValue(row.CardId, out var seed)) continue;
                    if (row.ImageSmall == seed.ImageSmall && row.ImageLarge == seed.ImageLarge) continue;
                    row.ImageSmall = seed.ImageSmall;
                    row.ImageLarge = seed.ImageLarge;
                    cardsChanged = true;
                }
                if (cardsChanged) await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TCGdex fallback seed failed: {ex}");
            db.ChangeTracker.Clear();
        }
    }

    // Complète les cartes manquantes de sets pokemontcg.io connus pour être bloqués à 250 cartes
    // (limite de pagination de leur API, qui renvoie une 500 sur `cards?...&page=2` pour certains
    // sets — ex: me2pt5 "Ascended Heroes", 295 cartes réelles). Le set reste rattaché à
    // pokemontcg.io (Source inchangée) ; seules les cartes au-delà de ce que pokemontcg.io peut
    // servir sont ajoutées, avec Source="tcgdex" pour tracer leur provenance réelle.
    private static async Task SeedCompletionCardsAsync(AppDbContext db)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            using var stream = asm.GetManifestResourceStream("Cardex.SeedData.tcgdex_completion_cards.json");
            if (stream is null) return;
            var json = await new StreamReader(stream, detectEncodingFromByteOrderMarks: true).ReadToEndAsync();
            var completionCards = JsonSerializer.Deserialize<List<TcgdexCompletionCardEntry>>(json, jsonOpts);
            if (completionCards is null || completionCards.Count == 0) return;

            foreach (var group in completionCards.GroupBy(c => c.SetId))
            {
                // Le set doit déjà exister (pokemontcg.io) — sinon rien à compléter ici, c'est le
                // rôle de SeedTcgdexFallbackAsync pour un set entièrement absent.
                if (!await db.CachedSets.AnyAsync(s => s.SetId == group.Key)) continue;

                var existingNumbers = (await db.CachedCards
                    .Where(c => c.SetId == group.Key)
                    .Select(c => c.Number)
                    .ToListAsync()).ToHashSet();

                var toAdd = group.Where(c => !existingNumbers.Contains(c.Number)).ToList();
                if (toAdd.Count == 0) continue;

                var maxSort = await db.CachedCards.Where(c => c.SetId == group.Key)
                    .Select(c => (int?)c.SortOrder).MaxAsync() ?? 0;
                var sort = maxSort + 1;

                db.CachedCards.AddRange(toAdd.Select(c => new CachedCard
                {
                    CardId = c.Id, SetId = c.SetId, Name = c.Name, Number = c.Number,
                    ImageSmall = c.ImageSmall, ImageLarge = c.ImageLarge, Rarity = c.Rarity,
                    SortOrder = sort++, Supertype = c.Supertype, Subtypes = c.Subtypes,
                    Types = c.Types, Source = "tcgdex"
                }));
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Completion seed failed: {ex}");
            db.ChangeTracker.Clear();
        }
    }

    private record SeedSetEntry(string Id, string Name, string Series, int Total,
        string ReleaseDate, string LogoUrl, string SymbolUrl);
    private record SeedCardEntry(string Id, string Name, string Number,
        string SetId, string ImageSmall, string? Rarity);
    private record TcgdexSeedSetEntry(string Id, string Name, string Series, int Total,
        string ReleaseDate, string LogoUrl, string SymbolUrl, bool StandardLegal, bool ExpandedLegal);
    private record TcgdexSeedCardEntry(string Id, string Name, string Number, string SetId,
        string ImageSmall, string? ImageLarge, string? Rarity, string? Supertype, string? Subtypes, string? Types);
    private record TcgdexCompletionCardEntry(string Id, string Name, string Number, string SetId,
        string ImageSmall, string? ImageLarge, string? Rarity, string? Supertype, string? Subtypes, string? Types);

    private static ImageSource LoadAndCrop(Stream stream)
    {
        var source = new BitmapImage();
        source.BeginInit();
        source.StreamSource = stream;
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.EndInit();
        source.Freeze();

        var fmt = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = fmt.PixelWidth, h = fmt.PixelHeight, stride = w * 4;
        var pixels = new byte[h * stride];
        fmt.CopyPixels(pixels, stride, 0);

        int left = w, right = 0, top = h, bottom = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (pixels[y * stride + x * 4 + 3] > 10)
                {
                    if (x < left)   left   = x;
                    if (x > right)  right  = x;
                    if (y < top)    top    = y;
                    if (y > bottom) bottom = y;
                }

        if (left >= right || top >= bottom) return source;

        var cropped = new CroppedBitmap(source,
            new System.Windows.Int32Rect(left, top, right - left + 1, bottom - top + 1));
        cropped.Freeze();
        return cropped;
    }
}

