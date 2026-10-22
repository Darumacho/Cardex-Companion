namespace Cardex.Services;

public static class SetShortCodes
{
    public static readonly IReadOnlyDictionary<string, string> BySetId =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // WotC era
        { "base1",       "BS"   }, // Base
        { "base2",       "JU"  }, // Jungle
        { "basep",       "WBP"  }, // Wizards Black Star Promos
        { "base3",       "FO"  }, // Fossil
        { "base4",       "BS2"  }, // Base Set 2
        { "base5",       "TR"  }, // Team Rocket
        { "gym1",        "GH"  }, // Gym Heroes
        { "gym2",        "GC"   }, // Gym Challenge
        { "neo1",        "NG" }, // Neo Genesis
        { "neo2",        "NDI" }, // Neo Discovery
        { "si1",         "SI"   }, // Southern Islands
        { "neo3",        "NR" }, // Neo Revelation
        { "neo4",        "NDE" }, // Neo Destiny
        { "base6",       "LC"   }, // Legendary Collection
        { "ecard1",      "EXP"  }, // Expedition Base Set
        { "bp",          "BOG"  }, // Best of Game
        { "ecard2",      "AQ"  }, // Aquapolis
        { "ecard3",      "SK" }, // Skyridge
        // EX era
        { "ex1",         "RS"   }, // Ruby & Sapphire
        { "ex2",         "SS" }, // Sandstorm
        { "np",          "NBP"  }, // Nintendo Black Star Promos
        { "ex3",         "DR"  }, // Dragon
        { "ex4",         "MA"  }, // Team Magma vs Team Aqua
        { "ex5",         "HL"   }, // Hidden Legends
        { "ex5.5",       "PCCP" }, // Poké Card Creator Pack
        { "tk1b",        "TKLT" }, // EX Trainer Kit Latios
        { "tk1a",        "TKLA" }, // EX Trainer Kit Latias
        { "ex6",         "FRLG" }, // FireRed & LeafGreen
        { "pop1",        "P1"   }, // POP Series 1
        { "ex7",         "TRR"  }, // Team Rocket Returns
        { "ex8",         "DX" }, // Deoxys
        { "ex9",         "EM"  }, // Emerald
        { "pop2",        "P2"   }, // POP Series 2
        { "ex10",        "UF"   }, // Unseen Forces
        { "ex11",        "DS"  }, // Delta Species
        { "ex12",        "LM"  }, // Legend Maker
        { "tk2a",        "TKPL" }, // EX Trainer Kit 2 Plusle
        { "tk2b",        "TKMN" }, // EX Trainer Kit 2 Minun
        { "pop3",        "P3"   }, // POP Series 3
        { "ex13",        "HP"  }, // Holon Phantoms
        { "pop4",        "P4"   }, // POP Series 4
        { "ex14",        "CG"   }, // Crystal Guardians
        { "ex15",        "DF"  }, // Dragon Frontiers
        { "ex16",        "PK"   }, // Power Keepers
        { "pop5",        "P5"   }, // POP Series 5
        // DP era
        { "dp1",         "DP"   }, // Diamond & Pearl
        { "dpp",         "DBP"  }, // DP Black Star Promos
        { "dp2",         "MT"   }, // Mysterious Treasures
        { "pop6",        "P6"   }, // POP Series 6
        { "dp3",         "SW"  }, // Secret Wonders
        { "dp4",         "GE"   }, // Great Encounters
        { "pop7",        "P7"   }, // POP Series 7
        { "dp5",         "MD"   }, // Majestic Dawn
        { "dp6",         "LA"   }, // Legends Awakened
        { "pop8",        "P8"   }, // POP Series 8
        { "dp7",         "STF"  }, // Stormfront
        { "pl1",         "PLT"  }, // Platinum
        { "pop9",        "P9"   }, // POP Series 9
        { "pl2",         "RR"   }, // Rising Rivals
        { "pl3",         "SVC"  }, // Supreme Victors
        { "pl4",         "ARC"  }, // Arceus
        // HGSS era
        { "ru1",         "RMB"  }, // Pokemon Rumble
        { "hgss1",       "HGSS" }, // HeartGold & SoulSilver
        { "hsp",         "HSP"  }, // HGSS Black Star Promos
        { "hgss2",       "UL"  }, // HS Unleashed
        { "hgss3",       "UD"  }, // HS Undaunted
        { "hgss4",       "TM"  }, // HS Triumphant
        { "col1",        "CL"   }, // Call of Legends
        // BW era
        { "bwp",         "BWP"  }, // BW Black Star Promos
        { "bw1",         "BW"   }, // Black & White
        { "mcd11",       "MCD11" }, // McDonalds 2011
        { "bw2",         "EP"   }, // Emerging Powers
        { "bw3",         "NV"   }, // Noble Victories
        { "bw4",         "ND"   }, // Next Destinies
        { "bw5",         "DE"   }, // Dark Explorers
        { "mcd12",       "MCD12" }, // McDonalds 2012
        { "bw6",         "DRX"  }, // Dragons Exalted
        { "dv1",         "DRV"  }, // Dragon Vault
        { "bw7",         "BC"   }, // Boundaries Crossed
        { "bw8",         "PST"  }, // Plasma Storm
        { "bw9",         "PFZ"  }, // Plasma Freeze
        { "bw10",        "PLB"  }, // Plasma Blast
        { "xyp",         "XBP"  }, // XY Black Star Promos
        { "bw11",        "LT"   }, // Legendary Treasures
        // XY era
        { "xy0",         "KSS"  }, // Kalos Starter Set
        { "xy1",         "XY"   }, // XY
        { "xy2",         "FF"   }, // Flashfire
        { "mcd14",       "MCD14" }, // McDonalds 2014
        { "xy3",         "FRF"  }, // Furious Fists
        { "xy4",         "PHF"  }, // Phantom Forces
        { "xy5",         "PC"   }, // Primal Clash
        { "dc1",         "DCR"  }, // Double Crisis
        { "xy6",         "ROS"  }, // Roaring Skies
        { "xy7",         "AO"   }, // Ancient Origins
        { "xy8",         "BRK"  }, // BREAKthrough
        { "mcd15",       "MCD15" }, // McDonalds 2015
        { "xy9",         "BRP"  }, // BREAKpoint
        { "g1",          "GEN"  }, // Generations
        { "xy10",        "FC"   }, // Fates Collide
        { "xy11",        "STM"  }, // Steam Siege
        { "mcd16",       "MCD16" }, // McDonalds 2016
        { "xy12",        "EVL"  }, // Evolutions
        // SM era
        { "smp",         "SBP"  }, // SM Black Star Promos
        { "sm1",         "SM"   }, // Sun & Moon
        { "sm2",         "GRI"  }, // Guardians Rising
        { "sm3",         "BSH"  }, // Burning Shadows
        { "sm35",        "SHL"  }, // Shining Legends
        { "sm4",         "CINV" }, // Crimson Invasion
        { "mcd17",       "MCD17" }, // McDonalds 2017
        { "sm5",         "UPR"  }, // Ultra Prism
        { "sm6",         "FBL"  }, // Forbidden Light
        { "sm7",         "CST"  }, // Celestial Storm
        { "sm75",        "DRJ"  }, // Dragon Majesty
        { "mcd18",       "MCD18" }, // McDonalds 2018
        { "sm8",         "LTH"  }, // Lost Thunder
        { "sm9",         "TUP"  }, // Team Up
        { "det1",        "DEP"  }, // Detective Pikachu
        { "sm10",        "UBN"  }, // Unbroken Bonds
        { "sm11",        "UMN"  }, // Unified Minds
        { "sm115",       "HFT"  }, // Hidden Fates
        { "sma",         "HFSV" }, // Hidden Fates Shiny Vault
        { "mcd19",       "MCD19" }, // McDonalds 2019
        { "sm12",        "CE"   }, // Cosmic Eclipse
        // SWSH era
        { "swshp",       "SWP"  }, // SWSH Black Star Promos
        { "swsh1",       "SWSH" }, // Sword & Shield
        { "swsh2",       "RCL"  }, // Rebel Clash
        { "swsh3",       "DAB"  }, // Darkness Ablaze
        { "fut20",       "FCL"  }, // Futsal Collection
        { "swsh35",      "CHP"  }, // Champions Path
        { "swsh4",       "VV"   }, // Vivid Voltage
        { "mcd21",       "MCD21" }, // McDonalds 2021
        { "swsh45sv",    "SFSV" }, // Shining Fates Shiny Vault
        { "swsh45",      "SHF"  }, // Shining Fates
        { "swsh5",       "BST"  }, // Battle Styles
        { "swsh6",       "CHR"  }, // Chilling Reign
        { "swsh7",       "EVS"  }, // Evolving Skies
        { "cel25c",      "CLSC" }, // Celebrations Classic Collection
        { "cel25",       "CEL"  }, // Celebrations
        { "swsh8",       "FST"  }, // Fusion Strike
        { "swsh9",       "BRS"  }, // Brilliant Stars
        { "swsh9tg",     "BRTG" }, // Brilliant Stars Trainer Gallery
        { "swsh10",      "ASR"  }, // Astral Radiance
        { "swsh10tg",    "ARTG" }, // Astral Radiance Trainer Gallery
        { "pgo",         "PGO"  }, // Pokemon GO
        { "mcd22",       "MCD22" }, // McDonalds 2022
        { "swsh11",      "LOR"  }, // Lost Origin
        { "swsh11tg",    "LOTG" }, // Lost Origin Trainer Gallery
        { "swsh12",      "SVT"  }, // Silver Tempest
        { "swsh12tg",    "SVTG" }, // Silver Tempest Trainer Gallery
        { "svp",         "SVP"  }, // Scarlet & Violet Black Star Promos
        { "swsh12pt5",   "CZ"   }, // Crown Zenith
        { "swsh12pt5gg", "CZGG" }, // Crown Zenith Galarian Gallery
        // SV era
        { "sv1",         "SV1"   }, // Scarlet & Violet
        { "sve",         "SVE"  }, // Scarlet & Violet Energies
        { "sv2",         "PAL"  }, // Paldea Evolved
        { "sv3",         "OBF"  }, // Obsidian Flames
        { "sv3pt5",      "MEW"  }, // 151
        { "sv4",         "PAR"  }, // Paradox Rift
        { "sv4pt5",      "PAF"  }, // Paldean Fates
        { "sv5",         "TEF"  }, // Temporal Forces
        { "sv6",         "TWM"  }, // Twilight Masquerade
        { "sv6pt5",      "SFA"  }, // Shrouded Fable
        { "sv7",         "SCR"  }, // Stellar Crown
        { "sv8",         "SSP"  }, // Surging Sparks
        { "sv8pt5",      "PRE"  }, // Prismatic Evolutions
        { "sv9",         "JTG"  }, // Journey Together
        { "sv10",        "DRI"  }, // Destined Rivals
        { "zsv10pt5",    "BLK"  }, // Black Bolt
        { "rsv10pt5",    "WHT"  }, // White Flare
        // ME series
        { "me1",         "MEG"  }, // Mega Evolution
        { "me2",         "PFL"  }, // Phantasmal Flames
        { "me2pt5",      "ASC"  }, // Ascended Heroes
        { "me3",         "POR"  }, // Perfect Order
        { "me4",         "CRI"  }, // Chaos Rising
        { "me5",         "PBL"  }, // Pitch Black
    };

    // Cardmarket (uniquement — pas TCGPlayer) veut le code court plutôt que le SetId brut, pour
    // tous les blocs sauf les Trainer Kit (règles dédiées) et quelques exceptions ponctuelles
    // (dpp/hsp via CardmarketCustomOverrides, bwp/xyp laissés au SetId brut — voir plus bas).
    // Vérifié empiriquement par l'utilisateur, bloc par bloc : "PFL" marche pour Phantasmal Flames
    // (me2), "ME2" non.
    public static readonly IReadOnlySet<string> CardmarketUsesShortCodeFrom =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "xy0", "xy1", "xy2", "mcd14", "xy3", "xy4", "xy5", "dc1", "xy6", "xy7", "xy8", "mcd15",
        "xy9", "g1", "xy10", "xy11", "mcd16", "xy12",
        "smp", "sm1", "sm2", "sm3", "sm35", "sm4", "mcd17", "sm5", "sm6", "sm7", "sm75", "mcd18",
        "sm8", "sm9", "det1", "sm10", "sm11", "sm115", "sma", "mcd19", "sm12",
        "swshp", "swsh1", "swsh2", "swsh3", "fut20", "swsh35", "swsh4", "mcd21", "swsh45sv",
        "swsh45", "swsh5", "swsh6", "swsh7", "cel25c", "cel25", "swsh8", "swsh9", "swsh9tg",
        "swsh10", "swsh10tg", "pgo", "mcd22", "swsh11", "swsh11tg", "swsh12", "swsh12tg", "svp",
        "swsh12pt5", "swsh12pt5gg",
        "sv1", "sve", "sv2", "sv3", "sv3pt5", "sv4", "sv4pt5", "sv5", "sv6", "sv6pt5", "sv7",
        "sv8", "sv8pt5", "sv9", "sv10", "zsv10pt5", "rsv10pt5",
        "me1", "me2", "me2pt5", "me3", "me4", "me5",
        "ex5.5", // Poké Card Creator Pack — exception pré-XY, "PCCP" marche, "EX5.5" non.

        // Blocs Base/Gym/Neo/E-Card, EX, Diamond & Pearl/Platinum, HGSS, Black & White — même
        // règle étendue à la demande de l'utilisateur. Exclus volontairement : dpp/hsp (override
        // dédié ci-dessous), bwp (confirmé correct tel quel avec le SetId brut), xyp (conflit non
        // résolu — son ShortCode "XBP" diffère du "XYP" confirmé fonctionner), et les sets
        // Trainer Kit (règles dédiées plus bas).
        "base1", "base2", "basep", "base3", "base4", "base5", "gym1", "gym2", "neo1", "neo2",
        "si1", "neo3", "neo4", "base6", "ecard1", "bp", "ecard2", "ecard3",
        "ex1", "ex2", "np", "ex3", "ex4", "ex5", "ex6", "pop1", "ex7", "ex8", "ex9", "pop2",
        "ex10", "ex11", "ex12", "pop3", "ex13", "pop4", "ex14", "ex15", "ex16", "pop5",
        "dp1", "dp2", "pop6", "dp3", "dp4", "pop7", "dp5", "dp6", "pop8", "dp7", "pl1", "pop9",
        "pl2", "pl3", "pl4",
        "ru1", "hgss1", "hgss2", "hgss3", "hgss4", "col1",
        "bw1", "mcd11", "bw2", "bw3", "bw4", "bw5", "mcd12", "bw6", "dv1", "bw7", "bw8", "bw9",
        "bw10", "bw11",
    };

    // Exceptions ponctuelles Cardmarket où ni le SetId brut, ni le ShortCode via
    // CardmarketUsesShortCodeFrom, ni les règles Trainer Kit ne suffisent — vérifié empiriquement
    // carte par carte par l'utilisateur. Code=null veut dire "pas de code du tout dans la requête,
    // juste le nom et le numéro" (ex. HGSS Black Star Promos). StripNumberPrefix retire un préfixe
    // du NUMÉRO avant de l'insérer (ex. "DP01" -> "01" pour DP Black Star Promos).
    public static readonly IReadOnlyDictionary<string, (string? Code, string? StripNumberPrefix)> CardmarketCustomOverrides =
        new Dictionary<string, (string?, string?)>(StringComparer.OrdinalIgnoreCase)
    {
        { "dpp", ("DPPR", "DP") }, // DP Black Star Promos : "DPPR 1" marche, "DPP DP1" non.
        { "hsp", (null,   null) }, // HGSS Black Star Promos : "HGSS1" seul marche, "HSP HGSS1" non.
    };

    // Les 4 sets "Trainer Kit" demi-deck (EX Latias/Latios, EX Plusle/Minun) partagent un même
    // code Cardmarket/TCGPlayer ("TK1"/"TK2") avec un suffixe a/b porté par le NUMÉRO de la carte,
    // pas par le set — contrairement à BySetId ci-dessus (codes distincts TKLA/TKLT/TKPL/TKMN,
    // nécessaires pour rester uniques dans l'import de deck). Vérifié empiriquement : "Bagon TK1A 1"
    // ne donne aucun résultat sur Cardmarket/TCGPlayer, "Bagon TK1 1A" si.
    public static readonly IReadOnlyDictionary<string, (string Code, string Suffix)> MarketplaceOverrides =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        { "tk1a", ("TK1", "a") }, // EX Trainer Kit Latias
        { "tk1b", ("TK1", "b") }, // EX Trainer Kit Latios
        { "tk2a", ("TK2", "a") }, // EX Trainer Kit 2 Plusle
        { "tk2b", ("TK2", "b") }, // EX Trainer Kit 2 Minun
    };

    // DP Trainer Kit : suffixe = initiale du Pokémon AVANT le numéro (ex. "L1"), pas de padding.
    // La plupart des autres (BW/XY) font l'inverse : NUMÉRO puis lettre (ex. "1S"). Exceptions
    // confirmées empiriquement par l'utilisateur au fil de plusieurs essais ("Cardmarket ne fait
    // rien de manière constante") : TK9 (Pikachu Libre/Suicune) repasse en lettre-avant-numéro
    // comme DP ; SM (TK10) est lettre-avant-numéro mais avec le numéro complété à 2 chiffres
    // seulement s'il est < 10 ("L01", "L10").
    // Les deux moitiés d'un même coffret partagent toujours le même numéro TK.
    public static readonly IReadOnlyDictionary<string, (string Code, string PokemonLetter, TrainerKitLetterPosition Position)> TrainerKitLetterOverrides =
        new Dictionary<string, (string, string, TrainerKitLetterPosition)>(StringComparer.OrdinalIgnoreCase)
    {
        { "tk-dp-l",     ("TK3",  "L", TrainerKitLetterPosition.BeforeNumber) },       // DP Trainer Kit Lucario
        { "tk-dp-m",     ("TK3",  "M", TrainerKitLetterPosition.BeforeNumber) },       // DP Trainer Kit Manaphy
        { "tk-bw-z",     ("TK5",  "Z", TrainerKitLetterPosition.AfterNumber) },        // BW Trainer Kit Zoroark
        { "tk-bw-e",     ("TK5",  "E", TrainerKitLetterPosition.AfterNumber) },        // BW Trainer Kit Excadrill
        { "tk-xy-n",     ("TK6",  "N", TrainerKitLetterPosition.AfterNumber) },        // XY Trainer Kit Noivern
        { "tk-xy-sy",    ("TK6",  "S", TrainerKitLetterPosition.AfterNumber) },        // XY Trainer Kit Sylveon
        { "tk-xy-b",     ("TK7",  "B", TrainerKitLetterPosition.AfterNumber) },        // XY Trainer Kit Bisharp
        { "tk-xy-w",     ("TK7",  "W", TrainerKitLetterPosition.AfterNumber) },        // XY Trainer Kit Wigglytuff
        { "tk-xy-latia", ("TK8",  "a", TrainerKitLetterPosition.AfterNumber) },        // XY Trainer Kit Latias
        { "tk-xy-latio", ("TK8",  "o", TrainerKitLetterPosition.AfterNumber) },        // XY Trainer Kit Latios
        { "tk-xy-p",     ("TK9",  "P", TrainerKitLetterPosition.BeforeNumber) },       // XY Trainer Kit Pikachu Libre
        { "tk-xy-su",    ("TK9",  "S", TrainerKitLetterPosition.BeforeNumber) },       // XY Trainer Kit Suicune
        { "tk-sm-l",     ("TK10", "L", TrainerKitLetterPosition.BeforeNumberPadded) }, // SM Trainer Kit Lycanroc
        { "tk-sm-r",     ("TK10", "A", TrainerKitLetterPosition.BeforeNumberPadded) }, // SM Trainer Kit Alolan Raichu
    };

    // Construit le "numéro+lettre" (ou "lettre+numéro") à insérer dans la requête marketplace,
    // selon la position confirmée empiriquement par set — voir TrainerKitLetterOverrides.
    public static string FormatTrainerKitNumber(string number, string letter, TrainerKitLetterPosition position) => position switch
    {
        TrainerKitLetterPosition.BeforeNumber => $"{letter}{number}",
        TrainerKitLetterPosition.BeforeNumberPadded => $"{letter}{(int.TryParse(number, out var n) && n < 10 ? n.ToString("D2") : number)}",
        _ => $"{number}{letter}",
    };
}

public enum TrainerKitLetterPosition { BeforeNumber, BeforeNumberPadded, AfterNumber }
