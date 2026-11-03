#!/usr/bin/env bash
# Preflight for a clickable prototype. Copy to $AW_DIR/prototypes/<slug>/preflight.sh and fill in
# ONLY what this project's local stack needs, taken from its run skill or docs. Delete the blocks
# it does not need; do not leave a check in for a tool the project does not use.
# One plain-language fix per miss. Exit 1 on any miss. Phase 2 does not start until it passes.
set -uo pipefail

misses=0
miss() {
  echo "MISSING: $1" >&2
  echo "  fix: $2" >&2
  misses=$((misses + 1))
}
ok() { echo "ok: $1"; }

# --- Container runtime (only if the stack runs containers) -------------------------------------
# if docker info >/dev/null 2>&1; then ok "container runtime is running"
# else miss "the container runtime is not running" "start it and wait until it reports running, then re-run this."; fi

# --- Package manager / runtime (one block per tool the stack's commands call) ------------------
# if command -v <tool> >/dev/null 2>&1; then ok "<tool> is installed"
# else miss "<tool> is not installed" "<the project's own install instruction>"; fi

# --- Secrets or login the stack needs (only if its docs say so) --------------------------------
# if <command that succeeds only when logged in> >/dev/null 2>&1; then ok "<login> is valid"
# else miss "no valid <login>" "run: <the project's login command>, then re-run this."; fi

# --- Free ports (list every port the stack binds; take them from its docs or compose file) -----
PORTS=() # e.g. PORTS=(3000 5432)
busy=0
if [ "${#PORTS[@]}" -gt 0 ] && ! command -v lsof >/dev/null 2>&1; then
  busy=1
  miss "cannot check ports (lsof is not installed)" "install lsof (for example: apt install lsof), then re-run this."
fi
for port in ${PORTS[@]+"${PORTS[@]}"}; do
  command -v lsof >/dev/null 2>&1 || break
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    busy=1
    miss "port :$port is already in use" \
      "another stack is probably still running: stop it with the project's stop command, or quit whatever uses :$port."
  fi
done
if [ "${#PORTS[@]}" -gt 0 ] && [ "$busy" -eq 0 ]; then ok "stack ports are free"; fi

# --- Free disk (set NEED_GB from the project: dependency install plus images) ------------------
NEED_GB=0 # e.g. 12; 0 skips the check
if [ "$NEED_GB" -gt 0 ]; then
  avail_gb=$(df -Pk "$HOME" | awk 'NR==2 {print int($4 / 1048576)}')
  if [ "${avail_gb:-0}" -ge "$NEED_GB" ]; then
    ok "${avail_gb} GB free disk"
  else
    miss "only ${avail_gb:-0} GB free disk (need $NEED_GB GB)" \
      "free up space, for example by emptying the Trash or removing old prototype worktrees."
  fi
fi

if [ "$misses" -gt 0 ]; then
  echo "preflight failed: $misses thing(s) to fix above." >&2
  exit 1
fi
echo "preflight passed."
