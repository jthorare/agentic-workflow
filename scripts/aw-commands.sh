#!/usr/bin/env bash
# scripts/aw-commands.sh: find the helper a repo already has before writing a new one (#4221).
#
#   aw-commands list [--max N] [--json]
#   aw-commands search <words...> [--json]
#   aw-commands show <id> [--set name=value]... [--json]
#   aw-commands validate [<file>]
#
# Two catalogs, read in this order:
#   repo     <git toplevel>/.agentic-workflow/commands.json, checked in, so every worktree and a fresh checkout see it
#   toolkit  the toolkit's own catalog: AW_COMMANDS_TOOLKIT, else commands.json beside the installed aw-commands (install-cs
#            ships it into the build), else config/commands.toolkit.json of the checkout this script sits in
# Both have the shape schemas/repo-config/commands.schema.json describes. An entry names its entrypoint as argv (an array,
# never a string for a shell) and the catalog never copies a helper's body. Nothing here runs an entry: show prints the
# invocation for the caller to run.
#
# list prints one line per entry (id, purpose, [catalog]), sorted by id, at most --max (default 50). search scores each entry by
# the words found in its id, purpose and terms (a whole id or term counts more) and prints the best 10, ties by id. A catalog
# with schema findings is skipped with a warning on stderr; validate names the file, the entry and the field of each finding.
# Exit: 0 ok, 1 nothing matched, no such id, or validate findings, 2 usage, 3 neither catalog exists (the message names both).
set -u

MAX_HITS=10
REPO_REL=".agentic-workflow/commands.json"
usage="usage: aw-commands list [--max N] [--json] | search <words...> [--json] | show <id> [--set name=value]... [--json] | validate [<file>]"

die() { echo "aw-commands: $*" >&2; exit 2; }

self="${BASH_SOURCE[0]}"
self="$(readlink -f "$self" 2> /dev/null || echo "$self")"
self_dir="$(cd "$(dirname "$self")" && pwd)"
if [ -n "${AW_COMMANDS_TOOLKIT:-}" ]; then
  toolkit="$AW_COMMANDS_TOOLKIT"
elif [ -f "$self_dir/commands.json" ]; then
  toolkit="$self_dir/commands.json"
else
  toolkit="$self_dir/../config/commands.toolkit.json"
fi
root="$(git rev-parse --show-toplevel 2> /dev/null || true)"
repo_cat=""; [ -z "$root" ] || repo_cat="$root/$REPO_REL"

command -v jq > /dev/null 2>&1 || { echo "aw-commands: jq is required" >&2; exit 2; }

# --- validation ------------------------------------------------------------------------------------------------------
# One line per finding: "<label>: entries[i] (id): field: message". A repo path to check comes out as
# "PATH<TAB>entries[i] (id)<TAB>run.argv[j]<TAB><path>" and is checked by the shell (jq has no file system).
# shellcheck disable=SC2016
VALIDATE_JQ='
def strs: type == "array" and all(.[]; type == "string" and length > 0);
def unknown($allowed): (keys - $allowed)[] | "\(.): unknown key";
def nonempty($f; $v): if ($v | type) == "string" and ($v | length) > 0 then empty else "\($f): must be a non-empty string" end;
def opt_strs($f; $v): if $v == null or ($v | strs) then empty else "\($f): must be an array of non-empty strings" end;
def home: test("^(/home/|/Users/|~)") or test("\\$(HOME|\\{HOME\\})");
def param_findings($p):
  if $p == null then empty
  elif ($p | type) != "array" then "params: must be an array"
  else $p | to_entries[] | .key as $k | .value as $v
    | if ($v | type) != "object" then "params[\($k)]: must be an object"
      else (($v | unknown(["name","required","description"])) | "params[\($k)].\(.)"),
        (if ($v.name | type) == "string" and ($v.name | test("^[a-z][a-z0-9_-]{0,31}$")) then empty else "params[\($k)].name: must be 1-32 lowercase letters, digits, _ or -" end),
        (if ($v.required | type) == "boolean" then empty else "params[\($k)].required: must be true or false" end),
        nonempty("params[\($k)].description"; $v.description)
      end
  end;
# detect (#4221 PR 2): optional; all = regexes that must all match, params = a regex with a capture group per declared param.
def bad_re($r): (try ("" | test($r)) catch "bad") == "bad";
def detect_findings($d; $names):
  if $d == null then empty
  elif ($d | type) != "object" then "detect: must be an object with all (and params)"
  else (($d | unknown(["all","params"])) | "detect.\(.)"),
    (if ($d.all | strs) and ($d.all | length) > 0
     then $d.all | to_entries[] | select(.value | bad_re(.)) | "detect.all[\(.key)]: not a valid regex"
     else "detect.all: must be a non-empty array of regexes" end),
    (if $d.params == null then empty
     elif ($d.params | type) != "object" then "detect.params: must be an object of param name to regex"
     else $d.params | to_entries[] | .key as $n | .value as $r
       | (if $names | index($n) then empty else "detect.params.\($n): \($n) is not a declared param" end),
         (if ($r | type) != "string" or ($r | length) == 0 then "detect.params.\($n): must be a regex"
          elif ($r | bad_re(.)) then "detect.params.\($n): not a valid regex"
          elif ($r | test("(^|[^\\\\])\\((?!\\?)")) | not then "detect.params.\($n): needs one capture group"
          else empty end)
     end)
  end;
def argv_findings($argv; $cwd; $names; $w):
  $argv | to_entries[] | .key as $j | .value as $s
  | (if ($s | home) then "run.argv[\($j)]: an owner-home path; use a repo-relative path or a command on PATH" else empty end),
    ([$s | scan("\\{([^}]*)\\}") | .[0]] | map(select(. as $n | $names | index($n) | not)) | .[] | "run.argv[\($j)]: {\(.)} is not a declared param"),
    (if $cwd == "repo" and ($s | contains("/")) and ($s | contains("{") | not) and ($s | startswith("-") | not) and ($s | startswith("/") | not) and ($s | home | not)
     then "PATH\t\($w)\trun.argv[\($j)]\t\($s)" else empty end);
def entry_findings($i; $e; $first):
  ("entries[\($i)]" + (if ($e | type) == "object" and ($e.id | type) == "string" then " (\($e.id))" else "" end)) as $w
  | if ($e | type) != "object" then "\($w): must be an object"
    else (if ($e.run | type) == "object" then $e.run else {} end) as $run
      | [($e.params // [])[]? | objects | .name | strings] as $names
      | ( ( ($e | unknown(["id","purpose","terms","run","params","prerequisites","effects","env","output","stop","owner","verified","detect"])),
            (if ($e.id | type) == "string" and ($e.id | test("^[a-z0-9][a-z0-9.-]{0,63}$")) then empty else "id: must be 1-64 lowercase letters, digits, . or -" end),
            (if ($e.id | type) == "string" and $first[$e.id] != $i then "id: duplicate of entries[\($first[$e.id])]" else empty end),
            (if ($e.purpose | type) == "string" and ($e.purpose | length) > 0 and ($e.purpose | length) <= 200 then empty else "purpose: must be a string of 1-200 characters" end),
            opt_strs("terms"; $e.terms),
            (if ($e.run | type) == "object" then (($run | unknown(["argv","cwd"])) | "run.\(.)") else "run: must be an object with argv and cwd" end),
            (if ($run.argv | strs) and ($run.argv | length) > 0 then empty else "run.argv: must be a non-empty array of strings" end),
            (if $run.cwd == "repo" or $run.cwd == "any" then empty else "run.cwd: must be repo or any" end),
            param_findings($e.params),
            detect_findings($e.detect; $names),
            opt_strs("prerequisites"; $e.prerequisites),
            (if ["read-only","writes-local","writes-remote"] | index($e.effects) then empty else "effects: must be one of read-only, writes-local, writes-remote" end),
            opt_strs("env"; $e.env),
            nonempty("output"; $e.output), nonempty("stop"; $e.stop), nonempty("owner"; $e.owner),
            (if ($e.verified | type) != "object" then "verified: must be an object with status and at"
             else (($e.verified | unknown(["status","at"])) | "verified.\(.)"),
               (if ["verified","unverified","stale"] | index($e.verified.status) then empty else "verified.status: must be verified, unverified or stale" end),
               (if ($e.verified.at | type) == "string" and ($e.verified.at | test("^[0-9]{4}-[0-9]{2}-[0-9]{2}$")) then empty else "verified.at: must be a YYYY-MM-DD date" end)
             end)
          ) | "\($w): \(.)" ),
        (if ($run.argv | strs) then argv_findings($run.argv; $run.cwd; $names; $w) | if startswith("PATH\t") then . else "\($w): \(.)" end else empty end)
    end;
if type != "object" then "must be an object with version and entries"
else (unknown(["$schema","version","entries"])),
  (if .version == 1 then empty else "version: must be 1" end),
  (if (.entries | type) != "array" then "entries: must be an array"
   else ([.entries | to_entries[] | select((.value | type) == "object" and (.value.id | type) == "string") | {k: .value.id, i: .key}]
         | group_by(.k) | map({(.[0].k): .[0].i}) | add // {}) as $first
     | .entries | to_entries[] | entry_findings(.key; .value; $first)
   end)
end
| if startswith("PATH\t") then . else "\($label): \(.)" end'

# The repo a catalog's paths are relative to: the folder above its .agentic-workflow/, else the git top level of its folder.
root_of() {
  case "$1" in
    */.agentic-workflow/commands.json) dirname "$(dirname "$1")" ;;
    *) git -C "$(dirname "$1")" rev-parse --show-toplevel 2> /dev/null || true ;;
  esac
}

# findings <file> [paths]: prints the findings; with "paths", also checks the repo paths entries name. 0 when there are none.
findings() {
  local f="$1" with_paths="${2:-}" err out base n=0 w field p
  if ! err="$(jq empty "$f" 2>&1 > /dev/null)"; then
    echo "$f: not valid JSON: $(head -1 <<< "$err")"
    return 1
  fi
  out="$(jq -r --arg label "$f" "$VALIDATE_JQ" "$f")" || { echo "$f: could not be checked"; return 1; }
  base="$(root_of "$f")"
  while IFS= read -r line; do
    [ -n "$line" ] || continue
    case "$line" in
      PATH$'\t'*)
        [ -n "$with_paths" ] && [ -n "$base" ] || continue
        IFS=$'\t' read -r _ w field p <<< "$line"
        [ -e "$base/$p" ] && continue
        echo "$f: $w: $field: $p does not exist in the repo ($base)"; n=$((n + 1)) ;;
      *) echo "$line"; n=$((n + 1)) ;;
    esac
  done <<< "$out"
  [ "$n" = 0 ]
}

# --- loading ---------------------------------------------------------------------------------------------------------
# The entries of every catalog that exists and has no schema finding, as one JSON array, each with its "source".
load() {
  local f src pair
  { [ -n "$repo_cat" ] && [ -f "$repo_cat" ]; } || [ -f "$toolkit" ] || return 3
  for pair in "repo:$repo_cat" "toolkit:$toolkit"; do
    src="${pair%%:*}" f="${pair#*:}"
    [ -n "$f" ] && [ -f "$f" ] || continue
    if ! findings "$f" > /dev/null; then
      echo "aw-commands: skipping $f: it has problems; run aw-commands validate $f" >&2
      continue
    fi
    jq -c --arg s "$src" '.entries[] | . + {source: $s}' "$f"
  done | jq -s 'sort_by(.id, (if .source == "repo" then 0 else 1 end))'
}

where_missing() {
  local t r
  if [ -f "$toolkit" ]; then t="found"; else t="missing"; fi
  if [ -z "$root" ]; then r="none (not in a git repo)"; elif [ -f "$repo_cat" ]; then r="$repo_cat (found)"; else r="$repo_cat (missing)"; fi
  echo "toolkit catalog: $toolkit ($t)"
  echo "repo catalog: $r"
}

no_catalog() {
  echo "aw-commands: no catalog: the toolkit's catalog is missing ($toolkit) and this repo has none (${repo_cat:-not in a git repo})" >&2
  exit 3
}

entries() { # entries: the loaded array, or exit 3 when neither catalog exists
  local all rc=0
  all="$(load)" || rc=$?
  [ "$rc" -ne 3 ] || no_catalog
  printf '%s\n' "$all"
}

line_jq='.[] | "\(.id)  \(.purpose)  [\(.source)]"'

# --- subcommands -----------------------------------------------------------------------------------------------------
cmd_list() {
  local max=50 json=0 all total
  while [ $# -gt 0 ]; do
    case "$1" in
      --max) max="${2:-}"; shift 2 || die "--max needs a number" ;;
      --json) json=1; shift ;;
      *) die "unknown option $1 ($usage)" ;;
    esac
  done
  case "$max" in "" | *[!0-9]*) die "--max is a whole number" ;; esac
  all="$(entries)" || exit $?
  if [ "$json" = 1 ]; then jq --argjson m "$max" '.[:$m]' <<< "$all"; return 0; fi
  jq -r --argjson m "$max" ".[:\$m] | $line_jq" <<< "$all"
  total="$(jq length <<< "$all")"
  [ "$total" -le "$max" ] || echo "($((total - max)) more; aw-commands list --max $total, or aw-commands search <words>)"
}

cmd_search() {
  local json=0 words=() all hits
  while [ $# -gt 0 ]; do
    case "$1" in
      --json) json=1; shift ;;
      -*) die "unknown option $1 ($usage)" ;;
      *) words+=("$1"); shift ;;
    esac
  done
  [ "${#words[@]}" -gt 0 ] || die "search needs words ($usage)"
  all="$(entries)" || exit $?
  # shellcheck disable=SC2016
  hits="$(jq --argjson n "$MAX_HITS" '
    ($ARGS.positional | map(ascii_downcase | select(length > 0))) as $words
    | [ .[] | . as $e
        | ([$e.id, $e.purpose] + ($e.terms // []) | join(" ") | ascii_downcase) as $text
        | ([$e.id] + ($e.terms // []) | map(ascii_downcase)) as $whole
        | ([$words[] | select(. as $w | $text | contains($w))] | length) as $found
        | ([$words[] | select(. as $w | $whole | index($w))] | length) as $exact
        | select($found > 0) | . + {score: ($found + 2 * $exact)} ]
    | sort_by(-.score, .id, .source) | .[:$n]' --args "${words[@]}" <<< "$all")"
  if [ "$(jq length <<< "$hits")" = 0 ]; then
    echo "aw-commands: nothing matched: ${words[*]} (aw-commands list shows every entry)" >&2
    return 1
  fi
  if [ "$json" = 1 ]; then printf '%s\n' "$hits"; else jq -r "$line_jq" <<< "$hits"; fi
}

cmd_show() {
  local id="${1:-}" json=0 sets=() all e
  [ -n "$id" ] || die "show needs an id ($usage)"
  shift
  while [ $# -gt 0 ]; do
    case "$1" in
      --set) case "${2:-}" in *=*) sets+=("$2") ;; *) die "--set takes name=value" ;; esac; shift 2 ;;
      --json) json=1; shift ;;
      *) die "unknown option $1 ($usage)" ;;
    esac
  done
  all="$(entries)" || exit $?
  e="$(jq -c --arg id "$id" 'map(select(.id == $id)) | .[0] // empty' <<< "$all")"
  [ -n "$e" ] || { echo "aw-commands: no entry $id; aw-commands search <words> finds one" >&2; return 1; }
  # shellcheck disable=SC2016
  e="$(jq -c '
    ($ARGS.positional | map(capture("^(?<k>[^=]+)=(?<v>.*)$")) | map({(.k): .v}) | add // {}) as $set
    | . + {invocation: [.run.argv[] | reduce ($set | to_entries[]) as $kv (.; gsub("\\{" + $kv.key + "\\}"; $kv.value))]}
    | . + {missing: [(.params // [])[] | select(.required and ($set[.name] == null)) | .name]}' --args "${sets[@]+"${sets[@]}"}" <<< "$e")"
  if [ "$json" = 1 ]; then printf '%s\n' "$e"; return 0; fi
  # shellcheck disable=SC2016
  jq -r --arg root "${root:-}" '
    def q: ([39] | implode) as $a | if test("^[A-Za-z0-9_./:=@%+,{}-]+$") then . else $a + gsub($a; $a + "\\" + $a + $a) + $a end;
    def list($f; $v): if ($v // []) | length > 0 then "\($f): \($v | join(", "))" else empty end;
    "id: \(.id) [\(.source)]",
    "purpose: \(.purpose)",
    "run: \(.invocation | map(q) | join(" "))",
    "cwd: \(.run.cwd)" + (if .run.cwd == "repo" and $root != "" and .source == "repo" then " (from \($root))" else "" end),
    (if (.params // []) | length > 0 then "params:", ((.params)[] | "  \(.name) (\(if .required then "required" else "optional" end)): \(.description)") else empty end),
    (.missing[] | "needs --set \(.)=<value>"),
    list("terms"; .terms), list("prerequisites"; .prerequisites),
    "effects: \(.effects)", list("env"; .env),
    "output: \(.output)", "stop: \(.stop)", "owner: \(.owner)",
    "verified: \(.verified.status) \(.verified.at)"' <<< "$e"
}

cmd_validate() {
  local files=() f bad=0
  if [ $# -gt 0 ]; then
    [ -f "$1" ] || die "no such file: $1"
    files=("$1")
  else
    [ -n "$repo_cat" ] && [ -f "$repo_cat" ] && files+=("$repo_cat")
    [ -f "$toolkit" ] && files+=("$toolkit")
    [ "${#files[@]}" -gt 0 ] || no_catalog
  fi
  for f in "${files[@]}"; do
    if findings "$f" paths; then echo "ok: $f ($(jq '.entries | length' "$f") entries)"; else bad=1; fi
  done
  return "$bad"
}

case "${1:-}" in
  list) shift; cmd_list "$@" ;;
  search) shift; cmd_search "$@" ;;
  show) shift; cmd_show "$@" ;;
  validate) shift; cmd_validate "$@" ;;
  -h | --help | help)
    sed -n '2,/^set -u$/p' "$self" | sed '$d' | sed 's/^# \{0,1\}//'
    echo
    where_missing ;;
  "") die "no subcommand ($usage)" ;;
  *) die "unknown subcommand $1 ($usage)" ;;
esac
