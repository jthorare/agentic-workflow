#!/usr/bin/env bash
# Renders the visual-critique cases: pages.py writes the HTML, headless Chrome screenshots each at 1512x982
# (ui-evidence's default viewport), the names in retina.txt at device pixel ratio 2 (3024x1964).
# Usage: render.sh <out-dir>   (CHROME overrides the browser; default google-chrome)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?usage: render.sh <out-dir>}"
chrome="${CHROME:-google-chrome}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
python3 -I "$here/pages.py" "$work"
for html in "$work"/*.html; do
  base="$(basename "$html" .html)"
  scale=1
  grep -qx "${base%%.*}" "$work/retina.txt" && scale=2
  "$chrome" --headless=new --disable-gpu --hide-scrollbars --no-first-run --user-data-dir="$work/profile" \
    --window-size=1512,982 --force-device-scale-factor="$scale" \
    --screenshot="$out/$base.png" "file://$html" >/dev/null 2>&1
done
