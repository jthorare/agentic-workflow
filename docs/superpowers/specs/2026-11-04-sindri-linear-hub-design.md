---
aw-doc: 1
id: sindri-linear-hub
kind: spec
title: "Sindri as the owner's Linear hub: work issues, start runs from them, keep both sides in step, plan across them"
status: draft
repos: [jthorare/agentic-workflow]
---

# Sindri as the owner's Linear hub

**Status:** draft for the owner, revision 3 (/autoplan rounds 1 and 2 folded in) · **Date:** 2026-11-04 · **Lane:** context (this spec), with api (commands, workflow starts) and ui (pages) · **Builds on:** the owned index spec `2026-10-27-sindri-owned-index-design.md` (the record store, feeds, D8 read-only tokens, §6.1 "no live calls at query time"), the Projects page spec `2026-10-29-sindri-projects-page-design.md` (the `linear-projects` feed, the owner's writes, P1 "the page reads only stored data"), the PR review page spec #2076 (`2026-11-04-sindri-pr-review-page-design.md` rev 3: the Reviews tab, the docs layer, Linear keys in `issues:`, the header's issue chip), `docs/sindri/linear.md`, `docs/csharp/sindri-scoping.md` (`step.scope.read-tracker`) · **Next:** /autoplan round 3, meta's UX check, the owner's read; then /specToProvenPR takes §12's phases.

Every file path below is one the author opened on main (1476cfe). Every command, route, event, key and type is marked **existing** or **new**. Facts about Linear's API that this repo doesn't hold yet are marked **(Ph0 checks)**, and Ph0 is a go/no-go with a written fallback for each (§12).

## The owner's steps: install -> works

1. **Connect write access once.** In Linear (as of 2026-11): Settings → Account → Security and access → Personal API keys → a key with **write** permission. In Sindri: Settings → **Connections** → Linear → **Connect write access**, paste it, **Check and save**. Sindri checks it can see your teams and write, stores it in the keychain, and approves the profile change in the same click when that pointer is the only change waiting (§5.1). The read-only key the index already uses stays as it is (D2). The terminal path `sindri index connect linear --write` does the same and reads the key from a hidden prompt.
2. **Open Work** (`G Q`) and pick **Linear** on its switch. Your issues: assigned to you first (open, by priority then last updated), then your teams' open ones. Filter by team, project, cycle, state, assignee, priority or label. A strip on top says what changed since you last looked (new assignments, mentions, teammates' state moves); the switch and the tray show its count.
3. **Change an issue.** Open it, change the state, assignee, priority, cycle, labels, title or description, or write a comment. It's written to Linear when you press Save, and the sheet shows Linear's answer.
4. **New issue.** `C` on Work's Linear view (or **New issue** on a project's page, or ⌘K "New Linear issue"). Team is required; the rest is optional.
5. **Start a run from an issue.** On the issue's sheet: **Scope it**. Sindri makes a Sindri item linked to the issue and starts the run. From then on the issue has one Sindri comment, marked automatic, that tracks the run's state, and each PR the run opens is attached to the issue. **Build it** and **Fix it** come in ship 2b (§6.1).
6. **Link by hand.** On an item's sheet, **Link issue** (or `sindri item link <item> ENG-12`). Sindri suggests a link when an item's title, branch or PR names a stored issue key; a suggestion is never applied without your click.
7. **Plan.** Projects → a project → **Plan**: its issues by state, each with its run, PR and review state. The Projects header's **Cycle** switch does the same for a team's current cycle.

## 0. Why

The owner runs their work from Sindri: PR reviews (#2076), workflows, agents, docs. Linear is where the team's issues live, and today Sindri only reads it (`docs/sindri/linear.md`: "Nothing is written to Linear"). So the owner switches to Linear for every status change, comment and new issue. They also copy issue text into Sindri by hand to start a run, and copy PR links back. The owner (2026-11-04): "sindri should also be able to tie into linear with an api key to be my project management hub as well."

What exists today (main 1476cfe):

| Piece | State | Where |
|---|---|---|
| Issue records | **Existing, read-only.** The `linear` feed stores the profile's teams' issues as `linear:issue/<uuid>` with a known-as link `linear:issue-key/ENG-12`. Its state is in `State`/`StateRaw`. Assignee, labels, cycle and priority are **not stored**. Comments are kept only as text lines inside the record (`- {name} ({day}): {body}`, newest 20), with no comment id and no author id | `LinearIssueItem.cs`, `LinearText.cs:40`, `docs/sindri/linear.md` §4 |
| Project records | **Existing, read-only.** The `linear-projects` feed, plus the viewer (who owns the key), fetched by a tick step | Projects page spec §4.1, §4.2; `TickSteps.cs`, `LinearViewerRun.cs` |
| The key | **Existing, read-only.** `context.sources.linear.token`, a secret pointer (`env:`, `file:`, `keychain:`, `op:`), saved by `sindri index connect linear` (human-only, `SND-AUTH-002`; keychain, else an owner-only file) | `LinearSettings.cs:17`, `LinearClient.cs` `Key()`, `Usage.cs:67` |
| Store writes | **Existing.** `IRecordStore.Commit(RecordBatch)` writes a page of records and the source's position in one transaction; `RecordBatch.Position` is required. A tick holds the source's lock (`SyncLocks`, `internal` to `Aw.Context.Features`, non-blocking `TryEnter`) for its whole pull, up to `IndexSync.FeedCap` = 4 minutes. A tombstone keeps the row; only `Purge` deletes it | `RecordBatch.cs:14`, `SyncLocks.cs`, `IndexSync.cs:28` |
| Item ↔ issue link | **Existing events, never appended.** `ItemCreated.Link` (a tracker record the item was created from) and `item.linked` (`ItemLinked`: command id, item, record key, alias `ENG-142`) fold into `item-aliases` (`AliasRow(Alias, Item, Record, Sequence)`, keyed by alias). An item holds one link. There is no unlink event, and neither event says who linked | `Aw.Sindri.Core/Events/ItemCreated.cs`, `ItemLinked.cs`, `Items/ItemAliasesProjection.cs` |
| Scoping from an issue | **Existing step, fixture adapter only.** `step.scope.read-tracker` reads through `ITrackerSource.Read(key)` ("an issue key or a project name"), whose only adapter is `FileTrackerSource` (`AW_SINDRI_TRACKER_DIR`), registered once. With that unset, the step fails with `SND-SCOPING-005`. Only `workflow.scoping.yaml` reads a tracker | `docs/csharp/sindri-scoping.md:74`, `SindriInfrastructureServiceCollectionExtensions.cs:142` |
| Owner-only writes | **Existing pattern.** `ProjectWrite.Begin` refuses an agent session with `SND-AUTH-002` even though the desk class lets sessions through | `Aw.Sindri.Core/Projects/ProjectWrite.cs:17-21` |
| Sindri reads Context | **Existing pattern.** A Sindri-side port in Core (`IProjectFacts`), implemented by `ContextProjectFacts` in `Aw.Sindri.Features/ProjectFacts/` over the context facade (`IContextProjects`). Features already references `Aw.Context.Core`, so the pattern adds no edge | `Aw.Sindri.Core/Projects/Facts/`, `deps.yaml` |
| Fake Linear | **Existing, in-process.** `HttpMessageHandler` fakes in `tests/Aw.Context.Tests/Linear/` (`FakeLinearIssues`, `FakeLinearProjects`), read-only | `LinearFeedFakeServerTests.cs` |

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| D1 | **Scope is all four parts: work issues in Sindri, issue → workflow, two-way links, planning views.** Owner, decided 2026-11-04 | Owner |
| D2 | **Two keys.** The index keeps its read-only key (`context.sources.linear.token`). Writes use a second pointer, `context.sources.linear.writeToken` (new), that only the write adapter resolves. A `writeToken` equal to `token`, or one that resolves to the same secret, is refused | The sync, the decision pass and every agent-reachable read path never hold a key that can change Linear. Open question 1 |
| D3 | **The write key lives in the keychain or 1Password.** `writeToken` accepts only `keychain:` and `op:`. `env:` and `file:` are refused with `SND-LINEAR-001`, except with a loopback url (the fake, in tests). A box with no usable keychain gets `SND-LINEAR-014` and the `op:` alternative, never a file | Agent sessions run as the owner's uid and inherit the shell's environment and files. This keeps the write key out of env, files and anything a session reads by accident. It is not a wall: on Linux the keyring is unlocked for the whole login session, so a session that deliberately runs `secret-tool lookup` can read it. That's accepted under the threat model (honest mistakes, not a bad actor) |
| D4 | **Pages read only stored data** (Projects P1, owned index §6.1). A write is **written through**: after the mutation, the write adapter re-reads the issue with the feed's own query (`Issue.graphql`), maps it with the feed's own mapper (`LinearIssueItem.Of`), and commits it as a batch of one with **no position** (`RecordBatch.Position` becomes nullable: null leaves the cursor as is). It takes no lock. `IRecordStore.Commit` gains one rule for `linear` records: a write whose `UpdatedAt` is strictly older than the stored one is skipped; equal passes | A tick holds the source lock for up to 4 minutes and the lock can't be waited on, so a lock would make most Saves end in "shows after the next sync". The older-`updatedAt` rule alone stops a tick that fetched before the save from putting the old value back, and no position means the write can't move the cursor. Equal passes because the `;f3` re-save has the same `updatedAt` and new text |
| D5 | **Every Linear write is the owner's action.** The commands, including starting a run and linking, are desk class with an owner check (`LinearWrite.Begin`, the `ProjectWrite.Begin` pattern): an agent session is refused with `SND-AUTH-002`. `item.linked` and `item.unlinked` carry who did it (`By`: the caller class), and the writeback service (D8) writes only to issues whose current link has `By = owner` | Owner rule. Same as `effect.pr.merge` and the Projects page. The `By` field makes "only owner links" a fact on the event, not a promise about who appends it |
| D6 | **Field ownership is the conflict rule.** Linear owns every issue field (title, description, state, assignee, priority, cycle, labels, estimate, project, team). Sindri owns the run (its state, its comment, its PR attachments). A human edit in Sindri racing one in Linear is caught **per field**: right before it writes, Sindri reads the issue live and compares each field it's about to send with the value the owner opened. A changed one is `SND-LINEAR-409`, and nothing is sent. The same live read checks the issue's team is still in `teams` (else `SND-LINEAR-005`). Nothing is merged silently | Comparing the whole issue's `updatedAt` would refuse the owner's priority edit because a teammate commented, or because Sindri's own comment bumped it. An issue moved to an unlisted team stays "listed" in the store for up to a tick |
| D7 | **No offline queue, and honest outcomes.** A write that can't reach Linear fails with its code and the edit stays in the form. Two outcomes are kept apart: **refused** (nothing was sent: `SND-LINEAR-010`) and **unknown** (sent, no answer: `SND-LINEAR-011`, CLI exit 3). After an unknown, Sindri re-reads the issue and shows "already applied" when Linear has the change, else offers Retry with the same client id | A queued write that lands an hour later is the silent overwrite D6 forbids. "Nothing was changed" after a timeout can be false, and a retry then double-posts a comment |
| D8 | **Runs write back through one living comment and attachments**, from a service with its own durable state, **keyed by issue**. Per linked issue: ONE comment Sindri edits as the run moves (started, needs you, PR opened, merged, done, parked), first line "Sindri run (automatic)", plus one attachment per PR. The service is level-triggered: per issue it stores the item it follows, the comment id, the hash of the last facts written and the attachments written, and on each pass it writes only when the wanted facts differ. It never moves the issue's state (state rules are Later, open question 5) and never posts a second comment | One comment that changes beats a comment per step. Level-triggered means a restart or a ledger replay writes nothing twice. A second "needs you" comment wouldn't reach the owner (Linear doesn't notify you of your own comments) and would ping teammates; the owner's signal is Needs you in Sindri |
| D9 | **No new top-bar tab.** Linear issues are a **Linear** view on **Work** (a Sindri / Linear switch), and planning views live on **Projects** (a project's Plan, a team's Cycle). The bar keeps #2076's nine tabs (#2076 D14) | Every tab costs width at the 1,000 px minimum (`sindri-ui.md:155`). Work is already "what's on my plate". Open question 3 |
| D10 | **Issue → run starts from the stored record.** A new `StoreTrackerSource` reads the issue, its description and its comments from the record store. The key it takes is the issue key `ENG-12`, resolved through the known-as link. The issue's text reaches the run fenced as untrusted. Comments come from the new `linear_comment` table (§4.1), and only the owner's (author id = the viewer's id) are included, unless the owner ticks "include teammates' comments" | The run's brief is the record as indexed, scrubbed and versioned. A teammate's "also update the deploy script" in a comment isn't an instruction to a code-writing agent. The author id is the only thing a comment can't fake: a body can contain a line that looks like the owner's |
| D11 | **Every write is two ledger events**: `linear.write.requested` (new: command id, actor `owner` or `service:linear-writeback`, target, mutation kind, client id, attempt, field names, a hash of the values) before the call, and `linear.write.applied` / `linear.write.failed` (new: Linear's answer version, or the code only, never Linear's message) after it. Refusals are `linear.write.refused` (new: actor, target, verb, code; for `SND-AUTH-002`, `-003`, `-005`, `-409`). No typed text goes into the ledger. Idempotency key: `linear.write.requested:<clientId>:<attempt>`; none of these events is Public. A `refused`, `applied` or `failed` all count as the outcome | "Who changed ENG-12 from Sindri, and when" for owner and service writes alike. An intent with no outcome after a crash is shown to the owner as "unknown outcome: check ENG-12" (a Needs you row and the doctor row) and never retried by itself. The writeback re-sending the same facts after a crash is safe; the owner's commands never retry on their own |
| D12 | **Creates are idempotent from the draft.** The new-issue and comment forms (and the CLI) mint a client id (`id` on `IssueCreateInput` and `CommentCreateInput`, **Ph0 checks**) when they open, and every retry of that draft sends the same id. Linear refusing a repeated id is read back and treated as success. The writeback's comment id is derived from (issue, generation): a comment deleted in Linear bumps the generation | A command id is new on every retry (`ProjectWrite.Append`), so it can't be the idempotency key. Linear won't take a reused id after a delete |
| D13 | **Sindri reaches Linear through Sindri-side ports.** `ILinearIssues` (reads) and `ILinearWriter` (writes), both new in `Aw.Sindri.Core/Linear/`, implemented in `Aw.Sindri.Features/Linear/` over the context facade (`IContextIssues`) and the context write port (`ILinearWrites`), like `ContextProjectFacts`. `deps.yaml` gains no edge | The `IProjectFacts` pattern. Features already references `Aw.Context.Core` |
| D14 | **Links come from three places**: starting a run (`ItemCreated.Link`), the owner's **Link issue** / `sindri item link|unlink` (new), and suggestions (an item's title, branch or PR names a stored issue key, or the PR already has a Linear attachment). Suggestions are shown, never applied. **One open item per issue**: linking or starting on an issue whose link is held by an open item is refused with `SND-LINEAR-007`, which names the item and offers to open it or unlink it first | A link only from run starts leaves Plan full of runs with no issue and issues with no run. Two open items on one issue would fight over one comment |
| D15 | **Fixtures are made up.** One stateful fake Linear in `tests/Aw.Context.Testing/Linear/` (new, entry type `FakeLinear`): mutations change what it serves, and switches (`AnswerWithNewerField`, `RateLimit`, `RevokeKey`, `RepeatClientId`, `NoAnswer`, `MoveTeam`) make it misbehave. Context, Sindri and the UI tests share it; it ships a README with one example per switch. No work-app names | Owner rule (app-agnostic toolkit). Sindri's and the UI's tests can't see `Aw.Context.Tests` internals |

## 2. Goals and non-goals

**Goals.**
- REQ-1 The owner lists, filters and opens their Linear issues in Sindri, from stored data, within one tick of a change in Linear.
- REQ-2 The owner creates issues, edits their fields and comments from Sindri, and each change is in Linear and on the page when Save returns, or the page says plainly that the outcome is unknown.
- REQ-3 The owner starts a scoping run from an issue in one click (ship 2), and a build or bug-fix run (ship 2b, once those recipes take a tracker key). The run's Sindri item is linked to the issue.
- REQ-4 A linked issue shows the run's state in one Sindri comment and every PR the run opens as an attachment, within one tick of the run moving.
- REQ-5 An issue cancelled or completed in Linear while its run is open asks the owner. The run isn't stopped silently.
- REQ-6 A project's Plan and a team's Cycle view show each issue with its run, PR and review state.
- REQ-7 An agent session can read issues and can't write to Linear through any Sindri path: command, route, MCP tool or service.
- REQ-8 The owner links and unlinks an item and an issue by hand, and sees suggested links.
- REQ-9 Work's Linear view says what changed since the owner last looked: new assignments to them, mentions, teammates' state moves on their issues.

**Non-goals.** Replacing Linear's own boards, roadmaps, triage inbox, notifications or SLA rules. Webhooks (Sindri runs on a laptop with no inbound URL). Bulk edits. Writing to any team the profile doesn't list. Archive, delete, relations (blocked-by) and file uploads in the issue editor: every sheet has **Open in Linear** for those.

**Later (not in this spec's ships).** State rules (Sindri moving an issue's state on run start or merge; open question 5). Project and cycle edits. Agent proposals (a session drafting an issue for the owner's one click). "Send as fix task" on a review (#2076) creating a Linear issue. A "assigned to you" Needs you row type is in, off by default (§3).

**Success measure.** Sindri can't see edits made in Linear's own app, so the baseline is counted by hand once: the owner counts one week of their issue edits and comments from Linear's activity view, before ship 1b. After that, `sindri linear history --since 7d` (new) reports what Sindri itself knows, each week for two weeks: writes through Sindri (edits, comments, creates), runs started from an issue (any kind), and opens of the Linear view. Targets: writes through Sindri at least half the baseline count; 3 runs a week from an issue after ship 2.

## 3. Where it sits in the app

| Module | Reads | Writes | Reached from |
|---|---|---|---|
| **Work → Linear** (new view on the existing tab) | Linear issues (stored), the since-you-looked strip | issue writes | top bar `G Q`, then the Sindri / Linear switch (it remembers the last pick) |
| **Issue sheet** (new sheet kind `Issue`, `sindri://linear/ENG-12`) | the issue record, its comments, its linked item and run, its PRs and their review state (#2076) | issue fields, comments; starts a run; link/unlink | a Work row, a Plan row, a strip row, #2076's review header chip, ⌘K "Go to issue…" |
| **New issue** (new dialog, like Create item) | teams, states, members, labels, cycles (stored) | `linear.write.*` | `C` on Work's Linear view (`C` on the Sindri view stays Create item), a project's page, ⌘K "New Linear issue" |
| **Projects → Plan** (new view on the project page) | the project's issues + runs + PRs + reviews | none beyond the sheet's | a project card |
| **Projects → Cycle** (new) | a team's current cycle's issues, same columns | none | the Projects header's Projects / Cycle switch |
| **Settings → Connections → Linear** (new group and row; #2076 adds Settings → Library beside it) | whether the read and write keys resolve | the keychain entry, the pointer, the approval | Settings, and every disabled write button's "Connect write access" link |

**The Work page's Linear view.** A filter bar (team, project, cycle, state, assignee, priority, label), rows grouped "Assigned to you" (open, by priority then last updated) then "Your teams", 50 rows per page with "Show more". The switch reads "Linear · 3 new" when the strip has items. ⌘K "Go to issue…" finds an issue by key or by text (the index already has the text). The Sindri view is unchanged.

**The since-you-looked strip** (REQ-9). Rows for: assigned to you, mentioned you, a teammate moved the state of an issue assigned to you. Each row opens the sheet. Hidden when there's nothing new. The marker advances when the owner presses **Dismiss**, or leaves the view after the strip was on screen 3 s; it's stored with the app's other preferences (`Api/PreferencesJson`). The tray tooltip shows the count. A Needs you row "ENG-12 assigned to you" is a setting, off by default.

**The issue sheet.** One form, one **Save** (`⌘S`), pinned at the bottom while the form scrolls (520 px sheet, text scale up to 1.3). A field the owner changed shows a dot; Save is disabled with no change. Esc, Back or another row with unsaved edits asks "Discard changes to ENG-12?". Only changed fields are sent (D6). The description and comments are shown as plain text: http(s) links open in the browser with their host shown, images aren't fetched, and `sindri://` text in a description or comment is not a link (only Sindri's own chips are). The description edit box is plain multi-line text.

**New issue** is a dialog, like Create item (`CreateItemDialog`). Closing it with typed text keeps the draft, with its client id, until Sindri restarts: reopening shows "Resume draft". Discarding makes a new client id next time.

**States.** Every surface uses the app's vocabulary (skeleton rows on first load only, the stale pill after 30 s, the Not running card, "Sending…" after 300 ms):

| Surface | Loading | Empty | Error / slow | Success |
|---|---|---|---|---|
| Work → Linear | skeleton rows; after the `;f3` upgrade "Updating issues… N of M" from `sindri index status` | no Linear source: "Connect Linear" (the read-key setup); none assigned: "No open issues assigned to you"; filters: "No issues match these filters" + Clear filters | feed failing: banner with last synced time and code, rows stay; offline: rows from the store "as of 10:05", writes disabled "Linear unreachable" | rows |
| Issue sheet | from the store at once | issue deleted or moved out of your teams: "no longer served" note, writes disabled | 409: the conflict banner (below); 011: the form locks, "Checking ENG-12…", then "Already saved" or "Linear didn't confirm it. Retry?"; 012: banner "Linear's rate limit: try again after 10:47", form kept; 010/004/006: banner with §8's text, form kept | "Saved" and Linear's answer in the fields |
| Issue sheet, saving | Save shows "Sending…"; fields lock so D6's compared values can't change under it | — | as above | focus back on the title |
| New issue | lookups from the store | team has no states stored yet: "Sync ENG first" | create 011: "Linear didn't confirm the issue. Checking…", then the new row or Retry (same client id) | dialog closes, focus on the new row |
| Unknown outcome after a crash | — | — | one Needs you row "Unknown outcome: check ENG-12" with the issue chip; it clears when the re-read decides | — |
| Settings → Connections → Linear | "Checking…" in a live region (always in the tree, flag before text) | not connected: "Connect write access" | key refused (`-004`), scope can't be read ("can't tell; try a write on a scratch team"), no keychain (`-014`) | "Write access: connected · checked 10:05" with **Replace key** and **Remove key** |
| Plan | skeleton columns | "No Linear issues in this project" / "This project isn't linked to a Linear project" | feed failing banner, as Work | columns |
| Cycle | skeleton | "ENG has no cycles" / "Between cycles: next starts 11-10" | as Plan | columns with dates and done/total |
| Links and suggestions | — | "No suggested issues" | `-007` names the open item holding the link | the chip on both sheets |

Without a write key, write controls are disabled with a "Connect write access" link to the Settings row; they enable live on `profile.approved`, no restart. Sindri stopped: the existing Not running card.

**Conflict display** (D6). A 409 shows a banner on the sheet: "ENG-12 changed in Linear at 10:01". Each changed field is marked with Linear's value beside the owner's, and actions: **Use Linear's** (per field), **Use Linear's for all**, and **Save mine anyway** (re-runs the check against the values just shown). Focus moves to the banner, and it's announced politely.

**Accessibility.** One line per surface; Ph3 and Ph6 name a UI test for each:
- Work switch: a two-tab tabs pattern (Left/Right, Home/End), name includes the count ("Linear, 3 new").
- Work rows: J/K and Enter (inherited); read as "ENG-12, title, state, assignee, priority, run state"; "Show more" is a button that keeps focus on the first new row.
- Strip: a region named "Since you last looked, 3 items"; each row a link; Dismiss a button.
- Issue sheet: `SheetName` "Issue ENG-12", edge tab "Reopen issue ENG-12"; the `Issue` DeepLinkKind claims its marker the way U8/U10 do; Back returns along the trail from a run frame. Focus after Save stays on the title; after a 409 it moves to the banner.
- Pickers (state, assignee, priority, cycle): a typeahead listbox; labels are multi-select and announce "3 labels selected".
- Settings key field: masked, named "Linear write key"; the check result in a live region.
- Plan columns: each column is a heading-led section; Tab moves between sections, arrows within; a row's sentence includes its column name. Below 1,100 px the columns stack.
- State and priority chips carry text, not colour only.

**Plan columns.** By Linear state type; canceled hidden by default, completed collapsed; 50 rows per column with "Show more".

**With #2076** (rev 3, a80fe7e58). A Plan row's PR chip opens #2076's review page. #2076's review header shows the issue chip to `sindri://linear/ENG-12` (plain text until this spec's Ph3 ships the sheet). #2076's `issues:` front-matter key takes Linear keys (`ENG-12`), so a Plan row shows the design-doc phase that names its issue. That's a read of #2076's `IContextLibrary`, nothing new in the store.

## 4. Data: what the store gains (context lane)

### 4.1 Issue fields, comments and changes (new, Ph1)

The `linear` feed already fetches the issue. It gains the fields it drops today, stored in side tables, not in the text (the text stays what search reads). All three are written by `IRecordStore.Commit` in the same transaction as the record: the parsed rows ride on `RecordWrite` (as #2076's library rows do), so a record and its side rows are never out of step.

- `linear_issue` (migration, new): `key` (PK, FK to `source_record` ON DELETE CASCADE), `team_id`, `identifier` (`ENG-12`), `state_id`, `state_type` (`backlog`, `unstarted`, `started`, `completed`, `canceled`, `triage`), `assignee_id`, `priority` (0 to 4), `estimate`, `cycle_id`, `project_id`, `parent_id`, `labels` (JSON array of ids), `due_date`, `sort_order`, `updated_at`. Indexed by `(assignee_id, state_type)`, `(project_id)`, `(cycle_id)`.
- `linear_comment` (new): `id` (Linear's), `issue_key` (FK, cascade), `author_id`, `created_at`, `body`, `sindri` (1 when the body carries Sindri's marker line). The newest 50 per issue. `LinearText` leaves out `sindri = 1` comments, so Sindri's own comment never changes the record's text, its chunks or what the decision pass reads.
- `linear_issue_change` (new): `issue_key`, `field` (`assignee`, `state`, `mention`), `old`, `new`, `at`, `via` (`feed` or `write-through`). Commit appends a row when the new `linear_issue` row differs from the stored one in assignee or state, and one `mention` row per new comment whose body mentions the viewer **(Ph0 checks the mention syntax)**. Rows older than 30 days are deleted by the same Commit.

A tombstoned issue keeps its rows (only `Purge` cascades), so every read filters `removed_at IS NULL`, and the strip and the writeback treat a tombstone as a deleted issue. `sindri index purge linear` clears the side tables by cascade and leaves the `linear-writeback` view alone (links and comment ids survive). No downgrade path.

The feed's version suffix moves from `;f2` to `;f3`, so every stored issue is re-saved once with its fields and comments. That's the same one-time re-read 0020 did (`docs/sindri/linear.md` §5), and `sindri index status` shows it the same way.

### 4.2 Lookups (new tick step, Ph1)

The pickers need the profile's teams' workflow states, cycles (current, next, last 3), members and labels. They're fetched by a tick step of the `linear` source, like the viewer (`LinearViewerRun` in `TickSteps`): after the issues, once per tick, into one small table each (`linear_state`, `linear_cycle`, `linear_member`, `linear_label`; keyed by Linear id, with `team_id`), replacing the team's rows in one transaction. No new source id, no second cursor. The lookups are owner-only reads.

### 4.3 Read path (new, Ph1)

`IContextIssues` on the context facade (the `IContextProjects` pattern): `List(filter, after)`, `Get(key)`, `Comments(key)`, `Lookups(team)`, `Since(marker, viewer)`. It reads the store only, and applies the record's audience to the side tables too: an agent session sees only issues of teams mapped to its repo (the audience the `linear` feed already sets).

`Since(marker, viewer)` reads `linear_issue_change` after the marker: `assignee` rows whose `new` is the viewer, `mention` rows for the viewer, and `state` rows with `via = feed` on issues assigned to the viewer that don't match a `linear.write.applied` of the owner's within the same minute. No new Linear call.

Sindri exposes `GET /sindri/linear/issues`, `GET /sindri/linear/issues/{key}`, `GET /sindri/linear/lookups/{team}` (owner only), `GET /sindri/linear/since` (owner only), the CLI verbs in §5.3, and the MCP tool `sindri_linear` (read only: `issues`, `show`). Issue text in a result is fenced as untrusted, like every record. The owner-only reads use `LinearWrite.Begin`'s caller test (`SND-AUTH-002`).

## 5. Writes (api lane, with context's adapter)

### 5.1 The key

`context.sources.linear.writeToken` (new): a secret pointer, `keychain:` or `op:` only (D3), same url and loopback rule as `token` (`LinearSettings.cs`): the write adapter takes its url from the same validated settings.

**Settings → Connections → Linear → Connect write access** (new) and `sindri index connect linear --write` (new flag, human-only) do the same thing:
1. Take the key: the app's masked field, or a hidden prompt / stdin on the CLI (no `--key` flag, so it's never in argv or shell history).
2. Check it can read the profile's teams and that it can write **(Ph0 checks how: the key's scope if Linear exposes it, read-only; never a probe mutation on a real team)**.
3. Store it in the keychain as `sindri-index/linear-write` (on Linux `secret-tool store`, key on stdin). No keychain: `SND-LINEAR-014`.
4. Write the pointer into the profile. If the only difference between the approved profile and the pending one is that pointer, approve in the same click. Otherwise the key is saved and the profile stays waiting: "Your profile has other changes waiting: review them in Settings."

The route refuses agent sessions (`SND-AUTH-002`). The key's field is in an `Aw.Redaction` rule, and a test greps the ledger, the request log and the telemetry spans after a connect.

Writes need a non-empty `context.sources.linear.teams`: an empty list reads every team the key can see, which is too wide to write to. Doctor's `context:linear` row adds `write key: resolves | not set | refused | same as read key` and `unresolved writes: N` (fails when one is older than a day).

### 5.2 The ports

- **Context side.** `ILinearWrites` (new, `Aw.Context.Core`), adapter in `Aw.Context.Infrastructure/Linear/` beside `LinearClient`. The error mapping (`Read`, `Classify`, `Unavailable`) moves out of `LinearClient` into a shared `LinearCalls` type, and key resolution into two separate types, `LinearReadKey` and `LinearWriteKey`. An architecture test checks that only the write adapter references `LinearWriteKey` and the read client never does. A mutation that returns `success: false`, or `errors` next to `data`, is a failure. Mutations **(names and inputs Ph0 checks against Linear's schema)**: `issueCreate`, `issueUpdate`, `commentCreate`, `commentUpdate`, `attachmentLinkURL`. Each is an embedded `.graphql` file under `Operations/` with a golden. The write-through (D4) is a method on the same adapter: re-read, map, `Commit` with no position.
- **Sindri side** (D13). `ILinearIssues` and `ILinearWriter` in `Aw.Sindri.Core/Linear/`, adapters in `Aw.Sindri.Features/Linear/`.

### 5.3 Commands (new)

Routes follow the existing `/sindri/...` shape (`ProjectRows.cs`). Exit codes follow `docs/csharp/sindri.md:46`: **0** ok, **1** Sindri isn't running, **2** refused, invalid or any other error, and **3** unknown outcome (sent, no answer; re-read didn't decide). Reads never start the control plane; writes start it like `project add`, and take `--no-start`.

| CLI | API | Who | MCP | What |
|---|---|---|---|---|
| `sindri linear issues [--team T] [--assignee me\|<name>] [--state S] [--project P] [--cycle current\|next] [--label L]... [--json]` | `GET /sindri/linear/issues` | owner, session (its audience) | `sindri_linear issues` | list |
| `sindri linear show <ENG-12> [--json]` | `GET /sindri/linear/issues/{key}` | owner, session | `sindri_linear show` | one issue (not found: 2, `SND-LINEAR-005`) |
| `sindri linear lookups <team> [--json]` | `GET /sindri/linear/lookups/{team}` | owner | none | the team's states, members, labels, cycles |
| `sindri linear issue new --team ENG --title T [--state S] [--assignee me\|none\|<name>] [--priority urgent\|high\|medium\|low\|none] [--project P] [--cycle current\|next] [--label L]... [--parent ENG-1] [--description T \| --description-file F] [--client-id U] [--no-start] [--json]` | `POST /sindri/linear/issues` | owner | none | create |
| `sindri linear issue edit <ENG-12> [field flags] [--clear-labels] [--no-start] [--json]` | `POST /sindri/linear/issues/{key}/change` | owner | none | edit the named fields only |
| `sindri linear comment <ENG-12> (--body T \| --body-file F) [--client-id U] [--no-start] [--json]` | `POST /sindri/linear/issues/{key}/comments` | owner | none | comment |
| `sindri item link <item> <ENG-12>` / `sindri item unlink <item> <ENG-12>` | `POST /sindri/items/{item}/links` / `DELETE /sindri/items/{item}/links/{key}` | owner | none | link (D14) |
| `sindri linear start <ENG-12> --recipe scoping [--include-teammates-comments] [--no-start]` | `POST /sindri/linear/issues/{key}/runs` | owner | none | §6.1 |
| `sindri linear history [--since 7d] [--unresolved] [--json]` | `GET /sindri/linear/history` | owner | none | the success measure's counts and the write log |

`--json` prints the route's body (as everywhere since #272); every write's body has `outcome` (`applied`, `refused`, `unknown`), `clientId` (creates and comments), and on `applied` Linear's answer. On the CLI, field values are names (`--state "In Review"`, `--assignee me`, `--priority high`, `--label bug`), resolved against the issue's team's stored lookups; `show` prints names. The edit sends each changed field with the value the owner opened, for D6's check: the CLI reads them from the store when the command starts, the app from the sheet.

The CLI mints the client id when `--client-id` isn't given and prints it in every outcome. An unknown outcome (exit 3) ends with the exact retry line:

```
$ sindri linear comment ENG-12 --body-file note.md
Linear didn't confirm the comment (SND-LINEAR-011). Checked ENG-12: not there yet.
Retry without a duplicate:  sindri linear comment ENG-12 --body-file note.md --client-id c-7f3a91
```

`sindri linear history` prints one line per write and the week's counts:

```
$ sindri linear history --since 7d
2026-11-12 10:02  edit     ENG-12  priority          applied   owner
2026-11-12 10:05  comment  ENG-12  c-7f3a91          unknown   owner
2026-11-12 10:40  comment  ENG-14  (writeback)       applied   service:linear-writeback
week: 23 writes through Sindri (14 edits, 7 comments, 2 creates) · 4 runs from an issue · 31 Linear view opens · 1 unresolved
```

`--unresolved` prints only intents with no outcome.

Every write goes through `LinearWrite.Begin` (new, `Aw.Sindri.Core/Linear/`): no caller or an agent session is `SND-AUTH-002` ("Linear writes are the owner's: an agent session can't change Linear (fix: ask the owner to run `sindri linear …`)"). A team the profile doesn't list is `SND-LINEAR-003`. Then it appends `linear.write.requested`, does the D6 check (one live read, team included), sends the mutation, writes through (D4), and appends the outcome (D11).

**Input limits.** Title 1 to 255 characters, description and comment up to 64 KiB **(Ph0 checks Linear's own caps)**. State, assignee, label and cycle ids come from the stored lookups of the issue's team; `--parent` must be in the same team; `--cycle current|next` resolves from `linear_cycle`, and a cycle that rolled over since the last tick is `SND-LINEAR-006`.

No new environment variables.

### 5.4 Worked example: a conflict

The owner opens ENG-12 at 10:00: priority 2, state In Review. A teammate moves it to Done in Linear at 10:01 and comments. At 10:02 the owner changes its priority to 1 in Sindri and presses Save. Sindri reads ENG-12 live: priority is still 2 and the team is still ENG, so the comment and the state move don't matter for this edit. The save goes through, and the sheet shows priority 1 and state Done from the re-read. Now the same, but the teammate changed the priority to 3 at 10:01: the live read says 3, not the 2 the owner opened. The save returns `SND-LINEAR-409`, the banner says "ENG-12 changed in Linear at 10:01", the priority field shows "Linear: 3 · yours: 1", and nothing was sent. The remaining gap (a change between the live read and the mutation, a few hundred ms) is accepted and named here. Linear's API has no conditional update **(Ph0 checks)**.

### 5.5 Worked example: no answer

The owner comments on ENG-12; the mutation times out after 15 s. The ledger has `linear.write.requested` (client id `c-7f3a91`, attempt 1) and `linear.write.failed SND-LINEAR-011`. Sindri re-reads ENG-12's comments: one with id `c-7f3a91` is there, so the sheet says "Comment posted (Linear was slow to answer)" and appends `linear.write.applied`. If it isn't there, the sheet says "Linear didn't confirm the comment. Retry?", and Retry sends attempt 2 with the same client id, so it can't post twice.

## 6. Issue → run, and back (api lane)

### 6.1 Start

On the issue sheet: **Scope it** (`workflow.scoping` with `scopeInput = { kind: tracker, key: ENG-12 }`). Only `workflow.scoping.yaml` takes a tracker key today, so ship 2 is Scope it. **Build it** and **Fix it** are Ph4b, right after Ph4: the build entry recipe (`workflow.build.yaml` or `workflow.line.yaml`; Ph4b picks the one an item starts) and the bug-fix entry (`workflow.investigate.yaml`, which hands to `workflow.reproduce.yaml`) gain a tracker intake (each a recipe version bump with its eval cases; `RCP-VERSION-001`). Until then the sheet shows only Scope it, and the scoping packet's handoff is how a scoped issue reaches a build. The start command (owner only, D5):
1. Refuses with `SND-LINEAR-007` when an open item already holds the issue's link (D14): "ENG-12 is linked to it_8k2 (scoping, running). Open it, or unlink it first."
2. Makes a Sindri item titled from the issue with `ItemCreated.Link` set to the record key, and `By = owner` (no separate `item.linked`, per `ItemCreated.cs`).
3. Starts the run with that item. The brief's issue text is fenced (D10).
4. The writeback service creates the issue's Sindri comment on its next pass (§6.2).

**The tracker source.** `ITrackerSource` is registered once, as a `CompositeTrackerSource` (new): `FileTrackerSource` when `AW_SINDRI_TRACKER_DIR` is set (the fixture adapter for tests), else `StoreTrackerSource`. For a project name (`Read("Checkout revamp")`), the store source answers from the `linear-projects` record with that name, so Scope a project keeps working from stored data.

**Unlink.** `item.unlinked` (new: command id, item, record key, alias, `By`) folds into `items` and `item-aliases`: the item's link and the alias row go. The writeback stops following that issue and leaves its comment as it is, with a last line "Unlinked from Sindri at 10:42".

### 6.2 Back to Linear (service writes)

A new service, `linear-writeback`, runs in process with no route or CLI verb, so nothing a session can reach triggers it. Its state is a new view, `linear-writeback`, **keyed by issue key** (item it follows, comment id, generation, hash of the last facts written, attachments written, failures in a row), folded from `ItemCreated`/`item.linked`/`item.unlinked` with `By = owner` and from `linear.write.*` events with actor `service:linear-writeback`. So it survives a restart and a `views rebuild`.

Each pass (on a ledger change for a linked item, at most once per 30 s per issue, at most 20 writes a minute in all, and only once the view's projector has reached the ledger head):
- It renders the wanted facts from the item's current state: run kind and state, who it waits on, each PR with CI and review state, the item link. If their hash equals the last one written, nothing is sent. The "Updated" time isn't part of the hash.
- No comment yet: `commentCreate` with a client id derived from (issue, generation). If Ph0 finds `CommentCreateInput` has no `id`: the body's marker line carries the same id, and the pass reads the issue's comments (`linear_comment`, then a live read) for it before creating.
- Else `commentUpdate`.
- Each PR the run opened with no attachment yet: `attachmentLinkURL` **(Ph0 checks it's idempotent by url per issue)**. Only https PR urls on the forge host of the item's repo and `sindri://item/…` are allowed, checked in the context adapter, not only in the service.
- Before each write it does the live team check (D6): not in `teams` is `-005` and counts as a failure.

**The comment's text** (teammates read it in Linear, so plain facts first; built only from fixed fields, never request text, agent text, or the issue's own title):

```
Sindri run (automatic): scoping, waiting on a decision from Jordan Lee
PR: https://github.com/<owner>/<repo>/pull/123 (CI green, review pending)
Updated 10:42 · item it_8k2 (sindri://item/it_8k2)
<!-- sindri:issue=ENG-12;gen=1 -->
```

The owner's name is the viewer's display name from the stored viewer, as text, not a mention (a mention would notify the owner of their own comment, or nobody).

**Failures.** 429: the service parks until the `Retry-After` time and says so in Needs you. Revoked write key (`SND-LINEAR-004`), deleted comment (the next pass bumps the generation and creates a new one, once), deleted or archived issue, issue moved to a team the profile doesn't list: the item gets a Needs you note and the service stops writing to that issue after 3 failures in a row. The doctor row shows the count. `context.sources.linear.writeback: off` (new) turns the service off; turning it off mid-run leaves the comment as it is.

### 6.3 From Linear (pull side)

A Sindri service reads issue state through `ILinearIssues` after each `linear` tick. A linked issue whose `state_type` became `completed` or `canceled` while its item is open files a request (`request.filed`, existing) with "ENG-12 was closed in Linear. Stop the run, or keep it?", answered with `decision.recorded` from Needs you, with the issue chip. The request id is derived from (item, issue key, the issue's `completedAt`/`canceledAt`), and that's the event's idempotency key, so a restart or `views rebuild` doesn't file it again, and reopened-then-closed (a new time) asks again. The run is never stopped without the owner's answer (REQ-5). A linked issue reassigned away from the owner, deleted, or moved to an unlisted team is noted in the item's timeline; deleted and moved also stop the writeback for it.

## 7. Planning views (ui lane, ship 3)

**Plan** (a project's page): columns by Linear state type. Each row is the issue key and title, assignee, priority, the linked run's state chip (running, needs you, parked, done), the PR with its CI and review state (#2076's review status), and the design-doc phase whose `issues:` names it (#2076's library). Issues with no link but a suggested one show the suggestion. Filters: assignee, cycle, has run, has PR, needs review. **Cycle**: the same for a team's current cycle, with the cycle's dates and done/total. Both read stored data only: issue fields (§4.1), items and instances (`views.db`), PR records, review state.

## 8. Errors (new)

| Code | HTTP | Retry? | When | Text |
|---|---|---|---|---|
| `SND-LINEAR-001` | 400 | no | `writeToken` scheme not `keychain:`/`op:`, `writeToken` same as `token`, or `writeback` key wrong | names the key path and, for the scheme, "the write key must be in the keychain or 1Password: an env var or file is readable by agent sessions" |
| `SND-LINEAR-002` | 409 | no | a write with no write key | "Connect write access: Settings → Connections → Linear (or `sindri index connect linear --write`)." |
| `SND-LINEAR-003` | 403 | no | a write to a team the profile doesn't list, or `teams` is empty | "ENG isn't in context.sources.linear.teams." |
| `SND-LINEAR-004` | 502 | no | Linear refused the write key (revoked, read-only) | "Linear refused the write key. Make a new one with write permission." |
| `SND-LINEAR-005` | 404 | no | unknown issue key, or the issue was deleted or moved out of the profile's teams (store or live read) | "No issue ENG-99 in your teams (deleted, moved, or not synced yet: `sindri linear show` after the next tick)." |
| `SND-LINEAR-006` | 400 | no | a field value that isn't in the team's stored lookups, or matches more than one | "No state 'Reviw' in ENG. States: Backlog, Todo, In Progress, In Review, Done." / "'Sam' matches 2 members: Sam Lee, Sam Ito." |
| `SND-LINEAR-007` | 409 | no | link or start on an issue an open item already holds | "ENG-12 is linked to it_8k2 (scoping, running). Open it, or unlink it first." |
| `SND-LINEAR-409` | 409 | after a look | D6: a field changed in Linear since it was opened (the suffix follows `SND-JOB-409`) | §5.4's text |
| `SND-LINEAR-010` | 503 | yes | Linear unreachable before sending | "Couldn't reach Linear, so nothing was changed. Try again." |
| `SND-LINEAR-015` | 502 | no | Linear refused the write with a 4xx other than the key (validation) | "Linear said no: <its message, first 200 chars>. Nothing was changed." (the message is shown, never stored: D11) |
| `SND-LINEAR-011` | 504 | same client id | sent, no answer (timeout, connection reset, 5xx); CLI exit 3 | "Linear didn't confirm it. Checking ENG-12…" then §5.5's text |
| `SND-LINEAR-012` | 429 | after the time | rate limited | "Linear's rate limit: try again after 10:47." Nothing was sent |
| `SND-LINEAR-013` | 200 | no | Linear applied it, but storing the answer failed | "Saved in Linear; Sindri shows it after the next sync." |
| `SND-LINEAR-014` | 409 | no | no usable keychain to store the write key | "No keychain to keep the write key in. Unlock your login keyring, or put the key in 1Password and set writeToken to op://…" |
| `SND-AUTH-002` (existing) | 403 | no | an agent session's write, link, start, or owner-only read | §5.3's text |

An edit sends all its fields in one `issueUpdate`, so it applies all or nothing; there's no partial edit to report. Each code is a row in `SindriErrors.cs` and `docs/csharp/sindri.md`'s error table.

## 9. Security

- **Agent sessions run as the owner's uid.** D3 keeps the write key out of env, files, argv and every read path in the process (an architecture test checks the last). On Linux the keyring is unlocked for the login session, so a session that deliberately looks can read it; that's accepted. This guards against honest mistakes, not a deliberate bad actor with the owner's shell.
- **Record text is untrusted.** An issue description can't trigger a write. Every write has an owner caller, or is a writeback on an issue whose link says `By = owner`. Issue text reaching a run is fenced, and teammates' comments are left out by author id (D10). The sheet shows teammates' text as plain text: no remote images, and no `sindri://` link from record text.
- **Writes are limited to the profile's teams** (`SND-LINEAR-003`), which must be listed: checked from the store before any call, and from the live read before the mutation.
- **No mass writes.** Owner commands touch one issue each. The writeback touches only linked issues, writes only on change, and at most 20 times a minute.
- **Every write is in the ledger** (D11) with its actor, and no key, typed text or Linear error message.
- **Attachment urls** are https PR urls on the forge host of the item's repo, or `sindri://item/…`, checked in the context adapter.
- **One click approves only the key.** Connect write access auto-approves only when the pointer is the whole pending change (§5.1).

## 10. Docs to update

`docs/sindri/linear.md` (write key, writeback, the read-only line, the `sindri_linear` MCP tool: read only), `docs/csharp/sindri-profile.md` (`writeToken`, `writeback`), `docs/csharp/sindri.md` (CLI, exit codes, errors, events), the events catalog (`EventCatalog` rows for `linear.write.*` and `item.unlinked`, the new `By` field), `docs/csharp/sindri-ui.md` (Work's Linear view and strip, the `Issue` sheet kind and its marker, New issue, Settings → Connections, Plan and Cycle, the palette rows "Go to issue…" and "New Linear issue"), `docs/csharp/sindri-scoping.md` (`CompositeTrackerSource`, `StoreTrackerSource`, the key form), `planning/ERD.md` (`linear_issue`, `linear_comment`, `linear_issue_change`, the lookup tables), `deps.yaml` (no new edge), the owned index spec's D8 and the Projects spec's non-goal (both say read-only: a pointer to this spec), AGENTS.md Commands, `src/Sindri/aot-smoke/expected.txt` (the doctor row). PR titles and doc sections name the feature, not the phase. No new environment variables, so `.env.example` doesn't change.

## 11. Rate and freshness

The `linear` feed ticks every 5 minutes today. A change in Linear shows in Sindri within one tick, and Sindri's own writes show when Save returns (D4). Linear's limit **(Ph0 checks: documented as requests per hour plus a complexity budget, per user)** is probably shared by both keys of one user, so the doctor row counts both and warns past half the budget. An owner save costs about three calls (the D6 read, the mutation, the re-read). The owner's commands don't retry by themselves (a 429 is `SND-LINEAR-012`); the writeback parks (§6.2). Ph0 measures one tick's cost, the `;f3` re-read's cost (pass: under 10 minutes for 1,000 issues on the fake's timing), and a save's, on the fake and on the owner's scratch team. Open question 2 asks whether 5 minutes is fast enough.

## 12. Phases and parallel plan (for /specToProvenPR)

Ship 1a = Ph0, Ph1, Ph3's list (read-only Work → Linear and the strip: daily use starts here). Ship 1b = Ph2, the rest of Ph3 (writes). Ship 2 = Ph4, Ph4b, Ph5 (start from an issue, links, write back). Ship 3 = Ph6 (planning).

| Phase | Lane | What | Depends on | Proof |
|---|---|---|---|---|
| **Ph0** | context | **Go/no-go.** Linear facts: mutation names and inputs, client `id` on `IssueCreateInput` and `CommentCreateInput`, `attachmentLinkURL` idempotency, conditional update (none?), what bumps `updatedAt`, mention syntax in comment bodies, key scope and how to read it, rate limit numbers and whether two keys share them, input caps. The stateful fake (D15) with its switches and README | — | `scripts/linear-probe.sh` (new) over the fake, and over the real API on the owner's **scratch** team only (owner-run, key from the keychain, never the real team); the facts written into §5. Fallbacks: no client id → a marker in the description/body plus read-before-create, shown as "may have been created: check ENG"; no key scope → the Settings row reports "can't tell, try a write on a scratch team"; **no scratch team** → the probe writes only issues it creates, under a `sindri-probe` label in one real team, and deletes nothing; **workspace blocks write keys** → ship 1b doesn't ship, ship 1a stays read-only and the Settings row says "your workspace doesn't allow write keys" |
| **Ph1** | context | `linear_issue`, `linear_comment`, `linear_issue_change` and lookup tables (migration), side rows on `RecordWrite`, nullable `RecordBatch.Position`, the Commit older-`UpdatedAt` rule, `LinearText` without Sindri comments, `;f3` re-read, the lookups tick step, `IContextIssues` with `Since`, the shared `LinearCalls` | Ph0 | unit: field and comment mapping per issue shape, change rows per field; integration: a tick over the fake stores fields, comments and lookups, a `;f2` store re-reads once, **a write-through while a tick holds the lock lands and isn't reverted**, a tombstone hides the issue |
| **Ph2** | api + context | `writeToken` (keychain/op only, `-014`) + `connect --write` + the Settings route, `ILinearWrites` + `LinearWriteKey` + write-through, Sindri ports and Features adapters, `LinearWrite.Begin`, the issue commands, `lookups`, D6 check with team, D11 events, errors, doctor rows, `linear history` | Ph0, Ph1 | unit per command and refusal (`SND-AUTH-002` for a session through CLI and API, 001 for an `env:` and a same-as-read key, 003, 005 incl. a live team move, 006, 014, 409); integration on the fake: create → stored at once, retry same client id → one issue, no-answer → exit 3 then "already applied"; the approve-only-the-pointer rule with a pending `teams` edit; the key in no ledger, log or span; architecture test on the two keys; aot-smoke `expected.txt` |
| **Ph3** | ui | Mockups first (`/design-mockup`: Work's Linear view with the strip, the issue sheet with the conflict banner, New issue, the Settings row; every state in §3's table; 1,000 and 1,440 px), owner's pick, then the views. The list and strip ship first (1a), writes after Ph2 (1b) | Ph1 read goldens (hand-written first), Ph2 for 1b | UI tests: filter, strip dismiss, edit, unsaved-edit prompt, conflict banner, disabled-without-key then live enable, offline, a description with an image url and a `sindri://` text (neither loads nor links), one test per accessibility line; screenshot run on the fake |
| **Ph4** | api | `CompositeTrackerSource` + `StoreTrackerSource`, the start command (`ItemCreated.Link`), `item link|unlink` and `item.unlinked`, `By` on link events, the one-open-item rule, suggestions, the pull-side request | Ph1, Ph2 | integration: scoping from a stored issue without `AW_SINDRI_TRACKER_DIR`, teammates' comments left out by author id (incl. a forged owner-shaped line), the text fenced; a second start is `-007`; recipe eval case `tracker-from-store`; a session's start and link refused; a restart doesn't re-file the closed-issue request |
| **Ph4b** | api | tracker intake for the build and bug-fix entry recipes (§6.1), Build it / Fix it buttons | Ph4 | recipe evals per recipe, version bumps |
| **Ph5** | api | `linear-writeback` service and view (living comment, PR attachments) | Ph2, Ph4 | integration on the fake: a run's moves edit one comment (count = 1), a PR adds one attachment, **a restart and a `views rebuild` mid-run write nothing twice**, a crash after create leaves one comment, a deleted comment is re-made once, a session-appended `item.linked` (`By = session`) gets no comment, 429 parks, the per-minute cap holds, an issue moved to an unlisted team stops writes |
| **Ph6** | ui | Plan and Cycle views | Ph1, Ph4, #2076's review state and `IContextLibrary` | UI tests over a fixture with runs, PRs, reviews and suggested links |
| **Ph7** | — | Owner proof (§13), the two-week measure | each ship | §13 |

**Parallel plan.** Day one: context on Ph0, then straight into Ph1. ui on Ph3's mockups (they need only this spec). api reads Ph0's facts and starts Ph2's slices red against the fake. After Ph1: ui ships the 1a list against Ph1's goldens; api runs Ph2, then Ph4, Ph4b and Ph5 (Ph4's link and tracker slices can start red alongside Ph2: different folders). Ph4b and Ph5 run in parallel after Ph4; Ph6 after Ph4. Context is free after Ph1 for #2076's Ph1/Ph2 (the library), which this spec's Ph6 reads.

**Shared files, one owner each.** Several phases touch the same files; to avoid merge fights, each has one owning phase and later phases rebase on it: API goldens and `docs/csharp/sindri.md`: Ph2 creates the Linear sections, Ph4 and Ph5 append. Help text (`Usage.cs`) and `PublicAPI.Unshipped.txt`: same order. `aot-smoke/expected.txt`: Ph2 only. `docs/csharp/sindri-ui.md`: Ph3, then Ph6. `EventCatalog`: Ph2 (`linear.write.*`), then Ph4 (`item.unlinked`, `By`).

**Gates per phase.** `-warnaserror` build; the touched projects' unit and integration lanes; `Aw.Architecture.Tests` (new folders, the key test); scoped format; `check-profile-doc.mjs` (Ph2, Ph5); `check-env-example.sh`; `check-deps.mjs` (Ph2); ApiGoldens classes, integration lane, `AW_GOLDEN_UPDATE=1` to regenerate (Ph1, Ph2, Ph4, Ph5); `Aw.Sindri.Ui.Tests`, Avalonia objects only inside `HeadlessUnitTestSession.Dispatch` (Ph3, Ph6); aot-smoke with `expected.txt` (Ph2's doctor rows); recipe evals (Ph4, Ph4b).

## 13. Proof

| Row | Check |
|---|---|
| **TP-1** | On the fake: a tick stores fields, comments and lookups for every fixture issue; Work's Linear view lists them filtered by assignee, cycle and label; a change in the fake shows after one tick; the strip lists a new assignment and a mention, not the owner's own save, and clears on Dismiss. Verifies REQ-1, REQ-9 |
| **TP-2** | Create, edit two fields, comment: each is in the fake and on the page when the command returns, including while a tick holds the source lock; a retried create and a retried comment (same client id) make one each; the ledger holds a requested and an applied event per write. Verifies REQ-2, D4, D11, D12 |
| **TP-3** | The §5.4 conflict on the fake: a teammate's comment doesn't block a priority edit; a teammate's priority change is `SND-LINEAR-409`, nothing sent, the banner shows both values; an issue moved to an unlisted team is `-005`, nothing sent. Verifies D6 |
| **TP-4** | An agent session's create, edit, comment, link, start, lookups and since: `SND-AUTH-002` each, nothing sent, a `linear.write.refused` event for each write; its issue reads work. An `env:` write key and a same-as-read key are `SND-LINEAR-001`. Verifies REQ-7, D3 |
| **TP-5** | Scope it from a stored issue: the brief is the issue (fenced, no teammates' comments by author id), the item is linked with `By = owner`, the issue has one Sindri comment, and the issue's indexed text doesn't contain it. Verifies REQ-3, D10 |
| **TP-6** | The run moves four times and opens a PR; Sindri restarts and `views rebuild` runs in between: still one living comment (edited), one attachment, no second comment. Verifies REQ-4, D8 |
| **TP-7** | The issue is completed in the fake mid-run: one request in Needs you, also after a restart, the run still running until answered. Verifies REQ-5 |
| **TP-8** | Link and unlink by hand; a second link on a held issue is `-007`; a PR naming ENG-12 shows a suggestion that isn't applied until clicked. Verifies REQ-8 |
| **TP-9** | Plan and Cycle on the fixture show run, PR, review and doc phase per row. Verifies REQ-6 |
| **TP-10** | On the owner's laptop, a scratch Linear team (or Ph0's label fallback): connect write access from Settings, create, edit, comment, start a scoping run, see the automatic comment in Linear. Verifies REQ-1 to REQ-4 for real |

## 14. Open questions for the owner

1. **One key or two?** I'd go with two: the index keeps its read-only key, and writes get their own key, kept in the keychain, that only the write path loads. Then nothing that reads Linear text (the sync, the decision pass, agent sessions' reads) holds a key that can change Linear. The cost is one more key to make, pasted once in Settings. The alternative is one read+write key for everything, which is simpler but gives every read path write power.
2. **Is a 5-minute sync fast enough?** I'd say yes for v1, since your own writes show at once (D4) and only teammates' changes wait up to 5 minutes. If it bugs you, the next step is a 1-minute tick while the app is open (it uses more of Linear's rate budget, measured in Ph0).
3. **Linear issues as a view on Work, or their own tab?** I'd go with a Sindri / Linear switch on Work plus Plan/Cycle on Projects, so no tenth tab at the 1,000 px minimum. The alternative is an Issues tab, with the window's minimum width measured again.
4. **Should only you start runs from an issue?** I'd say yes, for all three run kinds: an agent-started run would make Sindri comment on a Linear issue you never touched. An agent that wants one started can ask you through Needs you. The alternative lets a session start scoping runs (reads only, but the comment still lands in Linear).
5. **Should Sindri ever move an issue's state?** I'd say not in this spec: the run's state shows in the comment, and your Linear-GitHub integration probably moves issues on merge already. Moving state ("In Progress when a run starts, Done on merge") goes to Later, as a per-team opt-in. The alternative puts it in ship 2, with its own guard (only when the state is still the one Sindri last saw).
6. **Do you have a scratch Linear team, and can your workspace make write-scoped personal keys?** Ph0 needs the answer. Without a scratch team, Ph0 writes only issues it creates, under a `sindri-probe` label in one real team. Without write keys, only the read-only half (ship 1a) ships.
