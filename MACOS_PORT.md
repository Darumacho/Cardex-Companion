# Portage macOS — faisabilité et plan de travail

## Verdict

**WPF ne tourne pas sur macOS** — ce n'est pas une adaptation, c'est une réécriture de la
couche UI (les 7 fichiers `.xaml`/`.xaml.cs` de `Views/`). La bonne nouvelle : le reste du code
(modèles, DB, services API, et la quasi-totalité des ViewModels) est déjà du C# pur sans
dépendance Windows, et peut être réutilisé presque tel quel.

**Recommandation : [Avalonia UI](https://avaloniaui.net/)**, pas MAUI. Raisons :
- Vraiment cross-platform (Windows + macOS + Linux) depuis un seul projet — pas un fork séparé.
- Son modèle XAML/MVVM et son API `Media` (`SolidColorBrush`, `Color`, `ColorConverter`...) sont
  volontairement très proches de WPF — la migration des ViewModels est mécanique, pas une
  réécriture conceptuelle.
- Compatible `CommunityToolkit.Mvvm` ([ObservableProperty]/[RelayCommand]) sans changement.
- Contrairement à réutiliser le projet MAUI mobile (CardexMobile) : on garde les fonctionnalités
  desktop (Deck Builder, export CSV, Backup/Restore par dialogue fichier, son d'achievement...)
  plutôt que de repartir sur une UI pensée pour le tactile/mobile.

MAUI + Mac Catalyst reste une option plus rapide à démarrer (le projet CardexMobile a déjà un
dossier `Platforms/MacCatalyst` scaffoldé par défaut, juste jamais activé dans
`<TargetFrameworks>`), mais ça revient à construire une **troisième** UI dans l'esprit mobile,
pas à porter l'existant desktop. À envisager seulement si le portage Avalonia s'avère trop long.

## Ce qui se réutilise sans changement

- `Models/*.cs` — POCOs purs, aucune dépendance Windows.
- `Data/AppDbContext.cs` — EF Core + SQLite, déjà cross-platform.
- `Services/PokemonTcgService.cs`, `TCGdexService.cs`, `CollectionSyncService.cs`,
  `AppSettings.cs`, `AchievementService.cs`, `LanguageCatalog.cs` — HTTP/JSON/logique métier pure.
- La quasi-totalité des ViewModels (`CardViewModel`, `MainViewModel`, `SetViewModel`,
  `DeckBuilderViewModel`, etc.) — logique MVVM pure via CommunityToolkit.Mvvm, à l'exception des
  points listés ci-dessous.

## Ce qui doit être réécrit ou adapté

### 1. Les vues (réécriture complète, inévitable)
`Views/*.xaml` + leur `.xaml.cs` (7 fichiers) — `MainWindow`, `SettingsWindow`, `CardZoomWindow`,
etc. La structure logique (templates de carte, popups, triggers de couleur) se transpose, mais la
syntaxe XAML d'Avalonia diffère par endroits (styles, `MultiDataTrigger` n'existe pas tel quel,
`Popup` a une API différente) — compter une réécriture, pas un copier-coller.

### 2. Dépendances `System.Windows.*` dans les ViewModels/Services (fuites ponctuelles)
Trouvées par un grep sur `System.Windows` — 9 fichiers sur 39 dans `ViewModels`/`Services`,
toutes remplaçables par l'équivalent Avalonia (API très proche) :

| Fichier | Usage WPF | Équivalent Avalonia |
|---|---|---|
| `CardViewModel.cs`, `MainViewModel.cs` | `SolidColorBrush`, `Color`, `ColorConverter` (couleurs de tags/bordures) | `Avalonia.Media.SolidColorBrush`/`Color` (API quasi identique) |
| `AchievementViewModel.cs`, `CardViewModel.cs`, `SearchResultViewModel.cs`, `SetViewModel.cs`, `MainViewModel.cs`, `ImageCacheService.cs` | `BitmapImage` (images de cartes, 6 fichiers) | `Avalonia.Media.Imaging.Bitmap` |
| `DeckBuilderViewModel.cs` | `System.Windows.Threading.DispatcherTimer` (toast) | `Avalonia.Threading.DispatcherTimer` (même nom, même API) |
| `MainViewModel.cs` | `Application.Current.Resources[...]` (couleur de bordure collection dynamique) | `Application.Current.Resources` existe aussi côté Avalonia, syntaxe proche |

### 3. Dialogues Windows-only (8 appels `MessageBox.Show`, 2 fichiers avec `Microsoft.Win32.*FileDialog`)
`MainViewModel.cs` (6 `MessageBox.Show`) et `DeckBuilderViewModel.cs` (2) pour les confirmations ;
`Microsoft.Win32.OpenFileDialog`/`SaveFileDialog` pour CSV export, Backup/Restore. Avalonia n'a pas
de `MessageBox` intégré — soit un petit service de dialogue maison (une `Window` réutilisable),
soit le package `MessageBox.Avalonia`. Les file pickers passent par
`TopLevel.GetTopLevel(...).StorageProvider` (`FilePickerOpenOptions`/`SaveFileOptions`).

### 4. Mise à jour automatique (`Services/UpdateService.cs`)
Le mécanisme actuel télécharge/remplace un `.exe` Windows — ne transpose pas tel quel sur macOS.
À minima, prévoir un chemin différent (ex. simple lien vers la page de releases GitHub plutôt
qu'un remplacement silencieux), surtout vu les contraintes de signature/notarisation ci-dessous.

### 5. Curseur personnalisé (`CursorHelper.cs`, P/Invoke `user32.dll`/`gdi32.dll`)
Seule vraie dépendance Win32 bas niveau du repo : construit un curseur "loupe" à la main via
`CreateIconIndirect`/`CreateDIBSection` (Windows uniquement, aucun équivalent macOS). Utilisé une
seule fois (`App.xaml.cs:138`, `Resources["MagnifierCursor"]`). Bonne nouvelle : Avalonia a un
`Cursor` cross-platform qui accepte directement un bitmap — ce fichier entier disparaît au profit
de quelques lignes (`new Cursor(bitmap, hotspot)`), pas de portage ligne à ligne nécessaire.

### 6. Packaging et distribution macOS (hors "est-ce que ça tourne")
- Windows publie un `.exe` self-contained single-file (`dotnet publish -r win-x64
  --self-contained true`) — l'équivalent macOS est un bundle `.app` (`dotnet publish -r osx-x64`
  ou `osx-arm64`, `-p:UseAppHost=true`), généralement empaqueté dans un `.dmg` pour la
  distribution.
- **Gatekeeper** : une app macOS non signée par un compte Apple Developer ($99/an) affichera un
  avertissement de sécurité à l'installation (contournable manuellement, mais désagréable pour
  un testeur). Signature + notarisation (`codesign` + `xcrun notarytool`) sont nécessaires pour
  une distribution propre — à anticiper si l'app doit circuler au-delà de ta propre machine.
- Build Apple Silicon (`osx-arm64`) vs Intel (`osx-x64`) : à décider selon le(s) Mac(s) cible(s),
  ou publier les deux (comme CardexMobile ne gère qu'Android pour l'instant, pas de précédent
  direct dans ce repo à copier).

## Plan de travail suggéré (sur le Mac)

1. `dotnet workload install` les workloads macOS nécessaires (Avalonia ne demande pas de
   workload spécifique comme MAUI, juste le SDK .NET classique + le template Avalonia
   `dotnet new avalonia.app` ou l'ajout manuel du package `Avalonia`/`Avalonia.Desktop` à un
   nouveau projet).
2. Nouveau projet `Cardex.Avalonia.csproj` (ou renommage/réorganisation, au choix) ciblant
   `net9.0` multi-RID (`win-x64;osx-x64;osx-arm64`) — garder `Cardex.csproj` (WPF) intact en
   parallèle tant que le portage n'est pas à parité, pour ne pas perdre le build Windows actuel.
3. Déplacer `Models/`, `Data/`, `Services/` (hors `UpdateService.cs`/`ImageCacheService.cs`) et
   les ViewModels tels quels dans le nouveau projet — compiler, corriger les ~9 fichiers listés
   plus haut un par un.
4. Réécrire les vues une par une en commençant par `MainWindow.xaml` (la plus grosse, mais celle
   qui débloque le plus vite un rendu visible pour itérer).
5. Repenser `UpdateService` et le packaging/signature en dernier, une fois l'UI fonctionnelle.

## Vérification de faisabilité déjà faite (cette session, côté Windows)

- Audit `grep -rl "System.Windows"` sur `ViewModels/`+`Services/` : 9/39 fichiers concernés,
  tous listés ci-dessus avec leur équivalent Avalonia pressenti — rien d'exotique.
- Une seule dépendance P/Invoke Win32 dans tout le repo (`CursorHelper.cs`, voir point 5
  ci-dessus) ; aucun usage de la Registry Windows.
- La base SQLite (`Microsoft.EntityFrameworkCore.Sqlite`) est déjà cross-platform (utilisée par
  CardexMobile sous Android sans changement de schéma).
