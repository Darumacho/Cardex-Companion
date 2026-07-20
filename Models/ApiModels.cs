using System.Text.Json.Serialization;

namespace Cardex.Models;

public record ApiResponse<T>
{
    [JsonPropertyName("data")] public List<T> Data { get; init; } = [];
    [JsonPropertyName("page")] public int Page { get; init; }
    [JsonPropertyName("pageSize")] public int PageSize { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("totalCount")] public int TotalCount { get; init; }
}

public record ApiSet
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("series")] public string Series { get; init; } = "";
    [JsonPropertyName("total")] public int Total { get; init; }
    [JsonPropertyName("releaseDate")] public string ReleaseDate { get; init; } = "";
    [JsonPropertyName("images")] public ApiSetImages Images { get; init; } = new();
}

public record ApiSetImages
{
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";
    [JsonPropertyName("logo")] public string Logo { get; init; } = "";
}

public record ApiCard
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("number")] public string Number { get; init; } = "";
    [JsonPropertyName("set")] public ApiCardSet Set { get; init; } = new();
    [JsonPropertyName("images")] public ApiCardImages Images { get; init; } = new();
    [JsonPropertyName("rarity")] public string? Rarity { get; init; }
}

public record ApiCardSet
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
}

public record ApiCardImages
{
    [JsonPropertyName("small")] public string Small { get; init; } = "";
    [JsonPropertyName("large")] public string Large { get; init; } = "";
}
