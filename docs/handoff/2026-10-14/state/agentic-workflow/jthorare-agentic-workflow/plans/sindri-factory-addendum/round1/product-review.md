# Product Review: Sindri — factory addendum (§17)

_Reviewed by `/productReview` on 2026-10-09 | Mode: growth_

Plan: `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (revision 2), read against
`2026-10-07-sindri-design.md` §1–4, §13 and §15. "Users" here means the builder first (ring 0), then the
builder at work (ring 1), then a private product (ring 2), then a teammate (§15). "Growth" means more of the
builder's real work going through the factory, more repos, and eventually a second user.

## Verdict: ITERATE

One HIGH concern drives this: the first real users (ring 1) run under a human-gated `assist` policy on work
accounts, but most of the addendum's value (automatic handoff, `epicMerge: auto`, phone approvals over a
personal tailnet, an always-on Linux box, `act`-tier standing workflows) assumes autonomy or infrastructure that
policy may not allow (#1). Ring 1 is named as the place where the factory is "tested and improved first", so
the adoption funnel can stall right where the plan needs its signal. The MEDIUM concerns (#2–#6) are rollout
and guardrail gaps, not design flaws: no thin end-to-end slice or effort sizing, no light path for small work,
no total-human-effort guardrail now that the headline metric is gone, an unrevised notification budget, and a
second-user path that the addendum doesn't touch. The design itself (frozen packets, compiled sandbox,
deterministic injection, packet-miss feedback) is sound, and nothing here calls for rethinking it.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Three levers. (a) **Compounding taste and precedent**: the rule library (§17.6), packet misses (§17.2) and product precedents make each deliverable cheaper to specify than the last. This is built in and is the strongest lever. (b) **Ring expansion** (ring 0 → 1 → 2 → teammate): this is bolted on. The ring-1 policy fit is unexamined (#1) and the second-user path isn't updated (#6). (c) **Standing workflows** (§17.9): they pull work in without dispatch, which is built in, but they ship at `report` tier only in F4 |
| Activation funnel | For each deliverable: scope → desk studio → decision log → `packet validate` → workflow generation → `freeze` → build → review → epic merge. For a repo: `repo onboard` → profile → index → lenses. **Biggest drop-off:** the packet ceremony. Every build needs a frozen packet (invariant 14), and the addendum gives no light path for an XS bug or a one-file ticket, which is most ring-1 traffic (#3). Second biggest: ring-1 policy blocking the autonomous half (#1). Time to first factory-built PR is F0+F1+F2 after Plan 6, and none of these steps is sized (#2) |
| 10x usage | 10x deliverables moves the bottleneck to the desk, by design (product turns rise), and to the phone inbox. Infra blockers: one heavy job per host (invariant 5); one active host (F7 makes it swappable, not parallel); quota routing that keeps `reserveForHuman`. Workflow generation runs once per deliverable, which scales well. 10x repos: per-repo context-graph backtest arms and lens triggers each need calibration data, and the plan doesn't say how a new repo becomes trustworthy without its own backtest corpus (#7) |
| Retention hooks | Strong, natural cadences: the daily digest, the swipe inbox (per PR), the PR babysitter (per CI event), weekly evolve and cleanup, and nightly inspector runs. Risk: 11 standing workflows plus phone decisions plus rule approvals can blow the §10.1 notification budget (≤ 2 interrupting per merged PR), which the addendum doesn't revise (#5). Without that, the inbox turns into noise and gets ignored |
| Viral coefficient | Low by nature (a personal toolkit, a public repo kept generic). The real exposure surface is **ring-1 PRs that carry walkthroughs and evidence roll-ups** that teammates see, which is a good organic hook. The context graph as a "separable package" (§17.3) is the only explicit reuse path. §15's second-user model (profile init, namespaced claims) isn't extended to packets, rule layers, lenses or the phone surface (#6) |

## Strengths
1. **The packet-miss loop turns friction into a growth signal.** A mid-build product question becomes a scoping-backtest
   and evolve input (§17.2), so the funnel's main failure mode feeds the system that improves it.
2. **Metrics now match the job.** Splitting build-side turns (→ 0) from desk turns (expected to rise) is honest about
   where a product SME's time should go, and stops the factory from being "optimized" into answering product
   questions (invariant 12).
3. **"Measure, then keep" adapter discipline.** Each context-graph component earns its place per repo through a
   backtest arm (§17.3, Q2). That keeps the stack from growing on vibes and makes swaps cheap as the ecosystem moves.
4. **Lean bootstrap (§17.8) is a direct, measurable win** that also helps the builder's own desk sessions through the
   opt-in `lean` profile. That makes it the most immediately felt improvement in the addendum.
5. **The review surface choice is right-sized.** The PWA on the existing dashboard comes first, with native only if
   decision latency proves it's needed (§17.17 Q4).

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | HIGH | **The ring-1 policy fit is unexamined.** Ring 1 is human-gated `assist` on work accounts (§17.17 Q5), yet F2–F7 lean on things that policy may forbid or that need sign-off: `epicMerge: auto` (an agent merging into a shared-remote branch), signed phone decisions over a personal tailnet, workplace code on a personal always-on Linux box (§17.13), the PR babysitter at `act` tier, and unattended subscription use (§17.15). If ring 1 can't run these, the factory gets tested only on ring 0, and "ring 2 starts after F3 holds on ring 1" never triggers | Add a **ring-1 compatibility table**: one row per F-step and standing workflow, with columns for allowed under the current policy, allowed with a named setting (for example `epicMerge: manual`, `act` → `propose`, laptop-only host, work tailnet or none), and blocked. Define the ring-1 exit bar for F3 using only the allowed column. Settle the factory host for ring 1 explicitly (work laptop only?) before F7 |
| 2 | MEDIUM | **There's no thin vertical slice and no sizing.** F0's "first value" is schemas that can be checked, which isn't user value. The first factory-built PR needs F0+F1+F2, all after Plan 6, and unlike main-spec §13.1 there are no effort sizes. The lens catalog (13), evidence kinds (15) and standing workflows (11) are all listed as initial | Add an **F0.5 slice**: one hand-written packet → one task → `/specToProvenPR` in sindri mode → one PR, with the `test` provider only and the implementer and QA lenses only. Size each F-step the way §13.1 does. Shrink the "initial" catalogs to what F1–F3 actually exercise (about 4 lenses, 4 evidence kinds, 3 standing workflows) and mark the rest as backlog |
| 3 | MEDIUM | **There's no light path for small work.** Invariant 14 requires a frozen packet for every build Step. Most ring-1 tickets are XS/S bugs, and the main spec's auto-small mode targets exactly those. Desk ceremony for a one-line fix will push the builder back to manual sessions, which is the biggest activation drop-off | Define a **micro-packet**: auto-generated from a tracker item plus the triage size class, frozen without a desk session when the size ≤ S and there are no open product questions, using a single-Step workflow template. Say whether the triager standing workflow (§17.9) can produce it at `propose` tier |
| 4 | MEDIUM | **Total human effort has no guardrail.** Dropping "human turns per merged PR" and the −20% kill condition leaves no ceiling on desk and phone effort. If desk turns plus phone decisions grow faster than build turns shrink, the factory is a net loss and no metric says so | Keep one composite measure, **human minutes (or turns) per merged deliverable**, across desk, phone and build, against the step-0 baseline. Make "no worse than baseline after 4 weeks of factory builds" a kill or rollback criterion for F2+ |
| 5 | MEDIUM | **The notification budget isn't revised.** §10.1 caps interrupts at ≤ 2 per merged PR. The addendum adds a swipe inbox, rule and evolve approvals, scope keep/drop, tier promotions and 11 standing workflows, with no batching or budget rules for them | Extend §10.1: phone decisions are pull (the inbox) by default, not push. Push only for blocking parks. Each standing workflow gets a per-day interrupt budget, and the digest absorbs the rest |
| 6 | MEDIUM | **The second user isn't carried forward.** §15 covers profile init and namespaced claims only. Packets, rule layers (whose "personal taste" layer is public), lens overrides, phone enrollment and quota accounts are all single-human. That limits ring 2 and any teammate adoption, which is the only real viral path | Add a short §17.x "Second user" section: per-user rule layer, per-user passkey enrollment, packets owned by a named SME, quota accounts per user. Even "out of scope until ring 2" is fine if stated |
| 7 | LOW | **Cold start for new repos.** Context-graph arms, lens triggers and rule selection are each gated by backtests and measured precision. A freshly onboarded ring-1 repo has no corpus, so the order in which components and lenses come on for it is undefined | State the cold-start default for each component (code index + tracker only; lenses fixed by deterministic triggers; model pass off) and how many deliverables it takes to collect a corpus |
| 8 | LOW | **There's no stop condition for F-steps.** F-steps have "first value" but no exit bar or kill criterion, unlike §2 and §13.2 | Give each F-step an exit metric and a revert path, for example F1: first-call tokens ≤ budget on 90% of sessions |

## Scope Suggestions

### Cut
- **Most of the initial catalogs for now.** Keep about 4 lenses (product/acceptance, implementer-TS, QA, architecture),
  4 evidence kinds (`test`, `cli-transcript`, `api-transcript`, `ui-browser`) and 3 standing workflows (PR
  babysitter, triager, daily digest). Everything else becomes a registered-artifact backlog that evolve can propose.
- **The `narrative` evidence kind and the incident comms manager**: non-gating and far from the core loop.
- **§17.15 cross-account handoff** until a measured rate-limit park rate justifies it. Waiting for the window is fine
  at ring-1 volume.

### Keep
- Frozen packets plus deterministic validate and freeze. This is the backbone of trust in the funnel.
- Lean session bootstrap (F1). It's the fastest felt value and is measurable.
- Rule and taste growth plus product precedents. This is the compounding lever.
- Backtest-gated context-graph components. This is the discipline that keeps F5 honest.
- Stacked epics with the human owning epic → default.

### Add
- A ring-1 compatibility table (#1).
- An F0.5 vertical slice and per-step sizing (#2).
- A micro-packet path for XS/S work (#3).
- A total-human-effort guardrail metric (#4).
- A notification budget for the phone and standing workflows (#5).

## Key Questions for the Team
1. Under the workplace agent policy, may an agent merge into a non-default branch on the shared remote, and may
   workplace code or decisions travel over a personal tailnet or sit on a personal Linux box? If not, what is the
   ring-1 shape of F2, F4 and F7?
2. What share of ring-1 work in the last 60 days was XS/S tickets that would never justify a desk session? (The
   step-0 XS-share measurement can answer this.) That number decides how much the micro-packet path matters.
3. What's the smallest deliverable you'd trust the factory to build end to end first, and which 3–4 lenses does it
   actually need?
4. Is a teammate or a ring-2 collaborator a goal within the next two quarters, or is the factory single-user by
   design? The answer changes how much of #6 to spec now.

## Recommended Next Action
Before F0 starts, add the **ring-1 compatibility table** (per F-step and standing workflow: allowed, allowed with
setting, or blocked under the human-gated policy) and redefine "F3 holds on ring 1" using only allowed rows. Then
add an F0.5 thin slice (one packet → one task → one PR via `/specToProvenPR` in sindri mode) with an effort size.

verdict: NEEDS_WORK
