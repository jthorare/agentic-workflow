#!/usr/bin/env bash
# config/lib/heavy-config.sh — the one reader of the box's two heavy-slot files, sourced by scripts/heavy.sh and
# config/lib/locks.sh (#2061). Both read the files again on every pass of their wait loop, so a cap lowered or a
# reserve raised while a job waits applies to its next look. Sindri reads the same files in HeavyCap.cs.
#   $AW_STATE_DIR/heavy-slots-cap      the box-wide slot count (the owner's cap; wins over AW_HEAVY_SLOTS)
#   $AW_STATE_DIR/heavy-meta-reserve   how many of the lowest slots only AW_HEAVY_PRIORITY=meta jobs may take
# $AW_STATE_DIR defaults to ~/.agentic-workflow. Nothing here writes or exits; a missing or malformed file is "not set".

# heavy_cap <fallback>: the cap file's number when it is a whole number of 1 or more, else <fallback>.
heavy_cap() {
  local cap
  cap="$(cat "${AW_STATE_DIR:-$HOME/.agentic-workflow}/heavy-slots-cap" 2>/dev/null)"
  case $cap in ""|*[!0-9]*|0) printf '%s\n' "$1" ;; *) printf '%s\n' "$cap" ;; esac
}

# heavy_reserve <slots>: the reserve file's number (0 when missing or not a whole number), at most <slots>.
heavy_reserve() {
  local reserve
  reserve="$(cat "${AW_STATE_DIR:-$HOME/.agentic-workflow}/heavy-meta-reserve" 2>/dev/null)"
  case $reserve in ""|*[!0-9]*) reserve=0 ;; esac
  [ "$reserve" -gt "$1" ] && reserve="$1"
  printf '%s\n' "$reserve"
}
