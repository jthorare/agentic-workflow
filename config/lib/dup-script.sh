#!/usr/bin/env bash
# config/lib/dup-script.sh — the duplicate-script gate's work after its pre-filter (#4221 PR 2; docs/hooks/duplicate-script.md).
# Sourced by config/hooks/dup-script-gate.sh, never executed; defines DS_* constants and ds_* functions. Bash 3.2, jq.
# No model, no network, no git: the catalogs are found by walking up from the call's cwd, their index is cached per
# revision, and one jq run reads the script text, matches it and builds the text the agent sees. The script text is never
# stored; the state file holds counters, the advised entry ids and the exceptions.

DS_VERSION=1
DS_POLICY="duplicate-script/1"
DS_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DS_LOCK=""

ds_failopen() {
  (
    set +eu
    [ -f "$DS_LIB_DIR/failopen.sh" ] && . "$DS_LIB_DIR/failopen.sh" && aw_failopen dup-script-gate "$1"
  ) > /dev/null 2>&1 || true
}

ds_hash() { local h; if command -v sha256sum > /dev/null 2>&1; then h="$(printf '%s' "$1" | sha256sum)"; else h="$(printf '%s' "$1" | shasum -a 256)"; fi; printf '%s\n' "${h%% *}"; }
ds_stat() { stat -c '%s.%Y' "$1" 2> /dev/null || stat -f '%z.%m' "$1" 2> /dev/null || echo 0; }

# ds_mode: prints block or advisory. config/lib/policy.sh's policy_mode when that file sits beside this one.
ds_mode() {
  local m=""
  if [ -f "$DS_LIB_DIR/policy.sh" ]; then
    . "$DS_LIB_DIR/policy.sh" 2> /dev/null
    command -v policy_mode > /dev/null 2>&1 && m="$(policy_mode "$DS_POLICY" 2> /dev/null)"
  else
    # TODO(#4257): policy_mode — read the variable directly until config/lib/policy.sh (the P0 PR) is merged.
    m="${AW_POLICY_DUPLICATE_SCRIPT_1_MODE:-}"
  fi
  if [ "$m" = block ]; then echo block; else echo advisory; fi
}

# ds_catalogs <cwd>: sets DS_REPO_CAT (the first .agentic-workflow/commands.json walking up from cwd) and DS_TK_CAT
# (AW_COMMANDS_TOOLKIT when set, else the installed build's commands.json, else this checkout's config/commands.toolkit.json).
ds_catalogs() {
  local d="$1"
  DS_REPO_CAT="" DS_TK_CAT=""
  while [ -n "$d" ] && [ "$d" != / ]; do
    [ -f "$d/.agentic-workflow/commands.json" ] && { DS_REPO_CAT="$d/.agentic-workflow/commands.json"; break; }
    d="${d%/*}"
  done
  if [ -n "${AW_COMMANDS_TOOLKIT:-}" ]; then
    [ -f "$AW_COMMANDS_TOOLKIT" ] && DS_TK_CAT="$AW_COMMANDS_TOOLKIT"
  elif [ -f "$DS_STATE_ROOT/bin/cs/current/commands.json" ]; then
    DS_TK_CAT="$DS_STATE_ROOT/bin/cs/current/commands.json"
  elif [ -f "$DS_LIB_DIR/../commands.toolkit.json" ]; then
    DS_TK_CAT="$DS_LIB_DIR/../commands.toolkit.json"
  fi
}

# ds_index: sets DS_INDEX, the cached flat index of both catalogs (repo entries first; an id in both is the repo's), keyed
# by both files' path, size and mtime. Returns 1 when neither catalog exists.
ds_index() {
  local key f tmp files=()
  [ -n "$DS_REPO_CAT" ] && files+=("$DS_REPO_CAT")
  [ -n "$DS_TK_CAT" ] && files+=("$DS_TK_CAT")
  [ "${#files[@]}" -gt 0 ] || return 1
  key="$(ds_hash "$DS_REPO_CAT $( [ -n "$DS_REPO_CAT" ] && ds_stat "$DS_REPO_CAT") $DS_TK_CAT $( [ -n "$DS_TK_CAT" ] && ds_stat "$DS_TK_CAT")")"
  DS_INDEX="$DS_DIR/index-${key:0:16}.json"
  [ -s "$DS_INDEX" ] && return 0
  mkdir -p "$DS_DIR" 2> /dev/null && chmod 700 "$DS_DIR" 2> /dev/null || return 1
  tmp="$DS_INDEX.tmp.$$"
  jq -c -s '
    [ .[] | (.entries // [])[]? | select(type == "object" and (.id | type) == "string" and ((.run.argv // null) | type) == "array" and (.run.argv | length) > 0) ]
    | reduce .[] as $e ([]; if any(.[]; .id == $e.id) then . else . + [$e] end)
    | map({id, purpose: (.purpose // ""), terms: ((.terms // []) | map(ascii_downcase)), argv: .run.argv,
           params: [ (.params // [])[]? | {name, required: (.required // false)} ],
           prereq: ((.prerequisites // [])[0] // ""), detect: (.detect // null)})' "${files[@]}" > "$tmp" 2> /dev/null \
    && [ -s "$tmp" ] && mv -f "$tmp" "$DS_INDEX" || { rm -f "$tmp"; return 1; }
  for f in "$DS_DIR"/index-*.json; do [ "$f" = "$DS_INDEX" ] || rm -f "$f"; done
  return 0
}

# ds_match <payload>: one jq run. Prints one line of fields split by \x1f:
#   kind (unambiguous|uncertain|none) reason id purpose invocation argv0 prereq mark(true|false) exception-reason tool path
# A Write's text is its content; a Bash call's is the whole command (a heredoc body included). Matching, per entry:
#   the call runs the entry (its argv[0], or argv[0] argv[1] when argv[1] is a word) -> reuse: that entry is skipped;
#   a Write to the entry's own entrypoint (the same base name) -> canonical: the whole call is no duplicate;
#   every detect.all regex matches and every required param is found -> unambiguous, with the filled invocation;
#   detect matches but a param is missing, or 3 or more of its id and terms are words of the text -> uncertain.
ds_match() {
  printf '%s' "$1" | jq -r --slurpfile ix "$DS_INDEX" '
    def clean: gsub("[\u0000-\u001f\u007f]"; " ");
    def esc: gsub("(?<c>[.+*?()\\[\\]{}|^$\\\\])"; "\\\\\(.c)");
    def shq: if test("^[A-Za-z0-9_./:=@%+,-]+$") then . else @sh end;
    def firstcap($r): [ match($r; "i").captures[]? | .string | select(. != null and . != "") ][0];
    if type != "object" then error("not an object") else . end
    | (.tool_name // "") as $tool
    | (.tool_input // {}) as $in
    | (if $tool == "Write" then ($in.content // "") else ($in.command // "") end | tostring | .[0:65536]) as $text
    | (if $tool == "Write" then ($in.file_path // "" | tostring) else "" end) as $path
    | ($path | split("/") | last // "" | sub("\\.(sh|bash|py|mjs|js)$"; "")) as $pbase
    | ($text | ascii_downcase) as $low
    | ([$low | scan("[a-z0-9][a-z0-9-]*")] | unique) as $words
    | (" " + ($low | gsub("[^a-z0-9-]+"; " ")) + " ") as $spaced
    | ([$text | match("#[ \\t]*aw-dup-ok:([^\\n]*)"; "g") | .captures[0].string] | last) as $ovr
    | ($ovr != null) as $mark
    | (($ovr // "") | clean | gsub("^\\s+|\\s+$"; "") | .[0:200]) as $reason
    | [ $ix[0][] | . as $e
        | ($e.argv[0] | split("/") | last) as $a0
        | ([$a0] + (if (($e.argv[1] // "-") | test("^[-{]")) then [] else [$e.argv[1]] end) | map(esc) | join("\\s+")) as $runre
        | ($a0 | sub("\\.(sh|bash|py|mjs|js)$"; "")) as $a0base
        | if $tool == "Write" and $pbase != "" and $pbase == $a0base then {kind: "canonical"}
          elif ($text | test("(^|[\\s;|&(`$])" + $runre + "($|[\\s;|&)])")) then {kind: "reuse"}
          else
            (if $e.detect != null and ($e.detect.all | all(. as $r | $text | test($r; "i"))) then true else false end) as $det
            | ([$e.params[] | .name as $n | {name: $n, required, value: (if $e.detect.params[$n] then ($text | firstcap($e.detect.params[$n])) else null end)}]) as $vals
            | ([$vals[] | select(.required and .value == null)] | length == 0) as $filled
            | ([([$e.id] + $e.terms) | unique[] | select(if test(" ") then ($spaced | contains(" " + . + " ")) else (. as $t | $words | index($t)) end)] | length) as $score
            | if $det and $filled then
                {kind: "unambiguous", reason: "dup.unambiguous", score: 100,
                 inv: ([$e.argv[] | . as $a | reduce $vals[] as $v ($a; if $v.value then gsub("\\{" + $v.name + "\\}"; $v.value) else . end)] | map(shq) | join(" "))}
              elif $det then {kind: "uncertain", reason: "dup.uncertain.param", score: 50, inv: ""}
              elif $score >= 3 then {kind: "uncertain", reason: "dup.uncertain.terms", score: $score, inv: ""}
              else {kind: "none"} end
          end
        | . + {id: $e.id, purpose: $e.purpose, argv0: $a0, prereq: $e.prereq} ] as $all
    | (if any($all[]; .kind == "canonical") then {kind: "none", reason: "dup.canonical"}
       else ([$all[] | select(.kind == "unambiguous" or .kind == "uncertain")] | sort_by(-.score, .id) | .[0]) // {kind: "none", reason: "dup.none"} end) as $best
    | [$best.kind, ($best.reason // ""), ($best.id // ""), ($best.purpose // "" | clean), ($best.inv // ""), ($best.argv0 // ""),
       ($best.prereq // "" | clean), ($mark | tostring), $reason, $tool, ($path | clean)] | join("\u001f")
  ' 2> /dev/null
}

ds_lock() {
  local lk="$1.lock" i=0
  while ! mkdir "$lk" 2> /dev/null; do
    if [ -f "$lk/pid" ] && ! kill -0 "$(cat "$lk/pid" 2> /dev/null)" 2> /dev/null; then rm -f "$lk/pid"; rmdir "$lk" 2> /dev/null; continue; fi
    i=$((i + 1)); [ "$i" -ge 40 ] && return 1
    sleep 0.05
  done
  printf '%s\n' "$$" > "$lk/pid" 2> /dev/null
  DS_LOCK="$lk"
}
ds_unlock() { [ -n "$DS_LOCK" ] || return 0; rm -f "$DS_LOCK/pid"; rmdir "$DS_LOCK" 2> /dev/null; DS_LOCK=""; }

# ds_update <jq program> [jq args...]: under the lock, read the state (missing or unreadable = fresh), apply, write whole.
ds_update() {
  local prog="$1" tmp cur
  shift
  mkdir -p "$DS_DIR" 2> /dev/null && chmod 700 "$DS_DIR" 2> /dev/null || return 1
  ds_lock "$DS_FILE" || return 1
  cur='null'
  if [ -f "$DS_FILE" ]; then
    cur="$(jq -c --argjson v "$DS_VERSION" --arg p "$DS_POLICY" 'if type == "object" and .version == $v and .policy == $p then . else null end' "$DS_FILE" 2> /dev/null)" || cur='null'
    [ -n "$cur" ] || cur='null'
  fi
  tmp="$DS_FILE.tmp.$$"
  if printf '%s' "$cur" | jq -c --argjson v "$DS_VERSION" --arg p "$DS_POLICY" --arg sid "$DS_SID" --arg provider "${AW_PROVIDER:-claude}" \
      --arg iso "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$@" '
      (. // {version: $v, policy: $p, session: $sid, provider: $provider, advised: {}, exceptions: [],
             counters: {suggestions: 0, wouldDeny: 0, denies: 0, advisories: 0, deduped: 0, notInstalled: 0, exceptions: 0}})
      | .updatedAt = $iso | '"$prog" > "$tmp" 2> /dev/null && [ -s "$tmp" ] && mv -f "$tmp" "$DS_FILE"; then
    ds_unlock; return 0
  fi
  rm -f "$tmp"; ds_unlock; return 1
}

ds_advise() { jq -nc --arg m "$1" '{hookSpecificOutput: {hookEventName: "PreToolUse", additionalContext: $m}}'; }

# ds_main <payload>: the decision. Exit 0 allows (with or without advice on stdout), 2 denies (reason on stderr).
ds_main() {
  local input="$1" row kind reason id purpose inv argv0 prereq mark ereason tool path mode msg cwd
  local RE_SID='"session_id"[[:space:]]*:[[:space:]]*"([^"]*)"'
  [[ $input =~ $RE_SID ]] || return 0
  DS_SID="${BASH_REMATCH[1]}"
  [[ $DS_SID =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || return 0
  command -v jq > /dev/null 2>&1 || { ds_failopen jq-missing; return 0; }
  DS_STATE_ROOT="${AW_STATE_DIR:-${HOME:-}/.agentic-workflow}"
  DS_DIR="$DS_STATE_ROOT/duplicate-script"
  DS_FILE="$DS_DIR/$DS_SID.json"
  umask 077
  cwd="$(printf '%s' "$input" | jq -r 'if type == "object" then (.cwd // "") else error("x") end' 2> /dev/null)" \
    || { ds_failopen input-unparsable; return 0; }
  [ -n "$cwd" ] || cwd="$PWD"
  ds_catalogs "$cwd"
  ds_index || { ds_failopen no-catalog; return 0; }
  row="$(ds_match "$input")" || { ds_failopen match-failed; return 0; }
  [ -n "$row" ] || return 0
  IFS=$'\x1f' read -r kind reason id purpose inv argv0 prereq mark ereason tool path <<< "$row"
  case "$kind" in unambiguous | uncertain) ;; *) return 0 ;; esac

  # The exception: a reason is required; it is recorded and the call goes on.
  if [ "$mark" = true ] && [ -n "$ereason" ]; then
    ds_update '.exceptions = ((.exceptions // []) + [{id: $id, tool: $tool, path: $path, reason: $r, at: $iso}] | .[-50:])
               | .counters.exceptions += 1' --arg id "$id" --arg tool "$tool" --arg path "$path" --arg r "$ereason" \
      || ds_failopen state-write
    return 0
  fi

  # A helper that is not installed is never a reason to deny.
  if ! command -v "$argv0" > /dev/null 2>&1; then
    if jq -e --arg k "ni:$id" '.advised[$k] != null' "$DS_FILE" > /dev/null 2>&1; then return 0; fi
    ds_update '.advised["ni:" + $id] = $iso | .counters.notInstalled += 1' --arg id "$id" || ds_failopen state-write
    ds_advise "duplicate-script: \`$id\` does this ($purpose), but \`$argv0\` is not on PATH: ${prereq:-install it}."
    return 0
  fi

  if [ "$kind" = uncertain ]; then
    if jq -e --arg k "$id" '.advised[$k] != null' "$DS_FILE" > /dev/null 2>&1; then
      ds_update '.counters.deduped += 1' || ds_failopen state-write
      return 0
    fi
    ds_update '.advised[$id] = $iso | .counters.advisories += 1' --arg id "$id" || ds_failopen state-write
    ds_advise "duplicate-script: \`$id\` may already do this ($purpose): \`aw-commands show $id\`. Shown once per session."
    return 0
  fi

  msg="duplicate-script: this script redoes \`$id\` ($purpose). Run it instead: \`$inv\` (\`aw-commands show $id\`). If it can't do what you need, add \`# aw-dup-ok: <why>\` and go on."
  [ "$mark" = true ] && msg="$msg An \`# aw-dup-ok:\` exception needs a reason after the colon; this one is empty."
  mode="$(ds_mode)"
  if [ "$mode" = block ]; then
    ds_update '.counters.denies += 1 | .counters.suggestions += 1' || ds_failopen state-write
    printf 'duplicate-script: blocked (policy %s, block mode).\n%s\n' "$DS_POLICY" "$msg" >&2
    return 2
  fi
  ds_update '.counters.wouldDeny += 1 | .counters.suggestions += 1' || ds_failopen state-write
  ds_advise "$msg"
  return 0
}
