#!/usr/bin/env bash
# scripts/ci/tmp-leak-check.sh — run a command and fail when it leaves new temp entries behind (#3262).
#
# Lists the entries of ${TMPDIR:-/tmp} named `aw-*` or `tmp.*` (mktemp's default name) before the command,
# runs it, lists them again, and names each entry that is new and still there. It never deletes anything.
#
# Usage: scripts/ci/tmp-leak-check.sh -- <command...>
# Exit:  the command's own code when it failed (leaks are still printed);
#        1 when the command passed and left a new entry;
#        0 when the command passed and left nothing;
#        2 on a usage error.
# Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

if [ "${1:-}" != "--" ] || [ "$#" -lt 2 ]; then
  echo "usage: $0 -- <command...>" >&2
  exit 2
fi
shift

dir="${TMPDIR:-/tmp}"
dir="${dir%/}"
[ -n "$dir" ] || dir=/

list() {
  find "$dir" -mindepth 1 -maxdepth 1 \( -name 'aw-*' -o -name 'tmp.*' \) 2>/dev/null | LC_ALL=C sort
}

# The two bookkeeping files live outside the watched names' reach: they are removed on exit and filtered from the result.
before="$(mktemp "$dir/aw-leak-before.XXXXXX")" || exit 2
after="$(mktemp "$dir/aw-leak-after.XXXXXX")" || exit 2
trap 'rm -f "$before" "$after"' EXIT

list >"$before"
"$@"
rc=$?
list >"$after"

# comm needs sorted input; both lists are LC_ALL=C sorted.
leaks="$(LC_ALL=C comm -13 "$before" "$after" | grep -vxF -e "$before" -e "$after" || true)"

if [ -n "$leaks" ]; then
  count="$(printf '%s\n' "$leaks" | wc -l | tr -d ' ')"
  echo "tmp-leak-check: $count new entries left in $dir:" >&2
  printf '%s\n' "$leaks" | while IFS= read -r l; do printf '  leaked: %s\n' "$l" >&2; done
fi

[ "$rc" -eq 0 ] || exit "$rc"
[ -z "$leaks" ] || exit 1
exit 0
