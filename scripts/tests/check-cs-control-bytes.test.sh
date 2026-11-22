#!/usr/bin/env bash
# scripts/check-cs-control-bytes.sh (#2203): the real tree passes; a planted 0x08, 0x00 or 0x1F in a tracked .cs file fails
# naming file:line and the byte; tab, CR and LF pass; untracked files and other extensions are not scanned; bad usage exits 2.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/check-cs-control-bytes.sh"
TMP="$(mktemp -d)"
trap 'rm -r "$TMP"' EXIT

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }

out="$(bash "$CHECK" 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "the real tree passes" || bad "real tree rc=$rc: $out"

R="$TMP/repo"
mkdir -p "$R/src/Sub"
git -C "$R" init -q
printf 'class Clean\n{\n\tstring s = "a";\r\n}\n' > "$R/src/Clean.cs"
printf 'class Bad\n{\n    [GeneratedRegex(@"Evidence\x08")]\n}\n' > "$R/src/Sub/Bad.cs"
git -C "$R" add -A

out="$(bash "$CHECK" "$R" 2>&1)"; rc=$?
if [ "$rc" = 1 ] && grep -qx 'src/Sub/Bad.cs:3: control byte 0x08' <<<"$out"; then
  ok "a planted backspace fails naming file:line and the byte"
else
  bad "planted 0x08 rc=$rc out=$out"
fi
grep -q Clean.cs <<<"$out" && bad "tab or CR was flagged" || ok "tab, CR and LF pass"
printf '%s' "$out" | LC_ALL=C grep -q "$(printf '\x08')" && bad "the raw byte was printed" || ok "the byte is printed as hex, never raw"

printf 'a\x00b\n' > "$R/src/Nul.cs"
printf '// x\n// y\x1f\x0b\n' > "$R/src/Unit.cs"
git -C "$R" add -A
out="$(bash "$CHECK" "$R" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -qx 'src/Nul.cs:1: control byte 0x00' <<<"$out"; } && ok "a NUL byte fails" || bad "NUL rc=$rc out=$out"
{ grep -qx 'src/Unit.cs:2: control byte 0x1F' <<<"$out" && grep -qx 'src/Unit.cs:2: control byte 0x0B' <<<"$out"; } \
  && ok "every byte on a line is named (0x1F and 0x0B in a comment)" || bad "unit separator out=$out"

git -C "$R" rm -q --cached src/Sub/Bad.cs src/Nul.cs src/Unit.cs
printf 'x\x08\n' > "$R/notes.txt"
git -C "$R" add notes.txt
bash "$CHECK" "$R" >/dev/null 2>&1 && ok "untracked .cs files and other extensions are not scanned" || bad "untracked or .txt was scanned"

bash "$CHECK" "$TMP/missing" >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "a missing dir exits 2" || bad "missing dir rc=$rc"
mkdir "$TMP/plain"
bash "$CHECK" "$TMP/plain" >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "a dir that is not a git checkout exits 2" || bad "plain dir rc=$rc"
bash "$CHECK" a b >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "two arguments exit 2" || bad "two args rc=$rc"

[ "$fails" = 0 ] && echo "all passed" || { echo "$fails failed"; exit 1; }
