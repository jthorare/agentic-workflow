#!/bin/bash
# Renders the list builder's mockup PNGs from list.html with headless Chrome (Google Fonts are fetched; blocked fonts fall back).
# usage: bash shots-list.sh      (writes screens/list-*.png next to this script)
cd "$(dirname "$0")" || exit 1
mkdir -p screens
shot() { google-chrome --headless=new --no-sandbox --disable-gpu --hide-scrollbars --window-size="$3",900 --virtual-time-budget=4000 --screenshot="screens/$2.png" "file://$PWD/list.html?$1" >/dev/null 2>&1; }
# The measured top bar (§3 "Nine tabs at the minimum width"): the model, plus the bar as drawn at each width.
shot "page=bars" "list-bars" 1440
for w in 1000 1099 1100 1440; do shot "page=list" "list-main-$w" $w; done
# The list's states (§6.3 row 1, §5.6), at 1440 and, for the ones that change shape, 1000.
for s in loading empty nologin error pre2b; do shot "page=list&state=$s" "list-state-$s-1440" 1440; done
shot "page=list&state=loading" "list-state-loading-1000" 1000
shot "page=list&state=error" "list-state-error-1000" 1000
# The library errors sheet (header count): beside the page at 1440, covering it at 1000, and its other texts.
shot "page=list&sheet=errors" "list-sheet-errors-1440" 1440
shot "page=list&sheet=errors" "list-sheet-errors-1000" 1000
for l in loading empty rooterr; do shot "page=list&sheet=errors&lib=$l" "list-sheet-$l-1440" 1440; done
shot "page=list&lib=empty" "list-header-empty-1440" 1440
# Settings -> Library (§5.1, §6.3 last row), inside the Settings page's shape.
for s in main loading empty add; do shot "page=settings&slib=$s" "list-settings-$s-1440" 1440; done
# Home's "N PRs waiting on you" line.
shot "page=home" "list-home-1440" 1440
