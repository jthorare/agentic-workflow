#!/usr/bin/env bash
# Tests for scripts/ci/tmp-leak-check.sh: it names a new aw-* or tmp.* entry the command leaves, passes a clean
# command, keeps the command's own exit code, ignores entries that were there before, and deletes nothing.
set -uo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null; rm -rf "$p"; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; }
trap __aw_tmp_clean EXIT

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHECK="$HERE/../ci/tmp-leak-check.sh"
fails=0
ok()   { echo "PASS: $1"; }
fail() { echo "FAIL: $1"; fails=$((fails + 1)); }

T="$(mktemp -d)"
W="$(mktemp -d)"   # where the checker's stdout and stderr go, outside the watched dir
run() { TMPDIR="$T" bash "$CHECK" "$@" 2>"$W/err" >"$W/out"; }
mkdir "$T/aw-old" "$T/tmp.old" "$T/other-old"

# 1. A clean command passes.
run -- true; rc=$?
[ "$rc" = 0 ] && ok "a command that leaves nothing exits 0" || fail "clean command exit $rc"

# 2. A leaked aw-* dir is named, exit 1, and not deleted.
run -- bash -c 'mkdir "$TMPDIR/aw-leaked-1"'; rc=$?
[ "$rc" = 1 ] && ok "a leaked aw-* dir exits 1" || fail "aw leak exit $rc"
grep -qF "$T/aw-leaked-1" "$W/err" && ok "the leak is named" || fail "leak not named: $(cat "$W/err")"
[ -d "$T/aw-leaked-1" ] && ok "the leaked dir is not deleted" || fail "the checker deleted the dir"
[ -d "$T/aw-old" ] && [ -d "$T/tmp.old" ] && ok "entries from before the run are untouched" || fail "an old entry was deleted"

# 3. A leaked tmp.* file counts too, and two leaks are both named.
run -- bash -c 'touch "$TMPDIR/tmp.leaked2"; mkdir "$TMPDIR/aw-leaked-3"'; rc=$?
if [ "$rc" = 1 ] && grep -qF "$T/tmp.leaked2" "$W/err" && grep -qF "$T/aw-leaked-3" "$W/err"; then
  ok "a tmp.* file and a second leak are both named"
else fail "two leaks: rc=$rc $(cat "$W/err")"; fi
if grep -qF "aw-leaked-1" "$W/err"; then fail "a leak from an earlier run was reported again"; else ok "a leak from an earlier run is not reported again"; fi
if grep -qF "aw-old" "$W/err"; then fail "an entry from before the run was reported"; else ok "entries from before the run are not reported"; fi

# 4. Other names are ignored.
run -- bash -c 'mkdir "$TMPDIR/unrelated-new"'; rc=$?
[ "$rc" = 0 ] && ok "a new entry with another name is ignored" || fail "unrelated entry flagged ($rc)"

# 5. A command that makes and removes its dir passes.
run -- bash -c 'd="$(mktemp -d "$TMPDIR/tmp.XXXXXX")"; rmdir "$d"'; rc=$?
[ "$rc" = 0 ] && ok "a dir the command removed is no leak" || fail "removed dir flagged ($rc)"

# 6. The command's exit code wins, and its leaks are still named.
run -- bash -c 'mkdir "$TMPDIR/aw-leaked-6"; exit 7'; rc=$?
if [ "$rc" = 7 ] && grep -qF "aw-leaked-6" "$W/err"; then ok "the command's exit code wins and the leak is named"; else fail "rc=$rc $(cat "$W/err")"; fi

# 7. The command's stdout passes through.
run -- echo hello
grep -qx hello "$W/out" && ok "the command's output passes through" || fail "output lost"

# 8. Usage errors exit 2.
run; rc=$?; [ "$rc" = 2 ] && ok "no arguments exit 2" || fail "no args exit $rc"
run --; rc=$?; [ "$rc" = 2 ] && ok "no command exits 2" || fail "empty command exit $rc"
run true; rc=$?; [ "$rc" = 2 ] && ok "a missing -- exits 2" || fail "no -- exit $rc"

# 9. Its own bookkeeping files are never left behind.
left="$(find "$T" -maxdepth 1 -name 'aw-leak-*' | wc -l | tr -d ' ')"
[ "$left" = 0 ] && ok "the checker leaves none of its own files" || fail "left $left bookkeeping files"

if [ "$fails" = 0 ]; then echo "all passed"; exit 0; fi
echo "$fails failed"; exit 1
