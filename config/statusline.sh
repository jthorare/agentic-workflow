#!/usr/bin/env bash

# Claude Code statusline — reads session JSON from stdin
# Two-line output: dimmed header row + color-coded values
# Spec: docs/superpowers/specs/2026-03-21-statusline-config-design.md
#
# Column priority (left → right, leftmost always survive tier drops):
#   5h Usage | 7d Usage | Context | Model | Branch | Cost | Time | Cache | API | Lines | Live
#
# Width detection: ancestor process tty → terminal_width.d/<tty> → (no tty) legacy terminal_width → stty → $COLUMNS → 200
# Tiers (total visible chars, approx):
#   ≥156: FULL+Live   — all columns plus Live, branch×15, full ctx bar
#   ≥141: MEDIUM+Live — no Lines, plus Live (Live outranks Lines), branch×12
#   ≥116: FULL      — all columns except Live, branch×15, full ctx bar
#   ≥101: MEDIUM    — no Lines/Live, branch×12, full ctx bar
#   ≥78:  NARROW    — no Lines/Cache/API, 7d % only (no reset), narrow ctx, branch×12
#   ≥65:  COMPACT   — 5h % only (no reset), narrow ctx, model, branch×10, cost, time (64 chars)
#   <65:  COMPACT-S — same as COMPACT but drops Time column (54 chars)

INPUT=$(cat)

# Width detection, first match wins (AW_STATUSLINE_DEBUG=1 prints the source to stderr).
# Everything is per window: several Claude Code sessions run at once and share no state.
#   1. ancestor-tty: size of the controlling tty of the nearest ancestor process that has one,
#      i.e. the terminal THIS Claude Code session runs in.
#   2. terminal_width.d/<tty>: that same tty's file from shell-integration.sh.
#   3. no ancestor tty at all: legacy global ~/.claude/terminal_width (old installs only;
#      the shell integration no longer writes it), then stty </dev/tty, $COLUMNS, 200.
# Test overrides: AW_STATUSLINE_TTY (tty name, empty = none) replaces the ancestor lookup;
# AW_STATUSLINE_TTY_COLS_CMD (run with the tty name as $1) replaces the stty size read.
ancestor_tty() {
  local pid=$PPID hop tty out
  for hop in 1 2 3 4 5 6 7 8; do
    [ -n "$pid" ] && [ "$pid" -gt 1 ] 2>/dev/null || return 1
    out=$(ps -o ppid=,tty= -p "$pid" 2>/dev/null) || return 1
    tty=$(awk '{print $2}' <<<"$out")
    if [ -n "$tty" ] && [ "$tty" != "??" ] && [ "$tty" != "-" ]; then
      printf '%s' "$tty"
      return 0
    fi
    pid=$(awk '{print $1}' <<<"$out")
  done
  return 1
}
tty_cols() { # tty name -> columns
  if [ -n "${AW_STATUSLINE_TTY_COLS_CMD+x}" ]; then
    sh -c "$AW_STATUSLINE_TTY_COLS_CMD" _ "$1" 2>/dev/null
  elif [ "$(uname)" = "Darwin" ]; then
    stty -f "/dev/$1" size 2>/dev/null | awk '{print $2}'
  else
    stty -F "/dev/$1" size 2>/dev/null | awk '{print $2}'
  fi
}
valid_cols() { [ -n "$1" ] && [ "$1" -gt 0 ] 2>/dev/null; }

COLS=""; COLS_SRC=""
if [ -n "${AW_STATUSLINE_TTY+x}" ]; then ATTY=$AW_STATUSLINE_TTY; else ATTY=$(ancestor_tty); fi
if [ -n "$ATTY" ]; then
  COLS=$(tty_cols "$ATTY"); COLS_SRC=ancestor-tty
  if ! valid_cols "$COLS"; then
    # Brief pause so the shell's WINCH trap can write this tty's file after a resize
    # (Claude Code re-renders immediately); only this fallback needs it.
    sleep 0.05
    COLS=$(cat "$HOME/.claude/terminal_width.d/$ATTY" 2>/dev/null); COLS_SRC=terminal_width.d
  fi
else
  COLS=$(cat "$HOME/.claude/terminal_width" 2>/dev/null); COLS_SRC=terminal_width-legacy
fi
if ! valid_cols "$COLS"; then
  COLS_SRC=stty-dev-tty
  COLS=$({ stty size </dev/tty; } 2>/dev/null | awk '{print $2}')
fi
if ! valid_cols "$COLS"; then COLS_SRC=COLUMNS; COLS=${COLUMNS:-}; fi
if ! valid_cols "$COLS"; then COLS_SRC=default; COLS=200; fi
[ -n "${AW_STATUSLINE_DEBUG:-}" ] && echo "statusline width: $COLS (source: $COLS_SRC)" >&2

# --- Live column (judge health) ---
# `judge ✓`. The statusline runs often and must never wait on judge, so it only READS a
# cache and, when that is older than the TTL, starts a detached background refresh and
# prints the cached value right away.
LIVE_W=36                                    # visible width of the Live column
LIVE_TTL="${AW_STATUSLINE_LIVE_TTL:-15}"     # seconds before the cache is refreshed
STATE_DIR="${AW_STATE_DIR:-$HOME/.agentic-workflow}"

# Width in CHARACTERS, whatever the locale. bash 3.2 and printf count bytes in the C locale
# (what Claude Code gives the statusline), so count bytes minus UTF-8 continuation bytes.
vwidth() { printf '%s' "$1" | LC_ALL=C tr -d '\200-\277' | wc -c | tr -d ' '; }

# Pads $1 with spaces to $2 characters (no cut; wider input is returned as is).
vpad() {
  local n=$(( $2 - $(vwidth "$1") ))
  printf '%s' "$1"
  [ "$n" -gt 0 ] && printf '%*s' "$n" ''
  return 0
}

# Cuts $1 to at most $2 characters, never inside a multibyte sequence.
vcut() {
  local s="$1" max="$2" i=0 chars=0 b
  local -a bytes
  # shellcheck disable=SC2207
  bytes=($(printf '%s' "$s" | LC_ALL=C od -An -v -tu1))
  while [ "$i" -lt "${#bytes[@]}" ]; do
    b=${bytes[$i]}
    # a byte 128-191 continues the previous character; any other byte starts a new one
    if [ "$b" -lt 128 ] || [ "$b" -ge 192 ]; then
      [ "$chars" -ge "$max" ] && break
      chars=$((chars + 1))
    fi
    i=$((i + 1))
  done
  printf '%s' "$s" | LC_ALL=C head -c "$i"
}

# Finds an aw CLI: PATH first, then the installer's directory.
find_bin() {
  command -v "$1" 2>/dev/null && return 0
  [ -x "$HOME/.local/bin/$1" ] && echo "$HOME/.local/bin/$1"
}

# Health of the judge as `<ok|degraded|down> <failures24h>`. Shells out to `judge health`
# rather than reading decisions.sqlite directly, so every judge-health consumer (this, the
# SessionStart hook) agrees by construction. Only the background refresh
# calls this; the statusline itself reads the cached result.
judge_health() {
  local bin out status failures
  bin="$(find_bin judge)" || { echo "down 0"; return; }
  out="$("$bin" health 2>/dev/null)" || { echo "down 0"; return; }
  status="$(echo "$out" | jq -r '.status // empty' 2>/dev/null)"
  failures="$(echo "$out" | jq -r '.failures24h // 0' 2>/dev/null)"
  case "$status" in
    ok|degraded) echo "$status ${failures:-0}" ;;
    *) echo "down 0" ;;
  esac
}

# `judge ✓ 12` / `judge ⚠ 12` / `judge ✗ 12`. Args: status, this session's judge call count ("" = unknown).
judge_segment() {
  local glyph
  case "$1" in
    ok) glyph="✓" ;;
    degraded) glyph="⚠" ;;
    *) glyph="✗" ;;
  esac
  echo "judge $glyph${2:+ $2}"
}

# Background refresh of the judge-health cache file $1. Never prints; atomic write. The same
# lock and negative caching as live_refresh: a failing judge is not re-run on every render.
judge_refresh() {
  local cache="$1" lock="$1.lock" hs hf tmp now
  mkdir -p "$(dirname "$cache")" 2>/dev/null || return 0
  find "$lock" -maxdepth 0 -mmin +1 -exec rmdir {} \; 2>/dev/null
  mkdir "$lock" 2>/dev/null || return 0
  tmp="$cache.$$.tmp"
  now="$(date +%s)"
  read -r hs hf <<<"$(judge_health)"
  case "$hf" in ''|*[!0-9]*) hf=0 ;; esac
  jq -n --argjson at "$now" --arg hs "$hs" --argjson hf "$hf" '{at: $at, judge: $hs, failures: $hf}' >"$tmp" 2>/dev/null
  [ -s "$tmp" ] || printf '{"at":%s}\n' "$now" >"$tmp"
  mv -f "$tmp" "$cache" 2>/dev/null
  rmdir "$lock" 2>/dev/null
  return 0
}

# The Live column: the judge segment, read from a cached `judge health` (refreshed in the
# background, so a render never waits on it); `--` when there is no
# judge binary or nothing cached yet.
judge_only_text() {
  local cache="$STATE_DIR/statusline/judge-health.json" at="" hs="" now age
  find_bin judge >/dev/null || { echo "--"; return; }
  if [ -f "$cache" ]; then
    eval "$(jq -r '
      "at=\(.at // "" | tostring | @sh)",
      "hs=\(.judge // "" | tostring | @sh)"' "$cache" 2>/dev/null)"
  fi
  now=$(date +%s)
  case "$at" in ''|*[!0-9]*) age=999999 ;; *) age=$((now - at)) ;; esac
  if [ "$age" -ge "$LIVE_TTL" ] 2>/dev/null; then
    ( judge_refresh "$cache" </dev/null >/dev/null 2>&1 & )
  fi
  case "$hs" in ok|degraded|down) judge_segment "$hs" "" ;; *) echo "--" ;; esac
}

# Prints the Live column text for session $1; `--` whenever there is nothing to show.
live_text() {
  local sid="$1"
  # The id becomes part of a path elsewhere: accept only plain file-name characters.
  [[ "$sid" =~ ^[A-Za-z0-9._-]+$ ]] || { echo "--"; return; }
  judge_only_text
}

# Tier selection: Live (36 wide) outranks Lines, so it shows from 141 columns up.
if [ "$COLS" -ge 156 ] 2>/dev/null; then TIER=full-live
elif [ "$COLS" -ge 141 ] 2>/dev/null; then TIER=medium-live
elif [ "$COLS" -ge 116 ] 2>/dev/null; then TIER=full
elif [ "$COLS" -ge 101 ] 2>/dev/null; then TIER=medium
elif [ "$COLS" -ge 78 ] 2>/dev/null; then TIER=narrow
elif [ "$COLS" -ge 65 ] 2>/dev/null; then TIER=compact
else TIER=compact-s
fi
LIVE_HDR_SUF=" │ $(vpad Live "$LIVE_W")"

# Fallback for empty or invalid input
if [ -z "$INPUT" ] || ! echo "$INPUT" | jq empty 2>/dev/null; then
  LIVE_DASH=" │ $(vpad -- "$LIVE_W")"
  case "$TIER" in
  full-live|full)
    L_H=""; L_V=""; [ "$TIER" = full-live ] && { L_H="$LIVE_HDR_SUF"; L_V="$LIVE_DASH"; }
    printf '%b\n' "\033[2m5h Usage  │ 7d Usage  │ Context         │ Model      │ Branch          │ Cost    │ Time    │ Cache │ API  │ Lines    ${L_H}\033[0m"
    printf '%b\n' "--        │ --        │ ░░░░░░░░░░ --   │ --         │ --              │ --      │ --      │ --    │ --   │ --       ${L_V}"
    ;;
  medium-live|medium)
    L_H=""; L_V=""; [ "$TIER" = medium-live ] && { L_H="$LIVE_HDR_SUF"; L_V="$LIVE_DASH"; }
    printf '%b\n' "\033[2m5h Usage  │ 7d Usage  │ Context         │ Model      │ Branch       │ Cost    │ Time    │ Cache │ API  ${L_H}\033[0m"
    printf '%b\n' "--        │ --        │ ░░░░░░░░░░ --   │ --         │ --           │ --      │ --      │ --    │ --   ${L_V}"
    ;;
  narrow)
    printf '%b\n' '\033[2m5h Usage  │ 7d    │ Context    │ Model      │ Branch       │ Cost    │ Time    \033[0m'
    printf '%b\n' '--        │ --    │ ░░░░░ --   │ --         │ --           │ --      │ --      '
    ;;
  *)
    printf '%b\n' '\033[2m5h    │ Context    │ Model      │ Branch     │ Cost    │ Time    \033[0m'
    printf '%b\n' '--    │ ░░░░░ --   │ --         │ --         │ --      │ --      '
    ;;
  esac
  exit 0
fi

# Single jq call — extract all fields at once via eval-safe shell assignments.
# Why eval/@sh instead of @tsv/IFS: bash 3.2 on macOS does not preserve
# non-whitespace IFS characters in herestrings, causing @tsv tab-split to fail.
# Safety: every field is piped through @sh before reaching eval.
eval "$(echo "$INPUT" | jq -r '
  "MODEL=\(.model.display_name // "--" | ltrimstr("Claude ") | .[0:10] | @sh)",
  "DIR=\(.workspace.current_dir // "" | @sh)",
  "SESSION_ID=\(.session_id // "" | @sh)",
  "LIVE_CWD=\(.workspace.current_dir // .cwd // "" | @sh)",
  "CTX_PCT=\(.context_window.used_percentage // "" | tostring | @sh)",
  "BAR_FILL=\(if (.context_window.used_percentage // 0) > 0 then
      ((.context_window.used_percentage / 10) | round |
       if . > 10 then 10 elif . < 0 then 0 else . end)
     else 0 end | tostring | @sh)",
  "BAR_FILL5=\(if (.context_window.used_percentage // 0) > 0 then
      ((.context_window.used_percentage / 20) | round |
       if . > 5 then 5 elif . < 0 then 0 else . end)
     else 0 end | tostring | @sh)",
  "COST=\(.cost.total_cost_usd // 0 | tostring | @sh)",
  "TOTAL_MIN=\((.cost.total_duration_ms // 0) / 60000 | floor | tostring | @sh)",
  "API_PCT=\(if (.cost.total_duration_ms // 0) > 0 and (.cost.total_api_duration_ms != null) then
      (.cost.total_api_duration_ms * 100 / .cost.total_duration_ms | floor | tostring)
     else "" end | @sh)",
  "CACHE_PCT=\(if .context_window.current_usage then
      ((.context_window.current_usage.cache_read_input_tokens // 0) as $read |
       ((.context_window.current_usage.cache_creation_input_tokens // 0) + $read +
        (.context_window.current_usage.input_tokens // 0)) as $total |
       if $total > 0 then ($read * 100 / $total | floor | tostring) else "" end)
     else "" end | @sh)",
  "LINES_ADD=\(.cost.total_lines_added // 0 | tostring | @sh)",
  "LINES_DEL=\(.cost.total_lines_removed // 0 | tostring | @sh)",
  "RATE5H_PCT=\(.rate_limits.five_hour.used_percentage // "" | tostring | @sh)",
  "RATE5H_RESET=\(.rate_limits.five_hour.resets_at // "" | if type == "number" then floor | tostring else . end | @sh)",
  "RATE7D_PCT=\(.rate_limits.seven_day.used_percentage // "" | tostring | @sh)",
  "RATE7D_RESET=\(.rate_limits.seven_day.resets_at // "" | if type == "number" then floor | tostring else . end | @sh)"
')"

# --- Git branch ---
BRANCH="--"
if [ -n "$DIR" ] && [ "$DIR" != "null" ]; then
  BRANCH=$(git -C "$DIR" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "--")
  if [ "$BRANCH" = "HEAD" ]; then
    BRANCH=$(git -C "$DIR" rev-parse --short HEAD 2>/dev/null || echo "--")
  fi
fi
BRANCH15="$BRANCH"; [ "${#BRANCH}" -gt 15 ] && BRANCH15="${BRANCH:0:12}..."
BRANCH12="$BRANCH"; [ "${#BRANCH}" -gt 12 ] && BRANCH12="${BRANCH:0:9}..."
BRANCH10="$BRANCH"; [ "${#BRANCH}" -gt 10 ] && BRANCH10="${BRANCH:0:7}..."

# --- Context bar and color ---
# Repeats the character $1 $2 times. Built by repetition, never by slicing a string of
# glyphs: bash counts bytes in the C locale and would cut █ and ░ mid-sequence.
rep() { local out="" i=0; while [ "$i" -lt "$2" ]; do out="$out$1"; i=$((i + 1)); done; printf '%s' "$out"; }
BAR_FILL=${BAR_FILL:-0}; [ "$BAR_FILL" = "null" ] && BAR_FILL=0
BAR_FILL5=${BAR_FILL5:-0}; [ "$BAR_FILL5" = "null" ] && BAR_FILL5=0

CTX_INT=$(printf '%.0f' "${CTX_PCT:-0}" 2>/dev/null || echo "0")
if [ "$CTX_INT" -gt 75 ] 2>/dev/null; then CTX_COLOR='\033[31m'
elif [ "$CTX_INT" -ge 50 ] 2>/dev/null; then CTX_COLOR='\033[33m'
else CTX_COLOR='\033[32m'
fi

# Context column: colored bar + space + right-padded percentage
# %-4s pads "0%" → "0%  ", "76%" → "76% ", "100%" → "100%" — fixed column width
CTX_PCT_FMT=$(printf '%-4s' "${CTX_INT}%")
# Full (bar=10): 10 + 1 + 4 = 15 visible chars
CTX_FULL="${CTX_COLOR}$(rep █ "$BAR_FILL")$(rep ░ "$((10 - BAR_FILL))")\033[0m ${CTX_PCT_FMT}"
# Narrow (bar=5): 5 + 1 + 4 = 10 visible chars
CTX_NARROW="${CTX_COLOR}$(rep █ "$BAR_FILL5")$(rep ░ "$((5 - BAR_FILL5))")\033[0m ${CTX_PCT_FMT}"

# --- Cost ---
COST_FMT=$(printf '$%.2f' "${COST:-0}" 2>/dev/null || echo '$0.00')

# --- Time ---
TOTAL_MIN=${TOTAL_MIN:-0}; [ "$TOTAL_MIN" = "null" ] && TOTAL_MIN=0
if [ "$TOTAL_MIN" -ge 60 ] 2>/dev/null; then
  TIME_FMT="$((TOTAL_MIN / 60))h $((TOTAL_MIN % 60))m"
else
  TIME_FMT="${TOTAL_MIN}m"
fi

# --- Cache ---
if [ -n "$CACHE_PCT" ] && [ "$CACHE_PCT" != "null" ] && [ "$CACHE_PCT" != "" ]; then
  CACHE_FMT="${CACHE_PCT}%"
else
  CACHE_FMT="--"
fi

# --- API wait ---
if [ -n "$API_PCT" ] && [ "$API_PCT" != "null" ] && [ "$API_PCT" != "" ]; then
  API_FMT="${API_PCT}%"
else
  API_FMT="--"
fi

# --- Lines changed ---
LINES_FMT="+${LINES_ADD:-0} -${LINES_DEL:-0}"

# --- Usage color helper ---
# Args: $1 = integer percentage
usage_color() {
  if [ "$1" -gt 75 ] 2>/dev/null; then printf '\033[31m'
  elif [ "$1" -ge 50 ] 2>/dev/null; then printf '\033[33m'
  else printf '\033[32m'
  fi
}

# --- 5-hour rate limit ---
# Format: "87% 4pm" (percent + space + reset time — space avoids double-wide Unicode)
# Width 9 (full with reset) or 5 (compact, percent only)
HAS_RATE=false
USAGE5H=""       # 9-char colored field for FULL/MEDIUM/NARROW tiers
USAGE5H_SHORT="" # 5-char colored field for COMPACT tier (no reset time)

if [ -n "$RATE5H_PCT" ] && [ "$RATE5H_PCT" != "null" ] && [ "$RATE5H_PCT" != "" ]; then
  HAS_RATE=true
  PCT5H=$(printf '%.0f' "$RATE5H_PCT" 2>/dev/null || echo "0")
  COLOR5H=$(usage_color "$PCT5H")

  # Format reset time as 12-hour clock: "4pm", "12am", etc.
  RESET5H=""
  if [ -n "$RATE5H_RESET" ] && [ "$RATE5H_RESET" != "null" ] && [ "$RATE5H_RESET" != "" ]; then
    # `date -d @N` is GNU (Linux); `date -r N` is BSD (macOS). On GNU, -r wants a file, so try -d first.
    RESET5H=$(LC_ALL=C date -d "@$RATE5H_RESET" "+%I%p" 2>/dev/null || LC_ALL=C date -r "$RATE5H_RESET" "+%I%p" 2>/dev/null)
    RESET5H=$(printf '%s' "$RESET5H" | sed 's/^0//' | tr '[:upper:]' '[:lower:]')
  fi

  if [ -n "$RESET5H" ]; then
    TEXT5H="${PCT5H}% ${RESET5H}"   # e.g. "87% 4pm" — space avoids double-wide Unicode char
  else
    TEXT5H="${PCT5H}%"
  fi
  USAGE5H="${COLOR5H}$(printf '%-9s' "$TEXT5H")\033[0m"
  USAGE5H_SHORT="${COLOR5H}$(printf '%-5s' "${PCT5H}%")\033[0m"
fi

# --- 7-day rate limit ---
# Format: "65% Fri" (percent + space + day-of-week — space avoids double-wide Unicode)
# Width 9 (full with reset) or 5 (narrow, percent only)
USAGE7D=""       # 9-char colored field for FULL/MEDIUM tiers
USAGE7D_SHORT="" # 5-char colored field for NARROW tier (no reset day)

if [ -n "$RATE7D_PCT" ] && [ "$RATE7D_PCT" != "null" ] && [ "$RATE7D_PCT" != "" ]; then
  PCT7D=$(printf '%.0f' "$RATE7D_PCT" 2>/dev/null || echo "0")
  COLOR7D=$(usage_color "$PCT7D")

  # Format reset as day-of-week: "Fri", "Mon", etc.
  RESET7D=""
  if [ -n "$RATE7D_RESET" ] && [ "$RATE7D_RESET" != "null" ] && [ "$RATE7D_RESET" != "" ]; then
    RESET7D=$(LC_ALL=C date -d "@$RATE7D_RESET" "+%a" 2>/dev/null || LC_ALL=C date -r "$RATE7D_RESET" "+%a" 2>/dev/null)
  fi

  if [ -n "$RESET7D" ]; then
    TEXT7D="${PCT7D}% ${RESET7D}"   # e.g. "65% Fri" — space avoids double-wide Unicode char
  else
    TEXT7D="${PCT7D}%"
  fi
  USAGE7D="${COLOR7D}$(printf '%-9s' "$TEXT7D")\033[0m"
  USAGE7D_SHORT="${COLOR7D}$(printf '%-5s' "${PCT7D}%")\033[0m"
fi

# --- Adaptive output ---
# When rate limits are absent (API-key sessions), usage columns are hidden.
# Header strings are manually padded to match printf field widths in value rows.
#
# Tier visible widths (content + separators):
#   FULL:    9+9+15+10+15+7+7+5+4+9 = 90 content + 9×3 sep = 117
#   MEDIUM:  9+9+15+10+12+7+7+5+4   = 78 content + 8×3 sep = 102
#   NARROW:  9+5+10+10+12+7+7       = 60 content + 6×3 sep = 78
#   COMPACT: 5+10+10+10+7+7         = 49 content + 5×3 sep = 64

LIVE_H=""; LIVE_V=""
case "$TIER" in full-live|medium-live)
  LIVE_H="$LIVE_HDR_SUF"
  LIVE_V=" │ $(vpad "$(live_text "$SESSION_ID" "$LIVE_CWD")" "$LIVE_W")"
esac

if [ "$TIER" = full-live ] || [ "$TIER" = full ]; then
  # FULL: all columns, branch×15, full ctx bar (+ Live from 156 columns)
  if $HAS_RATE; then
    printf '%b\n' "\033[2m5h Usage  │ 7d Usage  │ Context         │ Model      │ Branch          │ Cost    │ Time    │ Cache │ API  │ Lines    ${LIVE_H}\033[0m"
    printf '%b\n' "${USAGE5H} │ ${USAGE7D} │ ${CTX_FULL} │ $(printf '%-10s' "$MODEL") │ $(printf '%-15s' "$BRANCH15") │ $(printf '%-7s' "$COST_FMT") │ $(printf '%-7s' "$TIME_FMT") │ $(printf '%-5s' "$CACHE_FMT") │ $(printf '%-4s' "$API_FMT") │ $(printf '%-9s' "$LINES_FMT")${LIVE_V}"
  else
    printf '%b\n' "\033[2mContext         │ Model      │ Branch          │ Cost    │ Time    │ Cache │ API  │ Lines    ${LIVE_H}\033[0m"
    printf '%b\n' "${CTX_FULL} │ $(printf '%-10s' "$MODEL") │ $(printf '%-15s' "$BRANCH15") │ $(printf '%-7s' "$COST_FMT") │ $(printf '%-7s' "$TIME_FMT") │ $(printf '%-5s' "$CACHE_FMT") │ $(printf '%-4s' "$API_FMT") │ $(printf '%-9s' "$LINES_FMT")${LIVE_V}"
  fi
elif [ "$TIER" = medium-live ] || [ "$TIER" = medium ]; then
  # MEDIUM: no Lines, branch×12, full ctx bar (+ Live from 141 columns)
  if $HAS_RATE; then
    printf '%b\n' "\033[2m5h Usage  │ 7d Usage  │ Context         │ Model      │ Branch       │ Cost    │ Time    │ Cache │ API  ${LIVE_H}\033[0m"
    printf '%b\n' "${USAGE5H} │ ${USAGE7D} │ ${CTX_FULL} │ $(printf '%-10s' "$MODEL") │ $(printf '%-12s' "$BRANCH12") │ $(printf '%-7s' "$COST_FMT") │ $(printf '%-7s' "$TIME_FMT") │ $(printf '%-5s' "$CACHE_FMT") │ $(printf '%-4s' "$API_FMT")${LIVE_V}"
  else
    printf '%b\n' "\033[2mContext         │ Model      │ Branch       │ Cost    │ Time    │ Cache │ API  ${LIVE_H}\033[0m"
    printf '%b\n' "${CTX_FULL} │ $(printf '%-10s' "$MODEL") │ $(printf '%-12s' "$BRANCH12") │ $(printf '%-7s' "$COST_FMT") │ $(printf '%-7s' "$TIME_FMT") │ $(printf '%-5s' "$CACHE_FMT") │ $(printf '%-4s' "$API_FMT")${LIVE_V}"
  fi
elif [ "$TIER" = narrow ]; then
  # NARROW: no Lines/Cache/API, 7d % only (no reset day), narrow ctx bar, branch×12
  if $HAS_RATE; then
    printf '%b\n' "\033[2m5h Usage  │ 7d    │ Context    │ Model      │ Branch       │ Cost    │ Time    \033[0m"
    printf '%b\n' "${USAGE5H} │ ${USAGE7D_SHORT} │ ${CTX_NARROW} │ $(printf '%-10s' "$MODEL") │ $(printf '%-12s' "$BRANCH12") │ $(printf '%-7s' "$COST_FMT") │ $(printf '%-7s' "$TIME_FMT")"
  else
    printf '%b\n' "\033[2mContext    │ Model      │ Branch       │ Cost    │ Time    \033[0m"
    printf '%b\n' "${CTX_NARROW} │ $(printf '%-10s' "$MODEL") │ $(printf '%-12s' "$BRANCH12") │ $(printf '%-7s' "$COST_FMT") │ $(printf '%-7s' "$TIME_FMT")"
  fi
else
  # COMPACT: 5h % only (no reset), narrow ctx, model, branch×10, cost, time
  #   Full COMPACT:  5+10+10+10+7+7 = 49 content + 5×3 sep = 64 chars
  #   No-Time COMPACT: drop Time when cols < 65 → 54 chars fits ≥54 col terminals
  if [ "$COLS" -ge 65 ] 2>/dev/null; then
    if $HAS_RATE; then
      printf '%b\n' "\033[2m5h    │ Context    │ Model      │ Branch     │ Cost    │ Time    \033[0m"
      printf '%b\n' "${USAGE5H_SHORT} │ ${CTX_NARROW} │ $(printf '%-10s' "$MODEL") │ $(printf '%-10s' "$BRANCH10") │ $(printf '%-7s' "$COST_FMT") │ $(printf '%-7s' "$TIME_FMT")"
    else
      printf '%b\n' "\033[2mContext    │ Model      │ Branch     │ Cost    │ Time    \033[0m"
      printf '%b\n' "${CTX_NARROW} │ $(printf '%-10s' "$MODEL") │ $(printf '%-10s' "$BRANCH10") │ $(printf '%-7s' "$COST_FMT") │ $(printf '%-7s' "$TIME_FMT")"
    fi
  else
    if $HAS_RATE; then
      printf '%b\n' "\033[2m5h    │ Context    │ Model      │ Branch     │ Cost    \033[0m"
      printf '%b\n' "${USAGE5H_SHORT} │ ${CTX_NARROW} │ $(printf '%-10s' "$MODEL") │ $(printf '%-10s' "$BRANCH10") │ $(printf '%-7s' "$COST_FMT")"
    else
      printf '%b\n' "\033[2mContext    │ Model      │ Branch     │ Cost    \033[0m"
      printf '%b\n' "${CTX_NARROW} │ $(printf '%-10s' "$MODEL") │ $(printf '%-10s' "$BRANCH10") │ $(printf '%-7s' "$COST_FMT")"
    fi
  fi
fi
