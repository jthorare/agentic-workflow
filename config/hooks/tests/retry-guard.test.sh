#!/usr/bin/env bash
# Tests for the retry guard (issue 4248): config/hooks/retry-record.sh (post-tool) and config/hooks/retry-gate.sh (pre-tool),
# sharing $AW_STATE_DIR/retry-guard/<session>.json (schema: docs/hooks/retry-guard.md).
# Covers each kind (missing-exe, exit 127, auth-expired, bad-args, missing-path, transient, unknown), a changed argument
# passing, a fixed prerequisite clearing the problem, success clearing it, polling successes never recorded, an interrupt
# never recorded, session reset, corrupted and old-version state failing open, no raw input or output in the state (a
# planted token), the override, a bad session id, parallel writers, file modes, the judge child, the empty-path latency
# (median under 20 ms over 50 runs) and the Claude, Codex and Cursor payloads (fixtures/<provider>/, through the adapters).
# Never touches the real HOME. Run: bash config/hooks/tests/retry-guard.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOKS="$(cd "$DIR/.." && pwd)"
GATE="$HOOKS/retry-gate.sh"
RECORD="$HOOKS/retry-record.sh"
FIX="$DIR/fixtures"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() {
  case "$2" in *"$3"*) echo "ok - $1" ;; *)
    echo "not ok - $1"; echo "  expected to contain: $3"; echo "  actual: $2"; fail=1 ;;
  esac
}
hasnt() {
  case "$2" in *"$3"*) echo "not ok - $1"; echo "  expected not to contain: $3"; echo "  actual: $2"; fail=1 ;;
    *) echo "ok - $1" ;;
  esac
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
export AW_STATE_DIR="$WORK/state"
export GH_CONFIG_DIR="$WORK/gh"; mkdir -p "$GH_CONFIG_DIR"; echo "github.com: {}" > "$GH_CONFIG_DIR/hosts.yml"
unset AW_JUDGE_CHILD AW_PROVIDER AW_HOOK_PROVIDER AW_RETRY_GUARD_NOW CURSOR_TRANSCRIPT_PATH CURSOR_PROJECT_DIR
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done   # a lane caller's AW_META_* would steer the adapters' lane hooks
BIN="$WORK/bin"; mkdir -p "$BIN"
export PATH="$BIN:$PATH"
# The rows below expect today's deny path; the guard ships advisory (#4257), so they turn block mode on. The
# "advisory default" section runs without it.
export AW_POLICY_RETRY_GUARD_1_MODE=block
RG="$AW_STATE_DIR/retry-guard"
LOG="$AW_STATE_DIR/logs/failopen.log"
mode() { stat -c %a "$1" 2> /dev/null || stat -f %Lp "$1"; }

# Payload builders (Claude Code's shapes, as the 2.1.331 binary sends them).
# pre <sid> <tool> <tool_input json>
pre() { jq -nc --arg s "$1" --arg t "$2" --argjson i "$3" --arg cwd "$WORK" \
  '{session_id:$s, hook_event_name:"PreToolUse", cwd:$cwd, tool_name:$t, tool_input:$i, tool_use_id:"toolu_x"}'; }
# failure <sid> <tool> <tool_input json> <error text> [is_interrupt]
failure() { jq -nc --arg s "$1" --arg t "$2" --argjson i "$3" --arg e "$4" --argjson intr "${5:-false}" --arg cwd "$WORK" \
  '{session_id:$s, hook_event_name:"PostToolUseFailure", cwd:$cwd, tool_name:$t, tool_input:$i, tool_use_id:"toolu_x",
    error:$e, is_interrupt:$intr, duration_ms:5}'; }
# success <sid> <tool> <tool_input json>
success() { jq -nc --arg s "$1" --arg t "$2" --argjson i "$3" --arg cwd "$WORK" \
  '{session_id:$s, hook_event_name:"PostToolUse", cwd:$cwd, tool_name:$t, tool_input:$i, tool_use_id:"toolu_x",
    tool_response:{stdout:"fine", stderr:"", interrupted:false}}'; }
bash_in() { jq -nc --arg c "$1" '{command:$c, description:"run"}'; }
# gate <payload>: sets RC, OUT, ERR
gate() { OUT="$(printf '%s' "$1" | bash "$GATE" 2> "$WORK/err")"; RC=$?; ERR="$(cat "$WORK/err")"; }
record() { printf '%s' "$1" | bash "$RECORD" > "$WORK/rec.out" 2> "$WORK/rec.err"; RRC=$?; }
st() { jq -r "$2" "$RG/$1.json" 2> /dev/null; }
ctx() { printf '%s' "$OUT" | jq -r '.hookSpecificOutput.additionalContext // empty' 2> /dev/null; }
reset() { rm -rf "$AW_STATE_DIR"; }

# --- the empty path ---------------------------------------------------------------------------------------------------------
reset
gate "$(pre s-empty Bash "$(bash_in 'ls -la')")"
check "empty: no state file -> exit 0" "$RC" "0"
check "empty: prints nothing" "$OUT$ERR" ""
check "empty: writes nothing" "$([ -e "$RG" ] && echo yes || echo no)" "no"

# --- missing-exe ------------------------------------------------------------------------------------------------------------
reset
CALL="$(bash_in 'aw-nosuch-tool --version')"
record "$(failure s-exe Bash "$CALL" $'/bin/bash: line 1: aw-nosuch-tool: command not found')"
check "missing-exe: record exits 0" "$RRC" "0"
check "missing-exe: record prints nothing" "$(cat "$WORK/rec.out")" ""
check "missing-exe: the kind" "$(st s-exe '.problems[].kind')" "missing-exe"
check "missing-exe: the resource is the exe" "$(st s-exe '.problems[].resource')" "aw-nosuch-tool"
check "missing-exe: the problem key" "$(st s-exe '.problems[].key')" "s-exe|missing-exe|aw-nosuch-tool|retry-guard/1"
check "missing-exe: version and policy" "$(st s-exe '"\(.version) \(.policy) \(.session)"')" "1 retry-guard/1 s-exe"
check "missing-exe: failures 1" "$(st s-exe '.counters.failures')" "1"
gate "$(pre s-exe Bash "$CALL")"
check "missing-exe: the same call is denied (exit 2)" "$RC" "2"
has "missing-exe: the deny names the exe and the fix" "$ERR" '`aw-nosuch-tool` is not installed'
has "missing-exe: the deny names the override" "$ERR" "# aw-retry:"
check "missing-exe: denies counted" "$(st s-exe '"\(.counters.denies) \(.problems[].denies)"')" "1 1"
gate "$(pre s-exe Bash "$(bash_in 'ls -la')")"
check "missing-exe: another call passes" "$RC:$OUT" "0:"
printf '#!/bin/sh\necho 1.0\n' > "$BIN/aw-nosuch-tool"; chmod +x "$BIN/aw-nosuch-tool"
gate "$(pre s-exe Bash "$CALL")"
check "missing-exe: once installed, the call passes" "$RC" "0"
check "missing-exe: ...and the problem is cleared" "$(st s-exe '.problems | length')" "0"
check "missing-exe: ...counted as cleared after change" "$(st s-exe '"\(.counters.cleared) \(.counters.clearedAfterChange)"')" "1 1"
rm -f "$BIN/aw-nosuch-tool"

reset
record "$(failure s-127 Bash "$(bash_in 'aw-other-missing -x')" $'Exit code 127\nsomething went wrong')"
check "exit 127: missing-exe from the first word" "$(st s-127 '"\(.problems[].kind) \(.problems[].resource)"')" "missing-exe aw-other-missing"

# --- bad-args ---------------------------------------------------------------------------------------------------------------
reset
BAD="$(bash_in 'git log --bogus-flag')"
record "$(failure s-bad Bash "$BAD" $'Exit code 129\nerror: unknown option `bogus-flag\'\nusage: git log [<options>]')"
check "bad-args: the kind" "$(st s-bad '.problems[].kind')" "bad-args"
check "bad-args: the resource is the call hash" "$(st s-bad '.problems | to_entries[0] | .key == .value.resource')" "true"
check "bad-args: the hash is 64 hex" "$(st s-bad '.problems | keys[0] | test("^[0-9a-f]{64}$")')" "true"
gate "$(pre s-bad Bash "$BAD")"
check "bad-args: the same call is denied" "$RC" "2"
has "bad-args: the deny says to change the arguments" "$ERR" "Change the arguments"
gate "$(pre s-bad Bash "$(bash_in 'git log --oneline')")"
check "bad-args: a changed argument passes" "$RC:$OUT" "0:"
record "$(success s-bad Bash "$BAD")"
check "bad-args: success of the same call clears it" "$(st s-bad '.problems | length')" "0"
check "bad-args: ...counted (cleared 1, after change 1)" "$(st s-bad '"\(.counters.cleared) \(.counters.clearedAfterChange)"')" "1 1"
reset
MCP='{"owner":"acme","repo":"app","pullNumber":"x"}'
record "$(failure s-badmcp mcp__github__get_pull_request "$MCP" 'invalid argument: pullNumber must be a number')"
gate "$(pre s-badmcp mcp__github__get_pull_request "$MCP")"
check "bad-args, non-Bash tool: advice only (exit 0)" "$RC" "0"
has "bad-args, non-Bash tool: the advice" "$(ctx)" "retry-guard:"
check "bad-args, non-Bash tool: advisories counted" "$(st s-badmcp '.counters.advisories')" "1"

# --- missing-path -----------------------------------------------------------------------------------------------------------
reset
P="$WORK/data/report.txt"
PC="$(bash_in "cat $P")"
record "$(failure s-path Bash "$PC" "Exit code 1
cat: $P: No such file or directory")"
check "missing-path: the kind and the path" "$(st s-path '"\(.problems[].kind) \(.problems[].resource)"')" "missing-path $P"
gate "$(pre s-path Bash "$PC")"
check "missing-path: denied while the path is missing" "$RC" "2"
has "missing-path: the deny names the path" "$ERR" "$P"
mkdir -p "$WORK/data"; echo hi > "$P"
gate "$(pre s-path Bash "$PC")"
check "missing-path: once it exists, the call passes" "$RC" "0"
check "missing-path: ...and the problem is cleared" "$(st s-path '.problems | length')" "0"
reset
record "$(failure s-relpath Bash "$(bash_in 'ls nope-dir')" "ls: cannot access 'nope-dir': No such file or directory")"
check "missing-path: a relative path is made absolute from cwd" "$(st s-relpath '.problems[].resource')" "$WORK/nope-dir"

# --- auth-expired -----------------------------------------------------------------------------------------------------------
reset
AC="$(bash_in 'gh pr view 7')"
record "$(failure s-auth Bash "$AC" $'Exit code 1\nHTTP 401: Bad credentials (https://api.github.com/graphql)\nTry authenticating with:  gh auth login')"
check "auth: the kind and the host" "$(st s-auth '"\(.problems[].kind) \(.problems[].resource)"')" "auth-expired github.com"
check "auth: the auth file's mtime is kept" "$(st s-auth '.problems[].authStamp | test("^[0-9]+$")')" "true"
gate "$(pre s-auth Bash "$AC")"
check "auth: the first repeat is denied" "$RC" "2"
has "auth: the deny names the login" "$ERR" "gh auth login"
gate "$(pre s-auth Bash "$AC")"
check "auth: the second repeat is advice, not another deny" "$RC" "0"
has "auth: ...with the advice" "$(ctx)" "gh auth login"
touch -d '+5 seconds' "$GH_CONFIG_DIR/hosts.yml" 2> /dev/null || touch -t "$(date -v+5S +%Y%m%d%H%M.%S 2> /dev/null)" "$GH_CONFIG_DIR/hosts.yml"
gate "$(pre s-auth Bash "$AC")"
check "auth: a changed auth file lets it through" "$RC:$OUT" "0:"
check "auth: ...and clears the problem" "$(st s-auth '.problems | length')" "0"
reset
record "$(failure s-auth2 Bash "$AC" 'HTTP 401: Bad credentials')"
AW_RETRY_GUARD_NOW=$(( $(date +%s) + 601 )) gate "$(pre s-auth2 Bash "$AC")"
check "auth: after 10 min, advice, never a deny" "$RC" "0"

# --- transient --------------------------------------------------------------------------------------------------------------
reset
TC="$(bash_in 'curl -s https://example.invalid/x')"
record "$(failure s-tr Bash "$TC" 'curl: (6) Could not resolve host: example.invalid')"
check "transient: the kind" "$(st s-tr '.problems[].kind')" "transient"
gate "$(pre s-tr Bash "$TC")"
check "transient: never denied" "$RC" "0"
has "transient: advice to wait 30 s" "$(ctx)" "wait 30 s"
record "$(failure s-tr Bash "$TC" 'curl: (28) Operation timed out after 30000 milliseconds')"
gate "$(pre s-tr Bash "$TC")"
has "transient: the wait doubles" "$(ctx)" "wait 60 s"
AW_RETRY_GUARD_NOW=$(( $(date +%s) + 61 )) gate "$(pre s-tr Bash "$TC")"
check "transient: past the wait, no advice" "$RC:$OUT" "0:"
for i in 1 2 3 4 5 6; do record "$(failure s-tr Bash "$TC" 'HTTP 503 Service Unavailable')"; done
gate "$(pre s-tr Bash "$TC")"
has "transient: the wait stops at 300 s" "$(ctx)" "wait 300 s"

# --- unknown ----------------------------------------------------------------------------------------------------------------
reset
UC="$(bash_in 'make check')"
record "$(failure s-unk Bash "$UC" $'Exit code 2\nmake: *** [check] Error 2')"
check "unknown: the kind" "$(st s-unk '.problems[].kind')" "unknown"
gate "$(pre s-unk Bash "$UC")"
check "unknown: after 1 failure, nothing" "$RC:$OUT" "0:"
record "$(failure s-unk Bash "$UC" $'Exit code 2\nmake: *** [check] Error 2')"
gate "$(pre s-unk Bash "$UC")"
check "unknown: after 2 failures, nothing" "$RC:$OUT" "0:"
record "$(failure s-unk Bash "$UC" $'Exit code 2\nmake: *** [check] Error 2')"
gate "$(pre s-unk Bash "$UC")"
check "unknown: after 3, never denied" "$RC" "0"
has "unknown: after 3, advice" "$(ctx)" "failed 3 times"

# --- successes and interrupts are never recorded -----------------------------------------------------------------------------
reset
for i in $(seq 1 10); do record "$(success s-poll Bash "$(bash_in 'gh run view 12 --json status')")"; done
check "polling: 10 successes write nothing" "$([ -e "$RG/s-poll.json" ] && echo yes || echo no)" "no"
record "$(failure s-intr Bash "$(bash_in 'sleep 100')" 'Interrupted by user' true)"
check "interrupt: never recorded" "$([ -e "$RG/s-intr.json" ] && echo yes || echo no)" "no"

# --- session reset ----------------------------------------------------------------------------------------------------------
reset
record "$(failure s-one Bash "$CALL" 'aw-nosuch-tool: command not found')"
gate "$(pre s-two Bash "$CALL")"
check "session reset: a new session id is not denied" "$RC:$OUT" "0:"

# --- corrupted and old state ------------------------------------------------------------------------------------------------
reset
mkdir -p "$RG"; printf 'not json {' > "$RG/s-bad.json"
gate "$(pre s-bad Bash "$CALL")"
check "corrupted: the gate allows" "$RC:$OUT" "0:"
has "corrupted: a failopen line" "$(cat "$LOG" 2> /dev/null)" '"hook":"retry-gate","cause":"state-unreadable"'
check "corrupted: the file is removed" "$([ -e "$RG/s-bad.json" ] && echo yes || echo no)" "no"
printf '{"version":1' > "$RG/s-bad2.json"
record "$(failure s-bad2 Bash "$CALL" 'aw-nosuch-tool: command not found')"
check "corrupted: record starts a new file" "$(st s-bad2 '"\(.version) \(.problems | length)"')" "1 1"
printf '{"version":99,"policy":"retry-guard/9","problems":{}}' > "$RG/s-old.json"
gate "$(pre s-old Bash "$CALL")"
check "old version: allowed" "$RC:$OUT" "0:"

# --- no raw input or output in the state ------------------------------------------------------------------------------------
reset
record "$(failure s-red Bash "$(bash_in 'aw-nosuch-tool --token PLANTED-INPUT-7f3a')" 'aw-nosuch-tool: command not found PLANTED-OUTPUT-9c1e')"
record "$(failure s-red Bash "$(bash_in 'git push --bogus PLANTED-INPUT-7f3a')" 'unknown flag: --bogus PLANTED-OUTPUT-9c1e')"
record "$(failure s-red Bash "$(bash_in 'deploy PLANTED-INPUT-7f3a')" 'it broke PLANTED-OUTPUT-9c1e')"
check "redaction: 3 problems recorded" "$(st s-red '.problems | length')" "3"
check "redaction: the input token is not in the state" "$(grep -c PLANTED-INPUT "$RG/s-red.json")" "0"
check "redaction: the output token is not in the state" "$(grep -c PLANTED-OUTPUT "$RG/s-red.json")" "0"

# --- the override -----------------------------------------------------------------------------------------------------------
reset
record "$(failure s-ovr Bash "$CALL" 'aw-nosuch-tool: command not found')"
gate "$(pre s-ovr Bash "$(bash_in 'aw-nosuch-tool --version # aw-retry: checking the installer put it on PATH')")"
check "override: allowed" "$RC" "0"
check "override: recorded with its reason" "$(st s-ovr '.overrides[0] | "\(.kind) \(.reason)"')" "missing-exe checking the installer put it on PATH"
check "override: the hash is the plain call's" "$(st s-ovr '.overrides[0].hash == (.problems | keys[0])')" "true"
check "override: counted" "$(st s-ovr '.counters.overrides')" "1"
check "override: the problem stays open" "$(st s-ovr '.problems | length')" "1"

for empty in 'aw-nosuch-tool --version # aw-retry:' 'aw-nosuch-tool --version # aw-retry:    '; do
  gate "$(pre s-ovr Bash "$(bash_in "$empty")")"
  check "override, empty reason ('${empty#*--version }'): still denied" "$RC" "2"
  has "override, empty reason: the deny says a reason is needed" "$ERR" "needs a reason"
done
check "override, empty reason: never recorded as an override" "$(st s-ovr '"\(.counters.overrides) \(.overrides | length)"')" "1 1"
LONG="$(printf 'x%.0s' $(seq 1 250))"
gate "$(pre s-ovr Bash "$(bash_in "aw-nosuch-tool --version # aw-retry:    $LONG   ")")"
check "override: the reason is trimmed and cut to 200 characters" "$(st s-ovr '.overrides[-1].reason | "\(length) \(startswith("x")) \(endswith("x"))"')" "200 true true"

# --- the error hash: same failure again vs new state each time -------------------------------------------------------------
reset
EC="$(bash_in 'make poll')"
record "$(failure s-eh Bash "$EC" 'still waiting: build 41 of 50')"
check "error hash: 64 hex" "$(st s-eh '.problems[].errorHash | test("^[0-9a-f]{64}$")')" "true"
check "error hash: one distinct error" "$(st s-eh '.problems[].distinctErrors')" "1"
record "$(failure s-eh Bash "$EC" 'still waiting: build 41 of 50')"
check "error hash: the same text again stays 1" "$(st s-eh '.problems[].distinctErrors')" "1"
record "$(failure s-eh Bash "$EC" 'still waiting: build 42 of 50')"
check "error hash: new text counts 2" "$(st s-eh '.problems[].distinctErrors')" "2"
record "$(failure s-eh Bash "$EC" 'still waiting: build 41 of 50')"
check "error hash: an earlier text again stays 2" "$(st s-eh '"\(.problems[].distinctErrors) \(.problems[].failures)"')" "2 4"
check "error hash: the text itself is not stored" "$(grep -c 'still waiting' "$RG/s-eh.json")" "0"

# --- 401 only as an HTTP status ---------------------------------------------------------------------------------------------
reset
record "$(failure s-401 Bash "$(bash_in 'node build.js')" 'SyntaxError: unexpected token, error at line 401')"
check "401: \"error at line 401\" is unknown, not auth" "$(st s-401 '.problems[].kind')" "unknown"
reset
record "$(failure s-401b Bash "$(bash_in 'curl -sf https://api.example.com/x')" 'request failed with status 401')"
check "401: \"status 401\" is auth-expired" "$(st s-401b '"\(.problems[].kind) \(.problems[].resource)"')" "auth-expired example.com"
reset
record "$(failure s-401c Bash "$(bash_in 'curl -sf https://api.example.com/y')" 'curl: (22) The requested URL returned error: 401 Unauthorized')"
check "401: \"401 Unauthorized\" is auth-expired" "$(st s-401c '.problems[].kind')" "auth-expired"

# --- a bad session id, the judge child, file modes, parallel writers --------------------------------------------------------
reset
record "$(failure '../evil' Bash "$CALL" 'aw-nosuch-tool: command not found')"
check "bad session id: nothing written" "$(find "$AW_STATE_DIR" -type f 2> /dev/null | wc -l | tr -d ' ')" "0"
AW_JUDGE_CHILD=1 record "$(failure s-child Bash "$CALL" 'aw-nosuch-tool: command not found')"
check "judge child: nothing written" "$([ -e "$RG/s-child.json" ] && echo yes || echo no)" "no"
record "$(failure s-mode Bash "$CALL" 'aw-nosuch-tool: command not found')"
check "modes: the folder 0700, the file 0600" "$(mode "$RG") $(mode "$RG/s-mode.json")" "700 600"
pids=""
for i in 1 2 3 4 5 6; do
  printf '%s' "$(failure s-par Bash "$(bash_in "aw-missing-$i")" "aw-missing-$i: command not found")" | bash "$RECORD" > /dev/null 2>&1 &
  pids="$pids $!"
done
for p in $pids; do wait "$p"; done
check "parallel: 6 writers, 6 problems" "$(st s-par '.problems | length')" "6"
check "parallel: no temp or lock left" "$(find "$RG" -name '*.tmp.*' -o -name '*.lock' | wc -l | tr -d ' ')" "0"

# --- provider payloads ------------------------------------------------------------------------------------------------------
reset
record "$(cat "$FIX/claude/post-tool-use-failure-bash.json")"
check "claude fixture: recorded" "$(st claude-fixture-1 '.problems[].kind')" "missing-exe"
gate "$(cat "$FIX/claude/pre-tool-use-bash.json")"
check "claude fixture: the pre-tool call is denied" "$RC" "2"

reset
bash "$HOOKS/adapters/codex.sh" "$RECORD" < "$FIX/codex/posttooluse-bash-exit127.json" > /dev/null 2>&1
check "codex: an exit-127 PostToolUse is recorded" "$(st codex-fixture-1 '"\(.provider) \(.problems[].kind) \(.problems[].resource)"')" "codex missing-exe aw-nosuch-tool"
OUT="$(bash "$HOOKS/adapters/codex.sh" "$GATE" < "$FIX/codex/pretooluse-bash-nosuch.json" 2> "$WORK/err")"; RC=$?
check "codex: the same call is denied (exit 2)" "$RC" "2"
has "codex: ...with the reason on stderr" "$(cat "$WORK/err")" "is not installed"
bash "$HOOKS/adapters/codex.sh" "$RECORD" < "$FIX/codex/posttooluse-bash-exit0.json" > /dev/null 2>&1
check "codex: an exit-0 PostToolUse clears it" "$(st codex-fixture-1 '.problems | length')" "0"

reset
OUT="$(bash "$HOOKS/adapters/cursor.sh" "$RECORD" < "$FIX/cursor/post-tool-use-failure-shell.json" 2> /dev/null)"
check "cursor: postToolUseFailure prints valid JSON" "$(printf '%s' "$OUT" | jq -e 'type == "object"' > /dev/null 2>&1 && echo yes || echo no)" "yes"
check "cursor: ...and is recorded from error_message" "$(st cursor-fixture-1 '"\(.provider) \(.problems[].kind)"')" "cursor missing-exe"
OUT="$(bash "$HOOKS/adapters/cursor.sh" "$GATE" < "$FIX/cursor/pretooluse-shell-nosuch.json" 2> /dev/null)"
check "cursor: preToolUse of the same call is denied" "$(printf '%s' "$OUT" | jq -r .permission)" "deny"
has "cursor: ...with the reason for the agent" "$(printf '%s' "$OUT" | jq -r .agent_message)" "is not installed"
bash "$HOOKS/adapters/cursor.sh" "$RECORD" < "$FIX/cursor/post-tool-use-failure-shell-timeout.json" > /dev/null 2>&1
OUT="$(bash "$HOOKS/adapters/cursor.sh" "$GATE" < "$FIX/cursor/pretooluse-shell-timeout.json" 2> /dev/null)"
check "cursor: a transient repeat is allowed" "$(printf '%s' "$OUT" | jq -r .permission)" "allow"
has "cursor: ...with the advice as agent_message" "$(printf '%s' "$OUT" | jq -r '.agent_message // empty')" "wait 30 s"

# --- the advisory default (#4257): a would-be deny is advice, exit 0, counters.wouldDeny +1 ------------------------------
for mode in unset banana; do
  reset
  record "$(failure s-adv Bash "$CALL" 'aw-nosuch-tool: command not found')"
  if [ "$mode" = unset ]; then
    OUT="$(printf '%s' "$(pre s-adv Bash "$CALL")" | env -u AW_POLICY_RETRY_GUARD_1_MODE bash "$GATE" 2> "$WORK/err")"; RC=$?
  else
    OUT="$(printf '%s' "$(pre s-adv Bash "$CALL")" | AW_POLICY_RETRY_GUARD_1_MODE=banana bash "$GATE" 2> "$WORK/err")"; RC=$?
  fi
  check "advisory ($mode): a would-be deny exits 0" "$RC" "0"
  check "advisory ($mode): nothing on stderr" "$(cat "$WORK/err")" ""
  has "advisory ($mode): the deny text comes as advice" "$(ctx)" '`aw-nosuch-tool` is not installed'
  check "advisory ($mode): counters.wouldDeny +1, denies 0" "$(st s-adv '"\(.counters.wouldDeny) \(.counters.denies) \(.problems[].wouldDeny)"')" "1 0 1"
done
printf '#!/bin/sh\necho 1.0\n' > "$BIN/aw-nosuch-tool"; chmod +x "$BIN/aw-nosuch-tool"
OUT="$(printf '%s' "$(pre s-adv Bash "$CALL")" | env -u AW_POLICY_RETRY_GUARD_1_MODE bash "$GATE" 2> /dev/null)"
check "advisory: a would-be deny that later clears counts as cleared after change" "$(st s-adv '"\(.counters.cleared) \(.counters.clearedAfterChange)"')" "1 1"
unlink "$BIN/aw-nosuch-tool"

# --- #4304 red rows ----------------------------------------------------------------------------------------------------------
bash_in_d() { jq -nc --arg c "$1" --arg d "$2" '{command:$c, description:$d}'; }
# cfail / cpre: PostToolUseFailure / PreToolUse with a caller-chosen cwd (the helpers above hardcode $WORK)
cfail() { jq -nc --arg s "$1" --arg t "$2" --argjson i "$3" --arg e "$4" --arg cwd "$5" \
  '{session_id:$s, hook_event_name:"PostToolUseFailure", cwd:$cwd, tool_name:$t, tool_input:$i, tool_use_id:"toolu_x", error:$e, is_interrupt:false, duration_ms:5}'; }
cpre() { jq -nc --arg s "$1" --arg t "$2" --argjson i "$3" --arg cwd "$4" \
  '{session_id:$s, hook_event_name:"PreToolUse", cwd:$cwd, tool_name:$t, tool_input:$i, tool_use_id:"toolu_x"}'; }
bump() { touch -d '+5 seconds' "$1" 2> /dev/null || touch -t "$(date -v+5S +%Y%m%d%H%M.%S 2> /dev/null)" "$1"; }

# #4304 item 1: Bash description must not be part of the call hash.
reset
record "$(failure s-desc Bash "$(bash_in_d 'aw-nosuch-tool x' 'first')" 'aw-nosuch-tool: command not found')"
gate "$(pre s-desc Bash "$(bash_in_d 'aw-nosuch-tool x' 'second')")"
check "#4304 item 1, description: a reworded description is still the same call (denied)" "$RC" "2"
record "$(success s-desc Bash "$(bash_in_d 'aw-nosuch-tool x' 'third')")"
check "#4304 item 1, description: a success under another description clears the row" "$(st s-desc '.problems | length')" "0"
reset
record "$(failure s-desc2 Agent '{"prompt":"p","description":"a"}' 'agent failed: invalid argument')"
gate "$(pre s-desc2 Agent '{"prompt":"p","description":"b"}')"
check "#4304 item 1, description: an Agent description still changes the hash (guard)" "$RC" "0"

# #4304 item 2: a bad-args row clears when a file the call depends on changed since the failure.
PROJ="$WORK/proj"; mkdir -p "$PROJ"; echo 'test:' > "$PROJ/Makefile"
reset
record "$(cfail s-ba Bash '{"command":"make test"}' 'make: invalid option -- x' "$PROJ")"
gate "$(cpre s-ba Bash '{"command":"make test"}' "$PROJ")"
check "#4304 item 2, bad-args: unchanged build file, the repeat is denied (guard)" "$RC" "2"
bump "$PROJ/Makefile"
gate "$(cpre s-ba Bash '{"command":"make test"}' "$PROJ")"
check "#4304 item 2, bad-args: a changed Makefile lets the repeat through" "$RC" "0"
check "#4304 item 2, bad-args: ...and clears the row" "$(st s-ba '.problems | length')" "0"
EMPTYD="$WORK/emptyproj"; mkdir -p "$EMPTYD"
reset
record "$(cfail s-ba2 Bash '{"command":"frobnicate --bogus"}' 'frobnicate: invalid option -- bogus' "$EMPTYD")"
gate "$(cpre s-ba2 Bash '{"command":"frobnicate --bogus"}' "$EMPTYD")"
check "#4304 item 2, bad-args: no stamped file, the repeat stays denied (guard)" "$RC" "2"
printf '#!/bin/sh\necho hi\n' > "$PROJ/run.sh"
reset
record "$(cfail s-ba3 Bash '{"command":"./run.sh --bogus"}' 'run.sh: unknown option --bogus' "$PROJ")"
bump "$PROJ/run.sh"
gate "$(cpre s-ba3 Bash '{"command":"./run.sh --bogus"}' "$PROJ")"
check "#4304 item 2, bad-args: an edited script named by the command lets the repeat through" "$RC" "0"
# (item 2, missing-path half: already pinned by "missing-path: once it exists, the call passes" above.)

# #4304 item 3: the Cursor adapter puts session_id first, so a nested "session_id" in tool_input never wins.
NEST='{"conversation_id":"cursor-sid-1","hook_event_name":"postToolUseFailure","cwd":"/tmp","tool_name":"Shell","tool_input":{"command":"echo hi","session_id":"evil-sid"},"error_message":"x: command not found"}'  # an unescaped nested key, as an MCP argument or a tool_response object carries
NESTPRE="$(printf '%s' "$NEST" | jq -c '.hook_event_name = "preToolUse"')"
reset
printf '%s' "$NEST" | bash "$HOOKS/adapters/cursor.sh" "$RECORD" > /dev/null 2>&1
check "#4304 item 3, retry-record: the real conversation id names the state file" "$([ -e "$RG/cursor-sid-1.json" ] && echo yes || echo no)" "yes"
check "#4304 item 3, retry-record: the nested id names no state file" "$([ -e "$RG/evil-sid.json" ] && echo yes || echo no)" "no"
FLEETD="$WORK/fleet4304"
printf '%s' "$NESTPRE" | AW_FLEET_DIR="$FLEETD" bash "$HOOKS/adapters/cursor.sh" "$HOOKS/fleet-state.sh" > /dev/null 2>&1
check "#4304 item 3, fleet-state: the real id names the file" "$([ -e "$FLEETD/cursor/cursor-sid-1.json" ] && echo yes || echo no)" "yes"
check "#4304 item 3, fleet-state: the nested id names no file" "$([ -e "$FLEETD/cursor/evil-sid.json" ] && echo yes || echo no)" "no"
reset
DUPCMD="cat > /tmp/watch-pr.sh <<'EOF'
#!/usr/bin/env bash
while true; do
  gh pr checks 7 --repo acme/app
  gh pr view 7 --repo acme/app --json state -q .state | grep -q MERGED && break
  sleep 60
done
EOF
true"
jq -nc --arg c "$DUPCMD" '{conversation_id:"cursor-sid-1",hook_event_name:"preToolUse",cwd:"/tmp",tool_name:"Shell",tool_input:{command:$c,session_id:"evil-sid"}}' \
  | bash "$HOOKS/adapters/cursor.sh" "$HOOKS/dup-script-gate.sh" > /dev/null 2>&1
check "#4304 item 3, dup-script: the nested id names no state file" "$([ -e "$AW_STATE_DIR/duplicate-script/evil-sid.json" ] && echo yes || echo no)" "no"
export CAPTURE_FILE="$WORK/capture4304.json"
printf '%s' "$NESTPRE" | bash "$HOOKS/adapters/cursor.sh" "$DIR/fixtures/capture-hook.sh" > /dev/null 2>&1
check "#4304 item 3, adapter: session_id and hook_event_name are the first two keys" "$(jq -c '[keys_unsorted[0:2][]]' "$CAPTURE_FILE")" '["session_id","hook_event_name"]'

# #4304 item 4: a redundant working_directory in the pre payload must not change the call hash.
reset
bash "$HOOKS/adapters/cursor.sh" "$RECORD" < "$FIX/cursor/post-tool-use-failure-shell.json" > /dev/null 2>&1
OUT="$(jq -c '.tool_input.working_directory = "/tmp"' "$FIX/cursor/pretooluse-shell-nosuch.json" | bash "$HOOKS/adapters/cursor.sh" "$GATE" 2> /dev/null)"
check "#4304 item 4, cursor: working_directory in pre but not in the failure still matches (deny)" "$(printf '%s' "$OUT" | jq -r .permission)" "deny"
OUT="$(jq -c '.tool_input.working_directory = "/elsewhere"' "$FIX/cursor/pretooluse-shell-nosuch.json" | bash "$HOOKS/adapters/cursor.sh" "$GATE" 2> /dev/null)"
check "#4304 item 4, cursor: a working_directory that differs from cwd is a different call (allow, guard)" "$(printf '%s' "$OUT" | jq -r .permission)" "allow"

# #4304 item 5a: an override comment followed by a trailing newline is still an override.
reset
record "$(failure s-nl Bash "$(bash_in 'aw-nosuch-tool y')" 'aw-nosuch-tool: command not found')"
gate "$(pre s-nl Bash "$(bash_in $'aw-nosuch-tool y # aw-retry: why\n')")"
check "#4304 item 5a, override: a trailing newline does not make the call a different one (guard: exit 0)" "$RC" "0"
check "#4304 item 5a, override: ...recorded with its reason" "$(st s-nl '.overrides[0].reason')" "why"

# --- latency of the empty path ----------------------------------------------------------------------------------------------
reset
EMPTY="$(pre s-fast Bash "$(bash_in 'ls -la')")"
printf '%s' "$EMPTY" > "$WORK/empty.json"
ns() { if [ -n "${EPOCHREALTIME:-}" ]; then echo "${EPOCHREALTIME/./}"; else perl -MTime::HiRes=time -e 'printf "%d\n", time * 1e6'; fi; }
: > "$WORK/times"
for i in $(seq 1 50); do
  a=$(ns); bash "$GATE" < "$WORK/empty.json" > /dev/null 2>&1; b=$(ns)
  echo $(( b - a )) >> "$WORK/times"
done
MED_US="$(sort -n "$WORK/times" | sed -n 25p)"
echo "# empty-path median: $((MED_US / 1000)).$(( (MED_US % 1000) / 100 )) ms over 50 runs"
check "latency: the empty path's median is under 20 ms" "$([ "$MED_US" -lt 20000 ] && echo yes || echo "no ($MED_US us)")" "yes"

exit "$fail"
