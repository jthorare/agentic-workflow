# Sindri UI app design: the control plane's views, actions and launching sessions

Status:
- **Revision 7 amendment (owner, 2026-10-12; PR #197):** §4.5's queue has API §3.6's four groups (`waitingOnYou`,
  `withYou`, `running`, `queued`), and a parked item shows in Waiting on you with a "parked" badge from its state; §4.6's
  item detail shows the item's `body` (scrubbed) and `link` from `GET /items/{itemId}`; §12.4 lists the destructive
  confirm's copy (§8, E-8): "<label> <id>?" with **Confirm** and **Keep**. Nothing else changes.
- **Revision 7 amendment (owner, 2026-10-12; PR #193):** §4.1's command lifecycle also covers the palette's rows: a
  ready command run from the palette keeps the palette open and its row shows the lifecycle.
- **Revision 8 note (owner session, 2026-10-13):** the app's contents were re-evaluated around five questions. `2026-10-13-sindri-ui-screens-design.md` supersedes this spec's §3, §4.2–§4.8, §10, §16, §18 and §19 with seven pages (Home · Workflows · Work · Recipes · Context · Agents · System), a recipe-driven instance frame, the context lens stack and phases U3', U5', U8–U13. Everything else here stands and is referenced from there.
- **Revision 7 amendment (owner, 2026-10-12; PR #184):** §12.4 lists two more app-owned sentences, "On in your system
  settings" (Settings, while the OS reduces motion) and "Looks again every 2 s" (the Not running card); §15's deps cycle
  row is decided (a `DEPS-CYCLE-001` exception). Lane decisions relayed by the orchestrator (2026-10-12): §7 and §18.1
  say WF P2 registers every engine catalog row, the desk and pause rows pending until WF P4/P5 flip them to ready and the
  protected rows (`answer-grant`, `answer-grant-tokens`, `answer-accept`; **(SO-20)** `ApproveRecipe` is an app confirmation) `cli-only:typed-confirmation`
  from the start, never app buttons, and the engine's queries stay off the
  catalog; §4.4, §7 and E-12 name `answer-grant-tokens` beside `answer-grant` (API §2.1, X30); §4.1's Unreadable row also
  covers a Sindri refusal after one token re-read and retry. Owner decision F6 (2026-10-12, review run 4): §8's key
  table lists `F6` / `Shift+F6` with `⌘⌥→` / `⌘⌥←` as the macOS alternative (and `fn+F6`), and no `Ctrl+F6`. Nothing
  else changes.
- **Revision 7 note (owner, 2026-10-11; README X55):** tmux owns every agent session and Warp attaches to it, so
  **Attach joins the live agent** and the human can type into whatever Step is running; the earlier "Sindri can't see
  inside Warp" limit in §6.2 and §6.6 no longer applies, and the `process` target kind is retired (API §8.1 rev 8).
  The screens, states and controls of revision 6 are otherwise unchanged; §6.2 and §6.4 carry the amended rows.
- **Approved by the owner on 2026-10-11** (revision 6).
- Draft, 2026-10-11; revision 6 after five `/autoplan` passes (pass 1: five lenses NEEDS_WORK, no CRITICAL; pass 2:
  product, design and devex PASS, one shared HIGH fixed; passes 3, 4 and 5: all five PASS). §17 lists
  every finding and its resolution.
- The two taste calls DESIGN.md and the specs don't settle were made by the owner on 2026-10-11 from three mocked
  variants (`shotgun/contact-sheet.html`): the **single-glance home** layout (§3.1) and the attention colour
  **`#f2994a`** (§11.3).

This spec designs the Sindri desktop app: an [Avalonia UI](https://avaloniaui.net/) app (C#, MVVM, compiled bindings, MIT
core only) that is the engineer's window onto the Sindri control plane. It fills the "UI app design" slot in
`docs/sindri/README.md` ("Design specs for the application"), and `/specToProvenPR` takes its **Phases** to PRs.

**Builds on, in this order of authority for its parts:**
- the API surface spec (`2026-10-10-sindri-api-surface-design.md`, "API"): routes and the command catalog (§3.1–§3.3),
  the views the UI renders (§3.6), callers and the authorization table (§4.2), the event stream (§5), versioning (§9),
  later collector queries (§11), and phase P10 (`ILauncher`, `OpenItem`);
- the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`, "WF"): engine-owned states and their answers
  (§3.8, §5.8) and the status layouts (§6.4);
- the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`, "CTX"): cited results (§8.4) and map
  review (§8.7);
- the dashboard spec (`2026-10-09-sindri-dashboard-design.md`, "DB"): what the UI shows (§4.4, §5, §5.1, §5.2, §7).
  **This spec supersedes its layout**; its content, states and signals stay unless replaced here;
- `docs/sindri/DESIGN.md`: the visual source of truth (Linear's design language);
- `.agents/rules/csharp.md` "The UI package" and AGENTS.md "Tech Stack" (UI row);
- the existing shell, PR #168 (`docs/csharp/sindri-ui.md`, `Aw.Sindri.Ui`, `Aw.Sindri.Ui.Desktop`), reused per §15.

**Mockups** (design references, not the implementation) live beside this file in
`2026-10-11-sindri-ui-design/` (§16 lists them).

## 1. Decisions

| # | Question | Decision |
|---|---|---|
| U1 | What the app is | A window onto the control plane. It renders what the API returns, re-queries on stream events, and never folds events, derives a state, or keeps its own copy of a sentence the API serves (`EmptyText`, `next`, `detail`). It is not a chat and has no terminal |
| U2 | Who the app is to the API | `X-Sindri-Caller: human`, `X-Sindri-Client: ui`, never `X-Sindri-Terminal` (API §6). So it may run queries, `request`, `pause`, `job`, `decision` and `desk` actions; `protected` actions are shown as the CLI to copy (API §4.2). This settles **X2** (§13) |
| U3 | How the app knows what a button may do | From the command catalog (`GET /commands`, API §3.3), keyed by action id, intersected with the `actions` list on the row or item it sits on. A control renders as `ready` (a button), `pending:<phase>` (disabled, with the phase and the CLI to copy) or `cli-only:<reason>` (a copy-the-CLI block) |
| U4 | Layout | **Single-glance home** (mockup variant 2, the owner's pick): a top bar, one home page with Needs you, Running and Queued, and Health stacked in that order, and item detail in a side sheet. Queue, Decisions, Services and Health are full pages behind the top bar (§3) |
| U5 | The one attention colour | **`#f2994a`** (Linear's in-product orange, the owner's pick; 9.4:1 on the canvas). Used only for needs-you (§11.3) |
| U6 | The headline interaction | One click opens an item in Warp with the engineer's agent (Claude Code, Codex or Cursor); **Attach** brings an open session's Warp tab forward; once the engine runs, opening is a takeover (`pause`), and **Hand back** is always visible while an item is with you (§6) |
| U7 | Launching | `OpenItem` becomes an asynchronous command: it records the request, and an in-process launcher service performs it through `ILauncher` and records the outcome. The app shows "Sent to Warp" or the failure with its fix (§6.6). This settles **X15** |
| U8 | Notifications | The app never fires OS notifications. `notify.desk` (WF §6.3) is the only source. The app shows the in-app pulse, the window title, the dock or taskbar badge and the tray badge. This settles **X5** |
| U9 | Text from agents and trackers | Plain text only (`TextBlock.Text`, never inlines or markup), already scrubbed and sanitized by the API (API §3.6) |
| U10 | Freshness | Every view shows its `asOf`: "updated Ns ago" from `projectedAt`, and the rebuilding and stalled states (API §2.2). After 30 s with no event or ping, views are marked stale (API §2.4) |
| U11 | Keyboard | Linear's model: single-key and `G`-chord navigation, `⌘K` (`Ctrl+K`) command palette fed by the catalog, every action reachable without a pointer (§8) |
| U12 | Views the API doesn't serve | Shown in the navigation as "not served yet" with the spec that will serve them; never faked from other data (§10) |

## 2. What the app is for

An engineer opens the app and, **in under 5 seconds**, knows:
1. **What needs me:** decisions, parks, escalations, failures and profile drift (one list, `GET /needs-you`).
2. **What's running and where:** each running item, its step, its agent and where it runs, and what is with me.
3. **What's queued.**
4. **Whether Sindri itself is healthy:** the process, its lease, the views, the services and the profile.

Agent work happens in the engineer's terminal and tools. The app's job is to get them there in one click, with the
right item and context, and to take the work back when they're done.

**The five-second test** is a design requirement, checked by the prover in each UI phase (§18.2): from a cold start with
the fixture ledger (§18.2), a fresh observer answers the four questions from one screen without scrolling or clicking.

**How we know it works** (read from the ledger, so nothing new is collected): the launch success rate
(`launch.started` over all launch outcomes; it counts handoffs to Warp, not sessions that did work, which the collector will measure later), the median time from a needs-you row appearing to its clearing, and the
share of decisions and answers made from the app (`human:ui`) versus the CLI (`human:cli`). Until a view owns them, the owner reads them
from the ledger (`sindri ledger --type 'launch.*'` for launches; needs-you rows' `since` against their clearing events, and
`decision.recorded` and engine answer events by actor, for the other two); the **Today** view (§10.2, after the collector spec) is their home.
This spec adds no metric route.

**Rules every screen follows:**
- **No logic.** A view model maps API fields to text and visibility. It never computes a state, an urgency, an
  ordering, a group, an allowed action, a health word, a row label or a clearing rule. Ordering comes from the API (queue groups, needs-you rows).
- **Re-query, never fold.** A stream event names what changed; the app re-reads the affected view (§9.2).
- **Words, not colour.** Every state is a word and a shape; colour only reinforces it (§11).
- **One sentence source.** Empty, rebuilding and stalled sentences come from the view's `empty` field; error text is
  `ProblemDetails.detail` as it is (API §3.6). The app owns only labels, headings and the sentences listed in §12.4.

## 3. Information architecture and navigation

### 3.1 Layout (the owner's pick: single-glance home)

Three layouts were mocked with the same fixture data (`shotgun/contact-sheet.html`), and the owner picked variant 2:

| Variant | Shape | For | Against |
|---|---|---|---|
| 1. Triage inbox | Sidebar with counts · list · detail pane · status bar | Linear's own model; the detail sits beside the list | Running and queued are one click away |
| **2. Single-glance home** (picked) | Top bar · one home page with Needs you, Running and Queued, and Health stacked · detail in a side sheet | Answers the four questions on one screen, the five-second test taken literally; actions inline on each row | A long needs-you list pushes the rest down (handled by the cap in §3.2); detail covers part of the page |
| 3. Lanes | Icon rail · a pinned needs-you strip · queue groups as board columns | Shows flow through the groups | Horizontal scrolling for needs-you; cards hide the engine's `next` sentence |

### 3.2 The window

```
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│ ◆ Sindri  [Home] Queue 4  Decisions 2  Services 5/6  Later ▾     ⌘K Search or run a command   ● Degraded · updated 2s ago · index failed │
├══════════════════════════════════ attention band (while a row pulses) ══════════════════════┤
│ Home                                                                                        │
│ 5 need you · 2 running · 1 with you · 4 queued                         G N Needs you  J K Move  O Open  ? │
│ ┌ NEEDS YOU 5 ────────────────────────────────────────────── most urgent first, then oldest ┐│
│ │▌park      ITM-01JC8 Retry storm in outbox dispatcher   12m  [⌂ Grant rounds — at a terminal] [Open in Claude Code ▾] Cancel ││
│ │           $ sindri workflow answer ITM-01JC8 --grant 1  [Copy]                            ││
│ │▌decision  REQ  Re-scope ITM-01JD2: drop the iOS head   26m  [Accept A] [Decline] [Open ▾] ││
│ │▌escalated ITM-01JB4 Context map review for tabs.layout 2h 56m [⌂ Accept — at a terminal] [Reject] ││
│ │▌failure   service index  Nightly index build failed     9h  [Run again]                   ││
│ │ profile   Profile changed since approval — 2 keys        2h  [⌂ Approve at a terminal]    ││
│ └───────────────────────────────────────────────────────────────────────────────────────────┘│
│ ┌ RUNNING 3 · WITH YOU 1 ───────────────────────┐ ┌ QUEUED 4 ────────────────────────────────┐│
│ │ ITM-01JE7 produce 2/3 · Claude Code [Open ▾][Take over]│ ITM-01JF0 Stream limits 429 fallback 1h ││
│ │ ITM-01JA1 with you since 12:01 [Hand back]    │ │ …                                         ││
│ └───────────────────────────────────────────────┘ └───────────────────────────────────────────┘│
│ ┌ SINDRI HEALTH  Running · pid 48211 · epoch 7 · API v1 · views 2s · services 5/6 · profile changed ┐│
└─────────────────────────────────────────────────────────────────────────────────────────────┘
                                                              ┌ detail sheet (closed: edge tab) ┐
```

- **Top bar** (48 px, `canvas` with a `hairline` bottom rule): brand mark; a segmented navigation **Home · Queue ·
  Decisions · Services · Later ▾** with counts from the views' rows; the `⌘K` field ("Search or run a command"); and the
  **health pill** at the right: a word: "Healthy", "Degraded", "Stalled" or "Rebuilding" from `health.word` (E-14), or the app's connection states "Stale" and "Not running" (§4.1), "updated Ns ago",
  and any problem named in words ("index failed", "profile changed"). The pill opens **Health**. "Later ▾" lists
  Agents, Repos, Today and Automatic work, each "not served yet" (§10.2).
- **Attention band:** a 2 px line in the attention colour under the top bar while any needs-you row is unseen and
  `pulse`; it pulses with them (and is static under reduced motion).
- **Home** (the default page, §4.2): a page title with a one-line summary in words, then four panels in a fixed order:
  **Needs you** (full width), **Running and With you** (left) beside **Queued** (right), and a **Sindri health** strip.
- **Caps:** Running and With you shows at most 5 rows (With you first, then running in the API's order), then "Show
  all N in Queue (`G R`)"; Queued shows 5. **The fold rule:** at 1280 × 800 and text scale 1.0, the summary line, Needs you
  (up to 5 rows) and the tops of Running and Queued are above the fold (**SO-19:** while any setup step is unfinished the Setup card sits above Needs you as at most three rows plus "and N more", filled by priority, so Needs you still starts above the fold; re-check the mockup number when the card lands); health is always above it in the top bar's
  pill, so Home's health strip may sit just below (the mockup at 5 rows ends near 840 px). At scale 1.3 the needs-you
  cap drops to 4. At the 900 × 600 minimum the page scrolls.
- **Needs-you cap:** Home shows at most 5 needs-you rows (all `pulse` rows first, as the API orders them), then
  "Show all N", which expands the panel in place. The summary line always states the totals; the fold rule above says
  what fits.
- **Detail sheet:** a right-side sheet (default 520 px, resizable 420–760 px, `surface-1`, a `hairline` left rule)
  beside the page, opened from any row with `Enter` or a click on its title. It shows the item, park, decision, service
  or job (§4.3–§4.8). `Esc` closes it and returns focus to the row. While it's closed, a slim edge tab shows the last
  opened subject. Below 1100 px window width the sheet covers the full width with a back affordance, and opening it moves focus to its
  heading.
- **Page beside the sheet** (owner, 2026-10-15; WCAG 2.2 2.4.11): a sheet beside the page never covers page content.
  The page area is the window width minus the sheet. Rows drop low-priority columns (since, repo, step) as the page
  narrows, and Home stacks its two panels. Nothing hides under the sheet, the title keeps at least 160 px, and the
  primary action stays visible.
- **Pages** behind the top bar (Queue, Decisions, Services, Health) use the same width and panels, with the detail
  sheet on the right.
- Window minimum 900 × 600. The window remembers its size, the sheet width and the last page (local preference).

### 3.3 Pages and navigation

| # | Page or part | Key | Opens on | Data (§10) |
|---|---|---|---|---|
| 1 | **Home** (Needs you, Running and With you, Queued, Health) **(SO-19)**: the Setup card sits above Needs you while any setup step is not done or skipped (three rows and "and N more"; key `G U`, in-app route `setup`, no OS scheme) | `G H` | Always the default | needs-you, queue, status, services |
| 1a | Home's **Needs you** panel, expanded | `G N` | the tray, the attention band | `GET /needs-you` |
| 2 | **Queue** (all four groups) | `G Q` | — | `GET /queue` |
| 2a | Queue filtered to Running / With you | `G R` / `G W` | Home's panel headers | the queue's `running` / `withYou` groups |
| 3 | **Decisions** (and requests) | `G D` | — | `GET /decisions`, `GET /requests` |
| 4 | **Services** (and jobs) | `G S` | — | `GET /services`, `GET /jobs/{id}` |
| 5 | **Health** | `G .` | the health pill, Home's health strip | `GET /status`, `GET /profile`, `GET /commands` |
| — | Detail sheet | `Enter` on a row | a row, a deep link | `GET /items/{id}`, timeline, decisions, instance |
| — | Agents, Repos, Today, Automatic work (rest) | — | "Later ▾", dimmed: "not served yet" | gaps (§10.2) |
| — | Settings | `⌘,` | — | local preferences only (§12.3) |

**Deep links inside the app:** `home/needs-you`, `needs-you/<rowId>`, `item/<ItemId>`, `decision/<DecisionId>`,
`service/<name>`, `job/<JobId>`. The tray menu and the palette use them. An OS-level `sindri://` URL scheme is later
(§12.2).

**Selection and focus are stable across refreshes:** a re-query keeps the focused row by its id (`NeedsYouRow.id`,
`ItemId`); if the row is gone, focus moves to the next row in the same panel, and a polite announcement says what
cleared ("ITM-01JC8 cleared").

## 4. Screens and their states

### 4.1 States every screen shares

Every view is in exactly one of these states, taken from the API, in this priority (highest first):

| State | When (from the API) | What the screen shows |
|---|---|---|
| **Version mismatch** | `Sindri-Api-Version` (or `/status.apiVersion`) differs from the app's | A full-window panel: "Sindri and the app are different versions" (API §9), both versions, and the fix "quit both and run `sindri`". No data views, no actions |
| **Not running** | No usable `control.json`, or ping fails (S1b's `SND-UI-001/002`) | The S1b panel: "Not running", the reason, and `sindri` to copy. The app keeps looking every 2 s (S1b) |
| **Connecting** | First load, before `/status` answers | The window chrome and skeleton rows (first load only, never on refresh) |
| **Unreadable** | `SND-UI-003`: the API answered with something this build doesn't read; or Sindri refused the call (a 403 with its own code, such as `SND-AUTH-001` after the app re-read the token and retried once) | A banner with the code and `sindri doctor` to copy (for a refusal, Sindri's code and detail as they are); the last good data stays, marked stale |
| **Stalled** | `asOf.stalled = true` | An error banner across the list: the view's `empty` sentence ("Views are stalled at N: run `sindri views rebuild`") and a **Rebuild views** button (`RebuildViews`, desk, §7). Rows still show, marked stale |
| **Rebuilding** | `asOf.rebuilding = true` | A progress banner with the `empty` sentence ("Rebuilding views (cursor N of M)…"); rows show as they come back; actions that read views are disabled with "retry after the rebuild" (`SND-API-010`) |
| **Stale** | No event or ping for 30 s, or a refresh failed while the process still answers ping | "updated Ns ago" turns into a word: **stale**, with a refresh button. After 3 failed refreshes: "Sindri not responding: run `sindri doctor`" (DB §5) |
| **Error** (one action) | A command returned `ProblemDetails` | An inline message under the control: `title` (the SND code) and `detail` as it is, including its `(fix: …)` |
| **Empty** | The view has no rows | The `empty` sentence, centred, in `ink-muted`, with the view's keyboard hint |
| **Normal** | — | Rows |

**Every command control has a lifecycle**, the same everywhere:

| Phase | Shows |
|---|---|
| `sending` | A spinner, the label kept. The control stays enabled so keyboard focus is kept; the view model refuses a second run. A polite announcement ("Sending…") after 300 ms |
| `accepted` | An inline check and the outcome in words for 2 s ("Resumed ITM-01JC8"), announced; the row then clears or updates on the next refresh |
| `refused` / `failed` | The inline error (above); focus stays on the control |
| Copy | "Copied" for 1.5 s, announced |

A second click while `sending` does nothing. The engine's nonce is minted at click time (§9.1), never when a form
renders.

**The palette's rows follow the same lifecycle** (owner, 2026-10-12; PR #193): running a `ready` command from the
palette (§8) keeps the palette open, and that row shows `sending`, then `accepted` or Sindri's code and detail as
`refused` / `failed`. Esc closes the palette at any point; the command keeps running.

**A subject that clears while its sheet is open** (a decision decided at the CLI, an instance resumed): the sheet keeps
the subject, shows "Cleared: <the timeline line that cleared it>" with **Close**, and disables any form while keeping
its text, which can still be copied.

Busy (`SND-API-009`) and read-your-writes timeouts (`SND-API-011`) after an accepted command keep the "done"
confirmation and mark the view stale until the next event (API §2.4).

### 4.2 Home

The default page (§3.2): four panels in a fixed order:
1. **Needs you:** up to five rows with their actions inline (§4.3), and "Show all N" expanding the panel in place.
2. **Running and With you:** one line each: id, title, step, agent, where it runs, since, budget, and its action
   (running rows: **Open ▾** and **Take over**; With-you rows: **Hand back**, and **Attach** once the engine runs).
   Running and Queued each show their own group's `empty` sentence when empty (E-18).
3. **Queued:** the group's count and its next five items, each with **Open**.
4. **Sindri health:** the health line (§4.8) as a strip; it opens **Health**.

States: shared (§4.1). Each panel is empty with its own group's or view's `empty` sentence (E-18).

### 4.3 Needs you

Home's first panel, and its expanded form (`G N`). Rows come from `GET /needs-you` in the API's order (urgency, then
oldest). Each row shows, left to right: the urgency bar (§5), the row's `label` as the API serves it (E-7: `parked · no-progress`, `escalated · gate`, `decision ·
re-scope`) with its urgency in words beneath
("needs you now", "failed", "to look at": the same words as its accessible name), the subject id (mono), the title, the repo, the reason (one line,
the full text in the sheet), the age (from `since`), and **its actions inline**: the primary action first, then the
row's other `actions` (E-7) as secondary buttons, then **Open in <agent> ▾** where the row has an item. A protected
answer shows a lock button ("Grant rounds — at a terminal") and the exact CLI with **Copy** on a second line under the
row's text, so the row never offers to run it. Clicking the title, or `Enter`, opens the detail sheet.

**The sheet per kind** (each with its one useful answer):

| Kind | Detail shows | Primary action | Others |
|---|---|---|---|
| `decision` | The question, the options with the recommended one marked, who filed it (a session or a human), the item, `recordedOnly` as the served `notice` | One button per option (`Decide`) | Open the item; an optional note field |
| `park` | The instance header (WF §6.4) and its rendered `next` sentence as it is; the input facts; the budget and loop lines; the answer form for the park reason (§4.4) | The reason's answer (§4.4) | Open in agent (§6), Take over, Cancel |
| `failure` | The service, its last run (slot, outcome, code), and the code's fix | **Run again** (`RunService`, job, `pending:P6`) | Pause the service (`PauseService`); its CLI is the served `cli` |
| `profile` | The changed key paths, approved and live hashes | Copy `sindri profile approve <hash>` (protected) | — |

### 4.4 The park and escalation states, each with its one useful answer

The sheet renders the answers the engine's `allowedCommands` and the catalog allow. The UI never decides which
answer fits a reason; it renders the instance's `allowedCommands` (WF §6.3, as action ids per E-13) intersected with the
catalog rows by id. This table
is what that intersection produces, so the mockups and tests can check it:

| State (WF §3.8) | The one useful answer | In the app |
|---|---|---|
| `parked: rounds` / `no-progress` | Grant rounds | **Copy** `sindri workflow answer <a> --grant 1` (`ApplyCommand:answer-grant`, protected, `cli-only:typed-confirmation`). On `no-progress`, also **Answer** with text (desk, a form) |
| `parked: budget` | Grant tokens | **Copy** `… --grant-tokens <n>` (`ApplyCommand:answer-grant-tokens`, protected, `cli-only:typed-confirmation`) |
| `parked: needs-answer` | Answer with text | **Answer** form: a plain-text box (≤ 1 MiB) → `ApplyCommand:answer-text` (desk, E-12) |
| `parked: timeout` | Resume | **Resume** button → `ApplyCommand:answer-resume` (desk) |
| `parked: spec-conflict` (revoked) | Cancel | **Cancel** (`ApplyCommand:cancel`, desk, confirm) |
| `parked: spec-conflict` (drift) | Re-approve, then resume | **Copy** `sindri recipe approve <recipe>` (protected), then **Resume** enabled once the instance's `allowedCommands` include it |
| `parked: spec-conflict` (dispatch) | Fix, then resume | The refused effect and code; **Resume** (desk) |
| `parked: SND-WF-010` | Resume in a build that has the block | **Resume** (desk) |
| `escalated: gate` | Accept or reject | **Copy** `… --accept` (protected); **Reject** button (`ApplyCommand:answer-reject`, desk) |
| `escalated: timeout` | Resume | **Resume** (desk) |
| `parked` mirroring a child (WF §5.7) | The child's answer, forwarded | The child's answers, rendered from the parent's `allowedCommands` (E-13 lists the forwarded actions), labelled "goes to <child>"; protected ones copy the CLI against the parent's address. The parent and child appear once in Needs you, under the child |
| `escalated: child` | Answer in the child | The child's answer, rendered from the child's `allowedCommands`, labelled "goes to <child>" |
| `taken-over` | Hand back | **Hand back** (`ApplyCommand:handback`, desk) |
| An item with no instance yet (S2 running) | Start its workflow | **Start** (`StartInstance`, desk) |
| Desk task (open Step task, WF W20) | Complete the Step | **Complete** form: outcome (from the declared outcomes), outputs as file picks (≤ 1 MiB each, contents sent; the server never opens a client path) → `CompleteStep` (desk). Also **Open in agent** |

Mockups: `mockups/park-grant.html` (protected answer), `mockups/park-needs-answer.html` (desk text answer),
`mockups/escalated-gate.html` (protected accept with a desk reject).

### 4.5 Queue, Running, With you

- **Queue:** the four groups in the API's order (`waitingOnYou`, `withYou`, `running`, `queued`; API §3.6), each a
  collapsible section with its count; items within a group in the API's order. There is no parked group: a parked item
  sits in Waiting on you and carries a "parked" badge, from its `state`. The app keeps a heading for a `parked` group only as a
  fallback; the API never sends one. Each row: id, title, repo, state as a
  word, step, agent (once launched), since, and the row's primary action from `actions`.
- **Running** (Home's left panel, and the Queue page filtered by `G R`): the `running` group, with the step (`produce round 2/3`), the agent and where it runs ("Claude Code in
  Warp", "factory worker"), the budget line as text plus a thin `ink-subtle` bar, **Open ▾** and **Take over** (§6.2). **Attach** belongs to With-you rows
  only.
- **With you** (pinned at the top of Home's left panel, and `G W`): the `withYou` group. Every row carries **Hand back** as its primary action and "with you since
  12:01 (25m)". This is the takeover's home (§6.4).
- **Create item:** `C` opens a small form (title, repo, body) → `CreateItem` (desk). **Close item** from the detail
  (`CloseItem`, desk, with the version the human saw; confirm for "cancelled").

Empty: each group hides when empty; the view shows its `empty` sentence when all are.

### 4.6 Item detail

The full item: header (id, title, repo, state as a word, version, since), its `body` (scrubbed, plain text) and its
`link` (the tracker record key) as `GET /items/{itemId}` serves them, the engine instance header and `next` once
S2 runs (WF §6.4, rendered line for line, ASCII as the API sends it), what it's waiting on, the answer block (§4.4),
the launch block (§6), pending decisions for the item, requests filed against it, and the timeline
(`GET /items/{id}/timeline`, paged by `next`, each entry: time, type, actor, one scrubbed line).

**Context tab** (later, with S3 and X9): renders a CTX §8.4 result for the item (`GET /context?item=`, §10.2):
- the header line with the anchors and map version;
- a **DEGRADED** banner (word plus icon) when `degraded` is non-empty, one line per source with its code;
- **conflicts** in their own block before the sections, always in full;
- sections as headings; each item with its `[Cn]` citation markers, `trust` as a word (`trusted` / `untrusted`),
  `(draft)` for unconfirmed items, and `provenance`;
- untrusted text in a fenced, `surface-2` block labelled with its source, as plain text;
- a sources table with each citation's `state` (`current`, `stale` with both versions, `unchecked`,
  `scrub-blocked`: "text withheld").

**Map review** (CTX §8.7) is read-only in the app: the draft's version, counts (deterministic, model, orphaned) and
the queue in impact order. Applying is terminal-bound (`review --apply` is protected, X9), so the panel shows
`sindri context map review` to copy.

### 4.7 Decisions and requests

Two tabs. **Pending decisions** (`GET /decisions?pending=true`): question, options, recommended option, item, class,
`recordedOnly`; option buttons (`Decide`). **Decided** (the same route, `pending=false`): who decided, when, the
choice. **Requests** (`GET /requests`): kind, item, the scrubbed text (plain text, ≤ 2,000 characters), actor, state,
and its decision id. **File a request** (`R`, from an item or globally): kind, item, text → `FileRequest`.

### 4.8 Services, jobs and Health

- **Services** (`GET /services`): name, cadence, paused, last run (slot, outcome as a word, code), next slot. Actions:
  **Run now** (`RunService`, job), **Pause** / **Resume** (desk). A running job opens a job panel (`GET /jobs/{id}`):
  its state, the last 200 progress lines (mono, plain text), result and code, and **Cancel** (`CancelJob`, job).
- **Health** (`GET /status`, `GET /profile`, `GET /commands`): process (running, pid, epoch, lease expiry, API address,
  `apiVersion`), views (cursor, head, `projectedAt`, rebuilding, stalled), services summary, profile (approved hash,
  changed paths, the copy-to-approve CLI), and the catalog's `pending` rows ("12 actions arrive in later phases",
  listed by phase). **Stop Sindri** (`StopProcess`, desk, confirm) sits here and only here.
- **The health line** (the top bar's pill in short, Home's health strip in full) is assembled from these fields, in a fixed order, each a word:
  `Running · pid · epoch · API v1 · views updated Ns ago | rebuilding | stalled · N services failed · profile changed`.

### 4.9 Every screen's state matrix

| Screen | Empty | Loading / rebuilding | Stalled | Error | Not running | Version mismatch |
|---|---|---|---|---|---|---|
| Needs you | `empty` ("Nothing is waiting on you. 3 running, 1 queued.") | skeleton once; rebuild banner | banner + Rebuild | inline per action | S1b panel | full panel |
| Home | per panel `empty` | as above | banner | — | S1b panel | full panel |
| Queue / Running / With you | `empty`; empty groups hidden | as above | banner | inline | S1b panel | full panel |
| Item detail | — (404: `SND-ITEM-404` detail with "back") | skeleton once | banner | inline | S1b panel | full panel |
| Decisions / Requests | `empty` | as above | banner | inline (`SND-ITEM-409` "decided elsewhere" refreshes the row) | S1b panel | full panel |
| Services / jobs | `empty` | as above | banner | inline (`SND-SVC-404`, `SND-JOB-404`) | S1b panel | full panel |
| Health | — | as above | its own view shows the stall | — | S1b panel | full panel |
| Launch flow | — (no launch yet: **Open in <agent>**; after one, while `launch.confirm` is set: "Sent to Warp <age> ago" and **Open another session**) | "Sending to Warp…" | — | §6.6 failure states | S1b panel | full panel |
| Context tab (later) | "No results" + `suggestedAnchors` | — | — | `SND-CONTEXT-*` detail; DEGRADED banner | S1b panel | full panel |

## 5. The needs-you pulse, urgency and badges

**Urgency is the API's** (`NeedsYouRow.urgency`: `pulse`, `colour`, `calm`; API §3.6). The app maps it:

| Urgency | Row | Top bar and Home | Window, dock, tray |
|---|---|---|---|
| `pulse` (decisions, parks, escalations) | A 3 px left bar in the attention colour that pulses (opacity 1 → 0.35 → 1, 1.6 s, ease-in-out) until **seen**; then steady | The Needs-you panel's count chip in the attention colour, and the attention band under the top bar, pulsing while any row is unseen | Title `(N) Needs you · Sindri`; dock / taskbar badge `N`; tray icon in the attention colour with `N` |
| `colour` (failures) | The steady attention bar, no motion | Count chip | Counted in `N` |
| `calm` (profile drift) | An `ink-subtle` bar | Counted in the chip, not in colour | Counted in `N`; the tray stays neutral when only calm rows exist |

- **Seen** is presentation state, kept by the app per row id: a row is seen once it has been on screen and focused for
  1 s, or acted on. It doesn't change the API's row; it only stops the motion. It's kept in memory and in the local
  preference file, keyed by row id, and pruned when the id leaves the list. An explicit acknowledge
  (`POST /needs-you/{id}/ack`) is API §11, later.
- **New rows** slide in (180 ms) with the pulsing bar; the Needs-you panel's header pulses once.
- **Stopping the motion:** **Mark all seen** (`Shift+A`, the palette, and a button on the Needs-you panel header) marks
  every current row seen. Settings has an in-app **Reduce motion** switch that forces the reduced-motion path whatever
  the OS says (WCAG 2.2.2).
- **Reduced motion** (the OS setting or the in-app switch, §11.4): no pulse, no slide. A pulse row shows a static 2 px outline in the
  attention colour plus the word `new` until seen.
- **One polite live region** announces changes: "2 now need you", "ITM-01JC8 cleared". Refreshes never move focus,
  scroll position or form input.
- **No OS notifications from the app** (U8, X5). `notify.desk` fires them, so nothing fires twice.

**Tray / menu-bar badge** (DB §5.2, replacing SwiftBar). The app keeps a tray icon while it runs, and closing the window
keeps the app in the tray (Quit is explicit):

| Tray state | Icon | Tooltip |
|---|---|---|
| Nothing needs you | Monochrome mark | "Sindri · 3 running · nothing needs you" |
| Rows need you | Mark in the attention colour with the count | "Sindri · 2 need you · 3 running" |
| Only calm rows | Monochrome with a small count | "Sindri · 1 to look at" |
| Stale / not responding | Hollow mark | "Sindri not responding" |
| Not running | Hollow, struck through | "Sindri isn't running · run `sindri`" |

The tray menu lists up to five needs-you rows (kind, id, title, age), then "Open Sindri", "Running: N", "Quit".
Clicking a row opens the app at `needs-you/<rowId>`. **No action runs from the tray** (DB §5.2).

## 6. The headline interaction: one click into an agent

From any item, park, escalation or running instance, one click opens the work in Warp with the engineer's chosen
agent. The mockup is `mockups/launch-flow.html`.

### 6.1 Launch targets and how one is chosen

| Target id | Label | Agent CLI (verified 2026-10-11) | Seeds a new session | Resumes a known session |
|---|---|---|---|---|
| `claude-code` | Claude Code | `claude` 2.1.296 | `claude --session-id <uuid> -n "sindri <item>" "<seed>"` | `claude --resume <uuid>` |
| `codex` | Codex | `codex` (codex-cli 0.162.1) | `codex -C <repo> "<seed>"` | `codex resume <id>` when the id is known |
| `cursor` | Cursor | `agent` (Cursor's CLI; formerly `cursor-agent`; not verified locally) | `agent "<seed>"` | `agent --resume=<chatId>` when known |

The terminal is **Warp** (`dev.warp.Warp-Stable`, verified 0.2026.10.07). Other terminals are later; `ILauncher` keeps
the terminal behind its port.

**Choosing the target** (the server resolves it, so the app holds no logic; §6.7 E-3):
1. **A per-click choice:** the split button's chevron, or `Shift+O` then `1`/`2`/`3`, picks a target for this launch.
2. **The remembered last choice for the repo:** the target of the last successful launch in this repo, derived by a
   projection from `launch.started` events. A per-click choice becomes the remembered one (it is recorded on the request,
   `chosenBy`).
3. **The profile default:** `launch.default` (and `launch.repos.<repo>.default`), changed through the profile like
   every config (protected).
4. **`claude-code`** when nothing else is set.

The split button's primary label always names the resolved target ("Open in Claude Code"); the menu marks which rule
chose it ("last used in agentic-workflow", "profile default").

### 6.2 Open, attach, resume and take over

The item's `launch` block (§6.7 E-2) lists the modes the server allows; the app renders them and decides none.

| Mode | When the server offers it | Button | What happens | Phase |
|---|---|---|---|---|
| **Open** | No launch is `requested` for the item, and the item isn't closed | **Open in <agent>** | A new session in a new Warp tab, in the item's repo (the path from the approved profile's repo entry), seeded with the item id only. The session pulls its state and context through the Sindri MCP server (`sindri_item`, `sindri_item_timeline`, `sindri_pending_decisions`, and `context_query` for context). Once the engine runs, opening is a takeover (§6.4) | U6a (before S2, no takeover); U6b adds the takeover |
| **Open another session** (not a mode: `OpenItem:open` behind a confirm) | Before the engine: the server sets `launch.confirm` because a launch for the item started **within the last 2 h** (`launch.confirmWindow`, a profile value) and nothing has taken it over or handed it back since | **Open another session** (asks `confirm.reason`) | As Open; the row and sheet show "Sent to Warp <age> ago · <agent> · tab `sindri ITM-…`" from `launch.lastStarted` | U6a |
| **Attach** | A tmux session `snd-<run>` for the item is alive (the launcher checks `tmux has-session`), whether the factory or the human started it | **Attach** | Opens a Warp tab running `tmux attach -t snd-<run>` on Sindri's socket (API §8.1 rev 7 note: tmux owns the session, Warp is a viewer). It starts no agent process. If the item is running, the first turn typed is a takeover and the row moves to **With you**; if it is already with you, `OpenItem:attach` reaches the engine as a no-op takeover. Sindri knows the session is alive because it owns it (X55) | U6b (needs the engine's `taken-over` state); the alive check lands with API P10a |
| **Resume** | A `launch.started` for the item carried a session id (Claude Code always; Codex and Cursor once their ids are known), **and the item was handed back since**. The handback is the human's signal that the session is finished | **Resume <agent> session** (next to **Open new**) | A new Warp tab running the target's resume command; like Open, it is a takeover once the engine runs | U6b (needs the engine's handback) |
| **Take over** (a running step) | The item is `running` | **Take over** (labelled "pauses automation") | `ApplyCommand:takeover` (`pause`). In the engine phases that exist (WF P1–P7) steps run on the desk runtime, so there is no worker process to stop. When a later engine phase adds a headless worker runtime, it owns stopping the worker and recording `run.stopped`; only then does the server offer **Resume** on that worker's session (X20) | U6b |

Session lifecycle facts (a launched session's SessionStart and SessionEnd reported through `sindri mcp`, so Attach and
Resume could follow the real session) are later; they need the collector or per-provider hook adapters (§10.2).

**The seed** is a fixed template plus ids, never item text (API §1 C-26):
`Sindri item ITM-01JC8. Read it with the sindri_item tool before you start; you are taking it over from Sindri.`

### 6.3 The mechanics (researched 2026-10-11)

| Fact | Value | Source |
|---|---|---|
| Warp's URL scheme | `warp://action/new_window?path=`, `warp://action/new_tab?path=`, `warp://launch/<launch_configuration_path>`, `warp://tab_config/<name>` | docs.warp.dev/terminal/more-features/uri-scheme |
| A command in a URI | **Not possible.** The scheme doesn't run a command or control an existing tab; startup commands come from a launch configuration or Tab Config file | same |
| Launch configurations (legacy, still documented) | YAML; macOS `~/.warp/launch_configurations/`, Linux `${XDG_DATA_HOME:-~/.local/share}/warp-terminal/launch_configurations/`, Windows `%APPDATA%\warp\Warp\data\launch_configurations\`. Fields include `windows[].tabs[].title`, `layout.cwd` (absolute), `layout.commands[].exec`, `color` | docs.warp.dev/terminal/sessions/launch-configurations |
| Tab Configs (Warp's recommended replacement) | TOML in `~/.warp/tab_configs/`; `[[panes]] type, directory, commands`; `{{param}}` placeholders open a dialog | docs.warp.dev/terminal/windows/tab-configs |
| Focus an existing tab from outside | **No documented way.** An undocumented, opt-in `warpctrl` scripting CLI exists in the bundle (strings only); not used | local inspection of Warp 0.2026.10.07 |
| Detect Warp | Bundle id lookup (`NSWorkspace urlForApplication(withBundleIdentifier:)`, or `mdfind`), and a handler for `warp://` | local |
| Claude Code | `--session-id <uuid>` (Sindri picks it, so resume never depends on Warp), `-n/--name`, `--resume`, `--mcp-config`; child env `CLAUDECODE=1`, `CLAUDE_CODE_SESSION_ID`. Its transcript is `~/.claude/projects/<cwd slug>/<session-id>.jsonl` | code.claude.com/docs/en/cli-reference; `claude --help` 2.1.296 |
| Codex | `codex [PROMPT]`, `-C/--cd`, `-c key=value` overrides (MCP servers too), `codex resume [id]`; `CODEX_SANDBOX` is set only when sandboxed | `codex --help` 0.162.1 |
| Cursor | CLI binary `agent` (`agent "prompt"`, `agent resume`, `agent --resume=<id>`, `agent ls`); MCP from `~/.cursor/mcp.json`; `CURSOR_AGENT=1` unreliable | cursor.com/docs/cli/overview (not verified locally) |
| PATH from a GUI process | A macOS GUI process gets launchd's short PATH. Resolve CLIs through the user's login shell; the command inside Warp runs in a login shell and sees the full PATH | inferred, checked locally |

**The launcher's recipe (`WarpLauncher`):**
1. **Build the file with a YAML serializer**, never by concatenation: `sindri-<launchId>.yaml` in Warp's
   launch-configuration folder, mode 0600. One tab, `title: "sindri <item>"`, `cwd: <repo path>` (the only place the
   path appears). Every id is checked against its pattern before use (`ITM-…`, `LCH-…`, a UUID for Claude Code session ids, and a per-target pattern in `launch.started`'s schema for Codex and Cursor ids); a non-matching session id drops Resume and falls back to Open.
2. **One `exec` line, built from a shell-quoted argument list:**
   `AW_SINDRI_CHILD=1 AW_SINDRI_ITEM=<item> AW_SINDRI_LAUNCH=<launchId> <cli> <args…>`, preceded by the guard
   `command -v <cli> >/dev/null 2>&1 || { printf '%s\n' '<cli> not found (SND-LAUNCH-003): <fix>'; exit 127; }`.
   `AW_SINDRI_CHILD=1` is already on the CLI's agent-session list (API §6), so every agent Sindri launches, Codex and
   Cursor included, is a `session` caller whatever its own markers are.
3. `open "warp://launch/sindri-<launchId>.yaml"` (macOS; `xdg-open` on Linux, `Start-Process` on Windows).
4. **Cleanup:** delete the launch file and its `mcp-<launchId>.json` 60 s after **any** outcome (`launch.started` or `launch.failed`) (U6a's spike records Warp's cold-start read latency and raises this if needed), and sweep any `sindri-*.yaml` older than 60 s when the launcher service
   starts, so an old launch can't be re-run from Warp's palette.
5. **MCP for the session:** Claude Code gets `--mcp-config <AW_SINDRI_HOME>/launch/mcp-<launchId>.json` (0600,
   written per launch and swept with the YAML file; stdio or HTTP per X8; no token in v0). Codex gets the server through `-c mcp_servers.sindri.*` overrides. Cursor reads only
   its own `mcp.json`, so until the switch registers the server (the cutover rule), a Cursor session has no Sindri
   tools: the target shows `limited: "Sindri tools aren't registered with Cursor yet"` and still launches.
6. **No GUI session:** when `sindri serve` can't reach the user's desktop session (started by launchd before login, no
   `DISPLAY`/`WAYLAND_DISPLAY`, an ssh session), the probe records `terminal.present=false, problem: no-gui-session`,
   and the target menu says so.

The probe (`ILauncher.Probe`) resolves each CLI through `$SHELL -lic 'command -v <cli>'` with an argument list (no
string interpolation) and a 5 s timeout. Tab Configs are the fallback adapter if Warp drops launch configurations;
U6a's spike unit opens Warp once with each and records which works, including whether `warp://launch/` takes an
absolute path (unverified), from a `sindri` started in a terminal and from one started by launchd.

### 6.4 Takeover and handback

- **Opening is a takeover once the engine runs** (U6b). `OpenItem:open` records `item.open-requested`. The engine
  applies it as a trigger of a new kind, `open-request` (X18, §6.7 E-6): applied directly in `Commit` in any state where
  a takeover is allowed, keyed by the ledger fact, actor `service:engine`, causation the request. It's a no-op when the
  instance is already taken over (attach, or a second open). **The launcher opens Warp only after the engine has
  recorded that takeover** (matched by causation), or at once when the item has no active instance. An instance started while a launch for its item is still `requested` (in flight) **starts taken over**, with that
  request as the takeover's causation (so the failed-launch fold below matches it) (the engine reads the item's open launch in `StartInstance`'s
  `Commit`), so automation never runs beside a session that's opening. If the engine refuses the takeover (the
  instance became terminal), the launch fails with the engine's code. So automation never runs beside the
  new session.
- **A takeover with no session:** when `launch.failed` follows the takeover its request caused, the needs-you `park`
  row "Hand back ITM-…?" appears at once (the needs-you projection folds the pair), not after `takeoverIdle`'s 2 h.
- Before S2 (U6a) there is no instance: the item records the launch and nothing pauses.
- **Handback is offered in four places, always one click:**
  1. the item's header while it's with you ("With you since 12:01 (25m) · **Hand back**");
  2. every With-you row on Home and in the Queue;
  3. the `takeoverIdle` reminder after 2 h, a needs-you `park` row ("Hand back ITM-01JA1?") with **Hand back** as its
     primary action (needs WF's §5.6 and §6.3 edit that turns idle takeovers into `park` rows, API §3.5);
  4. a session's own `sindri_request kind=handback` (API §3.2), which arrives as a `decision` row; accepting it hands
     back once the engine runs.
- A session may take over but never hand back (API §4.2); the app may do both, because it is `human`.

### 6.5 What the app never does

- Run agent text, show it as anything but plain text, or put item text in a launch command.
- Make a decision on an agent's behalf: the app acts only on a human click or key, never on a stream event, a
  single-instance message or a deep link (those only navigate).
- Approve a `protected` action: grants, accepts and map-review apply are copy-the-CLI only
  (`cli-only:typed-confirmation`). **(SO-20)** Three are the exception, typed or held in the app against a server challenge:
  `ApproveProfile`, `ApproveRecipe` (typed) and `ApproveAllRecipes` (held, a typed answer accepted); a stream event, a
  deep link or a single-instance message still never approves anything (setup-once spec §4).
- Resume a session that may still be running, or start two processes on one session.
- Start the control plane from inside the window (bare `sindri` does that), or start an agent without a click.

### 6.6 Failure states

The launch block shows a failure as a word, the code, `detail` as it is, and one fix:

| Failure | Detected | Code | Shown before the click? | Fix |
|---|---|---|---|---|
| Warp isn't installed | Probe (bundle id) | `SND-LAUNCH-002` | Yes: the button is replaced by "Warp isn't installed" | Install Warp; or copy the CLI command to run in any terminal |
| No GUI session for the server | Probe | `SND-LAUNCH-002` (`problem: no-gui-session`) | Yes | Start `sindri` from your desktop session |
| The agent CLI isn't on the login PATH | Probe (login shell); also the in-tab guard (`exit 127`) | `SND-LAUNCH-003` | Yes: that target is disabled in the menu with the reason | The target's install command |
| The repo isn't on disk | Probe (the profile repo's path; `git -C <path> rev-parse`) | `SND-LAUNCH-004` | Yes | `git clone …` into the profile path, or fix the path and approve the profile |
| Warp didn't accept the URI | `open` exit status | `SND-LAUNCH-001` | No | Retry; the CLI command to copy |
| `Launch` timed out | the 10 s limit | `SND-LAUNCH-009` | No | "A Warp tab may have opened: look for `sindri ITM-…` before opening again"; `launch.confirm` is set as after a start |
| No session to resume or attach | The launch block | `SND-LAUNCH-006` | Yes: the mode falls back to Open | — |
| The launch was interrupted (Sindri restarted mid-launch) | Launcher start-up | `SND-LAUNCH-007` | No | "A Warp tab may have opened: look for `sindri ITM-…` before opening again" (it is never retried on its own) |
| A launch is already in progress for the item | `OpenItem` | `SND-LAUNCH-008` | Yes: the button reads "Sending to Warp…" | Wait |
| The launcher never answered | `LaunchView` still `requested` after 15 s | — (app-owned text) | No | "Warp hasn't confirmed: run `sindri doctor`"; the launcher's timer expires the request at 30 s as `SND-LAUNCH-007`, so Warp never opens late |
| The engine refused the takeover | `launch.failed` with the engine's code | `SND-WF-*` | No | The code's fix |
| The item is closed | `ItemView.state` | `SND-ITEM-409` | Yes: no launch block | — |
| Sindri isn't running when the session starts | Inside the session: `sindri_item` returns `SND-API-001` | — | — | The session says so; run `sindri` |
| Cursor without Sindri tools | Probe | — (`limited`) | Yes: a note in the menu | Wait for the switch, or pick another target |

After the click, the app shows **"Sent to Warp"** once the launcher records `launch.started`, and **"Session live"**
once `tmux has-session` confirms the agent process is up (rev 7: Sindri owns the tmux session, so it can tell; it still
never claims the agent did any work). Once the engine runs, the item moves to **With you** with the takeover.

### 6.7 The contract the UI needs from `ILauncher` and `OpenItem`, as numbered API edits

`ILauncher` stays API P10's port. E-1…E-11 change API P10; E-12…E-19 are UI-driven edits elsewhere (E-17's detail is in §18.1, which also names the phase
each edit lands in).

| # | Edit | Why |
|---|---|---|
| **E-1** | `OpenItem` becomes asynchronous. Body `{ mode: open\|attach\|resume, target?: claude-code\|codex\|cursor, itemVersion }`; response `LaunchRequested(accepted, LaunchId)`, **replacing** P10's `ItemOpened(accepted, launchedIn)`; variants `OpenItem:open`, `:attach`, `:resume`, all `desk`, `ReadsViews`. The handler decides and appends `item.open-requested {launchId, item, mode, target, chosenBy}` (key `item.open-requested:<launchId>`); it refuses with `SND-LAUNCH-008` while the item has a launch in `requested`. An in-process **launcher hosted service** (caller `service:launcher`) reads the ledger from its own persisted `launcher_cursor`, acts on a request **only if it has no outcome and is under 30 s old**, waits for the engine's takeover when the item has an active instance that isn't already `taken-over` (E-6) **within the same 30 s deadline** (an instance already taken over, as for Attach or a second Open, needs no wait: the `open-request` trigger is a no-op there), **re-checks freshness immediately before `ILauncher.Launch`**, launches with a 10 s limit on `ILauncher.Launch` and **claims only while at least 10 s of the deadline remain**, so Warp never opens more than 30 s after the click (a timed-out `Launch` is `launch.failed SND-LAUNCH-009`, whose fix reads like `-007`'s: a tab may have opened). **Before deciding whether to wait for a takeover**, the launcher waits until the engine's item projection has reached the request's ledger sequence and treats any instance for the item, including one created after the request, as active; for such an instance it waits for the `open-request` trigger's recorded result (WF W12: taken over, or a no-op because it already was) rather than for a state change. A race test interleaves `StartInstance` and `OpenItem` and asserts Warp never opens while the instance is running, and appends `launch.started {launchId, item, target, sessionId?, tabTitle}` or `launch.failed {launchId, code}`, **both under one fact key, `launch.outcome:<launchId>`**, so the first outcome written wins: the launcher writes `launch.started` only if it wins that key (it records the outcome before reporting success to anyone), and an expiry that loses the race to a started launch is dropped. A `FakeTimeProvider` test fires the expiry while `ILauncher.Launch` is running and asserts exactly one outcome, and that it is `launch.started`. A timer (every 5 s) tries to append `launch.failed SND-LAUNCH-007` (same `launch.outcome` key) for every request with no outcome past its 30 s deadline; the launcher **claims** a request by adding its id to an in-process claimed set (under the lock the timer takes) before calling `ILauncher.Launch`, the timer skips claimed ids, and nothing is claimed after its deadline; a claim lost to a crash is swept at start-up as `SND-LAUNCH-007`, and the same sweep runs at start-up; a request is never retried. `SND-LAUNCH-008` counts only requests still inside their deadline. All three events are public and in `EventCatalog`. `sindri doctor` gains a `launcher` line (the probe's age, requests past their deadline, the last failure code), so "run `sindri doctor`" in §6.6 leads somewhere. New typed id `LaunchId` (`LCH-…`). A `launches` projection backs `GET /launches/{launchId}` → `LaunchView(state: requested\|started\|failed, target, chosenBy, code?, detail?, asOf)` | API §2.1: handlers never do slow work; a launch happens at most once and never without a fresh click |
| **E-2** | `ItemView` gains `launch`: `{ modes: [open\|attach\|resume], defaultTarget, chosenBy: lastUsed\|profile\|builtIn, attach?: {target, tabTitle, since}, resumable?: {target, sessionId}, inFlight?: LaunchId, lastStarted?: {target, at, tabTitle}, confirm?: {reason}, repoPath, blockers: [{code, detail}] }`, computed in `Core` (`LaunchOptions.For(item, instance, launches, probe, profile)`, the instance state coming from the engine's item projection, X7) from projections only, by §6.2's rules | The app renders modes and the default; it decides neither |
| **E-3** | A `launch_targets` projection over `launch.probed {terminal: {id, present, version?, problem?}, targets: [{id, present, version?, limited?, problem?}], repos: [{repo, present}]}`, appended by the launcher service on start, after a failed launch, and every 10 min **only when the result changed**. `GET /launch/targets` → `LaunchTargets(terminal, targets, repos, asOf)`. A **Check again** control: `ProbeLaunchTargets` (`job` class) | Probing is slow I/O, so it's a service that records facts, not a query |
| **E-4** | Profile keys `launch.default` and `launch.repos.<repo>.default` (each a target id) and `launch.confirmWindow` (a duration, default 2 h), in S1's closed profile schema | The profile default (§6.1); changing it is a profile approval |
| **E-5** | Codes `SND-LAUNCH-002` (no terminal: not installed, or no GUI session), `-003` (agent CLI not found), `-004` (repo not on disk), `-006` (no session to resume or attach), `-007` (interrupted; not retried), `-008` (a launch is already in progress), `-009` (`Launch` timed out; a tab may have opened), beside P10's `-001`. After `-007` or `-009`, `launch.confirm` is set as after a start | §6.6 |
| **E-6** | **WF edit (§5.4, §5.7):** a trigger kind `open-request` whose id and key are both `wf:{instance}:open:{launchId}`, applied directly in `Commit` wherever `takeover` is allowed; it is a trigger, not a command, so it takes no nonce, and it is exempt from the approval check like `takeover`; actor `service:engine` (WF §6.2 gains that actor row for this trigger); causation the `item.open-requested` event; a no-op when already taken over. The router delivers it to the item's active instance, **including an instance created after the request** (one started without E-19's takeover because the request arrived after its read). The launcher waits for its recorded result (E-1) and fails the launch with the engine's code when it's refused | X18; automation never runs beside the new session; the actor is never a made-up human |
| **E-7** | `NeedsYouRow` gains `actions` (the action ids allowed now, from the same source as E-13), `label` (the display kind and reason, for example `escalated · gate`, `parked · no-progress`, `decision · re-scope`), and `subject` carries the instance address for `park` rows | The app shows the row's kind and its secondary actions without deriving them |
| **E-8** | Command catalog rows gain `destructive: bool`, `readsViews: bool` and `shortcut?` (a default key, §8). `CloseItem` splits into `CloseItem:done` and `CloseItem:cancelled` (the latter destructive) | Confirmations, rebuild-time disabling and key hints without a client-side list |
| **E-9** | `CompleteStep` gets a catalog row with UI status `ready` once WF P4 lands; its route accepts output file contents (≤ 1 MiB each), as WF §6.1 says for the CLI. API §2.1's `ApplyCommand:complete` variant is dropped in favour of `CompleteStep` | The desk-task form (§4.4); one name |
| **E-10** | P10 is split: **P10a** (`OpenItem`, the launcher service, `WarpLauncher`, E-1…E-5, E-11; depends on P3 + P8) ships before the engine, and **P10b** (E-6's wait for the takeover) depends on WF P5. P10a carries the spike unit (§6.3) | The headline ships early; takeover lands with the engine |
| **E-11** | `sindri open <item> [--agent claude-code\|codex\|cursor] [--attach \| --resume]`; bare `sindri open` takes the mode the item's `launch` block puts first | One surface for the CLI and the app |
| **E-12** | API §2.1 and §4.1 define the `ApplyCommand` variants the app calls: `answer-text`, `answer-resume`, `answer-reject` (all `desk`), beside `answer-grant`, `answer-grant-tokens` and `answer-accept` (`protected`), `takeover` (`pause`), `handback` and `cancel` (`desk`). The field rule is restated per variant: `grant` is valid only on `answer-grant`, `grantTokens` only on `answer-grant-tokens` and `accept` only on `answer-accept`; any of these fields on another variant is refused, and the request is still classified `protected` (fail-closed) | Without them the fail-closed rule makes Reject and Resume `protected` |
| **E-13** | **WF edit (§6.3):** each `allowedCommands` entry on `GET /workflow/instances/{address}` is `{ actionId, cli }`, with ids filled and the class taken from the catalog; a parent mirroring a child lists the forwarded actions | The app intersects ids, never maps verbs |
| **E-14** | `GET /status` gains `health: { word: healthy\|degraded\|stalled\|rebuilding, problems: [{code, text}] }`, computed by `Core`'s `HealthSummary.For(…)` | The health pill renders it verbatim |
| **E-15** | Each `EventCatalog` entry gains `invalidates: [viewName]`, served by `GET /events/catalog` (a query, P5) | The app's refresh map comes from the server; an unknown type re-queries every visible view |
| **E-16** | Every `cli` string the API serves (catalog rows, `NeedsYouRow.cli`, `allowedCommands`) is built from checked values only: ids matched to their patterns, recipe names and hashes to theirs; no free text | A copied command can't carry a newline or a second command |
| **E-17** | Golden JSON per route and scenario from every API phase (defined in §18.1; X26) | The UI's contract tests |
| **E-18** | `QueueView.groups[]` each carry their own `empty` sentence from `EmptyText` ("Nothing running.", "Nothing queued."), and the view returns every group, empty ones included | Home's Running and Queued panels never need app-written empty text; the Queue page hides empty groups |
| **E-19** | **WF edit (`StartInstance`, §6.1):** `StartInstance` reads the item's launches (it is `ReadsViews`, so it waits for the head) and, when one is still `requested`, starts the instance taken over with that request as the causation (§6.4) | No automation beside a session that's opening |

## 7. Controls and the actions they call

Every control the app renders, the catalog action id it calls, the action class (API §4.1), and its UI status as the
catalog row should carry it. "Phase" is the API (P*) or engine (WF-P*) phase that ships it. `ready` means the app
renders a button once that phase ships; until then the catalog says `pending:<phase>`.

| Control | Where | Action id | Class | UI status |
|---|---|---|---|---|
| Accept / Decline / option buttons | Needs you, Decisions | `Decide` | decision | `pending:P4` → ready |
| File a request | Item, global (`R`) | `FileRequest` | request | `pending:P4` → ready |
| Create item | Queue (`C`) | `CreateItem` | desk | `pending:P3` → ready |
| Close item | Item | `CloseItem:done` · `CloseItem:cancelled` (E-8) | desk (cancelled is destructive) | `pending:P3` → ready |
| Start workflow | Item with no instance | `StartInstance` | desk | `pending:WF-P4` → ready |
| Open / Attach / Resume in <agent> | Item, park, running row | `OpenItem:open` · `:attach` · `:resume` | desk | `pending:P10a` → ready (E-1) |
| Check launch targets again | Target menu | `ProbeLaunchTargets` | job | `pending:P10a` → ready (E-3) |
| Take over | Running row, park | `ApplyCommand:takeover` | pause | `pending:WF-P5` → ready |
| Hand back | With you, item header, idle row | `ApplyCommand:handback` | desk | `pending:WF-P5` → ready |
| Answer (text) | Park `needs-answer`, `no-progress` | `ApplyCommand:answer-text` (E-12) | desk | `pending:WF-P4` → ready |
| Resume | Park `timeout`, drift, dispatch, `SND-WF-010`; escalated timeout | `ApplyCommand:answer-resume` | desk | `pending:WF-P4` → ready |
| Reject | Escalated gate | `ApplyCommand:answer-reject` | desk (destructive) | `pending:WF-P4` → ready |
| Grant rounds | Park `rounds`, `no-progress` | `ApplyCommand:answer-grant` | protected | `cli-only:typed-confirmation` |
| Grant tokens | Park `budget` | `ApplyCommand:answer-grant-tokens` (X30) | protected | `cli-only:typed-confirmation` |
| Accept (gate) | Escalated gate | `ApplyCommand:answer-accept` | protected | `cli-only:typed-confirmation` |
| Cancel instance | Park, item | `ApplyCommand:cancel` | desk (destructive) | `pending:WF-P4` → ready |
| Complete (desk task) | Item, park (task) | `CompleteStep` | desk | `pending:WF-P4` → ready (E-9); the file picks show the files' names and sizes and ask once before sending |
| Approve recipe (drift) **(SO-20)** | Park `spec-conflict: drift`, Recipes | `ApproveRecipe` (`recipe approve`) | protected | `ready`: typed in the app (`confirm: typed`) |
| Approve profile **(SO-20)** | Needs you `profile`, Health, the Setup card | `ApproveProfile` | protected | `ready`: typed in the app (`confirm: typed`) |
| Approve all recipes **(SO-20)** | The Setup card, Recipes | `ApproveAllRecipes` | protected | `ready`: held, or typed (`confirm: held`) |
| Rebuild views | Stalled banner, Health | `RebuildViews` | desk | `pending:P2` → ready |
| Run now / Run again | Services, `failure` row | `RunService` | job | `pending:P6` → ready |
| Pause / Resume service | Services | `PauseService` · `ResumeService` | desk | `pending:P6` → ready |
| Cancel job | Job panel | `CancelJob` | job | `pending:P6` → ready |
| Stop Sindri | Health | `StopProcess` | desk (destructive) | ready (S1, class in P1) |
| Apply map review | Context map review | (CTX `map review --apply`) | protected (X9) | `cli-only:typed-confirmation` |
| Copy CLI, Refresh, navigation, palette | everywhere | — (local) | — | ready |

`answer-resume`, `answer-reject` and `answer-text` are defined by E-12; until then the fail-closed rule would make them
`protected`. WF P2 registers every engine catalog row: the desk and pause rows as `pending:WF-P4` or `pending:WF-P5`, which WF
P4 and P5 flip to ready, and the protected rows (`answer-grant`, `answer-grant-tokens`, `answer-accept`,
`ApproveRecipe`) as `cli-only:typed-confirmation` from the start; they never become app buttons. **(SO-20)** `ApproveRecipe` is no longer in that list: it, `ApproveProfile` and `ApproveAllRecipes` are the three app confirmations (setup-once spec §4). The engine's queries (`ShowRecipe`, `ListRecipes`, `GetInstance` and the rest) have no catalog rows: the
app reads them as plain GETs, with no catalog gating.

Before the engine (S2) the app shows no engine controls: parks don't exist, and launching takes nothing over.

**A copy-the-CLI block** shows the exact `cli` string from the catalog row (with the row's ids filled by the API in
`NeedsYouRow.cli` or the instance's `allowedCommands`), a **Copy** button (`⌘C` when focused), a terminal glyph, and
one line: "Needs a typed confirmation at a terminal." It never offers to run it. A `pending:<phase>` control is drawn
disabled, and its phase and CLI sit on the next line in `InkSubtle`, focusable and copyable (disabled ink is never used
for information). The app refuses to show or copy a `cli` string that contains a control or bidi character (E-16 makes
this never happen; the check is a backstop).

## 8. Keyboard model and command palette

Linear's model: everything has a key, keys are shown where the action is, and `?` lists them.

| Keys | Action |
|---|---|
| `⌘K` / `Ctrl+K` | Command palette |
| `G` then `N` `H` `Q` `R` `W` `D` `S` `.` | Go to Needs you, Home, Queue, Running, With you, Decisions, Services, Health. The chord waits 1 s and shows "G …" in the top bar; any other key cancels it |
| `J` / `K` (and arrows) | Next / previous row |
| `Enter` / `Esc` | Open the detail sheet / close the topmost layer: palette or dialog, then the sheet, then the filter |
| `O` | Open (or Attach / Resume) with the resolved target; `Shift+O` opens the target menu |
| `A` | The row's primary action when it's a single `ready` button (Resume, Hand back, Run again). On a decision it applies the recommended option **only while the decision's sheet is open**. Never a protected action |
| `Shift+A` | Mark all seen (§5) |
| `X` / `Shift+X` | The row's reject-like action: on a decision, the option whose `rejectLike` is true; elsewhere the catalog's `shortcut` (E-8): `X` Reject or Decline, `Shift+X` Cancel the instance. Destructive rows confirm in-app |
| `T` | Take over; `B` hand back |
| `Y` | Copy the selected row's CLI |
| `C` / `R` | Create item / file a request |
| `/` | Filter the current list (by id, title, repo) |
| `?` | Shortcuts sheet |
| `⌘,` | Settings |
| `F6` / `Shift+F6` | Next / previous region: top bar, page, sheet (§11.2). On macOS also `⌘⌥→` / `⌘⌥←`: F6 is the Focus key there and reaches the app only with fn held (`fn+F6`), unless "Use F1, F2, etc. keys as standard function keys" is on. No `Ctrl+F6` |

- **Single keys are scoped to the panel or sheet with focus**; typing in a text field never triggers them.
- **Key bindings come from the catalog's `shortcut`** (E-8) where one exists, so one key never names two actions on a
  row; the fixed keys in the table (`G`, `J`, `K`, `O`, `A`, `T`, `B`, `Y`, `C`, `R`, `/`, `?`) are reserved, and the
  catalog test rejects a `shortcut` that uses one; the `?` sheet and the `kbd` chips show the row's own bindings.
- **A key guard:** for 500 ms after focus moved on its own (a refresh cleared the focused row), single-key actions are
  ignored, and while the pointer is over a panel new rows are held (a "N new" pill appears) instead of shifting the
  rows under it.
- **The palette** lists, in sections: **Actions** for the selected row (from its `actions` ∩ the catalog; `pending`
  rows shown disabled with their phase; `cli-only` rows as "Copy: sindri …"), **Go to** (views, items by id or title
  from the current views), **Sindri** (Rebuild views, Stop Sindri, Health). Fuzzy matching on labels and ids; each
  entry shows its key.
- **Shortcut hints** appear as `kbd` chips in `ink-subtle` beside buttons and in the palette.

## 9. How the app talks to the API

### 9.1 Requests

- Every request sends `X-Sindri-Caller: human` and `X-Sindri-Client: ui`; never `X-Sindri-Terminal`, never `Origin`.
- After an accepted command the app sends `X-Sindri-After: <sequence>` on its next query (API §2.4).
- Commands are sent once; the app never retries a command on its own. A `SND-ITEM-409` refreshes the view and shows
  its `detail` ("decided elsewhere", "changed since you looked").
- **Engine commands on an instance** (`ApplyCommand:*`, `CompleteStep`) take a nonce: the app requests it from
  `POST /workflow/instances/{a}/nonce` **at click time** (never when a form renders) and sends it with the command.
  `SND-WF-002` (version conflict) and `SND-WF-009` (stale nonce) are treated like `SND-ITEM-409`; `SND-WF-003`, `-004`
  and `-005` show their `detail` inline. The nonce POST's class is a cross-spec question (X23). `StartInstance` has no instance yet and takes none: WF §6.1
  makes it idempotent on `(recipe, item, attempt)`, which covers a repeated click.
- **Logging:** `Aw.Kernel.Telemetry` JSON lines on stderr (`AW_LOG_LEVEL`), codes, routes and timings only, never
  response bodies or agent text.
- **Time:** every timing rule (the 1 s chord, the 30 s staleness, the 250 ms debounce, the 15 s launch wait, the 1 s
  "seen") runs on an injected `TimeProvider`, so the unit lane tests them with `FakeTimeProvider`.
- The app reads `GET /commands` on connect and after every reconnect (a new epoch). The catalog is the only list of
  actions the app knows.

### 9.2 The stream: "go re-read"

One `GET /events` stream (S1b's follower, extended for P5's `Last-Event-ID`). Each event's type maps to the views it
invalidates through the server's `GET /events/catalog` (E-15: `invalidates` per type), read on connect. The app
re-queries those views (debounced to one refresh per view per 250 ms) and never applies the payload. A type the catalog
doesn't know re-queries every visible view. Until E-15 ships (P5), the app re-queries every visible view on each event.

A stream that ends falls back to S1b's 2 s look-again loop; `SND-API-012` (429) falls back to re-querying every 5 s
(API §5.4).

## 10. Data sources

### 10.1 Served by the API (with phase)

| View / part | Route | Phase |
|---|---|---|
| Connection, process, lease, profile summary | `GET /ping`, `GET /status` (+ `apiVersion`, `views`) | S1; P1; P2 |
| Action catalog | `GET /commands` | P1 |
| Services | `GET /services` | P2 |
| Profile drift | `GET /profile` | P2 |
| Queue, Running, With you, Home's queue panels | `GET /queue` | P3 |
| Item list (filters) | `GET /items?state=&repo=` | P3 |
| Item detail | `GET /items/{id}` | P3 (+ `launch`, E-2, P10a) |
| Item timeline | `GET /items/{id}/timeline?after=` | P3 |
| Needs you, tray, badges | `GET /needs-you` | P4 (+ `actions`, `label`, E-7) |
| Decisions | `GET /decisions?pending=&item=` | P4 |
| Requests | `GET /requests?state=` | P4 |
| Live refresh | `GET /events` | S1 (basic); P5 (filters, `Last-Event-ID`) |
| Audit view in Health (later) | `GET /ledger` | P5 |
| Jobs | `GET /jobs/{id}` | P6 |
| Instance header, `next`, `allowedCommands`, the answer forms | `GET /workflow/instances/{address}` | WF-P4 |
| Item state from the engine | the engine's item projection (X7) | first API phase after WF-P4 |
| Launch targets | `GET /launch/targets` | P10a (E-3) |
| Launch outcome | `GET /launches/{id}` | P10a (E-1) |
| Health word | `GET /status` `health` | E-14 (P2) |
| Event → views map | `GET /events/catalog` | E-15 (P5) |

### 10.2 Gaps (views the API doesn't serve yet)

| View | Needs | Owner |
|---|---|---|
| Session lifecycle for Attach and Resume (§6.2) | `launch.session-started` / `-ended` from per-provider hooks, or the collector | API §11 |
| **Agents** (every session on the machine, DB §5.2) | `GET /sessions`, `/sessions/{provider}/{id}` (+ `/timeline`); attach for non-Sindri sessions | API §11 collector spec |
| **Repos** (DB §5.3) | `GET /repos` | API §11 |
| **Automatic work** beyond services (DB §5.4: evolve, scope runs, proposals, channel builds, heavy-lock holder) | `GET /work` | API §11 |
| **Today** (DB §5.5) | `GET /today`, `GET /metrics/today` | API §11 |
| Needs-you sources: sessions, PRs, stale index, judge health | more needs-you projections | API §11 |
| **Receipts** | `GET /items/{id}/receipt` | API §11 |
| **Context tab** | `GET /context?q=&item=` | CTX + X9 |
| **Map review viewer** | `GET /context/map/review` | CTX P4 + X9 |
| **Recipes list** (approved, stale) | a `GET /workflow/recipes` route for `recipe list` | WF (no route today) |

Each gap appears in the top bar's "Later ▾" menu as "not served yet" (dimmed, with the owner spec on hover), and becomes a view in
the UI phase that follows its route.

## 11. Accessibility

### 11.1 Contrast against DESIGN.md's palette (WCAG 2.2)

| Foreground on background | Ratio | Use in the app |
|---|---|---|
| `ink #f7f8f8` on canvas / surface-1 / surface-2 | 19.6 / 17.9 / 17.2 | Titles, row text |
| `ink-muted #d0d6e0` on canvas / surface-1 | 14.3 / 13.0 | Secondary text, `empty` sentences |
| `ink-subtle #8a8f98` on canvas / surface-1 / surface-2 | 6.4 / 5.9 / 5.6 | Meta, ages, eyebrows, `kbd` (AA for all sizes) |
| `ink-tertiary #62666d` on canvas / surface-1 | 3.6 / 3.3 | **Disabled text only** (exempt); never for information |
| `primary #5e6ad2` as text on canvas | 4.44 | **Fails AA for text**: not used as text. Links and selected labels use `primary-hover #828fff` (7.3 on canvas) |
| white on `primary #5e6ad2` | 4.70 | Primary button labels (13 px / 500): AA |
| white on `#828fff` (DESIGN.md's `button-primary-hover`) | 2.87 | **Fails AA.** The app's hover darkens instead: white on `#535fc6` 5.5, pressed `#4c57be` 6.2 |
| error `#eb5757` on canvas / surface-1 | 6.0 / 5.5 | Error text and the ✕ glyph (with a word) |
| `success #27a644` on canvas | 6.6 | The word "ok" plus a check glyph |
| attention `#f2994a` on canvas / surface-1 | 9.4 / 8.6 | Needs-you bars, chips, the word "new" |
| hairline `#23252a`, `#34343a` on canvas | 1.3, 1.7 | Decoration only. Interactive controls are identified by a filled surface and a label, not by a hairline (1.4.11) |

**Deviation from DESIGN.md:** its focus ring is 2 px `primary-focus` at 50 % opacity, 1.8:1 on the canvas, below
the 3:1 non-text minimum. The app's focus ring is **2 px `primary-hover #828fff` at full opacity, offset 2 px** (6.6:1
on surface-1). Recorded as a proposed edit to DESIGN.md's "Elevation & Depth" level 4, together with the primary hover above (X22).

### 11.2 Focus order

- **Window:** top bar (navigation, search, health pill) → page title and summary → Home's panels in their fixed
  order (Needs you rows, each row's actions left to right; Running and With you; Queued; Health) → the detail sheet
  when open (header actions, answer block, launch block, then the rest in reading order). `F6` (on macOS also `⌘⌥→`, §8)
  cycles top bar, page and sheet; `Tab` moves within one; `J`/`K` move between rows across panels.
- **Opening the sheet** keeps focus on the row when the sheet sits beside the page; `Enter` again (or `Tab`) moves into
  its first action. When the sheet covers the page (below 1100 px), focus moves to the sheet's heading, and `Esc` or
  back returns it to the row.
- **Dialogs and the palette** trap focus and return it to the control that opened them.
- **Refreshes never move focus** (§3.3). A row that clears moves focus to the next row and announces it.
- Every control has an `AutomationProperties.Name` (the label plus the subject id: "Hand back ITM-01JA1"), and every
  status text carries its word as its name (S1b already does this).

### 11.3 Never colour alone; the attention colour (owner's call)

- Each state is a word and a glyph: `park` ⏸, `escalated` ▲, `decision` ?, `failure` ✕, `profile` ◇, `running` ▶,
  `with you` ◎, `queued` ○, `ok` ✓, `stale` ⌛. Urgency is also a word in the row's accessible name ("needs you now",
  "failed", "to look at").
- **The attention colour** (`#f2994a`, the owner's pick; 9.4:1 on the canvas) is used only for needs-you; errors use
  their own `Error` token (§14.1). DESIGN.md allows no second chromatic accent on marketing pages,
  and its "Known gaps" notes Linear's product palette for in-product status; this is that exception, used once.
  The alternatives mocked (amber `#f0b429`, coral `#eb5757`) are in the contact sheet.

### 11.4 Reduced motion

- The OS setting is read through a platform port (`IMotionPreference` in `Aw.Sindri.Ui`, adapters in the head: macOS
  `NSWorkspace.accessibilityDisplayShouldReduceMotion`, Windows `SPI_GETCLIENTAREAANIMATION`, GNOME
  `org.gnome.desktop.interface enable-animations`), re-read on change.
- The in-app **Reduce motion** switch (Settings) forces this path, and is how the prover tests it.
- With reduced motion on: no pulse, no slide-in, no skeleton shimmer; durations become 0; pulse rows get the static
  outline and the word `new` (§5).
- No motion conveys information on its own anywhere.

## 12. Desktop first, then mobile and browser heads

### 12.1 Desktop (this spec)

macOS, Linux and Windows from one `Aw.Sindri.Ui` and the `Desktop` head. The tray uses Avalonia's `TrayIcon`; the dock
or taskbar badge and `IMotionPreference` are head adapters.

### 12.2 What changes for later heads

| Concern | Mobile (iOS, Android) | Browser (WebAssembly) |
|---|---|---|
| Reaching the API | Not loopback: needs API §11's tailnet listener and tokens; query-only until passkeys | Needs an `Origin` allowlist (API §11), which commands refuse today |
| Layout | One pane at a time (list → detail), bottom tab bar with Needs you, Queue, With you, Health | As desktop |
| Actions | Decisions and requests first; `desk` waits for passkeys (§17.27.2); `protected` stays CLI-only | As desktop, after the allowlist |
| Launch | No Warp: Open becomes "copy the `sindri open` command" or "open on my Mac" (a later remote launch) | Same as mobile |
| Tray / badge | App icon badge; notifications come from `notify.desk` via push (later) | Tab title and favicon (DB §5.1's original form) |
| Keyboard | Hardware keyboard keeps §8; touch targets ≥ 44 px | §8 unchanged |
| `sindri://` deep links | The OS URL scheme opens `item/<id>` | URL routes |

Views and view models stay shared; only heads and adapters change (`.agents/rules/csharp.md` "The UI package").

### 12.3 Local preferences (not logic)

Window size, sheet width, last page, "seen" row ids, palette history, text scale and the in-app reduce-motion switch.
Stored in `$AW_SINDRI_HOME/ui/preferences.json` (0600) with a `version` field, written atomically (temp file and
rename), values clamped on read, and reset to defaults when malformed. Nothing in it changes what the API decides; the launch default lives in
the profile and the server (§6.1).

### 12.4 App-owned copy

Everything else the app shows comes from the API (`empty`, `next`, `detail`, `label`, `health.problems`, `cli`). These
sentences are the app's own, because no API exists at that moment or because they're the app's chrome:

| Where | Sentences |
|---|---|
| Home's summary line and panel headers (§4.2) | "N need you · N running · N with you · N queued" and the headers "NEEDS YOU N", "RUNNING N · WITH YOU N", "QUEUED N", built from the panels' row counts |
| Before the API answers (§4.1) | "Not running" and its reasons (S1b's `SND-UI-*`), "Looks again every 2 s" (the Not running card), "Connecting…", "Sindri and the app are different versions" (API §9's wording), "Sindri not responding: run `sindri doctor`", "stale", "updated Ns ago" |
| Command lifecycle (§4.1) | "Sending…", "Copied", the accepted outcome ("Resumed ITM-…", built from the action's label and the subject id), "Cleared: …" |
| Destructive confirm (§8, E-8) | The question "<label> <id>?" (for example "Close as cancelled ITM-…?"), with **Confirm** and **Keep** |
| Copy block (§7) | "Needs a typed confirmation at a terminal." |
| Launch (§6.6) | "Sending to Warp…", "Sent to Warp · tab `sindri ITM-…`", "Sent to Warp <age> ago · <agent>", "Open another session" (the confirm's question is the server's `confirm.reason`), "Warp hasn't confirmed: run `sindri doctor`", the menu's "last used in <repo>", "profile default" |
| Urgency words (§5, §11.3) | "needs you now", "failed", "to look at", "new" |
| Tray tooltips (§5) | The five tooltips |
| Announcements | "N now need you", "<id> cleared" |
| Settings (§12.3) | "On in your system settings" (under the reduce-motion switch while the OS reduces motion) |

Labels live in one resource file keyed by action id and view name, so every head shares them.

## 13. Cross-spec reconciliation: settled for the UI, and new rows

| # | Settled answer |
|---|---|
| **X2** (what the UI may do) | **API §4.2's table, confirmed screen by screen in §7.** The app is `human` with no terminal. WF §6.2's `ui` actor class ("read verbs only, until the UI's own design adds its forms") is retired: this spec adds the forms. WF §6.2 should drop the `ui` row and point here |
| **X5** (one needs-you list, one notifier) | `GET /needs-you` is the only list the app shows (§4.3). `notify.desk` is the only source of OS notifications; the app shows its in-app pulse, title, dock and tray badges and never notifies on its own (§5). WF's `--needs-me` reads `GET /needs-you` |
| **X15** (launching sessions) | API P10 owns `ILauncher`, split into P10a and P10b and changed by E-1…E-11 (§6.7). Targets: Warp plus Claude Code, Codex or Cursor; chosen per click, then the last choice for the repo, then the profile default, then Claude Code. Open, attach, resume and take over as §6.2. WF §7.2's "the next engine design" note points to API P10 and this spec |
| **X17** (build order vs what's built) | For the UI: S1b (#168) stays in draft until this spec is approved, then becomes UI phase **U0** (§15, §18) on S1. The UI's later phases wait for their API phases: U2 for P3, U3 for P4, U6a for P10a, and so on (§18) |

New rows for the README's table:

| # | Where they differ | Recommended answer | Changes |
|---|---|---|---|
| X18 | **Opening is a takeover.** API P10: `OpenItem` "the engine marks the takeover once S2 runs", with no mechanism. WF: `takeover` is a command on the instance | A WF trigger kind `open-request` applied in `Commit` (actor `service:engine`), and the launcher opens Warp only after it lands (E-6) | API P10b; WF §5.4, §5.7, §6.2 |
| X19 | **Launching is I/O inside a command.** API P10's `OpenItem` returns `launchedIn`, so its handler opens Warp, against API §2.1 (handlers never do slow work) | `OpenItem` records the request; a launcher service performs it once, inside a 30 s deadline, and records `launch.started` / `launch.failed` (E-1) | API P10a, §3.2, §3.4 |
| X20 | **Taking over a running Step.** WF: takeover pauses deadlines; it doesn't say what happens to a worker session that is mid-run. DB §7: never resume a running session | Take over only pauses. The engine phase that adds a headless worker runtime owns stopping the worker (`run.stopped`); only then is a resume of that session offered (§6.2) | WF (a later phase) |
| X21 | **Secondary actions on needs-you rows.** API §3.6 gives each row one `primaryAction`; the detail needs the others | `NeedsYouRow.actions` (E-7) | API §3.6 |
| X22 | **DESIGN.md's focus ring and primary hover** fail WCAG (1.8:1 ring; white on `#828fff` 2.87:1), and it has no error colour | A 2 px `#828fff` ring at full opacity; hover darkens to `#535fc6`, pressed `#4c57be`; an `Error` token `#eb5757` from Linear's product palette, beside the attention colour as the two documented in-product exceptions (§11.1, §14.1) | `docs/sindri/DESIGN.md` |
| X23 | **The engine's nonce mint is a POST that appends nothing.** WF §5.4: `POST /workflow/instances/{a}/nonce`. API: `ARCH-CQRS-001` says every POST is a command | Either a tested query-by-POST carve-out (like X9's), or a catalog row whose class is that of the command it's for. The app mints at click time either way (§9.1) | WF §5.4; API §2.5 |
| X24 | **The dashboard's first goal is deferred.** DB §1: "see the progress of every agent … including agents that Sindri didn't start". This spec serves only Sindri's items until the collector exists | Recorded, not dropped: **Agents** is the first view after the collector spec (API §11), and its sessions join Needs you | API §11; DB §1 |
| X26 | **Golden JSON from the API phases.** The UI's contract tests need every API phase to write per-route, per-scenario goldens (E-17); the API spec's §12 doesn't say so | Add E-17 to API §12 before P1's contract unit; U1 back-fills any merged phase | API §12 |
| X25 | **Notifications and the UI actor in the other specs.** API §5.1 says the app "drives … OS notifications"; WF §6.2 keeps a read-only `ui` actor | API §5.1: the app drives the pulse and badges only; `notify.desk` notifies (X5). WF §6.2: drop the `ui` row (X2) | API §5.1; WF §6.2 |

## 14. Token plan: DESIGN.md → Avalonia resources

One `ResourceDictionary`, `Aw.Sindri.Ui/Theme/Tokens.axaml`, merged into `SindriApp.axaml`, plus Fluent overrides in
`Theme/FluentOverrides.axaml`. Colours are `Color` resources with matching `SolidColorBrush` resources (`Brush.*`), so
styles bind brushes and code never holds a hex value. Dark only (DESIGN.md ships no light theme); a light variant is a
later `ThemeVariant` dictionary with the same keys.

### 14.1 Colour

| DESIGN.md token | Value | Resource key (`Color.*` and `Brush.*`) | Used for |
|---|---|---|---|
| canvas | `#010102` | `Canvas` | Window background |
| surface-1 | `#0f1011` | `Surface1` | Panels, the detail sheet, inputs |
| surface-2 | `#141516` | `Surface2` | Hovered rows, selected row, code blocks, `status-badge` |
| surface-3 | `#18191a` | `Surface3` | Menus, palette, tooltips |
| surface-4 | `#191a1b` | `Surface4` | Dialog |
| hairline | `#23252a` | `Hairline` | Dividers, panel borders |
| hairline-strong | `#34343a` | `HairlineStrong` | Input borders, the copy block |
| hairline-tertiary | `#3e3e44` | `HairlineTertiary` | Nested surfaces |
| ink | `#f7f8f8` | `Ink` | Primary text |
| ink-muted | `#d0d6e0` | `InkMuted` | Secondary text |
| ink-subtle | `#8a8f98` | `InkSubtle` | Meta, ages, `kbd` |
| ink-tertiary | `#62666d` | `InkTertiary` | Disabled text only |
| primary | `#5e6ad2` | `Primary` | Primary button, brand mark, selected nav indicator |
| primary-hover | `#828fff` | `PrimaryHover` | Links, selected labels, **focus ring** (§11.1). Not a button fill (2.87:1 with white) |
| — (derived, §11.1) | `#535fc6` | `PrimaryHoverFill` | Primary button hover (white 5.5:1) |
| primary-focus | `#5e69d1` | — (not used) | DESIGN.md's focus tint; the app's focus ring uses `PrimaryHover` instead (§11.1) |
| — (derived, §11.1) | `#4c57be` | `PrimaryPressed` | Primary button pressed (white 6.2:1) |
| on-primary | `#ffffff` | `OnPrimary` | Primary button text |
| semantic-success | `#27a644` | `Success` | `ok` glyphs |
| semantic-overlay | `#000000` | `Overlay` (60 % opacity) | Dialog scrim |
| — (new, §11.3) | `#f2994a` | `Attention` | Needs-you only |
| — (new, X22) | `#eb5757` | `Error` | Error text, the stalled banner's glyph and rule, destructive confirmations, form validation (always with a word and ✕) |
| — (derived) | `#f2994a` at 14 % on canvas | `AttentionSoft` | Needs-you chip background |
| — (derived, S1b) | `#1d2036` | `PrimarySoft` | Selected nav background, the running pill |

### 14.2 Type ramp (app scale)

DESIGN.md's ramp is marketing-scale (body 16 px). The app keeps its families, weights and tracking, and uses the
smaller end. Fonts: **Inter** (`Avalonia.Fonts.Inter`, as S1b) for Linear Display and Text; **JetBrains Mono** (OFL,
embedded) for Linear Mono, falling back to `Menlo, Consolas, monospace`.

| Resource key | Size / weight / line height / tracking | From DESIGN.md | Used for |
|---|---|---|---|
| `Type.PageTitle` | 20 / 600 / 1.2 / −0.4 | `card-title` reduced | View title |
| `Type.DetailTitle` | 18 / 500 / 1.25 / −0.3 | `card-title` reduced | Detail header |
| `Type.Body` | 13 / 400 / 1.5 / 0 | `body-sm` reduced | Rows, detail text |
| `Type.BodyStrong` | 13 / 500 | `body-sm` | Row titles |
| `Type.Caption` | 12 / 400 / 1.4 / 0 | `caption` | Meta, the health line |
| `Type.Eyebrow` | 11 / 500 / 1.3 / +0.4, uppercase | `eyebrow` reduced | Section labels |
| `Type.Button` | 13 / 500 / 1.2 / 0 | `button` reduced | Buttons |
| `Type.Mono` | 12 / 400 / 1.5 / 0 | `mono` reduced | Ids, CLI, hashes, job lines |
| `Type.Kbd` | 11 / 500 mono | — | Key hints |

Applied as `Style` classes on `TextBlock` (`Classes="body"` etc.) setting `FontSize`, `FontWeight`, `LineHeight` and
`LetterSpacing`. A user text-size preference (Settings) scales the ramp by 0.9–1.3 through one `Double` resource.

### 14.3 Spacing, radius, size, motion

| Group | Keys and values |
|---|---|
| Spacing (`Thickness` and `Double`) | `Space.XXS` 4 · `XS` 8 · `SM` 12 · `MD` 16 · `LG` 24 · `XL` 32 · `XXL` 48 (DESIGN.md; `section` 96 unused in the app) |
| Radius (`CornerRadius`) | `Radius.XS` 4 (chips, `kbd`) · `SM` 6 (tags, the copy block) · `MD` 8 (buttons, inputs) · `LG` 12 (panels, dialogs) · `Pill` 9999 (status pills) |
| Not used in the app | `section` 96 spacing; `rounded.xl` 16, `xxl` 24, `full`; `brand-secure`; the `inverse-*` colours; the display, headline, subhead, body-lg and body type sizes (marketing scale) |
| Sizes (`Double`, minimums scaled by the text scale) | `Size.Row` 36 (dense) / 40 (comfortable) · `Size.TopBar` 48 · `Size.Sheet` 520 (420–760) · `Size.AttentionBand` 2 · `Size.PanelGap` 16 · `Size.Control` 30 (desktop) / 44 (touch heads) · `Size.FocusRing` 2 |
| Motion (`TimeSpan`) | `Motion.Instant` 0 · `Motion.Fast` 120 ms (hover, press) · `Motion.Base` 180 ms (slide-in, panel) · `Motion.Pulse` 1600 ms (needs-you bar) · `Easing.Standard` cubic-bezier(0.2, 0, 0, 1). All become 0 under reduced motion (§11.4) |

### 14.4 Fluent theme overrides

`FluentTheme` stays (S1b) with a dark `ColorPaletteResources` mapped to the tokens, so stock controls (TextBox,
ComboBox, menus, scroll bars) match without restyling each one:

| Fluent palette key | Token |
|---|---|
| `Accent` | `Primary` `#5e6ad2` |
| `RegionColor` | `Canvas` |
| `AltHigh` / `AltMedium` / `AltLow` | `Canvas` / `Surface1` / `Surface2` |
| `BaseHigh` / `BaseMediumHigh` / `BaseMedium` / `BaseLow` | `Ink` / `InkMuted` / `InkSubtle` / `Hairline` |
| `ChromeHigh` / `ChromeMedium` / `ChromeLow` / `ChromeMediumLow` | `HairlineTertiary` / `Surface3` / `Surface2` / `Surface1` |
| `ChromeAltLow` / `ChromeGray` | `InkMuted` / `InkSubtle` |
| `ChromeDisabledHigh` / `ChromeDisabledLow` | `Hairline` / `InkTertiary` |
| `ListLow` / `ListMedium` | `Surface2` / `Surface3` |
| `ErrorText` | `Error` |

Plus control-level overrides: `ButtonBackground*` (secondary = `Surface1` + `Hairline`), corner radius `Radius.MD` on
buttons and inputs, `FocusAdornerTemplate` drawing the §11.1 ring, `ScrollBar` thin overlay style, and `ToolTip` on
`Surface3`. No shadows anywhere (`BoxShadow` unset); lifted panels may carry a 1 px top highlight (`#ffffff` at 4 %).

## 15. The existing shell (PR #168): what stays, what changes

| Part of S1b | Verdict |
|---|---|
| Package shape (`Aw.Sindri.Ui`, `Aw.Sindri.Ui.Desktop` head), no Sindri reference, `http`/`file` edges | **Keep** |
| `control.json` discovery, pid + epoch ping, 2 s look-again loop, `SND-UI-001…003` | **Keep**; they are §4.1's Not running and Unreadable states |
| SSE follower with one refresh per burst | **Keep and extend**: the event → views table (§9.2), `Last-Event-ID`, the 30 s staleness rule |
| Compiled bindings, view models without Avalonia types, headless view tests with frames | **Keep** |
| Hand composition, no container | **Keep** while the app has a handful of view models; revisit (a composition root in the head) at U2 if constructors grow past five ports |
| Inline brushes in `SindriApp.axaml` (`Canvas`, `Surface1`, `PrimarySoft`, …) | **Move** to `Theme/Tokens.axaml` with §14's keys; add the Fluent palette |
| `MainWindow` as one status card layout | **Replace** with §3.2's shell; its status card becomes the Health page, the health pill and Home's health strip |
| `ISindriApi` (ping, status, events) | **Extend** with the headers (`X-Sindri-Caller: human`, `X-Sindri-Client: ui`), the `apiVersion` check, `X-Sindri-After`, and one method per route in §10.1 as its API phase lands |
| Each bare `sindri` opens another window | **Change**: single instance (a second launch focuses the running one), and close-to-tray (§5) |
| The deps cycle question (`Sindri -> SindriUi` cli edge) | **Decided** (owner, 2026-10-12): declared, with a reasoned `DEPS-CYCLE-001` exception in `deps.yaml` |

## 16. Mockups

All in `2026-10-11-sindri-ui-design/`, self-contained HTML with the fixture data from the shotgun brief (one scenario
across every screen), each with a 1280-wide PNG beside it. They are design references, not the implementation. The
`mockups/` files are the visual reference; `shotgun/variant-2.html` is a layout reference only (it predates the
attention colour, the focus ring and the reduced-motion word). Where a mockup and this text differ, the text wins.
Known differences: no mockup shows **Open another session** (§6.2 and the prototype do); the `home`, `needs-you` and `queue` mockups put **Attach** on running rows, where the text gives running rows **Open ▾** and **Take over** (Attach is for With-you rows); the health pill in some mockups reads "Healthy" beside a failed service, where `health.word` is `degraded`; the mockups' kind chips show the short kind (`park`), where the app renders the API's `label`
(`parked · no-progress`, E-7); the sheet mockups use a slightly different page shell from `home` and `queue`; and the
Decisions count varies between files. The `/impeccable` critique (32/40) is in the review folder
(`impeccable-critique.md`); its 16 fixes are in the files.

| File | Shows |
|---|---|
| `shotgun/contact-sheet.html`, `shotgun/variant-{1,2,3}.html` (+ `.png`) | The three layouts (§3.1) |
| `mockups/home.html` | Home: Needs you, Running and With you, Queued, Health (§4.2) |
| `mockups/needs-you.html` | Home's Needs-you panel expanded, with the park detail sheet open |
| `mockups/queue.html` | Queue with its groups, and With you |
| `mockups/item-detail.html` | Item detail with timeline and the context tab (later) |
| `mockups/decisions.html` | Decisions and requests |
| `mockups/services-health.html` | Services, a job panel, Health, and the stalled banner |
| `mockups/launch-flow.html` | The launch flow: the split button and menu, "Sent to Warp", attach, hand back, and the failure states |
| `mockups/park-grant.html` | Park `rounds`: protected grant as copy-the-CLI |
| `mockups/park-needs-answer.html` | Park `needs-answer`: the desk text answer |
| `mockups/escalated-gate.html` | Escalated gate: protected accept, desk reject |
| `prototype.html` (+ `prototype-*.png`) | A clickable prototype of the whole app over the same fixture: navigation, sheets, every §4.4 answer, the launch flow, the palette, the keys, and a controls panel for the shared states, reduced motion and the large fixture |
| `mockups/states.html` | Not running, connecting, version mismatch, rebuilding, stale |

## 17. Review resolutions (`/autoplan`, 2026-10-11, pass 1 → revision 2)

The lens reviews and the consolidation are in `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-ui-design/`.
Pass-1 verdicts: product, architecture, design, devex and security all NEEDS_WORK; no CRITICAL. Every HIGH and MEDIUM
is fixed in the text; LOWs are fixed or answered below.

| Finding (lens) | Resolution |
|---|---|
| Launch comes last and waits for the engine (product C1, HIGH) | U6 split: **U6a** Open on API **P10a** before the engine; **U6b** takeover, Attach and Resume on WF P5 (E-10, §18) |
| Launcher has no delivery, dedupe, freshness or crash rules (arch F1, security F5, HIGH) | E-1: a launcher cursor, fact keys per `launchId`, act only on fresh requests with no outcome (< 30 s), `SND-LAUNCH-007` for interrupted requests (never retried), `SND-LAUNCH-008` while one is in flight, a `launches` projection |
| E-6 doesn't fit the engine router; human actor stamped by the engine (arch F2, HIGH) | E-6 rewritten as a WF trigger kind `open-request`, applied in `Commit`, actor `service:engine`; the launcher waits for the takeover (§6.4) |
| No liveness source for attach versus resume (arch F3, HIGH) | v0: Attach only focuses Warp; Resume only after a handback; session lifecycle later (§6.2, §10.2) |
| Nobody stops a running worker (arch F4, HIGH) | Deferred to the engine phase that adds a headless worker runtime; v0 Take over = pause; WF P1–P7 run steps on the desk runtime (§6.2, X20) |
| No nonce, no WF error mapping (arch F5, HIGH) | §9.1: nonce minted at click time; WF codes mapped; X23 for the POST's class |
| Undefined variants; verb-shaped `allowedCommands` (arch F6, HIGH) | E-12 (variants), E-13 (`allowedCommands` as `{actionId, cli}`) |
| Launched Codex/Cursor sessions act as `human` (security F1, HIGH) | The exec line sets `AW_SINDRI_CHILD=1` (§6.3) |
| Mockups missing (design D1, devex, product C8, arch F16, HIGH) | Generated: 11 screens plus PNGs (§16) |
| No fixture ledger, stall trigger, E-edit owners, X7 phase, contract source, screenshot / five-second / keyboard recipe, runnable launch proof (devex H1–H7, HIGH) | §18.1 (owners), §18.2 (the proving recipe), E-17 (golden JSON); U5 names the X7 phase dependency |
| Health word, needs-you kind, `CloseItem` destructive rule, refresh map, `readsViews` computed in the client (arch F7–F9, design D3) | E-14 `health`, E-7 `label`, E-8 variants and `readsViews`, E-15 `invalidates` |
| Exec line quoting; CLI guard falls through (security F3, arch F17) | YAML serializer, shell-quoted argv, path only in `cwd`, ids validated, `exit 127` (§6.3) |
| Keys hit the next row after an auto focus move; rows shift under the pointer (security F2) | A 500 ms key guard; hover-freeze with a "N new" pill (§8); engine commands carry version-bound nonces |
| Copied CLI with control characters (security F4) | E-16 (API builds `cli` from checked values); client backstop refuses control and bidi characters (§7) |
| No GUI session (arch F10) | Probe records `no-gui-session`; failure row (§6.3, §6.6) |
| Probe churn; stale probe (arch F11, product C7) | Append on change only; **Check again** (`ProbeLaunchTargets`, job) (E-3) |
| No Start control (arch F12) | `StartInstance` in §4.4 and §7 |
| "Attach" means two things (arch F13) | **Attach** focuses; **Take over** pauses (§6.2) |
| `chosenBy: perClick` can't be stored (arch F14) | Dropped from `ItemView`; recorded on the request and `LaunchView` |
| API §5.1 and WF §6.2 edits missing (arch F15) | X25 |
| CLI has no `--attach` (arch F18) | E-11 |
| Variant 2 contradicts the spec (design D2) | §16: the `mockups/` are the reference; variant 2 is layout-only |
| Mirrored child park (design D4) | Row added to §4.4 |
| No in-flight, accepted or copied states (design D5) | Command lifecycle in §4.1 |
| Subject cleared while its sheet is open (design D6) | §4.1 |
| "Sending to Warp…" never ends (design D7) | 15 s app text, 30 s server expiry (§6.6) |
| Primary hover fails AA (design D8) | Hover `#535fc6`, pressed `#4c57be` (§11.1, §14.1, X22) |
| No error token; `ErrorText` mapped to attention (design D9) | `Error` `#eb5757` (§14.1, X22) |
| Pending info in disabled ink (design D10) | Phase and CLI on a focusable `InkSubtle` line (§7) |
| Focus behind a full-width sheet (design D11) | Focus moves to the sheet heading (§3.2, §11.2) |
| Endless pulse (design D12) | Mark all seen and an in-app reduce-motion switch (§5) |
| Text scale clips fixed heights (design D13) | Scaled minimums; fold rule per scale (§3.2, §14.3) |
| `X` collides; `O` then digits in the palette (design D14, D15) | Keys from the catalog's `shortcut`; `X`/`Shift+X`; `Shift+O` only (§8, §6.1) |
| Running uncapped (design D16, product C3) | Running and With you capped at 5; `--large` fixture for the fold test (§3.2, §18.2) |
| Stale cross-references (design D17) | Fixed; Phases numbered §18 |
| App writes its own sentences (design D18) | §12.4 App-owned copy |
| Esc order, `G` chord, urgency vocabulary, 1.8:1, unused tokens, `A` on a decision, §11.3 candidates, `takeoverIdle` WF edit (design D19–D26) | §8, §5, §11, §14.3, §6.4; `A` on a decision only with its sheet open |
| Little value before the engine (product C2) | Accepted: v0 serves Needs you (decisions, failures, profile), the queue and launching; the five-second test also runs on `--large` |
| Run protected commands in Warp from the app (product C4) | **Deferred to the owner.** It would keep the typed confirmation in the terminal, but widens the launcher from agent sessions to arbitrary catalog CLI strings. Not designed here |
| No success measure (product C5) | §2: three numbers from the ledger |
| The dashboard's "every agent" goal dropped silently (product C6) | X24 |
| Dock badge and tray per platform; LiveSetting; reduce-motion toggle; hotspots; test budget; logging; `TimeProvider`; docs per phase (devex MEDIUM) | §18.2 (platform notes, budgets, docs), §9.1 (logging, time), §11.4 (in-app switch), §19 (hotspots) |
| Devex LOWs: single instance, prefs version, E-1 replaces `OpenItem`'s response, S1b's test-framework workaround, JetBrains Mono licence | §18.2 and §12.3; E-1 says "replacing"; U0 keeps S1b's `HeadlessUnitTestSession` workaround; the font ships with its OFL file |
| Security LOWs: launch-file sweep, `mcp.json` 0600, probe timeout and argv, navigate-only single-instance and deep links, atomic prefs, no body logging, loopback-only `control.json`, job lines, file-pick confirm, bidi, tray label escaping, agent-filed decisions under one key | §6.3, §6.5, §12.3, §9.1, §7, §8; loopback and job-line scrubbing are already S1b's and API §3.6's; tray labels escape `_` |

### Pass 2 → revision 3

Verdicts: product PASS, design PASS, devex PASS, architecture and security NEEDS_WORK (one shared HIGH). The pass-2
review is in `pass2/consolidated-review.md` beside the pass-1 files.

| Finding | Resolution |
|---|---|
| P2-1 (HIGH) Requests never expire while the service runs; a launch waiting on the takeover could open Warp late | A 5 s expiry timer at the 30 s deadline, the E-6 wait inside that deadline, a freshness re-check right before `ILauncher.Launch`; `SND-LAUNCH-008` counts only fresh requests (E-1, §6.6) |
| P2-2 Attach and Resume need engine states but sat in U6a | Moved to U6b with their own signal; U6a is Open only (§6.2, §18) |
| P2-3 `StartInstance` has no address for a nonce | It takes none; WF §6.1 makes it idempotent (§9.1) |
| P2-4 X18 and X20 carried the old design | Rewritten (§13, README) |
| P2-5 Mockups predate revision 2 | Updated with the `/impeccable` pass: Mark all seen, the Running cap, the Error token, the hover colour, the urgency words, and the launch flow's take-over frame. The critique showed the 800 px fold claim was wrong; §3.2's fold rule now says what fits (health stays in the pill) |
| P2-6…P2-10 (LOW) | E-6 wording and actor row; the pill's words split by source; E-17 pointer in §6.7; fixed keys reserved against catalog shortcuts; §7 sentence and the `primary-focus` row |

### Pass 3 → revision 4

Verdicts: all five lenses PASS (no HIGH). The reviews are in `pass3/`.

| Finding | Resolution |
|---|---|
| Expiry and launch can both record an outcome (security N1, arch A3-1) | One fact key `launch.outcome:<launchId>`, first write wins; the launcher claims before launching; a race test (E-1) |
| An instance started during an open launch runs beside it (security N2) | It starts taken over (§6.4) |
| A second Open before the engine is invisible (product G1) | `launch.lastStarted`; "Open another session" with a confirm (E-2, U6a) |
| Running rows showed Attach (design N1) | Running rows: Open ▾ and Take over; Attach only on With-you rows (§3.2, §4.2, §4.5); mockup difference listed in §16 |
| An empty Running panel has no sentence (design N2) | E-18: per-group `empty` in `QueueView` |
| E-17 isn't in the API spec; one golden per route can't drive states (devex) | E-17 is an edit to API §12, per route and scenario, fixed time and ids; U1 back-fills |
| LOWs: cap text, success-number owner, a takeover with no session, `answer-text` in §4.4, per-launch `mcp.json`, E-2 inputs, Resume takes over, E-6 key, the pill word | Fixed in §3.2, §2, §6.4, §4.4, §6.3, E-2, §6.2, E-6, §3.2 |

### Pass 4 → revision 5

Verdicts: all five lenses PASS (no HIGH). The reviews are in `pass4/`.

| Finding | Resolution |
|---|---|
| "Open another session" decided by the app; never clears; missing from §6.2, §4.9, §12.4 (product H1, design P4-1) | Served `launch.confirm` (set only while a started launch isn't taken over or handed back); rows in §6.2, §4.9, §12.4; noted in §16 |
| The launcher's claim is undefined; no limit on `Launch`; Attach and a second Open wait 30 s for a no-op takeover; starts-taken-over has no E-number (security P4-1, arch A4-1…A4-3) | Claimed set; a 10 s `Launch` limit; no wait when already taken over; E-19 (§18.1) |
| E-17 isn't where API-phase agents look (devex) | X26 in §13 and the README |
| LOWs: success-number queries, starts-taken-over causation and window, session id patterns, file deletion timing, X23 in U5, doctor's launcher line, E-18 order and intro, "view" vs "group", Home's summary copy | Fixed |

### Pass 5 → revision 6 (the last pass requested)

Verdicts: all five lenses PASS (no HIGH). The reviews are in `pass5/`.

| Finding | Resolution |
|---|---|
| The pre-engine confirm never clears (product I1, design P5-1) | A 2 h window (`launch.confirmWindow`), with a `FakeTimeProvider` test (§6.2, E-2) |
| Late or duplicate Warp opens after a `Launch` timeout; launch files kept after a failure (security P5-1) | `SND-LAUNCH-009` with "look for the tab" text and `launch.confirm`; files deleted after any outcome |
| A launch request racing `StartInstance` (arch A5-1); a 10 s `Launch` could open Warp ~40 s after the click | The launcher waits for the engine projection to reach the request, routes `open-request` to a just-created instance, waits for the trigger's recorded result; claims only with 10 s left; race test |
| LOWs: `launch.confirmWindow` in E-4; the success rate counts handoffs; E-19 in U6b and the README; §4.9's confirm; the summary line and headers in §12.4; the prototype in §16; "Open another session" isn't a mode | Fixed |

## 18. Phases

Each phase is one PR. It follows the slice recipe's spirit for the UI (a failing view-model test first, then the view,
then a headless view test with key frames), is written against the fake `ISindriApi` serving the API's golden JSON
(E-17), and is proven in the running app against a real control plane (§18.2). The UI phases start once this spec is
approved.

| # | Goal | Files (main ones) | Size | Depends on | Observable signal |
|---|---|---|---|---|---|
| **U0** | **Theme and shell.** §14's tokens and Fluent overrides; §3.2's window (top bar, Home's panel frame, detail sheet, health pill and strip) with S1b's status as Health; §4.1's pre-API states; single instance (a second launch sends "focus" over a local channel that only navigates); close-to-tray with the neutral and not-running tray states; `IMotionPreference` and the in-app switch; a LiveSetting spike on macOS | `Aw.Sindri.Ui/Theme/*`, `Views/Shell*`, `ViewModels/Shell*`, `Platform/IMotionPreference.cs`, the head's adapters | M | S1 merged; S1b rebased as its base | OS screenshot of the real `sindri-ui` in the theme; `sindri stop` → Not running in ≤ 2 s; a second launch focuses the first; key frames for every pre-API state; a contrast test over every §11.1 pair |
| **U1** | **Catalog, headers and freshness.** Headers; `GET /commands` with E-8; the control renderer (`ready`, `pending` with its focusable line, `cli-only` copy block); command lifecycle; `asOf` ("updated Ns ago", rebuilding, stalled + Rebuild views); the health pill from E-14; refresh on every event (E-15 when P5 ships); `X-Sindri-After`; palette shell and `?` sheet; the fixture script's first part | `Api/*`, `ViewModels/Controls/*`, `Views/CommandPalette*`, `scripts/sindri-ui-fixture.sh` | M | U0 + API P1, P2 (P5 optional) | The §18.2 stall shows the banner and **Rebuild views** is audited as `human:ui`; the palette lists every catalog row with its status |
| **U2** | **Queue, item detail, timeline, Create and Close item** | `Views/Queue*`, `Views/Item*`, view models | M | U1 + API P3 | `sindri-prove.sh human item add` appears without a click within 1 s; Close with a stale version shows `SND-ITEM-409`'s detail; five-second test passes on the fixture |
| **U3** | **Needs you, decisions, requests, Home, the pulse, Mark all seen, badges and the tray menu** | `Views/NeedsYou*`, `Views/Decisions*`, `Views/Home*`, `Platform/IDockBadge.cs`, tray | M | U2 + API P4 (+ E-7, E-16) | A session's `curl` request appears as one pulsing row; title `(1) Needs you · Sindri`; tray in the attention colour; Accept clears it everywhere; a CLI race shows "decided elsewhere"; reduced motion shows the outline; five-second test passes on `--large` |
| **U4** | **Services, jobs, Health** | `Views/Services*`, `Views/Health*` | S | U1 + API P6 | Run now streams job lines and Cancel stops them; a failing service raises a `failed` row that Run again clears |
| **U6a** | **Open in Warp.** The launch block and split button, the target menu with reasons and Check again, Open, "Sent to Warp", every §6.6 failure state. Before the engine, the item's row and sheet show "Sent to Warp 12m ago · Claude Code · tab `sindri ITM-…`" from `launch.lastStarted`, and, while the server sets `launch.confirm` (a launch started for the item in the last 2 h that hasn't since been taken over or handed back; a `FakeTimeProvider` test covers the window), the button reads **Open another session** and asks the server's `confirm.reason` before sending (Attach and Resume need the engine's states and come in U6b) | `Views/Launch*`, view models | M | U2 + API **P10a** (E-1…E-5, E-11) | One click opens a Warp tab titled `sindri ITM-…` running Claude Code in the repo; the row then shows "Sent to Warp … ago" and, from `launch.confirm`, the button becomes **Open another session** with a confirm; the session's transcript shows its first `sindri_item` call; a profile repo pointing at a missing folder shows `SND-LAUNCH-004` before the click; the other failures in the integration lane with the recording `ILauncher` |
| **U5** | **Engine surfaces.** §4.4's answers, mirrored child parks, Start, Complete, Take over, Hand back, idle-takeover rows | `Views/Park*`, `Views/Answer*`, view models | M | U3 + WF P4 and P5, the X7 projection (the first API phase after WF P4), E-9, E-12, E-13, and WF's §5.6/§6.3 edit (idle takeovers and desk tasks as `park` rows), and X23 settled (the nonce POST's class) | Each §4.4 row reproduced with a dev recipe: protected answers show only the CLI; desk answers resume the instance; Hand back returns the item to `running` |
| **U6b** | **Opening is a takeover; Attach and Resume** | the launch view models | S | U5 + U6a + API **P10b**, WF's E-6 trigger and E-19 | Open on a running item pauses it before Warp opens (ledger order: takeover, then `launch.started`); Attach on a with-you item brings Warp forward and starts no process; after Hand back, **Resume Claude Code session** reopens the same session id (transcript continues) |
| **U7** | **Context tab and map-review viewer** | `Views/Context*` | S | U2 + CTX P3/P4 + X9 (`GET /context`) | A degraded source shows the DEGRADED banner and the citation states; drafts read `(draft)` |
| later | Agents, Repos, Automatic work, Today, receipts, session lifecycle | — | — | API §11 collector spec | — |

### 18.1 Where each API and engine edit lands

| Edit | Lands in | Needed by |
|---|---|---|
| E-8 (`destructive`, `readsViews`, `shortcut`), E-12 (variant rows), E-16 (catalog `cli`) | API P1 (catalog); `CloseItem` variants in P3 | U1 |
| E-14 (`health`) | API P2 | U1 |
| E-7 (`actions`, `label`), E-16 (`NeedsYouRow.cli`) | API P4 | U3 |
| E-18 (per-group `empty`) | API P3 | U2, U3 |
| E-15 (`invalidates`, `GET /events/catalog`) | API P5 | U1 (optional until then) |
| E-17 (golden JSON) | every API phase from P1, for its own routes | every U phase |
| E-1…E-5, E-11 | API P10a | U6a |
| E-6 (WF trigger), E-19 (`StartInstance` starts taken over), P10b | WF P5 edit, API P10b | U6b |
| X23 (the nonce POST's class) | WF P4 | U5 |
| E-9, E-13, and E-12's engine rows | WF P2 registers every engine catalog row: desk and pause rows pending, which WF P4 and P5 flip to ready; the protected rows `cli-only:typed-confirmation` from the start, except **(SO-20)** `ApproveRecipe`, `ApproveProfile` and `ApproveAllRecipes`, which flip to `ready` with the setup-once phase SO-A2 | U5 |
| E-10 | the API spec's phase table | — |

**E-17.** Each API phase's integration tests write response bodies as golden JSON under
`tests/Aw.Sindri.Tests/Golden/Api/<route-slug>/<scenario>.json`, one file per route and scenario (for example
`needs-you/empty.json`, `needs-you/fixture.json`, `needs-you/stalled.json`, `items-id/parked.json`), so the fake can
drive §4.9's state matrix. The tests run with a fixed `FakeTimeProvider` and seeded ids, so every field is stable, not
only `asOf`. E-17 is an edit to the API spec's §12 (test strategy); U1's contract unit back-fills goldens for any API
phase that merged before the edit. The UI's contract test deserializes every golden through
`SindriUiJsonContext` and the fake `ISindriApi` serves them, so a wire change fails the UI's unit lane, not a prover
run. `deps.yaml` gains a `SindriUi -> Sindri` file edge for the goldens.

### 18.2 How agents prove a UI phase

- **Home and isolation:** API §12's `sindri-prove.sh`: a guarded temp `AW_SINDRI_HOME`, port 0, the phase's built
  `sindri` and `sindri-ui`.
- **The fixture ledger:** `scripts/sindri-ui-fixture.sh` (U1, extended by each phase) builds §16's scenario through real
  commands under `sindri-prove.sh`: `human item add` for the items, a session `curl` request (a decision row), a test
  service that fails (a failure row), a live profile edit (a profile row), and from U5 the dev recipes in `recipes/dev/`
  for parks and escalations. `--large` adds 30 items and 12 needs-you rows for the fold test.
- **A stalled projector:** the prover inserts one row with an unparseable payload of a registered type into the temp
  home's `ledger.db` with `sqlite3`. The projection throws, the views stall, and the banner shows. Recovery: delete the
  row, then **Rebuild views**. No production seam is added.
- **OS screenshots:** the prover writes the window bounds into the temp home's `ui/preferences.json` before starting
  `sindri-ui`, then captures with `screencapture -x -R<x>,<y>,<w>,<h>`. The terminal needs Screen Recording, granted
  once by the owner (S1b's evidence note). Headless frames are tests, not visual proof.
- **The five-second test:** a fresh subagent sees only the 1280 × 800 screenshot (no spec, no fixture) and answers the
  four questions of §2. It passes when all four answers match the fixture, on the default and `--large` fixtures.
- **Keyboard:** headless integration tests drive every key in §8 (chords, Esc layering, the key guard) and the focus
  order of §11.2; the PR adds a short manual pass for VoiceOver announcements (macOS live-region support is checked
  in U0's spike).
- **Reduced motion:** the in-app switch; key frames before and after.
- **Launch:** the integration lane uses a recording `ILauncher` (a fake with a contract test shared with
  `WarpLauncher`'s probe and plan building) for every failure state. The real-app proof on the owner's Mac covers the
  happy path (Warp tab title, the Claude transcript at `~/.claude/projects/<cwd slug>/<session-id>.jsonl` showing the
  `sindri_item` call) and the missing repo; it needs a logged-in `claude`, so it's an owner runbook step checked by
  the prover.
- **Platform notes:** the dock and taskbar badge is a port (`IDockBadge`): macOS `NSDockTile` through Objective-C
  interop, Windows `ITaskbarList3` overlay, Linux best effort (Unity `LauncherEntry` over D-Bus, else none). Avalonia's
  `TrayIcon` needs the AppIndicator extension on GNOME; the spec accepts that.
- **Test budget:** one key frame per state in the integration lane; full frame sets in the heavy lane, so each lane
  stays within LANE-PKG-001.
- **Docs per phase:** `docs/csharp/sindri-ui.md` gains the phase's views, routes, codes and proof recipe.

## 19. Parallel delivery with an agent team

An agent team orchestrated by `/specToProvenPR` builds the UI phases; the lead session only orchestrates.

### Dependency graph and waves

```
S1 ──► U0 ──► U1 ──┬──► U2 ──┬──► U3 ──► U5 ──► U6b
   (API P1,P2)     │   (P3)  │   (P4)   (WF P4,P5, X7)  (P10b, E-6)
                   │         ├──► U6a (P10a) ─────────────┘
                   │         └──► U7 (CTX + X9)
                   └──► U4 (P6)
```

| Wave | Runs at once | Stacked on | Notes |
|---|---|---|---|
| W0 | **U0** | S1 (S1b rebased) | No API phase needed |
| W1 | **U1** | U0 | Waits for API P1 and P2 to be ready |
| W2 | **U2** ∥ **U4** | U1 | U4 against the fake from P6's contract unit |
| W3 | **U3** ∥ **U6a** ∥ **U7** | U2 | U6a when P10a is ready; U7 when X9 is settled |
| W4 | **U5** | U3 | Engine-gated |
| W5 | **U6b** | U5 + U6a | P10b-gated |

### Units inside a phase

Each phase starts with a **contract unit** that runs alone: the `ISindriApi` additions (records, JSON context entries),
the goldens it reads, the fake's responses, the view-model interfaces and red tests, and the theme keys the phase adds.
Then one implementer per view fans out, each in its own `Views/<Name>*` and `ViewModels/<Name>*` folders.

### The team

| Role | Does | Never does |
|---|---|---|
| **Lead** | Keeps the wave board, dispatches the next ready unit, rebases stacked branches, relays owner questions | Write phase code |
| **Implementer** (≤ 3 at once) | One view: view-model test red → view model → view → headless test with key frames, in its own worktree | Edit another unit's folder, or a shared line that isn't its own |
| **Integrator** (per phase) | Merges units, resolves anchor lines, runs the merge gate, opens the draft PR | — |
| **Prover** (per phase) | Runs §18.2 against a real control plane, OS screenshots, the five-second test, the keyboard pass | Treat green tests as proof |
| **Design reviewer** (per phase) | Compares the running app with the mockups and runs `/impeccable` critique; checks contrast and focus order | Fix code |
| **Reviewers** | `/review` until zero blocking findings and zero issues | Fix code |

### Hotspots (one line per unit, appended at anchors)

`Api/SindriUiJsonContext.cs`, `ISindriApi.cs` and its fake, `Views/SindriApp.axaml` (merged dictionaries),
`Theme/Tokens.axaml` and `Theme/FluentOverrides.axaml` (keys added only in contract units), the shell's navigation
table, the key map, the palette's registration, the labels resource (§12.4), the head's composition, the preferences
record, `scripts/sindri-ui-fixture.sh`, `PublicAPI.Unshipped.txt`. Every other file belongs to exactly one unit. At
most two builds at once (the heavy-job lock's two slots).

### Done

A phase is done when its PR is ready (never merged by an agent): the signal shown in the running app with OS
screenshots, the merge gate green, the design review passed against the mockups, and the review loop at zero blocking
findings and zero issues.
