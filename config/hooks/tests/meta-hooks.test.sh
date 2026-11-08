#!/usr/bin/env bash
# Tests for config/hooks/meta-stop-gate.sh and meta-outbox.sh (Claude Code), and for the non-Claude lane hooks
# meta-lane-gate.sh and session-id-report.sh (directly and through the Codex and Cursor adapters), against a stub aw-meta.
# Run: bash config/hooks/tests/meta-hooks.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOKS="$(cd "$DIR/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done   # the caller may be a meta session: its team config, bin and claude must not reach the stubs
export HOME="$WORK/home"; mkdir -p "$HOME"
export AW_STATE_DIR="$WORK/state"
unset AW_JUDGE_CHILD AW_SINDRI_HOME CLAUDE_LOCAL_BIN   # a caller's Sindri home must not receive the session-id-report cases' launch files
export CALLS="$WORK/calls" STDIN_SEEN="$WORK/stdin" HOOKLOG="$WORK/hooklog"
STUB="$WORK/aw-meta"
cat > "$STUB" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$CALLS"
echo "$1|${AW_META_HOOK:-}" >> "$HOOKLOG"   # the command and the hook event the caller named (none: a hand-typed run)
if [ "$1" = outbox-record ] || [ "$1" = lane-reply ]; then cat > "$STDIN_SEEN"; fi
[ -n "${STUB_OUT:-}" ] && echo "$STUB_OUT"
# lane-reply answers STUB_REPLY_RC when set (a lane that records its reply and is still blocked), else STUB_RC like every command
if [ "$1" = lane-reply ]; then exit "${STUB_REPLY_RC:-${STUB_RC:-0}}"; fi
exit "${STUB_RC:-0}"
EOF
chmod +x "$STUB"
export AW_META_BIN="$STUB"
# The payload comes from a file, not a pipe: a hook that exits before reading stdin (aw-meta not installed, a judge child) would
# otherwise make printf die of SIGPIPE and the pipeline report 141 whenever the hook is faster than printf.
stop() { printf '%s' "$1" > "$WORK/in"; bash "$HOOKS/meta-stop-gate.sh" 2> "$WORK/err" < "$WORK/in"; }
P='{"session_id":"s-1","transcript_path":"/t/s-1.jsonl","stop_hook_active":false}'

: > "$CALLS"
stop "$P"; RC=$?
check "nothing owed: exit 0" "$RC" "0"
check "nothing owed: calls stop-check with session and transcript" "$(cat "$CALLS")" "stop-check --session s-1 --transcript /t/s-1.jsonl"

: > "$CALLS"
STUB_RC=1 STUB_OUT="OWED reply to sindri-api: docs check" stop "$P"; RC=$?
check "obligation open: blocks with exit 2" "$RC" "2"
check "obligation open: the list goes to stderr" "$(cat "$WORK/err")" "OWED reply to sindri-api: docs check"

: > "$CALLS"
STUB_RC=1 stop '{"session_id":"s-1","transcript_path":"/t/x","stop_hook_active":true}'; RC=$?
check "hook active: still asks, with --hook-active" "$(cat "$CALLS")" "stop-check --session s-1 --transcript /t/x --hook-active"
check "hook active: aw-meta decides (blocks)" "$RC" "2"

OUT="$(STUB_OUT="aw-meta: stop allowed after 3 identical blocks; still open: reply" stop "$P")"; RC=$?
check "loop guard let it through: exit 0, the warning shown as a systemMessage" "$RC:$(jq -r .systemMessage <<< "$OUT")" "0:aw-meta: stop allowed after 3 identical blocks; still open: reply"
OUT="$(stop "$P")"
check "nothing owed: prints nothing" "$OUT" ""

STUB_RC=2 stop "$P"; RC=$?
check "tool error: fails open" "$RC" "0"
grep -q '"hook":"meta-stop-gate","cause":"meta-exit-2"' "$AW_STATE_DIR/logs/failopen.log" 2> /dev/null
check "tool error: logged as HOOK-FAILOPEN meta-exit-2" "$?" "0"

stop ""; RC=$?
check "empty input: fails open" "$RC" "0"
stop '{"session_id":"s-1"}'; RC=$?
check "no transcript_path: fails open" "$RC" "0"
grep -q '"cause":"field-missing"' "$AW_STATE_DIR/logs/failopen.log"
check "no transcript_path: logged field-missing" "$?" "0"

: > "$CALLS"
AW_META_BIN="$WORK/missing" stop "$P"; RC=$?
check "aw-meta not installed: exit 0, nothing called" "$RC:$(cat "$CALLS")" "0:"

: > "$CALLS"
AW_JUDGE_CHILD=1 stop "$P"; RC=$?
check "judge child: exit 0, nothing called" "$RC:$(cat "$CALLS")" "0:"

# meta-outbox.sh
: > "$CALLS"
SEND='{"session_id":"s-1","tool_name":"SendMessage","tool_input":{"to":"sindri-api","message":"done"},"tool_response":{"msg_id":"m-9"}}'
OUT="$(printf '%s' "$SEND" | bash "$HOOKS/meta-outbox.sh")"; RC=$?
check "outbox: exit 0, silent" "$RC:$OUT" "0:"
check "outbox: calls outbox-record" "$(cat "$CALLS")" "outbox-record"
check "outbox: passes the hook payload on stdin" "$(cat "$STDIN_SEEN")" "$SEND"
OUT="$(printf '%s' "$SEND" | STUB_RC=2 bash "$HOOKS/meta-outbox.sh")"; RC=$?
check "outbox: aw-meta failure never blocks" "$RC:$OUT" "0:"
grep -q '"hook":"meta-outbox","cause":"meta-exit-2"' "$AW_STATE_DIR/logs/failopen.log"
check "outbox: failure logged" "$?" "0"

# --- meta-lane-gate.sh: the lane hook for a Codex or Cursor lane ---
# The hooks act only in a lane started by scripts/meta-lane.sh, which exports its own pid as AW_META_LANE_PID: this shell plays it.
export AW_META_LANE_PID=$$ AW_META_LANE_AGENT=codex AW_META_LANE_PROC=codex
# The payload comes from a file, as for stop(): a hook that exits early must not turn into a SIGPIPE in printf.
lane() { printf '%s' "$1" > "$WORK/in"; AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err" < "$WORK/in"; }
UPS='{"hook_event_name":"UserPromptSubmit","session_id":"s-2","prompt":"hi"}'

: > "$CALLS"
OUT="$(printf '%s' "$UPS" | bash "$HOOKS/meta-lane-gate.sh")"; RC=$?
check "outside a lane: exit 0, silent, aw-meta not called" "$RC:$OUT:$(cat "$CALLS")" "0::"
: > "$CALLS"
OUT="$(printf '%s' "$UPS" | AW_META_LANE=api bash "$HOOKS/meta-lane-gate.sh")"; RC=$?
check "only AW_META_LANE set (no team): still a pass-through" "$RC:$OUT:$(cat "$CALLS")" "0::"

: > "$CALLS"
OUT="$(STUB_OUT=$'MESSAGE FROM THE META to lane api:\n- rebase' lane "$UPS")"; RC=$?
check "UserPromptSubmit: the inbox is printed as plain text (Codex developer context)" "$RC:$OUT" "0:MESSAGE FROM THE META to lane api:
- rebase"
check "UserPromptSubmit: calls lane-inbox, nothing else" "$(cat "$CALLS")" "lane-inbox"
OUT="$(lane "$UPS")"
check "UserPromptSubmit with an empty inbox: prints nothing" "$OUT" ""
OUT="$(STUB_OUT="a message" lane '{"hook_event_name":"PostToolUse","session_id":"s-2","tool_name":"Shell"}')"
check "PostToolUse (Cursor): additionalContext JSON" "$(jq -c . <<< "$OUT")" '{"hookSpecificOutput":{"hookEventName":"PostToolUse","additionalContext":"a message"}}'
OUT="$(STUB_OUT="a message" lane '{"hook_event_name":"SessionStart","session_id":"s-2"}')"
check "SessionStart (Cursor): additionalContext JSON" "$(jq -r .hookSpecificOutput.additionalContext <<< "$OUT")" "a message"
STUB_RC=2 lane "$UPS" > /dev/null; RC=$?
check "lane-inbox tool error (no lane variables, no team config): fails open" "$RC" "0"
OUT="$(lane '{"hook_event_name":"PreToolUse","session_id":"s-2"}')"; RC=$?
check "an event the hook does not handle: exit 0, silent" "$RC:$OUT" "0:"
AW_JUDGE_CHILD=1 lane "$UPS" > /dev/null; RC=$?
check "judge child: exit 0" "$RC" "0"

STOP='{"hook_event_name":"Stop","session_id":"s-2","stop_hook_active":false,"last_assistant_message":"Rebased.\nREPLY TO META: rebased and pushed\nsecond line"}'
: > "$CALLS"; : > "$STDIN_SEEN"
lane "$STOP"; RC=$?
check "Stop with a REPLY TO META: line: the reply is recorded, then the stop is checked" "$RC:$(paste -sd'|' "$CALLS")" "0:lane-reply --stdin|lane-stop-check"
check "Stop: the reply text (marker removed, later lines kept) goes to lane-reply on stdin" "$(cat "$STDIN_SEEN")" "rebased and pushed
second line"
: > "$CALLS"
lane '{"hook_event_name":"Stop","session_id":"s-2","last_assistant_message":"All done, nothing to answer."}'; RC=$?
check "Stop without the marker: only lane-stop-check runs" "$RC:$(cat "$CALLS")" "0:lane-stop-check"
: > "$CALLS"
lane '{"hook_event_name":"Stop","session_id":"s-2","last_assistant_message":"I will end with REPLY TO META: later"}'
check "Stop: the marker mid-line is not a reply" "$(cat "$CALLS")" "lane-stop-check"
: > "$CALLS"
lane '{"hook_event_name":"Stop","session_id":"s-2","last_assistant_message":"REPLY TO META:   "}'
check "Stop: an empty reply is not recorded" "$(cat "$CALLS")" "lane-stop-check"
: > "$CALLS"
lane '{"hook_event_name":"Stop","session_id":"s-2","last_assistant_message":"REPLY TO META: first\nmore\nREPLY TO META: second"}'
check "Stop: the last marker wins" "$(cat "$STDIN_SEEN")" "second"
: > "$CALLS"
lane '{"hook_event_name":"Stop","session_id":"s-2","stop_hook_active":true}'
check "Stop on a continuation: --stop-hook-active" "$(cat "$CALLS")" "lane-stop-check --stop-hook-active"
: > "$CALLS"
STUB_RC=1 STUB_OUT="aw-meta: lane api owes the meta a reply (1 message(s)):" lane "$STOP"; RC=$?
check "lane-stop-check exit 1 (a reply is owed): blocks with exit 2, the reason on stderr" "$RC:$(cat "$WORK/err")" "2:aw-meta: lane api owes the meta a reply (1 message(s)):"
STUB_RC=2 lane "$STOP"; RC=$?
check "lane-stop-check exit 2 (tool error, team config removed): the stop is not blocked" "$RC" "0"
grep -q '"hook":"meta-lane-gate","cause":"lane-stop-check-exit-2"' "$AW_STATE_DIR/logs/failopen.log"
check "lane-stop-check exit 2: logged as HOOK-FAILOPEN" "$?" "0"
OUT="$(STUB_OUT="aw-meta: stop allowed after 3 identical blocks; lane api still owes: x" lane "$STOP")"; RC=$?
check "loop guard let it through: exit 0, the warning shown as a systemMessage" "$RC:$(jq -r .systemMessage <<< "$OUT")" "0:aw-meta: stop allowed after 3 identical blocks; lane api still owes: x"
AW_META_BIN="$WORK/missing" lane "$STOP"; RC=$?
check "aw-meta not installed: exit 0" "$RC" "0"
lane ""; RC=$?
check "empty input: fails open" "$RC" "0"


# --- a reply is recorded once; mail delivered since clears the marker ---
LS="$AW_STATE_DIR/meta/lane-sessions"
REPLIED="$LS/api.replied"
DUP='{"hook_event_name":"Stop","session_id":"s-2","last_assistant_message":"REPLY TO META: same answer"}'
unlink "$REPLIED" 2> /dev/null; : > "$CALLS"
printf '%s' "$DUP" > "$WORK/in"; AW_META_TEAM=t1 AW_META_LANE=api AW_META_DIR="$WORK/teamdir2" bash "$HOOKS/meta-lane-gate.sh" 2> /dev/null < "$WORK/in"
check "AW_META_DIR: the reply marker lands in the team's directory, not the flat one" "$([ -f "$WORK/teamdir2/lane-sessions/api.replied" ] && echo team):$([ -f "$REPLIED" ] && echo flat)" "team:"
unlink "$REPLIED" 2> /dev/null; : > "$CALLS"
lane "$DUP"; lane "$DUP"
check "the same reply at two Stops (a continuation, a stale transcript tail) is recorded once" "$(grep -c '^lane-reply' "$CALLS"):$(grep -c '^lane-stop-check' "$CALLS")" "1:2"
STUB_OUT="new mail" lane "$UPS" > /dev/null
lane "$DUP"
check "mail delivered since clears the marker: the same text is a new reply" "$(grep -c '^lane-reply' "$CALLS")" "2"
lane '{"hook_event_name":"Stop","session_id":"s-2","last_assistant_message":"REPLY TO META: another answer"}'
check "a different reply is recorded" "$(grep -c '^lane-reply' "$CALLS")" "3"
unlink "$REPLIED" 2> /dev/null; : > "$CALLS"
STUB_RC=2 lane "$DUP"
STUB_RC=0 lane "$DUP"
check "a reply aw-meta refused is not marked: the next Stop retries it" "$(grep -c '^lane-reply' "$CALLS")" "2"
grep -q '"hook":"meta-lane-gate","cause":"lane-reply-exit-2"' "$AW_STATE_DIR/logs/failopen.log"
check "the refused reply is logged" "$?" "0"
STUB_RC=2 STUB_OUT="AW-META-014: the lane variables are unset" lane "$DUP"
grep -q '"cause":"lane-stop-check-exit-2-AW-META-014"' "$AW_STATE_DIR/logs/failopen.log"
check "aw-meta's error code is part of the logged cause (never its text)" "$?" "0"

# --- only the lane's own agent acts ---
# Process trees as the real CLIs make them, under a stand-in lane script that exports AW_META_LANE_PID as meta-lane.sh does:
#   standalone: lane script -> codex (argv0 codex) -> hook                       (a native binary)
#   npm:        lane script -> node -> codex (argv0 codex) -> hook               (the npm install: a node wrapper spawns the binary)
#   nested:     the same, with a tool shell below the agent starting a second instance (codex exec) whose hook runs
cat > "$WORK/wrapper.js" <<'EOF'
const { spawnSync } = require('child_process');
const r = spawnSync('bash', process.argv.slice(2), { argv0: 'codex', stdio: 'inherit' });
process.exit(r.status === null ? 1 : r.status);
EOF
cat > "$WORK/agent.sh" <<'EOF'
#!/usr/bin/env bash
if [ "${SHAPE:-standalone}" = npm ]; then exec node "${0%/*}/wrapper.js" "${0%/*}/agent-body.sh" "$1" "$2"; fi
exec -a codex bash "${0%/*}/agent-body.sh" "$@"
EOF
cat > "$WORK/agent-body.sh" <<'EOF'
#!/usr/bin/env bash
if [ -n "${NESTED:-}" ]; then bash -c 'NESTED= bash "$0" "$1" "$2"; true' "${0%/*}/agent.sh" "$1" "$2"; true; else bash "$1" < "$2"; true; fi
EOF
cat > "$WORK/lane-script.sh" <<'EOF'
#!/usr/bin/env bash
export AW_META_LANE_PID=$$ AW_META_LANE_AGENT="${LANE_AGENT:-codex}" AW_META_LANE_PROC="${LANE_PROC:-codex}" AW_META_TEAM=t1 AW_META_LANE="${LANE_NAME:-api}"
bash "${0%/*}/agent.sh" "$1" "$2"
EOF
printf '%s' "$UPS" > "$WORK/ups.json"
printf '%s' '{"hook_event_name":"SessionStart","session_id":"agent-own-1"}' > "$WORK/start-own.json"
printf '%s' '{"hook_event_name":"SessionStart","session_id":"agent-nested-1"}' > "$WORK/start-nested.json"
shapes="standalone"; command -v node > /dev/null 2>&1 && shapes="standalone npm" || echo "ok - skipped: node is not installed (the npm-shaped cases)"
for shape in $shapes; do
  : > "$CALLS"
  OUT="$(SHAPE=$shape STUB_OUT="a message" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/ups.json")"
  check "$shape: meta-lane-gate: the lane's own agent gets its inbox" "$OUT:$(cat "$CALLS")" "a message:lane-inbox"
  : > "$CALLS"
  OUT="$(SHAPE=$shape NESTED=1 STUB_OUT="a message" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/ups.json")"
  check "$shape: meta-lane-gate: an instance the lane's agent started passes straight through" "$OUT:$(cat "$CALLS")" ":"
  unlink "$LS/api" 2> /dev/null
  SHAPE=$shape bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-own.json"
  check "$shape: session-id-report: the lane's own agent records its id" "$(cat "$LS/api" 2>/dev/null)" "agent-own-1"
  SHAPE=$shape NESTED=1 bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-nested.json"
  check "$shape: session-id-report: a nested instance does not overwrite the lane's id" "$(cat "$LS/api")" "agent-own-1"
  SHAPE=$shape NESTED=1 LANE_NAME=nested-only bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-nested.json"
  check "$shape: session-id-report: a nested instance writes no file of its own either" "$(test -e "$LS/nested-only" && echo written || echo no)" "no"
done
# a Claude lane: a codex exec inside it is nobody's hook
printf 'claude-session-id\n' > "$LS/claude-lane"
SHAPE=standalone LANE_AGENT=claude-code LANE_PROC=claude LANE_NAME=claude-lane bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-nested.json"
check "a codex exec inside a Claude lane leaves lane-sessions/<lane> unchanged" "$(cat "$LS/claude-lane")" "claude-session-id"
: > "$CALLS"
OUT="$(SHAPE=standalone LANE_AGENT=claude-code LANE_PROC=claude STUB_OUT="a message" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/ups.json")"
check "a codex exec inside a Claude lane gets no inbox and no stop gate" "$OUT:$(cat "$CALLS")" ":"
: > "$CALLS"
OUT="$(env -u AW_META_LANE_PID STUB_OUT="a message" AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/meta-lane-gate.sh" < "$WORK/ups.json")"
check "no AW_META_LANE_PID (not started by a lane script): passes straight through" "$OUT:$(cat "$CALLS")" ":"
unlink "$LS/nopid" 2> /dev/null
env -u AW_META_LANE_PID AW_META_TEAM=t1 AW_META_LANE=nopid bash "$HOOKS/session-id-report.sh" < "$WORK/start-own.json"
check "no AW_META_LANE_PID: session-id-report writes nothing" "$(test -e "$LS/nopid" && echo written || echo no)" "no"
: > "$CALLS"
OUT="$(AW_META_LANE_PID=999999 STUB_OUT="a message" AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/meta-lane-gate.sh" < "$WORK/ups.json")"
check "a session with no lane script among its ancestors passes through" "$OUT:$(cat "$CALLS")" ":"

# --- a busy lane: mail reaches it through the block; both replies "on it" are recorded ---
unlink "$REPLIED" 2> /dev/null; unlink "$LS/api.owed" 2> /dev/null; : > "$CALLS"
ON='{"hook_event_name":"Stop","session_id":"s-2","last_assistant_message":"Done.\nREPLY TO META: on it"}'
lane "$ON"
STUB_REPLY_RC=0 STUB_RC=1 STUB_OUT="aw-meta: lane api owes the meta a reply (1 message(s)): - second message" lane "$ON"
lane "$ON"
check "a busy lane that answers 'on it' to two messages (the second arrived through the block) has both replies recorded" "$(grep -c '^lane-reply' "$CALLS")" "2"
for _ in 1 2 3; do STUB_REPLY_RC=0 STUB_RC=1 STUB_OUT="aw-meta: lane api owes the meta a reply (1 message(s)): - third message" lane "$ON"; done
check "a new owed list is answered once; the same list blocked again (the stop-cap loop) does not record the reply again" "$(grep -c '^lane-reply' "$CALLS")" "3"
OUT="$(printf '%s' '{"hook_event_name":"SessionStart","session_id":"new-session"}' | AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/session-id-report.sh")"
check "a new session clears the reply marker and the owed checksum" "$(test -e "$REPLIED" -o -e "$LS/api.owed" && echo kept || echo cleared)" "cleared"

# through the real adapters (a stub aw-meta behind them)
CODEX="$HOOKS/adapters/codex.sh"; CURSOR="$HOOKS/adapters/cursor.sh"
unlink "$AW_STATE_DIR/meta/lane-sessions/api.replied" 2> /dev/null
CSTOP='{"hook_event_name":"Stop","session_id":"s-3","transcript_path":"/t/rollout.jsonl","stop_hook_active":false,"last_assistant_message":"REPLY TO META: ok"}'
: > "$CALLS"
OUT="$(printf '%s' "$CSTOP" | bash "$CODEX" "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err")"; RC=$?
check "codex Stop outside any lane: passes straight through (exit 0, silent, aw-meta not called)" "$RC:$OUT:$(cat "$CALLS")" "0::"
OUT="$(printf '%s' "$UPS" | bash "$CODEX" "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err")"; RC=$?
check "codex UserPromptSubmit outside any lane: passes straight through" "$RC:$OUT:$(cat "$CALLS")" "0::"
find "$LS" -mindepth 1 -delete 2> /dev/null   # the earlier cases left their marker and ids
OUT="$(printf '%s' '{"hook_event_name":"SessionStart","session_id":"abc-1"}' | bash "$CODEX" "$HOOKS/session-id-report.sh" 2> "$WORK/err")"; RC=$?
check "codex SessionStart outside any lane: passes straight through, writes nothing" "$RC:$OUT:$(ls "$AW_STATE_DIR/meta/lane-sessions" 2>/dev/null | wc -l | tr -d ' ')" "0::0"
OUT="$(printf '%s' "$UPS" | STUB_OUT="a message" AW_META_TEAM=t1 AW_META_LANE=api bash "$CODEX" "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err")"; RC=$?
check "codex UserPromptSubmit in a lane: the inbox is plain stdout (developer context)" "$RC:$OUT" "0:a message"
OUT="$(printf '%s' "$CSTOP" | STUB_RC=1 STUB_OUT="owes the meta a reply" AW_META_TEAM=t1 AW_META_LANE=api bash "$CODEX" "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err")"; RC=$?
check "codex Stop in a lane that owes a reply: blocked (exit 2), reason on stderr" "$RC:$OUT:$(cat "$WORK/err")" "2::owes the meta a reply"
OUT="$(printf '%s' "$CSTOP" | AW_META_TEAM=t1 AW_META_LANE=api bash "$CODEX" "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err")"; RC=$?
check "codex Stop, nothing owed: allowed" "$RC:$OUT" "0:"
# #2272: a Codex lane's reply reaches aw-meta through the real adapter, which blanks transcript_path for Stop (the reply comes from
# last_assistant_message alone)
unlink "$REPLIED" 2> /dev/null
: > "$CALLS"; : > "$STDIN_SEEN"
CREPLY='{"hook_event_name":"Stop","session_id":"s-3","turn_id":"t-1","transcript_path":"/t/rollout.jsonl","stop_hook_active":false,"last_assistant_message":"Done.\nREPLY TO META: codex reply\nmore"}'
OUT="$(printf '%s' "$CREPLY" | AW_META_TEAM=t1 AW_META_LANE=api bash "$CODEX" "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err")"; RC=$?
check "codex Stop with a REPLY TO META: line, through the adapter: recorded, then the stop is checked" "$RC:$OUT:$(paste -sd'|' "$CALLS")" "0::lane-reply --stdin|lane-stop-check"
check "codex Stop through the adapter: the reply text goes to lane-reply on stdin" "$(cat "$STDIN_SEEN")" "codex reply
more"

TR="$WORK/cursor-transcript.jsonl"
echo '{"role":"assistant","message":{"content":[{"type":"text","text":"Done.\nREPLY TO META: cursor reply"}]}}' > "$TR"
CUSTOP="{\"hook_event_name\":\"stop\",\"conversation_id\":\"c-1\",\"loop_count\":0,\"transcript_path\":\"$TR\"}"
: > "$CALLS"; : > "$STDIN_SEEN"
OUT="$(printf '%s' "$CUSTOP" | STUB_RC=1 STUB_OUT="owes the meta a reply" AW_META_TEAM=t1 AW_META_LANE=api bash "$CURSOR" "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err")"; RC=$?
check "cursor stop in a lane that owes a reply: followup_message" "$RC:$(jq -r .followup_message <<< "$OUT")" "0:owes the meta a reply"
check "cursor stop: the reply is taken from the transcript's last assistant text" "$(cat "$STDIN_SEEN")" "cursor reply"
OUT="$(printf '%s' "$CUSTOP" | bash "$CURSOR" "$HOOKS/meta-lane-gate.sh" 2> "$WORK/err")"; RC=$?
check "cursor stop outside any lane: {} and aw-meta not called" "$RC:$OUT" "0:{}"
OUT="$(printf '%s' '{"hook_event_name":"postToolUse","conversation_id":"c-1","tool_name":"Shell","tool_input":{},"tool_output":"{}"}' | STUB_OUT="a message" AW_META_TEAM=t1 AW_META_LANE=api bash "$CURSOR" "$HOOKS/meta-lane-gate.sh")"; RC=$?
check "cursor postToolUse in a lane: additional_context" "$RC:$(jq -r .additional_context <<< "$OUT")" "0:a message"
OUT="$(printf '%s' '{"hook_event_name":"sessionStart","conversation_id":"c-1"}' | STUB_OUT="a message" AW_META_TEAM=t1 AW_META_LANE=api bash "$CURSOR" "$HOOKS/meta-lane-gate.sh")"; RC=$?
check "cursor sessionStart in a lane: additional_context" "$RC:$(jq -r .additional_context <<< "$OUT")" "0:a message"

# --- session-id-report.sh ---
LS="$AW_STATE_DIR/meta/lane-sessions"
report() { printf '%s' "$1" > "$WORK/in"; bash "$HOOKS/session-id-report.sh" 2> "$WORK/err" < "$WORK/in"; }
find "$LS" -mindepth 1 -delete 2> /dev/null
OUT="$(report '{"hook_event_name":"SessionStart","session_id":"abc-123"}')"; RC=$?
check "outside a lane or a Sindri launch: exit 0, nothing written" "$RC:$OUT:$(ls "$LS" 2>/dev/null | wc -l | tr -d ' ')" "0::0"
OUT="$(printf '%s' '{"hook_event_name":"SessionStart","session_id":"0199aaaa-bbbb-4ccc-8ddd-eeeeffff0001"}' | AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/session-id-report.sh")"; RC=$?
check "in a lane: exit 0, silent, the id is lane-sessions/<lane>" "$RC:$OUT:$(cat "$LS/api")" "0::0199aaaa-bbbb-4ccc-8ddd-eeeeffff0001"
check "the lane file is 0600 and no temp file is left (only the id and the hook trace)" "$(stat -c %a "$LS/api" 2>/dev/null || stat -f %Lp "$LS/api"):$(ls -A "$LS" | tr '\n' ' ')" "600:api api.hook "
printf '%s' '{"session_id":"0199aaaa-bbbb-4ccc-8ddd-eeeeffff0002"}' | AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/session-id-report.sh"
check "a new session in the lane replaces the id" "$(cat "$LS/api")" "0199aaaa-bbbb-4ccc-8ddd-eeeeffff0002"
printf '%s' '{"session_id":"id-ui"}' | AW_META_TEAM=t1 AW_META_LANE=ui bash "$HOOKS/session-id-report.sh"
check "two lanes keep their own ids" "$(cat "$LS/api"):$(cat "$LS/ui")" "0199aaaa-bbbb-4ccc-8ddd-eeeeffff0002:id-ui"
printf '%s' '{"session_id":"id-team"}' | AW_META_TEAM=t1 AW_META_LANE=api AW_META_DIR="$WORK/teamdir" bash "$HOOKS/session-id-report.sh"
check "AW_META_DIR: the id is recorded under the team's directory, the flat one is untouched" "$(cat "$WORK/teamdir/lane-sessions/api"):$(cat "$LS/api")" "id-team:0199aaaa-bbbb-4ccc-8ddd-eeeeffff0002"
printf '%s' '{"session_id":"abc-1"}' | AW_SINDRI_LAUNCH=launch-7 AW_SINDRI_HOME="$WORK/sindri" bash "$HOOKS/session-id-report.sh"
check "a Sindri launch: \$AW_SINDRI_HOME/launches/<id>.session-id" "$(cat "$WORK/sindri/launches/launch-7.session-id" 2>/dev/null)" "abc-1"
printf '%s' '{"session_id":"abc-2"}' | AW_SINDRI_LAUNCH=launch-8 bash "$HOOKS/session-id-report.sh"
check "a Sindri launch without AW_SINDRI_HOME: under the state directory's sindri-app" "$(cat "$AW_STATE_DIR/sindri-app/launches/launch-8.session-id" 2>/dev/null)" "abc-2"
printf '%s' '{"session_id":"x; touch /tmp/pwn"}' | AW_META_TEAM=t1 AW_META_LANE=bad bash "$HOOKS/session-id-report.sh"
check "an id that is not an id is never written" "$(test -e "$LS/bad" && echo written || echo no)" "no"
printf '%s' '{"session_id":"abc-9"}' | AW_META_TEAM=t1 AW_META_LANE=../../escape bash "$HOOKS/session-id-report.sh"
check "a lane name with a path in it is never written" "$(find "$WORK" -name escape | wc -l | tr -d ' ')" "0"
printf '%s' '{"session_id":"abc-9"}' | AW_JUDGE_CHILD=1 AW_META_TEAM=t1 AW_META_LANE=judge bash "$HOOKS/session-id-report.sh"
check "judge child: nothing written" "$(test -e "$LS/judge" && echo written || echo no)" "no"
OUT="$(printf '%s' '{"hook_event_name":"sessionStart","conversation_id":"cur-1"}' | AW_META_TEAM=t1 AW_META_LANE=cur bash "$CURSOR" "$HOOKS/session-id-report.sh")"; RC=$?
check "through the cursor adapter: conversation_id is the id, the output is {}" "$RC:$OUT:$(cat "$LS/cur")" "0:{}:cur-1"

# --- a Codex meta lane (the team's metaLane): its role is registered once its id is reported; the lane commands do not run ---
# The lane's agent gets AW_META_CONFIG from scripts/meta-lane.sh; the hooks read metaLane from it.
printf '%s' '{"team":"t1","metaLane":"meta","lanes":["api"]}' > "$WORK/team.json"
unlink "$LS/meta" 2> /dev/null; : > "$CALLS"
AW_META_CONFIG="$WORK/team.json" LANE_NAME=meta bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-own.json"
check "meta lane: session-id-report records the id and registers the role under the team-lane name" "$(cat "$LS/meta" 2>/dev/null):$(cat "$CALLS")" "agent-own-1:role-set --session agent-own-1 --name t1-meta"
: > "$CALLS"
AW_META_CONFIG="$WORK/team.json" LANE_NAME=api bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-own.json"
check "another lane of the same team registers no role" "$(cat "$CALLS")" ""
: > "$CALLS"
AW_META_CONFIG="$WORK/team.json" NESTED=1 LANE_NAME=meta bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-nested.json"
check "an instance the meta's agent started registers no role" "$(cat "$CALLS")" ""
: > "$CALLS"
LANE_NAME=meta bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-own.json"
check "no team config (AW_META_CONFIG unset): the id is recorded and no role is registered" "$(cat "$LS/meta"):$(cat "$CALLS")" "agent-own-1:"
printf '%s' '{"team":"t1","lanes":["api"]}' > "$WORK/team-default.json"
: > "$CALLS"
AW_META_CONFIG="$WORK/team-default.json" LANE_NAME=meta bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-own.json"
check "a team config without metaLane: the meta lane is named meta" "$(cat "$CALLS")" "role-set --session agent-own-1 --name t1-meta"
: > "$CALLS"; unlink "$AW_STATE_DIR/logs/failopen.log" 2> /dev/null
STUB_RC=1 AW_META_CONFIG="$WORK/team.json" LANE_NAME=meta bash "$WORK/lane-script.sh" "$HOOKS/session-id-report.sh" "$WORK/start-own.json"; RC=$?
check "role-set failing: the hook still exits 0 and the id stays recorded" "$RC:$(cat "$LS/meta")" "0:agent-own-1"
grep -q '"hook":"session-id-report","cause":"role-set-exit-1"' "$AW_STATE_DIR/logs/failopen.log" 2> /dev/null
check "role-set failing: the cause is logged" "$?" "0"

: > "$CALLS"
OUT="$(AW_META_CONFIG="$WORK/team.json" LANE_NAME=meta STUB_OUT="a message" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/ups.json")"
check "meta lane: UserPromptSubmit prints the meta inbox (aw-meta inbox) and runs no lane command" "$OUT:$(cat "$CALLS")" "a message:inbox"
printf '%s' '{"hook_event_name":"Stop","session_id":"s-9","stop_hook_active":false,"last_assistant_message":"REPLY TO META: ok"}' > "$WORK/stop-meta.json"
: > "$CALLS"
AW_META_CONFIG="$WORK/team.json" LANE_NAME=meta STUB_RC=1 STUB_OUT="owes" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/stop-meta.json" 2> "$WORK/err"; RC=$?
check "meta lane: Stop runs no lane-reply or lane-stop-check and never blocks" "$RC:$(cat "$CALLS"):$(cat "$WORK/err")" "0::"
: > "$CALLS"
OUT="$(AW_META_CONFIG="$WORK/team.json" LANE_NAME=api STUB_OUT="a message" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/ups.json")"
check "another lane of the same team keeps its inbox" "$OUT:$(cat "$CALLS")" "a message:lane-inbox"

# --- #2122: every aw-meta call a hook makes names its event; the hook trace file says what the hook did ---
# trace [lane]: "<hook> <event> <outcome>" of lane-sessions/<lane>.hook when its first field is a UTC ISO time, else why not
trace() {
  local f="$LS/${1:-api}.hook" t h e o
  [ -f "$f" ] || { echo "no trace file"; return; }
  read -r t h e o < "$f"
  [[ "$t" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]+)?Z$ ]] || { echo "bad time: $t"; return; }
  echo "$h $e $o"
}
clear_lane() { find "$LS" -mindepth 1 -delete 2> /dev/null; : > "$CALLS"; : > "$HOOKLOG"; : > "$STDIN_SEEN"; }
STOPR='{"hook_event_name":"Stop","session_id":"s-5","last_assistant_message":"REPLY TO META: c1 reply"}'
AAR='{"hook_event_name":"AfterAgentResponse","session_id":"s-5","last_assistant_message":"Done.\nREPLY TO META: aar reply"}'

# C1: AW_META_HOOK on every aw-meta call
clear_lane; lane "$UPS" > /dev/null
check "C1 UserPromptSubmit: lane-inbox carries AW_META_HOOK=UserPromptSubmit" "$(cat "$HOOKLOG")" "lane-inbox|UserPromptSubmit"
clear_lane; lane '{"hook_event_name":"SessionStart","session_id":"s-5"}' > /dev/null
check "C1 SessionStart: lane-inbox carries AW_META_HOOK=SessionStart" "$(cat "$HOOKLOG")" "lane-inbox|SessionStart"
clear_lane; lane '{"hook_event_name":"PostToolUse","session_id":"s-5","tool_name":"Shell"}' > /dev/null
check "C1 PostToolUse: lane-inbox carries AW_META_HOOK=PostToolUse" "$(cat "$HOOKLOG")" "lane-inbox|PostToolUse"
clear_lane; lane "$STOPR"
check "C1 Stop: lane-reply and lane-stop-check carry AW_META_HOOK=Stop" "$(paste -sd' ' "$HOOKLOG")" "lane-reply|Stop lane-stop-check|Stop"
clear_lane; lane "$AAR"
check "C1 AfterAgentResponse: lane-reply carries AW_META_HOOK=AfterAgentResponse" "$(cat "$HOOKLOG")" "lane-reply|AfterAgentResponse"

# C2: the trace file, one line, "<UTC time> <hook> <event> <outcome>"
clear_lane; lane "$UPS" > /dev/null
check "C2 trace after a lane-inbox run: ran" "$(trace)" "meta-lane-gate UserPromptSubmit ran"
check "C2 trace is a single line" "$(wc -l < "$LS/api.hook" | tr -d ' ')" "1"
clear_lane; lane "$STOPR"
check "C2 trace after a Stop: ran" "$(trace)" "meta-lane-gate Stop ran"
clear_lane; printf '%s' "$UPS" > "$WORK/in"
AW_META_LANE_PID=999999 AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/meta-lane-gate.sh" 2> /dev/null < "$WORK/in"
check "C2 trace when the hook is not under the lane's agent: not-own-agent" "$(trace)" "meta-lane-gate UserPromptSubmit not-own-agent"
clear_lane; printf '%s' "$UPS" > "$WORK/in"
AW_META_BIN="$WORK/missing" AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/meta-lane-gate.sh" 2> /dev/null < "$WORK/in"
check "C2 trace when aw-meta is not installed: aw-meta-missing" "$(trace)" "meta-lane-gate UserPromptSubmit aw-meta-missing"
clear_lane; STUB_RC=2 lane "$UPS" > /dev/null
check "C2 trace when lane-inbox fails: lane-inbox-failed" "$(trace)" "meta-lane-gate UserPromptSubmit lane-inbox-failed"
clear_lane; STUB_REPLY_RC=2 STUB_RC=0 lane "$STOPR"
check "C2 trace when lane-reply fails: lane-reply-failed (the later lane-stop-check passing does not overwrite it)" "$(trace)" "meta-lane-gate Stop lane-reply-failed"
clear_lane; STUB_RC=2 lane '{"hook_event_name":"Stop","session_id":"s-5","last_assistant_message":"no marker"}'
check "C2 trace when lane-stop-check fails: lane-stop-check-failed" "$(trace)" "meta-lane-gate Stop lane-stop-check-failed"
clear_lane; lane "$UPS" > /dev/null; lane '{"hook_event_name":"Stop","session_id":"s-5","last_assistant_message":"no marker"}'
check "C2 trace is overwritten by the next hook run" "$(trace)" "meta-lane-gate Stop ran"
clear_lane; printf '%s' '{"hook_event_name":"SessionStart","session_id":"s-6"}' > "$WORK/in"
AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/session-id-report.sh" 2> /dev/null < "$WORK/in"
check "C2 session-id-report writes its trace: ran" "$(trace)" "session-id-report SessionStart ran"
clear_lane; AW_META_LANE_PID=999999 AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/session-id-report.sh" 2> /dev/null < "$WORK/in"
check "C2 session-id-report outside the lane's agent: not-own-agent" "$(trace)" "session-id-report SessionStart not-own-agent"
clear_lane; printf '%s' "$UPS" > "$WORK/in"; AW_JUDGE_CHILD=1 bash "$HOOKS/meta-lane-gate.sh" 2> /dev/null < "$WORK/in"
check "C2 outside a lane no trace is written" "$(ls -A "$LS" | wc -l | tr -d ' ')" "0"

# C5: the team's meta lane gets `aw-meta inbox` as context, never a lane command
printf '%s' '{"hook_event_name":"SessionStart","session_id":"s-7"}' > "$WORK/start-meta.json"
clear_lane
OUT="$(AW_META_CONFIG="$WORK/team.json" LANE_NAME=meta STUB_OUT="mail for the meta" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/ups.json")"
check "C5 meta lane UserPromptSubmit: the stub's inbox output is printed as plain text" "$OUT" "mail for the meta"
check "C5 meta lane UserPromptSubmit: calls inbox and never a lane-* command" "$(awk '{print $1}' "$CALLS" | paste -sd, -)" "inbox"
check "C5 meta lane trace: meta-lane" "$(trace meta)" "meta-lane-gate UserPromptSubmit meta-lane"
clear_lane
OUT="$(AW_META_CONFIG="$WORK/team.json" LANE_NAME=meta STUB_OUT="mail for the meta" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/start-meta.json")"
check "C5 meta lane SessionStart: additionalContext JSON with the inbox" "$(jq -r .hookSpecificOutput.additionalContext <<< "$OUT" 2> /dev/null)" "mail for the meta"
check "C5 meta lane SessionStart: calls inbox only" "$(awk '{print $1}' "$CALLS" | paste -sd, -)" "inbox"
clear_lane
AW_META_CONFIG="$WORK/team.json" LANE_NAME=meta STUB_RC=1 STUB_OUT="owes" bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/stop-meta.json" 2> /dev/null; RC=$?
check "C5 meta lane Stop: passes, nothing called, trace meta-lane" "$RC:$(cat "$CALLS"):$(trace meta)" "0::meta-lane-gate Stop meta-lane"
clear_lane
OUT="$(AW_META_CONFIG="$WORK/team.json" LANE_NAME=meta bash "$WORK/lane-script.sh" "$HOOKS/meta-lane-gate.sh" "$WORK/ups.json")"
check "C5 meta lane with an empty inbox prints nothing" "$OUT" ""

# C6: AfterAgentResponse records the reply, never runs lane-stop-check; Stop does not record it again
clear_lane
lane "$AAR"; RC=$?
check "C6 AfterAgentResponse: exit 0, the reply is recorded, lane-stop-check is not called" "$RC:$(cat "$CALLS")" "0:lane-reply --stdin"
check "C6 AfterAgentResponse: the reply text (marker removed) goes to lane-reply on stdin" "$(cat "$STDIN_SEEN")" "aar reply"
lane '{"hook_event_name":"Stop","session_id":"s-5","last_assistant_message":"Done.\nREPLY TO META: aar reply"}'
check "C6 a reply recorded at AfterAgentResponse is not recorded again at the next Stop with the same text" "$(grep -c '^lane-reply' "$CALLS"):$(grep -c '^lane-stop-check' "$CALLS")" "1:1"
clear_lane
STUB_RC=1 STUB_OUT="owes" lane "$AAR"; RC=$?
check "C6 AfterAgentResponse never blocks, even when the lane owes a reply" "$RC:$(cat "$WORK/err")" "0:"
clear_lane
lane '{"hook_event_name":"AfterAgentResponse","session_id":"s-5","last_assistant_message":"nothing to answer"}'
check "C6 AfterAgentResponse without the marker: aw-meta is not called" "$(cat "$CALLS")" ""

# C6: marker tolerance (Stop and AfterAgentResponse)
# row <text> <expected captured reply, "" = not captured>
for ev in AfterAgentResponse Stop; do
  while IFS='|' read -r label text want; do
    clear_lane
    lane "$(jq -nc --arg e "$ev" --arg m "$text" '{hook_event_name: $e, session_id: "s-5", last_assistant_message: $m}')"
    got="$(grep -c '^lane-reply' "$CALLS"):$(cat "$STDIN_SEEN")"
    if [ -n "$want" ]; then exp="1:$want"; else exp="0:"; fi
    check "C6 $ev marker: $label" "$got" "$exp"
  done <<'ROWS'
plain|REPLY TO META: x|x
bold marker|**REPLY TO META:** x|x
quoted|> REPLY TO META: x|x
list item|- REPLY TO META: x|x
star list item|* REPLY TO META: x|x
heading|## REPLY TO META: x|x
indented bold|  **REPLY TO META:** x|x
mid-line text is not a reply|see REPLY TO META: x|
mid-line after a quote mark is not a reply|> see REPLY TO META: x|
ROWS
done

# An answer that owes a reply but has no REPLY TO META: line is visible: trace reply-unmarked and a line in front of the block text
UNMARKED_LINE='Your last answer has no line starting REPLY TO META:, so meta did not get it. End the turn with REPLY TO META: <reply>.'
UNM='{"hook_event_name":"Stop","session_id":"s-5","last_assistant_message":"Rebased, all green."}'
clear_lane; STUB_RC=1 STUB_OUT="owes the meta a reply" lane "$UNM"; RC=$?
check "unmarked answer, lane owes a reply: exit 2, the line then the block text on stderr" "$RC:$(cat "$WORK/err")" "2:$UNMARKED_LINE
owes the meta a reply"
check "unmarked answer, lane owes a reply: trace reply-unmarked" "$(trace)" "meta-lane-gate Stop reply-unmarked"
printf '%s\n' '{"role":"assistant","message":{"content":[{"type":"text","text":"Rebased, all green."}]}}' > "$WORK/tr-unm.jsonl"
clear_lane
OUT="$(jq -nc --arg tp "$WORK/tr-unm.jsonl" '{hook_event_name:"stop",conversation_id:"c-1",loop_count:0,transcript_path:$tp}' | STUB_RC=1 STUB_OUT="owes the meta a reply" AW_META_TEAM=t1 AW_META_LANE=api bash "$CURSOR" "$HOOKS/meta-lane-gate.sh" 2> /dev/null)"
check "unmarked answer through the Cursor adapter: followup_message starts with the line" "$(jq -r .followup_message <<< "$OUT")" "$UNMARKED_LINE
owes the meta a reply"
clear_lane; lane "$UNM"; RC=$?
check "unmarked answer, nothing owed: allowed, no extra line, trace ran" "$RC:$(cat "$WORK/err"):$(trace)" "0::meta-lane-gate Stop ran"
clear_lane; STUB_REPLY_RC=0 STUB_RC=1 STUB_OUT="owes the meta a reply" lane "$STOPR"; RC=$?
check "marked reply, lane still owes (mail arrived mid-turn): no unmarked line, trace ran" "$RC:$(cat "$WORK/err"):$(trace)" "2:owes the meta a reply:meta-lane-gate Stop ran"

# A nested agent's not-own-agent trace does not replace the lane's own trace from this session
nested() { printf '%s' "$2" > "$WORK/in"; AW_META_LANE_PID=999999 AW_META_TEAM=t1 AW_META_LANE=api bash "$HOOKS/$1" 2> /dev/null < "$WORK/in"; }
clear_lane; STUB_RC=1 STUB_OUT="owes the meta a reply" lane "$UNM"
printf 'id-1\n' > "$LS/api"; touch -d '2 seconds ago' "$LS/api"
nested meta-lane-gate.sh "$UPS"
check "a nested agent's gate run keeps the lane's reply-unmarked trace (newer than the session id)" "$(trace)" "meta-lane-gate Stop reply-unmarked"
nested session-id-report.sh '{"hook_event_name":"SessionStart","session_id":"s-6"}'
check "a nested agent's session-id-report run keeps it too" "$(trace)" "meta-lane-gate Stop reply-unmarked"
touch "$LS/api"   # a new session id: the old trace is from an earlier session
nested meta-lane-gate.sh "$UPS"
check "a trace older than the session id is replaced by not-own-agent" "$(trace)" "meta-lane-gate UserPromptSubmit not-own-agent"

exit $fail
