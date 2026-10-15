# Plan Design Review — sindri-context-retrieval (slim)

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`
**Reviewed:** 2026-10-10

**Lens scope.** This is a backend engine with no GUI in its phases (the Avalonia app is a separate client). The
visual dimensions (layout, tokens, contrast, brand) do not apply and are not scored. The lens still applies to
three things a person reads and acts on: the **information architecture of a result** (sections, citations,
conflicts, degraded states), the **CLI text output** (the human rendering of the same result, plus the MCP text
rendering a session reads), and the **map review interaction** (Phase 4, the gate that turns the draft map into
the confirmed map, D8). Ratings below cover those three only.

## Inputs available
| Input | Present? |
|---|---|
| plan.md (the spec, passed with `--plan`) | ✓ |
| design-brief.md | ✗ (none; the spec's §8.3–§8.5 and Phase 4 are the design source) |
| product.md | ✗ |
| .impeccable.md | ✗ |
| design-tokens.json | ✗ (not relevant: no visual surface) |

Absent `.impeccable.md` ⇒ Brand/voice is N/A.

## Ratings
Anchors per `_shared/severity.md` (5 = nothing above LOW · 4 = MEDIUM only · 3 = ≥1 HIGH, none structural · 2 = structural HIGH · 1 = CRITICAL or unaddressed).

| Dimension | Rating | Top gaps (spec lines) → recommended change |
|---|---|---|
| Information architecture | 3/5 | **HIGH:** the result schema (§8.4, L286–296) has no place for things other parts of the spec need: the `affects` list with paths (L190–191, Phase 3 signal L465–466), and each item's provenance, confidence and draft-or-confirmed state (D8 L59; `map_version.status` L212). A session can't tell a model-proposed, unconfirmed node from a confirmed one. → Add `affects: [{node, path[], distance}]` and per-item `provenance`, `confidence`, `mapStatus` to the §8.4 example. **MEDIUM:** §8.3 (L279) fills one section called "decisions and conflicts", but §8.4 puts `conflicts` in a top-level array (L291). Which one applies decides whether packing can cut a conflict down to "+N more" refs, which would hide a disagreement that D10 (L61) says must always be shown. → Keep `conflicts` top-level and outside the budget, or always fill it first. Never truncate it. **MEDIUM:** section names aren't listed (only `"decisions"` appears). → Give the fixed set and its order: `graph`, `affects`, `decisions`, `code`, `records`. **LOW:** raw RRF `score` (0.031) and `why` tokens like `rrf:onyx#2,linear#1` mean nothing to a person. Keep them in JSON. In text, show rank and a plain reason. |
| Interaction states | 3/5 | **HIGH:** `sindri context query` has no exit-code or empty-result contract. Nothing says what a zero-hit query, an unknown `--node` key (`NotFound`, L136), or every source `Unavailable` returns, or whether `degraded` changes the exit code. Every other C# CLI in the repo pins its exit codes (aw-readiness 0/1/2, aw-packets 0/1/2, bugfix-state 0/1/2/3), and scripts and sessions branch on them. → Add a §8.4 sub-table: 0 = results (possibly degraded), 1 = no results or unknown anchor (with `suggestedAnchors`), 2 = tool error or all sources down. Show the empty state for every shape, not only `text` (L252). **MEDIUM:** `degraded` entries hold only `{source, code}` (L292), and they mix whole-source outages with per-record `scrub-blocked` (L234). → Add `ref` for record-level entries, plus `message` and `since`. In text, show a degraded banner at the top. **MEDIUM:** `map build` and `eval backtest` run long (heavy-job lock L392, labeling, model adjudicator). The spec has no progress output, no "waiting for a heavy slot" message, and no output for a budget hit (`context.labeling.maxTokens`, L365) or for labeling being off. → Say what each prints in those states. |
| User flow completeness | 3/5 | **HIGH:** the map review interaction is unspecified. Phase 4 says only that "`map review` accepts some and rejects others" (L478–479), and `POST /map/review` has no payload (L311). It doesn't cover: interactive TTY prompts vs a review file vs flags; what the owner sees for each proposal (key, label, provenance, confidence, evidence `SourceRef`s with a snippet, the code anchor); the order (confidence, feature subtree); how to stop and resume over hundreds of proposals; or undo. The other override actions (rename, merge, move, add, delete, L199) and resolving `orphaned` links (L201) have no flow at all. This flow is the gate on the core deliverable (D1, D8). → Add a §8.6 "Map review" with one worked CLI transcript, a non-interactive form (for example `map review --export review.yaml` / `--apply review.yaml`, kept outside repos), the order rule, batch accept by subtree or confidence, and the `orphaned` queue. **MEDIUM:** `map import` (Phase 3) has no error flow for a bad YAML map, unknown edge types, or key collisions with aliases. → Validation output with file:line. **LOW:** when output is truncated ("+12 more", L280), nothing says how to get the rest. → `--section <name> --more` or a follow-up `context show`. |
| Accessibility | 4/5 | **MEDIUM:** if map review is an interactive TTY flow, it needs keyboard-only operation and a non-interactive equivalent (covered by the fix above). The text output must not use color alone for status: `stale`, `unstable`, `degraded`, `untrusted` and conflict markers must be literal words. Honor `NO_COLOR` and non-TTY output. The sources table must wrap or stack on narrow terminals. → One line each in the §8.4 text-rendering note (L303). Status is already carried in words in the JSON, which is good. |
| Brand/voice consistency | N/A — missing .impeccable.md | (Wording note, unscored: "like today's scope maps", L303, is the only spec for the text rendering. Give one rendered example.) |

## Surface × state coverage
| Surface | Loading | Error | Empty | Success |
|---|---|---|---|---|
| CLI `context query` (text + `--json`) | n/a (≤2 s p95, L363) | Partial: `degraded` L136–137, L292; `stale`/`unstable` L298–300; exit codes and all-sources-down **GAP** | **GAP** (`suggestedAnchors` only for `text`, L252) | L286–303 (text render one line, L303) |
| CLI `context show` / `neighbors` / `path` | n/a | `NotFound` exists (L136); how it is shown **GAP** | **GAP** (no neighbors; no path between `from` and `to`) | L465–466 (Phase 3 signal) |
| CLI `context map import` | n/a | **GAP** | **GAP** | L464 |
| CLI `context map build` | **GAP** (heavy lock wait, labeling progress) | **GAP** (labeling off, budget hit, graphify failure) | **GAP** (no anchors extracted) | L477–478 |
| CLI `context map review` / `POST /map/review` | **GAP** | **GAP** | **GAP** (nothing to review; `orphaned` queue) | L478–479 (outcome only, no interaction) |
| CLI `context sources status` / `context_sources` | n/a | L217 (`last error code`), L402–403 (change-feed gap reported) | **GAP** (no sources configured) | L311–313 |
| CLI `context eval backtest \| probes \| map` | **GAP** | L367 (`small sample` can't pass) | L367 | L512–513 |
| MCP `context_query` / `context_node` | n/a | Partial: `degraded` in JSON; Sindri not running or MCP error mapping **GAP** | **GAP** | L313 (same JSON plus a text rendering) |
| Workflow block `context.retrieve` (Phase 10) | n/a | **GAP** (step behavior when sources are degraded) | **GAP** | L541–542 |

### Accessibility sub-table
| Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
|---|---|---|---|---|
| CLI text output (query, show, sources) | n/a (non-interactive) | Plain text with `[Cn]` markers and a sources table (L303); table wrapping **GAP** | Color use unspecified; require status as text and `NO_COLOR` **GAP** | n/a |
| CLI `map review` | **GAP** (interactive mode unspecified) | **GAP** | **GAP** | **GAP** (where the cursor sits after accept or reject in a batch) |
| MCP text rendering | n/a | Same as CLI text | n/a | n/a |

## Verdict
Three dimensions sit at 3/5. Each has a non-structural HIGH: per-item provenance and `affects` are missing from the
result schema, `context query` has no exit-code or empty-result contract, and the map review interaction is
unspecified. Each is fixed by adding one subsection to §8.4 or a new §8.6. No section needs rework.
verdict: NEEDS_WORK
