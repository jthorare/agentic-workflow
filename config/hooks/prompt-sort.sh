#!/usr/bin/env bash
# aw:prompt-sort — UserPromptSubmit hook (prompt sorter, Plan C). Sorts the real
# user prompt with `judge prompt-sort` (ONE batched Jev call) and prints a short
# scaffold note ONLY when a scaffold fires (all scaffold switches ship off, so
# by default this records a decision and prints nothing).
#
# Fails open: always exits 0; any failure, timeout (hard kill of the whole script
# after AW_PROMPT_SORT_BUDGET_MS of wall-clock time, default 4000) or bad judge output prints nothing.
# Machine text and slash commands never reach node (same prefixes as
# turn-origin.sh and judge/src/prompt-sort/tier1.ts). Never prints skill names:
# Prism's prism-route hook already does keyword skill routing.
set -uo pipefail
# HOOK-FAILOPEN: record why this hook printed nothing (config/lib/failopen.sh, installed at <hooks>/lib/failopen.sh).
# Logging only, in a subshell with its output discarded: it never changes this hook's exit code or output.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen prompt-sort "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# HOOK-WATCHDOG (#3778): the host timeout is a backstop; the hook ends itself first. The whole script runs in a child bash
# that a watchdog kills after a WALL-CLOCK budget (AW_PROMPT_SORT_BUDGET_MS, default 4000 ms), counted from the hook's start and covering every
# command, not a count of sleep passes. On a spent budget it writes one failopen `timeout` row and the hook prints nothing.
# The output is printed only at the very end, so a kill never leaves a half answer. A normal run ends the watchdog.
if [ -z "${AW_HOOK_WD_INNER:-}" ] && [ -z "${AW_JUDGE_CHILD:-}" ]; then
  _aw_ms="${AW_PROMPT_SORT_BUDGET_MS:-4000}"; case "$_aw_ms" in '' | *[!0-9]*) _aw_ms=4000 ;; esac
  # Kill a process and everything under it, by pid only: _aw_kill_tree from config/lib/kill-tree.sh, installed beside
  # failopen.sh. Without the lib (an older install) the fallback below is the lib's line word for word
  # (config/lib/tests/kill-tree.test.sh), so a timed-out script still leaves no judge running.
  for _aw_f in "$(cd "$(dirname "${BASH_SOURCE[0]}")" 2> /dev/null && pwd)/lib/kill-tree.sh" "$(cd "$(dirname "${BASH_SOURCE[0]}")" 2> /dev/null && pwd)/../lib/kill-tree.sh"; do
    if [ -f "$_aw_f" ]; then . "$_aw_f" 2> /dev/null && break; fi
  done
  type _aw_kill_tree > /dev/null 2>&1 || _aw_kill_tree() { local _c _k; kill -STOP "$1" 2> /dev/null; _k="$(pgrep -P "$1" 2> /dev/null)"; kill -9 "$1" 2> /dev/null; for _c in $_k; do _aw_kill_tree "$_c"; done; return 0; }
  # The script's temp files go in a dir this bash owns and removes on every path: a killed script runs no EXIT trap.
  _aw_tmp="$(mktemp -d 2> /dev/null)" || _aw_tmp=""
  AW_HOOK_WD_INNER=1 AW_HOOK_WD_TMP="$_aw_tmp" "$BASH" "${BASH_SOURCE[0]}" "$@" 0<&0 &
  _aw_child=$!
  (
    set +eu
    sleep "$((_aw_ms / 1000)).$(printf '%03d' "$((_aw_ms % 1000))")" 2> /dev/null || exit 0
    [ -z "$_aw_tmp" ] || : > "$_aw_tmp/.wd-fired"
    failopen timeout
    _aw_kill_tree "$_aw_child"
  ) > /dev/null 2>&1 < /dev/null &
  _aw_wd=$!
  trap '_aw_kill_tree "$_aw_child"; kill "$_aw_wd" 2> /dev/null; [ -z "$_aw_tmp" ] || rm -rf "$_aw_tmp"; exit 0' TERM
  _aw_rc=0
  { wait "$_aw_child"; } 2> /dev/null || _aw_rc=$?
  # A watchdog that fired is left to finish killing the tree under the script; any other is ended here.
  if [ -z "$_aw_tmp" ] || [ ! -e "$_aw_tmp/.wd-fired" ]; then
    pkill -P "$_aw_wd" 2> /dev/null
    kill "$_aw_wd" 2> /dev/null
  fi
  [ -z "$_aw_tmp" ] || rm -rf "$_aw_tmp"
  if [ "$_aw_rc" -eq 137 ]; then _aw_rc=0; fi
  exit "$_aw_rc"
fi
# The images a hook sends the judge (config/lib/image-paths.sh, installed beside failopen.sh). Without it the hook sends none, as before.
for _aw_f in "$(cd "$(dirname "${BASH_SOURCE[0]}")" 2> /dev/null && pwd)/lib/image-paths.sh" "$(cd "$(dirname "${BASH_SOURCE[0]}")" 2> /dev/null && pwd)/../lib/image-paths.sh"; do
  if [ -f "$_aw_f" ]; then . "$_aw_f" 2> /dev/null && break; fi
done
type aw_images_attach > /dev/null 2>&1 || { aw_images_attach() { printf '%s' "$1"; }; aw_image_paths() { :; }; aw_image_files() { cat > /dev/null; }; }
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || exit 0
command -v jq >/dev/null 2>&1 || exit 0
PROMPT="$(printf '%s' "$INPUT" | jq -r '.prompt // empty' 2>/dev/null)" || exit 0
[ -n "$PROMPT" ] || exit 0
SESSION_ID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2>/dev/null)"

case "$PROMPT" in
  "<"*|"[Request interrupted"*|"Another Claude session sent a message"*|"Base directory for this skill"*|"Caveat:"*|"This session is being continued"*) exit 0 ;;
esac
# "/bugFixOrchestrator FRN-1", "/clear": slash word then space or end. A pasted
# absolute path ("/Users/joi/x") has "/" after the first segment and passes.
if grep -qE '^[[:space:]]*/[A-Za-z][A-Za-z0-9:_-]*([[:space:]]|$)' <<<"$PROMPT"; then exit 0; fi

command -v judge >/dev/null 2>&1 || exit 0
PAYLOAD="$(jq -nc --rawfile p <(printf '%s' "$PROMPT") --arg s "$SESSION_ID" '{prompt:$p, sessionId:$s}' 2>/dev/null)" || exit 0
OUT_FILE="$(mktemp "${AW_HOOK_WD_TMP:-${TMPDIR:-/tmp}}/aw-hook.XXXXXX" 2>/dev/null)" || exit 0
IN_FILE="$(mktemp "${AW_HOOK_WD_TMP:-${TMPDIR:-/tmp}}/aw-hook.XXXXXX" 2>/dev/null)" || { rm -f "$OUT_FILE"; exit 0; }
trap 'rm -f "$OUT_FILE" "$IN_FILE"' EXIT

PAYLOAD="$(aw_images_attach "$PAYLOAD" "$(aw_image_paths "$PROMPT" prompt-sort)")"
printf '%s' "$PAYLOAD" > "$IN_FILE" 2>/dev/null || exit 0
# judge is a DIRECT child of this bash (stdin from a file, no subshell or pipe around it), so the watchdog's
# tree kill reaches it on a spent budget. Wrapped in ( … ) it was a grandchild and outlived the hook (#3778).
AW_SESSION_ID="$SESSION_ID" judge prompt-sort < "$IN_FILE" > "$OUT_FILE" 2>/dev/null &
PID=$!
# The wall-clock budget is the watchdog above: it kills judge, then this script, and logs `timeout`.
# A crashed or failed judge never contributes output; the prompt proceeds.
wait "$PID" 2>/dev/null || exit 0
CONTEXT="$(jq -r '.context // empty' "$OUT_FILE" 2>/dev/null)" || exit 0
[ -n "$CONTEXT" ] && printf '%s\n' "$CONTEXT"
exit 0
