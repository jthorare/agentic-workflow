# Plan Design Review: sindri-context-retrieval, round 5 (slim, final)

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (ctx-wt worktree, after round 4)
**Reviewed:** 2026-10-10
**Earlier:** round 1 (NEEDS_WORK), round 2 (PASS), round 4 `../round4/design-review.md` (NEEDS_WORK, one HIGH)

**Lens scope.** This is a backend engine with CLI, HTTP and MCP surfaces and no GUI. The review covers what a person or
session reads and acts on: the result IA, CLI and MCP output, states and exit codes, the map review flow and the
owner-check flow. Visual dimensions (tokens, contrast, brand) don't apply.

## Inputs available
| Input | Present? |
|---|---|
| plan.md (the spec, passed with `--plan`) | ✓ |
| design-brief.md | ✗ (§8.3–§8.7 are the design source) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (no visual surface) |

`.impeccable.md` is absent, so Brand/voice is N/A.

## Round-4 findings: verification
| Round-4 finding | Status | Where |
|---|---|---|
| HIGH: owner-check seeding used a verb that didn't exist | **Resolved** | `map export <file>` on the CLI (L415) and `GET …/map/export` (L414); `ExportMap` in Phase 3 keeps original timestamps and provenance and restores them on import (L978); owner-check step 4 names the pair (L868–869) |
| MEDIUM: owner check took the real port and MCP name | **Resolved** | Per-stage scratch home, `AW_SINDRI_PORT=7480`, `sindri-ownercheck` registration, teardown (L863–873). One follow-on MEDIUM below (two servers in one session) |
| MEDIUM: map-version lifecycle undefined | **Resolved, with one stale sentence** | Lifecycle bullet (L467–471): build → `draft`, queries read `current`, `promote` or an empty queue promotes. L477–478 still describes the old whole-file refusal (new MEDIUM below) |
| MEDIUM: MCP-off state had no code or fix | **Mostly resolved** | `SND-CONTEXT-020`, `/mcp` 403 with the code, shown in `sources status` (L441). A 403 at the transport surfaces in Claude Code as a failed server, not a tool error the session can read (LOW below) |
| MEDIUM: `SND-CONTEXT-004` suggested a rebuild | **Resolved** | L437: migrate on start, update the build if the store is newer, never delete `context.db`, check permissions |
| MEDIUM: no way to find an `--undo` id | **Resolved** | `map review --history` (L415, L470–471). Output format unstated (LOW) |
| MEDIUM: whole-file `--apply` refusal | **Resolved in the lifecycle bullet** (L469–470), contradicted at L477–478 (below) |
| LOW: `confirmed` undefined | **Resolved** | Round-4 record (L748–749). Not yet stated in §8.4 itself (LOW) |

## Summary
The round-4 HIGH is cleared: `map export` exists on the CLI and HTTP, round-trips provenance and timestamps, and the
owner-check outline seeds from it. The owner-check home now has its own port, MCP name and teardown, and the
map-version lifecycle, `--history`, MCP-disabled code and the `SND-CONTEXT-004` fix are all in. Two MEDIUMs remain,
both small text fixes. First, L477–478 still says `--apply` refuses any file from a map version that's no longer
current, which contradicts the partial-apply rule eight lines up. Second, during an owner check the work repo's
session sees both `sindri` and `sindri-ownercheck` with the same tool names, and the agent-rules snippet just says
"call `context_query`", so a check can pass against the real instance. No HIGH remains.

## Ratings
Anchors per `_shared/severity.md` (5 = nothing above LOW · 4 = MEDIUM only · 3 = ≥1 HIGH, none structural · 2 = structural HIGH · 1 = CRITICAL or unaddressed).

| Dimension | Rating | Top gap (spec lines) → recommended change |
|---|---|---|
| Information architecture | 4/5 | **MEDIUM: two rules for the same `--apply` case.** L469–470: "`--apply` works on the latest draft only; entries whose targets still match are applied and the rest listed as skipped (`SND-CONTEXT-043` only when nothing matches)". L477–478: "`--apply` refuses a file exported from a map version that is no longer current (`SND-CONTEXT-043`, fix: export again)". The second is the round-2 wording. It refuses the whole file and still speaks of a "current" export, though exports come from a draft. → Delete the first half of the L477 bullet and keep only "quitting mid-queue keeps every decision…". Reword the `-043` row (L446) as "No entry in the review file matches the latest draft". **LOW:** `map import`'s resulting status isn't in the lifecycle. Phase 3's signal (L984–986) needs an imported map to be `current` before `review` or `promote` exist. Say that import writes `current` (or `draft` plus an implicit promote). **LOW:** `--draft` (L467) is missing from the `context query` synopsis (L415) and from the HTTP and MCP parameters. **LOW:** `context map status` output is still undefined. It's the fix for `-003` and the natural place to show current and draft versions and pending counts. |
| Interaction states | 5/5 | **LOW: the first build leaves no `current` map.** After `map build` on a repo with nothing imported, a query reads `current`, finds none, and `SND-CONTEXT-030` says "`context map import` or `context map build`" (L442), which the owner just ran. → When a draft exists, say "map v13 is a draft; `map review` or `map promote`". **LOW: MCP disabled as a transport 403.** Claude Code shows a failed server, not the code. Only `sources status` explains it (L441). → In the per-repo registration docs, check `context sources status` before `claude mcp add`, or answer `initialize` and return `-020` as a tool error. **LOW (carried):** empty states for `neighbors`/`path`, an empty review queue and no sources configured; `--json` body on exit 2; `eval probes` baseline and exit code. |
| User flow completeness | 4/5 | **MEDIUM: an owner check can hit the real instance.** Step 5 registers `sindri-ownercheck` beside the existing `sindri` in the work repo (L870–871). Both expose `context_query`, and the work repo's agent file tells the session to "call `context_query`" (L420). The session may answer from the real `sindri` on the merged build and the check records a pass for the PR. → In the outline, remove or disable the real `sindri` local registration for the check and restore it at teardown, or require the steps to name the prefixed tool (`mcp__sindri-ownercheck__context_query`) and check `queryId`/`mapVersion` in the result against the scratch instance. **LOW:** imports restore `human` provenance (L978), and `POST …/map/import` (L414) has no terminal gate, while §3 (L95–96) gives `human` only to interactive CLI actions. → Restore `human` only from the CLI at a terminal. Otherwise downgrade it to `model` and queue it for review. **LOW:** the export is taken from the real home (merged build) and imported into the PR build. Say the import accepts the previous export schema version. **LOW:** port 7480 is fixed for every stage's owner check. Note that checks run one at a time. |
| Accessibility | 5/5 | No regressions. Single-key prompts with a non-interactive equivalent, terminal confirmation for `--apply`, status as words, `NO_COLOR`. **LOW:** show the override id after each interactive action (the transcript L457–465 shows none), and accept a `--history` list position for `--undo`. **LOW (carried):** the sources table on narrow terminals. |
| Brand/voice consistency | N/A (missing .impeccable.md) | Not scored. The text-output example is a Phase 1 deliverable in `docs/sindri/context.md`. |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| CLI `context query` (text + `--json`) | n/a (≤ 2 s p95) | L426–429, L434–448; `-011` exit 2 L440 (`--json` body on 2 unstated, LOW) | `-003`/`suggestedAnchors` L436; draft-only map → `-030` with a misleading fix (LOW) | L380–405 |
| CLI `context show` / `neighbors` / `path` | n/a | `-003`, `-030` | **GAP** (no neighbors / no path; LOW) | L415 |
| CLI `context map import` | n/a | Versioned schema, all-or-nothing, `-005` (L682, L978) | `-030` | L984–986; resulting status unstated (LOW) |
| CLI `context map export` | n/a | `-005` (L82, L978) | `-030` presumably | L978, L868 ✓ (was GAP) |
| CLI `context map build` | Progress lines and lock wait L428–429 | Exit 75, `-040`; `-042` fatal vs notice (LOW) | **GAP** (no anchors extracted; LOW) | L1000–1003 |
| CLI `context map status` / `promote` | n/a | n/a | **GAP** (LOW) | Output undefined (LOW); promote L468 |
| CLI `context map review` (interactive, `--export`, `--apply`, `--history`, `--undo`) | n/a | `-041` L444; `-043` defined twice, contradictory (MEDIUM, L469–470 vs L477–478) | Orphan queue L464; empty queue promotes (L468) | L456–481; `--history` ✓ |
| CLI `context sources status` / MCP `context_sources` | n/a | `-002`, `-020`, `-050`, `-070` | **GAP** (no sources configured; LOW) | L418–422 |
| CLI `context eval backtest \| probes \| map` | L428–429 | Exit 75, `-040` | Small-sample gate (§9) | Probes baseline and exit unstated (LOW) |
| MCP `context_query` / `context_node` / `context_neighbors` | n/a | Tool errors with code and fix (L416); disabled → transport 403 `-020` (LOW) | `suggestedAnchors` in `structuredContent` | L416 |
| Owner check (`owner-check.md`) | n/a | Pass/fail + counts (L872–875) | Seeded by `map export` → `map import` ✓ | Own port and MCP name ✓; two servers with the same tool names (MEDIUM) |
| Workflow block `context.retrieve` (Phase 10) | n/a | Deferred to the workflow-engine spec (LOW) | Deferred | Phase 10 |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| CLI text output (query, show, sources) | n/a (non-interactive) | Plain text, `[Cn]` markers, status as words (L403–407); narrow-terminal table **GAP** (LOW) | `NO_COLOR`, no colour-only status | n/a |
| CLI `map review` | Single-key prompts; `--export`/`--apply` alternative (L474–476) | Linear prompt text | Status as words | Sequential queue, resume after quit; `--history` lists ids for `--undo` (ids not shown inline, LOW) |
| MCP text rendering | n/a | Short summary + `structuredContent` (L416) | n/a | n/a |

## Verdict
Every round-4 HIGH and MEDIUM is resolved. What's left is two MEDIUM text fixes: the stale whole-file `--apply` refusal
at L477–478, which contradicts the partial-apply rule, and owner checks that can query the real `sindri` server
instead of `sindri-ownercheck`. Both are one-sentence edits and neither blocks the design. No dimension is at 3 or
below.
verdict: PASS
