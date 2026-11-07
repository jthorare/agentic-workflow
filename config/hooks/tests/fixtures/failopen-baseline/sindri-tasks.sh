#!/usr/bin/env bash
# aw:sindri-tasks — UserPromptSubmit hook. Tells a session once that a Sindri fix task is waiting on the
# branch it works on (spec 2026-11-04-sindri-pr-review-page-design.md §8.2). Sindri keeps one file per head:
# $AW_SINDRI_HOME/fix-tasks/<sha256 of "<head-owner>/<head-repo>:<branch>">.json, {"tasks":[{"item","pr"}]}.
# Prints one line per task not yet shown to this session (ids and numbers only, never finding text) and
# records it in fix-tasks/seen/<session id>. Seen files older than 7 days are pruned on the way.
# Starts no .NET process. Silent in child sessions (AW_JUDGE_CHILD, AW_SINDRI_CHILD), without a git origin
# on GitHub, with nothing new, or on any error. Exits 0 always; bounded at 500 ms overall.
[ -n "${AW_JUDGE_CHILD:-}${AW_SINDRI_CHILD:-}" ] && exit 0
command -v jq > /dev/null 2>&1 || exit 0
INPUT="$(cat 2> /dev/null || true)"
[ -n "$INPUT" ] || exit 0
SID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2> /dev/null)" || exit 0
case "$SID" in '' | *[!A-Za-z0-9-]*) exit 0 ;; esac

OUT_FILE="$(mktemp 2> /dev/null)" || exit 0
IDS_FILE="$(mktemp 2> /dev/null)" || { rm -f "$OUT_FILE"; exit 0; }
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
    [[ "$item" =~ ^[0-9A-HJKMNP-TV-Z]{26}$ ]] || continue
    [[ "$pr" =~ ^[0-9]+$ ]] || continue
    [ -f "$ft/seen/$SID" ] && grep -qxF "$item" "$ft/seen/$SID" 2> /dev/null && continue
    printf 'Fix task %s on #%s is waiting. `sindri review tasks` shows it.\n' "$item" "$pr" >> "$OUT_FILE"
    printf '%s\n' "$item" >> "$IDS_FILE"
  done < <(jq -r '(.tasks // []) | if type == "array" then .[] else empty end | select(type == "object") | [.item, .pr] | @tsv' "$ft/$hash.json" 2> /dev/null)
  return 0
}

( work ) > /dev/null 2>&1 &
PID=$!
TICKS=10
while kill -0 "$PID" 2> /dev/null; do
  if [ "$TICKS" -le 0 ]; then
    kill -9 "$PID" 2> /dev/null
    exit 0
  fi
  TICKS=$((TICKS - 1))
  sleep 0.05
done
wait "$PID" 2> /dev/null || exit 0
[ -s "$OUT_FILE" ] || exit 0
HOME_DIR="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"
mkdir -p "$HOME_DIR/fix-tasks/seen" 2> /dev/null && chmod 700 "$HOME_DIR/fix-tasks" "$HOME_DIR/fix-tasks/seen" 2> /dev/null
cat "$IDS_FILE" >> "$HOME_DIR/fix-tasks/seen/$SID" 2> /dev/null
cat "$OUT_FILE"
exit 0
