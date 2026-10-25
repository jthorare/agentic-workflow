#!/usr/bin/env bash
# scripts/cutover-clean-ts.sh — remove the TypeScript install once the C# build is published (cutover spec §8, §8.1).
#
#   cutover-clean-ts.sh [--pre-link | --post-link] [--dry-run]
#
#   --pre-link   steps 1 to 5: refuse if a TS job runs, reinstall the pre-commit hook of every repo in the TS profile,
#                bootout and delete the launchd plists, unset the TS git template, delete a TS-shaped judge database
#   --post-link  steps 6b to 10: the judge health gate, the TS wrappers, the TS state, the aw-live mod, retired build
#                output, the report. Reuses the log the pre-link run opened
#   (neither)    both halves, with `install-cs.sh --link` (step 6) between them
#   --dry-run    print what would change ("would ..."), write nothing; the read-only checks still run
#
# setup.sh runs: install-cs.sh --publish, this --pre-link, install-cs.sh --link, this --post-link, then the lever installers.
#
# Exit: 0 done or nothing to do; 1 refused at step 1 or the log could not be written (nothing changed); 2 usage;
# 3 stopped partway or the judge health gate failed (re-run ./setup.sh: every step is idempotent). The log path is printed on exit.
#
# It removes by shape and runs on every ./setup.sh, so each delete is gated on something only the TS install has. The C#
# home ($AW_STATE_DIR/sindri-app) and the kept list of spec §8.1 are never touched. No process is killed: step 1 refuses.
#
# Environment: AW_STATE_DIR (default ~/.agentic-workflow), CLAUDE_LOCAL_BIN (default ~/.local/bin), AW_LAUNCH_AGENTS_DIR,
# AW_PROFILE_DIR (the TS profile directory), DOTNET_ROOT (default ~/.dotnet, as install-cs.sh). Bash 3.2 compatible.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

mode=all
dry=0
for a in "$@"; do
  case "$a" in
    --pre-link) mode=pre ;;
    --post-link) mode=post ;;
    --dry-run) dry=1 ;;
    *) echo "usage: cutover-clean-ts.sh [--pre-link | --post-link] [--dry-run]" >&2; exit 2 ;;
  esac
done

say() { echo "cutover: $*"; }
warn() { echo "cutover: warning: $*" >&2; }

STATE="${AW_STATE_DIR:-${HOME:-}/.agentic-workflow}"
case "$STATE" in /?*) ;; *) echo "cutover: AW_STATE_DIR must be an absolute path (got '$STATE')" >&2; exit 2 ;; esac
BIN="${CLAUDE_LOCAL_BIN:-${HOME:-}/.local/bin}"
LA="${AW_LAUNCH_AGENTS_DIR:-${HOME:-}/Library/LaunchAgents}"
PROFILE="${AW_PROFILE_DIR:-$STATE/profile}"
TEMPLATE="$STATE/sindri/git-template"
LOGDIR="$STATE/logs"
LOG=""
LOG_NEW=0
removed=0
partial=0

if [ ! -d "$STATE" ]; then say "nothing to remove"; exit 0; fi
STATE_REAL="$(cd -P "$STATE" && pwd)"

on_exit() { [ -z "$LOG" ] || say "log: $LOG"; }
trap on_exit EXIT

# ---- helpers ----------------------------------------------------------------------------------------------------------

would() { if [ "$dry" = 1 ]; then echo "would $1"; else echo "$1"; fi; }
# L <text>: append to the log (redacted); no-op without a log.
redact() {
  sed -E \
    -e 's/gh[pousr]_[A-Za-z0-9]{16,}/[redacted]/g' \
    -e 's/github_pat_[A-Za-z0-9_]{16,}/[redacted]/g' \
    -e 's/sk-[A-Za-z0-9_-]{16,}/[redacted]/g' \
    -e 's/AKIA[0-9A-Z]{16}/[redacted]/g' \
    -e 's/xox[abprs]-[A-Za-z0-9-]{10,}/[redacted]/g' \
    -e 's/[Bb]earer +[A-Za-z0-9._~+\/=-]{12,}/Bearer [redacted]/g' \
    -e 's/-----BEGIN [A-Z ]*PRIVATE KEY-----.*/[redacted private key]/' \
    -e 's/((token|Token|TOKEN|secret|Secret|SECRET|password|Password|PASSWORD|passwd|api_key|apiKey|API_KEY|apikey|authorization|Authorization|credential|Credential)[A-Za-z_-]*["'\'']?[ ]*[:=][ ]*)[^ ].*/\1[redacted]/'
}
L() { [ -n "$LOG" ] || return 0; printf '%s\n' "$*" | redact >> "$LOG"; }
# Lfile <file>: append a file's text, redacted.
Lfile() { [ -n "$LOG" ] && [ -f "$1" ] && redact < "$1" >> "$LOG"; return 0; }

# in_state <path>: the path's parent resolves inside the state dir (a delete is refused otherwise).
in_state() {
  local p="$1" parent real
  parent="$(cd -P "$(dirname "$p")" 2>/dev/null && pwd)" || return 1
  real="$parent/$(basename "$p")"
  case "$real" in "$STATE_REAL"/?*) return 0 ;; *) return 1 ;; esac
}
count_entries() { find "$1" 2>/dev/null | wc -l | tr -d ' '; }

# del_state <path> <label>: delete a file, a link (unlinked, never followed) or a tree inside the state dir.
del_state() {
  local p="$1" label="$2" n
  [ -e "$p" ] || [ -L "$p" ] || return 0
  if ! in_state "$p"; then warn "refusing to delete $p: it resolves outside $STATE"; partial=1; return 0; fi
  if [ -L "$p" ]; then
    say "$(would "unlink $label ($p)")"
    [ "$dry" = 1 ] || rm -f "$p" || { partial=1; return 0; }
  else
    n="$(count_entries "$p")"
    say "$(would "remove $label ($p, $n entries)")"
    [ "$dry" = 1 ] || rm -rf "$p" || { partial=1; return 0; }
  fi
  removed=$((removed + 1))
  [ "$dry" = 1 ] || L "removed $label ($p)"
}
del_file() { # del_file <path> <label>: a plain file anywhere (a wrapper, a plist)
  local p="$1" label="$2"
  [ -e "$p" ] || [ -L "$p" ] || return 0
  say "$(would "remove $label ($p)")"
  [ "$dry" = 1 ] || rm -f "$p" || { partial=1; return 0; }
  removed=$((removed + 1))
  [ "$dry" = 1 ] || L "removed $label ($p)"
}

# The published C# build the pre-link steps run (nothing is linked yet): bin/cs/current, else the newest complete stamp.
cs_dir() {
  local d
  if [ -d "$STATE/bin/cs/current" ]; then (cd -P "$STATE/bin/cs/current" && pwd); return 0; fi
  for d in $(ls -t "$STATE/bin/cs" 2>/dev/null); do
    [ -f "$STATE/bin/cs/$d/build-info" ] && { echo "$STATE/bin/cs/$d"; return 0; }
  done
  return 1
}
cs_env() {
  if [ -z "${DOTNET_ROOT:-}" ] && [ -d "${HOME:-}/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; fi
}

# ts_db: the judge database has the TS shape (the stamped C# judge refuses it with AW-DATA-009; that check is read-only).
DB="$STATE/judge/decisions.sqlite"
# refuses_ts <judge health output>: the AW-DATA-009 refusal. `judge health` prints the error's message without its code.
refuses_ts() { case "$1" in *AW-DATA-009* | *"has tables but no schema_version table"*) return 0 ;; esac; return 1; }
ts_db() {
  local d out
  [ -f "$DB" ] || return 1
  d="$(cs_dir)" || { warn "no published C# judge to check $DB with; leaving the judge database alone"; return 1; }
  [ -x "$d/judge" ] || return 1
  cs_env
  out="$(AW_STATE_DIR="$STATE" AW_JUDGE_PROVIDERS=rules "$d/judge" health 2>&1 < /dev/null)"
  refuses_ts "$out"
}

latest_log() { ls -t "$LOGDIR"/cutover-*.log 2>/dev/null | head -n 1; }
incomplete_log() { local f; f="$(latest_log)"; [ -n "$f" ] && ! grep -q '^completed' "$f" 2>/dev/null && echo "$f"; return 0; }

# open_log: the log is created before anything else changes; the file is 0600 in a 0700 directory. Exit 1 if it can't be.
open_log() {
  local f
  [ "$dry" = 1 ] && return 0
  [ -z "$LOG" ] || return 0
  if [ "$mode" = post ]; then f="$(incomplete_log)"; [ -z "$f" ] || { LOG="$f"; return 0; }; fi
  if [ ! -d "$LOGDIR" ]; then (umask 077; mkdir -p "$LOGDIR") 2>/dev/null || { echo "cutover: could not create $LOGDIR; nothing was changed" >&2; exit 1; }; chmod 700 "$LOGDIR" 2>/dev/null; fi
  f="$LOGDIR/cutover-$(date +%Y%m%dT%H%M%S).log"
  [ ! -e "$f" ] || f="$LOGDIR/cutover-$(date +%Y%m%dT%H%M%S)-$$.log"
  (umask 077; : > "$f") 2>/dev/null || { echo "cutover: could not write the log in $LOGDIR; nothing was changed" >&2; exit 1; }
  chmod 600 "$f" 2>/dev/null
  [ -w "$f" ] || { echo "cutover: could not write the log $f; nothing was changed" >&2; exit 1; }
  LOG="$f"; LOG_NEW=1
  L "cutover $(date -u +%Y-%m-%dT%H:%M:%SZ) mode=$mode"
}

# ---- the TS profile repos (read once) --------------------------------------------------------------------------------

REPOS=""   # lines "name<TAB>path"
read_repos() {
  local f name path
  [ -d "$PROFILE/repos" ] || return 0
  for f in "$PROFILE"/repos/*.yaml; do
    [ -f "$f" ] || continue
    path="$(sed -nE 's/^path:[ ]*["'\'']?([^"'\'']*)["'\'']?[ ]*$/\1/p' "$f" | head -n 1)"
    name="$(sed -nE 's/^name:[ ]*["'\'']?([^"'\'']*)["'\'']?[ ]*$/\1/p' "$f" | head -n 1)"
    [ -n "$name" ] || name="$(basename "$f" .yaml)"
    [ -n "$path" ] || continue
    REPOS="$REPOS$name	$path
"
  done
}

# The running Sindri control plane (#1032). serve writes control.json as one compact line, {"url":..,"pid":N,"epoch":..,
# "temporal":{"pid":M,..}}: the serve's pid is the TOP-LEVEL one, never the Temporal server's. (Same helper as install-cs.sh;
# the two scripts are copied alone in their tests, so it is repeated, not sourced.)
serve_pid() { # the live serve pid, or nothing
  local f="${AW_SINDRI_HOME:-$STATE/sindri-app}/control.json" pid
  [ -f "$f" ] || return 0
  pid="$(jq -r '.pid // empty' "$f" 2> /dev/null || true)"
  if [ -z "$pid" ]; then
    pid="$(tr -d '\n' < "$f" | sed -E 's/"temporal"[ ]*:.*//' | sed -nE 's/.*"pid"[ ]*:[ ]*([0-9]+).*/\1/p' | head -n 1)"
  fi
  case "$pid" in ''|*[!0-9]*) return 0 ;; esac
  if kill -0 "$pid" 2> /dev/null; then echo "$pid"; fi
}
serve_cmd() { # $1 = pid: its executable (Linux, " (deleted)" stripped) and its command line, one per line
  local exe=""
  if [ -d /proc/self ]; then exe="$(readlink "/proc/$1/exe" 2> /dev/null || true)"; exe="${exe% (deleted)}"; fi
  printf '%s\n%s\n' "$exe" "$(ps -o args= -p "$1" 2> /dev/null || true)"
}
serve_runs_in() { # $1 = a directory: does the running serve execute from it?
  local pid
  pid="$(serve_pid)"
  [ -n "$pid" ] || return 1
  case "$(serve_cmd "$pid")" in *"$1/"*) return 0 ;; *) return 1 ;; esac
}

# ---- step 1: refuse while a TS job runs -------------------------------------------------------------------------------

step1() {
  local pid cmd label uid
  # a C# serve running from the retired bin/sindri-app/ build
  pid="$(serve_pid)"
  if [ -n "$pid" ]; then
    case "$(serve_cmd "$pid")" in *"$STATE/bin/sindri-app/"*)
      say "refused: the Sindri control plane (pid $pid) runs from $STATE/bin/sindri-app/. Nothing was changed. Run: sindri-app stop (or sindri stop), then re-run ./setup.sh"
      exit 1 ;;
    esac
  fi
  # a loaded launchd job that is running (macOS)
  if [ "$(uname)" = Darwin ] && command -v launchctl > /dev/null 2>&1; then
    uid="$(id -u)"
    for label in scorer scorer-audit sindri-observe sindri-index-quick sindri-index sindri-evolve; do
      [ -f "$LA/com.agentic-workflow.$label.plist" ] || continue
      if launchctl print "gui/$uid/com.agentic-workflow.$label" 2> /dev/null | grep -q 'state = running'; then
        say "refused: the launchd job com.agentic-workflow.$label is running. Nothing was changed. Re-run ./setup.sh when it has finished"
        exit 1
      fi
    done
  fi
  # a process holding the TS ledger open
  if [ -f "$STATE/sindri/ledger.db" ]; then
    if command -v lsof > /dev/null 2>&1; then
      pid="$(lsof -t "$STATE/sindri/ledger.db" 2> /dev/null | head -n 1)"
    elif command -v fuser > /dev/null 2>&1; then
      pid="$(fuser "$STATE/sindri/ledger.db" 2> /dev/null | tr -s ' ' '\n' | grep -E '^[0-9]+$' | head -n 1)"
    else
      pid=""; warn "neither lsof nor fuser exists; could not check whether the TS ledger is in use"
    fi
    if [ -n "$pid" ]; then
      say "refused: process $pid holds the TS ledger ($STATE/sindri/ledger.db) open. Nothing was changed. Stop it, then re-run ./setup.sh"
      exit 1
    fi
  fi
}

# ---- pre-link ---------------------------------------------------------------------------------------------------------

tmpl_matches() {
  local v
  v="$(git config --global --type=path --get init.templateDir 2>/dev/null)" || return 1
  [ "$v" = "$TEMPLATE" ]
}

capture_config() { # step 2's log: the judge config as the commands that put it back
  local cfg="$STATE/judge/config.json" k v id
  [ -f "$cfg" ] || return 0
  L "# judge config.json (deleted at step 5); re-enter with:"
  if ! jq -e . "$cfg" > /dev/null 2>&1; then L "# config.json is not valid JSON; its text follows"; Lfile "$cfg"; return 0; fi
  for k in $(jq -r 'keys[] | select(. != "providers" and . != "promptSort")' "$cfg"); do
    v="$(jq -c --arg k "$k" '.[$k]' "$cfg")"
    L "judge config set $k $v"
  done
  L "providers $(jq -c '.providers // null' "$cfg")"
  L "promptSort $(jq -c '.promptSort // null' "$cfg")"
  v="$(jq -r '.promptSort.enabled // empty' "$cfg")"
  [ -z "$v" ] || L "judge config set prompt-sort enabled $v"
  for id in $(jq -r '.promptSort.scaffolds // {} | to_entries[] | select(.value == true) | .key' "$cfg"); do
    L "judge prompt-sort scaffold $id on"
  done
}

pre_link() {
  local sindri_bin work=0 name path plist label uid
  read_repos
  [ -d "$PROFILE" ] || [ -L "$PROFILE" ] && work=1
  if [ "$(uname)" = Darwin ]; then
    for label in scorer scorer-audit sindri-observe sindri-index-quick sindri-index sindri-evolve; do
      [ -f "$LA/com.agentic-workflow.$label.plist" ] && work=1
    done
  fi
  tmpl_matches && work=1
  ts_db && work=1
  if [ "$work" = 0 ]; then return 0; fi

  open_log
  # step 2: log what a later stop would lose, then the hooks
  if [ "$dry" = 0 ]; then
    L "# TS profile repos (name, path)"
    printf '%s' "$REPOS" | while IFS="$(printf '\t')" read -r name path; do [ -n "$name" ] && L "repo $name $path"; done
    capture_config
    if command -v judge > /dev/null 2>&1; then
      L "# judge label status (the TS labels that will be lost)"
      judge label status 2>&1 < /dev/null | redact >> "$LOG" || true
    fi
    for path in "$PROFILE/profile.yaml" "$PROFILE"/repos/*.yaml; do
      [ -f "$path" ] || continue
      L "# $path"; Lfile "$path"
    done
  fi
  sindri_bin=""
  if [ -n "$REPOS" ]; then
    if [ "$dry" = 1 ]; then
      printf '%s' "$REPOS" | while IFS="$(printf '\t')" read -r name path; do say "would install hook v3 in $name ($path)"; done
    else
      d="$(cs_dir)" || { say "no published C# build in $STATE/bin/cs; run scripts/install-cs.sh --publish first"; partial=1; d=""; }
      if [ -n "$d" ]; then
        sindri_bin="$d/sindri"; cs_env
        while IFS="$(printf '\t')" read -r name path; do
          [ -n "$name" ] || continue
          if [ ! -d "$path/.git" ] && ! git -C "$path" rev-parse --git-dir > /dev/null 2>&1; then
            say "hook v3 not installed in $name: $path is not a readable git repo"; L "hook skipped $name $path"; continue
          fi
          if hook_out="$("$sindri_bin" scrub --install-pre-commit --repo "$path" --bin "$BIN/sindri" 2>&1 < /dev/null)"; then
            say "installed hook v3 in $name ($path)"; L "hook v3 $name $path"
          elif printf '%s' "$hook_out" | grep -q 'SND-SCRUB-003'; then
            # The repo has its own pre-commit hook (husky, core.hooksPath, ...): Sindri never replaces it, so this is a
            # skip with the manual line to add (the same answer `sindri doctor` gives), not a step-2 failure (#1029).
            say "hook v3 skipped in $name ($path): it has its own pre-commit hook; add 'sindri scrub --staged || exit 1' to it by hand"
            L "hook skipped (SND-SCRUB-003) $name $path: $(printf '%s' "$hook_out" | redact | tr '\n' ' ')"
          else
            say "hook v3 failed in $name ($path): $(printf '%s' "$hook_out" | redact | tail -n 3 | tr '\n' ' ')"
            L "hook failed $name $path: $(printf '%s' "$hook_out" | redact | tr '\n' ' ')"; partial=1
          fi
        done <<EOF
$REPOS
EOF
      fi
    fi
  fi
  [ "$partial" = 0 ] || { say "stopped at step 2; re-run ./setup.sh"; exit 3; }

  # step 3: launchd (macOS only; Linux has no launchd jobs, so it only gets the cron reminder)
  uid="$(id -u)"
  if [ "$(uname)" = Darwin ]; then
    for label in scorer scorer-audit sindri-observe sindri-index-quick sindri-index sindri-evolve; do
      plist="$LA/com.agentic-workflow.$label.plist"
      [ -f "$plist" ] || continue
      if [ "$dry" = 0 ] && command -v launchctl > /dev/null 2>&1; then
        launchctl bootout "gui/$uid" "$plist" > /dev/null 2>&1 || true
      fi
      del_file "$plist" "launchd job com.agentic-workflow.$label"
    done
  else
    say "remove any cron line you added by hand for scorer or sindri"
  fi

  # step 4: the git template
  if tmpl_matches; then
    say "$(would "unset git init.templateDir ($TEMPLATE)")"
    [ "$dry" = 1 ] || { git config --global --unset init.templateDir && L "unset init.templateDir" && removed=$((removed + 1)) || partial=1; }
  fi

  # step 5: the TS-shaped judge database
  if ts_db; then
    del_file "$STATE/judge/config.json" "judge config (values are in the log)"
    del_file "$DB" "TS judge database"
    del_file "$DB-shm" "TS judge database -shm"
    del_file "$DB-wal" "TS judge database -wal"
  fi
  [ "$partial" = 0 ] || { say "stopped at step 5; re-run ./setup.sh"; exit 3; }
}

# ---- post-link --------------------------------------------------------------------------------------------------------

is_ts_wrapper() { [ -f "$1" ] && [ ! -L "$1" ] && grep -qE 'exec .*dist/cli\.js' "$1" 2> /dev/null; }

# retirable <checkout> <rel>: the directory exists and only ignored build output is left in it (providers/lib.sh aw_remove_retired_dir).
retirable() {
  local repo="$1" rel="$2"
  [ -d "$repo/$rel" ] || return 1
  git -C "$repo" rev-parse --git-dir > /dev/null 2>&1 || return 1
  [ -z "$(git -C "$repo" ls-files -- "$rel")" ] || return 1
  [ -z "$(git -C "$repo" ls-files --others --exclude-standard -- "$rel")" ] || return 1
}
retire_dir() {
  local repo="$1" rel="$2"
  retirable "$repo" "$rel" || return 0
  say "$(would "remove $repo/$rel (retired; only ignored build output left)")"
  [ "$dry" = 1 ] || { rm -rf "${repo:?}/${rel:?}" && removed=$((removed + 1)) && L "removed $repo/$rel"; }
}

post_link() {
  local inc work=0 f d out rc skip left
  inc="$(incomplete_log)"
  [ "$dry" = 1 ] && inc=""
  # what is there to do
  ts_db && work=1
  for f in scorer sindri-next; do is_ts_wrapper "$BIN/$f" && work=1; done
  [ -L "$BIN/sindri-app" ] && case "$(readlink "$BIN/sindri-app")" in */sindri-app.sh) work=1 ;; esac
  [ -d "$STATE/bin/sindri-app" ] && work=1
  for f in sindri scorer audit; do [ -e "$STATE/$f" ] || [ -L "$STATE/$f" ] && work=1; done
  [ -e "$PROFILE" ] || [ -L "$PROFILE" ] && work=1
  for d in "$STATE"/*/bugfix; do [ -d "$d" ] && work=1; done
  for f in judge scorer sindri; do for d in dist node_modules; do retirable "$ROOT" "$f/$d" && work=1; done; done
  if command -v claude > /dev/null 2>&1; then
    claude plugin list --json 2> /dev/null | grep -q 'aw-live@agentic-workflow-mods' && work=1
    claude plugin marketplace list --json 2> /dev/null | grep -q 'agentic-workflow-mods' && work=1
  fi
  if [ "$work" = 0 ] && [ -z "$inc" ]; then return 0; fi
  open_log

  # step 6b: the judge gate
  if [ "$work" = 1 ] || [ -n "$inc" ]; then
    if ts_db_linked; then
      say "a TS-shaped judge database came back after the link; deleting it once more"
      del_file "$STATE/judge/config.json" "judge config"; del_file "$DB" "TS judge database"
      del_file "$DB-shm" "TS judge database -shm"; del_file "$DB-wal" "TS judge database -wal"
    fi
    if [ "$dry" = 0 ] && [ -x "$BIN/judge" ]; then
      out="$("$BIN/judge" health 2>&1 < /dev/null)"; rc=$?
      if refuses_ts "$out"; then say "judge health gate failed: AW-DATA-009 (a TS-shaped judge database). Re-run ./setup.sh"; exit 3; fi
      if [ "$rc" -ge 126 ]; then say "judge health gate failed: $BIN/judge does not run (exit $rc). Re-run ./setup.sh"; exit 3; fi
      [ "$rc" = 0 ] || warn "judge health exited $rc: $(printf '%s' "$out" | head -n 1)"
    elif [ "$dry" = 0 ]; then
      say "judge health gate failed: $BIN/judge is not installed. Re-run ./setup.sh"; exit 3
    fi
  fi

  # step 7: the TS wrappers, by shape
  for f in scorer sindri-next; do
    is_ts_wrapper "$BIN/$f" && del_file "$BIN/$f" "TS wrapper $f"
  done
  if [ -L "$BIN/sindri-app" ]; then
    case "$(readlink "$BIN/sindri-app")" in */sindri-app.sh) del_file "$BIN/sindri-app" "the sindri-app link" ;; esac
  fi
  if serve_runs_in "$STATE/bin/sindri-app"; then
    say "kept $STATE/bin/sindri-app: Sindri is still running from it; run \`sindri stop\`, then re-run ./setup.sh"
    L "kept $STATE/bin/sindri-app: Sindri is still running from it"
  else
    del_state "$STATE/bin/sindri-app" "the retired sindri-app builds"
  fi
  left=""
  for f in "$BIN"/sindri-*; do
    [ -e "$f" ] || [ -L "$f" ] || continue
    left="$left $(basename "$f")"
  done
  [ -z "$left" ] || { say "left alone (not a TS wrapper this script knows):$left"; L "left alone:$left"; }

  # step 8: the TS state
  for f in sindri scorer audit; do del_state "$STATE/$f" "TS state $f/"; done
  del_state "$PROFILE" "the TS profile"
  for d in "$STATE"/*/bugfix; do
    [ -d "$d" ] || continue
    case "$(basename "$(dirname "$d")")" in bugfix|sindri-app|meta|bin|cache|logs|locks|hooks|fleet|context-guard|judge|digests|toolkit|ui-evidence) continue ;; esac
    del_state "$d" "TS bug-fix state"
  done
  [ "$partial" = 0 ] || { say "stopped at step 8; re-run ./setup.sh"; exit 3; }

  # step 9: the aw-live mod, retired build output
  if command -v claude > /dev/null 2>&1; then
    if claude plugin list --json 2> /dev/null | grep -q 'aw-live@agentic-workflow-mods'; then
      say "$(would "uninstall the aw-live plugin")"
      [ "$dry" = 1 ] || { claude plugin uninstall aw-live@agentic-workflow-mods --scope user > /dev/null 2>&1 && L "uninstalled aw-live" || warn "could not uninstall aw-live; run: claude plugin uninstall aw-live@agentic-workflow-mods --scope user"; }
    fi
    if claude plugin marketplace list --json 2> /dev/null | grep -q 'agentic-workflow-mods'; then
      say "$(would "remove the marketplace agentic-workflow-mods")"
      [ "$dry" = 1 ] || { claude plugin marketplace remove agentic-workflow-mods > /dev/null 2>&1 && L "removed marketplace" || warn "could not remove the marketplace agentic-workflow-mods"; }
    fi
  fi
  for f in judge scorer sindri; do for d in dist node_modules; do retire_dir "$ROOT" "$f/$d"; done; done

  # step 10: the report
  [ "$dry" = 1 ] && return 0
  say "kept: judge hook files and state, context-guard, locks, hooks, fleet, ui-evidence runs, workflow state, sindri-app (the C# home), logs, meta, cache"
  L "kept: judge/{sessions,outbox,teammate-names.jsonl,evals,trait-check} context-guard locks hooks fleet ui-evidence/runs sindri-app logs meta cache"
  if [ "$removed" -gt 0 ] || [ -n "$inc" ] || [ "$LOG_NEW" = 1 ]; then
    L "next steps:"
    L "sindri profile init"
    L "sindri profile approve"
    grep '^repo ' "$LOG" 2> /dev/null | while read -r _ name path; do L "sindri repo onboard $path   # $name"; done
    L "sindri doctor"
    say "next steps (the C# profile starts empty):"
    sed -n '/^next steps:/,$p' "$LOG" | sed '1d' | sed 's/^/cutover:   /'
  fi
  L "completed"
}

# After the link the judge on PATH is the C# one: test the shape the same way, through the wrapper.
ts_db_linked() {
  local out
  [ -f "$DB" ] || return 1
  [ -x "$BIN/judge" ] || return 1
  out="$(AW_JUDGE_PROVIDERS=rules "$BIN/judge" health 2>&1 < /dev/null)"
  refuses_ts "$out"
}

# ---- run --------------------------------------------------------------------------------------------------------------

step1
case "$mode" in
  pre) pre_link ;;
  post) post_link ;;
  all)
    pre_link
    if [ "$removed" -gt 0 ] || [ -n "$(incomplete_log)" ]; then
      if [ "$dry" = 1 ]; then say "would run $SCRIPT_DIR/install-cs.sh --link"
      elif [ -x "$SCRIPT_DIR/install-cs.sh" ] || [ -f "$SCRIPT_DIR/install-cs.sh" ]; then
        bash "$SCRIPT_DIR/install-cs.sh" --link || { say "install-cs.sh --link failed; re-run ./setup.sh"; exit 3; }
      fi
    fi
    post_link ;;
esac

if [ "$removed" = 0 ] && [ -z "$LOG" ]; then say "nothing to remove"; fi
exit 0
