#!/usr/bin/env bash
# config/lib/tests/sindri-cs-hook-surface.test.sh — the frozen hook surface of the C# sindri (spec §4c).
# Runs the real SessionStart hook config/hooks/sindri-nudge.sh with the C# sindri (src/Sindri/Aw.Sindri.Host)
# first on PATH, in a scratch AW_SINDRI_HOME and AW_STATE_DIR. The contract is only what the hook reads:
# `sindri repo status --nudge` prints one line when the session's repo is not in the profile and
# nothing otherwise, exits 0, finishes inside the hook's budget, and is read-only (it writes nothing under the
# home and never starts the control plane).
#
# AW_SINDRI_CS_BIN  a built C# sindri (the apphost of a framework-dependent build). Unset: the test builds
#                   src/Sindri/Aw.Sindri.Host in Release into a temp dir first (needs dotnet 10).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HOOK="$ROOT/config/hooks/sindri-nudge.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

if [ -z "${AW_SINDRI_CS_BIN:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj" -c Release -o "$WORK/cs-build" -warnaserror -nologo -v quiet > "$WORK/build.log" 2>&1 \
    || { cat "$WORK/build.log"; echo "FAIL: could not build the C# sindri"; exit 1; }
  AW_SINDRI_CS_BIN="$WORK/cs-build/sindri"
fi
[ -x "$AW_SINDRI_CS_BIN" ] || { echo "FAIL: no C# sindri at $AW_SINDRI_CS_BIN"; exit 1; }
AW_SINDRI_CS_BIN="$(cd "$(dirname "$AW_SINDRI_CS_BIN")" && pwd)/$(basename "$AW_SINDRI_CS_BIN")"

# `sindri` on PATH exactly as the installed hook finds it.
mkdir -p "$WORK/shim"
printf '#!/usr/bin/env bash\nexec "%s" "$@"\n' "$AW_SINDRI_CS_BIN" > "$WORK/shim/sindri"
chmod +x "$WORK/shim/sindri"

export AW_STATE_DIR="$WORK/state"; mkdir -p "$AW_STATE_DIR"
export AW_LOG_LEVEL=none
unset AW_JUDGE_CHILD AW_SINDRI_CHILD AW_SINDRI_PROFILE AW_SINDRI_PORT 2> /dev/null || true

# A scratch home holding a live profile that names $2 as repo "$3" (edits apply on save, #2258: the CLI reads
# <home>/profile/profile.yaml, there is no approved snapshot).
approved_home() {
  local home="$1" path="$2" name="$3"
  mkdir -p "$home/profile"
  printf 'schemaVersion: 1\nuser: thor\ntenant: local\nrepos:\n  - name: %s\n    path: %s\nservices: {}\napi: { port: 7410 }\n' "$name" "$path" > "$home/profile/profile.yaml"
}

# hook <dir> <home> [env...]: stdout and the exit code of the real hook run in <dir>
hook() {
  local dir="$1" home="$2"; shift 2
  (cd "$dir" && env AW_SINDRI_HOME="$home" PATH="$WORK/shim:$PATH" "$@" bash "$HOOK")
}

mkdir -p "$WORK/repo-a" "$WORK/repo-b" "$WORK/outside"
git -C "$WORK/repo-a" init -q
git -C "$WORK/repo-b" init -q
git -C "$WORK/repo-a" -c user.email=t@example.invalid -c user.name=t commit -q --allow-empty -m init
git -C "$WORK/repo-a" worktree add -q "$WORK/wt-a"
A="$(cd "$WORK/repo-a" && pwd -P)"

HOME_APPROVED="$WORK/home-approved"; approved_home "$HOME_APPROVED" "$A" repo-a
HOME_NONE="$WORK/home-none"; mkdir -p "$HOME_NONE"
BEFORE="$(cd "$HOME_APPROVED" && find . | sort)"

RC=0; OUT="$(hook "$WORK/repo-b" "$HOME_APPROVED")" || RC=$?
check "repo not in the profile: the one line" "$OUT" "sindri: repo-b is not onboarded (no secret scan or index); run: sindri repo onboard"
check "repo not in the profile: exit 0" "$RC" "0"

RC=0; OUT="$(hook "$WORK/repo-a" "$HOME_APPROVED")" || RC=$?
check "onboarded repo: silent" "$OUT" ""
check "onboarded repo: exit 0" "$RC" "0"

OUT="$(hook "$WORK/wt-a" "$HOME_APPROVED")"
check "worktree of an onboarded repo: silent" "$OUT" ""

RC=0; OUT="$(hook "$WORK/repo-b" "$HOME_NONE")" || RC=$?
check "no profile: silent" "$OUT" ""
check "no profile: exit 0" "$RC" "0"

RC=0; OUT="$(hook "$WORK/outside" "$HOME_APPROVED")" || RC=$?
check "outside git: silent" "$OUT" ""
check "outside git: exit 0" "$RC" "0"

OUT="$(hook "$WORK/repo-b" "$HOME_APPROVED" AW_SINDRI_CHILD=1)"
check "sindri child session: silent" "$OUT" ""
OUT="$(hook "$WORK/repo-b" "$HOME_APPROVED" AW_JUDGE_CHILD=1)"
check "judge child session: silent" "$OUT" ""

# The plain verb, which the hook contract does not read, must agree on the snapshot the nudge used: exit 0 onboarded, 1 not.
RC=0; (cd "$WORK/repo-a" && AW_SINDRI_HOME="$HOME_APPROVED" "$AW_SINDRI_CS_BIN" repo status > /dev/null 2>&1) || RC=$?
check "plain status: onboarded exits 0" "$RC" "0"
RC=0; (cd "$WORK/repo-b" && AW_SINDRI_HOME="$HOME_APPROVED" "$AW_SINDRI_CS_BIN" repo status > /dev/null 2>&1) || RC=$?
check "plain status: not onboarded exits 1" "$RC" "1"

# A corrupt profile never blocks a session: silence, exit 0.
HOME_BAD="$WORK/home-bad"; mkdir -p "$HOME_BAD/profile"; echo "{ not: a: profile" > "$HOME_BAD/profile/profile.yaml"
RC=0; OUT="$(hook "$WORK/repo-b" "$HOME_BAD")" || RC=$?
check "unreadable profile: silent" "$OUT" ""
check "unreadable profile: exit 0" "$RC" "0"

# Read-only: nothing was created or changed under the homes, and no control plane was started.
check "read-only: the home is unchanged" "$(cd "$HOME_APPROVED" && find . | sort)" "$BEFORE"
check "read-only: nothing in the empty home" "$(find "$HOME_NONE" -mindepth 1 | wc -l | tr -d ' ')" "0"

# Bounded: well inside the hook's 1500 ms (4 s here, so a loaded machine is not a false failure).
START=$(date +%s)
hook "$WORK/repo-b" "$HOME_APPROVED" > /dev/null
check "bounded: a nudge returns within 4 s" "$(( $(date +%s) - START < 4 ))" "1"

exit "$fail"
