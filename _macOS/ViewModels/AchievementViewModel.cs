using Cardex.Models;
using Cardex.Services;
using Avalonia.Platform;
using Avalonia.Media.Imaging;

namespace Cardex.ViewModels;

public class AchievementViewModel
{
    public AchievementDef Def { get; }
    public bool IsUnlocked { get; }
    public DateTime? UnlockedAt { get; }

    public string Id                 => Def.Id;
    public string Emoji              => Def.Emoji;
    public string Name               => Def.Name;
    public string Description        => Def.Description;
    public string DisplayDescription => !IsUnlocked && Def.IsSecret ? "???" : Def.Description;

    public Bitmap? IconImage { get; }
    public bool HasIcon => IconImage is not null;

    public string UnlockedAtText => UnlockedAt.HasValue
        ? UnlockedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        : "";

    public AchievementViewModel(AchievementDef def, UnlockedAchievement? unlocked)
    {
        Def        = def;
        IsUnlocked = unlocked is not null;
        UnlockedAt = unlocked?.UnlockedAt;

        if (def.Icon is not null)
        {
            try
            {
                using var stream = AssetLoader.Open(
                    new Uri($"avares://Cardex/Assets/AchievementIcons/{def.Icon}"));
                IconImage = Bitmap.DecodeToWidth(stream, 32);
            }
            catch { }
        }
    }
}
