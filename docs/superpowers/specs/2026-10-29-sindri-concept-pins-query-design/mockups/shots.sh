#!/bin/bash
# Renders every mockup PNG from pins.html with headless Chrome (Google Fonts are fetched; blocked fonts fall back).
# Add several shots are 1100 px high (the dialog is taller than the other screens).
# usage: bash shots.sh      (writes screens/*.png next to this script)
cd "$(dirname "$0")" || exit 1
mkdir -p screens
shot() { google-chrome --headless=new --no-sandbox --disable-gpu --hide-scrollbars --window-size="$3","${4:-900}" --virtual-time-budget=3000 --screenshot="screens/$2.png" "file://$PWD/pins.html?$1" >/dev/null 2>&1; }
for v in a b c; do
  for w in 1440 1000; do
    shot "s=sheet&v=$v&mode=pin" "sheet-$v-$w" $w
    shot "s=sheet&v=$v&mode=edit" "sheet-$v-edit-$w" $w
    shot "s=cards&v=$v" "cards-$v-$w" $w
    shot "s=several&v=$v" "several-$v-$w" $w 1100
  done
  shot "s=sheet&v=$v&mode=pin&other=1" "sheet-$v-other-1440" 1440
done
shot "s=several&v=c&step=1" "several-c-step1-1440" 1440 1100
shot "s=several&v=c&step=1" "several-c-step1-1000" 1000 1100
