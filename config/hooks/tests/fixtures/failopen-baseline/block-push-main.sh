#!/usr/bin/env bash
set -euo pipefail

# PreToolUse hook: Block git push to main/master
# Exit 2 = deny, Exit 0 = allow.

INPUT=$(cat)
COMMAND=$(printf '%s\n' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null) || true
[ -z "$COMMAND" ] && exit 0

# Judge only the push command itself (#3621, #3679). Text that merely mentions a push (a heredoc body, a quoted commit
# message, an echo) must not match, and a real push must not slip through because of how it is written. Steps:
#   1. Drop heredoc bodies. Take the quote characters off quoted text but keep the text, and turn `;`, `&`, `|`, `(`, `)`
#      and newlines inside quotes into spaces, so quoted text can't split a command and a quoted target ("main") stays.
#   2. Split at `;`, `&&`, `||`, `|`, `&`, `(` and `)` into simple commands.
#   3. Per command, skip what can come before `git`: `then do else elif if while until ! { }`, VAR=x words, and the wrappers
#      env, nohup, time, command, sudo, exec, builtin (with their `-options`) and timeout (with its options and duration).
#   4. Keep the commands that now start with `git push`.
# A deny prints its reason on stderr: Claude Code shows only stderr on exit 2 (the adapters join stdout and stderr).
FLAT=$(printf '%s\n' "$COMMAND" | awk '
BEGIN { sq = sprintf("%c", 39); dquote = sprintf("%c", 34); st = "n"; hd = ""; hdtab = 0; acc = "" }
{
  line = $0
  if (st == "h") {
    t = line
    if (hdtab) sub(/^\t+/, "", t)
    if (t == hd) st = "n"
    next
  }
  out = acc; n = length(line); i = 1; pend = ""
  while (i <= n) {
    c = substr(line, i, 1)
    if (st == "s") {
      if (c == sq) st = "n"
      else if (c ~ /[;&|()]/) out = out " "
      else out = out c
      i++; continue
    }
    if (st == "d") {
      if (c == "\\") { out = out substr(line, i + 1, 1); i += 2; continue }
      if (c == dquote) st = "n"
      else if (c ~ /[;&|()]/) out = out " "
      else out = out c
      i++; continue
    }
    if (c == "\\") { out = out c substr(line, i + 1, 1); i += 2; continue }
    if (c == sq) { st = "s"; i++; continue }
    if (c == dquote) { st = "d"; i++; continue }
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
  if (st == "s" || st == "d") { acc = out " "; next }
  acc = ""
  print out
  if (pend != "") { hd = pend; st = "h" }
}
END { if (acc != "") print acc }')
PUSHES=$(printf '%s\n' "$FLAT" | awk '
{
  s = $0
  gsub(/[0-9]*>&[0-9-]*/, " ", s)
  gsub(/&>/, ">", s)
  gsub(/&&|\|\||;|\||&|\(|\)/, "\n", s)
  n = split(s, P, "\n")
  for (k = 1; k <= n; k++) {
    p = P[k]; wrap = 0
    for (;;) {
      sub(/^[ \t]+/, "", p)
      if (!match(p, /^[^ \t]+/)) break
      w = substr(p, 1, RLENGTH); r = substr(p, RLENGTH + 1)
      if (w ~ /^(then|do|else|elif|if|while|until|!|\{|\})$/) { p = r; wrap = 0; continue }
      if (w ~ /^(env|nohup|time|command|sudo|exec|builtin)$/) { p = r; wrap = 1; continue }
      if (w == "timeout") { p = r; wrap = 2; continue }
      if ((wrap == 1 && w ~ /^-[ugU]$/) || (wrap == 2 && w ~ /^-[sk]$/)) { p = r; sub(/^[ \t]+[^ \t]+/, "", p); continue }
      if (wrap > 0 && w ~ /^-/) { p = r; continue }
      if (wrap == 2 && w ~ /^[0-9.]+[smhd]?$/) { p = r; wrap = 0; continue }
      if (w ~ /^[A-Za-z_][A-Za-z0-9_]*=/) { p = r; continue }
      break
    }
    if (p ~ /^git[ \t]+push([ \t]|$)/) print p
  }
}')
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
