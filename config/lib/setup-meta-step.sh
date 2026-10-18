#!/usr/bin/env bash
# aw_setup_meta_step <toolkit-dir> <provider>...  — setup.sh --with-meta: run scripts/install-meta.sh once per provider (the binary
# is published by the first one only). Exit 3 from install-meta.sh is its tree refusal (a linked worktree, or a tree that is not the
# team's repo, without --allow-worktree): setup warns, goes on and the step still returns 0, because meta is opt-in and the refusal
# says what to do. Every other non-zero exit is a real failure: the remaining providers are still installed (one failing provider
# must not leave the others without their hooks) and the first failure's code is returned at the end: setup.sh records it, finishes
# the rest of setup, then exits with it (aw_setup_meta_finish).
aw_setup_meta_step() {
  local dir="$1" p built=0 rc skip failed=0
  shift
  for p in "$@"; do
    skip="${AW_SKIP_BUILD:-0}"; [ "$built" = 1 ] && skip=1
    rc=0
    AW_SKIP_BUILD="$skip" bash "$dir/scripts/install-meta.sh" --provider "$p" || rc=$?
    case $rc in
      0) built=1 ;;
      3) echo "  meta: NOT installed for $p (the tree was refused, see above); rerun scripts/install-meta.sh from the canonical checkout" >&2 ;;
      *) echo "  meta: scripts/install-meta.sh failed for $p (exit $rc); setup goes on and fails at the end, the installed aw-meta (if any) is unchanged and may be stale" >&2; [ "$failed" != 0 ] || failed="$rc" ;;
    esac
  done
  return "$failed"
}

# aw_setup_meta_finish <rc>  — the last line of setup.sh: the meta step's result, after everything else has run.
# 0 returns 0 (a refused tree, exit 3, was already warned about and is not a failure); any other code is returned with the message.
aw_setup_meta_finish() {
  local rc="${1:-0}"
  [ "$rc" = 0 ] || {
    echo "" >&2
    echo "meta install failed (exit $rc): the rest of setup ran; the installed aw-meta (if any) is unchanged and may be stale. Fix the cause above and rerun ./setup.sh --with-meta (or scripts/install-meta.sh)." >&2
    return "$rc"
  }
  return 0
}
