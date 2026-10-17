#!/usr/bin/env bash
# aw_setup_meta_step <toolkit-dir> <provider>...  — setup.sh --with-meta: run scripts/install-meta.sh once per provider (the binary
# is published by the first one only). Exit 3 from install-meta.sh is its tree refusal (a linked worktree, or a tree that is not the
# team's repo, without --allow-worktree): setup warns, goes on and the step still returns 0, because meta is opt-in and the refusal
# says what to do. Every other non-zero exit is a real failure and is returned, so setup (set -e) stops.
aw_setup_meta_step() {
  local dir="$1" p built=0 rc skip
  shift
  for p in "$@"; do
    skip="${AW_SKIP_BUILD:-0}"; [ "$built" = 1 ] && skip=1
    rc=0
    AW_SKIP_BUILD="$skip" bash "$dir/scripts/install-meta.sh" --provider "$p" || rc=$?
    case $rc in
      0) built=1 ;;
      3) echo "  meta: NOT installed for $p (the tree was refused, see above); rerun scripts/install-meta.sh from the canonical checkout" >&2 ;;
      *) echo "  meta: scripts/install-meta.sh failed for $p (exit $rc); setup stops here, the installed aw-meta (if any) is unchanged and may be stale" >&2; return "$rc" ;;
    esac
  done
  return 0
}
