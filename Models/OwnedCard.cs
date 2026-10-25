using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class OwnedCard
{
    [Key]
    public string CardId { get; set; } = "";
    public string SetId { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public bool IsHolo { get; set; } = false;
    public DateTime OwnedAt { get; set; } = DateTime.UtcNow;
}
