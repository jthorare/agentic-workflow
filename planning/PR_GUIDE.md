# Pull Request Review Guide

Use this checklist when opening or reviewing pull requests. Every item applies unless explicitly noted otherwise. The
rules themselves live in `.agents/rules/` (one file per domain); this guide is the reviewer's short form.

---

## Scope

- [ ] **One logical change per PR**, with atomic commits (`type: short description`, `planning/COMMIT_STRATEGY.md`).
- [ ] **The hook contract is unchanged** -- only what the hooks read stays frozen: the commands and
  arguments they call, the JSON fields they `jq`, the exit codes and stderr prefixes they branch on, and the files they
  write (`judge/sessions`, `judge/outbox`, `judge/teammate-names.jsonl`). Spec §4c.

---

## C# (`src/`, `tests/`)

- [ ] **Follows `.agents/rules/csharp.md`** -- package shape and dependency direction, typed ids, `Result<T>` errors,
  explicit DI in each project's `Add…()` method, one source-generated JSON context, ports for every technology.
- [ ] **Red test first** -- the failing test was committed before the code (`.agents/rules/csharp-testing.md`); new
  use cases come from `/new-slice`, new packages from `/new-package`.
- [ ] **Every test has one `Lane` trait** and the lanes stay within budget (`node scripts/ci/lane-budgets.mjs`).
- [ ] **No new edge outside `deps.yaml`** -- `node scripts/check-deps.mjs` passes.
- [ ] **No suppressions without a `Justification`**, and the ratchet baseline at the end of `.editorconfig` didn't grow.

---

## Shell and hooks

- [ ] **Hooks follow `.agents/rules/hooks.md`** -- one canonical script, a provider adapter, fail-open logged as
  `HOOK-FAILOPEN`.
- [ ] **Bash tests never touch the real `~/.claude`, `~/.codex`, `~/.cursor` or `~/.agentic-workflow`** -- point
  `HOME` at a `mktemp -d` directory.
- [ ] **`./setup.sh --providers claude,codex,cursor --dry-run` runs cleanly** and writes nothing.

---

## Security

- [ ] **No secrets in code, config or logs** -- `Aw.Redaction` scrubs C# log lines and exports.
- [ ] **SQL uses typed parameters** -- never interpolate input into SQL.
- [ ] **An http host binds to loopback in code** (the `/new-package` http shape: `LoopbackBinding`, Host and Origin
  checks); nothing in the environment or on the command line can widen it.
- [ ] **New environment variables are in `.env.example`** (`scripts/check-env-example.sh`).

---

## Documentation

- [ ] **`AGENTS.md` and the rule files match the change** -- commands, merge gate, structure; `scripts/sync-rules.sh
  --check` passes after adding or removing a rule.
- [ ] **New check ids have a rule record** (`node scripts/check-rules.mjs`).
- [ ] **No dead code** -- unused imports, variables and functions are removed.
