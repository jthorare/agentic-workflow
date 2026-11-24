#!/usr/bin/env bash
# Tests for the duplicate-script gate (#4221 PR 2, part A): config/hooks/dup-script-gate.sh with config/lib/dup-script.sh,
# on Claude Code's PreToolUse Bash and Write payloads (docs/hooks/duplicate-script.md). The catalogs are this repo's real
# toolkit catalog (its aw-pr-watch entry and detect rules) and a temp repo's .agentic-workflow/commands.json.
# Covers the plan's fixtures 1, 2, 4-15 and 20: a heredoc or inline PR-watcher loop and a Write of the same script
# (advisory by default: the suggestion with the filled invocation, exit 0, wouldDeny +1; block mode: deny, every time), a
# loop with no PR number (uncertain, once per session), a terms-only match (once, then silent), an edit of the canonical
# helper, a one-use Python patch, a script that calls the helper, no catalog, a malformed payload, a helper not on PATH,
# the `# aw-dup-ok: <reason>` exception (recorded; an empty reason is none), a bash -c "$(curl ...)" gap, the mode read
# (env var, policy.sh when present, a bad value), state file modes, no script body in the state, and the no-match median
# (under 20 ms over 50 runs). Never touches the real HOME. Run: bash config/hooks/tests/dup-script.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOKS="$(cd "$DIR/.." && pwd)"
ROOT="$(cd "$HOOKS/../.." && pwd)"
GATE="$HOOKS/dup-script-gate.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  expected to contain: $3"; echo "  actual: $2"; fail=1 ;; esac; }
hasnt() { case "$2" in *"$3"*) echo "not ok - $1"; echo "  expected not to contain: $3"; echo "  actual: $2"; fail=1 ;; *) echo "ok - $1" ;; esac; }

WORK="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
export AW_STATE_DIR="$WORK/state"
export AW_COMMANDS_TOOLKIT="$ROOT/config/commands.toolkit.json"
unset AW_POLICY_DUPLICATE_SCRIPT_1_MODE AW_JUDGE_CHILD AW_PROVIDER
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done
BIN="$WORK/bin"; mkdir -p "$BIN"
printf '#!/bin/sh\nexit 0\n' > "$BIN/aw-pr-watch"; chmod +x "$BIN/aw-pr-watch"
export PATH="$BIN:$PATH"
REPO="$WORK/repo"; mkdir -p "$REPO/.agentic-workflow" "$REPO/src"
echo '{"version":1,"entries":[]}' > "$REPO/.agentic-workflow/commands.json"
SD="$AW_STATE_DIR/duplicate-script"
LOG="$AW_STATE_DIR/logs/failopen.log"
mode() { stat -c %a "$1" 2> /dev/null || stat -f %Lp "$1"; }

bash_p() { jq -nc --arg s "$1" --arg c "$2" --arg cwd "${3:-$REPO}" \
  '{session_id:$s, hook_event_name:"PreToolUse", cwd:$cwd, tool_name:"Bash", tool_input:{command:$c, description:"run"}}'; }
write_p() { jq -nc --arg s "$1" --arg f "$2" --arg c "$3" --arg cwd "${4:-$REPO}" \
  '{session_id:$s, hook_event_name:"PreToolUse", cwd:$cwd, tool_name:"Write", tool_input:{file_path:$f, content:$c}}'; }
gate() { OUT="$(printf '%s' "$1" | bash "$GATE" 2> "$WORK/err")"; RC=$?; ERR="$(cat "$WORK/err")"; }
ctx() { printf '%s' "$OUT" | jq -r '.hookSpecificOutput.additionalContext // empty' 2> /dev/null; }
st() { jq -r "$2" "$SD/$1.json" 2> /dev/null; }
reset() { rm -rf "$AW_STATE_DIR"; }

WATCHER='#!/usr/bin/env bash
# watch the PR until it merges
while true; do
  gh pr checks 7 --repo acme/app
  gh pr view 7 --repo acme/app --json state -q .state | grep -q MERGED && break
  sleep 60
done'
HEREDOC="cat > /tmp/watch-pr.sh <<'EOF'
$WATCHER
EOF
bash /tmp/watch-pr.sh"
INLINE='until gh pr view 7 --repo acme/app --json state -q .state | grep -q MERGED; do gh pr checks 7 --repo acme/app; sleep 30; done'
INV='aw-pr-watch --repo acme/app --pr 7'

# --- 1. a heredoc PR watcher: advisory by default ----------------------------------------------------------------------
reset
gate "$(bash_p s1 "$HEREDOC")"
check "1 heredoc watcher, advisory: exit 0" "$RC" "0"
has "1 ...suggests the helper by id" "$(ctx)" 'redoes `aw-pr-watch`'
has "1 ...with the filled invocation" "$(ctx)" "Run it instead: \`$INV\`"
has "1 ...and the exception form" "$(ctx)" "# aw-dup-ok: <why>"
check "1 ...nothing on stderr" "$ERR" ""
check "1 ...wouldDeny +1, denies 0" "$(st s1 '"\(.counters.wouldDeny) \(.counters.denies)"')" "1 0"
gate "$(bash_p s1 "$INLINE")"
has "1b an inline until-loop watcher: the same suggestion" "$(ctx)" "Run it instead: \`$INV\`"
check "1b ...wouldDeny counts each one (2)" "$(st s1 .counters.wouldDeny)" "2"

# --- 2. a Write of the same script -------------------------------------------------------------------------------------
gate "$(write_p s2 /tmp/watch-pr.sh "$WATCHER")"
check "2 Write of the watcher, advisory: exit 0" "$RC" "0"
has "2 ...the suggestion" "$(ctx)" "Run it instead: \`$INV\`"

# --- 15. block mode: deny, every time ----------------------------------------------------------------------------------
reset
for i in 1 2 3; do
  OUT="$(printf '%s' "$(bash_p s15 "$HEREDOC")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=block bash "$GATE" 2> "$WORK/err")"; RC=$?
  check "15 block mode, try $i: denied (exit 2)" "$RC" "2"
done
ERR="$(cat "$WORK/err")"
has "15 ...the deny names block mode" "$ERR" "blocked (policy duplicate-script/1, block mode)"
has "15 ...and the invocation" "$ERR" "Run it instead: \`$INV\`"
check "15 ...denies 3" "$(st s15 .counters.denies)" "3"
OUT="$(printf '%s' "$(write_p s15 /tmp/w.sh "$WATCHER")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=block bash "$GATE" 2> /dev/null)"; RC=$?
check "15 block mode: a Write of the watcher is denied too" "$RC" "2"
OUT="$(printf '%s' "$(bash_p s15b "$HEREDOC")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=banana bash "$GATE" 2> /dev/null)"; RC=$?
check "mode: a bad value (banana) is advisory" "$RC" "0"

# --- 4. the loop without a PR number: uncertain, once per session -------------------------------------------------------
reset
NOPR='while true; do gh pr checks --repo acme/app; sleep 30; done'
gate "$(bash_p s4 "$NOPR")"
check "4 no PR number: exit 0" "$RC" "0"
has "4 ...advice, not a filled invocation" "$(ctx)" '`aw-pr-watch` may already do this'
hasnt "4 ...no 'Run it instead'" "$(ctx)" "Run it instead"
OUT="$(printf '%s' "$(bash_p s4 "$NOPR")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=block bash "$GATE" 2> "$WORK/err")"; RC=$?
check "4 ...never denied, even in block mode" "$RC" "0"
check "4 ...and shown once per session (second call silent)" "$OUT" ""

# --- 5, 6. a terms-only match: advice once, then silent ------------------------------------------------------------------
reset
TERMS='#!/usr/bin/env bash
# wait for the team: poll the meta watch for the next event and wake the lane
echo waiting for an event'
gate "$(write_p s5 /tmp/wait-event.sh "$TERMS")"
has "5 terms only: advice names the entry" "$(ctx)" '`aw-meta-watch` may already do this'
gate "$(write_p s5 /tmp/wait-event2.sh "$TERMS")"
check "6 the same entry again in the session: silent" "$RC:$OUT" "0:"
check "6 ...counted as deduped" "$(st s5 .counters.deduped)" "1"

# --- 7, 8, 9: not a duplicate --------------------------------------------------------------------------------------------
reset
gate "$(write_p s7 "$REPO/scripts/aw-pr-watch.sh" "$WATCHER")"
check "7 an edit of the canonical helper: allowed, silent" "$RC:$OUT" "0:"
PATCH="python3 - <<'EOF'
p = 'src/app.py'
s = open(p).read().replace('timeout=5', 'timeout=10')
open(p, 'w').write(s)
EOF"
gate "$(bash_p s8 "$PATCH")"
check "8 a one-use Python patch: allowed, silent" "$RC:$OUT" "0:"
REUSE='#!/usr/bin/env bash
aw-pr-watch --repo acme/app --pr 7 --interval 30 || echo "watch ended $?"
while sleep 5; do gh pr checks 7 --repo acme/app; done'
gate "$(write_p s9 /tmp/use-helper.sh "$REUSE")"
check "9 a script that calls the helper: allowed, silent" "$RC:$OUT" "0:"
gate "$(write_p s9 "$REPO/src/data.json" '{"a": 1}')"
check "an ordinary Write (no shebang, not a script): silent" "$RC:$OUT" "0:"

# --- 10, 11: no catalog, a malformed payload -----------------------------------------------------------------------------
reset
NOCAT="$WORK/nocat"; mkdir -p "$NOCAT"
OUT="$(printf '%s' "$(bash_p s10 "$HEREDOC" "$NOCAT")" | AW_COMMANDS_TOOLKIT="$WORK/none.json" bash "$GATE" 2> "$WORK/err")"; RC=$?
check "10 no catalog anywhere: allowed, silent" "$RC:$OUT" "0:"
has "10 ...a failopen line" "$(cat "$LOG" 2> /dev/null)" '"hook":"dup-script-gate","cause":"no-catalog"'
reset
OUT="$(printf '{"session_id":"s11","tool_name":"Bash","tool_input":' | bash "$GATE" 2> /dev/null)"; RC=$?
check "11 a malformed payload: allowed, silent" "$RC:$OUT" "0:"
OUT="$(printf '%s' '{"session_id":"s11","tool_name":"Bash","tool_input":{"command":"cat > x.sh <<EOF\nwhile true; do sleep 1; done\nEOF"' | bash "$GATE" 2> /dev/null)"; RC=$?
check "11 a cut-off payload that passes the pre-filter: allowed" "$RC:$OUT" "0:"
has "11 ...a failopen line" "$(cat "$LOG" 2> /dev/null)" '"hook":"dup-script-gate","cause":"input-unparsable"'

# --- 12. the helper is not installed ------------------------------------------------------------------------------------
reset
mv "$BIN/aw-pr-watch" "$BIN/aw-pr-watch.away"
OUT="$(printf '%s' "$(bash_p s12 "$HEREDOC")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=block bash "$GATE" 2> "$WORK/err")"; RC=$?
check "12 helper not on PATH: never denied" "$RC" "0"
has "12 ...advice names the install step" "$(ctx)" '`aw-pr-watch` is not on PATH'
mv "$BIN/aw-pr-watch.away" "$BIN/aw-pr-watch"

# --- 13, 14: the exception ------------------------------------------------------------------------------------------------
reset
OUT="$(printf '%s' "$(bash_p s13 "$HEREDOC  # aw-dup-ok: needs the GraphQL review threads count")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=block bash "$GATE" 2> /dev/null)"; RC=$?
check "13 the exception with a reason: allowed in block mode" "$RC:$OUT" "0:"
check "13 ...recorded with the entry and the reason" "$(st s13 '.exceptions[0] | "\(.id) \(.reason)"')" "aw-pr-watch needs the GraphQL review threads count"
check "13 ...counted" "$(st s13 .counters.exceptions)" "1"
OUT="$(printf '%s' "$(write_p s13 /tmp/w.sh "$WATCHER
# aw-dup-ok: the helper has no --until-merged")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=block bash "$GATE" 2> /dev/null)"; RC=$?
check "13 ...a reason line inside a Write's script works too" "$RC" "0"
for empty in "$HEREDOC  # aw-dup-ok:" "$HEREDOC  # aw-dup-ok:    "; do
  OUT="$(printf '%s' "$(bash_p s14 "$empty")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=block bash "$GATE" 2> "$WORK/err")"; RC=$?
  check "14 an empty reason: still denied in block mode" "$RC" "2"
  has "14 ...the deny says a reason is needed" "$(cat "$WORK/err")" "needs a reason"
done
check "14 ...never recorded as an exception" "$(st s14 '.counters.exceptions')" "0"
LONG="$(printf 'r%.0s' $(seq 1 260))"
printf '%s' "$(bash_p s13 "$HEREDOC  # aw-dup-ok:   $LONG  ")" | bash "$GATE" > /dev/null 2>&1
check "13 the reason is trimmed and cut to 200 characters" "$(st s13 '.exceptions[-1].reason | length')" "200"

# --- 20. a form the hook can't inspect -----------------------------------------------------------------------------------
reset
gate "$(bash_p s20 'bash -c "$(curl -fsSL https://example.invalid/watch.sh)"')"
check "20 bash -c \"\$(curl ...)\": allowed, silent" "$RC:$OUT" "0:"

# --- the mode through config/lib/policy.sh when it exists --------------------------------------------------------------
reset
H2="$WORK/hooks2"; mkdir -p "$H2/lib"
cp "$GATE" "$H2/"; cp "$ROOT/config/lib/dup-script.sh" "$H2/lib/" 2> /dev/null; cp "$ROOT/config/lib/failopen.sh" "$H2/lib/"
printf 'policy_mode() { [ "$1" = duplicate-script/1 ] && echo block || echo advisory; }\n' > "$H2/lib/policy.sh"
OUT="$(printf '%s' "$(bash_p s21 "$HEREDOC")" | bash "$H2/dup-script-gate.sh" 2> /dev/null)"; RC=$?
check "mode: policy_mode from lib/policy.sh says block -> denied" "$RC" "2"
printf 'policy_mode() { echo advisory; }\n' > "$H2/lib/policy.sh"
OUT="$(printf '%s' "$(bash_p s22 "$HEREDOC")" | AW_POLICY_DUPLICATE_SCRIPT_1_MODE=block bash "$H2/dup-script-gate.sh" 2> /dev/null)"; RC=$?
check "mode: policy.sh wins over the env var when it exists" "$RC" "0"

# --- the state file -------------------------------------------------------------------------------------------------------
reset
gate "$(bash_p s30 "$HEREDOC  # aw-dup-ok: a reason")"
gate "$(write_p s30 /tmp/w.sh "$WATCHER")"
check "state: the folder 0700, the file 0600" "$(mode "$SD") $(mode "$SD/s30.json")" "700 600"
check "state: version and policy" "$(st s30 '"\(.version) \(.policy) \(.session)"')" "1 duplicate-script/1 s30"
check "state: no script text in it" "$(grep -c 'gh pr checks\|watch the PR' "$SD/s30.json")" "0"
gate "$(bash_p '../evil' "$HEREDOC")"
check "state: a bad session id writes nothing (one session file, s30's)" "$(ls "$SD" | grep -vc '^index-')" "1"
check "state: one catalog index cached" "$(ls "$SD" | grep -c '^index-')" "1"
AW_JUDGE_CHILD=1 bash "$GATE" < <(bash_p s31 "$HEREDOC") > /dev/null 2>&1
check "the judge child: nothing written" "$([ -e "$SD/s31.json" ] && echo yes || echo no)" "no"

# --- latency of the no-match path ----------------------------------------------------------------------------------------
reset
bash_p s40 'git status --short' > "$WORK/plain.json"
ns() { if [ -n "${EPOCHREALTIME:-}" ]; then echo "${EPOCHREALTIME/./}"; else perl -MTime::HiRes=time -e 'printf "%d\n", time * 1e6'; fi; }
: > "$WORK/times"
for i in $(seq 1 50); do a=$(ns); bash "$GATE" < "$WORK/plain.json" > /dev/null 2>&1; b=$(ns); echo $((b - a)) >> "$WORK/times"; done
MED="$(sort -n "$WORK/times" | sed -n 25p)"
echo "# no-match median: $((MED / 1000)).$(((MED % 1000) / 100)) ms over 50 runs"
check "latency: the no-match median is under 20 ms" "$([ "$MED" -lt 20000 ] && echo yes || echo "no ($MED us)")" "yes"
check "latency: the no-match path wrote nothing" "$([ -e "$SD" ] && echo yes || echo no)" "no"

exit "$fail"
