#!/usr/bin/env bash
# Re-renders the README diagrams: each <name>.svg becomes a 2x <name>.png next to it.
# Needs Chromium (or Chrome) and Python with Pillow. Usage: docs/images/render.sh [path-to-chromium]
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
CHROME="${1:-$(command -v chromium || command -v chromium-browser || command -v google-chrome)}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

for svg in "$DIR"/*.svg; do
  name="$(basename "$svg" .svg)"
  # Size from the root <svg width="..." height="...">.
  read -r width height < <(python3 -c 'import re,sys; m=re.search(r"<svg[^>]*width=\"(\d+)\"[^>]*height=\"(\d+)\"", open(sys.argv[1]).read()); print(m[1], m[2])' "$svg")
  printf '<html><body style="margin:0;background:transparent"><img src="file://%s" width="%s" height="%s"></body></html>' \
    "$svg" "$width" "$height" > "$WORK/$name.html"
  "$CHROME" --headless=new --no-sandbox --disable-gpu --hide-scrollbars --force-device-scale-factor=2 \
    --default-background-color=00000000 --window-size=$((width + 100)),$((height + 260)) \
    --screenshot="$WORK/$name.png" "file://$WORK/$name.html" >/dev/null 2>&1
  python3 -c 'import sys; from PIL import Image; w, h = int(sys.argv[3]) * 2, int(sys.argv[4]) * 2; Image.open(sys.argv[1]).crop((0, 0, w, h)).save(sys.argv[2], optimize=True)' \
    "$WORK/$name.png" "$DIR/$name.png" "$width" "$height"
  echo "$name.png"
done
