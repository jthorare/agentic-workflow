#!/usr/bin/env bash
# Tests for scripts/aw-commands.sh (#4221, PR 1): the command catalog reader. Two catalogs: the toolkit's (AW_COMMANDS_TOOLKIT,
# a fixture here) and the repo's <git toplevel>/.agentic-workflow/commands.json, in a temp git repo. Covers: usage and --help;
# list and search are deterministic, bounded and tagged with their catalog; show prints the invocation with --set values and
# never runs the entry; validate names the file, the entry and the field for each finding; exit 3 only when neither catalog
# exists; a second worktree finds the same catalog; this repo's seed catalogs validate (and match the JSON Schema when Ajv is
# installed); config/hooks/git-context.sh prints one pointer line counting the repo's entries, and nothing for a missing file,
# a file that isn't JSON or a box without jq.
# Run: bash scripts/tests/aw-commands.test.sh
set -uo pipefail
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="$(cd "$DIR/.." && pwd)"
CLI="$DIR/aw-commands.sh"
HOOK="$ROOT/config/hooks/git-context.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in: $2"; fail=1 ;; esac; }
lacks() { case "$2" in *"$3"*) echo "not ok - $1"; echo "  unexpected: $3"; echo "  in: $2"; fail=1 ;; *) echo "ok - $1" ;; esac; }

WORK="$(cd "$(mktemp -d)" && pwd -P)"
cleanup() { find "$WORK" -delete 2> /dev/null; }
trap cleanup EXIT

entry() { # entry <id> <purpose> <terms json> <argv json> [cwd]
  printf '{"id":"%s","purpose":"%s","terms":%s,"run":{"argv":%s,"cwd":"%s"},"effects":"read-only","output":"one line","stop":"exits on its own","owner":"test","verified":{"status":"verified","at":"2026-11-23"}}' \
    "$1" "$2" "$3" "$4" "${5:-repo}"
}
catalog() { # catalog <file> <entry>...
  local f="$1" first=1 e; shift
  mkdir -p "$(dirname "$f")"
  { printf '{"version":1,"entries":['; for e in "$@"; do [ "$first" = 1 ] || printf ','; first=0; printf '%s' "$e"; done; printf ']}\n'; } > "$f"
}

# A toolkit catalog and a repo with its own catalog and the scripts it names.
TK="$WORK/toolkit/commands.json"
catalog "$TK" \
  "$(entry tk-status 'Show the toolkit status' '["status","health"]' '["true"]' any)" \
  "$(entry tk-watch 'Wait for the next team event' '["watch","event","wake"]' '["true","--until-event"]' any)"
export AW_COMMANDS_TOOLKIT="$TK"
REPO="$WORK/repo"
mkdir -p "$REPO/scripts"
g() { git -C "$REPO" -c user.name=t -c user.email=t@example.invalid "$@"; }
g init -q -b main
printf '#!/bin/sh\necho checked\n' > "$REPO/scripts/check-a.sh"; chmod +x "$REPO/scripts/check-a.sh"
MARK="$WORK/ran-marker"
catalog "$REPO/.agentic-workflow/commands.json" \
  "$(entry repo-check 'Check the docs for prose problems' '["prose","lint","docs"]' '["scripts/check-a.sh"]')" \
  "$(entry pr-checks 'Read the checks of a pull request' '["pull request","ci","checks"]' '["true","--pr","{pr}"]')" \
  "$(entry touches 'Touch a marker file' '["marker"]' "[\"touch\",\"$MARK\"]" any)"
# pr-checks takes a required pr param
jq '.entries[1].params = [{"name":"pr","required":true,"description":"the pull request number"}]' "$REPO/.agentic-workflow/commands.json" > "$WORK/c.json" && mv "$WORK/c.json" "$REPO/.agentic-workflow/commands.json"
g add -A && g commit -q -m base
cmd() { (cd "$REPO" && bash "$CLI" "$@"); }

# --- usage ---
cmd > /dev/null 2>&1; check "no subcommand is exit 2" "$?" "2"
cmd bogus > /dev/null 2>&1; check "an unknown subcommand is exit 2" "$?" "2"
help="$(cmd --help 2>&1)"; check "--help is exit 0" "$?" "0"
has "--help names the repo catalog" "$help" ".agentic-workflow/commands.json"
has "--help names exit 3" "$help" "3"

# --- list ---
out="$(cmd list)"; rc=$?
check "list: exit 0" "$rc" "0"
check "list: every entry of both catalogs, sorted by id" "$(cut -d' ' -f1 <<< "$out" | tr '\n' ' ')" "pr-checks repo-check tk-status tk-watch touches "
has "list: a repo entry is tagged repo" "$(grep '^repo-check ' <<< "$out")" "[repo]"
has "list: a toolkit entry is tagged toolkit" "$(grep '^tk-status ' <<< "$out")" "[toolkit]"
check "list: the same output twice" "$(cmd list)" "$out"
check "list --json: five entries" "$(cmd list --json | jq length)" "5"
check "list --json: each names its catalog" "$(cmd list --json | jq -r '[.[].source] | unique | join(",")')" "repo,toolkit"

# --- search ---
out="$(cmd search pull request checks)"; rc=$?
check "search: exit 0 on a match" "$rc" "0"
check "search: the best match first" "$(head -1 <<< "$out" | cut -d' ' -f1)" "pr-checks"
check "search: the same order twice" "$(cmd search pull request checks)" "$out"
check "search: matches the purpose, not only the id" "$(cmd search prose | head -1 | cut -d' ' -f1)" "repo-check"
check "search: matches a term" "$(cmd search wake | head -1 | cut -d' ' -f1)" "tk-watch"
check "search: case does not matter" "$(cmd search PROSE | head -1 | cut -d' ' -f1)" "repo-check"
cmd search zzzznothing > /dev/null 2>&1; check "search: nothing matched is exit 1" "$?" "1"
check "search --json: an array of hits" "$(cmd search checks --json | jq -r '.[0].id')" "pr-checks"

# --- show ---
out="$(cmd show pr-checks --set pr=12)"; rc=$?
check "show: exit 0" "$rc" "0"
has "show: the invocation with the value filled in" "$out" "run: true --pr 12"
has "show: the param and that it is required" "$out" "pr (required)"
out="$(cmd show pr-checks)"
has "show: a required param left out is named" "$out" "needs --set pr=<value>"
cmd show touches > /dev/null
check "show: never runs the entry" "$([ -e "$MARK" ] && echo ran || echo not-run)" "not-run"
check "show --json: the invocation array" "$(cmd show pr-checks --set pr=7 --json | jq -c .invocation)" '["true","--pr","7"]'
cmd show nosuch > /dev/null 2>&1; check "show: an unknown id is exit 1" "$?" "1"

# --- validate: one line per finding, naming file, entry and field ---
cmd validate > /dev/null; check "validate: both fixture catalogs are clean" "$?" "0"
BAD="$WORK/bad/.agentic-workflow/commands.json"
catalog "$BAD" \
  "$(entry good 'A good one' '[]' '["true"]')" \
  '{"id":"no-purpose","run":{"argv":["true"],"cwd":"repo"},"effects":"read-only","output":"x","stop":"x","owner":"x","verified":{"status":"verified","at":"2026-11-23"}}' \
  "$(entry argv-string 'Argv given as a string' '[]' '"true --x"')" \
  "$(entry home-path 'Points into a home' '[]' '["bash","/home/someone/x.sh"]')" \
  "$(entry good 'A second one with the same id' '[]' '["true"]')" \
  "$(entry stale 'Names a script that is gone' '[]' '["scripts/gone.sh"]')" \
  "$(entry slot 'Uses a param it never declares' '[]' '["true","{nope}"]')" \
  '{"id":"extra","purpose":"Has a key the schema lacks","run":{"argv":["true"],"cwd":"repo"},"effects":"read-only","output":"x","stop":"x","owner":"x","verified":{"status":"verified","at":"2026-11-23"},"colour":"red"}' \
  "$(entry bad-effect 'An unknown effect' '[]' '["true"]' | jq -c '.effects = "deletes-prod"')"
git -C "$WORK/bad" init -q -b main
out="$(cd "$WORK/bad" && bash "$CLI" validate "$BAD" 2>&1)"; rc=$?
check "validate: findings are exit 1" "$rc" "1"
has "validate: a missing purpose" "$out" "commands.json: entries[1] (no-purpose): purpose:"
has "validate: argv as a string" "$out" "entries[2] (argv-string): run.argv: must be a non-empty array of strings"
has "validate: an owner-home path" "$out" "entries[3] (home-path): run.argv[1]: an owner-home path"
has "validate: a duplicate id" "$out" "entries[4] (good): id: duplicate of entries[0]"
has "validate: a stale repo path" "$out" "entries[5] (stale): run.argv[0]: scripts/gone.sh does not exist"
has "validate: an undeclared param slot" "$out" "entries[6] (slot): run.argv[1]: {nope} is not a declared param"
has "validate: an unknown key" "$out" "entries[7] (extra): colour: unknown key"
has "validate: an unknown effect" "$out" "entries[8] (bad-effect): effects: must be one of"
lacks "validate: the good entry has no finding" "$out" "entries[0] (good):"
echo '{"version":1,"entries":[' > "$WORK/notjson.json"
out="$(bash "$CLI" validate "$WORK/notjson.json" 2>&1)"; rc=$?
check "validate: a file that isn't JSON is exit 1" "$rc" "1"
has "validate: ...and says so" "$out" "notjson.json: not valid JSON"
out="$(cd "$WORK/bad" && bash "$CLI" list 2> "$WORK/list.err")"
has "list: an invalid repo catalog is skipped with a warning" "$(cat "$WORK/list.err")" "run aw-commands validate"
check "list: ...and the toolkit's entries still list" "$(cut -d' ' -f1 <<< "$out" | tr '\n' ' ')" "tk-status tk-watch "

# --- exit 3 only when neither catalog exists ---
mkdir -p "$WORK/plain"; git -C "$WORK/plain" init -q -b main
out="$(cd "$WORK/plain" && bash "$CLI" list)"; rc=$?
check "a repo without a catalog: the toolkit's still list" "$rc" "0"
out="$(cd "$WORK/plain" && AW_COMMANDS_TOOLKIT="$WORK/none.json" bash "$CLI" list 2>&1)"; rc=$?
check "neither catalog: exit 3" "$rc" "3"
has "neither catalog: names the toolkit catalog" "$out" "$WORK/none.json"
has "neither catalog: names the repo catalog" "$out" "$WORK/plain/.agentic-workflow/commands.json"
out="$(cd "$REPO" && AW_COMMANDS_TOOLKIT="$WORK/none.json" bash "$CLI" list 2>&1)"; rc=$?
check "no toolkit catalog but a repo one: exit 0" "$rc" "0"

# --- a second worktree reads the same checked-in catalog ---
g worktree add -q "$WORK/wt2" -b second > /dev/null 2>&1
check "a second worktree: the repo entries list" "$(cd "$WORK/wt2" && bash "$CLI" list | grep -c '\[repo\]')" "3"
check "a second worktree: show works" "$(cd "$WORK/wt2" && bash "$CLI" show repo-check | grep '^run:')" "run: scripts/check-a.sh"

# --- bounded output ---
many=(); for i in $(seq -w 1 80); do many+=("$(entry "e$i" "Entry number $i about checks" '["checks"]' '["true"]' any)"); done
catalog "$WORK/many/.agentic-workflow/commands.json" "${many[@]}"
git -C "$WORK/many" init -q -b main
out="$(cd "$WORK/many" && AW_COMMANDS_TOOLKIT="$WORK/none.json" bash "$CLI" list)"
check "list: at most 50 entries by default" "$(grep -c '^e[0-9]' <<< "$out")" "50"
has "list: ...with a line saying how to see more" "$out" "--max"
check "search: at most 10 hits" "$(cd "$WORK/many" && AW_COMMANDS_TOOLKIT="$WORK/none.json" bash "$CLI" search checks | grep -c '^e[0-9]')" "10"

# --- this repo's own catalogs ---
out="$(cd "$ROOT" && AW_COMMANDS_TOOLKIT="$ROOT/config/commands.toolkit.json" bash "$CLI" validate 2>&1)"; rc=$?
check "this repo's catalogs validate" "$rc" "0"
[ "$rc" = 0 ] || echo "$out" | head -20
check "the toolkit catalog validates on its own" "$(bash "$CLI" validate "$ROOT/config/commands.toolkit.json" > /dev/null 2>&1; echo $?)" "0"
# heavy runs "$@", so a placeholder holding "dotnet build -warnaserror" would be one program name (exit 127): the command is appended.
check "the toolkit's heavy entry is bare heavy, the command appended" "$(cd "$ROOT" && AW_COMMANDS_TOOLKIT="$ROOT/config/commands.toolkit.json" bash "$CLI" show heavy --json | jq -c '[.invocation, .missing]')" '[["heavy"],[]]'
check "no seed entry packs a command line into one placeholder" "$(jq -r '[.entries[] | select([.params[]?.description] | any(test("arguments|separate words"))) | .id] | join(",")' "$ROOT/config/commands.toolkit.json" "$ROOT/.agentic-workflow/commands.json" | tr -d '\n')" ""
AJV_DIR=""
for d in "$ROOT/scripts/node_modules" "$(git -C "$ROOT" worktree list --porcelain 2> /dev/null | sed -n '1s/^worktree //p')/scripts/node_modules"; do
  [ -d "$d/ajv" ] && { AJV_DIR="$d"; break; }
done
if [ -n "$AJV_DIR" ] && command -v node > /dev/null 2>&1; then
  for f in "$ROOT/.agentic-workflow/commands.json" "$ROOT/config/commands.toolkit.json"; do
    res="$(node -e '
      const { createRequire } = require("module"); const req = createRequire(process.argv[1] + "/");
      const Ajv = req("ajv/dist/2020").default; const fs = require("fs");
      const ajv = new Ajv({ allErrors: true }); const ok = ajv.validate(JSON.parse(fs.readFileSync(process.argv[2])), JSON.parse(fs.readFileSync(process.argv[3])));
      console.log(ok ? "valid" : JSON.stringify(ajv.errors));' "$AJV_DIR" "$ROOT/schemas/repo-config/commands.schema.json" "$f" 2>&1)"
    check "the JSON Schema accepts $(basename "$f")" "$res" "valid"
  done
else
  echo "ok - skip: the JSON Schema check needs node and Ajv (npm ci --prefix scripts)"
fi

# --- the SessionStart pointer in git-context.sh ---
out="$(cd "$REPO" && bash "$HOOK")"; rc=$?
check "git-context: exit 0 with a catalog" "$rc" "0"
check "git-context: one pointer line, counting the repo's entries only" "$(grep -c '^Commands: ' <<< "$out")" "1"
has "git-context: the pointer" "$out" "Commands: 3 in .agentic-workflow/commands.json; find one with: aw-commands search <words>"
out="$(cd "$WORK/plain" && bash "$HOOK")"; rc=$?
check "git-context: no catalog: exit 0" "$rc" "0"
lacks "git-context: no catalog: no pointer" "$out" "Commands:"
mkdir -p "$WORK/plain/.agentic-workflow"; cp "$WORK/notjson.json" "$WORK/plain/.agentic-workflow/commands.json"
out="$(cd "$WORK/plain" && bash "$HOOK")"; rc=$?
check "git-context: a catalog that isn't JSON: exit 0" "$rc" "0"
lacks "git-context: a catalog that isn't JSON: no pointer" "$out" "Commands:"
mkdir -p "$WORK/nojq"; ln -s "$(command -v git)" "$WORK/nojq/git"
out="$(cd "$REPO" && PATH="$WORK/nojq" /bin/bash "$HOOK")"; rc=$?
check "git-context: no jq: exit 0" "$rc" "0"
lacks "git-context: no jq: no pointer" "$out" "Commands:"
out="$(cd "$WORK" && bash "$HOOK")"; rc=$?
check "git-context: outside a git repo: exit 0" "$rc" "0"
lacks "git-context: outside a git repo: no pointer" "$out" "Commands:"

[ "$fail" = 0 ] && echo "all aw-commands tests passed" || { echo "aw-commands tests FAILED"; exit 1; }
