using System.Net.Http;
using System.Text.Json;
using Cardex.Models;

namespace Cardex.Services;

// Fallback data source for sets pokemontcg.io doesn't have (e.g. Mega Evolution promos,
// Pokémon TCG Pocket). See plan: TCGdex comme source de secours.
public class TCGdexService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public TCGdexService()
    {
        _http = new HttpClient { BaseAddress = new Uri("https://api.tcgdex.net/v2/en/") };
    }

    public Task<List<TcgdexSetBrief>> GetSetsAsync() =>
        RetryAsync(() => FetchSetsAsync());

    public Task<TcgdexSetDetail?> GetSetDetailAsync(string setId) =>
        RetryAsync(() => FetchSetDetailAsync(setId));

    public Task<TcgdexCard?> GetCardAsync(string cardId) =>
        RetryAsync(() => FetchCardAsync(cardId));

    private async Task<List<TcgdexSetBrief>> FetchSetsAsync()
    {
        var response = await _http.GetStringAsync("sets");
        return JsonSerializer.Deserialize<List<TcgdexSetBrief>>(response, _json) ?? [];
    }

    private async Task<TcgdexSetDetail?> FetchSetDetailAsync(string setId)
    {
        try
        {
            var response = await _http.GetStringAsync($"sets/{setId}");
            return JsonSerializer.Deserialize<TcgdexSetDetail>(response, _json);
        }
        catch (HttpRequestException) { return null; }
    }

    private async Task<TcgdexCard?> FetchCardAsync(string cardId)
    {
        try
        {
            var response = await _http.GetStringAsync($"cards/{cardId}");
            return JsonSerializer.Deserialize<TcgdexCard>(response, _json);
        }
        catch (HttpRequestException) { return null; }
    }

    // Card image base URLs (e.g. .../sv03/006) need a quality+format segment appended.
    public static string BuildCardImageUrl(string baseUrl, string quality, string ext = "png") =>
        $"{baseUrl}/{quality}.{ext}";

    // Set logo/symbol base URLs (e.g. .../sv03/logo) just need a format extension appended.
    public static string BuildAssetUrl(string baseUrl, string ext = "png") =>
        $"{baseUrl}.{ext}";

    // TCGdex uses "Pokemon" (no accent) — normalize to match the "Pokémon" convention used
    // throughout the app (InferSupertype, DeckEntryVm.IsBasicEnergy, etc.).
    public static string? NormalizeSupertype(string? category) => category switch
    {
        "Pokemon" => "Pokémon",
        "Trainer" => "Trainer",
        "Energy"  => "Energy",
        _ => category
    };

    // Emulates pokemontcg.io's comma-joined "subtypes" string from TCGdex's separate
    // stage/trainerType/energyType fields, so downstream logic (InferSupertype, IsBasicEnergy,
    // MaxQuantity) keeps working unmodified regardless of data source.
    public static string? BuildSubtypes(TcgdexCard card) => card.Category switch
    {
        "Pokemon" => string.Join(", ", new[] { card.Stage, card.Suffix }.Where(s => !string.IsNullOrWhiteSpace(s))),
        "Trainer" => card.TrainerType,
        "Energy"  => card.EnergyType is null ? null : $"{card.EnergyType} Energy",
        _ => null
    } is { Length: > 0 } s ? s : null;

    private static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxAttempts = 3)
    {
        Exception? last = null;
        for (int i = 0; i < maxAttempts; i++)
        {
            try { return await action(); }
            catch (Exception ex)
            {
                last = ex;
                if (i < maxAttempts - 1)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
        }
        throw last!;
    }
}
