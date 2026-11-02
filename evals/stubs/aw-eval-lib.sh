#!/usr/bin/env bash
# Sourced by the eval stubs (evals/stubs/bin/gh, curl). aw_eval_root prints the first directory at or above $PWD that holds
# .aw-eval/ (the workspace root the case's scaffold made) and returns 1 when there is none.
aw_eval_root() {
  local d="$PWD"
  while :; do
    if [ -d "$d/.aw-eval" ]; then printf '%s' "$d"; return 0; fi
    [ "$d" = / ] && return 1
    d="${d%/*}"; [ -n "$d" ] || d=/
  done
}
# aw_eval_real NAME: the next NAME on PATH after the stubs' bin directory (prints its path, returns 1 when none)
aw_eval_real() {
  local self_dir="$1" name="$2" d dirs
  IFS=: read -ra dirs <<< "$PATH"
  for d in "${dirs[@]}"; do
    [ -n "$d" ] || continue
    [ "$(cd "$d" 2>/dev/null && pwd)" = "$self_dir" ] && continue
    if [ -x "$d/$name" ] && [ ! -d "$d/$name" ]; then printf '%s' "$d/$name"; return 0; fi
  done
  return 1
}
