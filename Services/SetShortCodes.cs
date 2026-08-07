namespace Cardex.Services;

public static class SetShortCodes
{
    public static readonly IReadOnlyDictionary<string, string> BySetId =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // WotC era
        { "base1",       "BS"   }, // Base
        { "base2",       "JGL"  }, // Jungle
        { "basep",       "WBP"  }, // Wizards Black Star Promos
        { "base3",       "FSL"  }, // Fossil
        { "base4",       "BS2"  }, // Base Set 2
        { "base5",       "TRK"  }, // Team Rocket
        { "gym1",        "GYH"  }, // Gym Heroes
        { "gym2",        "GYC"  }, // Gym Challenge
        { "neo1",        "NEOG" }, // Neo Genesis
        { "neo2",        "NEOD" }, // Neo Discovery
        { "si1",         "SI"   }, // Southern Islands
        { "neo3",        "NEOR" }, // Neo Revelation
        { "neo4",        "NEDT" }, // Neo Destiny
        { "base6",       "LC"   }, // Legendary Collection
        { "ecard1",      "EXP"  }, // Expedition Base Set
        { "bp",          "BOG"  }, // Best of Game
        { "ecard2",      "AQL"  }, // Aquapolis
        { "ecard3",      "SKYR" }, // Skyridge
        // EX era
        { "ex1",         "RS"   }, // Ruby & Sapphire
        { "ex2",         "SNDS" }, // Sandstorm
        { "np",          "NBP"  }, // Nintendo Black Star Promos
        { "ex3",         "DRG"  }, // Dragon
        { "ex4",         "TMA"  }, // Team Magma vs Team Aqua
        { "ex5",         "HL"   }, // Hidden Legends
        { "tk1b",        "TKLT" }, // EX Trainer Kit Latios
        { "tk1a",        "TKLA" }, // EX Trainer Kit Latias
        { "ex6",         "FRLG" }, // FireRed & LeafGreen
        { "pop1",        "P1"   }, // POP Series 1
        { "ex7",         "TRR"  }, // Team Rocket Returns
        { "ex8",         "DEOX" }, // Deoxys
        { "ex9",         "EMR"  }, // Emerald
        { "pop2",        "P2"   }, // POP Series 2
        { "ex10",        "UF"   }, // Unseen Forces
        { "ex11",        "DSP"  }, // Delta Species
        { "ex12",        "LGM"  }, // Legend Maker
        { "tk2a",        "TKPL" }, // EX Trainer Kit 2 Plusle
        { "tk2b",        "TKMN" }, // EX Trainer Kit 2 Minun
        { "pop3",        "P3"   }, // POP Series 3
        { "ex13",        "HPH"  }, // Holon Phantoms
        { "pop4",        "P4"   }, // POP Series 4
        { "ex14",        "CG"   }, // Crystal Guardians
        { "ex15",        "DRF"  }, // Dragon Frontiers
        { "ex16",        "PK"   }, // Power Keepers
        { "pop5",        "P5"   }, // POP Series 5
        // DP era
        { "dp1",         "DP"   }, // Diamond & Pearl
        { "dpp",         "DBP"  }, // DP Black Star Promos
        { "dp2",         "MT"   }, // Mysterious Treasures
        { "pop6",        "P6"   }, // POP Series 6
        { "dp3",         "SCW"  }, // Secret Wonders
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
        { "hgss2",       "HSU"  }, // HS Unleashed
        { "hgss3",       "HUD"  }, // HS Undaunted
        { "hgss4",       "HST"  }, // HS Triumphant
        { "col1",        "CL"   }, // Call of Legends
        // BW era
        { "bwp",         "BWP"  }, // BW Black Star Promos
        { "bw1",         "BW"   }, // Black & White
        { "mcd11",       "MC11" }, // McDonalds 2011
        { "bw2",         "EP"   }, // Emerging Powers
        { "bw3",         "NV"   }, // Noble Victories
        { "bw4",         "ND"   }, // Next Destinies
        { "bw5",         "DE"   }, // Dark Explorers
        { "mcd12",       "MC12" }, // McDonalds 2012
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
        { "mcd14",       "MC14" }, // McDonalds 2014
        { "xy3",         "FRF"  }, // Furious Fists
        { "xy4",         "PHF"  }, // Phantom Forces
        { "xy5",         "PC"   }, // Primal Clash
        { "dc1",         "DCR"  }, // Double Crisis
        { "xy6",         "ROS"  }, // Roaring Skies
        { "xy7",         "AO"   }, // Ancient Origins
        { "xy8",         "BRK"  }, // BREAKthrough
        { "mcd15",       "MC15" }, // McDonalds 2015
        { "xy9",         "BRP"  }, // BREAKpoint
        { "g1",          "GEN"  }, // Generations
        { "xy10",        "FC"   }, // Fates Collide
        { "xy11",        "STM"  }, // Steam Siege
        { "mcd16",       "MC16" }, // McDonalds 2016
        { "xy12",        "EVL"  }, // Evolutions
        // SM era
        { "smp",         "SBP"  }, // SM Black Star Promos
        { "sm1",         "SM"   }, // Sun & Moon
        { "sm2",         "GRI"  }, // Guardians Rising
        { "sm3",         "BSH"  }, // Burning Shadows
        { "sm35",        "SHL"  }, // Shining Legends
        { "sm4",         "CINV" }, // Crimson Invasion
        { "mcd17",       "MC17" }, // McDonalds 2017
        { "sm5",         "UPR"  }, // Ultra Prism
        { "sm6",         "FBL"  }, // Forbidden Light
        { "sm7",         "CST"  }, // Celestial Storm
        { "sm75",        "DRJ"  }, // Dragon Majesty
        { "mcd18",       "MC18" }, // McDonalds 2018
        { "sm8",         "LTH"  }, // Lost Thunder
        { "sm9",         "TUP"  }, // Team Up
        { "det1",        "DEP"  }, // Detective Pikachu
        { "sm10",        "UBN"  }, // Unbroken Bonds
        { "sm11",        "UMN"  }, // Unified Minds
        { "sm115",       "HFT"  }, // Hidden Fates
        { "sma",         "HFSV" }, // Hidden Fates Shiny Vault
        { "mcd19",       "MC19" }, // McDonalds 2019
        { "sm12",        "CE"   }, // Cosmic Eclipse
        // SWSH era
        { "swshp",       "SWP"  }, // SWSH Black Star Promos
        { "swsh1",       "SWSH" }, // Sword & Shield
        { "swsh2",       "RCL"  }, // Rebel Clash
        { "swsh3",       "DAB"  }, // Darkness Ablaze
        { "fut20",       "FCL"  }, // Futsal Collection
        { "swsh35",      "CHP"  }, // Champions Path
        { "swsh4",       "VV"   }, // Vivid Voltage
        { "mcd21",       "MC21" }, // McDonalds 2021
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
        { "mcd22",       "MC22" }, // McDonalds 2022
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
}
