# Product Review: Sindri UI app design (control plane views, actions and launching sessions)

_Reviewed by `/productReview` on 2026-10-11 | Mode: growth_

Plan: `docs/superpowers/specs/2026-10-11-sindri-ui-design.md` (worktree `scratchpad/ui-design`). Checked against the
API surface spec (P10, `NeedsYouRow`), the workflow engine spec (W19 `takeoverIdle`, `notify.desk`), and the dashboard
spec §1 (purpose and success criteria). Layout and attention colour are the owner's calls and are not relitigated.

"Growth" here means **adoption and habit for one engineer, not marketing growth**. Sindri is a single-user,
loopback-only toolkit, so the lens reads as: does the app become the place the engineer goes first (activation), do
they keep coming back without being told (retention), does it hold up when the engineer runs 10x more parallel work, and
does using it pull more of the work, and more of the engineer's agents, into Sindri?

## Verdict: ITERATE

There is one HIGH concern (C1). The headline interaction, one click into an agent (U6), comes last in the phase order.
Edit E-10 makes it wait on the workflow engine's takeover (WF-P5), even though §6.4 itself says that before S2
"nothing pauses; the item just records the launch". API P10 today depends only on P3 + P8. So the feature most likely
to make the app a daily habit comes out after every other phase, and E-10 ties it to a different spec's schedule. The
other concerns are MEDIUM or LOW: they are about activation before the engine lands, Home's layout under 10x load,
friction on protected answers, measuring use, and sessions Sindri didn't start (C2–C8). None of them blocks the design.
The rule-derived verdict is ITERATE.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Three levers are built into the product, not added on: (1) **one-click launch into Warp with the item seeded** (§6): it turns the app from a viewer into how work starts; (2) **the needs-you pulse plus the tray, dock and title badges** (§5): a habit loop that pulls the engineer back; (3) **copy-the-CLI blocks and the `⌘K` palette fed by the catalog** (§7, §8): they teach the CLI and the API. Lever 1 ships last and is gated on another spec (C1). Lever 2 is complete at U3 |
| Activation funnel | Funnel: run `sindri` → the window opens (U0) → the engineer sees something actionable → acts in the app or one click away → comes back via the tray. **Biggest drop-off:** "sees something actionable". Through U0–U2, Home shows Health, Queue and items, but Needs you has no source until P4 (U3), and parks and escalations have none until WF-P4 (U5). An early adopter opens a near-empty window, and the five-second test passes trivially on the fixture ledger, not on their own machine (C2). The second drop-off is the launch step: it comes last (C1) and has the most failure modes (§6.6). They are well handled, but probes only run every 10 min (C7) |
| 10x usage | 10x is about 30 running items and 50 queued under factory workers. Needs you is capped at 6 with "Show all N" and Queued shows the next 5, but **Running and With you has no cap** (§4.2). With 30 running rows the "Health above the fold at 1280 × 800" promise (§3.2) breaks. The five-second test only uses the small fixture ledger (C3). The re-query model (debounced to 250 ms per view, §9.2) scales fine on loopback. Hotkeys and the palette scale well |
| Retention hooks | Strong, with a natural cadence: per park, decision or failure (the pulse until seen, the tray count, the window title), the `takeoverIdle` reminder after 2 h (§6.4.3), and close-to-tray, so the app stays resident (§5). OS notifications belong to `notify.desk` (U8), so nothing fires twice. Gaps: the most frequent park answers (Grant rounds and Grant tokens) leave the app for a terminal (C4); sessions Sindri didn't start, which DB §1 lists as its first purpose, are deferred to "Later", so the engineer still checks terminals elsewhere (C6). Nothing records whether the loop works (C5) |
| Viral coefficient | N/A in the usual sense: single user, loopback-only, no sharing surface in v0 (mobile and browser heads are later, §12.2). The internal analogue is pull-through: using the app moves more work into Sindri items (`C` Create item, `R` File a request, sessions seeded through `sindri_item`). That is present. Exposure to others only happens through PRs and receipts, which are deferred (§10.2 Receipts) |

## Strengths

1. **Logic-free and API-led (U1, U3, §2 rules).** Every button comes from the catalog intersected with the row's
   `actions`, and every sentence comes from the API. When engine phases land, new controls appear without a UI release,
   so the app grows with the platform rather than lagging it.
2. **Retention is designed in, with good manners.** One attention colour used only for needs-you, "seen" state that
   only stops the motion, reduced-motion support, one notifier (`notify.desk`), close-to-tray, and the `takeoverIdle`
   reminder. This loop can build a habit without nagging.
3. **The launch flow is honest about failure.** §6.6 shows every known failure *before* the click where it can be
   detected (Warp missing, CLI not on PATH, repo missing, Cursor limited). It only claims "Sent to Warp", never
   "running". The seed holds ids only, and the session pulls context through MCP. This is the cheapest way to make the
   first launch succeed.
4. **The phases are proven against the real app**, with OS screenshots, the five-second test, and a keyboard-only pass
   per phase. Each UI phase can be built against the contract-tested fake before its API phase merges, so UI work
   doesn't stall on the API.

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| C1 | HIGH | **The headline interaction ships last, and E-10 gates it on the engine.** §Phases puts U6 (Launch) after U5. E-10 makes API P10 depend on **WF P5**. Today API P10 depends only on P3 + P8 ("the engine marks the takeover once S2 runs"), and §6.4 says "Before S2 there is no instance, so nothing pauses; the item just records the launch." So the most habit-forming feature (U6) waits on two specs' schedules, and E-10 contradicts §6.4 | Split U6. **U6a "Open in Warp"**: Open mode only, with E-1, E-3, E-4, E-5 and E-11, the split button, the target menu, "Sent to Warp" and the failure states. It depends on U2 + API P10 (P3 + P8), *not* WF-P5, and with no instance yet it just records the launch, as §6.4 already says. **U6b "Attach, Resume, Take over"**: E-2's `liveSession` and `resumable`, plus E-6. It depends on U5 + WF-P5. Change E-10 to say only the takeover parts (E-6, and attach/resume of running steps) depend on WF-P5. Move U6a to wave W3 next to U3 |
| C2 | MEDIUM | **Activation before the engine is thin.** Through U2, Needs you has no source (P4 is U3), and parks only come with WF-P4 (U5). The five-second test runs on the fixture ledger, so it passes trivially on a real early install. An adopter's first impression is likely to be a Health card and a short queue | Add one line per phase to §Phases saying what the engineer gets on their *own* machine at the end of that phase (not the fixture). Order U3 straight after U1 where the API allows it (P4 only needs P3 for items). Run the five-second test once per phase against the owner's real `AW_SINDRI_HOME` (read-only) as well as the fixture |
| C3 | MEDIUM | **Home breaks at 10x running work.** Needs you is capped (6) and Queued is capped (next 5), but Running and With you is not (§4.2, §3.2). With factory workers running about 30 items, Health drops below the fold and the five-second test fails | Cap Home's Running panel the same way: With you rows always shown (they are the engineer's), then up to 5 running rows, then "Show all N running" (or a link to `G R`). Add a "10x fixture" (about 30 running, 50 queued, 15 needs-you) to the prover's five-second test in U2 and U3 |
| C4 | MEDIUM | **The most frequent park answers leave the app.** Grant rounds and Grant tokens (`parked: rounds`, `no-progress`, `budget`) and Accept (gate) are copy-the-CLI only: copy, switch to a terminal, paste, then type the confirmation. That gate is correct under the threat model (honest mistakes), but it is the biggest repeated friction in the loop, and the app already has a launcher that opens Warp tabs | Add a **"Run at a terminal"** affordance next to Copy. It uses the same `ILauncher` path to open a Warp tab whose launch configuration runs the exact catalog `cli` string, so the CLI's typed confirmation still happens in a real terminal and the gate is unchanged (the app runs nothing itself). The button is the owner's call. If it's declined, record why in §6.5 |
| C5 | MEDIUM | **No signal on whether the app works for its user.** Nothing measures time from a needs-you row appearing to it clearing, the share of clears done from the app versus the CLI, or the launch success rate (`launch.started` versus `launch.failed`). Today and metrics are deferred (§10.2), so nobody can tell whether the habit loop or the headline launch works | Add a small block to the Health page (or the API's `/status`) built from projections the ledger already supports: launches started and failed in 7 days with the top failure code, and the median needs-you time-to-clear by `X-Sindri-Client` (ui versus cli). It's a few fields, not the Today view |
| C6 | MEDIUM | **The dashboard's first purpose is deferred.** DB §1 starts with "see the progress of every agent … including agents that Sindri didn't start", and its success criteria say "every live agent on the machine appears within one poll". This spec puts Agents under "Later ▾ not served yet". The app is right not to fake it (U12), but until then the engineer still keeps watch in Warp and their terminals, which weakens the "one place" retention claim | State in §2 that the UI's five-second promise covers Sindri items only until the API §11 collector ships, and record DB §1's agents criterion as deferred in §13 (a new X row), so the dashboard spec's success criteria aren't silently dropped. Name the UI phase that adds Agents as soon as `GET /sessions` lands |
| C7 | LOW | **Probe freshness.** Launch targets are probed on start and every 10 min (E-3). If the engineer installs Warp or a CLI after seeing "Warp isn't installed", the button stays wrong for up to 10 min, which hurts exactly when they're trying to activate | Re-probe when the window gains focus (rate-limited to once per 30 s) and on a "Check again" link in the failure state; it's a desk command, or a `launch.probe` job |
| C8 | LOW | **Document hygiene.** The Status line says "Revised after `/autoplan` (§17 lists every finding and its resolution)", but §17 reads "(Filled after `/autoplan`.)". §16 lists 11 `mockups/*.html` files, and `2026-10-11-sindri-ui-design/mockups/` is empty at review time (it may still be generating) | Fill §17, or change the Status line to say the revision is pending. Before approval, check that every §16 mockup exists, because the phase signals and the design reviewer diff against them |

## Scope Suggestions

### Cut
- **The palette's `O` then `1`/`2`/`3` target sub-flow (§6.1 step 1)** until U6b. The chevron and `Shift+O` cover the
  per-click choice; a second keyboard path to the same choice can wait.
- **The comfortable row size and the text-size scale preference (§14.2, §14.3)** from U0. They are cheap later, and U0
  should be as small as possible so it lands first.

### Keep
- **The single list from `GET /needs-you`, plus the pulse, tray and badges**: the retention engine.
- **Every launch failure shown before the click (§6.6)**: it decides whether the first launch succeeds.
- **"Not served yet" instead of faked views (U12, §10.2)**: it keeps trust, which keeps people coming back.
- **Close-to-tray and single instance**: they make the app resident, which the badge loop needs.

### Add
- **U6a "Open in Warp" before the engine (C1).**
- **A Running cap on Home and a 10x fixture (C3).**
- **Three outcome fields in Health: launch success, time-to-clear, app versus CLI clears (C5).**
- **"Run at a terminal" for protected answers, if the owner agrees (C4).**

## Key Questions for the Team

1. Does anything in E-1 to E-5 or E-11 *need* the engine's takeover, or can Open in Warp ship on API P10 (P3 + P8) as
   API P10 is written today, with the takeover joined in once WF-P5 lands?
2. In practice, which park answers are most common? If rounds and budget grants dominate, is a launcher-opened terminal
   running the exact CLI (typed confirmation kept) an acceptable way to cut that friction?
3. What does "it works" mean six weeks after U3: a median time-to-clear, a share of clears done in the app, a launch
   success rate? Who reads that number, and where?
4. When does the API §11 collector (Agents and sessions) land relative to U6, and should the UI phase that adds Agents
   be named now?

## Recommended Next Action

Split U6 into U6a (Open in Warp on API P10 as written, wave W3) and U6b (attach, resume and take over, after WF-P5).
Narrow E-10 to just the takeover edits, and fix §6.4 and the dependency graph to match. Then cap Home's Running panel
and add the 10x fixture to the five-second test.

verdict: NEEDS_WORK
