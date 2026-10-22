#!/usr/bin/env bash
# Tests for config/hooks/fleet-state.sh (id aw:fleet; session collector CO-1, spec 2026-10-16-sindri-session-collector-design.md 5.4).
# Run: bash config/hooks/tests/fleet-state.test.sh   Everything runs under a temp HOME; nothing real is touched.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$DIR/../../.." && pwd)"
HOOK="$REPO/config/hooks/fleet-state.sh"
FIX="$REPO/tests/fixtures/fleet-input/claude-code"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
export AW_STATE_DIR="$WORK/state"
export AW_FLEET_DIR="$WORK/fleet"
unset AW_JUDGE_CHILD AW_FLEET_DISABLE AW_HOOK_PROVIDER
SID="3970893f-1c2d-4e5f-8a9b-0c1d2e3f4a5b"
FILE="$AW_FLEET_DIR/claude-code/$SID.json"

# Ajv comes from scripts/node_modules (a dev dependency of scripts/), installed once under a lock.
# shellcheck source=../../../scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1
valid() { node "$DIR/validate-fleet-state.mjs" "$1" 2>/dev/null && echo valid || echo invalid; }

# run <fixture-name>: the hook as Claude runs it (payload on stdin); sets OUT and RC.
run() { OUT="$(bash "$HOOK" < "$FIX/$1.json" 2>"$WORK/stderr")"; RC=$?; }
field() { jq -r "$1" "$FILE"; }
mode() { stat -c %a "$1" 2>/dev/null || stat -f %Lp "$1"; }
fresh() { rm -rf "$AW_FLEET_DIR"; }

# --- each Claude event of 5.2, in the order a session lives ---
step() { # <fixture> <state> <reason|-> <tool|->
  run "$1"
  check "$1: exit 0, silent" "$RC:$OUT" "0:"
  check "$1: stderr silent" "$(cat "$WORK/stderr")" ""
  check "$1: schema-valid file" "$(valid "$FILE")" "valid"
  check "$1: state" "$(field .state)" "$2"
  check "$1: reason" "$(field '.reason // "-"')" "$3"
  check "$1: tool" "$(field '.tool // "-"')" "$4"
}
step session-start idle - -
check "provider and directory are the AgentId" "$(field .provider)" "claude-code"
check "event is the hook event name" "$(field .event)" "SessionStart"
check "session id" "$(field .sessionId)" "$SID"
check "cwd" "$(field .cwd)" "/home/dev/repo"
check "transcript" "$(field .transcript)" "/home/dev/.claude/projects/-home-dev-repo/$SID.jsonl"
check "env is {} on Linux" "$(field '.env | length')" "0"
check "at is a millisecond UTC stamp" "$(field .at | grep -cE '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z$')" "1"
step prompt-submit running - -
step permission-request asking approval Bash
cp "$FILE" "$WORK/before-notification"
step notification-permission asking approval Bash   # the notification that follows the request leaves the file as it is
check "permission_prompt after the request: the file is byte-identical" "$(cmp -s "$FILE" "$WORK/before-notification" && echo same || echo changed)" "same"
step post-tool-use running - -
step notification-elicitation asking input -
step post-tool-use running - -
step notification-idle idle - -
cp "$FILE" "$WORK/before-other"
run notification-other
check "other notification types write nothing" "$(cmp -s "$FILE" "$WORK/before-other" && echo same || echo changed):$RC:$OUT" "same:0:"
step prompt-submit running - -
step stop idle - -
step session-end ended - -

# --- a permission_prompt notification never writes: the request wrote asking, and a late notification must not undo an approval ---
fresh
run notification-permission
check "notification alone: nothing written" "$([ -e "$FILE" ] && echo file || echo none):$RC:$OUT" "none:0:"
for f in permission-request post-tool-use notification-permission; do run "$f"; done
check "request, approval, then the late notification: ends running" "$(field '[.state, .event, (.reason // "-")] | join(" ")')" "running PostToolUse -"
run notification-elicitation
cp "$FILE" "$WORK/before-late"
run notification-permission
check "permission_prompt while asking input: unchanged" "$(field '[.state, .reason] | join(" ")'):$(cmp -s "$FILE" "$WORK/before-late" && echo same || echo changed)" "asking input:same"
fresh; run session-start; run notification-permission
check "permission_prompt on an idle file: unchanged" "$(field .state)" "idle"

# --- PostToolUse over an empty or unparseable file leaves it alone ---
fresh; run session-start
: > "$FILE"; run post-tool-use
check "PostToolUse on an empty file: exit 0, still empty" "$RC:$OUT:$(wc -c < "$FILE" | tr -d ' ')" "0::0"
echo 'not json {' > "$FILE"; run post-tool-use
check "PostToolUse on an unparseable file: exit 0, untouched" "$RC:$(cat "$FILE")" "0:not json {"

# --- no payload text in the file ---
fresh
for f in session-start prompt-submit permission-request notification-permission notification-elicitation post-tool-use stop session-end; do run "$f"; done
run permission-request
check "no tool_input, command, message or prompt in the file" "$(grep -cE 'tool_input|"command"|"message"|List files|list the files|needs your' "$FILE")" "0"
check "only schema keys" "$(jq -r 'keys | join(",")' "$FILE")" "agentPid,agentStartTicks,at,cwd,env,event,provider,reason,schema,sessionId,state,tool,transcript"

# --- modes: file 0600, directories 0700, marker ---
check "file mode 0600" "$(mode "$FILE")" "600"
check "provider dir mode 0700" "$(mode "$AW_FLEET_DIR/claude-code")" "700"
check "provider dir has the .aw-fleet marker" "$([ -f "$AW_FLEET_DIR/claude-code/.aw-fleet" ] && echo yes || echo no)" "yes"
check "no temp file left beside the state file" "$(ls -A "$AW_FLEET_DIR/claude-code" | grep -v -e "^$SID.json$" -e '^.aw-fleet$' | wc -l | tr -d ' ')" "0"

# --- PostToolUse writes only when the file says asking, and otherwise exits before jq ---
SHIM="$WORK/shim"; mkdir -p "$SHIM"
cat > "$SHIM/jq" <<SH
#!/usr/bin/env bash
echo x >> "$WORK/jq-calls"
exec "$(command -v jq)" "\$@"
SH
chmod +x "$SHIM/jq"
jqcalls() { if [ -f "$WORK/jq-calls" ]; then wc -l < "$WORK/jq-calls" | tr -d ' '; else echo 0; fi; }
fresh; rm -f "$WORK/jq-calls"
PATH="$SHIM:$PATH" bash "$HOOK" < "$FIX/post-tool-use.json" > "$WORK/o" 2>&1; rc=$?
check "PostToolUse with no file: exit 0, no file, no output, no jq" "$rc:$([ -e "$FILE" ] && echo file || echo none):$(cat "$WORK/o"):$(jqcalls)" "0:none::0"
run prompt-submit
cp "$FILE" "$WORK/running"; rm -f "$WORK/jq-calls"
PATH="$SHIM:$PATH" bash "$HOOK" < "$FIX/post-tool-use.json" > "$WORK/o" 2>&1; rc=$?
check "PostToolUse on a running file: unchanged, no jq" "$rc:$(cmp -s "$FILE" "$WORK/running" && echo same || echo changed):$(jqcalls)" "0:same:0"
run permission-request; rm -f "$WORK/jq-calls"
PATH="$SHIM:$PATH" bash "$HOOK" < "$FIX/post-tool-use.json" > "$WORK/o" 2>&1; rc=$?
check "PostToolUse on an asking file: running, one jq" "$rc:$(field .state):$(jqcalls)" "0:running:1"

# --- refusals: exit 0, no file, no output ---
refuse() { # <label> <fleet-dir> <stdin-file>
  local out rc
  rm -rf "$2"
  out="$(AW_FLEET_DIR="$2" bash "$HOOK" < "$3" 2>&1)"; rc=$?
  check "$1: exit 0, no output" "$rc:$out" "0:"
  check "$1: no state file written" "$(find "$2" -name '*.json' 2>/dev/null | wc -l | tr -d ' ')" "0"
}
jq '.session_id = "../escape"' "$FIX/session-start.json" > "$WORK/bad-id.json"
refuse "path as session id" "$AW_FLEET_DIR" "$WORK/bad-id.json"
check "path as session id: no directory made" "$([ -e "$AW_FLEET_DIR" ] && echo made || echo none)" "none"
jq '.session_id = ""' "$FIX/session-start.json" > "$WORK/empty-id.json"
refuse "empty session id" "$AW_FLEET_DIR" "$WORK/empty-id.json"
jq 'del(.session_id)' "$FIX/session-start.json" > "$WORK/no-id.json"
refuse "missing session id" "$AW_FLEET_DIR" "$WORK/no-id.json"
echo 'this is not json {' > "$WORK/garbage.json"
refuse "bad payload" "$AW_FLEET_DIR" "$WORK/garbage.json"
: > "$WORK/empty.json"
refuse "empty stdin" "$AW_FLEET_DIR" "$WORK/empty.json"
jq '.hook_event_name = "SomethingElse"' "$FIX/session-start.json" > "$WORK/unknown-event.json"
refuse "unknown event" "$AW_FLEET_DIR" "$WORK/unknown-event.json"
jq 'del(.cwd)' "$FIX/session-start.json" > "$WORK/no-cwd.json"
refuse "missing cwd" "$AW_FLEET_DIR" "$WORK/no-cwd.json"
echo file > "$WORK/a-file"
refuse "unwritable dir (a file in the way)" "$WORK/a-file/fleet" "$FIX/session-start.json"
jq '.tool_name = "bad name; touch x"' "$FIX/permission-request.json" > "$WORK/bad-tool.json"
fresh
bash "$HOOK" < "$WORK/bad-tool.json" > "$WORK/o" 2>&1
check "tool name outside the pattern: asking approval without a tool, still valid" "$(valid "$FILE"):$(field '(.reason) + " " + (.tool // "-")')" "valid:approval -"

# --- off switches ---
fresh
AW_FLEET_DISABLE=1 bash "$HOOK" < "$FIX/session-start.json" > "$WORK/o" 2>&1
check "AW_FLEET_DISABLE=1 writes nothing" "$(cat "$WORK/o"):$([ -e "$AW_FLEET_DIR" ] && echo made || echo none)" ":none"
AW_JUDGE_CHILD=1 bash "$HOOK" < "$FIX/session-start.json" > "$WORK/o" 2>&1
check "AW_JUDGE_CHILD set writes nothing" "$(cat "$WORK/o"):$([ -e "$AW_FLEET_DIR" ] && echo made || echo none)" ":none"

# --- provider mapping (5.1): unset or claude -> claude-code; codex, cursor as is; anything else nothing ---
fresh
AW_HOOK_PROVIDER=claude bash "$HOOK" < "$FIX/session-start.json"
check "AW_HOOK_PROVIDER=claude -> claude-code" "$(ls "$AW_FLEET_DIR")" "claude-code"
fresh
AW_HOOK_PROVIDER=codex bash "$HOOK" < "$FIX/session-start.json"
check "AW_HOOK_PROVIDER=codex -> codex" "$(ls "$AW_FLEET_DIR"):$(jq -r .provider "$AW_FLEET_DIR/codex/$SID.json")" "codex:codex"
fresh
AW_HOOK_PROVIDER=cursor bash "$HOOK" < "$FIX/session-start.json"
check "AW_HOOK_PROVIDER=cursor -> cursor" "$(ls "$AW_FLEET_DIR")" "cursor"
fresh
AW_HOOK_PROVIDER=aider bash "$HOOK" < "$FIX/session-start.json"
check "any other AW_HOOK_PROVIDER writes nothing" "$([ -e "$AW_FLEET_DIR" ] && echo made || echo none)" "none"

# --- default directory: $AW_STATE_DIR/fleet ---
rm -rf "$AW_STATE_DIR"
(unset AW_FLEET_DIR; bash "$HOOK" < "$FIX/session-start.json")
check "AW_FLEET_DIR unset: \$AW_STATE_DIR/fleet" "$([ -f "$AW_STATE_DIR/fleet/claude-code/$SID.json" ] && echo yes || echo no)" "yes"

# --- agent pid and start ticks (5.4): the nearest ancestor whose argv[0] is claude ---
if [ -r /proc/self/stat ]; then
  fresh
  # A process named claude (exec -a) that runs the hook as its child and then reports its own pid and start ticks (stat field 22).
  ( exec -a claude bash -c "bash '$HOOK' < '$FIX/session-start.json'; echo \$\$ > '$WORK/agent-pid'; sed 's/^.*) //' /proc/\$\$/stat | cut -d' ' -f20 > '$WORK/agent-ticks'; true" )
  check "agentPid is the claude ancestor" "$(field .agentPid)" "$(cat "$WORK/agent-pid")"
  check "agentStartTicks is its stat start time" "$(field .agentStartTicks)" "$(cat "$WORK/agent-ticks")"
  check "ancestor found: schema-valid" "$(valid "$FILE")" "valid"
  fresh
  # Orphaned (setsid -f reparents it to init), so the test passes even when it runs inside a Claude session.
  setsid -f bash -c "bash '$HOOK' < '$FIX/session-start.json'; echo done > '$WORK/orphan-done'" < /dev/null > /dev/null 2>&1
  for _ in $(seq 1 50); do [ -f "$WORK/orphan-done" ] && break; sleep 0.1; done
  check "no claude ancestor: pid and ticks are null" "$(field '[.agentPid, .agentStartTicks] | @json')" "[null,null]"
  check "no claude ancestor: schema-valid" "$(valid "$FILE")" "valid"
fi

# --- agent pid and start time on macOS: no /proc, so one ps per ancestor step; the start is ps's lstart in UTC as epoch seconds ---
if [ ! -r /proc/self/stat ] && [ -x /bin/ps ]; then
  fresh
  # A process named claude (exec -a) that runs the hook as its child and then reports its own pid and start (ps lstart, UTC).
  ( exec -a claude bash -c "bash '$HOOK' < '$FIX/session-start.json'; echo \$\$ > '$WORK/agent-pid'; TZ=UTC /bin/ps -p \$\$ -o lstart= > '$WORK/agent-lstart'; true" )
  expected="$(date -j -u -f '%a %b %e %T %Y' "$(cat "$WORK/agent-lstart")" +%s)"
  check "macOS: agentPid is the claude ancestor" "$(field .agentPid)" "$(cat "$WORK/agent-pid")"
  check "macOS: agentStartTicks is its lstart in epoch seconds" "$(field .agentStartTicks)" "$expected"
  check "macOS: ancestor found: schema-valid" "$(valid "$FILE")" "valid"
  fresh
  # Orphaned (its parent exits, so launchd adopts it), so the test passes even when it runs inside a Claude session.
  ( bash -c "sleep 0.3; bash '$HOOK' < '$FIX/session-start.json'; echo done > '$WORK/orphan-done'" < /dev/null > /dev/null 2>&1 & )
  for _ in $(seq 1 50); do [ -f "$WORK/orphan-done" ] && break; sleep 0.1; done
  check "macOS: no claude ancestor: pid and ticks are null" "$(field '[.agentPid, .agentStartTicks] | @json')" "[null,null]"
  check "macOS: no claude ancestor: schema-valid" "$(valid "$FILE")" "valid"
fi

# --- under 100 ms per invocation (best of 5, every event) ---
# bash 5 has EPOCHREALTIME; macOS's bash 3.2 doesn't, so perl's clock stands in there.
now_us() { if [ -n "${EPOCHREALTIME:-}" ]; then echo "${EPOCHREALTIME/./}"; else perl -MTime::HiRes=time -e 'printf "%d\n", time * 1000000'; fi; }
fresh
slowest=0
for f in session-start prompt-submit permission-request notification-permission post-tool-use stop session-end; do
  best=999999
  for _ in 1 2 3 4 5; do
    s="$(now_us)"; bash "$HOOK" < "$FIX/$f.json"; e="$(now_us)"
    ms=$(( (e - s) / 1000 )); [ "$ms" -lt "$best" ] && best=$ms
  done
  [ "$best" -gt "$slowest" ] && slowest=$best
done
check "every event runs under 100 ms (slowest best-of-5: ${slowest} ms)" "$([ "$slowest" -lt 100 ] && echo fast || echo slow)" "fast"
exit $fail
