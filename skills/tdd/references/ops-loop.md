# The ops loop: comments and @-signals reach a session that answers and acts (D14b, D15)

Design only; nothing is installed (no scheduled task, routine, watch or account change). Facts marked *(contract)* come
from the tool contracts read on 2026-11-08 (`ArtifactComments`, `start_session`, scheduled tasks, the `schedule`
skill's RemoteTrigger notes, runtime 0.2.96 `comments`/`sample`/`mcp`); nothing ran end to end, and what was never
exercised is *(unverified)* (§5). Owner's words: D14 (b, c), D15 in `~/.system-design/house/system-design/tdd-system/status.md`.

## 0 · What exists today *(contract)*

| Piece | What it does | Limit that shapes the design |
|---|---|---|
| Artifact comment threads | viewers leave threads; a writer activates one for Claude by replying with "Send to Claude" or mentioning @claude | only activated threads can be replied to or resolved; plain comments never notify anyone |
| `ArtifactComments` (Claude Code) | `read` lists threads, activated or not; `reply` posts plain text <= 4096 bytes as "Claude · via the user"; `resolve`; `watch` | a reply is refused as a likely duplicate when a Claude reply already stands after every send-to-Claude request (override `acknowledge_duplicate`, never used unattended) |
| Watch and wake | a sent-to-Claude comment wakes the watching session only while its watch row says "auto-replies armed"; a publish arms it when auto-replies are on; `watch` arms it on an artifact the user can edit whose link the user gave in their own message | only a main-loop session (interactive, SDK, background) holds a watch: never a subagent, teammate or print (`claude -p`) session. Ctrl+C/Stop pauses auto-replies until the user's next message; the kill-all-agents gesture disarms them session-wide; after `--resume` in a terminal the replying watches "usually" come back |
| `Agent` subagent | a worker with its own context, model pinned per dispatch | holds no watch |
| `start_session` (desktop app) | a separate main-loop session, `fresh` or `fork`; `wake_when_done` continues the starter | its `model` must be "same family or cheaper" than the starter's ("anything higher is ignored"); permission mode the starter's or lower |
| Local scheduled task | `~/.claude/scheduled-tasks/<id>/SKILL.md`, 5-field cron in local time, a fresh memory-less session per run, may be saved in bypass mode | runs only while the desktop app is open (a missed run fires on next launch) |
| Cloud routine (`RemoteTrigger`) | create (prompt, model, repos, `mcp_connections`), run, `list_runs`/`get_run_log`; `create_webhook_trigger` attaches an event source such as a GitHub event with a filter | cron minimum one hour, in UTC; a cloud session sees no local file, so no packet, no `agents/` lane, no host MCP server (`~/.system-design` is not a git repo); routines cannot be deleted from the API |
| Page runtime 0.2.96 | `comments` (full or `composer_only`), `sample`, `mcp`, `db`, `room`, `user` | see §3 |
| Linear MCP | `get_notifications(unreadOnly)`, `list_issues(updatedAt, query, label)`, `list_comments(issueId)` | `query` searches title and description only, not comments; no webhook trigger here |

## 1 · The shortest working path (today, nothing new built)

One long-lived Claude Code session per project on the Mac mini publishes the TDD and holds the watch.

1. On the Mac mini, sign in to the Claude desktop app with the account that owns the artifacts (the owner's subscription at
   home; the work account on the work machine). Keep the app open.
2. Open one session per project in the project's repo; add the packet root (`~/.system-design/<repo-slug>/system-design/`)
   as a working directory; connect the MCPs answers need (Linear, Notion, the database server); turn comment
   auto-replies on for the session (where that switch sits in the app: *unverified*).
3. Publish or republish the TDD from that session (SKILL.md step 7); the publish arms auto-replies. For an artifact
   published elsewhere, paste its URL in your own message and ask for `ArtifactComments watch` on it: that arms it only
   when you can edit it.
4. Check: `ArtifactComments watch` with no `url` lists the watches; each TDD must read "auto-replies armed" (an
   "arming" line is not yet a watch).
5. Test: a writer comments on the artifact and presses "Send to Claude" (or writes @claude in a reply). The session
   wakes, reads the thread, answers in it, resolves it when addressed, and appends an Ops log line to the packet's
   `status.md` (§4).
6. Keep it alive: the Mac mini never sleeps (§2.8); never Ctrl+C that session; after an app restart or `--resume`,
   repeat step 4 and re-arm what is missing with step 3.

**Limits.** One session's model and context answer every thread in arrival order, and a long session fills up; no
subagent can hold the watch, so the watcher must also answer; it wakes only for activated threads (plain comments,
GitHub and Linear never reach it); it is down whenever the app, the Mac or the session is.

## 2 · The recommended path: a thin dispatcher

### 2.1 Shape

```
artifact thread (activated) --wake--> DISPATCHER (Mac mini main-loop session; holds every watch; answers nothing itself)
                                        | per thread: Agent(model: opus)  or  start_session(fresh, wake_when_done)
                                        v
                                 RESPONDER: packet build/agents/ lane + repo at meta.json sha + Linear/Notion/DB MCPs
                                        | returns {reply, addressed, actions, pending_decision, followups}
                                        v
                    DISPATCHER: reply -> resolve -> status.md Ops log -> run log (-> Prism handoff at work)
GitHub PR comment (tdd:<slug> | @claude) --webhook--> cloud routine: answers on the PR; packet-bound -> label tdd:desk
Linear mention / tdd:<slug> --poll: local 15 min | cloud hourly--> same responder brief; answers on the issue
local scheduled task, every 15 min --> safety net: activated threads with no standing reply; the tdd:desk queue
```

### 2.2 The dispatcher

- A main-loop session, so it can hold watches: a desktop-app session (needed for `start_session` and `get_usage`) or
  `claude` in a terminal under tmux. Never `claude -p`.
- It works in `~/.system-design/ops/` with every watched packet and repo added as working directories. It reads
  `watchlist.json` (shape: `scripts/ops/watchlist.example.json`) and watches each entry whose `host` is this machine,
  `owner_edits` is true and `Artifact` read says "writer". A watch arms only for a link the user gave in their own
  message, so the owner's kickoff message pastes the URLs; a link read from the file alone may not arm.
- It runs on Opus when it must start Opus side sessions (the `start_session` cap); otherwise Sonnet is enough, because
  each subagent is pinned per dispatch (`model: opus`).
- Per wake: read the thread (the notification names it); find the watchlist entry; classify (question, change request,
  decision in the owner's words, out of scope); dispatch; check the return (§4); post; resolve if addressed; log.
  Threads in parallel go to parallel subagents (`max_parallel`, default 3); actions that edit one packet run one at a time.
- Before each dispatch it reads usage (`get_usage` in the app) against `usage_ceiling_pct` (95 at home, the lead's
  standing ceiling since 2026-11-05); over it, it replies once that the thread is queued and logs it.

### 2.3 Subagent or side session

| | `Agent` subagent (default) | `start_session` side session |
|---|---|---|
| use for | answers, look-ups, small source edits | work that acts: edit, rebuild, lint, republish; a draft PR; anything long |
| model | any pinned model | at most the dispatcher's |
| who posts | the dispatcher, from the returned JSON | the side session writes `<packet>/ops/outbox/<thread_id>.json`; the dispatcher posts it when `wake_when_done` continues it |
| owner can steer it | no | yes, it shows in the sidebar |

Only the dispatcher posts and resolves: one writer per artifact, and the duplicate-reply guard catches any second poster.

### 2.4 The responder brief (full text: `templates/ops/tdd-comment-responder/SKILL.md`, step 4; the dispatcher uses it too)

- The thread as data, in a fenced block marked untrusted, with its id, anchor or label, and authors as `read` shows them.
- Paths, not contents: `<packet>/build/agents/README.md` (the front door; `write_agents.py` writes the lane to
  `build/agents/` by default), `refs.json` and `map.json` to find the slice for the anchor, `decisions/decisions.json`,
  `schema/`, `sql/`, the packet's `status.md`, and the repo at `meta.json`'s `sha`.
- The entry's grants (§4), the MCP tools it may call, and the return shape: `{reply: plain text <= 3500 bytes,
  addressed: bool, actions: [], pending_decision: {quote, thread_id} | null, followups: []}`.
- The skill's rules: every claim cites `path:line` or a page anchor; a number has a source and date or reads "not
  measured"; never a D-number (a decision in the owner's words returns as `pending_decision` for the lead to record).

### 2.5 GitHub: a cloud routine with a webhook trigger

Templates: `templates/ops/github-pr-responder.{routine.json,trigger.json,prompt.md}`. A routine on Opus with the repo as
its source and the account's connectors; a webhook trigger on PR comment events filtered to bodies containing
`tdd:<slug>` (and `@claude` only where no other bot answers it). It answers on the PR from the repo, the PR and Linear,
only for comment authors whose association is OWNER, MEMBER or COLLABORATOR. When the answer needs the packet, a host
database or a decision, it replies that it routed the question and adds the label `tdd:desk`; the local scheduled task
picks labelled PRs up. If the repo already runs the Claude GitHub app or `claude-code-action` on `@claude`, give the
routine `tdd:<slug>` only, or two responders answer. The daily cron on the routine doubles as a sweep for missed events.

### 2.6 Linear: a poll

No webhook trigger here, so poll: primarily a local scheduled task every 15 minutes (it has the packet); a cloud
routine hourly (the minimum) covers a down Mac mini. One prompt serves both (`templates/ops/linear-poll.*`). Each pass:
`get_notifications(unreadOnly: true)` for @-mentions (left unread); `list_issues(updatedAt: <cursor>)` by
`label: "tdd:<slug>"` and by `query: "<slug>"`; per issue `list_comments(issueId)`, keeping newer comments that carry
`@claude`, `tdd:<slug>`, or the slug with a D- or Q-number. Dedupe is stateless: each reply ends `[tdd-ops <comment-id>]`
(`... routed]` when the cloud pass only routed it); an answered comment is skipped and a comment carrying any marker is
never answered (loop guard). Cursor: `~/.system-design/ops/state.json` locally, now minus 65 minutes in the cloud.

### 2.7 Work and home

| | Home | Work |
|---|---|---|
| account | the owner's own subscription | the work account that owns the work artifacts |
| host | the Mac mini (this Mac while you are at it) | the work machine, with agentic-workflow installed (`setup.sh`) |
| signal in | wake, scheduled task, routines | the same; plus Prism: the session-start hook loads `session_load_context` for the repo slug (it must equal the Prism project name), and each responder brief carries that context |
| result out | reply, resolve, Ops log, run log | the same; plus `session_save_handoff` after each handled signal; messages to the lead go by `SendMessage`, which judge's send-gate may batch into `judge/outbox/<sid>.jsonl` until the next urgent send |
| PR review comments | the responder | review findings route to `/addressReview` (its source of truth is `$AW_DIR/reviews/<pr>.json`, `AW_DIR` set by `skills/_shared/repo-slug.sh`), not to a second responder |
| later | (none) | Sindri's standing workflows (webhooks as signal-with-start; README X59(b): planned, not built) replace the routines and mirror every transition into Sindri's ledger; Sindri's own Linear source is read-only, so writes stay with the responder |
| guards | the usage ceiling, memctl | the same, plus agentic-workflow's hooks (`block-destructive`, `block-push-main`, `detect-secrets`), which apply to the responder too |

### 2.8 Mac mini or this Mac

| | Mac mini | This Mac (Mac17,7, a laptop) |
|---|---|---|
| always on | yes, once the owner runs `sudo pmset -a sleep 0 disksleep 0 autorestart 1 womp 1` (a system setting: the owner's step) | no: on 2026-11-08 `pmset -g` showed `sleep 1`, held off only by Claude's power assertion; lid close and travel stop it |
| power cut | `autorestart 1` boots it, but with FileVault on an unattended boot waits at the unlock screen; plan restarts with `sudo fdesetup authrestart` | not a host for this |
| sign-in | the app's credentials sit in the login keychain, which only a GUI login unlocks; a headless or ssh `claude` needs a token instead (`claude setup-token`, *unverified*) | unlocked while you work |
| permission mode | default plus a narrow allowlist (`ArtifactComments`, the Linear and Notion tools, `Read`, scoped `Bash(gh *)`); bypass only for a macOS user that holds no work credentials | your interactive sessions' mode |
| memory | the dispatcher and its subagents only | shares the lead's memctl budget with Unity lanes |

Exactly one machine arms a given artifact (`host` in the watchlist), so two dispatchers never answer one thread.

## 3 · What the page can do with no session at all *(contract unless marked)*

- **`sample`, the in-page chat.** The page asks Claude on the viewer's own account; each call is memory-less (the page
  keeps the turns); its tools are page functions whose `execute` runs in the page. The first call asks the viewer's
  consent; cost is the viewer's usage; a call with tools is several paid rounds (commonly 30-90 s on the default tier)
  and never cached; it cannot browse. The TDD page already carries `content.json` inline, so its tools read locally:
  `getSection(id)`, `getDecision(id)`, `getTable(name)` (columns, sample rows, measured load), `findRefs(token)`. That
  answers "what did D7 decide" or "which PR adds this column" with no session. The table chat of D14 (c) is this chat
  scoped to one table card.
- **`mcp`, the viewer's connectors.** The page calls connectors by display name (`callTool("Linear", ...)`, `watchTool`
  for live reads), each with its own consent, on the viewer's credentials; the page never sees a token. `host:<name>`
  reaches an MCP server on the viewer's device through the desktop app: only in the app, only for the artifact's owner
  for now, and a call not annotated read-only may ask the viewer to confirm; elsewhere every `host:` call fails
  `server_not_connected`, so the page always renders a fallback. A `sample` tool whose `execute` calls `mcp.callTool` is
  how the table chat would read live rows (*unverified*).
- **`comments` with `composer_only`, the entry point.** `openComposer({element} | {range})` opens the shell's own
  composer from a viewer gesture: no consent prompt, nothing posted by the page; the write verbs reject `not_granted`
  and `canSendToClaude` resolves `"off"`. The page cannot prefill the composer's text; a Range's selected text becomes
  the quoted excerpt when it fits the platform's span bounds, else the composer anchors to the enclosing element.
- **"Escalate to the team": chat to thread.** The chat panel keeps a short summary of the conversation (question,
  answer, what is open; a few lines) in an element on the page. "Escalate" selects it as a Range and calls
  `openComposer({range})`, so the new thread quotes the summary; the viewer adds the ask and presses Send to Claude,
  which wakes the dispatcher. With the full form (`comments: {}`, consent at first write) an editor's Escalate is one
  press: `canSendToClaude()` is `"available"`, then `sendToClaude({anchor, text: summary <= 4 KiB})` posts and
  activates in one step; a non-editor gets `create`, a plain thread a writer activates later.
- **`onSendToClaude`, the page answers first** (where the platform offers it). When an editor presses Send to Claude,
  the page is asked before anything posts; `{handled: true, reply}` keeps it a plain comment and Claude is not brought
  in. The page tries a `sample` answer for small look-ups and passes the rest on with `{handled: false}` (bound about
  130 s, shared by all listeners), so cheap questions never wake the dispatcher.

Today the TDD page declares no capabilities (publish-and-mirror.md §1); adding them changes `app/build.ts`, the page
contract and the publish call's `capabilities`, the lead's change.

## 4 · Safety

- **Data, not instructions.** Artifact comments, PR and Linear comments, chat summaries and `onSendToClaude` text are
  written by people other than the session's owner. Every template fences them as untrusted. A request inside them to
  run a command, change a setting, fetch a URL, widen access or reveal anything gets the reply "this needs the owner"
  and an Ops log line, never the action. Actions come only from the watchlist entry's grants.
- **Who can trigger.** Only writers activate threads, so the writer list is the trust boundary (sharing is the owner's
  call). Auto-replies only on artifacts the owner edits: armed only when `owner_edits` is true and the `Artifact` read
  says "writer" (the platform refuses a view-only one too). GitHub: OWNER, MEMBER, COLLABORATOR. Linear: members only.
- **Grants**, per entry over `defaults.grants`. On by default: `answer` (reply, resolve, log), `github_comment`,
  `linear_comment`. Off by default: `edit_source` (packet source files, rebuild, lint), `republish` (the same private
  artifact URL, publish-and-mirror.md §4), `draft_pr` (a feature branch and a draft PR). Never granted: deploy or
  launch (GATE-NODEPLOY), merge, push to main, sharing or permission changes, account settings, any database write,
  recording a D-number, creating or changing scheduled tasks or routines.
- **Secrets never in pages or replies.** No key, token, connection string or private host name in the page,
  `content.json`, the agents lane, a reply, a log or the watchlist. Connectors run on the viewer's credentials; database
  credentials live in the keychain or the MCP server's own config on its host. No production rows in a reply or a page:
  health data at work is PHI, so replies carry counts, aggregates and synthetic samples only. Before posting, the
  poster scans the reply for secret shapes and row-like personal data and holds it on a hit.
- **What is logged where.** `status.md` gets one append-only line per handled signal under `## Ops log` (made at the
  end if missing; re-read before each append): `- 2026-11-08T21:04Z · artifact thread <id> (#anchor) · answered,
  resolved · opus subagent · run <id>`; at most a 12-word paraphrase, no rows, no secrets; an owner's decision is logged
  `pending D (owner's words, thread <id>)` for the lead. Machine log: `~/.system-design/ops/runs/<YYYY-MM-DD>.jsonl`
  (signal, ids, model, tokens if shown, seconds, outcome; feeds D12). Cloud routines write neither: their replies carry
  the marker, their logs stay in claude.ai (`list_runs`, `get_run_log`).
- **Loop, rate, stop.** Never answer a comment with a `[tdd-ops ...]` marker or shown as "Claude · via the user"; at
  most `max_replies_per_hour` per artifact (default 12); never `acknowledge_duplicate` unattended. Stop switches:
  `watch` with `on: false`; Stop in the dispatcher; disable the scheduled task; pause the routine at claude.ai/code/routines.

## 5 · Unverified (top three first)

1. **The GitHub webhook trigger.** `create_webhook_trigger`'s body (event names, filter syntax), how the event payload
   reaches the routine's prompt, whether a webhook-only routine may omit `cron_expression` (create requires exactly one
   of `cron_expression`/`run_once_at`), and whether the Claude GitHub app must be installed on the repo. `RemoteTrigger`
   is not loadable in the session that wrote this; `github-pr-responder.trigger.json` guesses its field names.
2. **Replies from a session that did not publish.** Whether a scheduled-task run, a side session or a restarted
   dispatcher can `reply` and `resolve` on an artifact it only read; whether a scheduled-task run counts as a main-loop
   session and its prompt as "the user's own message" for arming a watch. The poll safety net depends on the first.
3. **Survival on the Mac mini.** Whether armed watches and wakes survive an app restart, sleep and wake, and a network
   drop (the contract says watches reconnect on their own and replying watches "usually" return after `--resume`), and
   where the auto-replies switch lives.
4. Also never exercised: `Artifact`/`ArtifactComments` and `gh` auth in cloud routine sessions (assumed absent; the
   prompts work without them); the composer's span bounds for a quoted Range; `sample` tools that call `mcp.callTool`,
   and `host:` servers from a TDD page; Linear `save_comment` threading (schema not loaded); the scheduled task's
   minimum interval and whether bypass mode keeps its MCPs; `claude setup-token` for a headless Mac mini sign-in.

## 6 · D14 open items: data sources for table views and their chat

Options, each with what it needs. The shortest path today is the local database through a host MCP server in the
desktop app: no account, no compliance step. No other recommendation is made here.

| Option | For | What it needs | Who and what can reach it |
|---|---|---|---|
| Work production DB via a host MCP server | work | a read-only database MCP server in the work desktop app (`host:<name>`), a read-only role on a replica, credentials in the keychain; a compliance sign-off (PHI); only aggregates into pages and replies | the artifact owner, in the desktop app only *(contract)*; the local dispatcher; never cloud routines |
| Local database | work and personal | Postgres in Docker seeded from the repo's migrations; the same kind of host MCP server pointed at localhost | the owner's machine and its dispatcher; data only as real as the seed |
| "Grok bot at home" seed and sample generator | work (library-systems focus optional) | the owner's definition of the tool (Q: which model or service "grok bot" names); a generator that reads `schema/<table>.json` and writes synthetic rows labelled `synthetic, <generator> <version>, <date>` into the local database or a table card's sample rows; library-systems domain rules (branches, collections, loans, holds, patrons) | wherever its output lands |
| Render Postgres | personal | a Render Postgres instance and a read-only role, its URL in the keychain; a host MCP server for the page, or a claude.ai connector for routines if one exists (*unverified*) | the owner; cloud routines only through a connector |

Still missing from D14: the sentence that begins "For the table se" (status.md D14 (d)).

## 7 · Files and installing them (the owner's steps)

| File | What it is |
|---|---|
| `scripts/ops/watchlist.example.json` | the watchlist shape with field notes; copy to `~/.system-design/ops/watchlist.json` and fill it |
| `templates/ops/tdd-comment-responder/SKILL.md` | the local scheduled task: answers activated threads with no standing reply, the `tdd:desk` PR queue, logs; holds the responder brief |
| `templates/ops/github-pr-responder.routine.json`, `.trigger.json`, `.prompt.md` | the cloud routine body, its webhook trigger body (*unverified* fields) and its prompt |
| `templates/ops/linear-poll.prompt.md`, `.routine.json` | one poll prompt: a second local scheduled task in LOCAL mode (primary, 15 min) and the hourly cloud fallback routine |

Installing is the owner's call, one step at a time: (1) copy and fill the watchlist; (2) on the Mac mini, create the
scheduled task (`create_scheduled_task`, cron `*/15 * * * *` local, prompt = the template body); (3) start the
dispatcher (§2.2) and check its watch listing; (4) create the GitHub routine, attach its trigger, test with one
`tdd:<slug>` comment on a draft PR; (5) create the local Linear task, then its cloud fallback. Log each step in `status.md`.
