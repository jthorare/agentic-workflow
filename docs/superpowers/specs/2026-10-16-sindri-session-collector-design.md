# Sindri session collector: every live agent session on the box, its state, its host, and focus

**Status:** design, revision 2.7 (rev 2.7 records what CO-1 shipped, §15; rev 2.6 records what CO-3 shipped, §15; rev 2.1 approved; rev 2.2 adds the live read-only preview, CA-9, Q4; rev 2.3 the #324 /autoplan follow-up, §15; rev 2.4 its round 2; rev 2.5 its round 3) · **Date:** 2026-10-16, rev 2.2 2026-10-17, rev 2.3 to 2.7 2026-10-18 · **Owner decisions:** 2026-10-16
(§1, CO1), 2026-10-17 (the preview, Q4) · **Builds on:** the dashboard design `2026-10-09-sindri-dashboard-design.md` (DB), whose collector (§4), hook
spool (§4.2), states (§4.3) and attach (§7) this doc rewrites for the C# app; the API surface spec
`2026-10-10-sindri-api-surface-design.md` (API), §3, §4, §5, §9 and §11 ("these need their own spec once the collector
is designed in C#"); the UI screens spec `2026-10-13-sindri-ui-screens-design.md` (UI), §4.5 Agents, phase U11, and its
`fixture.md` vocabulary; the session launcher spec `2026-10-15-sindri-session-launcher-design.md` (LCH), rev 3.1, and
`docs/csharp/sindri-launcher.md` (the `launcher.json` contract); `docs/csharp/meta.md` (aw-meta's lane map); README rows
X49, X55 and X59(c) · **Lanes:** the ideas lane builds the collector, its hook and its routes; the API lane reviews the
API rows, takes the shared launcher reader over into SL-2 and builds the viewer-backed focus and the launch join; the
UI lane builds U11a; the engine and context lanes have no work here (§10) · **Next:** build (CO1), phase by phase
(§11).

The owner asked (2026-10-16): "when can Sindri show my active Claude Code sessions, behind tmux or not?" The UI's Agents
page (U11) is drawn, but its data source, the collector, has no C# spec: API §11 and README X49 and X59(c) defer it here.
The dashboard design of 2026-10-09 has the goal right ("agents that Sindri didn't start", DB §1) and was written for the
TypeScript `sindri fleet` that was never built. This doc is that collector, rewritten for the Sindri app:

1. **Discovery** of every live agent session on the box, whoever started it: Claude Code from its own session records
   plus the process table, Codex and Cursor from the process table and their hooks, and what each can't show (§4).
2. **State**: `running`, `asking` or `idle`, from the agent's own record where it has one and from a per-session state
   file the `aw:fleet` hook writes (§5).
3. **Host and focus**: which tmux server, terminal or app owns the session, and how a click brings it forward, with the
   cases that can't be done said plainly (§6).
4. **API rows** (§7), **UI rows** (§8), the **configuration seams** (§9), **lanes and phases** (§10, §11).

**Short answer to the owner's question** (§11.2): Claude Code sessions on Linux, in tmux or not, show in
`sindri agents` and `GET /sindri/sessions` after phases CO-0, CO-2a, CO-2b and CO-3, which wait on nothing outside the
ideas lane and nothing that isn't on main today; in the app after U11a. None of it waits on API P10a.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| CO1 | **Build pre-approved** (owner, 2026-10-16): built once this spec is /autoplan-clean, with no separate review round. The spec PR is marked ready and merged once /autoplan-clean, and the owner reads it on main | The owner asked for the feature directly; the review loop is /autoplan's (§13) |
| CO2 | **The collector is a lease-gated hosted service inside the Sindri process** (`Host/Server/SessionCollectorHost`, the `ProfileWatch` pattern: it runs only while `HeldLease.Current` is set), not a CLI that shells out (DB §3 amended by A §17.29). It polls every 2 s, keeps an in-memory snapshot, and serves it through queries. It never writes into an agent's own folders | One process, one API; the app reads the API only (UI §4a) |
| CO3 | **Live state is not ledgered.** `GET /sessions` reads the in-memory snapshot, a named exception to "queries read `views.db`" recorded in `docs/csharp/sindri.md` (as API P13's `GET /engine` reads Temporal live). The ledger gets one public event type, `fleet.changed`, for a session appearing (after two passes in a row), going, and entering `asking` (only once it has lasted 5 s, at most 6 per session per hour). `running` ↔ `idle` flips and leaving `asking` are never ledgered. Written by an internal `RecordFleetChanges` service with the lease grant, with no command gate, audit row or catalog row (the `CheckProfileService` pattern). Expected volume on the owner's box: 6 lanes × (1 appear + 1 go + up to ~40 asks) ≈ 250 rows a day in default permission mode, near 12 in bypass mode | A flip per turn per session would write thousands of permanent rows a day for ephemeral facts. Appear, go and "asking" are what a client must not miss; leaving `asking` is visible on the next poll |
| CO4 | **The code lives in Sindri, written new from aw-meta's, not shared.** `Aw.Sindri.Core/Sessions/` holds the records, pure rules and ports; `Aw.Sindri.Infrastructure/Sessions/` the `/proc`, record, state-file and tmux adapters. aw-meta's `DirectorySessionDirectory` and `ProcProcessTable` stay where they are | Meta is tier 2 and Sindri tier 3; a shared package would be a new tier-1 package with Core, Infrastructure and a compose edge (`deps.yaml`, csharp.md: Infrastructure may use tier-0 packages only) for about 150 lines, one copy of which retires when Sindri absorbs Meta (`docs/csharp/meta.md` "Sindri absorbs it"). Tier 0 is domain-free, so a kernel-level home is out. The new code also fixes a bug: aw-meta treats a record as live when `/proc/<pid>` exists, without checking `procStart`, so a recycled pid passes (CI-9 files the fix for Meta) |
| CO5 | **A session is live only when its process is live and is the same process.** For Claude Code: `/proc/<pid>/stat` field 22 (start time in clock ticks) equals the record's `procStart`, which Claude stores as a JSON string (DB §4.1). For a process found without a record: the pair (pid, start ticks) is its identity | Pids are recycled; a stale `sessions/<pid>.json` must not show a ghost |
| CO6 | **The hook writes a state file, not a log.** `aw:fleet` (`config/hooks/fleet-state.sh`) replaces `$AW_FLEET_DIR/<provider>/<session>.json` atomically (`mktemp` in the same directory under `umask 077`, then `mv`) on each mapped event. One file per session, last write wins, no rotation | DB §4.2's append-only spool needed size rotation, and two writers rotating a shared file race. The collector only needs the latest state. A state file is bounded by construction and pruned by one owner (the collector, §5.4) |
| CO7 | **The hook stores no free text.** It writes the event name, the derived state, the tool name for an approval (`Bash`, never `tool_input`), the Claude notification type (never its `message`), the cwd, the transcript path (a path, never its content), and the agent's pid and start ticks. Nothing in the file needs scrubbing | A hook that holds no prompt or tool text can't leak one; DB §4.2's "short scrubbed detail" is dropped |
| CO8 | **Focus only brings a live session forward; it never starts, resumes, types into or stops one.** It focuses the terminal that already shows the session, or, for a tmux session no terminal shows, opens the configured viewer on `tmux attach` (through LCH's `ITerminalViewer`, CO-4b). An ended session offers its resume command to copy (humans only), never a server-side resume (DB §7's rule "never resume a live session" kept). The action is called **Focus**, not Attach: U11's Attach on a launched row stays `OpenItem:attach` (joins the session; the first keystroke is a takeover) | Two processes writing one session corrupt it (DB §7). One word per action keeps the launched row's Attach (takeover semantics) apart from this one |
| CO9 | **The collector never changes a tmux server.** Its only tmux commands are `list-sessions`, `list-panes`, `list-clients` and `display-message -p`, plus, from CO-7a, the read-only `capture-pane -p` of the preview (CA-9), always with `-S <absolute socket path>`, always with `TMUX` and `TMUX_PANE` removed from the child's environment, each bounded by a 1 s timeout that kills only that child; a socket that timed out is skipped for the rest of its 10 s window (the preview keeps its own mark, CA-9; the pass never reads it). No `kill-*`, `send-keys`, `new-session`, `select-*`, `switch-client` or `set-*`, on any socket, the default one included. The one other tmux argv, `tmux -S <socket> attach -t '=<session>'` for the copy line and the viewer (§6.2), is built in exactly one place and never run by Sindri itself. An architecture test pins both (CI-5). `capture-pane` is a read: it never changes the server | The team-socket hazard (2026-10-15: a proof that inherited `TMUX` killed the team): reading the default server is safe, changing it is not |
| CO10 | **Focus is `desk`, human only, and answers synchronously.** `POST /sessions/{provider}/{id}/focus` is refused to `session` callers with `SND-AUTH-002`; it needs no terminal (the app may call it). Its service tries the focus (≤ 3 s, inside the command gate's 5 s), appends one non-public event, `fleet.focused`, and returns the outcome with the copy command. The query routes are open to every caller, repo-scoped and with paths, titles, panes, pids and resume commands hidden from `session` callers (CA-1), except the preview (CA-9), which is human-only: the endpoint checks the caller kind the auth layer already resolves (as `FocusSession` does) and refuses every caller that isn't the human token, deny by default (`SND-AUTH-002`). The preview has no MCP tool and no CLI verb, so agents never get one | Focus acts on the human's desktop; an agent has no reason to raise windows, and a session that knew a teammate's pane could act on it. A synchronous answer needs no worker, no replay rule after a restart, and gives the app the copy line directly |
| CO11 | **Linux first.** Discovery, state and focus ship on Linux (`/proc`). macOS gets a `ps`-based process table and its own focus adapters in a later phase (CO-5); until then the collector reports `SND-SESSION-002 unsupported platform` on macOS and the app shows that sentence | aw-meta is Linux only for the same reason; the owner's box is Linux |
| CO12 | **Sessions in a workflow are grouped under it; the rest are "outside a workflow".** A session whose environment carries `AW_SINDRI_LAUNCH` joins its launch (API P10a's `launches` projection) and through it its item and instance (U11's recipe-state groups). Every other session sits in the "Outside a workflow" group, which U11a builds before P10a exists | Answers the owner's question without waiting on P10a, and leaves U11's drawn groups unchanged |
| CO13 | **The agent catalog is the provider list.** A provider is an `AgentId` from LCH's catalog (`claude-code`, `codex`, `cursor`, or an added entry); the entry's `processName` and the built-ins' known executable paths find its processes, and `transcripts` its transcript. CO-2a builds the minimal C# reader of `config/launcher/builtins.json` and `launcher.json` (`AgentId`, `AgentCatalog.Resolve`/`All`, `LauncherConfigFile`), which SL-2 extends with `BuildArgv` and SL8 rather than writing a second one (CA-0) | No second list of agents; a teammate's aider shows up from one `launcher.json` entry. The reader is needed now, and SL-2 hasn't started |
| CO14 | **State words are the UI's chips.** The API's states are `running`, `asking`, `idle` and `unknown`; the UI shows them as `● RUNNING`, `◐ ASKING`, `○ IDLE` and `○ UNKNOWN`, added to `fixture.md`'s vocabulary in U11a (CU-6). `asking` is in ink, not the attention colour, until the owner answers Q3 | `fixture.md`: "no page invents a word", "waiting on you" is retired, and `--attn` means needs-you only (R8-13) |

## 2. Terms

- **Session:** one live agent CLI process and its conversation, named `<provider>/<id>` (the **ref**). `provider` is an
  `AgentId` (LCH SL3); `id` is the agent's session id, bounded by LCH L-9's generic pattern
  `^(?!\.)(?!.*\.\.)[A-Za-z0-9._:-]{1,128}$`. A process whose id can't be learned gets `pid-<pid>-<startTicks>`, which
  fits the pattern and is stable for the process's life.
- **Agent process:** the process running an agent CLI, matched by §4.1 step 2.
- **State:** `running` (a turn is running), `asking` (the agent waits on the human: an approval, a question or a
  login; with `reason` `approval`, `input` or `auth`), `idle` (the turn ended; the human's turn), or `unknown`.
- **State source:** `record` (Claude's `sessions/<pid>.json`), `hook` (the state file), `transcript` (only the
  transcript's modification time; the row says `estimated`) or `none`.
- **Host:** what owns and shows the session (§6.1): `tmux` (with its socket), `warp`, `terminal`, `ide`, `ssh`, `t3`,
  `unknown`.
- **Outside a workflow:** a live session that isn't a Sindri launch (CO12).
- **Fleet dir:** `$AW_FLEET_DIR`, default `$AW_STATE_DIR/fleet`, owned by Sindri (it prunes), written by the hook (§5).
- **Snapshot:** the collector's latest pass: every session, its state and host, `observedAt`, and one `problem` row per
  source that failed.

## 3. What exists today (2026-10-16, origin/main)

| Piece | State | Where |
|---|---|---|
| Claude Code session records | Written by Claude Code itself, one `<pid>.json` per live session, plus a `<pid>.<hash>.key` beside each that the collector must never open. Fields seen in v2.1.301: `pid`, `sessionId`, `cwd`, `startedAt`, `procStart` (a string), `version`, `kind` (`interactive`), `entrypoint` (`cli`), `tmux` (`<session>:@<window>.%<pane>`, with no socket), `name`, `nameSource`, `status` (`busy`; aw-meta also reads `idle`), `statusUpdatedAt`, `updatedAt`, `pidDomain`, `messagingSocketPath`, `bridgeSessionId`, `peerProtocol`, `peerFeatures`. Undocumented and versioned by Claude Code, so read defensively (aw-meta's rule), and only the fields the collector uses are parsed | `~/.claude/sessions/` |
| aw-meta's reader | `DirectorySessionDirectory` (pid, sessionId, name, status, cwd; live when `/proc/<pid>` exists, no `procStart` check), `ProcProcessTable` (`/proc/<pid>/stat`, `cmdline`), `ProcFormat.Stat` (comm-safe parse), `ProcessScanner` (cwd and open files, `lsof` on macOS) | `src/Meta/Aw.Meta.Infrastructure/Files/` |
| Sindri's own process reading | `IProcessAncestry` / `PosixProcessAncestry` (parent chain; notes that one Claude release's kernel name was its version file) and `PinnedProbe` (matches Claude by argv[0] basename or the exe path part `/claude/versions/`); `GitRepoLocator` (repo resolution, spawns `git rev-parse`) | `src/Sindri/Aw.Sindri.{Core,Infrastructure}/Control/`, `Host/Commands/Probe/PinnedProbe.cs` |
| Codex | No per-process record. Transcripts `~/.codex/sessions/YYYY/MM/DD/rollout-<ts>-<uuid>.jsonl`, first line `session_meta` with `id`, `cwd`, `cli_version`, `originator`, `source`. Hooks: `PermissionRequest`, `PostToolUse`, `SessionStart`, `SessionEnd`, `UserPromptSubmit`, `Stop`, `Interrupt` and others, run only after `/hooks` trust. On the owner's box `codex` is an npm wrapper (node) that starts the native binary; which process to match is CO-S's question | `~/.codex/`; `config/hooks/adapters/README.md` |
| Cursor | The CLI's catalog entry has `bin` and `processName` `agent` (`config/launcher/builtins.json`); it has hooks (`sessionStart` with `conversation_id`, `beforeSubmitPrompt`, `postToolUse`, `stop`). The Cursor IDE's agent chats run inside the IDE's one Electron process and have no process of their own. Not installed on the owner's box, so nothing here is verified against a live session | `config/hooks/adapters/README.md` (documented) |
| Session id hook | `session-id-report.sh` writes a Codex or Cursor session id for a Sindri launch or a Meta lane (SL-1b, merged #269) | `config/hooks/session-id-report.sh` |
| Launcher contract | `launcher.json`, `schemas/launcher.schema.json` with `x-aw-keys`, `config/launcher/{builtins,terminals}.json`, the bash reader (SL-0 #257, SL-1 #261 merged). No C# reader yet; the C# ports (`ISessionHost`, `ITerminalViewer`, SL-2) are in API P10a, which hasn't started; `deps.yaml`'s Sindri → Launcher edge is planned | `docs/csharp/sindri-launcher.md` |
| The owner's live layout | Six Claude Code lanes, each in a tmux session on the **default** socket (`/tmp/tmux-1000/default`; the team predates #264's `-L <team>`), started detached by `meta-lane.sh`, each shown in a Warp tab through a `tmux new -A` client. Warp exports `TERM_PROGRAM=WarpTerminal` and `WARP_FOCUS_URL=warp://session/<32 hex>` into each tab's shell. The six agents all carry one **stale** `WARP_FOCUS_URL` inherited through the tmux server, matching none of the clients' tabs, and `TERM_PROGRAM=tmux`. X11 session | read-only `ps`, `/proc`, `~/.claude/sessions` |
| API and UI | P1–P5 merged (the stream, `EventCatalog`, the catalog test); P10a, P11 not started. UI U0–U3 merged; Agents is a placeholder page | `src/Sindri/` |

## 4. Discovery

### 4.1 One pass

Every 2 s (`sessions.pollSeconds`, §9) the collector runs one pass with a 500 ms budget. A source that overruns or
throws is reported as a `problem` row (a fixed sentence per code, never an exception message) and leaves the others
intact (DB §4.1). **When the process table itself fails, the pass keeps the previous snapshot, ledgers no `gone` and
prunes nothing.**

1. **Process table.** Read `/proc/<pid>/stat` for every numeric entry (name, state, ppid, tty, start ticks), skipping
   zombies and processes of other users: the adapter reads `/proc/<pid>/status`'s `Uid:` line (the real uid, its
   first field) and compares it with an own uid injected into the adapter (the process's own in a real run; tests
   pass the fixture's uid), never the owner of the `/proc/<pid>` directory. Keep the parent map (host detection walks
   it, §6.1).
2. **Agent processes.** A process is an agent process when its executable matches a catalog entry, checked in this
   order: argv[0]'s basename (read from `/proc/<pid>/cmdline` **up to the first NUL only**; the rest, which may hold a
   `claude -p` prompt, is never read), then `/proc/<pid>/exe`'s path part (`/claude/versions/` for Claude, as
   `PinnedProbe`), then `comm` (truncated by the kernel to 15 characters, so only a `processName` of ≤ 15 characters
   matches there). Cursor's built-in name `agent` matches only with an exe path containing `cursor`. It is **not** a
   session when any ancestor (≤ 32 steps) is an agent process of the same entry, when its allowlisted environment has
   `AW_JUDGE_CHILD`, or (with no Claude record) when it has no controlling terminal (stat `tty_nr` 0): the toolkit's
   own `claude -p` calls from hooks, judge and scorer are never sessions.
3. **Claude records.** Read every `~/.claude/sessions/*.json` (never `*.key`), at most 256 files and 64 KB each,
   parsing only `pid`, `sessionId`, `cwd`, `procStart`, `kind`, `entrypoint`, `tmux`, `name`, `nameSource`, `status`,
   `statusUpdatedAt`, `startedAt`. A record is live when CO5 holds and either `kind` is `interactive` or the record is
   a T3 Code one (`entrypoint` `sdk-ts` with a cwd under `~/.t3/worktrees`): which `kind` T3's records carry is unverified (no
   T3 on the owner's box), so T3 records are found by entrypoint and cwd regardless of `kind`.
4. **State files.** Read `$AW_FLEET_DIR/<provider>/*.json` (§5); each carries the agent pid and start ticks, which
   identify a Codex or Cursor process the same way a Claude record does.
5. **Environment allowlist.** For each agent process and each terminal process found in §6.1, read
   `/proc/<pid>/environ` and keep only these names: `AW_SINDRI_LAUNCH`, `AW_SINDRI_CHILD`, `AW_JUDGE_CHILD`,
   `AW_META_TEAM`, `AW_META_LANE`, `TMUX`, `TMUX_PANE`, `TERM_PROGRAM`, `WARP_FOCUS_URL`, `KITTY_WINDOW_ID`,
   `KITTY_LISTEN_ON`, `WEZTERM_PANE`, `WINDOWID`, `DISPLAY`, `WAYLAND_DISPLAY`, `XDG_SESSION_TYPE`,
   `DBUS_SESSION_BUS_ADDRESS`. Every other value is dropped as it is parsed and never logged (CO-2a's test plants a fake
   token in a fixture `environ` and asserts it reaches no snapshot, log or response).
6. **Unidentified agent processes.** An agent process with no record and no state file is still a session:
   `<provider>/pid-<pid>-<startTicks>`, cwd from `/proc/<pid>/cwd`, state from §5.3.
7. **Repo.** Resolved per real cwd (§4.3), cached: a cwd is resolved once, off the pass, and the row gets its `repo`
   on the next pass; the cache drops an entry when the approved profile changes or the directory is gone.
8. **Ended sessions.** A session absent from a good pass moves to a 10-minute "recently ended" list with its resume
   command (CO8) and is then dropped.
9. **Restart.** At start the collector reads the ledger's `fleet.changed` rows of the last 24 h to rebuild the set it
   last reported, so a restart ledgers `appeared` only for new sessions and `gone` for ones that ended while it was
   down.

### 4.2 Per provider

| Provider | Live | Id | cwd, title | State | Honest limits |
|---|---|---|---|---|---|
| Claude Code | `sessions/<pid>.json` + CO5 | the record's `sessionId` | the record's `cwd`; title = the record's `name` (scrubbed, ≤ 80 chars, human callers only) | record `status` (`busy` → `running`, `idle` → `idle`), overridden by the hook (§5.2) | `status` is undocumented, and whether it ever reads `idle` at a turn end or changes at a permission prompt is **CO-0's first task** (§11). If it never reads `idle`, CO-3 uses §5.3's estimate until CO-1's hook |
| Codex | the process (§4.1 step 2; which of the wrapper and the native binary, per CO-S) | the state file's `session_id` (SessionStart hook); else, on Linux, a `rollout-*.jsonl` among `/proc/<pid>/fd` links, its uuid; else `pid-…` | `/proc/<pid>/cwd`; no title (the row shows the repo and the id's first 8 characters) | hook only (§5.2, CO-1b); without trusted hooks, `transcript` source | Whether Codex keeps its rollout open between writes is **unverified** (CO-S). Hooks run only after `/hooks` trust; untrusted, the row reads "state estimated: trust the aw:* hooks with /hooks in codex" |
| Cursor (CLI) | the process (exe path containing `cursor`, name `agent`) | the state file's `session_id` (`conversation_id`, mapped by `cursor.sh`); else `pid-…` | `/proc/<pid>/cwd` | hook (CO-1b): `beforeSubmitPrompt` → `running`, `stop` → `idle`. **No `asking`**: Cursor has no hook event for a pending approval or question | Unverified on a live CLI (not installed on the owner's box); the fixtures follow the documented payloads |
| Cursor (IDE chats) | **not discoverable** | — | — | — | The IDE runs every chat in one Electron process with no per-chat process or live record; `~/.cursor/chats/*/<id>/meta.json` is history, not liveness. The Agents page says "Cursor IDE chats aren't shown" in its footer |
| Added entries (aider, …) | the process, by `processName` | `pid-…` | `/proc/<pid>/cwd` | `unknown` | No hook, no record. Shown so they are counted |
| T3 Code | Claude records with `entrypoint: sdk-ts` and a cwd under `~/.t3/worktrees` (DB §4.1), whatever their `kind` (§4.1 step 3) | the record's | as Claude | as Claude | Host `t3`; focus can only copy (LCH §7: T3 owns its processes) |

### 4.3 Repo and launch join

- **Repo:** the cwd's main checkout resolved to an approved profile repo, by API §3.1's rule (git common dir, real path,
  longest match) through the existing `GitRepoLocator`, cached (§4.1 step 7). A cwd outside every profile repo has
  `repo: null`. A worktree keeps its path relative to the repo as `worktree`.
- **Launch:** `AW_SINDRI_LAUNCH` from the allowlist names the launch; the snapshot carries `launchId`, and the API joins
  it to the `launches` projection once P10a has built it (CA-6).
- **Meta lane:** `AW_META_TEAM` and `AW_META_LANE` give `lane: {team, lane}`, shown as "meta · sindri/ideas". Lanes
  started before SL-1's exports (today's) have neither; their row shows the tmux session name (`sindri-ideas`). The
  collector reads no Meta state.

### 4.4 macOS (CO-5, later)

No `/proc`. The process table comes from one `ps -A -o pid=,ppid=,uid=,tty=,lstart=,comm=` call per pass; the cwd from
`lsof -a -d cwd -p <pids> -Fn` (aw-meta's `ProcessScanner` fallback). Another process's environment can't be read on
macOS, so `AW_SINDRI_LAUNCH`, `TERM_PROGRAM` and `WARP_FOCUS_URL` come only from the state file: on macOS the hook also
records those allowlisted names from its own environment. Claude's `procStart` format on macOS is unverified; CO-5
compares the record's `startedAt` to `lstart` within 2 s instead. Focus adapters: `open -a Warp`, AppleScript for
Terminal.app and iTerm2 by tty. Until CO-5, `SND-SESSION-002`.

## 5. State: the `aw:fleet` hook

### 5.1 The file

`$AW_FLEET_DIR/<provider>/<sessionId>.json`, mode 0600, directories 0700 with a `.aw-fleet` marker file, contract
`schemas/fleet-state.schema.json` (2020-12; the bash writer's tests and the C# reader run every fixture in
`tests/fixtures/fleet/` against it, through `node scripts/check-fleet.mjs` in CI):

```jsonc
{
  "schema": 1,
  "provider": "claude-code",            // AgentId, mapped from AW_HOOK_PROVIDER (below)
  "sessionId": "3970893f-…",            // checked against L-9's generic pattern before the write
  "event": "PermissionRequest",         // the event as the canonical hook sees it (the adapters' Claude-shaped name)
  "state": "asking",                     // running | asking | idle | ended
  "reason": "approval",                  // asking only: approval | input | auth
  "tool": "Bash",                        // approval only: the tool name (≤ 64 chars, [A-Za-z0-9_.:-]); never tool_input
  "at": "2026-10-16T11:02:03.123Z",
  "cwd": "/home/…/repo",
  "transcript": "/home/…/x.jsonl",       // a path, or null
  "agentPid": 3023421, "agentStartTicks": 13279367,   // Linux; null where unknown
  "env": {}                              // macOS only (§4.4): the allowlisted names; {} on Linux
}
```

**Provider mapping.** The hook learns its provider from `AW_HOOK_PROVIDER`, which the adapters set to `codex` or
`cursor` (`config/hooks/adapters/common.sh`) and which is unset or `claude` for a Claude hook (Claude runs the script
directly; `install-lib.sh`'s provider name is `claude`). The hook maps it to the catalog's `AgentId`: `claude` or unset
→ `claude-code`; `codex` → `codex`; `cursor` → `cursor`; any other value writes nothing. The `provider` field and the
directory name are always the `AgentId`, so `$AW_FLEET_DIR/claude-code/`, never `claude/`. CO-0's schema (`provider`
an `AgentId` string) and every fixture in `tests/fixtures/fleet/` follow this mapping.

### 5.2 Events per provider

| Provider | Event (as the canonical hook sees it) | `state` | Phase |
|---|---|---|---|
| Claude Code | `SessionStart` | `idle` | CO-1 |
| | `UserPromptSubmit` | `running` | CO-1 |
| | `PermissionRequest` | `asking` `approval` (with `tool`) | CO-1 |
| | `Notification` with `notification_type` `elicitation_dialog` / `idle_prompt` | `asking` `input` / `idle`; **`permission_prompt` and every other type write nothing** (`PermissionRequest` owns `asking` `approval`: the notification arrives seconds after it with no link to a request, so after a quick approval it would turn `running` back into an `asking` nothing clears; rev 2.7) | CO-1 |
| | `PostToolUse` (matcher `.*`) | `running`, written **only when the file's current state is `asking`** (a `grep -q` on the file before anything else; otherwise exit 0 at once) | CO-1 |
| | `Stop` | `idle` | CO-1 |
| | `SessionEnd` | `ended` | CO-1 |
| Codex | `SessionStart`, `UserPromptSubmit`, `PermissionRequest`, `PostToolUse` (only when `asking`), `Stop`, `Interrupt`, `SessionEnd` | `idle`, `running`, `asking approval`, `running`, `idle`, `idle`, `ended` | CO-1b, through `adapters/codex.sh`; needs `/hooks` trust |
| Cursor | `sessionStart`, `beforeSubmitPrompt`, `stop` | `idle`, `running`, `idle` | CO-1b, through `adapters/cursor.sh`, which prints Cursor's required JSON; no asking state exists |

Which of Claude's `PermissionRequest` and `Notification` payloads arrive in the installed version is recorded first,
by CO-1's first task (it adds those events to `scripts/probe.sh`). Probed on Claude Code 2.1.302: a permission prompt fires `PermissionRequest` (with the tool), then a `permission_prompt` `Notification` seconds later; `PermissionRequest` alone drives `asking approval`. A Claude without that event never shows `asking` `approval` from this hook (an `elicitation_dialog` still writes `asking` `input`). `elicitation_dialog` is derived, not probed: if its notification lags like `permission_prompt`'s, an answer given first leaves `asking` `input` until the next event.

**Merge rule** (pure, `SessionStateRules.Merge`): the newer of the record's `statusUpdatedAt` and the file's `at` wins,
except that a hook `asking` stays until a later hook event or a later record status. As a slow fallback only (a
missed `PostToolUse`, a deny, or an Esc: probed on 2.1.302, a denied permission and an Esc at the dialog fire **no** hook event, not even `Stop`), `asking` ends when the transcript's modification time passes `at + 10 s`, or earlier when a newer Claude record status wins (expected to turn `idle` after an interrupt, **not probed**: the probe read no record after an Esc). The fallback shows `running` with `estimated: true`, not `idle`; a deny within 10 s of the prompt keeps `asking` until the next transcript write. The transcript is only
`stat`ed, never read. `ended` in a file whose process is still live reads `unknown`.

### 5.3 Without a hook

A session with no state file and no usable record status gets `source: transcript`: `running` when the transcript
(from the state file, the record's id with the catalog's `transcripts` glob, or `/proc/<pid>/fd`) changed in the last
60 s, else `idle`, and the row says `estimated` (DB §4.3). With no transcript either: `unknown`.

### 5.4 The hook's rules (`config/hooks/fleet-state.sh`, id `aw:fleet`)

- Reads the payload from stdin with `jq`, maps the event (§5.2), writes the file by `mktemp` + `mv` under `umask 077`,
  creates the provider directory (0700) with its `.aw-fleet` marker when missing, prints nothing on stdout (stdout of a
  `UserPromptSubmit` hook would reach the model; the Cursor adapter prints Cursor's JSON), and **always exits 0**.
  Bounded: no network, no CLI calls, one `jq` run and one `mv`; the hook test fails over 100 ms on the CI box.
- `agentPid`: the hook walks up from `$PPID` through `/proc/<pid>/stat` to the first ancestor matched by §4.1 step 2's
  argv[0]/exe rule for the provider (at most 8 steps), and records its start ticks. Not found: `null`.
- It is **advisory** (no allow or deny), so it doesn't call `failopen` (`.agents/rules/hooks.md`: advisory hooks don't
  log). A payload it can't read, an id that fails the pattern, or an unwritable directory is a silent exit 0.
- It writes nothing when `AW_FLEET_DISABLE=1` (for a session the human wants unseen) or `AW_JUDGE_CHILD` is set.
- **Pruning** is the collector's, never the hook's, and only after a good pass: it deletes a file only inside a
  provider subdirectory of the fleet dir that has the `.aw-fleet` marker and mode 0700, only a file that validates as a
  fleet-state file, whose session is not live and whose `at` is older than 24 h; it caps each provider directory at
  512 files (oldest dead sessions first). It never prunes when the fleet dir is inside a git work tree or while a test
  override (§9) is set. Pruned files are counted in telemetry.
- Installed by `scripts/install-fleet.sh --provider claude|codex|cursor` (the lever pattern, `install-lib.sh`), removed
  by `scripts/install-fleet.sh --uninstall --provider …`, and by `./setup.sh --with-fleet` (opt-in, like `--with-meta`;
  Q1). Codex prints the `/hooks` trust line after install.

## 6. Host detection and focus

### 6.1 Host detection

Walk the parent chain of the agent process (the table from §4.1 step 1), at most 32 steps:

| Found first | Host | Detail kept |
|---|---|---|
| a `tmux: server` process | `tmux` | `socket`: the server's listening socket path, from `/proc/<server>/fd` socket inodes matched in `/proc/net/unix` (no tmux call), cross-checked against the agent's own `TMUX` (`<path>,<server pid>,<n>`); `kind`: `sindri` when the path ends in `/sindri`, `team` when the session name matches `<team>-<lane>` of a team in `$AW_STATE_DIR/meta/team.json` (read only for its `team` and `lanes` names) or the socket name equals a team name, `default` for `/default`, else `other`. Then `tmux -S <socket> list-panes -a -F '#{pane_pid} #{session_name} #{window_index} #{pane_index} #{pane_id}'` (once per socket per 10 s) gives the pane holding the agent's ancestor, and `list-clients -F '#{client_pid} #{client_tty} #{client_session}'` the clients showing that session. **Terminal facts for a tmux-hosted session come only from each client's own chain**: the agent's own `TERM_PROGRAM`, `WARP_*`, `KITTY_*`, `WEZTERM_PANE` and `WINDOWID` are inherited through the tmux server and are ignored (the owner's six lanes all carry one stale `WARP_FOCUS_URL`) |
| `sshd` | `ssh` | none: a remote session can't be focused from here |
| a process whose name is in a `terminals.json` row's `processNames` (kitty; wezterm-gui; gnome-terminal-server; alacritty; konsole; foot; xterm) | `terminal` | `terminal` id, the terminal process pid, `WINDOWID` / `KITTY_WINDOW_ID` / `KITTY_LISTEN_ON` / `WEZTERM_PANE` from the shell under it |
| `warp` or `warp-terminal`, or `TERM_PROGRAM=WarpTerminal` on the shell under it | `warp` | `WARP_FOCUS_URL` from that shell, when it matches `^warp://session/[0-9a-f]{32}$` |
| `code`, `cursor` (an IDE's integrated terminal) | `ide` | the IDE id |
| a Claude record with `entrypoint: sdk-ts` under `~/.t3/worktrees` | `t3` | — |
| none of these | `unknown` | — |

The Claude record's own `tmux` field (`<session>:@<w>.%<p>`) is cross-checked against `list-panes` and, when the server
can't be reached, used alone with `socket: null`.

### 6.2 Focus

`POST /sessions/{provider}/{id}/focus` (CA-4) runs the first method that applies, within 3 s, and answers with the
outcome. The service appends one non-public `fleet.focused {ref, outcome, method, code?}` event (no argv).

| Host | Method | Needs | Outcome when it can't |
|---|---|---|---|
| `tmux`, a client attached | focus that client's terminal by the `warp` / `terminal` rows below | as below | `copy`: `tmux -S <socket> attach -t '=<session>'` |
| `tmux`, no client attached | open the configured viewer (LCH `ITerminalViewer.Open`) on `tmux -S <socket> attach -t '=<session>'` | SL-2 (CO-4b) | before SL-2: `copy` with that command |
| `warp` | `xdg-open <WARP_FOCUS_URL>` | CO-S confirms Warp focuses that tab | `copy` naming the tab and the cwd |
| any terminal, X11 | `wmctrl -i -a <window>`, the window found by `wmctrl -lp` matching the terminal pid (or `WINDOWID`) | `wmctrl` on PATH, an X11 session | `copy` |
| `terminal` kitty | `kitten @ --to <KITTY_LISTEN_ON> focus-window --match id:<KITTY_WINDOW_ID>` | kitty's remote control on; `KITTY_LISTEN_ON` a `unix:` socket | X11 row (CO-4c) |
| `terminal` wezterm | `wezterm cli activate-pane --pane-id <WEZTERM_PANE>` | — | X11 row (CO-4c) |
| `ide`, `t3`, `ssh`, `unknown` | — | — | `copy` with what is known (cwd, tty, IDE name) |

A focus child gets `DISPLAY`, `XDG_SESSION_TYPE` and `DBUS_SESSION_BUS_ADDRESS` from the target terminal's environment
(not from `sindri serve`'s, which may have none), and no `TMUX` or `TMUX_PANE`. Every command is an argv (no shell),
from a fixed list per method; its inputs are checked first: the socket path is absolute, owned by the user and under
`$TMUX_TMPDIR` or `/tmp/tmux-<uid>/`; the session name is one `list-sessions` just returned; ids and URLs match their
patterns. While a test override (§9) is set, focus answers `copy` only.

**What is impossible, said plainly** (the row and the outcome both say it, never a silent no-op):
- **A specific tab in a single-process terminal** (GNOME Terminal, Konsole) by pid: they host every tab in one server
  process; `wmctrl` raises the window, not the tab. The outcome is `focused-window`: "the session is in one of this
  window's tabs".
- **A specific Warp tab without a usable `WARP_FOCUS_URL`** (an older Warp, a session not started in a Warp shell, or a
  "no" from CO-S): Warp has no other external API for it; only the app comes forward (`focused-window`), else `copy`.
- **Any window on Wayland** without the terminal's own remote control: GNOME and KDE refuse focus requests from another
  process without an activation token, and `wmctrl`/`xdotool` don't work there. Wayland gets the Warp URL, kitty and
  wezterm methods only; everything else is `copy`.
- **A Cursor IDE chat**: no deep link opens a chat (DB §7): `copy` only.
- **A session over ssh, or on another machine**: `copy` only.
- **A T3 Code thread**: T3 owns it (LCH §7): `copy` only until the SL-T spike says otherwise.
- **Focusing without changing tmux**: when no client shows the session, attaching adds a client, which can resize the
  session's windows for its other clients. The collector never selects a window or pane (CO9); the row shows
  `window.pane` so the human can find it.

## 7. API rows (the API lane reviews; the ideas lane builds CA-0…CA-5, CA-7, CA-8)

Routes sit under `/sindri`. Classes and callers follow API §4. **Beside P10a's launch routes:** `session wrap`'s
`POST /sessions/{launch}/heartbeat` and `/exit` (API ARCH-CQRS-001, `issue` kind) share the `/sessions` prefix; they
are `POST`s whose last segment is a literal, while this spec's routes are `GET /sessions/{provider}/{id}` and
`POST /sessions/{provider}/{id}/focus` (three segments after `/sessions`), so no path matches both, and `{launch}` is a
launch id, never a provider. **`gone` is not `session.ended`:** `fleet.changed` `gone` says the collector stopped
seeing a process (any session, launched or not, no exit code or usage); P10a's `session.ended` is `service:launcher`'s
record that a launch ended (`exit` or `lost`, with its usage). A launched session gets both, and nothing derives one
from the other. API §11's per-session `/timeline` and per-session tokens
are **not** in this spec: tokens and budgets stay with launches (E-28, P10a), and a timeline needs transcript reading
(Q5).

| # | Row | Detail | Phase |
|---|---|---|---|
| CA-0 | The launcher reader | `AgentId` (`Core/Launch/AgentId.cs`, Vogen string, LCH SL3's pattern), `IAgentCatalog` with `Resolve` and `All` only (`Core/Launch/`), `AgentCatalog` over the embedded `config/launcher/builtins.json` merged with `launcher.json`'s `agents` (`Infrastructure/Launch/`), `LauncherConfigFile` (reads `agents` and `sessions`; accepts the schema's other sections, `viewer`, `host` and `meta`, without reading them; rejects unknown keys per section as LCH §4's table), `deps.yaml`'s Sindri → Launcher edge made current. SL-2 adds `BuildArgv`, SL8 and the viewer and host sections to these same types | CO-2a |
| CA-1 | `GET /sessions?provider=&state=&repo=&inWorkflow=` → `ListSessions` | `SessionList(sessions, recentlyEnded, problems, counts{live, asking, outsideWorkflow}, empty, asOf)`. A `SessionRow`: `ref`, `provider`, `providerLabel`, `id`, `idKnown`, `state`, `asking?{reason, tool?, since}`, `stateSource`, `estimated`, `repo?`, `worktree?` (repo-relative), `host{kind, label, socketKind?, attachedClients?, terminal?}`, `lane?{team, lane}`, `launchId?`, `instance?` and `item?` (CA-6), `startedAt`, `since` (the current state's), `focus{method, possible, why?}`. **Human callers only:** `title`, `host.session`, `host.pane`, `pid`, and on `recentlyEnded[]` entries `resume` (argv from the catalog's `resume` template, null when none). **As built (CO-3):** a `recentlyEnded[]` entry is `{session: <row>, endedAt, resume}`, the row nested, not flattened; `inWorkflow` means "has a Sindri launch" until CA-6 adds the item join; a session with no repo is hidden from every `session` caller, and the head `counts` cover what the caller may see, before the filters. `problems[]`: `{source, code, text}` (`text` a fixed sentence per code). `empty`: "No agent sessions are running." Query: any caller; `session` callers see only rows of their `X-Sindri-Repo`. Never in any row: the transcript path, the cwd, the environment, `messagingSocketPath`, `bridgeSessionId`, the socket path | CO-3 |
| CA-2 | `GET /sessions/{provider}/{id}` → `GetSession` | `SessionView`: the row plus, for human callers, `cwd`, `host.socket` and `stateHistory[]` (the last 20 state changes in this Sindri run, in memory: `{state, reason?, at, source}`), `asOf`. Unknown ref: `SND-SESSION-404`. **As built (CO-3):** `SessionView` is `{session: <row>, cwd, stateHistory, asOf}` with the row nested (and `host.socket` inside it), not flattened; for a `session` caller `cwd`, `stateHistory` and `host.socket` are null. An unknown, ended or other-repo ref is the same 404 | CO-3 |
| CA-3 | `fleet.changed` event | Payload `{provider, sessionId, change: appeared \| asking \| gone, state, reason?, repo?, host: kind, launchId?}`, no text. Public. Key `fleet.changed:<provider>:<id>:<change>:<observedAtMs>`. `Invalidates = [ApiViews.Status]`. Written by `RecordFleetChanges` (CO3's rules). An `EventCatalog` row and a row in API §3.4's table | CO-2a (event), CO-3 (docs row) |
| CA-4 | `POST /sessions/{provider}/{id}/focus` → `FocusSession` | Body `{expectStartedAt}` (the row's `startedAt`; a different process at that ref is `SND-SESSION-409`; **`startedAt` can be null** (Codex and Cursor rows have none): the caller then sends `expectStartedAt` null and the server skips the start-time comparison, matching on provider, sessionId and pid alone); class `desk`, callers `human` (terminal not needed), `session` → `SND-AUTH-002`. Response `FocusResult{outcome: focused \| focused-window \| opened-viewer \| copy, method, text, copy?: argv}` (`text` the sentence the app shows, from the API so the app keeps no copy); a session gone by then is `SND-SESSION-404`. Appends `fleet.focused` (not public). CLI `sindri agents focus <ref> [--json]`: exit 0 on `focused`, `focused-window` or `opened-viewer`, 1 on `copy` (the command printed on stdout, the reason on stderr) or when the server isn't running (`SND-API-001`), 2 on a refusal, a bad ref or another error | CO-4a |
| CA-5 | Codes (`SindriErrors`, `docs/csharp/sindri.md`) | `SND-SESSION-404` NotFound: no such live session (or outside a session caller's repo). `SND-SESSION-409` Conflict: the session at that ref is a different process. `SND-SESSION-002` Unavailable: the collector can't run here (`detail`: `unsupported-platform`, `proc-unreadable`, `disabled`); only these three replace the list. `SND-SESSION-003` test-override, informational: a test override (§9) is set in this run; it is one `problems` row beside a normal list, never a replacement | CO-2a (`-002`, `-003`), CO-3 (`-404`), CO-4a (`-409`); the preview (CA-9, CO-7b) answers `-404` and `-409` too, and refuses a `session` caller with `SND-AUTH-002` |
| CA-6 | The launch join | Once P10a's `launches` projection exists, `ListSessions` fills `item` and `instance` (address, recipe, state) for rows with a `launchId`, and the session leaves `inWorkflow=false` | P10a + CO-3 (API lane) |
| CA-7 | Catalog and goldens | One command-catalog row, `FocusSession` (desk), in `Host/Catalog/CommandCatalog.cs` and its golden `tests/Aw.Sindri.Tests/Golden/Api/commands/catalog.json`: CO-3 adds it to `Host/Catalog/PendingRows.cs` as `pending:CO-4a` (route `POST /sindri/sessions/{provider}/{id}/focus`, CLI `sindri agents focus <ref>`), so U11a's Focus can render `pending:CO-4a`, and CO-4a binds its type and flips it to `ready`. `ListSessions` and `GetSession` are queries and get **no** catalog row (the catalog holds command classes only, `ARCH-CQRS-003`); `GetSessionPreview` (CA-9) is a query too and gets no catalog row either; CO-7b adds the route and its goldens (`sessions-preview/`: `lines`, `truncated`, `empty`, `not-in-tmux`, `not-live`, `timed-out`, `not-found`, `refused-session`; built from the synthetic stub pane only). Goldens in `tests/Aw.Sindri.Tests/Golden/Api/sessions/` (`empty`, `fixture-three-providers`, `session-scoped`, `problems-source-failed`, `recently-ended`; and `unsupported-platform`, `proc-unreadable`, `disabled`, the three `-002` bodies, each a 503 problem+json because `-002` replaces the list; as built in CO-3), `sessions-id/` (`claude-tmux`, `codex-hook`, `not-found`, `session-caller-hidden-fields`), `sessions-focus/` (`focused-warp`, `copy-wayland`, `refused-session`, `refused-origin`, `stale-started-at`, `not-found`). The gate runs `Aw.Sindri.Ui.Tests` (it pins the catalog) | CO-3, CO-4a |
| CA-8 | `GET /status` gains `sessions{live, asking, problems}` (additive) | For the tray tooltip and the Agents tab count without a second call. **As built (CO-3):** the aggregate is box-wide and deliberately public to every caller, a `session` caller included and through the MCP `sindri_status` tool too, because it is counts only and no rows (the list stays scoped); zeros before the first pass | CO-3 |
| CA-9 | `GET /sessions/{provider}/{id}/preview` → `GetSessionPreview` | **Live read-only preview (owner decision, 2026-10-17, reversing Q4).** Response `SessionPreview{lines[], truncated, capturedAt, startedAt, host}` or, as HTTP 200, `SessionPreviewUnavailable{reason, capturedAt}` with `reason` one of `not-in-tmux`, `not-live`, `timed-out` (the 1 s read timed out). **Check order, per request, before any shared wait:** the caller kind first (a `session` caller always gets `SND-AUTH-002`, so it cannot probe which refs exist), then the ref lookup, then `expectStartedAt`, then the 200 answers. An unknown ref is HTTP 404 `SND-SESSION-404`, like CA-2. `startedAt` is the session's; the GET takes an optional `expectStartedAt`, and a different process at that ref is HTTP 409 `SND-SESSION-409`, the code CA-4 uses; the panel sends it (a null `startedAt` is sent as `expectStartedAt: null`, and the server then matches on provider, sessionId and pid alone, as in CA-4). **Source:** `tmux capture-pane -p -J -S -40 -t <pane>` on the socket the session was found on (CI-5), for **every active session the collector sees on any tmux server it reads** (the default server and the team sockets, Sindri-launched or not): the owner said "otherwise it's not a valuable control plane". A session not in tmux answers `not-in-tmux`. `<pane>` is the pane id (`%n`, `^%[0-9]+$`) the snapshot holds; the socket is checked as §6.2 checks it. **`not-live`:** the pane id the snapshot holds no longer exists on that socket, or the session's process is neither `#{pane_pid}` nor a descendant of it. `pane_pid` is the pane's first process (usually a shell) and the agent runs below it (§6.1, §6.2), so the check walks the session process's parents through the process table the collector already reads (CO5), at most 64 hops: reaching `pane_pid` means live, and running out of parents or hops first means `not-live`. One bounded `display-message -p -t <pane> '#{pane_pid}'` (same `-S`) runs before the capture, so a reused pane is `not-live` and never shows another session's text. One 1 s deadline covers both tmux spawns (the worst case is 1 s, not 2); a pane reused between the two commands is accepted as a one-poll window. `expectStartedAt` still catches a recycled session ref. **`PreviewRules`, in order:** (1) capture `-p -J -S -40`, decoded with replacement characters (never by throwing); (2) over the **whole** capture, before any split, strip escape sequences (CSI, OSC, DCS/APC/PM/SOS strings up to ST even when they span lines, an unterminated string to the end of the capture, other ESC), C0 controls but tab and LF (CR goes with them), DEL, C1 controls U+0080–U+009F, bidi marks, overrides and isolates (U+061C, U+200E, U+200F, U+202A–U+202E, U+2066–U+2069) and U+2028 and U+2029, then split on LF; (3) drop trailing blank lines; (4) keep the last 40 lines (the normal window: dropping older lines here does not set `truncated`); (5) cut each line at 200 UTF-16 code units without splitting a surrogate pair; (6) `truncated` is true only when a line was cut at 200 or the 64 KiB cap dropped output. Text is never rendered as markup. **Size and concurrency:** the capture reads at most 64 KiB of stdout (more: keep the last 64 KiB, cut on a line boundary so the partial first line, and any partial escape or UTF-8 sequence in it, is dropped, and set `truncated`); at most one capture is in flight per pane, and a second request waits for the first's answer; the caller, ref and `expectStartedAt` checks run per request, before that wait, so a coalesced request never skips its 409. **Served as-is, no scrub:** the only served text not passed through `Aw.Redaction` (the exception to the scrub of served text, §12). The exception covers the preview route's `lines` only; every other served field (the preview's `host`, `startedAt` and reason, and every other route) stays scrubbed. No "lines hidden" marker. Owner decision, 2026-10-17: "it's the same as me just opening the session myself". **Read on demand** when a client asks, **never in the collector pass**, with a 1 s timeout on the injected `TimeProvider` like the other per-source reads. A timeout answers `timed-out` and sets the preview's own per-socket "preview timed out" mark for 10 s; while it is set the preview answers `timed-out` without spawning tmux. Only previews read this mark. It is not CO9's window: the collector pass never reads it, so a slow `capture-pane` cannot blank the session rows on that socket or write `gone` rows. **Not logged, on every path:** no ledger event, no audit row, and no log line or span attribute holding pane text, on success and on error paths. Telemetry is a counter `sindri.sessions.preview.reads` tagged by result (`ok`, `not-in-tmux`, `not-live`, `timed-out`, `not-found`, `conflict`, `refused`) and a duration histogram, no text. **Human-only** (CO10): the endpoint refuses every caller that isn't the human token (`SND-AUTH-002`, as `FocusSession`), and there is no MCP tool and no CLI verb for the preview in this spec (a human CLI verb can come later as its own row). Query class, no catalog row (CA-7); `host` is the row's `host.kind` and label | CO-7a (read), CO-7b (route) |

CLI (CI-8): `sindri agents [--provider P] [--state S] [--repo R] [--json]` and `sindri agents show <ref> [--json]` read
the routes and never start the server (API §7). Exit: 0 ok (an empty list included), 1 when the server isn't running
(`SND-API-001`) or a `problems` row is present, 2 on usage or another error. `sindri agents` (not `sessions`) keeps it
apart from `sindri session wrap` (P10a). Telemetry: span `sindri.collector.pass` (sources, sessions, duration), metrics
`sindri.sessions` (gauge by provider and state), `sindri.collector.overrun` and `sindri.collector.pruned` (counters).
Logs carry refs and codes only.

## 8. UI rows (the UI lane)

| # | Row | Phase |
|---|---|---|
| CU-1 | **U11a: Agents with one group, "Outside a workflow"**, before U11's recipe groups exist. Page head: `Agents  N live · M asking · K outside a workflow`, each count from `counts` (every count derived from the rows, `fixture.md`). One row per live session: `glyph CHIP · provider · title or repo/id8 · worktree · since · host label · lane? · [Focus O]`. Chips per CO14; `estimated` after the chip when the source is `transcript`. Rows from a Meta lane read "meta · sindri/ideas". `recentlyEnded` sits under a fold, each with the resume command and a copy button (`Y`). `problems` render as one line each with the code (`SND-SESSION-003 test-override` included, above a normal list); only `SND-SESSION-002` (`unsupported-platform`, `proc-unreadable`, `disabled`) replaces the list with a page-level line ("Sindri can't see agent sessions on this system: <detail>"). Filters: provider, state, repo. The tab count is `sessions.live` from `GET /status`. Refresh on `fleet.changed`, and every 5 s while the page is visible (running/idle flips aren't events, CO3). Empty: the API's `empty` sentence. Footer: "Cursor IDE chats aren't shown". Each row's accessible name reads its provider, title, state and host; the page's live region announces "N agents · M asking" when counts change, never on a poll with no change, and refreshes never move focus or scroll (rev 7 §5) | U11a |
| CU-2 | **Focus** on a row (button, or `O` on the selected row) calls CA-4. While it runs (≤ 3 s) the button reads "Focusing…" and is disabled; then the row's live region shows the response's `text`: "Brought Warp forward", "Raised the terminal window: the session is in one of its tabs", "Opened <label> on the tmux session", or, for `copy`, the text plus the command with a copy button (`Y`). Focus moves to the copy button only if it is still on Focus (LCH L-7). A `SND-SESSION-404` reads "This session has ended" and the row leaves on the next refresh; `-409` reads "A different process has this id now: refreshed" and refreshes. A row whose `focus.possible` is false shows "Copy command" in place of Focus | U11a |
| CU-3 | **Session sheet**: Enter or a click on a row opens a 520 px sheet from `GetSession` (`{session, cwd, stateHistory, asOf}`; the row's fields are under `session`): the row's fields, `cwd`, host details (socket kind, session, pane, clients), the state history, and Focus. Deep link `session/<provider>/<id>` (UI §3's `session/<id>` takes the ref) | U11a |
| CU-4 | **Tray:** the tooltip adds "N agents · M asking", from `GET /status`'s `sessions`. The badge stays the needs-you count (UI X5, R8-13) unless the owner answers Q3 yes | U11a |
| CU-5 | **U11's recipe groups** take launched sessions (CA-6) out of "Outside a workflow" into their recipe state, where the row keeps U11's `OpenItem:attach` (CO8); the group stays for everything else. Requested edit to the UI spec's U11 row: "Depends on" reads `U8 + CO-3 + CA-6 + P10a + E-28` in place of "API §11 collector"; E-28's per-target spend stays P10a's launch usage, not the collector's | U11 |
| CU-6 | **Vocabulary:** `fixture.md` gains `● RUNNING` (exists), `◐ ASKING`, `○ IDLE`, `○ UNKNOWN` for sessions, ink colours except live `●` in `--success`; `asking` moves to `--attn` only if Q3 is yes | U11a |
| CU-7 | **Home** (only if the owner answers Q3 yes): an asking session becomes a needs-you row of kind `session` (`primaryAction` `FocusSession`), urgency below decisions | CO-6 |
| CU-8 | **Live preview panel** in the session sheet (CU-3) only; a step's details reuse it later as a U11 row. In Avalonia terms: the panel is **collapsed by default** and opened by a disclosure button "Show live terminal" (Space or Enter toggles; "Hide live terminal" when open; focus returns to the button on collapse; the state is remembered per session for the app run only). Open, it is a focusable, read-only `SelectableTextBlock` inside a `ScrollViewer` (Tab reaches it; arrows, PageUp, PageDown, Home and End scroll, and Left and Right scroll sideways; no text input), with `AutomationProperties.Name` "Live terminal preview, read-only" and `AutomationProperties.LiveSetting` Off (refreshes are never announced). Up to 16 lines are visible inside the sheet; the rest scroll inside the panel. Lines do not wrap: a long line scrolls sideways inside the panel. The text is selectable with the mouse and by keyboard: Ctrl+A inside the panel selects all its text and Ctrl+C copies it. Its header reads "Live, unfiltered terminal text" and the capture age ("updated 1 s ago", the age of the last successful capture), and "Paused" or "Ended" in those states. **Open** means the sheet is shown, the panel is expanded and the window is not minimised; it polls `GetSessionPreview` every **2 s only while open** (sending `expectStartedAt`), every 5 s when the window is visible but unfocused, and not at all when minimised; becoming active again fetches at once. States, with fixed sentences: first fetch "Reading the terminal…"; `not-in-tmux` "This session isn't in tmux, so there's nothing to preview." (plus "Use Focus to open it." only when the Focus control is enabled); `not-live` "The session isn't running in its pane any more."; `timed-out` "tmux didn't answer in time. Retrying."; 404 "This session has ended."; 409 "This session restarted. Refresh to see it."; serve down, refused or a network error: the last lines stay, dimmed, with "Preview paused: couldn't refresh (<code>)" and the age; polling continues every 2 s (5 s unfocused) and the dimmed note clears on the next good read; an empty capture "The terminal is empty."; a footer "Last 40 lines" always, plus "long lines cut at 200 characters" only when `truncated`. On 404 or 409 the panel keeps the last lines dimmed, stops polling and shows its sentence. A 404 offers no Refresh (the session has ended). A 409 offers a "Refresh" button: it re-reads `GetSession` first to get the new `startedAt`, then fetches the preview with it, and shows "Refreshing…" with the button disabled while it runs; a second 409 shows the sentence again with Refresh. State messages (ended, paused, timed-out, 409) go once per change to the page's existing polite live-region note, never to the lines. Dimmed text keeps 4.5:1. The panel follows the tail only while scrolled to the bottom. Scrolled up, it keeps the line the user was reading, found by its content where it still exists in the new 40 and otherwise the top, and shows a "Jump to latest" button (a Tab stop, automation name "Jump to latest"; End also jumps; when the button disappears, focus moves to the panel). Text uses the existing monospace token and the body ink and background pair (4.5:1). The client keeps the lines in memory only while the panel is open, never on disk | CO-7c |

## 9. Configuration seams

Box facts go in `launcher.json` (LCH SL2: machine-wide, no approval). Nothing here needs a profile change.

| Where | Key | Default | Meaning |
|---|---|---|---|
| `launcher.json` | `sessions.enabled` | true | the whole collector (`false` → `SND-SESSION-002 disabled`) |
| `launcher.json` | `sessions.pollSeconds` | 2 | pass cadence (1–30) |
| `launcher.json` | `sessions.providers` | every catalog entry | which agents to discover (`["claude-code", "codex"]`) |
| `launcher.json` | `sessions.tmuxSockets` | `[]` | extra socket paths to read besides the ones found from processes |
| `launcher.json` | `sessions.claudeSessionsDir` | `~/.claude/sessions` | for a relocated Claude config (`CLAUDE_CONFIG_DIR`) |
| `config/launcher/terminals.json` | per terminal `processNames` | — | the running process names for host detection (`wezterm-gui`, `gnome-terminal-server`), beside the existing launch argv (CO-0) |
| `config/launcher/terminals.json` | per terminal `focus` | absent | the kitty and wezterm focus argv templates (CO-4c); placeholders `{windowId}`, `{paneId}`, `{listenOn}` in the closed set for `focus` only |
| env | `AW_FLEET_DIR` | `$AW_STATE_DIR/fleet` | the state files (tests, the prover) |
| env | `AW_FLEET_DISABLE` | unset | the hook writes nothing in this session |
| env | `AW_SESSIONS_PROC_ROOT` | `/proc` | **test override**: tests and the prover's fixture tree only |
| env | `AW_SESSIONS_CLAUDE_DIR` | unset | **test override**: replaces `sessions.claudeSessionsDir` |

A test override in a real run shows as an informational `problems` row (`SND-SESSION-003 test-override`) beside the
normal list (it never replaces it: only `SND-SESSION-002`'s `unsupported-platform`, `proc-unreadable` and `disabled`
do), turns pruning off and makes focus answer `copy`. The `sessions` key is new in `launcher.json` schema 1 (additive, optional). The bash reader refuses
unknown keys, so CO-0 adds it to `x-aw-keys` and the bash reader accepts and ignores it in the same PR; an
`aw-meta-launch` installed before that PR refuses a file with the key until `scripts/install-meta.sh` reinstalls it
(CO-0's docs say so).

`.env.example` (ENV-EXAMPLE-002 fails an entry nothing reads, so each entry lands with the phase whose code first reads
it, not in CO-0): `AW_SESSIONS_PROC_ROOT` and `AW_SESSIONS_CLAUDE_DIR` in CO-2a (the C# adapters); `AW_FLEET_DISABLE`
in CO-1 (the hook); `AW_FLEET_DIR` in CO-2a, or in CO-1 if CO-1 merges first, since the hook reads it too
(`${AW_FLEET_DIR:-…}`, which the scan counts as a read); the later phase finds the entry there.

## 10. Rows per lane

### 10.1 Ideas lane (the collector, the hook, the contract)

| # | Row |
|---|---|
| CI-1 | Contract files (CO-0): `schemas/fleet-state.schema.json`; `tests/fixtures/fleet/` with `manifest.json`; `tests/fixtures/sessions/` (a `/proc`-shaped tree: `<pid>/{stat,cmdline,environ,status}`, `exe` and `cwd` as symlinks, `fd/` symlinks, `net/unix`; Claude, Codex, Cursor and aider processes, a `claude -p` under judge, a tmux server with its socket line, a Warp shell, a kitty shell, an sshd chain, a recycled pid, a zombie, another user's process by `status` `Uid:`; a `claude-sessions/` dir with matching and stale records); the `sessions` key and `terminals.json` `processNames`; `deps.yaml` (Sindri owns `fleet/` as a state-root path; a Hooks → Sindri file edge for the hook's writes, declared `status: planned` in CO-0 and flipped to `current` by CO-1 when `fleet-state.sh` writes `fleet/`, since check-deps fails a current edge the source lacks (DEPS-STALE-001) and a planned one it has (DEPS-STALE-002); Sindri → Launcher made current in CO-2a; `~/.claude/sessions` and the provider folders are outside the state root and not edges, as for Meta); `docs/csharp/sindri-sessions.md` (new). No `.env.example` entries (§9: each lands with its first reader) |
| CI-2 | Core (CO-2a, CO-2b): `Aw.Sindri.Core/Sessions/` with `SessionRef`, `ObservedSession`, `SessionState`, `AskingReason`, `HostInfo`, `StateSource`, `ProcessEntry`; pure rules `SessionLiveness.Matches` (CO5), `AgentProcessRules.Find` (§4.1 step 2), `SessionStateRules.Merge` (§5.2), `FleetChangeRules.Diff` (CO3's two-pass, 5 s and hourly caps), `HostRules.Detect` (§6.1), `FocusRules.Choose` (§6.2); ports `IProcessTable`, `IClaudeSessionRecords`, `IFleetStates`, `ITmuxServers`, `ISessionSnapshot`, `ISessionFocus` |
| CI-3 | Infrastructure: `Aw.Sindri.Infrastructure/Sessions/` with `ProcProcessTable` (stat, argv[0] only, exe, environ allowlist, cwd, fd links, `net/unix`; root from `AW_SESSIONS_PROC_ROOT`), `ClaudeSessionRecords`, `FleetStateFiles` (read and prune), `SessionSnapshotStore` (the in-memory `ISessionSnapshot`), `TmuxCli` (CO-2b; five verbs, `capture-pane` from CO-7a; through its own bounded `Process.Start`, as `GitRepoLocator` does, with a kill-on-timeout of the child only) |
| CI-4 | `Host/Server/SessionCollectorHost` (lease-gated, registered beside `ProfileWatch` in `SindriHostServiceCollectionExtensions`), calling the internal `Features/Sessions/RecordFleetChanges` service (appends with the grant; no gate, audit row or catalog row) |
| CI-5 | Architecture test `ARCH-SESS-001` (CO-2b): no type in Sindri's `Sessions` namespaces (Core, Infrastructure, Features) builds a tmux argv except two: `TmuxCli`, whose verbs are exactly `list-sessions`, `list-panes`, `list-clients`, `display-message` and, from CO-7a, `capture-pane` (five verbs; the only ones Sindri runs, `capture-pane` only with the exact read-only argv `capture-pane -p -J -S -40 -t <pane>` of CA-9, run on the socket the session was found on; still no attach, `send-keys` or kill), and `Core/Sessions/TmuxAttachLine`, the one place that builds `tmux -S <socket> attach -t '=<session>'` as data for the copy line and the viewer (§6.2), which no Sindri type executes (the human runs the copy line; the viewer's terminal runs it in CO-4b), so no process runner takes its output; unit tests (CO-7a's too) assert every `TmuxCli` call, `capture-pane` included, starts with the socket flag `-S <absolute socket path>` before the verb (asserted separately from `capture-pane`'s own `-S -40`, for all five verbs) and carries an environment without `TMUX`, `TMUX_PANE`, and that no Sessions type reads `cmdline` past the first NUL |
| CI-6 | The hook `config/hooks/fleet-state.sh` (§5.4) and `scripts/install-fleet.sh` (CO-1 Claude, CO-1b Codex and Cursor); `scripts/probe.sh` gains `PermissionRequest`, `Notification`, `SessionStart`, `SessionEnd`; mapping rows in `config/hooks/adapters/README.md`; the hook in `.agents/rules/hooks.md` |
| CI-7 | Focus (CO-4a): `Features/FocusSession/` (the slice) and `ISessionFocus` adapters in `Infrastructure/Sessions/Focus/`: `WarpUrlFocus`, `X11WindowFocus` (CO-4a), `KittyFocus`, `WeztermFocus` (CO-4c), each an argv runner with a 3 s timeout |
| CI-8 | `sindri agents`, `sindri agents show`, `sindri agents focus` (`Host/Commands/`), §7's exits; AGENTS.md's command list |
| CI-9 | aw-meta fix, filed separately: `DirectorySessionDirectory` checks `procStart` against `/proc/<pid>/stat` (CO4). Not part of the collector's phases; XS |
| CI-10 | Preview (CO-7a): `TmuxCli`'s `capture-pane` verb (CI-5), `Core/Sessions/PreviewRules` (CA-9's six numbered steps: escape, control, C1 and bidi stripping over the whole capture and then the LF split, trailing blanks, the 40 × 200 bounds, `truncated`; the 64 KiB cap on a line boundary), `Features/GetSessionPreview/` (target from the snapshot, on-demand read with a 1 s `TimeProvider` timeout and the preview's own socket mark, the pane check, one capture in flight per pane, the check order, the three reasons); no ledger write |

### 10.2 API lane

Reviews CA-0…CA-5, CA-7 and CA-8 in the ideas lane's PRs (shapes, classes, catalog). Takes CA-0's types over into SL-2
(extends, never replaces). Builds CA-6 inside P10a or right after it, and CO-4b (the viewer-backed focus) once SL-2
exists. **Request (2026-10-17):** reviews CA-9 and builds CO-7b (`GET /sindri/sessions/{provider}/{id}/preview`, query class with no catalog row, human token only through the caller-kind check `FocusSession` uses, `expectStartedAt`, goldens; the gate runs `Aw.Sindri.Ui.Tests`). CU-7's needs-you kind, if Q3 is yes, is the API lane's half of CO-6.

### 10.3 UI lane

CU-1…CU-4 and CU-6 are U11a, a new phase before U11; U11 takes CU-5; CU-7 waits on Q3. **Request (2026-10-17):** CU-8, the live preview panel, is CO-7c, in the session sheet (CU-3), after U11a and CO-7b.

### 10.4 Engine and context lanes

No rows. The engine's agent runtime (P8) launches through `ILauncher`, so its sessions carry `AW_SINDRI_LAUNCH` and
join through CA-6 with no engine change. The context lane is not involved (repo resolution is API §3.1's, already in
Sindri).

## 11. Phases

Each phase is one PR, driven by /specToProvenPR in its lane, built red first (the slice recipe; builders on Sonnet).
Sizes: S a few files, M one subsystem slice. **(owner)** steps run on the owner's box. **Proof safety:** every proof
that starts tmux uses a private socket `tmux -L aw-co-proof-$$` with `TMUX` unset, and stops only that socket (`tmux -L
aw-co-proof-$$ kill-server` on that private name, never another); no proof touches the default socket or `-L sindri`
except read-only. The CO-3 proof home is `sindri-prove.sh`'s with an approved two-repo profile seeded as its header
says (this checkout among the repos).

| Phase | What | Files | Size | Needs on main | Tests | Proof | Lane |
|---|---|---|---|---|---|---|---|
| **CO-0** | First task, **(owner)**: record Claude's record `status` at a turn end and while a permission prompt is open, on one Claude session in default permission mode, by running `watch -n1 "jq -c '{status,statusUpdatedAt}' ~/.claude/sessions/<pid>.json"` (read-only) beside it through one turn end and one open permission prompt; the readings, with the Claude version, go into `docs/csharp/sindri-sessions.md`. Then CI-1: contract files, fixtures, schema key, `processNames`, docs page, `deps.yaml` (the Hooks → Sindri file edge `planned`) | `schemas/fleet-state.schema.json`, `schemas/launcher.schema.json`, `config/launcher/terminals.json`, `scripts/launcher-lib.sh`, `scripts/check-fleet.mjs`, `scripts/check-launcher.mjs` (every `terminals.json` row needs `processNames`), `scripts/tests/check-launcher.test.sh` (the missing-`processNames` drift case), `scripts/tests/meta-launch.test.sh` (the `sessions` case), `scripts/tests/check-fleet.test.sh` (new), `scripts/ci/changed-areas.sh` (`schemas/fleet-state.schema.json` and `tests/fixtures/{fleet,sessions}/` run the bash job) with its case in `scripts/tests/ci-changed-areas.test.sh`, `AGENTS.md` (the bash-test list gains `check-fleet.test.sh`), `tests/fixtures/{fleet,sessions,launcher}/`, `deps.yaml`, `docs/csharp/sindri-sessions.md`, `docs/csharp/sindri-launcher.md`, `docs/sindri/README.md` (X49, X59(c) point here) | S | nothing (SL-0, SL-1 merged) | `node scripts/check-launcher.mjs` passes with a `sessions` fixture (valid) and a `sessions.bogus` fixture (invalid); `node scripts/check-fleet.mjs` validates every `tests/fixtures/fleet/` file against its manifest verdict (Ajv2020); `meta-launch.test.sh` accepts a `launcher.json` with `sessions`; a `terminals.json` row without `processNames` fails `check-launcher`; `check-fleet.test.sh` runs the valid and invalid fixtures; `check-deps` passes with the Hooks → Sindri file edge `planned`; `check-env-example` passes (no new entries) | CI green; **(owner)** the `watch` readings recorded with the Claude version | ideas |
| **CO-2a** | CA-0, CI-2 (liveness, agent-process, merge, change rules), CI-3 (all but `TmuxCli`), CI-4, CA-3, CA-5 `-002`: discovery, state from records and transcripts, the snapshot, `fleet.changed` | `src/Sindri/Aw.Sindri.Core/{Launch,Sessions}/*`, `…Infrastructure/{Launch,Sessions}/*` (`builtins.json` as a linked `EmbeddedResource`), `…Features/Sessions/RecordFleetChanges.cs`, `…Host/Server/SessionCollectorHost.cs`, `SindriHostServiceCollectionExtensions.cs`, `SindriErrors.cs`, `EventCatalog.cs`, `tests/Aw.Sindri.Tests/Golden/Api/events-catalog/catalog.json` (the `fleet.changed` row), `tests/Aw.Sindri.Tests/Sessions/*` (goldens in `tests/Aw.Sindri.Tests/Sessions/Golden/`), `.env.example` (§9), `deps.yaml`, `docs/csharp/sindri.md` (the snapshot exception, the event row) | M | API P1, P2, P5 (merged) | Unit: `SessionLiveness_rejects_a_recycled_pid`, `SessionLiveness_reads_procStart_as_a_string`, `AgentProcess_matches_argv0_then_exe_then_comm`, `AgentProcess_skips_a_claude_p_under_judge`, `AgentProcess_skips_a_descendant_of_the_same_agent`, `Merge_keeps_asking_until_a_later_hook_event`, `Merge_newer_record_status_wins`, `Merge_transcript_fallback_waits_10s`, `Changes_ledger_appeared_after_two_passes`, `Changes_ledger_asking_after_5s_and_at_most_6_an_hour`, `Changes_no_gone_when_the_process_table_fails`, `Environ_drops_every_name_off_the_allowlist` (planted token), `Cmdline_is_read_to_the_first_nul_only`, `Key_files_are_never_opened`, `Restart_rebuilds_the_reported_set_from_the_ledger`, `Catalog_merges_launcher_json_agents_over_builtins`; integration: the snapshot over `tests/fixtures/sessions/` equals `tests/Aw.Sindri.Tests/Sessions/Golden/snapshot-discovery.json`; the events-catalog golden and `Aw.Sindri.Ui.Tests` in the gate (its `GoldenContractTests` reads every API golden, `events-catalog` included) | **One `serve`**, `sindri-prove.sh` home, real `/proc`, started with `AW_SESSIONS_CLAUDE_DIR` set from the start to a temp dir seeded with copies of the box's `~/.claude/sessions/*.json` (never `*.key`), so both halves run in the same process with no restart: `sindri-prove.sh human ledger --type fleet.changed --json` lists one `appeared` per live record (count equal to `jq` over the copied records whose pid is live), and the `SND-SESSION-003 test-override` problem is present; then a stub (a copy of `/bin/sleep` named `claude` in a temp dir, started in a private-socket tmux pane, with a record written into that dir whose `procStart` is read from the stub's `/proc/<pid>/stat` after it starts) adds `appeared` within 4 s, and killing the stub adds `gone`; a record left after it exits never reappears. The unoverridden `~/.claude/sessions` read is CO-3's proof | ideas |
| **CO-2b** | CI-2 (`HostRules`), CI-3 (`TmuxCli`, `net/unix`), CI-5: host detection; an embedded reader of `config/launcher/terminals.json` (a linked `EmbeddedResource`, as `builtins.json`) that reads each row's `id` and `processNames` only, for §6.1's terminal row | `…Core/Sessions/{HostRules,TmuxAttachLine}.cs`, `…Infrastructure/Sessions/{TmuxCli,UnixSockets}.cs`, `…Infrastructure/Launch/TerminalProcessNames.cs`, `tests/Aw.Sindri.Tests/Sessions/Host*`, `tests/Aw.Architecture.Tests/Sessions*` | S | CO-2a | `HostRules_finds_the_tmux_server_and_its_socket`, `HostRules_takes_terminal_facts_from_the_client_chain_only` (fixture: agent with a stale Warp URL, client with another), `HostRules_finds_warp_kitty_and_ssh`, `HostRules_marks_a_team_socket`, `TmuxCli_passes_S_and_drops_TMUX`, `TmuxCli_skips_a_timed_out_socket_for_10s`; `TerminalProcessNames_reads_every_row_of_terminals_json`; integration over the fixture tree equals `tests/Aw.Sindri.Tests/Sessions/Golden/snapshot-hosts.json`; `ARCH-SESS-001` | the stub of CO-2a, in its private-socket pane: `GetSession` isn't served yet, so the proof reads the collector's debug log line (refs and host kinds only) showing `tmux` socket kind `other` with the pane; each of the box's live lanes shows `tmux` `default` with its Warp client | ideas |
| **CO-3** | CA-1, CA-2, CA-5 `-404`, CA-7 (the `FocusSession` pending row; the two queries get no catalog row), CA-8, CI-8 (`agents`, `agents show`): routes and CLI. **After this phase the owner's question is answered on the CLI and the API** | `…Features/{ListSessions,GetSession}/`, `Host/Endpoints/EndpointTable.cs`, `Host/Commands/{CommandTable,Verbs,Usage}.cs`, `Host/SindriJsonContext.cs`, `Host/Catalog/PendingRows.cs` (`FocusSession`, `pending:CO-4a`), `tests/Aw.Sindri.Tests/Golden/Api/commands/catalog.json`, `GetStatus`, goldens, `docs/csharp/sindri.md`, `docs/csharp/sindri-sessions.md`, `AGENTS.md` | M | CO-2b | Slice tests per route (red first); the CA-7 goldens; `ListSessions_scopes_a_session_caller_to_its_repo_and_hides_human_fields`; `GetSession_hides_cwd_socket_and_history_from_a_session_caller`; `Agents_cli_exits_1_when_serve_is_down`; catalog test; `Aw.Sindri.Ui.Tests` in the gate | `sindri-prove.sh human agents` lists every live Claude session on the box with state, repo and host (count equal to CO-2a's `jq` count); `sindri-prove.sh curl GET /sindri/sessions \| jq '.sessions\|length'` equals `agents --json`'s; `curl -H 'X-Sindri-Repo: other' "$(sindri-prove.sh url)/sindri/sessions"` (a session caller) returns no rows; `curl GET /sindri/status` has `sessions.live` equal to that count | ideas |
| **CO-1** | CI-6 for Claude Code: the `aw:fleet` hook, its installer and uninstaller, `--with-fleet`; `asking`. First task: the probe events, then real payloads into `tests/fixtures/fleet-input/` | `config/hooks/fleet-state.sh`, `scripts/install-fleet.sh`, `scripts/probe.sh`, `setup.sh`, `config/hooks/tests/fleet-state.test.sh`, `config/hooks/adapters/README.md`, `.agents/rules/hooks.md`, `AGENTS.md`, `scripts/ci/bash-tests.sh`, `.env.example` (`AW_FLEET_DISABLE`, and `AW_FLEET_DIR` if CO-2a hasn't merged, §9), `deps.yaml` (the Hooks → Sindri file edge flipped to `current`) | S | CO-0 to build and merge; CO-3 on main for its proof (`sindri agents` is CO-3's) | Under a temp `HOME`: each Claude event of §5.2 writes a schema-valid file with its state; `PostToolUse` writes only when the file says `asking` and otherwise exits before `jq`; `tool_input` and the notification `message` never appear; an invalid id, a bad payload, an unwritable dir each exit 0 with no file and no stdout; the file is 0600 and the dir 0700 with `.aw-fleet`; runs under 100 ms; `AW_FLEET_DISABLE=1` and `AW_JUDGE_CHILD=1` write nothing; `install-fleet.sh --uninstall` removes only `# aw:fleet` entries; `./setup.sh --with-fleet --dry-run` prints the installs | **(owner)** after `scripts/install-fleet.sh --provider claude`, a Claude session in default permission mode asking to run `ls` shows `ASKING · approval · Bash` in `sindri agents` within 2 s, and `RUNNING` within 2 s of approving; `fleet.changed` `asking` in the ledger. Off switch afterwards: `AW_FLEET_DISABLE=1` per session, or `install-fleet.sh --uninstall --provider claude` | ideas |
| **CO-S** (owner) | Spike: does `xdg-open "$WARP_FOCUS_URL"` (a client shell's own URL) focus that Warp tab on Linux X11? Does `wmctrl -lp` find Warp's and GNOME Terminal's windows by pid? Which process does a running Codex show (the node wrapper, the native binary), and does it hold its rollout file open? | scratch dir outside the repo; findings into §13.3 | S | none | — | **(owner)** each answer recorded with the command and the Warp/Codex version; a "no" turns that method into `copy` in CO-4a | ideas |
| **CO-4a** | CA-4, CA-5 `-409`, CA-7 (binds `FocusSession` and moves its row from `PendingRows` to `ready`), CI-7 (Warp URL, X11), CI-8 (`agents focus`): focus | `…Features/FocusSession/`, `…Core/Sessions/FocusRules.cs`, `…Infrastructure/Sessions/Focus/{WarpUrlFocus,X11WindowFocus}.cs`, Host tables, goldens | M | CO-3, CO-S | `FocusRules_picks_warp_url_then_x11_then_copy`, `FocusRules_copy_on_wayland_without_remote_control`, `Focus_refuses_a_session_caller`, `Focus_refuses_a_recycled_session` (`-409`), `Focus_argv_never_contains_a_shell`, `Focus_child_env_takes_display_from_the_terminal`, `Socket_outside_the_user_tmux_dir_is_refused`, `Focus_answers_copy_under_a_test_override`; a fake `ISessionFocus` records argv; goldens | **(owner)** `sindri-prove.sh human agents focus claude-code/<a lane's id>` brings that lane's Warp tab forward (or, if CO-S said no, raises Warp and exits 0 with `focused-window`, or prints the copy line and exits 1); on the private-socket stub with no client it prints `tmux -S … attach -t '=…'` and exits 1; a `curl` with no caller header gets `SND-AUTH-002` | ideas |
| **U11a** | CU-1, CU-2, CU-3, CU-4, CU-6: Agents with "Outside a workflow", Focus, the sheet, the tray tooltip, the vocabulary | `src/Sindri/Aw.Sindri.Ui/Views/Agents*`, `ViewModels/Agents*`, `Api/ISindriApi.cs` (three methods), `Views/Shell/ShellWindow.axaml` (the page), `Views/AppTrayIcons.cs`, `scripts/sindri-ui-fixture.sh` (a sessions section), `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/fixture.md`, `docs/csharp/sindri-ui.md` | M | CO-3's goldens (CO-4a's for Focus; without them Focus renders `pending:CO-4a` from the catalog) | View-model tests on the fake `ISindriApi` over the goldens: the page head counts equal the rows, chips and `estimated`, problems lines, `SND-SESSION-002` page line, empty sentence and footer, `fleet.changed` refresh, 5 s poll only while visible, Focus's running state and each outcome's text, `-404` and `-409` behavior, hidden fields absent; headless frames for the page, an asking row, the copy outcome, the sheet; focus and scroll stay put on refresh | **(owner)** `sindri` opens the app; Agents lists every live Claude session with host "tmux · Warp"; a session started by hand in a plain terminal appears within 4 s; Focus on a lane brings its Warp tab forward or shows the copy line | ui |
| **CO-1b** | CI-6 for Codex and Cursor (§5.2) | as CO-1, plus the adapter fixtures | S | CO-1; CO-S's Codex answer | the CO-1 cases for each provider through `adapters/codex.sh` and `adapters/cursor.sh` (Cursor's stdout is its valid JSON); `install-fleet.sh --provider codex` prints the trust line | a Codex session (after `/hooks` trust) shows `ASKING · approval` at a permission request in `sindri agents` | ideas |
| **CO-4b** | Viewer-backed focus: a tmux session with no client opens the configured viewer (`ITerminalViewer`) | `…Features/FocusSession/` (the `opened-viewer` branch) | S | SL-2 (in P10a), CO-4a | `Focus_opens_the_viewer_when_no_client_shows_the_session` on the fake viewer | `sindri-prove.sh human agents focus` on the private-socket stub opens an `exec-template` xterm under `xvfb-run` with one client attached, outcome `opened-viewer` | api |
| **CO-4c** | kitty and wezterm focus (`terminals.json` `focus`) | `…Infrastructure/Sessions/Focus/{KittyFocus,WeztermFocus}.cs`, `config/launcher/terminals.json`, schema | S | CO-4a; a teammate on kitty or wezterm | `FocusRules_prefers_kitty_remote_control`, `KittyFocus_refuses_a_non_unix_listen_on` | on a kitty box with remote control, focus raises the right kitty window | ideas |
| **CA-6** | The launch join (§7) | `ListSessions` read, the `launches` projection | S | P10a, CO-3 | `ListSessions_joins_a_launched_session_to_its_item` | `sindri-prove.sh human open ITM-…`; the session's row carries `item` and leaves `inWorkflow=false` | api |
| **U11** | CU-5 in UI's U11 as specced | as UI §8 | M | U8, P10a, E-28, CA-6 | as UI §8 | as UI §8 | ui |
| **CO-5** | macOS (§4.4) | `…Infrastructure/Sessions/{PsProcessTable,MacFocus}.cs`, the hook's `env` block | M | CO-4a | the rule tests over a `ps` fixture | **(owner, on a Mac)** the CO-3 and CO-4a proofs | ideas |
| **CO-6** | CU-7 and the needs-you `session` kind, if Q3 is yes | API P4's projection, `GetNeedsYou`, Home | S | CO-1, CO-3, the owner's answer | `NeedsYou_lists_an_asking_session_and_clears_it` | an asking session shows on Home's Needs you and clears when approved | api, ui |
| **CO-7a** | CA-9's read, CI-10, CI-5's fifth verb: `TmuxCli.capture-pane`, `PreviewRules`, the Core/Features read | `…Infrastructure/Sessions/TmuxCli.cs`, `…Core/Sessions/PreviewRules.cs`, `…Features/GetSessionPreview/`, `tests/Aw.Sindri.Tests/Sessions/Preview*`, `tests/Aw.Architecture.Tests/Sessions*`, `docs/csharp/sindri-sessions.md` | S | CO-3 (on main); CO-2b (`TmuxCli`, the snapshot's pane and socket) | `PreviewRules_keeps_the_last_40_lines`, `PreviewRules_cuts_each_line_at_200`, `PreviewRules_strips_escape_sequences_and_control_characters`, `PreviewRules_strips_before_cutting`, `PreviewRules_strips_c1_and_bidi_controls`, `PreviewRules_strips_a_multi_line_osc_string`, `PreviewRules_marks_truncated_only_for_a_cut_line_or_the_cap`, `PreviewRules_does_not_mark_truncated_for_the_normal_window`, `PreviewRules_splits_on_LF_and_drops_CR`, `Preview_keeps_the_last_64_KiB`, `Preview_cuts_the_64_KiB_on_a_line_boundary`, `Preview_allows_one_capture_per_pane`, `Preview_answers_not_in_tmux`, `Preview_answers_timed_out_on_timeout` (fake `TimeProvider`), `Preview_skips_tmux_while_the_preview_mark_is_set`, `Preview_timeout_never_skips_the_collector_pass`, `Preview_answers_not_live_when_the_pane_is_gone`, `Preview_answers_not_live_when_the_pane_was_reused`, `Preview_is_live_when_the_agent_is_a_child_of_the_pane_shell`, `Preview_answers_not_live_when_the_pane_belongs_to_another_process`, `Preview_reports_an_unknown_ref_as_not_found`, `Preview_conflicts_on_a_stale_startedAt`, `Preview_refuses_a_session_caller_before_resolving_the_ref` (an unknown ref as a session caller gets `SND-AUTH-002`, not 404), `Preview_checks_expectStartedAt_per_request_while_a_capture_is_shared`, `Preview_writes_no_ledger_event`, `TmuxCli_capture_pane_passes_S_and_the_exact_argv`, `TmuxCli_puts_the_socket_flag_before_the_verb_for_all_five_verbs`; `ARCH-SESS-001` with five verbs | the integration-lane test `Preview_reads_the_stub_pane_through_tmux`, on the private-socket tmux stub (proof safety above): the pane's last lines come back with escapes stripped, and a pane with 300-character lines comes back cut at 200 | ideas |
| **CO-7b** | CA-9's route, `GET /sindri/sessions/{provider}/{id}/preview`, query class with no catalog row (CA-7), human token only (a `session` caller is refused with `SND-AUTH-002`, as `FocusSession`), `expectStartedAt` (`SND-SESSION-409`), and its goldens | `Host/Endpoints/EndpointTable.cs`, `Host/SindriJsonContext.cs`, `tests/Aw.Sindri.Tests/Golden/Api/sessions-preview/` (`lines`, `truncated`, `empty`, `not-in-tmux`, `not-live`, `timed-out`, `not-found`, `refused-session`; the synthetic stub pane only), `docs/csharp/sindri.md`, `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (the `GET /sessions` route list; the one further cross-spec edit this spec allows) | S | CO-7a, CO-3 | Slice test per route (red first); the goldens; `Preview_refuses_a_session_caller`; `Preview_refuses_a_request_with_no_caller`; `Preview_text_is_not_scrubbed` (a token-shaped string in a stub pane line comes back byte for byte after `PreviewRules`), `Only_the_preview_route_skips_the_served_text_scrub` (the bypass is a named marker on the route, and the test lists the routes carrying it: exactly one), `Preview_has_no_mcp_tool_or_cli_verb` (the MCP tool list and the CLI table name no preview); `Preview_logs_no_pane_text` (success, timeout, 404, 409, cancellation and a thrown decode error: every log, span and audit sink holds no capture bytes); `Aw.Sindri.Ui.Tests` in the gate (it reads every API golden) | `sindri-prove.sh curl GET /sindri/sessions/<provider>/<id>/preview` returns the stub pane's lines; the same route with `sindri-prove.sh curl --no-token GET …` and with a hand-written `curl "$(sindri-prove.sh url)/sindri/sessions/<provider>/<id>/preview"` (no caller header, as a session) are each refused `SND-AUTH-002`; `sindri-prove.sh human ledger --json` prints the same before and after the read | api |
| **CO-7c** | CU-8: the live panel in the session sheet (CU-3) | `src/Sindri/Aw.Sindri.Ui/Views/Agents*`, `ViewModels/*`, `Api/ISindriApi.cs` (one method), `docs/csharp/sindri-ui.md` | S | CO-7b, U11a (the sheet) | View-model tests on the fake `ISindriApi` and a fake `TimeProvider`, one per state of CU-8 (collapsed by default, first fetch, `not-in-tmux`, `not-live`, `timed-out`, 404 with no Refresh, 409 with Refresh re-reading `GetSession` first and showing its busy state, a second 409, serve down with dimmed last lines and polling continuing until the dimmed note clears, empty, `truncated`, scrolled up with jump to latest, ended), for each state message reaching the live-region note once per change and never the lines, and for polling every 2 s only while open, every 5 s when unfocused, not at all when minimised, fetching at once on becoming active, keeping the reader's line on refresh; a headless UI test asserts the panel's name (a `SelectableTextBlock` in a `ScrollViewer`), the toggle's and "Jump to latest"'s automation names, Tab stops and tab order, focus moving to the panel when "Jump to latest" disappears, Ctrl+A and Ctrl+C, and a 520 px sheet showing up to 16 lines; a headless frame from the synthetic stub pane | **(owner)** open a live lane's session sheet from Agents: its terminal's last 40 lines show after "Show live terminal" and refresh every 2 s; closing the panel stops the polling; recorded without a screenshot or text capture of the panel (evidence uses the synthetic stub pane). The proof reads the panel, not a counter; `sindri.sessions.preview.reads` stays as telemetry. If most sessions the owner opens answer `not-in-tmux`, raise the pty-attach alternative as a new question | ui |

### 11.1 Parallelization plan

```
now ──► CO-0 ──► CO-2a ──► CO-2b ──► CO-3 ──┬──► CO-1 proof (owner) ──► CO-6 (if Q3)
          │                                  ├──► CO-4a (needs CO-S) ──► CO-4c, CO-5
          └──► CO-1 build + merge ──► CO-1b (needs CO-S)    │
now ──► CO-S (owner) ────────────────────────┘   CO-3 goldens ──► U11a (ui)
P10a (with SL-2, taking CA-0 over) ──► CO-4b, CA-6 (api) ──► U11 (ui, with U8, E-28)
CO-3 ──► CO-7a ──► CO-7b (api) ──► CO-7c (ui, with U11a)     (CO-7a starts after CO-3 is on main; never delays CO-2b, CO-3 or CO-4a)
```

| Wave | Ideas lane (one builder) | Second builder (bash only, when a slot is free) | API lane | UI lane | Owner |
|---|---|---|---|---|---|
| 0 | CO-0 | — | reviews the CA rows in CO-0's docs | — | CO-0's status observation; CO-S spike |
| 1 | CO-2a | CO-1 built and merged (needs only CO-0; its proof waits for CO-3) | — | — | — |
| 2 | CO-2b, then CO-3 | CO-1b (after CO-S) | reviews CO-2a, CO-3 | reads CO-3's goldens into U11a's plan | — |
| 3 | CO-4a, then CO-7a | — | reviews CO-4a; CO-7b | U11a; CO-7c (after CO-7b) | CO-1's proof (once CO-3 is on main); answers Q3 and Q7 |
| 4 | CO-5 (when a Mac is free), CO-4c (on demand) | — | P10a with SL-2 → CO-4b, CA-6 | U11 (after U8) | → CO-6 |

- CO-1 touches only bash and docs and needs no dotnet build, so the meta can build and merge it beside CO-2a; its
  owner proof reads `sindri agents`, so it runs only once CO-3 is on main.
- The preview (CO-7a to CO-7c) is built after CO-3 on a free slot and **never delays CO-2b or CO-3**: **CO-7a starts after CO-3 is on main and never delays CO-2b, CO-3 or CO-4a**; CO-7b and CO-7c queue in the API and UI lanes behind their current phases.
- Nothing in waves 0–3 waits on P10a, P11 or U8. **U11a's place in the UI lane's queue** (it can start once CO-3
  merges, ahead of U8) is the owner's call (Q7).
- Hotspots: `SindriErrors.cs`, `EventCatalog.cs`, the endpoint and command tables, `SindriJsonContext`,
  `commands/catalog.json`, `SindriHostServiceCollectionExtensions.cs`, `ISindriApi.cs`, `ShellWindow.axaml`,
  `deps.yaml`; each phase appends at the anchors (API "Shared files"). CO-2a (the events-catalog golden), CO-3 (the
  commands catalog's pending row and the sessions goldens), CO-4a and U11a change goldens the UI tests read, so their
  gates run `Aw.Sindri.Ui.Tests`. CA-0's `Core/Launch/` and `Infrastructure/Launch/` are shared with SL-2: P10a
  rebases on them.

### 11.2 When Sindri shows your sessions

| What | Phase | Waits on |
|---|---|---|
| Claude Code sessions, tmux or not, with running/idle and host, in `sindri agents` and `GET /sindri/sessions` (Linux) | CO-3 | CO-0, CO-2a, CO-2b; nothing outside the ideas lane |
| …with "asking for approval" | CO-1 | CO-0 (shows once CO-3 is in) |
| …in the app's Agents page, with Focus | U11a | CO-3 (CO-4a for Focus); its place in the UI queue (Q7) |
| Codex and Cursor CLI sessions | CO-2a/CO-3 for the process; CO-1b for ids and states | CO-S; Codex `/hooks` trust on the box |
| Sindri-launched sessions grouped under their workflow | CA-6, U11 | P10a, U8 |
| macOS | CO-5 | CO-4a |
| The live read-only preview of a session's terminal, in its sheet | CO-7c | CO-3, CO-7a, CO-7b, U11a; none of CO-2b, CO-3 waits on it |

## 12. Privacy and threat model

The threat model is the repo's: honest mistakes, not a hostile agent (README, `docs/csharp/bugfix.md` "Threat model").
The rules exist so nothing private leaks and nothing is damaged by accident:

- **No transcript or prompt text** enters the collector: transcripts are only `stat`ed (§5.2, §5.3), `cmdline` is read
  to its first NUL only (a `claude -p` prompt sits after it), the hook stores no free text (CO7), and the collector
  pass never captures a pane. The one exception is the on-demand **live preview** (CA-9; owner decision 2026-10-17,
  reversing Q4): `tmux capture-pane` text, served **as-is with no scrub**, to the human token only, read when the app
  asks and not logged. The owner: "it's the same as me just opening the session myself".
- **Titles** (Claude's `name`, which may be generated from a prompt) pass through `Aw.Redaction`
  (`Redactor.Default.Scrub`, `docs/csharp/redaction.md`), are capped at 80 characters, are dropped on
  `Outcome: Blocked`, and reach human callers only.
- **Environment:** the allowlist (§4.1 step 5) is applied while parsing; nothing else from `environ` is kept, logged or
  returned. `/proc/<pid>/environ` is readable only for the user's own processes, the only scope read.
- **Claude's files:** `.key` files are never opened (`*.json` glob only), and the record reader parses only the
  fields it uses, so `messagingSocketPath` and `bridgeSessionId` can't surface.
- **Session callers** see only their repo's rows, and can't read previews (CA-9), and no title, path, socket, tmux session, pane, pid or resume
  command (CA-1, CA-2): an agent that knew a teammate's pane on the default socket could act on it by accident.
  Public events carry no cwd, title or socket (CA-3).
- **No damage:** tmux is read with four verbs and the preview's read-only `capture-pane` (CO9, CA-9); pruning deletes only marked, schema-valid, dead files (§5.4); a
  failed pass changes nothing (§4.1); focus runs argv from a fixed list with checked inputs (§6.2).
- **Local only:** the API stays loopback (API §6); nothing in the collector sends anything off the box.

## 13. Review notes

### 13.1 /autoplan pass 1 (2026-10-16)

Five lenses on rev 1: product PASS, security PASS, architecture, design and devex NEEDS_WORK; no CRITICAL. Every HIGH
and MEDIUM was fixed in rev 2; the LOWs were fixed or are answered here.

| Finding (lens, severity) | Resolution |
|---|---|
| Collector in Features, appending through the command chain (arch HIGH) | CO2, CO3, CI-4: `Host/Server/SessionCollectorHost`, lease-gated; `RecordFleetChanges` appends with the grant, no gate, audit or catalog row |
| The app can't get the focus outcome or the copy line; no worker, no restart rule (arch HIGH, design HIGH, devex HIGH) | CO10, CA-4: focus is synchronous (≤ 3 s), returns `outcome`, `text` and `copy`; one non-public event; CLI exits defined |
| Attach's in-between and error states (design HIGH) | CU-2: "Focusing…", `-404`, `-409` texts |
| Attention colour and invented state words (design HIGH ×2) | CO14, CU-6: `RUNNING`/`ASKING`/`IDLE`/`UNKNOWN` chips added to `fixture.md`, ink colours, `--attn` only on Q3 yes; "waiting" gone from the API and the tooltip |
| No C# catalog reader on main; "none waits on P10a" was false (devex HIGH) | CO13, CA-0: CO-2a builds the minimal reader that SL-2 extends; the claim now holds |
| Cursor's process name is `agent`, not `cursor-agent` (devex HIGH, arch MEDIUM) | §3, §4.1, §4.2: matched by name `agent` with an exe path containing `cursor` |
| `probe.sh` doesn't hook the events CO-1 must record (devex HIGH) | CI-6, CO-1: probe gains those events first |
| `sindri doctor` sessions line in the proof but no row (devex HIGH, product LOW, arch LOW) | dropped; CO-2a proves through `sindri ledger --type fleet.changed` |
| Matching on `comm` is fragile; `exec -a` stub invisible (arch MEDIUM, devex MEDIUM) | §4.1 step 2: argv[0] then exe then comm; the stub is a copy of `sleep` named `claude` with `procStart` read after start |
| Stale `WARP_FOCUS_URL` through tmux (arch MEDIUM) | §6.1: terminal facts only from the client chain; CO-2b's fixture test |
| `claude -p` children flood the ledger; a failed pass makes all sessions "gone" (arch MEDIUM, security MEDIUM ×2) | §4.1 steps 2, 8, 9 and the failed-pass rule; CO3's two-pass rule; `AW_JUDGE_CHILD` |
| Transcript-mtime flapping (arch MEDIUM) | §5.2: `PostToolUse` ends `asking` (written only when asking); transcript fallback after 10 s |
| A `git` spawn per pass (arch MEDIUM) | §4.1 step 7: repo cached per real cwd, resolved off the pass |
| `deps.yaml` edges undeclarable; fleet dir owner (arch MEDIUM, devex MEDIUM) | CI-1: Sindri owns `fleet/`, Hooks → Sindri file edge, provider folders not edges |
| Event volume understated (arch MEDIUM) | CO3: `asking` only after 5 s, ≤ 6 per hour, leaving `asking` not ledgered, rows per day estimated |
| Pruning could delete fixtures (security MEDIUM) | §5.4: marker, mode, schema check, never in a git work tree or under a test override |
| Session callers see pane, pid, title, resume (security LOW ×3) | CA-1, CA-2, §12: human only |
| `terminals.json` holds launch names, not process names (devex MEDIUM) | §9: `processNames` in CO-0 |
| Profile keys need a schema change (devex MEDIUM, arch LOW) | §9: moved to `launcher.json` `sessions` |
| `sessions` verb next to `session wrap`; dropped `/timeline` and tokens unstated (devex MEDIUM) | CI-8: `sindri agents`; §7 states what is dropped |
| "Attached" means three things; two Attach actions (design MEDIUM) | CO8, CO12: the action is Focus, the group "Outside a workflow"; launched rows keep `OpenItem:attach` |
| No sheet, no page head, no `-002` rendering (design MEDIUM) | CU-1, CU-3 |
| CO-2 is an L (arch LOW) | split into CO-2a and CO-2b |
| Scope: four focus methods and three providers where the owner runs Claude in Warp on X11 (product MEDIUM) | CO-4a ships Warp URL and X11; kitty and wezterm move to CO-4c; Codex and Cursor hooks to CO-1b |
| Claude's `status` may never read `idle` (product MEDIUM) | CO-0's first task; §4.2 fallback |
| U11a's priority in the UI queue (product MEDIUM) | Q7 |
| Two ledger events per attach (product LOW) | one non-public `fleet.focused` |
| U11's dependency on "API §11 collector" and E-28 spend (product LOW) | CU-5's requested edit |
| No uninstall for the global hook (product LOW) | CO-1: `--uninstall` and `AW_FLEET_DISABLE` |
| Focus children lack `DISPLAY`/D-Bus (arch LOW) | §6.2 |
| Restart re-emits `appeared`; `Invalidates`; the snapshot exception (arch LOW) | §4.1 step 9; CA-3; CO3 |
| `KITTY_LISTEN_ON` unvalidated; test overrides in a real run; hook stdout and file mode; `problems.text`; timeouts (security LOW) | §6.2, §9, §5.4, CA-1, CO9 |
| Proof counts hard-coded at six; session-caller curl via `url`; seeded profile (devex MEDIUM/LOW) | §11: counts from `jq`, `sindri-prove.sh url`, seeded profile |
| Codex is a node wrapper (devex LOW) | CO-S |
| Linux `comm` truncation at 15 characters (devex LOW) | §4.1 step 2 |
| Glyph per state, keys, accessible names, live region (design LOW) | CU-1, CU-2, CU-6 |
| README X rows and API §11 should point here (devex LOW) | done at merge by the docs-sync of the first phase PR (CO-0 adds the pointer lines) |

### 13.2 Re-check of rev 2 (2026-10-16, rev 2.1)

Each claim checked against origin/main before the fix.

| Finding | Resolution |
|---|---|
| CO-0's `status` observation is an owner step with no command | CO-0 row: **(owner)**, `watch -n1 "jq -c '{status,statusUpdatedAt}' ~/.claude/sessions/<pid>.json"` through a turn end and a permission prompt |
| CO-0's file list missed the checks and tests its own Tests column names | CO-0 files: `scripts/check-launcher.mjs` (`processNames` required), `scripts/tests/check-launcher.test.sh` (drift case), `scripts/tests/meta-launch.test.sh` (`sessions` case), new `scripts/tests/check-fleet.test.sh`, `scripts/ci/changed-areas.sh` (+ its test; `schemas/` and `tests/fixtures/` didn't trigger the bash job), AGENTS.md's bash-test list, in place of "scripts/ci/ (wire it)" |
| `.env.example` in CO-0 fails ENV-EXAMPLE-002 (an entry nothing reads) | §9, CI-1: each entry lands with its first reader: `AW_SESSIONS_*` and `AW_FLEET_DIR` in CO-2a, `AW_FLEET_DISABLE` in CO-1; `AW_FLEET_DIR` goes in CO-1 instead if CO-1 merges first, since the hook's `${AW_FLEET_DIR:-…}` is a read by the scan's rules |
| A current Hooks → Sindri file edge in CO-0 fails DEPS-STALE-001 | CI-1, CO-0, CO-1: declared `planned` in CO-0, flipped to `current` by CO-1 (`deps.yaml` in its files) when `fleet-state.sh` writes `fleet/` |
| The hook's provider value vs the `AgentId` was unstated | §5.1: `AW_HOOK_PROVIDER` `claude` or unset → `claude-code`; `codex`, `cursor` unchanged; schema and fixtures follow |
| "Other users" by `/proc/<pid>` directory owner | §4.1 step 1: `/proc/<pid>/status` `Uid:` against an injected own uid (tests pass the fixture's) |
| A test override as `SND-SESSION-002` would replace the list | CA-5, §9, CU-1: `SND-SESSION-003` test-override, informational, beside the list; only `-002`'s `unsupported-platform`, `proc-unreadable`, `disabled` replace it |
| CO-2a's proof needed two `serve` runs (the override replaces the records dir); its events-catalog golden change wasn't gated by the UI tests | CO-2a: one `serve` with `AW_SESSIONS_CLAUDE_DIR` set from the start to a seeded copy of the live records; `tests/Aw.Sindri.Tests/Golden/Api/events-catalog/catalog.json` in its files and `Aw.Sindri.Ui.Tests` in its gate (`GoldenContractTests` reads every API golden); §11.1's list updated |
| `ARCH-SESS-001` forbade the attach argv the copy line and viewer need | CO9, CI-5: exactly one builder, `Core/Sessions/TmuxAttachLine`, never executed by Sindri; the four read-only verbs stay the only ones Sindri runs |
| CO-1 placement, terminal names in C#, catalog rows | §11, §11.1: CO-1 built after CO-0, proven only after CO-3 (diagram, waves, Needs on main agree); CO-2b adds an embedded `terminals.json` reader for `processNames`; CA-7, CO-3: no catalog rows for the queries `ListSessions` and `GetSession` (ARCH-CQRS-003), paths `tests/Aw.Sindri.Tests/Golden/Api/commands/catalog.json` and `Host/Catalog/CommandCatalog.cs`; CO-3 adds `FocusSession` to `PendingRows` (`pending:CO-4a`) so U11a can render it |
| Suggestions | CI-3: `TmuxCli` uses its own bounded `Process.Start`, as `GitRepoLocator`; session goldens in `tests/Aw.Sindri.Tests/Sessions/Golden/`; CA-0: `LauncherConfigFile` accepts `viewer`, `host`, `meta` unread; §4.1 step 3, §4.2, §6.1: T3 records found by entrypoint and cwd regardless of `kind` (unverified); §7: the launch routes beside `/sessions/{provider}/{id}`, and `gone` vs `session.ended` |

### 13.3 CO-S findings

To be filled by the spike.

## 14. Open questions

Each has a provisional answer, used until the owner says otherwise.

| # | Question | Provisional answer |
|---|---|---|
| Q1 | Install the `aw:fleet` hook by default, or opt-in? | Opt-in (`--with-fleet`) until the switch, like `--with-meta`; the switch makes it default with the other hooks |
| Q2 | Should Sindri read the default tmux server at all (the team-socket hazard)? | Yes, read-only, with CO9's five verbs (four reads and the preview's read-only `capture-pane`); it is how the owner's current team is found. The owner may restrict reading to `sessions.tmuxSockets` plus `-L sindri` |
| Q3 | Does a session asking for approval belong in Home's Needs you (R8-13's union) and the tray badge? | Yes, as a `session` row kind below decisions (CO-6), since a permission prompt waits on the owner. Until answered, only the Agents page and the tray tooltip show it, in ink |
| Q4 | Add a live preview (`tmux capture-pane`, X55, X59(c)) to the session sheet? | **Reversed by the owner, 2026-10-17: yes.** A live READ-ONLY preview of an agent session's terminal, shown in the session sheet (CU-3): `tmux capture-pane`, all active sessions the collector sees on any tmux server it reads ("otherwise it's not a valuable control plane"), the last 40 lines cut at 200 characters, refreshed every 2 s while the panel is open, no redaction scrub ("it's the same as me just opening the session myself"), reads not logged, human-only. CA-9, CO-7a to CO-7c |
| Q5 | Add DB §4.3's finer states (`stuck`, `interrupted`, `failed`, `limited`, `crashed`, `background`, subagent roll-up) and a timeline? | Not in v1: they need transcript reading. A later phase reads the transcript tail (≤ 1 MB, newest first, DB §4.1's bounds) for Claude only, after the owner has used v1 |
| Q6 | Show sessions of other users on a shared box? | No: the current user's processes only |
| Q7 | Does U11a jump ahead of U8 and the other UI-lane phases once CO-3 merges? | Yes: it needs nothing U8 builds, and it is what the owner asked for. The meta schedules it |

## 15. Changelog

- **Rev 1 (2026-10-16):** first draft, from the owner's question through the meta. CO1 records the owner's
  pre-approval of the build.
- **Rev 2 (2026-10-16):** /autoplan pass 1 (§13.1). Collector as a lease-gated host service with an ungated append;
  synchronous Focus (renamed from Attach) with one non-public event; the event renamed `fleet.changed` with a two-pass,
  5 s and hourly-cap rule; process matching by argv[0], exe, comm with descendant and judge-child exclusion; terminal
  facts from tmux client chains only; `PostToolUse` ends `asking`; repo cache; safe pruning; human-only fields; state
  words as UI chips; the minimal launcher reader (CA-0); `sindri agents`; config in `launcher.json`; CO-2 split into
  CO-2a and CO-2b; kitty/wezterm (CO-4c) and Codex/Cursor hooks (CO-1b) split out; U11a's sheet, states and texts;
  Q7.
- **Rev 2.1 (2026-10-16):** the re-check of rev 2 (§13.2): CO-0's owner `watch` step and its full file list;
  `.env.example` entries moved to their first readers; the Hooks → Sindri edge planned then current; the provider
  mapping; the `Uid:` check; `SND-SESSION-003` for test overrides; CO-2a's one-serve proof and UI-test gate;
  `ARCH-SESS-001`'s one attach-line builder; CO-1 proven after CO-3; CO-2b's `terminals.json` reader; no catalog rows
  for queries and a `FocusSession` pending row; the suggestions listed there. CO1 now has the spec PR marked ready and
  merged once /autoplan-clean.
- **Rev 2.2 (2026-10-17):** the live read-only session preview, from the owner decisions of 2026-10-17 (relayed by the
  meta). Q4 reversed (§14); `ARCH-SESS-001` (CI-5) allows a fifth `TmuxCli` verb, `capture-pane` with its exact
  read-only argv, and CO9 and CI-3 say so; new contract row CA-9 (the response, the 40 × 200 bounds, escape rules,
  served as-is as an explicit exception to "served text is scrubbed", on-demand read with a 1 s `TimeProvider`
  timeout, not logged, human-only); CA-7 notes the preview is a query with no catalog row; CU-8 (the panel) and CI-10 (the ideas-lane
  rows); phases CO-7a (ideas), CO-7b (api) and CO-7c (ui) after CO-3, with requests in §10.2 and §10.3; §11.1 says the
  preview never delays CO-2b or CO-3; §11.2 and §12 updated to match.
- **Rev 2.3 (2026-10-18):** the #324 /autoplan follow-up, round 1. The preview phases are renamed CO-5a/b/c to
  CO-7a/b/c (CO-5 stays macOS), and the changes are listed below.

| Id | Where | What changed |
|---|---|---|
| C1 | §11.2, §11.1, diagram, Needs | Preview phases renamed CO-7a, CO-7b, CO-7c (CO-7 was unused); added to the wave table after CO-3, one lane each; one start condition: CO-7a starts after CO-3 is on main and never delays CO-2b, CO-3 or CO-4a |
| C2 | CA-9, CA-5, CA-7, §10.2, CO-7b | One route `GET /sessions/{provider}/{id}/preview`; an unknown ref is 404 `SND-SESSION-404`; reasons `not-in-tmux`, `not-live`, `timed-out`; a `session` caller is refused with `SND-AUTH-002`; goldens list updated |
| C3 | CO10, CA-9, CO-7b | The preview is the one human-only query: the endpoint checks the caller kind the auth layer resolves, deny by default; no MCP tool and no CLI verb; tests `Preview_refuses_a_session_caller` and `Preview_has_no_mcp_tool_or_cli_verb` |
| C4 | CA-9, CU-8 | The response carries `startedAt`; `expectStartedAt` mismatch is 409 `SND-SESSION-409` (as CA-4); the panel sends it |
| C5 | CA-9, CO-7a | A timeout marks the socket under CO9's 10 s skip window; while marked the preview answers `timed-out` without spawning tmux |
| C6 | CA-9, CI-10, CO-7a | `PreviewRules` as six numbered steps; strips C1 and bidi controls; cuts at UTF-16 code units; tests `PreviewRules_strips_before_cutting`, `PreviewRules_strips_c1_and_bidi_controls` |
| C7 | CA-9, CO-7a | Capture reads at most 64 KiB; one capture in flight per pane |
| C8 | CA-9, CU-8, CO-7b | "Not logged" covers every path (no pane text in ledger, audit, log or span); the UI keeps lines in memory only; counter `sindri.sessions.preview.reads` and a duration histogram; test renamed `Preview_logs_no_pane_text` |
| C9 | CA-9, Q2, CO-7b, CO-7c | CA-9 names the exception to the served-text scrub rule; Q2 says five verbs; goldens, headless frames and evidence use the synthetic stub pane only |
| C10 | CU-8, CO-7c, Q4, §11.2 | The panel ships in the session sheet (CU-3) only; step details reuse it later as a U11 row; the owner proof opens a live lane's session sheet from Agents |
| C11 | CO-7a, CO-7b | CO-7a's proof is the integration test `Preview_reads_the_stub_pane_through_tmux`; the `sindri-prove.sh curl` proof moved to CO-7b |
| C12 | CU-8, CO-7c | CU-8 written out: focus and scroll behaviour, automation names, header label and age, what open means, fixed sentences per state, tail-following, token pair; a view-model test per state |
| C13 | CA-7, CO-7b, CI-3, CI-5 | CO-7b adds the route and its goldens; its docs list adds the API surface spec's route table row; CI-3 and CI-5 say five verbs, `capture-pane` from CO-7a |
| C14 | Status line, §15 | Revision 2.3; this table |

- **Rev 2.4 (2026-10-18):** the #324 /autoplan follow-up, round 2. The changes are listed below.

| Id | Where | What changed |
|---|---|---|
| R4 | CA-9, CO9, CI-10, CO-7a | Undoes C5: a preview timeout sets the preview's own per-socket mark for 10 s, read only by previews; the collector pass never reads it; test `Preview_timeout_never_skips_the_collector_pass` |
| R5 | CA-9 steps 4 and 6, CU-8 | `truncated` means a line cut at 200 or the 64 KiB cap dropped output; the last-40 window does not set it; the footer says "Last 40 lines" always and the cut note only when `truncated` |
| R6 | CA-9, CO-7a | `not-live` is a pane that is gone or whose `#{pane_pid}` is not the session's process, checked by one bounded `display-message` before the capture; `expectStartedAt` stays for a recycled ref |
| R7 | Engine spec, API surface spec, launcher spec, README X59(c) | Their preview sentences now say unscrubbed and human-only (owner, 2026-10-17; CA-9). Edited: `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (the `capture-pane` sentence at line 962), `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (line 1116), `docs/superpowers/specs/2026-10-15-sindri-session-launcher-design.md` (the pane-capture note at line 425; the lane owns that spec), `docs/sindri/README.md` (X59(c)); no other edits |
| R8 | CU-8, CO-7c | Collapsed by default with a "Show live terminal" disclosure; scrolled-up pins by content; ended and 409 keep dimmed lines, stop polling and offer Refresh; 409 has its own sentence; state messages go to the polite live-region note; dimmed text 4.5:1; no wrap, horizontal scroll, selectable and copyable |
| R9-1 | CA-9, CO-7a, CO-7b | Check order (caller kind, ref, `expectStartedAt`, then the 200 answers) per request, before the shared wait; `Preview_refuses_a_session_caller_before_resolving_the_ref`; the 409 test moved to CO-7a; `Preview_refuses_a_request_with_no_caller` |
| R9-2 | CA-9, CI-10, CO-7a | LF split and CR dropped; the 64 KiB cut on a line boundary; tests for both |
| R9-3 | CA-9, CO-7b | The "not logged" test covers success, timeout, 404, 409, cancellation and a thrown decode error; the counter's result values are listed; the proofs name their commands (`curl --no-token`, a hand-written no-header `curl`, `ledger --json` before and after); the API surface row is marked as the one cross-spec edit |
| R9-4 | CU-8, CO-7c | Polling: 2 s focused, 5 s unfocused, stopped when minimised; header reads Paused or Ended; the Focus hint shows only when Focus is enabled; the age is the last successful capture; the region is a Document control with named controls and a headless UI test; the owner proof is recorded without a text capture and adds a week-one read of the counter by result |
| R9-5 | Status line, §15 | Revision 2.4, dated; this table |

- **Rev 2.5 (2026-10-18):** the #324 /autoplan follow-up, round 3. The changes are listed below.

| Id | Where | What changed |
|---|---|---|
| R10-1 | CA-9, CO-7a | `not-live` fixed: the pane is live when the session's process is `pane_pid` or a descendant of it (a parent walk of at most 64 hops through the process table the collector reads); tests `Preview_is_live_when_the_agent_is_a_child_of_the_pane_shell`, `Preview_answers_not_live_when_the_pane_belongs_to_another_process`. One 1 s deadline covers both tmux spawns; a pane reused between them is a one-poll window |
| R10-2 | CU-8, CO-7c | 409 Refresh re-reads `GetSession` first, then fetches with the new `startedAt`, with a busy state; a second 409 shows the sentence again; 404 offers no Refresh; view-model tests |
| R10-3 | CU-8, CO-7c | "Jump to latest" hands focus to the panel; Ctrl+A and Ctrl+C by keyboard; the panel is a `SelectableTextBlock` in a `ScrollViewer` (no Document control type); up to 16 lines visible; serve-down polling continues and the note clears on the next good read |
| R10-4 | CA-9, CI-10, CO-7a | Escape stripping runs over the whole capture before the LF split (steps renumbered, still six); DEL, U+061C, U+200E, U+200F, U+2028, U+2029 stripped; decode with replacement characters; test `PreviewRules_strips_a_multi_line_osc_string` |
| R10-5 | CA-9, CO-7b | The unscrubbed exception covers the preview route's `lines` only; tests `Preview_text_is_not_scrubbed`, `Only_the_preview_route_skips_the_served_text_scrub`; the CA-9 citation points at §12 |
| R10-6 | CI-5, CO-7a | `ARCH-SESS-001`'s argv test asserts the socket flag `-S <absolute socket path>` before the verb, apart from `capture-pane`'s own `-S -40` |
| R10-7 | R7, CO-7b, CO-7c | R7 lists the launcher spec's aligned note; CO-7b's API edit is "the one further cross-spec edit" and names the `GET /sessions` route list; the CO-7c owner proof reads the panel, not a counter (the counter stays as telemetry) |
| R10-8 | Status line, §15 | Revision 2.5; this table |

- **Rev 2.6 (2026-10-18):** decision (API lane, #344 review): `startedAt` can be null (Codex and Cursor rows have none); in CA-4 and CA-9, when `startedAt` is null the caller sends `expectStartedAt` null and the server skips the start-time comparison, matching on provider, sessionId and pid alone. `focus.why` is the stable code `not-built` until CO-4a (the catalog row keeps `pending:CO-4a`). The Claude-records source failing is `SND-SESSION-001`, never a fourth `-002` reason. Also: what CO-3 shipped, written back (#344 review I1): `recentlyEnded[]` is `{session, endedAt, resume}`; `inWorkflow` means "has a launch" until CA-6; `SessionView` is `{session, cwd, stateHistory, asOf}`; the sessions goldens are `empty`, `fixture-three-providers`, `session-scoped`, `problems-source-failed`, `recently-ended` and the 503 `-002` bodies `unsupported-platform`, `proc-unreadable`, `disabled`; the status counts are box-wide for every caller.
- **Rev 2.7 (2026-10-18):** what CO-1 shipped, written back (#354 review). `Notification` `permission_prompt` never writes: `PermissionRequest` owns `asking` `approval`, and the notification, which arrives seconds later with no link to a request, would undo `running` after a quick approval (§5.2). Deny and Esc fire no hook event on Claude Code 2.1.302 (probed), so `asking` ends by a newer record status (expected, not probed) or the 10 s transcript-mtime fallback, which shows `running (estimated)` (§5.2 merge rule). The fixtures for the hook's input are `tests/fixtures/fleet-input/` (real payloads, `provenance.json`).
