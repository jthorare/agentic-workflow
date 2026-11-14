#!/usr/bin/env bash
# Tests for config/hooks/block-push-main.sh (#3621). Run: bash config/hooks/tests/block-push-main.test.sh
# 1. Only a real `git push` command is judged: a heredoc body or quoted text that holds a push line is allowed.
# 2. A block exits 2 with the reason on stderr (Claude Code shows only stderr on a deny) and nothing on stdout.
# Runs in a temp git repo; never touches $HOME.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$(cd "$DIR/.." && pwd)/block-push-main.sh"
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
REPO="$WORK/repo"; mkdir -p "$REPO"
git -C "$REPO" init -q -b feature
git -C "$REPO" config user.email t@t; git -C "$REPO" config user.name t
git -C "$REPO" commit -q --allow-empty -m init
git -C "$REPO" branch main

# run_hook <command> [branch]: sets rc, out (stdout), err (stderr).
run_hook() {
  git -C "$REPO" checkout -q "${2:-feature}"
  local payload; payload="$(jq -nc --arg c "$1" '{tool_name:"Bash",tool_input:{command:$c}}')"
  (cd "$REPO" && printf '%s' "$payload" | bash "$HOOK" > "$WORK/out" 2> "$WORK/err")
  rc=$?; out="$(cat "$WORK/out")"; err="$(cat "$WORK/err")"
}
allowed() { run_hook "$2" "${3:-feature}"; check "$1: allowed (exit 0)" "$rc" "0"; }
blocked() {
  run_hook "$2" "${3:-feature}"
  check "$1: blocked (exit 2)" "$rc" "2"
  check "$1: reason on stderr" "$(printf '%s' "$err" | grep -c '^BLOCKED:')" "1"
  check "$1: nothing on stdout" "$out" ""
}

# --- real pushes stay blocked, with the reason on stderr ---
blocked "push origin main" "git push origin main"
blocked "push -u origin master" "git push -u origin master"
blocked "push HEAD:main" "git push origin HEAD:main"
blocked "after cd and &&" "cd /tmp && git push origin main"
blocked "after a semicolon" "echo hi; git push origin master"
blocked "env assignment first" "FOO=1 git push origin main"
blocked "bare push on main" "git push" main
blocked "push origin on main, then more" "git push origin && echo done" main

# --- text that only mentions a push is not a push ---
allowed "heredoc body with a push line" $'cat > notes.md <<\'EOF\'\ngit push origin main\nEOF'
allowed "heredoc with a dash and a quoted tag" $'cat <<-"END"\n\tgit push -u origin main\n\tEND\necho done'
allowed "commit message in double quotes" 'git commit -m "docs: never git push origin main"'
allowed "commit message in single quotes" "git commit -m 'git push origin master is blocked'"
allowed "multi-line quoted message" $'git commit -m "first line\ngit push origin main\nlast line"'
allowed "echo of a push" "echo git push origin main"
allowed "gh pr create with --base main" 'git push -u origin feature && gh pr create --base main'
allowed "push of a branch, then a main checkout" "git push origin feature && git checkout main"

# --- real pushes that are fine ---
allowed "push main to a non-main refspec" "git push origin main:feature"
allowed "push a feature branch" "git push -u origin feature"
allowed "bare push on a feature branch" "git push" feature
allowed "a command that is not a push" "ls -la"

# a real push after a heredoc is still judged
blocked "real push after a heredoc" $'cat <<EOF\nhello\nEOF\ngit push origin main'

[ "$fail" = 0 ] && echo "all passed" || exit 1
