#!/usr/bin/env bash
# Tests for config/hooks/block-destructive.sh (#3621). Run: bash config/hooks/tests/block-destructive.test.sh
# 1. Only a real destructive command is judged: quoted text or a heredoc body that mentions one is allowed.
# 2. A block exits 2 with the reason on stderr (Claude Code shows only stderr on a deny) and nothing on stdout.
# Never touches $HOME.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$(cd "$DIR/.." && pwd)/block-destructive.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
export AW_STATE_DIR="$WORK/state"

# run_hook <command>: sets rc, out (stdout), err (stderr).
run_hook() {
  local payload; payload="$(jq -nc --arg c "$1" '{tool_name:"Bash",tool_input:{command:$c}}')"
  printf '%s' "$payload" | bash "$HOOK" > "$WORK/out" 2> "$WORK/err"
  rc=$?; out="$(cat "$WORK/out")"; err="$(cat "$WORK/err")"
}
allowed() { run_hook "$2"; check "$1: allowed (exit 0)" "$rc" "0"; }
blocked() {
  run_hook "$2"
  check "$1: blocked (exit 2)" "$rc" "2"
  check "$1: reason on stderr" "$(printf '%s' "$err" | grep -c '^BLOCKED:')" "1"
  check "$1: nothing on stdout" "$out" ""
}

# --- real destructive commands stay blocked, with the reason on stderr ---
blocked "rm -rf" "rm -rf /tmp/some-dir"
blocked "rm -r -f" "rm -r -f build"
blocked "rm --recursive --force" "rm --recursive --force build"
blocked "rm after cd and &&" "cd /tmp && rm -rf out"
blocked "reset --hard" "git reset --hard HEAD~1"
blocked "push --force" "git push --force origin feature"
blocked "checkout ." "git checkout ."
blocked "restore ." "git restore ."
blocked "clean -fd" "git clean -fd"
blocked "bash -c with rm -rf" 'bash -c "rm -rf /tmp/x"'

# --- text that only mentions one is not one ---
allowed "grep with a quoted rm -rf pattern" 'grep -nE "TMPDIR|sandbox|rm -rf" notes.md'
allowed "grep with a single-quoted pattern" "grep -n 'git reset --hard' notes.md"
allowed "commit message that names rm -rf" 'git commit -m "docs: never run rm -rf here"'
allowed "heredoc body with rm -rf" $'cat > notes.md <<\'EOF\'\nrm -rf /tmp/x\ngit reset --hard\nEOF'
allowed "multi-line quoted text" $'git commit -m "first\nrm -rf /\nlast"'

# --- commands that are fine ---
allowed "rm of one file" "rm notes.txt"
allowed "rm -r without force" "rm -r build"
allowed "rm and an unrelated -f in another command" "rm notes.txt && ls -f"
allowed "push --force-with-lease" "git push --force-with-lease origin feature"
allowed "checkout .gitignore" "git checkout .gitignore"
allowed "clean dry run" "git clean -n"
allowed "reset --soft" "git reset --soft HEAD~1"
allowed "ls" "ls -la"

[ "$fail" = 0 ] && echo "all passed" || exit 1
