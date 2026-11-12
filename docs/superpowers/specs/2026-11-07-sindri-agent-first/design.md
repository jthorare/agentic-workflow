# Sindri: agent-first workflows, connections, toolkits and memory (replaces recipes and approvals)

**Owner:** joithor · **Date:** 2026-11-07 · **Builders:** Sindri's agent team · **Kind:** system design, phased

## Summary

Sindri becomes a small, local workflow platform that an agent can drive end to end over MCP. A person describes an
automation in one sentence to an agent in Claude Code or Codex ("every morning, check whether the client library in
repo B still matches the API contracts in repo A, and open a PR if it drifted"), the agent builds it through Sindri's
MCP tools, test-runs it with the owner, and turns it on. Nobody has to open the app, though the app shows and edits the
same thing.

Five capabilities, built in order:

1. **No approvals, every verb for agents.** Profile approval, recipe approval and the agent-session refusals go away.
2. **Workflows as graphs of generic blocks**, built over MCP, run on the existing Temporal engine. Recipes and the
   factory pipeline are deleted.
3. **Connections**: OAuth APIs, MCP servers, REST APIs with a key and CLI tools, signed in once; polling triggers with a
   catch-up scan; a generic handoff inbox so outside systems can give Sindri work.
4. **Memory stores**: named, shared fact stores recalled by meaning, usable by workflows, agents and the owner's sessions.
5. **Toolkits and resources**: C# toolkits that add MCP tools, and per-workflow, per-block and per-toolkit databases,
   disks and sandbox sizes that Sindri provisions locally.

A sixth part, the canvas in the app, comes last.

## Why

- **Today's workflows don't work for everyday automation.** Sindri's recipe engine was built for one long pipeline
  (intake → scoping → design → build → prove → ship). Its blocks know their station (`step.scope.*`, `step.lens.round`,
  the line dispatcher), so they can't be reused, and building a small automation ("trigger → start an agent → report")
  means writing a new recipe from station-specific blocks plus C# for whatever is missing. The pipeline itself never
  became reliable. The owner's decision: drop it.
- **The work that keeps a team moving is small and repeatable**: tech-debt scans, dependency and contract drift checks,
  cleanup PRs, triage. Each one is a trigger, an agent with a scoped job, a condition and a notification. With generic
  blocks that is four nodes and zero new code.
- **Building must be agent-first.** The owner doesn't want to open an editor to build a workflow. An agent with good
  MCP tools and good docs should get from a sentence to a running workflow in minutes.
- **Approvals cost more than they protect here.** Sindri is single-owner and local. Every profile edit, recipe change and
  pin waits for `sindri profile approve <hash>` typed in a human terminal, and agent sessions are refused 89 call sites
  of `SND-AUTH-002` across 59 files. The owner's decision: trust the editor. The agents are good enough to test changes
  with the owner first.

## Goals

- G1. An agent builds, test-runs, activates, edits, pauses and debugs a workflow entirely over MCP.
- G2. One generic block catalog covers trigger → agent → condition → notify without new C#. New behaviour is a new block
  or a toolkit tool, never a new engine concept.
- G3. Any app the owner uses can be connected once (browser sign-in or a pasted key) and used by name from workflows,
  agents and toolkits. Tokens refresh on their own.
- G4. Events that happen while the laptop is closed are not lost: a catch-up scan runs on start and on wake.
- G5. Outside systems hand work to Sindri through one generic inbox contract.
- G6. Workflows, agents and the owner's sessions share named memory stores.
- G7. A workflow, block or toolkit declares the database, disk and sandbox size it needs, and Sindri provisions them on
  this box.
- G8. Everything the app can do, an agent can do over MCP or the CLI. The only human-only step is a provider's consent
  screen.

## Non-goals

- No cloud deployment, no multi-user server, no hosted relay. Sindri stays one process per box (the host lease).
- No public webhook endpoint and no tunnel. Inbound events are polled (owner decision, below).
- No workflow marketplace or sharing between people in this design.
- Project management (Work, Projects, Reviews, the Linear hub) is out of scope here and unchanged, except that its verbs
  stop refusing agents (Part 1).

## Anti-goals (a build that does these has failed even if it works)

- A workflow that needs a new C# block to do "start an agent with this prompt in this repo".
- Building that only works in the app.
- A connection whose token silently dies after an hour.
- A missed schedule that runs once per missed tick after a long sleep (three missed days = one run, not three).
- Any workplace-specific name, host, repo or tool in this repo. Workplace values live in the owner's private overlay
  (see "Generic core, private overlay").

## Owner decisions (2026-11-07)

| # | Question | Answer |
|---|---|---|
| D1 | Should profile and workflow approvals stay? | No. Drop the approval concept entirely, for the profile and for workflows. |
| D2 | What may agent sessions do? | Every verb in Sindri. No gate between agents and building or running workflows. |
| D3 | Keep recipes and the factory pipeline? | No. Remove recipes entirely; drop the pipeline. |
| D4 | How are workflows built? | Agent-first over MCP. The app is an option, never required. |
| D5 | What triggers exist? | Generic: schedule (cron), polling triggers on connections, manual, another workflow, the handoff inbox. |
| D6 | Inbound webhooks? | No. Catch-up only: Sindri polls each connection, and scans for what it missed when it starts or wakes. |
| D7 | How do outside systems hand work to Sindri? | Pull: Sindri polls an external inbox, claims work, turns it into an item. |
| D8 | Which connections? | GitHub, Linear, Slack, Google, two MCP-hosted note apps, Spotify, plus a generic kind for job tooling: MCP servers, REST APIs with a key, OAuth apps, and CLI tools. |
| D9 | Toolkits? | Yes, as a concept, and in C#. |
| D10 | Resources? | A workflow, a block or a toolkit can declare its own database, disk and sandbox size; Sindri provisions them. |
| D11 | Memory? | Yes, like a named fact store, shared across workflows and agents. |
| D12 | Agent management? | Agents make the whole product about 99% headless. |

## Generic core, private overlay

This repo stays generic. A workplace's workflows, inbox endpoints, CLI names and connection presets live in the owner's
private overlay folder (`$AW_SINDRI_HOME/overlay/`, or a private repo the profile points at), which Sindri loads like
any other source of workflow specs, connection presets and toolkits. Nothing in this design names a workplace.

---

## Part 1: no approvals, every verb for agents

### What changes

- **Profile edits apply on save.** `sindri profile set`, `repo add`, `context pin set` and the app's writes validate the
  new profile and write it atomically. A valid edit is live at once; an invalid one is refused with its `SND-PROFILE-*`
  finding and nothing is written. Remove `profile approve`, the approved-vs-live split (`ProfileDiff` drift states,
  "waiting for your approval" lines, the approval challenge, `setup.consent.given`'s approval gate), and the
  `ApproveProfile` setup step. One profile file, one hash for optimistic concurrency (`--if-hash`) only.
- **Agent sessions get every verb.** Remove the `SND-AUTH-002` refusals (89 call sites in 59 files under `src/`) and the
  human-only checks in `ProjectWrite.Begin`, the Linear writes, `index connect`, map builds, `onyx down`,
  `sandbox exec|login`, and the sandbox and model-limit verbs. Keep **attribution**: every write still records who did
  it (`By`: owner or `session:<id>`) on its ledger event, so the timeline shows what an agent changed.
- **What stays:** validation, optimistic concurrency, the host lease, the launch secret that ties a Step's report to its
  run (`SND-AUTH-003`: it proves *which run* is reporting, not *who may act*), and the sandbox.

### Done means

- `sindri profile approve` is gone (the verb answers "removed: edits apply on save").
- An agent session can run every verb that a human terminal can, proven by the API golden suite run as a session.
- `sindri doctor` has no approval rows. The Context and Setup pages have no "waiting for your approval" states.

---

## Part 2: workflows as graphs of generic blocks

### The spec

A workflow is a JSON document, versioned, stored by Sindri (not a file the owner edits by hand, though export and import
exist):

```json
{
  "version": 1,
  "name": "Contract drift check",
  "description": "Keeps repo B's client in line with repo A's API contracts",
  "concurrency": "one-per-workflow",
  "resources": { "database": { "name": "state" }, "sandbox": { "memory": "4g", "wall": "45m" } },
  "nodes": [
    { "id": "daily", "type": "schedule", "config": { "cron": "0 7 * * 1-5", "timezone": "America/Los_Angeles" } },
    { "id": "recall", "type": "memory_search", "config": { "memory": "drift-check", "query": "last checked commit and open PR" } },
    { "id": "agent", "type": "run_agent", "config": {
        "repo": "client-repo", "readOnlyRepos": ["api-repo"],
        "agent": "claude-code",
        "prompt": "Compare the API contracts in api-repo with the client in client-repo. Context: {{recall.facts}}. If they drifted, update the client, run its tests and open one PR. Otherwise report no drift.",
        "outputs": { "drifted": "boolean", "prUrl": "string?", "checkedCommit": "string" },
        "timeout": "45m" } },
    { "id": "remember", "type": "memory_write", "config": { "memory": "drift-check", "fact": "Checked api-repo at {{agent.checkedCommit}}; PR {{agent.prUrl}}" } },
    { "id": "if", "type": "condition", "config": { "when": "{{agent.drifted}}" } },
    { "id": "tell", "type": "notify", "config": { "to": "owner", "title": "Client drifted", "body": "PR: {{agent.prUrl}}" } }
  ],
  "edges": [
    { "from": "daily", "to": "recall" }, { "from": "recall", "to": "agent" },
    { "from": "agent", "to": "remember" }, { "from": "remember", "to": "if" },
    { "from": "if", "output": "true", "to": "tell" }
  ]
}
```

- **Nodes and edges.** A node is `{ id, type, title?, config }`. An edge is `{ from, output?, to }`; `output` defaults to
  `out`, and branching blocks name theirs (`condition`: `true` / `false`).
- **References.** Config strings interpolate `{{node.field}}`, `{{trigger.*}}`, `{{item}}` (inside a for-each) and
  `{{workflow.*}}`. A string that is exactly one reference keeps the referenced value's type.
- **One trigger.** A trigger is a block with no inbound edge. A workflow has exactly one (`called` runs need none).
- **Revisions.** Every save is an immutable revision with its hash. `activate` pins the revision that triggers run;
  `called` runs use the latest. Editing never changes a run in flight.

### Validation (on save and again at run start)

No trigger or more than one; an unconnected node; a cycle; a reference to a node that isn't upstream; an unknown block
type or a config that fails the block's schema; an invalid cron or timezone; a `run_agent` whose repo isn't in the
profile; a connection or memory name that doesn't exist. Drafts may save with wiring gaps; `activate` refuses any
finding. Every finding names the node and the field, and says how to fix it, in the repo's existing error style.

### Block catalog

The owner approved the block list on 2026-11-08. The per-block config, outputs and run notes live in
`2026-11-08-sindri-block-list.md`; this table is the picker's list (`title` and `group` come from `IGraphBlockType`).
Labels can change; the type name in a saved spec never does. `manual` stays as an alias of `called`.

| Group | Picker label (type name) |
|---|---|
| Triggers | Schedule (`schedule`), When called (`called`), Connection event (`connection_event`) |
| Data | News (`news`), Vulnerabilities (`vulnerabilities`), Meetings (`meetings`), Read conversation (`read_conversation`), Workflow result (`workflow_result`), Recall (`memory_search`), HTTP call (`http`), Toolkit tool (`call_tool`) |
| AI | Prompt (`prompt`), Sandbox (`run_agent`), Run on machine (`run_on_machine`), Jev (`jev`) |
| Logic | For each (`for_each`), Code (`code`, C#), Approval (`approval`), Add to conversation (`add_to_conversation`), Link thread (`link_thread`), Run workflow (`run_workflow`), Remember (`memory_write`), Forget (`memory_forget`), Only new (`only_new`), If (`condition`), Collect (`collect`), Wait (`wait`) |
| Outputs | Send notification (`notify`), Offer job (`offer_job`) |

New blocks are added the way blocks are today: a typed C# module with its contract and tests. The test for G2 is the
example above: it uses no block outside this table.

### `run_agent`, the block that matters most

It reuses what Sindri already has for launching agents (the agent runtime, P8 in `docs/csharp/sindri-workflow.md`):

- **Launch.** A session of `claude-code`, `codex` or `cursor` in a fresh worktree of `repo` on a branch named from the
  workflow and run (`wf/<slug>/<run>`), in a tmux session Sindri owns (`snd-<run>`), inside the sandbox (bubblewrap on
  Linux, Seatbelt on macOS) sized by the block's or workflow's `sandbox` resource. `readOnlyRepos` are mounted read
  only. Connections the block names are passed in (Part 3); nothing else is.
- **Report.** The agent reports through `workflow complete` with the launch secret, as today, and its `outputs` must
  match the block's declared output schema. The recovery from `.sindri-out` and the restart waits stay as they are.
- **Attach and take over.** The owner can attach to the live session from the app or `sindri open --attach`, and take it
  over; the run's timer pauses until handback. This is the one feature carried over from the old engine's human-in-the-loop.
- **Outputs.** The declared fields plus `status`, `branch`, `commit`, `sessionId`, `usage` (tokens, cost).
- **Failure.** A session that ends without a report, goes idle, or exceeds its timeout fails the node with the reason.
  `onFailure: stop | continue` on the node picks what happens next (default `stop`).

### The engine

- **Temporal stays.** One new workflow type, `GraphWorkflow`, interprets every spec: topological order (ties broken by
  spec order), one activity per block execution, `for_each` as child iterations with an `itemIndex`. Parking (an agent
  (an agent run, `approval`, `wait`) is a Temporal wait, not a polling sweep, so replay, timers and cancellation come from
  Temporal. Every node start, finish and failure is mirrored into the ledger, which stays the one public log.
- **Concurrency per workflow:** `one-per-workflow` (default; a trigger while a run is in flight is skipped and logged),
  `queue` (runs wait their turn), `unlimited`.
- **Run statuses:** `queued`, `running`, `waiting`, `succeeded`, `failed`, `cancelled`. Node statuses: `running`,
  `waiting`, `succeeded`, `failed`, `skipped`. A failure halts the walk unless the node says `continue`; nodes that never
  ran are `skipped`.
- **Idempotent triggers.** A run's id is `<workflow>@<revision>:<occurrence>` (the cron tick time, the connection event's
  delivery id, the inbox claim id, or a fresh id for a `called` run). A second start with the same id is refused, so a
  re-delivered event or a catch-up overlap never runs twice.
- **Missed schedules.** On start or wake, a schedule whose ticks passed while Sindri was down runs **once**, for the
  latest missed tick, then resumes its cadence.

### Building over MCP (G1, G8)

New tools on `/sindri/mcp` (and matching CLI verbs under `sindri wf ...`):

| Tool | Does |
|---|---|
| `workflow_docs` | Searches Sindri's workflow docs and block catalog by question or pattern (the docs ship in the binary) |
| `workflow_blocks` | Lists block types with their config schema, outputs and an example |
| `workflow_list`, `workflow_get` | Lists workflows; gets one with its latest and active revisions |
| `workflow_create` | Creates a draft from a spec or from a starter template |
| `workflow_apply_ops` | Applies small edits (add node, set config, connect, remove) against a `baseRevision`; a stale base is a conflict to re-read and retry |
| `workflow_replace` | Replaces the whole spec against a `baseRevision` |
| `workflow_validate` | Returns every finding without saving |
| `workflow_run` | Runs the latest revision now, with an optional trigger payload (test runs) |
| `workflow_activate`, `workflow_pause` | Pins a revision for its trigger; unpins |
| `workflow_runs`, `workflow_run_get` | Lists runs; gets one with every node's input, output, status and error |
| `workflow_cancel` | Cancels a run and stops its agent sessions |
| `workflow_delete` | Archives; its resources are kept (Part 5) |

Positions for the canvas are computed by Sindri, so an agent never sends layout. The docs the tools serve are written
for an agent that will never open the app: every block page has a complete example.

### Removal of recipes and the factory pipeline (D3)

Delete the recipe format, registry, approval, evals and `RecipeWorkflow`; the line dispatcher, the factory stations
(scoping, design, plan, build, prove, review, ship, PR babysitter) and their station-specific blocks; the `recipe *`,
`scoping *`, `docs check` and line-only `workflow *` verbs; their app pages (Workflow types / Recipes, recipe stats,
the instance frame's recipe parts); `schemas/recipes`, `schemas/scoping`, `schemas/line` where nothing else reads them;
and their tests, goldens and docs. Keep and reuse: the item model, the ledger, the launcher and agent runtime, the
sandbox, takeover and handback, the context engine, the code index, Work / Projects / Reviews and the Linear hub.
Instances in flight at upgrade are cancelled with a ledger note; there is no migration.

### Data (SQLite, Sindri's state)

| Table | Key columns |
|---|---|
| `workflow` | `id`, `slug` (unique), `name`, `active_revision` (null = off), `archived_at`, `created_by` |
| `workflow_revision` | `workflow_id`, `revision`, `spec` (JSON), `spec_hash`, `created_by`, `created_at` |
| `workflow_run` | `id` (`<workflow>@<revision>:<occurrence>`), `workflow_id`, `revision`, `trigger`, `status`, `outputs`, `error`, `item_id?`, timings |
| `workflow_node_run` | `run_id`, `node_id`, `item_index`, `status`, `input`, `output` (capped, with a `truncated` flag), `error`, `usage` |

`created_by` is `owner` or `session:<id>`.

### Done means (the Part 2 acceptance test)

An agent in Claude Code, given only the MCP tools and the sentence "every weekday at 7, check whether repo B's client
matches repo A's API contracts and open a PR if it drifted", creates the workflow, test-runs it once with `workflow_run`
(one no-drift run and one drift run on a fixture pair of repos), activates it, and the next scheduled tick opens a PR.
No C#, no app.

---

## Part 3: connections, polling triggers, the handoff inbox

### Connection kinds

| Kind | Holds | Sign-in |
|---|---|---|
| `oauth` | provider, client id (owner's own app registration), scopes, access and refresh tokens | Sindri opens the browser on the provider's consent page with PKCE and a `127.0.0.1:<port>` loopback redirect; the callback stores the tokens. A provider that refuses a plain-http loopback redirect is noted in its preset, and its preset says how to sign in instead |
| `mcp` | server URL, auth (OAuth with dynamic client registration when the server offers it, or a key) | Same browser flow, or paste a key |
| `rest` | base URL, header name, key | Paste the key, or an agent sets it |
| `cli` | binary, how it logs in (`env:NAME` key, a config file, or its own `login` command), allowed subcommands (optional) | Paste a key, or run its login once |

- **Presets** ship for GitHub, Linear, Slack, Google and Spotify (endpoints, default scopes, which events can be polled),
  and the owner's overlay can add more. Two MCP-hosted apps the owner uses are added from the overlay as `mcp`
  connections.
- **Storage.** Secrets live in the OS keychain (service `sindri-connection`, account `<name>`), or an owner-only file
  under `$AW_SINDRI_HOME/secrets/` where there is no keychain, as `sindri index connect` does today. The connection row
  holds only the pointer. The profile's existing secret pointers (`env:`, `file:`, `keychain:`, `op:`) are accepted.
- **Refresh.** Access tokens are refreshed before expiry by the scheduler; a refresh that fails marks the connection
  `needs-sign-in`, adds a Needs-you row and fails the nodes that use it with that reason. Never a silent death.
- **Use by name.** A workflow, block, agent or toolkit asks for `linear:work`, never a token. `run_agent` passes only the
  connections its node names: an `oauth`/`rest` token as an env var inside the sandbox, a `cli` binary on `PATH` with
  its credential, an `mcp` server added to the agent's MCP config for that session.
- **MCP tools:** `connection_list`, `connection_get` (never returns a secret), `connection_add` (starts the browser
  sign-in or stores a pasted key), `connection_test`, `connection_remove`.

### Polling triggers and the catch-up scan (D6)

- Each `connection_event` trigger names a connection and an event filter. Sindri keeps a **cursor** per (connection, event kind):
  the last `updatedAt`, event id or page token.
- While running, a scheduler service polls each connection that has an active `connection_event` trigger every 2 minutes (per
  connection, configurable, with the provider's rate limits respected and backoff on 429).
- **Catch-up:** on control-plane start and on system wake, every cursor is read forward to now before the regular cadence
  resumes, and missed schedules run once (Part 2).
- Every event carries a **delivery id**; a run's occurrence is that id, so a catch-up that overlaps a regular poll starts
  no second run.

### The handoff inbox (D7, G5)

A generic contract so any outside system can hand work to Sindri without reaching the laptop:

- An inbox-role `rest` connection points at an HTTP API that implements three calls: **list offers** for this owner, **claim** one
  (an atomic claim; a lost race returns 409 and nothing starts), **submit** a result (markdown and links).
- Sindri polls the inbox like any connection. A claimed offer becomes a Sindri **item** with the offer's payload (title,
  repo, branch, prompt, links) and starts any workflow whose trigger is a Connection event on that inbox-role connection (event kind `inbox.offer`) with a matching filter. When the
  workflow finishes, its `outputs` are submitted back.
- A **local endpoint** `POST 127.0.0.1:<port>/sindri/handoff` (with the per-start token) lets apps and scripts on the
  same machine create the same kind of item directly.
- The contract is documented as a small OpenAPI file in the repo, so a workplace system can implement it.

### Done means

The owner signs in to GitHub, Linear and Slack from `sindri connection add` (or an agent starts it) and each shows
`connected`. A Linear `connection_event` trigger fires within 2 minutes of a label change while awake. After the laptop sleeps
through an event, the run starts within a minute of wake, exactly once. A fixture inbox's offer becomes an item, runs a
workflow and gets its result submitted.

---

## Part 4: memory stores

- **A memory** is a named store of facts, created by an agent, a workflow or the owner. A **fact** is short text with
  tags, an optional expiry, and where it came from (workflow, run, item, session).
- **Embeddings** use the local embedding model Sindri already pins for the code index (`context/code-index.lock`,
  `nomic-embed-text` through Ollama). Recall ranks by cosine similarity with three match levels (loose, balanced,
  strict); without the model, recall falls back to keyword ranking and says so.
- **Writes deduplicate:** a fact similar to an existing one with the same tags refreshes it instead of adding
  another (the threshold is a constant with a test, calibrated on a fixture set).
- **Blocks:** `memory_search`, `memory_write`, `memory_forget`, and an optional `memory` field on `prompt` and
  `run_agent` that recalls before the call and lets the agent write facts through its MCP tools.
- **MCP tools:** `memory_list`, `memory_create`, `memory_search`, `memory_write`, `memory_forget`. A session the owner
  opens can read and write the same stores.
- **Data:** `memory(id, name, description, created_by)`, `memory_fact(id, memory_id, text, embedding, tags,
  workflow_id?, run_id?, item_id?, expires_at?, created_at, refreshed_at)`, in Sindri's SQLite, exact search (no ANN
  index until a store is large enough to need one).

---

## Part 5: toolkits and resources

### Toolkits (C#, D9)

- **A toolkit** is a C# project under `$AW_SINDRI_HOME/toolkits/<name>/` (or the overlay) that adds tools. It references
  a new package, `Aw.Sindri.Toolkit` (the SDK), and declares itself:

  ```csharp
  public static class ContractTools
  {
      public static Toolkit Define() => Toolkit.Named("contracts")
          .Needs(Resources.Database("cache"), Resources.Disk("1g"), Resources.Connection("github:me"))
          .Tool<DiffContractsRequest, DiffContractsResponse>("diff_contracts", DiffContracts.Handle);
  }
  ```

  Tools are typed request and response records with source-generated JSON; the input schema comes from the request type.
- **Scaffold:** `/new-toolkit` (like `/new-package` and `/new-slice`) makes the project, a test project and one sample
  tool. A toolkit builds with `-warnaserror` and passes its tests before Sindri starts it.
- **Running:** Sindri builds it (NativeAOT where possible), runs it as a local process in the sandbox with its declared
  resources and connections, and talks to it over MCP on stdio through the official MCP C# SDK. Its tools appear on
  `/sindri/mcp` as `<toolkit>.<tool>` and in workflows through `call_tool`.
- **Control:** every call is logged (`toolkit_call`: tool, caller, input and output capped, duration, error); a toolkit
  can be paused or switched off. MCP tools: `toolkit_list`, `toolkit_create` (scaffold), `toolkit_build`,
  `toolkit_start`, `toolkit_stop`, `toolkit_calls`.

### Resources (D10)

- Declared by a workflow (`resources` in the spec), a block (`resources` on a node), or a toolkit (`Needs(...)`):
  `database { name }`, `disk { size }`, `sandbox { cpu?, memory, wall }`, `connection { name }`.
- **Provisioning is local and automatic** on `activate` (workflow) or `start` (toolkit): a SQLite file per database at
  `$AW_SINDRI_HOME/resources/<owner-kind>/<slug>/<name>.db`, a folder per disk with its size checked before each run (an
  over-size disk fails the run with the numbers), sandbox limits applied to the session (capped by the box's own limits
  from the sandbox settings). The path of each resource is passed to the block, agent or toolkit by name.
- **Removal keeps data.** Archiving a workflow or deleting a toolkit keeps its databases and disks; `sindri resources
  purge <owner>` (or the MCP tool) deletes them.
- **MCP tools:** `resources_list` (each with size and owner), `resources_purge`.

---

## Part 6: the canvas in the app

The Avalonia app gets a Workflows page that shows each workflow as a graph (layout computed by Sindri), its runs with
every node's input and output, and live agent sessions with Attach. Editing in the app uses the same operations as
`workflow_apply_ops`. Connections, memory stores, toolkits and resources each get a list page with the same actions the
MCP tools have. The existing Workflows / Workflow types pages are replaced.

---

## Phases (each one or more PRs, each proven before the next)

| Phase | Lands | Done means |
|---|---|---|
| P1 | Drop approvals; open every verb to sessions (Part 1) | No `profile approve`; the API goldens pass as a session; doctor and pages have no approval states |
| P2 | Remove recipes and the factory pipeline (Part 2, "Removal") | The solution builds and tests green without them; docs and `AGENTS.md` updated |
| P3 | Graph spec, validator, `GraphWorkflow`, run store, blocks `schedule`, `called` (`manual` kept as an alias), `run_agent`, `condition`, `notify`, `prompt` | A spec run end to end by CLI on a fixture repo |
| P4 | MCP build tools and `workflow_docs` | Part 2's acceptance test |
| P5 | Remaining blocks (`for_each`, `collect`, `only_new`, `wait`, `approval`, `offer_job`, `run_workflow`, `workflow_result`, `http`; PR 9) | Each block's example spec runs |
| P5b | `code` (C# through Roslyn and `sindri-code-runner`; PR 9c) and the public data blocks `news`, `vulnerabilities`, `jev` (PR 9b) | Each block's example spec runs; `sindri wf validate` compiles Code nodes |
| P6 | Connections (`oauth`, `mcp`, `rest`, `cli`), presets, refresh | Part 3's sign-in checks |
| P7 | `connection_event` triggers, cursors, catch-up on start and wake | Part 3's poll and wake checks |
| P8 | Handoff inbox contract, `connection_event` on an inbox-role connection, local endpoint, conversation blocks (`read_conversation`, `add_to_conversation`, `link_thread`) | Part 3's inbox check |
| P8b | `meetings` block with the `granola` and `wispr-flow` presets (PR 10d, after PR 10b sign-in) | A fixture meeting list reaches a prompt, fenced |
| P9 | Memory stores, blocks and tools | A workflow remembers a fact and a later run recalls it; a session reads it |
| P10 | Resources (database, disk, sandbox) for workflows and blocks | The example spec gets its `state` database and sandbox size |
| P11 | Toolkit SDK, `/new-toolkit`, toolkit runtime and controls, `call_tool` | An agent scaffolds a toolkit, it builds, its tool is called from a workflow |
| P12 | The canvas and list pages in the app | Every page shows what the MCP tools return and can do the same actions |

P1 and P2 can run in parallel. P3 to P5 are the critical path to the first useful workflow.

## Constraints for the builders

- Follow the repo's rules: the C# slice recipe, `deps.yaml` for every new edge, lanes and budgets for tests, goldens for
  the API, `.env.example` for new variables, the bash suites for hooks and scripts.
- New error codes get their own prefix per area (for example `SND-GRAPH-*`, `SND-CONN-*`, `SND-MEM-*`, `SND-TK-*`,
  `SND-RES-*`), each with a message that says what to do.
- Secrets never reach the ledger, logs, MCP results or test output. `Aw.Redaction` covers every new path.
- Anything that reads a connection's data into a model prompt marks it as untrusted text, as `/sindri/mcp` does today.
- macOS and Linux are both supported; a feature that can't work on one says so in words (as the sandbox does).

## Risks and open questions

1. **Agents with every verb and live credentials (owner-accepted).** A badly prompted agent can build a workflow that
   acts with the owner's connections on a schedule. The owner accepts this (D2). Mitigations that cost nothing: the
   ledger records which session created or changed each workflow; `workflow_pause` and `connection_remove` are one call;
   the app shows "changed by session X" on each workflow.
2. **OAuth providers and loopback redirects.** Some providers refuse a plain-http loopback redirect or require a
   registered HTTPS URL. Each preset must be checked against the provider's current rules in P6; the fallback is a
   pasted token for that provider.
3. **Polling cost and rate limits.** Many `connection_event` triggers on one connection should share one poll per event kind. P7
   designs that sharing.
4. **Temporal and large graphs.** `for_each` over hundreds of items should use child workflows or batching so history
   stays under Temporal's limits. P5 measures it.
5. **Removing the pipeline removes working pieces some pages use** (scoping views, line facts on items). P2 lists every
   page and doc that reads them before deleting.

## For the builder: what's yours to decide

Table names and column types beyond the keys above; the exact MCP tool names (keep them short and consistent); the
starter templates; the poll interval default; the dedupe threshold for memory; how `for_each` maps onto Temporal; the
canvas layout algorithm. Write each decision into the docs as you make it.

## Settled before building (author's calls, from a builder read of this design)

These follow from D1 to D12. The owner can overturn any of them on this issue.

1. **Network access for `run_agent`.** A connection preset carries its host group. Naming a connection on a node opens
   that provider's hosts in the sandbox. A node with a `repo` gets the `github` group, so it can push and open a PR.
   Host lists live in the profile and apply on save (Part 1), so no approval is needed.
2. **The launcher and agent runtime are kept and rewired, not rewritten.** Kept: the launcher, launch ids, the launch
   secret and `SND-AUTH-003`, the `complete` report (as `sindri wf complete`), `.sindri-out` recovery, the watchdog,
   takeover and handback. A graph node run is what a Step was to them: one node run is one launch. Deleted: every other
   `workflow *` verb that only drives recipe instances.
3. **Ledger and items.**
   - The ledger stays append-only. Old `workflow.*` and line events stay readable as history, and the projector folds
     unknown or retired kinds as timeline entries only.
   - An item keeps a plain lifecycle (open, closed, cancelled) plus its links (Linear issue, PRs, runs).
   - Work, Projects, Reviews and the Linear hub stay. Their reads that came from line facts switch to item links and
     graph runs.
   - The PR babysitter and docs-sync recipes go with the pipeline. Either can come back later as a graph workflow.
4. **Conditions and interpolation.**
   - `condition.when` takes a small expression language: references, literals, `== != < <= > >=`, `and or not`,
     `exists(x)` and truthiness.
   - Interpolating an object or array into a string renders compact JSON. A null renders as an empty string.
   - A field that is exactly one reference keeps its type. The validator type-checks references against the upstream
     block's declared outputs.
5. **Sessions launched by a workflow get Sindri's MCP with every tool (D2).** Loop guards:
   - a run can't start its own workflow;
   - `run_workflow` and `called` (when started by another workflow) stop at depth 3;
   - every write a launched session makes is attributed to `session:<id>` and to its run.
6. **Connection events in v1.**
   - GitHub: PR opened, merged or review requested; issue opened or labelled; push to a branch.
   - Linear: issue created, state changed or labelled; comment added.
   - Slack: a message in a named channel that matches a filter; a mention of the owner.
   - Google: a calendar event starting within N minutes.
   - Spotify: no events (blocks only).
   - P7's done-check uses one GitHub and one Linear event.
7. **OAuth testing.** Tests run against a fake OAuth server (authorize, token, refresh, revoke) in the integration lane.
   The real sign-in to each provider is the owner's manual check at the end of P6. Client ids are stored in the connection
   preset (repo presets ship without them; the owner's overlay or `connection add --client-id` supplies them). Client
   secrets go in the keychain like tokens.
8. **The `prompt` block reuses the context engine's model routing** (`ModelRouting`: harness and model per route, with
   fallback) under a new `workflowPrompt` route. Billing and credentials are the owner's agent CLI logins, as for every
   other model call Sindri makes.
9. **`approval` uses the Needs-you queue and `sindri answer`.** Any caller may answer (D2), and the answer is attributed.
   An agent answering its own run's question is allowed and shows as such on the timeline.
10. **Platform for acceptance.** P3 and P4 are proven on macOS (the owner's box, Seatbelt) and on Linux in CI
    (bubblewrap). If the macOS sandbox can't host `run_agent` yet, closing that gap is part of P3. `AGENTS.md`'s
    "sandbox: not built yet" line is stale and gets fixed in P1.

## Corrections from the code read (PR card pass)

Reading the code for every PR turned up these. They change the PR plan, so they take precedence over the sections above:

1. **Most of the 89 `SND-AUTH-002` sites are not gates.** 44 are `"<Service> needs a caller"` null-caller guards. They
   stay, because they carry attribution. The real gates are the `Authorization.cs:21-31` table (which narrows launched
   sessions), the `NeedsTerminal` rows, and about a dozen per-service checks. PR2 removes only those.
2. **The launcher's types live in the engine.** `LaunchId`, `RunToken` and `InstanceAddress` are in
   `Core.Workflow.Engine` and are used by 193, 38 and 139 files. **PR3 splits in two:** 3a moves the shared types into
   `Launch` with no behaviour change, and 3b does the delete. Order: PR1 and PR2, then PR3a, then PR3b with PR4, and PRs
   4 to 7 merge close together, because PR4 stubs readers (`SweepLostSessions`, `GetLaunchPicks`,
   `ViewLinearItemLinks`) that PR7 brings back.
3. **Approval reaches 117 files through `IApprovedProfile`.** PR1 first makes it return the live profile. The rename is a
   follow-up. Five bash scripts that call `profile approve` change in PR1.
4. **Run ids can't be session or branch names** (they contain `:` and `@`). The launch id (`LCH-…`) stays the key for
   tmux sessions, and branches are `wf/<slug>/<short-run>`.
5. **The sandbox assumes an agent launch.** `SandboxRequest` needs a launch id, an item, an agent and a Step policy.
   **PR16 splits:** 16a adds a process variant of the sandbox request, and 16b adds the toolkit runtime, MCP exposure,
   call log and `call_tool`. Check that MCP clients accept dotted tool names (`<toolkit>.<tool>`) before choosing that
   form.
6. **Toolkits build outside the repo's rails** (`Directory.Build.props`, `nuget.config`, `global.json`). PR15 uses a
   local SDK feed at `$AW_SINDRI_HOME/sdk/packages`.
7. **No cron or wake code exists.** `Cadence` is a bare interval. PR5 adds a cron reader. PR11 detects wake from a
   wall-clock versus monotonic-clock gap. The existing scheduler already skips missed slots, and PR6 reuses that for
   "missed schedule runs once".
8. **`Aw.Sindri.Features` is at its 4-project-reference limit,** so the embedding and connector adapters live in Host
   (PR13, PR10).
9. **Inbox is a role on a `rest` connection,** not a fifth connection kind (PR12).
10. **Over MCP today, only `sindri_request` and `sindri_job` write.** "Agents get every verb" means new write tools in
    PRs 8, 10, 13 and 16, not just removing refusals.
