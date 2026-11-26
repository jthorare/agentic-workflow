#!/usr/bin/env bash
set -euo pipefail

# PreToolUse hook: Aggressive secret detection in bash commands
# Exit 2 = deny, Exit 0 = allow.

INPUT=$(cat)
COMMAND=$(printf '%s\n' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null) || true
[ -z "$COMMAND" ] && exit 0

# AWS access keys (AKIA for permanent, ASIA for STS temporary credentials)
if grep -qE 'A(KIA|SIA)[0-9A-Z]{16}' <<<"$COMMAND"; then
  echo "BLOCKED: Detected what appears to be an AWS access key."
  echo "Suggestion: Store AWS credentials in ~/.aws/credentials or use environment variables from .env files."
  exit 2
fi

# GitHub tokens (ghp_, gho_, ghs_, ghu_, ghr_ prefixes and fine-grained PATs)
if grep -qE '(gh[posur]_[A-Za-z0-9_]{36,}|github_pat_[A-Za-z0-9_]{22,})' <<<"$COMMAND"; then
  echo "BLOCKED: Detected what appears to be a GitHub token."
  echo "Suggestion: Use 'gh auth login' or store tokens in .env files, not in commands."
  exit 2
fi

# Bearer tokens in curl headers
if grep -qE 'curl\s+.*(-H|--header)\s+.*[Bb]earer\s+[A-Za-z0-9._/+=-]{20,}' <<<"$COMMAND"; then
  echo "BLOCKED: Detected a Bearer token in a curl command."
  echo "Suggestion: Store tokens in environment variables and reference them as \$TOKEN."
  exit 2
fi

# Secret assignments with high-entropy values (>20 chars) — case-insensitive
if grep -iqE "(export\s+)?(API_KEY|SECRET|TOKEN|PASSWORD|PRIVATE_KEY|ACCESS_KEY|SECRET_KEY)\s*=\s*['\"]?[A-Za-z0-9+/=_.,-]{20,}" <<<"$COMMAND"; then
  echo "BLOCKED: Detected a secret being assigned in a command."
  echo "Suggestion: Store secrets in .env files or a secrets manager, not inline in commands."
  exit 2
fi

exit 0
