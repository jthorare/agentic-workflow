#!/bin/bash
# Renders every review-page mockup PNG from review.html with headless Chrome (Google Fonts are fetched; blocked fonts fall back).
# usage: bash shots-review.sh      (writes screens/review-*.png next to this script)
# shot "<query>" <name> <width> [height]: height defaults to 900; the -full shots use a tall window to show the whole scroll.
cd "$(dirname "$0")" || exit 1
mkdir -p screens
shot() { google-chrome --headless=new --no-sandbox --disable-gpu --hide-scrollbars --window-size="$3","${4:-900}" --virtual-time-budget=3000 --screenshot="screens/review-$2.png" "file://$PWD/review.html?$1" >/dev/null 2>&1; }
# The two variants, at the two widths: the viewport (900 high) and the whole page (tall window).
for v in a b; do
  shot "v=$v" "$v-1440" 1440
  shot "v=$v" "$v-1440-full" 1440 2050
  shot "v=$v" "$v-1000" 1000
  shot "v=$v" "$v-1000-full" 1000 2450
done
shot "v=b&tab=docmap" "b-1440-docmap" 1440
shot "v=b&tab=ci" "b-1000-ci" 1000
# Variant A only: panel states (§6.3), diff states, head moved, focus after reload, whole-page states (§15 Ph6).
for s in empty error loading; do shot "v=a&panels=$s" "a-panels-$s-1440" 1440 2050; done
for s in loading none error offline cap; do shot "v=a&diff=$s" "a-diff-$s-1440" 1440; done
shot "v=a&banner=moved" "a-moved-1440" 1440
shot "v=a&focus=strip" "a-reload-focus-1440" 1440
for s in merged closed notfound notserved offline; do shot "v=a&page=$s" "a-page-$s-1440" 1440; done
