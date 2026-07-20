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

    public Task<List<ApiSet>> GetSetsAsync() =>
        RetryAsync(() => FetchSetsAsync());

    public Task<List<ApiCard>> GetCardsAsync(string setId, IProgress<int>? progress = null) =>
        RetryAsync(() => FetchCardsAsync(setId, progress));

    private async Task<List<ApiSet>> FetchSetsAsync()
    {
        var response = await _http.GetStringAsync("sets?orderBy=releaseDate&pageSize=250");
        var result = JsonSerializer.Deserialize<ApiResponse<ApiSet>>(response, _json);
        return result?.Data ?? [];
    }

    private async Task<List<ApiCard>> FetchCardsAsync(string setId, IProgress<int>? progress)
    {
        var all = new List<ApiCard>();
        int page = 1;

        while (true)
        {
            var url = $"cards?q=set.id:{setId}&page={page}&pageSize=250&orderBy=number";
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
