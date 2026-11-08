# Sindri v2: the owner's block list replaces "Block catalog v1"

**Owner:** joithor · **Date:** 2026-11-08 · **Amends:** #2258 (design.md "Block catalog v1", PR cards 6, 7, 9, 11, 12, 13, 16) · **Status:** approved by the owner, 2026-11-08

## Summary

The owner picked the block list the workflow picker shows. It replaces the v1 table in design.md. Five blocks are already
merged (#2336: `schedule`, `manual`, `condition`, `notify`, `prompt`) and keep their type names. Only their picker labels
change. There are no Slack-, Linear- or Figma-specific blocks: those apps come in through the generic connection trigger
and the connection tool block. There are no GitHub-specific blocks either: GitHub's events (PR opened, merged, review
requested, issue labelled, push) come in through Connection event on a GitHub connection, polled as PR 11 already plans
(D6 stands: no webhooks, a laptop can't receive them). Hacker News and OSV.dev become one provider each behind two
generic data blocks: News and Vulnerabilities.

The owner answered the open questions on 2026-11-08; their answers are folded into the blocks below. Code runs C#, not
JavaScript. HTTP call and Wait stay. Meetings reads two note-app connections. Jev is the same HTTP API the judge already
calls. No question is left, and the owner approved the list.

## The list

| Group | Picker label | Type name | Where it comes from |
|---|---|---|---|
| Triggers | Schedule | `schedule` | merged (#2336) |
| Triggers | When called | `called` | replaces `manual` + `workflow_call` |
| Triggers | Connection event | `connection_event` | was `poll`; also takes over `inbox` |
| Data | News | `news` | new, generic; Hacker News is the first provider |
| Data | Vulnerabilities | `vulnerabilities` | new, generic; OSV.dev is the first provider |
| Data | Meetings | `meetings` | new; Granola and Wispr Flow connections |
| Data | Read conversation | `read_conversation` | new |
| Data | Workflow result | `workflow_result` | new |
| Data | Recall | `memory_search` | v1, label only |
| Data | HTTP call | `http` | v1, built in C# |
| Data | Toolkit tool | `call_tool` | v1, label only; also runs a connection's tools |
| AI | Prompt | `prompt` | merged (#2336) |
| AI | Sandbox | `run_agent` | v1 `run_agent`, label only |
| AI | Run on machine | `run_on_machine` | new |
| AI | Jev | `jev` | new; the judge's Jev client |
| Logic | For each | `for_each` | v1 |
| Logic | Code | `code` | new, C# |
| Logic | Approval | `approval` | was `ask_owner` |
| Logic | Add to conversation | `add_to_conversation` | new |
| Logic | Link thread | `link_thread` | new |
| Logic | Run workflow | `run_workflow` | v1 |
| Logic | Remember | `memory_write` | v1, label only |
| Logic | Forget | `memory_forget` | v1, label only |
| Logic | Only new | `only_new` | v1 |
| Logic | If | `condition` | merged (#2336), label only |
| Logic | Collect | `collect` | v1 |
| Logic | Wait | `wait` | v1 |
| Outputs | Send notification | `notify` | merged (#2336), label only |
| Outputs | Offer job | `offer_job` | replaces `create_item` |

Every block type gets a `title` (the picker label) and a `group` on `IGraphBlockType`. `workflow_blocks` (PR 8) and the
canvas picker (PR 17) read both from there. A spec keeps the type name, so a renamed label never breaks a saved workflow.

## Old v1 -> new

| v1 block | Now | Why |
|---|---|---|
| `schedule` | Schedule, unchanged | merged |
| `manual` | folded into When called (`called`); `manual` stays as an alias the validator accepts | one trigger for a person, an agent or another workflow starting a run |
| `workflow_call` | folded into When called | same |
| `poll` | Connection event (`connection_event`) | same block, clearer name |
| `inbox` | a Connection event on an `inbox`-role connection (event kind `inbox.offer`) | owner: fold it in; PR 12's claim and submit stay |
| `run_agent` | Sandbox, label only | |
| `prompt` | Prompt, unchanged | merged |
| `condition` | If, label only; keeps the `true` / `false` outputs | the screenshot's "Continue when" is `true` with `false` left unwired |
| `for_each`, `collect`, `only_new`, `run_workflow`, `wait` | unchanged | owner kept `wait` for "check again in 2 hours" flows |
| `http` | HTTP call, built in C# | owner kept it; a `rest` connection's tools in Toolkit tool still work too |
| `call_tool` | Toolkit tool, label only | |
| `memory_search` / `memory_write` / `memory_forget` | Recall / Remember / Forget, labels only | |
| `notify` | Send notification, label only | merged |
| `create_item` | **replaced** by Offer job | an offer to the owner with Start / Dismiss |
| `ask_owner` | Approval | the run waits for the owner's yes / no |

The owner dropped three blocks tied to one workplace's tools (they belong in the private overlay, if anywhere), the
Dad joke block, and both GitHub blocks (GitHub trigger, GitHub merged PRs: Connection event covers GitHub). Eight are not made
because they are app-specific: Linear, Linear agent, Figma, Slack mention, Read Slack, Post to Slack, Update Linear issue
and Linear action. Each of those becomes a Connection event, Toolkit tool, Read conversation or Add to conversation on a
connection of that app.

## The new blocks

Config fields are the block's JSON schema. Outputs are the fields later nodes read as `{{node.field}}`.

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
- **Meetings** (`meetings`, data). Config: `connection` (a meeting-notes connection), `day` (default today in the
  profile timezone), `minMinutes?`, `include` (`notes` default; `transcript` adds the full text). Outputs: `meetings[]`
  (`title`, `start`, `end`, `attendees`, `notes`, `transcript?`, `link`, `source`), `count`. Two presets ship, both on
  the generic OAuth connections (PR 10; sign-in is PR 10b): **`granola`** (Granola) and **`wispr-flow`** (Wispr Flow),
  the two MCP-hosted note apps from D8. Each preset maps its app's meeting and notes data to the one shape above behind
  `IMeetingSource`, so a third note app is one class. Which API each app exposes (MCP tools or REST) is checked when the
  preset is built, with a probe like `scripts/probe-linear-projects.sh`. Notes and transcripts are untrusted and fenced
  wherever they reach a prompt.
- **Read conversation** (`read_conversation`, data). Config: `connection`, `thread` (the connection's thread reference),
  `since?`, `limit`. Outputs: `messages[]` (`author`, `text`, `at`, `id`), `thread`. Message text is untrusted and fenced
  wherever it reaches a prompt. A conversation is a message thread on any connection that has threads (a Slack thread, a
  GitHub PR's comments, a Linear issue's comments), plus a Sindri item's own thread.
- **HTTP call** (`http`, data). Built in C# on `HttpClient`. Config: `method`, `url`, `query?`, `headers?`, `body?`
  (JSON or text), `connection?` (signs the request with that connection's credential, so no secret sits in the spec),
  `timeout` (default 30 s), `expect?` (status codes that count as success, default 2xx). Outputs: `status`, `ok`,
  `headers`, `body` (parsed when the answer is JSON, else text, capped at 1 MB). A secret in `headers` or `url` is
  refused by the validator: credentials come only from a connection. The body is untrusted and fenced wherever it reaches
  a prompt.
- **Workflow result** (`workflow_result`, data). Config: `workflow`, `run` (`latest-succeeded` default, or a run id).
  Outputs: that run's `outputs`, `runId`, `finishedAt`, `status`. It reads only and starts nothing; Run workflow starts one.
- **Run on machine** (`run_on_machine`, AI). Config: `agent` (`claude-code`, `codex`, `cursor`), `folder` (a profile
  repo or folder), `prompt`, `outputs`, `timeout`. The agent runs in the owner's own environment: no sandbox and no
  fresh worktree, with the owner's logins and tools. The report, outputs, attach and takeover work as in Sandbox. Use
  Sandbox for work that should touch only a copy; Run on machine when the job needs the owner's real setup.
- **Jev** (`jev`, AI). The same HTTP API the judge calls today (docs/csharp/judge.md "Jev"): `POST
  https://api.typesafe.ai/v1/systemone` with `Authorization: Bearer <key>` and a body of `{state, model, questions}`.
  Config: `connection` (the `jev` preset, below), `model` (default: the judge's pinned model, today `jev-1.13.0`; the
  screenshot's `jev-1.14.0` is a valid value), `text` (the state; usually a reference such as `{{news.items}}`),
  `questions[]` (each a `name`, a `type` Jev takes: `choice`, `score` or `noul`, `instructions`, and `criteria`: for a
  choice, one criterion per answer, for example `yes` / `no`). Outputs: one field per question (for example
  `worth_sharing`) holding `choice`, `confidence` and `probabilities`, plus `model` and `usage` (`input_tokens`,
  `output_tokens`).
  The **`jev` connection preset** is a `rest` connection with that URL and the judge's key: `TYPESAFE_API_KEY` from the
  environment, else the keychain entry the judge reads. One key on the box serves both. The wire code is the judge's:
  `JevRequest`, `JevResponse`, the question records and the input cap move out of `Aw.Judge.Infrastructure` (all
  `internal` today) into a small shared package that the judge and Sindri both reference, the same move that made
  `Aw.Redaction` out of the scrubbing code both copied. The block redacts and caps what it sends as `JevProvider` does
  (`Aw.Redaction`, then 160,000 characters: Jev's 64k-token window), because Jev is a third party. Its errors read like
  the judge's reason codes: `no-api-key`, `network-error`, `http-NNN`, `timeout`.
- **Code** (`code`, logic). C#, not JavaScript. Config: `input` (a reference, for example `{{jev}}`), `code` (a C#
  method body: it gets `JsonNode input` and returns any value), `repo?` and `branch?` (the files the code may read, at
  that branch, read-only), `timeout` (default 5 s, max 60 s), `memoryMb` (default 256). Outputs: `result` (the returned
  value as JSON). Example:

  ```csharp
  var items = input["items"]!.AsArray();
  return items.Where(i => (int?)i["score"] > 100).Select(i => (string?)i["title"]).ToList();
  ```

  How it runs (proposal): Sindri wraps the body in a static method, compiles it with Roslyn (`CSharpCompilation`) against
  a fixed reference set (the BCL, `System.Text.Json`, `System.Linq`), and caches the assembly by the sha256 of the code
  and the reference set, so a second run doesn't compile again (a first compile takes about a second). The assembly runs
  in a small runner process (`sindri-code-runner`) inside the Sindri sandbox: no network, the repo mounted read-only,
  nothing else writable, a memory limit, and a kill at the timeout. The input goes in on stdin and the result comes back
  on stdout. It is a separate process because .NET has no in-process sandbox: an `AssemblyLoadContext` isolates types,
  not file or network access. The runner can't be NativeAOT (it loads a compiled assembly), so it is framework-dependent
  like the Sindri host. Compile errors come back as the node's error with line and column, and `sindri wf validate`
  compiles every Code node, so a typo fails at save time, not at 9:00.
- **Approval** (`approval`, logic). Config: `question`, `choices?` (default approve / reject), `timeout`, `default`.
  Outputs: `answer`, `answeredBy`, `note?`. Branches: one output per choice, plus `timeout`. A Needs-you row of kind
  `Question` (PR 9). This is a stop the workflow's author put in one run. It is not the approval concept D1 removed
  (profile and workflow approvals).
- **Add to conversation** (`add_to_conversation`, logic). Config: `connection` (or `sindri` for an item's own thread),
  `thread` or `channel`, `text`. Outputs: `message` (the posted message's reference), `thread`.
- **Link thread** (`link_thread`, logic). Config: `thread` (a connection's thread reference), `to` (an item, a run or a
  memory fact). Outputs: `link`. Later runs find the thread through the item.
- **Offer job** (`offer_job`, output). Config: `title`, `repo`, `prompt`, `links?`, `agent?`, `expires?`. It makes an
  item and a Needs-you row for the owner with Start and Dismiss. Start launches the agent with the prompt in the repo, as
  a Sandbox run. Outputs: `item`, `offerId`.
- **Wait** (`wait`, logic). Config: `for` (a duration, for example `2h`) or `until` (a time), max 7 days. Outputs:
  `waitedUntil`. A Temporal timer, as design.md already plans: the run shows `waiting`, a restart doesn't lose it, and a
  laptop that slept past the time fires it on wake.

## Where each lands (PR cards)

| Card | Blocks | Change to the card |
|---|---|---|
| PR 6 (#2336, merged) + engine's PR 6 part 2 (open) | Schedule, If, Send notification, Prompt; `manual` -> When called | part 2 adds `title` + `group` on `IGraphBlockType` and the label of each merged block; `called` replaces `manual` with `manual` kept as an alias |
| PR 7 | Sandbox, Run on machine | `run_on_machine` beside `run_agent` on the same launcher, without the sandbox wrap and worktree; folds in #1428 (Cursor can't run agent steps: its launcher entry has no policy or model form, SND-LAUNCH-013), so all three agents run in both blocks (owner) |
| PR 8 | (all) | `workflow_blocks` groups by `group` and shows `title`; one doc page per block in the list above |
| PR 9 | For each, Collect, Only new, Wait, HTTP call, Approval, Run workflow, Workflow result, Code, Offer job | drop `create_item`; `ask_owner` -> `approval`; `workflow_call` -> `called` (PR 6 part 2); Code brings the Roslyn compile, its cache and `sindri-code-runner` (big enough to split into its own PR 9c) |
| PR 9b (new card) | News, Vulnerabilities, Jev | the public data providers (host allowlist, no sign-in); Jev on the `jev` preset, with the judge's Jev wire code moved to a shared package (a new `deps.yaml` edge for Sindri); can run beside PR 11 |
| PR 11 | Connection event | `poll` -> `connection_event` (GitHub's events stay in its catalog) |
| After PR 10b (sign-in) | Meetings | the `granola` and `wispr-flow` presets and `IMeetingSource` |
| PR 12 | Read conversation, Add to conversation, Link thread | `inbox` trigger -> `connection_event` on an inbox-role connection; the conversation port per connection kind |
| PR 13 (#2326) + its blocks follow-up | Recall, Remember, Forget | labels only; the blocks follow-up is the one context already named after PR 6 + 7 |
| PR 16 | Toolkit tool | `call_tool` also lists a connection's tools (MCP connections, `rest` presets' actions) |

## Owner's answers (2026-11-08)

None open. Folded in above: Inbox (fold into Connection event), Meetings (Granola and Wispr Flow connections),
Conversations (as described), HTTP call (keep, in C#), Sandbox vs Run on machine (as described), Code (C#), Offer job (to
the owner), Wait (keep), Jev (the judge's HTTP API and key). The owner approved the list.
