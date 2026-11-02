#!/usr/bin/env bash
# launcher-lib.sh — the bash reader of launcher.json (spec docs/superpowers/specs/2026-10-15-sindri-session-launcher-design.md,
# SL-1), sourced by scripts/meta-launch.sh and scripts/meta-lane.sh. Needs jq.
#
# The file: $AW_LAUNCHER_CONFIG, else $state_dir/launcher.json, else none (the built-in defaults: the three built-in agents
# from config/launcher/builtins.json, every lane on claude-code, and for aw-meta-launch the Warp viewer it always had).
# The reader promises less than the C# one (spec section 4): it refuses unknown keys, unknown placeholders and wrong JSON
# types for the fields aw-meta-launch uses, and reads its key lists and the placeholder set from schemas/launcher.schema.json's
# x-aw-keys block. Every other rule is `sindri launch show`'s. A refusal is "SND-LAUNCH-011: <reason>" on stdout of
# launcher_validate; launcher_load prints it with the file name on stderr.
#
# Public functions (all read the globals launcher_file, LAUNCHER_DOC, LAUNCHER_CAT that launcher_load sets):
#   launcher_load <state_dir>       resolve and validate the file, build the merged catalog; 1 on a refusal
#   launcher_validate <file>        print the first refusal, return 1; return 0 when the reader accepts the file
#   launcher_viewer_kind            warp | exec-template | print (warp with no file)
#   launcher_apply_team_agent <team.json> <meta-lane>
#                                   team.json "agent" sets every lane, including the meta lane, and replaces this
#                                   launch's meta.lanesAgent and meta.lanes. Absent or null changes nothing. Returns 2
#                                   when the value is not an agent id.
#   launcher_lane_agent <lane> <meta-lane>   the lane's agent id (the meta lane's is meta.lanes.<meta-lane>, else claude-code: lanesAgent never reaches it)
#   launcher_agent_ok <id>          0 when the catalog has a usable entry, else prints SND-LAUNCH-003/-011 and returns 1
#   launcher_agent_field <id> <f>   one catalog field as text (empty for null/absent); arrays and objects as JSON
#   launcher_expand <json-array> <cwd> <title> <name> <seed> <sessionId> <bin> [<cmd words...>]
#                                   the array's elements expanded into NUL-separated arguments (spec section 4)

LAUNCHER_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LAUNCHER_ROOT="$(cd "$LAUNCHER_LIB_DIR/.." && pwd)"
LAUNCHER_SCHEMA="$LAUNCHER_ROOT/schemas/launcher.schema.json"
LAUNCHER_BUILTINS="$LAUNCHER_ROOT/config/launcher/builtins.json"
LAUNCHER_TERMINALS="$LAUNCHER_ROOT/config/launcher/terminals.json"
launcher_file=""; LAUNCHER_DOC='{"schema":1}'; LAUNCHER_CAT='{}'; LAUNCHER_STRICT=0

# The checks, as one jq program over the file; prints one refusal per line.
# shellcheck disable=SC2016
LAUNCHER_CHECKS='
  $S[0]["x-aw-keys"] as $K
  | . as $f
  | def unknown($allowed; $where): ((keys - $allowed)[]? | "unknown key \"\(.)\" in \($where)");
    def argv_errors($where):
      if type != "array" or any(.[]; type != "string") then "\($where) must be an array of strings"
      else (.[] | select(. != "{cmd...}")
            | [scan("\\{[^{}]*\\}")][] | select(. as $p | ($K.placeholders | index([$p])) == null)
            | "unknown placeholder \(.) in \($where)"),
           (.[] | select(. != "{cmd...}" and contains("{cmd...}")) | "{cmd...} must be a whole element of \($where), not part of \"\(.)\"")
      end;
    def str_or_null($where): if . == null or type == "string" then empty else "\($where) must be a string or null" end;
  [ if ($f | type) != "object" then "the file must be a JSON object"
    else
      ($f | unknown($K.root; "the top level")),
      (if ($f.schema | type) != "number" or $f.schema != 1 then "schema must be 1" else empty end),
      ($f | if has("viewer") then .viewer
              | if type != "object" then "viewer must be an object"
                else unknown($K.viewer; "viewer"),
                  (if (.kind | type) != "string" or (.kind as $k | $K.viewerKinds | any(. == $k) | not)
                     then "viewer.kind must be one of \($K.viewerKinds | join(", "))" else empty end),
                  (if (.kind == "warp" or .kind == "print") and (has("name") or has("argv"))
                     then "viewer.kind \(.kind) takes no name or argv" else empty end),
                  (if has("name") and (.name | type) != "string" then "viewer.name must be a string" else empty end),
                  (if has("argv") then .argv | argv_errors("viewer.argv") else empty end)
                end
            else empty end),
      ($f | if has("host") then .host
              | if type != "object" then "host must be an object"
                else unknown($K.host; "host"),
                  (if (.kind | type) != "string" or (.kind as $k | $K.hostKinds | any(. == $k) | not)
                     then "host.kind must be one of \($K.hostKinds | join(", "))" else empty end)
                end
            else empty end),
      ($f | if has("agents") then .agents
              | if type != "object" then "agents must be an object"
                else to_entries[] | .key as $id | .value
                  | if type != "object" then "agents.\($id) must be an object"
                    else (unknown($K.agent; "agents.\($id)")),
                      (if has("override") and (.override | type) != "boolean" then "agents.\($id).override must be true or false" else empty end),
                      (if has("label") and (.label | type) != "string" then "agents.\($id).label must be a string" else empty end),
                      (if has("bin") and (.bin | type) != "string" then "agents.\($id).bin must be a string" else empty end),
                      (if has("open") then .open | argv_errors("agents.\($id).open") else empty end),
                      (if has("resume") and .resume != null then .resume | argv_errors("agents.\($id).resume") else empty end),
                      (if has("sessionId") and (((.sessionId | type) != "string") or (.sessionId as $m | $K.sessionIdModes | any(. == $m) | not))
                         then "agents.\($id).sessionId must be one of \($K.sessionIdModes | join(", "))" else empty end),
                      (if has("sessionIdPattern") then .sessionIdPattern | str_or_null("agents.\($id).sessionIdPattern") else empty end),
                      (if has("processName") then .processName | str_or_null("agents.\($id).processName") else empty end),
                      (if has("transcripts") then .transcripts | str_or_null("agents.\($id).transcripts") else empty end),
                      (if has("reportedId") and .reportedId != null then .reportedId
                         | if type != "object" then "agents.\($id).reportedId must be an object or null"
                           else unknown($K.reportedId; "agents.\($id).reportedId"),
                             (if has("hook") and (.hook | type) != "boolean" then "agents.\($id).reportedId.hook must be true or false" else empty end),
                             (if has("newestFile") then .newestFile | str_or_null("agents.\($id).reportedId.newestFile") else empty end),
                             (if has("capture") then .capture | str_or_null("agents.\($id).reportedId.capture") else empty end)
                           end
                       else empty end)
                    end
                end
            else empty end),
      ($f | if has("meta") then .meta
              | if type != "object" then "meta must be an object"
                else unknown($K.meta; "meta"),
                  (if has("lanesAgent") and (((.lanesAgent | type) != "string") or (.lanesAgent | test($K.agentIdPattern) | not))
                     then "meta.lanesAgent must be an agent id (\($K.agentIdPattern))" else empty end),
                  (if has("lanes") then .lanes
                     | if type != "object" then "meta.lanes must be an object of lane name to agent id"
                       else to_entries[] | select((.value | type) != "string" or (.value | test($K.agentIdPattern) | not))
                         | "meta.lanes.\(.key) must be an agent id (\($K.agentIdPattern))"
                       end
                   else empty end)
                end
            else empty end)
    end
  ] | .[0] // empty
'

# launcher_validate <file>: prints "SND-LAUNCH-011: <reason>" and returns 1, or returns 0.
launcher_validate() {
  local out
  if ! out="$(jq -e . "$1" 2>&1 > /dev/null)"; then
    printf 'SND-LAUNCH-011: does not parse as JSON: %s\n' "$(printf '%s' "$out" | head -1)"; return 1
  fi
  out="$(jq -r --slurpfile S "$LAUNCHER_SCHEMA" "$LAUNCHER_CHECKS" "$1" 2>&1)" || { printf 'SND-LAUNCH-011: the reader failed: %s\n' "$out"; return 1; }
  [ -z "$out" ] && return 0
  printf 'SND-LAUNCH-011: %s\n' "$out"; return 1
}

# launcher_load <state_dir>: sets launcher_file ("" when there is none), LAUNCHER_DOC, LAUNCHER_CAT, LAUNCHER_STRICT.
launcher_load() {
  local state_dir="$1" f msg claude="${AW_META_CLAUDE:-}"
  command -v jq > /dev/null || { echo "launcher: jq is not installed" >&2; return 1; }
  [ -f "$LAUNCHER_SCHEMA" ] && [ -f "$LAUNCHER_BUILTINS" ] || { echo "launcher: schemas/launcher.schema.json or config/launcher/builtins.json is missing beside $LAUNCHER_LIB_DIR" >&2; return 1; }
  launcher_file=""; LAUNCHER_DOC='{"schema":1}'; LAUNCHER_STRICT=0
  if [ -n "${AW_LAUNCHER_CONFIG:-}" ]; then
    f="$AW_LAUNCHER_CONFIG"
    [ -f "$f" ] || { echo "launcher: AW_LAUNCHER_CONFIG names $f, which does not exist" >&2; return 1; }
  else f="$state_dir/launcher.json"; fi
  if [ -f "$f" ]; then
    if ! msg="$(launcher_validate "$f")"; then echo "launcher: $f: $msg" >&2; return 1; fi
    launcher_file="$f"; LAUNCHER_DOC="$(cat "$f")"; LAUNCHER_STRICT=1
  fi
  # The merged catalog: the built-ins, an override replacing the fields it names (AW_META_CLAUDE, set, replaces the
  # claude-code entry's bin and ranks above the file), an added entry as it is. A built-in id without "override": true
  # is dropped and carries the reason.
  # shellcheck disable=SC2016
  LAUNCHER_CAT="$(jq -c --slurpfile B "$LAUNCHER_BUILTINS" --arg claude "$claude" '
    ($B[0].agents) as $B | (.agents // {}) as $A
    | reduce ($A | keys[]) as $id ($B;
        if ($B | has($id)) then
          (if $A[$id].override == true then .[$id] = ($B[$id] + ($A[$id] | del(.override)))
           else .[$id] = {"_error": "\($id) is built in: add \"override\": true to replace it"} end)
        else .[$id] = ($A[$id] | del(.override)) end)
    | if $claude != "" and (.["claude-code"] | has("_error") | not) then .["claude-code"].bin = $claude else . end
  ' <<< "$LAUNCHER_DOC")" || { echo "launcher: could not build the catalog" >&2; return 1; }
}

launcher_viewer_kind() { jq -r '.viewer.kind // "warp"' <<< "$LAUNCHER_DOC"; }

# team.json "agent" is the team's choice of catalog agent. launcher.json is machine-wide (the viewer, and overrides of
# a built-in binary), so a team that wants Cursor or Codex names it here instead of shipping its own launcher file.
# The field wins over meta.lanesAgent and meta.lanes, including the meta lane: lanesAgent alone never reaches that pane.
launcher_apply_team_agent() { # <team.json> <meta-lane>
  [ -f "$1" ] || return 0
  local verdict id
  verdict="$(jq -r '
    if (.agent == null) then "unset"
    elif (.agent | type) != "string" or (.agent | test("^[a-z][a-z0-9-]{1,31}$") | not) then "bad"
    else "ok" end
  ' "$1")" || { echo "launcher: $1: could not read agent" >&2; return 2; }
  case "$verdict" in
    unset) return 0 ;;
    ok) ;;
    *) echo "launcher: $1: agent must be an agent id (^[a-z][a-z0-9-]{1,31}$), not $(jq -c .agent "$1")" >&2; return 2 ;;
  esac
  id="$(jq -r .agent "$1")"
  LAUNCHER_DOC="$(jq -c --arg a "$id" --arg m "$2" '.meta = ((.meta // {}) | .lanesAgent = $a | .lanes = {($m): $a})' <<< "$LAUNCHER_DOC")"
}

launcher_lane_agent() { # lane meta-lane
  # The meta's own pane stays Claude Code unless meta.lanes names it (spec section 9): lanesAgent is the lanes' default, never the
  # meta's, so a launcher.json written for Codex lanes under a Claude meta is refused by meta-launch.sh (one agent per team), not
  # silently turned into a Codex meta.
  if [ "$1" = "$2" ]; then jq -r --arg l "$1" '.meta.lanes[$l] // "claude-code"' <<< "$LAUNCHER_DOC"; return; fi
  jq -r --arg l "$1" '.meta.lanes[$l] // .meta.lanesAgent // "claude-code"' <<< "$LAUNCHER_DOC"
}

launcher_agent_ok() { # id: 0 when launchable
  local err
  if [ "$(jq -r --arg id "$1" 'has($id)' <<< "$LAUNCHER_CAT")" != true ]; then
    echo "SND-LAUNCH-003: agent '$1' is not in the catalog: add it to launcher.json or pick another agent" >&2; return 1
  fi
  err="$(jq -r --arg id "$1" '.[$id]._error // empty' <<< "$LAUNCHER_CAT")"
  if [ -n "$err" ]; then echo "SND-LAUNCH-011: $err" >&2; return 1; fi
  if [ "$(jq -r --arg id "$1" '.[$id] | (.bin | type) == "string" and (.open | type) == "array"' <<< "$LAUNCHER_CAT")" != true ]; then
    echo "SND-LAUNCH-011: agent '$1' needs bin and open" >&2; return 1
  fi
}

launcher_agent_field() { # id field
  jq -r --arg id "$1" --arg f "$2" '.[$id][$f] | if . == null then empty elif type == "string" then . else tojson end' <<< "$LAUNCHER_CAT"
}

# launcher_expand <json-array> cwd title name seed sessionId bin [cmd...]: NUL-separated arguments on stdout. Each element
# is one argument: {cmd...} (a whole element) is spliced as the cmd words, an element that is just {seed} is dropped when the
# seed is empty (unless LAUNCHER_KEEP_EMPTY_SEED=1: the prompt file exists but is empty), and every other placeholder is replaced in one pass (a value is never expanded again). With a launcher file
# (LAUNCHER_STRICT), an element that is exactly {title}, {name}, {sessionId} or {seed} and expands to a value starting
# with "-" is refused.
launcher_expand() {
  local tpl="$1" cwd="$2" title="$3" name="$4" seed="$5" sid="$6" bin="$7"; shift 7
  local cmd; cmd="$(printf '%s\0' "$@" | jq -Rs '[split("\u0000")[:-1][]]' 2> /dev/null)" || return 1
  [ "$#" = 0 ] && cmd='[]'
  # shellcheck disable=SC2016
  jq -j --argjson cmd "$cmd" --argjson strict "$LAUNCHER_STRICT" --argjson keep "${LAUNCHER_KEEP_EMPTY_SEED:-0}" --arg cwd "$cwd" --arg title "$title" --arg name "$name" \
    --arg seed "$seed" --arg sessionId "$sid" --arg bin "$bin" '
    {bin: $bin, cwd: $cwd, title: $title, name: $name, seed: $seed, sessionId: $sessionId, mcpConfig: ""} as $v
    | .[]
    | if . == "{cmd...}" then $cmd[]
      elif . == "{seed}" and $seed == "" and $keep == 0 then empty
      else
        (if $strict == 1 and (. as $e | ["{title}", "{name}", "{sessionId}", "{seed}"] | index([$e]))
            and ($v[.[1:-1]] | startswith("-"))
         then error("SND-LAUNCH-011: \(.) expands to a value starting with \"-\"") else . end)
        | gsub("\\{(?<n>[A-Za-z]+)\\}"; $v[.n] // "{\(.n)}")
      end
    | ., "\u0000"' <<< "$tpl"
}
