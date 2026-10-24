using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cardex.Services;

// Résout le sprite officiel (PokeAPI) correspondant à un nom de carte TCG, pour l'afficher à côté
// du Pokémon le plus collecté dans Settings → Stats. Best-effort : un nom non reconnu (carte
// Trainer/Energy, forme trop exotique) retourne null plutôt que de lever une exception — l'UI
// masque simplement l'image dans ce cas.
public class PokeSpriteService
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://pokeapi.co/api/v2/") };

    // Formes régionales avec leur propre entrée PokeAPI distincte du nom de base — essayées avant
    // le nettoyage générique, sinon on retomberait sur le sprite de la forme normale.
    private static readonly (string Prefix, string Suffix)[] RegionalForms =
    [
        ("Alolan ", "-alola"),
        ("Galarian ", "-galar"),
        ("Hisuian ", "-hisui"),
        ("Paldean ", "-paldea"),
    ];

    // "Prism Star" doit être listé avant "Star" seul : sinon le [\s-]+ qui précède l'alternance se
    // cale sur l'espace juste avant "Star" (plus proche de la fin), ne retire que " Star" et laisse
    // "Prism" accroché au nom (ex. "Lugia Prism Star" → "Lugia Prism" au lieu de "Lugia").
    private static readonly Regex SuffixPattern = new(
        @"[\s-]+(ex|EX|GX|VMAX|VSTAR|V-UNION|VUNION|V|Prime|BREAK|Prism Star|Star|LEGEND|4ever)$",
        RegexOptions.Compiled);

    private static readonly string[] StripPrefixes =
        ["Dark ", "Light ", "Shining ", "Mega ", "Radiant ", "Team Rocket's ", "Rocket's ", "Ancient ", "Future "];

    public async Task<string?> GetSpriteUrlAsync(string cardName)
    {
        foreach (var candidate in BuildCandidates(cardName))
        {
            var url = await TryFetchAsync(candidate);
            if (url is not null) return url;
        }
        return null;
    }

    // Retire les décorations TCG (Dark/Mega/Shining..., ex/GX/V/VMAX...) pour ne garder que le nom
    // d'espèce — utilisé aussi par MainViewModel.RefreshStatsAsync pour regrouper "Lugia",
    // "Shining Lugia", "Lugia V", "Lugia VSTAR"... sous un même Pokémon dans la stat "le plus
    // collecté" (sinon chaque variante est comptée séparément et aucune ne ressort). Les formes
    // régionales (Alolan/Galarian/...) ne sont PAS nettoyées ici : ce sont des Pokémon distincts
    // aux yeux d'un collectionneur, contrairement aux simples variantes de carte.
    public static string CleanSpeciesName(string cardName)
    {
        var cleaned = cardName;
        foreach (var p in StripPrefixes)
            if (cleaned.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[p.Length..];
        cleaned = SuffixPattern.Replace(cleaned, "");
        return cleaned.Trim();
    }

    private static IEnumerable<string> BuildCandidates(string cardName)
    {
        foreach (var (prefix, suffix) in RegionalForms)
        {
            if (cardName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return Slugify(cardName[prefix.Length..]) + suffix;
        }

        yield return Slugify(CleanSpeciesName(cardName));
    }

    private static string Slugify(string name) =>
        Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    // Icône "overworld" plutôt que l'artwork officiel — ordre de préférence demandé : Sword/Shield
    // (Gen 8), puis Gen 7, puis Scarlet/Violet (Gen 9). Toutes les formes/variantes n'ont pas
    // forcément d'icône pour chaque génération (Pokémon ajoutés après coup, etc.), d'où la chaîne
    // de repli plutôt qu'un chemin unique.
    private static readonly string[][] IconPaths =
    [
        ["versions", "generation-viii", "sword-shield", "icons", "front_default"],
        ["versions", "generation-vii", "icons", "front_default"],
        ["versions", "generation-ix", "scarlet-violet", "icons", "front_default"],
    ];

    private async Task<string?> TryFetchAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        try
        {
            var json = await _http.GetStringAsync($"pokemon/{slug}");
            using var doc = JsonDocument.Parse(json);
            var sprites = doc.RootElement.GetProperty("sprites");

            foreach (var path in IconPaths)
            {
                var url = TryGetPath(sprites, path);
                if (url is not null) return url;
            }
        }
        catch { /* best-effort — nom non reconnu ou API indisponible */ }
        return null;
    }

    private static string? TryGetPath(JsonElement root, string[] path)
    {
        var current = root;
        foreach (var key in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(key, out current))
                return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
