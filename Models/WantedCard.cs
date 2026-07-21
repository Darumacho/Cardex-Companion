using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class WantedCard
{
    [Key] public string CardId { get; set; } = "";
    public string SetId { get; set; } = "";
}
