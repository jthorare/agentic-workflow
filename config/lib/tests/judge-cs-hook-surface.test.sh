#!/usr/bin/env bash
# config/lib/tests/judge-cs-hook-surface.test.sh — the frozen hook surface (spec §4a step 3, §4c).
# The contract is only what the hooks read (spec §4c); the rest of the C# CLI is its own design.
# Runs the real hook scripts in config/hooks/ with the C# judge (src/Judge) first on PATH, offline
# (AW_JUDGE_PROVIDERS=rules: pre-rules and rule fallbacks only, no model call), and checks each
# hook's stdout, exit code and the files it leaves in a scratch AW_STATE_DIR.
# When the TS judge is built (judge/dist/cli.js), every case also runs against it and the two runs
# must agree on exit codes, stderr and parsed JSON (keys sorted, ids and times masked), so a change on either side that moves the surface fails.
#
# A case that expects a pass also expects no fail-open log line: send-gate and scope-gate allow, and done-gate
# falls back to its evidence check, on any judge failure, so a pass alone can't tell a working judge from a broken one.
# Three stub `claude` CLIs (answering 'continue', answering 'batch', and one that exits 1) reach the branches the
# offline rules can't: ask-check continue, send-gate's batch queue and flush, and a degraded health.
#
# AW_JUDGE_CS_BIN  a built C# judge (the NativeAOT publish or the JIT apphost). Unset: the test builds
#                  src/Judge/Aw.Judge.Host in Release into a temp dir first (needs dotnet 10).
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HOOKS="$ROOT/config/hooks"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

if [ -z "${AW_JUDGE_CS_BIN:-}" ]; then
  dotnet build "$ROOT/src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj" -c Release -o "$WORK/cs-build" -warnaserror -nologo -v quiet > "$WORK/build.log" 2>&1 \
    || { cat "$WORK/build.log"; echo "FAIL: could not build the C# judge"; exit 1; }
  AW_JUDGE_CS_BIN="$WORK/cs-build/judge"
fi
[ -x "$AW_JUDGE_CS_BIN" ] || { echo "FAIL: no C# judge at $AW_JUDGE_CS_BIN"; exit 1; }
# Absolute, so the PATH shim works from whatever directory a hook runs `judge` in.
AW_JUDGE_CS_BIN="$(cd "$(dirname "$AW_JUDGE_CS_BIN")" && pwd)/$(basename "$AW_JUDGE_CS_BIN")"

SIDES="cs"
TS_CLI="$ROOT/judge/dist/cli.js"
if [ -f "$TS_CLI" ] && command -v node > /dev/null 2>&1; then SIDES="cs ts"; else echo "NOTE: judge/dist not built; checking the C# judge against the recorded expectations only"; fi

# One PATH shim per side, so the hooks call `judge` exactly as installed hooks do.
mkdir -p "$WORK/shim-cs" "$WORK/shim-ts"
printf '#!/usr/bin/env bash\nexec "%s" "$@"\n' "$AW_JUDGE_CS_BIN" > "$WORK/shim-cs/judge"
printf '#!/usr/bin/env bash\nexec node "%s" "$@"\n' "$TS_CLI" > "$WORK/shim-ts/judge"
chmod +x "$WORK/shim-cs/judge" "$WORK/shim-ts/judge"

# Stub agent CLIs, each alone in a directory put on PATH (XPATH) for the cases that use it, with
# AW_JUDGE_PROVIDERS=rules,claude-cli: one answers 'continue' and one 'batch' (claude -p's JSON envelope), one fails.
mkdir -p "$WORK/claude-continue" "$WORK/claude-batch" "$WORK/claude-broken"
printf '#!/usr/bin/env bash\nprintf %%s %s\n' "'{\"type\":\"result\",\"is_error\":false,\"result\":\"\",\"structured_output\":{\"decision\":\"continue\"}}'" > "$WORK/claude-continue/claude"
printf '#!/usr/bin/env bash\nprintf %%s %s\n' "'{\"type\":\"result\",\"is_error\":false,\"result\":\"\",\"structured_output\":{\"decision\":\"batch\"}}'" > "$WORK/claude-batch/claude"
printf '#!/usr/bin/env bash\necho "stub claude: broken" >&2\nexit 1\n' > "$WORK/claude-broken/claude"
chmod +x "$WORK/claude-continue/claude" "$WORK/claude-batch/claude" "$WORK/claude-broken/claude"
XPATH=""

export AW_JUDGE_PROVIDERS=rules AW_LOG_LEVEL=none
unset AW_OUTBOX_DIR AW_TEAMMATE_NAMES_FILE AW_JUDGE_SESSIONS_DIR AW_JUDGE_CHILD 2> /dev/null || true

# fresh <side> <case>: a new scratch state dir for this side and case
fresh() { local d="$WORK/state-$1-$2"; rm -rf "$d"; mkdir -p "$d/judge"; echo "$d"; }

# hook <side> <state> <script> <stdin> -> writes $WORK/<case>.<side>.{out,err,rc}
hook() {
  local side="$1" state="$2" script="$3" input="$4" name="$5" rc=0
  PATH="$WORK/shim-$side:${XPATH:+$XPATH:}$PATH" AW_STATE_DIR="$state" bash "$HOOKS/$script" <<< "$input" \
    > "$WORK/$name.$side.out" 2> "$WORK/$name.$side.err" || rc=$?
  echo "$rc" > "$WORK/$name.$side.rc"
}

# judge <side> <state> args... — the same side's judge, for setup and for reading state back
judge_on() { local side="$1" state="$2"; shift 2; PATH="$WORK/shim-$side:${XPATH:+$XPATH:}$PATH" AW_STATE_DIR="$state" judge "$@"; }

# The contract is field names, values and exit codes, not formatting: JSON lines are compared after jq -S
# (sorted keys, canonical numbers and escapes), with ids and timestamps masked.
norm() {
  [ -f "$1" ] || return 0
  while IFS= read -r line || [ -n "$line" ]; do
    printf '%s' "$line" | jq -cS '.' 2> /dev/null || printf '%s\n' "$line"
  done < "$1" | sed -E 's/[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:.]+Z/<TS>/g; s/"id":"[^"]+"/"id":"<ID>"/g'
}

# state_dump <state>: every file the hooks wrote (not the judge's own sqlite), normalized
state_dump() {
  local state="$1" f
  (cd "$state" && find . -type f ! -name 'decisions.sqlite*' ! -name 'config.json' | LC_ALL=C sort) | while read -r f; do
    echo "== $f"; norm "$state/$f"
  done
}

FAILS=0
fail() { echo "FAIL: $*"; FAILS=$((FAILS + 1)); }

# same <case>: the cs and ts runs agree (stdout, stderr, exit code, state files)
same() {
  local name="$1"
  [ "$SIDES" = "cs ts" ] || return 0
  diff <(norm "$WORK/$name.cs.out") <(norm "$WORK/$name.ts.out") > /dev/null || fail "$name: stdout differs from the TS judge: $(diff <(norm "$WORK/$name.cs.out") <(norm "$WORK/$name.ts.out") | head -5)"
  diff <(norm "$WORK/$name.cs.err") <(norm "$WORK/$name.ts.err") > /dev/null || fail "$name: stderr differs from the TS judge"
  diff "$WORK/$name.cs.rc" "$WORK/$name.ts.rc" > /dev/null || fail "$name: exit $(cat "$WORK/$name.cs.rc") vs TS $(cat "$WORK/$name.ts.rc")"
  diff <(state_dump "$WORK/state-cs-$name") <(state_dump "$WORK/state-ts-$name") > /dev/null || fail "$name: hook-written files differ from the TS judge's run"
}

rc_of() { cat "$WORK/$1.cs.rc"; }
out_of() { cat "$WORK/$1.cs.out"; }
err_of() { cat "$WORK/$1.cs.err"; }
failopen_log() { cat "$WORK/state-cs-$1/logs/failopen.log" 2> /dev/null || true; }
# no_failopen <case>...: the hook passed because the judge answered, not because it failed open
no_failopen() { local c; for c in "$@"; do [ -z "$(failopen_log "$c")" ] || fail "$c: the hook failed open: $(failopen_log "$c")"; done; }

# ---- send-gate (wake-gate) ----
for side in $SIDES; do
  s="$(fresh "$side" send-urgent)"; hook "$side" "$s" send-gate.sh '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"blocked: need your review"}}' send-urgent
  s="$(fresh "$side" send-ack)"; hook "$side" "$s" send-gate.sh '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"ok"}}' send-ack
  s="$(fresh "$side" send-undecided)"; hook "$side" "$s" send-gate.sh '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"still working through the second task now"}}' send-undecided
  # batch (a stub claude decides it): queued in judge/outbox/s1.jsonl; the next urgent send in the same
  # state dir flushes the queue and attaches it.
  s="$(fresh "$side" send-batch)"
  XPATH="$WORK/claude-batch" AW_JUDGE_PROVIDERS=rules,claude-cli hook "$side" "$s" send-gate.sh '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"still working through the second task now"}}' send-batch
  cp "$s/judge/outbox/s1.jsonl" "$WORK/send-batch.$side.outbox" 2> /dev/null || : > "$WORK/send-batch.$side.outbox"
  hook "$side" "$s" send-gate.sh '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"blocked: need your review"}}' send-flush
done
out_of send-urgent | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || fail "send-urgent: expected allow, got $(out_of send-urgent)"
out_of send-ack | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || fail "send-ack: expected deny (pure ack dropped), got $(out_of send-ack)"
# rules-only chain: no pre-rule settles it and no model is allowed, so wake-gate escalates (exit 2) and
# send-gate takes its fail-open path: allow, logging judge-exit-2 (not some other exit: the judge ran).
out_of send-undecided | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || fail "send-undecided: expected allow, got $(out_of send-undecided)"
failopen_log send-undecided | has 'judge-exit-2' || fail "send-undecided: expected the escalation's judge-exit-2 fail-open, got '$(failopen_log send-undecided)'"
out_of send-batch | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || fail "send-batch: expected deny (queued), got $(out_of send-batch)"
[ "$(wc -l < "$WORK/send-batch.cs.outbox" | tr -d ' ')" = 1 ] || fail "send-batch: expected one outbox line, got '$(cat "$WORK/send-batch.cs.outbox")'"
jq -e '.text == "still working through the second task now" and .agentType == "builder-a"' "$WORK/send-batch.cs.outbox" > /dev/null || fail "send-batch: outbox line $(cat "$WORK/send-batch.cs.outbox")"
out_of send-flush | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || fail "send-flush: expected allow, got $(out_of send-flush)"
out_of send-flush | has 'queued progress, now attached' || fail "send-flush: expected the queued message attached, got $(out_of send-flush)"
[ ! -s "$WORK/state-cs-send-batch/judge/outbox/s1.jsonl" ] || fail "send-flush: the outbox wasn't emptied"
no_failopen send-urgent send-ack send-batch
for c in send-urgent send-ack send-undecided send-batch send-flush; do [ "$(rc_of $c)" = 0 ] || fail "$c: exit $(rc_of $c)"; done
for c in send-urgent send-ack send-undecided send-batch; do same $c; done
if [ "$SIDES" = "cs ts" ]; then
  diff <(norm "$WORK/send-batch.cs.outbox") <(norm "$WORK/send-batch.ts.outbox") > /dev/null || fail "send-batch: the queued outbox line differs from the TS judge's run"
  diff <(norm "$WORK/send-flush.cs.out") <(norm "$WORK/send-flush.ts.out") > /dev/null || fail "send-flush: stdout differs from the TS judge"
fi

# ---- scope-gate (brief-scope + brief save) ----
for side in $SIDES; do
  s="$(fresh "$side" scope-exempt)"
  hook "$side" "$s" scope-gate.sh '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu-x","tool_input":{"subagent_type":"Explore","name":"scout","description":"find the config loader","prompt":"Acceptance: list the files. Proof: rg config"}}' scope-exempt
  judge_on "$side" "$s" brief get tu-x > "$WORK/scope-exempt.$side.brief" 2>&1 || true
  s="$(fresh "$side" scope-empty)"
  hook "$side" "$s" scope-gate.sh '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu-y","tool_input":{"subagent_type":"general-purpose","description":"","prompt":""}}' scope-empty
done
out_of scope-exempt | jq -e '(.hookSpecificOutput.permissionDecision // "allow") == "allow"' > /dev/null 2>&1 || [ -z "$(out_of scope-exempt)" ] || fail "scope-exempt: expected allow, got $(out_of scope-exempt)"
out_of scope-empty | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || fail "scope-empty: expected deny, got $(out_of scope-empty)"
# scope-gate saved the brief through `judge brief save`; read back what the hooks read later.
jq -e '.toolUseId == "tu-x" and .dispatchName == "scout" and .sessionId == "s1"' "$WORK/scope-exempt.cs.brief" > /dev/null 2>&1 \
  || fail "scope-exempt: the saved brief isn't tu-x for scout: $(cat "$WORK/scope-exempt.cs.brief")"
no_failopen scope-exempt scope-empty
if [ "$SIDES" = "cs ts" ]; then
  # Only what hooks read back from a brief: its id and acceptance criteria (done-gate, done-gate-annotate).
  diff <(jq -S '{toolUseId, acceptanceCriteria}' "$WORK/scope-exempt.cs.brief" 2> /dev/null) <(jq -S '{toolUseId, acceptanceCriteria}' "$WORK/scope-exempt.ts.brief" 2> /dev/null) > /dev/null \
    || fail "scope-exempt: the saved brief differs: cs=$(cat "$WORK/scope-exempt.cs.brief") ts=$(cat "$WORK/scope-exempt.ts.brief")"
fi
for c in scope-exempt scope-empty; do same $c; done

# ---- subagent-start-map (map-by-name / map-by-dispatch + set-agent-id) ----
for side in $SIDES; do
  s="$(fresh "$side" map-name)"
  printf '%s' '{"toolUseId":"tu-a","sessionId":"s1","promptId":"p1","dispatchName":"builder-a","subagentType":"general-purpose","goal":"g","acceptanceCriteria":"a","proofCommand":"p"}' | judge_on "$side" "$s" brief save
  echo '{"name":"builder-a"}' > "$s/judge/teammate-names.jsonl"
  hook "$side" "$s" subagent-start-map.sh '{"agent_id":"agent-1","agent_type":"builder-a","session_id":"s1","prompt_id":"p1"}' map-name
  judge_on "$side" "$s" brief get tu-a | jq -r '.agentId' > "$WORK/map-name.$side.agent"
  s="$(fresh "$side" map-dispatch)"
  printf '%s' '{"toolUseId":"tu-b","sessionId":"s2","promptId":"p9","dispatchName":null,"subagentType":"lean-coder","goal":"g","acceptanceCriteria":"a","proofCommand":"p"}' | judge_on "$side" "$s" brief save
  hook "$side" "$s" subagent-start-map.sh '{"agent_id":"agent-2","agent_type":"lean-coder","session_id":"s2","prompt_id":"p9"}' map-dispatch
  judge_on "$side" "$s" brief get tu-b | jq -r '.agentId' > "$WORK/map-dispatch.$side.agent"
done
[ "$(cat "$WORK/map-name.cs.agent")" = agent-1 ] || fail "map-name: agentId $(cat "$WORK/map-name.cs.agent"), expected agent-1"
[ "$(cat "$WORK/map-dispatch.cs.agent")" = agent-2 ] || fail "map-dispatch: agentId $(cat "$WORK/map-dispatch.cs.agent"), expected agent-2"
no_failopen map-name map-dispatch
for c in map-name map-dispatch; do [ "$(rc_of $c)" = 0 ] || fail "$c: exit $(rc_of $c)"; same $c; done

# ---- done-gate-annotate (brief get) ----
for side in $SIDES; do
  s="$(fresh "$side" annotate)"
  printf '%s' '{"toolUseId":"tu-c","sessionId":"s1","promptId":"p1","dispatchName":null,"subagentType":"lean-coder","goal":"g","acceptanceCriteria":"all tests pass","proofCommand":"npm test"}' | judge_on "$side" "$s" brief save
  hook "$side" "$s" done-gate-annotate.sh '{"tool_use_id":"tu-c","tool_response":{"result":"Finished: all tests pass (npm test)."}}' annotate
done
out_of annotate | jq -e '.hookSpecificOutput.additionalContext | startswith("Brief acceptance criteria plausibly met")' > /dev/null || fail "annotate: got $(out_of annotate)"
no_failopen annotate
same annotate

# ---- done-gate (ask-check, brief get task:<sid>) ----
transcript() { jq -nc --arg t "$2" '{type:"assistant", message:{content:[{type:"text", text:$t}]}}' > "$1"; }
transcript "$WORK/t-merge.jsonl" "Next I will merge the release branch."
transcript "$WORK/t-next.jsonl" "I'll look at the logs next."
# A claim that meets the brief's criteria in words the no-brief evidence check (RF-2) wouldn't accept, so it
# passes only if done-gate read the brief; one that misses them; and one with RF-2 evidence for the no-brief case.
transcript "$WORK/t-meets.jsonl" "Done — all tests pass."
transcript "$WORK/t-misses.jsonl" "Done — refactored the parser."
transcript "$WORK/t-evidence.jsonl" "Done — all tests pass, ran npm test."
stop() { jq -nc --arg t "$1" '{transcript_path:$t, session_id:"s1"}'; }
task_brief() { printf '%s' '{"toolUseId":"task:s1","sessionId":"s1","promptId":"p1","dispatchName":null,"subagentType":"main","goal":"g","acceptanceCriteria":"all tests pass","proofCommand":"npm test"}' | judge_on "$1" "$2" brief save; }
for side in $SIDES; do
  s="$(fresh "$side" done-ask)"; hook "$side" "$s" done-gate.sh "$(stop "$WORK/t-merge.jsonl")" done-ask
  s="$(fresh "$side" done-escalated)"; hook "$side" "$s" done-gate.sh "$(stop "$WORK/t-next.jsonl")" done-escalated
  s="$(fresh "$side" done-continue)"
  XPATH="$WORK/claude-continue" AW_JUDGE_PROVIDERS=rules,claude-cli hook "$side" "$s" done-gate.sh "$(stop "$WORK/t-next.jsonl")" done-continue
  s="$(fresh "$side" done-brief)"; task_brief "$side" "$s"; hook "$side" "$s" done-gate.sh "$(stop "$WORK/t-meets.jsonl")" done-brief
  s="$(fresh "$side" done-brief-miss)"; task_brief "$side" "$s"; hook "$side" "$s" done-gate.sh "$(stop "$WORK/t-misses.jsonl")" done-brief-miss
  s="$(fresh "$side" done-nobrief)"; hook "$side" "$s" done-gate.sh "$(stop "$WORK/t-evidence.jsonl")" done-nobrief
done
[ "$(rc_of done-ask)" = 0 ] || fail "done-ask: a merge next step must stop to ask (exit 0), got $(rc_of done-ask)"
[ ! -e "$WORK/state-cs-done-ask/judge/sessions/s1.json" ] || fail "done-ask: must not record auto_continued_at"
[ "$(rc_of done-escalated)" = 0 ] || fail "done-escalated: an escalated ask-check stops to ask (exit 0), got $(rc_of done-escalated)"
[ ! -e "$WORK/state-cs-done-escalated/judge/sessions/s1.json" ] || fail "done-escalated: must not record auto_continued_at"
[ "$(rc_of done-continue)" = 2 ] || fail "done-continue: ask-check 'continue' keeps going (exit 2), got $(rc_of done-continue)"
jq -e '.auto_continued_at | test("^[0-9]{4}-[0-9]{2}-[0-9]{2}T")' "$WORK/state-cs-done-continue/judge/sessions/s1.json" > /dev/null 2>&1 \
  || fail "done-continue: expected judge/sessions/s1.json with auto_continued_at"
[ "$(rc_of done-brief)" = 0 ] || fail "done-brief: a claim meeting the brief must pass, got $(rc_of done-brief)"
[ "$(rc_of done-brief-miss)" = 0 ] || fail "done-brief-miss: a briefed claim need not repeat the criteria (q93 JG-05), exit 0, got $(rc_of done-brief-miss)"
[ "$(rc_of done-nobrief)" = 0 ] || fail "done-nobrief: a claim with evidence and no brief must pass, got $(rc_of done-nobrief)"
no_failopen done-ask done-escalated done-continue done-brief done-brief-miss done-nobrief
for c in done-ask done-escalated done-continue done-brief done-brief-miss done-nobrief; do same $c; done

# ---- judge-health (SessionStart) and the statusline's judge_health ----
# statusline.sh's own functions, run against the shim: `<ok|degraded|down> <failures24h>`.
sed -n '/^find_bin() {/,/^}/p; /^judge_health() {/,/^}/p' "$ROOT/config/statusline.sh" > "$WORK/statusline-health.sh"
grep -q '^judge_health() {' "$WORK/statusline-health.sh" || fail "statusline: judge_health() not found in config/statusline.sh"
statusline_health() { (export PATH="$WORK/shim-$1:$PATH" AW_STATE_DIR="$2"; . "$WORK/statusline-health.sh"; judge_health); }
for side in $SIDES; do
  s="$(fresh "$side" health)"; hook "$side" "$s" judge-health.sh '' health
  statusline_health "$side" "$s" > "$WORK/health.$side.statusline"
  # One failure in the last 24 h (a provider that exits 1) makes it degraded.
  s="$(fresh "$side" health-degraded)"
  printf '%s' '{"text":"still working through the second task now","senderKind":"teammate"}' \
    | XPATH="$WORK/claude-broken" AW_JUDGE_PROVIDERS=claude-cli,rules judge_on "$side" "$s" wake-gate > /dev/null 2>&1 || true
  hook "$side" "$s" judge-health.sh '' health-degraded
  statusline_health "$side" "$s" > "$WORK/health-degraded.$side.statusline"
  # A store the judge can't open: the health check itself fails.
  s="$(fresh "$side" health-down)"; mkdir -p "$s/judge/decisions.sqlite"
  hook "$side" "$s" judge-health.sh '' health-down
  statusline_health "$side" "$s" > "$WORK/health-down.$side.statusline"
done
[ -z "$(out_of health)" ] || fail "health: a fresh judge is ok and the hook prints nothing, got $(out_of health)"
[ "$(cat "$WORK/health.cs.statusline")" = "ok 0" ] || fail "health: the statusline reads 'ok 0', got '$(cat "$WORK/health.cs.statusline")'"
out_of health-degraded | has '^judge: degraded — 1 failures in the last 24h' || fail "health-degraded: expected the degraded warning, got '$(out_of health-degraded)'"
[ "$(cat "$WORK/health-degraded.cs.statusline")" = "degraded 1" ] || fail "health-degraded: the statusline reads 'degraded 1', got '$(cat "$WORK/health-degraded.cs.statusline")'"
[ "$(out_of health-down)" = "judge: health check failed" ] || fail "health-down: expected 'judge: health check failed', got '$(out_of health-down)'"
[ "$(cat "$WORK/health-down.cs.statusline")" = "down 0" ] || fail "health-down: the statusline reads 'down 0', got '$(cat "$WORK/health-down.cs.statusline")'"
for c in health health-degraded health-down; do
  [ "$(rc_of $c)" = 0 ] || fail "$c: exit $(rc_of $c)"
  same $c
  if [ "$SIDES" = "cs ts" ]; then
    diff "$WORK/$c.cs.statusline" "$WORK/$c.ts.statusline" > /dev/null || fail "$c: the statusline differs from the TS judge's: $(cat "$WORK/$c.cs.statusline") vs $(cat "$WORK/$c.ts.statusline")"
  fi
done

if [ "$FAILS" -gt 0 ]; then echo "judge-cs-hook-surface: $FAILS failure(s) (sides: $SIDES)"; exit 1; fi
echo "All judge-cs-hook-surface tests passed (sides: $SIDES)."
