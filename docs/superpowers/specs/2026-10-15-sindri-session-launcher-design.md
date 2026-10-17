# Sindri session launcher: any terminal, any agent CLI, and a T3 Code spike

**Status:** design, revision 3.1 (after /autoplan passes 1–2 and a narrow re-check, §12) · **Date:** 2026-10-15 · **Owner decisions:**
2026-10-15 (§1; rev 1's direction approved, "i approve of 254"; non-Claude Meta lanes allowed, §11 Q5) ·
**Builds on:** the API surface spec `2026-10-10-sindri-api-surface-design.md` (API), §3, §3.4, §3.8, §6, §8.1, §9 and
phase P10a; the UI design spec `2026-10-11-sindri-ui-design.md` (UI), §5, §6.1–§6.7 (E-1…E-5, E-11) and phases
U6a/U6b; the UI screens spec `2026-10-13-sindri-ui-screens-design.md` (screens), E-28 and the Agents page (§4.5, U11);
the workflow engine spec `2026-10-10-sindri-workflow-engine-design.md` (WF), §6.5 and P8; README rows X15, X41, X52,
X55, X61 · **Lanes:** the API lane builds the launcher inside P10a; the ideas lane builds the `aw-meta-launch` half, the
non-Claude Meta lanes and runs the T3 Code spike; the UI lane changes copy in U6a; the engine lane takes L-6, L-11 and
L-14 into P8 (§6) · **Next:** the owner's review, then /specToProvenPR takes each phase to PRs.

Sindri's headline action is one click into a live agent session. Today's design runs that session in tmux and shows it
in **Warp**, and starts **Claude Code, Codex or Cursor** from a fixed table (UI §6.1, API §8.1). None of it is built
yet: the code has a stub `ILaunchRequests` and a trigger name that says "opens Warp"
(`src/Sindri/Aw.Sindri.Core/Workflow/Engine/Triggers/TriggerNames.cs:60`). The one launcher running today,
`aw-meta-launch`, wires Warp, tmux and Claude Code together in bash (`scripts/meta-launch.sh`, `scripts/meta-lane.sh`).

A teammate on T3 Code, kitty, GNOME Terminal or an ssh session can't use either. This doc makes the terminal and the
agent CLI configuration before API P10a writes `WarpLauncher`, which is the cheapest moment to do it. It is the first
batch from the ideas lane's data set (ideas 081, 086, 091, 092; configurability audit rows CFG-01, CFG-02, CFG-03,
CFG-41, CFG-42).

The design already separates the pieces: tmux owns the process and Warp only shows it (X55). This doc names that split
as three ports, puts the terminal and the agent catalog in a machine file, and keeps the API and UI specs' launch
pipeline: `OpenItem`'s modes, the request, claim and expiry rules (E-1), the launch secret, budgets and the needs-you
rows. What it does change in their contracts is listed as numbered edits in §6 (L-1…L-16).

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| SL1 | **`ILauncher` is composed of three ports** (owner, 2026-10-15, batch 1): an **agent catalog** (data: how to start or resume each CLI), a **session host** (who owns the process: `tmux`, or `none` for an app that owns its own) and a **terminal viewer** (what a human sees: `warp`, `exec-template`, `print`, later others). `ILauncher` keeps its name, its owner (API P10a) and its launch pipeline (API §8.1, UI E-1); `WarpLauncher` becomes one viewer adapter | The split already exists in the design (X55); naming it lets a new terminal or CLI be an adapter or a data row, never a change to `OpenItem`'s modes |
| SL2 | **The terminal is a fact about the box, not policy.** It lives in a machine file, `$AW_STATE_DIR/launcher.json`, and changing it needs no profile approval. The approved profile only names agent ids (`launch.default`, `launch.repos.<repo>.default`, UI E-4; `agents.<id>.dailyBudget`, screens E-28; `workflow.agent.target`, WF §6.5) and, for factory Steps, the entries it trusts (SL8). Every profile key that names an agent takes any `AgentId` and never checks the catalog (L-14) | Two teammates can share one profile with different terminals. Approval protects what the factory may do, and which window shows a session isn't that |
| SL3 | **Agent targets are data.** Built-in catalog entries for `claude-code`, `codex` and `cursor` carry today's argv from UI §6.1. `launcher.json` may add entries (`aider`, `opencode`, `gemini`…) and may replace a built-in one only with `"override": true` (§4). A target id is an open string (`AgentId`, pattern `^[a-z][a-z0-9-]{1,31}$`) everywhere a target is named: `OpenItem.target`, `sindri open --agent`, events (L-9) and the profile and engine keys (L-14). An id the catalog doesn't know is refused at launch with `SND-LAUNCH-003` | A teammate on another CLI adds one JSON entry. Ids stay stable in events and goldens |
| SL4 | **tmux is the host for every Step** (X55 unchanged). An unattended launch always uses the `tmux` host, whatever `launcher.json`'s `host` says. The `host` setting applies to **attended opens only**; `none` is for a GUI app that owns its own agent process (T3 Code) | Joining any Step live needs a host Sindri can attach to and capture. A human open needs only to start a session |
| SL5 | **A viewer failure never stops a tmux-hosted session** (generalises API §8.1's Warp rule). The session starts first in the host, then the viewer opens `tmux attach`. `launch.started` gains `viewer {kind, label?, outcome, code?, detail?}` and `attach` (L-10). On a tmux host, a viewer that fails (`SND-LAUNCH-001`, or `-009` after the host started) is `launch.started` with `viewer.outcome: failed` and the attach command to copy, never `launch.failed`. On a `none` host the viewer is the process owner, so its failure stays `launch.failed` | Same guarantee for every terminal, and the `print` viewer makes an ssh or headless box a supported setup rather than an error |
| SL6 | **One file for Sindri and `aw-meta-launch`** (owner, 2026-10-15: "also allow non-claude agents in them"). The Meta launcher reads the same `launcher.json` for its viewer and for its lanes' agent: `meta.lanesAgent` and per-lane `meta.lanes.<lane>` may name any catalog agent. Warp's multi-pane layout stays a Warp-only capability; other viewers open one window per lane. A non-Claude lane launches in SL-1 and takes part in /meta's obligations from SL-1b (§10.2) | The live launcher today is Meta's, and a teammate meets it first, on whatever CLI they use |
| SL7 | **T3 Code is a spike first** (owner, 2026-10-15). T3 Code starts and owns its agent process and can't adopt a `claude --session-id` or `codex resume` session (audit, verified in source). It is a second host family, not a viewer, and gets an adapter only if the spike's proof passes (§7) | It changes liveness, usage and takeover, so it is measured before it is designed in |
| SL8 | **Unattended Steps run only approved argv** (review C7). A Step launch (`attended: false`, L-1) uses a built-in entry, an override whose `argvHash` equals its built-in's (only `bin`'s directory changed, §2), or an added or overriding entry whose `argvHash` the approved profile lists under `agents.<id>.stepArgvHash` (L-11). Anything else parks the Step with `SND-LAUNCH-012`. The check and the argv build use the same entry, loaded at service start or **Check again**; nothing re-reads the file between them. Human opens keep Q1: any entry in the box's file | `launcher.json` needs no approval, so it must not decide what the factory runs unattended |

## 2. Terms

- **Agent catalog entry:** the argv templates and facts for one agent CLI: `bin`, `open`, `resume`, `sessionId` mode,
  `sessionIdPattern`, `reportedId`, `processName`, `transcripts`, `usage` reader id, `mcp` form (§4).
- **Catalog source:** where an entry came from: `builtin`, `override` (a built-in id replaced with `"override": true`)
  or `added`.
- **`argvHash`** (review X6, X7): `sha256:` plus the lowercase hex SHA-256 of the RFC 8785 (JCS) serialization, in
  UTF-8, of this object, taken from the **merged** entry (an override merged over its built-in, `resume: null`
  included):

  | Key | Value |
  |---|---|
  | `bin` | the basename of `bin` (`/opt/claude/bin/claude` → `claude`) |
  | `open`, `resume` | the templates, before expansion |
  | `sessionId` | the mode (`pre-chosen-uuid`, `reported`, `none`) |
  | `mcp` | the form name; the args a built-in form adds are covered by it |

  Not inputs: `bin`'s directory (a per-box path, so one shared profile approves the same entry on every box), `label`,
  `processName`, `transcripts`, `usage`, `sessionIdPattern`, `reportedId`, and everything expanded at launch (ids, item
  text, the seed, the MCP config path). Doctor checks `bin` itself: it prints each entry's resolved path and warns
  when it doesn't resolve. Goldens in `tests/fixtures/launcher/argv-hash/` fix the value; `launch.started`,
  `sindri launch show` and doctor reproduce it.
- **Session host:** the process owner. `tmux` (Sindri's socket `-L sindri-launch`, session `snd-<run>` or `snd-<launch>`), or
  `none`.
- **Terminal viewer:** opens a human-visible window running a command (for a tmux host,
  `tmux -L sindri-launch attach -t …`), or brings an app forward. Its **label** is what the app names it: `Warp` for `warp`,
  `viewer.name` for `exec-template` (required there), none for `print` (the copy has label-less forms, L-7).
- **Attended:** a launch a human asked for (`OpenItem`, `sindri open`). Unattended launches come from the engine's agent
  runtime (WF §6.5), skip the viewer and always use the tmux host (L-1, SL4).
- **Launch target:** what `OpenItem.target` names. A target id equals an agent id; the viewer and host come from
  `launcher.json`, so the target menu stays one list of agents (UI §6.1).
- **Lane identity:** `AW_META_TEAM` and `AW_META_LANE`, exported by `meta-lane.sh` into each lane's process
  environment (L-12). Every lane-side hook and command takes the lane from them and nothing else (§10.2).

## 3. The ports

All three live in `Aw.Sindri.Core/Launch/` beside `ILauncher` (API P10a's `Core/Launch/*`). Adapters live in
`Aw.Sindri.Infrastructure/Launch/`.

| Port | Members | Notes |
|---|---|---|
| `IAgentCatalog` | `Resolve(AgentId) → Result<AgentEntry>`; `All()`; `BuildArgv(AgentEntry, LaunchMode, ArgvInputs) → Result<IReadOnlyList<string>>` | Pure. `ArgvInputs` holds only ids, the repo path, the session name, the seed (a fixed template plus ids, UI §6.2) and the per-launch MCP config path. The result is an argument list, never a string. `AgentEntry` carries its `source` and `argvHash` |
| `ISessionHost` | `Start(HostSession, argv, cwd, env, deadline) → Result<HostRef>`; `IsAlive(HostRef)`; `AttachCommand(HostRef) → argv?` (null for `none`); `AttachedClients(HostRef) → int`; `Capture(HostRef)` (later, the collector X59c); `Stop(HostRef)` | `tmux` adapter: `tmux -L sindri-launch new-session -d -s snd-… -c <cwd> -e K=V…` with the argv wrapped by `sindri session wrap`. `none`: `Start` is refused; the viewer starts the process |
| `ITerminalViewer` | `Open(ViewerRequest{title, cwd, argv, host}) → Result<ViewerOutcome>`; `Probe() → TerminalFacts{id, label?, present, version?, problem?}` | `ViewerOutcome` is `opened`, `unconfirmed`, `skipped` (print, or unattended) or `failed(code, detail?)` |

**The agent's environment** (review C10). UI §6.3 step 2 put `AW_SINDRI_CHILD=1`, `AW_SINDRI_ITEM`, `AW_SINDRI_LAUNCH`
and the missing-CLI guard into Warp's exec line; with tmux as the host that line only runs `tmux attach`, so they move:
`ISessionHost.Start(env)` passes them with `new-session -e` (tmux ≥ 3.2, checked by the host probe; the floor the P10a build found `new-session -e` needs, provisional owner default 2026-10-17), never with
`set-environment -g`, and `sindri session wrap` sets them again before it starts the agent as its child (wrap stays
the parent: it waits for a reported id, L-9, and sends API §8.1's heartbeat), so a session started by hand inside the
pane has them too. `AW_SINDRI_HOME` is passed the same way when it isn't the default (the prover's temp home), so the
id hook (§4) writes under the right home. `session wrap` keeps the guard: when the agent's `bin` doesn't resolve it prints
`<bin> not found (SND-LAUNCH-003): <fix>` and exits 127. None of these variables is a secret; the launch secret keeps
its 0600 one-time file (API §8.1), and the tmux server's global environment never carries it.

**`ILauncher.Launch`** becomes: resolve the agent (catalog) → for an unattended launch, check SL8 (`SND-LAUNCH-012`) and
use the tmux host → refuse at budget (E-28, unchanged) → start in the host → open the viewer on the host's attach
command (attended only) → return the viewer outcome, `attach`, `catalogSource` and `argvHash`, which the launcher
service records on `launch.started` (L-10). For a `none` host the viewer is asked to start the agent itself (T3 Code,
§7), and a viewer failure is `launch.failed` with the viewer's code; if the viewer started a thread before failing, the
adapter stops it.

**Timing: `Launch` owns its deadlines** (review C3, X17). E-1's 10 s limit stays as the service's outer backstop, but
`Launch` enforces its own stages, so the service always knows whether the host started:

| Stage | Bound | When it runs out |
|---|---|---|
| Catalog and host start | 4 s, a timeout passed to `ISessionHost.Start` | `Launch` returns `Failed(SND-LAUNCH-009, stage: host)` and runs `tmux -L sindri-launch kill-session -t =snd-…`, so a start that completes late leaves no orphan. The service writes `launch.failed` |
| Viewer observation window | 3 s after the host started | A non-zero exit inside it is `failed SND-LAUNCH-001`; the launcher polls `AttachedClients` (`tmux -L sindri-launch list-clients -t =snd-…`) every 250 ms, and the first attached client is `opened`. A viewer still running, or exited 0, with no client at the end is `unconfirmed` |
| Viewer `Open` itself hangs | the end of the window | `Launch` returns `Started(viewer: failed SND-LAUNCH-009)`: the host started, so the service writes `launch.started` (SL5) |
| E-1's 10 s outer limit | backstop only | It fires only if `Launch` itself hangs. The service writes `launch.failed SND-LAUNCH-009` and stops `snd-<launch>` if it exists |

The result type carries the marker: `LaunchResult` is `Started(HostRef, ViewerOutcome, …)` or `Failed(code, stage)`.
Host start plus the window stays under 8 s. This rule is the same for `warp` and `exec-template`; it never reports
"Sent to kitty" for a single-instance terminal that exited 0 without a window.

**A late attach upgrades `unconfirmed`** (review X9). A Warp or kitty cold start can take longer than 3 s. After a
`launch.started` with `unconfirmed`, the launcher service keeps polling `AttachedClients` every 1 s in the background
(it doesn't hold the claim) until E-1's 30 s request deadline. The first client appends `launch.viewer-attached
{launchId, at}` (additive, key `launch.viewer-attached:<launch>`, so a restarted poller can't append it twice; L-10),
and the projections then report `viewer.outcome: opened` with `late: true`. After
the deadline `unconfirmed` stays.

`ILauncher.Probe` (UI E-3) fills `launch.probed` as today. `terminal` keeps its shape; `terminal.id` is now the viewer
kind (`warp`, `exec-template`, `print`; an exec-template's name is `terminal.label`), so no golden changes shape. Added fields (additive, API §9 versioning
rules; L-4): `terminal.label`, `terminal.source` (`file` or `detected`), `terminal.configPath`,
`terminal.detail {line, reason}` when `problem` is `config-invalid`, `host: {kind, present, version?, problem?}` and,
on each target row, `source` and `overridden`.

## 4. `launcher.json`

Machine-wide, at `$AW_STATE_DIR/launcher.json`. Read when the launcher service starts and on **Check again**
(`ProbeLaunchTargets`). One JSON Schema file, `schemas/launcher.schema.json`, is the contract (review C11). The C#
reader parses type-driven through `AwJson` with a source-generated context, rejects unknown keys, and checks
everything the schema checks; its tests run every fixture in `tests/fixtures/launcher/` against the schema file and
against the reader, with the same verdict. The **bash reader** (SL-1) promises less (review X15): it refuses unknown
keys, unknown placeholders and wrong JSON types for the fields SL-1 uses, with the same `SND-LAUNCH-011` wording. It
reads the key lists and the placeholder set from one `x-aw-keys` block in the schema file, not a copy. `x-aw-keys` is an
unknown keyword to JSON Schema 2020-12, so SL-0's CI check validates with Ajv 8's `Ajv2020` class with `strict: false` (a new
dev dependency of that check; strict mode rejects unknown keywords), and the validator the C# tests use must likewise
ignore unknown keywords (a fixture with `x-aw-keys` present proves it). Each fixture
carries a `bash: accept | refuse` tag, and `meta-launch.test.sh` runs the bash reader over the whole set and asserts
each tag. The full check for a bash user is `sindri launch show`, or doctor once Sindri runs. The built-in entries live
once, in `config/launcher/builtins.json`: the C# build embeds it (a linked `EmbeddedResource` in
`Aw.Sindri.Infrastructure`, with its `deps.yaml` file edge) and the bash reader reads it. The terminal templates the
generator writes live once, in `config/launcher/terminals.json`.

```jsonc
{
  "schema": 1,
  "viewer": {
    "kind": "exec-template",                   // warp | exec-template | print   (v1)
    "name": "kitty",                           // exec-template only, required: the label in the UI ("Sent to kitty")
    "argv": ["kitty", "--title", "{title}", "--directory", "{cwd}", "--", "{cmd...}"]
  },
  "host": { "kind": "tmux" },                  // tmux (default) | none (attended opens only, with a viewer that owns processes)
  "agents": {                                  // merged over the built-in claude-code, codex and cursor entries
    "aider": {
      "label": "Aider",
      "bin": "aider",
      "open":   ["{bin}", "--message", "{seed}"],
      "resume": null,                          // no resume: the Resume mode is never offered for this agent
      "sessionId": "none",                     // pre-chosen-uuid | reported | none
      "sessionIdPattern": null,                // required unless sessionId is none: the id must match before use
      "reportedId": null,                      // sessionId: reported only: {"hook": true|false, "newestFile": "<glob>"|null, "capture": "<regex, one group>"|null}
      "processName": "aider",                  // the process a pane runs while the agent is up (Meta's claude_in_pane)
      "transcripts": null,                     // a glob with {sessionId}: the agent's transcript (Meta's has_transcript)
      "usage": "none",                         // claude-code | codex | cursor | none (built-in readers only)
      "mcp": "none"                            // claude-config-file | codex-overrides | none
    },
    "claude-code": { "override": true, "bin": "/opt/claude/bin/claude" }   // replaces fields of a built-in entry
  },
  "meta": {
    "lanesAgent": "claude-code",               // the agent for aw-meta-launch's lanes (any catalog id)
    "lanes": { "review": "codex" }             // per-lane overrides, by lane name, in every team on this box
  }
}
```

Rules:
- **Placeholders are a closed set:** `{bin}`, `{cwd}`, `{title}`, `{name}`, `{seed}`, `{sessionId}`, `{mcpConfig}`,
  `{cmd...}`. `{name}` is the session's name (Meta's `-n <team>-<lane>`; Sindri's `sindri ITM-…`). `{cmd...}` is the
  host's attach argv, spliced as separate arguments: Sindri's is `tmux -L sindri-launch attach -t =snd-…`, Meta's is
  `tmux -L <team> attach -t =<team>-<lane>` (owner, 2026-10-16: the team runs on its own tmux socket, #264). Each other placeholder expands to exactly one argument;
  there is no string concatenation inside an argument and no shell. An unknown placeholder fails validation.
- **No option smuggling** (review C18). An expanded `{title}`, `{name}`, `{sessionId}` or `{seed}` that starts with
  `-` is refused before the spawn (ids and the seed template never do; the check is a backstop), and `{cwd}` is always
  absolute. A viewer whose terminal re-parses a single string after `-e` (`gnome-terminal -e`, `konsole -e "…"`,
  `xterm -e` with one string) runs a shell on it; the doc's worked examples use the argv forms (`--` before
  `{cmd...}`), and the generator never writes a single-string `-e` form.
- **Overriding a built-in entry needs `"override": true`** (review C8). Without it, an `agents.claude-code` key is
  `SND-LAUNCH-011` ("`claude-code` is built in: add `"override": true` to replace it"). An override replaces only the
  fields it names; `usage` and `mcp` stay the built-in's unless named. Doctor lists every override with its fields, and
  `launch.probed` marks the target `overridden: true`. An override that changes only `bin`'s directory, as in the
  example above, keeps the built-in's `argvHash` (§2), so Steps still run it (SL8).
- **Unapproved entries are named before a Step parks** (review X8). For each added or overriding entry whose `argvHash`
  the approved profile doesn't list, doctor, `sindri launch init` and `sindri launch show` print: "Steps on `<id>` will
  park: approve `agents.<id>.stepArgvHash: sha256:…`", with the exact profile line. Human opens are unaffected.
- **Session ids are checked against the entry.** `sessionIdPattern` is required unless `sessionId` is `none`; the
  built-ins carry today's patterns (a UUID for Claude Code; Codex and Cursor from UI §6.3 step 1). A session id that
  doesn't match drops Resume and falls back to Open, as today. User-supplied patterns (`sessionIdPattern`,
  `reportedId.capture`) run in C# with `RegexOptions.NonBacktracking` and a 100 ms match timeout.
- **Reported session ids come from the CLI's own hook** (review X2). For `sessionId: reported`, the id is never
  guessed from timing when a hook can report it:

  | Step | Rule |
  |---|---|
  | Hook | `config/hooks/session-id-report.sh` runs through the provider adapters at Codex `SessionStart` and Cursor `sessionStart` (`reportedId.hook: true` says the CLI has this hook; the hook reads `session_id` for both, since `cursor.sh` already maps Cursor's `conversation_id` to it). It exits 0 at once unless the host set `AW_SINDRI_LAUNCH` (Sindri) or `AW_META_TEAM` and `AW_META_LANE` (Meta). Otherwise it writes the id, atomically and 0600, to that launch's `$AW_SINDRI_HOME/launches/<launchId>.session-id` (beside API §8.1's `<launchId>.secret`; `AW_SINDRI_HOME` from the agent's environment, §3) or to Meta's `lane-sessions/<lane>`. The key is a variable the host set on that one session, so two sessions started together can't swap ids |
  | Read | `session wrap` (or `meta-lane.sh`) waits up to 30 s for the file, checks the id against the generic bound (L-9) and `sessionIdPattern`, and reports it to Sindri, which records it in the ledger as `launch.session-reported` (L-9), or writes the lane file |
  | Fallback | Only when no hook is installed for the CLI or it didn't fire: the `reportedId.newestFile` glob, taking files created after this start within 30 s that no other live launch or lane has claimed, and the `capture` group. Exactly one candidate is used. More than one is refused: no id, and doctor or `aw-meta health` shows "ambiguous session id" |
  | No id | No Resume, as today |

  `aw-meta-launch` starts `reported` lanes whose CLI has no hook installed one at a time, each waiting for its id or
  30 s; with the hook installed (SL-1b), lanes start together. The hook is installed user-wide for Codex and Cursor by
  `install-meta.sh` (SL-1b), so from then on it also writes for any session that carries `AW_SINDRI_LAUNCH`, Sindri's
  included; on a box without SL-1b's install, Sindri's `reported` launches use the fallback until the switch installs
  the hook with the other hooks.
- **`usage` and `mcp` name built-in readers and forms only.** A user-added agent can't add code; with `usage: none`
  its sessions record no tokens, and its target shows `limited: "no usage reader: budgets can't be enforced"` and is
  refused when its profile entry has a `dailyBudget` (`SND-LAUNCH-010`'s fix text names this).
- **Missing file** (review X16): the launcher runs `sindri launch init`'s detection in memory, once per launcher
  service start and again on **Check again** (never per periodic probe, review C19), and records
  `terminal.source: detected`. It falls back to `print` only when detection finds nothing. The rest are the built-in
  defaults: `tmux` host, the three built-in agents, `meta.lanesAgent: claude-code`. Doctor shows the detected viewer
  and the command that makes it permanent (`sindri launch init`).
- **Invalid file** (review C6, X4): doctor shows the parse error, the file and the line; the probe records
  `terminal.problem: config-invalid` with `terminal.configPath` and `terminal.detail {line, reason}`. Errors are
  checked per section and per entry:

  | Error in | Effect |
  |---|---|
  | the JSON itself (doesn't parse) | Attended opens are refused with `SND-LAUNCH-011`; Steps park with `SND-LAUNCH-011` |
  | `viewer` or `host` | Attended opens are refused with `SND-LAUNCH-011`; Steps run (they use neither) |
  | one entry under `agents` | That entry is dropped from the catalog and doctor shows it with its line. Launches of that id are refused or parked with `SND-LAUNCH-011` (detail: the id); other agents run |
  | `meta` | Only `aw-meta-launch` refuses; Sindri ignores it |
- **Permissions** (review C18): the file may run commands as the user, like their shell rc. Doctor warns when the file
  is group- or world-writable, when its owner isn't the current user, or when its parent directory is group- or
  world-writable; the same checks apply to the `AW_LAUNCHER_CONFIG` target. It never holds secrets (secret pointers
  stay in the profile, X18).

**Precedence for the config** (review C12): environment, then the file, then the built-in defaults.
`AW_LAUNCHER_CONFIG` names the file (for tests and the prover's guarded temp home; doctor prints the resolved path).
Meta's existing variables rank above the file for the lanes they already cover: `AW_META_CLAUDE` replaces the
`claude-code` entry's `bin`, `AW_META_CLAUDE_ARGS` is inserted after `{bin}`, before the session flags and the prompt,
as `meta-lane.sh` does today, and `AW_WARP_LAUNCH_DIR` is where the `warp` viewer writes Meta's launch configuration.
They stay for tests; `docs/csharp/meta.md` marks the first two as deprecated for users in favour of an `override`
entry.

**Precedence for the target** stays UI §6.1's: per-click choice → last used in the repo → profile
`launch.repos.<repo>.default` → `launch.default` → `claude-code`. A rule whose agent isn't in this box's catalog is
**skipped**, and resolution goes to the next rule (review C6): the button names the agent that resolved, `chosenBy`
names the rule that supplied it, and the menu shows the skipped default disabled with `SND-LAUNCH-003` and the fix
"add `<id>` to launcher.json or pick another target". It doesn't fail profile validation, because those keys take any
`AgentId` (L-14): the profile is shared and the catalog is per box.

**The generator** (review C5). `sindri launch init [--viewer <name>] [--force] [--stdout]` writes `launcher.json` for
this box: with no GUI session it picks `print`; else Warp when present; else `$TERM_PROGRAM` when it names a terminal
in `config/launcher/terminals.json`; else the first of those terminals on the login PATH (kitty, WezTerm, GNOME
Terminal, Alacritty, Konsole, foot, xterm). It validates the result against the schema, writes it 0600, refuses to
replace a file without `--force`, and `--stdout` writes to stdout instead. On success it prints what it chose and why,
for example "Wrote ~/.agentic-workflow/launcher.json: kitty (from $TERM_PROGRAM). The next open uses it." The bash
equivalent, `aw-meta-launch --init-launcher [--viewer <name>]`, uses the same detection and the same templates file.
`sindri launch show [<id>] [--json]` prints the merged catalog with each entry's source, resolved `bin`, `argvHash`
and whether Steps may run it (SL8). Both sit beside the existing `sindri launch probe`. Exit codes: 0 ok, 1 refused (a
file exists without `--force`, or the result fails the schema), 2 usage. Doctor's fix text for a missing file, for
`SND-LAUNCH-002` and for `SND-LAUNCH-011` names `sindri launch init`.

## 5. Viewer adapters (v1)

The viewer outcome on a tmux host is recorded on `launch.started` (SL5); the session keeps running whatever the
viewer does. Only catalog, budget, SL8 and host failures are `launch.failed`.

| Adapter | Open | Probe | Viewer outcome on a tmux host |
|---|---|---|---|
| `warp` | UI §6.3's recipe unchanged: per-launch YAML (serializer, 0600) with one `exec` running the attach argv, `warp://launch/…` through `open`/`xdg-open`, deleted 60 s after the outcome; then §3's 3 s window, client check and late-attach upgrade | bundle id or `warp://` handler; GUI session present | `opened` (a client attached, or later with `late: true`), `unconfirmed`, `failed SND-LAUNCH-001` (URI refused), `failed SND-LAUNCH-009` (§3) |
| `exec-template` | spawns `argv` detached (new session, stdin and stdout to `/dev/null`, reaped), placeholders expanded per §4; the first 2 KB of its stderr during the window is kept, passed through `Aw.Redaction`, and becomes `viewer.detail` on a failure and doctor's `launcher` line (review X20); then §3's window, client check and late-attach upgrade | `argv[0]` resolves through the login shell as a positional argument (`bash -lc 'command -v -- "$1"' _ "$argv0"`; UI §6.3's 5 s probe); GUI session present (`DISPLAY`/`WAYLAND_DISPLAY`, or macOS); a doctor warning when `argv` lacks `{title}` | `opened`, `unconfirmed`, `failed SND-LAUNCH-001` (non-zero exit in the window, or the spawn failed; `detail` holds the stderr), `failed SND-LAUNCH-009` |
| `print` | nothing opens; `launch.started` carries the attach command, which the app and `sindri open` print | always present | `skipped` |

A viewer the probe finds absent (`SND-LAUNCH-002`: no Warp, the template's binary not on PATH, or no GUI session)
replaces the button before the click, as UI §6.6 does today; its fix names `sindri launch init` and the `print`
viewer. On a `none` host the same codes are `launch.failed`.

Worked examples for `exec-template` (in `config/launcher/terminals.json`, rendered into `docs/csharp/sindri.md`; data,
not built-in code): GNOME Terminal `["gnome-terminal", "--title={title}", "--working-directory={cwd}", "--",
"{cmd...}"]`; kitty as in §4; WezTerm `["wezterm", "start", "--cwd", "{cwd}", "--", "{cmd...}"]`; Alacritty
`["alacritty", "--title", "{title}", "--working-directory", "{cwd}", "-e", "{cmd...}"]` (Alacritty's `-e` takes argv);
foot, Konsole and xterm alike. GNOME Terminal and `kitty --single-instance` exit 0 through a server even when no window
appears; §3's client check is what makes their outcome honest. Later adapters, each its own small phase when someone
needs it: `iterm` and `macos-terminal` (AppleScript), `vscode`/`cursor` (opens the folder; the attach command is
copied, since VS Code can't be told to run a command from outside without an extension).

Attach (API §8.1, UI §6.2) is the same `Open` call with the host's attach argv; for a `none` host it is not offered
(`SND-LAUNCH-006`, the mode falls back to Open), except T3 Code's deep link if §7 adopts it.

## 6. API, engine and UI edits

Numbered so the owning lane can take them into its spec and phase. This doc edits no other spec.

| # | Spec | Edit | Phase |
|---|---|---|---|
| L-1 | API §8.1, README X15, X55 | `ILauncher` is composed of `IAgentCatalog`, `ISessionHost`, `ITerminalViewer` (§3); "Warp is a viewer" becomes "the configured viewer"; the "a Warp that fails to open leaves the session running" rule is SL5 for every viewer. `LaunchRequest` gains `Attended` (bool): true for `OpenItem` and `sindri open` (a `--headless` server's human opens too; with no GUI its detected viewer is `print`), false for the engine's agent runtime. Unattended launches skip the viewer (`viewer {kind: none, outcome: skipped}`), always use the tmux host (SL4) and are checked by SL8. `Launch` owns its stage deadlines and returns `Started` or `Failed(code, stage)` (§3 timing); the service records the stage on `launch.failed` as `detail.stage` (`host` or `backstop`, additive), so the copy can tell a host-stage `-009` from one where a window may have opened (L-7); E-1's 10 s limit is the service's backstop. The agent's environment and the missing-CLI guard move to `ISessionHost.Start(env)` and `session wrap` (§3) | P10a |
| L-2 | API §8.1, Phases | P10a's `Infrastructure/Launch/{Warp,Process}Launcher.cs` becomes `Launch/{TmuxSessionHost, WarpViewer, ExecTemplateViewer, PrintViewer, AgentCatalog, LauncherConfigFile, LauncherInit}.cs`; `ProcessLauncher` is gone already (X55). `TriggerNames.OpenRequest`'s doc comment (`TriggerNames.cs:60`, "before the launcher opens Warp") and `NoLaunchRequests`' wording say "the viewer". `sindri launch init` and `sindri launch show` join `sindri launch probe` in the catalog (new rows; the gate runs `Aw.Sindri.Ui.Tests`, which pins the catalog) | P10a |
| L-3 | API §9, UI E-5, §6.6 | New codes: `SND-LAUNCH-011` "launcher.json is invalid" (`detail`: file, line, reason, and the entry id when one entry is invalid; fix: correct it, run `sindri launch init --force`, or delete it to use the detected defaults) and `SND-LAUNCH-012` "this agent entry isn't approved for unattended Steps" (`detail`: id, `reason: not-approved \| hash-changed`, `current` hash and, for `hash-changed`, the `approved` hash; fix: "run `sindri launch show <id>` and approve `agents.<id>.stepArgvHash: <current>` in the profile, or use a built-in entry"). `SND-LAUNCH-003`'s detail covers `not-in-catalog`. `SND-LAUNCH-002`'s text says "no terminal viewer" instead of Warp. **Re-classed for tmux hosts:** `-001`, and `-009` once the host started, are viewer outcomes on `launch.started` (SL5); on `none` hosts and before the host started they stay `launch.failed`. After a `launch.started` whose viewer outcome isn't `opened`, `launch.confirm` is set as after any start | P10a |
| L-4 | API §3.4 / UI E-3 | `launch.probed` gains `terminal.label`, `terminal.source` (`file \| detected`; with no file it is `detected` even when detection finds nothing and falls back to `print`), `terminal.configPath` (the resolved file path, also when the file is missing), `terminal.detail {line, reason}` when `terminal.problem` is `config-invalid`, `host {kind, present, version?, problem?}` (the attended host; Steps always use tmux, SL4), and on target rows `source` and `overridden`; `terminal.id` is the viewer kind; target rows gain the `limited` reason `no-usage-reader` | P10a |
| L-5 | API §3.8 / screens E-28 | A target with `usage: none` and a `dailyBudget` is refused with `SND-LAUNCH-010` (detail `no-usage-reader`) | P10a |
| L-6 | WF §6.5 (engine P8) | The agent runtime calls `ILauncher` with `Attended: false` (L-1). The launch uses the tmux host whatever `launcher.json`'s `host` says (SL4), so no Step parks for its host. An entry SL8 doesn't approve parks the Step with `SND-LAUNCH-012` and doesn't launch | engine P8 |
| L-7 | UI §6.1, §6.2, §6.6, the copy table (§ "Launch (§6.6)" row), Settings, U6a, `mockups/launch-flow.html` | Copy and states, all from `launch.probed`, `launch.started`, `launch.viewer-attached` and E-2 (L-15); no new view. See the copy table below this one. **Picker for N agents:** the menu lists every catalog entry, built-ins first in their fixed order, then added entries by id; `Shift+O` then `1`–`9` picks by position, arrow keys reach the rest. Built-in positions never change; an added entry can shift the positions of added entries after it. **Accessibility** (review X5): every outcome line is announced in the launch block's live region. When the outcome asks the human to run a command (print, unconfirmed, failed), focus moves to the copy button **only if focus is still on the launch button that sent the request**; otherwise focus stays where it is, and the live region announces the line and "copy button in the launch block" (UI §5: refreshes never move focus). The copy button is keyboard-reachable and announces "Copied". **Settings** shows the configured terminal read-only, with **Check again**: "Terminal: kitty (exec-template) · from `<terminal.configPath>`", "Terminal: kitty (detected · run `sindri launch init` to keep it)" or "Terminal: none found · open commands are printed". U6a's title "Open in Warp" becomes "Open in a terminal", and its proof names `<label>` | U6a |
| L-8 | `docs/sindri/profile.md` | Regenerated from the C# `SindriProfile` schema (it still documents the TS profile, CFG-42), including E-4's `launch.*`, E-28's `agents.*` and L-11's `stepArgvHash` | phase SL-0 |
| L-9 | UI E-1, E-11, §6.3 step 1; README X52 | Target ids open (review C1): E-1's body `target?` and `item.open-requested.target`, `launch.started.target` and E-11's `sindri open --agent <id>` become the typed id `AgentId` (pattern `^[a-z][a-z0-9-]{1,31}$`); `OpenItem` checks the pattern, and the launcher checks the catalog (`SND-LAUNCH-003`, `not-in-catalog`). `launch.started.sessionId` is bounded by a generic pattern in the event schema, `^(?!\.)(?!.*\.\.)[A-Za-z0-9._:-]{1,128}$` (no leading `.` and no `..`, since ids expand into `transcripts` globs), and checked against the entry's `sessionIdPattern` before it is written and again before Resume uses it; the per-target patterns move from the schema to the catalog. **`session wrap` and the reported id** (review X18, re-check H1, M2): for a `reported` entry the launcher writes the capture rule (`sessionId` mode, `sessionIdPattern`, `reportedId`; no secret) to `$AW_SINDRI_HOME/launches/<launchId>.capture.json` (0600), beside the launch secret's `<launchId>.secret` (API §8.1); `session wrap --launch <id>` reads it, takes the id as §4 says, and sends it on its next heartbeat as an optional `sessionId`. The heartbeat route stays `issue`-class and appends nothing (API §2.5); `service:launcher`, which already watches liveness to write `session.ended`, appends the additive event `launch.session-reported {launchId, sessionId}` (key `launch.session-reported:<launch>`, public, in `EventCatalog`, a row in API §3.4's table) the first time a heartbeat or wrap's exit call carries an id, always before that launch's `session.ended`. The `launches` projection keeps it, so it survives `sindri views rebuild`. **Per-launch files** are flat files in `launches/`, beside the secret: UI §6.3 step 4's 60 s cleanup also deletes `<id>.capture.json`; `session wrap` deletes `<id>.session-id` once it has read it; the launcher service deletes both on `session.ended`; and its start-up sweep removes any `launches/*.capture.json` or `*.session-id` older than 60 s whose launch has no live session. X52's "target ids `claude-code`, `codex`, `cursor`" reads "catalog ids; these three are built in" | P10a |
| L-10 | API §3.4 / UI E-1 | `launch.started` gains (additive) `viewer {kind, label?, outcome: opened\|unconfirmed\|skipped\|failed, code?, detail?}` (`detail` is the viewer's redacted stderr, §5), `attach` (argv built from checked ids, E-16), `catalogSource` (`builtin\|override\|added`) and `argvHash` (review C2, C9). `tabTitle` holds the viewer's `{title}` (`sindri ITM-…`) for every viewer. New additive event `launch.viewer-attached {launchId, at}` (§3, review X9; re-check M1): key `launch.viewer-attached:<launch>`, public, written by `service:launcher`, phase P10a, an `EventCatalog` entry and a row in API §3.4's event table; the `launches` projection turns `unconfirmed` into `opened` with `late: true`. `viewer.detail` is left out of the public form, as `request.filed`'s text is: it is terminal stderr, so it is in the private event, `LaunchView` and doctor only. `LaunchView` carries `viewer` and `attach` for the app | P10a |
| L-11 | screens E-28, API §3.8 (profile `agents.*`) | `agents.<id>.stepArgvHash` (optional; one hash or a list): the `argvHash` values an unattended Step may run for this id when its entry isn't built in and isn't a `bin`-directory-only override (SL8). The hash excludes `bin`'s directory (§2), so one value serves every box with the same templates; a list covers a template change rolling out across boxes. Doctor prints the current hash beside the approved ones when none matches | P10a (profile), engine P8 (use) |
| L-12 | API §6 | `AW_SINDRI_CHILD=1` is set for every Sindri-hosted agent by the host and by `session wrap` (§3), and in every `aw-meta-launch` lane's process environment (SL-1), exported by `meta-lane.sh` (not `new-session -e`, which would change the Warp layout and every tmux command that must stay byte-identical with no file), never `set-environment -g` on the user's default tmux server; so a lane or session running `sindri` is a session caller whatever its CLI. A shell left in a lane pane after its agent exits still carries it; `docs/csharp/meta.md` says to run the human's `sindri` approvals from another terminal | P10a, SL-1 |
| L-13 | `docs/csharp/meta.md` | Launcher rows, the "Claude Code only" statement, the state-files table, the env precedence (§4), lane identity (`AW_META_TEAM`, `AW_META_LANE`), the lane commands and the Codex `/hooks` trust step follow SL-1 and SL-1b | SL-1, SL-1b |
| L-14 | WF §6.5 and P8; UI E-4; screens E-28 and API §3.8; API §3 (the `POST /items/{itemId}/open` row) | Every remaining key that names an agent takes `AgentId` (review X1): WF §6.5's `workflow.agent.target` (and P8's signal, "with `workflow.agent.target: claude-code`…", which stays as an example); E-4's `launch.default` and `launch.repos.<repo>.default` in S1's profile schema; the keys of E-28's `agents.<target>` map (`agents.<id>.dailyBudget`, L-11's `stepArgvHash`) and the screens spec's Agents page (§4.5), which lists one row per catalog entry; and the API §3 open row's `target?` and its CLI form `--agent <id>`. Profile validation checks the id pattern only and never the catalog, because the catalog is per box. `agents.softLimit` stays a reserved sibling key: it can't match the id pattern (it has an upper-case letter), so no agent id collides with it | P10a (API, E-4, E-28), engine P8 (`workflow.agent.target`) |
| L-15 | UI E-2 | `ItemView.launch.lastStarted` gains `viewer {kind, label?, outcome}` (with `late` when upgraded) and `attach` (argv), from the `launches` projection (review X4). `resumable` (`LaunchOptions.For`) takes its `sessionId` from the `launches` projection: `launch.started.sessionId`, else `launch.session-reported.sessionId` (L-9, review X18, re-check H1), so it reads projections only and survives a views rebuild. The app renders both and decides nothing | P10a (E-2), U6a (copy) |
| L-16 | API, profile approve preview (`sindri profile approve`) | For each added or changed `agents.<id>.stepArgvHash`, the preview prints the entry on this box whose `argvHash` matches (`bin`, `open`, `resume`, source), or "no entry on this box has this hash" (review X8). The human approves argv they can read, not an opaque hash | P10a |

**L-7's copy table** (review X19). `<label>` is the viewer's label; `print` has none and uses the label-less form.

| State | With a label | `print` (no label) |
|---|---|---|
| Sending | "Sending to <label>…" | "Starting session…" |
| `opened` | "Sent to <label>", then "Session live" | n/a |
| `unconfirmed` | "Session started · <label> hasn't attached yet: run `<attach>`" (copy button); becomes "Sent to <label>" on `launch.viewer-attached` | n/a |
| viewer `failed` | "Session started · <label> didn't open (`SND-LAUNCH-001`): run `<attach>`" (copy button), with `viewer.detail` on the detail line | n/a |
| `skipped` | n/a | "Session started: run `<attach>`" (copy button) |
| "Session live" | shows for every started outcome once `has-session` confirms, beside the attach line, which stays until a client attaches | same |
| `-007`/`-009` fix | "A <label> window may have opened: look for `sindri ITM-…`, or run `tmux -L sindri-launch ls`" | "A session may have started: run `tmux -L sindri-launch ls`" |
| `-009` at the host stage (`launch.failed` with `detail.stage: host`) | "The session didn't start in time: run `sindri doctor`" (no window opened, since the viewer never ran) | same |
| The launcher never answered (UI §6.6, app-owned, 15 s) | "Sindri hasn't answered: run `sindri doctor`" | same |
| `lastStarted` row and sheet (E-2, L-15) | opened: "Sent to <label> 12m ago · Claude Code · `sindri ITM-…`"; otherwise "Started 12m ago · Claude Code · not attached · `<attach>`" | "Started 12m ago · Claude Code · not attached · `<attach>`" |
| `SND-LAUNCH-011` (new §6.6 row, before the click) | the button is replaced by "launcher.json is invalid, line <terminal.detail.line>"; detail `<terminal.configPath>` and `reason`; fix as L-3 | same |
| Skipped default (menu) | disabled with `SND-LAUNCH-003` and §4's fix | same |

The UI copy table's "Launch (§6.6)" row, U6a's proof and `mockups/launch-flow.html` take these strings in place of
the Warp ones.

Nothing else changes in `OpenItem`'s modes, the request, claim and expiry rules, the launch secret, `session.ended`,
`session.usage`, budgets or the needs-you rows. Wrap's heartbeat gains only L-9's optional `sessionId`, which reaches the ledger as `launch.session-reported`.

## 7. T3 Code spike

What the audit found (T3 Code source, `pingdotgg/t3code` main, 2026-10-15; MIT): T3 Code starts and owns its agent
process (Codex's app-server, or the Claude Agent SDK). It is driven from outside through its own MCP server
(`t3_project_list/create`, `t3_thread_launch`, `t3_thread_read`, `t3_thread_wait`) after a one-time OAuth pairing, and
a thread is brought forward with `t3code://threads/<env>/<thread>`. A deep link alone only drafts a prompt.

The spike runs with a scratch project and no Sindri code merged. **The teammate who uses T3 Code runs it on their own
box** (review C14), with the owner watching or reading the evidence. It records the T3 Code version and commit it
proved (review C18); SL-4's probe later reports the installed version and warns outside the tested range.

**The pairing credential** (review C13) lives in `$AW_SINDRI_HOME/auth/t3code.token` (0600, the directory 0700), or
behind a profile secret pointer (X18). It is never in `launcher.json`, argv, events, logs or the spike's evidence; the
evidence (transcripts, command output, screenshots) is passed through `Aw.Redaction`, and screenshots are checked by
hand before they are attached. A T3-hosted session has no launch secret, so it can never `step-report`; its liveness
from `t3_thread_read` is a request Sindri records, not a decision.

| # | Question | Pass when |
|---|---|---|
| T1 | Can a script pair once and then launch a thread with a fixed seed, unattended? | `t3_thread_launch` returns a thread id from a non-interactive client after one pairing |
| T2 | Does the session reach Sindri's MCP endpoint? | The thread's first tool call is `sindri_item` (or the reason it can't be, recorded) |
| T3 | Can Sindri tell the thread is alive and when it ends? | `t3_thread_read`/`wait` gives a state that maps to `session.ended` (`exit` or `lost`) |
| T4 | Can usage be read? | Token counts per thread are readable, or the spike records that `usage: none` applies |
| T5 | Does Claude Code's `UserPromptSubmit` hook fire inside a T3 thread? | Recorded yes or no; no means the first typed turn is not a takeover, so opening stays the only takeover |
| T6 | Does the deep link focus the thread from `open`/`xdg-open`? | The desktop app comes forward on the thread |
| T7 | Is a T3 thread a session caller (review C10)? | `sindri whoami` (or any `sindri` call) run by the thread's agent classifies as a session, because the thread's process carries `AW_SINDRI_CHILD` or the harness probe (API §6) recognises T3's process. Fail blocks SL-4: a thread that the CLI takes for the human is not shipped |
| T8 | Does the teammate get to a live thread (review C14)? | From a fresh pairing, the teammate opens an item and reaches a live T3 thread, or the fallback below, in under 10 minutes; the time is recorded |

**The fallback, written now** (in `docs/csharp/sindri.md`, SL-0): a T3 Code user sets the `print` viewer (or their
terminal's `exec-template`), and `sindri open ITM-…` prints the `tmux -L sindri-launch attach …` command to run in any
terminal, including one inside T3 Code if it has one. The session is the normal tmux-hosted CLI, so liveness, usage,
budgets and takeover all work; T3 Code just isn't the window.

Outcome: a findings note appended to this spec (§11) and an owner decision. If T1–T3 and T7 pass, phase SL-4 builds a
`t3code` adapter as a **viewer that owns its process** with the `none` host: human opens and resumes only (SL4). If
T1, T3 or T7 fails, T3 Code users take the fallback, and the idea stays in the data set.

## 8. Testing

Fits `.agents/rules/csharp-testing.md`:
1. **Contract tests over a fake** (`Aw.Sindri.Testing`): `SessionHostContract` (start → alive; stop → not alive; the
   attach argv names the session; ids are validated before use; `env` reaches the process; `AttachedClients` counts a
   client; a start past its deadline returns failed and leaves no session) and `TerminalViewerContract` (open records
   one outcome under `FakeTimeProvider`; non-zero exit inside 3 s is `failed` with its stderr as `detail`; a client
   inside 3 s is `opened`; none is `unconfirmed`; no secret and no item text in argv or files; per-launch files are
   0600 and swept), each run against a recording fake and the real adapters that can run headless.
2. **Unit:** golden argv per built-in agent and mode; `launcher.json` parsing and merging over the defaults, from the
   shared `tests/fixtures/launcher/` set, each fixture also validated against `schemas/launcher.schema.json` with the
   same verdict; an override without `"override": true` is `SND-LAUNCH-011`; `argvHash` goldens (JCS form, merged
   entry, `bin` by basename; a `bin`-directory-only override hashes equal to its built-in); placeholder expansion with
   spaces, quotes, `$`, newlines and a leading `-` (each stays one argument, a leading `-` is refused); the generic
   session-id bound refuses `..` and a leading `.`; target precedence with a skipped default; a `usage: none` target
   with a budget is refused; SL8 (built-in passes; a `bin`-only override passes; an added entry passes only with a
   matching `stepArgvHash`, else `-012` with `not-approved` or `hash-changed`); per-section and per-entry errors (§4's
   table); the generator's detection under a fake PATH, `$TERM_PROGRAM` and no GUI session, used in memory when there
   is no file; `LaunchResult` stage markers under `FakeTimeProvider` (host past 4 s, viewer past the window).
3. **Integration lane:** the real `TmuxSessionHost` on a private socket and `TMUX_TMPDIR`, a stub agent script in place
   of `claude`, the `print` viewer and an `exec-template` viewer that is a script recording its argv; four viewer
   scripts: one that attaches (`opened`), one that attaches after 5 s (`unconfirmed`, then `launch.viewer-attached`),
   one that exits 0 without attaching (`unconfirmed`), one that writes to stderr and exits 1 (`failed` with that
   `detail`, session alive and attachable); the stub agent sees `AW_SINDRI_CHILD`, `AW_SINDRI_ITEM` and
   `AW_SINDRI_LAUNCH`; `session wrap`'s guard exits 127 with `SND-LAUNCH-003` for a missing `bin`; two concurrent
   opens of a stub `reported` agent record each its own id through the stub hook as `launch.session-reported`, and
   Resume still offers it after `sindri views rebuild`, and with the hook absent the fallback
   refuses both as ambiguous; `launch.probed` with `host`; the whole launch inside E-1's 10 s limit.
4. **Real-app proof** through `scripts/sindri-prove.sh` (§10's proof column).
5. **Bash:** `scripts/tests/meta-launch.test.sh` gains `AW_LAUNCHER_CONFIG` cases (`print`, a recording
   `exec-template`, an unknown key refused with `SND-LAUNCH-011`, every shared fixture against its `bash:` tag,
   `--init-launcher` under a fake PATH, a non-Claude lane agent, `AW_META_CLAUDE` winning over the file, two `reported`
   lanes started one at a time with distinct ids, `tmux -L <team> show-environment -g` carrying none of the lane variables);
   SL-1b's cases are in §10.2.

## 9. What stays out

- No `iterm`, `macos-terminal`, `vscode` or `zellij` adapter in this batch (each is a later small phase).
- No change to who may open, attach or take over (API §4, §6), the launch secret or `session wrap`'s heartbeat beyond
  L-9's optional `sessionId` (recorded as `launch.session-reported`).
- No per-repo terminal choice: the terminal belongs to the box. A per-repo agent default is already the profile's.
- No agent-session detection change beyond L-12 (API §6's marker list, CFG-06); it moves to catalog data in a later
  batch.
- The meta's own pane stays Claude Code in this batch: its inbound messages come from its Claude transcript
  (`docs/csharp/meta.md`). The lanes may run any catalog agent (SL6, SL-1b).
- Remote runners (X61b) aren't designed here; `ISessionHost` is where they would fit. Pane capture that is stored (X59c)
  will pass through `Aw.Redaction` first. The live preview (collector spec CA-9) stores nothing and is served
  unscrubbed, to the human only (owner, 2026-10-17).
- Each teammate runs their own Sindri, profile copy and `launcher.json` (review C20). One shared Sindri for a team is
  X61. `launch.started` records the viewer kind, the target and the catalog source (L-10), and doctor's `launcher`
  section prints one line from the `launches` projection: opens in the last 30 days by viewer and by agent. That is
  this batch's adoption measure.

## 10. Phases

| Phase | What | Where | Size | Needs | Proof | Lane |
|---|---|---|---|---|---|---|
| **SL-0** | The `launcher.json` schema and data files, the doc, and the regenerated `docs/sindri/profile.md` (L-8): `schemas/launcher.schema.json` with its `x-aw-keys` block, `config/launcher/builtins.json`, `config/launcher/terminals.json` (the worked examples), the shared fixtures with their `bash:` tags, the `argvHash` goldens, the T3 fallback doc (§7); `AW_LAUNCHER_CONFIG` in `.env.example`; in `deps.yaml`, the file edges from the bash reader and from `Aw.Sindri.Infrastructure` (the embedded built-ins) to the schema and data files, and to `launcher.json` | `schemas/`, `config/launcher/`, `tests/fixtures/launcher/`, `docs/csharp/sindri.md`, `docs/sindri/profile.md`, `.env.example`, `deps.yaml` | S | this spec approved | Every fixture validates against the schema with its expected verdict (a small check run in CI); each `argvHash` golden is the JCS hash of its fixture; `profile.md` matches `SindriProfile`'s keys (a test lists the schema's keys and fails on a key the doc lacks); `scripts/check-env-example.sh` and `node scripts/check-deps.mjs` pass | ideas |
| **SL-1** | `aw-meta-launch` reads `launcher.json`: viewer `warp` (today's layout), `exec-template` (one window per lane) or `print` (one `tmux -L <team> attach -t =<team>-<lane>` line per lane); the lanes' agent from `meta.lanesAgent` and `meta.lanes.<lane>` through the catalog (`open`/`resume` argv, `{name}`, `processName` for `claude_in_pane`, `transcripts` for `has_transcript`); `reported` lanes start one at a time with §4's single-candidate fallback; the bash reader's narrowed checks (§4); env precedence (§4); `AW_SINDRI_CHILD=1`, `AW_META_TEAM` and `AW_META_LANE` exported by `meta-lane.sh` in every lane's environment (L-12; not `new-session -e`, so the no-file tmux commands match the pre-SL-1 ones apart from the added `-L <team>`), and the last two in `.env.example`; `--init-launcher`. A non-Claude lane launches, and `--dry-run` and the launch print "lane <l>: <agent>, launch-only for now: no obligations". Until SL-1b the lane map and `aw-meta health` show such a lane as they show any lane with no Claude session, and `skills/meta/SKILL.md` gains one line: don't message a lane marked launch-only. `install-meta.sh`'s Claude-only check stays until SL-1b. `docs/csharp/meta.md` (L-13) | `scripts/meta-launch.sh`, `scripts/meta-lane.sh`, `scripts/tests/meta-launch.test.sh`, `skills/meta/SKILL.md`, `.env.example`, `docs/csharp/meta.md` | S | SL-0 | `meta-launch.test.sh` passes the new cases (§8.5); on a box with no Warp, `aw-meta-launch --dry-run` with a kitty `exec-template` prints one kitty argv per lane; `aw-meta-launch --init-launcher` under a PATH holding only kitty writes the kitty template; two Codex lanes (a stub `codex` writing rollout files) each record a distinct session id; with no file, the owner's Warp layout matches today's apart from the added `-L <team>` | ideas |
| **SL-1b** | **Non-Claude Meta lanes** (owner, 2026-10-15), detailed in §10.2: the per-provider install with the Codex `/hooks` trust step; `session-id-report.sh` and the lane hook `meta-lane-gate.sh`, both passing straight through outside a lane; lane identity from `AW_META_TEAM`/`AW_META_LANE`; the lane source for the lane map; the commands `lane-send`, `lane-inbox`, `lane-stop-check` and `lane-reply`; Codex delivery at UserPromptSubmit and Stop, Cursor at `sessionStart`, `postToolUse` and the capped `stop` follow-up; the guarded wake; `aw-meta health` rows; the /meta skill text. Under the meta's three conditions (§10.2) | `scripts/install-meta.sh`, `src/Meta/…` (Lanes, the four lane slices), `config/hooks/session-id-report.sh`, `config/hooks/meta-lane-gate.sh`, `config/hooks/adapters/README.md`, `config/hooks/tests/{meta-hooks,failopen}.test.sh`, `skills/meta/SKILL.md`, `docs/csharp/meta.md`, `.env.example`, their tests | M–L | SL-1 | A team with one Claude lane and two Codex lanes, started together: after the `/hooks` trust step each Codex lane records its own session id from the hook (distinct, each matching its rollout); both appear in the lane map; a Codex lane receives a message sent with `lane-send` at its next prompt, or woken while idle with no client attached; its stop is blocked while it owes a reply and released after it ends a turn with `REPLY TO META:`, which the stop hook records through `lane-reply` while the lane runs in Codex's default `workspace-write` sandbox; a lane that never replies is let through after `AW_META_STOP_MAX` blocks and shows "obligation not delivered (stop cap)"; `lane-stop-check` exiting 2 (the team config removed) doesn't block the stop; the meta's `stop-check` sees the reply and the meta's own obligation was cleared by `lane-send`. A Codex lane whose hooks aren't trusted shows "hooks not running: trust them with /hooks in codex" in `aw-meta health`. A Codex session outside any lane passes straight through both hooks. Where `cursor-agent` is installed, a Cursor lane receives the message at `sessionStart` or its next tool call, and its stop follow-up stops after `loop_limit`, shown in `aw-meta health`. A lane on a CLI with no hooks shows "launch-only: no obligations". Condition (a): with a Claude-only team, every existing command's output is byte-identical to before; in the mixed run, the Claude lane's rows and its hook behaviour are unchanged, and the Codex lanes' rows are the only additions. The full run of condition (b) passes; nothing in the run installs aw-meta (condition c) | ideas |
| **SL-2** | The three ports and `launcher.json` inside P10a (L-1…L-5, L-9…L-12, L-14…L-16): `IAgentCatalog` with the embedded built-ins, `TmuxSessionHost` (env, `AttachedClients`, the 4 s start deadline), `WarpViewer`, `ExecTemplateViewer` (stderr detail), `PrintViewer`, the late-attach poll and `launch.viewer-attached`, the config reader against the shared fixtures, the in-memory detection, `sindri launch init` and `show`, SL8 with `-012`'s reasons and the approve preview, `SND-LAUNCH-011`/`-012`, the `launch.started`, `launch.probed` and E-2 additions, the capture file, `session wrap`'s `sessionId` and `launch.session-reported`, doctor's adoption line, the `TriggerNames.cs:60` comment | `src/Sindri/Aw.Sindri.Core/Launch/`, `…Infrastructure/Launch/`, `…Host/Commands/`, `…Features/` (E-2) | M (inside P10a's L) | API P10a's own needs (P3 + P8), SL-0 | P10a's proofs unchanged with no `launcher.json` on a box with Warp; plus `sindri-prove.sh human open ITM-…` with an `exec-template` viewer under `xvfb-run xterm` opens a window titled `sindri ITM-…` with one tmux client attached and `launch.started.viewer.outcome: opened`; a viewer script that attaches after 5 s records `unconfirmed`, then `launch.viewer-attached`, and the item's view reports `opened`; a viewer script that exits 1 with a message records `failed` with that message as `detail`, and the session stays attachable; with `print`, `open` exits 0 and prints the attach command, and attaching by hand reaches the live agent, whose environment has `AW_SINDRI_CHILD=1`; with no file on a box with only xterm, the open uses the detected xterm template; `sindri launch init` on that box writes a file the next open uses; a `bin`-only override of `claude-code` shows no "will park" line in `sindri launch show`; `sindri profile approve`'s preview shows the argv behind a `stepArgvHash`. The gate runs `Aw.Sindri.Ui.Tests` (new catalog rows) | API |
| **SL-3** | UI copy and states for any terminal (L-7, L-15) | `Views/Launch*`, Settings, `mockups/launch-flow.html` | S (inside U6a) | SL-2 | U6a's proof with the `print` viewer shows "Session started: run `<attach>`", the copy button takes focus when focus was on the launch button and announces "Copied"; with focus moved elsewhere before the outcome arrives, focus stays there and the live region announces the line; a viewer that attaches late turns "hasn't attached yet" into "Sent to <label>"; the item row reads `lastStarted` from E-2's `viewer` and `attach`; an invalid `launcher.json` replaces the button before the click with "line N" from `terminal.detail` and the file from `terminal.configPath`; a profile default this box lacks resolves to the next rule with the reason in the menu; Settings shows the terminal and its source; with Warp, the strings read "Sent to Warp" as before | UI |
| **SL-T** | T3 Code spike (§7): T1–T8 answered on the teammate's box | a scratch dir outside the repo; findings in §11 | S | the teammate pairs T3 Code once | The §7 table filled with evidence (commands, ids, redacted screenshots, the T3 version) and an owner decision | ideas |
| **SL-4** | `t3code` viewer with the `none` host, if SL-T passes | `…Infrastructure/Launch/T3CodeViewer.cs` | M | SL-2, SL-T + owner decision | `sindri-prove.sh human open ITM-…` with `viewer.kind: t3code` starts a T3 thread whose first call is `sindri_item`; `sindri` from inside the thread is a session caller; ending the thread writes `session.ended`; Attach isn't offered; the pairing token is only in `auth/`; a Step on the same box still starts in tmux | API |
| **SL-5** | Agent runtime: `Attended: false`, always the tmux host, unapproved entries park (L-6, SL8); `workflow.agent.target` takes `AgentId` (L-14) | engine P8 | XS (inside P8) | SL-2 | Evals: on a box whose `launcher.json` sets `host: none`, a Step starts in tmux; with `workflow.agent.target: aider` (an added entry), a Step without a matching `stepArgvHash` parks with `SND-LAUNCH-012` `not-approved`; with the hash approved, it launches; after the entry's `open` template changes, it parks with `hash-changed` and both hashes; a `bin`-directory-only override of `claude-code` launches with no hash approved | engine |

### 10.1 Parallelization plan

The ideas lane runs one builder at a time; the other lanes take their edits into phases they already own.

| Wave | Ideas lane | API lane | UI lane | Engine lane | Owner and teammate |
|---|---|---|---|---|---|
| 0 | SL-0 (schema, data, goldens, docs) | reads L-1…L-5, L-9…L-12, L-14 (API and profile keys), L-15 (E-2) and L-16 into P10a's plan | reads L-7 and L-15 into U6a's plan | reads L-6, L-11 and L-14 (`workflow.agent.target`) into P8's plan | owner approves this spec; the teammate pairs T3 Code for SL-T |
| 1 | SL-1 (bash), then SL-T with the teammate | P10a with SL-2 folded in | — | — | — |
| 2 | SL-1b (non-Claude Meta lanes); the meta reinstalls aw-meta after its merge and trusts the Codex hooks | — | U6a with SL-3 | P8 with SL-5 | decides SL-4 from SL-T's findings |
| 3 | — | SL-4 if approved | — | — | — |

SL-0 and SL-1 don't wait on any other lane, so a Claude Code teammate on any terminal can use `aw-meta-launch` before
P10a lands, and a teammate on Codex or Cursor can launch lanes then and take part in /meta's obligations once SL-1b
merges. SL-1b and SL-2 touch no common files and run in parallel. SL-1b ships `config/hooks/session-id-report.sh`;
SL-2 reads only the file it writes (`$AW_SINDRI_HOME/launches/<launchId>.session-id`, §4) and uses the fallback on a box
where SL-1b's install hasn't put the hook in place, so neither waits on the other. SL-2 doesn't wait on SL-T: the `none` host
is in the port from the start, and only its T3 adapter waits. SL-5 needs SL-2's `Attended` flag and P8's L-14 key, both
in wave 1–2.

### 10.2 SL-1b: non-Claude Meta lanes

(1) **Install.** `install-meta.sh` accepts any installed provider: it publishes and links `aw-meta`/`aw-meta-launch`
for every provider and installs the lane hooks per provider. The Claude-only check (`scripts/install-meta.sh:33-36`)
goes. For Codex it prints the trust step after every install, since Codex records trust against each hook's hash
(`config/hooks/adapters/README.md`, Codex protocol facts): "open `codex` and run `/hooks` to trust the `aw:*` hooks".

(2) **Lane identity and liveness** (review X11). `aw-meta-launch` has `meta-lane.sh` export `AW_META_TEAM` and `AW_META_LANE` into each lane's
process environment (SL-1; not `new-session -e`, which would change the no-file tmux commands). Every lane-side hook and command takes the lane from them only; none has a
`--lane` flag. The session id comes from `session-id-report.sh` (§4), so lanes start together. A lane source beside
today's `~/.claude/sessions` reader (`Aw.Meta.Core/Lanes/LaneSource.cs`, `LiveSession.cs`) reads
`lane-sessions/<lane>`, the pane's `processName` and the `transcripts` glob, so `lanes-refresh` and the lane map see a
Codex or Cursor lane. `role-set` stays the Claude path.

(3) **Commands** (all new, additive, `--json` like the others; exit 0 ok, 1 attention, 2 usage or tool error, the error
one stderr line `AW-META-NNN: <reason>`). New codes: `AW-META-014` the lane variables are unset, `AW-META-015` the lane
has no `lane-sessions/<lane>` record, `AW-META-016` unknown lane, `AW-META-017` a Claude lane (use `SendMessage`). As
meta.md requires of hook commands, `lane-inbox`, `lane-stop-check` and `lane-reply` exit 0 silently when no team config
exists; `lane-send` needs it (`AW-META-002`):

| Command | Who runs it | What it does | Exit codes |
|---|---|---|---|
| `aw-meta lane-send --lane <l> (--text <t> \| --file <f>) [--json]` | the meta | Appends to lane `<l>`'s inbox file under `$AW_STATE_DIR/meta/`, beside the team's files, and records the send in `meta.db` as `outbox-record` does for `SendMessage`, with `to` `<team>-<lane>`, so the meta's own obligation clears. Wakes the lane if it's idle (5) | 0 sent; 1 sent but the lane is launch-only or its hooks aren't running; 2 unknown lane (`-016`), a Claude lane (`-017`), or usage |
| `aw-meta lane-inbox [--json]` | the lane hook | Prints the lane's unread messages and marks them delivered; records the hook heartbeat | 0 (empty or not); 2 when the lane variables are unset (`-014`) |
| `aw-meta lane-stop-check [--stop-hook-active] [--json]` | the lane hook at stop | Reports whether the lane owes the meta a reply; records the heartbeat. Loop guard as `stop-check`'s (meta.md): a check without `--stop-hook-active` blocks and resets the count; each continuation with it and the same owed set counts one block, and after `AW_META_STOP_MAX` (default 3) the next is let through with a one-line warning, and `aw-meta health` shows "obligation not delivered (stop cap)" | 0 nothing owed, or the cap reached; 1 owed (the hook blocks with the reason); 2 unset variables (`-014`) |
| `aw-meta lane-reply (--text <t> \| --file <f> \| --stdin) [--json]` | the lane's stop hook (4), or the lane's agent where its sandbox lets it write `$AW_STATE_DIR/meta/` | Inserts a message row in `meta.db`, as `inbox-sync` records one (`msg_id` `lane:<lane>:<n>`, `from` `<team>-<lane>`), so the meta's `stop-check` (`Aw.Meta.Features/StopCheck`) derives its `reply` obligation from it and `lane-send`'s next send to `<team>-<lane>` closes it; clears the lane's owed reply. The running log records team, lane, provider and session id | 0 ok; 2 when the variables are unset (`-014`) or `lane-sessions/<lane>` has no record (`-015`) |

Claude's cross-session `SendMessage` (and `outbox-record`'s PostToolUse payload, `Aw.Meta.Features/OutboxRecord`)
isn't available to other CLIs, which is why these go through aw-meta's own files. `skills/meta/SKILL.md` says: send to
a non-Claude lane with `aw-meta lane-send`; the lane map shows which lanes those are. A non-Claude lane's seed prompt
says to answer the meta by ending the turn with a `REPLY TO META:` line (4).

(4) **Delivery per provider** (review X3), through the existing adapters `config/hooks/adapters/codex.sh` and
`cursor.sh`, by one new script, `config/hooks/meta-lane-gate.sh`, beside `meta-stop-gate.sh` and `meta-outbox.sh`:

| Provider | Inbox delivered at | Stop gate | Source (`config/hooks/adapters/README.md`) |
|---|---|---|---|
| Codex | UserPromptSubmit: plain stdout becomes developer context | Stop: `decision: "block"` with the reason while `lane-stop-check` exits 1 | Codex protocol facts |
| Cursor | `sessionStart` and `postToolUse`, as `additional_context` | `stop`: `followup_message` while owed. It is **capped by `loop_limit`** (default 5), after which Cursor ends the turn; `aw-meta health` then shows "obligation not delivered (Cursor loop_limit)" | Cursor protocol facts; the `prompt-sort.sh` row: `beforeSubmitPrompt` returns `{continue}` only and can't carry context, so it isn't used |

The adapters README's `meta-stop-gate.sh` and `meta-outbox.sh` rows stay unmapped for Codex and Cursor; a new
`meta-lane-gate.sh` row records the mapping above.

**The hook records the reply, not the agent** (re-check M4). Codex runs the lane's agent in its `workspace-write`
sandbox by default, where it can't write `$AW_STATE_DIR/meta/`; hooks run in the CLI's own process, outside that
sandbox, as `probe-log.sh` at Codex Stop already writes `$AW_STATE_DIR/probe/codex/` (adapters README mapping). So
the lane prompt tells the agent to end a turn that answers the meta with a line `REPLY TO META:` followed by the reply.
At stop, `meta-lane-gate.sh` takes the turn's last assistant text, as `done-gate.sh` does (Codex `last_assistant_message`;
Cursor the transcript's last assistant text), and pipes the text after that marker to `aw-meta lane-reply --stdin`
before it runs `lane-stop-check`. The agent never needs write access to the state dir, and no lane argv widens the
sandbox. **Exit codes:** only `lane-stop-check` exit 1 becomes a block (Codex `decision: "block"`, Cursor
`followup_message`); exit 0, exit 2 (unset variables, no team config, a tool error) and anything else pass through,
with the stderr line logged by `failopen.sh`.

**Pass-through** (review X12). Codex and Cursor hooks are user-wide, so `meta-lane-gate.sh` and
`session-id-report.sh` exit 0 at once unless `AW_META_LANE` is set (`session-id-report.sh` also runs for
`AW_SINDRI_LAUNCH`), skip `AW_JUDGE_CHILD` as `meta-stop-gate.sh` does, and run under `failopen.sh`. Both join
`config/hooks/tests/failopen.test.sh`, and `meta-hooks.test.sh` gains a non-lane Codex session that passes through.

(5) **Wake an idle lane** (review X14). `lane-send` wakes a lane only when all of these hold; otherwise the message
waits for the lane's next hook:

| Check | How |
|---|---|
| The lane is idle | its last lane-hook event is a stop, with no prompt event since (heartbeats from `lane-inbox` and `lane-stop-check`) |
| The agent is still running | `#{pane_current_command}` of `=<team>-<lane>` (on `-L <team>`) is the entry's `processName`; a bare shell is reported as "lane down" and gets no keys |
| No human is attached | `tmux -L <team> list-clients -t =<team>-<lane>` is empty |

The wake is one fixed line sent with `tmux -L <team> send-keys -t =<team>-<lane> -l '<line>'` (literal, exact target), then
`tmux -L <team> send-keys -t =<team>-<lane> Enter` (owner, 2026-10-16: the team runs on its own tmux socket, #264).

(6) **Health** (review X13). `aw-meta health` and the lane map show, per non-Claude lane: taking part; "hooks not
running: trust them with /hooks in codex" when no lane-hook heartbeat arrived since launch; "obligation not delivered
(Cursor loop_limit)" or "(stop cap)"; "ambiguous session id" (§4); or "launch-only: no obligations" for a CLI with no hook support.

**Conditions (the meta, 2026-10-15):** (a) every existing aw-meta command, flag and exit code, and the Claude hook
contract (`meta-stop-gate.sh`, `meta-outbox.sh` and their JSON), stays unchanged: non-Claude support is additive only;
(b) the pre-ready run, through the heavy lock, includes `Aw.Meta.Tests` unit and integration, `Aw.Architecture.Tests`,
`bash src/Meta/aot-smoke/run.sh`, and the bash suites `meta-hooks`, `meta-launch`, `install-meta` and `failopen`;
(c) the lane never installs aw-meta itself; the meta reinstalls after merge.

## 11. Open questions

| # | Question | Answer |
|---|---|---|
| Q1 | May user-added catalog entries run without profile approval? | Provisional: yes for **human opens**: they run as the user, in the user's terminal, the same as typing the command. The profile still decides which agent is the default, and budgets still apply (L-5). **Unattended Steps** run only built-in entries, `bin`-directory-only overrides, or entries whose `argvHash` the profile approves (SL8); the approve preview shows the argv behind each hash (L-16) |
| Q2 | Should a T3 Code user be able to attach to factory Steps? | Provisional: no. Steps stay tmux-hosted (SL4), on a T3 box too. T3 Code covers human opens and resumes |
| Q3 | Default viewer with no `launcher.json`? | Provisional: `init`'s detection run in memory (Warp, then `$TERM_PROGRAM`, then the first terminal on PATH), else `print`, resolved once per launcher service start and on Check again (§4). The owner's setup is unchanged, a Linux GUI box gets a window, and a headless box works |
| Q4 | Does `aw-meta` keep bash for launching or move to the C# `ILauncher`? | Provisional: bash with `jq` in SL-1, reading the shared schema and built-ins files with the narrowed checks (§4); revisit when aw-meta and Sindri share a launcher assembly |
| Q5 | May Meta lanes run non-Claude agent CLIs (review C4)? | **Decided (owner, 2026-10-15): yes**, "also allow non-claude agents in them". SL-1 launches them; SL-1b makes them take part in /meta's obligations through aw-meta's own inbox and outbox files and the per-provider hook adapters (§10.2). The meta's own pane stays Claude Code in this batch (§9) |

## 12. Review notes (/autoplan pass 1, 2026-10-15)

Findings from `consolidated-review.md` (product, arch, design, devex, security) and what changed.

| Finding | What changed (section) |
|---|---|
| C1 (HIGH) closed target enum | L-9 opens `target`, `--agent`, `launch.started.target` and X52 to `AgentId`, checked against the catalog; `sessionIdPattern` per entry; N-agent picker in L-7 (SL3, §4, §6) |
| C2 (HIGH) viewer failure vs `launch.failed` | `launch.started` gains `viewer` and `attach` (L-10); `-001`/`-009` re-classed for tmux hosts, kept as `launch.failed` for `none` hosts (L-3); §5's last column is now the viewer outcome; L-7 copy (SL5, §5, §6) |
| C3 (HIGH) 10 s viewer wait | 3 s observation window plus a `tmux list-clients` check; `unconfirmed` outcome; the budget fits E-1's 10 s (§3, §5, §8) |
| C4 (HIGH) non-Claude Meta lanes | Owner decided: allowed. SL6 and SL-1 let `meta.lanesAgent` and `meta.lanes.<lane>` name any catalog agent; new phase SL-1b with the meta's three conditions; §11 Q5 decided (SL6, §4, §9, §10, §10.1, §11) |
| C5 (HIGH) hand-written config | `sindri launcher init` and `aw-meta-launch --init-launcher` detect the terminal and write the file; doctor's fix text names it; proofs in SL-1 and SL-2 (§4, §10) |
| C6 (HIGH) `-011` and disabled default in the UI | §6.6 row for `SND-LAUNCH-011` before the click; a default missing from this box falls through to the next precedence rule (§4, L-7) |
| C7 (MEDIUM) unapproved argv in Steps | SL8, `SND-LAUNCH-012`, `agents.<id>.stepArgvHash` (L-11), SL-5's evals; Q1 split by attended (§1, §6, §10, §11) |
| C8 (MEDIUM) silent overrides | `"override": true` required; doctor and `launch.probed` show overrides (§4, L-4) |
| C9 (MEDIUM) no record of what ran | `catalogSource` and `argvHash` on `launch.started` (§2, L-10) |
| C10 (MEDIUM) agent env and guard | `ISessionHost.Start(env)` and `session wrap` set `AW_SINDRI_CHILD`/`ITEM`/`LAUNCH` and keep the `command -v` guard; SL-1 exports `AW_SINDRI_CHILD` in lane panes (L-12); spike T7 (§3, §6, §7) |
| C11 (MEDIUM) two parsers, missing fields | One `schemas/launcher.schema.json` both readers check, shared fixtures and built-ins file; `processName`, `transcripts`, `{name}`, `reportedId` added (§4, SL-0, SL-1) |
| C12 (MEDIUM) env vs file precedence | env > file > defaults; Meta's `AW_META_CLAUDE*` and `AW_WARP_LAUNCH_DIR` ranked; the first two deprecated for users (§4, L-13) |
| C13 (MEDIUM) T3 pairing credential | `$AW_SINDRI_HOME/auth/t3code.token` 0600 or a secret pointer; redacted from evidence (§7) |
| C14 (MEDIUM) spike on the wrong box | The teammate runs SL-T; T8 times the path to a live thread; the fallback is written now (§7, §10) |
| C15 (MEDIUM) voice, `print` copy, a11y, Settings | Copy names the viewer's label; `print` and `lastStarted` copy; live region and focus for the copy button; read-only terminal in Settings (L-7) |
| C16 (MEDIUM) `attended` undefined | Defined in §2 and L-1 (`LaunchRequest.Attended`) |
| C17 (LOW) files without a phase | `.env.example`, `deps.yaml` in SL-0; `docs/csharp/meta.md` in SL-1/SL-1b (L-13); `TriggerNames.cs:60` in L-2 and SL-2 |
| C18 (LOW) leading `-`, `-e`, perms, T3 version | §4's option-smuggling rule and owner/parent-dir checks; the T3 version pinned in §7 |
| C19 (LOW) default viewer flips | Resolved once per service start; `terminal.source` (§4, L-4) |
| C20 (LOW) one Sindri per teammate | One line in §9; usage counted from `launch.started` (L-10) |

Rev 3 renames `sindri launcher init|show` to `sindri launch init|show` (§12.1, LOW).

### 12.1 Review notes (/autoplan pass 2, 2026-10-15)

Findings from `pass2/consolidated-review.md` (X1–X20 and the LOWs) and the five lens files, and what changed.

| Finding | What changed (section) |
|---|---|
| X1 (HIGH) agent ids still closed in profile and engine keys | New L-14: WF §6.5/P8 `workflow.agent.target`, E-4's `launch.default`/`launch.repos.<repo>.default`, E-28's `agents.<id>` map and the API §3 open row's `target?` take `AgentId`, checked by pattern only; `agents.softLimit` reserved; P8 in the phase column; E-28 cited to the screens spec and API §3.8, which joins "Builds on"; SL-5's evals use `workflow.agent.target: aider` (header, SL2, SL3, §4, §6, §10) |
| X2 (HIGH) newest-file id capture races | Ids come from the CLI's SessionStart hook (`session-id-report.sh`) keyed on `AW_SINDRI_LAUNCH` or `AW_META_TEAM`/`AW_META_LANE`; the file search is a fallback that skips claimed files and refuses on more than one match; lanes without a hook start one at a time; two-Codex-lane proofs in SL-1 and SL-1b and a concurrent case in the integration lane (§4, §8, §10, §10.2) |
| X3 (HIGH) Cursor `beforeSubmitPrompt` can't carry context | Cursor delivery at `sessionStart`/`postToolUse` `additional_context` and the `stop` `followup_message`, capped by `loop_limit` and shown in health; cites `config/hooks/adapters/README.md` (§10.2 (4), (6)) |
| X4 (HIGH) copy without data | L-4 adds `terminal.configPath` and `terminal.detail {line, reason}`; new L-15 adds `lastStarted.viewer {kind, label?, outcome}` and `attach` to E-2 (§3, §4, §6) |
| X5 (HIGH) focus on an async outcome | Focus moves to the copy button only if it's still on the launch button that sent the request; otherwise a live-region announcement (L-7, SL-3) |
| X6 (MEDIUM) per-box `bin` in a shared profile | `argvHash` uses `bin`'s basename; `stepArgvHash` takes one hash or a list (§2, L-11) |
| X7 (MEDIUM) canonical form undefined | RFC 8785 JCS over the merged entry's `{bin basename, open, resume, sessionId, mcp}`; inputs and non-inputs listed; goldens in SL-0 (§2, §8, SL-0) |
| X8 (MEDIUM) `bin`-only override parks Steps silently | Such an override keeps the built-in's hash; doctor, `init` and `show` print the exact profile line for unapproved entries; `-012` gains `reason: not-approved \| hash-changed` and names `sindri launch show <id>`; new L-16 shows the argv in the approve preview (SL8, §4, L-3, L-16) |
| X9 (MEDIUM) `unconfirmed` never upgrades | Background poll to E-1's 30 s deadline and the additive `launch.viewer-attached`; the projection reports `opened` with `late: true` (§3, L-10, L-7, SL-2, SL-3) |
| X10 (MEDIUM) `host` box-wide vs per target | Unattended launches always use tmux; `host` applies to attended opens only; the `host-none-human-only` reason is gone (SL4, §4, L-4, L-6, SL-5) |
| X11 (MEDIUM) SL-1b commands and lane identity | `AW_META_TEAM`/`AW_META_LANE` per lane session, no `--lane` flag on the lane side; `lane-send`, `lane-inbox`, `lane-stop-check`, `lane-reply` with exit codes; `skills/meta/SKILL.md` edits; the interim lane-map state before SL-1b (§2, §10, §10.2) |
| X12 (MEDIUM) global lane hooks | Both new hooks pass straight through outside a lane, skip `AW_JUDGE_CHILD`, run under `failopen.sh`, and join `failopen.test.sh` and `meta-hooks.test.sh` (§10.2) |
| X13 (MEDIUM) Codex `/hooks` trust | `install-meta.sh` prints the trust step; health flags a lane whose hook never fired; in SL-1b's proof and L-13 (§10.2, SL-1b) |
| X14 (MEDIUM) `send-keys` wake | Idle defined; skipped when a client is attached or the pane runs a shell; literal mode, exact target (§10.2 (5)) |
| X15 (MEDIUM) bash parity unreachable in `jq` | Bash promise narrowed to unknown keys, placeholders and the types SL-1 uses; `x-aw-keys` block; fixtures tagged `bash:` and run by `meta-launch.test.sh` (§4, §8, SL-0) |
| X16 (MEDIUM) no-file default on Linux GUI | `init`'s detection runs in memory with `terminal.source: detected`; `print` only when nothing is found; Q3 updated (§4, L-4, §11) |
| X17 (MEDIUM) 10 s limit hides host state | `Launch` owns a 4 s host-start deadline with `kill-session` cleanup and returns `Started` or `Failed(code, stage)`; E-1's 10 s is a backstop (§3, L-1) |
| X18 (MEDIUM) `reportedId` path and `resumable` | `capture.json` per launch for `session wrap`; the liveness `sessionId` is stated as the one change; E-2's `resumable` reads it (L-9, L-15, §6 closing line, §9) |
| X19 (MEDIUM) copy gaps | Copy table with label-less `print` forms, "Sindri hasn't answered" vs "hasn't attached yet", and "Session live" for every started outcome (§6) |
| X20 (MEDIUM) exec-template failure has no reason | First 2 KB of stderr, redacted, as `viewer.detail` and in doctor (§5, L-10) |
| LOWs | `meta` errors affect only Meta and one bad entry drops only itself (§4 table); Warp wording in `tabTitle`, the copy table and U6a (L-7, L-10); lane variables with `new-session -e`, never `-g`, and the pane-shell limit noted (L-12); doctor's opens-by-viewer-and-agent line (§9); "launch-only for now" instead of a phase id (SL-1); `host-none-human-only` removed; built-in picker positions stable (L-7); `init`/`show` output and exit codes, `sindri launch init\|show` beside `launch probe`, `--stdout` in place of `--print`, SL-2's gate runs `Aw.Sindri.Ui.Tests` (§4, L-2); embedded `builtins.json` resource and `deps.yaml` edge (§4, SL-0); `NonBacktracking` with a match timeout (§4); the session-id bound refuses `..` (L-9); `argv[0]` probed as a positional argument (§5); Check again re-runs detection (§4); `AW_META_CLAUDE_ARGS` "inserted after `{bin}`" (§4); the 4 s bound is a timeout (§3); `install-meta.sh:33-36` (§10.2); SL8 doesn't re-read the file between check and build (SL8) |

### 12.2 Narrow re-check (2026-10-15)

Findings from `recheck-rev3.md` (head a3f19eb6) and what changed.

| Finding | What changed (section) |
|---|---|
| H1 (HIGH) the reported id isn't in the ledger | `service:launcher` appends the additive public event `launch.session-reported {launchId, sessionId}` (key `launch.session-reported:<launch>`) when wrap's heartbeat or exit call first carries the id; the heartbeat route still appends nothing; E-2's `resumable` reads it from the `launches` projection, so it survives a views rebuild; L-9, L-15, §6's closing line, §9, §4's Read row, §8 and SL-2 reworded |
| M1 (MEDIUM) `launch.viewer-attached` unregistered | Key `launch.viewer-attached:<launch>`, public, P10a, `EventCatalog` entry and an API §3.4 row, through L-10 (§3, L-10) |
| M2 (MEDIUM) per-launch directory not beside the secret | Flat files in `$AW_SINDRI_HOME/launches/`: `<id>.capture.json` and `<id>.session-id` beside `<id>.secret`; covered by the 60 s cleanup, wrap's read, `session.ended` and the start-up sweep (§4, L-9, §10.1) |
| M3 (MEDIUM) `lane-reply` names a store `inbox-sync` doesn't read | `lane-reply` inserts a `meta.db` message row (`msg_id` `lane:<lane>:<n>`, `from` `<team>-<lane>`); `lane-send` records its send `to` `<team>-<lane>` (§10.2 (3)) |
| M4 (MEDIUM) Codex stop gate can hold a lane forever | `lane-stop-check --stop-hook-active` with `AW_META_STOP_MAX` and a "(stop cap)" health line; the stop hook records the reply from the turn's last assistant text (`REPLY TO META:`), since hooks run outside Codex's sandbox (`probe-log.sh` already writes `$AW_STATE_DIR/probe/codex/`); only exit 1 blocks, exit 2 passes; proof cases in SL-1b (§10.2 (3), (4), (6), SL-1b) |
| M5 (MEDIUM) SL-1b byte-identical claim | Scoped to a Claude-only team; in the mixed run the Claude lane's rows are unchanged and the Codex rows are the only additions (SL-1b) |
| SL-1 review run 1 (PR #261) | Lane variables are exported by `meta-lane.sh`, not set with `new-session -e`, so the no-file golden stays byte-identical (L-12, §2, SL-1, §10.2 (2)); reported-id lanes start under a team start lock in `meta-lane.sh`, and the fallback counts paths absent from a snapshot taken before the agent runs |
| LOWs | `reportedId.hook` is a boolean (the hook reads `session_id`; `cursor.sh` maps it); `terminal.source: default` dropped; the hook writes for Sindri launches once SL-1b installs it, and `AW_SINDRI_HOME` joins the agent's environment (§3, §4, §10.1); host-stage `-009` has its own copy and `launch.failed` carries `detail.stage` (L-1, L-7); `x-aw-keys` validated with `Ajv2020`, `strict: false` (§4); lane commands' `AW-META-014`…`-017` and their no-team-config behaviour (§10.2 (3)); `viewer.detail` left out of the public form (L-10); wrap starts the agent as its child rather than exec'ing it (§3) |
