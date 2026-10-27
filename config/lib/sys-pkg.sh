# config/lib/sys-pkg.sh — install system packages for setup.sh and the install scripts, on macOS (Homebrew) and Linux
# (apt-get, dnf, yum, pacman, zypper). Sourced, never run. Bash 3.2 compatible (macOS /bin/bash).
#
#   aw_sys_install <name>...  install the named packages (logical names below), mapped to this box's package manager.
#                             Linux runs the manager through sudo when not root (sudo prompts when it needs to); macOS
#                             installs Homebrew first when it is missing. Returns 1 when it cannot install.
#   aw_sys_root <cmd>...      run one command as root (sudo when not root); returns 1 when there is no way to.
#   aw_ensure_brew            macOS: put Homebrew on PATH, installing it when missing (it brings the Xcode Command Line Tools).
#
#   AW_DRY_RUN=1              print the commands, run nothing.
#   AW_NO_SYSTEM_INSTALL=1    never install: print the exact command and return 1 (for CI and boxes you don't administer).
#   AW_DEPS_OS                overrides `uname -s` (tests).
#
# Logical names: jq build-tools tmux bubblewrap secret-tool clang zlib-dev lsof curl git socat python3 pip docker.
# A name with no package on a platform (bubblewrap on macOS, build-tools under Homebrew) is skipped there.

_aw_sys_os() { echo "${AW_DEPS_OS:-$(uname -s)}"; }

_aw_sys_mgr() {
  if [ "$(_aw_sys_os)" = "Darwin" ]; then echo brew; return; fi
  local m
  for m in apt-get dnf yum pacman zypper; do
    if command -v "$m" >/dev/null 2>&1; then echo "$m"; return; fi
  done
  echo none
}

# _aw_sys_pkg <manager> <name>: the package(s) that provide <name>, empty when the platform needs none.
_aw_sys_pkg() {
  case "$1:$2" in
    brew:build-tools|brew:bubblewrap|brew:secret-tool|brew:clang|brew:zlib-dev|brew:lsof) echo "" ;;  # Command Line Tools / base system
    brew:python3|brew:pip) echo "python@3.13" ;;
    brew:docker) echo "cask:docker-desktop" ;;
    apt-get:build-tools) echo "build-essential" ;;
    pacman:build-tools) echo "base-devel" ;;
    *:build-tools) echo "gcc-c++ make" ;;
    apt-get:secret-tool|zypper:secret-tool) echo "libsecret-tools" ;;
    *:secret-tool) echo "libsecret" ;;
    apt-get:zlib-dev) echo "zlib1g-dev" ;;
    pacman:zlib-dev) echo "zlib" ;;
    *:zlib-dev) echo "zlib-devel" ;;
    pacman:python3) echo "python" ;;
    pacman:pip) echo "python-pip" ;;
    *:pip) echo "python3-pip" ;;
    apt-get:docker) echo "docker.io" ;;
    dnf:docker|yum:docker) echo "moby-engine" ;;
    *) echo "$2" ;;
  esac
}

# _aw_sys_sudo: prints the prefix that makes a command run as root ("" as root, "sudo " otherwise); fails without sudo.
_aw_sys_sudo() {
  if [ "${EUID:-$(id -u)}" = "0" ]; then echo ""; return 0; fi
  command -v sudo >/dev/null 2>&1 || return 1
  echo "sudo "
}

aw_sys_root() {
  local s
  if ! s="$(_aw_sys_sudo)"; then echo "  root is needed for: $* (and sudo is not installed)"; return 1; fi
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then echo "  [dry-run] would run: ${s}$*"; return 0; fi
  # shellcheck disable=SC2086
  ${s}"$@"
}

aw_ensure_brew() {
  [ "$(_aw_sys_os)" = "Darwin" ] || return 0
  command -v brew >/dev/null 2>&1 && return 0
  local b
  for b in /opt/homebrew/bin/brew /usr/local/bin/brew; do
    if [ -x "$b" ]; then eval "$("$b" shellenv)"; return 0; fi
  done
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then echo "  [dry-run] would install Homebrew (https://brew.sh)"; return 0; fi
  if [ "${AW_NO_SYSTEM_INSTALL:-0}" = "1" ]; then
    echo "  Homebrew is missing and AW_NO_SYSTEM_INSTALL=1. Install it from https://brew.sh, then re-run."; return 1
  fi
  echo "  Installing Homebrew (it asks for your password once)..."
  sudo -v || { echo "  Homebrew needs an administrator password; install it from https://brew.sh, then re-run."; return 1; }
  NONINTERACTIVE=1 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)" || {
    echo "  Homebrew install failed; install it from https://brew.sh, then re-run."; return 1; }
  for b in /opt/homebrew/bin/brew /usr/local/bin/brew; do
    if [ -x "$b" ]; then eval "$("$b" shellenv)"; return 0; fi
  done
  echo "  Homebrew installed but brew is not where it should be (/opt/homebrew or /usr/local)."; return 1
}

aw_sys_install() {
  local mgr pkgs="" n p s="" cmd
  mgr="$(_aw_sys_mgr)"
  for n in "$@"; do
    p="$(_aw_sys_pkg "$mgr" "$n")"
    [ -n "$p" ] && pkgs="$pkgs $p"
  done
  pkgs="${pkgs# }"
  [ -n "$pkgs" ] || return 0
  if [ "$mgr" = "none" ]; then
    echo "  No supported package manager (apt-get, dnf, yum, pacman, zypper); install these yourself: $*"; return 1
  fi
  if [ "$mgr" != "brew" ] && ! s="$(_aw_sys_sudo)"; then
    echo "  Installing $pkgs needs root, and sudo is not installed. As root, run: $mgr install $pkgs"; return 1
  fi
  case "$mgr" in
    brew)    # formulae and casks go in separate commands: --cask applies to every name on its line
      local f="" c=""
      for p in $pkgs; do case "$p" in cask:*) c="$c ${p#cask:}" ;; *) f="$f $p" ;; esac; done
      cmd="${f:+brew install$f}${f:+${c:+ && }}${c:+brew install --cask$c}" ;;
    apt-get) cmd="${s}apt-get install -y $pkgs" ;;
    dnf|yum) cmd="${s}$mgr install -y $pkgs" ;;
    pacman)  cmd="${s}pacman -S --needed --noconfirm $pkgs" ;;
    zypper)  cmd="${s}zypper --non-interactive install $pkgs" ;;
  esac
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then
    [ "$mgr" = "brew" ] && aw_ensure_brew
    echo "  [dry-run] would run: $cmd"
    return 0
  fi
  if [ "${AW_NO_SYSTEM_INSTALL:-0}" = "1" ]; then
    echo "  AW_NO_SYSTEM_INSTALL=1, so nothing is installed. Run:"
    echo "    $cmd"
    return 1
  fi
  if [ "$mgr" = "brew" ]; then aw_ensure_brew || return 1; fi
  if [ "$mgr" = "apt-get" ] && [ -z "${_AW_SYS_APT_UPDATED:-}" ]; then
    echo "  Running: ${s}apt-get update"
    ${s}apt-get update -qq || echo "  WARN: apt-get update failed; installing from the current package lists"
    _AW_SYS_APT_UPDATED=1
  fi
  echo "  Running: $cmd"
  # the command is built here from fixed words and package names (no user input), so eval is safe
  eval "$cmd"
}
