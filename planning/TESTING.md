# Testing Strategy

## Overview

Each test sets up its own state: SQLite-backed suites open a temp file, and suites that touch files use a temp directory (temp git repos, transcript fixtures, a scratch `HOME`). No test shares state with another or touches the real `~/.agentic-workflow`.

The C# tests (xUnit v3 on Microsoft.Testing.Platform) follow `.agents/rules/csharp-testing.md`: TDD order, the `unit`, `integration` and `heavy` lanes with their time budgets, fakes with contract tests, builders, golden files and the architecture tests. The bash suites follow `.agents/rules/testing.md`. There is no TypeScript test infrastructure.

## Running Tests

```bash
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8          # unit lane
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=integration" --ignore-exit-code 8   # integration lane
dotnet test --solution AgenticWorkflow.slnx --filter-not-trait "Lane=heavy" --ignore-exit-code 8     # everything but containers, browsers and index builds
node scripts/ci/lane-budgets.mjs                                                                     # after a Release build: each package's lane wall clock vs its budget
bash scripts/ci/bash-tests.sh                                                                        # every tracked bash suite
```

The full list is under Commands in `AGENTS.md`.

## Skill evals

Skills have their own tests, apart from the C# and bash suites. `scripts/skill-evals.sh` runs `claude plugin eval` on each skill's cases in `evals/` (spec `docs/superpowers/specs/2026-10-25-skill-revamp-evals-design.md`).

- **What they measure.** Routing: how often the skill fires on a natural prompt (fire rate), and that it stays quiet on a near miss. Behaviour: the score of a forced run with the skill minus the same run without it (the delta).
- **Where they run.** By hand, by the lane lead or the owner. They spend shared model usage, so no CI job runs them and builders never do. A run is capped at $60 a day.
- **What CI covers.** `bash scripts/tests/skill-evals.test.sh` tests the runner against a stub `claude`: flags, exit codes, the staged rewrite, the spend tally and the token scrub. `scripts/skill-evals.sh --selftest --all` checks every case file and grader without a model.

## Coverage Policy

Coverage is earned through real tests; nothing is hidden with an ignore annotation. The C# build's coverage and mutation targets are the ones in the `csharp-testing` rule and the spec it cites.

## Writing New Tests

1. C#: write the failing test first, in `tests/Aw.<Pkg>.Tests/<Slice>/` (`/new-slice` generates it red).
2. Bash: add `scripts/tests/<name>.test.sh` (or under `config/**/tests/`), point `HOME` and `AW_STATE_DIR` at a `mktemp -d` directory, and add the suite to `scripts/ci/bash-tests.sh` and the `AGENTS.md` list.
