---
description: Test infrastructure, coverage policy, and test count baseline
globs:
  - "**/*.test.ts"
  - "**/*.spec.ts"
  - "**/vitest.config.ts"
paths:
  - "**/*.test.ts"
  - "**/*.spec.ts"
  - "**/vitest.config.ts"
alwaysApply: false
---

# Testing Rules

## Test Infrastructure

Five Vitest packages, each with v8 coverage, `testTimeout: 10_000`, and 100% line/function/branch/statement thresholds in `vitest.config.ts` (enforced by `npm run test:coverage`):

| Package | Tests | Coverage excludes |
|---------|-------|-------------------|
| `scorer` | transcript parsing, reports, probe summaries | `src/cli.ts` |
| `judge` | judge CLI logic | `src/cli.ts` |
| `sindri` | in-memory SQLite ledgers, temp git repos, fake SystemProbe/GitRunner/IndexIo, tracker contract tests, evolve fixtures (`tests/evolve-fixtures.ts`: a temp repo with an approved profile and an open ledger, fake process and model runners; synthetic hook-fire transcripts under `tests/fixtures`) | `src/system-real.ts` (OS-specific branches; smoke-tested in `tests/real.test.ts`, which also covers `src/git-real.ts` and `src/index/sandbox-real.ts` in full); `src/scope/model-real.ts`, `src/scope/io-real.ts`; `src/cli.ts`, `src/gen.ts` (thin wiring; generated outputs are checked by tests) |
| `skills/ui-evidence` | qa-runner scripts, lint, parity, publish; a real-browser test under `tests/browser` | `src/run-script.ts`, `src/parity-run.ts`, `src/step-exec.ts`, `src/bin.ts` (process wiring), `src/pixelmatch.d.ts` (types) |
| `skills/bugFixOrchestrator` | the bugfix-state CLI: gates, journey, refusals | `src/bin.ts` |

`/* v8 ignore */` annotations are prohibited — write the test instead.

## Shell Tests

Bash suites live in `config/**/tests/`, `scripts/tests/`, and `providers/tests/` (`*.test.sh`, run with `bash <file>`). They must never touch the real `~/.claude`, `~/.codex`, `~/.cursor`, or `~/.agentic-workflow` — point `HOME` (or the relevant `AW_*`/`CLAUDE_*` path override) at a `mktemp -d` directory.

## Test Count Baseline

- scorer: 471 tests across 38 test files
- judge: 625 tests across 49 test files
- sindri: 1365 tests across 92 test files
- skills/ui-evidence: 252 tests across 24 test files (including the real-browser tests under `tests/browser`)
- skills/bugFixOrchestrator: 70 tests across 4 test files

Any PR that reduces these counts needs explicit justification.
