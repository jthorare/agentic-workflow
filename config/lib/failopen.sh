#!/usr/bin/env bash
# config/lib/failopen.sh — HOOK-FAILOPEN logging for hooks that fail open
# (spec docs/superpowers/specs/2026-10-09-agent-readiness-design.md §4c, B5).
#
# Sourced, never executed. bash 3.2 compatible. A hook calls
#   aw_failopen <hook> <cause>
# right before it allows (or lets through) because of its own internal error:
# judge missing or erroring, input it can't read, unreadable state. One JSON
# line goes to ${AW_STATE_DIR:-$HOME/.agentic-workflow}/logs/failopen.log:
#   {"ts":"…Z","event":"HOOK-FAILOPEN","hook":"send-gate","cause":"judge-exit-127","provider":"claude"}
# Read the log as untrusted input; it is general observability, not a gate.
#
# `aw_failopen <hook> input <raw-stdin> [jq-predicate]` classifies a payload
# the hook couldn't use: empty-input, jq-missing or input-unparsable; and,
# when the predicate holds (the payload names the hook's own tool), field-missing.
# It logs nothing for any other readable JSON object.
#
# Logging only: it never prints, never fails and never changes the caller's
# decision. Hooks still call it from a subshell (see their failopen() wrapper)
# so the caller's set -e/-u can't reach it. Payload content is never written:
# only the hook name and a cause built from fixed words, both reduced to
# [A-Za-z0-9._:-]. With neither AW_STATE_DIR nor HOME set it logs nothing.
# It writes only to a regular file (or creates one): a symlink, FIFO or device
# at the log path is never opened, since opening a FIFO would block the hook.
# The log rotates to failopen.log.1 past AW_FAILOPEN_MAX_BYTES (default 1 MiB),
# one writer at a time (mkdir lock failopen.log.rotating).
# AW_FAILOPEN_INPUT_LOGGED=1 (set by an adapter that already logged the host
# payload) silences the input causes, so one bad payload counts once.

aw_failopen() {
  local hook="${1:-unknown}" cause="${2:-unknown}" provider="${AW_PROVIDER:-claude}" dir log ts size
  if [ "$cause" = "input" ]; then
    [ -n "${AW_FAILOPEN_INPUT_LOGGED:-}" ] && return 0
    if [ -z "${3-}" ]; then
      cause="empty-input"
    elif ! command -v jq > /dev/null 2>&1; then
      cause="jq-missing"
    elif ! printf '%s' "$3" | jq -e 'type == "object"' > /dev/null 2>&1; then
      cause="input-unparsable"
    elif [ -n "${4-}" ] && printf '%s' "$3" | jq -e "$4" > /dev/null 2>&1; then
      cause="field-missing"
    else
      return 0
    fi
  fi
  if [ -n "${AW_STATE_DIR:-}" ]; then
    dir="$AW_STATE_DIR/logs"
  elif [ -n "${HOME:-}" ]; then
    dir="$HOME/.agentic-workflow/logs"
  else
    return 0
  fi
  log="$dir/failopen.log"
  if [ -L "$log" ] || { [ -e "$log" ] && [ ! -f "$log" ]; }; then return 0; fi
  hook="${hook//[^A-Za-z0-9._:-]/_}"
  cause="${cause//[^A-Za-z0-9._:-]/_}"
  cause="${cause:0:80}"
  provider="${provider//[^A-Za-z0-9._:-]/_}"
  ts="$(date -u +%Y-%m-%dT%H:%M:%SZ 2> /dev/null)" || ts=""
  (
    umask 077
    mkdir -p "$dir" || exit 0
    aw_failopen_rotate "$log"
    printf '{"ts":"%s","event":"HOOK-FAILOPEN","hook":"%s","cause":"%s","provider":"%s"}\n' \
      "$ts" "$hook" "$cause" "$provider" >> "$log"
  ) > /dev/null 2>&1 || true
  return 0
}

# aw_failopen_rotate <log> — move a log past AW_FAILOPEN_MAX_BYTES to <log>.1.
# Hooks run in parallel, so one writer rotates (mkdir lock, cleared when older
# than a minute) and re-checks the size under the lock; a non-regular <log>.1 is
# never replaced.
aw_failopen_size_over() {
  local size
  size="$(wc -c < "$1" 2> /dev/null)" || return 1
  [ "${size// /}" -gt "${AW_FAILOPEN_MAX_BYTES:-1048576}" ] 2> /dev/null
}
aw_failopen_rotate() {
  local log="$1" lock="$1.rotating"
  aw_failopen_size_over "$log" || return 0
  if [ -d "$lock" ] && [ -n "$(find "$lock" -maxdepth 0 -mmin +1 2> /dev/null)" ]; then
    rmdir "$lock" 2> /dev/null
  fi
  mkdir "$lock" 2> /dev/null || return 0
  if aw_failopen_size_over "$log" && { [ ! -e "$log.1" ] || { [ -f "$log.1" ] && [ ! -L "$log.1" ]; }; }; then
    mv -f "$log" "$log.1"
  fi
  rmdir "$lock" 2> /dev/null
  return 0
}
