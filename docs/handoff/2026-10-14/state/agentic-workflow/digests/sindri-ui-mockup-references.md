# Sindri UI mockup: reference-pattern pass (started 2026-10-13)

## Ask
Owner: "work on the mockup for Sindri with design" — Linear design system in Avalonia; borrow control-plane
patterns from https://superplane.com and https://paperclip.ing instead of reinventing them.

## What exists (all on main unless noted)
- Approved UI spec rev 7: docs/superpowers/specs/2026-10-11-sindri-ui-design.md (owner-approved rev 6 on 2026-10-11;
  §3.1 owner picked "single-glance home", top bar + Home panels + right detail sheet; §14 token plan DESIGN.md → Avalonia;
  §16 mockup list; §18 phases U0..U7).
- Mockups: docs/superpowers/specs/2026-10-11-sindri-ui-design/mockups/*.html (+png): home, needs-you, queue,
  item-detail, decisions, services-health, launch-flow, park-grant, park-needs-answer, escalated-gate, states;
  prototype.html; shotgun/variant-{1,2,3}.html (owner picked variant 2).
- Visual source of truth: docs/sindri/DESIGN.md (Linear tokens; app ramp in spec §14).
- Built so far (draft PRs, worktrees under .claude/worktrees/): #184 feat/sindri-ui-u0 (theme + shell, evidence in
  docs/evidence/feat-sindri-ui-u0/), #193 u1 (catalog/headers/freshness), #197 u2 (queue, item sheet). Views in
  src/Sindri/Aw.Sindri.Ui/Views/{Shell,Queue,Item,Palette}, Theme/Tokens.axaml etc.
- No ~/.agentic-workflow/<slug>/design/ artifacts (design-tokens.json, .impeccable.md, screens.json) for this repo.

## Reference captures (repo .playwright-mcp/, untracked)
- superplane-full2.jpeg + sp-0..5.jpeg; paperclip-full2.jpeg + pc-0..7.jpeg (lazy panels: scroll page first, then capture).

## Patterns worth borrowing
Superplane: stage board (Backlog/Implement/Verify/Done) with a per-column "what runs here" subtitle + rule icon;
card chips (Agent question, Clarity n / Confidence n, Review #27, Merged #25, source icon, assignee, age);
All/Active/My segmented filter; automation run view = Summary key/value panel + stacked collapsible rows
(Triggered → Running → Logs → Output) beside a vertical block chain with per-block status ("Did not run").
Paperclip: Linear-style sidebar (New Issue, Dashboard "1 live", Inbox, Projects, Work: Issues/Goals, Agents with
"1 live" dots, Company: Org); issue page = breadcrumb id · project, title, "Live issue runs (1)" panel with Stop /
Open run, Comments · Sub-issues · Activity tabs, right Properties panel (Status, Priority, Labels, Assignee, Project,
Started/Created/Updated); budget table (agent, bar, $used/$cap, total row); ticket thread + TRACE list with green
mono status words; org chart with Active badges; heartbeat timeline.

## Open question to the owner (asked 2026-10-13)
Keep the approved layout (top bar, single-glance home, side sheet) and borrow patterns inside it, or reopen the
layout toward Paperclip/Linear's sidebar + inbox (shotgun variant 1, which the owner rejected)?

## Owner's reframe (2026-10-13)
Re-evaluate what the UI needs. Valuable: knowledge-graph visualizer (context map), health of systems (dependencies +
engine execution), recipe management/inspection, agents in flight, and work scoped in Linear (MVP): start scoping
from a Linear Project with a PRD → scoping recipe + context engine → surfaces/outputs → design session in own tools
→ hand off to a builder recipe. Owner wants ASCII mockups first. Proposed nav: Home · Projects · Work · Recipes ·
Context · Agents · System (Decisions folds into Home/Needs you; Services folds into System).
Backing today: scope map = TS `sindri scope` (surfaces, workstreams, acceptance, open questions, recall/precision);
context graph = CTX spec §6.1 nodes/edges, §8.7 map review; recipes = WF spec §3 (+ registry P2, evals §9.2);
engine = Temporal, `workflow list` (P4a); agents = API §11 collector (later); tmux `snd-<run>` sessions (X55).

## Approved so far (2026-10-13, in chat)
- Context page = lens stack: INTENT band (Linear/Slack/Onyx records, decisions, conflicts) over PRODUCT GRAPH band
  (anchor, neighbors, cascade) over CODE band (files from map, symbols from index), cross-layer edges drawn
  vertically; lens toggles intent/product/files/symbols/draft; COVERAGE strip per lens with gaps → review queue;
  FILE MAP view = tree annotated with feature keys + intent counts, unmapped dirs, suggested links accept/reject.
  Owner: "yes that is perfect". Symbols lens waits on context phase 9.
- Pending owner answers: nav (Home · Projects · Work · Recipes · Context · Agents · System) and default page.
- Navigation settled (owner, 2026-10-13): seven items, Home default with a one-line Projects strip.
- Projects → **Workflows** (owner lgtm, 2026-10-13): list of root instances grouped by recipe, each group's columns =
  its recipe's states; instance detail is recipe-driven: STATES band (path order, loops ⇄, branches folded, current
  state expanded with step/gates/next/budget/timeout/commands), FACTS panel (recipe-declared inputs+outputs with a
  viewer per type: file, item, scope-map, packet, link, text — needs output declarations + fact types in the schema),
  CHILDREN panel (child instances grouped by the recipe's child declaration), timeline/decisions/requests/context tabs.
  Desk states render export/do-it-in-your-tools/import/checks. Subject = first input, adapter per kind for the header link.
  Scope map rows carry a graph column (▣ mapped · ◇ new → draft proposal in next map · ░ unmapped).
- Recipes (owner lgtm v1): list with 30 d stats (open, runs, done, failed, human rate, median, tokens/run, 4-wk trend,
  last change); inspector reuses the Workflows states band with open instances placed on it and per-state stats
  (median, parks, escalations, takeovers, gate refusals, loops), OUTCOMES / HUMAN TIME / COST panels, OPEN and EVALS.
- Agents (owner lgtm v1): sessions grouped under recipe › state; per-row actions from engine rules (live agent Step:
  Attach, Take over, Request, Cancel; tool/model Step: Open instance; with you: Attach, Hand back; parked/escalated:
  the answer kind, protected ones as CLI copy, Open in agent, Cancel); BY AGENT 30 d table; BUDGET today bars.
- System / item sheet / Home strip approved (owner lgtm). Spec written: docs/superpowers/specs/2026-10-13-sindri-ui-screens-design.md
  (branch docs/sindri-ui-screens), rev-8 note in the rev-7 spec, README row. Next: owner reviews the spec; then write
  E-20..E-31 into the API / WF / CTX specs + a README X row (the X52 practice); then writing-plans.
- HTML mockups in progress (4 parallel builders, 2026-10-13): docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/
  {home, workflows, workflow-project, workflow-incident, item-sheet, recipes, recipe-inspector, agents, context,
  context-filemap, system}.html, each a copy of rev-7 home.html's shell with the seven-tab nav. After: Playwright
  PNGs at 1280 wide (file:// via the MCP, saves under .playwright-mcp then move), check vs drawings, mockup table in
  the spec (§4 intro / new §4.9), commit on docs/sindri-ui-screens. Then the E-20..E-31 docs PR; WF P2 needs E-22 first.
- /impeccable pass requested (owner, 2026-10-13): critique → refine all axes → enhance → fix, over the ten mockups.
  teach done: docs/sindri/PRODUCT.md written (register product; "dense, calm, exact"; four anti-refs); load with
  IMPECCABLE_CONTEXT_DIR=docs/sindri. Assessment A (design review) running as a background agent; Assessment B
  (npx impeccable detect) = 220 findings: cramped padding 107, undersized functional text 57 (10.5px labels in the
  instance frames), tiny body 29 (11.5px), Inter "overused" 11 (false positive, spec'd font), low contrast 9, em
  dashes 3 files, tight line height 1. JSON at the session scratchpad detect.json. PNGs captured for 9 of 11 pages
  (recipe-inspector clipped at 800: needs taller capture; context-filemap and system pending). Mockup server:
  python http.server on 127.0.0.1:8765 serving the mockups dir.
- Captures filed for 10/11 pages (recipe-inspector pending a 900-tall capture; context-filemap captured at 1475 tall,
  re-capture at 800). Rendering defects seen so far, for the fix pass: item-sheet FACTS clipped below the fold and
  the Work summary line wraps beside the 520 sheet; recipe-inspector OUTCOMES bars missing and the blocks line cut
  at 800; context MAP REVIEW model row overlaps ("feature:session.restore 0.74" over the evidence line, stray
  "map_override / row" words); system IMPACT panel lines overlap (scoping / builder.feature consequences) and the
  legend wraps mid-sentence; workflow-project CHILDREN titles truncate ("Ba…"). Critique slug:
  specs-2026-10-13-sindri-ui-screens-design-mockups (compute via import of critique-storage.mjs; its CLI prints nothing).
- Critique done (2026-10-13): 25/40. Report at docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/impeccable-critique.md
  (+ .impeccable/critique snapshot). P1s: cross-page data drift (one fixture), rendering overlaps (context map-review
  row, system impact panel, inspector 1289 wide, children/crumb truncation, item-sheet head), topbar overflow at 1280
  (search 220/192), risky initial focus + bare Cancel X + no undo, glyph vocabulary (●◐○◎ per-page legends). P2s:
  protected label spellings with em dashes, side-stripes + always-pulsing band, item sheet 520 vs 760, type floor
  (10.5/11.5 px) and cramped chips. Next: owner decisions (health word, stripes, cancel safety, sheet width), then
  refine passes by agents per file group, re-capture, re-critique, commit.
- Owner decisions (2026-10-14): one health word (worst wins: Degraded in the fixture); stripes removed + band only
  when needs-you > 0; read-first focus, Cancel behind a confirm, undo for bulk accept; 760 for every instance sheet.
  Baseline committed 3ea9e73e (mockups, PNGs, PRODUCT.md, critique, fixture.md, fixes.md; .impeccable/ gitignored).
  Wave 1 (shared shell over 11 files, one script in the scratchpad) running as a background agent; wave 2 = four
  per-page agents per fixes.md "Wave 2"; then re-capture PNGs, re-run detect + a second critique, update the spec's
  mockup table, commit on docs/sindri-ui-screens.
- Wave 1 committed f07a0ba8 (detector 220 → 31; 11 real left). Wave 2 launched 2026-10-14 as four parallel agents
  (A home+workflows, B instance frames+item sheet, C recipes+inspector+agents, D context+filemap+system), each
  loading the impeccable references per pass and verifying with scratchpad/dom-check.keep.cjs + detect + screenshots.
  After: re-capture the 11 PNGs (Playwright MCP, viewport = page height), second critique run (same slug for the
  trend), spec §4 mockup table + note on fixes.md/fixture.md, commit; then the E-20..E-31 docs PR.
- Wave 2 done and verified by the lead (2026-10-14): all 11 PASS dom-check; detector clean except the em-dash advisory
  on tabular nulls in recipes + recipe-inspector. Committed as wave 2. PNGs re-captured via dom-check --shots (full
  page). Spec: §4.9 Mockups table + R8-13 added. Second critique assessment A launched (session model, independent,
  told not to read the first critique). Owner preference saved: builders on Sonnet (memory feedback_builders_on_sonnet).
  Remaining: write critique #2 snapshot (same slug → trend 25 → N), commit PNGs + spec + critique, then the
  E-20..E-31 docs PR (use Sonnet builders for it).
- Critique run 2 (2026-10-14): 28/40 (trend 25 → 28), appended to impeccable-critique.md, committed. Remaining P1s:
  Attach semantics (three meanings), not-yet-wired actions rendered live (decision Accept), fixture drift
  (workflow.project missing from Recipes; engine table rows; index-failure time 03:00 vs 12:21; INC-14 NOW behind
  sheets), three behind-sheet treatments. P2s: human-blocked vocabulary/counts, shell drift (band/sheet tab/hints/
  legends), protected button has no click (show the command line everywhere), >4 choices on three rows.
  Owner to decide: a third pass (Sonnet builders) on these, or stop here and do the E-20..E-31 docs PR.
- Owner (2026-10-14): "fix the P1s and then option 2" (full clickable prototype for rev 8, like rev 7's prototype.html).
  fixture.md gained "Sentences that must read the same on every page" (Attach: joins the live session, first keystroke
  takes over; decision row: Accept records now, engine acts when it runs; INC-14 NOW; PRD-exports.md; one behind-sheet
  rule) + Recipes (8) with workflow.project + code index ✕ 03:00 + engine table 8 rows/activities 4. fixes.md "Wave 3".
  Wave 3 running on a Sonnet builder over the 11 files. After it: verify (dom-check, detect, greps), commit, re-capture
  PNGs; then Phase B: prototype.html in 2026-10-13-sindri-ui-screens-design/ (Sonnet builder; reference rev-7
  prototype.html 154 KB: router, sheets, every §4.4 answer, launch flow, palette, keys, reduced motion, controls panel,
  --large fixture); lead verifies by clicking through with the Playwright MCP; spec §4.9 gains the prototype row.
- Wave 3 verified by the lead and committed with fresh renders (2026-10-14): 11/11 DOM PASS, Recipes 8 everywhere,
  one Attach sentence, no 12:21 index time, detector clean except tabular nulls. Known gap left: Home's JE9/JD6 rows are
  tool Steps but still show Attach (should be Open instance + menu, as on Agents). Prototype build launched on a Sonnet
  builder: prototype.html in the rev-8 spec dir, scripted click-through + DOM checks + prototype-*.png; lead then clicks
  through with the Playwright MCP (serve the spec dir on 8766), adds the prototype row to spec §4.9, commits.
- Prototype builder (Sonnet) hit the session usage limit mid-build; prototype.html was on disk at ~310 KB, no PNGs
  yet; resumed via SendMessage after the reset (2026-10-14). If it stops again: resume the same agent, don't relaunch.
- Prototype built (Sonnet, after a rate-limit resume) and committed: prototype.html (310 KB) + prototype-*.png; spec
  §4.9 row. Lead click-through via Playwright MCP on 127.0.0.1:8766: Enter on the park row → item sheet; Grant rounds
  reveals the CLI with an announcement; Agents ▾ → Cancel… → Keep (focused, nothing changes) and → Cancel item (3 live,
  row gone, "ITM-01JE9 cancelled. 11 items open"); 0 console errors. Builder deviations to tell the owner: Build draft
  enabled (per spec), Run again live, tool-step rows get Open instance not Attach (Home mockup still shows Attach),
  inline popovers for Start/Create/Validate/Import, seed data for non-fixture inspector numbers. Open in a browser:
  file path docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/prototype.html.
- Owner (2026-10-14): UI "too busy compared to Superplane or Human Layer": uniform columns/rows, cards instead of
  colour bands, ITM-* ids as noise. Approved "the calm pass" = fixes.md Wave 4 (eight rules: sections by surface,
  ids recede, one action per row, 3-4 columns + progress bar on Workflows, type scale 20/15/14/13/12/11, uppercase
  state chips, nothing repeated, 40 px rhythm); committed 1d409fad. Wave 4a (shell, Sonnet) running; then 4b four
  per-page lanes (Sonnet), verify (dom-check, detect, screenshots), commit, rebuild prototype from the new mockups
  (scratchpad/build/build.py per the prototype builder; re-run its click-through), third critique for the trend.
  Human Layer capture: .playwright-mcp/hl-0.jpeg (task view: STATUS chip · SESSION title+tag · UPDATED; four columns).
- Wave 4a committed 7edad4b0 (shell: type scale, chips, sections/bands, .id hidden, legend+motion removed; health
  sentence). Wave 4b launched: four Sonnet lanes (A home+workflows, B sheets, C recipes+inspector+agents, D context+
  filemap+system). After: lead verifies (dom-check 11, detect, read PNGs), re-capture PNGs (dom-check --shots with the
  file list), commit; rebuild prototype (Sonnet builder: scratchpad/build/build.py regenerates prototype.html from the
  mockups; re-run its click-through scripts in scratchpad/t; re-capture prototype-*.png); third critique (same slug).
- Wave 4b verified by the lead and committed c613c97e (11/11 PASS, detector clean except inspector's tabular nulls;
  renders re-captured; Home 34 visible controls from 45, Agents 21 from 26). Prototype rebuild (Sonnet, from
  scratchpad/build/build.py + updated click-through scripts) and critique run 3 (session model) launched in parallel.
  Then: write snapshot 3 (slug specs-2026-10-13-sindri-ui-screens-design-mockups; trend 25 → 28 → N), append to
  impeccable-critique.md, verify the prototype by clicking through, commit, report. Then the E-20..E-31 docs PR.
- Critique run 3 (post calm pass): 27/40, trend 25 → 28 → 27; committed 3fcd360f. The direction holds (Home, sheets,
  Context, System praised); consistency drifted: chip colours/words differ per page (Live vs Running), Run again
  disabled on Home but live on System, Recipes lost its numbers (45% blank), ids hidden inside NOW sentences and on
  Work/Agents where the engineer copies them, inspector under-counts, System "last 8 of 88"/"4 open" wrong, Agents
  meter hidden at rest, Workflows sparse. Proposed wave 5 (one Sonnet lane, "consistency"): chip table in fixture.md,
  ids at rest in --ink-tertiary in a fixed slot (never inside prose), Recipes columns back (failed, tokens p50,
  spark), Run again truth (pending on both until API P6), inspector/System counts, Agents meter at rest, Workflows
  action column auto + ⌘N + order rule. Prototype rebuild (Sonnet) still running.
- Owner rule (2026-10-14): one consistent label-to-colour mapping for state chips → fixture.md "State chips" table
  (20 words; LIVE retired; stages are tags), committed bea24290 with fixes.md Wave 5. Wave 5 lane (Sonnet) launched
  over all 11 mockups while the prototype rebuild (Sonnet) still runs; the prototype must be rebuilt once more after
  wave 5 (build.py), then lead click-through, commit, a fourth critique for the trend, then the E-20..E-31 docs PR.
- Wave 5 landed (chip audit 0 violations; LIVE gone); lead changed ids at rest from --ink-tertiary (under AA) to
  --ink-subtle, hover --ink-muted; verified and committed with renders. Next: rebuild prototype (Sonnet, build.py +
  scripts), lead re-runs walk.mjs, commit prototype; critique run 4 (trend 25 → 28 → 27 → N); then the docs PR.
- Agents meter text to subtle ink, committed cbae68b6; all 11 detector-clean except the inspector/recipes tabular-null
  advisory. Final prototype rebuild (Sonnet) and critique run 4 (session model, blind) launched. After both: lead
  re-runs walk.mjs, commits prototype + PNGs, writes snapshot 4, reports the trend; then the E-20..E-31 docs PR.
- Critique run 4: 29/40, trend 25 → 28 → 27 → 29; committed. Remaining P1s (a small wave 6 if the owner wants it):
  one definition of need you / waiting on you / with you with counts derived from rows; Recipes % vs count from one
  fixture source; band on all pages or none + one health sentence from the pill's reasons; the Attach note under every
  Attach primary. P2: queued tag shrink, sheet capture height, Timeline "more", one drawn state per page, needs-you
  ids at rest, "soft 80%" colour. Note for the Avalonia build: bundle Inter + JetBrains Mono. Prototype rebuild pending.
- Final prototype committed 9f20ed53 (330 KB; walk.mjs 23/23 run by the lead, dom.mjs 21 states + chip audit 0
  violations). Branch docs/sindri-ui-screens is 18 commits ahead of main, nothing pushed, no PR. Open with the
  owner: wave 6 (four P1s of critique 4) now or later; then the E-20..E-31 docs PR (write the edits into the API, WF,
  CTX specs + README X rows + ERD column for E-30; Sonnet builders), then re-brief WF P2, WF P4, CTX phase 4 lanes.
- Owner (2026-10-14): "Docs then Wave 6". Docs PR in progress on docs/sindri-ui-screens: three Sonnet lanes writing
  E-20..E-31 into the API spec (E-20,22,23,24,25,28,29,30 halves; new phases P12 recipe stats, P13 engine), the WF
  spec (E-21, E-22 schema outputs/kinds + RCP rules, path order, E-30 ledger column in Phase 4, E-31 scoping recipe)
  and the CTX spec (E-26 lenses, E-27 filemap, E-31, new SND-CONTEXT codes; Phase 4/9). Lead then: README X62 row +
  check ERD/docs/csharp/sindri.md for the ledger column, commit, push, open the PR; then wave 6 (critique 4's P1s).
- Docs lanes done and committed 8ca572bd: API §3.8 + P12/P13 + codes; WF §3.2/3.3/3.10/5.6/9.1/12 + P2/P4 +
  docs/csharp/sindri.md ledger column; CTX §8.5-8.9, §6.1 propose, Phases 4/9; README rows X60 (edits written in) and
  X61 (open: map review accept/reject protected vs desk; recommended desk with Undo, promote/import stay protected).
  Branch 19 commits ahead of main. Next: push (block-push-main hook: push alone in its own command), gh pr create,
  then wave 6 (critique 4's four P1s) on a Sonnet lane, rebuild prototype, commit.
- PR #210 open (docs/sindri-ui-screens → main): https://github.com/jthorare/agentic-workflow/pull/210. Wave 6 lane
  (Sonnet) launched over fixture.md + 11 mockups; spec R8-13 amended (band withdrawn, human-queue definition). After
  the lane: lead verify (dom-check, greps, count check, detect), re-capture, commit, rebuild prototype (Sonnet,
  build.py + scripts), lead walk.mjs, commit, push, fifth critique for the trend, report.
- 2026-10-14: the session scratchpad was wiped on resume (dom-check, build.py, t/*.mjs lost). Tooling now lives in
  the repo: docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/tools/ (dom-check.cjs written by the lead;
  the prototype rebuild lane recreates build-prototype.py, prototype-runtime.js, walk/more/extra.mjs, proto-dom.mjs,
  README.md). Servers: mockups on 127.0.0.1:8765, spec dir on 8766 (python http.server; restart after a resume).
  Wave 6 landed (fixture: human queue, Recipes numbers table, health sentence; 11 mockups); lead verifying.
- Wave 6 verified by the lead (tools/dom-check.cjs 11/11; detector clean except tabular nulls) and committed 423354d9;
  pushed to PR #210. Critique run 5 launched (blind). Prototype rebuild lane (Sonnet) recreating tools/ in the repo.
  After both: snapshot 5 (trend 25 → 28 → 27 → 29 → N), lead runs tools/walk.mjs, commit prototype + tools, push,
  report. Then: owner decides X61 (map review desk vs protected) and the PR review.
- Critique run 5: 28/40 (trend 25 → 28 → 27 → 29 → 28: plateau). Remaining P1s are build-time consistency: one
  Attach component, derived open counts + JD6's recipe in the fixture, a popover on every lock button. Product
  questions for the owner: count the desk answer on Home (6) or don't chip it; "open" includes done rows; typed
  confirmation in ⌘K instead of a terminal (contradicts the protected class). Prototype rebuild lane still running.
- Final prototype + tools/ committed (lead ran tools/walk.mjs 25/25; build --check up to date). PR #210 holds the
  whole rev-8 branch. Done with the mockup loop: the score plateaued (27-29); remaining items are build-time
  consistency (one Attach component, derived counts, lock-button popover) and three product questions for the owner
  (count the desk answer on Home; "open" includes done rows; typed confirmation in ⌘K vs a terminal) plus X61.
  Run tooling: see tools/README.md (NODE_PATH=skills/ui-evidence/node_modules; servers 8765 mockups, 8766 spec dir).
