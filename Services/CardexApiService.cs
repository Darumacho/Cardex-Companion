using System.Net.Http;
using System.Text.Json;

namespace Cardex.Services;

// Enrichissement live (comblement de trous + découverte de nouveaux sets/cartes) depuis notre
// propre API Cardex, en complément du pipeline de secours existant (pokemontcg.io + seed TCGdex
// statique). Best-effort uniquement : aucune donnée locale n'est jamais écrasée, un échec réseau
// ne doit jamais bloquer le démarrage.
public class CardexApiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CardexApiService(string apiKey)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://cardex-api.dev/api/") };
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<List<CardexApiSet>> GetSetsAsync()
    {
        try
        {
            var response = await _http.GetStringAsync("sets?pageSize=250");
            var page = JsonSerializer.Deserialize<PagedResult<CardexApiSet>>(response, _json);
            return page?.Items ?? [];
        }
        catch { return []; }
    }

    public async Task<List<CardexApiCard>> GetSetCardsAsync(string setId)
    {
        try
        {
            var response = await _http.GetStringAsync($"sets/{setId}/cards?pageSize=250");
            var page = JsonSerializer.Deserialize<PagedResult<CardexApiCard>>(response, _json);
            return page?.Items ?? [];
        }
        catch { return []; }
    }

    private record PagedResult<T>(List<T> Items);
}

public record CardexApiSet(string Id, string Name, string Series, int Total, string ReleaseDate,
    string? LogoUrl, string? SymbolUrl, bool StandardLegal, bool ExpandedLegal, string Source);

public record CardexApiCard(string Id, string SetId, string Name, string Number, string? ImageSmall,
    string? ImageLarge, string? Rarity, string? Supertype, List<string>? Subtypes, List<string>? Types,
    string Source);
