using Avalonia.Media.Imaging;

namespace Cardex.ViewModels;

// Snapshot immuable recalculé à chaque ouverture de Settings (pas de notification de propriété
// individuelle : MainViewModel remplace l'instance entière et notifie juste "Stats").
public class CollectionStatsViewModel
{
    public int TotalOwnedCards { get; init; }
    public int TotalHoloCards { get; init; }
    public int TotalSets { get; init; }
    public int SetsBegun { get; init; }
    public int SetsComplete { get; init; }
    public string? TopPokemonName { get; init; }
    public int TopPokemonCount { get; init; }
    // Bitmap Avalonia (ImageSource côté WPF) : le sprite PokeAPI est recadré pour retirer le padding
    // transparent (voir MainViewModel.CropTransparentPadding / Platform.ImageCrop).
    public Bitmap? TopPokemonSprite { get; init; }
    public bool HasTopPokemonSprite => TopPokemonSprite is not null;
    public List<RarityStat> RarityBreakdown { get; init; } = [];
    public List<SeriesCompletionStat> TopCategories { get; init; } = [];
    public int UnlockedAchievements { get; init; }
    public int TotalAchievements { get; init; }

    // Les 6 Pokémon les plus collectés (mode "équipe"), même nettoyage/filtre que TopPokemonName —
    // calculé systématiquement (pas seulement si le mode équipe est actif) : coût marginal face au
    // reste de RefreshStatsAsync, et évite de dupliquer la logique de tri pour juste basculer l'affichage.
    public List<PokemonTeamMember> TopPokemonTeam { get; init; } = [];
}

public record RarityStat(string Rarity, int Count, string PercentText);
public record SeriesCompletionStat(string SeriesName, string PercentText, int Owned, int Total);

public class PokemonTeamMember
{
    public required string Name { get; init; }
    public required int Count { get; init; }
    public Bitmap? Sprite { get; init; }
    public bool HasSprite => Sprite is not null;
}
