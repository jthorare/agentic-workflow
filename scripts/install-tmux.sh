#!/usr/bin/env bash
# The toolkit's tmux config (config/tmux/tmux.conf: a purple status bar in ANSI color names with the session, windows,
# the active pane's git branch, its folder and the host), installed to ~/.tmux.conf. Opt-in: ./setup.sh --with-tmux.
#
#   tmux missing         installed through config/lib/sys-pkg.sh; when that fails, a skip line and exit 0
#   no ~/.tmux.conf      the config is written
#   same content         "already installed", nothing written
#   other content        the old file is copied to ~/.tmux.conf.aw-bak first (a later different file goes to
#                        ~/.tmux.conf.aw-bak.<timestamp>, so no backup is ever overwritten), then the config is written
#   --restore            also appends config/tmux/restore.conf (TPM, tmux-resurrect, tmux-continuum) as the last lines,
#                        clones TPM to ~/.tmux/plugins/tpm and installs the plugins on a private tmux server
#                        (tmux -L, never the default one: install_plugins reads TMUX_PLUGIN_MANAGER_PATH from the server
#                        that ran tpm, and a bare call asks the default server and fails with "unknown variable")
#
# It never starts tmux for you and never touches a shell rc. Fail-open: a failure warns and exits 0, so setup.sh goes on.
#
# Usage: scripts/install-tmux.sh [--restore] [--dry-run]
#   AW_DRY_RUN=1 is the same as --dry-run (setup.sh sets it).
#   AW_TMUX_TPM_URL  the TPM git URL (default https://github.com/tmux-plugins/tpm; tests point it at a local repo)
set -euo pipefail

DRY="${AW_DRY_RUN:-0}"
RESTORE=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    --restore) RESTORE=1 ;;
    *) echo "usage: install-tmux.sh [--restore] [--dry-run]" >&2; exit 1 ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/config/tmux/tmux.conf"
RESTORE_SRC="$ROOT/config/tmux/restore.conf"
CONF="$HOME/.tmux.conf"
BAK="$CONF.aw-bak"
TPM_DIR="$HOME/.tmux/plugins/tpm"
TPM_URL="${AW_TMUX_TPM_URL:-https://github.com/tmux-plugins/tpm}"
TPM_REF="v3.1.0"

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

# The file ~/.tmux.conf should hold: the config, plus the restore block when asked for.
want="$(mktemp)"
trap 'rm -f "$want"' EXIT
cat "$SRC" > "$want"
if [ "$RESTORE" = "1" ]; then cat "$RESTORE_SRC" >> "$want"; fi

if [ -f "$CONF" ] && cmp -s "$want" "$CONF"; then
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
  cp "$want" "$CONF" || { echo "  tmux: WARN: could not write $CONF (non-fatal)"; exit 0; }
  echo "  tmux: installed $CONF (in a running tmux: prefix + r reloads it)"
fi

[ "$RESTORE" = "1" ] || exit 0

if [ -d "$TPM_DIR/.git" ]; then
  echo "  tmux: TPM already in $TPM_DIR"
elif [ "$DRY" = "1" ]; then
  echo "  [dry-run] would clone TPM $TPM_REF to $TPM_DIR and install the plugins on a private tmux server"
  exit 0
else
  mkdir -p "$(dirname "$TPM_DIR")"
  git clone -q --depth 1 --branch "$TPM_REF" "$TPM_URL" "$TPM_DIR" 2>/dev/null \
    || { echo "  tmux: WARN: could not clone TPM from $TPM_URL (non-fatal); session restore is not set up"; exit 0; }
  echo "  tmux: cloned TPM $TPM_REF to $TPM_DIR"
fi
PLUGINS="$HOME/.tmux/plugins"
# Installed plugins are never re-installed: a private server would load the real continuum with restore on, and with no
# other tmux server running it could restore the last save into that throwaway server. On a first install continuum
# isn't on disk yet when tpm runs, so nothing restores.
if [ -d "$PLUGINS/tmux-resurrect" ] && [ -d "$PLUGINS/tmux-continuum" ]; then
  echo "  tmux: plugins already installed"
  exit 0
fi
[ "$DRY" = "1" ] && { echo "  [dry-run] would install the plugins on a private tmux server"; exit 0; }

# A private server reads the new config, which runs tpm and so sets TMUX_PLUGIN_MANAGER_PATH; install_plugins runs inside it.
sock="aw-tpm-$$"
unset TMUX
if tmux -L "$sock" -f "$CONF" new-session -d -s aw-tpm 2>/dev/null \
  && tmux -L "$sock" run-shell "$TPM_DIR/bin/install_plugins" >/dev/null 2>&1; then
  echo "  tmux: plugins installed (tmux-resurrect, tmux-continuum); a claude pane comes back as a shell: run claude --resume"
else
  echo "  tmux: WARN: plugin install failed (non-fatal); in tmux, press prefix + I to install them"
fi
tmux -L "$sock" kill-server 2>/dev/null || true
