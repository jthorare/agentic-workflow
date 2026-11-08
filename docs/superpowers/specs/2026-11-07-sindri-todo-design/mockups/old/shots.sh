#!/bin/bash
# Renders every mockup PNG from todo.html with headless Chrome (Google Fonts are fetched; blocked fonts fall back).
# usage: bash shots.sh      (writes screens/*.png next to this script)
cd "$(dirname "$0")" || exit 1
mkdir -p screens
shot() { google-chrome --headless=new --no-sandbox --disable-gpu --hide-scrollbars --window-size="$3","${4:-900}" --virtual-time-budget=4000 --screenshot="screens/$2.png" "file://$PWD/todo.html?$1" >/dev/null 2>&1; }
# The page (§3.1) at 1440 and at the 1000 px minimum; a taller one shows From elsewhere whole.
shot "page=todo" "page-1440" 1440
shot "page=todo" "page-1000" 1000
shot "page=todo" "page-full-1440" 1440 1350
shot "page=todo&sel=1" "page-selected-1440" 1440
shot "page=todo&done=1" "done-expanded-1440" 1440 1100
# States (§3.4)
shot "page=todo&state=empty" "empty-1440" 1440
shot "page=todo&state=notrunning" "notrunning-1440" 1440
shot "page=todo&state=elsefail" "elsewhere-failing-1440" 1440 1350
shot "page=todo&edit=1" "edit-1440" 1440
shot "page=todo&conflict=1" "conflict-409-1440" 1440
shot "page=todo&toast=1" "undo-toast-1440" 1440
# Quick-add (§3.2) over the Reviews page: N works on any page
shot "page=reviews&quick=parsed" "quickadd-1440" 1440
shot "page=reviews&quick=pr" "quickadd-pr-1440" 1440
shot "page=reviews&quick=fail" "quickadd-failed-1440" 1440
shot "page=reviews&quick=parsed" "quickadd-1000" 1000
# Home with the new line (§3), and the measured top bar (§11 TP-7)
shot "page=home" "home-1440" 1440
shot "page=bars" "bars" 1440 1000
# Fallback if ten tabs do not fit at 1000 px: Todo as a button in Home's header
shot "page=home&v=home" "fallback-home-1000" 1000
