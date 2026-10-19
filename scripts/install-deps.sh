#!/usr/bin/env bash
# scripts/install-deps.sh — the external tools the toolkit shells out to, installed or checked once per box.
# Called by setup.sh; also runnable alone. Honors AW_DRY_RUN=1 (prints what it would do, writes nothing).
#
#   user-level, installed here (no root):
#     uv           Linux: astral.sh installer into ~/.local/bin; macOS: brew
#     graphify     the code index's feature-cluster tool, `uv tool install graphifyy==<pin> --exclude-newer <date>`;
#                  the pin is sindri/package.json's "sindri.graphifyPin" (what `sindri index setup` installs)
#     temporal, temporal-test-server   scripts/binaries-lock.sh fetch <name> (sha256-verified, idempotent) into
#                  ${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin/<name>-<pin>/, where sindri serve and the tests look;
#                  `temporal --version` is run afterwards and a failed fetch or a binary that won't run fails the script
#   systemd-run --user --scope is checked (a warning only: the Sindri sandbox's limits need a user manager; AW_DEPS_SYSTEMD_PROBE replaces the command, tests).
#   bwrap is also probed with a no-op sandbox (`bwrap --ro-bind / / true`); when that fails because AppArmor restricts
#   user namespaces (Ubuntu 24.04), the /etc/apparmor.d/bwrap profile is written and loaded with sudo, then probed again.
#   system packages, installed when missing through config/lib/sys-pkg.sh (sudo + apt-get/dnf/yum/pacman/zypper on
#   Linux, Homebrew on macOS, installing Homebrew itself when needed); AW_NO_SYSTEM_INSTALL=1 only prints the command:
#     Linux: bubblewrap (bwrap: graphify never runs unsandboxed), socat (the Sindri sandbox's forwarders), libsecret-tools (secret-tool: Sindri keychain:
#            pointers), clang + zlib1g-dev (NativeAOT), lsof, tmux, curl, git
#     macOS: tmux
#
#   bubblewrap and socat are SOFT until SB-5a wires the Sindri sandbox into a launch: installed when missing, but a failure to install them only warns (SOFT_PKGS below).
#
# Exit: 0 everything present or installed, 1 something could not be installed (system package, AppArmor fix, user-level tool).
# Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dry() { [ "${AW_DRY_RUN:-0}" = "1" ]; }
OS="${AW_DEPS_OS:-$(uname -s)}"
FAILED=0

case ":$PATH:" in *":$HOME/.local/bin:"*) ;; *) export PATH="$HOME/.local/bin:$PATH" ;; esac

echo "=== External tools (uv, graphify, Temporal, system packages) ==="

# --- system packages: check, then install what is missing (config/lib/sys-pkg.sh: sudo on Linux, Homebrew on macOS) ---
# shellcheck source=config/lib/sys-pkg.sh
source "$ROOT/config/lib/sys-pkg.sh"
MISSING_PKGS=""
BWRAP_BROKEN=0
BWRAP_RESTRICTED=""
need() {   # <command> <logical package (sys-pkg.sh)> [<what needs it>]
  if command -v "$1" &>/dev/null; then echo "  $1: found"; else
    echo "  $1: MISSING${3:+ ($3)}"; MISSING_PKGS="$MISSING_PKGS $2"
  fi
}
check_system() {
  MISSING_PKGS=""
  if [ "$OS" = "Linux" ]; then
    need bwrap bubblewrap "network sandbox for graphify; it never runs unsandboxed"
    need socat socat "Sindri sandbox forwarders; without it a sandboxed launch is SND-LAUNCH-014 no-forwarder"
    need secret-tool secret-tool "Sindri keychain: pointers"
    need clang clang "NativeAOT publish"
    if [ -e "${AW_DEPS_ZLIB_H:-/usr/include/zlib.h}" ]; then echo "  zlib.h: found"; else
      echo "  zlib.h: MISSING (NativeAOT publish)"; MISSING_PKGS="$MISSING_PKGS zlib-dev"
    fi
    need lsof lsof "port and lock probes"
    need tmux tmux "Sindri sessions"
    need curl curl "downloads"
    need git git
  elif [ "$OS" = "Darwin" ]; then
    need tmux tmux "Sindri sessions"
  fi
}
# bwrap present is not enough: Ubuntu 24.04 (kernel.apparmor_restrict_unprivileged_userns=1, no profile for bwrap) refuses
# the user namespace, so graphify could never run. Probe a no-op sandbox; AW_DEPS_BWRAP_PROBE replaces the command (tests).
probe_bwrap() {
  BWRAP_BROKEN=0
  [ "$OS" = "Linux" ] && command -v bwrap &>/dev/null || return 0
  if _why="$(eval "${AW_DEPS_BWRAP_PROBE:-bwrap --ro-bind / / true}" 2>&1)"; then echo "  bwrap: sandbox works"; else
    echo "  bwrap: found but cannot create a sandbox (${_why:-no output})"; BWRAP_BROKEN=1
    _restrict=""; read -r _restrict <"${AW_DEPS_USERNS_FILE:-/proc/sys/kernel/apparmor_restrict_unprivileged_userns}" 2>/dev/null || true
    BWRAP_RESTRICTED="$_restrict"
  fi
}

# The sandbox's cgroup limits run through `systemd-run --user --scope`. Not a package to install (systemd is the base system),
# and the sandbox is not wired into a launch yet, so this only warns: a missing user manager is SND-LAUNCH-014 no-user-manager.
probe_systemd_run() {
  [ "$OS" = "Linux" ] || return 0
  if ! command -v systemd-run &>/dev/null; then
    echo "  systemd-run: MISSING (WARN: the Sindri sandbox's limits need a systemd user manager)"; return 0
  fi
  if eval "${AW_DEPS_SYSTEMD_PROBE:-systemd-run --user --scope --quiet true}" >/dev/null 2>&1; then echo "  systemd-run: user scope works"; else
    echo "  systemd-run: found but no user scope (WARN: the Sindri sandbox's limits need a systemd user manager; loginctl enable-linger \"\$USER\")"
  fi
}

# Installed when missing, but a failure to install them only warns until SB-5a wires the Sindri sandbox into a launch (flip this list then).
SOFT_PKGS="bubblewrap socat"
hard_missing() {   # the packages in $MISSING_PKGS that setup cannot go on without
  local p hard=""
  for p in $MISSING_PKGS; do case " $SOFT_PKGS " in *" $p "*) ;; *) hard="$hard $p" ;; esac; done
  echo "$hard"
}

check_system
if [ -n "$MISSING_PKGS" ]; then
  echo ""
  echo "  Installing missing system packages:$MISSING_PKGS"
  INITIAL_HARD="$(hard_missing)"
  if aw_sys_install $MISSING_PKGS && ! dry; then
    echo "  Checking again:"
    check_system
    if [ -n "$MISSING_PKGS" ]; then
      echo "  WARN: still missing after the install:$MISSING_PKGS"
      if [ -n "$(hard_missing)" ]; then FAILED=1; else echo "  (only $SOFT_PKGS: nothing needs them until the Sindri sandbox is wired in, so setup goes on)"; fi
    fi
  elif ! dry; then
    if [ -n "$INITIAL_HARD" ]; then FAILED=1; else echo "  WARN: could not install$MISSING_PKGS (nothing needs them until the Sindri sandbox is wired in, so setup goes on)"; fi
  fi
fi
probe_bwrap
probe_systemd_run

# --- uv ---
if command -v uv &>/dev/null; then
  echo "  uv: already installed ($(uv --version 2>/dev/null || echo 'unknown version'))"
elif dry; then
  echo "  [dry-run] would install uv (brew on macOS, astral.sh/uv/install.sh elsewhere)"
else
  echo "  Installing uv..."
  if [ "$OS" = "Darwin" ]; then aw_ensure_brew && brew install uv || true
  elif command -v curl &>/dev/null; then curl -LsSf https://astral.sh/uv/install.sh | sh || true; fi
  if command -v uv &>/dev/null; then echo "  uv: installed"; else
    echo "  WARN: uv install failed; install it (https://docs.astral.sh/uv/getting-started/installation/) and rerun"; FAILED=1
  fi
fi

# --- graphify at the pin sindri/package.json declares ---
PKG="$ROOT/sindri/package.json"
PIN="$(jq -r '.sindri.graphifyPin // empty' "$PKG" 2>/dev/null)"
PIN_DATE="$(jq -r '.sindri.graphifyPinDate // empty' "$PKG" 2>/dev/null)"
if [ -z "$PIN" ] || [ -z "$PIN_DATE" ]; then
  echo "  WARN: sindri/package.json has no sindri.graphifyPin; skipping graphify"; FAILED=1
elif command -v graphify &>/dev/null && [ "$(graphify --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)" = "$PIN" ]; then
  echo "  graphify: already installed ($PIN)"
elif dry; then
  echo "  [dry-run] would run: uv tool install graphifyy==$PIN --exclude-newer $PIN_DATE"
elif command -v uv &>/dev/null; then
  echo "  Installing graphifyy==$PIN (dependencies age-gated to $PIN_DATE)..."
  if uv tool install "graphifyy==$PIN" --exclude-newer "$PIN_DATE"; then echo "  graphify: installed ($PIN)"; else
    echo "  WARN: graphify install failed; run: uv tool install graphifyy==$PIN --exclude-newer $PIN_DATE"; FAILED=1
  fi
else
  echo "  WARN: graphify not installed (uv missing)"; FAILED=1
fi

# --- Temporal CLI + time-skipping test server (binaries.lock) ---
for _bin in temporal temporal-test-server; do
  if dry; then
    echo "  [dry-run] would run: scripts/binaries-lock.sh fetch $_bin"
  elif _path="$(bash "$ROOT/scripts/binaries-lock.sh" fetch "$_bin")"; then
    # sindri serve and the tests look for the CLI at <AW_STATE_DIR>/cache/bin/temporal-<pin>/temporal (the path fetch printed)
    if [ "$_bin" = "temporal" ]; then
      if _v="$("$_path" --version 2>&1)"; then echo "  $_bin: $_path ($_v)"; else
        echo "  WARN: $_path does not run: $_v"; FAILED=1
      fi
    else
      echo "  $_bin: $_path"
    fi
  else
    echo "  WARN: scripts/binaries-lock.sh fetch $_bin failed"; FAILED=1
  fi
done

# --- bwrap present but unusable: apply the AppArmor profile (root), probe again ---
BWRAP_PROFILE='abi <abi/4.0>,
include <tunables/global>
profile bwrap /usr/bin/bwrap flags=(unconfined) {
  userns,
  include if exists <local/bwrap>
}'
if [ "$BWRAP_BROKEN" = "1" ]; then
  echo ""
  if [ "$BWRAP_RESTRICTED" = "1" ]; then
    echo "  AppArmor blocks unprivileged user namespaces (kernel.apparmor_restrict_unprivileged_userns=1) and has no profile"
    echo "  for /usr/bin/bwrap, so graphify's sandbox cannot start. Adding the profile /etc/apparmor.d/bwrap:"
    printf '%s\n' "$BWRAP_PROFILE" | sed 's/^/    | /'
    if dry; then
      echo "  [dry-run] would run: sudo tee /etc/apparmor.d/bwrap; sudo apparmor_parser -r /etc/apparmor.d/bwrap"
      echo "  [dry-run] a real run would apply it and probe again"
    elif [ "${AW_NO_SYSTEM_INSTALL:-0}" = "1" ]; then
      echo "  AW_NO_SYSTEM_INSTALL=1, so nothing is applied. Write that profile with sudo tee, then run:"
      echo "    sudo apparmor_parser -r /etc/apparmor.d/bwrap"
      FAILED=1
    elif printf '%s\n' "$BWRAP_PROFILE" | aw_sys_root tee /etc/apparmor.d/bwrap >/dev/null \
      && aw_sys_root apparmor_parser -r /etc/apparmor.d/bwrap; then
      probe_bwrap
      [ "$BWRAP_BROKEN" = "1" ] && FAILED=1
    else
      echo "  WARN: applying the AppArmor profile failed"; FAILED=1
    fi
  else
    echo "  bwrap cannot create a sandbox and the AppArmor userns restriction is not the cause; run \`bwrap --ro-bind / / true\` and read its error."
    if dry; then echo "  [dry-run] a real run would stop here"; else FAILED=1; fi
  fi
fi

[ "$FAILED" = "0" ]
