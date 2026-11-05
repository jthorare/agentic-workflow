---
aw-doc: 1
id: sindri-linear-hub
kind: spec
title: "Sindri as the owner's Linear hub: work issues, start runs from them, keep both sides in step, plan across them"
status: draft
repos: [jthorare/agentic-workflow]
---

# Sindri as the owner's Linear hub

**Status:** draft for the owner, revision 2 (/autoplan round 1 folded in) · **Date:** 2026-11-04 · **Lane:** context (this spec), with api (commands, workflow starts) and ui (pages) · **Builds on:** the owned index spec `2026-10-27-sindri-owned-index-design.md` (the record store, feeds, D8 read-only tokens, §6.1 "no live calls at query time"), the Projects page spec `2026-10-29-sindri-projects-page-design.md` (the `linear-projects` feed, the owner's writes, P1 "the page reads only stored data"), the PR review page spec #2076 (`2026-11-04-sindri-pr-review-page-design.md`: the Reviews tab, the docs layer), `docs/sindri/linear.md`, `docs/csharp/sindri-scoping.md` (`step.scope.read-tracker`) · **Next:** /autoplan rounds 2 and 3, meta's UX check, the owner's read; then /specToProvenPR takes §12's phases.

Every file path below is one the author opened on main (951bc516d). Every command, route, event, key and type is marked **existing** or **new**. Facts about Linear's API that this repo doesn't hold yet are marked **(Ph0 checks)**, and Ph0 is a go/no-go with a written fallback for each (§12).

## The owner's steps: install -> works

1. **Connect write access once.** In Linear: Settings → Account → Security and access → Personal API keys → a key with **write** permission. In Sindri: Settings → Connections → Linear → **Connect write access**, paste it, **Check and save**. Sindri checks it can see your teams and write, stores it in the keychain and approves the profile change in the same click when it's the only one waiting. The read-only key the index already uses stays as it is (D2). The terminal path `sindri index connect linear --write` does the same.
2. **Open Work** (`G Q`) and pick **Linear** on its switch. Your issues: assigned to you first, then your teams' open ones. Filter by team, project, cycle, state, assignee, priority or label. A strip on top says what changed since you last looked (new assignments, mentions, teammates' state moves).
3. **Change an issue.** Open it, change the state, assignee, priority, cycle, labels, title or description, or write a comment. It's written to Linear when you press Save, and the row shows Linear's answer.
4. **New issue.** `C` on Work's Linear view (or **New issue** on a project's page). Team is required; the rest is optional.
5. **Start a run from an issue.** On the issue's sheet: **Scope it**. Sindri makes a Sindri item linked to the issue and starts the run. From then on the issue has one Sindri comment that tracks the run's state, and each PR the run opens is attached to the issue. **Build it** and **Fix it** come in a later phase (§6.1).
6. **Link by hand.** On an item's sheet, **Link issue** (or `sindri item link <item> ENG-12`). Sindri suggests a link when an item's title, branch or PR names a stored issue key; a suggestion is never applied without your click.
7. **Plan.** Projects → a project → **Plan**: its issues by state, each with its run, PR and review state. The Projects header's **Cycle** switch does the same for a team's current cycle.

## 0. Why

The owner runs their work from Sindri: PR reviews (#2076), workflows, agents, docs. Linear is where the team's issues live, and today Sindri only reads it (`docs/sindri/linear.md`: "Nothing is written to Linear"). So the owner switches to Linear for every status change, comment and new issue. They also copy issue text into Sindri by hand to start a run, and copy PR links back. The owner (2026-11-04): "sindri should also be able to tie into linear with an api key to be my project management hub as well."

What exists today (main 951bc516d):

| Piece | State | Where |
|---|---|---|
| Issue records | **Existing, read-only.** The `linear` feed stores the profile's teams' issues as `linear:issue/<uuid>` with comments, attachments and relations, and a known-as link `linear:issue-key/ENG-12`. Its state is in `State`/`StateRaw`. Assignee, labels, cycle and priority are **not stored** | `LinearIssueItem.cs`, `docs/sindri/linear.md` §4 |
| Project records | **Existing, read-only.** The `linear-projects` feed, plus the viewer (who owns the key) | Projects page spec §4.1, §4.2 |
| The key | **Existing, read-only.** `context.sources.linear.token`, a secret pointer (`env:`, `file:`, `keychain:`, `op:`), saved by `sindri index connect linear` (human-only, `SND-AUTH-002`) | `LinearSettings.cs:17`, `LinearClient.cs` `Key()` |
| Item ↔ issue link | **Existing event, never appended.** `item.linked` (`ItemLinked`: command id, item, record key, alias `ENG-142`) folds into `item-aliases` (`AliasRow(Alias, Item, Record, Sequence)`, keyed by alias: a re-link moves the key). Nothing appends it today | `Aw.Sindri.Core/Events/ItemLinked.cs`, `Items/ItemAliasesProjection.cs`, `Core/Views/AliasRow.cs` |
| Scoping from an issue | **Existing step, fixture adapter only.** `step.scope.read-tracker` reads through `ITrackerSource.Read(key)` ("an issue key or a project name"), whose only adapter is `FileTrackerSource` (`AW_SINDRI_TRACKER_DIR`). With that unset, the step fails with `SND-SCOPING-005`. Only `workflow.scoping.yaml` reads a tracker | `docs/csharp/sindri-scoping.md:74`, `recipes/sindri/workflow.scoping.yaml` |
| Owner-only writes | **Existing pattern.** `ProjectWrite.Begin` refuses an agent session with `SND-AUTH-002` even though the desk class lets sessions through | `Aw.Sindri.Core/Projects/ProjectWrite.cs:17-21` |
| Sindri reads Context | **Existing pattern.** Through a Sindri-side port in Core (`IProjectFacts`), implemented in Host over the context facade (`IContextProjects`). `Aw.Sindri.Features` is at its fan-out limit of 4 project references (`deps.yaml`) | `Aw.Sindri.Core/Projects/Facts/` |
| Fake Linear | **Existing, in-process.** `HttpMessageHandler` fakes in `tests/Aw.Context.Tests/Linear/` (`FakeLinearIssues`, `FakeLinearProjects`), read-only | `LinearFeedFakeServerTests.cs` |

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **Scope is all four parts: work issues in Sindri, issue → workflow, two-way links, planning views.** Owner, decided 2026-11-04 | Owner |
| D2 | **Two keys.** The index keeps its read-only key (`context.sources.linear.token`). Writes use a second pointer, `context.sources.linear.writeToken` (new), that only the write adapter resolves | The sync, the decision pass and every agent-reachable read path never hold a key that can change Linear. Open question 1 |
| D3 | **The write key lives in an owner-unlocked store.** `writeToken` accepts only `keychain:` and `op:`. `env:` and `file:` are refused with `SND-LINEAR-001`, except with a loopback url (the fake, in tests) | Agent sessions run as the owner's uid and inherit the shell's environment. An `env:` or `file:` write key would let a session call Linear with curl, past `SND-AUTH-002` and the ledger. The pointer scheme is the real boundary, so it's stated here, not left to taste |
| D4 | **Pages read only stored data** (Projects P1, owned index §6.1). A write is **written through**: after the mutation, the write adapter re-reads the issue with the feed's own query (`Issue.graphql`) and maps it with the feed's own mapper (`LinearIssueItem.Of`), and commits it under the `linear` source's lock. `IRecordStore.Commit` gains one rule: a `linear` record is never replaced by one with an older `updatedAt` | Without the lock and the rule, a tick that fetched before the save and commits after it puts the old value back. The page would flip back after Save. Same mapper means the next tick finds the same text and leaves it alone |
| D5 | **Every Linear write is the owner's action.** The commands, including starting a run from an issue, are desk class with an owner check (`LinearWrite.Begin`, the `ProjectWrite.Begin` pattern): an agent session is refused with `SND-AUTH-002`. The writeback service (D8) writes only to issues an owner command linked | Owner rule. Same as `effect.pr.merge` and the Projects page. A session-started run would make a service write the owner never asked for |
| D6 | **Field ownership is the conflict rule.** Linear owns every issue field (title, description, state, assignee, priority, cycle, labels, estimate, project). Sindri owns the run (its state, its comment, its PR attachments). One exception: a per-team state rule (§6.3) lets Sindri move `state`, guarded by "only if the state is still the one Sindri stored". A human edit in Sindri racing one in Linear is caught **per field**: right before it writes, Sindri reads the issue live and compares each field it's about to send with the value the owner opened. A changed one is `SND-LINEAR-409`, and nothing is sent. Nothing is merged silently | Comparing the whole issue's `updatedAt` would refuse the owner's priority edit because a teammate commented, or because Sindri's own comment bumped it |
| D7 | **No offline queue, and honest outcomes.** A write that can't reach Linear fails with its code and the edit stays in the form. Two outcomes are kept apart: **refused** (nothing was sent: `SND-LINEAR-010`) and **unknown** (sent, no answer: `SND-LINEAR-011`). After an unknown, Sindri re-reads the issue and shows "already applied" when Linear has the change, else offers Retry | A queued write that lands an hour later is the silent overwrite D6 forbids. "Nothing was changed" after a timeout can be false, and a retry then double-posts a comment |
| D8 | **Runs write back through one living comment and attachments**, from a service with its own durable state. Per linked issue: ONE comment Sindri edits as the run moves (started, needs you, PR opened, merged, done, parked), plus one attachment per PR. The service is level-triggered: per item it stores the comment id, the hash of the last text written and the attachments written, and on each pass it writes only when the wanted text differs. It doesn't move the issue's state unless the owner turns on a state rule (§6.3) | One comment that changes beats a comment per step. Level-triggered means a restart or a ledger replay writes nothing twice. The team's Linear-GitHub integration usually moves states on merge already |
| D9 | **No new top-bar tab.** Linear issues are a **Linear** view on **Work** (a Sindri / Linear switch), and planning views live on **Projects** (a project's Plan, a team's Cycle). The bar keeps #2076's nine tabs. If the owner picks sections-in-cards over a ninth tab in #2076 (its open question 1), this still holds | Every tab costs width at the 1,000 px minimum (`sindri-ui.md:155`). Work is already "what's on my plate". Open question 3 |
| D10 | **Issue → run starts from the stored record.** A new `StoreTrackerSource` (Host; Core port `ITrackerSource`, existing) reads the issue and its children from the record store. The key it takes is the issue key `ENG-12`, resolved through the known-as link. The issue's text reaches the run fenced as untrusted, and comments by anyone but the owner are left out unless the owner ticks "include comments" | The run's brief is the record as indexed, scrubbed and versioned. A teammate's "also update the deploy script" in a comment isn't an instruction to a code-writing agent |
| D11 | **Every write is two ledger events**: `linear.write.requested` (new: command id, actor `owner` or `service:linear-writeback`, target, mutation kind, client id, field names, a hash of the values) before the call, and `linear.write.applied` / `linear.write.failed` (new: Linear's answer version or the code) after it. Refusals are `linear.write.refused` (new: actor, target, code; for `SND-AUTH-002`, `-003`, `-409`). No typed text goes into the ledger; it's in Linear and the record store | "Who changed ENG-12 from Sindri, and when" for owner and service writes alike. An intent with no outcome after a crash is shown to the owner as "unknown outcome: check ENG-12", never retried by itself |
| D12 | **Creates are idempotent from the draft.** The new-issue and comment forms mint a client id (`id` on `IssueCreateInput` and `CommentCreateInput`, **Ph0 checks**) when they open, and every retry of that draft sends the same id. Linear refusing a repeated id is read back and treated as success | A command id is new on every retry (`ProjectWrite.Append`), so it can't be the idempotency key |
| D13 | **Sindri reaches Linear through Sindri-side ports.** `ILinearIssues` (reads) and `ILinearWriter` (writes), both new in `Aw.Sindri.Core/Linear/`, implemented in Host over the context facade (`IContextIssues`) and the context write port (`ILinearWrites`). `StoreTrackerSource` lives in Host. `deps.yaml` gains no Features edge | The `IProjectFacts` pattern. `Aw.Sindri.Features` is at its fan-out limit |
| D14 | **Links come from three places**: starting a run (automatic), the owner's **Link issue** / `sindri item link|unlink` (new), and suggestions (an item's title, branch or PR names a stored issue key, or the PR already has a Linear attachment). Suggestions are shown, never applied | A link only from run starts leaves Plan full of runs with no issue and issues with no run |
| D15 | **Fixtures are made up.** One stateful fake Linear in `tests/Aw.Context.Testing/Linear/` (new): mutations change what it serves, and switches make it answer with a newer field, a 429, a revoked key, a repeated client id or no answer at all. Context, Sindri and the UI tests share it. No work-app names | Owner rule (app-agnostic toolkit). Sindri's and the UI's tests can't see `Aw.Context.Tests` internals |

## 2. Goals and non-goals

**Goals.**
- REQ-1 The owner lists, filters and opens their Linear issues in Sindri, from stored data, within one tick of a change in Linear.
- REQ-2 The owner creates issues, edits their fields and comments from Sindri, and each change is in Linear and on the page when Save returns, or the page says plainly that the outcome is unknown.
- REQ-3 The owner starts a scoping run from an issue in one click (ship 2), and a build or bug-fix run (ship 2b, once those recipes take a tracker key). The run's Sindri item is linked to the issue.
- REQ-4 A linked issue shows the run's state in one Sindri comment and every PR the run opens as an attachment, within one tick of the run moving.
- REQ-5 An issue cancelled or completed in Linear while its run is open asks the owner. The run isn't stopped silently.
- REQ-6 A project's Plan and a team's Cycle view show each issue with its run, PR and review state.
- REQ-7 An agent session can read issues and can never write to Linear, directly or through a service.
- REQ-8 The owner links and unlinks an item and an issue by hand, and sees suggested links.
- REQ-9 Work's Linear view says what changed since the owner last looked: new assignments to them, mentions, teammates' state moves on their issues.

**Non-goals.** Replacing Linear's own boards, roadmaps, triage inbox, notifications or SLA rules. Webhooks (Sindri runs on a laptop with no inbound URL). Bulk edits. Writing to any team the profile doesn't list. Archive, delete, relations (blocked-by) and file uploads in the issue editor: every sheet has **Open in Linear** for those.

**Later (not in this spec's ships).** Project and cycle edits. Agent proposals (a session drafting an issue for the owner's one click). "Send as fix task" on a review (#2076) creating a Linear issue.

**Success measure.** Before ship 1, count one week of the owner's issue edits and comments in Linear's history (the baseline). For two weeks after ship 1, `sindri linear history --since 7d` (new) shows two numbers each week: the share of the owner's issue edits and comments made through Sindri (target: half), and runs started from an issue (target: 3 a week after ship 2). The same weekly check compares each `linear.write.applied` event with Linear's issue history; any mismatch is a bug.

## 3. Where it sits in the app

| Module | Reads | Writes | Reached from |
|---|---|---|---|
| **Work → Linear** (new view on the existing tab) | Linear issues (stored), the since-you-looked strip | issue writes | top bar `G Q`, then the Sindri / Linear switch (it remembers the last pick) |
| **Issue sheet** (new, `sindri://linear/ENG-12`) | the issue record, its comments, its linked item and run, its PRs and their review state (#2076) | issue fields, comments; starts a run; link/unlink | a Work row, a Plan row, ⌘K by issue key |
| **Projects → Plan** (new view on the project page) | the project's issues + runs + PRs + reviews | none beyond the sheet's | a project card |
| **Projects → Cycle** (new) | a team's current cycle's issues, same columns | none | the Projects header's Projects / Cycle switch |
| **New issue** (new sheet) | teams, states, members, labels, cycles (stored) | `linear.write.*` | `C` on Work's Linear view, a project's page, ⌘K "New Linear issue" |
| **Settings → Connections → Linear** (new row) | whether the write key resolves | the keychain entry, the pointer, the approval | Settings, and every disabled write button's "Connect write access" link |

**The Work page's Linear view.** A filter bar (team, project, cycle, state, assignee, priority, label), rows grouped "Assigned to you" then "Your teams", 50 rows per page with "Show more". The switch shows the count of open issues assigned to the owner. The Sindri view is unchanged.

**States.** Every new surface uses the app's existing vocabulary (skeleton rows on first load only, the stale pill after 30 s, the Not running card):
- **Not connected** (no write key): the issue list works read-only; write controls are disabled with a "Connect write access" link to the Settings row; they enable live on `profile.approved`, no restart.
- **No Linear source at all**: the Linear view is an empty state with "Connect Linear" (the existing read-key setup).
- **First load after the `;f3` re-read**: "Updating issues… N of M" from `sindri index status` until the re-read ends.
- **Zero issues / filter-empty**: "No open issues assigned to you" / "No issues match these filters" with Clear filters.
- **Feed failing**: a banner with the last synced time and the code; rows stay.
- **Offline or Linear unreachable**: rows and sheets open from the store with "as of 10:05"; write controls disabled with "Linear unreachable" instead of failing on click.
- **Sindri stopped**: the existing Not running card.

**Conflict display** (D6). A 409 shows a banner on the sheet: "ENG-12 changed in Linear at 10:01". Each changed field is marked in the form with Linear's value beside the owner's, and two actions: **Use Linear's** (per field) and **Save mine anyway** (re-runs the check against the values just shown). Focus moves to the banner, and it's announced politely.

**Accessibility.** The new sheet, pickers and Plan columns follow the app's rules (F6 regions, focus never nowhere, live regions gated). State and priority chips carry text, not colour only. Focus after Save stays on the sheet's title; after a create it moves to the new row; after a 409 to the banner. The screen reader reads a row as "ENG-12, title, state, assignee, priority, run state". Ph3 and Ph6 name a UI test for each.

**Plan columns.** By Linear state type. Below 1,100 px the columns stack into grouped sections (the width at which sheets already cover the page).

**With #2076.** A Plan row's PR chip opens #2076's review page; the review page's header shows the issue chip, which opens `sindri://linear/ENG-12`. The docs layer's `issues:` front-matter key accepts Linear keys (`ENG-12`), so a Plan row shows the design-doc phase that names its issue. That's a read of #2076's `IContextLibrary`, nothing new in the store.

## 4. Data: what the store gains (context lane)

### 4.1 Issue fields (new, Ph1)

The `linear` feed already fetches the issue. It gains the fields it drops today, stored in a side table, not in the text (the text stays what search reads):

`linear_issue` (migration, new): `key` (PK, FK to `source_record` ON DELETE CASCADE), `team_id`, `identifier` (`ENG-12`), `state_id`, `state_type` (`backlog`, `unstarted`, `started`, `completed`, `canceled`, `triage`), `assignee_id`, `priority` (0 to 4), `estimate`, `cycle_id`, `project_id`, `parent_id`, `labels` (JSON array of ids), `due_date`, `sort_order`, `updated_at`, `field_updated_at` (JSON: per-field change time when Linear reports it, **Ph0 checks**; else null). Indexed by `(assignee_id, state_type)`, `(project_id)`, `(cycle_id)`.

**How it's written.** The parsed fields ride on `RecordWrite` (as #2076's library rows do), and `IRecordStore.Commit` replaces the row in the same transaction as the record. So a record and its side row are never out of step, and the cascade removes the row with a tombstoned record. A write-through (D4) is a batch of one through the same path, at the source's current position, so it can't move the cursor back.

The feed's version suffix moves from `;f2` to `;f3`, so every stored issue is re-saved once with its fields. That's the same one-time re-read 0020 did (`docs/sindri/linear.md` §5), and `sindri index status` shows it the same way.

### 4.2 Lookups (new feed `linear-meta`, Ph1)

The pickers need the profile's teams' workflow states, cycles (current, next, last 3), members and labels. A new feed, source id `linear-meta`, reads them once per tick into one small table each (`linear_state`, `linear_cycle`, `linear_member`, `linear_label`; keyed by Linear id, with `team_id`). These aren't records, so they're written by the feed's own commit (one transaction per tick, replacing the team's rows), not through `RecordBatch`. It gets its own source id because the driver keys cursor, lock and outcome by source (Projects spec P2). The lookups are owner-only: agent sessions don't write, so they never need a member list.

### 4.3 Read path (new, Ph1)

`IContextIssues` on the context facade (the `IContextProjects` pattern): `List(filter, after)`, `Get(key)`, `Lookups(team)`, `Since(marker)`. It reads the store only, and applies the record's audience to the side table too: an agent session sees only issues of teams mapped to its repo (the audience the `linear` feed already sets). Sindri exposes `GET /sindri/linear/issues`, `GET /sindri/linear/issues/{key}`, `GET /sindri/linear/lookups` (owner only), the CLI `sindri linear issues|show|history`, and the MCP tool `sindri_linear` (read: `issues`, `show`). Issue text in a result is fenced as untrusted, like every record.

**Since you last looked** (REQ-9). The app stores a marker (a time) per owner when the Linear view is shown. `Since(marker)` diffs stored rows: issues newly assigned to the viewer, comments that mention them, and state changes by others on issues assigned to them. No new Linear call.

## 5. Writes (api lane, with context's adapter)

### 5.1 The key

`context.sources.linear.writeToken` (new): a secret pointer, `keychain:` or `op:` only (D3), same url and loopback rule as `token` (`LinearSettings.cs`): the write adapter takes its url from the same validated settings. **Settings → Connections → Linear → Connect write access** (new) and `sindri index connect linear --write` (new flag, human-only) do the same thing: ask for the key, check it can read the profile's teams and that it can write **(Ph0 checks how: the key's scope if Linear exposes it, read-only; never a probe mutation on a real team)**, store it in the keychain as `sindri-index/linear-write`, write the pointer, and approve when it's the only waiting change (#2076's Settings rule). Writes need a non-empty `context.sources.linear.teams`: an empty list reads every team the key can see, which is too wide to write to. Doctor's `context:linear` row adds `write key: resolves | not set | refused`.

### 5.2 The ports

- **Context side.** `ILinearWrites` (new, `Aw.Context.Core`), adapter in `Aw.Context.Infrastructure/Linear/` beside `LinearClient`. The error mapping (`Read`, `Classify`, `Unavailable`) moves out of `LinearClient` into a shared `LinearCalls` type, and key resolution into two separate types, `LinearReadKey` and `LinearWriteKey`. An architecture test checks that only the write adapter references `LinearWriteKey` and the read client never does. A mutation that returns `success: false`, or `errors` next to `data`, is a failure. Mutations **(names and inputs Ph0 checks against Linear's schema)**: `issueCreate`, `issueUpdate`, `commentCreate`, `commentUpdate`, `attachmentLinkURL`. Each is an embedded `.graphql` file under `Operations/` with a golden.
- **Sindri side** (D13). `ILinearIssues` and `ILinearWriter` in `Aw.Sindri.Core/Linear/`, Host adapters.

### 5.3 Commands (new)

Routes follow the existing `/sindri/...` shape (`ProjectRows.cs`).

| CLI | API | Who | MCP | Exit | What |
|---|---|---|---|---|---|
| `sindri linear issues [--team T] [--assignee me\|<name>] [--state S] [--project P] [--cycle current\|next] [--label L]... [--json]` | `GET /sindri/linear/issues` | owner, session (its audience) | `sindri_linear issues` | 0 ok, 1 serve down, 2 usage | list |
| `sindri linear show <ENG-12> [--json]` | `GET /sindri/linear/issues/{key}` | owner, session | `sindri_linear show` | 0, 1 not found or serve down, 2 | one issue |
| `sindri linear issue new --team ENG --title T [--state S] [--assignee me\|<name>] [--priority 0-4] [--project P] [--cycle current\|next] [--label L]... [--parent ENG-1] [--description-file F] [--client-id U] [--json]` | `POST /sindri/linear/issues` | owner | none | 0 created, 1 serve down or unknown outcome, 2 refused or invalid | create |
| `sindri linear issue edit <ENG-12> [field flags] [--json]` | `POST /sindri/linear/issues/{key}/change` | owner | none | same | edit the named fields only |
| `sindri linear comment <ENG-12> (--body T \| --body-file F) [--client-id U] [--json]` | `POST /sindri/linear/issues/{key}/comments` | owner | none | same | comment |
| `sindri item link <item> <ENG-12>` / `sindri item unlink <item> <ENG-12>` | `POST /sindri/items/{item}/links` | owner | none | 0, 1, 2 | link (D14) |
| `sindri linear start <ENG-12> --recipe scoping [--include-comments]` | `POST /sindri/linear/issues/{key}/runs` | owner | none | 0 started, 1, 2 | §6.1 |
| `sindri linear history [--since 7d] [--json]` | `GET /sindri/linear/history` | owner | none | 0, 1, 2 | the success measure's counts and the write log |

`--json` prints the route's body (as everywhere since #272). On the CLI, the field flags' values are names (`--state "In Review"`, `--assignee me`, `--label bug`), resolved against the issue's team's stored lookups. The edit sends each changed field with the value the owner opened, for D6's check: the CLI reads them from the store when the command starts, the app from the sheet.

Every write goes through `LinearWrite.Begin` (new, `Aw.Sindri.Core/Linear/`): no caller or an agent session is `SND-AUTH-002` ("Linear writes are the owner's: an agent session can't change Linear (fix: ask the owner to run `sindri linear …`)"). A team the profile doesn't list is `SND-LINEAR-003`. Then it appends `linear.write.requested`, does the D6 check (one live read), sends the mutation, re-reads and commits (D4), and appends the outcome (D11).

**Input limits.** Title 1 to 255 characters, description and comment up to 64 KiB **(Ph0 checks Linear's own caps)**. State, assignee, label and cycle ids come from the stored lookups of the issue's team; `--parent` must be in the same team; `--cycle current|next` resolves from `linear_cycle`, and a cycle that rolled over since the last tick is `SND-LINEAR-006`.

### 5.4 Worked example: a conflict

The owner opens ENG-12 at 10:00: priority 2, state In Review. A teammate moves it to Done in Linear at 10:01 and comments. At 10:02 the owner changes its priority to 1 in Sindri and presses Save. Sindri reads ENG-12 live: priority is still 2 (unchanged, so the comment and the state move don't matter for this edit). The save goes through, and the row shows priority 1 and state Done from the re-read. Now the same, but the teammate changed the priority to 3 at 10:01: the live read says 3, not the 2 the owner opened. The save returns `SND-LINEAR-409`, the banner says "ENG-12 changed in Linear at 10:01", the priority field shows "Linear: 3 · yours: 1", and nothing was sent. The remaining gap (a change between the live read and the mutation, a few hundred ms) is accepted and named here. Linear's API has no conditional update **(Ph0 checks)**.

### 5.5 Worked example: no answer

The owner comments on ENG-12; the mutation times out after 15 s. The ledger has `linear.write.requested` (client id `c-7f…`) and `linear.write.failed SND-LINEAR-011`. Sindri re-reads ENG-12's comments: one with id `c-7f…` is there, so the sheet says "Comment posted (Linear was slow to answer)" and appends `linear.write.applied`. If it isn't there, the sheet says "Linear didn't confirm the comment. Retry?", and Retry sends the same client id, so it can't post twice.

## 6. Issue → run, and back (api lane)

### 6.1 Start

On the issue sheet: **Scope it** (`workflow.scoping` with `scopeInput = { kind: tracker, key: ENG-12 }`). Only `workflow.scoping.yaml` takes a tracker key today, so ship 2 is Scope it. **Build it** and **Fix it** are Ph4b: the build entry recipe (`workflow.build.yaml` or `workflow.line.yaml`; Ph4b picks the one an item starts) and the bug-fix entry (`workflow.investigate.yaml`, which hands to `workflow.reproduce.yaml`) gain a tracker intake (each a recipe version bump with its eval cases; `RCP-VERSION-001`). Until then the sheet shows only Scope it, and the scoping packet's handoff is how a scoped issue reaches a build. The start command (owner only, D5):
1. Makes a Sindri item titled from the issue and appends `item.linked` (existing) with the record key and the alias `ENG-12`.
2. Starts the run with that item. The brief's issue text is fenced (D10).
3. The writeback service creates the issue's Sindri comment on its next pass (§6.2).

A second start on an issue whose item is open asks first: "ENG-12 already has a running scoping run (item it_…). Start another?"

`AW_SINDRI_TRACKER_DIR`, when set, still wins (the fixture adapter for tests); `StoreTrackerSource` is used otherwise.

### 6.2 Back to Linear (service writes)

A new service, `linear-writeback`, runs in process with no route or CLI verb, so nothing a session can reach triggers it. Its state is a new view, `linear-writeback` (item, issue key, comment id, hash of the last text written, attachments written, failures in a row), folded from `linear.write.*` events with actor `service:linear-writeback`. So it survives a restart and a `views rebuild`.

Each pass (on a ledger change for a linked item, at most once per 30 s per item):
- It renders the wanted comment text from the item's current state. If its hash equals the last one written, nothing is sent.
- No comment yet: `commentCreate` with a client id derived from the item id, so a crash after the create and a retry can't make two.
- Else `commentUpdate`.
- Each PR the run opened with no attachment yet: `attachmentLinkURL` **(Ph0 checks it's idempotent by url per issue)**. Only forge PR urls and `sindri://` are allowed.
- It writes only to issues linked by an owner command (`item.linked` from an owner caller).

**The comment's text** (teammates read it in Linear, so plain facts first):

```
Sindri run: scoping, needs a decision from @owner
PR: https://github.com/<owner>/<repo>/pull/123 (CI green, review pending)
Updated 10:42 · item it_8k2 (sindri://item/it_8k2)
```

When the run starts needing the owner, the service posts a new short comment ("needs a decision from @owner") as well as editing the living one, because an edit sends no Linear notification. That's the only time it posts a second comment.

**Failures.** 429: the service parks until the `Retry-After` time and says so in Needs you. Revoked write key (`SND-LINEAR-004`), deleted comment (it creates a new one, once), deleted or archived issue, issue moved to a team the profile doesn't list: the item gets a Needs you note and the service stops writing to that issue after 3 failures in a row. The doctor row shows the count. `context.sources.linear.writeback: off` (new) turns the service off; turning it off mid-run leaves the comment as it is.

### 6.3 State moves (opt-in, per team)

`context.sources.linear.stateRules` (new, default none): per team, `onRunStart: <state name>` and `onMerged: <state name>`. A rule moves only the issue linked to the run, never other issues a PR names. A move is made only when the issue is still in the state Sindri last stored (else skipped and noted in the comment: "left as In Review: changed in Linear"). Each move is a `linear.write.*` event with actor `service:linear-writeback`. Without rules Sindri never moves a state (D8).

### 6.4 From Linear (pull side)

A Sindri service reads issue state through `ILinearIssues` after each `linear` tick. A linked issue whose `state_type` became `completed` or `canceled` while its item is open files a request (`request.filed`, existing) with "ENG-12 was closed in Linear. Stop the run, or keep it?", answered with `decision.recorded` from Needs you, with the issue chip. It's filed once per transition: reopened then closed again asks again. The run is never stopped without the owner's answer (REQ-5). A linked issue reassigned away from the owner, deleted, or moved to an unlisted team is noted in the item's timeline; deleted and moved also stop the writeback for it.

## 7. Planning views (ui lane, ship 3)

**Plan** (a project's page): columns by Linear state type. Each row is the issue key and title, assignee, priority, the linked run's state chip (running, needs you, parked, done), the PR with its CI and review state (#2076's review status), and the design-doc phase whose `issues:` names it (#2076's library). Issues with no link but a suggested one show the suggestion. Filters: assignee, cycle, has run, has PR, needs review. **Cycle**: the same for a team's current cycle, with the cycle's dates and done/total. Both read stored data only: issue fields (§4.1), items and instances (`views.db`), PR records, review state.

## 8. Errors (new)

| Code | When | Text |
|---|---|---|
| `SND-LINEAR-001` | `writeToken` (scheme not `keychain:`/`op:`), `writeback` or `stateRules` key wrong | names the key path and, for `writeToken`, "the write key must be in the keychain or 1Password: an env var or file is readable by agent sessions" |
| `SND-LINEAR-002` | a write with no write key | "Connect write access: Settings → Connections → Linear (or `sindri index connect linear --write`)." |
| `SND-LINEAR-003` | a write to a team the profile doesn't list, or `teams` is empty | "ENG isn't in context.sources.linear.teams." |
| `SND-LINEAR-004` | Linear refused the write key (revoked, read-only) | "Linear refused the write key. Make a new one with write permission." |
| `SND-LINEAR-005` | unknown issue key, or the issue was deleted or moved out of the profile's teams | "No issue ENG-99 in your teams (deleted, moved, or not synced yet: `sindri linear show` after the next tick)." |
| `SND-LINEAR-006` | a field value that isn't in the team's stored lookups, or matches more than one | "No state 'Reviw' in ENG. States: Backlog, Todo, In Progress, In Review, Done." / "'Sam' matches 2 members: Sam Lee, Sam Ito." |
| `SND-LINEAR-409` | D6: a field changed in Linear since it was opened | §5.4's text |
| `SND-LINEAR-010` | Linear unreachable before sending, or it refused with a 4xx other than the key | "Linear didn't take it, so nothing was changed. Try again." |
| `SND-LINEAR-011` | sent, no answer (timeout, connection reset, 5xx) | "Linear didn't confirm it. Checking ENG-12…" then §5.5's text |
| `SND-LINEAR-012` | rate limited (429) | "Linear's rate limit: try again after 10:47." Nothing was sent |
| `SND-LINEAR-013` | Linear applied it, but storing the answer failed | "Saved in Linear; Sindri shows it after the next sync." (the next tick stores it) |
| `SND-AUTH-002` (existing) | an agent session's write | §5.3's text |

An edit sends all its fields in one `issueUpdate`, so it applies all or nothing; there's no partial edit to report.

## 9. Security

- **Agent sessions run as the owner's uid.** So the write key's pointer scheme is the boundary (D3): keychain or 1Password only. D2 keeps it out of every read path in the process, and an architecture test checks it. This guards against honest mistakes, not a deliberate bad actor with the owner's shell.
- **Record text is untrusted.** An issue description can't trigger a write. Every write has an owner caller, or is a writeback on an issue an owner command linked. Issue text reaching a run is fenced, and teammates' comments are left out by default (D10).
- **Writes are limited to the profile's teams** (`SND-LINEAR-003`), which must be listed, checked before any call.
- **No mass writes.** Owner commands touch one issue each. The writeback touches only linked issues and writes only on change; state rules touch only the run's own issue.
- **Every write is in the ledger** (D11) with its actor, and no key or typed text.
- **Attachment urls** are forge PR urls or `sindri://` only.

## 10. Docs to update

`docs/sindri/linear.md` (write key, writeback, state rules, the read-only line), `docs/csharp/sindri-profile.md` (`writeToken`, `writeback`, `stateRules`), `docs/csharp/sindri.md` (CLI, errors, events), the events catalog (`EventCatalog` rows for `linear.write.*`), `docs/csharp/sindri-ui.md` (Work's Linear view, the issue sheet, New issue, the Settings row, Plan and Cycle), `docs/csharp/sindri-scoping.md` (`StoreTrackerSource`, the key form), `planning/ERD.md` (`linear_issue`, the lookup tables), `deps.yaml` (no new Features edge; the Host adapters), the owned index spec's D8 and the Projects spec's non-goal (both say read-only: a pointer to this spec), AGENTS.md Commands, `src/Sindri/aot-smoke/expected.txt` (the doctor row).

## 11. Rate and freshness

The `linear` feed ticks every 5 minutes today. A change in Linear shows in Sindri within one tick, and Sindri's own writes show when Save returns (D4). Linear's limit **(Ph0 checks: documented as requests per hour plus a complexity budget, per user)** is probably shared by both keys of one user, so the doctor row counts both and warns past half the budget. An owner save costs about three calls (the D6 read, the mutation, the re-read). The owner's commands don't retry by themselves (a 429 is `SND-LINEAR-012`); the writeback parks (§6.2). Ph0 measures one tick's cost, the `;f3` re-read's cost, and a save's, on the fake and on the owner's scratch team. Open question 2 asks whether 5 minutes is fast enough.

## 12. Phases and parallel plan (for /specToProvenPR)

Ship 1 = Ph0 to Ph3 (work issues in Sindri). Ship 2 = Ph4, Ph5 (scope from an issue, write back, links). Ship 2b = Ph4b (build and fix from an issue). Ship 3 = Ph6 (planning).

| Phase | Lane | What | Depends on | Proof |
|---|---|---|---|---|
| **Ph0** | context | **Go/no-go.** Linear facts: mutation names and inputs, client `id` on `IssueCreateInput` and `CommentCreateInput`, `attachmentLinkURL` idempotency, conditional update (none?), what bumps `updatedAt`, per-field change times, key scope and how to read it, rate limit numbers and whether two keys share them, input caps. The stateful fake (D15) with its switches | — | a probe script over the fake, and over the real API on the owner's **scratch** team only (owner-run, never the real one); the facts written into §5. Fallbacks if a fact fails: no client id → create-then-reconcile by a marker in the description, shown as "may have been created: check ENG"; no key scope → the Settings row reports "can't tell, try a write on a scratch team" |
| **Ph1** | context | `linear_issue` + lookup tables (migration), side rows on `RecordWrite`, the Commit older-`updatedAt` rule, `;f3` re-read, `linear-meta` feed, `IContextIssues` with `Since`, the shared `LinearCalls` | Ph0 | unit: field mapping per issue shape; integration: a tick over the fake stores fields and lookups, a `;f2` store re-reads once, **a tick in flight during a write-through doesn't revert it** |
| **Ph2** | api + context | `writeToken` (keychain/op only) + `connect --write` + the Settings row's route, `ILinearWrites` + `LinearWriteKey`, Sindri ports and Host adapters, `LinearWrite.Begin`, the three issue commands, D6 check, write-through, D11 events, errors, doctor row, `linear history` | Ph0, Ph1 | unit per command and refusal (`SND-AUTH-002` for a session through CLI and API, 001 for an `env:` write key, 003, 005, 006, 409); integration on the fake: create → stored at once, retry same client id → one issue, no-answer → 011 then "already applied"; architecture test on the two keys; aot-smoke `expected.txt` |
| **Ph3** | ui | Mockups first (`/design-mockup`: Work's Linear view with the strip, the issue sheet with the conflict banner, New issue, the Settings row; every state in §3; 1,000 and 1,440 px), owner's pick, then the views | Ph1 read goldens (hand-written first), Ph2 | UI tests: filter, edit, conflict banner, disabled-without-key then live enable, offline, the accessibility lines; screenshot run on the fake |
| **Ph4** | api | `StoreTrackerSource` (Host), the start command, `item.linked` on start, `item link|unlink`, suggestions, the second-start question, the pull-side request | Ph1, Ph2 | integration: scoping from a stored issue without `AW_SINDRI_TRACKER_DIR`, teammates' comments left out, the text fenced; recipe eval case `tracker-from-store`; a session's start refused |
| **Ph5** | api | `linear-writeback` service and view (living comment, the needs-you comment, PR attachments), state rules | Ph2, Ph4 | integration on the fake: a run's moves edit one comment (count = 1), a PR adds one attachment, **a restart and a `views rebuild` mid-run write nothing twice**, a crash after create leaves one comment, 429 parks, a closed issue makes one request, no state move without a rule |
| **Ph4b** | api | tracker intake for the build and bug-fix entry recipes (§6.1), Build it / Fix it buttons | Ph4 | recipe evals per recipe, version bumps |
| **Ph6** | ui | Plan and Cycle views | Ph1, Ph4, #2076's review state and `IContextLibrary` | UI tests over a fixture with runs, PRs, reviews and suggested links |
| **Ph7** | — | Owner proof (§13), the two-week measure | each ship | §13 |

**Parallel plan.** Day one: context on Ph0, then straight into Ph1. ui on Ph3's mockups (they need only this spec). api reads Ph0's facts and starts Ph2's slices red against the fake. After Ph1: api runs Ph2, then Ph4 and Ph5 (Ph4's link and tracker slices can start red alongside Ph2: different folders), ui builds Ph3 against hand-written goldens. Ph4b and Ph6 run in parallel after Ph4. Context is free after Ph1 for #2076's Ph1/Ph2 (the library), which this spec's Ph6 reads.

**Shared files, one owner each.** Several phases touch the same files; to avoid merge fights, each has one owning phase and later phases rebase on it: API goldens and `docs/csharp/sindri.md`: Ph2 creates the Linear sections, Ph4 and Ph5 append. Help text (`Usage.cs`) and `PublicAPI.Unshipped.txt`: same order. `aot-smoke/expected.txt`: Ph2 only. `docs/csharp/sindri-ui.md`: Ph3, then Ph6.

**Gates per phase.** `-warnaserror` build; the touched projects' unit and integration lanes; `Aw.Architecture.Tests` (new folders, the key test); scoped format; `check-profile-doc.mjs` (Ph2, Ph5); `check-env-example.sh`; `check-deps.mjs` (Ph2); ApiGoldens classes (Ph1, Ph2, Ph4, Ph5); `Aw.Sindri.Ui.Tests` (Ph3, Ph6); aot-smoke with `expected.txt` (Ph2's doctor row); recipe evals (Ph4, Ph4b).

## 13. Proof

| Row | Check |
|---|---|
| **TP-1** | On the fake: a tick stores fields and lookups for every fixture issue; Work's Linear view lists them filtered by assignee, cycle and label; a change in the fake shows after one tick; the strip lists a new assignment. Verifies REQ-1, REQ-9 |
| **TP-2** | Create, edit two fields, comment: each is in the fake and on the page when the command returns; a retried create and a retried comment (same client id) make one each; the ledger holds a requested and an applied event per write; a tick in flight doesn't revert the edit. Verifies REQ-2, D4, D11, D12 |
| **TP-3** | The §5.4 conflict on the fake: a teammate's comment doesn't block a priority edit; a teammate's priority change is `SND-LINEAR-409`, nothing sent, the banner shows both values. Verifies D6 |
| **TP-4** | An agent session's create, edit, comment, link and start: `SND-AUTH-002` each, nothing sent, a `linear.write.refused` event each; its reads work. An `env:` write key is `SND-LINEAR-001`. Verifies REQ-7, D3 |
| **TP-5** | Scope it from a stored issue: the brief is the issue (fenced, no teammates' comments), the item is linked, the issue has one Sindri comment. Verifies REQ-3 |
| **TP-6** | The run moves four times and opens a PR; Sindri restarts and `views rebuild` runs in between: still one living comment (edited), one needs-you comment, one attachment. Verifies REQ-4, D8 |
| **TP-7** | The issue is completed in the fake mid-run: one request in Needs you, the run still running until answered. Verifies REQ-5 |
| **TP-8** | Link and unlink by hand; a PR naming ENG-12 shows a suggestion that isn't applied until clicked. Verifies REQ-8 |
| **TP-9** | Plan and Cycle on the fixture show run, PR, review and doc phase per row. Verifies REQ-6 |
| **TP-10** | On the owner's laptop, a scratch Linear team: connect write access from Settings, create, edit, comment, start a scoping run, see the comment in Linear. Verifies REQ-1 to REQ-4 for real |

## 14. Open questions for the owner

1. **One key or two?** I'd go with two: the index keeps its read-only key, and writes get their own key, kept in the keychain, that only the write path loads. Then nothing that reads Linear text (the sync, the decision pass, agent sessions' reads) holds a key that can change Linear. The cost is one more key to make, pasted once in Settings. The alternative is one read+write key for everything, which is simpler but gives every read path write power.
2. **Is a 5-minute sync fast enough?** I'd say yes for v1, since your own writes show at once (D4) and only teammates' changes wait up to 5 minutes. If it bugs you, the next step is a 1-minute tick while the app is open (it uses more of Linear's rate budget, measured in Ph0).
3. **Linear issues as a view on Work, or their own tab?** I'd go with a Sindri / Linear switch on Work plus Plan/Cycle on Projects, so no tenth tab at the 1,000 px minimum. The alternative is an Issues tab, with the window's minimum width measured again.
4. **Should only you start runs from an issue?** I'd say yes, for all three run kinds: an agent-started run would make Sindri comment on a Linear issue you never touched. An agent that wants one started can ask you through Needs you. The alternative lets a session start scoping runs (reads only, but the comment still lands in Linear).
5. **State rules (§6.3): any defaults?** I'd say none. Sindri never moves an issue's state until you set a rule per team, because your Linear-GitHub integration may already move it on merge.
