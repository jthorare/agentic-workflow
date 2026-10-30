#!/usr/bin/env bash
# meta-gate-reserve <ready-queue-depth> | --show | --clear
# Owner 2026-10-17: the meta gate reserves box-wide heavy slots in proportion to its merge-queue depth
# (0-2 -> 0, 3-4 -> 1, 5-6 -> 2, >=7 -> all slots). Gates run with AW_HEAVY_PRIORITY=meta use reserved slots;
# every other expensive job takes only slots above the reservation. See bin-r1b/heavy.
set -u
f="${AW_STATE_DIR:-$HOME/.agentic-workflow}/heavy-meta-reserve"
cap="$(cat "${AW_STATE_DIR:-$HOME/.agentic-workflow}/heavy-slots-cap" 2>/dev/null)"; case $cap in ""|*[!0-9]*|0) cap=3 ;; esac
case "${1:-}" in
  --show) echo "reserve=$(cat "$f" 2>/dev/null || echo 0) cap=$cap"; exit 0 ;;
  --clear) echo 0 > "$f"; echo "reserve=0"; exit 0 ;;
  ""|*[!0-9]*) echo "usage: meta-gate-reserve <depth>|--show|--clear" >&2; exit 2 ;;
esac
d=$1
if [ "$d" -le 2 ]; then r=0; elif [ "$d" -le 4 ]; then r=1; elif [ "$d" -le 6 ]; then r=2; else r=$cap; fi
[ "$r" -gt "$cap" ] && r=$cap
echo "$r" > "$f.tmp" && mv "$f.tmp" "$f"
echo "depth=$d reserve=$r cap=$cap"
