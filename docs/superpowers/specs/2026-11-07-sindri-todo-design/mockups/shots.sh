#!/bin/bash
# Renders every mockup PNG from todo.html with headless Chrome (Google Fonts are fetched; blocked fonts fall back).
# usage: bash shots.sh [screen...]   (writes screens/<screen>-1000.png at 1000 x 600 and <screen>-1440.png at 1440 x 900 next to this script)
cd "$(dirname "$0")" || exit 1
mkdir -p screens
SCREENS=(home-collapsed home-expanded home-expanded-top attn-a attn-b attn-c attn-c-hold attn-d attn-e attn-open-a attn-open-d attn-open-hold focus-panel focus-needs quickadd workflows settings-a settings-b settings-c settings-d)
[ $# -gt 0 ] && SCREENS=("$@")
shot() { google-chrome --headless=new --no-sandbox --disable-gpu --window-size="$2","$3" --virtual-time-budget=4000 --screenshot="screens/$1-$2.png" "file://$PWD/todo.html?s=$1" >/dev/null 2>&1; }
for s in "${SCREENS[@]}"; do
  shot "$s" 1000 600
  shot "$s" 1440 900
done
