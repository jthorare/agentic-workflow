#!/bin/bash
# Renders the sheet mockup PNGs from sheets.html with headless Chrome (Google Fonts are fetched; blocked fonts fall back).
# usage: bash shots-sheets.sh      (writes screens/sheet-*.png next to this script)
cd "$(dirname "$0")" || exit 1
mkdir -p screens
shot() { google-chrome --headless=new --no-sandbox --disable-gpu --hide-scrollbars --window-size="$3",900 --virtual-time-budget=3000 --screenshot="screens/$2.png" "file://$PWD/sheets.html?$1" >/dev/null 2>&1; }
# Release 1: the fix-task sheet from a diff line (both widths), a required check and a requirement (1440 only).
for w in 1440 1000; do shot "sheet=fix&src=line" "sheet-fix-line-$w" $w; done
shot "sheet=fix&src=check" "sheet-fix-check-1440" 1440
shot "sheet=fix&src=req" "sheet-fix-req-1440" 1440
# Sending, the three outcomes, the contributor's PR and the half-done send.
for w in 1440 1000; do
  for s in sending o1 o2 o3 contrib half; do shot "sheet=fix&src=line&state=$s" "sheet-fix-$s-$w" $w; done
done
# Release 2: the inline comment composer, the confirm sheet, Posting… and SND-REVIEW-010..013.
for w in 1440 1000; do
  shot "composer=1" "sheet-composer-$w" $w
  shot "sheet=confirm" "sheet-confirm-$w" $w
  shot "sheet=confirm&state=own" "sheet-confirm-own-$w" $w
  shot "sheet=confirm&state=posting" "sheet-posting-$w" $w
  for e in e010 e011 e012 e013; do shot "sheet=confirm&state=$e" "sheet-$e-$w" $w; done
done
shot "composer=1&suggest=1" "sheet-composer-suggest-1440" 1440
