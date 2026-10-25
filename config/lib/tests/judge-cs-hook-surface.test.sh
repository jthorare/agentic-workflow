#!/usr/bin/env bash
# config/lib/tests/judge-cs-hook-surface.test.sh — the frozen hook surface (spec §4a step 3, §4c).
# The contract is only what the hooks read (spec §4c); the rest of the C# CLI is its own design.
# Runs the real hook scripts in config/hooks/ with the C# judge (src/Judge) first on PATH, offline
# (AW_JUDGE_PROVIDERS=rules: pre-rules and rule fallbacks only, no model call), and checks each
# hook's stdout, exit code and the files it leaves in a scratch AW_STATE_DIR.
#
# A case that expects a pass also expects no fail-open log line: send-gate and scope-gate allow, and done-gate
# falls back to its evidence check, on any judge failure, so a pass alone can't tell a working judge from a broken one.
# prompt-sort, done-gate on the sorter's session file, trait-check and the not-yet-built stub verbs (cutover spec §10, §5.2) are
# checked against the C# judge only (offline, no stub Jev: the decisive-axis and findings paths are the C# unit tests').
# Three stub `claude` CLIs (answering 'continue', answering 'batch', and one that exits 1) reach the branches the
# offline rules can't: ask-check continue, send-gate's batch queue and flush, and a degraded health.
#
# AW_JUDGE_CS_BIN  a built C# judge (the NativeAOT publish or the JIT apphost); first choice (CI). Unset: the installed
#                  `judge` on PATH; none: the test builds src/Judge/Aw.Judge.Host in Release into a temp dir (needs dotnet 10).
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HOOKS="$ROOT/config/hooks"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

if [ -z "${AW_JUDGE_CS_BIN:-}" ] && command -v judge > /dev/null 2>&1; then
  AW_JUDGE_CS_BIN="$(command -v judge)"
fi
if [ -z "${AW_JUDGE_CS_BIN:-}" ]; then
  dotnet build "$ROOT/src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj" -c Release -o "$WORK/cs-build" -warnaserror -nologo -v quiet > "$WORK/build.log" 2>&1 \
    || { cat "$WORK/build.log"; echo "FAIL: could not build the C# judge"; exit 1; }
  AW_JUDGE_CS_BIN="$WORK/cs-build/judge"
fi
[ -x "$AW_JUDGE_CS_BIN" ] || { echo "FAIL: no C# judge at $AW_JUDGE_CS_BIN"; exit 1; }
# Absolute, so the PATH shim works from whatever directory a hook runs `judge` in.
AW_JUDGE_CS_BIN="$(cd "$(dirname "$AW_JUDGE_CS_BIN")" && pwd)/$(basename "$AW_JUDGE_CS_BIN")"

# One PATH shim, so the hooks call `judge` exactly as installed hooks do.
mkdir -p "$WORK/shim-cs"
printf '#!/usr/bin/env bash\nexec "%s" "$@"\n' "$AW_JUDGE_CS_BIN" > "$WORK/shim-cs/judge"
chmod +x "$WORK/shim-cs/judge"

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

FAILS=0
fail() { echo "FAIL: $*"; FAILS=$((FAILS + 1)); }

rc_of() { cat "$WORK/$1.cs.rc"; }
out_of() { cat "$WORK/$1.cs.out"; }
err_of() { cat "$WORK/$1.cs.err"; }
failopen_log() { cat "$WORK/state-cs-$1/logs/failopen.log" 2> /dev/null || true; }
# no_failopen <case>...: the hook passed because the judge answered, not because it failed open
no_failopen() { local c; for c in "$@"; do [ -z "$(failopen_log "$c")" ] || fail "$c: the hook failed open: $(failopen_log "$c")"; done; }

# ---- send-gate (wake-gate) ----
for side in cs; do
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

# ---- scope-gate (brief-scope + brief save) ----
for side in cs; do
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

# ---- subagent-start-map (map-by-name / map-by-dispatch + set-agent-id) ----
for side in cs; do
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
for c in map-name map-dispatch; do [ "$(rc_of $c)" = 0 ] || fail "$c: exit $(rc_of $c)"; done

# ---- done-gate-annotate (brief get) ----
for side in cs; do
  s="$(fresh "$side" annotate)"
  printf '%s' '{"toolUseId":"tu-c","sessionId":"s1","promptId":"p1","dispatchName":null,"subagentType":"lean-coder","goal":"g","acceptanceCriteria":"all tests pass","proofCommand":"npm test"}' | judge_on "$side" "$s" brief save
  hook "$side" "$s" done-gate-annotate.sh '{"tool_use_id":"tu-c","tool_response":{"result":"Finished: all tests pass (npm test)."}}' annotate
done
out_of annotate | jq -e '.hookSpecificOutput.additionalContext | startswith("Brief acceptance criteria plausibly met")' > /dev/null || fail "annotate: got $(out_of annotate)"
no_failopen annotate

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
for side in cs; do
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

# ---- judge-health (SessionStart) and the statusline's judge_health ----
# statusline.sh's own functions, run against the shim: `<ok|degraded|down> <failures24h>`.
sed -n '/^find_bin() {/,/^}/p; /^judge_health() {/,/^}/p' "$ROOT/config/statusline.sh" > "$WORK/statusline-health.sh"
grep -q '^judge_health() {' "$WORK/statusline-health.sh" || fail "statusline: judge_health() not found in config/statusline.sh"
statusline_health() { (export PATH="$WORK/shim-$1:$PATH" AW_STATE_DIR="$2"; . "$WORK/statusline-health.sh"; judge_health); }
for side in cs; do
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
done

# ---- prompt-sort (UserPromptSubmit; cutover spec §10) ----
# `judge prompt-sort` < {prompt, sessionId}: the hook reads .context and ignores the exit. Offline (rules only, so Jev is out
# and the sorter runs on its keyword heuristics) every scaffold is off: nothing is printed, a 'prompt-sort' decision is
# recorded and judge/sessions/<sid>.sort.json is written. A decisive axis needs a stub Jev; no real-hook case reaches one
# (the hook has no seam for it), so that path is the C# unit tests' (SortPromptTests, over FakeBatchClassifier).
sqlite_count() { command -v sqlite3 > /dev/null 2>&1 && sqlite3 "$1/judge/decisions.sqlite" "$2" 2> /dev/null || echo "?"; }
s="$(fresh cs ps-plain)"
hook cs "$s" prompt-sort.sh '{"prompt":"add a dark mode toggle to the settings page","session_id":"s1"}' ps-plain
SORT="$s/judge/sessions/s1.sort.json"
[ "$(rc_of ps-plain)" = 0 ] || fail "ps-plain: exit $(rc_of ps-plain)"
[ -z "$(out_of ps-plain)" ] || fail "ps-plain: every scaffold is off, so the hook prints nothing, got '$(out_of ps-plain)'"
no_failopen ps-plain
jq -e '.prompts == 1 and (.requirements | type) == "object" and (.requirements.uiEvidence // false) == false' "$SORT" > /dev/null 2>&1 \
  || fail "ps-plain: expected judge/sessions/s1.sort.json {prompts:1, requirements:{}}, got '$(cat "$SORT" 2> /dev/null)'"
n="$(sqlite_count "$s" "SELECT COUNT(*) FROM decisions WHERE question = 'prompt-sort'")"
{ [ "$n" = 1 ] || [ "$n" = "?" ]; } || fail "ps-plain: expected one prompt-sort decision row, got $n"
# Machine text and slash commands stop in the hook; a confirmation stops in the sorter. None is recorded or counted.
for c in 'ps-machine|<system-reminder>machine text</system-reminder>' 'ps-slash|/clear' 'ps-confirm|sounds good'; do
  name="${c%%|*}"; prompt="${c#*|}"
  s="$(fresh cs "$name")"
  hook cs "$s" prompt-sort.sh "$(jq -nc --arg p "$prompt" '{prompt:$p, session_id:"s1"}')" "$name"
  [ "$(rc_of "$name")" = 0 ] && [ -z "$(out_of "$name")" ] || fail "$name: exit $(rc_of "$name"), stdout '$(out_of "$name")'"
  [ ! -e "$s/judge/sessions/s1.sort.json" ] || fail "$name: a skipped prompt must not touch the session file"
  n="$(sqlite_count "$s" "SELECT COUNT(*) FROM decisions WHERE question = 'prompt-sort'")"
  { [ "$n" = 0 ] || [ "$n" = "?" ]; } || fail "$name: a skipped prompt records no decision, got $n"
  no_failopen "$name"
done

# ---- done-gate on judge/sessions/<sid>.sort.json (requirements.uiEvidence, written by the sorter) ----
# done-gate reads the flag the sorter set: a done claim with no UI evidence is blocked (exit 2) and the flag stays; the
# "at most once" is stop_hook_active (a re-stop exits 0 first). A claim that shows UI evidence passes the UI check and
# done-gate deletes the flag with jq, keeping the rest. Offline the sorter sets no requirement itself (no Jev-judged UI
# axis), so the flag is set the way the sorter writes it (a jq edit of the C# writer's own file), and the sorter's next run
# must still read the rewritten file.
s="$(fresh cs done-ui)"
hook cs "$s" prompt-sort.sh '{"prompt":"redesign the settings page layout","session_id":"s1"}' done-ui-sort
SORT="$s/judge/sessions/s1.sort.json"
jq '.requirements.uiEvidence = true' "$SORT" > "$SORT.tmp" && mv "$SORT.tmp" "$SORT"
hook cs "$s" done-gate.sh "$(stop "$WORK/t-evidence.jsonl")" done-ui-blocked
[ "$(rc_of done-ui-blocked)" = 2 ] || fail "done-ui-blocked: a done claim with no UI evidence must be blocked (exit 2), got $(rc_of done-ui-blocked)"
err_of done-ui-blocked | has 'UI evidence' || fail "done-ui-blocked: expected the UI evidence message, got '$(err_of done-ui-blocked)'"
jq -e '.requirements.uiEvidence == true and .prompts == 1' "$SORT" > /dev/null 2>&1 || fail "done-ui-blocked: a block keeps the flag and the rest, got '$(cat "$SORT")'"
hook cs "$s" done-gate.sh "$(stop "$WORK/t-evidence.jsonl" | jq -c '. + {stop_hook_active:true}')" done-ui-again
[ "$(rc_of done-ui-again)" = 0 ] || fail "done-ui-again: a re-stop (stop_hook_active) never blocks again, got exit $(rc_of done-ui-again)"
transcript "$WORK/t-shot.jsonl" "Done — settings page updated, ran npm test, screenshot at shots/settings.png."
hook cs "$s" done-gate.sh "$(stop "$WORK/t-shot.jsonl")" done-ui-shown
[ "$(rc_of done-ui-shown)" = 0 ] || fail "done-ui-shown: a claim with a screenshot passes, got exit $(rc_of done-ui-shown): $(err_of done-ui-shown)"
jq -e '(.requirements.uiEvidence // false) == false and .prompts == 1' "$SORT" > /dev/null 2>&1 || fail "done-ui-shown: done-gate must delete requirements.uiEvidence and keep the rest, got '$(cat "$SORT")'"
hook cs "$s" prompt-sort.sh '{"prompt":"redesign the settings page layout again","session_id":"s1"}' done-ui-sort2
jq -e '.prompts == 2' "$SORT" > /dev/null 2>&1 || fail "done-ui-sort2: the sorter must read the jq-rewritten file and count on (prompts 2), got '$(cat "$SORT")'"
no_failopen done-ui-sort done-ui-again done-ui-sort2

# ---- trait-check (PostToolUse edit, Stop; opt-in lever) ----
# exit 0 -> pass; 2 -> findings on stderr, the hook exits 2 with them; 3 -> Jev unavailable, the hook fails open
# logging jev-unavailable; anything else -> judge-exit-N. Offline (rules only) Jev is out, so a repo with a matching
# trait is exit 3 and a repo with none is exit 0. The findings path needs a stub Jev: the C# unit tests have it.
mkrepo() { # mkrepo <dir> [with-trait]
  mkdir -p "$1/src"; git -C "$1" init -q; echo 'export const a = 1;' > "$1/src/a.ts"
  if [ "${2:-}" = with-trait ]; then
    mkdir -p "$1/.agents/traits"
    printf 'version: 1\nname: no-todo\nsubject: TODO comments\nrequirement: No TODO comments are left in source files.\npurpose: Work left undone is tracked in issues.\nguidance: Open an issue and link it.\npaths:\n  - "src/**"\nseverity: block\n' > "$1/.agents/traits/no-todo.yaml"
  fi
  git -C "$1" add -A; git -C "$1" -c user.name=t -c user.email=t@t -c commit.gpgsign=false commit -q -m init
  echo '// TODO later' >> "$1/src/a.ts"
}
inrepo() { local dir="$1"; shift; (cd "$dir" && "$@"); }
EDIT='{"hook_event_name":"PostToolUse","tool_name":"Edit","tool_input":{"file_path":"src/a.ts"}}'
STOPEV='{"hook_event_name":"Stop","session_id":"s1"}'
mkrepo "$WORK/repo-none"; mkrepo "$WORK/repo-trait" with-trait
s="$(fresh cs tc-none-edit)"; inrepo "$WORK/repo-none" hook cs "$s" trait-check.sh "$EDIT" tc-none-edit
s="$(fresh cs tc-none-stop)"; inrepo "$WORK/repo-none" hook cs "$s" trait-check.sh "$STOPEV" tc-none-stop
s="$(fresh cs tc-jev-edit)"; inrepo "$WORK/repo-trait" hook cs "$s" trait-check.sh "$EDIT" tc-jev-edit
for c in tc-none-edit tc-none-stop; do
  [ "$(rc_of $c)" = 0 ] && [ -z "$(out_of $c)" ] && [ -z "$(err_of $c)" ] || fail "$c: a repo with no traits is exit 0 and silent, got exit $(rc_of $c): $(err_of $c)"
done
no_failopen tc-none-edit tc-none-stop
[ "$(rc_of tc-jev-edit)" = 0 ] || fail "tc-jev-edit: Jev unavailable fails open (exit 0), got $(rc_of tc-jev-edit)"
failopen_log tc-jev-edit | has 'jev-unavailable' || fail "tc-jev-edit: expected the hook to log jev-unavailable, got '$(failopen_log tc-jev-edit)'"
# The exit codes are trait-check's own: the CLI itself.
s="$(fresh cs tc-cli)"
rc=0; (cd "$WORK/repo-trait" && judge_on cs "$s" trait-check --edit --file src/a.ts > /dev/null 2> "$WORK/tc-cli.err") || rc=$?
[ "$rc" = 3 ] && has 'jev unavailable' < "$WORK/tc-cli.err" || fail "tc-cli: trait-check with Jev excluded must exit 3 'jev unavailable', got $rc: $(cat "$WORK/tc-cli.err")"

# ---- the not-yet-built stub (cutover spec §5.2, Q9): empty stdout, one stderr line, exit 1 ----
for c in 'label|label' 'label status|label' 'adjudicate ask-check|adjudicate' 'eval wake-gate|eval' 'eval import-turns x.jsonl|eval import-turns' \
  'prompt-sort why abc|prompt-sort why' 'prompt-sort import|prompt-sort import' 'prompt-sort outcomes|prompt-sort outcomes' 'prompt-sort adjudicate|prompt-sort adjudicate' \
  'prompt-sort eval|prompt-sort eval' 'prompt-sort promote --apply|prompt-sort promote'; do
  args="${c%%|*}"; verb="${c#*|}"; rc=0
  # shellcheck disable=SC2086
  out="$(judge_on cs "$(fresh cs stub)" $args 2> "$WORK/stub.err")" || rc=$?
  { [ "$rc" = 1 ] && [ -z "$out" ] && [ "$(cat "$WORK/stub.err")" = "judge: $verb is not in this build yet" ]; } \
    || fail "stub '$args': expected exit 1, empty stdout and 'judge: $verb is not in this build yet', got exit $rc, stdout '$out', stderr '$(cat "$WORK/stub.err")'"
done
rc=0; judge_on cs "$(fresh cs stub)" prompt-sort bogus > /dev/null 2> "$WORK/stub.err" || rc=$?
{ [ "$rc" = 1 ] && [ "$(cat "$WORK/stub.err")" = "judge: unknown prompt-sort subcommand: bogus" ]; } || fail "prompt-sort bogus: got exit $rc, '$(cat "$WORK/stub.err")'"

if [ "$FAILS" -gt 0 ]; then echo "judge-cs-hook-surface: $FAILS failure(s)"; exit 1; fi
echo "All judge-cs-hook-surface tests passed."
