using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class FavoriteSet
{
    [Key] public string SetId { get; set; } = "";
}
