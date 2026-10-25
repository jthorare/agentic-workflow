---
description: Test infrastructure for the bash suites; C# tests have their own rule
globs:
  - "**/tests/*.test.sh"
paths:
  - "**/tests/*.test.sh"
alwaysApply: false
---

# Testing Rules

C# tests follow `.agents/rules/csharp-testing.md`. There is no TypeScript test infrastructure in the toolkit: the Vitest packages are gone. What is left is the bash suites.

## Shell Tests

Bash suites live in `config/**/tests/`, `scripts/tests/`, and `providers/tests/` (`*.test.sh`, run with `bash <file>`). They must never touch the real `~/.claude`, `~/.codex`, `~/.cursor`, or `~/.agentic-workflow` — point `HOME` (or the relevant `AW_*`/`CLAUDE_*` path override) at a `mktemp -d` directory.
