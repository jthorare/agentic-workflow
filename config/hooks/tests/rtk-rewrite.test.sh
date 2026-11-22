#!/usr/bin/env bash
# Tests for config/hooks/rtk-rewrite.sh (#3778). Run: bash config/hooks/tests/rtk-rewrite.test.sh
# Claude Code reads stdout as ONE JSON document, and the rewrite applies to the command's start only:
# a heredoc script followed by a `git diff` line gets no rewrite; a rewritable first line with more lines after it gives one document.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$(cd "$DIR/.." && pwd)/rtk-rewrite.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'find "$WORK" -delete 2> /dev/null' EXIT
mkdir -p "$WORK/bin"
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/bin/rtk"; chmod +x "$WORK/bin/rtk"

# run <command>: sets out (stdout) and rc.
run() {
  local payload; payload="$(jq -nc --arg c "$1" '{tool_name:"Bash",tool_input:{command:$c}}')"
  out="$(printf '%s' "$payload" | PATH="$WORK/bin:$PATH" bash "$HOOK" 2> /dev/null)"; rc=$?
}
docs() { jq -s length <<< "$out" 2> /dev/null; }
rewritten() { jq -s -r '.[0].hookSpecificOutput.updatedInput.command' <<< "$out" 2> /dev/null; }

run 'git status'
check "git status: one document" "$(docs)" "1"
check "git status becomes rtk git status" "$(rewritten)" "rtk git status"
check "git status: allow" "$(jq -r '.hookSpecificOutput.permissionDecision' <<< "$out")" "allow"

run $'cd /tmp && python3 - <<\'EOF\'\nprint(1)\nEOF\ngit diff --stat'
check "heredoc script then a git diff line: no output" "$out" ""
check "heredoc script then a git diff line: exit 0" "$rc" "0"

run $'git log --oneline\necho second line'
check "git log then a second line: exactly one document" "$(docs)" "1"
check "git log then a second line: the whole command is rewritten" "$(rewritten)" $'rtk git log --oneline\necho second line'

run $'echo hi\ngit status'
check "a git line that is not first: no output" "$out" ""
run $'git status\ngit diff\ngit show HEAD'
check "three git lines: one document" "$(docs)" "1"
check "three git lines: rtk goes on the start only" "$(rewritten)" $'rtk git status\ngit diff\ngit show HEAD'

run 'npm test -- --watch=false'
check "npm test keeps its args" "$(rewritten)" "rtk npm test -- --watch=false"
run 'npm run test'
check "npm run test becomes rtk npm test" "$(rewritten)" "rtk npm test"
run 'npx tsc --noEmit'
check "npx tsc becomes rtk tsc" "$(rewritten)" "rtk tsc --noEmit"
run 'tsc'
check "bare tsc becomes rtk tsc" "$(rewritten)" "rtk tsc"
run 'npx eslint src'
check "npx eslint becomes rtk lint" "$(rewritten)" "rtk lint src"
run 'cargo build --release'
check "cargo build" "$(rewritten)" "rtk cargo build --release"
run 'vitest run a.test.ts'
check "vitest run" "$(rewritten)" "rtk vitest run a.test.ts"
run 'next build'
check "next build" "$(rewritten)" "rtk next build"
run 'ls -la'
check "unrelated command: no output" "$out" ""
run 'echo "git status"'
check "quoted mention: no output" "$out" ""

[ "$fail" = 0 ] && echo "all passed" || exit 1
