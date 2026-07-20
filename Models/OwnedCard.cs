using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class OwnedCard
{
    [Key]
    public string CardId { get; set; } = "";
    public string SetId { get; set; } = "";
    public DateTime OwnedAt { get; set; } = DateTime.UtcNow;
}
