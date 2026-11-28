#!/usr/bin/env bash
# Tests for scripts/install-tmux.sh (the toolkit's tmux config, ./setup.sh --with-tmux) and setup.sh's --with-tmux flag.
# Run: bash scripts/tests/install-tmux.test.sh
# Everything runs under a temp HOME. tmux runs only on private sockets (tmux -L) with TMUX unset, never the default
# server, so a live tmux session (agent lanes) is never touched.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
INSTALL="$ROOT/scripts/install-tmux.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac; }
lacks() { case "$2" in *"$3"*) echo "not ok - $1"; echo "  unexpected: $3"; fail=1 ;; *) echo "ok - $1" ;; esac; }

unset TMUX TMUX_PANE AW_DRY_RUN
if ! command -v tmux >/dev/null 2>&1; then echo "ok - # SKIP tmux is not installed"; exit 0; fi

WORK="$(mktemp -d)"
SOCK="aw-tmux-test-$$"
cleanup() { tmux -L "$SOCK" kill-server 2>/dev/null; rm -rf "$WORK"; }
trap cleanup EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
CONF="$HOME/.tmux.conf"
run() { bash "$INSTALL" "$@" 2>&1; }
same_as_toolkit() { cmp -s "$CONF" "$ROOT/config/tmux/tmux.conf" && echo same || echo differs; }
backups() { find "$HOME" -maxdepth 1 -name '.tmux.conf.aw-bak*' | wc -l | tr -d ' '; }

# --- usage ---
out="$(run --bogus)"; rc=$?
check "an unknown flag is a usage error" "$rc" "1"
contains "the usage line names the flag" "$out" "usage: install-tmux.sh [--dry-run]"

# --- dry run with no ~/.tmux.conf ---
out="$(run --dry-run)"; rc=$?
check "dry run exits 0" "$rc" "0"
contains "dry run names the target path" "$out" "[dry-run] would write $CONF"
check "dry run writes nothing" "$([ -e "$CONF" ] && echo yes || echo no)" "no"
out="$(AW_DRY_RUN=1 run)"
contains "AW_DRY_RUN=1 is a dry run" "$out" "[dry-run] would write $CONF"

# --- clean install, and the config parses on this tmux ---
out="$(run)"; rc=$?
check "install exits 0" "$rc" "0"
contains "install says it wrote the file" "$out" "installed $CONF"
check "~/.tmux.conf is the toolkit config" "$(same_as_toolkit)" "same"
check "no backup on a clean box" "$(backups)" "0"
tmux -L "$SOCK" -f "$CONF" new-session -d -s t -c "$HOME"; rc=$?
check "tmux starts with the config" "$rc" "0"
check "status-style is bg=default,fg=white" "$(tmux -L "$SOCK" show -gv status-style)" "bg=default,fg=white"
contains "the folder shows ~ for home" "$(tmux -L "$SOCK" display -p -t t "#{T:status-right}")" "#[fg=white]~ "
contains "the session chip turns yellow on the prefix key" "$(tmux -L "$SOCK" show -gv status-left)" "#{?client_prefix,#[bg=yellow"
lacks "no clock in status-right" "$(tmux -L "$SOCK" show -gv status-right)" "%H"
tmux -L "$SOCK" kill-server 2>/dev/null
# 256-color numbers would ignore the terminal's theme.
check "the config has no 256-color numbers" "$(grep -Ec 'colou?r[0-9]' "$ROOT/config/tmux/tmux.conf")" "0"

# --- re-run is idempotent ---
out="$(run)"; rc=$?
check "re-run exits 0" "$rc" "0"
contains "re-run says already installed" "$out" "already installed"
check "re-run makes no backup" "$(backups)" "0"

# --- an existing, different ~/.tmux.conf is backed up, and a second one never overwrites the first backup ---
printf 'set -g status-style bg=green\n' > "$CONF"
out="$(run --dry-run)"
contains "dry run names the backup" "$out" "would back up $CONF to $CONF.aw-bak and write $CONF"
check "dry run leaves the old file" "$(cat "$CONF")" "set -g status-style bg=green"
out="$(run)"
contains "install says it backed up" "$out" "backed up $CONF to $CONF.aw-bak"
check "the backup holds the old content" "$(cat "$CONF.aw-bak")" "set -g status-style bg=green"
check "~/.tmux.conf is the toolkit config after the backup" "$(same_as_toolkit)" "same"
printf 'set -g status-style bg=blue\n' > "$CONF"
run >/dev/null
check "a second different file leaves the first backup" "$(cat "$CONF.aw-bak")" "set -g status-style bg=green"
check "a second different file gets its own timestamped backup" "$(cat "$CONF".aw-bak.* 2>/dev/null)" "set -g status-style bg=blue"

# --- tmux missing and no package manager: a skip line, exit 0, nothing written ---
mkdir -p "$WORK/nobin" "$WORK/home2"
for t in bash dirname cat mktemp rm cmp cp date mkdir uname id; do ln -sf "$(command -v "$t")" "$WORK/nobin/$t"; done
out="$(HOME="$WORK/home2" PATH="$WORK/nobin" "$WORK/nobin/bash" "$INSTALL" 2>&1)"; rc=$?
check "no tmux: exit 0" "$rc" "0"
contains "no tmux: a skip line" "$out" "tmux: skipped: tmux could not be installed (non-fatal)"
check "no tmux: nothing written" "$([ -e "$WORK/home2/.tmux.conf" ] && echo yes || echo no)" "no"

# --- an old restore block (TPM, resurrect, continuum) is replaced by the plain config, with a backup ---
rm -f "$CONF".aw-bak*
{ cat "$ROOT/config/tmux/tmux.conf"; printf '%s\n' "set -g @plugin 'tmux-plugins/tmux-continuum'" "set -g @continuum-restore 'on'" "run '~/.tmux/plugins/tpm/tpm'"; } > "$CONF"
old="$(cat "$CONF")"
out="$(run)"; rc=$?
check "old restore block: exit 0" "$rc" "0"
contains "old restore block: backed up" "$out" "backed up $CONF to $CONF.aw-bak"
check "old restore block: the backup holds the old file" "$(cat "$CONF.aw-bak")" "$old"
check "old restore block: ~/.tmux.conf is the toolkit config" "$(same_as_toolkit)" "same"
lacks "old restore block: no continuum left" "$(cat "$CONF")" "continuum"

# --- --restore is gone ---
out="$(run --restore)"; rc=$?
check "--restore is a usage error" "$rc" "1"

# --- setup.sh: --with-tmux --dry-run prints the step and writes nothing; without the flag it never runs ---
mkdir -p "$WORK/shome"
run_setup() { HOME="$WORK/shome" AW_STATE_DIR="$WORK/sstate" AW_NO_SYSTEM_INSTALL=1 bash "$ROOT/setup.sh" --providers claude --dry-run "$@" 2>&1; }
out="$(run_setup --with-tmux)"; rc=$?
check "setup.sh --with-tmux --dry-run: exit 0" "$rc" "0"
contains "setup.sh --with-tmux --dry-run: names the target" "$out" "[dry-run] would write $WORK/shome/.tmux.conf"
check "setup.sh --with-tmux --dry-run: wrote nothing" "$([ -e "$WORK/shome/.tmux.conf" ] && echo yes || echo no)" "no"
out="$(run_setup --with-tmux-restore)"; rc=$?
check "setup.sh --with-tmux-restore is refused" "$rc" "1"
contains "setup.sh --with-tmux-restore: names the option" "$out" "unknown option: --with-tmux-restore"
out="$(run_setup)"
lacks "setup.sh without the flag: no tmux step" "$out" "=== tmux config ==="
exit $fail
