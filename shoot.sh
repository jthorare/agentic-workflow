#!/bin/bash
# screenshots every home-*.html (browser lock held by the caller)
cd "$(dirname "$0")"
for f in home-*.html; do
  n=${f%.html}; w=1440; [ "$n" = home-all-1000 ] && w=1000
  google-chrome --headless=new --screenshot="$PWD/$n.png" --window-size=$w,900 --virtual-time-budget=5000 --hide-scrollbars "file://$PWD/$f" >/dev/null 2>&1
done
ls -la *.png
