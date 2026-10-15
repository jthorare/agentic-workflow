# Product Review: Sindri context retrieval engine (round 5, final)

_Reviewed by `/productReview` on 2026-10-10 | Mode: growth | slim_

Plan: `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (current revision, §14 through "Round 4").
I checked that the round-4 product MEDIUMs landed, then read the round-4 edits for added owner burden and scope creep.
I didn't reopen the owner-settled decisions: Onyx CE, the product graph as the core, MCP in Phase 1, ring-1 work
product, the issue-level backtest, parallel agent-team delivery, and the accepted LOW on sizing owner time.

## Verdict: SHIP

There are no CRITICAL or HIGH concerns. All six round-4 product findings were resolved or explicitly accepted (table
below). The round-4 edits added one real gap and two small ones:

- #1 (MEDIUM): the new map-version lifecycle lets the scheduled rebuild leave sessions on an aging `current` map until
  the owner promotes it. Phase 4 also still says "draft then flip".
- #2 (LOW): the fixed owner-check outline is right, but it adds about seven manual steps to each of about 10 checks.
- #3 (LOW): the Onyx-arm rerun and the held snippet have no named step that carries them out.

Each fix is one sentence or one small script. None changes the architecture, the stage graph or the phase order.

## Round-4 product findings: verified

| R4 | Status | Where |
|---|---|---|
| #1 `map export` | Resolved | §8.5 CLI `map … export <file>` and HTTP `GET …/map/export`; Phase 3 `ExportMap` keeps original timestamps and provenance and has a round-trip test; owner-check outline step 4 seeds the home from an export, never a copy of `context.db` |
| #2 S-6 needs S-5B | Resolved (the alias alternative) | §5.2 `linear` row: "Writes identifier → UUID aliases on every fetch" (S-5A), so S-6 dedups without sync; the S-6 row stays S-3 · S-5A · S-FX. The S-5A row and Phase 5 Files don't name the alias write (see #3) |
| #3 graph gated off | Resolved | §9.3: "The product graph itself is never gated off"; its arm is a floor; a `graph+reviewed` arm, labeled leaky, shows the ceiling |
| #4 snippet before useful results | Resolved in substance | §8.5: the snippet goes in by hand once S-1M's owner check shows useful results, otherwise it waits for S-5B. There's no count threshold and no P-L1 usage line. That's acceptable for a single owner (see #3) |
| #5 owner effort unsized | Accepted as LOW (§14 Round 4) | Not reopened. #2 covers what round 4 added |
| #6 Onyx arms after S-7B | Resolved | §9.3: "The `onyx` arms rerun once S-6 merges, without blocking S-7B" |

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Unchanged and sound: MCP plus the agent-rules snippet (now held until results are useful), `item` at S-5B, `context.retrieve` at Phase 10. The new lifecycle can quietly weaken the main lever, the graph's freshness, between reviews (#1) |
| Activation funnel | Better than round 4. The snippet waits for useful results, so the first habit-forming answers aren't empty. The owner-check home is now well defined. The remaining friction is the owner's per-check setup (#2) and who triggers the deferred snippet (#3) |
| 10x usage | Holds. The query path is local, there's no model call by default, and server-side caps apply. Lifecycle reads hit `current` only, so 10x queries don't touch drafts. At 10x rebuilds, unreviewed drafts pile up, and `retained`/purge covers storage but not freshness (#1) |
| Retention hooks | The 7-day MCP count in `sources status` and the probes that don't regress are still the hooks. A stale `current` map is the main retention risk: sessions get answers that no longer match the code, and nothing tells the owner (#1) |
| Viral coefficient | N/A: a single-user toolkit. A second repo spreads through `context.repos` and per-repo registration. Not raised again |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | MEDIUM | **The scheduled rebuild can leave sessions on a stale map.** §8.7 now says `map build` writes a `draft`, queries read `current`, and only `map promote` or "emptying the review queue" makes a draft current. Phase 4 adds "a scheduled rebuild". So a rebuild after code moves (renamed files, new settings keys) produces a draft that sessions never see until the owner sits down to review. If the owner reviews weekly or less, `implements` links, `affects` paths and `--symbol` lookups drift from the code, and nothing reports it. It's also unclear whether a rebuild whose queue is already empty (every proposal settled by existing overrides) counts as "emptying" and becomes current by itself. Phase 4 Files still says "`BuildMap` (batched, draft then flip)", and §10 tests "batched build and flip", which reads like an automatic flip and contradicts §8.7 | In §8.7, say that a build whose review queue is empty after overrides becomes `current` automatically (deterministic changes to already-accepted nodes included, if that's the intent). Otherwise the draft waits, and doctor `context:map` warns with the draft's age and how many commits `current` is behind; `sources status` shows the same line. Replace "flip" in Phase 4 and §10 with the lifecycle's terms ("building → draft; promote or empty queue → current") |
| 2 | LOW | **Round 4 added manual steps to every owner check.** The fixed `owner-check.md` outline is the right call for safety: AOT build, a fresh scratch home, port 7480, the profile copied, edited and approved, an optional export and import, MCP add and remove, and teardown. But the owner repeats about seven setup and teardown steps for each of about 10 checks (S-1M, S-2W, S-3, S-4, S-5A, S-5B, S-6, S-CF, S-7B, S-8). Only the profile approval and the checks themselves need the human. The rest is boilerplate, and a missed teardown leaves a `sindri-ownercheck` MCP entry or a stray process on 7480 | Add `scripts/owner-check.sh <stage> up\|down` to S-1C, where `denylist-check.sh` already lands, or to S-1M. `up` builds, creates the fresh home, copies the profile and applies the template edits, optionally exports and imports the map, and registers MCP. Then it stops at the terminal for `profile approve`. `down` tears everything down and is idempotent. `owner-check.md` then lists only the real steps and pass criteria |
| 3 | LOW | **Two deferred actions have no owner.** (a) §9.3 says the `onyx` arms rerun once S-6 merges, but that's work data. No stage's owner check and no item under "Gates that stay sequential" carries the rerun, so the `onyx` layer and the "Onyx `Limit` 20 vs 50" decision may never be recorded. (b) If S-1M's check finds thin results, the held agent-rules snippet has no step at S-5B that adds it. (c) The alias write that S-6's dedup depends on is only in §5.2's table, not in the S-5A row or Phase 5 Files, so the S-5A owner could miss it | Add "rerun the `onyx` arms and record their decisions" to S-6's owner check (or S-8's, if S-7B hasn't merged by then), and list it under "Gates that stay sequential". Add "add the agent-rules snippet if it was held" as a step in S-5B's owner check. Add "identifier → UUID aliases" to the S-5A row and Phase 5 Files, with a contract test |

## Recommended Next Action

Make the three edits: the auto-current rule for empty-queue builds plus the draft-age warning in doctor (and "flip"
removed from Phase 4 and §10); `scripts/owner-check.sh` in S-1C; the Onyx-rerun and snippet steps in the S-6 and S-5B
owner checks, with the alias write named in the S-5A row. Record them under "Round 5" in §14, then hand the spec to
`/specToProvenPR`. No further product review round is needed.

verdict: PASS
