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

# run <args…>: the script against the temp state and repo, with the stub sindri. Sets OUT (both streams) and CODE.
run() {
  OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$REPO" SINDRI="$T/stub-sindri" "$SCRIPT" "$@" 2>&1)"; CODE=$?
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
    printf '{"valid":true,"hash":"%s"}\n' "$(printf 'ab%.0s' $(seq 1 32))"
    flag approved && exit 0; exit 1 ;;
  "--headless") touch "$AW_SINDRI_HOME/stub/serving" ;;
  "status --json")
    if flag serving; then echo '{"running":true,"profile":{"needsApproval":false}}'; else echo '{"running":false}'; fi; exit 1 ;;
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
  "context map refresh") touch "$AW_SINDRI_HOME/stub/mapped" ;;
  "recipe list") flag recipes && echo 'workflow.scoping@0123456789ab approved (shipped)' || echo 'workflow.scoping@0123456789ab unapproved (shipped)' ;;
  "item add "*) echo "ITM-0A1B2C created (sequence 3)" ;;
  "workflow start "*) echo "workflow.scoping started" ;;
  "workflow status "*) echo "ITM-0A1B2C/workflow.scoping/1 running" ;;
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
if [ "$steps" = "0 1 2 3 4 5 6 7 8 9 " ]; then ok "the steps run 0 to 9 in order"; else fail "the steps run 0 to 9 in order (got: $steps)"; fi
if [ ! -e "$STATE/sindri-app-firstrun" ]; then ok "a dry run creates no home"; else fail "a dry run creates no home"; fi
cmds="$(printf '%s\n' "$OUT" | grep -E '^\+ AW_SINDRI_HOME=[^ ]+ AW_SINDRI_PORT=[0-9]+ AW_SINDRI_APP_NO_MCP=1 sindri ' | sed -E 's#^.*NO_MCP=1 sindri ##' | awk '{print $1" "$2}' | tr '\n' '|')"
want="onyx status|profile init|profile validate|--headless |status --json|code-index status|services run|context map|context map|context map|recipe list|item add|workflow start|"
if [ "$cmds" = "$want" ]; then ok "the commands come in the documented order"; else fail "the commands come in the documented order (got: $cmds)"; fi
OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$REPO" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
expect "the dry run prints the approval commands for the owner and keeps going" 0 "profile approve <hash>" "recipe approve-all" "workflow start workflow.scoping"
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
OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_PORT=7410 FIRST_RUN_REPO_PATH="$REPO" "$SCRIPT" up --dry-run 2>&1)"; CODE=$?
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
  OUT="$(AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$1" SINDRI="$T/stub-sindri" "$SCRIPT" up --no-embeddings --no-graph --no-ui 2>&1)"; CODE=$?
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
    *"tools ok; agentic-workflow at $REPO"*|*"profile approve"*) ok "FIRST_RUN_REPO_PATH still overrides the default repo" ;;
    *) fail "FIRST_RUN_REPO_PATH still overrides the default repo"; printf '%s\n' "$OUT" | sed 's/^/       | /' ;;
  esac
  rm -rf "$STATE/sindri-app-firstrun"
else
  echo "  skip the checkout preflight cases: jq isn't installed"
fi

echo "context-first-run: stop at approval, then a whole run"
# jq is a real dependency of the script; the real-run cases need it.
if ! command -v jq >/dev/null 2>&1; then
  echo "  skip the real-run cases: jq isn't installed"
else
  mkdir -p "$STATE/cache/bin/temporal-1.9.1"
  run up --no-embeddings --no-graph --no-ui
  expect "up writes the profile, then stops for approval with the exact command (exit 3)" 3 "profile approve abababab" "approve profile ababab" "continue"
  lacks "nothing past the approval stop ran" "--headless"
  if [ -f "$STATE/sindri-app-firstrun/.first-run-scratch" ]; then ok "the home carries the scratch marker"; else fail "the home carries the scratch marker"; fi
  P="$STATE/sindri-app-firstrun/profile/profile.yaml"
  if grep -q "path: $REPO" "$P" && grep -q 'context-docs-refresh' "$P" && grep -q 'enabled: false' "$P" && grep -q '^context:' "$P" && grep -q '^index:' "$P"; then ok "the profile onboards the repo with services, index and context"; else fail "the profile onboards the repo with services, index and context"; sed 's/^/       | /' "$P"; fi
  if logged "home=sindri-app-firstrun port=7481" && ! logged "port=7480" && ! logged "port=7410"; then ok "every call used the scratch home and port 7481"; else fail "every call used the scratch home and port 7481"; fi
  before="$(wc -l < "$P")"
  run up --no-embeddings --no-graph --no-ui
  expect "a second up before approval stops again (exit 3)" 3 "profile approve"
  if [ "$(wc -l < "$P")" = "$before" ]; then ok "the profile isn't written twice"; else fail "the profile isn't written twice"; fi

  mkdir -p "$STATE/sindri-app-firstrun/stub"; : > "$STATE/sindri-app-firstrun/stub/approved"
  run continue --no-embeddings --no-graph --no-ui
  expect "after approval, continue serves, indexes, maps, then stops for the recipes (exit 3)" 3 "recipe approve-all"
  if logged '--headless' && logged 'services run code-index-main-move' && logged 'context map refresh'; then ok "serve, the index build and the map refresh ran"; else fail "serve, the index build and the map refresh ran"; fi
  if [ -s "$STUB_LOG.mcp" ] && [ "$(sort -u "$STUB_LOG.mcp")" = 1 ]; then ok "every sindri call runs with AW_SINDRI_APP_NO_MCP=1 (no MCP registration in ~/.claude.json)"; else fail "every sindri call runs with AW_SINDRI_APP_NO_MCP=1"; sort "$STUB_LOG.mcp" | uniq -c | sed 's/^/       | /'; fi
  if ! logged 'item add'; then ok "no item was added before the recipes were approved"; else fail "no item was added before the recipes were approved"; fi

  : > "$STATE/sindri-app-firstrun/stub/recipes"
  : > "$STUB_LOG"
  run continue --no-embeddings --no-graph --no-ui
  expect "after the recipes are approved, continue adds the item and starts scoping (exit 0)" 0 "ITM-0A1B2C is being scoped"
  if ! logged 'services run' && ! logged 'context map refresh' && ! logged '--headless'; then ok "a finished step is skipped on the second pass"; else fail "a finished step is skipped on the second pass"; sed 's/^/       | /' "$STUB_LOG"; fi
  if logged 'workflow start workflow.scoping --item ITM-0A1B2C --fact fact:scopeInput='; then ok "scoping started on the new item with the brief as its input"; else fail "scoping started on the new item with the brief as its input"; fi
  if grep -q '"path":"docs/sindri/README.md"' "$STATE/sindri-app-firstrun/first-run/scope-input.json"; then ok "the scope input names the repo's brief"; else fail "the scope input names the repo's brief"; fi

  # step 6's waiting paths: forget the map, script the states the status answers, and run again
  step6() { # step6 <state>… ; leaves OUT and CODE of a continue with a 1 s poll and a 3 s timeout
    rm -f "$STATE/sindri-app-firstrun/stub/mapped" "$STATE/sindri-app-firstrun/stub/mapstate.bak"
    : > "$STATE/sindri-app-firstrun/stub/mapstate"
    local st; for st in "$@"; do echo "$st" >> "$STATE/sindri-app-firstrun/stub/mapstate"; done
    : > "$STUB_LOG"
    OUT="$(FIRST_RUN_MAP_POLL_SECONDS=1 FIRST_RUN_MAP_TIMEOUT=3 AW_STATE_DIR="$STATE" FIRST_RUN_REPO_PATH="$REPO" SINDRI="$T/stub-sindri" "$SCRIPT" continue --no-embeddings --no-graph --no-ui 2>&1)"; CODE=$?
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
  rm -f "$STATE/sindri-app-firstrun/stub/mapstate"; : > "$STATE/sindri-app-firstrun/stub/mapped"

  run status
  expect "status reads the plane, the index, the map and the item" 0 "ITM-0A1B2C/workflow.scoping/1 running"
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
