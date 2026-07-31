using System.ComponentModel.DataAnnotations;

namespace Cardex.Models;

public class UnlockedAchievement
{
    [Key]
    public string Id { get; set; } = "";
    public DateTime UnlockedAt { get; set; }
}
