#!/usr/bin/env bash
# aw:sindri-tasks — UserPromptSubmit hook. Tells a session once that a Sindri fix task is waiting on the
# branch it works on (spec 2026-11-04-sindri-pr-review-page-design.md §8.2). Sindri keeps one file per head:
# $AW_SINDRI_HOME/fix-tasks/<sha256 of "<head-owner>/<head-repo>:<branch>">.json, {"tasks":[{"item","pr"}]}.
# Prints one line per task not yet shown to this session (ids and numbers only, never finding text) and
# records it in fix-tasks/seen/<session id>. Seen files older than 7 days are pruned on the way.
# Starts no .NET process. Silent in child sessions (AW_JUDGE_CHILD, AW_SINDRI_CHILD), without a git origin
# on GitHub, with nothing new, or on any error. Exits 0 always; bounded at 1500 ms of wall-clock time overall (AW_SINDRI_TASKS_BUDGET_MS), whole script included.
# HOOK-FAILOPEN: only the budget timeout is logged (config/lib/failopen.sh, installed at <hooks>/lib/failopen.sh).
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen sindri-tasks "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
[ -n "${AW_JUDGE_CHILD:-}${AW_SINDRI_CHILD:-}" ] && exit 0
# HOOK-WATCHDOG (#3778): the host timeout is a backstop; the hook ends itself first. The whole script runs in a child bash
# that a watchdog kills after a WALL-CLOCK budget (AW_SINDRI_TASKS_BUDGET_MS, default 1500 ms), counted from the hook's start and covering every
# command, not a count of sleep passes. On a spent budget it writes one failopen `timeout` row and the hook prints nothing.
# The output is printed only at the very end, so a kill never leaves a half answer. A normal run ends the watchdog.
if [ -z "${AW_HOOK_WD_INNER:-}" ] && [ -z "${AW_SINDRI_CHILD:-}" ]; then
  _aw_ms="${AW_SINDRI_TASKS_BUDGET_MS:-1500}"; case "$_aw_ms" in '' | *[!0-9]*) _aw_ms=1500 ;; esac
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
command -v jq > /dev/null 2>&1 || exit 0
INPUT="$(cat 2> /dev/null || true)"
[ -n "$INPUT" ] || exit 0
SID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2> /dev/null)" || exit 0
case "$SID" in '' | *[!A-Za-z0-9-]*) exit 0 ;; esac

OUT_FILE="$(mktemp "${AW_HOOK_WD_TMP:-${TMPDIR:-/tmp}}/aw-hook.XXXXXX" 2> /dev/null)" || exit 0
IDS_FILE="$(mktemp "${AW_HOOK_WD_TMP:-${TMPDIR:-/tmp}}/aw-hook.XXXXXX" 2> /dev/null)" || { rm -f "$OUT_FILE"; exit 0; }
trap 'rm -f "$OUT_FILE" "$IDS_FILE"' EXIT

work() {
  local home ft url path head hash line item pr
  home="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"
  ft="$home/fix-tasks"
  [ -d "$ft/seen" ] && find "$ft/seen" -type f -mtime +7 -delete 2> /dev/null
  url="$(git remote get-url origin 2> /dev/null)" || return 0
  # https://github.com/o/r(.git), git@github.com:o/r(.git), ssh://git@github.com/o/r(.git)
  path="$(printf '%s' "$url" | sed -nE 's#^(https?://([^@/]+@)?|ssh://([^@/]+@)?|git@)github\.com[:/]+([^/]+/[^/]+)$#\4#p')"
  path="${path%.git}"
  path="${path%/}"
  case "$path" in */*/* | '' | */) return 0 ;; esac
  head="$(git symbolic-ref --short -q HEAD 2> /dev/null)" || return 0
  [ -n "$head" ] || return 0
  head="$(printf '%s' "$path" | tr '[:upper:]' '[:lower:]'):$head"
  if command -v sha256sum > /dev/null 2>&1; then
    hash="$(printf '%s' "$head" | sha256sum | cut -d' ' -f1)"
  else
    hash="$(printf '%s' "$head" | shasum -a 256 | cut -d' ' -f1)"
  fi
  [ -f "$ft/$hash.json" ] || return 0
  while IFS=$'\t' read -r item pr; do
    [[ "$item" =~ ^ITM-[0-9a-f]{32}$ ]] || continue
    [[ "$pr" =~ ^[0-9]+$ ]] || continue
    [ -f "$ft/seen/$SID" ] && grep -qxF "$item" "$ft/seen/$SID" 2> /dev/null && continue
    printf 'Fix task %s on #%s is waiting. `sindri review tasks` shows it.\n' "$item" "$pr" >> "$OUT_FILE"
    printf '%s\n' "$item" >> "$IDS_FILE"
  done < <(jq -r '(.tasks // []) | if type == "array" then .[] else empty end | select(type == "object") | [.item, .pr] | @tsv' "$ft/$hash.json" 2> /dev/null)
  return 0
}

# work runs in this bash: the watchdog bounds it now, and on a spent budget it kills the whole tree, git included (#3778).
work > /dev/null 2>&1
[ -s "$OUT_FILE" ] || exit 0
HOME_DIR="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"
mkdir -p "$HOME_DIR/fix-tasks/seen" 2> /dev/null && chmod 700 "$HOME_DIR/fix-tasks" "$HOME_DIR/fix-tasks/seen" 2> /dev/null
cat "$IDS_FILE" >> "$HOME_DIR/fix-tasks/seen/$SID" 2> /dev/null
cat "$OUT_FILE"
exit 0
