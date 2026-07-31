using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class CardTag
{
    [Key]
    public string CardId { get; set; } = "";
    public int    TagId  { get; set; }
}
