#!/bin/bash
# Renders every mockup PNG from pages.html with headless Chrome (Google Fonts are fetched; blocked fonts fall back).
# usage: bash shots.sh      (writes screens/*.png next to this script)
cd "$(dirname "$0")" || exit 1
mkdir -p screens
shot() { google-chrome --headless=new --no-sandbox --disable-gpu --hide-scrollbars --window-size="$3","${4:-900}" --virtual-time-budget=3000 --screenshot="screens/$2.png" "file://$PWD/pages.html?$1" >/dev/null 2>&1; }
for w in 1440 1000; do
  for v in a b c; do
    shot "s=section&v=$v" "section-$v-$w" $w
    shot "s=sheet&v=$v" "sheet-$v-$w" $w
    shot "s=accept&v=$v" "accept-$v-$w" $w
    shot "s=none&v=$v" "none-$v-$w" $w
  done
  shot "s=today" "today-$w" $w
done
