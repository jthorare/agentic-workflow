# Sindri session collector: contract files

The session collector shows every live agent session on the box (Claude Code, Codex, Cursor CLI, added entries), in tmux
or not, with its state (running, asking, idle) and its host, in `sindri agents` and `GET /sindri/sessions`. This page is
the contract the first phase (CO-0) lands: the fleet state file, the fixtures every reader and writer is tested against,
the `launcher.json` `sessions` key, the terminals' process names, and the dependency edge. The design is
`docs/superpowers/specs/2026-10-16-sindri-session-collector-design.md` (rev 2.1, "the spec" below); the collector itself
is built by CO-2a to CO-3 and the hook by CO-1.

## The fleet state file

`$AW_FLEET_DIR/<provider>/<sessionId>.json` (default `$AW_FLEET_DIR` = `$AW_STATE_DIR/fleet`), mode 0600, in provider
directories of mode 0700 that carry a `.aw-fleet` marker file. It is written by the `aw:fleet` hook
(`config/hooks/fleet-state.sh`, CO-1) and read, and pruned, by the collector (spec §5). One JSON Schema,
`schemas/fleet-state.schema.json` (2020-12, validate with Ajv2020), is the contract for the bash writer's tests and the C#
reader.

```jsonc
{
  "schema": 1,
  "provider": "claude-code",            // an AgentId, never "claude"
  "sessionId": "3970893f-…",
  "event": "PermissionRequest",         // the event as the canonical hook sees it
  "state": "asking",                    // running | asking | idle | ended
  "reason": "approval",                 // asking only: approval | input | auth
  "tool": "Bash",                       // approval only: <= 64 chars of [A-Za-z0-9_.:-]; never tool_input
  "at": "2026-10-16T11:02:03.123Z",
  "cwd": "/home/…/repo",
  "transcript": "/home/…/x.jsonl",      // a path, or null
  "agentPid": 3023421, "agentStartTicks": 13279367,   // Linux; both null where unknown
  "env": {}                             // macOS only (spec §4.4); {} on Linux
}
```

The schema also states what must never be in the file: unknown keys are errors, so a `tool_input` or a notification
`message` key fails validation (privacy, spec §12). `reason` is present exactly when `state` is `asking`, `tool` only
with `reason: approval`, and `agentStartTicks` is an integer exactly when `agentPid` is.

**Provider mapping.** The hook learns its provider from `AW_HOOK_PROVIDER`, which the adapters set to `codex` or
`cursor` and which is unset or `claude` for a Claude hook. It maps `claude` or unset to `claude-code`, `codex` to
`codex`, `cursor` to `cursor`; any other value writes nothing. The `provider` field and the directory name are always
the `AgentId`: `$AW_FLEET_DIR/claude-code/`, never `claude/`. Every fixture below follows this, and
`scripts/check-fleet.mjs` fails a valid fixture that sits outside the directory named by its own `provider`.

## Fixtures

| Path | What |
|---|---|
| `tests/fixtures/fleet/` | Fleet state files with `manifest.json`: per file, the expected verdict (`valid` or `invalid`) against the schema. Valid ones are one per hook event and provider (Claude's seven events and three notification kinds, Codex, Cursor); invalid ones each break one rule (provider, state, reason and tool pairing, `tool_input`, `message`, a path as session id, a bad timestamp, missing `env`, a file that does not parse) |
| `tests/fixtures/sessions/proc/` | A `/proc`-shaped tree for the C# process-table adapter (`AW_SESSIONS_PROC_ROOT`, CO-2a): `<pid>/{stat,cmdline,environ,status}` (NUL-separated where `/proc` is), `exe`, `cwd` and `fd/<n>` as symlinks, and `net/unix`. Processes: Claude under Warp, under kitty, in a tmux lane and over ssh; a `claude -p` under judge; Codex with its rollout on an fd; Cursor's `agent` and another program also called `agent`; aider; a tmux server with its socket line; a recycled pid; a zombie; another user's process (`status` `Uid:`). A fake `ANTHROPIC_API_KEY` in the Claude `environ` files must reach no snapshot, log or response |
| `tests/fixtures/sessions/claude-sessions/` | Claude Code session records for `AW_SESSIONS_CLAUDE_DIR`: two that match a live process, one whose pid was recycled (`procStart` differs), one with no process, and a `.key` file the reader must never open |
| `tests/fixtures/sessions/manifest.json` | Each process with its role and, where it matters, what the collector must conclude; the record files with their kind |

`node scripts/check-fleet.mjs [--root DIR]` validates every fleet fixture against its manifest verdict and checks that the
sessions tree and its manifest agree (exit 0 ok, 1 a check failed, 2 a tool error). `bash scripts/tests/check-fleet.test.sh`
proves the repo passes and that each kind of drift fails. Ajv is a dev dependency of `scripts/` (`npm ci --prefix scripts`;
the test installs it when missing). The hook's own test (CO-1) and the C# reader (CO-2a) run the same fleet files.

## `launcher.json`: the `sessions` key

Optional, additive, in schema 1 (`schemas/launcher.schema.json`, `$defs.sessions`; the full file is in
`docs/csharp/sindri-launcher.md`). Box facts only; nothing here needs a profile change.

| Key | Default | Meaning |
|---|---|---|
| `sessions.enabled` | true | The whole collector (`false` gives `SND-SESSION-002 disabled`) |
| `sessions.pollSeconds` | 2 | Pass cadence, 1 to 30 |
| `sessions.providers` | every catalog entry | Which agents to discover (`["claude-code", "codex"]`) |
| `sessions.tmuxSockets` | `[]` | Extra absolute socket paths to read besides the ones found from processes |
| `sessions.claudeSessionsDir` | `~/.claude/sessions` | For a relocated Claude config (`CLAUDE_CONFIG_DIR`) |

The bash reader (`scripts/launcher-lib.sh`) refuses unknown keys, so CO-0 adds `sessions` to `x-aw-keys.root` and the
reader accepts the key and ignores its content. An `aw-meta-launch` installed before this change refuses a file that
has the key until `scripts/install-meta.sh` reinstalls it. The environment variables the collector will read
(`AW_FLEET_DIR`, `AW_FLEET_DISABLE`, `AW_SESSIONS_PROC_ROOT`, `AW_SESSIONS_CLAUDE_DIR`) are in `.env.example`: each
landed with the phase whose code first reads it (CO-1 for `AW_FLEET_DISABLE`, CO-2a for the rest), because
`scripts/check-env-example.sh` fails an entry nothing reads.

## Terminals' process names

Each row of `config/launcher/terminals.json` has `processNames`, the names the terminal runs as (`kitty`, `wezterm-gui`,
`gnome-terminal-server`, `alacritty`, `konsole`, `foot`, `xterm`). They are matched against argv[0]'s basename or the exe's, as spec §4.1 step 2 does for agents, because the kernel truncates `comm` to 15 characters and `gnome-terminal-server` is longer. Host detection (spec §6.1) walks a session's parent
chain and calls it host `terminal` when a process name is in a row's `processNames`. `node scripts/check-launcher.mjs`
fails a row without them.

## Dependency edge

`deps.yaml`: Sindri owns `fleet/` as a state-root path (`files`), and a Hooks to Sindri `file` edge records the hook's
writes. The edge is `current` since CO-1 added `config/hooks/fleet-state.sh`, because
`check-deps` fails a current edge the source lacks (DEPS-STALE-001) and a planned one it finds (DEPS-STALE-002).
`~/.claude/sessions` and the provider folders are outside the state root, so they are not edges.

## Claude's record `status` (owner observation, pending)

Claude Code's `~/.claude/sessions/<pid>.json` has an undocumented `status` field. Whether it reads `idle` at a turn end
and whether it changes while a permission prompt is open decides whether CO-3 can trust it or must use the transcript
estimate until the hook (spec §4.2, §5.3). The observation is the owner's (read-only):

```bash
watch -n1 "jq -c '{status,statusUpdatedAt}' ~/.claude/sessions/<pid>.json"
```

run beside one Claude session in default permission mode, through one turn end and one open permission prompt. Record
the Claude version (`claude --version`) and the readings here:

| Moment | `status` | `statusUpdatedAt` moved? |
|---|---|---|
| Turn running | _pending_ | _pending_ |
| Turn ended, prompt back | _pending_ | _pending_ |
| Permission prompt open | _pending_ | _pending_ |
| After approving | _pending_ | _pending_ |

Claude Code version: _pending_.

## Host detection (CO-2b)

`Core/Sessions/HostRules.Detect` answers, for one live session, what shows it (collector spec §6.1). The inputs are the process table, the agent's allowlisted
environment and the Claude record's `tmux` field; the world comes in through `IHostSources` (environment by pid, socket by tmux server pid, the tmux view by
socket, the terminals and the Meta teams), which `Features/Sessions/PassHostSources` backs for one pass, each read under the pass's own deadline.

- **The chain:** `HostChain.Ancestors` follows parents (at most 32, ending at pid 0, a missing parent or a cycle); `Classify` takes the first ancestor that is a host:
  a `tmux: server`, `sshd`, a `terminals.json` terminal (matched by comm, argv[0] or exe basename; comm is cut at 15 characters), Warp or an IDE (`code`, `cursor`).
- **Kinds:** `tmux`, `warp`, `terminal`, `ide`, `ssh`, `t3` (an sdk-ts record under `.t3/worktrees`, when no ancestor is a host), `unknown`. A Warp shell (`TERM_PROGRAM=WarpTerminal`)
  is Warp whichever process name the terminal runs under; the agent's own `TERM_PROGRAM` counts only when no ancestor is a host and it is not tmux-hosted.
- **Tmux:** the socket comes from the server's open `socket:[inode]` files matched to `/proc/net/unix` (`UnixSockets`), never from calling tmux. The agent's own `TMUX`
  fills in only when the table gives none and the server pid in it is this server's. The pane is the one whose pid is the agent or an ancestor. The terminal that shows a
  tmux session comes from the chain of an attached client of that session only: the agent inherited `TERM_PROGRAM` and `WARP_*` through the server, which say nothing.
  The label is `tmux · <terminal>`, or `tmux` with no client or a server that can't be read (that is not a problem row; only a pass-deadline timeout is).
- **Socket kind:** `sindri` (path ends `/sindri`, or `/sindri-launch` for the launcher's own socket), `default` (ends `/default`, whatever the session is named, so the owner's lanes there show `default`), `team` (the socket
  is named for a Meta team, or the session is `<team>-<lane>`), else `other`. Teams come from `<state>/meta/team.json` and `<state>/meta/teams/*/team.json` (`team` and `lanes` only).
- **Facts for human callers only** (never in the ledger payload): `TerminalFacts` (`XWindow` from `WINDOWID`, `KittyWindow`, `KittyListenOn`, `WeztermPane`, `WarpFocusUrl` only when it
  has the form `warp://session/<32 lower-case hex>`) and `TmuxFacts` (`Socket`, `SocketKind`, `Session`, `Pane`).
- **`TmuxCli`** (`Infrastructure/Sessions`) is the only code that runs tmux (ARCH-SESS-001, spec CI-5): the five read-only verbs `list-sessions`, `list-panes`,
  `list-clients`, `display-message` and, from CO-7a, `capture-pane` (the one list `TmuxRules.ReadVerbs` in the architecture test), every call as `tmux -S <absolute socket> <verb> …` with `TMUX` and
  `TMUX_PANE` removed from the child's environment, through its own bounded `Process.Start` that kills the child only. Output uses tabs (session names may hold spaces).
  A socket path that is not absolute is refused before any spawn. Each call is bounded by 1 s (`TmuxCli.CallBudget`, spec CO9); one read makes two calls (panes, then clients), so a
  silent socket costs at most 2 s before it is skipped, and a pass deadline cancels without marking the socket. One read of a socket gives panes and clients and is kept 10 s; a socket that timed out is not asked again for 10 s. The pass calls `list-panes` and `list-clients`; the preview (below) calls `display-message` and `capture-pane`.
- **Debug log:** one line per session whose host kind or socket kind changed (`session <ref> host <kind> tmux socket kind <kind>`): refs and kinds only, no path, cwd or title.
- Tests: `HostRulesTests` (unit), `TmuxCliTests` (against a stand-in program), the port contracts (`TmuxServersContract`, `UnixSocketsContract`, `TeamNamesContract`, `TerminalNamesContract` in
  `Aw.Sindri.Testing`, bound to each fake and to its real adapter; the tmux one runs against a private `tmux -L aw-co2b-<pid>` server it starts and kills itself, skipped without tmux), `HostSourceFilesTests`, and the host golden
  `tests/Aw.Sindri.Tests/Sessions/Golden/snapshot-hosts.json` over `tests/fixtures/sessions/proc`.

## Built so far

CO-2a (discovery): `Aw.Sindri.Core/Sessions/` (the records, the pure rules `SessionLiveness`, `AgentProcessRules`, `SessionStateRules.Merge`,
`FleetChangeRules.Diff`/`Restore`, `EnvironmentAllowlist`, the ports), `Aw.Sindri.Core/Launch/` and `Infrastructure/Launch/` (the minimal launcher reader and agent
catalog, CA-0), `Infrastructure/Sessions/` (`ProcProcessTable`, `ClaudeSessionRecords`, `FleetStateFiles`, `TranscriptFiles`, `SessionSnapshotStore`),
`Features/Sessions/` (the pass, `RecordFleetChanges`, `RestoreFleetHistory`) and `Host/Server/SessionCollectorHost`. Host detection landed in CO-2b (the previous section); the routes and the CLI are CO-3 (the section below). The event is `fleet.changed`; the codes are `SND-SESSION-001`
(a source failed), `-002` (`unsupported-platform`, `proc-unreadable`, `disabled`) and `-003` (a test override) in `docs/csharp/sindri.md`. The snapshot over
`tests/fixtures/sessions/` is pinned by `tests/Aw.Sindri.Tests/Sessions/Golden/snapshot-discovery.json`. `LaunchId` (the engine's, `Core/Workflow/Engine/LaunchId.cs`, `LCH-` and 32 hex) is the
typed id of `AW_SINDRI_LAUNCH`; a value of another shape is dropped.

## Routes and CLI (CO-3)

`GET /sindri/sessions?provider=&state=&repo=&inWorkflow=` (`ListSessions`, CA-1) and `GET /sindri/sessions/{provider}/{id}` (`GetSession`, CA-2) read the collector's in-memory
snapshot; both are queries and have no catalog row. `Core/Sessions/SessionRows` is the one place that turns an `ObservedSession` into a `SessionRow` and decides who sees what.

- **Callers:** a human or service caller sees every session. A `session` caller sees only the sessions whose repo is its `X-Sindri-Repo` (a session with no repo header sees none), and
  `title`, `pid`, `host.session`, `host.pane`, `recentlyEnded[].resume`, `cwd`, `host.socket` and `stateHistory` are null for it. The 404 of `GetSession` is the same for an unknown ref
  and one in another repo.
- **Row:** `ref`, `provider`, `providerLabel`, `id`, `idKnown`, `state`, `asking`, `stateSource` (`none`, `record`, `hook`, `transcript`), `estimated`, `repo`, `worktree` (the directory
  below the repo's checkout in the approved profile, else null), `host{kind, label, socketKind, attachedClients, terminal}`, `lane{team, lane}`, `launchId`, `startedAt`, `since`,
  `focus`. `focus` is `{method: none, possible: false, why: not-built}` until CO-4a (a stable code; the `FocusSession` catalog row keeps the phase label `pending:CO-4a`). `item` and `instance` come with CA-6.
  `inWorkflow` means "has a Sindri launch" until the launch join exists.
- **Launch step:** `launch{instance, state, attempt}` says which workflow instance, Step (state id) and attempt the session's Sindri launch belongs to, so a row reads "on `<instance>` · `<state>`"
  with no second call (`Core/Sessions/SessionLaunchSteps` looks the launch up in the `launches` view). `launch` is null when no launch of Sindri's is known for the session; its members are
  null for an attended (click) launch. For example, a session with no launch row serves `"launch": null`, while one whose launch was a click serves `"launch": {"instance": null, "state": null, "attempt": null}`. A session's `state` is its own and unrelated to `launch.state`.
- **`SessionList`:** `sessions`, `recentlyEnded` (each `{session, endedAt, resume}`), `problems`, `counts{live, asking, outsideWorkflow}` (over what the caller may see, before the
  filters), `empty` (`No agent sessions are running.` when no row is shown), `asOf` (the last pass). Before the first pass the list is empty, with `asOf` the time of the request (no pass has run yet; `GetSession` answers 404). A `SND-SESSION-002` problem in the pass
  replaces the list (`503`, `detail` the reason); `-001` and `-003` stay problem rows.
- **Ended by Stop:** stopping a Sindri launch records `session.ended` with reason `exit` once the host stop succeeded, so its launches row is no longer live and is never marked `lost`; nothing is recorded if the row isn't live (`docs/csharp/sindri-launcher.md`, "Stop ends the session").
- **`SessionView`:** `{session, cwd, stateHistory, asOf}`. `stateHistory` is the snapshot's `History` (`StateHistoryRules`): per live session, the first state and each change of state or
  asking reason, at most 20, oldest first, in memory for this Sindri run.
- **Status:** `GET /status` has `sessions{live, asking, problems, running}`, box-wide for every caller and deliberately public: a `session` caller sees the numbers (also through the MCP `sindri_status` tool), never the rows. `problems` counts every problem row, `SND-SESSION-003` (a test override) included.
- **Startup window:** the repo of a directory is looked up off the pass and cached, so for the first seconds after `serve` starts (and right after a profile approval) a row can show no repo (`-` in the CLI); it fills in on a later pass.
- **CLI:** `sindri agents [--provider P] [--state S] [--repo R] [--json]` and `sindri agents show <provider>/<id> [--json]` (`Host/Commands/AgentsVerbs`) call the routes and never
  start the control plane. Exit 0 (an empty list too), 1 when Sindri isn't running (`SND-API-001`) or a problem row is present, 2 on usage or another error. `--json` is the raw body.
- **Goldens:** `tests/Aw.Sindri.Tests/Golden/Api/sessions/` (`empty`, `fixture-three-providers`, `session-scoped`, `problems-source-failed`, `recently-ended`, `unsupported-platform`,
  `proc-unreadable`, `disabled`; these three are the `SND-SESSION-002` bodies, 503 problem+json), `sessions-id/` (`claude-tmux`, `codex-hook`, `not-found`, `session-caller-hidden-fields`) and `status/sessions.json`, from
  `ApiGoldens/SessionScenarios` (the snapshot is published into the host's store). `Aw.Sindri.Ui.Tests` reads every API golden and is in the gate.

## Live preview (CO-7a)

The read behind `GET /sindri/sessions/{provider}/{id}/preview` (spec CA-9, CI-10). CO-7a builds the read; the route, its goldens and the telemetry counter are CO-7b's. The read is human-only,
on demand (never in the collector pass), writes no ledger event and logs no pane text.

- **Service:** `Features/GetSessionPreview/GetSessionPreviewService`, `IQuery<GetSessionPreviewResponse>` for `GetSessionPreviewRequest(SessionRef Ref, DateTimeOffset? ExpectStartedAt)`. It is registered
  (`AddQuery`) and needs only the route to be mapped. The response is a `SessionPreview{Lines, Truncated, CapturedAt, StartedAt, Host{Kind, Label}}` or, still HTTP 200, a
  `SessionPreviewUnavailable{Reason, CapturedAt}` (`Reason` one of the `PreviewReason` constants `not-in-tmux`, `not-live`, `timed-out`). Both derive from `GetSessionPreviewResponse`; the endpoint serializes
  the concrete type with the package's JSON context. The text in `Lines` is not scrubbed (spec §12 names this the one exception) and the endpoint must not scrub it.
- **Check order, per request, before any shared wait:** (1) no caller is `SND-AUTH-002` (a `session` caller is let through since owner decision q93, and sees only its own repo's sessions via `SessionRows.Find`); (2) the collector-unavailable errors of the pass
  (`SND-SESSION-002`), as `GetSession`; (3) the ref (`SessionRows.Find`): `SND-SESSION-404`; (4) `ExpectStartedAt`: when it is not null and differs from the row's `startedAt` (a row with none counts as different), `SND-SESSION-409` (`SindriErrors.SessionConflict`).
  A caller whose row has no `startedAt` sends null, which skips the comparison, so the match is on the ref (provider and session id); null with a row that has a `startedAt` also skips it (the query is optional);
  (5) the 200 answers: a row whose host is not tmux, or with no socket or pane, is `not-in-tmux`; the shared read; `timed-out`; `not-live`; the lines.
- **Liveness (`not-live`):** `TmuxCli.Read(socket, pane)` runs `display-message -p -t <pane> '#{pane_pid}'` and then `capture-pane -p -J -S -40 -t <pane>`, both `-S <socket>` first, under one 1 s deadline (one
  `CancellationTokenSource` on the injected `TimeProvider` for both spawns). A pane, server or tmux that isn't there answers `Gone`. The service then calls `PreviewRules.InPane`: the pid of the session must
  be in the process table with the same start ticks and be `pane_pid` or a descendant of it within 64 parent hops (a loop or a missing parent ends the walk). An unreadable process table is `not-live`:
  text that could not be checked is never shown.
- **One read per pane:** `PaneCaptures` (a singleton) keeps one in-flight read per (socket, pane). A request that finds one waits for its answer; the checks above ran first, so a shared read never skips a 409.
  It is not a cache: the next request after the read finishes reads again.
- **Timeout mark:** a read that runs past the deadline answers `timed-out` and sets the preview's own per-socket mark for 10 s (`TmuxCli`, apart from the pass's `_timedOut`); while it is set the preview answers
  `timed-out` without spawning tmux. The pass never reads this mark, so a slow `capture-pane` cannot blank session rows. The preview does not read the pass's mark either.
- **`PreviewRules`** (`Core/Sessions`, pure; the six steps of CA-9): `Of(bytes)` decodes UTF-8 with replacement characters, `Of(text, dropped)` strips over the whole capture before the split (CSI; OSC, DCS, APC, PM and SOS
  strings up to ST or, for OSC, a bell, even across lines, an unterminated string to the end; other escapes; C0 controls but tab and LF, so CR goes; DEL; C1 controls; bidi marks, overrides and isolates; U+2028 and U+2029),
  splits on LF, drops trailing blank lines, keeps the last 40, cuts each line at 200 UTF-16 code units without splitting a surrogate pair, and sets `Truncated` only for a cut line or a dropped byte range.
  A raw U+009B or U+009D is a stripped control character like the rest of C1; it does not start a sequence.
- **64 KiB cap:** `TmuxCli` keeps the last 64 KiB plus one bytes of stdout (never more than twice that in memory); `PreviewRules.Of(bytes)` keeps the last 64 KiB, drops the first (partial) line unless the extra byte before it
  is a line feed, and sets `Truncated`.
- **Safety:** the argv is fixed (`ARCH-SESS-001` allows `capture-pane` as the fifth verb of `TmuxCli` alone); the pane id must match `%[0-9]+` and the socket must be absolute, else `Gone` with no spawn.
  Tests that run a real tmux use a private `-L` server (`PrivateTmux`); the stand-in tests never run tmux.
- Tests: `PreviewRulesTests`, `PreviewTmuxCliTests` (stand-in program), `GetSessionPreviewTests`, `PaneReaderContract` (bound to `FakePaneReader` and to `TmuxCli` on a private server), and the integration test
  `Preview_reads_the_stub_pane_through_tmux` (a pane that prints escapes and a 300-character line).

## API rows for review

The API lane reviews these rows (spec §7; the ideas lane builds them in the phases named). None is built in CO-0.

| # | Row | Phase |
|---|---|---|
| CA-0 | The launcher reader: `AgentId`, `IAgentCatalog` (`Resolve`, `All`), `AgentCatalog` over `builtins.json` merged with `launcher.json`'s `agents`, `LauncherConfigFile` (reads `agents` and `sessions`; accepts `viewer`, `host`, `meta` unread; rejects unknown keys per section) | CO-2a |
| CA-1 | `GET /sessions?provider=&state=&repo=&inWorkflow=` to `ListSessions`: `SessionList`, `SessionRow`, human-only fields, `problems[]` | CO-3 |
| CA-2 | `GET /sessions/{provider}/{id}` to `GetSession`: the row plus human-only `cwd`, `host.socket`, `stateHistory[]` | CO-3 |
| CA-3 | `fleet.changed` event (`appeared`, `asking`, `gone`), public, no text, `Invalidates = [ApiViews.Status]` | CO-2a, CO-3 |
| CA-4 | `POST /sessions/{provider}/{id}/focus` to `FocusSession` (class `desk`, human only), `sindri agents focus` | CO-4a |
| CA-5 | Codes `SND-SESSION-002`, `-003`, `-404`, `-409` | CO-2a, CO-3, CO-4a |
| CA-6 | The launch join (`item`, `instance` from P10a's `launches`) | P10a + CO-3 |
| CA-7 | Catalog row `FocusSession` as `pending:CO-4a`, no rows for the queries (`ARCH-CQRS-003`), the sessions goldens | CO-3, CO-4a |
| CA-8 | `GET /status` gains `sessions{live, asking, problems}` (additive) | CO-3 |
