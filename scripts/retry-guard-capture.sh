#!/usr/bin/env bash
# scripts/retry-guard-capture.sh (issue 4382): checks the retry guard against real Codex and Cursor hook payloads.
#
#   retry-guard-capture.sh [--providers codex,cursor] [--out DIR] [--from DIR]
#
# The retry guard hashes a shell call in the pre hook and again in the failure hook, and the two hashes must match
# (docs/hooks/retry-guard.md). Its Codex and Cursor fixtures are hand-written. This script:
#   1. runs one failing shell call (`aw-nosuch-tool --version`) through the real host, headless: `codex exec` and
#      `cursor-agent -p`, each in a throwaway folder. This uses the host's own sign-in and one short model turn.
#   2. The installed aw:* hooks fire as usual. AW_HOOK_CAPTURE_DIR makes their adapter (config/hooks/adapters/common.sh)
#      save each raw payload. Nothing in the host's config is edited, so Codex's hook trust stays as it is.
#   3. feeds the pre event and the failure event through the checkout's adapter and the guard's call hash
#      (config/lib/retry-guard.sh), and prints one line per provider: PASS when both hash the same, FAIL with the reason.
#   4. prints the keys where each real payload differs from the guard's fixture (config/hooks/tests/fixtures/).
#
#   --providers   which hosts (default codex,cursor)
#   --out DIR     also save the pair per provider, sanitized ($HOME and the throwaway folder replaced), as
#                 <provider>-pre.json and <provider>-failure.json: the raw payloads to add under tests/fixtures/replay/
#   --from DIR    skip step 1 and check captures already in DIR (<provider>.<n>.json, as the tap names them)
#
# Needs jq, and the installed hooks to carry the tap: run ./setup.sh from a checkout that has this script first.
# AW_CAPTURE_TIMEOUT caps each host run (seconds, default 300).
# Exit: 0 every provider PASS, 1 a FAIL, 2 usage, a host missing or nothing captured.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ADAPTERS="$ROOT/config/hooks/adapters"
FIX="$ROOT/config/hooks/tests/fixtures"
MARKER="aw-nosuch-tool"
PROMPT="Run this exact shell command once with your shell tool, then stop and report its output: $MARKER --version"

usage() { sed -n '4p' "$0" | sed 's/^# *//' >&2; exit 2; }
PROVIDERS="codex cursor"
FROM=""
OUT=""
while [ $# -gt 0 ]; do
  case "$1" in
    --providers) [ $# -ge 2 ] || usage; PROVIDERS="$(printf '%s' "$2" | tr ',' ' ')"; shift 2 ;;
    --from) [ $# -ge 2 ] || usage; FROM="$2"; shift 2 ;;
    --out) [ $# -ge 2 ] || usage; OUT="$2"; shift 2 ;;
    -h | --help) usage ;;
    *) echo "retry-guard-capture: unknown argument: $1" >&2; usage ;;
  esac
done
for p in $PROVIDERS; do
  case "$p" in codex | cursor) ;; *) echo "retry-guard-capture: unknown provider: $p (codex or cursor)" >&2; exit 2 ;; esac
done
command -v jq > /dev/null 2>&1 || { echo "retry-guard-capture: jq is missing" >&2; exit 2; }
[ -z "$FROM" ] || [ -d "$FROM" ] || { echo "retry-guard-capture: --from: no such folder: $FROM" >&2; exit 2; }

TMP="$(mktemp -d)"
trap 'find "$TMP" -delete 2> /dev/null' EXIT
printf '#!/usr/bin/env bash\ncat > "$NORM_FILE"\n' > "$TMP/norm.sh"
chmod +x "$TMP/norm.sh"
WS="$TMP/ws"
mkdir -p "$WS"
[ -z "$OUT" ] || mkdir -p "$OUT" || exit 2

bounded() {
  local t="${AW_CAPTURE_TIMEOUT:-300}"
  if command -v timeout > /dev/null 2>&1; then timeout "$t" "$@"
  elif command -v gtimeout > /dev/null 2>&1; then gtimeout "$t" "$@"
  else "$@"; fi
}

# preflight <provider> <pre event> <failure event>: prints why the host's installed hooks can't capture the pair, or
# nothing. Each event needs an entry that runs adapters/<provider>.sh, and that adapter's common.sh needs the tap.
preflight() {
  local p="$1" file adapter ev
  case "$p" in
    codex) file="${CODEX_HOOKS_FILE:-${CODEX_HOME:-$HOME/.codex}/hooks.json}" ;;
    cursor) file="${CURSOR_HOOKS_FILE:-$HOME/.cursor/hooks.json}" ;;
  esac
  [ -f "$file" ] || { echo "no hooks file at $file: run ./setup.sh"; return; }
  for ev in "$2" "$3"; do
    adapter="$(jq -r --arg ev "$ev" --arg a "adapters/$p.sh" '
      [.hooks[$ev]? // [] | .. | strings | select(contains($a))] | first // empty' "$file" 2> /dev/null)"
    [ -n "$adapter" ] || { echo "no aw hook on $ev in $file: run scripts/install-retry-guard.sh --provider $p"; return; }
  done
  adapter="${adapter#\'}"; adapter="${adapter%%\'*}"; adapter="${adapter%% *}"
  grep -q aw_adapter_capture "$(dirname "$adapter")/common.sh" 2> /dev/null \
    || echo "the installed adapter $(dirname "$adapter")/common.sh has no capture tap: run scripts/install-retry-guard.sh --provider $p"
}

# capture <provider>: runs the host once with the tap on; returns 1 when its CLI is missing.
capture() {
  local p="$1" cap="$TMP/raw"
  mkdir -p "$cap"
  case "$p" in
    codex)
      command -v codex > /dev/null 2>&1 || return 1
      (cd "$WS" && AW_HOOK_CAPTURE_DIR="$cap" bounded codex exec --skip-git-repo-check -C "$WS" "$PROMPT") < /dev/null > "$TMP/codex.log" 2>&1 ;;
    cursor)
      command -v cursor-agent > /dev/null 2>&1 || return 1
      (cd "$WS" && AW_HOOK_CAPTURE_DIR="$cap" bounded cursor-agent -p --force --trust --workspace "$WS" "$PROMPT") < /dev/null > "$TMP/cursor.log" 2>&1 ;;
  esac
  return 0
}

# normalized <provider> <file>: the payload as the canonical hook sees it, through the checkout's adapter.
normalized() {
  rm -f "$TMP/n.json"
  AW_HOOK_CAPTURE_DIR="" NORM_FILE="$TMP/n.json" bash "$ADAPTERS/$1.sh" "$TMP/norm.sh" < "$2" > /dev/null 2>&1
  cat "$TMP/n.json" 2> /dev/null
}

# call_hash <normalized payload>: "<hash> <canonical input>", by the guard's own rg_call and rg_hash.
call_hash() {
  (
    # shellcheck source=../config/lib/retry-guard.sh
    . "$ROOT/config/lib/retry-guard.sh"
    rg_call "$1" || exit 1
    rg_hash
    printf '%s %s\n' "$RG_HASH" "$RG_CANON"
  )
}

# keydiff <label> <capture> <fixture>: one line naming the top-level and tool_input keys only one of them has.
keydiff() {
  local d
  d="$(jq -rn --slurpfile a "$2" --slurpfile b "$3" '
    def ks: [keys_unsorted[]] + ((.tool_input // {}) | if type == "object" then [keys_unsorted[] | "tool_input." + .] else [] end);
    ($a[0] | ks) as $x | ($b[0] | ks) as $y
    | [($x - $y | sort | join(", ")), ($y - $x | sort | join(", "))]
    | select(.[0] != "" or .[1] != "")
    | "only in the capture: \(if .[0] == "" then "-" else .[0] end); only in the fixture: \(if .[1] == "" then "-" else .[1] end)"')"
  [ -z "$d" ] || printf '  %s vs %s: %s\n' "$1" "${3#"$ROOT"/}" "$d"
}

# sanitize <file>: $HOME and the throwaway folder replaced in every string, user_email dropped.
sanitize() {
  jq --arg home "${HOME:-/nonexistent-home}" --arg ws "$WS" '
    walk(if type == "string" then (split($ws) | join("/tmp/aw-capture")) | (split($home) | join("/home/user")) else . end)
    | if has("user_email") then .user_email = null else . end' "$1"
}

RC=0
worst() { [ "$1" -gt "$RC" ] && RC="$1"; return 0; }
SRC="$FROM"
[ -n "$SRC" ] || SRC="$TMP/raw"

for p in $PROVIDERS; do
  case "$p" in
    codex) PRE_EV="PreToolUse"; FAIL_EV="PostToolUse"; PRE_FIX="$FIX/codex/pretooluse-bash-nosuch.json"; FAIL_FIX="$FIX/codex/posttooluse-bash-exit127.json" ;;
    cursor) PRE_EV="preToolUse"; FAIL_EV="postToolUseFailure"; PRE_FIX="$FIX/cursor/pretooluse-shell-nosuch.json"; FAIL_FIX="$FIX/cursor/post-tool-use-failure-shell.json" ;;
  esac
  if [ -z "$FROM" ]; then
    why="$(preflight "$p" "$PRE_EV" "$FAIL_EV")"
    [ -z "$why" ] || { echo "$p: NO CAPTURE ($why)"; worst 2; continue; }
    capture "$p" || { echo "$p: NO CAPTURE (its CLI is not on PATH)"; worst 2; continue; }
  fi
  pre=""; fail=""; pre_n=""; fail_n=""; ok_post=0
  for f in "$SRC/$p".*.json; do
    [ -f "$f" ] || continue
    grep -qF "$MARKER" "$f" || continue
    n="$(normalized "$p" "$f")"
    [ "$(printf '%s' "$n" | jq -r '.tool_name // empty' 2> /dev/null)" = "Bash" ] || continue
    ev="$(jq -r '.hook_event_name // empty' "$f" 2> /dev/null)"
    if [ "$ev" = "$PRE_EV" ] && [ -z "$pre" ]; then
      pre="$f"; pre_n="$n"
    elif [ "$ev" = "$FAIL_EV" ] && [ -z "$fail" ]; then
      if [ "$p" = codex ] && [ "$(printf '%s' "$n" | jq -r '.aw_tool_failed // false')" != "true" ]; then ok_post=1; continue; fi
      fail="$f"; fail_n="$n"
    fi
  done
  if [ -z "$pre" ] && [ -z "$fail" ] && [ "$ok_post" = 0 ]; then
    echo "$p: NO CAPTURE (no hook payload names $MARKER: are the aw:retry-* hooks installed from this checkout? see $([ -n "$FROM" ] && echo "$FROM" || echo "the $p log"))"
    [ -z "$FROM" ] && [ -s "$TMP/$p.log" ] && tail -n 5 "$TMP/$p.log" | sed 's/^/  | /'
    worst 2; continue
  fi
  if [ -z "$fail" ]; then
    echo "$p: FAIL (no failure event: the shell call did not come back as $FAIL_EV$([ "$ok_post" = 1 ] && echo ' with a nonzero exit'))"; worst 1; continue
  fi
  if [ -z "$pre" ]; then
    echo "$p: FAIL (no pre event: $PRE_EV never fired for the shell call)"; worst 1; continue
  fi
  a="$(call_hash "$pre_n")"; b="$(call_hash "$fail_n")"
  tool="$(jq -r '.tool_name // ""' "$pre")"
  if [ -n "$a" ] && [ "${a%% *}" = "${b%% *}" ]; then
    echo "$p: PASS (call hash ${a:0:12}, host tool $tool)"
  else
    echo "$p: FAIL (call hashes differ: pre ${a:0:12}, failure ${b:0:12})"
    printf '  pre input:     %s\n  failure input: %s\n' "${a#* }" "${b#* }"
    worst 1
  fi
  keydiff "pre" "$pre" "$PRE_FIX"
  keydiff "failure" "$fail" "$FAIL_FIX"
  if [ -n "$OUT" ]; then
    sanitize "$pre" > "$OUT/$p-pre.json" && sanitize "$fail" > "$OUT/$p-failure.json" \
      && echo "  saved $OUT/$p-pre.json and $OUT/$p-failure.json (review them before adding under tests/fixtures/replay/)"
  fi
done
exit "$RC"
