#!/bin/bash
# Renders every mockup PNG from projects.html with headless Chrome (Google Fonts are fetched; blocked fonts fall back).
# usage: bash shots.sh      (writes screens/*.png next to this script)
cd "$(dirname "$0")" || exit 1
mkdir -p screens
shot() { google-chrome --headless=new --no-sandbox --disable-gpu --hide-scrollbars --window-size="$3",900 --virtual-time-budget=3000 --screenshot="screens/$2.png" "file://$PWD/projects.html?$1" >/dev/null 2>&1; }
for v in a b c; do
  shot "v=$v" "$v-1440" 1440
  shot "v=$v" "$v-1000" 1000
  shot "v=$v&sheet=card" "$v-sheet-1440" 1440
  shot "v=$v&fold=finished" "$v-finished-1440" 1440
done
# Page states and sheets (§3.1, §3.2), drawn once in variant A: they're the same in every variant.
for s in loading ctxerr empty syncing noled; do shot "v=a&state=$s" "state-$s-1440" 1440; done
shot "v=a&marks=1" "state-marks-1440" 1440
shot "v=a&sheet=card" "sheet-covers-1000" 1000
for s in add edit remove; do shot "v=a&sheet=$s" "sheet-$s-1440" 1440; done
