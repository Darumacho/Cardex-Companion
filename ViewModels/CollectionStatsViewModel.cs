using System.Windows.Media;

namespace Cardex.ViewModels;

// Snapshot immuable recalculé à chaque ouverture de Settings (pas de notification de propriété
// individuelle : MainViewModel remplace l'instance entière et notifie juste "Stats").
public class CollectionStatsViewModel
{
    public int TotalOwnedCards { get; init; }
    public int TotalSets { get; init; }
    public int SetsBegun { get; init; }
    public int SetsComplete { get; init; }
    public string? TopPokemonName { get; init; }
    public int TopPokemonCount { get; init; }
    // ImageSource (pas BitmapImage) : le sprite PokeAPI est recadré pour retirer le padding
    // transparent (voir MainViewModel.CropTransparentPadding), ce qui donne un CroppedBitmap.
    public ImageSource? TopPokemonSprite { get; init; }
    public bool HasTopPokemonSprite => TopPokemonSprite is not null;
    public List<RarityStat> RarityBreakdown { get; init; } = [];
    public List<SeriesCompletionStat> TopCategories { get; init; } = [];
    public int UnlockedAchievements { get; init; }
    public int TotalAchievements { get; init; }
}

public record RarityStat(string Rarity, int Count, string PercentText);
public record SeriesCompletionStat(string SeriesName, string PercentText, int Owned, int Total);
