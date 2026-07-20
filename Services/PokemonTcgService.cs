using System.Net.Http;
using System.Text.Json;
using Cardex.Models;

namespace Cardex.Services;

public class PokemonTcgService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public PokemonTcgService(string? apiKey = null)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://api.pokemontcg.io/v2/") };
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<List<ApiSet>> GetSetsAsync()
    {
        var url = "sets?orderBy=releaseDate&pageSize=250";
        var response = await _http.GetStringAsync(url);
        var result = JsonSerializer.Deserialize<ApiResponse<ApiSet>>(response, _json);
        return result?.Data ?? [];
    }

    public async Task<List<ApiCard>> GetCardsAsync(string setId, IProgress<int>? progress = null)
    {
        var all = new List<ApiCard>();
        int page = 1;
        int pageSize = 250;

        while (true)
        {
            var url = $"cards?q=set.id:{setId}&page={page}&pageSize={pageSize}&orderBy=number";
            var response = await _http.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<ApiResponse<ApiCard>>(response, _json);
            if (result is null || result.Data.Count == 0) break;
            all.AddRange(result.Data);
            progress?.Report(all.Count);
            if (all.Count >= result.TotalCount) break;
            page++;
        }

        return all;
    }
}
