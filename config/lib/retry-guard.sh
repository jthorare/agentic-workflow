#!/usr/bin/env bash
# config/lib/retry-guard.sh — what config/hooks/retry-gate.sh and retry-record.sh share (issue 4248): the call hash,
# the state file's path, its lock and its one write. The state file's contract is docs/hooks/retry-guard.md.
# Sourced, never executed; defines functions and the RG_* constants only. bash 3.2 compatible. The hooks source it only
# once a state file exists or a failure must be written, so the ordinary call never pays for it.

RG_VERSION=1
RG_POLICY="retry-guard/1"
RG_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# rg_failopen <hook> <cause>: one HOOK-FAILOPEN line (config/lib/failopen.sh beside this file); logging only.
rg_failopen() {
  (
    set +eu
    [ -f "$RG_LIB_DIR/failopen.sh" ] && . "$RG_LIB_DIR/failopen.sh" && aw_failopen "$1" "$2"
  ) > /dev/null 2>&1 || true
}

# rg_now: sets RG_NOW (epoch seconds; AW_RETRY_GUARD_NOW wins, for tests).
rg_now() {
  case "${AW_RETRY_GUARD_NOW:-}" in
    "" | *[!0-9]*) RG_NOW="$(date +%s)" ;;
    *) RG_NOW="$AW_RETRY_GUARD_NOW" ;;
  esac
}

# rg_call <payload>: one jq run. Sets RG_TOOL, RG_CANON (tool_input, keys sorted, compact, with a Bash command's trailing
# `# aw-retry: <reason>` removed), RG_REASON (that reason, or ""), RG_EXE (a Bash command's first word that is not an
# assignment, without its folder), RG_CMD (the Bash command, override removed), RG_CWD, RG_EVENT and RG_INTR
# ("true"/"false"). Returns 1 when the payload is not a JSON object.
rg_call() {
  local out
  out="$(printf '%s' "$1" | jq -r '
    def clean: gsub("[\u0000-\u001f\u007f]"; " ");
    def sortkeys: if type == "object" then to_entries | sort_by(.key) | map(.value |= sortkeys) | from_entries
                  elif type == "array" then map(sortkeys) else . end;
    if type != "object" then error("not an object") else . end
    | (.tool_name // "" | tostring) as $tool
    | (.tool_input // {}) as $in
    | (if $tool == "Bash" and ($in | type) == "object" and (($in.command // null) | type) == "string" then
         ($in.command | capture("^(?<c>[\\s\\S]*?)\\s*#\\s*aw-retry:\\s*(?<r>[^\\n]*)\\z") // {c: $in.command, r: ""})
       else {c: "", r: ""} end) as $ov
    | (if $tool == "Bash" and ($in | type) == "object" then ($in | .command = $ov.c) else $in end) as $canonIn
    | ($ov.c | [splits("\\s+")] | map(select(length > 0 and (test("^[A-Za-z_][A-Za-z0-9_]*=") | not))) | .[0] // "") as $exe
    | $tool, ($canonIn | sortkeys | tojson), ($ov.r | clean | .[0:120]), ($exe | clean),
      ($ov.c | clean), (.cwd // "" | tostring | clean), (.hook_event_name // "" | tostring),
      ((.is_interrupt // false) | tostring)
  ' 2> /dev/null)" || return 1
  # Each value is one line: tojson escapes newlines, and clean() turns control characters into spaces.
  { IFS= read -r RG_TOOL; IFS= read -r RG_CANON; IFS= read -r RG_REASON; IFS= read -r RG_EXE
    IFS= read -r RG_CMD; IFS= read -r RG_CWD; IFS= read -r RG_EVENT; IFS= read -r RG_INTR; } <<< "$out"
  case "$RG_EXE" in */*) RG_EXE="${RG_EXE##*/}" ;; esac
  case "$RG_EXE" in *[!A-Za-z0-9._+-]* | "") RG_EXE="" ;; esac
  return 0
}

# rg_hash: sets RG_HASH, sha256 (hex) of "<tool>\n<canonical input>".
rg_hash() {
  local h
  if command -v sha256sum > /dev/null 2>&1; then
    h="$(printf '%s\n%s' "$RG_TOOL" "$RG_CANON" | sha256sum)"
  else
    h="$(printf '%s\n%s' "$RG_TOOL" "$RG_CANON" | shasum -a 256)"
  fi
  RG_HASH="${h%% *}"
}

# rg_mtime <file>: its mtime in epoch seconds, or "".
rg_mtime() { stat -c %Y "$1" 2> /dev/null || stat -f %m "$1" 2> /dev/null || true; }

# rg_auth_file <host>: the provider's auth file for that host, or "".
rg_auth_file() {
  case "$1" in
    github.com) printf '%s\n' "${GH_CONFIG_DIR:-${HOME:-}/.config/gh}/hosts.yml" ;;
  esac
}

# rg_lock <file>: the mkdir lock <file>.lock holding this pid, the same lock locks.sh's acquire_lock makes. It retries
# every 50 ms for 2 s, not every second: a hook holds it for a few ms, and acquire_lock's 1 s steps made writers that
# started together retry together and give up (6 parallel failures kept 3). A dead holder's lock is reclaimed with
# locks.sh's _lock_reclaim_dead when locks.sh sits beside this file.
rg_lock() {
  local lk="$1.lock" i=0
  if [ -z "${RG_LOCKS_LOADED:-}" ]; then
    if [ -f "$RG_LIB_DIR/locks.sh" ]; then . "$RG_LIB_DIR/locks.sh" 2> /dev/null; fi
    RG_LOCKS_LOADED=1
  fi
  while ! mkdir "$lk" 2> /dev/null; do
    if command -v _lock_reclaim_dead > /dev/null 2>&1 && _lock_reclaim_dead "$lk" 2> /dev/null; then continue; fi
    i=$((i + 1)); [ "$i" -ge 40 ] && return 1
    sleep 0.05
  done
  printf '%s\n' "$$" > "$lk/pid" 2> /dev/null || true
  RG_LOCK="$lk"
}
rg_unlock() {
  [ -n "${RG_LOCK:-}" ] || return 0
  rm -f "$RG_LOCK/pid" 2> /dev/null
  rmdir "$RG_LOCK" 2> /dev/null
  RG_LOCK=""
}

# rg_update <file> <jq program> [jq args...]: under the lock, read the state (a missing, unreadable or other-version file
# is a fresh one), run the program on it with $now (epoch) and $iso, write the whole result to <file>.tmp.<pid> and
# rename it over <file>. Returns 1 when the lock or the write failed (the caller fails open).
rg_update() {
  local f="$1" prog="$2" tmp cur
  shift 2
  rg_lock "$f" || return 1
  cur='null'
  if [ -f "$f" ]; then
    cur="$(jq -c --argjson v "$RG_VERSION" --arg p "$RG_POLICY" \
      'if type == "object" and .version == $v and .policy == $p then . else null end' "$f" 2> /dev/null)" || cur='null'
    [ -n "$cur" ] || cur='null'
  fi
  tmp="$f.tmp.$$"
  if printf '%s' "$cur" | jq -c --argjson now "$RG_NOW" --argjson v "$RG_VERSION" --arg p "$RG_POLICY" \
      --arg sid "$RG_SID" --arg provider "${AW_PROVIDER:-claude}" "$@" '
      ($now | todate) as $iso
      | (. // {version: $v, policy: $p, session: $sid, provider: $provider, problems: {}, overrides: [],
               counters: {failures: 0, denies: 0, advisories: 0, overrides: 0, cleared: 0, clearedAfterChange: 0}})
      | .updatedAt = $iso
      | '"$prog" > "$tmp" 2> /dev/null && [ -s "$tmp" ] && mv -f "$tmp" "$f"; then
    rg_unlock; return 0
  fi
  rm -f "$tmp"; rg_unlock; return 1
}
