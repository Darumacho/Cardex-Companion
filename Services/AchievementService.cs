using Cardex.Data;
using Cardex.Models;
using Microsoft.EntityFrameworkCore;

namespace Cardex.Services;

public record AchievementDef(string Id, string Emoji, string Name, string Description, bool IsSecret = false, string? CustomSound = null, string? Icon = null);

public static class AchievementService
{
    public static readonly IReadOnlyList<AchievementDef> All = new List<AchievementDef>
    {
        new("first_set",        "✅", "Complete!",                              "Complete your first set",                                                               Icon: "first_set.png"),
        new("triple_threat",    "🎯", "Triple Threat",                          "Complete 3 sets",                                                                       Icon: "triple_threat.png"),
        new("mcdonalds",        "🍟", "I'm lovin' it!",                         "Complete a McDonald's set",                                   IsSecret: true,            Icon: "mcdonalds.png"),
        new("pop5",             "😏", "Yeah, sure buddy",                       "Complete the POP Series 5 set",                              IsSecret: true,            Icon: "pop5.png"),
        new("genwunner",        "🕹️", "Genwunner",                             "Complete a set from the Base or Gym era",                                               Icon: "genwunner.png"),
        new("golden_gen",       "✨", "The golden gen",                         "Complete a set from the Neo or E-Card era",                                             Icon: "golden_gen.png"),
        new("too_much_water",   "🌊", "Too much water",                         "Complete a set from the EX era",                                                        Icon: "too_much_water.png"),
        new("gen4_win",         "💎", "Gen 4... the win!",                      "Complete a set from the Diamond & Pearl, Platinum or HeartGold & SoulSilver era",       Icon: "gen4_win.png"),
        new("big_set",          "📖", "Thank you Pokémon Company",              "Complete a set with more than 250 cards",                     IsSecret: true,            Icon: "big_set.png"),
        new("first_tag",        "🏷️", "Organized",                             "Create your first tag",                                                                 Icon: "first_tag.png"),
        new("duplicate_5",      "🔄", "I need some backup!",                    "Have 5+ copies of a single card",                                                       Icon: "duplicate_5.png"),
        new("duplicate_20",     "😱", "Alright, that's enough now",             "Have 20+ copies of a single card",                           IsSecret: true,            Icon: "duplicate_20.png"),
        new("collector_1000",   "📦", "I'm something of a collector myself",    "Own 1000 cards",                                                                        Icon: "collector_1000.png"),
        new("hoarder_2500",     "📚", "That's at least... more than 5 binders!", "Own 2500 cards",                                                                       Icon: "hoarder_2500.png"),
        new("backup_used",      "💾", "Just in case",                           "Use the Backup functionality",                                                          Icon: "backup_used.png"),
        new("refresh_set",      "🔃", "Great software, 10/10",                  "Use the set refresh button",                                                            Icon: "refresh_set.png"),
        new("excluded_10",      "🚫", "Don't worry, these don't count",         "Tag 10 cards as Excluded",                                                              Icon: "excluded_10.png"),
        new("wanted_10",        "🎄", "Christmas list",                         "Tag 10 cards as Wants",                                                                 Icon: "wanted_10.png"),
        new("rare_holo_star_5", "⭐", "Now that's some big money",              "Own 5 different Rare Holo Star cards",                        IsSecret: true,            Icon: "rare_holo_star_5.png"),
        new("darmanitan_5",     "🔥", "You have great tastes",                  "Own 5 different Darmanitan cards",                           IsSecret: true,            Icon: "darmanitan_5.png"),
        new("garchomp_8",       "🐉", "Look, he's ZOOMIN'",            "Own 8 different Garchomp cards",                             IsSecret: true,            Icon: "garchomp_8.png"),
        new("wimpod_6",         "🦀", "It's a Tactical Retreat, okay?",                  "Own 6 different Wimpod and Golisopod cards",                      IsSecret: true,            Icon: "wimpod_6.png"),
        new("konami_code",      "🎮", "Outstanding!",                           "Enter the Konami code",                                      IsSecret: true,            Icon: "konami_code.png", CustomSound: "Outstanding.mp3"),
        new("briggs",           "😬", "Anything but this",                      ";D",                                                         IsSecret: true,            Icon: "briggs.png",       CustomSound: "Briggs.mp3"),
        new("regis",            "🤖", "ÜN ÜN ÜN *angry computer noises*",       "Own at least one of each Regi",                              IsSecret: true,            Icon: "regis.png"),
        new("cynthia_wannabe",  "👑", "Cynthia Wannabe",                        "Own at least one card of each Pokémon from Cynthia's team",  IsSecret: true,            Icon: "cynthia_wannabe.png"),
        new("full_deck",        "🃏", "Ready to Battle",                        "Save a complete 60-card deck",                                                          Icon: "full_deck.png"),
        new("five_full_decks",  "🏆", "Deck Master",                            "Save 5 complete 60-card decks",                                                         Icon: "five_full_deck.png"),
    };

    public static event Action<AchievementDef>? Unlocked;

    public static async Task CheckAsync(string id, AppDbContext db)
    {
        if (await db.UnlockedAchievements.AnyAsync(a => a.Id == id)) return;
        var def = All.FirstOrDefault(a => a.Id == id);
        if (def is null) return;
        db.UnlockedAchievements.Add(new UnlockedAchievement { Id = id, UnlockedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Unlocked?.Invoke(def);
    }
}
