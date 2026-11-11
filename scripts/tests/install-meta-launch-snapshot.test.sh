#!/usr/bin/env bash
# Tests for the aw-meta-launch link that scripts/install-meta.sh --allow-worktree writes (#2640): the tree it was installed from
# is a throwaway worktree that the worktree sweep later removes, so the link must not point into it. The installer runs from a
# linked worktree of a temp git repo (AW_SKIP_BUILD=1 with a stub aw-meta, as in install-meta.test.sh), the worktree is removed,
# and the installed aw-meta-launch must still resolve and run a dry run.
# Run: bash scripts/tests/install-meta-launch-snapshot.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TOP="$DIR/.."
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
command -v tmux > /dev/null || { echo "ok - skipped: tmux not installed"; exit 0; }
# the dry run asks tmux which sessions exist: on a private server (scripts/test-tmux.sh), never the box's own
AW_TEST_TMUX=t1
. "$DIR/test-tmux.sh" || exit 2
WORK="$(mktemp -d)"
trap 'rm -r "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done   # the caller may be a meta session: its team config and bin must not reach the stubs
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_SKIP_BUILD=1 CLAUDE_LOCAL_BIN="$WORK/linkbin"
unset CLAUDE_DIR AW_DRY_RUN AW_HOOKS_DIR CODEX_HOOKS_FILE CODEX_HOME CURSOR_HOOKS_FILE CLAUDE_SETTINGS_FILE CLAUDE_HOOKS_DIR AW_HOOK_ADAPTER_FLAGS SETTINGS_FILE TOOLKIT_DIR AW_LAUNCHER_CONFIG
mkdir -p "$WORK/linuxbin" "$HOME/.claude" "$AW_STATE_DIR/bin"
printf '#!/usr/bin/env bash\necho Linux\n' > "$WORK/linuxbin/uname"; chmod +x "$WORK/linuxbin/uname"
export PATH="$WORK/linuxbin:$PATH"
echo '{}' > "$HOME/.claude/settings.json"
printf '#!/usr/bin/env bash\nexit 0\n' > "$AW_STATE_DIR/bin/aw-meta"; chmod +x "$AW_STATE_DIR/bin/aw-meta"

# The tree: the installer, everything the launcher reads (the scripts it sources, the build-path list, the launcher schema and
# data), and what the hook install copies.
mkdir -p "$WORK/tree/scripts" "$WORK/tree/schemas" "$WORK/tree/config/hooks/adapters" "$WORK/tree/config/lib" "$WORK/tree/config/launcher"
cp "$DIR/install-meta.sh" "$DIR/meta-launch.sh" "$DIR/meta-lane.sh" "$DIR/launcher-lib.sh" "$DIR/meta-team-lib.sh" "$DIR/meta-version-lib.sh" \
  "$DIR/meta-start-lock.sh" "$DIR/meta-build-paths.txt" "$WORK/tree/scripts/"
cp "$TOP/schemas/launcher.schema.json" "$WORK/tree/schemas/"
cp "$TOP/config/launcher/"*.json "$WORK/tree/config/launcher/"
cp "$TOP/config/hooks/adapters/"* "$WORK/tree/config/hooks/adapters/" 2> /dev/null
cp -r "$TOP/config/lib/." "$WORK/tree/config/lib/"
cp "$TOP/config/hooks/meta-stop-gate.sh" "$TOP/config/hooks/meta-outbox.sh" "$TOP/config/hooks/meta-board-line.sh" "$WORK/tree/config/hooks/"
cp "$TOP/global.json" "$WORK/tree/"
git -C "$WORK/tree" init -q && git -C "$WORK/tree" add -A && git -C "$WORK/tree" -c user.email=t@t -c user.name=t commit -qm t
git -C "$WORK/tree" worktree add -q --detach "$WORK/wt" HEAD

OUT="$(bash "$WORK/wt/scripts/install-meta.sh" --provider claude --allow-worktree 2>&1)"; RC=$?
check "install from a linked worktree with --allow-worktree: exit 0" "$RC" "0"
[ "$RC" = 0 ] || printf '%s\n' "$OUT" | sed 's/^/  /'
check "aw-meta-launch does not point into the worktree" \
  "$(case "$(readlink "$CLAUDE_LOCAL_BIN/aw-meta-launch" 2> /dev/null)" in ("$WORK/wt/"*) echo into-worktree ;; ('') echo none ;; (*) echo elsewhere ;; esac)" "elsewhere"

# The sweep removes the tree the install ran from.
git -C "$WORK/tree" worktree remove --force "$WORK/wt"
check "the worktree is gone" "$([ -e "$WORK/wt" ] && echo present || echo gone)" "gone"
check "aw-meta-launch still resolves after the worktree is removed (#2640)" \
  "$(readlink -e "$CLAUDE_LOCAL_BIN/aw-meta-launch" > /dev/null && echo resolves || echo dangles)" "resolves"

# ... and still runs: a dry run of a team, which sources every lib and reads the launcher schema and data.
mkdir -p "$AW_STATE_DIR/meta" "$WORK/repo"
jq -n --arg r "$WORK/repo" '{team:"t1", repo:$r, metaLane:"meta", lanes:["api"]}' > "$AW_STATE_DIR/meta/team.json"
OUT="$(AW_META_LEGACY_TMUX_SOCKET=legacy-snapshot "$CLAUDE_LOCAL_BIN/aw-meta-launch" --dry-run 2>&1)"; RC=$?
check "aw-meta-launch --dry-run runs from the install after the worktree is removed (#2640)" "$RC" "0"
[ "$RC" = 0 ] || printf '%s\n' "$OUT" | sed 's/^/  /'
check "the dry run names the lane's session" "$(grep -q 't1-api' <<< "$OUT" && echo named || echo missing)" "named"
tmux kill-server 2> /dev/null
exit "$fail"
