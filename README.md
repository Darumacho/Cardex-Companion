# Cardex — Pokémon TCG Collection Companion

> A Windows desktop app to manage your Pokémon TCG card collection.

---

## Features

### Catalogue & Navigation
- **Full Pokémon TCG catalogue** loaded automatically from [pokemontcg.io](https://pokemontcg.io)
- Sidebar organized into three sections: **★ Favorites**, **My Collection**, **All Sets**
- **▾ ALL SETS** toggle to collapse/expand all series at once
- Set symbol and logo displayed in the sidebar and as a set header
- **✓ Completed!** badge when a set is 100% owned

### Collection Management
- Mark cards as **Owned** and track **quantity** per card (−/+)
- Global **Mark all as owned / unowned** toggle on the active set
- **My Collection** sidebar section grouping all sets with at least one owned card
- **⊕ Duplicate Cards** section on the home screen listing every card owned more than once

### Want List
- Star ★ any card to add it to your **Want List**
- **★ Wanted Cards** section on the home screen with visual card previews

### Search & Filters
- **Global search** by name or number across the entire catalogue
- Per-set filters: **rarity** dropdown, **name** search
- **Show owned / Show missing / Show wants / Show dupes** filter buttons in a 2×2 grid

### Market Prices
- **Cardmarket** (€) and **TCGPlayer** ($) low prices displayed on each card tile
- Clickable icons → open the card's listing page directly in your browser
- Hover tooltip: `Cardmarket : €1.89` / `TCGPlayer : $12.99`
- 24-hour cache: prices are refreshed automatically in the background

### Favorite Sets
- Star ★ a set in the sidebar to add it to your favorites
- **★ Favorite Sets** section on the home screen with visual set tiles

### Backup & Restore
- **💾 Backup** exports your collection to a portable `.cardex` file (JSON)
- **📂 Restore** reimports a backup, replacing the current collection with a confirmation prompt
- Only user data is saved: owned cards (with quantities), want list, and favorite sets
- Cache data (sets, cards, prices) is excluded — it is re-downloaded automatically

### CSV Export
- Export your full collection via **⬇ CSV Export** in the status bar
- Columns: `Name`, `Set`, `Number`, `Rarity`, `Quantity`, `Cardmarket (€)`, `TCGPlayer ($)`, `Cardmarket URL`, `TCGPlayer URL`
- UTF-8 encoded, Excel-compatible

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+F` | Focus the global search bar |
| `Escape` | Clear search / go back to home |
| `Ctrl+B` | Backup collection |
| `Ctrl+R` | Restore collection |
| `Ctrl+E` | Export CSV |

### Auto-Updater
- Checks for new releases on GitHub at startup
- Notification banner with an **⬇ Install** button → downloads and restarts automatically

---

## Screenshots

> *(coming soon)*

---

## Installation

1. Download `Cardex.exe` from the [Releases](https://github.com/Darumacho/Cardex-Companion/releases) page
2. Run the executable — no installation required (single-file, self-contained)
3. On first launch, sets and cards are indexed automatically

> **Requirements:** Windows 10/11 x64

---

## Configuration

A `settings.json` file is created automatically in `%AppData%\Cardex\` on first launch.

```json
{
  "ApiKey": "your-pokemontcg-io-api-key"
}
```

A free API key from [pokemontcg.io](https://pokemontcg.io) raises rate limits. The app works without one in limited mode.

---

## Technical Overview

| Layer | Technology |
|---|---|
| UI | WPF .NET 9, XAML |
| Pattern | MVVM — CommunityToolkit.Mvvm 8.3 |
| Database | SQLite via Entity Framework Core 8 |
| API | [pokemontcg.io](https://pokemontcg.io) |
| Distribution | Single-file exe, self-contained (win-x64) |

### Project Structure

```
Cardex/
├── Assets/              # Cardmarket and TCGPlayer icons
├── Data/                # EF Core DbContext
├── Models/              # Database entities and API models
├── SeedData/            # Embedded seed data (sets + cards)
├── Services/
│   ├── PokemonTcgService    # pokemontcg.io API client
│   ├── ImageCacheService    # Local image cache
│   ├── UpdateService        # Update check and installation
│   └── AppSettings          # Configuration loader
├── ViewModels/
│   ├── MainViewModel        # Root view model
│   ├── SetViewModel         # A set with its filters and cards
│   ├── CardViewModel        # A card (owned, wanted, prices, links)
│   ├── SeriesViewModel      # A sidebar series group
│   └── SearchResultViewModel
└── Views/
    └── MainWindow.xaml
```

---

## Local Data

The SQLite database is stored in `%AppData%\Cardex\cardex.db` and contains:

| Table | Content |
|---|---|
| `CachedSets` | Metadata for all sets |
| `CachedCards` | Cards with market prices and URLs |
| `OwnedCards` | Personal collection with quantities |
| `WantedCards` | Want list |
| `FavoriteSets` | Starred sets |

---

## Development

```bash
# Run in development
dotnet run

# Publish as single-file exe
dotnet publish -c Release
# → bin/Release/net9.0-windows/win-x64/publish/Cardex.exe
```
