#!/usr/bin/env bash
# Tests for scripts/context-first-run.sh: the dry-run transcript (a golden), the refusal of a home that looks real, the stop at
# approval, a whole run against a stub sindri in a temp state dir, and `down` on a missing home. Nothing here touches a real
# Sindri home, port or profile, starts a server, pulls a model or needs ollama, graphify or temporal (the stub run turns the
# embeddings and graph layers off). Run: bash scripts/tests/context-first-run.test.sh
# Regenerate the golden after an intended change: CFR_GOLDEN_UPDATE=1 bash scripts/tests/context-first-run.test.sh
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
SCRIPT="$ROOT/scripts/context-first-run.sh"
GOLDEN="$HERE/fixtures/context-first-run.up-dry-run.txt"
PASS=0
FAIL=0
T="$(mktemp -d "${TMPDIR:-/tmp}/context-first-run-test.XXXXXX")"
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
lacks() { case "$OUT" in *"$2"*) fail "$1"; printf '%s\n' "$OUT" | sed 's/^/       | /' ;; *) ok "$1" ;; esac; }
logged() { grep -q -- "$1" "$STUB_LOG" 2>/dev/null; }

STATE="$T/state"
REPO="$T/work/agentic-workflow"
mkdir -p "$STATE" "$REPO"
git -C "$REPO" init -q -b main && git -C "$REPO" -c user.name=t -c user.email=t@t commit -q --allow-empty -m init

# The runs that reach the preflight probe their port, so they take a free one from the kernel: the script's default 7481 is
# shared by every checkout and by a real first run on the box, and a listener there turned this suite red (#3029). The dry runs
# keep the default, because they print the port and bind or probe nothing.
PORT="$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()')"

# run <args…>: the script against the temp state and repo, with the stub sindri. Sets OUT (both streams) and CODE.
run() {
  OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_PORT="$PORT" FIRST_RUN_REPO_PATH="$REPO" SINDRI="$T/stub-sindri" "$SCRIPT" "$@" 2>&1)"; CODE=$?
}
norm() { sed -e "s#$T#<T>#g" -e "s#$ROOT#<ROOT>#g"; }

# ---- the stub sindri: logs each call, answers from flag files in the home ----
cat > "$T/stub-sindri" <<'STUB'
#!/usr/bin/env bash
echo "home=$(basename "${AW_SINDRI_HOME:-unset}") port=${AW_SINDRI_PORT:-unset} $*" >> "$STUB_LOG"
echo "${AW_SINDRI_APP_NO_MCP:-unset}" >> "$STUB_LOG.mcp"
flag() { [ -e "$AW_SINDRI_HOME/stub/$1" ]; }
case "$*" in
  "profile init") printf 'schemaVersion: 1\nuser: "thor"\nworkflow:\n  effects: []\n' > "$AW_SINDRI_HOME/profile/profile.yaml" ;;
  "profile validate --json")
    printf '{"valid":true,"hash":"%s"}\n' "$(printf 'ab%.0s' $(seq 1 32))" ;;
  "--headless") mkdir -p "$AW_SINDRI_HOME/stub"; touch "$AW_SINDRI_HOME/stub/serving" ;;
  "status --json")
    if flag serving; then echo '{"running":true}'; else echo '{"running":false}'; fi; exit 1 ;;
  "status") echo "running" ;;
  "code-index status --repo agentic-workflow --json") flag indexed && { echo '{"repos":[{"state":"ok"}]}'; exit 0; }; echo '{"repos":[{"state":"missing"}]}'; exit 1 ;;
  "code-index status --repo agentic-workflow") echo "agentic-workflow ok" ;;
  "services run code-index-main-move") touch "$AW_SINDRI_HOME/stub/indexed" ;;
  "context map status --json")
    flag mapped || exit 1
    # stub/mapstate: one state per line; each call reads the first and drops it unless it is the last (sticky). Default published.
    f="$AW_SINDRI_HOME/stub/mapstate"; st=published
    if [ -s "$f" ]; then st="$(head -1 "$f")"; [ "$(wc -l < "$f")" -le 1 ] || sed -i.bak 1d "$f"; fi
    printf '{"state":"%s","publishedVersion":1}\n' "$st"; exit 0 ;;
  "context map status") echo "map v1 current" ;;
  "context map refresh")
    # stub/busy: the map service already runs a refresh (#3010): refuse with its job id, as the real verb does
    if flag busy; then
      echo "SND-JOB-409 A RefreshContextMap job is already running for refresh (fix: run \`sindri jobs show JOB-0123abcd\`, or wait for it to end)" >&2
      exit 2
    fi
    touch "$AW_SINDRI_HOME/stub/mapped" ;;
  "jobs show JOB-0123abcd --json")
    # stub/jobstate: one state per line, read like mapstate; the service's job maps the repo when it succeeds
    f="$AW_SINDRI_HOME/stub/jobstate"; st=succeeded
    if [ -s "$f" ]; then st="$(head -1 "$f")"; [ "$(wc -l < "$f")" -le 1 ] || sed -i.bak 1d "$f"; fi
    [ "$st" != succeeded ] || touch "$AW_SINDRI_HOME/stub/mapped"
    printf '{"id":"JOB-0123abcd","kind":"RefreshContextMap","state":"%s"}\n' "$st"; exit 0 ;;
  "stop") rm -f "$AW_SINDRI_HOME/stub/serving" ;;
  *) echo "stub: unexpected: $*" >&2; exit 2 ;;
esac
STUB
chmod +x "$T/stub-sindri"
export STUB_LOG="$T/calls.log"

echo "context-first-run: dry run"
OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$REPO" "$SCRIPT" up --dry-run 2>&1 | norm)"; CODE=${PIPESTATUS[0]}
if [ "${CFR_GOLDEN_UPDATE:-0}" = 1 ]; then printf '%s\n' "$OUT" > "$GOLDEN"; echo "  (golden rewritten: $GOLDEN)"; fi
if [ "$CODE" = 0 ] && [ "$OUT" = "$(cat "$GOLDEN" 2>/dev/null)" ]; then ok "the up --dry-run transcript matches its golden"; else fail "the up --dry-run transcript matches its golden (exit $CODE)"; diff <(printf '%s\n' "$OUT") "$GOLDEN" | head -20 | sed 's/^/       | /'; fi
steps="$(printf '%s\n' "$OUT" | grep '^== ' | sed -E 's/^== ([0-9]+)\. .*/\1/' | tr '\n' ' ')"
if [ "$steps" = "0 1 2 3 4 5 6 7 " ]; then ok "the steps run 0 to 7 in order"; else fail "the steps run 0 to 7 in order (got: $steps)"; fi
if [ ! -e "$STATE/sindri-app-firstrun" ]; then ok "a dry run creates no home"; else fail "a dry run creates no home"; fi
cmds="$(printf '%s\n' "$OUT" | grep -E '^\+ AW_SINDRI_HOME=[^ ]+ AW_SINDRI_PORT=[0-9]+ AW_SINDRI_APP_NO_MCP=1 sindri ' | sed -E 's#^.*NO_MCP=1 sindri ##' | awk '{print $1" "$2}' | tr '\n' '|')"
want="profile init|profile validate|--headless |status --json|code-index status|services run|context map|context map|context map|"
if [ "$cmds" = "$want" ]; then ok "the commands come in the documented order"; else fail "the commands come in the documented order (got: $cmds)"; fi
OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$REPO" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
expect "the dry run asks for no approval and keeps going" 0 "edits apply on save"
OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$REPO" FIRST_RUN_CONTEXT_EXTRA="  models: { default: { harnesses: [codex] } }" "$SCRIPT" up --dry-run --stop-after 5 2>&1)"; CODE=$?
expect "--stop-after 5 ends after the index build, and FIRST_RUN_CONTEXT_EXTRA lands in the context: section (exit 0)" 0 "stopped after step 5" "|   publish: { waitForIndexMinutes: 15 }
    |   models: { default: { harnesses: [codex] } }"
case "$OUT" in *"== 6."*) fail "--stop-after 5 runs nothing past step 5" ;; *) ok "--stop-after 5 runs nothing past step 5" ;; esac
OUT="$(AW_STATE_DIR="$STATE" "$SCRIPT" up --stop-after x 2>&1)"; CODE=$?
expect "--stop-after takes a step number" 2 "step number 0 to 7"
OUT="$(AW_STATE_DIR="$STATE" "$SCRIPT" down --stop-after 5 2>&1)"; CODE=$?
expect "--stop-after belongs to up only" 2
run down --dry-run
expect "down --dry-run prints stop and delete, runs nothing" 0 "sindri-app-firstrun" " stop" "rm -rf"
run status --dry-run
expect "status --dry-run prints its reads" 0 "code-index status --repo agentic-workflow" "context map status"
if [ ! -e "$STUB_LOG" ]; then ok "no dry run called the sindri command"; else fail "no dry run called the sindri command"; fi

echo "context-first-run: a home that looks real is refused"
for h in "$STATE/sindri-app" "$T/elsewhere" "$STATE/sindri-app-ownercheck-s1"; do
  OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_HOME="$h" FIRST_RUN_REPO_PATH="$REPO" SINDRI="$T/stub-sindri" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
  expect "refuses FIRST_RUN_HOME=${h#"$T"/}" 2 "refusing"
done
OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_HOME="sindri-app-firstrun" FIRST_RUN_REPO_PATH="$REPO" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
expect "refuses a relative FIRST_RUN_HOME" 2 "absolute path"
mkdir -p "$T/real/sindri-app-firstrun" && echo x > "$T/real/sindri-app-firstrun/ledger.db"
OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_HOME="$T/real/sindri-app-firstrun" FIRST_RUN_REPO_PATH="$REPO" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
expect "refuses a first-run-named home that holds files and has no marker" 2 "no .first-run-scratch marker"
OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_PORT=7410 FIRST_RUN_REPO_PATH="$REPO" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?  # test-ports: ok: dry run, the script refuses or rewrites the port and binds nothing
expect "refuses the real instance's port" 2 "7410"
OUT="$(AW_STATE_DIR="$STATE" "$SCRIPT" up --bogus 2>&1)"; CODE=$?
expect "an unknown option is a usage error" 2 "unknown option"

echo "context-first-run: the repo name"
NAMED="$T/work/first-run-main"
git -C "$REPO" worktree add -q --detach "$NAMED" main
git -C "$REPO" remote add origin "git@example.com:owner/real-name.git"
nm() { OUT="$(env AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$1" FIRST_RUN_REPO_NAME="${2:-}" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?; }
nm "$NAMED"
expect "a worktree named differently from its origin gets the origin's name (scp-style URL)" 0 "code-index status --repo real-name"
git -C "$REPO" remote set-url origin "https://example.com/owner/https-name/"
nm "$NAMED"
expect "an https origin with a trailing slash gives its last segment" 0 "--repo https-name"
nm "$NAMED" custom-name
expect "FIRST_RUN_REPO_NAME overrides the origin's name" 0 "--repo custom-name"
nm "$NAMED" "bad name"
expect "an invalid FIRST_RUN_REPO_NAME is a usage error" 2 "not a profile repo name"
git -C "$REPO" remote remove origin
nm "$NAMED"
expect "with no origin the name falls back to the path's basename" 0 "--repo first-run-main"

echo "context-first-run: the checkout preflight"
# A real (non-dry) run reaches the preflight; --no-embeddings --no-graph keep it to git, jq and temporal. Only the repo checks matter here.
pf() { # pf <repo path>: sets OUT and CODE of a real up against that repo
  OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_PORT="$PORT" FIRST_RUN_REPO_PATH="$1" SINDRI="$T/stub-sindri" "$SCRIPT" up --stop-after 0 --no-embeddings --no-graph --no-ui 2>&1)"; CODE=$?
}
G() { git -C "$1" -c user.name=t -c user.email=t@t "${@:2}"; }
if command -v jq >/dev/null 2>&1; then
  mkdir -p "$STATE/cache/bin/temporal-1.9.1"
  DET="$T/work/detached"
  git -C "$REPO" worktree add -q --detach "$DET" main
  pf "$DET"
  case "$OUT" in
    *"HEAD is not contained"*|*"uncommitted"*|*"has none of"*) fail "a detached HEAD at main passes the checkout preflight"; printf '%s\n' "$OUT" | sed 's/^/       | /' ;;
    *) ok "a detached HEAD at main passes the checkout preflight" ;;
  esac
  G "$REPO" worktree add -q -b side "$T/work/side" main
  G "$T/work/side" commit -q --allow-empty -m "not in main"
  pf "$T/work/side"
  expect "a commit that is not in main is refused" 1 "HEAD is not contained in main" "git worktree add --detach"
  echo x > "$DET/stray.txt"
  pf "$DET"
  expect "a dirty linked worktree is refused" 1 "uncommitted or untracked" "clean tree"
  rm -f "$DET/stray.txt"
  echo x > "$REPO/stray.txt"
  pf "$REPO"
  expect "a dirty main checkout is refused and named" 1 "shared main checkout" "index a clean worktree"
  rm -f "$REPO/stray.txt"
  pf "$REPO"
  case "$OUT" in
    *"tools ok; agentic-workflow at $REPO"*) ok "FIRST_RUN_REPO_PATH still overrides the default repo" ;;
    *) fail "FIRST_RUN_REPO_PATH still overrides the default repo"; printf '%s\n' "$OUT" | sed 's/^/       | /' ;;
  esac
  rm -rf "$STATE/sindri-app-firstrun"
else
  echo "  skip the checkout preflight cases: jq isn't installed"
fi

echo "context-first-run: no approval stop, a whole run"
# jq is a real dependency of the script; the real-run cases need it.
if ! command -v jq >/dev/null 2>&1; then
  echo "  skip the real-run cases: jq isn't installed"
else
  mkdir -p "$STATE/cache/bin/temporal-1.9.1"
  run up --no-embeddings --no-graph --no-ui
  expect "up writes the profile, serves, indexes and maps (exit 0)" 0 "context-first-run up: done"
  lacks "no profile approval is asked for" "profile approve"
  lacks "no recipe approval is asked for" "approve-all"
  if [ -f "$STATE/sindri-app-firstrun/.first-run-scratch" ]; then ok "the home carries the scratch marker"; else fail "the home carries the scratch marker"; fi
  P="$STATE/sindri-app-firstrun/profile/profile.yaml"
  if grep -q "path: $REPO" "$P" && grep -q 'context-docs-refresh' "$P" && grep -q 'enabled: false' "$P" && grep -q '^context:' "$P" && grep -q '^index:' "$P"; then ok "the profile onboards the repo with services, index and context"; else fail "the profile onboards the repo with services, index and context"; sed 's/^/       | /' "$P"; fi
  # test-ports: ok: the port=N strings are what the stub sindri logged, matched as text; nothing binds them
  if logged "home=sindri-app-firstrun port=$PORT " && ! logged "port=7480" && ! logged "port=7410"; then ok "every call used the scratch home and the run's port"; else fail "every call used the scratch home and the run's port"; fi
  before="$(wc -l < "$P" | tr -d ' ')"
  run up --no-embeddings --no-graph --no-ui
  expect "a second up finishes too (exit 0)" 0 "context-first-run up: done"
  if [ "$(wc -l < "$P" | tr -d ' ')" = "$before" ]; then ok "the profile isn't written twice"; else fail "the profile isn't written twice"; fi

  if logged '--headless' && logged 'services run code-index-main-move' && logged 'context map refresh'; then ok "serve, the index build and the map refresh ran"; else fail "serve, the index build and the map refresh ran"; fi
  if [ -s "$STUB_LOG.mcp" ] && [ "$(sort -u "$STUB_LOG.mcp")" = 1 ]; then ok "every sindri call runs with AW_SINDRI_APP_NO_MCP=1 (no MCP registration in ~/.claude.json)"; else fail "every sindri call runs with AW_SINDRI_APP_NO_MCP=1"; sort "$STUB_LOG.mcp" | uniq -c | sed 's/^/       | /'; fi
  : > "$STUB_LOG"
  run continue --no-embeddings --no-graph --no-ui
  expect "a second continue finishes too (exit 0)" 0 "context-first-run up: done"
  if ! logged 'services run' && ! logged 'context map refresh' && ! logged '--headless'; then ok "a finished step is skipped on the second pass"; else fail "a finished step is skipped on the second pass"; sed 's/^/       | /' "$STUB_LOG"; fi

  # step 6's waiting paths: forget the map, script the states the status answers, and run again
  step6() { # step6 <state>… ; leaves OUT and CODE of a continue with a 1 s poll and a 3 s timeout
    rm -f "$STATE/sindri-app-firstrun/stub/mapped" "$STATE/sindri-app-firstrun/stub/mapstate.bak"
    : > "$STATE/sindri-app-firstrun/stub/mapstate"
    local st; for st in "$@"; do echo "$st" >> "$STATE/sindri-app-firstrun/stub/mapstate"; done
    : > "$STUB_LOG"
    OUT="$(FIRST_RUN_MAP_POLL_SECONDS=1 FIRST_RUN_MAP_TIMEOUT=3 AW_STATE_DIR="$STATE" FIRST_RUN_PORT="$PORT" FIRST_RUN_REPO_PATH="$REPO" SINDRI="$T/stub-sindri" "$SCRIPT" continue --no-embeddings --no-graph --no-ui 2>&1)"; CODE=$?
  }
  step6 held held published
  expect "a held map is waited for, then published (exit 0)" 0 "the map is held" "map published (v1, published)"
  if logged 'context map refresh'; then ok "the refresh ran once for it"; else fail "the refresh ran once for it"; fi
  step6 published-stale
  expect "published-stale fails step 6 and prints the status command" 1 "step 6 (map)" "'published-stale'" "context map status"
  step6 held
  expect "a map still held at the timeout fails step 6" 1 "step 6 (map)" "still held after" "context map status"
  step6 overdue overdue published-without-waiting
  expect "overdue is waited for like held, and published-without-waiting passes" 0 "the map is overdue" "map published (v1, published-without-waiting)"
  # #3010: the map service started its own refresh before step 6, so 'context map refresh' answers SND-JOB-409
  F="$STATE/sindri-app-firstrun/stub"
  : > "$F/busy"; printf 'running\nrunning\nsucceeded\n' > "$F/jobstate"
  step6 held published
  expect "SND-JOB-409: the service's job is followed until it succeeds, then the map publishes (exit 0)" 0 "already building the map (JOB-0123abcd)" "waiting for JOB-0123abcd" "JOB-0123abcd succeeded" "map published (v1, published)"
  if logged 'jobs show JOB-0123abcd --json'; then ok "the named job is read with jobs show"; else fail "the named job is read with jobs show"; fi
  printf 'waiting\nsucceeded\n' > "$F/jobstate"
  step6 published
  expect "SND-JOB-409 and the job is waiting (parked on a model limit): it is followed, not failed (exit 0)" 0 "JOB-0123abcd is waiting" "jobs resume JOB-0123abcd" "JOB-0123abcd succeeded"
  printf 'running\nfailed\n' > "$F/jobstate"
  step6 published
  expect "SND-JOB-409 and the service's job fails: step 6 fails and names the job" 1 "step 6 (map)" "JOB-0123abcd ended 'failed'" "jobs show JOB-0123abcd"
  echo running > "$F/jobstate"
  step6 published
  expect "SND-JOB-409 and the job still runs at the timeout: step 6 fails" 1 "step 6 (map)" "JOB-0123abcd was still running after"
  rm -f "$F/busy" "$F/jobstate" "$F/jobstate.bak"

  rm -f "$STATE/sindri-app-firstrun/stub/mapstate"; : > "$STATE/sindri-app-firstrun/stub/mapped"

  run status
  expect "status reads the plane, the index and the map" 0 "agentic-workflow ok" "map v1 current"
  run down
  expect "down deletes the home" 0 "deleted"
  if [ ! -e "$STATE/sindri-app-firstrun" ]; then ok "the home is gone"; else fail "the home is gone"; fi
fi

echo "context-first-run: down on a missing home"
run down
expect "down with no home says there is nothing to clean (exit 0)" 0 "nothing to clean"
run status
expect "status with no home says so (exit 1)" 1 "no first-run home"

echo
echo "context-first-run: $PASS passed, $FAIL failed"
[ "$FAIL" = 0 ]
