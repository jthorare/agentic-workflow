# Product Review: Sindri — factory addendum (§17), revision 3

_Reviewed by `/productReview` on 2026-10-09 | Mode: growth | Round 2_

Plan: `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (revision 3: §17.19 resolutions, §17.20
parallelism P1–P4, §17.21 multiplayer). Read against the main spec (`2026-10-07-sindri-design.md` §2, §10.1, §13,
§15) and the readiness design (`2026-10-09-agent-readiness-design.md` §4b, §10). Round-1 inputs:
`round1/consolidated-review.md` (C1–C17) and `round1/product-review.md`.

Out of scope, because they're decided: the C#/.NET port, stacked epics, the swappable host, ring 1 before ring 2,
and "Sindri grows Sindri". The addendum is kept generic on purpose (public repo), so missing workplace specifics
are not counted as gaps.

"Growth" means the same thing as in round 1: more of the builder's real work going through the factory, then
ring 1, then ring 2, then a teammate.

## Verdict: ITERATE

One HIGH concern drives this verdict. The micro-packet resolution for C14 (#1) creates a hole in the C1
capability fix. It freezes a packet "without a desk step", using a write scope that comes from triage, which is a
model. That breaks invariant 13 ("freezing is a desk action"; "no model output may set capability fields") for the
work class that makes up most ring-1 traffic. It also conflicts with ring 1's human-gated policy.

Most round-1 findings are resolved for real. Five are only partly resolved: C10 (#2), C13 (#5), C15 (#6), and C3
and C6 (#4, where the resolution is in §17.19 but the old text in earlier sections still says the opposite).

The new sections bring new MEDIUM risks:
- P3 federation starts with ring 1 but has no ring-1 row and no rule for which way data may flow (#3).
- The §17.21 multiplayer seams quietly add scope to the port's kernel (#7).
- The first factory-built PR still waits until after the whole port (#8).

None of these call for rethinking the design.

## Round-1 resolution check

| Id | Status | Notes |
|---|---|---|
| C1 ceiling | **Partly resolved** | The ceiling (profile ∩ lens ∩ task) and the split between prompt text and capability fields are right. Micro-packets get round it (#1) |
| C2 protection tiers | Resolved | Imported packs are prompt text only, and upgrades never self-adopt. §17.6's "the phone inbox approves proposed rules" is narrowed by §17.12 to prompt-text rules. Fine, but see #4 |
| C3 enforceability | Resolved in §17.19 | §17.5's table still lists the gate as the enforcer for commands (#4) |
| C4 holdouts | Resolved | Kept in sindri-private state, with a canary probe |
| C5 phase-2 checks | Resolved | §17.6 still says "enforced at commit and by the sindri-side verifier" without naming phase 2 (#4) |
| C6 lease and host tags | Resolved in §17.19 | §17.13 still says "fenced epoch bump" with per-host semantics (#4). Where the ring-1 lease lives isn't stated: no personal box, and the work laptop sleeps (#2) |
| C7 graph boundaries | Resolved | Ports, the shim, a single writer, lowest-trust consolidation, and evals that stay in Sindri |
| C8 phone binding | Resolved | Enrollment, revocation, signed (item, version, nonce), a separate walkthrough origin, and the Plan 6 bind amendment. §17.12's signed-record field list still has no version or nonce (#4) |
| C9 epic gate | Resolved | `--match-head-commit`, human or trusted-bot threads only, an explicit undraft entry, and ring 1 off by default |
| C10 ring-1 fit | **Partly resolved** | The table exists and "holds" is limited to its rows. Some rows are missing and "holds" has no metric (#2) |
| C11 phone states | Resolved (spec deferred to the Plan 6 amendment) | States, buttons, 10 s undo, captions and the queue mapping are all there |
| C12 devex commands | Resolved | `packet init/amend`, `--json`, `session spec`, `why-denied`, `SINDRI_MODE` and the skill lint |
| C13 thin slice and sizing | **Partly resolved** | F0.5 and a 4/4/3 starting catalog were added. F-steps are still unsized, with no exit bars (#5) |
| C14 micro-packets | **Resolved, with a defect** | See #1 |
| C15 human effort | **Partly resolved** | There's a ceiling but no comparison against the baseline, and the per-surface budget has no defaults (#6) |
| C16 schemas and glossary | Resolved | |
| C17 handover dry-run/back | Resolved | |
| Round-1 product #6 (second user) | Resolved, more than asked | §17.20 P4 and §17.21 now spec team mode in depth (see #7) |
| Round-1 product #7 (cold start) | **Unresolved** | #9 |
| Round-1 product #8 (F-step stop rules) | **Unresolved** | Folded into #5 |

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | (a) **Compounding taste and precedent** (§17.6) is still the strongest lever, and it's built in. (b) **Ring expansion** is now partly built in: the ring-1 table, and P3 federation as the bridge between home and work. But P3 has no rule for which way data flows and no ring-1 row (#3), and the ring-2 trigger still has no metric (#2). (c) **Scouting** (§17.18) is a new built-in lever that pulls capabilities in from outside. It's well guarded (5 cards a week, and the cadence drops if precision is low) |
| Activation funnel | For small work: tracker item → triage → micro-packet → freeze → build → PR. This removes round 1's biggest drop-off, but the freeze step breaks invariant 13 (#1). For the factory as a whole, time to first value is R0–R3 (about 10–16 weeks of porting, by the readiness doc's own sizes) → F0 → F0.5. The first factory-built PR lands about a quarter out, with no factory usage signal in between (#8) |
| 10x usage | Much better than round 1. P1 adds a scheduler, WIP limits, resource classes and a capacity model capped by human minutes, and the plan names human review bandwidth as the real ceiling. Risk: P1 joins F2, which is unsized and already the largest F-step (#5). New repos still have no cold-start default (#9) |
| Retention hooks | Cadences are strong: the digest, the inbox, the babysitter, weekly scouting and evolve. A per-surface notification budget exists, but it has no defaults and isn't reconciled with §10.1's `maxInterruptsPerHour` or "≤ 2 per merged PR" (#6). With phone decisions off on ring 1, every ring-1 decision lands at the desk, so the desk-queue size is ring 1's retention risk. The digest needs a "desk queue age" line |
| Viral coefficient | Now spelled out. P3 published packs carry improvements between instances, P4 team mode serves a team, and ring-1 PRs carry walkthroughs teammates see. The P3 channel between home and work is also the riskiest for the ring-1 relationship (#3) |

## Strengths
1. **The resolutions are real, not cosmetic.** Most C-items got a mechanism rather than a sentence: the capability
   ceiling as an intersection, signed (item, version, nonce) decisions, a shared lease checked before every outward
   action, lowest-trust consolidation, and a split table of patterns against boundaries. That table in §17.19 is the
   most honest part of the document.
2. **The prompt-text vs capability-field split reconciles the core tenet with safety.** Sindri can still grow
   everything (invariant 16), and only capability changes need a human. This is the right shape for "Sindri grows
   Sindri".
3. **§17.20 names the true ceilings.** Model quota and human review bandwidth are the limits, and the capacity
   model ties the session cap to human minutes. That stops parallelism from outrunning the human, which is the
   failure mode that most often kills agent factories.
4. **The ring-1 table sets defaults well.** Everything autonomous is off until the workplace allows it, and "holds"
   is measured only over rows that are on. That turns an open policy risk into named, testable settings.
5. **F0.5 and the 4/4/3 starting catalog** directly fix round 1's breadth concern, and scouting is now the stated
   way the rest gets added.

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | HIGH | **Micro-packets break the C1 fix.** §17.19 says an S-or-smaller item gets an auto-generated packet "frozen without a desk step", with "write scope from triage". The triager is a model-backed standing workflow (§17.9). Write scope is a capability field, and §17.19 says no model output may set capability fields. Invariant 13 and §17.12 also say freezing is a desk action. So the most common work class (round 1 found it to be most ring-1 traffic) skips the ceiling the rest of the document builds. On ring 1 it also conflicts with the human-gated policy: an auto-frozen packet plus auto-start means no human gate before the build | Make the micro-packet write scope **deterministic**: paths the tracker item or stack trace names explicitly, plus their tests, intersected with the profile ceiling, and capped by file count. The triager may *suggest* paths, which then show up as a scope-expansion request. Freezing becomes a **one-tap freeze** decision: a desk item, or a phone item where phone decisions are on. Alternatively, write an explicit, bounded exception into invariant 13. Add a ring-1 row: "micro-packet auto-freeze: Off" |
| 2 | MEDIUM | **The ring-1 table is missing rows, and "holds" has no metric.** Missing rows: containers and Docker on the work host; auto-start or auto-small; micro-packet auto-freeze (#1); P3 federation (#3); workplace connectors in the context graph; scouting over ring-1 gaps; lean bootstrap on host sessions. Where the lease lives for ring 1 isn't stated (work laptop only, which sleeps). "F3 holds on ring 1" is limited to rows that are on but still has no threshold, so the ring-2 trigger is a judgment call, and with most rows off the bar could be trivially easy | Add the missing rows. State that ring 1 runs as **its own instance on the work laptop** and holds its own lease, which is P3's shape from day one. Define "holds" as numbers, for example N micro- or full packets merged, a build-side turns reduction against the ring-1 baseline, and zero out-of-scope writes reaching a PR, over at least 4 weeks |
| 3 | MEDIUM | **P3 federation has no rule for data direction and no ring-1 row.** P3 "starts when ring 1 begins" and shares published packs and idea cards "between home and work". Rules, product precedents and lens wording distilled at work can carry workplace knowledge even as prompt text: precedents are literally how the human answered workplace product questions. That's the kind of flow a human-gated workplace policy may forbid, and it could put ring 1 at risk | Default to **home → work only**. Packs published from a ring-1 instance need an explicit workplace allowance (a ring-1 table row), a scrub pass, and a kind allowlist that never includes `product-precedent` or packet-layer rules. Record each crossing in the audit stream |
| 4 | MEDIUM | **"§17.19 wins" leaves contradictory text in place.** Several earlier sections still say what §17.19 overrides: §17.5's enforcement table (commands enforced by the gate), §17.6 (rule checks "by the sindri-side verifier", no phase 2), §17.12 (the signed record has no item version or nonce; "packet freezes stay at the desk" vs micro-packets), §17.13 (a per-host "fenced epoch bump"), and invariant 13 vs micro-packets. §17.17 sits after §17.21. For a second reader (a teammate, or an agent building from this doc) this is an onboarding cost and a source of wrong builds, since the factory's own packets will cite these sections | Fold each §17.19 resolution into its home section, and shrink §17.19 to a changelog that maps each C-id to the section it changed. Renumber so §17.17 Decisions comes last, or move it into §17.0 |
| 5 | MEDIUM | **F-steps are still unsized and have no exit bars** (round-1 #2 and #8). F2 now also takes on P1 (scheduler, resource classes, capacity model) and is the largest step. The readiness doc sizes R0–R3 and has stop rules, while R4 is just "with the addendum" | Size F0–F7 the way main-spec §13.1 does. Give each an exit metric and a revert path. Add the readiness stop rules (2× per packet, 1.5× per step). Consider splitting F2 into F2a (task compiler and handoff, serial) and F2b (P1 scheduler) |
| 6 | MEDIUM | **The human-effort guardrail is a ceiling, not a net-benefit test.** A weekly human-minutes ceiling stops overload, but it can't tell whether the factory saves time: desk minutes could rise by more than build turns fall while staying under the ceiling. How minutes are measured (transcript timestamps? dashboard dwell?) isn't defined. The per-surface notification budget has no defaults and isn't reconciled with §10.1 (`maxInterruptsPerHour` 6, ≤ 2 interrupts per merged PR) | Add **human minutes per merged deliverable against the step-0 baseline**, with "no worse than baseline after 4 weeks of factory builds" as the F2+ rollback criterion. Define the measurement source. Give each surface a default budget and say how it composes with §10.1 (for example phone pushes count toward `maxInterruptsPerHour`) |
| 7 | MEDIUM | **The multiplayer seams grow the port's kernel without a size.** The readiness doc puts the event envelope, a transactional outbox "for every state change and every outward action", the `Bus` and `Secrets` ports and OTel propagation into `Aw.Kernel` from the first ported package. That's real behavior change (outward actions go through a dispatcher) inside a port governed by a byte-compat golden harness, and R1b and R2 weren't resized. P4 demand is unproven: it "starts only when a team adopts it" | Keep the truly cheap seams: the envelope fields, the `LedgerStore` and `Lease` ports, and the tenant and ring keys. Put the outbox dispatcher and the `Bus` behind a flag and build them in **F2**, not during the port, so golden-harness parity isn't fighting a semantic change. Add a line to the cutover contract that says which seams may change observable behavior |
| 8 | MEDIUM | **The first factory PR is about a quarter away.** F0 starts only after readiness Level 4 (R0–R3, which the readiness doc estimates at roughly 10–16 weeks). Until then nothing exercises the pipeline, though the core tenet says Sindri should build Sindri. Meanwhile R1a already builds the C# packet schema with `validate` and `freeze`, and R2 runs the ports as packets | **Dogfood F0.5 on R2.** Once R1a's packet schema exists, run one later port packet (for example `Scorer`) through the thin slice: micro- or full packet → one task → `/specToProvenPR` in sindri mode → PR. The port is decided either way, and this gets a first factory PR and the first packet-miss data weeks earlier |
| 9 | LOW | **Cold start for new repos is still undefined** (round-1 #7). Context-graph arms, lens triggers and rule selection are gated by backtests, and a freshly onboarded repo has no corpus | State the per-component cold-start defaults (code index plus tracker only, deterministic lens triggers, model pass off) and the corpus size before each arm is evaluated |
| 10 | LOW | **There are many swipe-inbox feeders and no ordering rule.** Idea cards (≤ 5 a week), prompt-text rules, evidence accepts, scope keep-or-drop and undrafts share one inbox with no priority | Order the inbox by what blocks work: undrafts and evidence first, then rules, then idea cards. Let the digest absorb anything older than N days that isn't blocking |

## Scope Suggestions

### Cut
- **The §17.21 build items that need P4** (roles, collaboration surfaces, hash-chained audit, SLOs, per-tenant
  vaults) as near-term work. Keep the table as the target, and treat everything past the cheap seams as P4 backlog
  (#7).
- **The outbox dispatcher and `Bus` during the port.** Move them to F2 (#7).

### Keep
- The capability ceiling and the prompt-text vs capability-field split.
- The pattern vs boundary table, which should become §17.5's actual table.
- The ring-1 default-off table and "holds" limited to its on rows.
- F0.5 and the 4/4/3 starting catalog.
- §17.20's capacity model capped by human minutes, and conflict-aware dispatch.
- The scouting guardrails (cards per week, precision-driven cadence, consent for likeness).

### Add
- A deterministic micro-packet write scope and a one-tap freeze (#1).
- The missing ring-1 rows, the ring-1 instance and lease placement, and a numeric "holds" bar (#2).
- A one-way-by-default rule for P3 and a pack kind allowlist (#3).
- F-step sizes, exit metrics and stop rules (#5).
- Human minutes per merged deliverable against the baseline (#6).
- F0.5 dogfooded on an R2 port packet (#8).

## Key Questions for the Team
1. Does the workplace policy allow an agent build to start on a ticket with no human action in between? If not,
   micro-packets on ring 1 need the one-tap freeze, and auto-small doesn't apply there.
2. May anything learned at work (rule wording, lens prompts, precedents) flow to the personal instance? If the
   answer is unclear, P3 should be home → work only.
3. Which R2 port packet is small and self-contained enough to be the F0.5 dogfood run?
4. What numbers would convince you that F3 "holds" on ring 1: merged count, turns saved, weeks, zero scope escapes?
5. Is P4 a goal within two quarters? If not, which §17.21 seams are really worth carrying through the port now?

## Recommended Next Action
Rewrite the micro-packet rule in §17.19 so that write scope is computed deterministically and freezing is a
one-tap human decision. Add the matching ring-1 row ("micro-packet auto-freeze: Off"). Then fold every §17.19
resolution back into its home section so the earlier text stops contradicting it.

verdict: NEEDS_WORK
