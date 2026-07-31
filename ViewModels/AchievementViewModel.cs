using Cardex.Models;
using Cardex.Services;
using System.Windows;
using System.Windows.Media.Imaging;

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

    public BitmapImage? IconImage { get; }
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
                var info = Application.GetResourceStream(
                    new Uri($"pack://application:,,,/Assets/AchievementIcons/{def.Icon}"));
                if (info is not null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = info.Stream;
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    IconImage = bmp;
                }
            }
            catch { }
        }
    }
}
