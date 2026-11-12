#!/usr/bin/env bash
# aw:meta-outbox — PostToolUse hook, matcher SendMessage (Claude Code only). Hands every sent message to
# `aw-meta outbox-record`, which closes the obligations it answers (a reply to a peer, a lane-map broadcast, a "main
# moved" announcement, an owner answer). aw-meta records only the registered meta's sends. For any session's send it may
# print a warning line ("aw-meta: warning: ..."; a STATUS: message that holds a "?", #2654); that line goes back to the sender
# as PostToolUse additionalContext. Never blocks: always exits 0 and prints nothing else; a failure is logged as a fail-open.
set -uo pipefail
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen meta-outbox "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0
# The team: AW_META_TEAM when the lane launcher set it; else the tmux socket the session runs on (the launcher names it after the
# team), taken only when that team exists on disk (teams/<name>/team.json, or the legacy config's own team) and is not "default" (tmux's
# own socket). Anything else: no team selected. Mirrors scripts/meta-team-lib.sh.
if [ -z "${AW_META_TEAM:-}" ] && [ -n "${TMUX:-}" ]; then
  sock="${TMUX%%,*}"; sock="${sock##*/}"
  mroot="${AW_STATE_DIR:-$HOME/.agentic-workflow}/meta"
  if [[ "$sock" =~ ^[a-z0-9][a-z0-9-]*$ ]] && [ "$sock" != default ] && { [ -f "$mroot/teams/$sock/team.json" ] || [ "$(jq -r 'if (.team | type) == "string" then .team else empty end' "$mroot/team.json" 2> /dev/null)" = "$sock" ]; }; then
    export AW_META_TEAM="$sock"
  fi
fi
META="${AW_META_BIN:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-meta}"
[ -x "$META" ] || exit 0
INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
OUT="$(printf '%s' "$INPUT" | AW_LOG_LEVEL=none "$META" outbox-record 2> /dev/null)"; rc=$?
[ "$rc" = 0 ] || { failopen "meta-exit-$rc"; exit 0; }
W="$(printf '%s\n' "$OUT" | grep -m1 '^aw-meta: warning: ')"
[ -n "$W" ] || exit 0
jq -nc --arg m "$W" '{hookSpecificOutput: {hookEventName: "PostToolUse", additionalContext: $m}}' 2> /dev/null || true
exit 0
