# Consolidated Plan Review — 2026-10-10-sindri-context-retrieval

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (first draft, commit d12526b)
**Reviewed:** 2026-10-10
**Lenses applied:** product · arch · design · devex · security

## Normalized findings

HIGH rows only; MEDIUM and LOW rows are listed with their resolutions in the spec's §14.

| id | severity | lens | location | finding | recommended fix |
|---|---|---|---|---|---|
| A-F1 | HIGH | arch | §4, §8.5, §11 | `compose` edge (Add… only) can't carry endpoints/MCP that bind Features types; Sindri fan-out at 4/4 | Core facade port + narrow compose for registration + recorded fan-out exception |
| A-F2 | HIGH | arch | §6.1 | `affects` can't reach its headline example at depth 2; `on` missing; sibling over-expansion | Typed steps, state hop = 1, visited set, fixtures |
| A-F3 / P-1 | HIGH | arch, product | §9, Phases 4/7/8/9 | Eval truth needs `implements`, which only arrives in Phase 9 | File-level `implements` from the Phase 4 extractor |
| A-F4 | HIGH | arch | §9.1, Phase 3 | Graph arm leaks future knowledge (today's map and overrides) | Per-sample as-of maps; overrides dated before the cut |
| A-F5 | HIGH | arch | §5.1, §6.1 | No canonical record identity across Onyx and native Linear | Canonical `RecordKey`, dedup before fusion |
| S-H1 | HIGH | security | §8.5, §3 | User-scope MCP registration exposes work context to personal sessions (Prism) | Per-repo `--scope local`; doctor check |
| S-H2 | HIGH | security | §8.5 | Routes don't state Host/Origin checks (DNS rebinding, cross-site POST) | Inherit S1 LoopbackGuard; tests |
| S-H3 | HIGH | security | §7 | Model-written text from untrusted sources becomes trusted | Trust by provenance until human acceptance |
| S-H4 | HIGH | security | §5.2 | Onyx exposure (bind, cloud providers, telemetry) outside Sindri's boundary | Onyx setup doc + doctor `context:onyx` |
| D-1 | HIGH | devex | Phases | No way to run C# sindri before the switch (TS wrapper on PATH) | `$SINDRI` defined in docs; signals use it |
| D-2 | HIGH | devex | §3, §6.2 | State under TS `$AW_STATE_DIR/sindri/` | `$AW_SINDRI_HOME/context/` |
| D-3 | HIGH | devex | §8 | No error catalogue, codes off S1 convention | `SND-CONTEXT-*` table, exit codes |
| D-4 | HIGH | devex | §8.5 | Graph keys (with `/`, `#`) in URL paths | Keys in query string/body |
| D-5 | HIGH | devex | §8 | No profile `context:` example or defaults | §8.8 example |
| D-6 | HIGH | devex | §5.2 | No Onyx or Linear setup guide, unpinned Onyx | §8.8 + `docs/sindri/onyx.md` |

## Top tensions (cross-lens)

1. **Usable early vs. safe early** — product (P-2, value from Linear docs sooner) vs security (S-H1/S-H4, egress and exposure). Resolution: Linear moves before Onyx (D17), and Phase 2 (extra scrub patterns) still lands before any intent source; MCP is per-repo and gated by `context.mcp.enabled`.
2. **Measurement early vs. phase scope** — product (P-1/P-3) vs arch (A-F3/A-F4). Resolution: file-level `implements` in Phase 4 and the probe runner in Phase 3 give measurement without pulling the code block forward; per-sample as-of maps keep it honest.
3. **Rule purity vs. composition** — arch (A-F1) vs devex (D-1, a runnable Phase 1). Resolution: a Core facade keeps ARCH-DIR-004 intact; only registration needs the new `compose` edge.
4. **Bulk review speed vs. human-only decisions** — product (P-6) vs security (S-M5). Resolution: bulk accept limited to deterministic proposals, review only at an interactive terminal.
5. No tension between design and devex: both asked for exit codes, error codes and text-output examples; one fix serves both.

## Per-lens highlights

### Product
- Top wins: the product graph as core; D-series reasons; issue-level backtest.
- Top gaps: eval uncomputable before Phase 9; ring-1 value late; probes late; small-corpus gate.
- File: `plans/2026-10-10-sindri-context-retrieval/product-review.md`
### Architecture
- Top wins: read-only ports, live fetch, query-time cascade.
- Top gaps: composition rule, cascade definition, eval leakage, record identity, override durability.
- File: `plans/2026-10-10-sindri-context-retrieval/arch-review.md`
### Design
- Top wins: section-ordered packing, citations with versions.
- Top gaps: exit codes and empty states, map review flow, `affects`/provenance in items, conflicts inside the budget.
- File: `plans/2026-10-10-sindri-context-retrieval/design-review.md`
### Devex
- Top wins: phase signals, contract tests.
- Top gaps: running the C# build pre-switch, state dir, error catalogue, profile example, Onyx/Linear setup.
- File: `plans/2026-10-10-sindri-context-retrieval/devex-review.md`
### Security
- Top wins: single ingest boundary, egress re-scrub, query log without text.
- Top gaps: MCP scope, API guard, trust of model output, Onyx exposure.
- File: `plans/2026-10-10-sindri-context-retrieval/security-review.md`

## Recommended next actions
1. Settle composition with a Core facade + narrow `compose` edge — unblocks Phase 1 and every later block — arch.
2. File-level `implements` in Phase 4 and per-sample as-of maps — makes the eval gates real — arch, product.
3. Per-repo MCP registration, inherited LoopbackGuard, trust by provenance — keeps work text out of personal sessions and injection out of Sindri's voice — security.

All three were applied in the revised spec (§14).

## Lens status
| Lens | File | Status |
|---|---|---|
| Product | product-review.md | ✓ (verdict: NEEDS_WORK) |
| Architecture | arch-review.md | ✓ (verdict: NEEDS_WORK) |
| Design | design-review.md | ✓ (verdict: NEEDS_WORK) |
| Devex | devex-review.md | ✓ (verdict: NEEDS_WORK) |
| Security | security-review.md | ✓ (verdict: NEEDS_WORK) |

## Overall verdict
The first draft had no CRITICAL and fifteen HIGH findings (A-F1–F5, S-H1–H4, D-1–D-6), so it rates NEEDS_WORK; every HIGH was fixed in the revised spec, which was not re-reviewed.
verdict: NEEDS_WORK
