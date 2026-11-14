#!/usr/bin/env bash
set -euo pipefail

# PreToolUse hook: Block destructive commands
# Reads tool input JSON from stdin. Exit 2 = deny, Exit 0 = allow.

INPUT=$(cat)
COMMAND=$(printf '%s\n' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null) || true
[ -z "$COMMAND" ] && exit 0

# Judge only the commands themselves (#3621). Text that merely mentions one (a quoted grep pattern, a commit message, a
# heredoc body) must not match. Steps: blank out heredoc bodies and quoted strings, split the rest into simple commands at
# `;`, `&&`, `||` and `|`, and run every rule against one command at a time. A shell `-c` string or `eval` runs its quoted
# text as commands, so a command line holding one is judged unblanked. (The same sanitizer is in block-push-main.sh: the
# hooks ship as single files, with no shared sourced file.)
# A deny prints its reason on stderr: Claude Code shows only stderr on exit 2 (the adapters join stdout and stderr).
if printf '%s\n' "$COMMAND" | grep -qE '(^|[^[:alnum:]_])(ba|z|da|k)?sh[[:space:]]+(-[a-zA-Z]+[[:space:]]+)*-[a-zA-Z]*c\b|(^|[^[:alnum:]_])eval\b'; then
  SANITIZED="$COMMAND"
else
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
fi

while IFS= read -r SEG; do
  [ -n "$SEG" ] || continue

  # rm with recursive and force flags in any combination
  # Catches: rm -rf, rm -r -f, rm -r --force, rm --recursive -f, rm -rfd, etc.
  if printf '%s\n' "$SEG" | grep -qE '\brm\b' && \
     printf '%s\n' "$SEG" | grep -qE '(\s-[a-zA-Z]*r|\s--recursive)' && \
     printf '%s\n' "$SEG" | grep -qE '(\s-[a-zA-Z]*f|\s--force)'; then
    echo "BLOCKED: rm -rf is destructive and irreversible." >&2
    echo "Suggestion: Use 'trash' or 'mv' to a backup location instead." >&2
    exit 2
  fi

  # git reset --hard
  if printf '%s\n' "$SEG" | grep -qE '\bgit\s+reset\s+--hard\b'; then
    echo "BLOCKED: git reset --hard discards all uncommitted changes." >&2
    echo "Suggestion: Use 'git stash' to save changes, or 'git reset --soft' to unstage." >&2
    exit 2
  fi

  # git push --force (but NOT --force-with-lease)
  # Match --force followed by whitespace or end-of-string to avoid matching --force-with-lease
  if printf '%s\n' "$SEG" | grep -qE '\bgit\s+push\s+.*--force(\s|$)' && \
     ! printf '%s\n' "$SEG" | grep -qE '\bgit\s+push\s+.*--force-with-lease\b'; then
    echo "BLOCKED: git push --force can overwrite remote history." >&2
    echo "Suggestion: Use 'git push --force-with-lease' for safer force pushes, or create a PR." >&2
    exit 2
  fi

  # git checkout . (anchored to end of command to avoid false positive on .gitignore etc.)
  if printf '%s\n' "$SEG" | grep -qE '\bgit\s+checkout\s+(--\s+)?\.\s*$'; then
    echo "BLOCKED: git checkout . discards all unstaged changes." >&2
    echo "Suggestion: Use 'git stash' to save changes, or checkout specific files." >&2
    exit 2
  fi

  # git restore . (modern equivalent of git checkout -- .)
  if printf '%s\n' "$SEG" | grep -qE '\bgit\s+restore\s+(--\s+)?\.\s*$'; then
    echo "BLOCKED: git restore . discards all unstaged changes." >&2
    echo "Suggestion: Use 'git stash' to save changes, or restore specific files." >&2
    exit 2
  fi

  # git clean -f (any flag combo containing f, or --force long form)
  if printf '%s\n' "$SEG" | grep -qE '\bgit\s+clean\b' && \
     printf '%s\n' "$SEG" | grep -qE '(\s-[a-zA-Z]*f|\s--force)'; then
    echo "BLOCKED: git clean -f permanently deletes untracked files." >&2
    echo "Suggestion: Use 'git clean -n' (dry run) first to preview what would be deleted." >&2
    exit 2
  fi
done <<EOF
$(printf '%s\n' "$SANITIZED" | awk '{ gsub(/&&|\|\||;|\|/, "\n"); print }')
EOF

exit 0
