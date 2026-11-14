#!/usr/bin/env bash
set -euo pipefail
# HOOK-FAILOPEN: record why this hook is failing open (config/lib/failopen.sh,
# installed at <hooks>/lib/failopen.sh). Logging only: it runs in a subshell
# with its output discarded, so it can never change this hook's exit code,
# stdout or stderr, whatever set -e/-u or the environment do to it.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen block-push-main "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# No EXIT trap: on bash 3.2 a trap turns a set -u abort under set -e into exit 0
# and defers SIGTERM. A crash keeps its exit code and is not counted under
# Claude Code (see "Fail-open logging" in .agents/rules/hooks.md); under
# Codex/Cursor the adapter counts it.

# PreToolUse hook: Block git push to main/master
# Exit 2 = deny, Exit 0 = allow.

INPUT=$(cat)
COMMAND=$(printf '%s\n' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null) || true
# No command to check: log why when the payload itself was unreadable.
if [ -z "$COMMAND" ]; then
  failopen input "$INPUT" '.tool_name == "Bash"'
  exit 0
fi

# Judge only the push command itself (#3621). Text that merely mentions a push (a heredoc body, a quoted commit message,
# an echo) must not match. Steps: blank out heredoc bodies and quoted strings, split the rest into simple commands at
# `;`, `&&`, `||` and `|`, and keep the commands that start with `git push` (after optional VAR=value words or `env`).
# A deny prints its reason on stderr: Claude Code shows only stderr on exit 2 (the adapters join stdout and stderr).
SANITIZED=$(printf '%s\n' "$COMMAND" | awk '
BEGIN { sq = sprintf("%c", 39); dquote = sprintf("%c", 34); st = "n"; hd = ""; hdtab = 0 }
{
  line = $0
  if (st == "h") {
    t = line
    if (hdtab) sub(/^\t+/, "", t)
    if (t == hd) st = "n"
    next
  }
  out = ""; n = length(line); i = 1; pend = ""
  while (i <= n) {
    c = substr(line, i, 1)
    if (st == "s") { if (c == sq) { st = "n"; out = out c } i++; continue }
    if (st == "d") {
      if (c == "\\") { i += 2; continue }
      if (c == dquote) { st = "n"; out = out c }
      i++; continue
    }
    if (c == "\\") { out = out c substr(line, i + 1, 1); i += 2; continue }
    if (c == sq) { st = "s"; out = out c; i++; continue }
    if (c == dquote) { st = "d"; out = out c; i++; continue }
    if (c == "<" && substr(line, i, 2) == "<<" && substr(line, i, 3) != "<<<") {
      rest = substr(line, i + 2)
      if (match(rest, /^-?[ \t]*[\047"\\]?[A-Za-z_][A-Za-z0-9_]*/)) {
        w = substr(rest, 1, RLENGTH)
        hdtab = (substr(w, 1, 1) == "-")
        sub(/^-?[ \t]*[\047"\\]?/, "", w)
        pend = w
      }
      out = out "<<"; i += 2; continue
    }
    out = out c; i++
  }
  print out
  if (pend != "") { hd = pend; st = "h" }
}')
PUSHES=$(printf '%s\n' "$SANITIZED" | sed -e 's/&&/\n/g' -e 's/||/\n/g' -e 's/;/\n/g' -e 's/|/\n/g' \
  | grep -E '^[[:space:]]*((env|command)[[:space:]]+)?([A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*[[:space:]]+)*git[[:space:]]+push\b' || true)
[ -n "$PUSHES" ] || exit 0

# Check if pushing to main or master explicitly (with optional flags before remote, and refspec support)
# Blocks: git push origin main, git push -u origin main, git push --set-upstream origin main, git push origin HEAD:main
# Allows: git push origin main:feature (pushing main TO a non-main refspec)
if printf '%s\n' "$PUSHES" | grep -E '\bgit\s+push\b.*\b(main|master)\b' | grep -qvE '\bgit\s+push\b.*\b(main|master)\s*:'; then
  echo "BLOCKED: Pushing directly to main/master is not allowed." >&2
  echo "Suggestion: Create a feature branch and open a pull request instead." >&2
  exit 2
fi

# Check if pushing with current branch being main/master (bare push without explicit non-main destination)
# Handles: git push, git push origin, git push --tags, git push -u origin (each as its own command, so
# `git push && echo done` counts)
if printf '%s\n' "$PUSHES" | grep -qE '\bgit\s+push(\s+(-[a-zA-Z]|--[a-zA-Z-]+=?\S*)\s*)*\s*($|2>)' || \
   printf '%s\n' "$PUSHES" | grep -qE '\bgit\s+push\s+(--[a-zA-Z-]+=?\S*\s+|-[a-zA-Z]\s+\S*\s+)*origin\s*($|2>)'; then
  # Note: returns "" in non-git directories — branch check silently passes (known limitation)
  CURRENT_BRANCH=$(git branch --show-current 2>/dev/null || echo "")
  if [ "$CURRENT_BRANCH" = "main" ] || [ "$CURRENT_BRANCH" = "master" ]; then
    echo "BLOCKED: You are on '$CURRENT_BRANCH'. Pushing directly to main/master is not allowed." >&2
    echo "Suggestion: Create a feature branch and open a pull request instead." >&2
    exit 2
  fi
fi

exit 0
