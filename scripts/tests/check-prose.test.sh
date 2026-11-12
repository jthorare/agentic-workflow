#!/usr/bin/env bash
# Tests for scripts/check-prose.sh (issue 2919): one fixture per rule family under scripts/tests/fixtures/check-prose/
# gets its hits and exit code, --strict, --counts, --base on a scratch git repo (only added lines count), and the
# refusals (usage, vale missing). Runs the real pinned vale: $AW_VALE_BIN when set, else scripts/binaries-lock.sh
# fetch vale into the temp dir; without the network the vale cases SKIP and the refusals still run.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$REPO/scripts/check-prose.sh"
FIX="scripts/tests/fixtures/check-prose"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-prose-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

run() { set +e; OUT="$(cd "$REPO" && bash "$SCRIPT" "$@" 2>&1)"; RC=$?; set -e; }
expect() {   # <label> <rc> [<regex>...]: every regex must match a line of the output
  local label="$1" rc="$2" re; shift 2
  if [ "$RC" -ne "$rc" ]; then fail "$label: exit $RC, want $rc"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  for re in "$@"; do
    if ! printf '%s\n' "$OUT" | has -E -- "$re"; then fail "$label: output lacks /$re/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  done
  ok "$label"
}
lacks() {   # <label> <regex>: no line of the output matches
  if printf '%s\n' "$OUT" | has -E -- "$2"; then fail "$1: output has /$2/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; else ok "$1"; fi
}

echo "check-prose refusals"

run --bogus
expect "an unknown option is a usage error" 2 'unknown option --bogus'
run --base
expect "--base without a ref is a usage error" 2 'Usage'

set +e
OUT="$(cd "$REPO" && AW_VALE_BIN="$TMPROOT/no-such-vale" bash "$SCRIPT" "$FIX/clean.md" 2>&1)"; RC=$?
set -e
expect "an AW_VALE_BIN that is not an executable exits 2" 2 'AW_VALE_BIN=.* is not an executable'

# vale missing: no AW_VALE_BIN, an empty state dir, and a PATH with only git, jq and the base system.
mkdir -p "$TMPROOT/tools" "$TMPROOT/empty-state"
for t in git jq; do ln -s "$(command -v "$t")" "$TMPROOT/tools/$t"; done
if PATH="$TMPROOT/tools:/usr/bin:/bin" command -v vale >/dev/null 2>&1; then
  echo "  SKIP vale missing: a vale sits in /usr/bin or /bin"
else
  set +e
  OUT="$(cd "$REPO" && env -u AW_VALE_BIN PATH="$TMPROOT/tools:/usr/bin:/bin" AW_STATE_DIR="$TMPROOT/empty-state" \
    bash "$SCRIPT" "$FIX/clean.md" 2>&1)"; RC=$?
  set -e
  expect "vale missing exits 2 and names the fetch" 2 'vale is missing; run scripts/binaries-lock.sh fetch vale'
fi

# The real vale for the rest.
if [ -z "${AW_VALE_BIN:-}" ]; then
  if AW_VALE_BIN="$(bash "$REPO/scripts/binaries-lock.sh" fetch vale --dest "$TMPROOT/bin" 2>/dev/null)"; then
    export AW_VALE_BIN
  else
    echo "  SKIP the vale cases: scripts/binaries-lock.sh fetch vale failed (no network?)"
    echo "check-prose: $PASS passed, $FAIL failed"
    [ "$FAIL" -eq 0 ]; exit
  fi
fi

echo "check-prose rule families"

run "$FIX/clean.md"
expect "the clean fixture passes with no hits (a file handle, managed code, elevated privileges, an unlock prompt)" 0 \
  '1 files, 0 errors, 0 warnings'

run "$FIX/vague-verbs.md"
expect "vague verbs are errors" 1 \
  "^$FIX/vague-verbs.md:3:13: error Aw.VagueVerbs: 'handles' says nothing" \
  "^$FIX/vague-verbs.md:5:11: error Aw.VagueVerbs: 'manages'"

run "$FIX/hedging.md"
expect "hedges are errors" 1 "hedging.md:3:1: error Aw.Hedging: 'It seems that' hedges" "hedging.md:5:12: error Aw.Hedging: 'perhaps'"

run "$FIX/slop.md"
expect "slop words, wordy forms and filler are errors" 1 \
  "slop.md:3:18: error Aw.Slop: 'robust'" \
  "slop.md:5:18: error Aw.Wordy: Use 'to' instead of 'in order to'" \
  "slop.md:7:12: error Aw.Filler: Cut 'very'"

run "$FIX/dashes.md"
expect "two dashes in one sentence are an error" 1 'dashes.md:3:[0-9]+: error Aw.DashRuns'
lacks "an en dash in a number range is no dash run" 'dashes.md:5:'

run "$FIX/structure.md"
expect "the 80% rules are warnings and don't fail" 0 \
  'structure.md:3:1: warning Aw.SentenceLength' \
  "structure.md:5:[0-9]+: warning Aw.Passive: 'is written'" \
  'structure.md:7:[0-9]+: warning Aw.NounStack' \
  'structure.md:9:[0-9]+: warning Aw.ParagraphLength' \
  '0 errors, 4 warnings'
run --strict "$FIX/structure.md"
expect "--strict fails on warnings" 1 '0 errors, 4 warnings'

run "$FIX/front-matter.md"
expect "front matter Vale can't parse is an error on line 1" 1 'front-matter.md:1:1: error Aw.FrontMatter: Vale cannot parse this front matter'

run "$FIX/clean.md" "$FIX/vague-verbs.md" "$FIX/slop.md"
expect "several files go through one run" 1 '3 files, 5 errors, 0 warnings'
first="$(printf '%s\n' "$OUT" | head -1)"
case "$first" in "$FIX/slop.md:3:"*) ok "hits are sorted by file, then line" ;; *) fail "first hit is $first" ;; esac

run --counts "$FIX"
expect "--counts prints hits per directory" 1 '^scripts/tests [0-9]+ errors, [0-9]+ warnings$'
lacks "--counts prints no single hits" ': error Aw\.'

echo "check-prose --base (a scratch repo)"

S="$TMPROOT/repo"
mkdir -p "$S/scripts" "$S/docs"
cp "$SCRIPT" "$S/scripts/"; cp "$REPO/.vale.ini" "$REPO/binaries.lock" "$S/"; cp -R "$REPO/.vale" "$S/"
printf '# Old\n\nThe old service handles the retry.\n' >"$S/docs/old.md"
printf '# Kept\n\nThe kept service handles the retry.\n' >"$S/docs/kept.md"
git -C "$S" init -q; git -C "$S" add -A
git -C "$S" -c user.name=t -c user.email=t@example.invalid commit -q -m base
printf '\nThe new line perhaps hedges.\n' >>"$S/docs/old.md"
printf '# New\n\nThe new store is robust.\n' >"$S/docs/new.md"
printf 'The planning note manages it.\n' >"$S/notes.md"

set +e; OUT="$(cd "$S" && bash scripts/check-prose.sh --base HEAD 2>&1)"; RC=$?; set -e
expect "--base reports hits on added lines and in new files" 1 \
  "^docs/old.md:5:[0-9]+: error Aw.Hedging: 'perhaps'" "^docs/new.md:3:[0-9]+: error Aw.Slop: 'robust'"
lacks "--base drops a hit on an unchanged line" '^docs/old.md:3:'
lacks "--base skips an unchanged file" '^docs/kept.md'
lacks "--base keeps to the default scope" '^notes.md'

printf -- '---\nname: bad\nhint: [a] [--b <c,...>]\n---\n\n# Bad\n\nThe old body.\n' >"$S/docs/bad-fm.md"
printf '# Moved\n\nThe moved service handles the retry.\n' >"$S/docs/moved-old.md"
git -C "$S" add -A; git -C "$S" -c user.name=t -c user.email=t@example.invalid commit -q -m more
B2="$(git -C "$S" rev-parse HEAD)"
printf '\nThe new body perhaps hedges.\n' >>"$S/docs/bad-fm.md"
git -C "$S" mv docs/moved-old.md docs/moved-new.md
printf '\nThe moved line perhaps hedges.\n' >>"$S/docs/moved-new.md"
set +e; OUT="$(cd "$S" && bash scripts/check-prose.sh --base "$B2" 2>&1)"; RC=$?; set -e
expect "--base keeps a front-matter error when the PR edits only the body" 1 '^docs/bad-fm.md:1:1: error Aw.FrontMatter'
expect "--base counts a renamed file's changed lines" 1 "^docs/moved-new.md:5:[0-9]+: error Aw.Hedging: 'perhaps'"
lacks "--base drops a renamed file's old hits" '^docs/moved-new.md:3:'
git -C "$S" checkout -q -- docs/bad-fm.md; git -C "$S" rm -q --cached docs/bad-fm.md; rm -f "$S/docs/bad-fm.md"

git -C "$S" -c user.name=t -c user.email=t@example.invalid commit -q -am more
git -C "$S" checkout -q -- . 2>/dev/null || true
rm -f "$S/docs/new.md" "$S/notes.md"
set +e; OUT="$(cd "$S" && bash scripts/check-prose.sh --base HEAD 2>&1)"; RC=$?; set -e
expect "--base with nothing changed passes" 0
set +e; OUT="$(cd "$S" && bash scripts/check-prose.sh --base no-such-ref 2>&1)"; RC=$?; set -e
expect "--base on an unknown ref exits 2" 2 'is not a commit'

echo "check-prose: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
