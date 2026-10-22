#!/usr/bin/env bash
# scripts/sindri-sandbox-e2e.sh — a sandboxed Sindri runs the factory line (workflow.line) end to end on a throwaway
# clone of this repo, building one real feature, and reports exactly where it stops and why.
#
#   sindri-sandbox-e2e.sh --feature <spec-file> [options]
#   --feature FILE   the brief or spec the line consumes (required): a file in --repo, or copied into the clone
#   --sindri CMD     the sindri command line (split on spaces), skipping the publish. A `dotnet …/sindri.dll` is
#                    replaced by the apphost beside it: the control plane starts itself by re-running
#                    Environment.ProcessPath, which under `dotnet x.dll` is the dotnet muxer (SND-CLI-002)
#   --repo PATH      the repo to clone (default: this one); the clone gets a new branch sandbox/e2e-<stamp>
#   --start RECIPE   the recipe to start (default workflow.line)
#   --title TEXT     the item's title (default: the feature file's first heading)
#   --stall SECONDS  no progress for this long is a stop (default 300)
#   --max-wait SEC   the whole follow gives up after this long (default 3600)
#   --auto-desk      answer a desk state with its default outcome (the first the status offers), and a budget park
#                    with a grant of 200k tokens (the most one grant may give), recording each
#   --map FILE       the context map to import (authored map format, docs/sindri/demo-handoff.md 5b); default: a small
#                    map generated for the feature (its doc, one surface, one code node)
#   --code FILE      the repo file the generated map says the feature changes (default: the first repo file the
#                    feature names in backticks, else docs/sindri/README.md)
#   --desk-max N     --auto-desk answers at most N desk states (default 25)
#   --agent ID       profile workflow.agent.target: agent Steps launch real sessions of that agent (default: none, so
#                    every Step is completed at the desk and the line stops at the first Step that needs a model)
#   --no-context     leave the context section out of the profile (scoping then parks at gather: no lenses)
#   --keep           keep the scratch dir (default: stop serve and delete it on exit)
#   --print-config   parse the arguments, run the guard, print the configuration and exit 0 (no scratch, no serve)
#
# The run: guarded scratch home (scripts/sindri-prove.sh's guard: AW_SINDRI_HOME under $TMPDIR) -> publish (unless
# --sindri) -> local clone on a new branch -> the file forge (AW_FORGE_ADAPTER=file, home in the scratch dir: effect.pr.*
# never reaches GitHub) -> profile (the clone, the effect allowlist), validate, approve as the human -> approve the
# shipped recipes, children first -> item add, workflow start -> follow until it stops -> one STOP block.
# Nothing here touches the owner's Sindri: AW_SINDRI_HOME and AW_STATE_DIR are inside the scratch dir, the port is 0.
#
# Exit: 0 the line ended resolved; 1 it stopped (the STOP block says where and why); 2 a harness error.
# Tests: bash scripts/tests/sindri-sandbox-e2e.test.sh
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROVE="$ROOT/scripts/sindri-prove.sh"

die() { echo "sindri-sandbox-e2e: $*" >&2; exit 2; }
say() { echo "sindri-sandbox-e2e: $*" >&2; }

FEATURE="" SINDRI_ARG="" REPO_SRC="$ROOT" START="workflow.line" TITLE="" STALL=300 MAX_WAIT=3600
AUTO_DESK=0 DESK_MAX=25 AGENT="" KEEP=0 PRINT_CONFIG=0 CONTEXT=1 MAP_FILE="" CODE_FILE=""

usage() { sed -n '2,/^# Tests:/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; }

parse_args() {
  while [ $# -gt 0 ]; do
    case "$1" in
      -h | --help) usage; exit 0 ;;
      --feature) [ $# -ge 2 ] || die "--feature needs a file"; FEATURE="$2"; shift 2 ;;
      --sindri) [ $# -ge 2 ] || die "--sindri needs a command"; SINDRI_ARG="$2"; shift 2 ;;
      --repo) [ $# -ge 2 ] || die "--repo needs a path"; REPO_SRC="$2"; shift 2 ;;
      --start) [ $# -ge 2 ] || die "--start needs a recipe id"; START="$2"; shift 2 ;;
      --title) [ $# -ge 2 ] || die "--title needs text"; TITLE="$2"; shift 2 ;;
      --stall) [ $# -ge 2 ] || die "--stall needs seconds"; STALL="$2"; shift 2 ;;
      --max-wait) [ $# -ge 2 ] || die "--max-wait needs seconds"; MAX_WAIT="$2"; shift 2 ;;
      --auto-desk) AUTO_DESK=1; shift ;;
      --map) [ $# -ge 2 ] || die "--map needs a file"; MAP_FILE="$2"; shift 2 ;;
      --code) [ $# -ge 2 ] || die "--code needs a repo path"; CODE_FILE="$2"; shift 2 ;;
      --desk-max) [ $# -ge 2 ] || die "--desk-max needs a number"; DESK_MAX="$2"; shift 2 ;;
      --agent) [ $# -ge 2 ] || die "--agent needs an agent id"; AGENT="$2"; shift 2 ;;
      --no-context) CONTEXT=0; shift ;;
      --keep) KEEP=1; shift ;;
      --print-config) PRINT_CONFIG=1; shift ;;
      *) die "unknown argument $1 (see --help)" ;;
    esac
  done
  [ -n "$FEATURE" ] || die "--feature <spec-file> is required"
  case "$DESK_MAX" in '' | *[!0-9]*) die "--desk-max takes a whole number (got $DESK_MAX)" ;; esac
  case "$STALL" in '' | *[!0-9]*) die "--stall takes whole seconds (got $STALL)" ;; esac
  case "$MAX_WAIT" in '' | *[!0-9]*) die "--max-wait takes whole seconds (got $MAX_WAIT)" ;; esac
  case "$START" in workflow.[a-z]*) ;; *) die "--start takes a recipe id like workflow.line (got $START)" ;; esac
  [ -f "$FEATURE" ] || die "the feature file $FEATURE doesn't exist"
  [ -z "$MAP_FILE" ] || [ -f "$MAP_FILE" ] || die "the map file $MAP_FILE doesn't exist"
  [ -d "$REPO_SRC/.git" ] || [ -f "$REPO_SRC/.git" ] || die "$REPO_SRC is not a git checkout"
}

# The scratch guard: the dir must resolve strictly inside realpath(${TMPDIR:-/tmp}) (sindri-prove.sh repeats it for AW_SINDRI_HOME).
guard_scratch() {
  local dir="$1" real tmp
  [ -d "$dir" ] || { echo "sindri-sandbox-e2e: scratch $dir is not a directory" >&2; return 2; }
  real="$(cd -P "$dir" && pwd -P)" || return 2
  tmp="$(cd -P "${TMPDIR:-/tmp}" && pwd -P)" || return 2
  case "$real" in "${tmp%/}"/?*) return 0 ;; esac
  echo "sindri-sandbox-e2e: scratch $dir is not strictly inside ${tmp} (refusing to run there)" >&2
  return 2
}

# --sindri: a `dotnet <dir>/sindri.dll` becomes the apphost beside it.
resolve_sindri() {
  local arg="$1" dll
  case "$arg" in
    dotnet\ *.dll)
      dll="${arg#dotnet }"
      if [ -x "${dll%.dll}" ]; then say "using the apphost ${dll%.dll} instead of 'dotnet $dll' (serve re-runs its own executable)"; arg="${dll%.dll}"; fi ;;
  esac
  printf '%s\n' "$arg"
}

# ---- the run -------------------------------------------------------------------------------------------------

SCRATCH="" SERVE_PID="" HOME_DIR="" REPO="" ITEM="" ADDRESS="" EVENTS_SEEN=0 AW_STATE_DIR_REAL="" FEATURE_REL=""
DESK_LOG=() FINDINGS=()
# The script's own globals are set above, so they read plainly: check-env-example.sh takes a default-value expansion for an env read.
or_none() { if [ -n "$1" ]; then printf '%s' "$1"; else printf none; fi; }

# The agent sessions serve launches run in tmux sessions snd-LCH-* on the socket sindri-launch, shared with the owner's Sindri.
# Only a session whose pane process (or a descendant) has AW_SINDRI_HOME equal to $1 is killed. launch_tmux, proc_children and
# proc_environ are seams the test overrides.
launch_tmux() { tmux -L sindri-launch "$@"; }
proc_children() { pgrep -P "$1" 2>/dev/null || true; }
proc_environ() { # one NAME=value per line
  if [ "$(uname -s)" = Darwin ]; then ps eww -p "$1" -o command= 2>/dev/null | tr ' ' '\n'
  else tr '\0' '\n' < "/proc/$1/environ" 2>/dev/null; fi
}
pid_has_home() { # the pid or a descendant (three levels) has AW_SINDRI_HOME=$2
  local c g
  proc_environ "$1" | grep -qxF "AW_SINDRI_HOME=$2" && return 0
  for c in $(proc_children "$1"); do
    proc_environ "$c" | grep -qxF "AW_SINDRI_HOME=$2" && return 0
    for g in $(proc_children "$c"); do proc_environ "$g" | grep -qxF "AW_SINDRI_HOME=$2" && return 0; done
  done
  return 1
}
kill_launched_sessions() {
  local home="$1" s p
  [ -n "$home" ] || return 0
  while IFS= read -r s; do
    case "$s" in snd-LCH-*) ;; *) continue ;; esac
    while IFS= read -r p; do
      [ -n "$p" ] || continue
      if pid_has_home "$p" "$home"; then
        launch_tmux kill-session -t "=$s" > /dev/null 2>&1 && say "ended agent session $s"
        break
      fi
    done < <(launch_tmux list-panes -t "=$s" -F '#{pane_pid}' 2>/dev/null)
  done < <(launch_tmux list-sessions -F '#{session_name}' 2>/dev/null)
}

cleanup() {
  local rc=$?
  if [ -n "$SCRATCH" ] && [ -n "${AW_SINDRI_HOME:-}" ] && [ "$AW_SINDRI_HOME" = "$HOME_DIR" ]; then
    # stop serve first: as the human at a terminal (a session's stop is refused), then the pid control.json names
    if [ -f "$HOME_DIR/control.json" ]; then
      local pid; pid="$(jq -r '.pid // empty' "$HOME_DIR/control.json" 2>/dev/null || true)"
      human stop </dev/null >/dev/null 2>&1 || true
      if [ -n "$pid" ]; then
        local i; for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20; do kill -0 "$pid" 2>/dev/null || break; sleep 0.5; done
        kill -0 "$pid" 2>/dev/null && kill "$pid" 2>/dev/null
      fi
    fi
    # the agent sessions serve launched outlive it: end this home's, with --keep too (a live agent with no serve is useless)
    [ "$KEEP" != 1 ] || say "--keep keeps $SCRATCH, but the agent sessions serve launched are ended: they are useless without serve"
    kill_launched_sessions "$HOME_DIR"
  fi
  if [ -n "$SCRATCH" ]; then
    if [ "$KEEP" = 1 ]; then say "kept $SCRATCH"; else
      # the scratch dir was made by this run and passed the guard
      guard_scratch "$SCRATCH" 2>/dev/null && find "$SCRATCH" -delete 2>/dev/null
    fi
  fi
  exit "$rc"
}

# sindri as the session (reads) and as the human (everything that changes state, and reads that need the owner's scope)
session() { "$PROVE" "$@"; }
human() { "$PROVE" human "$@"; }
hjson() { human "$@" | sed -n '/^[{[]/,$p'; }   # the JSON part of a human call (the pty may add a line before it)

heavy_prefix() {
  local h="$AW_STATE_DIR_REAL"; [ -n "$h" ] || h="$HOME/.agentic-workflow"; h="$h/bin-r1b/heavy"
  [ -x "$h" ] && printf '%s\n' "$h --expensive"
}

publish_sindri() {
  local out="$SCRATCH/bin" tree="$SCRATCH/build" log="$SCRATCH/publish.log" pre commit
  # shellcheck source=lib/sindri-build-inputs.sh
  source "$ROOT/scripts/lib/sindri-build-inputs.sh"
  mkdir -p "$tree" "$out"
  say "publishing sindri (framework-dependent) into $out; log: $log"
  git -C "$ROOT" ls-files -z --cached --others --exclude-standard -- "${SINDRI_BUILD_PATHSPECS[@]}" context \
    | while IFS= read -r -d '' f; do if [ -f "$ROOT/$f" ]; then printf '%s\0' "$f"; fi; done \
    | (cd "$ROOT" && tar --null -cf - -T -) | tar -xf - -C "$tree" || die "could not copy the build inputs"
  # the shipped recipes are HEAD's (as sindri-app.sh embeds them): an edited working-tree recipe is never embedded
  find "$tree/recipes/sindri" -delete 2>/dev/null
  git -C "$ROOT" archive HEAD recipes/sindri | tar -xf - -C "$tree" || die "could not stage recipes/sindri from HEAD"
  commit="$(git -C "$ROOT" rev-parse --short HEAD)"
  pre="$(heavy_prefix)"
  # shellcheck disable=SC2086 # $pre is the heavy launcher and its flag
  (cd "$tree" && $pre dotnet publish src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj -c Release --no-self-contained \
    -p:AwRecipesDir="$tree/recipes/sindri" -o "$out") </dev/null >"$log" 2>&1 || { tail -20 "$log" >&2; die "publishing sindri failed"; }
  [ -x "$out/sindri" ] || die "the publish left no sindri in $out"
  say "published sindri at $commit"
  SINDRI_CMD="$out/sindri"
}

write_profile() {
  local file="$HOME_DIR/profile/profile.yaml"
  mkdir -p "$HOME_DIR/profile"
  {
    echo "schemaVersion: 1"
    echo "user: sandbox"
    echo "repos:"
    echo "  - { name: repo, path: $REPO }"
    if [ "$CONTEXT" = 1 ]; then
      echo "services:"
      echo "  context-docs-refresh: { every: 5m }"
      echo "context:"
      echo "  repos: [repo]"
      echo "  docs: { globs: [\"AGENTS.md\", \"docs/**/*.md\", \"planning/**/*.md\"] }"
      echo "  sessions: { enabled: true }"
    fi
    echo "workflow:"
    # the effect ids the code registers (grep '\"effect\\.' src/Sindri); effect.pr.open and effect.line.start-phase, which the docs
    # name, are not registered
    echo "  effects: [effect.line.report, effect.claim.ref, effect.notify.desk, effect.pr.push, effect.pr.reply, effect.pr.record, effect.pr.rerun-checks, effect.pr.restack, effect.pr.create-stacked, effect.pr.merge, effect.pr.merge-docs]"
    echo "  forge: { adapter: file, hosts: [forge.test] }"
    if [ -n "$AGENT" ]; then echo "  agent: { target: $AGENT }"; fi
  } > "$file"
  chmod 600 "$file"
}

approve_profile() {
  local h
  h="$(session profile validate --json | jq -r .hash)"   # exits 1 while the profile is not approved
  [ -n "$h" ] && [ "$h" != null ] || die "profile validate gave no hash (the profile is invalid: $(session profile validate 2>&1 | head -3))"
  say "profile hash ${h:0:12}"
  printf 'approve profile %s\n' "${h:0:6}" | human profile approve "$h" | grep -q '^Approved' || die "the profile approval was refused"
}

# Registers and approves every shipped recipe, children first: a refused one is retried while any other progresses.
approve_recipes() {
  local pending="" f id v out progress next pass
  for f in "$REPO/recipes/sindri/"workflow.*.yaml; do pending="$pending $f"; done
  APPROVED=() REFUSED=(); : > "$SCRATCH/refusals.tsv"
  for pass in 1 2 3 4 5 6 7 8; do
    progress=0; next=""
    for f in $pending; do
      id="$(sed -n 's/^id: *//p' "$f" | head -1)"; v="$(sed -n 's/^version: *//p' "$f" | head -1)"
      out="$(printf 'approve %s %s\n' "$id" "$v" | human recipe approve "$f" 2>&1)"
      if printf '%s\n' "$out" | grep -qE '^(approved|already approved)'; then
        APPROVED+=("$id"); progress=1
      else
        next="$next $f"
        printf '%s\t%s\n' "$id" "$(printf '%s\n' "$out" | grep -E 'SND-|RCP-' | tail -1 | cut -c1-300)" >> "$SCRATCH/refusals.tsv"
      fi
    done
    pending="$next"
    [ -z "$pending" ] && break
    [ "$progress" = 1 ] || break
  done
  for f in $pending; do id="$(sed -n 's/^id: *//p' "$f" | head -1)"; REFUSED+=("$id"); done
}

# Picks the files the generated map joins to the feature: CODE (up to three, existing, not tests) and TESTF (at most one test
# file). --code FILE comes first, then the files the feature names in backticks, then fallbacks that exist in the clone.
is_test_path() { case "$1" in tests/* | */tests/* | */test/* | *Test.* | *Tests.* | *.test.* | *.spec.*) return 0 ;; *) return 1 ;; esac; }
add_code() {
  local x
  for x in "${CODE[@]:-}"; do [ "$x" = "$1" ] && return 0; done
  [ "${#CODE[@]}" -ge 3 ] || CODE+=("$1")
  return 0
}
pick_map_files() {
  local c f; CODE=() TESTF=""
  if [ -n "$CODE_FILE" ]; then
    [ -f "$REPO/$CODE_FILE" ] || die "the code file $CODE_FILE is not in the clone (--code)"
    add_code "$CODE_FILE"
  else
    for c in $(grep -oE '`[A-Za-z0-9_./-]+\.[a-z]+`' "$REPO/$FEATURE_REL" | tr -d '`'); do
      [ -f "$REPO/$c" ] || continue
      if is_test_path "$c"; then [ -n "$TESTF" ] || TESTF="$c"; else add_code "$c"; fi
    done
  fi
  if [ "${#CODE[@]}" -lt 2 ]; then
    [ ! -f "$REPO/docs/sindri/README.md" ] || add_code docs/sindri/README.md
    while IFS= read -r f; do
      [ "${#CODE[@]}" -ge 2 ] && break
      is_test_path "$f" || add_code "$f"
    done < <(git -C "$REPO" ls-files 'src/*.cs' 2>/dev/null)
  fi
  [ "${#CODE[@]}" -ge 1 ] || die "no code file for the map: the clone has none to fall back to (--code)"
  [ -n "$TESTF" ] || TESTF="$(git -C "$REPO" ls-files 'tests/*Tests.cs' 2>/dev/null | head -1)"
  return 0
}

# Writes an authored map for the feature to $1 (outside any git worktree: the scratch dir): a feature, two surfaces that are
# part-of it, code nodes that implement them, a test file joined by a tests edge, and the record that mentions the feature.
generate_map() {
  local file="$1" i=0 c surf
  pick_map_files
  {
    cat <<EOF
schemaVersion: 1
kind: authored
repo: repo
nodes:
  - {key: "feature:sandbox", label: "Sandbox feature"}
  - {key: "surface:screen/sandbox", label: "The page the feature changes"}
  - {key: "surface:report/sandbox-docs", label: "The docs the feature changes"}
EOF
    for c in "${CODE[@]}"; do printf '  - {key: "code:%s", label: "%s", provenance: deterministic}\n' "$c" "$(basename "$c")"; done
    [ -z "$TESTF" ] || printf '  - {key: "code:%s", label: "%s", provenance: deterministic}\n' "$TESTF" "$(basename "$TESTF")"
    cat <<EOF
  - {key: "record:repo:repo/$FEATURE_REL", label: "$(feature_title)"}
edges:
  - {from: "surface:screen/sandbox", to: "feature:sandbox", type: part-of}
  - {from: "surface:report/sandbox-docs", to: "feature:sandbox", type: part-of}
  - {from: "record:repo:repo/$FEATURE_REL", to: "feature:sandbox", type: mentions}
EOF
    for c in "${CODE[@]}"; do
      surf="surface:screen/sandbox"; [ $((i % 2)) = 1 ] && surf="surface:report/sandbox-docs"
      printf '  - {from: "code:%s", to: "%s", type: implements, evidence: [{key: "repo:repo/%s"}]}\n' "$c" "$surf" "$c"
      i=$((i + 1))
    done
    [ -z "$TESTF" ] || printf '  - {from: "code:%s", to: "surface:screen/sandbox", type: tests, evidence: [{key: "repo:repo/%s"}]}\n' "$TESTF" "$TESTF"
  } > "$file"
}

# Imports the map as the human: the first call types a wrong line (the prompt waits for one; end of input hangs it), which
# prints the hash and is refused; the second types the challenge.
import_map() {
  local file="$SCRATCH/maps/map.yaml" out h
  mkdir -p "$SCRATCH/maps"
  if [ -n "$MAP_FILE" ]; then cp "$MAP_FILE" "$file"; else generate_map "$file"; fi
  out="$(printf 'x\n' | human context map import "$file" 2>&1)"
  h="$(printf '%s\n' "$out" | grep -oE 'hash[: ]+(sha256:)?[0-9a-f]{6,64}' | head -1 | grep -oE '[0-9a-f]{6,64}$')"
  [ -n "$h" ] || { say "FINDING map import printed no hash: $(printf '%s\n' "$out" | head -3 | cut -c1-300)"; return 1; }
  out="$(printf 'import map %s\n' "${h:0:6}" | human context map import "$file" 2>&1)"
  printf '%s\n' "$out" | grep -qE 'Imported' || { say "FINDING map import refused: $(printf '%s\n' "$out" | tail -2 | cut -c1-300)"; return 1; }
  say "context map imported ($(human context map status 2>&1 | head -1 | cut -c1-200))"
}

# the last refusal text recorded for a recipe id
refusal_of() { awk -F'\t' -v id="$1" '$1==id {t=$2} END {print t}' "$SCRATCH/refusals.tsv" 2>/dev/null; }

# the first heading of the feature file, else its name
feature_title() {
  local t; t="$(sed -n 's/^# *//p' "$FEATURE" | head -1)"
  printf '%s\n' "${t:-$(basename "$FEATURE")}"
}

status_json() { hjson workflow status "$1" --json 2>/dev/null; }

# one line per instance of the item: address, recipe, state, chip word, since
instances() { hjson workflow list --item "$ITEM" --json 2>/dev/null \
  | jq -r '.instances[]? | [.address, .recipe.id, .state, .chip.word, (.engineOwned.reason // ""), .since] | @tsv'; }

stop_block() { # $1 = state, $2 = reason, $3 = address (may be empty)
  local state="$1" reason="$2" addr="${3:-$ADDRESS}"
  echo
  echo "=================== STOP ==================="
  echo "state:   $state"
  echo "reason:  $reason"
  echo "item:    $(or_none "$ITEM")   root: $(or_none "$ADDRESS")   scratch: $(or_none "$SCRATCH")"
  if [ -n "$ITEM" ]; then
    echo "--- instances"; instances | sed 's/^/  /'
    if [ -n "$addr" ]; then
      echo "--- status $addr"; human workflow status "$addr" 2>&1 | sed 's/^/  /'
      echo "--- last 20 events $addr"; human workflow events "$addr" 2>&1 | tail -20 | sed 's/^/  /'
    fi
    echo "--- needs-you"; session curl GET /sindri/needs-you 2>/dev/null | jq -r '.rows[]? | "  \(.kind)\t\(.reason)\t\(.cli)"' 2>/dev/null
    echo "--- jobs"; hjson jobs --json 2>/dev/null | jq -r '.jobs[]? | "  \(.id) \(.kind) \(.state) \(.summary // "")"' 2>/dev/null
    echo "--- next command a human would run"
    local cmd; cmd="$(status_json "$addr" | jq -r '.answer.cli // (.commands[]? | select(.verb != "takeover" and .verb != "cancel") | .cli) // empty' 2>/dev/null | head -1)"
    echo "  ${cmd:-(none offered: see the status above)}"
  fi
  if [ "${#DESK_LOG[@]}" -gt 0 ]; then echo "--- desk states answered by --auto-desk"; printf '  %s\n' "${DESK_LOG[@]}"; fi
  echo "============================================"
}

main() {
  parse_args "$@"
  local sindri_cmd
  if [ -n "$SINDRI_ARG" ]; then sindri_cmd="$(resolve_sindri "$SINDRI_ARG")"; else sindri_cmd=""; fi
  if [ "$PRINT_CONFIG" = 1 ]; then
    guard_scratch "${TMPDIR:-/tmp}" 2>/dev/null || true
    printf 'feature=%s\nsindri=%s\nrepo=%s\nstart=%s\nstall=%s\nmax-wait=%s\nauto-desk=%s\ndesk-max=%s\nagent=%s\ncontext=%s\nkeep=%s\n' \
      "$FEATURE" "${sindri_cmd:-publish}" "$REPO_SRC" "$START" "$STALL" "$MAX_WAIT" "$AUTO_DESK" "$DESK_MAX" "$(or_none "$AGENT")" "$CONTEXT" "$KEEP"
    exit 0
  fi
  for t in git jq curl script; do command -v "$t" >/dev/null || die "$t is required"; done

  SCRATCH="$(mktemp -d "${TMPDIR:-/tmp}/sindri-sandbox-e2e.XXXXXX")" || die "no scratch dir"
  trap cleanup EXIT
  guard_scratch "$SCRATCH" || exit 2
  HOME_DIR="$SCRATCH/home"; mkdir -p "$HOME_DIR" "$SCRATCH/state/cache" "$SCRATCH/facts"
  AW_STATE_DIR_REAL="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
  # the temporal CLI serve starts lives in the real state dir's cache; the scratch state sees it through a link
  if [ -d "$AW_STATE_DIR_REAL/cache/bin" ]; then ln -s "$AW_STATE_DIR_REAL/cache/bin" "$SCRATCH/state/cache/bin"; fi
  export AW_SINDRI_HOME="$HOME_DIR" AW_STATE_DIR="$SCRATCH/state" AW_FORGE_ADAPTER=file AW_FORGE_HOSTS=forge.test
  unset AW_SINDRI_PORT AW_SINDRI_PROFILE
  if [ -z "${DOTNET_ROOT:-}" ] && command -v dotnet >/dev/null; then DOTNET_ROOT="$(dirname "$(readlink -f "$(command -v dotnet)")")"; export DOTNET_ROOT; fi

  # 2. the binary
  if [ -n "$sindri_cmd" ]; then SINDRI_CMD="$sindri_cmd"; else publish_sindri; fi
  export SINDRI="$SINDRI_CMD"

  # 3. the target repo: a local clone on a new branch
  REPO="$SCRATCH/repo"
  git clone -q --local "$REPO_SRC" "$REPO" || die "clone failed"
  git -C "$REPO" checkout -q -b "sandbox/e2e-$(date +%s)" || die "branch failed"
  git -C "$REPO" config user.email sandbox@example.invalid; git -C "$REPO" config user.name sandbox
  # the feature file lives in the clone (the scope input names a path inside the repo)
  case "$(cd -P "$(dirname "$FEATURE")" && pwd -P)/$(basename "$FEATURE")" in
    "$(cd -P "$REPO_SRC" && pwd -P)"/*) FEATURE_REL="${FEATURE#"$REPO_SRC"/}"; [ -f "$REPO/$FEATURE_REL" ] || FEATURE_REL="" ;;
    *) FEATURE_REL="" ;;
  esac
  if [ -z "$FEATURE_REL" ]; then FEATURE_REL="docs/sandbox-feature.md"; mkdir -p "$REPO/docs"; cp "$FEATURE" "$REPO/$FEATURE_REL"; fi
  git -C "$REPO" add -A >/dev/null 2>&1; git -C "$REPO" commit -qm "sandbox: the feature brief" >/dev/null 2>&1 || true

  # 4. the forge: the file adapter's world is $AW_STATE_DIR/forge/file, inside the scratch dir
  say "forge: file adapter under $AW_STATE_DIR/forge/file (hosts forge.test); nothing reaches GitHub"

  # 5. the profile
  write_profile
  approve_profile
  [ -f "$HOME_DIR/control.json" ] || die "the profile approval did not leave a running control plane"
  SERVE_PID="$(jq -r '.pid' "$HOME_DIR/control.json")"
  say "control plane pid $SERVE_PID at $(jq -r .url "$HOME_DIR/control.json")"
  approve_recipes
  say "recipes approved: ${#APPROVED[@]}; refused: ${REFUSED[*]:-none}"
  local r; for r in "${REFUSED[@]:-}"; do [ -n "$r" ] && echo "FINDING recipe $r refused: $(refusal_of "$r")" >&2; done
  printf '%s\n' "${APPROVED[@]}" | grep -qx "$START" || { stop_block "not started" "$START is not approved; refused recipes: ${REFUSED[*]:-none}. $(refusal_of "$START")" ""; exit 1; }

  if [ "$CONTEXT" = 1 ]; then import_map || say "scoping will park at gather without a map"; fi

  # 6. the item and the start
  local title out
  title="$TITLE"; [ -n "$title" ] || title="$(feature_title)"
  out="$(human item add "$title" --repo repo 2>&1)"
  ITEM="$(printf '%s\n' "$out" | grep -oE 'ITM-[0-9a-f]+' | head -1)"
  [ -n "$ITEM" ] || die "item add gave no id: $out"
  printf '{"kind":"brief","path":"%s"}\n' "$FEATURE_REL" > "$SCRATCH/facts/scopeInput.json"
  local base; base="$(git -C "$REPO" rev-parse HEAD)"
  printf '{"line":"line","phase":"1","attempt":1,"base":"%s","runArea":{}}\n' "$base" > "$SCRATCH/facts/lineContext.json"
  local facts=(--fact "fact:scopeInput=$SCRATCH/facts/scopeInput.json")
  if [ "$START" = workflow.line ]; then facts+=(--fact "fact:lineContext=$SCRATCH/facts/lineContext.json"); fi
  out="$(hjson workflow start "$START" --item "$ITEM" "${facts[@]}" --json 2>&1)"
  ADDRESS="$(printf '%s\n' "$out" | jq -r '.instance.address // empty' 2>/dev/null)"
  if [ -z "$ADDRESS" ]; then stop_block "not started" "workflow start refused: $(printf '%s\n' "$out" | head -3 | cut -c1-400)" ""; exit 1; fi
  say "started $ADDRESS"

  follow
}

# 7. follow until it stops
follow() {
  local started last_change now snapshot prev="" row addr recipe state chip reason since deepest
  started="$(date +%s)"; last_change="$started"
  while :; do
    now="$(date +%s)"
    [ $((now - started)) -le "$MAX_WAIT" ] || { stop_block "timeout" "the run is still going after ${MAX_WAIT}s" ""; exit 1; }
    kill -0 "$SERVE_PID" 2>/dev/null || { stop_block "serve exited" "the control plane (pid $SERVE_PID) is gone; see $HOME_DIR/logs/serve.log" ""; exit 1; }
    snapshot="$(instances)"
    if [ "$snapshot" != "$prev" ]; then prev="$snapshot"; last_change="$now"; fi
    # events count as progress too
    local ev; ev="$(human workflow events "$ADDRESS" 2>/dev/null | wc -l | tr -d ' ')"
    if [ "$ev" != "$EVENTS_SEEN" ]; then EVENTS_SEEN="$ev"; last_change="$now"; fi
    # the root decides terminal; any instance of the item that is stopped is where the run stands
    row="$(printf '%s\n' "$snapshot" | awk -F'\t' -v a="$ADDRESS" '$1==a')"
    chip="$(printf '%s' "$row" | cut -f4)"
    case "$chip" in
      DONE) local oc; oc="$(status_json "$ADDRESS" | jq -r '.header.outcome // .outcome // empty')"
            if [ -z "$oc" ] || [ "$oc" = resolved ]; then
              echo; echo "TERMINAL: $ADDRESS is done${oc:+ ($oc)}"; instances | sed 's/^/  /'; exit 0
            fi
            stop_block "terminal: ${oc}" "the root instance ended $oc" "$ADDRESS"; exit 1 ;;
      CANCELLED) stop_block "cancelled" "the root instance was cancelled" "$ADDRESS"; exit 1 ;;
    esac
    # the deepest stopped instance: parked, at a desk, failed, escalated or a decision
    deepest="$(printf '%s\n' "$snapshot" | awk -F'\t' '$4=="PARKED"||$4=="WITH YOU"||$4=="FAILED"||$4=="ESCALATED"||$4=="DECISION"' | sort -t$'\t' -k6 | tail -1)"
    if [ -n "$deepest" ]; then
      addr="$(printf '%s' "$deepest" | cut -f1)"; recipe="$(printf '%s' "$deepest" | cut -f2)"
      state="$(printf '%s' "$deepest" | cut -f3)"; chip="$(printf '%s' "$deepest" | cut -f4)"; reason="$(printf '%s' "$deepest" | cut -f5)"
      if { [ "$chip" = "WITH YOU" ] || [ "$chip" = DECISION ]; } && [ "$AUTO_DESK" = 1 ]; then   # a desk task shows as DECISION or WITH YOU
        if auto_desk "$addr"; then last_change="$(date +%s)"; sleep 1; continue; fi
      fi
      if [ "$chip" = PARKED ] && [ "$AUTO_DESK" = 1 ]; then   # a budget park gets a grant: a scratch home, the point is the next stop
        case "$reason" in budget*) if auto_grant "$addr"; then last_change="$(date +%s)"; sleep 1; continue; fi ;; esac
      fi
      case "$chip" in
        "WITH YOU" | DECISION) stop_block "desk" "$recipe has an open desk task (chip $chip) in state $state: a Step the line can't run itself, answered by a human or an agent" "$addr" ;;
        PARKED)     stop_block "parked" "$recipe parked in $state: ${reason:-unknown}" "$addr" ;;
        *)          stop_block "$(printf '%s' "$chip" | tr 'A-Z ' 'a-z-')" "$recipe is $chip in $state ${reason}" "$addr" ;;
      esac
      exit 1
    fi
    [ $((now - last_change)) -le "$STALL" ] || { stop_block "stalled" "no change for ${STALL}s" "$ADDRESS"; exit 1; }
    sleep 3
  done
}

# Answers a budget park with a token grant of $GRANT_TOKENS (an answer on a parent is forwarded to the parked child).
# Shares --desk-max with the desk answers; each grant is in the summary's desk list. Returns 1 at --desk-max.
GRANT_TOKENS=200000   # the most one grant may give (SND-WF-003)
auto_grant() {
  local addr="$1"
  [ "${#DESK_LOG[@]}" -lt "$DESK_MAX" ] || return 1
  say "auto-desk: $addr grant $GRANT_TOKENS tokens"
  DESK_LOG+=("$addr grant-tokens $GRANT_TOKENS")
  # a grant is a protected answer: the human types the challenge's action back at the terminal. The challenge names the
  # parked child (an answer on a parent is forwarded), so a first call that types an empty line (refused, nothing
  # recorded) reads it from the preview. Not </dev/null: the pty never passes the end of input on, so that call waits.
  local challenge
  challenge="$(printf '\n' | human workflow answer "$addr" --grant-tokens "$GRANT_TOKENS" 2>&1 | sed -n "s/.*type '\([^']*\)' to confirm.*/\1/p" | head -1)"
  [ -n "$challenge" ] || challenge="grant-tokens $GRANT_TOKENS $addr"
  printf '%s\n' "$challenge" | human workflow answer "$addr" --grant-tokens "$GRANT_TOKENS" 2>&1 | tail -2 | sed 's/^/  /' >&2
  return 0
}

# Answers a desk state with the first outcome its status offers: the status text prints the command ("sindri workflow
# complete <address> --run <token> --outcome <o> --output fact:<name>=<file> ..."), where <o> may be a choice
# ("<answered|doneAsking>"); an interview's choice takes doneAsking, which closes it instead of asking again. Each output
# gets a placeholder file: [] for fact:answers (no answers), {} for any other (whatever checks the fact next says so, and
# that stop is the finding). Returns 1 when the status offers no complete command or --desk-max answers were given.
auto_desk() {
  local addr="$1" st text run outcome outs=() key f n=${#DESK_LOG[@]} args=()
  [ "$n" -lt "$DESK_MAX" ] || return 1
  st="$(status_json "$addr")"
  text="$(printf '%s' "$st" | jq -r '.next // empty' | tr '\n' ' ' | sed 's/  */ /g')"
  case "$text" in *"workflow complete"*) ;; *) return 1 ;; esac
  run="$(printf '%s' "$text" | grep -oE -- '--run [^ ]+' | head -1 | cut -d' ' -f2)"
  outcome="$(printf '%s' "$text" | grep -oE -- '--outcome [^ ]+' | head -1 | cut -d' ' -f2)"
  case "$outcome" in
    "<"*">") outcome="${outcome#<}"; outcome="${outcome%>}"
             case "|$outcome|" in *"|doneAsking|"*) outcome=doneAsking ;; *) outcome="${outcome%%|*}" ;; esac ;;
  esac
  [ -n "$run" ] && [ -n "$outcome" ] || return 1
  mkdir -p "$SCRATCH/desk"
  for key in $(printf '%s' "$text" | grep -oE -- '--output fact:[A-Za-z0-9_]+=' | sed 's/--output //; s/=$//'); do
    f="$SCRATCH/desk/$n-${key#fact:}.json"; args+=(--output "$key=$f")
    if [ "$key" = fact:answers ]; then printf '[]\n' > "$f"; else printf '{}\n' > "$f"; fi
  done
  say "auto-desk: $addr run $run outcome $outcome ${args[*]:-}"
  DESK_LOG+=("$addr run $run -> $outcome ${args[*]:-}")
  # The printed command can leave out an output the outcome needs (an interview's doneAsking also writes
  # fact:interviewClosed); a refusal that names one gets its placeholder and one more try per fact.
  local out tries=0
  while :; do
    out="$(human workflow complete "$addr" --run "$run" --outcome "$outcome" ${args[@]+"${args[@]}"} 2>&1 | tail -2)"
    key="$(printf '%s' "$out" | grep -oE 'required output fact:[A-Za-z0-9_]+' | head -1 | sed 's/required output //')"
    [ -n "$key" ] && [ "$tries" -lt 3 ] || break
    tries=$((tries + 1)); f="$SCRATCH/desk/$n-${key#fact:}.json"; printf '{}\n' > "$f"; args+=(--output "$key=$f")
    say "auto-desk: $addr also needs $key"
    DESK_LOG[${#DESK_LOG[@]}-1]="$addr run $run -> $outcome ${args[*]}"
  done
  printf '%s\n' "$out" | sed 's/^/  /' >&2
  return 0
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then main "$@"; fi
