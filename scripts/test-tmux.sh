#!/usr/bin/env bash
# scripts/test-tmux.sh — source this from any test or scratch script that needs a real tmux. Never run plain `tmux` from one.
#
# Why: a shell that runs inside a tmux client has $TMUX set, and tmux follows $TMUX before $TMUX_TMPDIR, so a test that only
# exported TMUX_TMPDIR still sent `new-session` and `kill-server` to the box's own server (the live /meta team's). This helper
# makes that impossible:
#   * it unsets TMUX, and the lane environment a lane or meta pane exports (the list scripts/ci/bash-tests.sh strips), which the
#     private server would otherwise inherit into its global environment (#1420);
#   * it makes a temp dir (mktemp -d, so its path matches */tmp.*/*) and exports TMUX_TMPDIR under it;
#   * it picks a socket name AW_TEST_TMUX (unique per run: t1-<pid>-<random>; a caller may preset a name for the team under test,
#     since the private TMUX_TMPDIR already makes it unique to this run) and defines
#         ttmux() { command tmux -L "$AW_TEST_TMUX" "$@"; }
#   * it puts a `tmux` shim first on PATH, so a script under test that calls plain `tmux` reaches the same private server and
#     the shim refuses (exit 2) if TMUX is set, TMUX_TMPDIR is not exactly this run's own temp dir, or an argument is -S;
#   * it checks, with `ttmux`, that the socket resolves inside its temp dir; if it resolves to /tmp/tmux-<uid>/default or anywhere
#     else, it kills only its own marker session and returns 2 (a sourced script should `|| exit 2`).
# ttmux_cleanup stops the private server and deletes the temp dir; it is installed as the EXIT trap when none is set.
# Variables: AW_TEST_TMUX_DIR (the temp dir), AW_TEST_TMUX (the socket name), TMUX_TMPDIR.
# Usage:  . "$(dirname "$0")/../test-tmux.sh" || exit 2
#         ttmux new-session -d -s keepalive "sleep 600"

if ! command -v tmux > /dev/null 2>&1; then
  echo "test-tmux: tmux is not installed" >&2
  return 2 2> /dev/null || exit 2
fi
unset TMUX AW_META_TEAM AW_META_DIR AW_META_LANE AW_META_LANE_PID AW_META_LANE_AGENT AW_META_LANE_PROC AW_SINDRI_CHILD AW_SINDRI_LAUNCH AW_LAUNCHER_CONFIG
_tt_real="$(type -P tmux)"
AW_TEST_TMUX_DIR="$(cd "$(mktemp -d)" && pwd -P)"   # physical path: tmux resolves its socket dir through symlinks
export TMUX_TMPDIR="$AW_TEST_TMUX_DIR/tmux"
mkdir -p "$TMUX_TMPDIR" "$AW_TEST_TMUX_DIR/bin"
case "${AW_TEST_TMUX:-}" in
  "") AW_TEST_TMUX="t1-$$-$RANDOM" ;;
  *[!a-z0-9-]*) echo "test-tmux: AW_TEST_TMUX '$AW_TEST_TMUX' must be lowercase letters, digits and dashes" >&2; return 2 2> /dev/null || exit 2 ;;
esac
case "$AW_TEST_TMUX" in default) echo "test-tmux: AW_TEST_TMUX must not be 'default'" >&2; return 2 2> /dev/null || exit 2 ;; esac
export AW_TEST_TMUX AW_TEST_TMUX_DIR

printf '%s\n' '#!/usr/bin/env bash' \
  '[ -z "${TMUX:-}" ] || { echo "test-tmux shim: TMUX is set: refusing to run tmux" >&2; exit 2; }' \
  "case \"\${TMUX_TMPDIR:-}\" in \"$AW_TEST_TMUX_DIR/tmux\"|\"$AW_TEST_TMUX_DIR/tmux/\") ;; *) echo \"test-tmux shim: TMUX_TMPDIR is not this run's own temp dir: refusing to run tmux\" >&2; exit 2 ;; esac" \
  'for a in "$@"; do case "$a" in -S|-S*) echo "test-tmux shim: -S (an explicit socket path) is refused: use -L <name>" >&2; exit 2 ;; esac; done' \
  "case \"\${1:-}\" in -L) exec \"$_tt_real\" \"\$@\" ;; esac" \
  "exec \"$_tt_real\" -L \"$AW_TEST_TMUX\" \"\$@\"" > "$AW_TEST_TMUX_DIR/bin/tmux"
chmod +x "$AW_TEST_TMUX_DIR/bin/tmux"
export PATH="$AW_TEST_TMUX_DIR/bin:$PATH"

ttmux() { command tmux -L "$AW_TEST_TMUX" "$@"; }

ttmux_cleanup() {
  if [ -n "${AW_TEST_TMUX_DIR:-}" ] && [ -d "$AW_TEST_TMUX_DIR" ]; then
    TMUX='' TMUX_TMPDIR="$AW_TEST_TMUX_DIR/tmux" "$_tt_real" -L "$AW_TEST_TMUX" kill-server 2> /dev/null
    rm -rf "$AW_TEST_TMUX_DIR"
  fi
}
[ -n "$(trap -p EXIT)" ] || trap ttmux_cleanup EXIT

# The check: where does this socket live? It must be inside our temp dir, never the default socket.
_tt_expected="$AW_TEST_TMUX_DIR/tmux/tmux-$(id -u)/$AW_TEST_TMUX"
_tt_path="$(ttmux new-session -d -s awtest-guard \; display-message -p '#{socket_path}' 2> /dev/null)"
case "$_tt_path" in
  "$AW_TEST_TMUX_DIR"/*) [ "$_tt_path" = "$_tt_expected" ] || _tt_path="" ;;
  *) _tt_path="" ;;
esac
if [ -z "$_tt_path" ]; then
  ttmux kill-session -t awtest-guard 2> /dev/null   # only our own marker session; never kill-server before the socket is known to be ours
  echo "test-tmux: the test socket does not resolve inside $AW_TEST_TMUX_DIR (expected $_tt_expected): refusing" >&2
  unset _tt_real _tt_expected _tt_path
  return 2 2> /dev/null || exit 2
fi
ttmux kill-session -t awtest-guard 2> /dev/null
unset _tt_expected _tt_path
