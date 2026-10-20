#!/usr/bin/env bash
# scripts/install-deps.sh — the external tools the toolkit shells out to, installed or checked once per box.
# Called by setup.sh; also runnable alone. Honors AW_DRY_RUN=1 (prints what it would do, writes nothing).
#
#   user-level, installed here (no root):
#     uv           Linux: astral.sh installer into ~/.local/bin; macOS: brew
#     graphify     the code index's feature-cluster tool, `uv tool install graphifyy==<pin> --exclude-newer <date>`;
#                  the pin is sindri/package.json's "sindri.graphifyPin" (what `sindri index setup` installs)
#     code-index graphify  the new code index's own copy: a virtual environment at ${AW_STATE_DIR:-~/.agentic-workflow}/cache/code-index/
#                  graphify-<version>-<requirements sha12>/, `uv pip install --require-hashes` from context/code-index-requirements.txt
#                  with the lock's --exclude-newer date (context/code-index.lock, scripts/code-index-lock.sh); the build checks that binary
#     ollama model the lock's embedding model: `ollama pull <name>` when `ollama list` doesn't show the pinned digest (the build never pulls).
#                  Ollama itself is not installed here: without it, or without a running server, this step warns and skips (embeddings are optional)
#     temporal, temporal-test-server   scripts/binaries-lock.sh fetch <name> (sha256-verified, idempotent) into
#                  ${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin/<name>-<pin>/, where sindri serve and the tests look;
#                  `temporal --version` is run afterwards and a failed fetch or a binary that won't run fails the script
#   bwrap is also probed with a no-op sandbox (`bwrap --ro-bind / / true`); when that fails because AppArmor restricts
#   user namespaces (Ubuntu 24.04), the /etc/apparmor.d/bwrap profile is written and loaded with sudo, then probed again.
#   system packages, installed when missing through config/lib/sys-pkg.sh (sudo + apt-get/dnf/yum/pacman/zypper on
#   Linux, Homebrew on macOS, installing Homebrew itself when needed); AW_NO_SYSTEM_INSTALL=1 only prints the command:
#     Linux: bubblewrap (bwrap: graphify never runs unsandboxed), libsecret-tools (secret-tool: Sindri keychain:
#            pointers), clang + zlib1g-dev (NativeAOT), lsof, tmux, curl, git
#     macOS: tmux
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

check_system
if [ -n "$MISSING_PKGS" ]; then
  echo ""
  echo "  Installing missing system packages:$MISSING_PKGS"
  if aw_sys_install $MISSING_PKGS && ! dry; then
    echo "  Checking again:"
    check_system
    if [ -n "$MISSING_PKGS" ]; then echo "  WARN: still missing after the install:$MISSING_PKGS"; FAILED=1; fi
  elif ! dry; then
    FAILED=1
  fi
fi
probe_bwrap

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

# --- code index pins (context/code-index.lock): graphify in its own environment, the Ollama embedding model ---
CI_LOCK="$ROOT/scripts/code-index-lock.sh"
if ! bash "$CI_LOCK" check >/dev/null 2>&1; then
  echo "  WARN: context/code-index.lock fails scripts/code-index-lock.sh check; skipping the code index pins"; FAILED=1
else
  CI_VER="$(bash "$CI_LOCK" get graphify-version)"; CI_DATE="$(bash "$CI_LOCK" get graphify-date)"; CI_SHA="$(bash "$CI_LOCK" get requirements-sha256)"
  CI_ENV="${AW_STATE_DIR:-$HOME/.agentic-workflow}/cache/code-index/graphify-$CI_VER-${CI_SHA:0:12}"
  CI_REQ="$ROOT/context/code-index-requirements.txt"
  if [ -x "$CI_ENV/bin/graphify" ] && [ "$("$CI_ENV/bin/graphify" --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)" = "$CI_VER" ]; then
    echo "  code-index graphify: already installed ($CI_ENV)"
  elif dry; then
    echo "  [dry-run] would run: uv venv $CI_ENV && uv pip install --python $CI_ENV/bin/python --require-hashes -r $CI_REQ --exclude-newer $CI_DATE"
  elif command -v uv &>/dev/null; then
    echo "  Installing graphifyy==$CI_VER into $CI_ENV (hash-pinned, dependencies age-gated to $CI_DATE)..."
    # A venv's scripts name their own path, so it is built where it lives; an earlier half-built one is removed first.
    if { [ ! -e "$CI_ENV" ] || find "$CI_ENV" -delete; } && mkdir -p "$(dirname "$CI_ENV")" && uv venv --quiet "$CI_ENV" \
      && uv pip install --quiet --python "$CI_ENV/bin/python" --require-hashes -r "$CI_REQ" --exclude-newer "$CI_DATE" \
      && [ "$("$CI_ENV/bin/graphify" --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)" = "$CI_VER" ]; then
      echo "  code-index graphify: installed ($CI_ENV)"
    else
      echo "  WARN: code-index graphify install failed; rerun after: uv venv $CI_ENV && uv pip install --python $CI_ENV/bin/python --require-hashes -r $CI_REQ --exclude-newer $CI_DATE"; FAILED=1
    fi
  else
    echo "  WARN: code-index graphify not installed (uv missing)"; FAILED=1
  fi

  CI_MODEL="$(bash "$CI_LOCK" get model)"; CI_DIGEST="$(bash "$CI_LOCK" get model-digest)"; CI_ID="${CI_DIGEST#sha256:}"; CI_ID="${CI_ID:0:12}"
  if ! command -v ollama &>/dev/null; then
    echo "  WARN: ollama not installed; skipping the embedding model $CI_MODEL (install Ollama, https://ollama.com/download, and rerun)"
  elif dry; then
    echo "  [dry-run] would run: ollama pull $CI_MODEL (unless ollama list shows id $CI_ID)"
  elif ! _list="$(ollama list 2>&1)"; then
    echo "  WARN: the Ollama server does not answer; start it (the app, or \`ollama serve\`) and rerun to pull $CI_MODEL"
  elif printf '%s\n' "$_list" | awk -v m="$CI_MODEL" -v id="$CI_ID" '$1 == m && $2 == id { f = 1 } END { exit !f }'; then
    echo "  ollama model: already pulled ($CI_MODEL, $CI_ID)"
  else
    echo "  Pulling $CI_MODEL (about 275 MB)..."
    if ! ollama pull "$CI_MODEL"; then
      echo "  WARN: ollama pull $CI_MODEL failed; run it by hand"; FAILED=1
    else
      # The pin is the digest, and a moved tag pulls another one: read the list again and compare the ids.
      _got="$(ollama list 2>/dev/null | awk -v m="$CI_MODEL" '$1 == m { print $2; exit }')"
      if [ "$_got" = "$CI_ID" ]; then echo "  ollama model: pulled ($CI_MODEL, $CI_ID)"; else
        echo "  WARN: $CI_MODEL now has id ${_got:-none}, not the pinned $CI_ID (the tag moved?); update context/code-index.lock in a reviewed change"; FAILED=1
      fi
    fi
  fi
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
