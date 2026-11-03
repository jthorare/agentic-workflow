#!/usr/bin/env bash
# #1898: /weeklyRetro's `git shortlog` line must name a revision. Without one, git reads a log from stdin when stdin
# isn't a terminal (an agent's shell): it hangs on an open pipe and prints nothing on /dev/null.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SKILL="${WEEKLYRETRO_SKILL:-$ROOT/skills/weeklyRetro/SKILL.md}"
fail=0

tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
repo="$tmp/repo"
git init -q -b main "$repo"
echo one >"$repo/a.txt"
git -C "$repo" add a.txt
git -C "$repo" -c user.name="Ana Ruiz" -c user.email=ana@example.com commit -q -m "add a"

# The skill's shortlog line, run as an agent would: in the repo, stdin not a terminal, bounded.
run_line() { # run_line <skill file>
  local line
  line="$(grep -m1 '^git shortlog' "$1")" || { echo "no shortlog line in $1"; return 2; }
  (cd "$repo" && SINCE_DATE="$(date -d '-7 days' +%Y-%m-%d 2>/dev/null || date -v-7d +%Y-%m-%d)" timeout 10 bash -c "$line" </dev/null)
}

check() { # check <name> <skill file> <expect: pass|fail>
  local out got=fail
  out="$(run_line "$2" 2>&1)" || true
  case "$out" in *"Ana Ruiz"*) got=pass ;; esac
  if [ "$got" = "$3" ]; then echo "PASS: $1"; else echo "FAIL: $1: expected $3, got $got (output: ${out:-<empty>})"; fail=1; fi
}

printf '%s\n' 'git shortlog -sne HEAD --since="$SINCE_DATE" --no-merges' >"$tmp/ok.md"
printf '%s\n' 'git shortlog -sne --since="$SINCE_DATE" --no-merges' >"$tmp/bad.md"
check "a shortlog line naming HEAD lists the author" "$tmp/ok.md" pass
check "a shortlog line without a revision lists nobody" "$tmp/bad.md" fail
check "weeklyRetro's shortlog line lists the week's authors without a terminal" "$SKILL" pass

exit "$fail"
