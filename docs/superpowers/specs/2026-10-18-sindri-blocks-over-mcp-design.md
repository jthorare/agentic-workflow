# Sindri blocks over MCP: two generic tools, `sindri_list_tools` and `sindri_execute_tool_call`

**Status:** design, approved (owner, 2026-10-18; every choice answered in §C) · **Date:** 2026-10-18 · **Owner decisions:** 2026-10-17
(the two-tool shape, below), 2026-10-18 (sessions outside a Step may call blocks, C3) · **Amends:** the API surface spec `2026-10-10-sindri-api-surface-design.md` (API), §4.1, §4.2,
ARCH-CQRS-001, §8 and §9; the workflow engine spec `2026-10-10-sindri-workflow-engine-design.md` (ENG), §2, §3.4 and §6.5; the session
launcher spec `2026-10-15-sindri-session-launcher-design.md` (LCH), the per-launch MCP config · **Lanes:** the API lane
writes and builds §A (the MCP side); the engine lane writes and builds §B (run under the Step's policy, the call
record) ·
**Next:** the owner reads it and answers §C; then phases (§D).

The owner decided (2026-10-17): an agent session reaches Sindri's blocks through **two generic MCP tools**, not one
tool per block.
- `sindri_list_tools` returns the blocks this Step may call, each with its input schema.
- `sindri_execute_tool_call` asks Sindri to run one named block under the Step's policy and records the call on the Step.

So the MCP tool list, and the tokens it costs every session, stay fixed as blocks are added. A session sees a new block
through `sindri_list_tools` only when a recipe lets its Step call it.

## Terms

- **Callable block:** a block whose descriptor opts in to being called from a session (§B.1).
- **Step session:** an agent session `ILauncher` started for one Step run (ENG §6.5), whose per-launch MCP config
  names that run (§A.2).
- **Plain session:** every other session that reaches `/sindri/mcp` with the repo header: the owner's own Claude Code
  in the repo, or a session opened by `OpenItem` with no run. It may call blocks too (owner, 2026-10-18), from a
  narrower set and with no Step to record against (§A.6, §B.5).
- **Call:** one `sindri_execute_tool_call`, named by its call id. Its state is `running` until it ends, and it ends `ok` (the
  block ran and passed or answered), `failed` (the block ran and failed, timed out, or was cut off) or `refused`
  (Sindri didn't run it; the session got a tool error).
- **Calls** is the word the app and the docs use for these, everywhere. "Evidence" stays the evidence packs,
  `evidence:` facts and the evidence-provider block kind.
- **Scope:** `run:<WFI-id>:<token>` for a Step session, `repo:<name>` for a plain session. A call id is unique only
  within its scope.

## A. The MCP side (API lane)

### A.1 The two tools

They join API §8's table after `sindri_job` and follow its rules: the repo comes from `X-Sindri-Repo`, every call is
stamped `session`, and errors are tool errors carrying the SND code. Their names are the owner's C2 decision,
with the `sindri_` prefix of Sindri's other tools.

| Tool | Arguments | Handler | Kind |
|---|---|---|---|
| `sindri_list_tools` | none | `ListCallableBlocks` (also `GET /workflow/instances/{id}/runs/{token}/tools` and `GET /tools?repo=R`, §A.4) | query (`readOnlyHint`) |
| `sindri_execute_tool_call` | `name`, `arguments` (an object), `callId?`; plain sessions also `ref?`, `itemId?` (§A.6) | `ExecuteBlockCall`: **MCP only**, no HTTP route (§A.2) | `block-call`, a new session-only action class (§A.4) |

**What the agent is told.**
- The `sindri_list_tools` description says: "The Sindri blocks you may run here, with their inputs. Call this before
  `sindri_execute_tool_call`."
- The `sindri_execute_tool_call` description says: "Run one Sindri block from `sindri_list_tools`. A long block answers `running`;
  send the `callId` from that answer again to get its result. Outside a Step, pass `ref`, the full commit sha to run
  against. Every call is recorded."
- A Step's seed (UI §6.2's template) gains one line, but only when the state lists tools: "Sindri blocks you can run
  are listed by `sindri_list_tools`."

**`sindri_list_tools`** answers `{ scope, run?, tools, reason? }`:
- `scope` is `step` or `plain`, and `run` is the run's address for a Step session.
- `tools` is `[{ name, descriptorHash, description, inputSchema, mode, timeoutSeconds, readsCode }]`, ordered by name.
  - `name` is the block id.
  - `description` is the descriptor's `Description` (§B.1): Sindri's wording, so it isn't fenced.
  - `inputSchema` is a JSON Schema (draft 2020-12, the shape MCP uses for a tool's own input) built from the
    descriptor's `ParameterSpec`s, each with its description, its enum when it has one, and its item type for lists.
    Only the parameters a session may set are in it: pinned values and facts never are (§B.1). An enum whose values
    come from repo config (`gate.tests.green`'s `target`) is filled when the list is made (§B.1).
  - `readsCode` (from the descriptor) says the block runs against a checkout, so a plain call must pass `ref` (§A.6).
  - `busy` is set while another call holds the folder's lock (§B.2): "a call is building in this folder; don't build
    here until it ends".
  - `mode` is `sync` (the call answers within `timeoutSeconds`) or `async` (the call answers `running` at once).
    `timeoutSeconds` is the block's `MaxDuration` (§B.4). The timing rule below is the only one; §B cites it.
- `reason` is set only when `tools` is empty: `no-tools-listed` (the state lists none), `run-not-open`, `taken-over`
  (C5), `none-callable-in-this-build`, `no-plain-tools` (the owner narrowed `workflow.tools.plain` to nothing) or `needs-repo-config`
  (every listed block needs repo config that isn't committed on the base, such as `checks.json`, §B.1). A single
  block left out for that reason, or for `needs-recipe-pin` (§A.6), is named in `omitted: [{ name, reason }]`, so the agent and the owner see why.

**`sindri_execute_tool_call`** answers
`{ callId, scope, state, waiting?, busy?, commit?, outcome?, reason?, result?, reasons?, truncated?, spent?, ledgerSeq?, startedAt, durationMs? }`:
- `state` is `running` or `ended`. `outcome` (`ok` or `failed`), `durationMs` and `ledgerSeq` are set once it
  has ended.
  - `waiting` is `heavy-lock` while the call waits for a heavy slot, and absent once it runs.
  - `spent` is what a `Budget` block reported spending (§B.5), in the profile's currency.
  - `commit` is the commit (and, for a Step, the folder's tree hash, a `dirty` flag and `changedDuringCall`, §B.2) the
    block runs against. It is set on every answer of a block that `readsCode` once the slot and lock are held, so
    a `waiting` answer has none.
  - `busy` is as on the list row.
  - `reason` names why a call `failed`: `timeout`, `host-restart`, `run-ended`, `takeover`, `invalid-argument` (a
    problem only found when the block ran, such as a path that leaves the checkout) or the block's own code. This
    is the one list; §B cites it.
  - `ledgerSeq` is the ledger seq of the call's `step.tool-called` event, or `session.tool-called` for a plain
    session (§B.3, §B.5).
  - `result` is the block's answer and `reasons` its failure reasons. Both are scrubbed and capped at 64 KiB
    (`truncated: true` past it), and they are the same body the ledger points to, in the sync answer as in a
    polled one.
- **Checks, in this order, before anything runs:**
  1. Shape: `name` is a string, `arguments` an object, `callId` a UUID if given; `ref` (a full 40-hex commit sha) and
     `itemId` (an item id) only from a plain session. Otherwise `SND-CONTEXT-022`, as for any MCP tool, and also
     when a Step session sends `ref` or `itemId`.
  2. **The stored-call lookup comes first.** A `callId` already stored in this scope is answered from the store
     (below) and nothing else is checked: a poll never spends budget, never meets `-006`, and still works after
     `sindri_list_tools` has emptied.
  3. The run: for a Step session, the run is open and is the instance's open attempt (`SND-TOOL-404`, which wins over
     the empty list's `-001`). A takeover doesn't refuse a call (C5): it runs and is marked `duringTakeover`.
  4. Size: `arguments` are at most 16 KiB of JSON. Otherwise `SND-TOOL-002`.
  5. `name` must be in this session's `sindri_list_tools`. Otherwise `SND-TOOL-001`.
  6. `arguments` must validate against its `inputSchema`. Otherwise `SND-TOOL-002`, with each JSON-pointer path and
     message in the error's `violations` (A.3). The validator is built in Sindri from `ParameterSpec`'s closed type
     set; no JSON Schema library is added.
  7. A plain call to a block that `readsCode` must pass `ref`, which must be a commit in the registered repo, and
     `itemId` must be an item of this repo. Otherwise `SND-TOOL-002` or `SND-ITEM-404`.
  8. Budget and concurrency: `SND-TOOL-004`, `-006`.
  9. **The claim.** Only now does the API claim (scope, `callId`) with one atomic insert into the call store (state
     `waiting`). A unique conflict (two identical sends racing past step 2) becomes the stored-call answer, so a
     client retry never runs a call twice and never records a false `-006`. A call's row exists before the engine
     is asked: for a Step call the API sends the `block-call` Update and waits only until it is accepted, then holds
     the MCP request per the timing rule below (§B.2). If the engine's validator refuses the Update, the API deletes
     its `waiting` row and records the refusal under a server-made id, as for any refusal, so the `callId` stays free.
- **Timing: one rule** (`mode` comes from the block, never the call):
  - An `async` block answers `running` at once.
  - A `sync` block holds the request while it waits for a heavy slot, at most 30 s, then while it runs, at most
    `timeoutSeconds`. It answers `ended` if it finishes within both.
  - A `sync` call still waiting after 30 s answers `running` with `waiting: heavy-lock`, and the session polls.
  - Any block still running at `timeoutSeconds` after its slot was granted ends `failed`, reason `timeout`.
  - So `mode: sync` means "normally answered in the call": an agent must still cope with `running`.
- **One call id, one call, per scope.** The session may pass its own `callId`. With none, the server makes one.
  The store key is (scope, `callId`), so two runs, or a run and a plain session, never see each other's calls.
  - Sending the same `callId` with the same `name`, `arguments`, `ref` and `itemId` again runs nothing. It returns
    the call's current state, so this is how a session polls (no job, no `JOB-` row; jobs don't survive a restart
    and sessions can't start them).
  - The same `callId` with any of those different is `SND-TOOL-409`. Because `ref` is a full sha, a reused id can't
    return an old commit's result for a moved branch. The API checks it against the stored call
    before the engine is asked, so the answer doesn't depend on Temporal's Update dedupe.
  - **A refusal doesn't claim the `callId`** (the claim is step 9). A refused call is recorded under a server-made
    id, with the sent `callId` as a field, so the session can fix its arguments and send the same `callId` again.
  - A call still running when the host stops ends `failed`, reason `host-restart`. A poll after the restart returns
    that, and the session calls again with a new id.
- **Fencing.** Every string in `result` and `reasons` that isn't Sindri's own wording is fenced as `sindri:call`, and
  its object carries `"trust": "untrusted"`, as job results are since P8. The block's own reason codes aren't fenced;
  their free text is. So is a block name the session sent that isn't a listed block, wherever it's shown.

### A.2 Which session a call belongs to

A call has to name its Step run, because the run's policy picks the allowed blocks and the run gets the record.
P8 keeps only `X-Sindri-Repo` of the `X-Sindri-*` headers on MCP, and the launch secret never rides MCP (owner,
2026-10-16).
- **`X-Sindri-Run` (choice C1).** The per-launch MCP config that `ILauncher` writes (LCH: the `mcp` form,
  `{mcpConfig}`) sets `X-Sindri-Run: <WFI-id>:<run token>` for a Step session. BA-1 adds it to the form.
  - `<WFI-id>` is the instance's opaque id (`WFI-` and 32 lower-case hex digits, as every instance route takes), and
    `<run token>` is the decimal run counter. The grammar is `^WFI-[0-9a-f]{32}:[0-9]{1,9}$`, so the value is safe
    to splice into Codex's `-c` overrides.
  - A header that doesn't match the grammar is refused (`SND-CONTEXT-022`), never dropped, so a Step session can't
    silently turn into a plain one.
  - The endpoint's header filter becomes a two-entry allowlist (`X-Sindri-Repo`, `X-Sindri-Run`), and its existing
    header-drop test gains the second entry.
- **No proof, by design.** The run token is a public counter, so the header names the run without proving the caller
  is that run's session. Under the non-adversarial threat model, the guard is against a session reaching the wrong run
  by mistake, not against forging.
- **Server checks for a Step session:** the run is the instance's open attempt on the agent runtime, and its repo is
  the header's repo (`SND-TOOL-404` otherwise; where the engine's "not the open attempt" and 404 overlap, the engine
  decides and it's 404). During a takeover calls still run (C5).
- **No HTTP route for `sindri_execute_tool_call` in v1.** An HTTP route would take the run from its URL, and any `session`
  curl could then reach another run by mistake. The MCP endpoint calls the `ExecuteBlockCall` service through the
  same chain as every command (gate, audit, authorization, validation). This is ARCH-CQRS-001's fifth carve-out, an
  MCP-only command; the list stays one place.
- **The per-launch server isn't confused with a repo-local one.** A repo where the owner ran `sindri mcp register`
  already has a server named `sindri` (with no run header). The per-launch config names its server `sindri-step`, and
  the Step's rendered policy denies `mcp__sindri__*`: a deny rule in the settings file the `claude-settings-file` form
  writes per launch (L-17's StepPolicy rendering, #295). It is a per-run value, not argv, so no `argvHash` changes and
  no approved entry parks, so a Step session can't call blocks as a plain session by
  mistake. There is no `--strict-mcp-config`: the session keeps every other MCP server it has (serena, github and the
  rest). Codex's `-c` overrides name `mcp_servers.sindri-step` and set `mcp_servers.sindri.enabled=false`. Sindri never
  registers a `sindri` server for Codex (API §8), and doctor warns on a user-wide one. The proof checks the Claude case
  (A.5 step 7).
- **A session with no `X-Sindri-Run` is a plain session** (§A.6).
- **An agent with no per-launch MCP form** (`mcp: none`) launches with no Sindri MCP at all (API §8), so it can't call
  blocks; it is never a plain session, and the seed line is left out.

### A.3 Errors (API §9)

| Code | Kind (status) | When | Message (fix) |
|---|---|---|---|
| `SND-TOOL-001` | Invalid (400) | The named block isn't in this session's `sindri_list_tools` | "`<name>` isn't a block you can run here (fix: call `sindri_list_tools`)" |
| `SND-TOOL-002` | Invalid (400) | `arguments` are over 16 KiB or don't validate against the block's `inputSchema` (extension `violations: [{path, message}]`); a plain call to a `readsCode` block without `ref`, or with a `ref` that isn't a commit in the repo | "The arguments for `<name>` don't fit its inputs (fix: see `violations`; outside a Step, pass `ref` as a full commit sha)" |
| `SND-TOOL-004` | Forbidden (403) | This run's call budget is spent (`workflow.calls.perRun`, §B.4). It doesn't refill within the run | "This Step has used its `<n>` calls (fix: finish the Step, or raise `workflow.calls.perRun` in the profile)" |
| `SND-TOOL-006` | Conflict (409) | A call is already running for this run, or the repo already runs `workflow.calls.plainConcurrent` plain calls (default 2), §B.4 | "Another call is still running (fix: wait for it to end, then call again; your own running call is polled with its `callId`)" |
| `SND-TOOL-404` | NotFound (404) | No open agent run at `X-Sindri-Run` in this repo | "No open Step run `<run>` in `<repo>` (fix: this Step has ended; there is nothing left to call or report)" |
| `SND-TOOL-409` | Conflict (409) | The same `callId` came with a different `name`, `arguments`, `ref` or `itemId` | "Call `<callId>` was sent with other inputs (fix: use a new `callId`)" |
| `SND-ITEM-404` | NotFound (404) | A plain call's `itemId` isn't an item of this repo (the existing code) | its existing message |

- **The MCP error carries extensions.** `McpToolError` gains an optional `Extensions` object, written only when set,
  so a code's extension members (API §9: cleaned, arrays cut at 100) reach the agent. Today only the code and the
  message do. A golden covers `SND-TOOL-002`'s `violations`.
- **A block that runs and fails isn't an error.** Its call answers `ended`/`failed` with the block's reasons and is
  recorded like any call.
- **Refusals are recorded (C4).** A call refused with `-001`, `-002`, `-004`, `-006` or `SND-ITEM-404` is still
  recorded as a `refused` entry with its code and no result (§B.3). `SND-CONTEXT-022`, `SND-TOOL-404` and `SND-TOOL-409` have
  nothing to record against. They go to the structured log with the repo and the run header.

### A.4 Catalog, CLI, goldens, telemetry, docs and the app

- **Action class `block-call`** (API §4.1): a session asks Sindri to run a callable block. It appends only the call's
  own records (`*.tool-started`, `*.tool-called`) and never a transition. The authorization table (§4.2)
  gives it to `session` only. A human runs a block through a recipe; the CLI only lists.
  API §4.1, §4.2 and ARCH-CQRS-001 are amended to match.
- **Catalog rows:** `ListCallableBlocks` (query) and `ExecuteBlockCall` (`block-call`). The UI lane's pins change:
  labels, the row count and copy blocks.
- **Read routes** (queries, all from the one `block_calls` store, §B.2):
  - `GET /workflow/instances/{id}/runs/{token}/tools` (a Step's list) and `GET /tools?repo=R` (what a plain session may call);
  - `GET /workflow/instances/{id}/runs/{token}/calls` (the run's calls, running ones included, each with `cited`);
  - `GET /calls?repo=R[&item=ITM-…][&after=seq]` (plain calls);
  - `GET /calls/result?scope=<scope>&callId=<id>` (one call's body, fenced as in the answer). The scope is a query
    value, so its `:` and a repo name need only URL-encoding.
  The app refreshes on `step.tool-started`/`session.tool-started` and `*.tool-called` (ids and the block only, §B.3),
  so a running call shows as soon as it starts.
- **CLI:** `sindri workflow tools <address> [--run N] [--json]`, `sindri tools [--repo R] [--json]`, `sindri calls
  [--repo R] [--item I] [--run <WFI-id>:<token>] [--json]`, `sindri call <callId> (--run <WFI-id>:<token> | --repo R)
  [--json]` (one call with its result, for debugging a failed call from a terminal), and `sindri recipe tools <file>
  --state S [--repo R] [--json]` (the enum values that come from repo config are filled from `--repo`, else shown as
  "from the repo's checks.json"), which runs in process (like
  `recipe validate`), so a block author checks a recipe's tools before anything is launched.
- **Step report:** `CompleteStep` (`sindri workflow complete --run`) gains the optional `calls: [<callId>, ...]` of
  §B.3. The API binds it and passes it through; the engine validates it. The CLI flag is `--call <id>`
  (repeatable). The request golden changes.
- **Goldens:** every read route, the `sindri_list_tools` answer for each scope and reason, the `sindri_execute_tool_call` answer
  for `ended`/`ok`, `ended`/`failed`, `running`, `truncated` and each error code, and the tools/list golden with the
  two tools after `sindri_job`.
- **Telemetry** (`Aw.Kernel.Telemetry`): one span per call, `sindri.block_call`, with the block, the scope, the outcome
  and the reason. Counters `sindri.block_calls` (by block, scope and outcome) and `sindri.block_call.duration`. They
  answer whether agents use calls at all.
- **Docs:** each phase updates them.
  - `docs/csharp/sindri.md`: the codes, the action class, the two tools and the CLI verbs.
  - `docs/csharp/sindri-workflow.md`: how to make a block callable, and the recipe's `tools:`.
  - `docs/csharp/sindri-profile.md`: the four profile keys (`workflow.tools.plain`, `workflow.calls.perRun`,
    `workflow.calls.plainPerDay` (a notice only), `workflow.calls.plainConcurrent`).
  - The help text, the seed line, and the tool descriptions above.
- **The app (BU-1).** Calls go where the human already looks at a Step:
  - **A "Calls" list under the Step's trace** in the item sheet. One row per call, in seq order: a glyph plus a word
    for the state (`waiting`, `running`, `ok`, `failed`, `refused`), the block, the duration, the failure reason in
    words (`timed out`, `ended with the Step`, `Sindri restarted`, `taken over`, `bad argument`), the code it saw
    (the commit's short sha, plus `uncommitted edits` or `changed during the call` when set), and, once the Step has
    reported, `cited` or `uncited` (`not yet reported` before that). A call made during a takeover says `during
    takeover` instead, and a refused row says neither: refusals can't be cited.
    Colour is never the only signal.
  - A running call shows as soon as it starts, from the run's calls route.
  - A row opens the call's result in the read-only result view, rendered as untrusted text (the fence's rule), with
    `truncated` said in words.
  - Refusals show their code's message and the fenced name the session sent. The refusals-capped marker is one
    row. Refusals get no Timeline line.
  - The list says "No calls" when there are none.
  - **A Timeline line** per ended call: "call `<block>` · `<outcome>` · `<duration>` · `<short sha>`".
  - **Keyboard and focus:** the list is in the sheet's tab order, and Enter opens a row. A row that appears or
    changes while the sheet is open never moves focus. The live region announces only an ended `ok` or `failed`
    call ("call `<block>` ended `<outcome>`"), and refusals at most once per 10 s as "`<n>` calls refused" (UI §5).
  - **Plain calls** have no Step to sit under. In v1 they show on the linked item's Timeline as "plain call, not
    proof" when the call passed an `itemId` (the event's ledger item is that item, §B.5), as "plain call `<block>` ·
    `<outcome>` · `<short sha>` · not proof", and in `sindri calls`. A repo-wide list waits for the Repos screen,
    which isn't served yet.

### A.5 Proof

API §8's proof shape, with a launched Step session in place of `claude -p`. A recipe with one state lists two
callable blocks, one `sync` and one `async`. The journey:
1. `sindri_list_tools` shows exactly those two, with their descriptions, schemas and modes.
2. A bad argument gets `SND-TOOL-002`, and the agent sees its `violations` path.
3. A good `sync` call answers `ended`/`ok`, and its `ledgerSeq` is in `workflow events`.
4. The same `callId` again runs nothing (the block's own counter is unchanged), and the same `callId` with other
   arguments gets `SND-TOOL-409`.
5. The `async` call answers `running`, the run's calls route lists it running, and polling the same `callId` ends it.
   (BU-1's proof shows the same in the app.)
6. A step report citing both call ids is accepted, and one citing an unknown id is refused.
7. With a repo-local `sindri` server registered too, the Step session reaches `sindri-step` (`sindri_list_tools` says
   `scope: step`), its `mcp__sindri__*` tools are denied, and serena is still listed among its MCP servers.
8. A plain session in the same repo (no run header) gets every callable block (`scope: plain`), a `Network` one included. A call without `ref` to a
   `readsCode` block is refused; with `ref` it runs, answers the `commit`, and is a `session.tool-called` event on
   the item's Timeline it names, not on the Step.
9. With `workflow.calls.plainPerDay: 1`, a second plain call still runs; its event carries `capPassed: true` and the log
   says so. The setup lists one `Network` block for step 8.

### A.6 Calls outside a Step (owner, 2026-10-18)

The owner's own sessions may call blocks: "my own sessions should be able to use sindri block tools" (C3), and "my
sessions can use every block. no matter what. if i'm steering it's high priority and sensitivity" (owner, 2026-10-18,
C6). The engine side is §B.5.
- **Whose session** (owner, 2026-10-18, C11: "sindri can know if the session is owned by a sindri workflow. otherwise
  it's me"). The only test is the run header: a session with `X-Sindri-Run` belongs to a Sindri workflow (a Step
  session, §A.2), and every other session is the owner's, the team's lane sessions and a session `OpenItem` opened
  included. Everything below is the owner's treatment.
- **Bound to the repo only.** No run header. MCP is stateless and Claude Code sends no session id, so "the session"
  is just `caller: session` plus the repo: the cap and the record are per repo.
- **`sindri_list_tools` for a plain session** is every Callable block (§B.5), Network and Budget ones included; only the
  owner's optional `workflow.tools.plain` list narrows it. `scope: plain`. A block with a required parameter that
  has no default and can only be pinned in a recipe is left out with `omitted` reason `needs-recipe-pin`.
- **High priority** (C6). An owner's call takes its heavy slot ahead of merge trains, transition gates, Step calls and
  agent work, including jobs waiting through the box's heavy wrapper (§B.5: Sindri publishes how many owner calls
  wait, and the wrapper holds that many slots back, as it does for the meta's reserve; the meta's wrapper change,
  owner-approved at BM-1). Owner calls hold at most the box's slot cap minus one, so a gate or a merge train always gets a slot. The timing rule (§A.1) is unchanged: a
  sync call still waits at most 30 s before answering `running`.
- **Sensitivity** (owner, 2026-10-18, C10 option (b)): an owner's call is treated as an agent's call is, and marked as
  the owner's (`caller: owner` on its events and in the app). Its result is scrubbed as every result is and kept
  30 days.
- **`sindri_execute_tool_call`** takes an optional `itemId` and `ref`:
  - `itemId`: the item the call is about. The call shows on its Timeline as "plain call, not proof" (choice C8).
  - `ref`: the full 40-hex sha of the commit the block runs against, in a scratch checkout Sindri makes and removes
    (choice C7). It is required for a block that `readsCode`, because the registered repo's HEAD is rarely what a
    session in a lane worktree is working on, and names like `HEAD` or a branch are refused for the same reason.
    The commit must exist in the registered repo (a lane worktree's commits do, since worktrees share the object
    store), else `SND-TOOL-002`. A session gets it with `git rev-parse HEAD` after committing. A block that doesn't
    read code runs with no checkout.
- **Caps:** none refuses an owner's call for its count. `workflow.calls.plainPerDay` (default 200) is a notice: past
  it the event carries `capPassed: true` and the log says so. `workflow.calls.plainConcurrent` (default 2) calls run
  at once per repo, then `SND-TOOL-006`, and the box-wide bound above keeps a slot for gates and trains.
- **Budget blocks** spend from no run: their spend is recorded on the call (`spent`) and is never refused (§B.5).
- **The owner sees the totals.** `GET /status` gains `plainCalls: [{ repo, today, spentToday,
  capPassed }]`, and the app's Health page shows one line per repo ("`<repo>`: `<n>` calls today, `<spent>` spent").
- **Record:** `session.tool-called` on the repo, kept 30 days (§B.5). It never satisfies a gate and is never citable
  in a step report.

## B. The engine side (engine lane)

A call never moves the instance. It runs one block for the session and records what happened, and the Step's own
report and the recipe's gates still decide the transition (ENG §6.5: "the session reports; the engine decides").
The answer's shape, the order of checks and the claim, the timing rule, the store key, the refusal rule and the one
list of failed reasons are §A.1's; §B cites them and never restates them.

### B.1 Which blocks are callable

- **The descriptor opts in.** `BlockDescriptor` gains these fields, each written only when set, so every existing
  descriptor hashes as before (like `HumanOnly`):
  - `Callable` (`bool?`).
  - `Description` (`string?`), Sindri's own one-line text, which `sindri_list_tools` serves (§A.1).
  - `ReadsCode` (`bool?`, written only when true): the block runs against a checkout. `sindri_list_tools` shows it as
    `readsCode`, a plain call to it must pass `ref` (§A.1 check 7, §A.6), and its answers carry `commit` (§A.1).
  - `MaxDuration`, from DT-J2 (draft PR #306, not merged yet). The catalog test checks `descriptor.MaxDuration`
    itself, never `BlockLimits`' 2-minute fallback.
- **`ParameterSpec` gains** these fields (R1-05, R1-09), also written only when set:
  - `Description`;
  - `Enum` (fixed allowed literals, part of the hashed descriptor);
  - `EnumSource` (`string?`): the values come from repo config when the list is made, not from the descriptor. The
    descriptor hashes only the source's name (`checks.targets`), never its values. `sindri recipe tools`, which
    runs with no repo (§A.4), prints it as "targets from the repo's `checks.json`";
  - `ItemType` (for a list);
  - `SessionSettable` (`bool?`): the session may set this parameter per call. A parameter without it never appears
    in `inputSchema`.
- **The catalog test** `Every_callable_block_is_described_bounded_and_writes_no_state` requires, for every Callable
  block:
  - its kind is `gate` or `evidenceProvider` (a `step`, `effect` or `stepInput` block is never callable);
  - no `Effects`, `WriteScope` or `AutonomyTier` capability;
  - a `Description` and a `MaxDuration`;
  - a `Description` on each session-settable parameter;
  - no session-settable parameter that is a capability field.

  A block pack that breaks any of these fails its own build. A callable block may write build output under the
  folder it runs in (B.2), and nothing else.
- **What the session may set (R1-09).** `inputSchema` (§A.1) is built from the session-settable parameters only.
  The following never come from a call; the recipe pins them, or the block's defaults supply them:
  - capability fields;
  - fact inputs (`{ fact: ... }`);
  - every other parameter.

  A session-settable `path` parameter resolves inside the call's folder. An absolute path or a `..` that leaves it
  is `SND-TOOL-002` before anything runs. A symlink out of the folder is found only when the block runs, so that
  call ends `failed`, reason `invalid-argument` (§A.1's list). A call can't widen what the Step reaches.
- **The recipe lists them per state.** An active state gains `tools:`. Each entry is a block id (`- gate.tests.green`)
  or `{ block: <id>, with: { ... } }`, which pins parameters. A pinned value is a literal, a `{ fact }` or a
  `{ profile }`, checked like a Step's `with:`.
  - `tools:` is written only when non-empty, so every approved recipe without it keeps its hash (R1-19).
  - With it, the recipe hash covers it, approval pins it, and `recipe show` and the approval preview print it.
  - There is no recipe-level default list in v1 (R1-31): each state says what its session may call.
- **The validator checks the list** (new rows in `docs/csharp/sindri-workflow.md`, after `RCP-TIME-003`):

| Code | Error when |
|---|---|
| `RCP-TOOL-001` | A `tools:` entry is not a registered block, or its descriptor is not `Callable` |
| `RCP-TOOL-002` | `tools:` is on a state whose Step can't run in an agent session: a wait or terminal state, a `humanOnly` Step, or a Step with only the block or desk runtime |
| `RCP-TOOL-003` | A listed block needs a capability (`Network`, `Budget`, `Tools`) that the state's own Step doesn't hold. A call runs with the Step's capabilities, never wider (owner, 2026-10-17: a Step only tightens) |
| `RCP-TOOL-004` | A required parameter that isn't session-settable has no pinned value and no default, or a pin names a parameter the block doesn't have, or a pin sets a capability field to anything but a literal or `{ profile }` |
| `RCP-TOOL-005` | A `ReadsCode` block is named in a transition's `gates:`. v1 builds no checkout for a transition gate (B.2, "A called gate does not stand in") |

- **`sindri_list_tools` is computed, not stored.** `ListCallableBlocks` returns, for an open agent run, the state's `tools:`
  entries whose block is Callable in the running build, ordered by id.
  - Each entry carries `mode`: `sync` when `MaxDuration` ≤ 30 s, otherwise `async` (§A.1).
  - An `EnumSource` parameter's `enum` is filled in `inputSchema` at that moment, from the run's `checks.json` copy
    (below). Validation (§A.1 check 6) uses the same values, so a target the list didn't show is `SND-TOOL-002`.
  - A block whose repo config is missing or invalid on the base is left out, in `omitted` with reason
    `needs-repo-config` (§A.1).
  - A block named in an approved recipe but gone from the build drops out. Calling it is `SND-TOOL-001`, and the
    approval drift check (ENG §5.8) reports it.
- **The first callable block (R1-01)** is `gate.tests.green`, new in BM-1.
  - It runs one test target that the repo declares in `.agentic-workflow/checks.json` (per-repo config, schema
    `schemas/checks.schema.json`, new in BM-1): each target's argv command, its working directory, its filter
    template, and the env names it needs. Its pass or fail is its outcome.
  - Its only session-settable parameters are `target` (`EnumSource: checks.targets`) and `filter`. The command is
    never session-set.
  - **`checks.json` is never read from the folder the agent edits.** Sindri reads it with
    `git show <base>:.agentic-workflow/checks.json`. For a Step call the base is `fact:change.base` (the merge
    target in `fact:change` `{ repo, branch, base, head, pr? }`, LN DTC4), else the registered repo's default
    branch. A plain call reads it from the default branch's tip, never from `ref`. Its sha256 (`checksSha256`) is
    recorded on every call (B.3).
  - **So a target exists only once it is on the base.** A target added on the branch works only after it merges.
    With no `checks.json` on the base, or one that fails its schema, the block is omitted with `needs-repo-config`,
    and the `sindri doctor` check (BM-1) names the repo and the fix ("commit `.agentic-workflow/checks.json` on the
    default branch"). The `checks.json` reference doc says the same.
  - **`filter` is a value, never a command.** It is bound as exactly one argv element into the target's template:
    never a shell string, never in an option position. A value starting with `-` is preceded by the template's own
    `--`; with no `--` in the template, the call ends `failed`, reason `invalid-argument`.
  - Capabilities: `Tools`. `ReadsCode`: true. `MaxDuration`: 20 min, so it is `async`.
  - **The first shipped state that lists it** is `workflow.review-loop`'s `fix` state, as
    `tools: [gate.tests.green]`. Its Step, `step.review.fix`, is an agent Step (`AgentStep`) that holds `Tools`, so
    RCP-TOOL-002 and -003 pass. The fixer runs the tests before it reports `fixed`. This changes that recipe's hash
    before it is approved.
  - **It survives DTC4.** The line spec's DTC4 (LN-Y2) reshapes the review loop's states but keeps
    `step.review.fix`, in the phase worktree. LN-Y2 carries `tools: [gate.tests.green]` to whichever state runs
    `step.review.fix`, and that Step keeps `Tools`. A BM-1 recipe test,
    `Every_state_running_step_review_fix_lists_gate_tests_green`, makes a reshape that drops it fail.

  `evidence.check` and the two shipped gates stay non-callable: they read engine state, so they mean nothing called
  from a session.

### B.2 Running a call

- **The call store.** One table, `block_calls`, in the ledger database `$AW_SINDRI_HOME/ledger.db` (a migration in
  BM-1), beside `events`, so a call row and its event are written in one transaction. It is owned by an
  `IBlockCallStore` port in Core with a SQLite implementation. It is the one source for the read routes (§A.4).
  - **Key:** (scope, `callId`) (§A.1), scope `run:WFI-<32 hex>:<token>` or `repo:<name>`. A refused call is stored
    under a server-made id with the sent `callId` as a field (§A.1).
  - **Who writes the row.** The API inserts it as `waiting` (§A.1 step 9, the claim). The engine only updates it:
    `waiting` → `running` → `ok` | `failed`. If the Update's validator then refuses (below), the API deletes its
    claimed row and records the refusal under a server-made id (§A.1's refusal rule), in one transaction.
  - **Columns:** `scope`, `callId`, `sentCallId`, `block`, `descriptorHash`, `argumentsSha256`, `ref`, `itemId`,
    `state` (`waiting` | `running` | `ok` | `failed` | `refused`), `reason`, `commit`, `tree`, `dirty`, `endTree`,
    `changedDuringCall`, `checksSha256`, `resultSha256`, `resultBytes`, `truncated`, `ledgerSeq`, `started`,
    `ended`, `duringTakeover`.
  - **What reads it:** the stored-call lookup and `SND-TOOL-409` (§A.1), the poll, the read routes and the app's
    rows (§A.4), and the plain concurrency check (B.4). The workflow never reads it (below).
  - **Bodies are per-call rows,** not entries in a shared store. `block_call_bodies(scope, callId, kind, sha256,
    bytes)`, with `kind` `arguments` or `result`, in the same database. Each body belongs to one call, so deleting a
    call's row deletes its bodies and never another call's. The existing content store, `workflow/facts/<sha256>`
    (`HomeFactStore`), is not used: it is SHA-addressed and shared, so a 30-day delete there could remove a body a
    fact still needs. History and the ledger hold only ids and hashes, never a body (R1-18).
  - **Retention.** Plain calls' rows and bodies go after 30 days (B.5). A Step call's rows and bodies live as long as
    the ledger, on purpose: they are the Step's record, at most `perRun` × (16 KiB + 64 KiB) per run.
- **The run's call state lives in the workflow.** A Temporal Update validator must be deterministic and can't read
  SQLite, so `RecipeWorkflow` keeps, per open run: the call in flight, the count against the budget, and each
  ended call's id, outcome and `duringTakeover`. It is filled from `RunBlockCall`'s result. The Update's validator
  and `CompleteStep`'s citation check (B.3) read only this.
- **`BlockContext`** (a BM-0 record in Core) is what a running call gets:
  - `Repo`, `Worktree` (path, or null for a block that doesn't read code), `Commit`, `Tree`, `Dirty`;
  - the approved profile, the run's `StepPolicy` (null for a plain call) and the Step's capabilities;
  - `Scope` and `CallId`;
  - `Spend`, a sink a `Budget` block reports its spend to (tokens and cost). For a Step call it also spends the
    run's ENG §3.7 budget (B.4); for a plain call it spends from no budget. Either way the total is the answer's and
    the `*.tool-called` event's `spent` (§A.1);
  - a `CancellationToken`.

  It relates to the gate context this way: for a gate block, the runner builds the existing `GateContext` (`Gate`,
  `Arguments`, `Facts`, `Threshold`) from the call, and passes the checkout through a new init-only member,
  `GateContext.Checkout` (repo, worktree, commit). Nothing calls `IGate.Evaluate` or an `IEvidenceProvider` outside
  tests today, so BM-1 adds the runner: `RunBlockCall`'s body, shared by Step and plain calls.
- **The path of a Step call:**
  1. The API runs §A.1's checks in their order and claims the row (step 9).
  2. The API writes the arguments body, then sends the `block-call` Update to the instance's `RecipeWorkflow` and
     waits only until it is accepted (`WaitForStage.Accepted`). The Update carries `{ callId, block,
     argumentsSha256 }` only. Dedupe is the API's claim, not the Update id.
  3. The Update's validator refuses before anything enters history, from the workflow's run call state. Each
     refusal is a typed result that the API maps to §A.3, so no new `SND-WF` code is needed:
     - the run is not the instance's open attempt, or its Step isn't on the agent runtime: `SND-TOOL-404`;
     - the block is not in `sindri_list_tools`: `SND-TOOL-001`;
     - another call of this run is in flight: `SND-TOOL-006` (one call in flight per run, B.4);
     - the budget is spent: `SND-TOOL-004`.
  4. The handler runs the `MarkCallStarted` local activity. It updates the row's `started` (the row stays
     `waiting`) and appends `step.tool-started` (B.3).
  5. The handler runs one `RunBlockCall` activity: one attempt, no retries, with a heartbeat. It takes the heavy
     slot and the folder lock (below), records `commit`, `tree` and `dirty`, marks the row `running`, runs the
     block, records `endTree` and `changedDuringCall`, then marks the row ended and appends `step.tool-called`
     (B.3) in one transaction.
  6. The API answers by §A.1's timing rule, reading the row.
- **Where a Step call runs (owner, 2026-10-18, C9).** In the Step's own working folder, as it is, uncommitted edits
  included. Today that is the run's worktree (ENG §6.5). With the line spec (LN-WF2, DTC4) it is the phase worktree,
  shared by the phase's Steps. Either way it is Sindri's folder, never the owner's or the shared checkout.
  - Before the block starts, the call records the folder's `commit` (`git rev-parse HEAD`), its `tree` (the folder
    as it is, untracked files included: `git add -A` into a temporary index file, then `git write-tree`; the
    folder's own index is untouched) and `dirty` (the tree differs from the commit's).
  - **After the block ends, it takes the tree again** (`endTree`). When it differs from `tree`, the call is
    `changedDuringCall: true`: the folder moved under the run (an edit or a commit), so the result may not match
    either tree. The answer shows it (§A.1's `commit`), and B.3's event carries it.
  - A callable block writes only build output under that folder. It never commits, stages or edits a tracked
    file.
  - **Overlap with the agent's own builds (provisional default).** A call holds a per-folder lock,
    `<git-dir>/sindri-call.lock` (a `flock`), for as long as it runs, as well as its heavy slot.
    - In a run's own worktree this changes nothing in v1: one call per run is in flight (`-006`). It matters once
      the phase's Steps share one folder (DTC4): a call from one Step then waits for another Step's call.
    - The agent's own build doesn't take that lock in v1. The heavy wrapper lives on the box, outside the repo, so it
      can't change through a reviewed PR.
    - While a call holds the lock, `sindri_list_tools` and the answers carry `busy` (§A.1).
    - Making the wrapper take the same lock is a follow-up. It is done once the wrapper is versioned in the repo.
    - Time spent waiting on either lock is slot wait (`waiting: heavy-lock`, §A.1's timing rule).
- **Restarts (R1-13).** A single-attempt activity is not resumed. When the host restarts, the activity's heartbeat
  times out and it ends `failed` with reason `host-restart`. On startup, a sweep also:
  - ends every `waiting` or `running` row the workflow no longer owns that way, appending its `*.tool-called`
    under the same idempotency key (B.3), so nothing is appended twice;
  - removes orphaned scratch worktrees under `$AW_SINDRI_HOME/calls/worktrees/` (B.5): only by that path prefix,
    with `git worktree remove`, then `git worktree prune`. `--force` is used only for a path under that prefix,
    which is always Sindri's own scratch, never a run's or a user's worktree;
  - removes orphaned scratch `TMPDIR`s under `$AW_SINDRI_HOME/calls/tmp/`, by that prefix.
- **The heavy slot (R1-15), taken in Sindri.** `RunBlockCall` takes the slot in-process through `IHeavySlots.Take`
  (`Aw.Sindri.Core.Locks`), with the holder `sindri call <block> <scope>`, never through the box's wrapper. So
  Sindri sees the moment the slot is granted, which the row's `waiting` → `running`, `waiting: heavy-lock` and
  `lockWaitMs` rest on.
  - Every `ReadsCode` call takes one box-wide slot, whatever its command.
  - `MaxDuration` starts when the slot is granted (§A.1's timing rule).
  - **A ceiling on the wait.** `Take` waits at most 60 min. Past it, the call ends `failed`, reason `timeout`
    (§A.1's list), with its `lockWaitMs`. A plain call holds one of its repo's `plainConcurrent` places while it
    waits.
  - The child starts with `AW_HEAVY_LOCK_HELD=1`, `AW_STATE_DIR` and `AW_HEAVY_JOB_LOCK` (the lock Sindri took), so
    a `heavy` wrapper inside the target's command runs straight through and never takes a second slot.
  - **Priority.** A Step call takes its slot at the default priority. A plain call, which is always the owner's,
    takes it at `High` (B.5). The order and the rules that make it hold against the box wrapper's jobs are B.5's.
  - **`IHeavySlots` reads the box's rules, as the wrapper does** (BM-1). Today `HeavyLockSettings` reads only
    `AW_HEAVY_SLOTS`. It gains:
    - the box cap file, `$AW_STATE_DIR/heavy-slots-cap`, which wins over `AW_HEAVY_SLOTS`;
    - the memory guard: slot k ≥ 2 needs `AW_HEAVY_MIN_FREE_MB` + (k − 2) × `AW_HEAVY_MIN_FREE_STEP_MB` available.
      It is never skipped, whatever the priority;
    - meta's reserve, `$AW_STATE_DIR/heavy-meta-reserve`: slots it reserves are taken only at `High` (B.5) or by
      meta's own jobs.
  - With one call in flight per run, a run holds at most one slot for calls.
- **The environment (R1-29).** The block runs as the host. Its process environment is a fixed base plus the repo's
  names, never the host's or the session's:
  - the fixed base: `PATH`, `LANG`, the three lock variables above, the real `HOME` (the repo's tools need it),
    and a scratch `TMPDIR` under `$AW_SINDRI_HOME/calls/tmp/<id>/`, removed when the call ends. `<id>` is a
    server-made GUID v7 (`GuidV7Ids`), never the `callId`, which is unique only within its scope. HOME is never
    swapped: a temporary HOME makes each build download its own package cache. `AW_HEAVY_PRIORITY` and every other
    host `AW_HEAVY_*` are stripped;
  - the env names the target lists in `checks.json`, after the denylist.
  - **The denylist wins** over the repo's names: `*TOKEN*`, `*SECRET*`, `*KEY*`, `*PASSWORD*`, `*PASSWD*`,
    `*CREDENTIAL*`, `*AUTH*`, `GH_*`, `GITHUB_*`, `AWS_*`, `AZURE_*`, `GOOGLE_APPLICATION_CREDENTIALS`,
    `AW_SINDRI_LAUNCH_SECRET`, and the provider keys (`ANTHROPIC_*`, `OPENAI_*`, `CURSOR_*`). A denied name is
    dropped and named in the call's reasons, never passed.
- **Takeover (R1-10; owner, 2026-10-18, C5).** A call during a takeover runs, and is recorded with
  `duringTakeover: true`. A step report can't cite it (B.3). A call in flight when the takeover starts keeps running.
- **How a call ends early.** A call in flight when its run ends (step report, the state's timeout, `session.ended`,
  cancel) is cancelled through its heartbeat and ends `failed`, reason `run-ended`.
- **A called gate does not stand in for a transition's gate (R1-12).** The engine decides from its own runs, never
  from a session's call. In v1 no transition gate gets a checkout (`GateContext.Checkout` is null on a transition),
  so a `ReadsCode` block can't be a transition gate (RCP-TOOL-005). The transition's own test run is the recipe's
  checks Step, not `gate.tests.green`.

  Transition gates with a checkout, and reusing a call for one when the descriptor hash, the arguments hash and the
  tree all match, are follow-ups, not in v1.

### B.3 The call's record

- **Two ledger events per call,** each with a fixed idempotency key, so a retried local activity or the restart
  sweep can't append one twice.
  - `step.tool-started`, appended by `MarkCallStarted` (B.2 step 4), key `call:<scope>:<callId>:started`:
    `instance`, `run`, `callId` and `block` only. The app refreshes on it (§A.4).
  - `step.tool-called`, appended by `RunBlockCall` with the row's end (B.2 step 5), or by the sweep, key
    `call:<scope>:<callId>:called`. Its fields:
    - `instance`, `run`, `callId`, `block`;
    - `descriptorHash` (R1-35; the running build's);
    - `argumentsSha256`, `checksSha256` (when the block reads `checks.json`);
    - `commit`, `tree`, `dirty`, `changedDuringCall` (a `ReadsCode` block);
    - `outcome` (`ok` | `failed` | `refused`) and `reason` (null-omitted; §A.1's list);
    - `resultBytes`, `resultSha256`, `truncated`;
    - `durationMs`, `lockWaitMs`;
    - `duringTakeover` (null-omitted);
    - `launch` and `client` (R1-39: the run's launch id, and the MCP client name the API stamps).

  `ledgerSeq` (§A.1) is the `step.tool-called` event's seq. Neither event triggers a transition.
- **Example lines (R1-36), as `workflow events` prints them:**
  - `tool-started gate.tests.green`
  - `tool-called gate.tests.green · ok · 4m 12s (waited 40s for a heavy slot) · a1b2c3d dirty`
  - `tool-called gate.tests.green · failed · 1m 3s · 3 tests failed`
  - `tool-called gate.tests.green · failed · invalid-argument · /filter starts with '-'`
  - `tool-called gate.tests.green · refused · SND-TOOL-002 arguments: /filter must be a string`
  - `tool-refusals-capped · 50 refused calls recorded; later ones are not`
- **The result body (R1-38).**
  1. It is scrubbed by `Aw.Redaction` first.
  2. Then it is cut to 64 KiB. A longer one keeps its first 64 KiB and is marked `truncated`.
  3. It is stored as the call's `result` row in `block_call_bodies` (B.2). The order (scrub, then cut) means a
     secret split across the cut is still removed.

  A failed call's body is the block's reasons.
- **Refused calls (C4).** The API records a call refused with `SND-TOOL-001`, `-002`, `-004` or `-006`
  under §A.1's refusal rule. It writes a `refused` row and a `step.tool-called` event, with no result and no
  `step.tool-started`, through the `IBlockCallLog.RecordRefused` port (R1-14).
  - That is a Features service the API calls directly. It is not the `RecordEvent` activity.
  - Refused calls don't count against the call budget. They have their own cap per run, equal to the budget (B.4).
    Past it, one `step.tool-refusals-capped` event marks the cut.
  - `SND-TOOL-404` refusals have no run. They go to the structured log only (scrubbed; R1-39), never to the ledger.
- **Using the calls.**
  - A step report may cite calls in `calls: [<callId>, ...]` (§A.4). Validation (ENG §5.5) refuses an id that isn't
    a recorded `ok` or `failed` call of this run, or that is `duringTakeover`, from the workflow's run call state
    (B.2).
  - The run's calls, read from `block_calls` for the app (§A.4), the Judge's facts for a later route (DT §8.3) and
    `workflow why` (DT-J3), are every call of the run in seq order. Calls the report didn't cite are marked
    uncited, so a report that leaves out a failing call can be seen.
- **Visibility.** Call events have the stream's usual visibility, a plain call's included (C10).
- **Telemetry** is §A.4's, in one place.

### B.4 Time and budget

- **Sync or async is the block's own,** and the wait and the answer follow §A.1's timing rule. No P6 job is
  involved: jobs don't survive a restart, and a session can't start one (R1-07). Every callable block is bounded,
  which the catalog test enforces (B.1).
- **The per-run call budget** is `workflow.calls.perRun` (default 50, with a row in `docs/csharp/sindri-profile.md`).
  Past it, a call is refused with `SND-TOOL-004`. A poll never spends it (§A.1).
  - A block with the `Budget` capability also spends the run's ENG §3.7 budget. An over-budget run parks with
    `budget` after the call ends, never in the middle of one.
- **Concurrency.** One call in flight per run (`SND-TOOL-006`). Plain calls: `workflow.calls.plainConcurrent` per
  repo (default 2, §A.6), then `SND-TOOL-006`.
- **The state's clock keeps running (R1-33).** Calls don't pause the state's `timeout`, so a long call can use up the
  Step's time. That is by design: the recipe's timeout bounds the whole Step, calls included.

### B.5 Calls outside a Step

The owner decided (2026-10-18, C3) that their own sessions, outside any Step, may use the block tools as well. A call
like this has no instance and no run. It never moves anything, and it is never proof for a work item.

The owner then widened that (2026-10-18, C6): "my sessions can use every block. no matter what. if i'm steering it's
high priority and sensitivity." And (C11): "sindri can know if the session is owned by a sindri workflow. otherwise
it's me". So a session Sindri launched for a workflow is a Step session, which §A.2 defines by its valid
`X-Sindri-Run`. **Every other session is the owner's**, team lanes included, and gets the same treatment. Its calls are
treated exactly like a Step's calls (C10: the same visibility, the same reads), only marked as the owner's.

- **Which blocks.**
  - A plain session gets **every Callable block**, `Network` and `Budget` ones included. There is no default subset.
  - What stays is structural. Only `gate` and `evidenceProvider` blocks are Callable (B.1): a Step, an effect or an
    input fill means nothing as a call.
  - A required parameter that the session can't set and that has no default must be pinned by a recipe's
    `{block, with}` or by `workflow.tools.plain`'s `{block, with}`. A plain session has no recipe, so without a pin in
    `workflow.tools.plain` the block can't be called: `sindri_list_tools` names it in `omitted` with reason
    `needs-recipe-pin`.
  - `workflow.tools.plain: [<block id> | { block, with }]` is optional, at box level or under a `repos[]` entry. It
    supplies pins and can narrow the list. `ProfileRules` checks each entry, as RCP-TOOL-001 and RCP-TOOL-004 do.
  - A Step session never sees this list: a Step keeps only its state's `tools:`.
- **Where it runs (C7).** A `ReadsCode` block runs in a fresh detached scratch worktree under
  `$AW_SINDRI_HOME/calls/worktrees/<id>/` (a server-made GUID v7), at the commit `ref` names (§A.1, §A.6), removed
  with `git worktree remove` when the call ends. It never runs in the shared checkout or in the session's own
  folder. A block that doesn't read code gets no checkout (`BlockContext.Worktree` null) and takes no `ref`.
  - **`ref` is checked once:** `git rev-parse --verify --end-of-options <ref>^{commit}` in the registered repo must
    give back the same sha. Only that sha is used after that (the worktree, the record, the answer's `commit`).
  - The policy is fixed: no `StepPolicy`, the scratch worktree as the only writable path, and the approved profile.
  - B.2's environment, denylist and heavy-slot rules apply unchanged. `dirty` and `changedDuringCall` are always
    false.
- **Who runs it.** `BlockCallRunner`, a hosted service in the Sindri host. It is a bounded queue that runs
  `workflow.calls.plainConcurrent` calls at once per repo (default 2, B.4), each through the same `RunBlockCall`
  body as B.2. Its `BlockContext` has no instance, run or `StepPolicy`.
  - The API claims the row as for a Step call (§A.1 step 9), then enqueues it. The calls live in `block_calls` with
    scope `repo:<name>`, and their bodies in `block_call_bodies`.
  - The stored-call lookup, the poll, `SND-TOOL-409` and the restart sweep work as in §A.1 and B.2. On a restart, a
    `waiting` or `running` row ends `failed`, reason `host-restart`, and its scratch worktree is removed. No
    Temporal and no P6 job are involved.
- **High priority (C6).** A plain call takes its slot through `IHeavySlots.Take(priority: High)` and gets the next
  free slot before anything else that waits:
  1. plain calls, ahead of meta's merge-train reservation too. Meta decided this on 2026-10-18; it is reversible and
     has been noted to the owner;
  2. meta's merge trains, which otherwise keep their reservation rule;
  3. transition gates;
  4. Step calls and agent work.

  Nothing is pre-empted: a running gate step or build finishes, and the owner's call takes the next free slot.
  §A.1's timing rule still holds: a sync call waits at most 30 s for a slot, then answers `running`.
  - **Against the box wrapper's jobs.** Merge trains and agent builds wait through the wrapper, outside Sindri, so
    there is no shared queue. While any `High` call waits, Sindri writes the count of waiting `High` calls to
    `$AW_STATE_DIR/heavy-owner-waiting` (removed at zero, and on start). The wrapper treats it like
    `heavy-meta-reserve`: that many free slots go to Sindri first. That wrapper change is meta's: the wrapper lives
    on the box, outside the repo, so it is a dependency of BM-1 (§D), not part of it. Until it lands, a plain call is
    first among Sindri's waiters only.
  - **Gates and trains always get a slot.** Plain calls hold at most the box cap − 1 slots box-wide (the cap from
    `heavy-slots-cap`, B.2). With a cap of 1, a plain call waits for a free slot and holds the only one, as before.
  - The memory guard is never skipped (B.2): `High` changes who goes next, not whether a slot is safe.
- **How it is recorded.** On the repo's stream, with B.3's idempotency keys and B.3's visibility:
  `session.tool-started` (`callId` and `block` only) when it starts, and `session.tool-called` when it ends. The
  latter has B.3's fields without `instance`, `run` and `launch`, plus `caller: owner`, the `commit` and `spent`.
  - When the call passed an `itemId`, both events' ledger `item` is that item, so they reach its Timeline (§A.4's
    "plain call … not proof" line).
  - Results and bodies are read as a Step call's are (§A.4). Spans carry `reason` as for a Step call (§A.4).
  - A sweep deletes plain calls' rows and their bodies after 30 days (C8). Each body is its call's own row (B.2), so
    the sweep never removes another call's body. The body is scrubbed by `Aw.Redaction`, as every result is.
  - Refused calls are recorded the same way (C4).
  - Calls are counted and shown per repo: `sindri ledger --type session.tool-called` and the places §A.4 lists.
- **Linking to an item.** A plain call that names an item never satisfies a gate, a step report's `calls:` can't
  cite it, and it is not a fact (C8).
- **Budget and caps ("no matter what").**
  - A `Budget` block called from a plain session spends from no Sindri budget, because there is no run. It reports
    through `BlockContext.Spend` (B.2), the total is the answer's and `session.tool-called`'s `spent`, and it is
    never refused for budget.
  - `workflow.calls.plainPerDay` (per repo per local day, default 200) refuses nothing. Past it, each event carries
    `capPassed: true` and the host logs one notice a day.
  - Refused entries (a bad name or bad arguments) keep their own cap: 50 a day per repo (fixed in v1, as a Step's
    refusal cap equals its default budget of 50). Past it, one `session.tool-refusals-capped` event for that day,
    so the ledger stays bounded.

## C. Owner decisions (2026-10-18)

Every choice was put to the owner in plain words; these are the answers. C3, C6 and C11 are quoted; the rest are
"the other questions are rest as recommended".

1. **Binding a call to its Step:** the Step's id travels in that session's tool settings (`X-Sindri-Run`), with no
   secret. This loosens the 2026-10-16 rule that only the repo header rides MCP (§A.2).
2. **Tool names:** `sindri_list_tools` and `sindri_execute_tool_call`.
3. **Sessions outside a Step get the block tools:** "my own sessions should be able to use sindri block tools"
   (§A.6, §B.5).
4. **Refused calls are recorded** as short entries with no result (§A.3, §B.3).
5. **During a takeover**, calls still run, are marked `duringTakeover`, and never count as the Step's proof.
6. **Every block for the owner's sessions, at high priority:** "my sessions can use every block. no matter what. if
   i'm steering it's high priority and sensitivity" (§A.6, §B.5).
7. **What the owner's call runs against:** a commit named by its full id, in a scratch copy Sindri makes and throws
   away; every answer names the commit.
8. **What the owner's calls count for:** logged and kept 30 days, never proof for an item or a Step.
9. **What a Step's call tests:** the Step's working folder as it is (Sindri's own folder for that phase of the item),
   locked during the call, with the exact code it saw recorded.
10. **Sensitivity:** (b). The owner's calls are treated as agent calls are, marked `caller: owner`.
11. **Who is the owner:** "sindri can know if the session is owned by a sindri workflow. otherwise it's me". Only a
   session with `X-Sindri-Run` is a workflow's; every other session is the owner's, team lanes included.

## D. Phases

Contract first, so the two lanes build in parallel.

| Phase | Lane | Holds | Waits on | Docs it updates |
|---|---|---|---|---|
| **BM-0** | engine | The contract, red: `Callable`, `Description`, `ReadsCode` and the `ParameterSpec` fields (`EnumSource` among them); `tools:` with the RCP-TOOL-001..005 fixtures; `BlockContext` and `GateContext.Checkout`; `IBlockCallStore`, `IBlockCallLog`, the request and response records for `ListCallableBlocks` and `ExecuteBlockCall`, with stub services; the `*.tool-started` and `*.tool-called` records; the typed refusals | P8 (the agent runtime), DT-J2 (`MaxDuration`; draft PR #306, not merged) | none |
| **BM-1** | engine | The validator rows; the catalog test; `gate.tests.green`, `checks.json` read from the base (`fact:change.base`, else the default branch), `needs-repo-config`, `schemas/checks.schema.json`, a `sindri doctor` check that validates every registered repo's `checks.json` and names a repo that has none, and the `deps.yaml` file edge for it (`node scripts/check-deps.mjs`); `sindri_list_tools` with its enum sources and `omitted`; the `block_calls` and `block_call_bodies` migration; the run call state in `RecipeWorkflow`; the `block-call` Update, `MarkCallStarted` and `RunBlockCall` (the runner), with the events' idempotency keys; the heavy slot through `IHeavySlots` with its wait ceiling, the `High` priority for plain calls, the box cap file, the memory guard (never skipped), meta's reserve, the `heavy-owner-waiting` count and the cap − 1 limit on plain calls; the commit, tree, dirty and `changedDuringCall` record; the per-folder lock (calls only); the restart sweep (rows, scratch worktrees, scratch `TMPDIR`s); the environment base and denylist; the bodies and the 30-day sweep; the budget and concurrency keys; `calls:` validation; `BlockCallRunner` for plain calls (`caller: owner`), `BlockContext.Spend` and `spent`; `workflow.review-loop`'s `fix` `tools:` and the recipe test that keeps it through DTC4. Tests: unit (filtering, refusals, schema building, enum sources, path confinement, `invalid-argument`, the denylist, `ref` checking, the filter as one argv value, the plain pin rule); integration (an Update through the time-skipping server, single flight, cancel on run end, takeover (C5), the restart sweep, a Step call in a dirty folder, a folder edited during a call, a slot-wait timeout, a plain call in a scratch worktree, a waiting plain call beating a waiting Step call and a waiting wrapper job, plain calls never holding the last slot, a `Budget` block's `spent` with no run) | BM-0; meta's wrapper change (the wrapper reads `heavy-owner-waiting` like `heavy-meta-reserve`, B.5), owned by meta and landed on the box, before BM-1's priority test can pass against wrapper jobs | `docs/csharp/sindri-workflow.md` (`tools:`, RCP-TOOL rows, a "Block calls" section), `docs/csharp/sindri-profile.md` (the keys), `docs/csharp/sindri.md` (the events, the tables, the doctor check), a `checks.json` reference in `docs/csharp/` (targets come from the base) |
| **BA-1** | api | §A: the two MCP tools, the routes, `X-Sindri-Run` and the launcher's per-launch MCP form change, schema validation, the stored-call lookup, the claim and the 409 check, the refusal recording, the catalog rows, the goldens. Built against BM-0's records and stubs, so it starts the day BM-0 lands. Its gate also runs `Aw.Sindri.Ui.Tests`, which pins the catalog rows | BM-0 | §A's list |
| **BA-2** | api, with engine | §A.5's proof end to end, with a real launched Step session and a plain session | BM-1, BA-1 | the proof log |
| **BU-1** | ui | The calls on a Step (cited, uncited, waiting, running), the plain calls (§A.4), and the app steps of §A.5's proof | BM-1, BA-1 | `docs/csharp/sindri-ui.md` |

BM-1 and BA-1 run at the same time, each lane with its own builders. BA-2 and BU-1 need both, and run at the same
time as each other.

**The v1 signal.** Calls earn more `tools:` entries when, read through `sindri ledger --type step.tool-called`, most
review-loop `fix` runs make at least one `gate.tests.green` call, and fewer `fixed` reports are followed by a red
checks run or new findings in the next round than before calls shipped.

**Follow-ups, not in v1:** the heavy wrapper taking the per-folder lock for the agent's own builds, once the wrapper
is versioned in the repo (provisional default, reversible); transition gates with a checkout, and reusing a call for
one on an exact match (R1-12); a recipe-level default `tools:` (R1-31); per-session attribution for Sindri-launched
sessions (`X-Sindri-Launch`);
a Sindri budget for plain calls' spend, if the owner wants one later.
