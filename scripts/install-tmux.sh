#!/usr/bin/env bash
# The toolkit's tmux config (config/tmux/tmux.conf: a purple status bar in ANSI color names with the session, windows,
# the active pane's git branch, its folder and the host), installed to ~/.tmux.conf. Opt-in: ./setup.sh --with-tmux.
#
#   tmux missing         installed through config/lib/sys-pkg.sh; when that fails, a skip line and exit 0
#   no ~/.tmux.conf      the config is written
#   same content         "already installed", nothing written
#   other content        the old file is copied to ~/.tmux.conf.aw-bak first (a later different file goes to
#                        ~/.tmux.conf.aw-bak.<timestamp>, so no backup is ever overwritten), then the config is written
#
# The config has no session-restore plugins (TPM, tmux-resurrect, tmux-continuum). Every `tmux -L <team>` server shares one
# resurrect save, and continuum restores it into any tmux server that starts alone, so a team launch got an old session
# (and had its client switched to it) in place of its own lane.
#
# It never starts tmux for you and never touches a shell rc. Fail-open: a failure warns and exits 0, so setup.sh goes on.
#
# Usage: scripts/install-tmux.sh [--dry-run]
#   AW_DRY_RUN=1 is the same as --dry-run (setup.sh sets it).
set -euo pipefail

DRY="${AW_DRY_RUN:-0}"
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    *) echo "usage: install-tmux.sh [--dry-run]" >&2; exit 1 ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/config/tmux/tmux.conf"
CONF="$HOME/.tmux.conf"
BAK="$CONF.aw-bak"

# shellcheck source=config/lib/sys-pkg.sh
source "$ROOT/config/lib/sys-pkg.sh"

if ! command -v tmux >/dev/null 2>&1; then
  if [ "$DRY" = "1" ]; then
    echo "  tmux: not installed"
    AW_DRY_RUN=1 aw_sys_install tmux || true
  else
    echo "  tmux: not installed; installing it"
    if ! AW_DRY_RUN=0 aw_sys_install tmux || ! command -v tmux >/dev/null 2>&1; then
      echo "  tmux: skipped: tmux could not be installed (non-fatal); install it, then re-run ./setup.sh --with-tmux"
      exit 0
    fi
  fi
fi

if [ -f "$CONF" ] && cmp -s "$SRC" "$CONF"; then
  echo "  tmux: $CONF already installed"
elif [ "$DRY" = "1" ]; then
  if [ -e "$CONF" ]; then
    echo "  [dry-run] would back up $CONF to $([ -e "$BAK" ] && echo "$BAK.<timestamp>" || echo "$BAK") and write $CONF"
  else
    echo "  [dry-run] would write $CONF"
  fi
else
  if [ -e "$CONF" ]; then
    bak="$BAK"; [ -e "$bak" ] && bak="$BAK.$(date +%Y%m%d%H%M%S)"
    cp -p "$CONF" "$bak" || { echo "  tmux: WARN: could not back up $CONF to $bak; left it as it is (non-fatal)"; exit 0; }
    echo "  tmux: backed up $CONF to $bak"
  fi
  cp "$SRC" "$CONF" || { echo "  tmux: WARN: could not write $CONF (non-fatal)"; exit 0; }
  echo "  tmux: installed $CONF (in a running tmux: prefix + r reloads it)"
fi
