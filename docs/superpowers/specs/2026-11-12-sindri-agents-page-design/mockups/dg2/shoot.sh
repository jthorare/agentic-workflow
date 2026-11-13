#!/bin/bash
cd "$(dirname "$0")"
for n in $(cat frames.txt); do
  w=1440; case $n in *-1000) w=1000;; esac
  google-chrome --headless=new --screenshot="$PWD/$n.png" --window-size=$w,900 --virtual-time-budget=5000 --hide-scrollbars "file://$PWD/$n.html" >/dev/null 2>&1
done
ls *.png | wc -l
