#!/usr/bin/env bash
# skills/_shared/skill-lock.sh must run on bash 3.2 (the macos-15 runner) and BSD tools (#2534): none of the bash 4 or
# GNU-only forms, and no bash >= 4 refusal. A static scan; SKILL_LOCK_FILE overrides the file (to prove a planted form fails).
# Run: bash scripts/tests/skill-lock-portable.test.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FILE="${SKILL_LOCK_FILE:-$ROOT/skills/_shared/skill-lock.sh}"
FAILS=0
bad() { FAILS=$((FAILS + 1)); echo "FAIL: $1"; }
ok()  { echo "PASS: $1"; }

lockhits="$(grep -nE '\b(mapfile|readarray|coproc)\b|\$\{?BASHPID|declare[[:space:]]+-A|\$\{[A-Za-z_]+(,,|\^\^)|wait[[:space:]]+-n|\{[a-z_]+\}[<>]|sed[[:space:]]+-i[[:space:]]|find[[:space:]].*-printf|stat[[:space:]]+-c|date[[:space:]]+-d|readlink[[:space:]]+-f|touch[[:space:]]+-d|BASH_VERSINFO' "$FILE" | grep -vE '^[0-9]+:[[:space:]]*#' || true)"
if [ -n "$lockhits" ]; then bad "skill-lock.sh uses a bash 4 or GNU-only form"; printf '%s\n' "$lockhits" | sed 's/^/      | /'; else ok "skill-lock.sh has no bash 4 or GNU-only forms"; fi

[ "$FAILS" -eq 0 ] || exit 1
