# Sindri PR babysitter and docs sync

Spec: `docs/superpowers/specs/2026-10-15-sindri-pr-babysitter-docs-sync-design.md`. This page holds what the C# build has so far.

## `sindri docs check --facts <dir> [--json]` (API17)

A local verb: it runs in this process and needs no running control plane. It runs the rules `step.docs.check` runs, through one shared service (`DocsCheckService`), over a docs worktree.

`<dir>` holds the Step's facts, as the docs agent finds them in `$AW_SINDRI_RUN_DIR`:

| File | Holds |
|------|-------|
| `workspace.json` | `fact:workspace`: the docs worktree's path, one JSON string |
| `docsScope.json` | `fact:docsScope`: `{ "base": "<commit>", "paths": ["docs/**", "*.md"], "candidates": [{ "path": "...", "designNamed": true }] }` |

| Rule | Fails when |
|------|------------|
| `DOCS-PATH-001` | a changed file is neither under the profile's doc globs nor a doc the design names |
| `DOCS-LINK-001` | a relative link in a changed `.md` file does not resolve inside the worktree |
| `DOCS-MD-001` | a changed `.md` file opens a code fence it never closes |
| `DOCS-CHANGE-001` | the worktree holds no change since `base` |

Output: one line per finding with its fix, then the verdict; `--json` prints `{ "findings": [{ rule, item, message, fix }], "passed": bool }`.

Exit codes: 0 no findings, 1 findings, 2 usage or a tool error (a missing directory or fact file, a worktree git cannot read).
