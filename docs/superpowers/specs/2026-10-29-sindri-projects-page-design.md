# Sindri's Projects page: one card per work stream the owner runs, with links into Linear and the repo

**Status:** draft for the owner, revision 1 · **Date:** 2026-10-29 · **Issue:** #1566 · **Builds on:** the owned index spec `2026-10-27-sindri-owned-index-design.md` (the record store, the Linear feed, the GitHub feed, "no live calls at query time" §6.1), the API surface spec `2026-10-10-sindri-api-surface-design.md` (views, `asOf`, `empty`, who may run what), the UI spec `2026-10-11-sindri-ui-design.md` and `docs/csharp/sindri-ui.md` (pages, the action catalog, labels), owner rulings q145 and q146 (no live Linear search when a query runs; one batched `updatedAt` check for cited records) · **Next:** /specToProvenPR takes §11's phases to PRs.

Every file path is one the author opened on main (e280467e8). Every command, route, event and type is marked **existing** or **new**.

## 0. Why

The owner runs at least four projects at once, each in a different state. Linear is too fine-grained to show that: it shows issues and cycles, not "what am I driving and where does each one stand". The owner wants a page in the Sindri app with one card per work stream: what it is, what state it's in, how much is open, when it last moved, and a link out to Linear or the code when they need the detail.

Projects come from two places (#1566):
1. **Linear projects the owner leads.** Linear's `lead` field names the owner.
2. **Projects attached by hand.** A Linear project the owner really drives but doesn't lead, or a work stream that isn't in Linear at all.

What Sindri has today (checked in code, 2026-10-29):

| Need | Today | Where |
|---|---|---|
| Linear projects in the store | **None.** `Projects.graphql` (id, name, url, `state`, dates, `lead{id name}`, teams) runs only for live lookups (`LinearRecords.cs:115,207`, `LinearSearch.cs:104`, `LinearScope.cs:51`). The key `linear:project/<id>` exists (`LinearRecordKeys.cs:10`), but no feed writes it | `src/Context/Aw.Context.Infrastructure/Linear/` |
| Which project an issue is in | **Fetched, then dropped.** `IssuesPull.graphql:9` asks for `project { id name }`; `LinearIssueItem.Of` keeps only a `Project: <name>` line in the text (`LinearText.cs:43`). The link kind `InProject` exists (`RecordLinkKind.cs:43`) and nothing emits it | `LinearIssueItem.cs:19-31` |
| Who the owner is on Linear | **Nothing.** No `viewer` query, no profile key. The probe reads `projects`, `documents`, `issues` with `first: 1` (`Probe.graphql:2-4`) | — |
| Which PRs belong to an issue | **Not from Linear.** `IssuesPull` reads no attachments. GitHub PR records carry `closes`/`relates` links to GitHub issues only (`GitHubRecords.cs:17-53`) | `src/Connectors/Aw.Connectors.Features/PullGitHub/` |
| Open PRs per repo | Derivable: `source_record` rows with `kind = 'pr'`, `state` in (`open`, `draft`), by `repo` (index `source_record_by_state`, `0018_s4_record_store.sql:27`). No branch is stored (`GitHubPullDetail.cs:8`) | record store |
| A projects view, page or event | **None.** `PageId.cs:4-32` lists Home, Workflows, Work, Recipes, Context, Agents, System, Jobs, Settings. Items have no project or tag (`ItemSummary.cs:16-25`) | `src/Sindri/Aw.Sindri.Ui/Navigation/` |

So the page needs three new facts in the store (projects, issue→project, issue→PR) and one new piece of owner state (attached projects). Everything else is a read.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| P1 | **The page reads only stored data.** Opening it makes no Linear or GitHub call. Freshness is the last sync, shown on the page | q145/q146: no live calls at query time. Same rule as the owned index §6.1 |
| P2 | **Linear projects are records in the owned index**, written by a new project feed beside the issue feed, keyed `linear:project/<id>` (existing key), kind `Project` (existing `RecordKind.Project`) | One store, one sync driver, one progress line (owned index §5.2, §5.5). The map and search get the projects too, for free |
| P3 | **"The owner" on Linear is the user the Linear key belongs to** (Linear's `viewer`), read once per sync tick and saved. A profile key overrides it when the key isn't the owner's own | The owner sets up nothing new. The override covers a shared or bot key (§9 Q2) |
| P4 | **Card counts and the repo link come from links in the store**: issue `in-project` project, and issue `relates` PR from the GitHub attachments Linear keeps on each issue. A card's repos are the repos of its PRs, plus any the owner adds by hand | Linear's GitHub integration already records which PR fixes which issue. No branch naming rule to guess at, no new GitHub call |
| P5 | **Attached projects are Sindri ledger events** (`project.attached`, `project.changed`, `project.detached`), folded into `views.db`. Commands are desk class: the app and the CLI may run them, an agent session is refused (`SND-AUTH-002`) | Owner state that must survive a resync and every map rebuild. Not the profile: every edit would need `profile approve` (`docs/csharp/sindri-profile.md:9-10`) |
| P6 | **A card is a summary, never a task list.** It shows counts and links, not issues | #1566: "a mental-model card, not a task list" |
| P7 | **Items don't belong to projects in v1** | #1566 default. §10 says where it would go |
| P8 | **A project's open issues are always in the store, however old.** The window (`context.index.windowDays`, default 183) still bounds everything else | An open issue nobody touched for seven months is still open. Without this the count is quietly low (§4.3) |

## 2. Goals and non-goals

**Goals**
- The app has a **Projects** page with one card per project the owner leads in Linear, plus each attached project.
- Each card shows: name, state, health (when Linear has one), last activity, open issues, open PRs, the repos, and links to the Linear project and each repo's PR list.
- The owner can attach a Linear project they don't lead, add a project that isn't in Linear, set its state, add repos and links, hide a project they lead but don't want on the page, and take any of that back.
- `sindri projects [--json]` prints the same cards (new verb), so an agent session can read them.
- Opening the page costs no network call. A Linear or GitHub sync that is failing shows on the page as "as of 14:05, Linear sync is failing".

**Non-goals**
- Issue lists, burndown, cycles, estimates or any planning feature. Linear does that.
- Editing anything in Linear. Sindri's Linear key is read-only (owned index §5.4).
- Items or workflow instances grouped by project (P7).
- Projects from other trackers (Jira, GitHub Projects). The page reads `source_record` rows of kind `Project`, so a later feed that writes them shows up without a page change. That's the only hook this spec leaves.
- Per-branch PR counts. The GitHub feed stores no branch (`GitHubPullDetail.cs:8`).

## 3. What a card shows

```
┌──────────────────────────────────────────────────────────────┐
│ Checkout rewrite                              In progress · ● at risk │
│ Lead: you · Linear · target Nov 14                                     │
│                                                                        │
│ 12 open issues · 3 open PRs · last activity 2 h ago                    │
│ repos: web-app, api                                                    │
│                                                                        │
│ [Open in Linear]  [web-app PRs]  [api PRs]                    [⋯]      │
└──────────────────────────────────────────────────────────────┘
```

| Field | Linear project | Attached Linear project | Hand-made project (no Linear) |
|---|---|---|---|
| Name | Linear's name | Linear's name | The owner's |
| State | Linear's project status (§4.1), one of: Backlog, Planned, In progress, Paused, Done, Canceled | Linear's, unless the owner set one (then theirs, marked "set by you") | The owner's, same six words |
| Health | Linear's newest project update: on track, at risk, off track. Blank when there's none | same | none |
| Lead | "you" or the lead's name | the lead's name | none |
| Target date | Linear's `targetDate` when set | same | the owner's, optional |
| Open issues | stored issues `in-project` with state `open` (P8) | same | none (no tracker) |
| Open PRs | stored PRs in state `open` or `draft` linked to any of the project's issues (P4) | same | open PRs in the project's repos, labelled "in web-app (whole repo)" since there's nothing narrower |
| Last activity | the newest `updated_at` of the project, its issues and their PRs | same | the newest `updated_at` of the repo's PRs |
| Repos | repos of the linked PRs, plus repos the owner added | same | the owner's |
| Links | the Linear project url, each repo's open-PR list, any url the owner added | same | the repo PR lists and the owner's urls |
| Note | none | the owner's one line, optional | the owner's one line, optional |

A repo's "PR list" link is `https://github.com/<owner>/<repo>/pulls`. Sindri builds it from the repo's GitHub name in `context.sources.github.repos`. A repo that isn't listed there has no PR link and no PR count; the card says "PRs not synced for this repo" instead of a 0.

**Order.** In progress first, then Planned, Paused, Backlog. Inside each group, the newest last activity first. Done and Canceled projects stay on the page for 14 days after they closed, under a folded "Finished" row, then leave the page. They stay in the store and come back if Linear reopens them.

**Empty states** (the served `empty` sentence, API spec §3.6):
- No Linear source configured and nothing attached: "No projects yet. Connect Linear or add one by hand." with the **Add project** button.
- Linear configured and synced, the owner leads nothing: "You don't lead any Linear projects. Attach one you drive, or add one by hand."
- Linear configured, the first sync hasn't reached projects yet: "Linear is still syncing (usable at about 14:20). Projects you lead show up then."

**Freshness line**, under the page title, from the same source rows `sindri index status` prints (owned index §5.8): "Linear synced 4 min ago · GitHub synced 2 min ago". A source that is failing or waiting shows its text from that table ("Linear: needs token"), and the counts on every card are marked "as of 13:52". The lens freshness rows (#1559) read the same state (`IIndexSync.Progress`, `IndexSync.cs:43`).

## 4. Data: what the sync stores

### 4.1 The project feed (new, Context)

`LinearProjectFeed : RecordFeed` in `Aw.Context.Infrastructure/Linear/`, source id `linear` (the same source as the issue feed, so one progress row and one doctor row `index:linear`). It runs on the same `index-sync` service tick, every 5 minutes (`IndexSyncService.cs:12-27`), through the same driver (`RecordSyncRun.cs`).

**What it reads.** A new operation `ProjectsPull.graphql`:

```graphql
query ProjectsPull($filter: ProjectFilter, $first: Int, $after: String) {
  viewer { id name }
  projects(filter: $filter, first: $first, after: $after, orderBy: updatedAt) {
    nodes {
      id name description url updatedAt createdAt startedAt completedAt canceledAt targetDate
      status { name type }
      lead { id name }
      teams { nodes { key } }
      lastUpdate { health createdAt url }
    }
    pageInfo { hasNextPage endCursor }
  }
}
```

Filter: the profile's teams (`context.sources.linear.teams`, `LinearFilters.InTeams`), the same scope as the issue feed. An empty list means every team the key can see, as today.

`status { name type }`, `startedAt` and `lastUpdate` aren't in today's `Projects.graphql`. The legacy `state` string is. Phase P0 runs this query once against the owner's workspace and records the answer. If `status` or `lastUpdate` is missing, the feed falls back to `state` (same six values) and no health, and the spec gets the field list that worked.

**No window, no cursor.** Projects are few: tens, rarely hundreds. Each tick reads every project in scope, 50 a page, 1 to 4 calls, and commits them as one page. A project that disappears from the answer (deleted, or moved to a team out of scope) gets `removed_at` in the same commit, the way Reconcile does for other records (owned index §5.5). That's simpler than a cursor and catches every status change within 5 minutes. Cost: 288 ticks a day × 1 to 4 calls = 288 to 1,152 Linear calls a day. Linear's limit for an API key is 1,500 requests an hour (§9 Q4 checks it on the owner's plan).

**Stored row** (`RecordItem`, `RecordItem.cs:23-38`):

| Field | Value |
|---|---|
| Key | `linear:project/<id>` |
| Kind | `Project` |
| Title | name |
| Text | name, description, status name, target date, the newest update's health, scrubbed like any record (owned index §4.5). Searchable and embedded like any record |
| Author | lead name |
| Url | the project url |
| State | status type mapped: `backlog`, `planned`, `started`, `paused` → `open`; `completed` → `done`; `canceled` → `canceled` |
| StateRaw | the status `type` (`started`, `paused`, ...). The card's six words come from this, not from the status name, which teams rename |
| Created/Updated/Closed | `createdAt`, `updatedAt`, `completedAt` or `canceledAt` |
| Version | `updatedAt` plus `lastUpdate.createdAt`, so a new project update re-saves the record even when Linear doesn't bump the project's `updatedAt` |
| Links | `led-by` → `linear:user/<lead id>` with the lead's name as the link title (**new link kind** `LedBy`) |
| Audience | `linear:<first team key>` |

Health and target date have no column. They live in `RecordItem` as two optional new fields, `Health` (`onTrack`, `atRisk`, `offTrack`) and `DueAt`, stored as two new nullable columns on `source_record` (migration `0020_project_fields.sql`, `planning/ERD.md` gets the same). The alternative was parsing them back out of the text, which breaks the moment the text format changes.

### 4.2 Who the owner is (new)

`viewer { id name }` rides on the first `ProjectsPull` page of each tick, so it costs no extra call. The feed saves it into a new one-row-per-source table:

```sql
-- 0020_project_fields.sql (same migration as §4.1's two columns)
CREATE TABLE source_identity (
    source   TEXT PRIMARY KEY NOT NULL,   -- 'linear'
    user_ref TEXT NOT NULL,               -- 'linear:user/<id>'
    name     TEXT NOT NULL,
    read_at  TEXT NOT NULL
) STRICT;
```

The profile key `projects.linear.me` (new, optional, a Linear user id) wins over the viewer when set. It's for a box whose Linear key belongs to a bot or a shared account. `profile approve` shows it like any key. `check-profile-doc.mjs` gets the row.

"Led by the owner" is then: a `Project` record with a `led-by` link whose `to_ref` equals the owner's `user_ref`.

### 4.3 Issues: their project, their PRs, and old open issues

Three changes to the existing issue feed (`LinearFeed.cs`, `LinearIssueItem.cs`, `IssuesPull.graphql`):

1. **`in-project` link.** `LinearIssueItem.Of` emits `RecordLink(InProject, linear:project/<project.id>, title: project.name)` when `project` is set. The field is already fetched (`IssuesPull.graphql:9`). The issue's version doesn't change, so already-stored issues pick the link up on their next save, and P0's backfill (below) forces one pass.
2. **PR links from attachments.** `IssuesPull.graphql` adds `attachments(first: 10) { nodes { url sourceType } }`. Each attachment whose url parses as `https://github.com/<o>/<r>/pull/<n>` becomes `RecordLink(Relates, github:pr/<o>/<r>/<n>)`. That's the same key the GitHub feed writes (`GitHubRecords.cs`), so `record_link.to_key` resolves when the PR is synced and stays a dangling `to_ref` when it isn't (a repo not in the profile). The cost is one more connection per issue in an already-paged query. P0 measures the query's complexity at 25 issues a page (`LinearFeed.cs:17`) and drops the page size to 20 if Linear refuses it.
3. **Open issues of shown projects, whatever their age (P8).** After the project page commits, the feed reads, for every project the page shows (led by the owner, or attached), the ids and `updatedAt` of its issues that aren't done or canceled:

   ```graphql
   query ProjectOpenIssues($projects: [ID!], $first: Int, $after: String) {
     issues(filter: { project: { id: { in: $projects } }, state: { type: { nin: ["completed", "canceled"] } } }, first: $first, after: $after) {
       nodes { id updatedAt }
       pageInfo { hasNextPage endCursor }
     }
   }
   ```

   Any id the store doesn't have at that `updatedAt` is fetched in full through the existing `IssuesPull` with an `id: { in: [...] }` filter (at most 25 a call) and committed like any page. That's usually zero calls a tick; the first tick after a project shows up is the expensive one (a project with 300 open issues is 3 id pages plus up to 12 issue pages).

   A stored issue that is open in the store but not in this answer has closed or left the project. It gets re-fetched by id in the same pass, so the count drops within one tick instead of waiting for the issue to be re-synced by `updatedAt`.

   These records are ordinary records: searchable, on the map. The window (D12) keeps bounding the *issue feed*. It doesn't delete them, because retention is `retainDays` (owned index Q7), not the window.

**Backfill on upgrade.** Stored issues need the two new links, and a replayed page with the same `version` writes nothing (owned index §5.5). So the issue's `Version` becomes `updatedAt` plus a feed-format suffix (`;f2`), and migration 0020 resets the Linear source's `source_cursor` row to the Recent phase. The next ticks re-read the window, every issue's version differs, and each is re-saved with its links. That's a full re-read of the window's issues, the size of the first sync (owned index §5.5). A test pins that a stored issue without the suffix is re-saved and one with it is skipped.

### 4.4 GitHub

Nothing new is fetched. The page reads PR records the GitHub feed already stores: state, repo, `updated_at` (`GitHubRecords.cs:17-53`). A PR links to a project only through an issue's attachment (§4.3). A PR in a listed repo with no Linear issue attached counts toward nothing, except a hand-made project's "whole repo" count.

## 5. Owner state: attached and hidden projects

### 5.1 Events (new, ledger)

| Event | Payload | When |
|---|---|---|
| `project.attached` | `id` (Sindri's, `prj_<ulid>`), `linear` (`linear:project/<id>` or null), `name` (required when `linear` is null), `state` (null, or one of the six words), `repos` (profile repo names), `links` (urls, https only, at most 10), `note` (one line, at most 200 characters), `target` (date or null) | The owner attaches a Linear project or adds one by hand |
| `project.changed` | `id`, `version` (the one the sheet showed), the fields that changed | Any edit |
| `project.detached` | `id`, `version` | Remove an attached project |
| `project.hidden` | `linear` (`linear:project/<id>`) | Hide a project the owner leads |
| `project.shown` | `linear` | Take a hide back |

Every event is public (`EventCatalog`, `Public: true`) and invalidates the new view `ApiViews.Projects`. A version that doesn't match the current one is refused with `SND-PROJECT-002` ("this project changed since you opened it"), the same rule `CloseItem` follows.

Attaching a Linear project the owner already leads is refused (`SND-PROJECT-001`, "you already lead this one, it's on the page"). Attaching the same Linear project twice is refused the same way. The UI's attach picker never offers either.

An attached project's `state`, when set, overrides Linear's on the card, marked "set by you". Clearing it goes back to Linear's.

### 5.2 Projection

`ProjectsProjection : IProjection` (`Core/Projects/`) folds the five events into one `views.db` table `project_attached` (id, linear key, name, state, repos JSON, links JSON, note, target, version, created/updated) plus `project_hidden` (linear key). Pure fold, rebuilt by `sindri views rebuild` like every projection (API spec §3).

### 5.3 Commands (new)

| Command | Route | Class | CLI |
|---|---|---|---|
| `AttachProject` | `POST /sindri/projects` | desk | `sindri project add --linear <url-or-key> \| --name <text> [--repo R]... [--link URL]... [--state S] [--note T] [--target DATE]` |
| `ChangeProject` | `POST /sindri/projects/{id}/change` | desk | `sindri project edit <id> [same flags] [--clear-state]` |
| `DetachProject` | `POST /sindri/projects/{id}/detach` | desk | `sindri project remove <id>` |
| `HideProject` / `ShowProject` | `POST /sindri/projects/hide`, `/show` | desk | `sindri project hide <linear-key>` / `show` |

Desk class: the app and the CLI at the terminal run them; an agent session gets 403 `SND-AUTH-002` (API spec, "who may run what"). Each gets a row in `Host/Catalog/ProjectRows.cs` (new) and a label in `Labels.Actions.cs` (`ActionLabelsTests` enforces it), and `Golden/Api/commands/catalog.json` gains five rows.

`--linear` takes a Linear project url (`https://linear.app/<ws>/project/<slug>-<id>`) or a `linear:project/<id>` key. The url's slug id is matched against stored projects' `url`, so the command needs no Linear call. A project that isn't in the store (out of the profile's teams) is refused with `SND-PROJECT-003` and says which teams the profile syncs.

## 6. Read path

### 6.1 Context: `ProjectFacts` (new query, Core port + Infrastructure SQL)

`IRecordStore` gets one method:

```
ProjectFacts(ProjectFactsRequest(UserRef? Owner, IReadOnlyList<RecordKey> Projects, IReadOnlyList<ContextRepoName> WholeRepos), ct)
  -> ProjectFactsAnswer(IReadOnlyList<ProjectFact> Projects, IReadOnlyList<RepoFact> Repos, UserRef? Owner)

ProjectFact  Key, Title, Url, StateRaw, Health, DueAt, Lead, LeadIsOwner, UpdatedAt, ClosedAt, RemovedAt,
             OpenIssues, OpenPrs, LastActivity, Repos (from linked PRs)
RepoFact     Repo, OpenPrs, LastActivity
```

It returns every non-removed project led by `Owner` plus each key in `Projects`, with counts done in SQL over `record_link` and `source_record` (one statement per kind; the indexes `record_link_by_to_key` and `source_record_by_state` cover them). `Owner` null means "read it from `source_identity`, or the `projects.linear.me` key".

A unit test pins each count against a seeded store; an integration test pins the statement plan uses the indexes (no full scan of `source_record`) at 50,000 records.

### 6.2 Sindri: `ListProjects` (new query slice)

`GET /sindri/projects` (Features `ListProjects`, Host `ListProjectsEndpoint`, `EndpointTable.MapQueries`):

1. Read `project_attached` and `project_hidden` from `views.db`.
2. Call Context's `ProjectFacts` with the attached Linear keys and the hand-made projects' repos (through the Host's Context glue, as `ContextCalls` does today).
3. Merge: led projects minus hidden, plus attached; apply the owner's overrides; order (§3).
4. Read the Linear and GitHub sync rows (`IIndexSync.Progress`) for the freshness line.

Answer:

```json
{
  "asOf": { ... },
  "empty": null,
  "freshness": [ { "source": "linear", "state": "ok", "text": "synced 4 min ago", "at": "2026-10-29T13:52:00Z" } ],
  "projects": [
    {
      "id": "linear:project/8f2c...", "attachedId": null, "origin": "led",
      "name": "Checkout rewrite", "state": "inProgress", "stateSetByOwner": false,
      "health": "atRisk", "lead": "you", "target": "2026-11-14",
      "openIssues": 12, "openPrs": 3, "prScope": "linked",
      "lastActivity": "2026-10-29T11:40:00Z",
      "repos": [ { "name": "web-app", "prsUrl": "https://github.com/acme/web-app/pulls", "synced": true } ],
      "links": [ { "label": "Linear", "url": "https://linear.app/..." } ],
      "note": null, "finished": false, "version": null
    }
  ],
  "finished": [ ... same shape ... ]
}
```

`origin` is `led`, `attached` or `manual`. `prScope` is `linked` (PRs linked through issues) or `wholeRepo` (a hand-made project). Goldens under `tests/Aw.Sindri.Tests/Golden/Api/projects/`: `empty-no-linear.json`, `empty-leads-nothing.json`, `syncing.json`, `mixed.json` (one led, one attached with a state override, one manual, one hidden, one finished 3 days ago, one finished 20 days ago that must not appear), `linear-failing.json`. The app's records in `Aw.Sindri.Ui/Api/Projects/` deserialize them through `SindriUiJsonContext` (`GoldenContractTests`).

`sindri projects [--json]` (new verb) prints the same answer: one line per card in the text form, the JSON as-is with `--json`. It's a query, so a session may run it.

## 7. The page (app)

- `PageId.Projects`, a tab labelled **Projects** between Home and Workflows, chord `G P` (free today, `ShellViewModel.Chords.cs:109-121`). The top bar holds seven tabs at 900 px (`NavEntry.cs:37`). An eighth doesn't fit, so §9 Q1 asks which way to go; the default moves **Recipes** into the System page's sub-nav, since it's the tab the owner opens least.
- `ShellViewModel.Projects.cs` with `InitProjects`, following `ShellViewModel.Jobs.cs:25-46`; it calls `Health.Follow(Page.Refresh)`, so any event re-reads the view (at most once per 250 ms, `sindri-ui.md:302-306`). `context.sources.synced` events (`EventCatalog.cs:17`) already pass through the same stream, so a finished sync refreshes the page.
- Cards use the existing `Border.card` style (`SindriApp.axaml:42-48`) in a wrapping grid, two columns at 900 px, three from 1,400 px. No new tokens. Health is a dot plus the word (never color alone): `Brush.Success`, `Brush.Attention`, `Brush.Error` (existing, `Theme/Tokens.axaml`).
- **Add project** (top right) opens a sheet with two tabs: *From Linear* (a search box over stored projects in scope that the owner doesn't lead and hasn't attached, no live call) and *By hand* (name, state, repos from the profile's repo list, links, note, target). The `⋯` menu on a card: Edit, Remove (attached and manual), Hide (led). Each is one catalog action.
- Every link opens in the browser (`ILauncher.OpenUri`, `Platform/ILauncher.cs`, as the map's node panel does at `NodePanelViewModel.cs:153`).
- Copy goes in a new `Labels.Projects.cs`.

## 8. Errors

| Code | When | Text |
|---|---|---|
| `SND-PROJECT-001` | Attaching a project already on the page | "That project is already on your page." |
| `SND-PROJECT-002` | A version mismatch on change, detach | "This project changed since you opened it. Reopen it and try again." |
| `SND-PROJECT-003` | `--linear` names a project not in the store | "Sindri doesn't sync that project. It syncs these Linear teams: ENG, OPS. Add the team to `context.sources.linear.teams`." |
| `SND-PROJECT-004` | A bad field: no name on a manual project, a repo not in the profile, a non-https link, a note over 200 characters, more than 10 links | names the field |

Registered in `docs/csharp/sindri.md`'s error table and the error-code test.

## 9. Questions for the owner (defaults in brackets)

1. **The top bar is full.** Seven tabs fit at 900 px and Projects would be the eighth. [Move Recipes into System's sub-nav and put Projects second.] Other options: Projects as a panel on Home instead of a tab; or drop the 900 px bar width to fit eight.
2. **Whose Linear key is it?** The page finds "projects I lead" by asking Linear who the key belongs to. If your key is a personal API key, that's you and nothing to set up. If it's a workspace or bot key, you'd set your Linear user id once in the profile. [Personal key, nothing to set.]
3. **Finished projects.** [Stay 14 days under a folded "Finished" row, then leave the page.]
4. **Linear's rate limit.** The project read adds up to about 1,150 calls a day on top of the issue sync. [Fine on a normal plan's 1,500 an hour; P0 reads your actual limit from Linear's response headers.]
5. **A hand-made project with a repo counts every open PR in that repo.** There's no branch or label to narrow it. [OK for v1, labelled "whole repo".]

## 10. Later (not in this spec)

- **Items in a project.** An item already links one tracker issue (`item.linked`, `ItemLinked.cs:10`). Once issues carry `in-project`, a card could show "2 items running" for free. That's a read-side join, no new event; it waits for the owner to ask.
- **Project updates as a timeline.** `ProjectUpdates.graphql` exists for live lookups. Storing updates as records would give the card a "last update: …" line. Left out to keep the feed one query.
- **Other trackers.** P2's hook.

## 11. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Packages | Depends on | Proof |
|---|---|---|---|---|
| **P0** | Run `ProjectsPull` and `ProjectOpenIssues` once with the owner's key, record the fields that came back, the complexity, the rate-limit headers; fix §4.1's query to match | none (a script, `scripts/probe-linear-projects.sh`, owner-run) | — | the recorded answer in the PR body |
| **P1** | Context data: migration 0020, `LedBy` link kind, `Health`/`DueAt`, `LinearProjectFeed`, `source_identity`, `in-project` and attachment links on issues, the open-issues pass, the version suffix and cursor reset | `Aw.Context.Core`, `.Infrastructure`, `.Features` | P0 | unit: feed mapping, state map, removal, the open-issues pass with a fake Linear; integration: a full tick on a temp store |
| **P2** | Context read: `ProjectFacts` | `Aw.Context.Core`, `.Infrastructure` | P1's migration (can start on the schema alone) | unit counts on a seeded store; integration plan check at 50,000 rows |
| **P3** | Sindri owner state: events, projection, five commands, catalog rows, CLI `project …` | `Aw.Sindri.Core`, `.Features`, `.Host` | — (parallel with P1) | unit per command; catalog golden; `SND-AUTH-002` for a session |
| **P4** | Sindri read: `ListProjects`, `GET /sindri/projects`, `sindri projects`, goldens | `Aw.Sindri.Features`, `.Host` | P2, P3 | API goldens (§6.2), integration |
| **P5** | The page | `Aw.Sindri.Ui` | P4's goldens (the UI builds against them first) | UI tests (nav, chords, top bar), `GoldenContractTests`, a screenshot run with the fixture |
| **P6** | Owner proof | — | P1–P5 | the owner's real workspace: their led projects appear with the counts Linear shows (±0 on open issues) |

**Parallel plan.** Three lanes from day one: P1→P2 (context), P3 (api lane, Sindri core), and P5's view against hand-written goldens (ui lane). P4 joins them. P0 is the owner's 5 minutes and gates only P1's query text; P1's mapping and tests start on today's `Projects.graphql` fields.

**Gates per phase.** Build `-warnaserror`; the touched projects' unit and integration lanes; `Aw.Architecture.Tests` for new folders and usings (P1, P3, P5); scoped format; P1 also `check-env-example.sh` (no new env var expected) and `check-profile-doc.mjs` (the new key); P3–P5 also `Aw.Sindri.Ui.Tests` (catalog rows and goldens); P4 the API goldens.

## 12. Proof

1. A fake Linear (the existing test double pattern, `FakeRecordFeed`) with three projects: one led by the viewer, one led by someone else, one completed 20 days ago; issues across them, some old and open, with GitHub attachments.
2. P6 on the owner's box: open the page, compare each led project's open issue count with Linear's project view filtered to open issues. They match exactly. Close one issue in Linear; the count drops within one sync tick (5 minutes).
3. With the network off, the page opens and shows the last counts with "as of".
