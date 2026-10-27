#!/usr/bin/env bash
# scripts/cutover-prove.sh — the P2 scratch-box proof of the cutover (spec docs/superpowers/specs/2026-10-23-cutover-design.md
# §12 P2, with §7.1, §8, §8.1 and §10): run the whole switch on a seeded fake TS install without the real state.
#
#   cutover-prove.sh [--dry-run] [--keep] [--skip-downloads] [--budget-ms N] [--work DIR]
#   --dry-run         guard, print the scratch layout and the checks, build and write nothing (exit 0)
#   --keep            leave the scratch box in place when done (its path is printed); default: delete it
#   --skip-downloads  AW_CS_SKIP_DOWNLOADS=1 for install-cs.sh (SDK, temporal and Chromium must already be there)
#   --budget-ms N     the nudge hook's budget the cold `sindri repo status --nudge` must stay inside (default
#                     $AW_SINDRI_NUDGE_BUDGET_MS, else 4000: the value config/hooks/sindri-nudge.sh uses)
#   --work DIR        the scratch box (must not exist; strictly inside the temp root); default: a fresh mktemp dir
#
# Everything the switch touches lives under one scratch dir inside realpath(${TMPDIR:-/tmp}): AW_STATE_DIR, CLAUDE_LOCAL_BIN,
# AW_LAUNCH_AGENTS_DIR, GIT_CONFIG_GLOBAL, CLAUDE_DIR, CODEX_HOME, CURSOR_DIR, AW_HOOKS_DIR and the Playwright cache. HOME is
# NOT changed (a temp HOME under dotnet leaks a 3.3 GB NuGet cache); `claude` and `launchctl` are stubs that log their calls,
# so no real plugin, marketplace or launchd job is reached. The guard runs first and refuses any variable that resolves outside
# the scratch dir.
#
# Order (the order setup.sh runs, spec §8): seed the TS install; check a failed --publish and a step-1 refusal change nothing;
# install-cs.sh --publish; cutover-clean-ts.sh --pre-link; install-cs.sh --link; cutover-clean-ts.sh --post-link; then the
# P2 exits. One PASS or FAIL line per check; exit 0 only when every check passed (a SKIP is printed, never counted as a pass).
#
# Builds .NET: run it under the box-wide heavy slot (`heavy scripts/cutover-prove.sh`), never inside an agent's own build budget.
# Tests: bash scripts/tests/cutover-prove.test.sh (argument parsing, guard and dry run; builds nothing).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"

die() { echo "cutover-prove: $*" >&2; exit 2; }
usage() { sed -n '2,21p' "$0" | sed 's/^# \{0,1\}//'; }

dry=0 keep=0 skip_dl=0 budget="${AW_SINDRI_NUDGE_BUDGET_MS:-4000}" work_arg=""
while [ $# -gt 0 ]; do
  case "$1" in
    -h | --help) usage; exit 0 ;;
    --dry-run) dry=1 ;;
    --keep) keep=1 ;;
    --skip-downloads) skip_dl=1 ;;
    --budget-ms) [ $# -ge 2 ] || die "--budget-ms needs a number"; budget="$2"; shift ;;
    --work) [ $# -ge 2 ] || die "--work needs a directory"; work_arg="$2"; shift ;;
    *) usage >&2; exit 2 ;;
  esac
  shift
done
case "$budget" in '' | *[!0-9]*) die "--budget-ms must be a whole number of milliseconds (got $budget)" ;; esac

# --- the guard -------------------------------------------------------------------------------------------------------------
# real PATH: like realpath(3), parts that don't exist yet are kept as written; fails when the deepest existing part is a file.
real() {
  local p="$1" rest="" head
  case "$p" in /*) ;; *) p="$PWD/$p" ;; esac
  head="$p"
  while [ "$head" != "/" ] && [ ! -e "$head" ] && [ ! -L "$head" ]; do rest="/${head##*/}$rest"; head="${head%/*}"; [ -n "$head" ] || head="/"; done
  [ -d "$head" ] || return 1
  head="$(cd -P "$head" 2> /dev/null && pwd -P)" || return 1
  [ "$head" = "/" ] && head=""
  printf '%s\n' "${head}${rest}"
}
inside() { case "$1" in "${2%/}"/?*) return 0 ;; *) return 1 ;; esac; }

temp_root="$(real "${TMPDIR:-/tmp}")" || die "TMPDIR is not usable"
if [ -n "$work_arg" ]; then
  WORK="$(real "$work_arg")" || die "--work $work_arg can't be resolved"
  [ ! -e "$WORK" ] || die "--work $WORK exists; give a path that doesn't"
  mkdir -p "$(dirname "$WORK")" 2> /dev/null || true
else
  WORK="$temp_root/cutover-prove.$$.$(date +%s)"
fi
inside "$WORK" "$temp_root" || die "the scratch box must be strictly inside $temp_root (got $WORK)"
case "$WORK" in "$HOME" | "$HOME"/.agentic-workflow* | "$HOME"/.claude* | "$HOME"/.local*) die "refusing a scratch box under the real home state ($WORK)" ;; esac

STATE="$WORK/state" LBIN="$WORK/bin" LAUNCH="$WORK/LaunchAgents" GITCFG="$WORK/gitconfig"
CLAUDE_D="$WORK/claude" CODEX_D="$WORK/codex" CURSOR_D="$WORK/cursor" STUBS="$WORK/stubs" REPO="$WORK/repo"
SLUG="scratch-repo"
guard_vars() {
  local pair name value r
  for pair in "AW_STATE_DIR=$STATE" "CLAUDE_LOCAL_BIN=$LBIN" "AW_LAUNCH_AGENTS_DIR=$LAUNCH" "GIT_CONFIG_GLOBAL=$GITCFG" \
    "CLAUDE_DIR=$CLAUDE_D" "CODEX_HOME=$CODEX_D" "CURSOR_DIR=$CURSOR_D" "AW_HOOKS_DIR=$STATE/hooks" "PLAYWRIGHT_BROWSERS_PATH=$WORK/pw" \
    "AW_SINDRI_HOME=$STATE/sindri-app"; do
    name="${pair%%=*}"; value="${pair#*=}"
    r="$(real "$value")" || die "$name=$value can't be resolved"
    inside "$r" "$WORK" || die "$name=$value resolves outside the scratch box ($r)"
  done
}
guard_vars

CHECKS=(
  "failed-publish-changes-nothing:a failed --publish leaves the seeded TS install untouched"
  "step1-refusal-changes-nothing:a step-1 refusal (a process holding the TS ledger) exits 1 and changes nothing"
  "publish:install-cs.sh --publish publishes and verifies all binaries; PATH and current untouched"
  "pre-link:cutover-clean-ts.sh --pre-link exits 0"
  "link:install-cs.sh --link links the six C# wrappers over the TS ones"
  "post-link:cutover-clean-ts.sh --post-link exits 0 (the judge health gate)"
  "removed-exactly-8:every path of the spec 8 list is gone"
  "kept-byte-identical:every seeded path outside the list, and the settings registrations, are byte-identical"
  "hook-v3:the v2 pre-commit hook is v3, baked with the stable wrapper path"
  "hook-refuses-secret:v3 refuses a staged secret (SND-SCRUB-002)"
  "hook-passes-clean:v3 passes a clean commit"
  "hook-suites:judge-cs-hook-surface and sindri-cs-hook-surface pass against the installed binaries"
  "seeded-hooks:prompt-sort.sh and trait-check.sh run against the installed judge; their registrations are untouched"
  "judge-stub-sort-why:judge prompt-sort why x prints its not-yet-built line, nothing on stdout, exit 1"
  "judge-stub-sort-promote:judge prompt-sort promote prints its not-yet-built line, nothing on stdout, exit 1"
  "setup-dry-run:./setup.sh --providers claude,codex,cursor --dry-run is clean"
  "second-run-no-change:a second sequence changes nothing and prints 'cutover: nothing to remove'"
  "live-judge-state-survives:live C# judge state (decisions.sqlite, config.json) survives a second sequence"
  "nudge-cold-budget:cold 'sindri repo status --nudge' through the wrapper is inside the nudge budget"
)

if [ "$dry" = 1 ]; then
  echo "cutover-prove: dry run, nothing is built or written"
  echo "cutover-prove: scratch box $WORK (inside $temp_root)"
  echo "cutover-prove: AW_STATE_DIR=$STATE CLAUDE_LOCAL_BIN=$LBIN AW_LAUNCH_AGENTS_DIR=$LAUNCH GIT_CONFIG_GLOBAL=$GITCFG"
  echo "cutover-prove: CLAUDE_DIR=$CLAUDE_D CODEX_HOME=$CODEX_D CURSOR_DIR=$CURSOR_D PLAYWRIGHT_BROWSERS_PATH=$WORK/pw (HOME stays $HOME)"
  echo "cutover-prove: nudge budget ${budget} ms; downloads $([ "$skip_dl" = 1 ] && echo skipped || echo allowed); keep=$keep"
  echo "cutover-prove: sequence: seed, failed publish, step-1 refusal, install-cs --publish, clean --pre-link, install-cs --link, clean --post-link, checks, second sequence"
  for c in "${CHECKS[@]}"; do echo "cutover-prove: check ${c%%:*} - ${c#*:}"; done
  exit 0
fi

for tool in git jq perl python3; do command -v "$tool" > /dev/null 2>&1 || die "$tool is required"; done
[ -f "$ROOT/scripts/cutover-clean-ts.sh" ] || die "scripts/cutover-clean-ts.sh is not in this checkout (merge feat/cutover-p2-clean-ts first)"

# --- scratch box, results ----------------------------------------------------------------------------------------------------
mkdir -p "$WORK" || die "can't create $WORK"
: > "$WORK/.cutover-prove-scratch"
fails=0 passes=0 skips=0
pass() { passes=$((passes + 1)); echo "PASS $1"; }
fail() { fails=$((fails + 1)); echo "FAIL $1${2:+: $2}"; }
skip() { skips=$((skips + 1)); echo "SKIP $1${2:+: $2}"; }
holder=""
cleanup() {
  if [ -n "$holder" ]; then kill "$holder" 2> /dev/null; fi
  if [ "$keep" = 1 ]; then echo "cutover-prove: scratch box kept at $WORK"; return; fi
  if [ -f "$WORK/.cutover-prove-scratch" ] && inside "$WORK" "$temp_root"; then find "$WORK" -delete 2> /dev/null; fi
}
trap cleanup EXIT

sha256() { shasum -a 256 2> /dev/null || sha256sum; }
mode_of() { stat -c %a "$1" 2> /dev/null || stat -f %Lp "$1"; }
now_ms() { perl -MTime::HiRes=time -e 'printf "%d\n", time * 1000'; }
snap() { # snap <dir>...: one line per file or link: path, kind, mode, hash (or the link target)
  local d f
  for d in "$@"; do
    [ -e "$d" ] || continue
    (cd "$d" && find . \( -type f -o -type l \) -print | LC_ALL=C sort | while IFS= read -r f; do
      if [ -L "$f" ]; then printf '%s\tL\t%s\n' "$d/${f#./}" "$(readlink "$f")"
      else printf '%s\tF\t%s\t%s\n' "$d/${f#./}" "$(mode_of "$f")" "$(sha256 < "$f" | cut -d' ' -f1)"; fi
    done)
  done
}
snap_box() { snap "$STATE" "$LBIN" "$LAUNCH" "$CLAUDE_D" "$CODEX_D" "$CURSOR_D" "$REPO/.git/hooks"; [ ! -f "$GITCFG" ] || snap_file "$GITCFG"; }
snap_file() { printf '%s\tF\t%s\t%s\n' "$1" "$(mode_of "$1")" "$(sha256 < "$1" | cut -d' ' -f1)"; }
# the logs the cleanup script writes are its own, and install-cs's lock/cache scratch is not state: leave them out of "unchanged"
stable() { grep -v -e "$STATE/logs/cutover-" -e "$STATE/cache/install-cs" -e "$STATE/bin/cs/\.lock" || true; }

# --- the isolated environment ------------------------------------------------------------------------------------------------
mkdir -p "$STATE" "$LBIN" "$LAUNCH" "$CLAUDE_D" "$CODEX_D" "$CURSOR_D" "$STUBS" "$WORK/tmp" "$WORK/pw" || die "can't create the scratch layout"
export TMPDIR="$WORK/tmp"
export AW_STATE_DIR="$STATE" AW_STATE_ROOT="$STATE" CLAUDE_LOCAL_BIN="$LBIN" AW_LAUNCH_AGENTS_DIR="$LAUNCH" GIT_CONFIG_GLOBAL="$GITCFG"
export CLAUDE_DIR="$CLAUDE_D" CLAUDE_CONFIG_DIR="$CLAUDE_D" CODEX_HOME="$CODEX_D" CURSOR_DIR="$CURSOR_D" AW_HOOKS_DIR="$STATE/hooks"
export PLAYWRIGHT_BROWSERS_PATH="$WORK/pw" AW_SINDRI_HOME="$STATE/sindri-app" AW_SINDRI_PORT=0 AW_NO_SYSTEM_INSTALL=1
export AW_JUDGE_PROVIDERS=rules AW_LOG_LEVEL=none GIT_TERMINAL_PROMPT=0
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
[ "$skip_dl" = 1 ] && export AW_CS_SKIP_DOWNLOADS=1
for v in $(env | grep -E '^(AW_META|TMUX|AW_SINDRI_(CHILD|PROFILE|JUDGE|LAUNCH|ITEM|EXE|UI|CLI_TIMEOUT)|AW_PROFILE_DIR|AW_JUDGE_CS_BIN|AW_SINDRI_CS_BIN|CLAUDECODE|CLAUDE_CODE_)' | cut -d= -f1); do unset "$v"; done
# stubs first on PATH: nothing real is reached by the cleanup's plugin and launchd steps
for s in claude launchctl; do
  printf '#!/usr/bin/env bash\necho "%s $*" >> "%s/stub-calls.log"\nexit 0\n' "$s" "$WORK" > "$STUBS/$s"; chmod +x "$STUBS/$s"
done
export PATH="$STUBS:$LBIN:$PATH"
printf '[user]\n\tname = Prove\n\temail = prove@example.invalid\n[init]\n\tdefaultBranch = main\n\ttemplateDir = %s/sindri/git-template\n' "$STATE" > "$GITCFG"

# --- seed the fake TS install -------------------------------------------------------------------------------------------------
put() { mkdir -p "$(dirname "$1")"; printf '%s\n' "${2:-$1}" > "$1"; }
ts_wrapper() { printf '#!/usr/bin/env bash\nexport SINDRI_BIN="%s/%s"\nexec "/usr/bin/node" "%s/sindri/dist/cli.js" "$@"\n' "$LBIN" "$1" "$STATE" > "$LBIN/$1"; chmod 755 "$LBIN/$1"; }
seed() {
  local n
  for n in judge sindri scorer sindri-next; do ts_wrapper "$n"; done
  # scripts/sindri-app.sh links $LBIN/sindri-app to itself in the checkout; the switch deletes that script, so the link dangles
  put "$STATE/bin/sindri-app/stamp1/sindri-app" "ts app"; ln -s "$WORK/checkout/scripts/sindri-app.sh" "$LBIN/sindri-app"
  printf '#!/bin/sh\necho foreign view\n' > "$LBIN/sindri-view"; chmod 755 "$LBIN/sindri-view"
  # TS-only state (removed)
  put "$STATE/sindri/ledger.db" "ledger"; put "$STATE/sindri/index/graph.json"; put "$STATE/sindri/channels.json" '{"stable":null,"next":null}'
  put "$STATE/sindri/git-template/hooks/pre-commit"; put "$STATE/sindri/spool/a.json"; put "$STATE/sindri/logs/x.log"
  put "$STATE/scorer/reports/r1.md"; put "$STATE/audit/a1.json"; put "$STATE/$SLUG/bugfix/ticket-1/state.json" '{"ts":true}'
  put "$STATE/profile/profile.yaml" "schemaVersion: 1
repos:
  - $SLUG"
  put "$STATE/profile/repos/$SLUG.yaml" "name: $SLUG
path: $REPO"
  # the TS judge database: tables, no schema_version
  python3 -I - "$STATE/judge/decisions.sqlite" << 'PY' || die "can't create the TS-shaped decisions.sqlite (python3 sqlite3)"
import sqlite3, sys, os
os.makedirs(os.path.dirname(sys.argv[1]), exist_ok=True)
c = sqlite3.connect(sys.argv[1])
c.execute("CREATE TABLE decisions (id TEXT PRIMARY KEY, question TEXT, ts TEXT)")
c.execute("CREATE TABLE failures (id TEXT, reason TEXT)")
c.execute("INSERT INTO decisions VALUES ('d1','brief','2026-01-01')")
c.commit(); c.close()
PY
  put "$STATE/judge/decisions.sqlite-shm" "shm"; put "$STATE/judge/decisions.sqlite-wal" "wal"
  put "$STATE/judge/config.json" '{"questions":{"brief":{"enabled":true}},"promptSort":{"budgetMs":900}}'
  # launchd plists (macOS removes the six; any OS keeps the decoy)
  for n in scorer scorer-audit sindri-observe sindri-index-quick sindri-index sindri-evolve; do put "$LAUNCH/com.agentic-workflow.$n.plist" "<plist>$n</plist>"; done
  put "$LAUNCH/com.example.keep.plist" "<plist>keep</plist>"
  # kept state (spec 8 "Kept, never touched")
  put "$STATE/judge/sessions/s1.json" '{"s":1}'; put "$STATE/judge/outbox/o1.json" '{"o":1}'; put "$STATE/judge/teammate-names.jsonl" '{"n":1}'
  put "$STATE/judge/evals/e1.json"; put "$STATE/judge/trait-check/t1.json"; put "$STATE/context-guard/g1"; put "$STATE/locks/heavy-slots-cap" "3"
  put "$STATE/fleet/f1.json"; put "$STATE/ui-evidence/runs/run1/summary.json" '{"ts":true}'
  for n in plans reviews proof verification design; do put "$STATE/$SLUG/$n/k1.md"; done
  put "$STATE/digests/d1.md"; put "$STATE/toolkit/t1"; put "$STATE/logs/old.log"; put "$STATE/meta/m1"; put "$STATE/bin/aw-meta"; put "$STATE/cache/c1"
  put "$STATE/sindri-app/keep.txt" "the C# Sindri home"
  # registered hooks (kept untouched): prompt-sort and trait-check
  mkdir -p "$STATE/hooks"; cp "$ROOT/config/hooks/prompt-sort.sh" "$ROOT/config/hooks/trait-check.sh" "$STATE/hooks/"
  jq -n --arg d "$STATE/hooks" '{hooks:{
    UserPromptSubmit:[{hooks:[{type:"command",command:($d+"/prompt-sort.sh # aw:prompt-sort")}]}],
    PostToolUse:[{matcher:"Edit|Write",hooks:[{type:"command",command:($d+"/trait-check.sh # aw:trait-check-edit")}]}],
    Stop:[{hooks:[{type:"command",command:($d+"/trait-check.sh # aw:trait-check-stop")}]}]}}' > "$CLAUDE_D/settings.json"
  # one onboarded repo with a v2 pre-commit hook
  git init -q "$REPO" && git -C "$REPO" commit -q --allow-empty -m init || die "can't create the seeded repo"
  printf '#!/bin/sh\n# sindri-pre-commit v2\nSINDRI_BIN=%s exec %s scrub --staged\n' "'$LBIN/sindri'" "'$LBIN/sindri'" > "$REPO/.git/hooks/pre-commit"
  chmod 755 "$REPO/.git/hooks/pre-commit"
}
seed
before="$WORK/before.snap"; snap_box | stable > "$before"
[ -s "$before" ] || die "the seed is empty"

CLEAN="$ROOT/scripts/cutover-clean-ts.sh" CS="$ROOT/scripts/install-cs.sh"
run() { local name="$1"; shift; "$@" > "$WORK/$name.out" 2> "$WORK/$name.err"; echo $? > "$WORK/$name.rc"; }
rc() { cat "$WORK/$1.rc"; }
shown() { tail -n 12 "$WORK/$1.out" "$WORK/$1.err" 2> /dev/null | head -c 1500; }
same_as() { # same_as <snapshot file> -> 0 when the box now matches it
  snap_box | stable > "$WORK/now.snap"; cmp -s "$1" "$WORK/now.snap"
}
diff_note() { diff "$1" "$WORK/now.snap" | head -n 6 | tr '\n' ';'; }

# 1. a failed --publish changes nothing: a copy of the script outside any git checkout dies before it touches a thing
nogit="$WORK/nogit"; mkdir -p "$nogit/scripts"
cp "$CS" "$nogit/scripts/install-cs.sh"; cp -R "$ROOT/scripts/lib" "$nogit/scripts/lib"; cp "$ROOT/global.json" "$nogit/global.json"
run fail-publish env GIT_CEILING_DIRECTORIES="$WORK" bash "$nogit/scripts/install-cs.sh" --publish
if [ "$(rc fail-publish)" = 1 ] && same_as "$before"; then pass failed-publish-changes-nothing
else fail failed-publish-changes-nothing "rc=$(rc fail-publish) $(diff_note "$before") $(shown fail-publish)"; fi

# 2. a step-1 refusal changes nothing: a process holds the TS ledger open
if command -v lsof > /dev/null 2>&1 || command -v fuser > /dev/null 2>&1; then
  ( exec 3< "$STATE/sindri/ledger.db"; exec sleep 600 ) & holder=$!
  sleep 1
  run refusal bash "$CLEAN"
  if [ "$(rc refusal)" = 1 ] && same_as "$before"; then pass step1-refusal-changes-nothing
  else fail step1-refusal-changes-nothing "rc=$(rc refusal) $(diff_note "$before") $(shown refusal)"; fi
  kill "$holder" 2> /dev/null; holder=""
else
  skip step1-refusal-changes-nothing "neither lsof nor fuser exists on this box"
fi

# 3. publish
run publish bash "$CS" --publish
stamp_dir="$(find "$STATE/bin/cs" -mindepth 1 -maxdepth 1 -type d ! -name '.*' 2> /dev/null | LC_ALL=C sort | tail -n 1)"
nobin=""
for b in sindri judge bugfix-state ui-evidence aw-packets aw-readiness; do [ -n "$stamp_dir" ] && [ -x "$stamp_dir/$b" ] || nobin="$nobin $b"; done
if [ "$(rc publish)" = 0 ] && [ -z "$nobin" ] && [ ! -e "$STATE/bin/cs/current" ] && grep -q 'dist/cli.js' "$LBIN/sindri" && grep -q 'dist/cli.js' "$LBIN/judge"; then
  pass publish
else fail publish "rc=$(rc publish) missing:[$nobin] $(shown publish)"; fi

# 4/5/6. the setup.sh order
run prelink bash "$CLEAN" --pre-link
if [ "$(rc prelink)" = 0 ]; then pass pre-link; else fail pre-link "rc=$(rc prelink) $(shown prelink)"; fi
run link bash "$CS" --link
cs_ok=1
for b in sindri judge bugfix-state ui-evidence aw-packets aw-readiness; do
  if [ ! -x "$LBIN/$b" ] || ! grep -q 'cs/current' "$LBIN/$b" || grep -q 'dist/cli.js' "$LBIN/$b"; then cs_ok=0; fi
done
if [ "$(rc link)" = 0 ] && [ -L "$STATE/bin/cs/current" ] && [ "$cs_ok" = 1 ]; then pass link; else fail link "rc=$(rc link) wrappers_ok=$cs_ok $(shown link)"; fi
run postlink bash "$CLEAN" --post-link
if [ "$(rc postlink)" = 0 ]; then pass post-link; else fail post-link "rc=$(rc postlink) $(shown postlink)"; fi

# a cold nudge through the wrapper: the first call after the publish (a stopped control plane, no warm state)
cold_home="$WORK/nudge-home"; mkdir -p "$cold_home"
t0="$(now_ms)"
(cd "$REPO" && AW_SINDRI_HOME="$cold_home" sindri repo status --nudge > "$WORK/nudge.out" 2> "$WORK/nudge.err"); nrc=$?
t1="$(now_ms)"; cold=$((t1 - t0))
t0="$(now_ms)"; (cd "$REPO" && AW_SINDRI_HOME="$cold_home" sindri repo status --nudge > /dev/null 2>&1); t1="$(now_ms)"; warm=$((t1 - t0))
echo "cutover-prove: nudge cold ${cold} ms, warm ${warm} ms, budget ${budget} ms"
if [ "$nrc" = 0 ] && [ "$cold" -lt "$budget" ]; then pass nudge-cold-budget; else fail nudge-cold-budget "rc=$nrc cold=${cold}ms budget=${budget}ms $(head -c 300 "$WORK/nudge.err")"; fi

# --- the exits ---------------------------------------------------------------------------------------------------------------
# removed exactly the spec 8 list; the plists go on macOS only (Linux prints a reminder about cron)
roots=("$STATE/sindri" "$STATE/profile" "$STATE/scorer" "$STATE/audit" "$STATE/$SLUG/bugfix" "$STATE/judge/config.json"
  "$STATE/judge/decisions.sqlite" "$STATE/judge/decisions.sqlite-shm" "$STATE/judge/decisions.sqlite-wal"
  "$STATE/bin/sindri-app" "$LBIN/sindri-app" "$LBIN/scorer" "$LBIN/sindri-next")
replaced=("$LBIN/judge" "$LBIN/sindri" "$REPO/.git/hooks/pre-commit")   # the hook: v2 becomes v3 (checked below)
if [ "$(uname -s)" = Darwin ]; then
  for n in scorer scorer-audit sindri-observe sindri-index-quick sindri-index sindri-evolve; do roots+=("$LAUNCH/com.agentic-workflow.$n.plist"); done
fi
left=""
# the post-link `judge health` (spec §8 step 6b) creates the C# database at the TS path: a database the C# judge accepts is not left
cs_db=0
if [ -f "$STATE/judge/decisions.sqlite" ] && judge health > /dev/null 2>&1; then cs_db=1; fi
for r in "${roots[@]}"; do
  case "$r" in "$STATE/judge/decisions.sqlite"*) [ "$cs_db" = 1 ] && continue ;; esac
  if [ -e "$r" ] || [ -L "$r" ]; then left="$left $r"; fi
done
tpl="$(git config --global --get init.templateDir 2> /dev/null || true)"
[ -z "$tpl" ] || left="$left init.templateDir=$tpl"
if [ -z "$left" ]; then pass removed-exactly-8; else fail removed-exactly-8 "still there:$left"; fi

# kept: every seeded path outside the list (and outside the two replaced wrappers) is byte-identical
under() { local p="$1" r; shift; for r in "$@"; do case "$p" in "$r" | "$r"/*) return 0 ;; esac; done; return 1; }
: > "$WORK/kept.snap"
while IFS= read -r line; do
  p="${line%%$'\t'*}"
  if under "$p" "${roots[@]}" "${replaced[@]}"; then continue; fi
  printf '%s\n' "$line" >> "$WORK/kept.snap"
done < "$before"
# the global git config lost only the templateDir line: compare it by its other lines
grep -v -F "$GITCFG" "$WORK/kept.snap" > "$WORK/kept2.snap" || true
snap_box | stable > "$WORK/after.snap"
missing="$(LC_ALL=C sort "$WORK/kept2.snap" | LC_ALL=C comm -23 - <(LC_ALL=C sort "$WORK/after.snap") | cut -f1 | head -n 8 | tr '\n' ' ')"
cfg_ok=1; git config --global --get user.name > /dev/null 2>&1 || cfg_ok=0
if [ -z "$missing" ] && [ "$cfg_ok" = 1 ]; then pass kept-byte-identical; else fail kept-byte-identical "changed or gone: $missing cfg_ok=$cfg_ok"; fi

# v2 is now v3, refuses a staged secret, passes a clean commit
hook="$REPO/.git/hooks/pre-commit"
if grep -qx '# sindri-pre-commit v3' "$hook" && ! grep -q 'pre-commit v2' "$hook" && grep -qF "$LBIN/sindri" "$hook"; then pass hook-v3
else fail hook-v3 "$(head -n 5 "$hook" 2> /dev/null | tr '\n' '|')"; fi
printf 'token %s%s\n' "ghp_" "$(printf 'a%.0s' $(seq 1 36))" > "$REPO/secret.txt"
git -C "$REPO" add secret.txt
out="$(git -C "$REPO" commit -q -m "secret" 2>&1)"; crc=$?
if [ "$crc" != 0 ] && printf '%s' "$out" | grep -q 'SND-SCRUB-002'; then pass hook-refuses-secret; else fail hook-refuses-secret "rc=$crc $out"; fi
git -C "$REPO" reset -q; find "$REPO" -maxdepth 1 -name secret.txt -delete
printf 'clean\n' > "$REPO/clean.txt"; git -C "$REPO" add clean.txt
out="$(git -C "$REPO" commit -q -m "clean" 2>&1)"; crc=$?
if [ "$crc" = 0 ]; then pass hook-passes-clean; else fail hook-passes-clean "rc=$crc $out"; fi

# the real hooks, against the installed binaries, pass the spec 10 suites
cur="$STATE/bin/cs/current"
suites_ok=1 suites_note="" TMO=()
if command -v timeout > /dev/null 2>&1; then TMO=(timeout 1500); fi
for s in judge-cs-hook-surface:AW_JUDGE_CS_BIN=judge sindri-cs-hook-surface:AW_SINDRI_CS_BIN=sindri; do
  f="${s%%:*}"; kv="${s#*:}"
  if ! env "${kv%%=*}=$(cd "$cur" && pwd -P)/${kv#*=}" ${TMO[@]+"${TMO[@]}"} bash "$ROOT/config/lib/tests/$f.test.sh" > "$WORK/$f.out" 2>&1; then
    suites_ok=0; suites_note="$suites_note $f: $(tail -n 4 "$WORK/$f.out" | tr '\n' '|')"
  fi
done
if [ "$suites_ok" = 1 ]; then pass hook-suites; else fail hook-suites "$suites_note"; fi

# the seeded prompt-sort and trait-check registrations: the real hook scripts run against the installed judge, the settings stay
sett_before="$(sha256 < "$CLAUDE_D/settings.json")"
ps_out="$(printf '{"prompt":"fix the failing build in foo","sessionId":"prove-1","session_id":"prove-1"}' | bash "$STATE/hooks/prompt-sort.sh" 2> /dev/null)"; ps_rc=$?
tc_rc=0; (cd "$REPO" && printf '{"hook_event_name":"Stop","session_id":"prove-1","stop_hook_active":false}' | bash "$STATE/hooks/trait-check.sh" > /dev/null 2>&1) || tc_rc=$?
if [ "$ps_rc" = 0 ] && [ "$tc_rc" = 0 ] && [ "$sett_before" = "$(sha256 < "$CLAUDE_D/settings.json")" ] && grep -q 'aw:prompt-sort' "$CLAUDE_D/settings.json" && grep -q 'aw:trait-check-stop' "$CLAUDE_D/settings.json"; then
  pass seeded-hooks
else fail seeded-hooks "prompt-sort rc=$ps_rc out=$ps_out trait-check rc=$tc_rc"; fi

# the not-yet-built stub (spec 5.2, Q9): empty stdout, exactly one stderr line, exit 1
stub_check() { # stub_check <check> <expected stderr line> <judge args...>
  local name="$1" want="$2" o e r; shift 2
  o="$(judge "$@" 2> "$WORK/stub.err")"; r=$?; e="$(cat "$WORK/stub.err")"
  if [ "$r" = 1 ] && [ -z "$o" ] && [ "$e" = "$want" ]; then pass "$name"; else fail "$name" "rc=$r stdout=[$o] stderr=[$e]"; fi
}
stub_check judge-stub-sort-why "judge: prompt-sort why is not in this build yet" prompt-sort why x
stub_check judge-stub-sort-promote "judge: prompt-sort promote is not in this build yet" prompt-sort promote

# setup.sh --dry-run is clean (no real HOME writes: the dry run writes nothing; the providers read the scratch dirs)
run setup-dry bash "$ROOT/setup.sh" --providers claude,codex,cursor --dry-run
if [ "$(rc setup-dry)" = 0 ]; then pass setup-dry-run; else fail setup-dry-run "rc=$(rc setup-dry) $(shown setup-dry)"; fi

# live C# judge state: create it with the C# judge, then run the sequence again
q="$(judge config get 2> /dev/null | jq -r '.questions | keys[0] // empty' 2> /dev/null)"
if [ -n "$q" ]; then judge config set "$q" threshold 0.7 > /dev/null 2>&1; fi
judge health > /dev/null 2>&1
live_ok=1
[ -f "$STATE/judge/config.json" ] && [ -f "$STATE/judge/decisions.sqlite" ] || live_ok=0
live_before="$(snap "$STATE/judge" | grep -e 'config.json' -e 'decisions.sqlite' | grep -v -e '-wal' -e '-shm')"
snap_box | stable > "$WORK/flipped.snap"
run again-publish bash "$CS" --publish
run again-pre bash "$CLEAN" --pre-link
run again-link bash "$CS" --link
run again-post bash "$CLEAN" --post-link
snap_box | stable > "$WORK/now.snap"
chg="$(diff "$WORK/flipped.snap" "$WORK/now.snap" | grep -v -e 'decisions.sqlite-' | head -n 6 | tr '\n' ';')"
nothing=0; cat "$WORK/again-pre.out" "$WORK/again-post.out" 2> /dev/null | grep -q 'cutover: nothing to remove' && nothing=1
if [ "$(rc again-publish)$(rc again-pre)$(rc again-link)$(rc again-post)" = 0000 ] && [ "$nothing" = 1 ] && [ -z "$chg" ]; then pass second-run-no-change
else fail second-run-no-change "rcs=$(rc again-publish)$(rc again-pre)$(rc again-link)$(rc again-post) nothing_to_remove=$nothing diff=$chg"; fi
live_after="$(snap "$STATE/judge" | grep -e 'config.json' -e 'decisions.sqlite' | grep -v -e '-wal' -e '-shm')"
# the database can be rewritten by its own health call; what must survive is the TS-free pair existing and answering
if [ "$live_ok" = 1 ] && [ -f "$STATE/judge/config.json" ] && [ -f "$STATE/judge/decisions.sqlite" ] && [ "$(printf '%s' "$live_before" | grep config.json)" = "$(printf '%s' "$live_after" | grep config.json)" ] && judge health > /dev/null 2>&1; then
  pass live-judge-state-survives
else fail live-judge-state-survives "live_ok=$live_ok before=[$live_before] after=[$live_after]"; fi
# ./setup.sh itself on the flipped box is the owner's P4 step; its dry run is checked above

echo "cutover-prove: $passes passed, $fails failed, $skips skipped"
[ "$fails" = 0 ]
