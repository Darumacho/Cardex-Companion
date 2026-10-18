namespace Cardex.Models;

// Détail par langue des exemplaires possédés d'une carte, utilisé uniquement quand plus d'une
// langue est suivie (voir AppSettings.TrackedLanguages). OwnedCard.Quantity reste la source de
// vérité agrégée (somme de toutes les langues) pour tout le reste de l'app (complétion, CSV,
// backup, sync, achievements) — cette table est purement additive.
public class OwnedCardLanguage
{
    public string CardId { get; set; } = "";
    public string SetId { get; set; } = "";
    public string Language { get; set; } = "EN";
    public int Quantity { get; set; } = 1;
}
