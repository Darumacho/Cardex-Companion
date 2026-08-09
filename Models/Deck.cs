using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class Deck
{
    [Key] public int Id { get; set; }
    public string Name { get; set; } = "New Deck";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
