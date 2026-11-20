#!/usr/bin/env bash
# HOOK-FAILOPEN coverage for every hook that can fail open
# (spec 2026-10-09-agent-readiness-design.md §4c, B5).
#
# Each case runs the real hook twice from an installed-style layout (hooks next
# to lib/): the current hook with lib/failopen.sh, and the baseline copy of the
# same hook from before the logging was added (fixtures/failopen-baseline/,
# origin/main at 2004bdc). Logging is the only allowed difference, so:
#   - stdout, stderr (stage path and bash's "line N:" normalized, "write error: Broken pipe" lines dropped) and the exit
#     code must be byte-identical between the two runs;
#   - every run is bounded (10 s): a hang counts as exit 142 and fails;
#   - the run with the lib appends exactly the expected HOOK-FAILOPEN lines
#     ("-" = a normal path that must log nothing, "?" = don't check the log);
#   - no log line ever carries payload content.
# Run: bash config/hooks/tests/failopen.test.sh
set -uo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HOOKS="$ROOT/config/hooks"
LIBDIR="$ROOT/config/lib"
fail=0
# The cases set a lane's variables themselves ($LANE); the suite must not inherit another lane's or a Sindri launch's.
# AW_META_DIR too: it would move the lane reply marker out of each case's AW_STATE_DIR into one shared dir, so a run (or a parallel run) that records a reply makes the next skip lane-reply.
unset TMUX TMUX_PANE AW_META_DIR AW_META_CONFIG AW_META_TEAM AW_META_LANE AW_META_LANE_PID AW_META_LANE_AGENT AW_META_LANE_PROC AW_SINDRI_CHILD AW_SINDRI_LAUNCH AW_SINDRI_HOME AW_LAUNCHER_CONFIG AW_JUDGE_CHILD
SECRET="SENTINEL-PAYLOAD-7f3a"

BASELINE="$HOOKS/tests/fixtures/failopen-baseline"

# stage_current <with-locks 0|1> / stage_baseline <with-locks 0|1> — a temp
# hooks dir laid out the way the installers lay it out (hooks, adapters/, lib/).
stage_current() {
  local d; d="$(mktemp -d)"
  cp "$HOOKS"/*.sh "$d/"
  mkdir -p "$d/lib" "$d/adapters"
  cp "$HOOKS/adapters/"*.sh "$d/adapters/"
  [ "$1" = 1 ] && cp "$LIBDIR/locks.sh" "$d/lib/locks.sh"
  cp "$LIBDIR/failopen.sh" "$d/lib/failopen.sh"
  printf '%s' "$d"
}
stage_baseline() {
  local d; d="$(mktemp -d)"
  cp "$BASELINE"/*.sh "$d/"
  mkdir -p "$d/lib" "$d/adapters"
  cp "$BASELINE/adapters/"*.sh "$d/adapters/"
  [ "$1" = 1 ] && cp "$BASELINE/lib/locks.sh" "$d/lib/locks.sh"
  printf '%s' "$d"
}

# path_without <tool...> — a PATH dir holding every tool in /usr/bin and /bin
# (and jq's dir) except the named ones.
path_without() {
  local d f name skip; d="$(mktemp -d)"
  for f in /usr/bin/* /bin/* "$(dirname "$(command -v jq)")"/jq; do
    name="$(basename "$f")"; skip=0
    for t in "$@"; do [ "$name" = "$t" ] && skip=1; done
    [ "$skip" = 1 ] || [ -e "$d/$name" ] || ln -s "$f" "$d/$name"
  done
  printf '%s' "$d"
}

FAKE_BIN="$(mktemp -d)"
JQ_DIR="$(dirname "$(command -v jq)")"
BASE_PATH="/usr/bin:/bin:$JQ_DIR"
# judge stub: JUDGE_MODE picks the behavior for every subcommand.
# It reads all of stdin first, like the real judge: the hooks run `printf | judge` under pipefail, so a stub that answers
# without reading lets printf die of SIGPIPE (141) whenever the stub wins the race. On a loaded box that is a different
# cause or decision in the current run and the baseline run, a flake in the comparison below, not a hook difference.
cat > "$FAKE_BIN/judge" <<'EOF'
#!/usr/bin/env bash
cat > /dev/null
case "${JUDGE_MODE:-}" in
  exit1) exit 1 ;;
  garbage) echo 'not-json'; exit 0 ;;
  empty) echo '{}'; exit 0 ;;
  nobrief)
    [ "$1" = brief ] && { echo "no brief for tool_use_id: $3" >&2; exit 1; }
    exit 0 ;;
  *) echo "{\"decision\":\"$JUDGE_MODE\"}"; exit 0 ;;
esac
EOF
chmod +x "$FAKE_BIN/judge"
NO_JUDGE_BIN="$(mktemp -d)"
# jq wrapper that fails one chosen invocation (FAIL_JQ_ON = a substring of its args).
JQ_FAIL_BIN="$(mktemp -d)"
cat > "$JQ_FAIL_BIN/jq" <<EOF
#!/usr/bin/env bash
case "\$*" in *"\${FAIL_JQ_ON:-@@never@@}"*) exit 5 ;; esac
exec "$(command -v jq)" "\$@"
EOF
chmod +x "$JQ_FAIL_BIN/jq"

# bounded <out-prefix> <stdin-file> <cmd...> — run with a 10 s watchdog; writes
# <out>.o, <out>.e and <out>.r (142 when the watchdog had to kill it).
bounded() {
  local out="$1" in="$2"; shift 2
  "$@" < "$in" > "$out.o" 2> "$out.e" &
  local p=$! i=0
  while kill -0 "$p" 2>/dev/null; do
    if [ "$i" -ge 200 ]; then kill -9 "$p" 2>/dev/null; wait "$p" 2>/dev/null; echo 142 > "$out.r"; return; fi
    sleep 0.05; i=$((i + 1))
  done
  wait "$p"; echo $? > "$out.r"
}

# run_case <name> <hook path inside the stage dir> <stdin> <expected> [VAR=value ...]
# Env: CASE_PATH (default fake judge on PATH), LOCKS (default 1), WANT_RC,
# NO_HOME=1 (HOME and AW_STATE_DIR unset), FIFO_LOG=1 (logs/failopen.log is a
# FIFO), HOOK_ARGS (adapter argv), INJECT (a line inserted after INPUT=$(cat) or input=$(cat)
# in both copies of the hook).
run_case() {
  local name="$1" hook="$2" input="$3" want="$4"; shift 4
  local cur base st t envs=() log got r1 r2
  cur="$(stage_current "${LOCKS:-1}")"; base="$(stage_baseline "${LOCKS:-1}")"
  st="$(mktemp -d)"; t="$(mktemp -d)"
  if [ -n "${INJECT:-}" ]; then
    for d in "$cur" "$base"; do
      sed -i.bak -E "/^(INPUT|input)=/a\\
$INJECT
" "$d/$hook"
    done
  fi
  if [ "${FIFO_LOG:-0}" = 1 ]; then mkdir -p "$st/logs"; mkfifo "$st/logs/failopen.log"; fi
  printf '%s' "$input" > "$t/in"
  if [ "${NO_HOME:-0}" = 1 ]; then envs=(-u HOME -u AW_STATE_DIR); else envs=(AW_STATE_DIR="$st"); fi
  # shellcheck disable=SC2086
  bounded "$t/1" "$t/in" env ${envs[@]+"${envs[@]}"} PATH="${CASE_PATH:-$FAKE_BIN:$BASE_PATH}" "$@" bash "$cur/$hook" ${HOOK_ARGS:-}
  # shellcheck disable=SC2086
  bounded "$t/2" "$t/in" env ${envs[@]+"${envs[@]}"} PATH="${CASE_PATH:-$FAKE_BIN:$BASE_PATH}" "$@" bash "$base/$hook" ${HOOK_ARGS:-}
  r1="$(cat "$t/1.r")"; r2="$(cat "$t/2.r")"
  # A reader that exits without reading stdin (a missing judge, a hook that exits 3) races the writer's printf: on a loaded
  # box bash adds "printf: write error: Broken pipe" to stderr in one run and not the other. That is harness timing, not
  # hook behavior, so those lines are dropped from the comparison; rc and stdout still must match.
  sed -e "s#$cur#<stage>#g" -e 's#line [0-9]*:#line N:#g' -e '/write error: Broken pipe$/d' "$t/1.e" > "$t/1.en"
  sed -e "s#$base#<stage>#g" -e 's#line [0-9]*:#line N:#g' -e '/write error: Broken pipe$/d' "$t/2.e" > "$t/2.en"
  if ! cmp -s "$t/1.o" "$t/2.o" || ! cmp -s "$t/1.en" "$t/2.en" || [ "$r1" != "$r2" ]; then
    echo "FAIL: $name — differs from the baseline (origin/main) hook"
    echo "  current:  rc=$r1 out=$(cat "$t/1.o") err=$(cat "$t/1.en")"
    echo "  baseline: rc=$r2 out=$(cat "$t/2.o") err=$(cat "$t/2.en")"
    fail=1; return
  fi
  if [ -n "${WANT_RC:-}" ] && [ "$r1" != "$WANT_RC" ]; then
    echo "FAIL: $name — expected exit $WANT_RC, got $r1"; fail=1; return
  fi
  log="$st/logs/failopen.log"
  got=""; [ -f "$log" ] && got="$(jq -r 'select(.event == "HOOK-FAILOPEN") | .hook + " " + .cause' "$log" 2>/dev/null | paste -sd';' -)"
  case "$want" in
    "?") ;;
    "-") [ -z "$got" ] || { echo "FAIL: $name — expected no HOOK-FAILOPEN, got: $got"; fail=1; return; } ;;
    *) [ "$got" = "$want" ] || { echo "FAIL: $name — expected '$want', got '$got'"; fail=1; return; } ;;
  esac
  if [ -f "$log" ] && grep -q "$SECRET" "$log"; then
    echo "FAIL: $name — payload content reached the log"; fail=1; return
  fi
  echo "PASS: $name (rc=$r1)"
}

SEND="{\"session_id\":\"s1\",\"tool_name\":\"SendMessage\",\"tool_input\":{\"to\":\"main\",\"message\":\"$SECRET progress\"}}"

# --- send-gate.sh (PreToolUse SendMessage) ---
CASE_PATH="$NO_JUDGE_BIN:$BASE_PATH" run_case "send-gate: judge missing" send-gate.sh "$SEND" "send-gate judge-exit-127"
run_case "send-gate: judge exits 1" send-gate.sh "$SEND" "send-gate judge-exit-1" JUDGE_MODE=exit1
run_case "send-gate: judge prints garbage" send-gate.sh "$SEND" "send-gate judge-output-unparsable" JUDGE_MODE=garbage
run_case "send-gate: judge gives no decision" send-gate.sh "$SEND" "send-gate judge-decision-missing" JUDGE_MODE=empty
run_case "send-gate: empty stdin" send-gate.sh "" "send-gate empty-input"
run_case "send-gate: unparsable stdin" send-gate.sh "$SECRET {" "send-gate input-unparsable"
run_case "send-gate: SendMessage without a message" send-gate.sh '{"session_id":"s1","tool_name":"SendMessage","tool_input":{"to":"main"}}' "send-gate field-missing"
LOCKS=0 run_case "send-gate: locks lib missing" send-gate.sh "$SEND" "send-gate locks-lib-missing"
run_case "send-gate: control message is not a failure" send-gate.sh '{"session_id":"s1","tool_name":"SendMessage","tool_input":{"type":"shutdown_request"}}' "-"
run_case "send-gate: send decision logs nothing" send-gate.sh "$SEND" "-" JUDGE_MODE=send
run_case "send-gate: drop decision logs nothing" send-gate.sh "$SEND" "-" JUDGE_MODE=drop
run_case "send-gate: unknown decision fails closed (queued), logs nothing" send-gate.sh "$SEND" "-" JUDGE_MODE=maybe

# --- scope-gate.sh (PreToolUse Agent) ---
AGENT="{\"session_id\":\"s1\",\"tool_name\":\"Agent\",\"tool_use_id\":\"tu1\",\"tool_input\":{\"subagent_type\":\"x\",\"description\":\"g\",\"prompt\":\"$SECRET\"}}"
CASE_PATH="$NO_JUDGE_BIN:$BASE_PATH" run_case "scope-gate: judge missing" scope-gate.sh "$AGENT" "scope-gate judge-exit-127"
run_case "scope-gate: judge exits 1" scope-gate.sh "$AGENT" "scope-gate judge-exit-1" JUDGE_MODE=exit1
run_case "scope-gate: judge prints garbage" scope-gate.sh "$AGENT" "scope-gate judge-output-unparsable" JUDGE_MODE=garbage
run_case "scope-gate: judge gives no decision" scope-gate.sh "$AGENT" "scope-gate judge-decision-missing" JUDGE_MODE=empty
run_case "scope-gate: unknown decision" scope-gate.sh "$AGENT" "scope-gate judge-decision-unknown" JUDGE_MODE=maybe
run_case "scope-gate: unparsable stdin" scope-gate.sh "nope" "scope-gate input-unparsable"
run_case "scope-gate: empty stdin" scope-gate.sh "" "scope-gate empty-input"
run_case "scope-gate: Agent call without tool_use_id" scope-gate.sh '{"tool_name":"Agent","tool_input":{}}' "scope-gate field-missing"
run_case "scope-gate: missing decision denies, logs nothing" scope-gate.sh "$AGENT" "-" JUDGE_MODE=missing

# --- external-write-guard.sh (PreToolUse gh pr merge|comment|review, Linear, Slack) ---
SESS="$(mktemp -d)"; printf 'not json' > "$SESS/s9.json"
run_case "external-write-guard: matched write without session id" external-write-guard.sh \
  '{"tool_name":"Bash","tool_input":{"command":"gh pr merge 1"}}' "external-write-guard session-id-missing"
run_case "external-write-guard: session state unreadable" external-write-guard.sh \
  '{"session_id":"s9","tool_name":"Bash","tool_input":{"command":"gh pr merge 1"}}' "external-write-guard session-state-unreadable" AW_JUDGE_SESSIONS_DIR="$SESS"
run_case "external-write-guard: unparsable stdin" external-write-guard.sh "$SECRET" "external-write-guard input-unparsable"
run_case "external-write-guard: empty stdin" external-write-guard.sh "" "external-write-guard empty-input"
run_case "external-write-guard: unmatched tool logs nothing" external-write-guard.sh '{"tool_name":"Bash","tool_input":{"command":"ls"}}' "-"

# --- done-gate.sh (Stop) ---
NOASSIST="$(mktemp)"; echo '{"type":"user","message":{"content":"hi"}}' > "$NOASSIST"
BROKEN="$(mktemp)"; echo '{"type":"assistant", broken' > "$BROKEN"
CLAIM="$(mktemp)"; echo '{"type":"assistant","message":{"content":[{"type":"text","text":"All done."}]}}' > "$CLAIM"
WORKING="$(mktemp)"; echo '{"type":"assistant","message":{"content":[{"type":"text","text":"Still working on the parser."}]}}' > "$WORKING"
run_case "done-gate: transcript unreadable" done-gate.sh '{"session_id":"s1","transcript_path":"/nonexistent/t.jsonl"}' "done-gate transcript-unreadable"
run_case "done-gate: no assistant line" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$NOASSIST\"}" "done-gate no-assistant-line"
run_case "done-gate: assistant line unparsable" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$BROKEN\"}" "done-gate transcript-line-unparsable"
run_case "done-gate: unparsable stdin" done-gate.sh "$SECRET" "done-gate input-unparsable"
run_case "done-gate: empty stdin" done-gate.sh "" "done-gate empty-input"
run_case "done-gate: ask-check judge fails" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$WORKING\"}" "done-gate judge-ask-check-exit-1" JUDGE_MODE=exit1
run_case "done-gate: brief lookup judge fails" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$CLAIM\"}" "done-gate judge-brief-exit-1" JUDGE_MODE=exit1
CASE_PATH="$NO_JUDGE_BIN:$BASE_PATH" run_case "done-gate: judge missing for the brief" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$CLAIM\"}" "done-gate judge-brief-exit-127"
run_case "done-gate: no brief is not a failure" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$CLAIM\"}" "-" JUDGE_MODE=nobrief
run_case "done-gate: re-invocation logs nothing" done-gate.sh '{"stop_hook_active":true}' "-"
run_case "done-gate: no claim source logs nothing" done-gate.sh '{"session_id":"s1"}' "-"

# --- outbox-flush.sh (TeammateIdle) ---
OUTBOX="$(mktemp -d)"; printf 'not json\n' > "$OUTBOX/s1.jsonl"
run_case "outbox-flush: outbox unreadable" outbox-flush.sh '{"session_id":"s1","teammate_name":"t"}' "outbox-flush outbox-unreadable" AW_OUTBOX_DIR="$OUTBOX"
run_case "outbox-flush: unparsable stdin" outbox-flush.sh "$SECRET" "outbox-flush input-unparsable"
run_case "outbox-flush: empty stdin" outbox-flush.sh "" "outbox-flush empty-input"

# --- meta-stop-gate.sh (Stop) and meta-outbox.sh (PostToolUse SendMessage), against a stub aw-meta ---
printf '#!/usr/bin/env bash\ncat > /dev/null\nexit "${META_MODE:-0}"\n' > "$FAKE_BIN/aw-meta"
chmod +x "$FAKE_BIN/aw-meta"
META_STOP='{"session_id":"s1","transcript_path":"/t/s1.jsonl","stop_hook_active":false}'
META_SEND='{"session_id":"s1","tool_name":"SendMessage","tool_input":{"to":"x","message":"y"}}'
run_case "meta-stop-gate: unparsable stdin" meta-stop-gate.sh "$SECRET" "meta-stop-gate input-unparsable" AW_META_BIN="$FAKE_BIN/aw-meta"
run_case "meta-stop-gate: empty stdin" meta-stop-gate.sh "" "meta-stop-gate empty-input" AW_META_BIN="$FAKE_BIN/aw-meta"
run_case "meta-stop-gate: no transcript_path" meta-stop-gate.sh '{"session_id":"s1"}' "meta-stop-gate field-missing" AW_META_BIN="$FAKE_BIN/aw-meta"
run_case "meta-stop-gate: aw-meta errors" meta-stop-gate.sh "$META_STOP" "meta-stop-gate meta-exit-2" AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
run_case "meta-stop-gate: nothing owed logs nothing" meta-stop-gate.sh "$META_STOP" "-" AW_META_BIN="$FAKE_BIN/aw-meta"
run_case "meta-outbox: empty stdin" meta-outbox.sh "" "meta-outbox empty-input" AW_META_BIN="$FAKE_BIN/aw-meta"
run_case "meta-outbox: aw-meta errors" meta-outbox.sh "$META_SEND" "meta-outbox meta-exit-2" AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
run_case "meta-outbox: recorded send logs nothing" meta-outbox.sh "$META_SEND" "-" AW_META_BIN="$FAKE_BIN/aw-meta"
# --- meta-lane-gate.sh (non-Claude lane hook) and session-id-report.sh (SessionStart), in a lane ---
LANE="AW_META_TEAM=t1 AW_META_LANE=api AW_META_LANE_PID=$$ AW_META_LANE_AGENT=codex AW_META_LANE_PROC=codex"
META_LANE_PROMPT='{"hook_event_name":"UserPromptSubmit","session_id":"s1","prompt":"hi"}'
META_LANE_STOP="{\"hook_event_name\":\"Stop\",\"session_id\":\"s1\",\"stop_hook_active\":false,\"last_assistant_message\":\"done\\nREPLY TO META: $SECRET ok\"}"
# shellcheck disable=SC2086
run_case "meta-lane-gate: unparsable stdin" meta-lane-gate.sh "$SECRET" "meta-lane-gate input-unparsable" $LANE AW_META_BIN="$FAKE_BIN/aw-meta"
# shellcheck disable=SC2086
run_case "meta-lane-gate: empty stdin" meta-lane-gate.sh "" "meta-lane-gate empty-input" $LANE AW_META_BIN="$FAKE_BIN/aw-meta"
# shellcheck disable=SC2086
run_case "meta-lane-gate: lane-inbox errors" meta-lane-gate.sh "$META_LANE_PROMPT" "meta-lane-gate lane-inbox-exit-2" $LANE AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
# shellcheck disable=SC2086
run_case "meta-lane-gate: lane-stop-check errors, lane-reply errors" meta-lane-gate.sh "$META_LANE_STOP" "meta-lane-gate lane-reply-exit-2;meta-lane-gate lane-stop-check-exit-2" $LANE AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
# shellcheck disable=SC2086
run_case "meta-lane-gate: nothing owed logs nothing" meta-lane-gate.sh "$META_LANE_STOP" "-" $LANE AW_META_BIN="$FAKE_BIN/aw-meta"
# shellcheck disable=SC2086
run_case "meta-board-line: unparsable stdin" meta-board-line.sh "$SECRET" "meta-board-line input-unparsable" $LANE AW_META_BIN="$FAKE_BIN/aw-meta"
# shellcheck disable=SC2086
run_case "meta-board-line: board-line errors" meta-board-line.sh "$META_LANE_PROMPT" "meta-board-line board-line-exit-2" $LANE AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
# shellcheck disable=SC2086
run_case "meta-board-line: unchanged board logs nothing" meta-board-line.sh "$META_LANE_PROMPT" "-" $LANE AW_META_BIN="$FAKE_BIN/aw-meta"
run_case "meta-board-line: outside a lane logs nothing" meta-board-line.sh "$SECRET" "-" AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
# shellcheck disable=SC2086
run_case "meta-lane-gate: outside a lane logs nothing" meta-lane-gate.sh "$SECRET" "-" AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
# shellcheck disable=SC2086
run_case "session-id-report: unparsable stdin" session-id-report.sh "$SECRET" "session-id-report input-unparsable" $LANE
# shellcheck disable=SC2086
run_case "session-id-report: empty stdin" session-id-report.sh "" "session-id-report empty-input" $LANE
# shellcheck disable=SC2086
run_case "session-id-report: no session_id" session-id-report.sh '{"hook_event_name":"SessionStart"}' "session-id-report field-missing" $LANE
# shellcheck disable=SC2086
run_case "session-id-report: an id that is not an id" session-id-report.sh '{"session_id":"../x y"}' "session-id-report id-invalid" $LANE
# shellcheck disable=SC2086
run_case "session-id-report: a lane name that is a path" session-id-report.sh '{"session_id":"abc-123"}' "session-id-report lane-name-invalid" AW_META_TEAM=t1 AW_META_LANE=.. AW_META_LANE_PID=$$
# shellcheck disable=SC2086
run_case "session-id-report: a recorded id logs nothing" session-id-report.sh '{"session_id":"abc-123"}' "-" $LANE
run_case "session-id-report: outside a lane logs nothing" session-id-report.sh "$SECRET" "-"

# --- sindri-tasks.sh (UserPromptSubmit): logs nothing, so its baseline is itself ---
WANT_RC=0 run_case "sindri-tasks: unparsable stdin" sindri-tasks.sh "$SECRET {" "-"
WANT_RC=0 run_case "sindri-tasks: empty stdin" sindri-tasks.sh "" "-"
WANT_RC=0 run_case "sindri-tasks: no session id" sindri-tasks.sh '{"prompt":"hi"}' "-"

# --- Bash safety hooks (PreToolUse Bash) ---
NO_CAT="$(path_without cat)"
for h in block-destructive block-push-main detect-secrets; do
  run_case "$h: unparsable stdin" "$h.sh" "$SECRET rm -rf /" "$h input-unparsable"
  run_case "$h: empty stdin" "$h.sh" "" "$h empty-input"
  run_case "$h: Bash call without a command string" "$h.sh" '{"tool_name":"Bash","tool_input":{"cmd":"x"}}' "$h field-missing"
  run_case "$h: normal command logs nothing" "$h.sh" '{"tool_name":"Bash","tool_input":{"command":"ls"}}' "-"
  # These three run under set -e. On bash 3.2 an EXIT trap there turns a set -u
  # abort into exit 0, so they have no crash trap: a crash keeps its exit code
  # and is not counted (documented in .agents/rules/hooks.md).
  CASE_PATH="$NO_CAT" WANT_RC=127 run_case "$h: crash keeps its exit code (not counted)" "$h.sh" '{"tool_input":{"command":"ls"}}' "-"
  INJECT=': "$AW_NOPE_UNSET"' WANT_RC=1 run_case "$h: set -u abort keeps exit 1" "$h.sh" '{"tool_input":{"command":"ls"}}' "-"
done
# No fail-open hook has an EXIT trap: on bash 3.2 a trap turns a set -u abort
# under set -e into exit 0, and it defers SIGTERM until a running $(judge …)
# returns. A crash keeps its exit code and is not counted under Claude Code.
INJECT='exit 3' WANT_RC=3 run_case "send-gate: crash keeps its exit code (not counted)" send-gate.sh "$SEND" "-"
INJECT=': "${AW_NOPE_UNSET:?}"' WANT_RC=1 run_case "scope-gate: unbound abort keeps exit 1 (not counted)" scope-gate.sh "$AGENT" "-"

# SIGTERM while the hook waits on a slow judge: the current hook must die as
# fast as the baseline (no trap deferring the signal), within a bounded wait.
SLOW_BIN="$(mktemp -d)"
printf '#!/usr/bin/env bash
cat > /dev/null
sleep 4
echo "{\\"decision\\":\\"send\\"}"
' > "$SLOW_BIN/judge"; chmod +x "$SLOW_BIN/judge"
# term_exit_ms <hook path> <stdin> — ms from start until the hook exits after a TERM at 0.5 s.
term_exit_ms() {
  local in t0 t1 p i=0; in="$(mktemp)"; printf '%s' "$2" > "$in"
  t0="$(perl -MTime::HiRes=time -e 'printf "%d", time*1000')"
  env PATH="$SLOW_BIN:$BASE_PATH" AW_STATE_DIR="$(mktemp -d)" bash "$1" < "$in" > /dev/null 2>&1 &
  p=$!
  sleep 0.5; kill -TERM "$p" 2>/dev/null
  while kill -0 "$p" 2>/dev/null; do
    [ "$i" -ge 200 ] && { kill -9 "$p" 2>/dev/null; break; }
    sleep 0.05; i=$((i + 1))
  done
  wait "$p" 2>/dev/null
  t1="$(perl -MTime::HiRes=time -e 'printf "%d", time*1000')"
  echo $((t1 - t0))
}
for pair in "send-gate.sh:$SEND" "scope-gate.sh:$AGENT" \
            "done-gate.sh:{\"session_id\":\"s1\",\"last_assistant_message\":\"Still working on the parser.\"}"; do
  h="${pair%%:*}"; payload="${pair#*:}"
  cur="$(stage_current 1)"; base="$(stage_baseline 1)"
  ms_cur="$(term_exit_ms "$cur/$h" "$payload")"; ms_base="$(term_exit_ms "$base/$h" "$payload")"
  if [ "$ms_cur" -le $((ms_base + 1000)) ] && [ "$ms_cur" -lt 3000 ]; then
    echo "PASS: $h: SIGTERM during a slow judge exits as fast as the baseline (${ms_cur} ms vs ${ms_base} ms)"
  else
    echo "FAIL: $h: SIGTERM during a slow judge — current ${ms_cur} ms, baseline ${ms_base} ms"; fail=1
  fi
done
run_case "block-destructive: deny logs nothing" block-destructive.sh '{"tool_input":{"command":"rm -rf /tmp/x"}}' "-"

# --- The logger can never change a decision, even when it can't run ---
# Every fail-open hook and both adapters, on an unreadable payload and on a
# payload that reaches its judge/state failure path, with HOME unset, with
# date missing, and with logs/failopen.log replaced by a FIFO.
NO_DATE="$(path_without date judge)"
OUTBOX2="$(mktemp -d)"; printf 'not json\n' > "$OUTBOX2/s1.jsonl"
CLAIM2="$(mktemp)"; echo '{"type":"assistant","message":{"content":[{"type":"text","text":"All done."}]}}' > "$CLAIM2"
hostile() {
  local label="$1" hook="$2" input="$3"; shift 3
  NO_HOME=1 run_case "$label, HOME unset" "$hook" "$input" "?" "$@"
  CASE_PATH="$FAKE_BIN:$NO_DATE" run_case "$label, date missing" "$hook" "$input" "?" "$@"
  FIFO_LOG=1 run_case "$label, log is a FIFO" "$hook" "$input" "?" "$@"
}
for h in send-gate scope-gate external-write-guard done-gate outbox-flush block-destructive block-push-main detect-secrets; do
  hostile "$h: unreadable payload" "$h.sh" "$SECRET"
done
hostile "send-gate: judge fails" send-gate.sh "$SEND" JUDGE_MODE=exit1
hostile "scope-gate: judge fails" scope-gate.sh "$AGENT" JUDGE_MODE=exit1
hostile "external-write-guard: matched write, no session" external-write-guard.sh '{"tool_name":"Bash","tool_input":{"command":"gh pr merge 1"}}'
hostile "done-gate: brief judge fails, then RF-2 denies" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$CLAIM2\"}" JUDGE_MODE=exit1
hostile "meta-stop-gate: unreadable payload" meta-stop-gate.sh "$SECRET" AW_META_BIN="$FAKE_BIN/aw-meta"
hostile "meta-stop-gate: aw-meta errors" meta-stop-gate.sh "$META_STOP" AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
hostile "meta-outbox: aw-meta errors" meta-outbox.sh "$META_SEND" AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
# shellcheck disable=SC2086
hostile "meta-board-line: aw-meta errors" meta-board-line.sh "$META_LANE_PROMPT" $LANE AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
# shellcheck disable=SC2086
hostile "meta-lane-gate: aw-meta errors at stop" meta-lane-gate.sh "$META_LANE_STOP" $LANE AW_META_BIN="$FAKE_BIN/aw-meta" META_MODE=2
# shellcheck disable=SC2086
hostile "meta-lane-gate: unreadable payload" meta-lane-gate.sh "$SECRET" $LANE AW_META_BIN="$FAKE_BIN/aw-meta"
# shellcheck disable=SC2086
hostile "session-id-report: unreadable payload" session-id-report.sh "$SECRET" $LANE
# shellcheck disable=SC2086
hostile "session-id-report: a reported id" session-id-report.sh '{"session_id":"abc-123"}' $LANE
hostile "outbox-flush: outbox unreadable" outbox-flush.sh '{"session_id":"s1","teammate_name":"t"}' AW_OUTBOX_DIR="$OUTBOX2"
for h in block-destructive block-push-main detect-secrets; do
  hostile "$h: Bash call without a command" "$h.sh" '{"tool_name":"Bash","tool_input":{"cmd":"x"}}'
done
for p in codex cursor; do
  HOOK_ARGS="block-destructive.sh" hostile "$p adapter: garbage host payload" "adapters/$p.sh" "$SECRET {"
  HOOK_ARGS="/nonexistent/hook.sh" hostile "$p adapter: hook script missing" "adapters/$p.sh" '{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"ls"}}'
done

# --- Provider adapters ---
EXIT3="$(mktemp -d)/exit3.sh"; printf '#!/usr/bin/env bash\nexit 3\n' > "$EXIT3"
PRE='{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"ls"}}'
for p in codex cursor; do
  HOOK_ARGS="/nonexistent/hook.sh" run_case "$p adapter: hook script missing" "adapters/$p.sh" "$PRE" "$p-adapter hook-script-missing"
  HOOK_ARGS="$EXIT3" run_case "$p adapter: hook exits 3" "adapters/$p.sh" "$PRE" "$p-adapter hook-exit-3"
  run_case "$p adapter: no hook script argument" "adapters/$p.sh" "$PRE" "$p-adapter args-invalid"
  HOOK_ARGS="block-destructive.sh" run_case "$p adapter: unparsable host payload" "adapters/$p.sh" "$SECRET {" "$p-adapter input-unparsable"
  HOOK_ARGS="block-destructive.sh" run_case "$p adapter: empty host payload" "adapters/$p.sh" "" "$p-adapter empty-input"
  HOOK_ARGS="block-destructive.sh" run_case "$p adapter: allowing hook logs nothing" "adapters/$p.sh" "$PRE" "-"
done
# One bad host payload is one event: the hook behind the adapter doesn't add field-missing.
HOOK_ARGS="--event beforeShellExecution block-destructive.sh" run_case "cursor adapter: garbage shell payload counts once" adapters/cursor.sh "$SECRET" "cursor-adapter input-unparsable"
HOOK_ARGS="--event beforeShellExecution block-destructive.sh" run_case "cursor adapter: empty shell payload counts once" adapters/cursor.sh "" "cursor-adapter empty-input"
HOOK_ARGS="block-destructive.sh" CASE_PATH="$JQ_FAIL_BIN:$FAKE_BIN:$BASE_PATH" run_case "codex adapter: payload normalization fails" \
  adapters/codex.sh "$PRE" "codex-adapter payload-unparsable" FAIL_JQ_ON='.prompt_id = (.prompt_id'
HOOK_ARGS="block-destructive.sh" CASE_PATH="$JQ_FAIL_BIN:$FAKE_BIN:$BASE_PATH" run_case "cursor adapter: payload normalization fails" \
  adapters/cursor.sh '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"ls"}}' "cursor-adapter payload-unparsable" FAIL_JQ_ON='def jsonish'
HOOK_ARGS="block-destructive.sh" CASE_PATH="$JQ_FAIL_BIN:$FAKE_BIN:$BASE_PATH" run_case "cursor adapter: result rendering fails" \
  adapters/cursor.sh '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"ls"}}' "cursor-adapter result-unparsable" FAIL_JQ_ON='--argjson rc'

exit $fail
