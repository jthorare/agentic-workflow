#!/usr/bin/env bash
# Tests for scripts/install-tldraw.sh (tldraw offline: the app, and a check for the agent skills the app installs).
# Run: bash scripts/tests/install-tldraw.test.sh
# Everything runs under a temp HOME with a fake brew on PATH and a file:// "release" holding a stand-in AppImage;
# no real app, Homebrew or network is touched.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
INSTALL="$ROOT/scripts/install-tldraw.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac; }
lacks() { case "$2" in *"$3"*) echo "not ok - $1"; echo "  unexpected: $3"; fail=1 ;; *) echo "ok - $1" ;; esac; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME" "$WORK/bin" "$WORK/release"
export AW_TLDRAW_DIR="$WORK/share" AW_TLDRAW_BIN_DIR="$WORK/localbin" AW_TLDRAW_APP="$WORK/Applications/tldraw offline.app"
export AW_TLDRAW_URL_BASE="file://$WORK/release"
unset AW_DRY_RUN AW_TLDRAW_SHA256
CALLS="$WORK/calls.log"; : > "$CALLS"
export FAKE_CALLS="$CALLS"

# Fake brew: logs its arguments; `list --cask tldraw` fails until an install made the app; `install` fails when FAKE_BREW_FAIL=1.
cat > "$WORK/bin/brew" <<'SH'
#!/usr/bin/env bash
echo "brew $*" >> "$FAKE_CALLS"
case "$*" in
  "list --cask tldraw") [ -d "$AW_TLDRAW_APP" ] ;;
  "install --cask tldraw") [ "${FAKE_BREW_FAIL:-0}" = "1" ] && exit 1; mkdir -p "$AW_TLDRAW_APP" ;;
  *) exit 0 ;;
esac
SH
chmod +x "$WORK/bin/brew"
export PATH="$WORK/bin:/usr/bin:/bin"   # not the inherited PATH: a real tldraw-offline in ~/.local/bin makes the installer skip (#1214)

# A stand-in AppImage for each arch, and its sha256.
printf '#!/bin/sh\necho tldraw-offline-fake\n' > "$WORK/release/tldraw-offline-linux-x86_64.AppImage"
cp "$WORK/release/tldraw-offline-linux-x86_64.AppImage" "$WORK/release/tldraw-offline-linux-arm64.AppImage"
if command -v sha256sum >/dev/null 2>&1; then FAKE_SUM="$(sha256sum "$WORK/release/tldraw-offline-linux-x86_64.AppImage" | cut -d' ' -f1)"
else FAKE_SUM="$(shasum -a 256 "$WORK/release/tldraw-offline-linux-x86_64.AppImage" | cut -d' ' -f1)"; fi

run() { bash "$INSTALL" "$@" 2>&1; }

# --- usage ---
out="$(run --bogus)"; rc=$?
check "an unknown flag is a usage error" "$rc" "1"
contains "the usage line names --dry-run" "$out" "usage: install-tldraw.sh [--dry-run]"

# --- macOS ---
out="$(AW_DEPS_OS=Darwin run --dry-run)"; rc=$?
check "macOS dry run exits 0" "$rc" "0"
contains "macOS dry run prints the brew command" "$out" "[dry-run] would run: brew install --cask tldraw"
lacks "macOS dry run never installs" "$(cat "$CALLS")" "install --cask"
check "macOS dry run makes no app" "$([ -d "$AW_TLDRAW_APP" ] && echo yes || echo no)" "no"

out="$(AW_DRY_RUN=1 AW_DEPS_OS=Darwin run)"
contains "AW_DRY_RUN=1 is a dry run" "$out" "[dry-run] would run: brew install --cask tldraw"

: > "$CALLS"
out="$(FAKE_BREW_FAIL=1 AW_DEPS_OS=Darwin run)"; rc=$?
check "a failed brew install is non-fatal" "$rc" "0"
contains "a failed brew install warns" "$out" "WARN: brew install --cask tldraw failed (non-fatal)"
lacks "a failed install skips the skills line" "$out" "next step"

: > "$CALLS"
out="$(AW_DEPS_OS=Darwin run)"; rc=$?
check "macOS install exits 0" "$rc" "0"
contains "macOS installs through the cask" "$(cat "$CALLS")" "brew install --cask tldraw"
contains "macOS reports the install" "$out" "installed with brew install --cask tldraw"
contains "no skills yet: the next step is printed" "$out" "Develop → Install Agent Skills"

: > "$CALLS"
out="$(AW_DEPS_OS=Darwin run)"
contains "a present app is left alone" "$out" "tldraw: app already installed"
lacks "a present app is not reinstalled" "$(cat "$CALLS")" "install --cask"

out="$(PATH="/usr/bin:/bin" AW_TLDRAW_APP="$WORK/none.app" AW_DEPS_OS=Darwin run)"; rc=$?
check "no Homebrew is non-fatal" "$rc" "0"
contains "no Homebrew points at the download page" "$out" "Homebrew not found"

# --- the agent-skills check ---
mkdir -p "$HOME/.claude/skills/tldraw-offline" "$HOME/.codex/skills/tldraw-offline" "$HOME/.cursor/skills/tldraw-offline"
echo "# a hand-written skill, no marker" > "$HOME/.cursor/skills/tldraw-offline/SKILL.md"
out="$(AW_DEPS_OS=Darwin run)"
contains "an unmarked skill does not count" "$out" "next step"
printf '%s\n' "<!-- installed-by:tldraw-desktop-agent-skills -->" "# tldraw" > "$HOME/.claude/skills/tldraw-offline/SKILL.md"
cp "$HOME/.claude/skills/tldraw-offline/SKILL.md" "$HOME/.codex/skills/tldraw-offline/SKILL.md"
out="$(AW_DEPS_OS=Darwin run)"
contains "marked skills are reported per provider" "$out" "agent skills installed for: claude codex"
lacks "installed skills drop the next step" "$out" "next step"
check "the skill files are never written" "$(cat "$HOME/.cursor/skills/tldraw-offline/SKILL.md")" "# a hand-written skill, no marker"
rm -rf "$HOME/.claude" "$HOME/.codex" "$HOME/.cursor"

# --- Linux ---
out="$(AW_DEPS_OS=Linux AW_TLDRAW_ARCH=x86_64 run --dry-run)"; rc=$?
check "Linux dry run exits 0" "$rc" "0"
contains "Linux dry run names the asset" "$out" "would download tldraw-offline-linux-x86_64.AppImage (v"
check "Linux dry run writes nothing" "$([ -e "$AW_TLDRAW_DIR" ] && echo yes || echo no)" "no"

out="$(AW_DEPS_OS=Linux AW_TLDRAW_ARCH=x86_64 run)"; rc=$?
check "a checksum mismatch is non-fatal" "$rc" "0"
contains "the pinned sha256 refuses a different file" "$out" "does not match its pinned sha256; not installed"
check "a refused download leaves no AppImage" "$([ -e "$AW_TLDRAW_DIR/tldraw-offline.AppImage" ] && echo yes || echo no)" "no"
check "a refused download leaves no temp file" "$(ls -A "$AW_TLDRAW_DIR")" ""

out="$(AW_TLDRAW_URL_BASE="file://$WORK/missing" AW_TLDRAW_SHA256="$FAKE_SUM" AW_DEPS_OS=Linux AW_TLDRAW_ARCH=x86_64 run)"; rc=$?
check "a failed download is non-fatal" "$rc" "0"
contains "a failed download warns" "$out" "download failed"

out="$(AW_TLDRAW_SHA256="$FAKE_SUM" AW_DEPS_OS=Linux AW_TLDRAW_ARCH=aarch64 run)"; rc=$?
check "Linux install exits 0" "$rc" "0"
contains "Linux reports the install" "$out" "installed $AW_TLDRAW_DIR/tldraw-offline.AppImage"
check "the AppImage is executable" "$([ -x "$AW_TLDRAW_DIR/tldraw-offline.AppImage" ] && echo yes || echo no)" "yes"
check "the launcher link runs it" "$("$AW_TLDRAW_BIN_DIR/tldraw-offline")" "tldraw-offline-fake"
contains "Linux prints the skills step too" "$out" "Develop → Install Agent Skills"

out="$(AW_DEPS_OS=Linux AW_TLDRAW_ARCH=x86_64 run)"
contains "a present AppImage is left alone" "$out" "tldraw: app already installed"

out="$(AW_TLDRAW_DIR="$WORK/share2" AW_DEPS_OS=Linux AW_TLDRAW_ARCH=riscv64 PATH="$WORK/bin:/usr/bin:/bin" run)"; rc=$?
check "an unknown arch is non-fatal" "$rc" "0"
contains "an unknown arch warns" "$out" "no AppImage for riscv64"

# --- other OSes ---
out="$(AW_DEPS_OS=MINGW64_NT run)"; rc=$?
check "another OS is skipped with exit 0" "$rc" "0"
contains "another OS gets a skip line" "$out" "tldraw: skipped on MINGW64_NT"

# --- setup.sh wiring ---
setup="$(cat "$ROOT/setup.sh")"
contains "setup.sh runs the installer" "$setup" 'scripts/install-tldraw.sh'
contains "setup.sh has the --no-tldraw opt-out" "$setup" '--no-tldraw) WITH_TLDRAW=0'

exit "$fail"
