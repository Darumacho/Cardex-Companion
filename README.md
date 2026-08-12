# Cardex — Pokémon TCG Collection Companion

> A Windows desktop app to manage your Pokémon TCG card collection and build tournament decks.

---

## Features

### Catalogue & Navigation
- **Full Pokémon TCG catalogue** loaded automatically from [pokemontcg.io](https://pokemontcg.io)
- Sidebar organized into sections: **★ Favorites**, **My Collection**, **All Sets**
- **▾ ALL SETS** toggle to collapse/expand all series at once
- Set symbol and logo displayed in the sidebar and as a set header
- **✓ Completed!** badge when a set is 100% owned
- Home screen sections (Favorites, My Collection, Duplicates, Wanted Cards) remember their
  expanded/collapsed state between launches

### Collection Management
- Mark cards as **Owned** and track **quantity** per card (−/+)
- Mark cards as **Excluded** to remove them from completion tracking
- Global **Check all / Uncheck all** toggle on the active set
- **My Collection** sidebar section grouping all sets with at least one owned card
- **⊕ Duplicate Cards** section on the home screen listing every card owned more than once
- **Per-set ↻ Refresh** button to re-sync a set from the API without clearing the database
- **⬆ Import** cards from a text file (`CODE-NUMBER` / `CODE-NUMBER x3` format, see Settings → Templates)

### Custom Tags
- Create colored tags (My Tags settings tab) and assign one per card via a dropdown on each card tile
- Useful for personal organization (e.g. "For trade", "PSA submission", "Binder A")

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

### Deck Builder
A dedicated tab (🃏 button in the header) separate from the Collection, laid out in three panels:

- **Current deck** (left) — name, save/new, live Pokémon/Trainer/Energy/60 counters shown as a
  segmented composition bar, quantity controls per card, price estimate (Cardmarket + TCGPlayer),
  export to `.txt`
- **Card search** (center) — filter by supertype, subtype, set, custom tag, owned-only; results
  shown as a card grid (click to zoom, "+ Deck" to add)
- **Saved decks** (right) — inline rename, duplicate, delete; Pk/Tr/En chip counts per deck
- **Deck-building rule enforced**: no more than 4 copies of a same-named Pokémon across all
  printings/sets (e.g. 2 Darmanitan from set A + 2 from set B is the max — a 5th is blocked with
  an on-screen warning)
- **Format legality checker** — ✓/✕ **Standard** and **Expanded** badges update live as the deck
  changes, based on each card's set legality (Basic Energy is always legal regardless of print);
  hover a badge to see which cards aren't legal in that format
- **Import a decklist**:
  - **⬆ File** — open a `.txt` file in PTCGO/Pokémon TCG Live export format
  - **📋 Paste** — paste a decklist copied from Limitless TCG, TCGPlayer, PTCGL, etc. (same text
    format, no file needed)
- **📤 Export** — save the current deck as a `.txt` file in the same PTCGO format

### Achievements
- Unlockable achievements (set completions, collection milestones, backups, tags, and a few
  hidden ones) tracked in **Settings → Achievements**
- Animated toast notification with sound when one unlocks

### Backup & Restore
- **💾 Backup** exports your collection to a portable `.cardex` file (JSON)
- **📂 Restore** reimports a backup, replacing the current collection with a confirmation prompt
- Only user data is saved: owned cards (with quantities), want list, favorite sets, and tags
- Cache data (sets, cards, prices) and decks are excluded — sets/cards are re-downloaded automatically

### CSV Export
- Choose a **data range**: Collection, Wants, Duplicates, or Missing
- Choose an **export format**:
  - **Normal** — full data: `Name, Set, Number, Rarity, Quantity, Cardmarket (€), TCGPlayer ($), URLs`
  - **Cardmarket** — wantlist import: `Name;Edition;Quantity`
  - **TCGPlayer** — wishlist import: `Quantity, Name, Set Name, Number, Condition, Language`
- UTF-8 encoded, Excel-compatible

### Settings
Accessible via the ⚙ button — a dedicated window with five tabs:
  - **Options** — collection border color picker, Show My Collection toggle, achievement sound
  - **My Tags** — create/edit/delete custom colored tags
  - **Achievements** — progress and unlock status for every achievement
  - **Templates** — reference for the mass-import text format (set codes table, syntax examples)
  - **Shortcuts** — reference list of all keyboard shortcuts

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
├── Assets/              # Icons (nav, Cardmarket, TCGPlayer), achievement sounds
├── Converters/          # XAML value converters (image loading, ratio/wrap widths, etc.)
├── Data/                # EF Core DbContext
├── Models/              # Database entities and API models
├── SeedData/            # Embedded seed data (sets + cards)
├── Services/
│   ├── PokemonTcgService    # pokemontcg.io API client
│   ├── ImageCacheService    # Local image cache (thumbnails + full-res)
│   ├── AchievementService   # Achievement unlock checks
│   ├── UpdateService        # Update check and installation
│   └── AppSettings          # Configuration loader (persisted UI state, API key, ...)
├── ViewModels/
│   ├── MainViewModel        # Root view model
│   ├── SetViewModel         # A set with its filters and cards
│   ├── CardViewModel        # A card (owned, wanted, excluded, prices, links)
│   ├── SeriesViewModel      # A sidebar series group
│   ├── DeckBuilderViewModel # Deck Builder tab (current deck, browser, saved decks)
│   ├── TagViewModel / TagSectionViewModel
│   ├── AchievementViewModel
│   └── SearchResultViewModel
└── Views/
    ├── MainWindow.xaml      # Main application window (Collection + Deck Builder tabs)
    ├── DeckPanelView.xaml   # Deck Builder: current deck panel
    ├── DeckBuilderView.xaml # Deck Builder: card search + saved decks
    ├── PasteDeckDialog.xaml # Deck Builder: paste-a-decklist import dialog
    ├── SettingsWindow.xaml  # Settings (Options, Tags, Achievements, Templates, Shortcuts)
    ├── CardZoomWindow.xaml  # Full-res card viewer
    └── ExportDialog.xaml    # CSV export options
```

---

## Local Data

The SQLite database is stored in `%AppData%\Cardex\collection.db` and contains:

| Table | Content |
|---|---|
| `CachedSets` | Metadata for all sets |
| `CachedCards` | Cards with market prices and URLs |
| `OwnedCards` | Personal collection with quantities |
| `WantedCards` | Want list |
| `FavoriteSets` | Starred sets |
| `ExcludedCards` | Cards excluded from completion tracking |
| `Tags` / `CardTags` | Custom colored tags and their card assignments |
| `Decks` / `DeckCards` | Saved decks and their card lists |
| `UnlockedAchievements` | Achievement unlock records |

---

## Development

```bash
# Run in development
dotnet run

# Publish as single-file, self-contained exe
dotnet publish Cardex.csproj -c Release -r win-x64 -p:PublishSingleFile=true
# → bin/Release/net9.0-windows/win-x64/publish/Cardex.exe
```
