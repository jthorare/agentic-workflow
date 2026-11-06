#!/usr/bin/env bash
# aw:trait-check — PostToolUse on file edits + Stop (lever, opt-in via
# scripts/install-trait-check.sh). Judges the repo's traits
# (<repo>/.agents/traits/*.yaml plus the private overlay
# ~/.agentic-workflow/traits/<repo>/*.yaml) against what changed, via
# `judge trait-check` (Jev):
#   PostToolUse: the edited files' diff vs HEAD, each file judged in its own
#                repo (an absolute path into another repo gets that repo's
#                traits). Likely violations go back to the agent as feedback
#                (exit 2 + stderr); the edit already happened.
#   Stop:        the branch's diff vs its merge base with origin/main, for the
#                working directory's repo and every other repo this session
#                edited files in (passed as --session <session_id>; the judge
#                keeps judge/trait-check/sessions/<id>.repos). Likely
#                severity: block violations make the agent continue, once:
#                stop_hook_active exits 0 first, so this never loops.
# A repo without traits costs one `git rev-parse` and a 'skipped' log line.
set -uo pipefail
# HOOK-FAILOPEN: record why this hook is failing open (config/lib/failopen.sh,
# installed at <hooks>/lib/failopen.sh). Logging only: it runs in a subshell
# with its output discarded, so it can never change this hook's exit code,
# stdout or stderr, whatever set -e/-u or the environment do to it.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen trait-check "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}

[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0
JUDGE_BIN="${AW_JUDGE_BIN:-judge}"

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
EVENT="$(printf '%s' "$INPUT" | jq -r '.hook_event_name // empty' 2>/dev/null)" || { failopen input "$INPUT"; exit 0; }

case "$EVENT" in
  Stop)
    [ "$(printf '%s' "$INPUT" | jq -r '.stop_hook_active // false')" = "true" ] && exit 0
    ARGS=(--stop)
    ;;
  PostToolUse)
    # Only tools that change files: Claude Edit/Write/MultiEdit/NotebookEdit,
    # Codex apply_patch, Cursor Write/Edit. Cursor installs without a matcher,
    # and its Read carries a path too.
    printf '%s' "$INPUT" | jq -e '(.tool_name // "") | test("edit|write|patch|replace"; "i")' > /dev/null 2>&1 || exit 0
    # Edited paths, whatever the host calls them: Claude Edit/Write/MultiEdit
    # (file_path), NotebookEdit (notebook_path), Cursor (file_path/path/
    # target_file), and Codex apply_patch, whose patch text names each file on
    # an `*** Add File:` / `*** Update File:` / `*** Move to:` line.
    FILES="$(printf '%s' "$INPUT" | jq -r '
      (.tool_input // {}) as $in
      | if ($in | type) != "object" then empty else
          ([$in.file_path, $in.notebook_path, $in.path, $in.target_file] | map(select(type == "string" and . != "")) | .[]),
          ($in | .. | strings | split("\n")[] | capture("^\\*\\*\\* (?:Add File|Update File|Move to): (?<p>.+)$")? | .p)
        end' 2>/dev/null | awk 'NF && !seen[$0]++')"
    [ -n "$FILES" ] || exit 0
    ARGS=(--edit)
    while IFS= read -r f; do ARGS+=(--file "$f"); done <<< "$FILES"
    ;;
  *) exit 0 ;;
esac

# The session id lets Stop find the other repos this session's edits touched.
SID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty | strings' 2>/dev/null)"
[ -n "$SID" ] && ARGS+=(--session "$SID")

command -v "$JUDGE_BIN" > /dev/null 2>&1 || { failopen judge-missing; exit 0; }

ERR_FILE="$(mktemp "${TMPDIR:-/tmp}/trait-check.XXXXXX")" || { failopen mktemp; exit 0; }
"$JUDGE_BIN" trait-check "${ARGS[@]}" > /dev/null 2> "$ERR_FILE"
RC=$?
MSG="$(cat "$ERR_FILE" 2>/dev/null)"
rm -f "$ERR_FILE"

case "$RC" in
  0) exit 0 ;;
  2) printf '%s\n' "$MSG" >&2; exit 2 ;;
  3) failopen jev-unavailable; exit 0 ;;
  *) failopen "judge-exit-$RC"; exit 0 ;;
esac
