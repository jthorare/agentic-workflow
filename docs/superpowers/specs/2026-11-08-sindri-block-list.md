# Sindri v2: the owner's block list replaces "Block catalog v1"

**Owner:** joithor · **Date:** 2026-11-08 · **Amends:** #2258 (design.md "Block catalog v1", PR cards 6, 7, 9, 11, 12, 13, 16) · **Status:** draft for the owner's read

## Summary

The owner picked the block list the workflow picker shows. It replaces the v1 table in design.md. Five blocks are already
merged (#2336: `schedule`, `manual`, `condition`, `notify`, `prompt`) and keep their type names. Only their picker labels
change. There are no Slack-, Linear- or Figma-specific blocks: those apps come in through the generic connection trigger
and the connection tool block. Hacker News and OSV.dev become one provider each behind two generic data blocks: News and
Vulnerabilities.

## The list

| Group | Picker label | Type name | Where it comes from |
|---|---|---|---|
| Triggers | Schedule | `schedule` | merged (#2336) |
| Triggers | GitHub | `github` | new: a GitHub preset of the connection trigger (Q1) |
| Triggers | When called | `called` | replaces `manual` + `workflow_call` |
| Triggers | Connection event | `connection_event` | was `poll`; also takes over `inbox` (Q2) |
| Data | News | `news` | new, generic; Hacker News is the first provider |
| Data | Vulnerabilities | `vulnerabilities` | new, generic; OSV.dev is the first provider |
| Data | Meetings | `meetings` | new (Q3) |
| Data | Dad joke | `dad_joke` | new |
| Data | GitHub merged PRs | `github_merged_prs` | new |
| Data | Read conversation | `read_conversation` | new (Q4) |
| Data | Workflow result | `workflow_result` | new |
| Data | Recall | `memory_search` | v1, label only |
| Data | Toolkit tool | `call_tool` | v1, label only; also runs a connection's tools (Q5) |
| AI | Prompt | `prompt` | merged (#2336) |
| AI | Sandbox | `run_agent` | v1 `run_agent`, label only (Q6) |
| AI | Run on machine | `run_on_machine` | new (Q6) |
| AI | Jev | `jev` | new (Q7) |
| Logic | For each | `for_each` | v1 |
| Logic | Code | `code` | new (Q8) |
| Logic | Approval | `approval` | was `ask_owner` |
| Logic | Add to conversation | `add_to_conversation` | new (Q4) |
| Logic | Link thread | `link_thread` | new (Q4) |
| Logic | Run workflow | `run_workflow` | v1 |
| Logic | Remember | `memory_write` | v1, label only |
| Logic | Forget | `memory_forget` | v1, label only |
| Logic | Only new | `only_new` | v1 |
| Logic | If | `condition` | merged (#2336), label only |
| Logic | Collect | `collect` | v1 |
| Outputs | Send notification | `notify` | merged (#2336), label only |
| Outputs | Offer job | `offer_job` | replaces `create_item` (Q9) |

Every block type gets a `title` (the picker label) and a `group` on `IGraphBlockType`. `workflow_blocks` (PR 8) and the
canvas picker (PR 17) read both from there. A spec keeps the type name, so a renamed label never breaks a saved workflow.

## Old v1 -> new

| v1 block | Now | Why |
|---|---|---|
| `schedule` | Schedule, unchanged | merged |
| `manual` | folded into When called (`called`); `manual` stays as an alias the validator accepts | one trigger for a person, an agent or another workflow starting a run |
| `workflow_call` | folded into When called | same |
| `poll` | Connection event (`connection_event`) | same block, clearer name |
| `inbox` | a Connection event on an `inbox`-role connection (event kind `inbox.offer`) | not in the owner's list; PR 12's claim and submit stay (Q2) |
| `run_agent` | Sandbox, label only | |
| `prompt` | Prompt, unchanged | merged |
| `condition` | If, label only; keeps the `true` / `false` outputs | the screenshot's "Continue when" is `true` with `false` left unwired |
| `for_each`, `collect`, `only_new`, `run_workflow` | unchanged | |
| `wait` | **dropped** from v1 | not in the owner's list (Q10) |
| `http` | **dropped** as a block | raw requests go through a `rest` connection's tools in Toolkit tool (Q5) |
| `call_tool` | Toolkit tool, label only | |
| `memory_search` / `memory_write` / `memory_forget` | Recall / Remember / Forget, labels only | |
| `notify` | Send notification, label only | merged |
| `create_item` | **replaced** by Offer job | (Q9) |
| `ask_owner` | Approval | the run waits for the owner's yes / no |

The owner dropped three blocks tied to one workplace's tools (they belong in the private overlay, if anywhere). Eight are not made
because they are app-specific: Linear, Linear agent, Figma, Slack mention, Read Slack, Post to Slack, Update Linear issue
and Linear action. Each of those becomes a Connection event, Toolkit tool, Read conversation or Add to conversation on a
connection of that app.

## The new blocks

Config fields are the block's JSON schema. Outputs are the fields later nodes read as `{{node.field}}`.

- **GitHub** (`github`, trigger). Config: `connection` (a GitHub connection), `event` (`pr.opened`, `pr.merged`,
  `pr.review_requested`, `issue.labelled`, `push`), `repo`, `filter`. Outputs: the event payload, `deliveryId`. It is
  `connection_event` with the connection kind fixed to GitHub, so polling, cursors and catch-up are PR 11's.
- **When called** (`called`, trigger). Config: `inputs` (a JSON schema for what the caller passes). Outputs:
  `{{trigger.input.*}}`, `calledBy` (`owner`, `session:<id>` or `workflow:<id>@<run>`). `sindri wf run`, `workflow_run`
  over MCP and Run workflow all start it. The call-depth guard from PR 9 stays.
- **Connection event** (`connection_event`, trigger). PR 11's `poll` block under a new name: `connection`, `event` (a
  catalog id from `PollCatalog`), `filter`.
- **News** (`news`, data). Config: `provider` (`hackernews` default; `rss` takes any feed URL, which covers most news
  sites), `feed` (Hacker News: `top`, `new`, `best`, `ask`, `show`; RSS: a URL), `query?`, `limit` (default 30, max 200),
  `since?`. Outputs: `items[]` (`title`, `url`, `source`, `score?`, `comments?`, `publishedAt`, `id`), `count`.
  Providers are C# classes behind `INewsProvider`, so adding a news site is one class plus its host in the allowlist.
  Public reads need no connection.
- **Vulnerabilities** (`vulnerabilities`, data). Config: `provider` (`osv` default), and either `packages[]`
  (`ecosystem`, `name`, `version`) or `repo` (Sindri reads the repo's lockfiles: `packages.lock.json`, `package-lock.json`,
  `go.sum`, `Cargo.lock`, `poetry.lock`). Outputs: `vulns[]` (`id`, `aliases`, `summary`, `severity`, `package`,
  `version`, `fixedIn`, `url`), `count`. OSV's batch query API needs no key. Behind `IVulnerabilityProvider`.
- **Meetings** (`meetings`, data). Config: `connection` (a calendar connection: Google from D8), `day` (default today in
  the profile timezone), `calendar?`, `minMinutes?`. Outputs: `meetings[]` (`title`, `start`, `end`, `attendees`,
  `description`, `link`, `notes?`), `count`. What `notes` reads is Q3.
- **Dad joke** (`dad_joke`, data). No config. Outputs: `joke`, `id`. Reads `icanhazdadjoke.com`. It's the smallest real
  data block, so it doubles as the first-run example and the doc tests' network fixture.
- **GitHub merged PRs** (`github_merged_prs`, data). Config: `connection`, `repos[]`, `since` (default: this workflow's
  last successful run, else 24 h), `base?`, `limit` (default 50). Outputs: `prs[]` (`repo`, `number`, `title`, `author`,
  `url`, `mergedAt`, `body`, `labels`), `count`.
- **Read conversation** (`read_conversation`, data). Config: `connection`, `thread` (the connection's thread reference),
  `since?`, `limit`. Outputs: `messages[]` (`author`, `text`, `at`, `id`), `thread`. Message text is untrusted and fenced
  wherever it reaches a prompt. Q4 covers what a conversation is.
- **Workflow result** (`workflow_result`, data). Config: `workflow`, `run` (`latest-succeeded` default, or a run id).
  Outputs: that run's `outputs`, `runId`, `finishedAt`, `status`. It reads only and starts nothing; Run workflow starts one.
- **Run on machine** (`run_on_machine`, AI). Config: `agent` (`claude-code`, `codex`, `cursor`), `folder` (a profile
  repo or folder), `prompt`, `outputs`, `timeout`. The agent runs in the owner's own environment: no sandbox and no
  fresh worktree, with the owner's logins and tools. The report, outputs, attach and takeover work as in Sandbox. Q6 covers
  when to use which.
- **Jev** (`jev`, AI). Config: `connection` (a `rest` connection with a Jev preset), `model` (for example
  `jev-1.14.0`), `text`, `questions[]` (named yes / no or label questions), `longContext` (bool). Outputs: one field per
  question (for example `worth_sharing`), `model`, `usage`. Q7 covers how Sindri reaches it.
- **Code** (`code`, logic). Config: `input` (a reference, for example `{{jev}}`), `code` (JavaScript; the body gets
  `input` and returns the result), `repo?` and `branch?` (read-only files the code may read), `timeout` (default 5 s).
  Outputs: `result`. Q8 covers what runs it.
- **Approval** (`approval`, logic). Config: `question`, `choices?` (default approve / reject), `timeout`, `default`.
  Outputs: `answer`, `answeredBy`, `note?`. Branches: one output per choice, plus `timeout`. A Needs-you row of kind
  `Question` (PR 9). This is a stop the workflow's author put in one run. It is not the approval concept D1 removed
  (profile and workflow approvals).
- **Add to conversation** (`add_to_conversation`, logic). Config: `connection` (or `sindri` for an item's own thread),
  `thread` or `channel`, `text`. Outputs: `message` (the posted message's reference), `thread`.
- **Link thread** (`link_thread`, logic). Config: `thread` (a connection's thread reference), `to` (an item, a run or a
  memory fact). Outputs: `link`. Later runs find the thread through the item.
- **Offer job** (`offer_job`, output). Config: `title`, `repo`, `prompt`, `links?`, `agent?`, `expires?`. It makes an
  item and a Needs-you row with Start and Dismiss. Start launches the agent with the prompt in the repo, as a Sandbox run.
  Outputs: `item`, `offerId`. Q9 covers who the offer goes to.

## Where each lands (PR cards)

| Card | Blocks | Change to the card |
|---|---|---|
| PR 6 (#2336, merged) + engine's PR 6 part 2 (open) | Schedule, If, Send notification, Prompt; `manual` -> When called | part 2 adds `title` + `group` on `IGraphBlockType` and the label of each merged block; `called` replaces `manual` with `manual` kept as an alias |
| PR 7 | Sandbox, Run on machine | `run_on_machine` beside `run_agent` on the same launcher, without the sandbox wrap and worktree |
| PR 8 | (all) | `workflow_blocks` groups by `group` and shows `title`; one doc page per block in the list above |
| PR 9 | For each, Collect, Only new, Approval, Run workflow, Workflow result, Code, Offer job | drop `wait`, `http`, `create_item`; `ask_owner` -> `approval`; `workflow_call` -> `called` (PR 6 part 2) |
| PR 9b (new card) | News, Vulnerabilities, Dad joke, Jev | the public data providers (host allowlist, no sign-in) and Jev on a `rest` connection; can run beside PR 11 |
| PR 11 | Connection event, GitHub, GitHub merged PRs, Meetings | `poll` -> `connection_event`; the GitHub trigger preset; the two GitHub/calendar reads reuse the pollers' clients |
| PR 12 | Read conversation, Add to conversation, Link thread | `inbox` trigger -> `connection_event` on an inbox-role connection; the conversation port per connection kind |
| PR 13 (#2326) + its blocks follow-up | Recall, Remember, Forget | labels only; the blocks follow-up is the one context already named after PR 6 + 7 |
| PR 16 | Toolkit tool | `call_tool` also lists a connection's tools (MCP connections, `rest` presets' actions) |

## Open questions for the owner

1. **GitHub trigger:** its own block type, or a preset of Connection event that the picker shows as "GitHub"? I'd go
   with the preset: one poller, and nothing else to keep in step.
2. **Inbox:** it isn't in your list. Fold it into Connection event on an inbox-role connection (PR 12's claim and submit
   stay), or drop the handoff inbox from v2?
3. **Meetings:** calendar events only, or also the meeting's notes / transcript? From which app: one of the MCP note
   apps from D8? The roll-up in your screenshot classifies each meeting's text, so it needs more than a title.
4. **Conversations:** what is a "conversation" in Read conversation, Add to conversation and Link thread? I think it's a
   message thread on any connection that has threads (a Slack thread, a GitHub PR's comments, a Linear issue's comments),
   plus a Sindri item's own thread. Link thread ties an outside thread to an item so later runs can find it. Right?
5. **Raw HTTP:** dropping the `http` block means every outside call needs a connection or a toolkit. OK, or keep `http`
   hidden for agents only?
6. **Sandbox vs Run on machine:** Sandbox = agent in a fresh worktree inside the sandbox (today's `run_agent`). Run on
   machine = agent in your real environment, unsandboxed, with your logins. Is that the split you mean, or is Run on
   machine a shell command rather than an agent?
7. **Jev:** how does Sindri reach it: an HTTP API with a key (a `rest` connection preset), or a local binary? Where do
   its docs live, so the preset can ship in this repo?
8. **Code:** your screenshot shows JavaScript (`const j=input||{}`). Run it in an embedded interpreter inside Sindri
   (Jint: no process, no file or network access, a time and memory cap), or in Node inside the sandbox (full JS, slower to
   start)? I'd go with Jint. What do Repo and Branch mean there: files the code may read?
9. **Offer job:** to whom? My draft offers it to you as a Needs-you row with Start. The other reading: it posts an offer
   on an outside system's inbox, so another team or agent picks it up (the reverse of the handoff inbox).
10. **Wait:** it isn't in your list. Drop it, or keep it for "check again in 2 hours" flows?
11. **Dad joke:** a real block in the shipped list, or only an example in the docs?
