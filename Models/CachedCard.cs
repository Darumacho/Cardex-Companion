using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class CachedCard
{
    [Key] public string CardId { get; set; } = "";
    public string SetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public string ImageSmall { get; set; } = "";
    public string? Rarity { get; set; }
    public int SortOrder { get; set; }
    public decimal? CmLow { get; set; }
    public decimal? TcgLow { get; set; }
    public DateTime? PricesUpdatedAt { get; set; }
    public string? CmUrl { get; set; }
    public string? TcgUrl { get; set; }
}
