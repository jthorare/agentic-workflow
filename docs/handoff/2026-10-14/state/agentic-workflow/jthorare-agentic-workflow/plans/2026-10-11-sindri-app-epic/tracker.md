# Sindri build tracker

Kept by the oracle session (agentic-workflow-29). One snapshot per check, newest first. The order of work is the
wave table and "Execution plan" in `docs/sindri/README.md` on `main`.

## 2026-10-12 — snapshot 2

`main` = `6e3f25f5`. Merged since snapshot 1: #174 (API P0), #177 (S-1C), #178, #176 (S-FX), #175 (S-2R), #179 (engine P1).
Each was checked with its head merged onto main (repo and C# checks) before merging. Built: 6 of 46 phases (~13%); wave 0 is 6 of 8.

| Lane | Phase | Where | State | Next / blocker |
|---|---|---|---|---|
| API | P1 CQRS spine | local, units A–D written, integrating; unit E (`sindri-prove.sh`, goldens) started | no PR yet | Draft PR in a few hours, ready ~half a day later. Critical path: S-1A, engine P2, U1 wait on it |
| Engine | P3 pure engine | #180 (draft) at `dcd5c917`, retargeted after #179 | review loop | Ready from the lane |
| Engine | P1 | merged #179 | done | Re-prove through `sindri-prove.sh` once API P1 lands (owner condition) |
| Context | S-1A | — | waiting | API P1 |
| UI | U0 | local | not pushed | Push and open the PR |

**Spec change merged:** engine spec revision 7.1 (#179): null-in-hash, optional block-id area, `RCP-NAME-001`. No other spec uses the block-id form.
**Open owner question:** context §4 fan-out exception, compose edge or project edge (#177 took project).

## 2026-10-11 ~20:25 UTC — snapshot 1

`main` = `45e8d4f4`: S1 (#163), Judge (#156), the heavy lock (#157), the dependabot bump (#150) and the epic (#170,
#172, #173) all merged. The full local gate passes on `main`. The `aw-state-dir-isolation` test flakes while lanes hold
the heavy lock or write proof logs; it passes when nothing else is writing.

Peer sessions: 4 busy (agentic-workflow-26, -9a, -30, -95).

| Lane | Phase | Where | State | Next / blocker |
|---|---|---|---|---|
| Gate | S1, Judge | merged | done | — |
| API | P0 `--slice-only` | #174 | review run 5: all findings fixed; mergeable | **Ready for the owner to merge**; P1 is stacked on it |
| API | P1 CQRS spine | `feat/sindri-api-p1-cqrs`, local (47 files uncommitted) | in progress | Needs #174 merged |
| Engine | P1 recipe model and validator | `feat/sindri-wf-p1-recipe-model` + units a–f (local) | integrating units; no PR yet | **Branch is 213 commits behind `main`**: still based on the old S1 branch. Rebase onto `main` before opening the PR |
| Engine | P3 pure engine | `feat/sindri-wf-p3-pure-engine` + units a, b | contract unit pushed (red); units in progress | Same rebase |
| Context | S-1C `compose` edge | #177 | review run 3: all findings fixed; mergeable | **Ready for the owner to merge** |
| Context | S-2R Redaction patterns | #175 (draft) | review run 1: **7 issues open**, 13 suggestions, 6 nits | Address run 1, then review again |
| Context | S-FX fixture app | #176 (draft) | review run 4: 0 blocking, 0 issue (4 nits) | **Conflicts with `main`**: needs a rebase |
| UI | U0 theme and shell | `feat/sindri-ui-u0` + contract and units a, b, d (local, nothing pushed) | integrating | Push and open the PR; U1 waits for API P1 and P2 |

**Spec conformance (oracle scan):** no lane reintroduces a retired or forbidden term.
- **Engine:** reserves `SND-WF-007` and `SND-RECIPE-004` as retired, as the spec says.
- **API P0:** keeps S1's `SND-API-004/005`.
- **Context:** its `ConnectAI`, `LINEAR_API_KEY` and `POST /context/query` hits are the same counts as `main` (old docs).
- **UI U0:** no `ReflectionBinding` and no OS notification code in what's pushed.

**Watch next:**
- #174 and #177 merging;
- the engine rebase onto `main`;
- #176's conflict;
- #175's open issues;
- U0's first push.
