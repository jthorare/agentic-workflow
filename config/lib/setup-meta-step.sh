#!/usr/bin/env bash
# aw_setup_meta_step <toolkit-dir> <provider>...  — setup.sh --with-meta: run scripts/install-meta.sh once per provider (the binary
# is published by the first one only). Every non-zero exit is a failure, exit 3 (install-meta.sh's tree refusal: a linked worktree,
# or a tree that is not the team's repo, without --allow-worktree) included: it used to return 0 and left the old aw-meta in place
# behind one easy-to-miss line (#2283). The remaining providers are still installed (one failing provider must not leave the others
# without their hooks) and the first failure's code is returned at the end: setup.sh records it, finishes the rest of setup, then
# exits with it (aw_setup_meta_finish).
aw_setup_meta_step() {
  local dir="$1" p built=0 rc skip failed=0
  shift
  # install-meta.sh appends its end-of-run notes (the behind-origin/main warning) to this file; aw_setup_meta_finish repeats them last
  AW_META_NOTE_FILE="$(mktemp "${TMPDIR:-/tmp}/aw-meta-note.XXXXXX" 2> /dev/null || true)"; export AW_META_NOTE_FILE
  for p in "$@"; do
    skip="${AW_SKIP_BUILD:-0}"; [ "$built" = 1 ] && skip=1
    rc=0
    AW_SKIP_BUILD="$skip" bash "$dir/scripts/install-meta.sh" --provider "$p" || rc=$?
    case $rc in
      0) built=1 ;;
      3) echo "  meta: NOT installed for $p (the tree was refused, see above); setup goes on and fails at the end; rerun scripts/install-meta.sh from the canonical checkout" >&2; [ "$failed" != 0 ] || failed="$rc" ;;
      *) echo "  meta: scripts/install-meta.sh failed for $p (exit $rc); setup goes on and fails at the end, the installed aw-meta (if any) is unchanged and may be stale" >&2; [ "$failed" != 0 ] || failed="$rc" ;;
    esac
  done
  aw_meta_last_note=""
  [ -z "$AW_META_NOTE_FILE" ] || { aw_meta_last_note="$(tail -1 "$AW_META_NOTE_FILE" 2> /dev/null || true)"; rm -f "$AW_META_NOTE_FILE"; }
  unset AW_META_NOTE_FILE
  return "$failed"
}

# aw_setup_meta_finish <rc>  — the last line of setup.sh: the meta step's result, after everything else has run.
# The behind-origin/main warning install-meta.sh saved (aw_meta_last_note) is repeated last, exit 0 or not. 0 returns 0; any other code (a refused tree, exit 3, included) is returned with the message.
aw_setup_meta_finish() {
  local rc="${1:-0}"
  [ "$rc" = 0 ] || {
    echo "" >&2
    echo "meta install failed (exit $rc): the rest of setup ran; the installed aw-meta (if any) is unchanged and may be stale. Fix the cause above and rerun ./setup.sh --with-meta (or scripts/install-meta.sh)." >&2
    [ -z "${aw_meta_last_note:-}" ] || echo "$aw_meta_last_note" >&2
    return "$rc"
  }
  [ -z "${aw_meta_last_note:-}" ] || echo "$aw_meta_last_note" >&2
  return 0
}
