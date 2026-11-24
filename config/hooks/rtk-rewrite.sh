#!/usr/bin/env bash
# PreToolUse hook: Rewrite eligible commands to use rtk for token compression
# Reads tool input JSON from stdin. Always exits 0 (never blocks).
# If the command STARTS with a rewrite pattern, outputs ONE JSON document with updatedInput (#3778).
# The match is on the start of the whole command, not on each line: a heredoc script followed by a `git diff` line is not a git
# command. The rest of a multi-line command stays as it was. Claude Code reads stdout as one JSON document, so the encoder
# (jq -n --rawfile) never emits one object per line.
# Safety hooks run first — this runs 4th in the Bash chain after all safety checks.

INPUT=$(cat)
COMMAND=$(printf '%s\n' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null) || true
[ -z "$COMMAND" ] && exit 0

# Only rewrite if rtk is available
command -v rtk &>/dev/null || exit 0

REWRITTEN=""
NL=$'\n'

case "$COMMAND" in
  # git status/log/diff/push/show → rtk git <sub> (full args preserved)
  "git status"* | "git log"* | "git diff"* | "git push"* | "git show"*) REWRITTEN="rtk $COMMAND" ;;
  # vitest run → rtk vitest run (full args preserved)
  "vitest run"*) REWRITTEN="rtk $COMMAND" ;;
  # npm test → rtk npm test; npm run test → rtk npm test (args preserved)
  "npm run test"*) REWRITTEN="rtk npm test${COMMAND#"npm run test"}" ;;
  "npm test"*) REWRITTEN="rtk npm test${COMMAND#"npm test"}" ;;
  # npx tsc / tsc → rtk tsc (args preserved, npx removed)
  "npx tsc" | "npx tsc "* | "npx tsc$NL"*) REWRITTEN="rtk tsc${COMMAND#"npx tsc"}" ;;
  "tsc" | "tsc "* | "tsc$NL"*) REWRITTEN="rtk tsc${COMMAND#tsc}" ;;
  # npx eslint / eslint → rtk lint (args preserved, npx removed)
  "npx eslint" | "npx eslint "* | "npx eslint$NL"*) REWRITTEN="rtk lint${COMMAND#"npx eslint"}" ;;
  "eslint" | "eslint "* | "eslint$NL"*) REWRITTEN="rtk lint${COMMAND#eslint}" ;;
  # cargo test / cargo build → rtk cargo <sub> (full args preserved)
  "cargo test"* | "cargo build"*) REWRITTEN="rtk $COMMAND" ;;
  # next build → rtk next build (full args preserved)
  "next build"*) REWRITTEN="rtk $COMMAND" ;;
esac

# If no rewrite matched, pass through unchanged
[ -z "$REWRITTEN" ] && exit 0

# Output updated input JSON for Claude Code to apply
jq -n --rawfile c <(printf '%s' "$REWRITTEN") '{hookSpecificOutput: {hookEventName: "PreToolUse", permissionDecision: "allow", updatedInput: {command: $c}}}'
exit 0
