#!/usr/bin/env bash
# scripts/check-parity.sh (W6, #3266): the real docs/parity/slack.md passes; a bare row fails naming file:line and the row;
# every status word needs its detail; row numbers run 1..N; --release accepts only done, partial, missing #N and dropped.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/check-parity.sh"
TMP="$(mktemp -d)"
trap 'rm -r "$TMP"' EXIT

fails=0
ok() { echo "PASS: $1"; }
bad() { echo "FAIL: $1"; fails=$((fails + 1)); }

# ledger <file> <row lines...>: a ledger with the real header and the given rows.
ledger() {
  local f="$1"; shift
  { echo "# Slack parity ledger"; echo; echo "| # | Function | How, and the limit | Status | Evidence |"; echo "|---|---|---|---|---|"
    for r in "$@"; do echo "$r"; done; } > "$f"
}

out="$(bash "$CHECK" "$ROOT/docs/parity/slack.md" 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "the real ledger passes" || bad "real ledger rc=$rc: $out"

Parity_check_fails_on_a_bare_row() {
  # partial with an empty How cell is bare; so is a status with no detail at all
  ledger "$TMP/bare.md" '| 1 | Channels | `conversations.history` | planned M1 | |' '| 2 | Direct messages | | partial | |'
  out="$(bash "$CHECK" "$TMP/bare.md" 2>&1)"; rc=$?
  if [ "$rc" = 1 ] && grep -q "^$TMP/bare.md:6: row 2: partial needs its limit" <<<"$out"; then
    ok "Parity_check_fails_on_a_bare_row: partial with no limit fails naming file:line and the row"
  else
    bad "Parity_check_fails_on_a_bare_row rc=$rc out=$out"
  fi
  grep -q 'row 1:' <<<"$out" && bad "a good planned row was flagged" || ok "a planned row with a phase passes"
}
Parity_check_fails_on_a_bare_row

# Each status word without its detail is a bare row.
for case in \
  'done|done needs an evidence path or #N|| done | |' \
  'missing|missing needs an issue (missing #N)|x | missing | |' \
  'dropped|dropped needs a reason (dropped: why)|x | dropped | |' \
  'dropped-colon|dropped needs a reason (dropped: why)|x | dropped: | |' \
  'planned|planned needs a phase (planned M1)|x | planned | |' \
  'differs|differs needs its limit|| differs | |' \
  'blocked|blocked needs the P0 probe line that decides it|no API | blocked | |' \
  'empty|unknown status ""|x |  | |' \
  'tbd|unknown status "TBD"|x | TBD | |'; do
  name="${case%%|*}"; rest="${case#*|}"; want="${rest%%|*}"; cells="${rest#*|}"
  ledger "$TMP/$name.md" "| 1 | Fn | $cells"
  out="$(bash "$CHECK" "$TMP/$name.md" 2>&1)"; rc=$?
  { [ "$rc" = 1 ] && grep -qF "row 1: $want" <<<"$out"; } && ok "bare $name fails: $want" || bad "bare $name rc=$rc out=$out"
done

# Every good form passes, alone and in a comma list.
ledger "$TMP/good.md" \
  '| 1 | A | x | done | `docs/sindri/messages.md` |' \
  '| 2 | B | x | done | #3281 |' \
  '| 3 | C | limit | partial, planned M1 | |' \
  '| 4 | D | local | differs, planned M1 (local), M2 (opt-in sync) | |' \
  '| 5 | E | x | missing #3300 | |' \
  '| 6 | F | | dropped: admin-only | |' \
  '| 7 | G | no API **(guess; P0 line 7)** | blocked | |' \
  '| 8 | H | x | planned M4 (Release 3) | |'
out="$(bash "$CHECK" "$TMP/good.md" 2>&1)"; rc=$?
[ "$rc" = 0 ] && ok "every good status form passes" || bad "good forms rc=$rc out=$out"

# A bare part inside a comma list still fails.
ledger "$TMP/list.md" '| 1 | A | x | partial, planned | |'
out="$(bash "$CHECK" "$TMP/list.md" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -qF 'row 1: planned needs a phase' <<<"$out"; } && ok "a bare part of a comma list fails" || bad "list rc=$rc out=$out"

# Row numbers run 1..N: a gap or a repeat fails, so no row goes silently absent.
ledger "$TMP/gap.md" '| 1 | A | x | planned M1 | |' '| 3 | C | x | planned M1 | |'
out="$(bash "$CHECK" "$TMP/gap.md" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -qF 'row 3: expected row 2' <<<"$out"; } && ok "a gap in the row numbers fails" || bad "gap rc=$rc out=$out"
ledger "$TMP/dup.md" '| 1 | A | x | planned M1 | |' '| 1 | B | x | planned M1 | |'
out="$(bash "$CHECK" "$TMP/dup.md" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -qF 'row 1: expected row 2' <<<"$out"; } && ok "a repeated row number fails" || bad "dup rc=$rc out=$out"
ledger "$TMP/none.md"
out="$(bash "$CHECK" "$TMP/none.md" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -qF 'no ledger rows' <<<"$out"; } && ok "a ledger with no rows fails" || bad "no rows rc=$rc out=$out"
ledger "$TMP/cols.md" '| 1 | A | x | planned M1 |'
out="$(bash "$CHECK" "$TMP/cols.md" 2>&1)"; rc=$?
{ [ "$rc" = 1 ] && grep -qF 'row 1: 4 cells, expected 5' <<<"$out"; } && ok "a row with the wrong cell count fails" || bad "cols rc=$rc out=$out"

# --release is W6's pass: planned, differs and blocked rows fail; done, partial, missing #N and dropped pass.
out="$(bash "$CHECK" --release "$TMP/good.md" 2>&1)"; rc=$?
if [ "$rc" = 1 ] && grep -qF 'row 4: differs is not a release status' <<<"$out" \
   && grep -qF 'row 7: blocked is not a release status' <<<"$out" \
   && grep -qF 'row 8: planned is not a release status' <<<"$out" \
   && ! grep -qE 'row [1256]:' <<<"$out"; then
  ok "--release fails planned, differs and blocked, passes done, partial, missing and dropped"
else
  bad "--release rc=$rc out=$out"
fi
ledger "$TMP/shipped.md" '| 1 | A | x | done | #1 |' '| 2 | B | limit | partial | |' '| 3 | C | x | missing #2 | |' '| 4 | D | | dropped: rare | |'
bash "$CHECK" --release "$TMP/shipped.md" >/dev/null 2>&1 && ok "--release passes a shipped ledger" || bad "--release shipped failed"

bash "$CHECK" >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "no file exits 2" || bad "no file rc=$rc"
bash "$CHECK" "$TMP/absent.md" >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "a missing file exits 2" || bad "missing file rc=$rc"
bash "$CHECK" --bogus "$TMP/good.md" >/dev/null 2>&1; rc=$?
[ "$rc" = 2 ] && ok "an unknown flag exits 2" || bad "unknown flag rc=$rc"

[ "$fails" = 0 ] && echo "all passed" || { echo "$fails failed"; exit 1; }
