#!/usr/bin/env bash
# Tests for scripts/meta-gate-reserve.sh (the meta gate's heavy-slot reserve), pinning what it does today. Everything runs
# against a temp AW_STATE_DIR (HOME is never changed). Covers: the depth tiers (0-2 -> 0, 3-4 -> 1, 5-6 -> 2, >=7 -> the slot cap);
# the cap file heavy-slots-cap (missing, empty, 0 and non-numeric fall back to 3; a smaller cap clamps every tier); --show with
# and without a reserve file; --clear; usage (exit 2); the reserve file holds just the number and no .tmp is left behind.
# Run: bash scripts/tests/meta-gate-reserve.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$DIR/meta-gate-reserve.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'find "$WORK" -delete 2> /dev/null' EXIT
export AW_STATE_DIR="$WORK/state"
F="$AW_STATE_DIR/heavy-meta-reserve"
CAP="$AW_STATE_DIR/heavy-slots-cap"
reset() { find "$AW_STATE_DIR" -mindepth 1 -delete 2> /dev/null; mkdir -p "$AW_STATE_DIR"; }
run() { bash "$SCRIPT" "$@" 2>&1; }
# at <cap file content or "-" for none> <depth>: stdout line of one run
at() {
  reset
  if [ "$1" != "-" ]; then printf '%s' "$1" > "$CAP"; fi
  run "$2"
}

# --- depth tiers, default cap (no cap file: 3) ---
check "depth 0" "$(at - 0)" "depth=0 reserve=0 cap=3"
check "depth 2" "$(at - 2)" "depth=2 reserve=0 cap=3"
check "depth 3" "$(at - 3)" "depth=3 reserve=1 cap=3"
check "depth 4" "$(at - 4)" "depth=4 reserve=1 cap=3"
check "depth 5" "$(at - 5)" "depth=5 reserve=2 cap=3"
check "depth 6" "$(at - 6)" "depth=6 reserve=2 cap=3"
check "depth 7" "$(at - 7)" "depth=7 reserve=3 cap=3"
check "depth 50" "$(at - 50)" "depth=50 reserve=3 cap=3"

# --- cap file variants ---
check "an empty cap file means 3" "$(at "" 7)" "depth=7 reserve=3 cap=3"
check "a cap of 0 means 3" "$(at 0 7)" "depth=7 reserve=3 cap=3"
check "a cap of abc means 3" "$(at abc 7)" "depth=7 reserve=3 cap=3"
check "cap 5: depth 7 reserves all 5" "$(at 5 7)" "depth=7 reserve=5 cap=5"
check "cap 5: depth 5 still reserves 2" "$(at 5 5)" "depth=5 reserve=2 cap=5"
check "cap 2: depth 5 clamps to 2" "$(at 2 5)" "depth=5 reserve=2 cap=2"
check "cap 2: depth 7 clamps to 2" "$(at 2 7)" "depth=7 reserve=2 cap=2"
check "cap 2: depth 3 stays 1" "$(at 2 3)" "depth=3 reserve=1 cap=2"
check "cap 1: depth 5 clamps to 1" "$(at 1 5)" "depth=5 reserve=1 cap=1"
check "cap 1: depth 7 clamps to 1" "$(at 1 7)" "depth=7 reserve=1 cap=1"
check "cap 1: depth 0 stays 0" "$(at 1 0)" "depth=0 reserve=0 cap=1"

# --- the reserve file ---
reset; run 5 > /dev/null
check "the file holds just the number" "$(cat "$F")" "2"
check "the file is one line" "$(wc -l < "$F" | tr -d ' ')" "1"
check "no .tmp is left behind" "$(ls "$AW_STATE_DIR" | tr '\n' ' ')" "heavy-meta-reserve "
run 0 > /dev/null
check "a later depth replaces it" "$(cat "$F")" "0"
check "...and still no .tmp" "$(ls "$AW_STATE_DIR" | tr '\n' ' ')" "heavy-meta-reserve "

# --- --show ---
reset
check "--show without a reserve file" "$(run --show)" "reserve=0 cap=3"
printf '2\n' > "$F"
check "--show with a reserve file" "$(run --show)" "reserve=2 cap=3"
printf '4' > "$CAP"
check "--show reads the cap file" "$(run --show)" "reserve=2 cap=4"
check "--show changes nothing" "$(cat "$F")" "2"

# --- --clear ---
reset; printf '3\n' > "$F"
check "--clear prints" "$(run --clear)" "reserve=0"
check "--clear writes 0" "$(cat "$F")" "0"
reset
run --clear > /dev/null
check "--clear with no file creates it" "$(cat "$F")" "0"

# --- usage ---
reset
out="$(bash "$SCRIPT" 2>&1)"; rc=$?
check "no argument exits 2" "$rc" "2"
check "no argument prints usage" "$out" "usage: meta-gate-reserve <depth>|--show|--clear"
for bad in -1 x 1x --bogus; do
  out="$(bash "$SCRIPT" "$bad" 2>&1)"; rc=$?
  check "'$bad' exits 2" "$rc" "2"
  check "'$bad' prints usage" "$out" "usage: meta-gate-reserve <depth>|--show|--clear"
done
check "usage writes no reserve file" "$([ -e "$F" ] && echo yes || echo no)" "no"

exit "$fail"
