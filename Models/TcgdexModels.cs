using System.Text.Json.Serialization;

namespace Cardex.Models;

// DTOs mirroring the TCGdex REST API (https://api.tcgdex.net/v2/en/), used as a fallback
// data source for sets pokemontcg.io doesn't have (see plan: TCGdex comme source de secours).

public record TcgdexSetBrief
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("logo")] public string? Logo { get; init; }
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
}

public record TcgdexSerie
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
}

public record TcgdexLegal
{
    [JsonPropertyName("standard")] public bool Standard { get; init; }
    [JsonPropertyName("expanded")] public bool Expanded { get; init; }
}

public record TcgdexSetCardBrief
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("localId")] public string LocalId { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("image")] public string? Image { get; init; }
}

// Full set detail (GET /sets/{id}) — includes the brief card list, but no per-card pricing/rarity.
public record TcgdexSetDetail
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("logo")] public string? Logo { get; init; }
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("releaseDate")] public string ReleaseDate { get; init; } = "";
    [JsonPropertyName("serie")] public TcgdexSerie Serie { get; init; } = new();
    [JsonPropertyName("legal")] public TcgdexLegal Legal { get; init; } = new();
    [JsonPropertyName("cards")] public List<TcgdexSetCardBrief> Cards { get; init; } = [];
}

public record TcgdexCardmarketPricing
{
    [JsonPropertyName("low")] public decimal? Low { get; init; }
}

public record TcgdexTcgplayerVariantPricing
{
    [JsonPropertyName("marketPrice")] public decimal? MarketPrice { get; init; }
    [JsonPropertyName("lowPrice")] public decimal? LowPrice { get; init; }
}

public record TcgdexTcgplayerPricing
{
    [JsonPropertyName("normal")] public TcgdexTcgplayerVariantPricing? Normal { get; init; }
    [JsonPropertyName("reverse-holofoil")] public TcgdexTcgplayerVariantPricing? ReverseHolofoil { get; init; }
    [JsonPropertyName("holofoil")] public TcgdexTcgplayerVariantPricing? Holofoil { get; init; }
}

public record TcgdexPricing
{
    [JsonPropertyName("cardmarket")] public TcgdexCardmarketPricing? Cardmarket { get; init; }
    [JsonPropertyName("tcgplayer")] public TcgdexTcgplayerPricing? Tcgplayer { get; init; }
}

// Full card detail (GET /cards/{id}) — the only endpoint carrying rarity/pricing.
public record TcgdexCard
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("localId")] public string LocalId { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("image")] public string? Image { get; init; }
    [JsonPropertyName("rarity")] public string? Rarity { get; init; }
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("types")] public List<string>? Types { get; init; }
    [JsonPropertyName("stage")] public string? Stage { get; init; }
    [JsonPropertyName("suffix")] public string? Suffix { get; init; }
    [JsonPropertyName("trainerType")] public string? TrainerType { get; init; }
    [JsonPropertyName("energyType")] public string? EnergyType { get; init; }
    [JsonPropertyName("legal")] public TcgdexLegal Legal { get; init; } = new();
    [JsonPropertyName("pricing")] public TcgdexPricing? Pricing { get; init; }
}
