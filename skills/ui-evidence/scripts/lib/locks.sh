#!/usr/bin/env bash
# skills/ui-evidence/scripts/lib/locks.sh — sources the repo's canonical
# mkdir-based mutex primitives (config/lib/locks.sh, owned by this plan) and
# adds only the ui-evidence-specific composite: the box-wide stack lock (one
# slot) and a slot of the box-wide heavy-job lock (AW_HEAVY_SLOTS, default 2:
# at most two heavy jobs at a time), taken together and released even on
# failure (Global Constraints — a real Playwright run is a heavy job, and
# :3000/:5002/Postgres/Redis are shared by every session on the box).
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../../../../config/lib/locks.sh"

with_stack_lock_and_heavy_job_lock() {
  local timeout_s="$1"; shift
  local stack_lock="${AW_QA_STACK_LOCK:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/locks/qa-stack.lock}"
  local heavy_lock="${AW_HEAVY_JOB_LOCK:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/locks/heavy-job.lock}"
  mkdir -p "$(dirname "$stack_lock")" "$(dirname "$heavy_lock")"

  # Wider lock first (box-wide stack lock), then the session-scoped one, to
  # avoid a box-wide/session-wide deadlock ordering inversion.
  acquire_lock "$stack_lock" "$timeout_s" || { echo "could not acquire qa-stack.lock within ${timeout_s}s" >&2; return 1; }
  local heavy_slot
  if ! heavy_slot="$(acquire_any_lock "$heavy_lock" "${AW_HEAVY_SLOTS:-2}" "$timeout_s")"; then
    release_lock "$stack_lock"
    echo "could not acquire a heavy-job.lock slot within ${timeout_s}s" >&2
    return 1
  fi

  # Both locks are freed no matter how the command exits (crash, timeout, or
  # normal failure) — Global Constraints requires this.
  local rc=0
  trap 'release_lock "'"$heavy_slot"'"; release_lock "'"$stack_lock"'"' RETURN
  "$@" || rc=$?
  return $rc
}
