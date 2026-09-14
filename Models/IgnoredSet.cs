using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class IgnoredSet
{
    [Key] public string SetId { get; set; } = "";
}
