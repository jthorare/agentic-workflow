#!/bin/bash
cd "$(dirname "$0")"
ls messages-*.html | xargs -P4 -I{} bash -c 'f={}; n=${f%.html}; w=1440; case $n in *-1200) w=1200;; *-1000) w=1000;; esac; google-chrome --headless=new --screenshot="$PWD/$n.png" --window-size=$w,900 --virtual-time-budget=3000 --hide-scrollbars "file://$PWD/$f" >/dev/null 2>&1'
ls *.png | wc -l
