---
aw-doc: 1
id: sindri-linear-hub
kind: spec
title: "Sindri as the owner's Linear hub: work issues, start runs from them, keep both sides in step, plan across them"
status: draft
repos: [jthorare/agentic-workflow]
---

# Sindri as the owner's Linear hub

**Status:** first draft for the owner, revision 1 · **Date:** 2026-11-04 · **Lane:** context (this spec), with api (commands, workflow starts) and ui (pages) · **Builds on:** the owned index spec `2026-10-27-sindri-owned-index-design.md` (the record store, feeds, D8 read-only tokens, §6.1 "no live calls at query time"), the Projects page spec `2026-10-29-sindri-projects-page-design.md` (the `linear-projects` feed, the owner's writes, P1 "the page reads only stored data"), the PR review page spec #2076 (`2026-11-04-sindri-pr-review-page-design.md`: the Reviews tab, the docs layer), `docs/sindri/linear.md`, `docs/csharp/sindri-scoping.md` (`step.scope.read-tracker`) · **Next:** 3 /autoplan rounds, meta's UX check, the owner's read; then /specToProvenPR takes §12's phases.

Every file path below is one the author opened on main (951bc516d). Every command, route, event, key and type is marked **existing** or **new**. Facts about Linear's API that this repo doesn't hold yet are marked **(Ph0 checks)**.

## The owner's steps: install -> works

1. **Make a write key once.** In Linear: Settings → Account → Security and access → Personal API keys → a key with **write** permission. Then at the terminal: `sindri index connect linear --write`, then `sindri profile approve`. The read-only key the index already uses stays as it is (D2).
2. **Open Work.** Your Linear issues are listed next to Sindri's own items: assigned to you first, then your teams' open ones. Filter by team, project, cycle, state, assignee, priority or label.
3. **Change an issue.** Open it, change the state, assignee, priority, cycle, labels, title or description, or write a comment. It's written to Linear when you press Save, and the row shows the change at once.
4. **New issue.** `N I` (or **New issue** on Work or a project's page). Team is required; the rest is optional.
5. **Start a run from an issue.** On the issue's sheet: **Scope it**, **Build it** or **Fix it**. Sindri makes a Sindri item linked to the issue and starts the run. From then on the issue has one Sindri comment that tracks the run's state, and each PR the run opens is attached to the issue.
6. **Plan.** Projects → a project → **Plan**: its issues by state, each with its run, PR and review state. The **Cycle** view does the same for a team's current cycle.

## 0. Why

The owner runs their work from Sindri: PR reviews (#2076), workflows, agents, docs. Linear is where the team's issues live, and today Sindri only reads it (`docs/sindri/linear.md`: "Nothing is written to Linear"). So the owner switches to Linear for every status change, comment and new issue. They also copy issue text into Sindri by hand to start a run, and copy PR links back. The owner (2026-11-04): "sindri should also be able to tie into linear with an api key to be my project management hub as well."

What exists today (main 951bc516d):

| Piece | State | Where |
|---|---|---|
| Issue records | **Existing, read-only.** The `linear` feed stores the profile's teams' issues as `linear:issue/<uuid>` with comments, attachments and relations. Its state is in `State`/`StateRaw`. Assignee, labels, cycle and priority are **not stored** | `LinearIssueItem.cs`, `docs/sindri/linear.md` §4 |
| Project records | **Existing, read-only.** The `linear-projects` feed, plus the viewer (who owns the key) | Projects page spec §4.1, §4.2 |
| The key | **Existing, read-only.** `context.sources.linear.token`, a secret pointer, saved by `sindri index connect linear` (human-only, `SND-AUTH-002`) | `LinearSettings.cs`, `LinearClient.cs:63` |
| Item ↔ issue link | **Existing event, unused by any page.** `item.linked` (`ItemLinked`: item, record key, alias `ENG-142`) folds into the `item-aliases` view | `Aw.Sindri.Core/Events/ItemLinked.cs`, `Items/ItemAliasesProjection.cs` |
| Scoping from an issue | **Existing step, fixture adapter only.** `step.scope.read-tracker` reads through `ITrackerSource`, whose only adapter is `FileTrackerSource` (`AW_SINDRI_TRACKER_DIR`). With that unset, the step fails with `SND-SCOPING-005` | `docs/csharp/sindri-scoping.md:74` |
| Owner-only writes | **Existing pattern.** `ProjectWrite.Begin` refuses an agent session with `SND-AUTH-002` even though the desk class lets sessions through | `Aw.Sindri.Core/Projects/ProjectWrite.cs:17` |

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **Scope is all four parts: work issues in Sindri, issue → workflow, two-way links, planning views.** Owner, decided 2026-11-04 | Owner |
| D2 | **Two keys.** The index keeps its read-only key (`context.sources.linear.token`). Writes use a second pointer, `context.sources.linear.writeToken` (new), that only the write port resolves | The sync, the decision pass and every agent-reachable read path never hold a key that can change Linear. A bug or a prompt-injected record in the read path can't write. Open question 1 |
| D3 | **Pages read only stored data** (Projects P1, owned index §6.1). A write is **written through**: Linear's mutation answers with the changed issue, and Sindri stores that answer in the record store in the same command. The page shows the change at once without waiting for the next tick | Keeps the one read path. No live call when a page opens |
| D4 | **Every Linear write is the owner's action.** The commands are desk class with an owner check (`LinearWrite.Begin`, the `ProjectWrite.Begin` pattern): an agent session is refused with `SND-AUTH-002`. Two narrow service writes are allowed (D7) | Owner rule. Same as `effect.pr.merge` and the Projects page |
| D5 | **Field ownership is the conflict rule.** Linear owns every issue field (title, description, state, assignee, priority, cycle, labels, estimate, project). Sindri owns the run (its state, its comment, its PR attachments). No field has two automatic writers, so "both sides changed it" can only happen to a human edit in Sindri racing a human edit in Linear. That case is caught: Sindri checks the issue's `updatedAt` right before it writes, and a newer one is `SND-LINEAR-409` with "changed in Linear since you opened it" and a reload. Nothing is merged silently | A sync with two writers per field needs merge rules the owner would have to learn. One writer per field needs none |
| D6 | **No offline queue in v1.** A write that can't reach Linear fails with its code, and the edit stays in the form. Nothing is written later behind the owner's back | A queued write that lands an hour later is the silent overwrite D5 forbids |
| D7 | **Runs write back through one living comment and attachments.** Per linked issue, Sindri keeps ONE comment it edits as the run moves (started, needs you, PR opened, merged, done, parked), plus one attachment per PR. It doesn't move the issue's state unless the owner turns on a per-team state rule (§6.3) | One comment that changes beats a comment per step. The team's Linear-GitHub integration usually moves states on merge already, so Sindri moving them too would fight it |
| D8 | **No new top-bar tab.** Linear issues are listed in **Work** next to Sindri items, and planning views live on **Projects** (a project's Plan, a team's Cycle). The bar keeps #2076's nine tabs | Every tab costs width at the 1,000 px minimum (`sindri-ui.md:155`). Work is already "what's on my plate". Open question 3 |
| D9 | **Issue → run starts from the stored record.** A new `StoreTrackerSource` (Core port `ITrackerSource`, existing) reads the issue and its children from the record store, so `step.scope.read-tracker` works without `AW_SINDRI_TRACKER_DIR` and without a live call | The run's brief is the record as indexed, scrubbed and versioned, the same text search returns |
| D10 | **Every write is a ledger event** (`linear.issue.created`, `linear.issue.updated`, `linear.comment.created`; new), with the command id, the fields sent and Linear's answer version | Audit ("who changed ENG-12 from Sindri, when"), and a replay knows a create already happened |
| D11 | **Fixtures are made up** (the fake Linear in `tests/fixtures/linear/` gains mutations). No work-app names | Owner rule (app-agnostic toolkit) |

## 2. Goals and non-goals

**Goals.**
- REQ-1 The owner lists, filters and opens their Linear issues in Sindri, from stored data, within one tick of a change in Linear.
- REQ-2 The owner creates issues, edits their fields and comments from Sindri, and each change is in Linear and on the page when Save returns.
- REQ-3 The owner starts a scoping, build or bug-fix run from an issue in one click. The run's Sindri item is linked to the issue.
- REQ-4 A linked issue shows the run's state in one Sindri comment and every PR the run opens as an attachment, within one tick of the run moving.
- REQ-5 An issue cancelled or completed in Linear while its run is open puts the item in needs-decision. The run isn't stopped silently.
- REQ-6 A project's Plan and a team's Cycle view show each issue with its run, PR and review state.
- REQ-7 An agent session can read issues and propose one, and can never write to Linear.
- REQ-8 Project and cycle edits (name, state, lead, target date; cycle dates) work from Sindri (ship 3).

**Non-goals.** Replacing Linear's own boards, roadmaps, triage inbox, notifications or SLA rules. Webhooks (Sindri runs on a laptop with no inbound URL). Bulk edits beyond one field on a selection. Writing to any team the profile doesn't list.

**Success measure.** For two weeks after ship 1, the owner's Linear tab opens less than before (their word), and every write Sindri made matches what Linear shows (the ledger's `linear.*` events against Linear's issue history, checked once a week in Ph9).

## 3. Where it sits in the app

| Module | Reads | Writes | Reached from |
|---|---|---|---|
| **Work** (existing tab) | + Linear issues (stored) | + issue writes | top bar `G W` |
| **Issue sheet** (new, `sindri://linear/<ENG-12>`) | the issue record, its comments, its linked item and run, its PRs and their review state (#2076) | issue fields, comments; starts a run | a Work row, a Plan row, ⌘K by issue key |
| **Projects → Plan** (new view on the project page) | the project's issues + runs + PRs + reviews | none beyond the sheet's | a project card |
| **Projects → Cycle** (new) | a team's current cycle's issues, same columns | none | the Plan view's cycle switch |
| **New issue** (new sheet, `N I`) | teams, states, members, labels, cycles (stored) | `linear.issue.created` | Work, Plan, ⌘K |

**With #2076.** Reviews stays the ninth tab, and this spec adds none. A Review page's "Send as fix task" makes a Sindri item (#2076 §8.2). In ship 3 it can also create a Linear issue when the PR's own issue is in Linear (open question 5). The docs layer's `issues:` front-matter key accepts Linear keys (`ENG-12`), so a Plan row shows the design-doc phase that names its issue. That's a read of #2076's `IContextDocs`, nothing new in the store.

## 4. Data: what the store gains (context lane)

### 4.1 Issue fields (new, Ph1)

The `linear` feed already fetches the issue. It gains the fields it drops today, stored in a side table, not in the text (the text stays what search reads):

`linear_issue` (migration, new): `key` (PK, FK to `source_record` ON DELETE CASCADE), `team_id`, `identifier` (`ENG-12`), `state_id`, `state_type` (`backlog`, `unstarted`, `started`, `completed`, `canceled`, `triage`), `assignee_id`, `priority` (0 to 4), `estimate`, `cycle_id`, `project_id`, `parent_id`, `labels` (JSON array of ids), `due_date`, `sort_order`, `updated_at`. Indexed by `(assignee_id, state_type)`, `(project_id)`, `(cycle_id)`.

The feed's version suffix moves from `;f2` to `;f3`, so every stored issue is re-saved once with its fields. That's the same one-time re-read 0020 did (`docs/sindri/linear.md` §5), and `sindri index status` shows it the same way.

### 4.2 Lookups (new feed `linear-meta`, Ph1)

The pickers need the profile's teams' workflow states, cycles (current, next, last 3), members and labels. A new feed, source id `linear-meta`, reads them once per tick into one small table each (`linear_state`, `linear_cycle`, `linear_member`, `linear_label`; keyed by Linear id, with `team_id`). It gets its own source id because the driver keys cursor, lock and outcome by source (Projects spec P2).

### 4.3 Read path (new, Ph1)

`IContextIssues` on the context facade (the `IContextProjects` pattern): `List(filter, after)`, `Get(key)`, `Lookups(team)`. It reads the store only. Sindri exposes `GET /linear/issues`, `GET /linear/issues/{key}`, `GET /linear/lookups`, the CLI `sindri linear issues|show`, and the MCP tool `sindri_linear` (read). Issue text in a result is fenced as untrusted, like every record. An agent session sees only issues of teams mapped to its repo (the audience the `linear` feed already sets).

## 5. Writes (api lane, with context's adapter)

### 5.1 The key

`context.sources.linear.writeToken` (new): a secret pointer with the same schemes and the same loopback rule as `token` (`LinearSettings.cs`). `sindri index connect linear --write` (new flag, human-only) asks for the key, checks it can read the profile's teams and that Linear reports write permission on it **(Ph0 checks how: the key's scope field, or a dry mutation)**, stores it in the keychain as `sindri-index/linear-write`, and writes the pointer. Doctor's `context:linear` row adds `write key: resolves | not set`. Without a write key, every write button is shown disabled with "Connect a write key: `sindri index connect linear --write`".

### 5.2 The port

`ILinearWrites` (new, `Aw.Context.Core`), adapter in `Aw.Context.Infrastructure/Linear/` beside `LinearClient`, reusing `LinearHttp` and its error mapping. The adapter resolves `writeToken` per call and never `token`, and the read client never resolves `writeToken`. An architecture test checks both directions. Mutations **(names Ph0 checks against Linear's schema)**: `issueCreate` (with a client-made `id`, so a retried create is the same issue), `issueUpdate`, `commentCreate`, `commentUpdate`, `attachmentLinkURL`, `projectUpdate`, `cycleUpdate`.

### 5.3 Commands (new)

| CLI | API | Class | What |
|---|---|---|---|
| `sindri linear new --team ENG --title T [--state S] [--assignee me\|<name>] [--priority 0-4] [--project P] [--cycle current\|next] [--label L]... [--parent ENG-1] [--description-file F]` | `POST /linear/issues` | desk, owner | create |
| `sindri linear edit <ENG-12> --version <updatedAt> [field flags]` | `PATCH /linear/issues/{key}` | desk, owner | edit the named fields only |
| `sindri linear comment <ENG-12> (--body T \| --body-file F)` | `POST /linear/issues/{key}/comments` | desk, owner | comment |
| `sindri linear project edit <key> --version V [--state] [--lead] [--target]` | `PATCH /linear/projects/{key}` | desk, owner | ship 3 |
| `sindri linear cycle edit <id> --version V [--starts] [--ends] [--name]` | `PATCH /linear/cycles/{id}` | desk, owner | ship 3 |

Every one of them goes through `LinearWrite.Begin` (new, `Aw.Sindri.Core/Linear/`): no caller or an agent session is `SND-AUTH-002` ("Linear writes are the owner's: an agent session can't change Linear (fix: the human runs `sindri linear …`, or file a proposal: §5.5)"). Then it does the D5 check (read the issue's `updatedAt` live, compare with `--version`), sends the mutation, stores Linear's answer through the record store's write path (D3), and appends the ledger event (D10). A team the profile doesn't list is `SND-LINEAR-003`, refused before any call.

### 5.4 Worked example: a conflict

The owner opens ENG-12 at 10:00 (stored `updatedAt` 09:58). A teammate moves it to Done in Linear at 10:01. At 10:02 the owner changes its priority in Sindri and presses Save. Sindri reads ENG-12's `updatedAt` live: 10:01, newer than 09:58. The save returns `SND-LINEAR-409`, the sheet says "ENG-12 changed in Linear at 10:01 (state: Done). Reload to see it, then save again", and the priority edit stays in the form. Nothing was sent. The remaining gap (a change between the check and the mutation, a few hundred ms) is accepted and named here. Linear's API has no conditional update **(Ph0 checks)**.

### 5.5 Agent proposals (ship 3)

An agent session can file a `request` (existing action class `Request`, open to sessions) of kind `linear.propose` with an issue draft (team, title, description, labels). It shows in Needs you, and the owner creates it with one click (it becomes `POST /linear/issues` as the owner) or dismisses it. Nothing reaches Linear without that click.

## 6. Issue → run, and back (api lane)

### 6.1 Start

On the issue sheet: **Scope it** (`workflow.scoping` with `scopeInput = { kind: tracker, key: linear:issue/<uuid> }`), **Build it** and **Fix it** (the recipes whose intake takes a tracker key; Ph0 lists which shipped recipes do, and a button whose recipe can't take one isn't shown). The start command (new, `POST /linear/issues/{key}/runs`, desk, owner, or a session for scoping only, open question 4):
1. Makes a Sindri item titled from the issue and appends `item.linked` (existing) with the record key and the alias `ENG-12`.
2. Starts the run with that item.
3. Creates the issue's Sindri comment (§6.2).

A second start on an issue whose item is open asks first: "ENG-12 already has a running build (item it_…). Start another?"

### 6.2 Back to Linear (service writes)

A new service, `linear-writeback` (`service:linear-writeback`), watches the ledger for linked items. On each run change it edits the issue's one Sindri comment (`commentUpdate`; the comment id is kept in the `item-aliases` row). The comment holds: run state in words, what it's waiting on, the PR links, and a deep link `sindri://item/<id>`. On each PR the run opens it adds an attachment (`attachmentLinkURL`). These two are the only service writes, and they're only allowed on issues the owner linked by starting a run. The row's grant is `service:linear-writeback` on the desk class (`CommandRow.ServiceGrants`, existing). `context.sources.linear.writeback: off` (new) turns it off.

### 6.3 State moves (opt-in, per team)

`context.sources.linear.stateRules` (new, default none): per team, `onRunStart: <state name>` and `onMerged: <state name>`. A move is made only when the issue is still in the state Sindri last stored (else skipped and noted in the comment: "left as In Review: changed in Linear"). Without rules Sindri never moves a state (D7).

### 6.4 From Linear (pull side)

On each `linear` tick, a linked issue whose `state_type` became `completed` or `canceled` while its item is open appends `item.decision.requested` (existing kind) with "ENG-12 was closed in Linear. Stop the run, or keep it?" The run is never stopped without the owner's answer (REQ-5). A linked issue reassigned away from the owner only notes it in the item's timeline.

## 7. Planning views (ui lane, ship 3)

**Plan** (a project's page): columns by Linear state type. Each row is the issue key and title, assignee, priority, the linked run's state chip (running, needs you, parked, done), the PR with its CI and review state (#2076's review status), and the design-doc phase whose `issues:` names it (#2076's docs layer). Filters: assignee, cycle, has run, has PR, needs review. **Cycle**: the same for a team's current cycle, with the cycle's dates and done/total. Both read stored data only: issue fields (§4.1), items and instances (`views.db`), PR records, review state.

## 8. Errors (new)

| Code | When | Text |
|---|---|---|
| `SND-LINEAR-001` | `writeToken`, `writeback` or `stateRules` key wrong | names the key path (the `SND-CONTEXT-001` pattern) |
| `SND-LINEAR-002` | a write with no write key | "Connect a write key: `sindri index connect linear --write`." |
| `SND-LINEAR-003` | a write to a team the profile doesn't list | "ENG isn't in context.sources.linear.teams." |
| `SND-LINEAR-004` | Linear refused the write key (revoked, read-only) | "Linear refused the write key. Make a new one with write permission." |
| `SND-LINEAR-409` | the D5 check found a newer `updatedAt` | §5.4's text |
| `SND-LINEAR-010` | Linear unreachable or rate limited during a write | "Linear didn't answer, so nothing was changed. Try again." (D6) |
| `SND-AUTH-002` (existing) | an agent session's write | §5.3's text |

## 9. Security

- **The write key never meets the read path** (D2): an architecture test checks that only `ILinearWrites`'s adapter resolves `writeToken`.
- **Record text is untrusted.** An issue description can't trigger a write. Every write has a human caller, or is one of the two service writes on an issue the owner linked.
- **Writes are limited to the profile's teams** (`SND-LINEAR-003`), checked before any call.
- **Every write is in the ledger** (D10), with no key and no body text beyond what the owner typed.

## 10. Docs to update

`docs/sindri/linear.md` (write key, writeback, state rules, the read-only line), `docs/csharp/sindri-profile.md` (`writeToken`, `writeback`, `stateRules`), `docs/csharp/sindri.md` (CLI and errors), `docs/csharp/sindri-ui.md` (Work's Linear rows, the issue sheet, `N I`, Plan and Cycle), `docs/csharp/sindri-scoping.md` (`StoreTrackerSource`), `planning/ERD.md` (`linear_issue`, the lookup tables), the owned index spec's D8 and the Projects spec's non-goal (both say read-only: a pointer to this spec), AGENTS.md Commands, `src/Sindri/aot-smoke/expected.txt` (the doctor row).

## 11. Rate and freshness

The `linear` feed ticks every 5 minutes today. A change in Linear shows in Sindri within one tick, and Sindri's own writes show at once (D3). Linear's API key limit **(Ph0 checks: documented as requests per hour plus a complexity budget)** is shared by the feeds and the writes. Ph0 measures one tick's cost on the fixture and on the owner's real teams, and the doctor row warns past half the budget. Open question 2 asks whether 5 minutes is fast enough while the owner is working issues.

## 12. Phases and parallel plan (for /specToProvenPR)

Ship 1 = Ph0 to Ph3 (work issues in Sindri). Ship 2 = Ph4, Ph5 (issue → run and back). Ship 3 = Ph6, Ph7 (planning, project and cycle edits, proposals).

| Phase | Lane | What | Depends on | Proof |
|---|---|---|---|---|
| **Ph0** | context | Linear facts: mutation names and inputs, client `id` on create, conditional update (none?), key scopes and how to read them, rate limit numbers. The fake Linear (`tests/fixtures/linear/`) gains those mutations and a write-key check. Shipped recipes that take a tracker key | — | a probe script over the real API with a scratch team (owner-run) and over the fake; the facts written into §5 |
| **Ph1** | context | `linear_issue` + lookup tables (migration), `;f3` re-read, `linear-meta` feed, `IContextIssues` | Ph0 | unit: field mapping per issue shape; integration: a tick over the fake stores fields and lookups, a `;f2` store re-reads once |
| **Ph2** | api + context | `writeToken` key + `connect --write`, `ILinearWrites` adapter, `LinearWrite.Begin`, the three issue commands, D5 check, write-through, ledger events, errors, doctor row | Ph0, Ph1 | unit per command and refusal (`SND-AUTH-002` for a session, 003, 409); integration on the fake: create → stored at once, retry same id → one issue; architecture test on the two keys; aot-smoke `expected.txt` |
| **Ph3** | ui | Mockups first (`/design-mockup`: Work with Linear rows, the issue sheet, New issue; minimum width and 1,440 px), owner's pick, then the views | Ph1 read goldens (hand-written first), Ph2 | UI tests: filter, edit, conflict reload, disabled-without-key; screenshot run on the fake |
| **Ph4** | api | `StoreTrackerSource`, the start command, `item.linked` on start, the second-start question | Ph1 | integration: scoping from a stored issue without `AW_SINDRI_TRACKER_DIR`; recipe eval case `tracker-from-store` |
| **Ph5** | api | `linear-writeback` service (living comment, PR attachments), state rules, the pull-side decision on closed issues | Ph2, Ph4 | integration on the fake: a run's moves edit one comment (count = 1), a PR adds one attachment, a closed issue makes one decision, no state move without a rule |
| **Ph6** | ui | Plan and Cycle views | Ph1, Ph4, #2076's review state and `IContextDocs` | UI tests over a fixture with runs, PRs and reviews |
| **Ph7** | api + ui | project and cycle edits, agent proposals (`linear.propose`) | Ph2 | unit + UI tests; a session's proposal needs the owner's click |
| **Ph8** | — | Owner proof (§13), two-week measure | each | §13 |

**Parallel plan.** Day one: context on Ph0, then straight into Ph1. ui on Ph3's mockups (they need only this spec). api reads Ph0's facts and starts Ph2's slices red against the fake. After Ph1: api runs Ph2 and Ph4 in parallel (different packages: Linear writes vs workflow start), ui builds Ph3 against hand-written goldens. Ph5 follows Ph2 + Ph4, and Ph6 follows Ph4 and #2076's Ph5. Context is free after Ph1 for #2076's Ph1/Ph2 (the docs layer), which this spec's Ph6 reads.

**Gates per phase.** `-warnaserror` build; the touched projects' unit and integration lanes; `Aw.Architecture.Tests` (new folders, the key test); scoped format; `check-profile-doc.mjs` (Ph2, Ph5); `check-env-example.sh`; ApiGoldens classes (Ph1, Ph2, Ph4); `Aw.Sindri.Ui.Tests` (Ph3, Ph6, Ph7); aot-smoke with `expected.txt` (Ph2's doctor row); recipe evals (Ph4).

## 13. Proof

| Row | Check |
|---|---|
| **TP-1** | On the fake: a tick stores fields and lookups for every fixture issue; Work lists them filtered by assignee, cycle and label; a change in the fake shows after one tick. Verifies REQ-1 |
| **TP-2** | Create, edit two fields, comment: each is in the fake and on the page when the command returns; a retried create makes one issue; the ledger holds three `linear.*` events. Verifies REQ-2 |
| **TP-3** | The §5.4 conflict on the fake: `SND-LINEAR-409`, nothing sent, the form kept. Verifies D5 |
| **TP-4** | An agent session's create, edit and comment: `SND-AUTH-002` each, nothing sent; its read and its proposal work. Verifies REQ-7 |
| **TP-5** | Scope it from a stored issue: the brief is the issue, the item is linked, the issue has one Sindri comment. Verifies REQ-3 |
| **TP-6** | The run moves four times and opens a PR: still one comment (edited), one attachment. Verifies REQ-4 |
| **TP-7** | The issue is completed in the fake mid-run: one decision, the run still running until answered. Verifies REQ-5 |
| **TP-8** | Plan and Cycle on the fixture show run, PR, review and doc phase per row. Verifies REQ-6 |
| **TP-9** | On the owner's laptop, a scratch Linear team: connect the write key, create, edit, comment, start a scoping run, see the comment in Linear. Verifies REQ-1 to REQ-4 for real |

## 14. Open questions for the owner

1. **One key or two?** I'd go with two: the index keeps its read-only key, and writes get their own key that only the write path loads. Then nothing that reads Linear text (the sync, the decision pass, agent sessions' reads) holds a key that can change Linear. The cost is one more key to make. The alternative is one read+write key for everything, which is simpler but gives every read path write power.
2. **Is a 5-minute sync fast enough?** I'd say yes for v1, since your own writes show at once (D3) and only teammates' changes wait up to 5 minutes. If it bugs you, the next step is a 1-minute tick while the app is open (it uses more of Linear's rate budget, measured in Ph0).
3. **Linear issues in Work, or their own tab?** I'd go with Work plus Plan/Cycle on Projects, so no tenth tab at the 1,000 px minimum. The alternative is an Issues tab, with the window's minimum width measured again.
4. **May an agent session start a scoping run from an issue?** I'd say yes for scoping only: it reads, writes nothing to code, and the comment it makes is Sindri's service write. Build and fix stay owner-started. Alternatively, owner-only for all three.
5. **Should "Send as fix task" on a review (#2076) also create a Linear issue when the PR's issue is in Linear?** I'd say not by default: a checkbox on the fix-task sheet, off by default, ship 3.
6. **State rules (§6.3): any defaults?** I'd say none. Sindri never moves an issue's state until you set a rule per team, because your Linear-GitHub integration may already move it on merge.
