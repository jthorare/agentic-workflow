#!/usr/bin/env bash
# Tests for scripts/model-limit-check.sh: the refusals of a home or port that isn't this check's, the dry runs (nothing written, no sindri
# called), the fake claude and codex (the usage-limit answer while limited, a well-formed answer otherwise), and a whole up, pause,
# resume --cli and down against a stub sindri that calls the fakes through the PATH the script gives it. Nothing here touches a real
# Sindri home, port or profile, starts a server or needs ollama, graphify or temporal (the stub run turns the index layers off).
# Run: bash scripts/tests/model-limit-check.test.sh
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
SCRIPT="$ROOT/scripts/model-limit-check.sh"
PASS=0
FAIL=0
T="$(mktemp -d "${TMPDIR:-/tmp}/model-limit-check-test.XXXXXX")"
trap 'rm -rf "$T"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
# expect <name> <exit code> <substring>...  (uses $CODE and $OUT)
expect() {
  local name="$1" code="$2" good=1 s; shift 2
  [ "$CODE" = "$code" ] || good=0
  for s in "$@"; do case "$OUT" in *"$s"*) ;; *) good=0 ;; esac; done
  if [ "$good" = 1 ]; then ok "$name"; else fail "$name (exit $CODE, want $code)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
}
logged() { grep -q -- "$1" "$STUB_LOG" 2>/dev/null; }

STATE="$T/state"
REPO="$T/work/agentic-workflow"
HOME_DIR="$STATE/sindri-app-firstrun-modellimit"
mkdir -p "$STATE" "$REPO"
git -C "$REPO" init -q -b main && git -C "$REPO" -c user.name=t -c user.email=t@t commit -q --allow-empty -m init

run() { OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$REPO" SINDRI="$T/stub-sindri" MODEL_LIMIT_POLL_SECONDS=1 MODEL_LIMIT_PAUSE_TIMEOUT=3 MODEL_LIMIT_JOB_TIMEOUT=3 "$SCRIPT" "$@" 2>&1)"; CODE=$?; }

# ---- the stub sindri: logs each call, answers from flag files in the home; refresh and resume call the fakes as serve would ----
LABEL_PROMPT=$'Name each group of related source files as one feature of a software product: 2 to 5 plain words.\nAnswer with JSON only.\nGroups:\nc1: src/a.cs, src/b.cs\nc2: docs/x.md\n'
export LABEL_PROMPT
cat > "$T/stub-sindri" <<'STUB'
#!/usr/bin/env bash
echo "home=$(basename "${AW_SINDRI_HOME:-unset}") port=${AW_SINDRI_PORT:-unset} $*" >> "$STUB_LOG"
s="$AW_SINDRI_HOME/stub"; mkdir -p "$s"
flag() { [ -e "$s/$1" ]; }
case "$*" in
  "profile init") printf 'schemaVersion: 1\nuser: "thor"\n' > "$AW_SINDRI_HOME/profile/profile.yaml" ;;
  "profile validate --json") printf '{"valid":true,"hash":"%s"}\n' "$(printf 'ab%.0s' $(seq 1 32))"; flag approved && exit 0; exit 1 ;;
  "--headless") touch "$s/serving" ;;
  "status --json") if flag serving; then echo '{"running":true,"profile":{"needsApproval":false}}'; else echo '{"running":false}'; fi; exit 1 ;;
  "status") echo "running" ;;
  "code-index status --repo agentic-workflow --json") flag indexed && { echo '{"repos":[{"state":"ok"}]}'; exit 0; }; exit 1 ;;
  "services run code-index-main-move") touch "$s/indexed" ;;
  "context map refresh --detach --json")
    # serve's labeling call walks claude-code, then codex; both at their limit park the job
    printf '%s' "$LABEL_PROMPT" | claude -p --output-format json >/dev/null; c=$?
    printf '%s' "$LABEL_PROMPT" | codex exec --json -- - >/dev/null; x=$?
    [ "$c" != 0 ] && [ "$x" != 0 ] && touch "$s/waiting"
    echo '{"accepted":{"commandId":"c","sequence":1},"jobId":"JOB-0001","links":{}}' ;;
  "jobs show JOB-0001 --json")
    if flag done; then echo '{"id":"JOB-0001","state":"succeeded","waitingOn":null}'
    elif flag waiting; then echo '{"id":"JOB-0001","state":"waiting","waitingOn":"model-limit"}'
    else echo '{"id":"JOB-0001","state":"running","waitingOn":null}'; fi ;;
  "jobs show JOB-0001") echo "JOB-0001 context-map-refresh $(flag done && echo succeeded || echo 'waiting on model-limit: claude-code at its usage limit; codex at its usage limit')" ;;
  "jobs resume JOB-0001")
    flag waiting || { echo "SND-JOB-003" >&2; exit 2; }
    rm -f "$s/waiting"
    printf '%s' "$LABEL_PROMPT" | claude -p --output-format json >/dev/null && touch "$s/done" ;;
  "context map status") echo "map v1 current" ;;
  "stop") rm -f "$s/serving" ;;
  *) echo "stub: unexpected: $*" >&2; exit 2 ;;
esac
STUB
chmod +x "$T/stub-sindri"
export STUB_LOG="$T/calls.log"

echo "model-limit-check: dry runs"
run up --dry-run
expect "up --dry-run walks home, fakes, first run to step 5, refresh, the pause and the app (exit 0)" 0 \
  "sindri-app-firstrun-modellimit (port 7482)" "fake-bin/claude" "harnesses: [claude-code, codex]" "stopped after step 5" \
  "context map refresh --detach --json" "jobs show <job> --json" '.waitingOn == "model-limit"' "Check 1 (#2112 step 1)"
case "$OUT" in *"context map status --json"*|*"recipe list"*) fail "up --dry-run stops the first run before its map and recipe steps" ;; *) ok "up --dry-run stops the first run before its map and recipe steps" ;; esac
run resume --cli --dry-run
expect "resume --cli --dry-run brings claude-code back, resumes and waits for success" 0 "rm $HOME_DIR/model-limit/limited-claude-code" "jobs resume <job>" '.state == "succeeded"'
run resume --dry-run
expect "resume --dry-run waits for the Resume pressed in the app" 0 "press Resume" '.waitingOn == "model-limit") | not'
run limit codex --dry-run
expect "limit --dry-run prints the flag it would write" 0 "touch $HOME_DIR/model-limit/limited-codex"
run down --dry-run
expect "down --dry-run hands the scratch home to context-first-run.sh down" 0 "rm -rf $HOME_DIR"
if [ ! -e "$HOME_DIR" ]; then ok "no dry run creates the home"; else fail "no dry run creates the home"; fi
if [ ! -e "$STUB_LOG" ]; then ok "no dry run calls sindri"; else fail "no dry run calls sindri"; fi

echo "model-limit-check: refusals"
for h in "$STATE/sindri-app" "$STATE/sindri-app-firstrun" "$T/elsewhere"; do
  OUT="$(AW_STATE_DIR="$STATE" MODEL_LIMIT_HOME="$h" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
  expect "refuses MODEL_LIMIT_HOME=${h#"$T"/}" 2 "refusing"
done
OUT="$(AW_STATE_DIR="$STATE" MODEL_LIMIT_HOME=sindri-app-firstrun-modellimit "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
expect "refuses a relative MODEL_LIMIT_HOME" 2 "absolute path"
mkdir -p "$T/real/sindri-app-firstrun-modellimit" && echo x > "$T/real/sindri-app-firstrun-modellimit/ledger.db"
OUT="$(AW_STATE_DIR="$STATE" MODEL_LIMIT_HOME="$T/real/sindri-app-firstrun-modellimit" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
expect "refuses a home that holds files and has no marker" 2 "no .first-run-scratch marker"
OUT="$(AW_STATE_DIR="$STATE" MODEL_LIMIT_PORT=7410 "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
expect "refuses the real instance's port" 2 "7410"
OUT="$(AW_STATE_DIR="$STATE" MODEL_LIMIT_POLL_SECONDS=x "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
expect "refuses a poll interval that isn't whole seconds" 2 "whole seconds"
OUT="$(AW_STATE_DIR="$STATE" "$SCRIPT" limit cursor 2>&1)"; CODE=$?
expect "limit takes claude-code or codex only" 2 "claude-code or codex"
OUT="$(AW_STATE_DIR="$STATE" "$SCRIPT" up --cli 2>&1)"; CODE=$?
expect "--cli belongs to resume only" 2 "resume"
OUT="$(AW_STATE_DIR="$STATE" "$SCRIPT" up --bogus 2>&1)"; CODE=$?
expect "an unknown option is a usage error" 2 "unknown option"
OUT="$(AW_STATE_DIR="$STATE" "$SCRIPT" resume 2>&1)"; CODE=$?
expect "resume before up says to run up" 2 "run scripts/model-limit-check.sh up first"

if ! command -v jq >/dev/null 2>&1; then
  echo "  skip the real-run cases: jq isn't installed"
else
  mkdir -p "$STATE/cache/bin/temporal-1.9.1"
  echo "model-limit-check: up stops for approval, with the fakes in place"
  run up --no-embeddings --no-graph --no-ui
  expect "up writes the profile, then stops for approval and names its own continue (exit 3)" 3 "profile approve abababab" "Then run:  scripts/model-limit-check.sh up"
  P="$HOME_DIR/profile/profile.yaml"
  if grep -q '^  models:' "$P" && grep -q 'default: { harnesses: \[claude-code, codex\] }' "$P"; then ok "the profile routes every kind to claude-code, then codex"; else fail "the profile routes every kind to claude-code, then codex"; sed 's/^/       | /' "$P"; fi
  if logged "home=sindri-app-firstrun-modellimit port=7482" && ! logged "port=7481" && ! logged "port=7410"; then ok "every call used the check's home and port 7482"; else fail "every call used the check's home and port 7482"; fi

  echo "model-limit-check: the fakes"
  F="$HOME_DIR/fake-bin"; L="$HOME_DIR/model-limit"
  if [ -x "$F/claude" ] && [ -x "$F/codex" ] && [ -e "$L/limited-claude-code" ] && [ -e "$L/limited-codex" ]; then ok "both fakes are written, both at their limit"; else fail "both fakes are written, both at their limit"; ls -la "$F" "$L" 2>&1 | sed 's/^/       | /'; fi
  OUT="$(printf '%s' "$LABEL_PROMPT" | env -i PATH="$PATH" HOME="$T" "$F/claude" -p --output-format json)"; CODE=$?
  if [ "$CODE" = 1 ] && [ "$(printf '%s' "$OUT" | jq -r '.is_error')" = true ] && printf '%s' "$OUT" | jq -r .result | grep -q "^You've hit your session limit"; then ok "a limited claude prints its real limit envelope and exits 1 (minimal environment)"; else fail "a limited claude prints its real limit envelope (exit $CODE)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
  OUT="$(printf '%s' "$LABEL_PROMPT" | "$F/codex" exec --json -- -)"; CODE=$?
  if [ "$CODE" = 1 ] && printf '%s\n' "$OUT" | jq -se 'map(.type) == ["error","turn.failed"] and (.[0].message | startswith("You'"'"'ve hit your usage limit"))' >/dev/null; then ok "a limited codex prints an error event and turn.failed and exits 1"; else fail "a limited codex prints an error event and turn.failed (exit $CODE)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
  run unlimit claude-code
  expect "unlimit takes one fake off its limit" 0 "limited: codex"
  OUT="$(printf '%s' "$LABEL_PROMPT" | "$F/claude" -p --output-format json)"; CODE=$?
  if [ "$CODE" = 0 ] && [ "$(printf '%s' "$OUT" | jq -c '.structured_output.decision, (.structured_output.text | fromjson | map(.id + "=" + .labels[0]))' | tr '\n' ' ')" = '"answered" ["c1=Fake feature c1","c2=Fake feature c2"] ' ]; then ok "claude answers a labeling prompt with one label per group in structured_output.text"; else fail "claude answers a labeling prompt (exit $CODE)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
  run unlimit codex
  OUT="$(printf 'Proposals\nFor each numbered proposal decide\nn1: X | a\nn2: Y\n' | "$F/codex" exec --json -- -)"; CODE=$?
  if [ "$CODE" = 0 ] && [ "$(printf '%s\n' "$OUT" | jq -sc '[.[0].item.type, (.[0].item.text | fromjson | .text | fromjson | map(.keep)), .[1].type]')" = '["agent_message",[true,true],"turn.completed"]' ]; then ok "codex answers a cleanup prompt with an agent_message keeping every proposal"; else fail "codex answers a cleanup prompt (exit $CODE)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
  OUT="$("$F/claude" --version)"; CODE=$?
  expect "a fake asked anything but a prompt prints a version and exits 0" 0 "fake-claude"
  if [ "$(cut -d' ' -f2- "$L/calls.log" | tr '\n' '|')" = "claude-code limit|codex limit|claude-code answer labeling|codex answer map-cleanup|" ]; then ok "every prompt call is one line in calls.log"; else fail "every prompt call is one line in calls.log"; sed 's/^/       | /' "$L/calls.log"; fi
  run limit claude-code; run limit codex
  expect "limit puts a fake back at its limit" 0 "limited: claude-code, codex"
  : > "$L/calls.log"

  echo "model-limit-check: approve, pause, resume, down"
  mkdir -p "$HOME_DIR/stub"; : > "$HOME_DIR/stub/approved"
  : > "$STUB_LOG"
  run up --no-embeddings --no-graph --no-ui
  expect "after approval, up serves, indexes, refreshes and waits until the job pauses on model-limit (exit 0)" 0 "JOB-0001 is waiting on model-limit" "Check 1 (#2112 step 1)"
  if logged '--headless' && logged 'services run code-index-main-move' && logged 'context map refresh --detach --json' && ! logged 'recipe list'; then ok "serve, the index and the refresh ran; nothing past step 5 of the first run"; else fail "serve, the index and the refresh ran; nothing past step 5 of the first run"; sed 's/^/       | /' "$STUB_LOG"; fi
  if [ "$(cut -d' ' -f2- "$L/calls.log" | tr '\n' '|')" = "claude-code limit|codex limit|" ]; then ok "the refresh reached both fakes through the PATH the script gave sindri"; else fail "the refresh reached both fakes through the PATH"; sed 's/^/       | /' "$L/calls.log"; fi
  if [ "$(cat "$L/job.id")" = JOB-0001 ]; then ok "the job id is kept"; else fail "the job id is kept"; fi
  run resume --cli
  expect "resume --cli brings claude-code back, resumes, and the job succeeds with a labeling answer (exit 0)" 0 "Check 2 (#2112 step 2)" "the fake claude answered 1 call(s)"
  if [ -e "$L/limited-codex" ] && [ ! -e "$L/limited-claude-code" ]; then ok "codex stays at its limit, claude-code is back"; else fail "codex stays at its limit, claude-code is back"; fi
  run status
  expect "status shows the job, the limits and the last calls" 0 "JOB-0001" "limited: codex" "claude-code answer labeling"
  run down
  expect "down stops the plane and deletes the home" 0 "deleted"
  if [ ! -e "$HOME_DIR" ]; then ok "the home is gone"; else fail "the home is gone"; fi
fi

echo
echo "model-limit-check: $PASS passed, $FAIL failed"
[ "$FAIL" = 0 ]
