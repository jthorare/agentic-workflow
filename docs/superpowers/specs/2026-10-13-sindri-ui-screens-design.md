# Sindri UI app, revision 8: the seven screens

Status:
- Draft, 2026-10-13. Designed with the owner in one session from ASCII mockups; every screen below was approved as
  drawn ("lgtm", "lgtm for v1", "yes that is perfect"). Not yet through `/autoplan`.
- **Amended 2026-10-16** (owner, through the scoping station spec #258 rev 3.2): R8-14; U9 takes an anchor set and a
  pinned map version (§4.4, §8); E-26 widened and E-32 added (§6, §8.1).
- **Supersedes** §3 (information architecture), §4.2–§4.8 (the pages), §10 (data sources), §16 (mockups), §18 (phases)
  and §19 (the agent team) of the revision-7 spec `2026-10-11-sindri-ui-design.md`. Everything else there stands:
  the window and detail-sheet mechanics (§3.2), the shared states (§4.1), the park and escalation answers (§4.4), the
  needs-you pulse (§5), the launch flow (§6), controls and action classes (§7), the keyboard model (§8), the API
  conversation (§9), accessibility (§11), the heads (§12), the token plan (§14) and the shell verdicts (§15).
- The HTML mockups of revision 7 stay as the visual reference for the shell, Home's needs-you panel, the park and
  escalation sheets, the launch flow and the pre-API states. This revision's mockups are the ASCII drawings in §4;
  their HTML counterparts are produced by the first phase that builds each screen (§8).

**Why a revision.** The owner re-evaluated what the app must contain (2026-10-13). Five things earn a screen: the
context graph with its layers overlaid; the health of the system's dependencies and of the engine's execution;
recipe management and inspection with their numbers; agents in flight and what one can do to them; and the work
scoped from the project management tool (Linear for the MVP), from a project's PRD through scoping to a builder
recipe. Two reference products were read for patterns worth keeping rather than reinventing: SuperPlane
(stage columns that say what runs in them, status chips on cards, the run view's summary and step chain) and
Paperclip (the issue page's properties column and live-run panel, the trace list, the budget table, the "N live"
signal). Ideas only; no code or asset is copied.

**Builds on**, in this order of authority for its parts: the revision-7 UI spec (above); the API surface spec
(`2026-10-10-sindri-api-surface-design.md`, "API"); the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`,
"WF"); the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`, "CTX"); `docs/csharp/packets.md`
(the packet checks); `docs/sindri/scope.md` (scope maps); `docs/sindri/DESIGN.md` (Linear's design language) and
revision 7's §14 token plan.

## 1. Decisions (owner, 2026-10-13)

| # | Decision | Reason |
|---|---|---|
| R8-1 | **Seven pages:** Home · Workflows · Work · Recipes · Context · Agents · System. Home is the default page. Decisions folds into Home's Needs you; Services folds into System | Each page answers one of the five questions; Home keeps the five-second test |
| R8-2 | The approved layout stays: top bar, single-glance Home, right detail sheet, dark Linear theme | Three PRs (U0–U2) stand on it; the new pages fit inside it |
| R8-3 | **Workflows is recipe-driven.** The list groups root instances by recipe and draws each group's columns from its recipe's states; the detail page renders every panel from the recipe (states, facts, children) and the engine (status). The factory line is one recipe among triage, incident response and the rest | The first draft hardcoded the factory line; the owner: "too software factory specific and not dynamic enough for the recipes it could hold" |
| R8-4 | **Context is a lens stack:** intent, product graph and code are three bands on one vertical axis with the cross-layer edges drawn between them, lens toggles, a coverage strip and a file-map view | The context engine is a multi-store system (code index, product graph, file map joined to Linear and Slack); the owner was "missing the visualization of the multiple lenses overlaid on top of each other" |
| R8-5 | **Recipes show numbers:** runs, done, failed, human-intervention rate, medians, tokens, trend; the inspector places open instances on the states band and gives per-state stats | Owner: "execution numbers, successful v failed, human intervention rate and other stats per recipe" |
| R8-6 | **Agents are grouped under the recipe state they serve,** and each row offers the actions the engine allows in that state | Owner: "active relative to their recipe + interactions users can take against them" |
| R8-7 | **System draws dependencies as a graph** with health on the edges and an impact panel, and shows engine execution in depth (Temporal's worker, instances, pending work, history, mirror lag) | Owner's pick from the last group |
| R8-8 | **The item sheet uses the Workflows frame** (states, facts, children); an item is a child instance | Owner's pick; one frame for every instance |
| R8-9 | Home's strip under Needs you lists root **workflow** instances across recipes, incidents and triage first | Owner's pick |
| R8-10 | A desk step (export, do it in your own tools, import, checks, freeze) is how a `desk` state renders, not a page of its own. The design session lives in the owner's tools; Sindri records a link and a timestamp and advances only on import | Sindri can't see inside Figma; the state machine stays the recipe's |
| R8-11 | New surfaces a scoping run finds become **draft proposals in the next map version**, never writes to the current map | Map promotion stays a `protected` command (CTX §8.7) |
| R8-12 | The UI reads Temporal's history and pending work **through Sindri's API**, never from Temporal directly | One client contract (rev 7 §9); the dev server stays loopback-private |
| R8-13 | During the mockup fix passes (2026-10-14): the health word is one word, worst verdict wins; no side stripes; initial focus on a read action and Cancel behind a one-line confirm, bulk accepts get Undo; every instance sheet is 760 px; one state-chip mapping of word to colour (`fixture.md`); sections by surface instead of cards, ids at rest in subtle ink, one inline action per row; the attention band is withdrawn (wave 6: the health pill carries the state, and one health sentence generated from the pill's reasons reads the same on Home and System); one definition of the human queue (needs you is the union of parked, escalated, decision, failed service and profile rows; "waiting on you" is retired; with you is neutral and never counted) | The critiques' P1s and the owner's calls; `fixes.md` waves 1–6 carry the lists |
| R8-14 | **A scope opens in Context** (owner, 2026-10-16; #258 SC11). When scoping finishes, its graphs open in the lens stack: U9 takes an **anchor set plus a pinned map version** (the stop's), filtered to the scope's found ids, drawn per **scope lens** (Code · Product · Related · History · All; the band toggles keep the name "lenses"), with a "live" toggle to the current map. The pinned graph is drawn from the scope packet's frozen `lenses.json` alone, so it reads the same after the map version is gone and opens after the run area is purged | The owner reads a scope's blast radius as a graph, as it stood when they approved it; every scope view before this was a table |

## 2. What the app is for (restated)

The engineer's window onto the control plane, now answering five questions on five pages, with Home and Work as
before:

| Question | Page | Data owner |
|---|---|---|
| What needs me, what is running, what is queued, is Sindri healthy? | **Home** (rev 7 §4.2, plus the workflows strip) | API P4, P3, P2 |
| Where is each piece of work in its recipe, and what did it produce? | **Workflows** | WF P4/P5 through API P11 and the edits in §6 |
| Which items exist and in what state? | **Work** (rev 7's Queue, §4.5) | API P3 |
| How do the recipes perform, and where do they stall? | **Recipes** | WF P2 registry, the stats query in §6 |
| What is the product, what was decided about it, where does it live in code? | **Context** | CTX phases 1–9 |
| Which agents are live, on what, and what can I do to them? | **Agents** | API §11 collector, P10, WF P8 |
| What depends on what, what is failing, what is the engine doing? | **System** | API P2, P6, doctor, the engine query in §6 |

## 3. Navigation

```
◆ Sindri   Home   Workflows 12   Work 10   Recipes 7   Context   Agents 4   System      ⌘K Search or run a command      ● Healthy · 2s ago
```

| # | Page | Key | Count in the tab | Detail sheet opens |
|---|---|---|---|---|
| 1 | Home (default) **(SO-19)**: a Setup card above Needs you until every setup step is done or skipped (three rows and "and N more", so Needs you stays above the fold); chord `G U` and the in-app route `setup` open it (no OS scheme) | `G H` | — | any row |
| 2 | Workflows | `G W` | open root instances | an instance (§4.2) |
| 3 | Work | `G Q` | items | an item, in the Workflows frame (§4.7) |
| 4 | Recipes | `G R` | recipes | a recipe (§4.3) |
| 5 | Context | `G C` | — | a node |
| 6 | Agents | `G A` | live sessions | a session's instance |
| 7 | System | `G .` | — | a dependency's doctor check, an instance's history |

Rev 7's `G N`, `G D` and `G S` chords become `G N` (Home's Needs you, unchanged), `G D` (Home filtered to decision
rows) and `G S` (System). Deep links add `workflow/<address>`, `recipe/<id>`, `node/<key>`, `session/<id>`,
`system/<dependency>`. Everything in rev 7 §3.2 (the window, caps, the sheet, focus stability) applies to every page.

## 4. Screens

Conventions in the drawings: `●` done or ok · `◐` in progress or degraded · `○` not started or absent · `✕` failed ·
`▲` escalated · `▌` parked · `◌` a model call with no terminal · `⇄` a loop · `▣` the current or anchored one ·
`◇` proposed or model-suggested · `░` unmapped · `⌂` a `protected` action, rendered as the CLI to copy (rev 7 §7).
Every drawing is 1280 wide at text scale 1.0; the sheet variants are 520 wide.

### 4.1 Home

Rev 7 §4.2 as approved (Needs you, Running and With you, Queued, the health strip), with one addition between
Needs you and the two columns: the **Workflows strip**, one line per root instance, running incident and triage
instances first, then by age.

```
┌ WORKFLOWS 8 open ──────────────────────────────────────────────────────────────────────────────────────── Open Workflows ▸ ┐
│ ▲ INC-14 Outbox retry storm         incident.response   mitigate r1/3 ⇄ verify   1h 24m   agent rolling back · comms posting │
│ ◐ sentry #8812 TypeError            triage.alert        classify · desk          4m       needs your class            [Answer]│
│ ◐ APP · Offline sync                workflow.project    build 4/9                3h       ITM-01JE7 produce 2/3               │
│ ◐ ENG · Billing exports             workflow.project    design · desk            2h       with you: import the design output │
│ ◐ ENG · Tab manager v2              workflow.project    scoping r2/3             46m      challenger round 2                  │
│ … 3 more                                                                                                                     │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- Cap 5 rows, then "… N more" which opens Workflows. The fold rule of rev 7 §3.2 now counts this strip: at 1280 × 800
  the summary line, Needs you (up to 5), the strip (up to 3 when Needs you has 5) and the tops of the two columns are
  above the fold; the strip gives rows back to Needs you first. Provisional owner default (pass 6, pending the owner's answer):
  a Needs-you row is two lines (about 61 px: title, chip and id, then the served sentence and the row's actions), so five rows
  and three strip rows put the column tops near y = 712 at 1280 × 800 and text scale 1.0.
- A row's `[Answer]` is the desk answer control of rev 7 §4.4; a protected answer shows the CLI.
- Data: the Workflows list query (§6, E-20) with `root=true`, ordered by the recipe's `urgency` class (E-21).

### 4.2 Workflows

**List.** Root instances grouped by recipe. Each group's columns are its recipe's states in path order (§5.1), so the
groups differ on purpose. The drawings in this section are illustrative; states come from the recipe (the recipe files and goldens, not these pictures, say which states a recipe has and what they are called). The groups follow the order the list serves: the engine puts the groups with a running incident or triage instance first,
and the app keeps that order.

```
Workflows    8 open · 2 need you · 1 with you                                     [Recipe ▾] [Subject ▾]      [Start… ⌘N]
┌ workflow.project · Linear projects  3 ─────────────────────────────────────────────────────────────────────────────────────┐
│ SUBJECT                 INPUTS            intake  studio  scoping  design   build      verify  ship     NOW                 │
│ ENG · Tab manager v2    PRD-tabs.md ✓     ●       ●       ◐ r2/3   ○        ○          ○       ○        challenger round 2  │
│ ENG · Billing exports   PRD-exports ✓     ●       ●       ●        ◐ desk   ○          ○       ○        with you: import    │
│ APP · Offline sync      PRD-sync.md ✓     ●       ●       ●        ●        ◐ 4/9      ○       ○        ITM-01JE7 produce   │
├ triage.alert · webhooks  6 ───────────────────────────────────────────────────────────────────────────────────────────────┤
│ SUBJECT                 INPUTS            received  classify  route    done                               NOW              │
│ sentry #8812 TypeError  payload ✓         ●         ◐ desk    ○        ○                                  needs your class │
│ sentry #8809 429s       payload ✓         ●         ●         ●        ● → ITM-01JF0 queued               done 12m         │
│ pagerduty P-2211        payload ✓         ●         ▲ escalated: gate.severity 0.91                        accept at a term.│
├ incident.response  1 ─────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ SUBJECT                 INPUTS            detected  triage  mitigate ⇄ verify  comms   postmortem  closed  NOW             │
│ INC-14 outbox retry storm  P-2211 ✓       ●         ●       ◐ r1/3          ◐ child  ○           ○       mitigation running│
├ intake.linear  2  ·  scoping.project (standalone)  0 ────────────────────────────────────────────────────────── show all ▾ ┤
│ 4 done today ▸ (collapsed)                                                                                                │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
   ● done   ◐ in progress   ○ not started   ▲ escalated   ▌parked   ⇄ loop        Enter opens · A answers a decision or completes a desk task · B hand back · X cancel
```

- **SUBJECT** is the instance's first input rendered by its kind's adapter (§5.4): a Linear project shows team and
  name; a webhook shows its source and id; an item shows its id and title. **INPUTS** shows each declared input with
  ✓ present or ✕ missing; a missing required input is why a stage reads `—` (blocked).
- A stage cell shows the glyph, then the loop counter (`r2/3`), `desk` for a desk state, `child` when the state waits
  on children, `n/m` for a children state's progress, or an engine-owned badge (`▲ escalated: <reason>`,
  `▌ parked: <reason>`) in place of the glyph.
- **NOW** is the served `now` sentence of the row (the list serves it on each instance), shortened to the cell; the frame's expanded state shows the engine's `next` sentence whole.
- **Start…** opens rev 7's start form (X34): approved recipes and their declared inputs; a subject kind's adapter
  offers a picker (the profile's Linear teams' projects, for `item` kind the queue).
- Keys: `Enter` opens the instance; `A` answers a park or escalation (desk answers inline, protected ones as CLI);
  `B` hands back; `X` cancels (destructive, confirmed); `N` starts.

**Instance detail (the frame).** One screen for every recipe. The panels are the recipe's states, the recipe's facts
and the recipe's children; the expanded state is the engine's status layout (WF §6.4) rendered as controls.

```
workflow/PRJ-sync   Offline sync                                     workflow.project@a8c21e7f · v12 · build · since 09:14 (3h)   Esc ✕
subject  Linear APP · PRD-sync.md (linear:doc) · feature:sync                 [Open in Linear ↗]  [Open in Context ▸]  [Cancel X]

┌ STATES  from the recipe, in path order ─────────────────────────────────────────────────────────────────────────────────────┐
│ intake ● ── studio ● ── scoping ● r1 ── design ● desk ── ▣ build ◐ 4/9 ── verify ○ desk ── ship ○                            │
│ ▣ build · step.children(builder.feature) one child per packet task · gates on exit: gate.evidence.all, gate.judge.verify ≥ 0.8  │
│   next   Build runs each packet task as a child item; verify opens for you when every child is done.                           │
│   budget 1.2M / 3M (state) · timeout parks in 2d 4h · commands  takeover, cancel                                 [Take over T] │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
┌ FACTS  declared by the recipe, produced so far ─────────────────────────────────────────────────────────────────────────────┐
│ fact:brief        ● PRD-sync.md · linear:doc/… · updated 5d ago                                                   [Open ↗]   │
│ fact:scope-map    ● scope-sync-v3.json · 14 surfaces · ▣ 9 · ◇ 3 · ░ 2 · backtest 0.74 / 0.70 PASS      [View ▸] [Export ↗]  │
│ fact:design       ● design-output/ · imported 10-12 08:50 · figma.com/file/… ↗   (desk step: design, completed by you)       │
│ fact:packet       ● packet.json · frozen sha256:3f1c09 · 9 tasks · checks 9/9 ✓                                   [View ▸]   │
│ fact:evidence     ◐ 4 of 9 packs                                                                                              │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
┌ CHILDREN  9 · workflow.step, grouped by packet workstream ─────────────────────────────────────────────────────────────────┐
│ ws-1 UI      ITM-01JE7 Sync status pill    ◐ running produce 2/3 · Claude Code 6m   [Open ▾] [Take over T]                    │
│              ITM-01JF0 Conflict resolver   ○ queued after ITM-01JE7                                                           │
│ ws-3 Mobile  ITM-01JC8 Background sync     ▌parked no-progress · mirrored here           [⌂ Grant rounds — at a terminal]     │
│ … 6 more                                                                                                     Open all in Work ▸│
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
  Timeline 41   Decisions 3   Requests 1   Context 14
```

The same frame carrying an incident; nothing in the chrome changes:

```
workflow/INC-14   Outbox retry storm                              incident.response@5d90e1b2 · v4 · mitigate · since 11:02 (1h 24m)   Esc ✕
subject  PagerDuty P-2211 (webhook) · feature:outbox.dispatch                                    [Open in PagerDuty ↗]  [Open in Context ▸]

┌ STATES ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ detected ● ── triage ● sev2 ── ▣ mitigate ◐ r1/3 ⇄ verify ○ ── comms ◐ child ── postmortem ○ desk ── closed ○                  │
│ ▣ mitigate · step.produce (agent) · loop ≤ 3 with verify · gate on exit: gate.metric.below(error_rate, 1%) for 10m              │
│   next   Agent is applying the rollback in ITM-01JG2; verify re-checks the error rate 10 min after each attempt.               │
│   budget 60k / 200k · timeout escalates in 36m · commands  takeover, cancel              [Attach O] [Take over T] [Cancel X]  │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
┌ FACTS ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ fact:alert      ● P-2211 payload · 11:02 · error_rate 4.2%                                                        [View ▸]   │
│ fact:severity   ● sev2 · decided by you 11:09 ("customer-visible, no data loss")                                               │
│ fact:runbook    ● runbooks/outbox-retry.md (repo-docs) · cited R2                                                   [Open ↗]  │
│ fact:timeline   ◐ comms child posts to #incidents every 15m · last 12:15                                                        │
│ fact:postmortem ○ desk step, opens after closed                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
┌ CHILDREN 2 ──────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ mitigate   ITM-01JG2 Roll back dispatcher 1.4.2   ◐ running · Claude Code · 14m          [Open ▾] [Take over T]              │
│ comms      INC-14/comms/1  incident.comms        ◐ running · next post 12:30                                                  │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
  Timeline 18   Decisions 1   Requests 0   Context 6
```

- **Header:** the state word is the status header's served `state`, followed by `: <detail>` when the engine serves a detail (for example `escalated: gate`). The loop round (`r1/3`) is not in the header: it stays on the band's cell. The frame's drawings are illustrative; states come from the recipe.
- **STATES band:** the recipe's states in path order (§5.1); the current state is expanded with its Step or children
  declaration, its exit gates with thresholds, the engine's `next`, budget and timeout lines, and the commands the
  engine lists, as buttons with rev 7 §7's classes. Engine-owned states (parked, escalated, taken-over) are badges on
  the state they interrupted, with rev 7 §4.4's answer controls.
- **FACTS panel:** every input and output the recipe declares, in declaration order, with ● produced, ◐ in progress,
  ○ not yet. The viewer per fact type (§5.2) gives the row its summary and buttons. A desk step's outputs appear here
  as the step completes them; while the desk step is open, the panel's row for its pending output carries the
  complete form (file picker or text, the checks that will run, then the complete command).
- **The scope-map viewer** (fact type `scope-map`) adds a **graph column** to every surface: `▣` mapped to a product
  node, `◇` new (a draft proposal in the next map version, R8-11), `░` unmapped (cited in the brief, no node); and
  an **items column** linking each surface to the child items that implement it once a builder has run.
- **The packet viewer** (fact type `packet`) lists the tasks by workstream with their acceptance counts and the
  `PKT-*` check results; a failed check names the check and the tasks (`PKT-SCOPE-002: t4 and t7 both write
  src/sync/queue.ts`) and the freeze control stays disabled.
- **CHILDREN panel:** child instances grouped by the recipe's child declaration (the state that spawned them) and,
  for a children state fed by a packet, by workstream. Each row carries the child's state, the agent and age, and
  the actions its state allows (§4.5); a park mirrored to the parent shows here and in the parent's band.
- Tabs: Timeline (the ledger events for the address and its children), Decisions, Requests, Context (rev 7 U7,
  now the lens stack anchored at the subject's node).
- Data: `GET /workflow/instances/{address}` with `states`, `facts`, `children` (E-20, E-22), the status layout
  (WF §6.4) as structured fields (E-23), the ledger timeline (API P3), context (CTX).

### 4.3 Recipes

> **Build note (U10a).** The inspector without stats is built first, as the read-only sheet `recipe/<id>[@hash]`; the Recipes list page and the stats (E-24, E-25) are U10b. See `docs/csharp/sindri-ui.md` "U10a" for what the recipe route serves and the gaps G1 to G8.

**List.** Execution numbers over a window (default 30 days). "Human" is the share of runs that needed a person: a
park answered, an escalation decided, or a takeover.

```
Recipes   7 · 30 days ▾ · 184 runs · 81% done without you                                       [Validate file…]  [Import]
RECIPE               STATUS      OPEN  RUNS  DONE  FAILED  HUMAN   MEDIAN    TOKENS/RUN  TREND (4 wk)   LAST CHANGE
workflow.item        ● approved   4    112   97    6       19%     1h 40m    310k        ▂▄▆█           10-02 v7
workflow.step        ● approved   6    —     —     —       —       —         —           (child only)   10-02 v4
builder.feature      ● approved   9    38    31    2       13%     2h 05m    420k        ▄▆▆█           10-08 v3
triage.alert         ● approved   3    24    22    0       42%     6m        18k         ▆▄▂▂           09-28 v2
incident.response    ● approved   1    3     2     1       100%    3h 10m    190k        ▁▂▁▄           09-30 v1
scoping.project      ◆ changed    1    7     5     0       29%     48m       540k        ▂▂▄▄           today v3 · re-approve at a terminal
intake.linear        ○ draft      —    —     —     —       —       —         —           —              never approved
   failed = ended in cancelled or a terminal failure state · human = runs with a park answered, an escalation decided or a takeover
```

- Child-only recipes (never a root) show `—` for run stats and say so; their numbers appear under their parents'
  per-state rows.
- STATUS is the registry's word (WF P2): approved, changed since approval (a pinned block or the file changed,
  `SND-RECIPE-006`), draft. Re-approval is `protected`; **(SO-21)** it is typed in the app (`ApproveRecipe`), and the CLI stays the fallback.
- **Validate file…** runs the validator on a chosen file in-process (`recipe validate`); **Import** registers a
  file as a draft (an `issue` kind command, X26).

**Inspector.** The states band of §4.2 with every open instance placed on it and the per-state numbers under it.

```
recipe/workflow.item   workflow.item @ 3f1c09a2   v7   schema 1   approved 10-02 by you                                       Esc  ✕
[Run evals E]  [Validate]  [Open file ↗]  [Versions 7 ▾]                                        30 days ▾  112 runs · 97 done · 6 failed · 9 cancelled

┌ STATES · open instances on them ─────────────────────────────────────────────────────────────────────────────────────────────────┐
│ intake ● ── produce ◐◐ ⇄ checks ◐ ── gate.verify.judge ── review ○ desk ── done                                                   │
│                ▌1 parked                 ▲1 escalated                                                                            │
│ per state      produce           checks           gate.verify.judge     review (desk)                                            │
│ median         52m               9m               —                     3h 20m (waiting on you)                                  │
│ parks          14 no-progress    2 budget         —                     —                                                        │
│ escalations    —                 —                11 below 0.80          —                                                        │
│ takeovers      6                 1                —                     —                                                        │
│ gate refusals  —                 —                11 of 108 (10%)        —                                                        │
│ loops          avg 1.6 of 3      —                                                                                               │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
┌ OUTCOMES 30 d ─────────────────────────────────┐ ┌ HUMAN TIME ─────────────────────────────┐ ┌ COST ───────────────────────────┐
│ done without you    81  ▮▮▮▮▮▮▮▮▮▮▮▮▮▮▮▮░░░░    │ │ parks answered      16 · median 2h 10m  │ │ tokens/run  310k (p50) 710k (p90)│
│ done after a park   12  ▮▮▮░░░░░░░░░░░░░░░░░    │ │ escalations decided 11 · median 42m     │ │ $/run       $1.84 (p50)          │
│ done after takeover  4  ▮░░░░░░░░░░░░░░░░░░░    │ │ takeovers            7 · median 25m     │ │ 30 d total  $214                 │
│ failed               6  ▮░░░░░░░░░░░░░░░░░░░    │ │ human rate          19% (last 4 wk ▂▄▆█)│ │ budget hits 2                    │
│ cancelled            9  ▮▮░░░░░░░░░░░░░░░░░░    │ └─────────────────────────────────────────┘ └─────────────────────────────────┘
└────────────────────────────────────────────────┘
┌ OPEN 4 ────────────────────────────────────────────────┐ ┌ EVALS 12/12 ✓ · last run today ──────────────────────────────────────┐
│ ITM-01JE7 produce r2/3    Claude Code  6m    [Open ▾]   │ │ ✓ 01 happy-path   ✓ 02 checks-fail-once   ✓ 03 budget-park   …      │
│ ITM-01JE9 checks          Codex        2m    [Open ▾]   │ │ recorded against v7 · 3 cases recorded from real runs (--record)      │
│ ITM-01JC8 ▌parked no-progress 12m   [⌂ Grant — terminal]│ └──────────────────────────────────────────────────────────────────────┘
│ ITM-01JB4 ▲escalated gate 2h56m     [⌂ Accept — terminal]│   blocks 7 pinned: step.produce v3 · gate.verify.judge v2 · …   definition  states 5 · transitions 8 · effects 3
└────────────────────────────────────────────────────────┘
```

- Per-state rows: median time in the state, parks by reason, escalations, takeovers, gate refusals (refused / evaluated),
  loop average. Every number is a count over ledger events for the window (E-24).
- Human time is the median between the park or escalation event and its answer; a takeover's time is until handback.
- Cost uses the scorer's price table (tokens × model price); when the table lacks a model the cell reads "tokens only".
- **Versions ▾** lists the registry's approved hashes with dates; choosing one re-scopes the numbers to runs under it.
- Data: `GET /workflow/recipes` (WF P2, with E-25's stats block), `GET /workflow/recipes/{id}` (the definition,
  evals, open instances), `recipe eval` (P3) recorded as a job (API P6).

### 4.4 Context (the lens stack)

The engine joins three stores: intent (Linear, Slack, Onyx), the product graph (Sindri's), and code (the file map
from the product map's `implements` edges, and the symbol index). The page shows one anchor through all three at
once: three bands on one vertical axis, with the cross-layer edges (`mentions` and `decides` downward, `implements`
and `tests` upward) drawn between the bands.

```
Context   map v13 current · draft v14 (46 proposals) · code index 12:21 ✕ · intent 2h behind      [Build draft]  [⌂ Promote — at a terminal]
[⌘/ feature:tabs.tab-manager                           ]  depth 2 ▾   lenses  ☑ intent  ☑ product  ☑ files  ☐ symbols  ☐ draft

┌ INTENT · Linear · Slack · Onyx ─────────────────────────────────────── 5 records · 2 decisions · 1 conflict ─────────────────┐
│  ENG-482 "Restore panes per window"  ·  PRD-tabs.md §3 (linear:doc)  ·  #tabs-design 09-30 (slack)  ·  ENG-301 ⚠ orphaned   │
│      │ decides                     │ mentions                      │ mentions                                                  │
│  ◇ decision "panes restore per window, not per workspace"  (supersedes ◇ "per workspace", 09-12)      conflict with PRD §3.2 ▸ │
└──────┼─────────────────────────────┼───────────────────────────────┼──────────────────────────────────────────────────────────┘
       ▼                             ▼                               ▼
┌ PRODUCT GRAPH · features · surfaces · actions · things · flows ──────── own scope 7 · affects 3 at depth ≤ 2 ──────────────────┐
│                          feature:layout                                                                                        │
│                 part-of ╱           ╲ part-of                                                                                  │
│   feature:layout.split-pane        ▣ feature:tabs.tab-manager ◀──── anchor                                                      │
│        │ writes                          ▲ reads        │ part-of                                                              │
│   thing:pane-layout ─────────────────────┘    action:tab-manager.restore-layout ──step-of──▶ flow:open-tab-split-resize  d1   │
│        ▲ writes                                                                                                                │
│   action:split-pane.split-vertical ──on──▶ surface:command/split-vertical                   feature:session.restore  d2 (dep)   │
└──────┬───────────────────────────────────────────┬──────────────────────────────────┬──────────────────────────────────────────┘
       ▲ implements                                ▲ implements · tests               ▲ implements (model-suggested 0.74)
┌ CODE · files (map v13) · symbols (index 12:21) ───────────────────── 6 files · 11 symbols · 1 feature with no code ───────────┐
│  src/layout/SplitPane.ts  #setOrientation #split      src/tabs/Manager.ts  #restoreLayout  ·  tests/tabs/restore.test.ts     │
│  src/layout/paneStore.ts  #write #read                src/session/restore.ts  ◇ suggested, unconfirmed                        │
│  calls  Manager.restoreLayout → paneStore.read → SplitPane.setOrientation                                                     │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

COVERAGE of the anchor's scope (7 nodes)   intent ▮▮▮▮▮░░ 5/7 cited   product ▮▮▮▮▮▮▮ 7/7 confirmed   code ▮▮▮▮▮▮░ 6/7 implemented
   gaps  surface:dialog/restore-layout  has a PRD mention but no code  ·  feature:session.restore  code only, no intent        [Open gaps]
```

Below the stack, in the right column and under it: the **node card** (kind, provenance and confidence, evidence,
counts, the cascade list with depths; Neighbors, Path to…, Query, Edit), the **map review queue** (CTX §8.7:
deterministic bulk accept, model proposals by impact with accept, reject, rename, move, skip; orphaned links with
repoint or drop) and **sources** (each adapter's state, counts and age; Sync all).

- **Lenses:** intent, product, files, symbols, draft. A lens off collapses its band to its header line. `symbols`
  reads "index not built" until CTX phase 9; `draft` switches the product band to the draft map (`--draft`).
- **Conflicts:** a decision that supersedes another, or contradicts a cited record, is marked on the product node
  it decides; the `▸` opens both quotes side by side.
- **Coverage:** for the anchor's own scope, the share of nodes with an intent citation, a confirmed (non-model)
  product entry, and an `implements` edge. The gaps line names the nodes and `[Open gaps]` filters the review queue
  to them.
- **Pinned to a scope** (amended 2026-10-16, R8-14; #258 U7): opened from a scoping stop or a scope viewer, the page
  draws the scope packet's frozen lens graph instead of querying an anchor: the header reads "as of the stop · map
  v13 · 3 anchors" with "◂ Back to the stop"; a **scope lens** switch (Code · Product · Related · History · All)
  picks which lens's found ids are drawn, each in its band, with an empty state per lens from its coverage row; the
  page is read-only; "live" redraws the same ids on the current map (E-32) and marks the ones gone since the stop.
- **The file-map view** (the `files` lens toggled to its tree form): the repository tree annotated with the feature
  keys each file implements and the intent counts behind them; unmapped directories and model-suggested links are
  marked, with accept and reject inline (each one `map_override` row).

```
FILE MAP · agentic-workflow @ 7c3e1a2 · map v13                      show  ☑ features  ☑ intent counts  ☐ unmapped only
src/
├─ layout/                           feature:layout                                 ENG 4 · PRD 2
│  ├─ SplitPane.ts                   feature:layout.split-pane  · .orientation        ENG-482 · PRD-tabs §3
│  ├─ paneStore.ts                   thing:pane-layout                                PRD-tabs §3
│  └─ Resize.ts                      feature:layout.split-pane.resize                 —  ⚠ no intent
├─ tabs/
│  ├─ Manager.ts                     feature:tabs.tab-manager · action:…restore-layout  ENG-482 · #tabs-design
│  └─ Strip.tsx                      surface:screen/tab-strip                          PRD-tabs §2
├─ session/
│  └─ restore.ts                     ◇ feature:session.restore (suggested 0.74)        —            [Accept] [Reject]
└─ billing/                          ░ unmapped · 14 files                                          [Propose features]
tests/tabs/restore.test.ts           tests → action:tab-manager.restore-layout
```

- Data: `GET /sindri/context/node`, `neighbors`, `path`, `query` (CTX phase 1, 3), `map`, `map/review` (phase 4),
  sources (phase 1, 5, 6), the code index (phase 9), plus E-26 (a `lenses` query that returns the three bands and
  the coverage for an anchor in one call) and E-27 (the file map).

### 4.5 Agents

Sessions grouped under the recipe and state they run; each row offers the actions the engine allows in that state.

```
Agents   4 live · 1 with you · 2 waiting on you · $4.12 today                                                [30 days ▾]  [Budgets]
┌ workflow.item › produce  (step.produce · agent Step) ─────────────────────────────────────────────────────────────────────────┐
│ ● snd-01JE7.2   Claude Code   ITM-01JE7 Sync status pill       r2/3   84k ▮▮▮▮░░░░ 200k   6m     [Attach O] [Take over T] [Request R] [Cancel X]│
│ ● snd-01JD9.1   Claude Code   ITM-01JD9 Judge outbox drain     r1/3   22k ▮░░░░░░░ 200k   1m     [Attach O] [Take over T] [Request R] [Cancel X]│
│ ◐ snd-01JA1.3   Claude Code   ITM-01JA1 Onyx secret pointers   —      41k            with you 25m [Attach O] [Hand back B]                      │
├ workflow.item › checks  (step.checks · tool Step, no terminal) ───────────────────────────────────────────────────────────────┤
│ ● snd-01JE9.1   Codex         ITM-01JE9 Replay on reconnect    1/1    12k ▮░░░░░░░ 200k   2m     [Open ▾] [Take over T] [Cancel X]            │
├ scoping.project › challenge  (step.scope.challenge · model, no session) ───────────────────────────────────────────────────────┤
│ ◌ model run     challenger    PRJ-tabs Tab manager v2          r2/3   84k ▮▮░░░░░░ 600k   46m    [Open instance ▸] [Take over T] [Cancel X]   │
├ waiting on you ────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ ▌ no session    —             ITM-01JC8 Background sync        parked no-progress · 12m        [⌂ Grant rounds — terminal] [Open in Claude Code ▾] [Cancel X]│
│ ▲ no session    —             ITM-01JB4 Context map review     escalated gate 0.62 · 2h56m     [⌂ Accept — terminal] [Reject] [Open in Codex ▾]          │
└────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
  ● live, nobody attached   ◐ with you   ◌ model call, no terminal   ▌ parked   ▲ escalated      Attach joins the tmux session; the first keystroke is a takeover

┌ BY AGENT 30 d ─────────────────────────────────────────────────────────────┐ ┌ BUDGET today ─────────────────────────────────┐
│ AGENT         SESSIONS  DONE  TAKEN OVER  MEDIAN   TOKENS/SESSION  $/SESSION │ │ Claude Code  ▮▮▮▮▮▮░░░░  $3.10 / $8.00         │
│ Claude Code   96        88    9%          48m      290k            $1.70    │ │ Codex        ▮▮▮░░░░░░░  $1.02 / $4.00         │
│ Codex         41        39    2%          11m      40k             $0.22    │ │ Cursor       ░░░░░░░░░░  $0.00 / $2.00         │
│ Cursor        3         2     33%         1h 05m   180k            $0.98    │ │ total        ▮▮▮▮░░░░░░  $4.12 / $14  soft 80% │
│ challenger    7         7     0%          14m      110k            $0.41    │ └────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────────────────────────────┘
```

**The action sets**, from the engine's rules (WF §6.3, API §4.2, rev 7 §6.2 and §7):

| Row state | Actions | Class |
|---|---|---|
| Live agent Step, nobody attached (`●`) | Attach (joins the tmux session; the first keystroke is a takeover), Take over, File a request, Cancel | desk, desk, desk, destructive |
| Tool or model Step, no terminal (`◌` or `●` without a tmux session) | Open instance, Take over, Cancel | — |
| With you (`◐`) | Attach, Hand back | desk |
| Parked (`▌`) | The park's answer (grant rounds, grant tokens, text, resume; `protected` ones as the CLI), Open in an agent (rev 7 §6.1), Cancel | per rev 7 §4.4 |
| Escalated (`▲`) | Accept (`protected`, CLI) or Reject (desk), Open in an agent, Cancel | per rev 7 §4.4 |

- Budgets: per agent target per day from the profile (`agents.<target>.dailyBudget`, E-28), with the soft warning at
  80 % and the hard stop at 100 % (new sessions for that target are refused with `SND-LAUNCH-010`, E-28); the bars
  use the attention colour at the soft line and `Error` at the hard one, always with the word.
- Data: the collector (API §11) for sessions, `GET /launch/targets` (P10a), the engine's instance states (P11), the
  scorer's price table, E-28.

### 4.6 System

**Dependencies as a graph**, health on the edges, and the impact of what is failing now.

```
System   ◐ Degraded · code index failed · Onyx 2h behind · engine 4 open · views 2s ago
┌ DEPENDENCIES  what depends on what · health on the edge ─────────────────────────────────────┐ ┌ IMPACT of the failures now ───────────────────┐
│                                                                                               │ │ ✕ code index (nightly 03:00, SND-INDEX-012)     │
│   Linear API ●──────┐                 ┌──● Ollama 11434 (nomic-embed)                         │ │   → context: symbols lens stale since 10-12     │
│   Slack ●───────┐   │                 │                                                       │ │   → scoping: `code` source reads the old index  │
│   Onyx 3000 ◐───┴───┴──▶ CONTEXT ◀────┴──✕ code index ◀── graphify ● · git ●                  │ │   → builder.feature: input.context.retrieve     │
│                   2h behind  │            12:21 failed                                        │ │     degraded (cited results only)               │
│                              ▼ input.context.retrieve                                         │ │   nothing parks; 2 open instances run degraded  │
│   recipes ● ──▶ ENGINE ◀──● Temporal dev 7233 (12 ms) ──● temporal.db                          │ │                                      [Run again]│
│        7 approved   │ ╲                                                                       │ │ ◐ Onyx sync 2h behind                           │
│                     │  ╲── mirrors ──▶ ledger.db ● wal · head 4236 · views 4236 (0 behind)     │ │   → intent lens: Slack and Linear comments may  │
│                     ▼                                                                         │ │     be stale; native Linear adapter unaffected  │
│   tmux ● 3 ──▶ SESSIONS ◀── Warp ●  · claude ● 2.1.3 · codex ● · cursor ○ not found           │ │                                       [Sync now]│
│                     │                                                                         │ │ ○ cursor not found                              │
│                     ▼                                                                         │ │   → launch target hidden in every Open ▾ menu   │
│   API 127.0.0.1:47811 ● v1 ──▶ UI (this app) · CLI · MCP /sindri/mcp (212 queries / 7 d)      │ │   → no run affected                             │
└───────────────────────────────────────────────────────────────────────────────────────────────┘ └─────────────────────────────────────────────────┘
   ● ok   ◐ degraded   ✕ failed   ○ absent      Enter on a node opens its doctor check and fix · the graph is deps.yaml plus the profile's sources
```

- The graph's nodes are the doctor's checks (`docs/csharp/sindri.md` "Doctor"), the profile's sources (CTX §8.8) and
  the launch targets (`GET /launch/targets`); the edges are declared once in `deps.yaml` (package, cli, db, file
  edges) and in a new `consumers` list per doctor check (E-29). The impact panel is the transitive closure of the
  failing node's consumers, each with its own sentence and, where one exists, the fix control.
- Layout: a fixed layered drawing (sources left, engines centre, surfaces bottom), not a force graph; nodes are
  focusable in reading order (rev 7 §11.2).

**Engine execution in depth.** The worker, every open instance with what it is waiting on, the pending work, and the
mirror lag; a selected instance shows its history with the ledger events it mirrored into.

```
┌ ENGINE · Temporal dev 7233 · namespace sindri ──────────────────────────────────────────────────────────────────────────────────────────┐
│ worker  1 · sindri-48211 · task queue sindri · polling · activities 2 running · 0 backlog · last heartbeat 1s      mirror lag  0 events (0 ms)│
│ INSTANCE          RECIPE@HASH           STATE                 WAITING ON                         TIMERS                 RETRIES  HISTORY  │
│ ITM-01JE7         workflow.item@3f1c09  produce r2/3          activity step.produce (async, 6m)  timeout 1d 23h        0        412 ev   │
│ ITM-01JE9         workflow.item@3f1c09  checks                activity step.checks (2m)          produce timeout 12:30 1 (exit 1) 88 ev  │
│ ITM-01JC8         workflow.item@3f1c09  ▌parked no-progress   signal answer (grant)              none                  —        640 ev   │
│ ITM-01JB4         workflow.item@3f1c09  ▲escalated gate       signal answer (accept/reject)      escalate TO fired     —        301 ev   │
│ PRJ-tabs          workflow.project@a8c2 scoping → child       child PRJ-tabs/scoping/1           parks in 1h 14m       0        57 ev    │
│ INC-14            incident.response@5d9 mitigate r1/3         child ITM-01JG2 · activity         escalates in 36m      0        120 ev   │
│ pending  updates 0 · signals 0 · stuck > 5 min: 0 · continue-as-new: 0           dev server up 3d 4h · temporal.db 48 MB      [Open Temporal UI ↗]│
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
┌ ITM-01JE9 · history (last 8 of 88) ──────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ 12:26:10  ActivityTaskScheduled   step.checks      attempt 2 · retry after exit 1 ("checks runner: ECONNRESET") · backoff 10 s            │
│ 12:26:00  ActivityTaskFailed      step.checks      attempt 1 · exit 1                                                                     │
│ 12:25:50  ActivityTaskStarted     step.checks      worker sindri-48211                                                                    │
│ 12:25:49  TimerStarted            produce-timeout  fires 12:30                                        ledger ↔ 4231 step.round-started  │
│ 12:24:01  WorkflowExecutionUpdateAccepted  takeover? no · complete (run 1.1, outcome done)            ledger ↔ 4228 step.completed       │
│ …                                                                                                                        [Full history ↗]│
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- "Stuck" is an instance whose only pending work is a signal or update older than 5 minutes with no timer; it is a
  `degraded` contribution to `health.word` (rev 7 E-14).
- The services table and the process · views · profile strip of the revision-7 `services-health` mockup stay below,
  unchanged.
- Data: E-30, a `GET /engine` family served by Sindri from the Temporal SDK's describe and history queries (R8-12):
  worker, instances with pending activities, timers, signals and updates, retry attempts and the last error, history
  pages with the ledger sequence each event mirrored into (the mirror writes the Temporal event id on the ledger row,
  WF §5.6). "Open Temporal UI" is a link to the dev server's loopback UI (WF §6.1), shown only when it is up.

### 4.7 The item sheet

An item is a child instance, so the sheet is the Workflows frame of §4.2 at sheet width. The live run is the current
state expanded; the trace is that state's activities; rev 7's status card and "Properties" become the subject line
and the facts.

```
item/ITM-01JE7   Sync status pill                        workflow.item@3f1c09a2 · v7 · produce r2/3 · since 12:14 (12m)       Esc  ✕
subject  packet task t3 of PRJ-sync (ws-1 UI) · repo agentic-workflow · Linear ENG-511 · feature:sync.status-pill    [Open in Linear ↗] [Open in Context ▸]

┌ STATES ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ intake ● ── ▣ produce ◐ r2/3 ⇄ checks ● r1 failed ── gate.verify.judge ○ ≥ 0.80 ── review ○ desk ── done                       │
│ ▣ produce · step.produce · ● Claude Code in Warp · snd-01JE7.2 · since 12:20 (6m) · 84k / 200k tokens                          │
│   next   Produce the Step output (round 2/3). Round 1's checks found 2 findings: second ack returns 500; route missing from     │
│          the catalog.                                                                                                          │
│   trace  ● step.produce r1   done     12:23  4 files · +182 −9                                                                  │
│          ● step.checks       failed   12:25  2 findings                                                                         │
│          ◐ step.produce r2   running  12:26  attempt 1                                                                          │
│   timeout parks in 1d 23h · commands  takeover, request, cancel                      [Attach O]  [Take over T]  [Request R]  [Cancel X]│
└────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
┌ FACTS ──────────────────────────────────────────────────────────────┐ ┌ CHILDREN 0 ──────────────────────────────────────────────┐
│ fact:task      ● t3 from packet 3f1c09 · acceptance 2 visible · 1 held│ │ none; workflow.item declares no children                 │
│ fact:spec      ● spec.md sha256:1c0e…                       [Open ↗] │ └──────────────────────────────────────────────────────────┘
│ fact:output    ◐ round 1 · 4 files · round 2 running          [Diff ↗]│   Timeline 6   Decisions 0   Requests 0   Context 12
│ fact:findings  ● round 1 · 2 findings                        [View ▸] │
│ fact:evidence  ○ after checks pass                                     │
└───────────────────────────────────────────────────────────────────────┘
```

- The launch block of rev 7 §6 (Open ▾ with targets and reasons, Sent to Warp, Attach, Resume, Open another session)
  is the expanded state's control row when the state is an agent Step; the pre-engine form (before WF P4) shows the
  launch block alone with rev 7 U6a's "Sent to Warp … ago" line.
- Rev 7's park and escalation sheets (§4.4) are this frame with the engine-owned badge expanded in the band.
- Create and Close item (U2) are unchanged; Close's version check (`SND-ITEM-409`) renders in the subject line.

### 4.8 Every screen's state matrix

Rev 7 §4.9 applies to every new page: not running, connecting, version mismatch, unreadable, stale, rebuilding and
stalled render the same banners and placeholders. Two additions:

| State | Where | Rendering |
|---|---|---|
| **Not served yet** | A panel whose API phase hasn't landed (§8) | The panel frame with its title and "arrives with API Pn" in `InkSubtle`, as the U0 shell does today |
| **Index not built** | Context's `symbols` lens, the file map's symbol column | The lens toggle disabled with the words; the band's header reads "symbols · index not built (CTX phase 9)" |

### 4.9 Mockups

The HTML mockups live in `2026-10-13-sindri-ui-screens-design/mockups/`, one self-contained file per screen with a
1280-wide PNG beside it, all rendering the one scenario in `fixture.md` (ids, titles, counts, the health word, the
glyph vocabulary and the protected-action label). They are design references, not the implementation; where a
mockup and this text differ, the text wins, and `fixture.md` wins on data. They passed an `/impeccable` critique
(`impeccable-critique.md`, 25/40 on the first render) and the fix passes in `fixes.md`: one shell in every file (the
top bar fits 1280, one health word, no side stripes, the attention band only while needs-you is non-zero, one glyph
set, no em dashes, an 11 px floor), then per page the layout, harden, clarify, onboard, typeset, quieter, distill,
animate (hover and press only), adapt and optimize passes. The owner's calls during that pass: worst-verdict health
word, stripes removed, read-first focus with Cancel behind a confirm, 760 px for every instance sheet (R8-13).

| File | Shows |
|---|---|
| `home.html` | §4.1: Home with the Workflows strip |
| `workflows.html` | §4.2: the list, grouped by recipe |
| `workflow-project.html` | §4.2: the instance frame for PRJ-sync (build, 4 of 9 children) |
| `workflow-incident.html` | §4.2: the same frame for INC-14, with the Cancel confirm drawn |
| `item-sheet.html` | §4.7: ITM-01JE7 in the frame at 760, over the Work list |
| `recipes.html`, `recipe-inspector.html` | §4.3: the list with its numbers; the inspector for workflow.item |
| `agents.html` | §4.5: sessions under their recipe state, the action menu open on one row, budgets |
| `context.html`, `context-filemap.html` | §4.4: the lens stack with computed cross-band edges and the undo toast; the file map as a tree |
| `system.html` | §4.6: the dependency graph with the impact panel, the engine table and a history |
| `prototype.html` (+ `prototype-*.png`) | A clickable prototype of the whole app over the same fixture: the seven pages, the G chords and hash deep links, every sheet, every §4.4 answer (protected ones reveal the command), the launch flow, Take over and Hand back, Cancel behind its confirm, map review with the undo toast, jobs that stream, the palette and the `?` sheet, and a controls panel for the shared states, reduced motion, text scale and the large fixture. Scripted click-throughs with assertions live beside the build tooling; the PNGs are its page captures |

## 5. The recipe-driven rendering contract

What the app needs from a recipe and the engine so that §4.2, §4.3, §4.5 and §4.7 render any recipe.

### 5.1 Path order of states

The states band shows a recipe's states left to right in **path order**: a depth-first walk of the transition graph
from the recipe's initial state, taking the first transition listed from each state as the main path (the recipe's transition
order, WF §3.5, is the tie-break), with loops (`⇄`) drawn between the two states they connect and branches folded under the
state they leave from (a `⤷` glyph with the branch's first state, expanded on focus). Terminal states come last. The
engine computes and serves the order (E-20's `states[].order` and `loops`), so the app never parses YAML. A recipe
whose graph has more than one initial path is ordered by the first transition listed, and the band says "ordered by
transition list" in its header.

### 5.2 Fact types and viewers

Recipe inputs carry `{ key, label?, type? }` with `type: text | file | item` today (WF §3.2, X34). This revision adds
**output declarations** and more types, as a WF schema edit (E-22):

| Fact type | Viewer | Summary line | Controls |
|---|---|---|---|
| `text` | inline text | the text, one line | Copy |
| `file` | file reference | name, size, hash | Open ↗ |
| `item` | item row | id, title, state | Open |
| `link` | URL | host and title | Open ↗ |
| `scope-map` | §4.2's scope-map table with the graph and items columns | surfaces, mapped / new / unmapped counts, backtest | View, Export ↗ |
| `packet` | tasks by workstream with `PKT-*` results | tasks, workstreams, checks n/m | View, Freeze (desk) |
| `evidence` | the evidence pack list (rev 7 U7, Evidence) | packs produced / expected | View |
| `decision` | the decision with its quote | the wording, who, when | Open record ↗ |
| `payload` | a JSON tree (scrubbed) | the first three keys | View |

A type the app doesn't know renders as `payload`. A recipe lists `outputs: [{ key, label?, type?, from: <state> }]`;
`from` places the fact under the state that produces it, which the band uses for the ◐ mark.

### 5.3 Desk steps

A `desk` state's expanded row shows the engine's `next` text, the outputs the state expects (from §5.2) each with its
input control (file picker, text, link), the gates that run on `complete` (named, with a one-line description from
the block descriptor), and the complete command in rev 7 §7's form: a desk action calls `workflow complete` with the
outputs; a `protected` one shows the CLI. The export-then-import pattern (scope map out, design in, packet frozen) is
two desk states in `workflow.project`, not UI logic.

### 5.4 Subjects

An instance's **subject** is its first declared input. A subject kind has an adapter in the app for the header line
and the list's SUBJECT cell: `item` (id and title, from the queue), `linear-project` (team and name, the PRD document
if the project has one, `Open in Linear`), `webhook` (source, id, received at), `file` (name), `text` (first line); `packet` and `payload` render the payload's id and title, else the key.
Kinds are the API's `inputs[].kind` (E-22); an unknown kind renders the key.

### 5.5 Children

A state that declares `child` (WF §3.4) or runs `step.children` groups its instances in the CHILDREN panel under that
state's name. When the children come from a packet (the state's input is a `packet` fact), the group is sub-grouped
by the packet's workstreams, in the packet's order.

## 6. Edits this revision asks of the other specs

Numbered on from rev 7's E-1…E-19.

| # | Spec | Edit | Needed by |
|---|---|---|---|
| E-20 | API | `GET /workflow/instances?root=true&recipe=&subject=` and `GET /workflow/instances/{address}`: the recipe id and hash, version, state, `since`, the engine-owned badge, `states[]` with `order`, `glyph`, `loops`, `desk`, `children` progress, and the expanded state's status fields; `facts[]`; `children[]` grouped | U8, U3's strip |
| E-21 | WF | Recipe top level gains `urgency: incident \| triage \| work` (default `work`) for the strip's order and the list's group order | U3 |
| E-22 | WF, API | Recipe `outputs` (§5.2) and the added fact types; `inputs[].kind` for subjects (§5.4); `recipe show` and `GET /workflow/recipes/{id}` return both | U8 |
| E-23 | API | The WF §6.4 status layout as structured fields on the instance view (`next`, `answer`, `inputs`, `timeout`, `budget`, `paused`, `commands`), alongside the text form the CLI prints | U8, U5 |
| E-24 | API | `GET /workflow/recipes/{id}/stats?window=` : runs, done, failed, cancelled, human rate, medians, per-state parks by reason, escalations, takeovers, gate refusals, loop averages, tokens p50/p90, 4-week buckets; computed from ledger events, cached per window | U10 |
| E-25 | API | `GET /workflow/recipes` carries the stats block of E-24 at the default window and the registry status word | U10 |
| E-26 | CTX | `GET /sindri/context/lenses?anchor=&depth=&draft=` : the three bands for one anchor (intent records and decisions with their cross edges, the product neighborhood and cascade, the files and symbols with `implements` and `tests`), conflicts, and coverage. **Amended (owner, 2026-10-16; #258 C1, C10):** `anchor` repeats (at most 20; one anchor keeps today's shape), each item carries `anchors[]` (which anchors reached it) and `coverage` gains `byAnchor`; a set answers `LensesView` schema 2 (`anchors[]` in place of `anchor`), more than 20 is `SND-CONTEXT-006`, and anchors not held (or out of a session's scope) are listed in `missingAnchors[]`; `mapVersion=` reads a retained or current version, and a version not retained answers its own new code (not 003) | U9 |
| E-27 | CTX | `GET /sindri/context/filemap?repo=&path=` : the tree with feature keys, intent counts, unmapped directories and suggested links; accept and reject call `map/review` | U9 |
| E-28 | API, profile | `agents.<target>.dailyBudget` in the profile; the collector reports today's spend per target; `SND-LAUNCH-010` refuses a new session for a target at its hard limit | U11 |
| E-29 | doctor, deps | Each doctor check declares `consumers` (the checks and surfaces that depend on it); `GET /status` returns the dependency graph with health and the impact closure | U12 |
| E-30 | API, WF | `GET /engine`, `GET /engine/instances`, `GET /engine/instances/{address}/history?page=` served from the Temporal SDK; the ledger mirror records the Temporal event id on each row so history lines link to ledger sequences | U12 |
| E-31 | CTX, WF | A scoping run's new surfaces are written as draft proposals to the next map version with the run as evidence (R8-11); the scope-map fact carries each surface's graph status | U8, U9 |
| E-32 | CTX | **New (owner, 2026-10-16; #258 C15):** `GET /sindri/context/subgraph?nodes=&mapVersion=&depth=` (at most 20 keys) and `QUERY /sindri/context/subgraph` `{ nodes[], mapVersion?, depth? }` (the HTTP QUERY method; `new HttpMethod("QUERY")`) (at most 1000 keys): `SubgraphView`, exactly the given node keys (`code:<path>` keys included) and the edges among them, placed in the three bands (each capped at 100, `truncated`) with E-26's parts, at a given map version or the current one, plus `missing[]` (keys the version doesn't hold, or outside a session's scope: one answer); `depth` (0–4, default 0) widens around the set | U9 |

## 7. Reference patterns borrowed (ideas only)

| Pattern | From | Where it landed |
|---|---|---|
| Stage columns with a one-line "what runs here" subtitle | SuperPlane board | §4.2's expanded state line; the list's group headers |
| Status chips on cards (agent question, review n, merged n) | SuperPlane | the stage cells' badges and the NOW column |
| Run view: summary facts, then a step chain with per-step status | SuperPlane automation view | §4.7's trace and §4.6's history |
| Properties column on an issue | Paperclip | §4.7's subject line and facts |
| "Live issue runs" panel with Stop and Open run | Paperclip | the expanded state's control row |
| Trace list with status words in mono | Paperclip | §4.7's trace |
| Budget table with bars and a total row | Paperclip | §4.5's budget panel |
| "N live" beside the thing that is live | Paperclip sidebar | the Agents tab count and the ● rows |

## 8. Phases

Rev 7's U0, U1, U2, U4, U6a and U6b stand as merged or in flight. U3, U5 and U7 change; U8–U12 are new. Each phase
follows rev 7 §18's recipe (view-model test first, headless frames, the fake `ISindriApi` on golden JSON, proof in the
running app) and §18.2's proof methods.

| # | Goal | Files (main ones) | Size | Depends on | Observable signal |
|---|---|---|---|---|---|
| **U3'** | Needs you, Home with the **Workflows strip**, the pulse, badges, tray (rev 7 U3 plus §4.1) | `Views/Home*`, `Views/NeedsYou*` | M | U2 + API P4 + E-20 (root list) | The strip shows a running incident first; five-second test passes with the strip on the `--large` fixture |
| **U8** | **Workflows** list and the instance frame (§4.2, §5), the item sheet re-based on it (§4.7), fact viewers for text, file, item, link, payload | `Views/Workflows*`, `Views/Instance*`, `ViewModels/Facts/*` | L | U2 + WF P4 + API P11 + E-20, E-22, E-23 | Two dev recipes (a three-state triage and a desk round trip) render in the same frame; a desk complete with a file output resumes the instance; the item sheet's frames match U2's golden rows |
| **U5'** | Engine surfaces inside the frame (rev 7 U5: answers, mirrored parks, Start, Complete, Take over, Hand back). **Start form landed first** (`Views/Start*`, `N`; `docs/csharp/sindri-ui.md` "U5': the Start form"); the rest follows | `Views/Park*`, `Views/Answer*`, `Views/Start*` | M | U8 + WF P5, E-9, E-12, E-13 | rev 7 U5's signals, in the band |
| **U9** | **Context lens stack** and the file map (§4.4), replacing rev 7 U7. **Amended (owner, 2026-10-16; R8-14, #258 U7):** the page opens on an **anchor set** (one or more node keys) plus an optional **pinned map version** and an optional found-id filter, not only on one anchor; the header names the set and the version ("as of the stop · map v13 · 3 anchors" or "live · map v15 · 3 anchors"); a pinned page draws the packet's frozen lens graph (`lenses.json`, read through #258 API9) with no map call, is read-only (Build draft, Promote, the draft band, Edit and map review read "live only") and carries "◂ Back to the stop"; the live toggle redraws the same ids through E-32's POST at the current map and marks `missing[]` ids "gone since the stop" in words; a scope lens switch sits beside the band toggles; with no set it is today's single-anchor stack | `Views/Context*`, `Views/FileMap*` | L | U8 + CTX phases 3, 4 + E-26, E-27; E-32 for the pinned set | On the fixture map, the stack shows a decision conflict, a model-suggested file link accepted from the tree appears in the next review, and coverage counts match `context map status`; opened on a fixture scope packet, the header reads "as of the stop · map vN · 2 anchors", each scope lens draws its found ids from the packet, and the live toggle redraws at the current map and marks a removed node |
| **U10** | **Recipes** list with stats and the inspector (§4.3) | `Views/Recipes*` | M | U8 + WF P2, P3 + E-24, E-25 | The inspector places the fixture's open instances on the band; stats equal a SQL count over the fixture ledger; Run evals streams as a job |
| **U11** | **Agents** (§4.5) with budgets | `Views/Agents*`, `Platform/IDockBadge.cs` | M | U8 + API §11 collector + P10a + E-28 | A session started by the prover appears under its recipe state within 2 s; Attach on it brings Warp forward; a target at its hard limit is refused with `SND-LAUNCH-010` and the bar reads it. U11a shipped the sessions slice: the page, the session sheet, the tab and tray counts; Focus send is U11a-f (after CO-4a) and the preview is CO-7c |
| **U12** | **System**: the dependency graph, impact, engine in depth (§4.6) | `Views/System*` | M | U4 + E-29, E-30 | Killing the code index's nightly job turns its node ✕ and lists three consumers; the engine table shows the prover's stalled instance as stuck within 5 min; a history line opens its ledger sequence |
| **U13** | Fact viewers for `scope-map`, `packet`, `evidence`, `decision`; the scoping round trip end to end | `ViewModels/Facts/*` | M | U8 + the scoping recipe spec + E-31 + `aw-packets` as a block | On the fixture project: scope map with ▣/◇/░ columns, export, import of a packet with one failing `PKT-SCOPE-002`, fix, freeze, builder handoff creates the items under CHILDREN |

### 8.1 Where each edit lands

| Edit | Lands in | Needed by |
|---|---|---|
| E-20, E-23 | API P11 (engine item states) | U3', U8 |
| E-21, E-22 | WF P2 (schema), surfaced by API P11 | U8 |
| E-24, E-25 | an API phase after P11 ("P12 stats") | U10 |
| E-26, E-27 | CTX phase 4 (map review) extends to lenses; the file map in CTX phase 4 (file level) and 9 (symbols) | U9 |
| E-28 | API P10a (targets) + the profile schema | U11 |
| E-29 | S1's doctor + `scripts/check-deps.mjs` (consumers from `deps.yaml`) | U12 |
| E-30 | WF P4 (mirror writes the event id) + an API phase ("P13 engine") | U12 |
| E-31 | the scoping recipe spec + CTX phase 4 | U13 |
| E-26's anchor set and `mapVersion`, E-32 | CTX S-LB (#258 C1, C10, C15) | U9 |

### 8.2 How agents prove these phases

Rev 7 §18.2 applies. Additions: the fixture script gains two dev recipes (`recipes/dev/triage-three-state.yaml`,
`recipes/dev/desk-round-trip.yaml`) and a fixture map (`tests/Aw.Context.Tests/Fixtures/map-tabs.json`) with one
conflict, one orphan and one suggested link; the five-second test asks a fifth question on Workflows ("which
workflow is furthest along and what is it waiting on?"); the System proof uses `sqlite3` to stop the index job's
last-run row, as the stall proof does for views.

## 9. Parallel delivery with an agent team

For `/specToProvenPR`, as rev 7 §19.

### Dependency graph and waves

```
U3' ──┐
U8  ──┼──▶ U5' ──▶ U13
      ├──▶ U9
      ├──▶ U10
      ├──▶ U11
U4  ──┴──▶ U12
```

| Wave | Lanes (parallel) | Waits on |
|---|---|---|
| 1 | U3' · U8 | API P4, P11; WF P4; E-20…E-23 |
| 2 | U5' · U9 · U10 · U11 · U12 | U8 merged; CTX 3–4; WF P2–P5; API collector, P10a; E-24…E-30 |
| 3 | U13 | U5'; the scoping recipe spec; E-31 |

### The contract unit

Before wave 1, one unit lands the **golden JSON** for E-20, E-22 and E-23 (API P11's integration tests write them;
`SindriUiJsonContext` deserializes them in the UI's unit lane, rev 7 E-17) and the two dev recipes. Every lane builds
against the fake `ISindriApi` serving those files.

### Units inside a phase

Each phase is three units in order: view models and their tests against the fake (red, then green); views with
headless frames; the proof script extension and the OS screenshots. U8 adds a fourth, the fact-viewer registry, which
U13 extends.

### The team

| Role | Owns | Lanes |
|---|---|---|
| Lead (the main session) | the contract unit, merges, the five-second test, this spec's §6 edits filed on the other specs | — |
| UI builder A | U8, then U5' | 1, 2 |
| UI builder B | U3', then U9 | 1, 2 |
| UI builder C | U10, then U11 | 2 |
| UI builder D | U12 | 2 |
| Prover | the fixture script, dev recipes, fixture map, screenshots, the reviews' evidence | every wave |

### Hotspots (one line per unit, appended at anchors)

- `Aw.Sindri.Ui/Views/Shell/ShellWindow.axaml` (navigation): U3', U8, U9, U10, U11, U12 each add a page; the lead lands
  the seven tabs and placeholders in the contract unit so the lanes only fill them.
- `Aw.Sindri.Ui/Api/ISindriApi.cs`: one method per route; each lane appends its own, in route order.
- `Aw.Sindri.Ui/ViewModels/Facts/FactViewerRegistry.cs`: U8 creates, U13 extends.
- `scripts/sindri-ui-fixture.sh`: every lane appends a section under its phase anchor.
- `docs/csharp/sindri-ui.md`: each phase adds its section at its anchor.

### Done

A phase is done when its observable signal in §8 is shown on the owner's Mac through `sindri-prove.sh`, its review
loop reached zero blocking and zero issue findings, and its section in `docs/csharp/sindri-ui.md` names the views,
routes, codes and proof recipe.

## 10. Open questions

| # | Question | Owner | Default until answered |
|---|---|---|---|
| O1 | Price table for cost cells: the scorer's today, or a profile table per model? | scorer / profile | the scorer's; "tokens only" when a model is missing |
| O2 | Does `GET /engine` expose Temporal's raw event names, or Sindri's own vocabulary? | API | raw names in the history panel, Sindri's words everywhere else |
| O3 | Daily budget per target, or per recipe as well? | profile | per target only (E-28); per recipe stays a budget in the recipe (WF §3.7) |
| O4 | The file map for a repo with no product map: propose features from directories (`[Propose features]`) through the extractor, or leave unmapped until the first `map build`? | CTX | unmapped; the button runs `map build` scoped to the directory |
