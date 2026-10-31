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
            var all = new List<CardexApiSet>();
            var page = 1;
            while (true)
            {
                var response = await _http.GetStringAsync($"sets?pageSize=250&page={page}");
                var result = JsonSerializer.Deserialize<PagedResult<CardexApiSet>>(response, _json);
                if (result is null || result.Items.Count == 0) break;
                all.AddRange(result.Items);
                if (all.Count >= result.TotalCount) break;
                page++;
            }
            return all;
        }
        catch { return []; }
    }

    // Pagine jusqu'à épuisement (certains sets, ex. swshp avec 307 cartes, dépassent les 250 de
    // la première page) — sans ça, les cartes au-delà de 250 n'étaient jamais considérées pour le
    // comblement d'images, silencieusement, puisque GetSetCardsAsync ne demandait que la page 1.
    public async Task<List<CardexApiCard>> GetSetCardsAsync(string setId)
    {
        try
        {
            var all = new List<CardexApiCard>();
            var page = 1;
            while (true)
            {
                var response = await _http.GetStringAsync($"sets/{setId}/cards?pageSize=250&page={page}");
                var result = JsonSerializer.Deserialize<PagedResult<CardexApiCard>>(response, _json);
                if (result is null || result.Items.Count == 0) break;
                all.AddRange(result.Items);
                if (all.Count >= result.TotalCount) break;
                page++;
            }
            return all;
        }
        catch { return []; }
    }

    private record PagedResult<T>(List<T> Items, int TotalCount);
}

public record CardexApiSet(string Id, string Name, string Series, int Total, string ReleaseDate,
    string? LogoUrl, string? SymbolUrl, bool StandardLegal, bool ExpandedLegal, string Source);

// PricesUpdatedAt délibérément absent : l'API renvoie ce champ dans des formats non-ISO8601
// incohérents selon les cartes (ex. "2026-08-29 13:31:32.867804" sans "T"/offset, vu sur
// me2pt5-1), ce qui fait planter tout le JsonSerializer.Deserialize de la page concernée — les
// appelants restampent plutôt la date à DateTime.UtcNow au moment de l'enregistrement local.
public record CardexApiCard(string Id, string SetId, string Name, string Number, string? ImageSmall,
    string? ImageLarge, string? Rarity, string? Supertype, List<string>? Subtypes, List<string>? Types,
    string Source, decimal? CardmarketLowPrice, decimal? TcgplayerLowPrice, string? CardmarketUrl,
    string? TcgplayerUrl);
