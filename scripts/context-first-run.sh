#!/usr/bin/env bash
# scripts/context-first-run.sh — the first real context run of the C# Sindri on THIS repo, as one command (owner's call).
# Walk-through and what to check at each step: docs/sindri/first-context-run.md.
#
#   scripts/context-first-run.sh up [--dry-run] [--no-ui] [--no-embeddings] [--no-graph] [--stop-after N]
#   scripts/context-first-run.sh up --time [--dry-run]      time home, profile, serve, app and rows; never waits on the index or the map
#   scripts/context-first-run.sh continue [...]     the same as up: every step checks its outcome first and skips what is done
#   scripts/context-first-run.sh status [--dry-run]
#   scripts/context-first-run.sh down [--dry-run]
#
# up   0. Preflight: git, jq, ollama (server and the pinned embedding model), the code-index graphify, a sandbox for it (bwrap
#         on Linux), the pinned temporal binary, and that the repo's checkout is clean and its HEAD is contained in origin/main
#         or main (a detached worktree of origin/main is the intended place; any dirty tree is refused). Missing tools are printed with the
#         install command; nothing is installed.
#      1. Scratch home: AW_SINDRI_HOME=$AW_STATE_DIR/sindri-app-firstrun (0700, marker .first-run-scratch), port 7481.
#      2. Profile: `sindri profile init`, then this repo as the one repo, the context-docs-refresh service, and the
#         `index:` and `context:` sections. Written only when the home has no profile yet.
#      3. The profile is live as written (edits apply on save); there is nothing to approve and this step does not stop.
#      4. Serve: `sindri --headless` (the control plane and its Temporal dev server), then `sindri status --json`.
#      5. First code index build: `sindri services run code-index-main-move` (there is no build verb: the service starts the
#         BuildCodeIndex job), then `sindri code-index status --repo <repo> --json` until the repo is ok.
#      6. Context map: `sindri context map refresh` (builds, cleans and publishes the map), then `sindri context map status`.
#         When the map service already started its own refresh (SND-JOB-409), it follows that job with `sindri jobs show <id>` first.
#      7. The Sindri app (sindri-ui) on the scratch home.
# status  The plane, the index and the map, read only.
# down    `sindri stop`, closes the app this script opened, and deletes the scratch home. Nothing else is touched.
#
# --stop-after N ends `up` with exit 0 once step N (0 to 7) is done; scripts/model-limit-check.sh stops after the index build (5).
# --time (up only) times six steps with a wall clock and stops after the sixth: home (folder and marker exist), profile (init, repo,
#   `profile validate` exit 0), serve (control.json written and `status --json` answers), workflow (`wf create hello-sindri
#   --template hello-sindri` unless `wf show` answers, `wf activate`, `wf run`), app (sindri-ui with AW_UI_DRIVER=1 under Xvfb,
#   `aw-ui-driver ready`) and rows (at least one row under NeedsYouRows, RunningRows or QueuedRows on Home: the item "Hello from
#   Sindri" that hello-sindri's last node adds). It prints
#   `first-run <step> <seconds>` per step and `first-run total <seconds>`, and writes the same to <home>/first-run.json
#   ({"steps":{...},"total":N}). The preflight skips ollama and graphify, and the index and the map are never waited on. A rows wait
#   that passes FIRST_RUN_ROWS_TIMEOUT prints `first-run rows timeout`, still prints the steps that finished and exits 1. Not with
#   --stop-after or --no-ui. The app is stopped at the end (the plane stays up for `down`).
# --dry-run prints every command it would run (and the checks it would make) and runs nothing, writes nothing: not the home,
# not a profile, no build. It always shows the whole path as if nothing were done yet.
#
# Environment (all optional; scripts/context-first-run.sh never reads or uses the real home, $AW_STATE_DIR/sindri-app):
#   AW_STATE_DIR              default ~/.agentic-workflow
#   FIRST_RUN_HOME            default $AW_STATE_DIR/sindri-app-firstrun; its name must start with sindri-app-firstrun
#   FIRST_RUN_PORT            default 7481 (owner-check.sh uses 7480, the real instance 7410)
#   FIRST_RUN_REPO_PATH       the checkout to index; default the checkout this script runs from (the shared main checkout is never derived)
#   FIRST_RUN_REPO_NAME       the repo's name in the profile, items and code index; default the basename of its origin URL (.git stripped), else of the path
#   FIRST_RUN_INDEX_TIMEOUT   seconds to wait for the first build; default 1800
#   FIRST_RUN_MAP_POLL_SECONDS  seconds between map status reads while it is held; default 10
#   FIRST_RUN_MAP_TIMEOUT     seconds to wait for a held map; default 1020
#   FIRST_RUN_CONTEXT_EXTRA   YAML lines, indented two spaces, appended to the profile's context: section when it is written; default none
#   SINDRI                    the sindri command line, split on spaces; default `sindri` on the PATH (the C# build
#                             scripts/install-cs.sh links; run it first)
#   FIRST_RUN_ROWS_TIMEOUT    --time: seconds to wait for a Home row; default 180
#   FIRST_RUN_UI_DRIVER       --time: the aw-ui-driver command line, split on spaces; default scripts/aw-ui-driver.sh
#   FIRST_RUN_XVFB            --time: 0 uses the DISPLAY you have instead of starting Xvfb (tests/ui-checks/xvfb-lib.sh); default 1
#   AW_SINDRI_UI              the sindri-ui binary; default $AW_STATE_DIR/bin/cs/current/sindri-ui
# It exports AW_SINDRI_APP_NO_MCP=1 for every Sindri it starts, so the scratch plane never registers a 'sindri' MCP server in ~/.claude.json.
#
# Exit: 0 ok; 1 a step failed (the message names it); 2 usage or a refused setup.
# Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

die()   { echo "context-first-run: $*" >&2; exit 2; }
broke() { echo "context-first-run: $*" >&2; exit 1; }
usage() { sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//' >&2; exit 2; }

[ "$#" -ge 1 ] || usage
action="$1"; shift
case "$action" in up|continue|status|down) ;; -h|--help) usage ;; *) usage ;; esac
[ "$action" = continue ] && action=up

dry=0 no_ui=0 no_embeddings=0 no_graph=0 stop_after= time_mode=0
while [ "$#" -gt 0 ]; do
  case "$1" in
    --dry-run) dry=1 ;;
    --no-ui) [ "$action" = up ] || usage; no_ui=1 ;;
    --no-embeddings) [ "$action" = up ] || usage; no_embeddings=1 ;;
    --no-graph) [ "$action" = up ] || usage; no_graph=1 ;;
    --time) [ "$action" = up ] || usage; time_mode=1 ;;
    --stop-after)
      [ "$action" = up ] && [ "$#" -ge 2 ] || usage
      case "$2" in [0-7]) stop_after="$2" ;; *) die "--stop-after takes a step number 0 to 7 (got '$2')" ;; esac
      shift ;;
    *) echo "context-first-run: unknown option $1" >&2; usage ;;
  esac
  shift
done

if [ "$time_mode" = 1 ]; then
  [ -z "$stop_after" ] || die "--time stops after its own fifth step; it takes no --stop-after"
  [ "$no_ui" = 0 ] || die "--time starts the app; it takes no --no-ui"
  # Nothing waits on the index, so the layers that need ollama and graphify stay off.
  no_embeddings=1 no_graph=1
fi

# The tmux the team shares must never be reached from here (AGENTS memory: lanes inherit TMUX).
unset TMUX TMUX_PANE
# The real profile must never steer the scratch instance (owner-check.sh does the same).
unset AW_SINDRI_PROFILE
# Every Sindri this script starts is throwaway: sindri must not register it as the 'sindri' MCP server in the real ~/.claude.json.
export AW_SINDRI_APP_NO_MCP=1

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
home="${FIRST_RUN_HOME:-$state/sindri-app-firstrun}"
port="${FIRST_RUN_PORT:-7481}"
timeout_s="${FIRST_RUN_INDEX_TIMEOUT:-1800}"
map_poll="${FIRST_RUN_MAP_POLL_SECONDS:-10}"
map_timeout="${FIRST_RUN_MAP_TIMEOUT:-1020}"
sindri_cmd="${SINDRI:-sindri}"
read -r -a SX <<<"$sindri_cmd"
real_home="$state/sindri-app"
self="scripts/context-first-run.sh"

repo_path="${FIRST_RUN_REPO_PATH:-}"
[ -n "$repo_path" ] || repo_path="$root"
repo="${FIRST_RUN_REPO_NAME:-}"
if [ -z "$repo" ]; then
  origin="$(git -C "$repo_path" remote get-url origin 2>/dev/null || true)"
  origin="${origin%/}"
  origin="${origin%.git}"
  origin="${origin%/}"
  repo="${origin##*[/:]}"
  [ -n "$repo" ] || repo="$(basename "$repo_path")"
fi
# The shape of a profile repo name (RepoName): 1 to 100 of letters, digits, '.', '_', '-', starting with a letter or digit.
case "$repo" in
  '' | [!A-Za-z0-9]* | *[!A-Za-z0-9._-]*) die "the repo name '$repo' is not a profile repo name (letters, digits, '.', '_', '-', starting with a letter or digit); set FIRST_RUN_REPO_NAME" ;;
esac
[ "${#repo}" -le 100 ] || die "the repo name '$repo' is longer than 100 characters; set FIRST_RUN_REPO_NAME"

# ---- the guard: this script only ever works in a scratch home of its own ----
guard() {
  case "$home" in /*) ;; *) die "FIRST_RUN_HOME must be an absolute path (got '$home')" ;; esac
  case "$(basename "$home")" in
    sindri-app-firstrun | sindri-app-firstrun-*) ;;
    *) die "refusing $home: a first-run home is named sindri-app-firstrun or sindri-app-firstrun-<name> (this looks like a real home)" ;;
  esac
  [ "$home" != "$real_home" ] || die "refusing $home: that is the real Sindri home"
  [ ! -L "$home" ] || die "refusing $home: it is a link"
  if [ -e "$home" ] && [ ! -f "$home/.first-run-scratch" ] && [ -n "$(ls -A "$home" 2>/dev/null)" ]; then
    die "refusing $home: it exists, holds files, and has no .first-run-scratch marker (this looks like a real home)"
  fi
  case "$port" in '' | *[!0-9]*) die "FIRST_RUN_PORT must be a number (got '$port')" ;; esac
  [ "$port" != 7410 ] || die "refusing port 7410: that is the real instance's port"
}

# ---- running things ----
step()  { echo; echo "== $1"; }
note()  { echo "-- $*"; }
shown() { printf '%s' "AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port AW_SINDRI_APP_NO_MCP=1 $sindri_cmd"; }
# words <args>: the arguments as a shell would need them typed (an argument with a space is quoted).
words() {
  local a out=""
  for a in "$@"; do
    case "$a" in *[[:space:]]*) a="\"$a\"" ;; esac
    out="$out${out:+ }$a"
  done
  printf '%s' "$out"
}
# sx_run <args>: sindri in the scratch home and port. In a dry run it only prints the command.
sx() {
  if [ "$dry" = 1 ]; then echo "+ $(shown) $(words "$@")"; return 0; fi
  AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port" "${SX[@]}" "$@"
}
# sx_out <args>: like sx, stdout captured in $OUT and the exit code in $RC; stderr passes through.
sx_out() {
  if [ "$dry" = 1 ]; then echo "+ $(shown) $(words "$@")"; OUT=""; RC=0; return 0; fi
  OUT="$(AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port" "${SX[@]}" "$@")"; RC=$?
}
# sx_quiet <args>: sindri like sx, for a read whose failure is an answer (never printed in a dry run).
sx_quiet() { AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port" "${SX[@]}" "$@"; }
# plain <command…>: any other command, printed in a dry run.
plain() {
  if [ "$dry" = 1 ]; then echo "+ $*"; return 0; fi
  "$@"
}
# check <text>: what a dry run says it would verify.
check() { [ "$dry" != 1 ] || echo "  check: $*"; }

listening() { (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; }

# ---- 0. preflight ----
MISSING=""
miss() { MISSING="$MISSING  - $1"$'\n'"      install: $2"$'\n'; }

preflight() {
  step "0. preflight"
  if [ "$dry" = 1 ]; then
    echo "  check: git, jq on PATH"
    echo "  check: $repo_path is a git checkout with a clean tree whose HEAD is contained in origin/main or main (the code index never builds from another branch)"
    if [ "$time_mode" = 1 ]; then
      echo "  skip: ollama and graphify (--time never waits on the index)"
    else
      echo "  check: ollama on PATH, answering on 127.0.0.1:11434, with the pinned model from context/code-index.lock"
      echo "  check: the code-index graphify under $state/cache/code-index/graphify-*, and bwrap (Linux) for its sandbox"
    fi
    echo "  check: the pinned temporal binary under $state/cache/bin/temporal-*"
    echo "  check: nothing but this script's own plane listens on port $port"
    return 0
  fi
  command -v git >/dev/null 2>&1 || miss "git" "your package manager"
  command -v jq >/dev/null 2>&1 || miss "jq" "apt-get install jq, or brew install jq"
  if [ -d "$repo_path/.git" ] || git -C "$repo_path" rev-parse --git-dir >/dev/null 2>&1; then
    fix="git worktree add --detach <path> origin/main, then set FIRST_RUN_REPO_PATH=<path> or run $self from there"
    base=""
    for ref in refs/remotes/origin/HEAD refs/heads/main refs/heads/master; do
      if git -C "$repo_path" rev-parse --verify -q "$ref" >/dev/null 2>&1; then base="$ref"; break; fi
    done
    if [ -z "$base" ]; then
      miss "$repo_path has none of origin/HEAD, main or master to compare HEAD with" "$fix"
    elif ! git -C "$repo_path" merge-base --is-ancestor HEAD "$base" >/dev/null 2>&1; then
      miss "$repo_path: HEAD is not contained in ${base#refs/*/} ($base): the code index never builds from another branch" "$fix"
    fi
    gdir="$(git -C "$repo_path" rev-parse --path-format=absolute --git-dir 2>/dev/null || true)"
    gcommon="$(git -C "$repo_path" rev-parse --path-format=absolute --git-common-dir 2>/dev/null || true)"
    if [ -n "$(git -C "$repo_path" status --porcelain 2>/dev/null)" ]; then
      if [ "$gdir" = "$gcommon" ]; then
        miss "$repo_path is a shared main checkout with uncommitted or untracked changes; index a clean worktree instead" "$fix"
      else
        miss "$repo_path has uncommitted or untracked changes; the index must build from a clean tree" "$fix"
      fi
    fi
  else
    miss "$repo_path is not a git checkout" "set FIRST_RUN_REPO_PATH to the repo's checkout"
  fi
  if [ "$no_embeddings" = 0 ]; then
    model="$(awk '$1 == "ollama-model" { print $2; exit }' "$root/context/code-index.lock" 2>/dev/null)"
    model="${model:-nomic-embed-text:latest}"
    if ! command -v ollama >/dev/null 2>&1; then
      miss "ollama" "scripts/install-deps.sh (or https://ollama.com/download), then: ollama pull $model"
    elif ! curl -fsS --max-time 5 http://127.0.0.1:11434/api/version >/dev/null 2>&1; then
      miss "the ollama server isn't answering on 127.0.0.1:11434" "ollama serve"
    elif ! ollama list 2>/dev/null | grep -q "^${model%%:*}"; then
      miss "the embedding model $model isn't pulled" "ollama pull $model"
    fi
  fi
  if [ "$no_graph" = 0 ]; then
    # shellcheck disable=SC2231 # the glob is the check
    ls -d "$state"/cache/code-index/graphify-* >/dev/null 2>&1 || miss "the code-index graphify environment" "scripts/install-deps.sh"
    if [ "$(uname -s)" = Linux ] && ! { [ -x /usr/bin/bwrap ] || [ -x /bin/bwrap ]; }; then
      miss "bubblewrap (graphify never runs unsandboxed)" "scripts/install-deps.sh (sudo apt-get install bubblewrap)"
    fi
  fi
  # shellcheck disable=SC2231
  ls -d "$state"/cache/bin/temporal-* >/dev/null 2>&1 || miss "the pinned temporal binary" "scripts/binaries-lock.sh fetch temporal (scripts/install-cs.sh also fetches it)"
  if listening && [ ! -f "$home/control.json" ]; then
    miss "port $port is in use by something that is not this script's plane" "stop it, or set FIRST_RUN_PORT"
  fi
  if [ -n "$MISSING" ]; then
    printf 'Missing before the first run:\n%s' "$MISSING" >&2
    broke "step 0 (preflight): install the above, then run $self up again (nothing was installed)"
  fi
  echo "  tools ok; $repo at $repo_path is clean and contained in main"
}

# ---- 1. home ----
make_home() {
  step "1. scratch home $home (port $port)"
  if [ "$dry" = 1 ]; then
    echo "+ mkdir -p $home/profile && chmod 700 $home && touch $home/.first-run-scratch"
    return 0
  fi
  (umask 077 && mkdir -p "$home/profile" "$home/first-run" "$home/logs") || broke "step 1 (home): can't create $home"
  chmod 700 "$home"
  : >"$home/.first-run-scratch" || broke "step 1 (home): can't mark $home"
}

# ---- 2. profile ----
profile_file="$home/profile/profile.yaml"
write_profile() {
  step "2. profile that onboards $repo"
  if [ "$dry" = 1 ]; then
    sx profile init
    echo "+ append to $profile_file:"
    profile_tail | sed 's/^/    | /'
    sx profile validate --json
    check "profile validate gives a hash (exit 0 valid, 2 invalid)"
    return 0
  fi
  if [ -f "$profile_file" ]; then
    note "a profile exists; not rewritten ($profile_file)"
  else
    sx profile init || broke "step 2 (profile): 'profile init' failed"
    profile_tail >>"$profile_file" || broke "step 2 (profile): can't append to $profile_file"
  fi
  sx_out profile validate --json
  [ "$RC" -le 1 ] || broke "step 2 (profile): the profile is invalid (see above); fix $profile_file, then run $self up again"
  HASH="$(printf '%s' "$OUT" | jq -r '.hash // empty' 2>/dev/null)"
  [ -n "$HASH" ] || broke "step 2 (profile): 'profile validate --json' gave no hash"
  PROFILE_RC="$RC"
}

profile_tail() {
  cat <<EOF
repos:
  - name: $repo
    path: $repo_path
services:
  context-docs-refresh: { every: 5m }
index:
  embeddings: { enabled: $([ "$no_embeddings" = 1 ] && echo false || echo true) }
  graph: { enabled: $([ "$no_graph" = 1 ] && echo false || echo true) }
context:
  publish: { waitForIndexMinutes: 15 }
EOF
  [ -z "${FIRST_RUN_CONTEXT_EXTRA:-}" ] || printf '%s\n' "$FIRST_RUN_CONTEXT_EXTRA"
}

# ---- 3. profile applies on save ----
approval_gate() {
  step "3. profile (edits apply on save; nothing to approve)"
  if [ "$dry" = 1 ]; then
    echo "+ nothing to approve: the profile written in step 2 is the live one"
    return 0
  fi
  note "the profile is live ($HASH)"
}

# ---- 4. serve ----
start_serve() {
  step "4. start the control plane"
  if [ "$dry" = 1 ]; then
    sx --headless
    sx status --json
    check "status --json has running true (its exit code is 1 while health isn't healthy; that's fine)"
    return 0
  fi
  sx_out status --json
  if [ "$(printf '%s' "$OUT" | jq -r '.running // false' 2>/dev/null)" = true ]; then
    note "the control plane is already running"
  else
    sx --headless; rc=$?
    [ "$rc" -le 1 ] || broke "step 4 (serve): 'sindri --headless' exited $rc; see $home/logs/serve.log"
    sx_out status --json
  fi
  [ "$(printf '%s' "$OUT" | jq -r '.running // false' 2>/dev/null)" = true ] || broke "step 4 (serve): the control plane isn't running; see $home/logs/serve.log"
  echo "  running on port $port"
}

# ---- 5. first code index build ----
build_index() {
  step "5. first code index build for $repo"
  if [ "$dry" = 1 ]; then
    sx code-index status --repo "$repo" --json
    check "skip the build when that exits 0 (the repo's index is ok)"
    sx services run code-index-main-move
    check "the service starts the BuildCodeIndex job and the verb follows it; exit 0 succeeded"
    echo "+ repeat '$(shown) code-index status --repo $repo --json' every 10 s for up to $timeout_s s until it exits 0"
    return 0
  fi
  sx_out code-index status --repo "$repo" --json
  if [ "$RC" = 0 ]; then note "the index is already ok"; return 0; fi
  sx services run code-index-main-move || broke "step 5 (index): 'services run code-index-main-move' failed; see 'sindri jobs' and 'sindri doctor' in $home"
  local waited=0
  while :; do
    sx_out code-index status --repo "$repo" --json
    [ "$RC" != 0 ] || break
    [ "$waited" -lt "$timeout_s" ] || broke "step 5 (index): $repo's index wasn't ok after ${timeout_s}s; run: $(shown) code-index status --repo $repo   and   $(shown) jobs"
    sleep 10; waited=$((waited + 10))
    note "waiting for the index (${waited}s): $(printf '%s' "$OUT" | jq -r '[.repos[]? | .state] | join(",")' 2>/dev/null)"
  done
  echo "  $repo's index is ok"
}

# ---- 6. context map ----
# 'context map status --json' is the map's own status with the publish document's fields beside it (ContextMapText.WithPublish):
# .state is held, overdue, published, published-without-waiting, published-stale or published-by-hand; .publishedVersion is the
# published map's number (.version is the map's own version record). No map yet is an error answer (exit non-zero).
map_state() { printf '%s' "$OUT" | jq -r '.state // empty' 2>/dev/null; }
map_ok() { case "$(map_state)" in published | published-without-waiting | published-by-hand) return 0 ;; *) return 1 ;; esac; }
# refresh_map: 'context map refresh'. When the map service already started its own refresh (#3010: it starts one as soon as the repo is
# in the profile, before the code index is ok), the verb answers SND-JOB-409 naming that job: follow it until it ends instead of failing.
refresh_map() {
  local err="$home/.first-run-refresh.err" rc job waited=0 st
  sx context map refresh 2>"$err"; rc=$?
  cat "$err" >&2
  [ "$rc" != 0 ] || return 0
  grep -q 'SND-JOB-409' "$err" || broke "step 6 (map): 'context map refresh' failed; see $(shown) jobs"
  job="$(grep -o 'JOB-[0-9a-fA-F]\{6,\}' "$err" | head -1)"
  [ -n "$job" ] || broke "step 6 (map): 'context map refresh' answered SND-JOB-409 without a job id; see $(shown) jobs"
  note "the map service is already building the map ($job); following it"
  while :; do
    sx_out jobs show "$job" --json
    [ "$RC" = 0 ] || broke "step 6 (map): 'jobs show $job' exited $RC; run: $(shown) jobs show $job"
    st="$(printf '%s' "$OUT" | jq -r '.state // empty' 2>/dev/null)"
    case "$st" in
      running) ;;
      # waiting: parked on a signal, not ended (a map refresh parks on a model limit, #2112); keep following it
      waiting) note "$job is waiting (parked, e.g. on a model limit); to resume it: $(shown) jobs resume $job" ;;
      succeeded) note "$job succeeded"; return 0 ;;
      *) broke "step 6 (map): the map service's $job ended '${st:-unknown}'; run: $(shown) jobs show $job" ;;
    esac
    [ "$waited" -lt "$map_timeout" ] || broke "step 6 (map): $job was still running after ${waited}s; run: $(shown) jobs show $job"
    sleep "$map_poll"; waited=$((waited + map_poll))
    note "waiting for $job (${waited}s)"
  done
}
publish_map() {
  step "6. publish the context map"
  if [ "$dry" = 1 ]; then
    sx context map status --json
    check "skip when .state is published, published-without-waiting or published-by-hand (no map is a non-zero exit)"
    sx context map refresh
    check "builds a draft, cleans the review queue and promotes it as you (no typed phrase); with the index built it is not held; exit 0"
    check "on SND-JOB-409 (the map service already runs a refresh) follow the named job with 'jobs show <id> --json' every $map_poll s until it ends; fail unless it succeeded"
    sx context map status --json
    check "wait up to $map_timeout s (every $map_poll s) while .state is held or overdue (waitForIndexMinutes 15 plus two ticks); fail on published-stale or anything else"
    return 0
  fi
  sx_out context map status --json
  if [ "$RC" = 0 ] && map_ok; then note "a map is published ($(map_state))"; return 0; fi
  refresh_map
  local waited=0
  while :; do
    sx_out context map status --json
    [ "$RC" = 0 ] || broke "step 6 (map): 'context map status' exited $RC after the refresh; run: $(shown) context map status"
    map_ok && break
    case "$(map_state)" in
      held | overdue) ;;
      *) broke "step 6 (map): the map's state is '$(map_state)', not published; run: $(shown) context map status" ;;
    esac
    [ "$waited" -lt "$map_timeout" ] || broke "step 6 (map): the map was still $(map_state) after ${waited}s; run: $(shown) context map status   (to publish by hand: context map promote)"
    sleep "$map_poll"; waited=$((waited + map_poll))
    note "the map is $(map_state), waiting for the code index (${waited}s)"
  done
  echo "  map published (v$(printf '%s' "$OUT" | jq -r '.publishedVersion // "?"' 2>/dev/null), $(map_state))"
}

# ---- 7. the app ----
find_ui() {
  if [ -n "${AW_SINDRI_UI:-}" ]; then printf '%s\n' "$AW_SINDRI_UI"; return; fi
  printf '%s\n' "$state/bin/cs/current/sindri-ui"
}
open_ui() {
  step "7. open the Sindri app on the scratch home (Context page)"
  if [ "$no_ui" = 1 ]; then note "skipped (--no-ui)"; return 0; fi
  if [ "$dry" = 1 ]; then
    echo "+ AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port AW_SINDRI_APP_NO_MCP=1 <sindri-ui: \$AW_SINDRI_UI, else $state/bin/cs/current/sindri-ui> > $home/logs/ui.log 2>&1 &"
    check "the process is alive 2 s later; its pid is kept in $home/first-run/ui.pid"
    return 0
  fi
  if [ -f "$home/first-run/ui.pid" ] && kill -0 "$(cat "$home/first-run/ui.pid")" 2>/dev/null; then note "the app is already open"; return 0; fi
  local ui; ui="$(find_ui)"
  [ -n "$ui" ] && [ -x "$ui" ] || broke "step 7 (app): no sindri-ui found; build it with scripts/install-cs.sh, or set AW_SINDRI_UI, or use --no-ui"
  AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port" nohup "$ui" >"$home/logs/ui.log" 2>&1 &
  echo $! >"$home/first-run/ui.pid"
  sleep 2
  kill -0 "$(cat "$home/first-run/ui.pid")" 2>/dev/null || broke "step 7 (app): sindri-ui exited at once; see $home/logs/ui.log"
  echo "  open (pid $(cat "$home/first-run/ui.pid")); click Context in the rail"
}

# ---- --time: the first run on a clock ----
rows_timeout="${FIRST_RUN_ROWS_TIMEOUT:-180}"
read -r -a UIDRV <<<"${FIRST_RUN_UI_DRIVER:-$root/scripts/aw-ui-driver.sh}"
TIMES=""   # "<step> <seconds>" lines, in order
T_TOTAL=0

# now_ms: milliseconds since the epoch (date +%s%3N where it works, else python3: macOS date has no %N).
now_ms() {
  local n; n="$(date +%s%3N 2>/dev/null)"
  case "$n" in '' | *[!0-9]*) n="$(python3 -c 'import time; print(int(time.time()*1000))')" ;; esac
  printf '%s' "$n"
}
# secs <from_ms> <to_ms>: the span in seconds with one decimal.
secs() { awk -v a="$1" -v b="$2" 'BEGIN { printf "%.1f", (b - a) / 1000 }'; }
# lap <step>: the step ended now; record it (T_LAST is the end of the step before).
lap() {
  local t; t="$(now_ms)"
  TIMES="$TIMES$1 $(secs "$T_LAST" "$t")"$'\n'
  T_LAST="$t"
}
# report <ok>: the step lines, the total, and first-run.json in the home (when it exists). ok is true or false.
report() {
  local step val steps_json="" total
  total="$(secs "$T_START" "$(now_ms)")"
  while read -r step val; do
    [ -n "$step" ] || continue
    echo "first-run $step $val"
    steps_json="$steps_json${steps_json:+,}\"$step\":$val"
  done <<<"$TIMES"
  echo "first-run total $total"
  if [ -d "$home" ]; then
    printf '{"ok":%s,"steps":{%s},"total":%s}\n' "$1" "$steps_json" "$total" >"$home/first-run.json"
  fi
}

# ui_cmd: the driver, pointed at the app this script started.
ui_drv() { AW_UI_DRIVER_DIR="$home/first-run/driver" "${UIDRV[@]}" "$@"; }

time_serve_files() {
  # serve ends when control.json is written and status answers (start_serve checked the answer).
  local i
  for i in $(seq 1 50); do [ -f "$home/control.json" ] && return 0; sleep 0.2; done
  broke "step 4 (serve): the control plane answers but $home/control.json was never written"
}

# time_workflow: make (unless it exists), activate and run hello-sindri, so Home gets an item. A failed verb ends the run with its stderr.
time_workflow() {
  step "workflow: run hello-sindri so Home has a row"
  local err="$home/.first-run-wf.err" verb
  if sx_quiet wf show hello-sindri >/dev/null 2>&1; then
    note "hello-sindri already exists; not created"
  else
    sx wf create hello-sindri --template hello-sindri 2>"$err" || broke "workflow: 'wf create hello-sindri' failed: $(cat "$err")"
  fi
  sx wf activate hello-sindri 2>"$err" || broke "workflow: 'wf activate hello-sindri' failed: $(cat "$err")"
  sx wf run hello-sindri 2>"$err" || broke "workflow: 'wf run hello-sindri' failed: $(cat "$err")"
}

time_start_app() {
  step "app: sindri-ui with the UI driver on the scratch home"
  if [ "$FIRST_RUN_XVFB_ON" = 1 ]; then
    # shellcheck source=../tests/ui-checks/xvfb-lib.sh
    . "$root/tests/ui-checks/xvfb-lib.sh"
    xvfb_start 1600x1000x24 || broke "app: no Xvfb display (install xvfb, or set FIRST_RUN_XVFB=0 with a DISPLAY)"
  fi
  local ui; ui="$(find_ui)"
  [ -n "$ui" ] && [ -x "$ui" ] || broke "app: no sindri-ui found; build it with scripts/install-cs.sh, or set AW_SINDRI_UI"
  mkdir -p "$home/first-run/driver"
  AW_UI_DRIVER=1 AW_UI_DRIVER_DIR="$home/first-run/driver" AW_SINDRI_HOME="$home" AW_SINDRI_PORT="$port" \
    nohup "$ui" >"$home/logs/ui.log" 2>&1 &
  echo $! >"$home/first-run/ui.pid"
  ui_drv ready --wait-ms 60000 >/dev/null 2>&1 || broke "app: the UI driver never answered (see $home/logs/ui.log)"
  echo "  the driver answers (pid $(cat "$home/first-run/ui.pid"))"
}

# rows_seen: success when any of the three Home lists has a descendant that shows text. wait-for takes one selector and has no
# child count, so this reads each list's tree and counts the nodes that show text.
rows_seen() {
  local name n
  for name in NeedsYouRows RunningRows QueuedRows; do
    n="$(ui_drv tree --name "$name" --depth 8 2>/dev/null | jq '[.. | objects | select((.text // "") != "")] | length' 2>/dev/null)"
    case "$n" in '' | *[!0-9]*) continue ;; esac
    if [ "$n" -ge 1 ]; then echo "  $name has a row"; return 0; fi
  done
  return 1
}

time_wait_rows() {
  step "rows: the first row on Home"
  local deadline; deadline=$(( $(now_ms) + rows_timeout * 1000 ))
  while ! rows_seen; do
    if [ "$(now_ms)" -ge "$deadline" ]; then
      stop_app_pid
      report false
      echo "first-run rows timeout"
      exit 1
    fi
    sleep 1
  done
}

stop_app_pid() {
  local p; p="$(cat "$home/first-run/ui.pid" 2>/dev/null)"
  [ -n "$p" ] && kill "$p" 2>/dev/null
  return 0
}

do_up_time() {
  FIRST_RUN_XVFB_ON="${FIRST_RUN_XVFB:-1}"
  if [ "$dry" = 1 ]; then
    preflight
    echo
    echo "== time: six steps on a wall clock (nothing waits on the index or the map)"
    make_home
    echo "  clock: home ends when the folder and marker exist"
    write_profile
    echo "  clock: profile ends when 'profile validate' exits 0"
    start_serve
    echo "  clock: serve ends when $home/control.json exists and 'status --json' answers"
    sx wf show hello-sindri
    check "skip the create when that answers"
    sx wf create hello-sindri --template hello-sindri
    sx wf activate hello-sindri
    sx wf run hello-sindri
    echo "  clock: workflow ends when 'wf run' exits 0"
    [ "$FIRST_RUN_XVFB_ON" != 1 ] || echo "+ Xvfb through tests/ui-checks/xvfb-lib.sh"
    echo "+ AW_UI_DRIVER=1 AW_UI_DRIVER_DIR=$home/first-run/driver AW_SINDRI_HOME=$home AW_SINDRI_PORT=$port <sindri-ui> &"
    echo "+ ${UIDRV[*]} ready --wait-ms 60000"
    echo "  clock: app ends when the driver answers"
    echo "+ poll '${UIDRV[*]} tree --name <list>' for NeedsYouRows, RunningRows, QueuedRows up to $rows_timeout s"
    echo "  clock: rows ends when one list has a row; on timeout print 'first-run rows timeout' and exit 1"
    echo "+ print 'first-run <step> <seconds>' per step and 'first-run total <seconds>'; write $home/first-run.json"
    return 0
  fi
  preflight
  T_START="$(now_ms)"; T_LAST="$T_START"
  make_home; lap home
  write_profile; lap profile
  approval_gate
  start_serve; time_serve_files; lap serve
  time_workflow; lap workflow
  time_start_app; lap app
  time_wait_rows; lap rows
  stop_app_pid
  echo
  report true
  echo "context-first-run up --time: done. The plane is still up; clean up with: $self down"
}

# ---- the verbs ----
# stopped <step>: with --stop-after <step>, end here (exit 0).
stopped() {
  [ "$stop_after" = "$1" ] || return 0
  echo
  echo "context-first-run up: stopped after step $1 (--stop-after $1)."
  exit 0
}

do_up() {
  guard
  if [ "$time_mode" = 1 ]; then do_up_time; return; fi
  preflight; stopped 0
  make_home; stopped 1
  write_profile; stopped 2
  approval_gate; stopped 3
  start_serve; stopped 4
  build_index; stopped 5
  publish_map; stopped 6
  open_ui; stopped 7
  echo
  echo "context-first-run up: done. Look at the app, then clean up with: $self down"
}

do_status() {
  guard
  step "status of the first run in $home"
  if [ "$dry" != 1 ] && [ ! -d "$home" ]; then echo "no first-run home at $home (nothing to report)"; exit 1; fi
  sx status
  sx code-index status --repo "$repo"
  sx context map status
}

do_down() {
  guard
  step "down: stop the plane, close the app, delete $home"
  if [ "$dry" != 1 ] && [ ! -d "$home" ]; then
    echo "nothing to clean: $home doesn't exist"
    exit 0
  fi
  if [ "$dry" = 1 ]; then
    sx stop
    echo "+ kill <pid in $home/first-run/ui.pid> (the app this script opened, if it still runs)"
    echo "+ rm -rf $home"
    return 0
  fi
  if [ -f "$home/control.json" ]; then sx stop || note "'sindri stop' failed; the plane may already be down"; fi
  if [ -f "$home/first-run/ui.pid" ]; then
    uipid="$(cat "$home/first-run/ui.pid")"
    kill -0 "$uipid" 2>/dev/null && kill "$uipid" 2>/dev/null
  fi
  if listening; then
    broke "something still listens on port $port after 'sindri stop'; the home was left in place. Stop that process by hand (lsof -nP -iTCP:$port -sTCP:LISTEN), then run $self down again"
  fi
  [ -f "$home/.first-run-scratch" ] || die "refusing to delete $home: no .first-run-scratch marker"
  rm -rf "$home"
  echo "deleted $home; stopped port $port. The installed build under $state/bin/cs and the pulled models stay (they aren't this script's)."
}

"do_$action"
