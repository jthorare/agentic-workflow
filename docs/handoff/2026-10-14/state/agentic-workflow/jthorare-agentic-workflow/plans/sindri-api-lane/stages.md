# Sindri API lane — stages (spec docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md, amended by UI spec §6.7 E-1…E-19, §18.1; README X52)

## Contracts
- Autonomy: run continuously (owner's brief 2026-10-11). Stop only for: human merge of each PR, a needed spec change, the 5-run review cap.
- Git topology: repo jthorare/agentic-workflow (not a fork), base `main`, push remote `origin`. Never push main. One branch per phase off main (or stacked on its parent phase's branch until that merges). Worktrees under .claude/worktrees/sindri-api-*. Never switch branches in the main checkout.
- CI unavailable (billing): run the AGENTS.md merge gate locally, paste output in each PR.
- Heavy builds: at most two at once across lanes (heavy-job lock, 2 slots).
- Proofs only through scripts/sindri-prove.sh (from P1); never the real $AW_SINDRI_HOME, setup.sh, providers, MCP registration.
- Tell the owner when P1, P3, P6, P10a, P10b merge.

## Stages
| # | Goal | Needs (merged) | Branch | Status |
|---|---|---|---|---|
| P0 | `--slice-only` for new-slice.mjs (C7) | — | feat/sindri-api-p0-slice-only | MERGED 13f51f72 |
| P1 | CQRS spine, callers, human token (X40), issue kind + carve-outs (X26), catalog E-8/E-12/E-16, EventFactory (C6), sindri-prove.sh, goldens (E-17) | S1 #163 (merged), P0 | feat/sindri-api-p1-cqrs | draft PR #181 (integrated 42c695e5) |
| P2 | projector, head, service.ran, profile-watch, health (E-14) | P1 | — | — |
| P3 | items, queue, E-18, item.linked, CreateItem link? | P2 | — | — |
| P5 | event stream, ledger read, GET /events/catalog (E-15) | P1+P2 | — | — |
| P4 | requests, decisions, needs-you, E-7 | P3 | — | — |
| P6 | jobs, services | P2+P5 | — | — |
| P8 | sindri_* MCP tools | P4+P6 | — | — |
| P11 | engine item states | P4 + engine P4 | — | — |
| P10a | sindri open, launch contract §8.1, E-1…E-5, E-11 | P3+P8 | — | — |
| P10b | opening is a takeover, E-6 (with engine lane) | P10a + engine P5 | — | — |

## P0 detail
- Observable signal: `bash scripts/tests/scaffold.test.sh` passes its --slice-only case (only the slice's own files change + its own lines in PublicAPI.Unshipped.txt, A.cs, IdsJsonContext); on a real package the red test compiles and fails.
- Files: scripts/scaffold/new-slice.mjs, scripts/scaffold/lib/*, scripts/tests/scaffold.unit.mjs, scripts/scaffold/e2e.sh, docs/csharp/scaffolds/new-slice.md, skills/new-slice/SKILL.md.
- Size: S.
