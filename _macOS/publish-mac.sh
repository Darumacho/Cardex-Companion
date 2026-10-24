#!/bin/zsh
# Publie Cardex pour macOS sous forme de bundle .app autonome (runtime .NET inclus).
# Usage : _macOS/publish-mac.sh [osx-arm64|osx-x64]   (défaut : osx-arm64, Mac Apple Silicon)
# Sortie : _macOS/publish/<rid>/Cardex.app
#
# Signature « ad hoc » uniquement (codesign -s -) : suffisant pour lancer l'app sur ce Mac.
# Sur un autre Mac, Gatekeeper avertira (app non notarisée) — voir MACOS_PORT.md, packaging.
set -euo pipefail

RID="${1:-osx-arm64}"
MACOS_DIR="${0:A:h}"
PROJECT="$MACOS_DIR/CardexMac.csproj"
OUT="$MACOS_DIR/publish/$RID"
APP="$OUT/Cardex.app"

# Version unique : celle du csproj (même numéro que le build Windows)
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -1)"
BUNDLE_ID="com.darumacho.cardex"

echo "→ dotnet publish ($RID, version $VERSION)"
rm -rf "$OUT"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true \
  -p:UseAppHost=true -o "$OUT/bin" -nologo -v quiet

echo "→ Assemblage de Cardex.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/bin/." "$APP/Contents/MacOS/"
cp "$MACOS_DIR/Assets/Cardex.icns" "$APP/Contents/Resources/Cardex.icns"
rm -rf "$OUT/bin"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>                 <string>Cardex</string>
    <key>CFBundleDisplayName</key>          <string>Cardex</string>
    <key>CFBundleIdentifier</key>           <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>              <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>   <string>$VERSION</string>
    <key>CFBundleExecutable</key>           <string>Cardex</string>
    <key>CFBundleIconFile</key>             <string>Cardex.icns</string>
    <key>CFBundlePackageType</key>          <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>LSApplicationCategoryType</key>    <string>public.app-category.utilities</string>
    <!-- .NET 9 requiert macOS 12 (Monterey) minimum -->
    <key>LSMinimumSystemVersion</key>       <string>12.0</string>
    <key>NSHighResolutionCapable</key>      <true/>
</dict>
</plist>
PLIST

echo "→ Signature ad hoc"
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"

echo "✓ $APP ($(du -sh "$APP" | cut -f1))"

# Fichier à joindre à la release GitHub (à côté de Cardex.exe) : c'est lui que l'updater macOS
# télécharge. Le nom doit correspondre à UpdateService.AssetName (_macOS/Services/UpdateService.cs).
ARCH="${RID#osx-}"
ZIP="$OUT/Cardex-macOS-$ARCH.zip"
ditto -c -k --keepParent "$APP" "$ZIP"
echo "✓ $ZIP ($(du -sh "$ZIP" | cut -f1)) — à joindre à la release GitHub"
