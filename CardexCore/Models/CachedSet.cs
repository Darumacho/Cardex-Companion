using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class CachedSet
{
    [Key] public string SetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Series { get; set; } = "";
    public int Total { get; set; }
    public string ReleaseDate { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public string SymbolUrl { get; set; } = "";
    public DateTime CachedAt { get; set; }
}
