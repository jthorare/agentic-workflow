# Plan Design Review — sindri-context-retrieval, round 2 (slim)

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (revised after `/autoplan`)
**Reviewed:** 2026-10-10
**Round 1:** `../design-review.md` (NEEDS_WORK: three dimensions at 3/5)

**Lens scope (unchanged).** This is a backend engine with CLI, HTTP and MCP surfaces and no GUI. The lens covers what a
person or session reads and acts on: how a result is organized, the CLI and MCP text rendering, and the map review
interaction. Visual dimensions (tokens, contrast, brand) do not apply.

## Inputs available
| Input | Present? |
|---|---|
| plan.md (the spec, passed with `--plan`) | ✓ |
| design-brief.md | ✗ (the spec's §8.3–§8.7 are the design source) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (no visual surface) |

`.impeccable.md` is absent, so Brand/voice is N/A.

## Round-1 findings: verification
| Round-1 finding | Status | Where (spec lines) |
|---|---|---|
| HIGH: no exit-code or empty-result contract | **Resolved** | §8.6 L413–416: 0 / 1 / 2 / 75, each with its fix; `SND-CONTEXT-003` points to `suggestedAnchors` (L422); `suggestedAnchors` is top level in the result (L379) |
| HIGH: `affects` and per-item provenance missing from the result | **Resolved** | L372–374 (`affects` with `depth` and `path`); L389 (`provenance`, `confirmed`, `trust` on every item) |
| HIGH: map review interaction unspecified | **Resolved** | §8.7 L435–455: transcript, impact order, bulk accept for deterministic proposals, the `orphaned` queue, `--export`/`--apply`, subcommands for rename, merge, move, add and delete; human-only rule L95–96, `SND-CONTEXT-041` |
| MEDIUM: conflicts could be truncated | **Resolved** | L361–362: conflicts and `degraded` are outside the budget and always returned in full; unit test L547 |
| MEDIUM: section names and order not fixed | **Resolved** | L359–360: `graph`, `affects`, `decisions`, `code`, `records` |
| MEDIUM: degraded detail (`message`, `since`, `ref`) | **Mostly resolved** | L380 adds `message` and `since`. There is still no `ref`, and no stated home for record-level `scrub-blocked` (§7 L294–295). See IA below |
| MEDIUM: no progress, lock-wait or budget-hit output for long jobs | **Resolved** | L415–416 (stderr progress lines, lock holder named); `SND-CONTEXT-040`, `-042` |
| MEDIUM/a11y: text output, colour-only status, `NO_COLOR` | **Resolved** | L391–394: `DEGRADED` banner, `(draft)` marker, conflicts block first, status as words, `NO_COLOR`; the example is a Phase 1 deliverable |
| MEDIUM: `map import` error flow | **Answered** | §14 S-L4 L649–650 (versioned schema, all-or-nothing, worktree refusal). Error location (file:line) is not stated. LOW |
| LOW: raw `score`/`why` in text | Open, LOW | The text rendering (L391–394) doesn't say whether rank and reason replace them |
| LOW: how to get "+12 more" items | Open, LOW | L361 |

## Summary
Every round-1 HIGH is fixed in the text, and each fix is concrete and tested. The exit codes are pinned and mapped to
the error catalogue. The result schema carries `affects` with paths and per-item provenance, confirmation and trust.
Map review is now a full flow, with a worked transcript, an impact-ordered queue, a non-interactive export/apply form,
the human-only rule and an orphan queue. What remains are MEDIUM edge cases: exit code 1 can't tell "nothing matched"
from "every source was down"; a review file can go stale between `--export` and `--apply`; a reject can't be undone
through any named flow; and record-level `scrub-blocked` has no `ref` and no clear home. None of them is structural.

## Ratings
Anchors per `_shared/severity.md` (5 = nothing above LOW · 4 = MEDIUM only · 3 = ≥1 HIGH, none structural · 2 = structural HIGH · 1 = CRITICAL or unaddressed).

| Dimension | Rating | Top gap (spec lines) → recommended change |
|---|---|---|
| Information architecture | 4/5 | **MEDIUM:** `degraded` entries (L380) are per source, but §7 says a `Blocked` scrub "drops the text, keeps the ref, and reports `scrub-blocked`" (L294–295) without saying where. It could be a `degraded` entry, a citation `state`, or the egress `[REDACTED:blocked]` field (L311–312). → Make it a citation `state: "scrub-blocked"` on the affected `Cn` (it already keeps the ref), and keep `degraded` for whole-source outages. **LOW:** `confirmed` isn't defined. The L374 example shows a deterministic item `confirmed: true`, but D8 (L60) says the extracted map is a draft until the owner confirms it. → One line: `confirmed` = the node's map version is `current` and, for model provenance, a human accepted it (or whatever the rule is). **LOW:** the text rendering doesn't say how `score`/`why` appear. |
| Interaction states | 4/5 | **MEDIUM:** exit 1 covers "no results" (L413), and "possibly degraded" stays exit 0. So when every source is `Unavailable` and the graph is empty, the result is exit 1 with the "no results" fix. That sends the user to rephrase the query instead of `context sources status`. → State that a run with no results and at least one `degraded` entry exits 1 and leads with `SND-CONTEXT-010` and its fix, or give all-sources-down its own code. **LOW:** no empty states for `neighbors` (no neighbors), `path` (no path within the length cap), `sources status` (no sources configured) and `map review` (empty queue). → One line each in `docs/sindri/context.md`. |
| User flow completeness | 4/5 | **MEDIUM:** a `--export` file can go stale. If `map build` (scheduled, L844) produces a new draft between export and `--apply`, what happens to entries whose target key moved or vanished? → Stamp the export with the map version. On `--apply`, apply entries whose rejection identity (L253–254) still matches, list the rest as skipped, and never apply a file silently against another version. **MEDIUM:** there is no undo or revisit for a reject. Rejections are permanent until the quoted span changes (L253–254), and §8.7 names no flow to reverse one. → Add `map review --rejected` (list and un-reject), or say that `add` restores a rejected node and records the override. **LOW:** `[q]uit` (L445) doesn't say the work done so far is kept and the next run resumes. It is implied by "every action is one `map_override` row" (L451). Say it. **LOW:** bulk accept covers deterministic proposals only. A first real build may yield hundreds of model proposals; consider accept-subtree. |
| Accessibility | 5/5 | Map review uses single-key prompts with a non-interactive equivalent (L445–454). Status is in words and `NO_COLOR` is honoured (L393–394). **LOW:** the sources table on narrow terminals (wrap or stack) is still unstated. |
| Brand/voice consistency | N/A — missing .impeccable.md | (Unscored: the text-output example is deferred to `docs/sindri/context.md` in Phase 1, L394. Fine for a spec.) |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| CLI `context query` (text + `--json`) | n/a (≤ 2 s p95, L539) | L413–415, L380, L387–388, `DEGRADED` banner L392 | L413 exit 1 + `SND-CONTEXT-003`/`suggestedAnchors` L422, L379; all-sources-down vs no-match ambiguous (MEDIUM) | L368–394 |
| CLI `context show` / `neighbors` / `path` | n/a | L413, `SND-CONTEXT-003` L422, `-030` L426 | **GAP** (no neighbors / no path; LOW) | L832–834 |
| CLI `context map import` | n/a | L649–650 (schema, all-or-nothing, worktree `-005`); location not stated (LOW) | `SND-CONTEXT-030` before import L426 | L832 |
| CLI `context map build` | L415–416 (lock wait, extract and label progress) | L416 exit 75, `-040`, `-042` L427–429 | **GAP** (no anchors extracted; LOW) | L847–849 |
| CLI `context map review` / `POST /map/review` | n/a (interactive) | `-041` L428, L95–96 | Orphan queue L446; empty queue **GAP** (LOW) | L439–455 |
| CLI `context sources status` / `context_sources` | n/a | L274, `-002`, `-050`, `-070` L421–431 | **GAP** (no sources configured; LOW) | L402–409 |
| CLI `context eval backtest \| probes \| map` | L415–416 | Exit 75, `-040`, small-sample gate L501–503 | L501–503 | L885–886 |
| MCP `context_query` / `context_node` | n/a | L404 (tool errors with code and fix); off when `context.mcp.enabled` is false | Via `suggestedAnchors` in `structuredContent` | L404 |
| Workflow block `context.retrieve` (Phase 10) | n/a | **GAP** (step behavior when degraded; LOW, deferred to the workflow-engine spec) | **GAP** | L915–916 |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| CLI text output (query, show, sources) | n/a (non-interactive) | Plain text, `[Cn]` markers, status as words (L391–394); narrow-terminal table **GAP** (LOW) | `NO_COLOR`, no colour-only status (L393–394) | n/a |
| CLI `map review` | Single-key prompts (L445, L447); `--export`/`--apply` alternative (L452–454) | Linear prompt text, one proposal per line | Status as words | Sequential queue; resume after `[q]uit` implied, not stated (LOW) |
| MCP text rendering | n/a | Short summary + `structuredContent` (L404) | n/a | n/a |

## Verdict
All three round-1 HIGH findings are resolved in the text. Every dimension is now 4 or 5, and the open items are MEDIUM
or LOW (the all-sources-down exit code, stale review files, undoing a reject, where `scrub-blocked` goes). They can be
fixed in the phase that owns them (S-1A for exit codes and citation state, S-4 for review).
verdict: PASS
