#!/usr/bin/env bash
# Tests for config/lib/sys-pkg.sh: each package manager gets its own package names and install command, a name with no
# package on a platform is skipped, sudo is used only when not root, and a box with no sudo or no manager fails clearly.
# Dry runs only (AW_DRY_RUN=1), on a PATH of fakes, so nothing is installed.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/sys-pkg-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

BIN="$TMPROOT/bin"; mkdir -p "$BIN"
for t in bash sh uname id; do p="$(command -v "$t")" && ln -s "$p" "$BIN/$t"; done
fake() { printf '#!/bin/sh\nexit 0\n' >"$BIN/$1"; chmod +x "$BIN/$1"; }
unfake() { find "$BIN" -name "$1" -delete; }

# run <os> <names...> (as this non-root user): aw_sys_install in a dry run; OUT and RC
run() {
  local os="$1"; shift
  set +e
  OUT="$(env -i PATH="$BIN" AW_DRY_RUN=1 AW_DEPS_OS="$os" bash -c 'source "$1"; shift; aw_sys_install "$@"' _ "$REPO/config/lib/sys-pkg.sh" "$@" 2>&1)"
  RC=$?
  set -e
}
expect() {   # <label> <rc> <regex>
  if [ "$RC" -eq "$2" ] && printf '%s\n' "$OUT" | has -E -- "$3"; then ok "$1"; else
    fail "$1: exit $RC, want $2, output:"; printf '%s\n' "$OUT" | sed 's/^/      | /'
  fi
}

echo "sys-pkg"

fake sudo
fake apt-get
run Linux secret-tool zlib-dev build-tools pip docker
expect "apt-get: Debian names through sudo" 0 "would run: sudo apt-get install -y libsecret-tools zlib1g-dev build-essential python3-pip docker.io$"

run Linux java
expect "apt-get: java is the JDK 21 headless JRE" 0 "would run: sudo apt-get install -y openjdk-21-jre-headless$"

unfake apt-get; fake dnf
run Linux java
expect "dnf: java is java-21-openjdk-headless" 0 "would run: sudo dnf install -y java-21-openjdk-headless$"
run Linux secret-tool zlib-dev build-tools
expect "dnf: Fedora names" 0 "would run: sudo dnf install -y libsecret zlib-devel gcc-c\\+\\+ make$"

unfake dnf; fake pacman
run Linux zlib-dev build-tools python3 pip
expect "pacman: Arch names, --needed --noconfirm" 0 "would run: sudo pacman -S --needed --noconfirm zlib base-devel python python-pip$"

run Linux java
expect "pacman: java is jre21-openjdk-headless" 0 "would run: sudo pacman -S --needed --noconfirm jre21-openjdk-headless$"

unfake pacman; fake zypper
run Linux java
expect "zypper: java is java-21-openjdk-headless" 0 "would run: sudo zypper --non-interactive install java-21-openjdk-headless$"
run Linux tmux
expect "zypper: non-interactive" 0 "would run: sudo zypper --non-interactive install tmux$"

unfake zypper
run Linux tmux
expect "no package manager fails clearly" 1 "No supported package manager"

fake apt-get; unfake sudo
run Linux tmux
expect "not root and no sudo fails clearly" 1 "needs root, and sudo is not installed"

fake brew
run Darwin tmux bubblewrap build-tools python3 docker
expect "brew: no sudo, Linux-only names skipped, docker is a cask" 0 "would run: brew install tmux python@3.13 && brew install --cask docker-desktop$"

run Darwin java
expect "brew: java is openjdk@21" 0 "would run: brew install openjdk@21$"

run Darwin bubblewrap clang
expect "nothing to install on macOS is a no-op" 0 "^$"

echo ""
echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
