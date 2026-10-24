#!/bin/zsh
# Génère _macOS/Assets/Cardex.icns (icône de l'app macOS) à partir de Logo.png.
# Usage : _macOS/make-icon.sh [image source]   (défaut : Logo.png à la racine du repo)
# Outils macOS intégrés uniquement (sips, iconutil).
#
# La source actuelle fait 210 px : les tailles au-delà (256 px et plus) sont agrandies et donc
# légèrement floues. Fournir un logo 1024×1024 et relancer ce script pour une icône nette partout.
set -euo pipefail

REPO="${0:A:h:h}"
SRC="${1:-$REPO/Logo.png}"
OUT="$REPO/_macOS/Assets/Cardex.icns"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Maître 1024×1024 : le logo occupe ~80 % du carré, comme la grille des icônes macOS
# (un logo collé aux bords paraît plus gros que les autres icônes du Dock).
sips -s format png -z 824 824 "$SRC" --out "$WORK/logo824.png" >/dev/null
sips --padToHeightWidth 1024 1024 "$WORK/logo824.png" --out "$WORK/master.png" >/dev/null

ICONSET="$WORK/Cardex.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
  sips -z $size $size "$WORK/master.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  sips -z $((size * 2)) $((size * 2)) "$WORK/master.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done

mkdir -p "${OUT:h}"
iconutil -c icns "$ICONSET" -o "$OUT"
echo "Icône générée : $OUT"
