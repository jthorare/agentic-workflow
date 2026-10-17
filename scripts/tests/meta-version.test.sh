#!/usr/bin/env bash
# Tests for the staleness check of an installed aw-meta (scripts/meta-version-lib.sh, used by scripts/meta-launch.sh): the binary's
# commit (`aw-meta version --json`) against the last commit that touched the sources it is built from. A stub aw-meta stands in for the
# binary and a temp git repo for the checkout. Runs on Linux and macOS.
# Run: bash scripts/tests/meta-version.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null
unset AW_META_NO_STALE_CHECK TMUX AW_LAUNCHER_CONFIG
mkdir -p "$HOME"
# shellcheck source=../meta-version-lib.sh
. "$DIR/meta-version-lib.sh"

R="$WORK/repo"
mkdir -p "$R/src/Meta" "$R/docs" "$R/scripts" "$R/config"
git -C "$R" init -q
commit() { git -C "$R" add -A && git -C "$R" -c user.email=t@t -c user.name=t commit -qm "$1" && git -C "$R" rev-parse HEAD; }
echo one > "$R/src/Meta/a.cs"; C1="$(commit meta-one)"
echo doc > "$R/docs/a.md"; C2="$(commit docs-only)"
echo two > "$R/src/Meta/a.cs"; C3="$(commit meta-two)"
echo notes > "$R/docs/b.md"; C4="$(commit docs-again)"

stub() { # stub <path> <commit|none|unknown>: an aw-meta whose `version --json` reports that commit, or has no version command
  case "$2" in
    none) printf '#!/usr/bin/env bash\necho "unknown command" >&2\nexit 2\n' > "$1" ;;
    *) printf '#!/usr/bin/env bash\nif [ "${1:-}" = version ]; then echo '"'"'{"commit":"%s","builtAt":"2026-10-17T00:00:00Z","os":"macOS","rid":"osx-arm64"}'"'"'; exit 0; fi\nexit 0\n' "$2" > "$1" ;;
  esac
  chmod +x "$1"
}
B="$WORK/aw-meta"

# the version line
stub "$B" "$C3"
check "version line: commit, build time, OS and runtime id" "$(meta_version_line "$B")" "aw-meta $C3 (built 2026-10-17T00:00:00Z, macOS osx-arm64)"
stub "$B" none
check "version line: a binary with no version command says so" "$(meta_version_line "$B" | grep -c 'predates .aw-meta version')" "1"

# the staleness line
stub "$B" "$C3"
check "binary built at the last Meta change: no warning, even with later docs commits" "$(meta_stale_line "$B" "$R")" ""
stub "$B" "$C4"
check "binary built after the last Meta change: no warning" "$(meta_stale_line "$B" "$R")" ""
stub "$B" "$C1"
OUT="$(meta_stale_line "$B" "$R")"
check "binary older than the last Meta change: one warning naming both commits and the fix" \
  "$(printf '%s\n' "$OUT" | wc -l | tr -d ' '):$(printf '%s' "$OUT" | grep -c "${C1:0:12}.*older than the Meta sources.*${C3:0:12}.*scripts/install-meta.sh")" "1:1"
stub "$B" "$C2"
check "a binary from a docs-only commit between two Meta changes is older too" "$(meta_stale_line "$B" "$R" | grep -c 'older than the Meta sources')" "1"
stub "$B" unknown
check "an unstamped binary: warns it cannot be compared" "$(meta_stale_line "$B" "$R" | grep -c 'no commit stamp')" "1"
stub "$B" none
check "a binary with no version command: warns it predates the command" "$(meta_stale_line "$B" "$R" | grep -c "predates 'aw-meta version'")" "1"
stub "$B" "0123456789abcdef0123456789abcdef01234567"
check "a commit this checkout does not have: warns" "$(meta_stale_line "$B" "$R" | grep -c 'does not have')" "1"
check "no binary: no warning" "$(meta_stale_line "$WORK/missing" "$R")" ""
mkdir -p "$WORK/plain"
check "not a git checkout: no warning" "$(meta_stale_line "$B" "$WORK/plain")" ""

# the launcher: one stderr line, not fatal, switched off by AW_META_NO_STALE_CHECK=1
if command -v tmux > /dev/null 2>&1 && command -v jq > /dev/null 2>&1; then
  cp "$DIR/meta-launch.sh" "$DIR/launcher-lib.sh" "$DIR/meta-team-lib.sh" "$DIR/meta-version-lib.sh" "$R/scripts/"
  cp -r "$DIR/../config/launcher" "$R/config/"
  mkdir -p "$R/schemas"; cp "$DIR/../schemas/launcher.schema.json" "$R/schemas/"
  commit launcher > /dev/null
  mkdir -p "$AW_STATE_DIR/meta" "$WORK/repo2"
  jq -n --arg r "$WORK/repo2" '{team:"t1",repo:$r,metaLane:"meta",lanes:["api"]}' > "$AW_STATE_DIR/meta/team.json"
  launch() { env AW_META_BIN="$B" AW_META_LEGACY_TMUX_SOCKET=version-test-nothing "$@" bash "$R/scripts/meta-launch.sh" --dry-run 2>&1 >/dev/null; }
  stub "$B" "$C1"
  OUT="$(launch)"; RC=$?
  check "meta-launch.sh: a stale binary is one warning line on stderr, and the launch goes on" \
    "$RC:$(printf '%s\n' "$OUT" | grep -c 'warning: the installed aw-meta .* is older than the Meta sources')" "0:1"
  OUT="$(launch AW_META_NO_STALE_CHECK=1)"; RC=$?
  check "meta-launch.sh: AW_META_NO_STALE_CHECK=1 switches the check off" "$RC:$(printf '%s\n' "$OUT" | grep -c 'warning')" "0:0"
  stub "$B" "$(git -C "$R" rev-parse HEAD)"
  OUT="$(launch)"; RC=$?
  check "meta-launch.sh: a current binary: no warning" "$RC:$(printf '%s\n' "$OUT" | grep -c 'warning')" "0:0"
else
  echo "ok - skipped: the launcher cases need tmux and jq"
fi
exit $fail
