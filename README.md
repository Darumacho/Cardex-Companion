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
- Mark cards as **Excluded** to remove them from completion tracking
- Global **Check all / Uncheck all** toggle on the active set
- **My Collection** sidebar section grouping all sets with at least one owned card
- **⊕ Duplicate Cards** section on the home screen listing every card owned more than once
- **Per-set ↻ Refresh** button to re-sync a set from the API without clearing the database

### Views
- **List view** — card grid with full details (price, rarity, owned/wanted badges)
- **Binder view** — compact card binder showing all cards as thumbnails
  - Click a card to toggle owned
  - `Ctrl+Click` to toggle excluded — excluded cards show a **⊘** overlay
- **Card zoom** — click the 🔍 button on any card image to open a full HD view

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
- Choose a **data range**: Collection, Wants, Duplicates, or Missing
- Choose an **export format**:
  - **Normal** — full data: `Name, Set, Number, Rarity, Quantity, Cardmarket (€), TCGPlayer ($), URLs`
  - **Cardmarket** — wantlist import: `Name;Edition;Quantity`
  - **TCGPlayer** — wishlist import: `Quantity, Name, Set Name, Number, Condition, Language`
- UTF-8 encoded, Excel-compatible

### Settings
- Accessible via the ⚙ button — opens a dedicated window with two tabs:
  - **Options**: collection border color picker, Show My Collection toggle
  - **Shortcuts**: reference list of all keyboard shortcuts

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+F` | Focus the search bar (global or per-set depending on context) |
| `Escape` | Clear search / go back to home |
| `Ctrl+A` | Check all / Uncheck all cards in the active set |
| `Ctrl+B` | Backup collection |
| `Ctrl+R` | Restore collection |
| `Ctrl+E` | Export CSV |
| `Left click` *(binder)* | Toggle owned |
| `Ctrl+Left click` *(binder)* | Toggle excluded |

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
├── Converters/          # XAML value converters
├── Data/                # EF Core DbContext
├── Models/              # Database entities and API models
├── SeedData/            # Embedded seed data (sets + cards)
├── Services/
│   ├── PokemonTcgService    # pokemontcg.io API client
│   ├── ImageCacheService    # Local image cache (thumbnails + full-res)
│   ├── UpdateService        # Update check and installation
│   └── AppSettings          # Configuration loader
├── ViewModels/
│   ├── MainViewModel        # Root view model
│   ├── SetViewModel         # A set with its filters and cards
│   ├── CardViewModel        # A card (owned, wanted, excluded, prices, links)
│   ├── SeriesViewModel      # A sidebar series group
│   └── SearchResultViewModel
└── Views/
    ├── MainWindow.xaml      # Main application window
    ├── SettingsWindow.xaml  # Settings (options + shortcuts)
    ├── CardZoomWindow.xaml  # Full-res card viewer
    └── ExportDialog.xaml    # CSV export options
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
| `ExcludedCards` | Cards excluded from completion tracking |

---

## Development

```bash
# Run in development
dotnet run

# Publish as single-file exe
dotnet publish -c Release -o publish
# → publish/Cardex.exe
```
