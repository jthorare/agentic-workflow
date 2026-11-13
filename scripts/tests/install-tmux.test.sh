#!/usr/bin/env bash
# Tests for scripts/install-tmux.sh (the toolkit's tmux config, ./setup.sh --with-tmux) and setup.sh's --with-tmux flags.
# Run: bash scripts/tests/install-tmux.test.sh
# Everything runs under a temp HOME. tmux runs only on private sockets (tmux -L) with TMUX unset, never the default
# server, so a live tmux session (agent lanes) is never touched. TPM is a local stand-in repo; no network.
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

unset TMUX TMUX_PANE AW_DRY_RUN AW_TMUX_TPM_URL
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
contains "the usage line names both flags" "$out" "usage: install-tmux.sh [--restore] [--dry-run]"

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
check "the config has no 256-color numbers" "$(cat "$ROOT/config/tmux/tmux.conf" "$ROOT/config/tmux/restore.conf" | grep -Ec 'colou?r[0-9]')" "0"

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

# --- --restore: the plugin block goes last, TPM is cloned, install_plugins runs on a private server ---
# A stand-in TPM: `tpm` sets TMUX_PLUGIN_MANAGER_PATH on the server that runs it (as the real one does), and
# install_plugins records what it reads, so a call on a server that never ran tpm shows up as "missing".
TPM_SRC="$WORK/tpm-src"; mkdir -p "$TPM_SRC/bin"
cat > "$TPM_SRC/tpm" <<'SH'
#!/usr/bin/env bash
tmux set-environment -g TMUX_PLUGIN_MANAGER_PATH "$HOME/.tmux/plugins/"
SH
cat > "$TPM_SRC/bin/install_plugins" <<'SH'
#!/usr/bin/env bash
v="$(tmux show-environment -g TMUX_PLUGIN_MANAGER_PATH 2>/dev/null | cut -d= -f2-)"
echo "${v:-missing}" > "$HOME/install_plugins.ran"
mkdir -p "$HOME/.tmux/plugins/tmux-resurrect" "$HOME/.tmux/plugins/tmux-continuum"
SH
chmod +x "$TPM_SRC/tpm" "$TPM_SRC/bin/install_plugins"
git -C "$TPM_SRC" init -q && git -C "$TPM_SRC" add -A \
  && git -C "$TPM_SRC" -c user.name=t -c user.email=t@t commit -qm tpm && git -C "$TPM_SRC" tag v3.1.0
export AW_TMUX_TPM_URL="file://$TPM_SRC"
out="$(run --restore --dry-run)"
contains "restore dry run names the clone" "$out" "would clone TPM v3.1.0 to $HOME/.tmux/plugins/tpm"
check "restore dry run clones nothing" "$([ -e "$HOME/.tmux/plugins/tpm" ] && echo yes || echo no)" "no"
out="$(run --restore)"; rc=$?
check "restore exits 0" "$rc" "0"
check "the run line is the last line of ~/.tmux.conf" "$(tail -n 1 "$CONF")" "run '~/.tmux/plugins/tpm/tpm'"
contains "the conf keeps the base config" "$(cat "$CONF")" 'set -g status-style "bg=default,fg=white"'
check "TPM is cloned" "$([ -x "$HOME/.tmux/plugins/tpm/bin/install_plugins" ] && echo yes || echo no)" "yes"
check "install_plugins ran on a server that ran tpm" "$(cat "$HOME/install_plugins.ran" 2>/dev/null)" "$HOME/.tmux/plugins/"
contains "restore says the plugins are installed" "$out" "plugins installed"
n="$(backups)"
rm -f "$HOME/install_plugins.ran"
out="$(run --restore)"
contains "restore re-run: conf already installed" "$out" "already installed"
contains "restore re-run: TPM already there" "$out" "TPM already in"
contains "restore re-run: plugins already there" "$out" "plugins already installed"
# A private server with the real continuum loaded could restore a save into it, so a re-run starts none.
check "restore re-run starts no private server" "$([ -e "$HOME/install_plugins.ran" ] && echo ran || echo none)" "none"
check "restore re-run makes no new backup" "$(backups)" "$n"

# --- setup.sh: --with-tmux --dry-run prints the step and writes nothing; without the flag it never runs ---
mkdir -p "$WORK/shome"
run_setup() { HOME="$WORK/shome" AW_STATE_DIR="$WORK/sstate" AW_NO_SYSTEM_INSTALL=1 bash "$ROOT/setup.sh" --providers claude --dry-run "$@" 2>&1; }
out="$(run_setup --with-tmux)"; rc=$?
check "setup.sh --with-tmux --dry-run: exit 0" "$rc" "0"
contains "setup.sh --with-tmux --dry-run: names the target" "$out" "[dry-run] would write $WORK/shome/.tmux.conf"
check "setup.sh --with-tmux --dry-run: wrote nothing" "$([ -e "$WORK/shome/.tmux.conf" ] && echo yes || echo no)" "no"
out="$(run_setup --with-tmux-restore)"
contains "setup.sh --with-tmux-restore --dry-run: names the TPM clone" "$out" "would clone TPM"
out="$(run_setup)"
lacks "setup.sh without the flag: no tmux step" "$out" "=== tmux config ==="
exit $fail
