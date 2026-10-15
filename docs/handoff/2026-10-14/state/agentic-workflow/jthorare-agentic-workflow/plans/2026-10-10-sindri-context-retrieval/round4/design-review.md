# Plan Design Review: sindri-context-retrieval, round 4 (slim)

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (ctx-wt worktree, after rounds 2 and 3)
**Reviewed:** 2026-10-10
**Earlier:** round 1 `../design-review.md` (NEEDS_WORK), round 2 `../round2/design-review.md` (PASS)

**Lens scope.** This is a backend engine with CLI, HTTP and MCP surfaces and no GUI. The review covers what a person or
session reads and acts on: the result IA, the CLI and MCP output, states and exit codes, the map review flow and the
owner-check flow. Visual dimensions (tokens, contrast, brand) don't apply.

## Inputs available
| Input | Present? |
|---|---|
| plan.md (the spec, passed with `--plan`) | ✓ |
| design-brief.md | ✗ (the spec's §8.3–§8.7 are the design source) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (no visual surface) |

`.impeccable.md` is absent, so Brand/voice is N/A.

## Round-2 findings: verification
| Round-2 finding | Status | Where |
|---|---|---|
| MEDIUM: all-sources-down shared exit 1 with "no results" | **Resolved** | `SND-CONTEXT-011`, exit 2 (L434). The exit-code paragraph (L421–423) doesn't list it under 2 yet (LOW, below) |
| MEDIUM: `scrub-blocked` had no home | **Resolved** | Citation `state: "scrub-blocked"` (L301–302). The §8.4 state list (L394–395) and the text rendering (L398–401) leave it out (LOW) |
| MEDIUM: stale `--export` file | **Resolved, but too strictly** | `SND-CONTEXT-043` (L439, L465). See the new MEDIUM under User flow: a whole file of edits is discarded, and "current" clashes with the `draft` status |
| MEDIUM: no undo for a reject | **Partly resolved** | `--undo <override-id>` (L467). No way to find an override id (new MEDIUM) |
| LOW: `[q]uit` resume | **Resolved** | L466 |
| LOW: `confirmed` undefined | Open, LOW | L381, L396 against D8 (L60) |
| LOW: empty states for `neighbors`, `path`, `sources status`, empty review queue | Open, LOW | Unchanged |

## Summary
Nothing regressed structurally. The round-2 MEDIUMs on exit codes, `scrub-blocked`, stale review files and resume are
fixed in the text. The changes since round 2 add new gaps. The most serious is in the owner-check flow: it tells the
owner to seed a scratch home "by `map import` of an export" (L826), but no verb exports the reviewed map. `map review
--export` writes the pending queue, not the decisions, and copying `context.db` is forbidden. As written, every owner
check that needs a reviewed map (S-5B, S-CF, S-7B, S-8) can't be run. Also new: `--undo` needs an override id that
nothing displays; `--apply` drops a whole edited file whenever the map moves on, and the draft/current lifecycle it
depends on is never defined; and a session whose MCP is switched off (the default) gets no code or fix. The fixes are
small, but the export gap is HIGH because the spec prescribes a step that can't be run as written.

## Ratings
Anchors per `_shared/severity.md` (5 = nothing above LOW · 4 = MEDIUM only · 3 = ≥1 HIGH, none structural · 2 = structural HIGH · 1 = CRITICAL or unaddressed).

| Dimension | Rating | Top gap (spec lines) → recommended change |
|---|---|---|
| Information architecture | 4/5 | **MEDIUM: the map-version lifecycle is undefined.** `map_version.status` is `building | draft | current | retained` (L276). Builds "flip `current`" (L289), yet D8 says the map stays a draft until the owner confirms it (L60), and review runs on "map v13 (draft…)" (L451). The spec never says what moves a draft to `current`: completing review, the build flip, or something else. It also doesn't say which version a query reads while review is part done. `--apply` "refuses a file exported from a map version that is no longer current" (L465), but the export came from a *draft*, which by the enum is never `current`. → Define the transitions in one place: build → `draft`, then `current` on review completion or immediately with per-item `confirmed`. Name the version queries read. Reword `SND-CONTEXT-043` in terms of "the latest draft". **LOW:** `confirmed` still has no definition (the L381 example shows a deterministic item `confirmed: true` before any review). **LOW:** the citation states at L394–395 omit `scrub-blocked`, and the text rendering doesn't say how `stale`, `unchecked` and `scrub-blocked` show. **LOW:** `context map status`, the fix for `SND-CONTEXT-003` (L430), has no defined output. |
| Interaction states | 4/5 | **MEDIUM: the MCP-off state has no code or fix.** `context.mcp.enabled` defaults to false (L482), and Phase 1 docs tell the owner to register MCP per repo (L91–94, L898). §8.5 only says "Off when `context.mcp.enabled` is false" (L411). The session then sees an MCP connection failure or an empty tool list with nothing to act on, and `context:mcp-scope` checks scope only. → Keep `/mcp` up and return a tool error with a new code (S-1M range, `SND-CONTEXT-02x`: "MCP access disabled; set `context.mcp.enabled` and `profile approve`"). Report the flag in `context sources status` beside the registration status (L415–417). **MEDIUM: `SND-CONTEXT-004`'s fix is wrong.** For "Store schema mismatch or the store can't open", the fix is "Rebuild with `context map build`" (L431). `map build` doesn't migrate a schema, doesn't exist before Phase 4, and R3-1 says the owner's review decisions can't be rebuilt (L700). → Split the code: schema newer than the binary (fix: run the matching build) versus can't open (permissions or path). Never suggest a rebuild as the fix for the real store. **LOW:** the exit-code paragraph lists exit 2 as tool error only (L421–423), while `SND-CONTEXT-011` is exit 2 (L434). It also doesn't say whether `--json` still prints the result body with `degraded` on 2. **LOW:** with labeling off by default, does `map build` fail with `SND-CONTEXT-042` (L438) or finish deterministic-only with a notice? Say it's a notice, exit 0. **LOW:** `eval probes` has no stated baseline or exit code for "doesn't regress" (L881–882), which every phase signal from Phase 3 depends on. |
| User flow completeness | 3/5 | **HIGH: owner-check seeding uses a verb that doesn't exist.** "A check that needs prior real state (a reviewed map) seeds the scratch home by `map import` of an export, not by copying `context.db`" (L825–826). The CLI has `map import`, `build`, `status` and `review [--export|--apply]` (L410). `review --export` writes the pending queue (L462), not the accepted, rejected and renamed decisions. So S-5B, S-CF, S-7B and S-8's owner checks can only seed by redoing the whole review in the scratch home, and the model proposals and their decisions may not reproduce. The spec also doesn't say whether imported decisions keep `human` provenance (§3 L95–96 gives `human` only to interactive review), which changes `confirmed` and `trust` in the scratch results. → Add `context map export <file>` (outside worktrees, `SND-CONTEXT-005`): the current map plus its overrides in the import schema, with original provenance and dates. Have `map import` at an interactive terminal restore overrides as `human`. Name this pair in the owner-check steps. **MEDIUM: the owner check collides with the real instance and MCP registration.** The owner-check home is a second Sindri on the owner's machine (L823–825). The spec gives it no port (stage owners get `AW_SINDRI_PORT` from 7491 up, L800, but the owner check doesn't). The Phase 1 and S-3M signals need "a Claude Code session in the work repo" (L906, L936), whose `--scope local` registration points at the real port. → In `owner-check.md`: run the PR build on its own port, re-point the work repo's local MCP registration to it for the check, and restore it after. Or register a second server name (`sindri-check`) and remove it after the check. **MEDIUM: `--undo <override-id>` has no way to find the id.** The review transcript (L450–457) shows no ids, and no verb lists overrides or rejections. → Add `map review --history [--rejected]` listing override ids with action, target and date. Show the id after each action in the interactive transcript. **MEDIUM: a refused `--apply` discards all the owner's edits.** With a scheduled rebuild (L946–947), an edited `review.yaml` can be refused whole, and the fix is to export and edit again (L439, L465). → Apply the entries whose rejection identity (L260–261) still matches the latest draft. List the rest as skipped with reasons. Exit 1 only if nothing applied. Or pause scheduled rebuilds while a review export is outstanding. |
| Accessibility | 5/5 | No regressions. Single-key prompts with a non-interactive equivalent, `--apply` confirmation at a terminal (L463–464), status in words and `NO_COLOR` (L400–401). **LOW:** `--undo` takes an opaque id. Once ids are listed (above), accept list position as well. **LOW:** the sources table on narrow terminals is still unstated. |
| Brand/voice consistency | N/A (missing .impeccable.md) | Not scored. The text-output example is a Phase 1 deliverable in `docs/sindri/context.md` (L401). |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| CLI `context query` (text + `--json`) | n/a (≤ 2 s p95, L554) | L421–423, L426–441; all-down `-011` exit 2 L434 (output shape on 2 unstated, LOW); `DEGRADED` banner L399 | Exit 1 + `-003`/`suggestedAnchors` L421, L430, L386 | L375–401 |
| CLI `context show` / `neighbors` / `path` | n/a | `-003` L430, `-030` L435 | **GAP** (no neighbors / no path within cap; LOW) | L409–410 |
| CLI `context map import` | n/a | Schema, all-or-nothing, worktree `-005` (L665–666, L432) | `-030` L435 | L934–936 |
| CLI `context map export` | **GAP: verb missing** (HIGH, owner-check seeding L826) | GAP | GAP | GAP |
| CLI `context map build` | Progress lines and lock wait L423–424 | Exit 75, `-040` L436; `-042` fatal vs notice unclear (LOW) | **GAP** (no anchors extracted; LOW) | L950–952 |
| CLI `context map status` | n/a | n/a | **GAP** (no map: `-030` presumably) | **GAP** (output undefined; LOW) |
| CLI `context map review` (interactive, `--export`, `--apply`, `--undo`) | n/a | `-041` L437, `-043` L439 (whole-file refusal, MEDIUM); `--undo` with unknown id unstated | Orphan queue L456; empty queue **GAP** (LOW) | L449–468; undo target discovery **GAP** (MEDIUM) |
| CLI `context sources status` / MCP `context_sources` | n/a | `-002`, `-050`, `-070` L429, L440–441; MCP-disabled flag not shown (MEDIUM) | **GAP** (no sources configured; LOW) | L415–417 |
| CLI `context eval backtest \| probes \| map` | L423–424 | Exit 75, `-040`; small-sample gate L515–517 | L515–517 | L988–989; probes "doesn't regress" baseline and exit unstated (LOW) |
| MCP `context_query` / `context_node` / `context_neighbors` | n/a | Tool errors with code and fix (L411); MCP disabled: **GAP** (MEDIUM) | `suggestedAnchors` in `structuredContent` | L411 |
| Owner check (`owner-check.md`, owner-check home) | n/a | Pass/fail plus counts record (L826–827) | Empty `context.db` by design; seeding needs `map export` **GAP** (HIGH) | Port and MCP re-pointing **GAP** (MEDIUM) |
| Workflow block `context.retrieve` (Phase 10) | n/a | Deferred to the workflow-engine spec (LOW) | Deferred | L1018–1019 |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| CLI text output (query, show, sources) | n/a (non-interactive) | Plain text, `[Cn]` markers, status as words (L398–401); narrow-terminal table **GAP** (LOW) | `NO_COLOR`, no colour-only status | n/a |
| CLI `map review` | Single-key prompts (L455, L457); `--export`/`--apply` alternative (L462–464) | Linear prompt text | Status as words | Sequential queue; resume after quit (L466); `--undo` needs an opaque id (LOW) |
| MCP text rendering | n/a | Short summary + `structuredContent` (L411) | n/a | n/a |

## Verdict
User flow completeness drops to 3/5 on one HIGH: the owner-check flow seeds its scratch home from a map export that no
verb produces (L826), so the owner checks for S-5B, S-CF, S-7B and S-8 can't be run as written. Adding `context map
export` with provenance-preserving import clears it. The MEDIUMs (map-version lifecycle, MCP-off state, the
`SND-CONTEXT-004` fix, owner-check port and MCP re-pointing, undo discovery, whole-file `--apply` refusal) are one-line
or one-paragraph fixes.
verdict: NEEDS_WORK
