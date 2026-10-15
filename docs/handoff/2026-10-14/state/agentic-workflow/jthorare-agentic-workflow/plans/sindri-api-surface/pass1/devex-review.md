# Plan Devex Review — sindri-api-surface

**Plan:** docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md
**Reviewed:** 2026-10-10
**Developers judged:** coding-agent implementers, reviewers and the P8 prover run by `/specToProvenPR` as a parallel team (spec "Parallel delivery"), plus the one human owner.
**Checked against:** S1 code at `.claude/worktrees/sindri-s1-app-shell` (commit a363f22), read-only. Owner decisions taken as given and not re-argued: CQRS, v0 local-only auth through `X-Sindri-Caller`, webhooks cut, MCP proven by a real subagent.

## Summary

The spec is unusually complete for an API design. It has a decision table, an endpoint table for every phase, codes reserved per phase, a conflict list against S1, a signal for each phase and the team's rules. Most of its DX problems come from facts it states about S1 and the repo's tooling that the S1 code contradicts. A parallel agent team will hit those on its first slice:
- `SND-API-003` is already taken.
- `docs/sindri/errors.md` is the generated TS catalogue.
- `POST /profile/validate` doesn't exist.
- `/new-slice` can't scaffold into Sindri's hand-written `EndpointTable`/`CommandTable`.
- C1 is already fixed.

The second cluster is the agents' own runtime. Agents run inside Claude Code, where `CLAUDECODE` is set, so every `sindri` desk or decision verb in the phase signals is refused with `SND-AUTH-002`. `sindri` on `PATH` is the TS build. Three implementers would share one default home and port 7410. The P8 prover can't see an MCP server added after its session started.

None of these is CRITICAL, because every one has a cheap written fix, but together they block the "proof by hand" step of most phases. Time to first success: about 30–40 min for an implementer as written (cold build, a `/new-slice` refusal, and finding a way to approve a profile and act as the human). About 10 min with the fixes below.

## Findings by severity

### CRITICAL

None.

### HIGH

- **API surface ergonomics:** Error code collision. The spec reserves `SND-API-003` for "`X-Sindri-After` not reached within 2 s" (503). S1 already defines `SND-API-003` as `PortInUse` ("The API port is taken by another program"), and `sindri serve` exits 2 with it. Duplicate codes also break `ErrorCatalogue.Validate` (`SindriErrorsTests.The_catalogue_is_valid`) the moment P2 adds the row. The spec's own rule, "the codes are reserved per phase in §9, so two phases never pick the same code", is broken by S1 itself.
  - Evidence: plan line 83 "Past 2 s: `SND-API-003` (503)"; line 332 "`SND-API-003` | Unavailable (503) | `X-Sindri-After` not reached within 2 s | P2". S1 `src/Sindri/Aw.Sindri.Core/SindriErrors.cs`: `PortInUse = new("SND-API-003", ErrorKind.Unavailable, "The API port is in use")`. S1 `docs/csharp/sindri.md` line 147.
  - Fix:
    - Renumber the new API codes after S1's set: `SND-API-004` for read-your-writes, `-005` for streams, `-006` for idempotency, `-007` for an unknown caller.
    - Add one sentence to §9: "S1 owns SND-API-001..003, SND-CLI-001..002, SND-LEASE-*, SND-PROFILE-*, SND-LEDGER-*, SND-SCHED-001."

- **Documentation completeness:** The spec points every phase at the wrong error doc. §9 and the shared-files rule say to register new codes in `docs/sindri/errors.md` and print "the fix line from `docs/sindri/errors.md`".
  - That file is generated from the TS `sindri/src/errors.ts` ("Do not edit by hand").
  - It already has `SND-ITEM-404` with a different meaning ("No such item in the ledger. Run `sindri observe`").
  - It defines `SND-CLI-002` differently from the C# build ("Invalid arguments" against "`sindri serve` didn't come up").
  - The C# catalogue lives in `docs/csharp/sindri.md` (error table, lines 135–150). The C# CLI prints its fix from the error message (`Output.cs`), not from any doc.
  - Evidence: plan lines 325–327, 471. S1 `docs/sindri/errors.md` lines 3, 8, 37.
  - Fix:
    - Replace every `docs/sindri/errors.md` with `docs/csharp/sindri.md` (its error table).
    - Say the CLI's fix text lives in the `ErrorDefinition` or message, as S1 does.
    - Note that the TS and C# catalogues share the `SND-` prefix but not meanings until the switch. A reviewer must not "fix" one to match the other.

- **Setup / install:** `/new-slice` can't be used on Sindri as the spec says it will. §3 says "**Slice** is the `/new-slice` name (`--surface http --route …`)". The conflict rules depend on "the `/new-slice` anchors" to keep parallel edits to adjacent appended lines. But S1's host files don't carry the anchors the generator needs:
  - `new-slice.mjs` inserts endpoints at `/(_ = app\.MapGroup\("[^"]*"\)[^;]*);/`. S1's `EndpointTable.cs` uses `var group = app.MapGroup("/sindri"); _ = group.MapGet(…)` with inline lambdas.
  - CLI arms go above `_ => CommandOutput.Usage`. S1's `CommandTable.cs` ends in `_ => await UnknownVerb(args, output)`.
  - `--surface cli` is refused for an http host (`NS-SURFACE`, since the Host is `Microsoft.NET.Sdk.Web`), so the CLI verbs (`Host/Commands/{Queue,Items,ItemAdd}Command.cs`) are hand-written in every phase.
  - Every implementer's first `/new-slice` call stops with `NS-EDIT` and the agent improvises. Three agents improvising on the same four files is the merge-conflict pattern the spec is trying to avoid.
  - Evidence: plan lines 100–102, 466–469. `scripts/scaffold/new-slice.mjs` lines 37, 190, 199. S1 `Host/Endpoints/EndpointTable.cs`, `Host/Commands/CommandTable.cs`.
  - Fix: Add a step to P1, or to S1 if it is still open: "Reshape `EndpointTable`/`CommandTable` to the scaffold's anchors (or teach `new-slice.mjs` a Sindri anchor), and prove it with `/new-slice --package Sindri --slice ListCommands --surface http --route "GET /commands"` producing a red test." Then add a "CLI verbs are hand-written in `Verbs`/`CommandTable` at anchor X" line, so P3+ agents don't look for a generator that doesn't exist.

- **Setup / install (agents as developers):** Agents can't run the human half of their own phase signals. §6 makes the CLI send `X-Sindri-Caller: session` whenever `CLAUDECODE` (or `AW_SINDRI_CHILD` or a Codex/Cursor variable) is set. Every implementer is a Claude Code subagent, so `CLAUDECODE` is set. The signals "shown by hand in the running app" then fail by design:
  - P3: `sindri item add`. P4: `sindri decide D-… accept` → resolved. P6: `sindri services run heartbeat`. P7: `sindri park`. P9: `sindri start`. P10: `sindri open`.
  - Every one returns `SND-AUTH-002`.
  - Also, `profile approve` needs a TTY (`SND-PROFILE-004`). P2 (`GetProfile`, `ListServices`) and P6 (`SND-SVC-404` "no such service in the approved profile") need an approved profile in the temp home, and the spec gives no non-interactive route to one.
  - The only ways around it today are undocumented: `curl` without the header (counted as human), or `env -u CLAUDECODE`. Each agent will discover its own, and reviewers will flag it as a bypass.
  - Evidence: plan lines 271–273 (env rule), 408–415 (signals), 480 "its signal is shown by hand in the running app". S1 `Verbs.Approve`/`Confirmation` refuses non-interactive approval.
  - Fix: Add a "Proving a phase as an agent" paragraph to the delivery section. Given the repo's honest-mistake threat model, an explicit escape is fine:
    1. Human steps in a signal run as `env -u CLAUDECODE -u AW_SINDRI_CHILD <bin> …`, or `curl` without `X-Sindri-Caller`, and the evidence says which.
    2. A test fixture or recipe seeds an approved profile in a temp `AW_SINDRI_HOME`. For example, `curl -X POST …/profile/approve` with the live `confirmHash` as human, or a `Testing` helper that writes `approved/<hash>`.
    3. The P1 signal keeps its session-refusal check, so the guard is still proven.

- **Setup / install (parallel team):** Concurrent implementers will collide on S1's defaults, and `sindri` on `PATH` is the wrong binary.
  - S1 defaults to `AW_SINDRI_HOME=$AW_STATE_DIR/sindri-app` and port `7410`. With 3 implementers each running `sindri serve` for a signal, they share one ledger and one host lease (`SND-LEASE-001`), or lose the port (S1's `SND-API-003`).
  - `which sindri` on this machine resolves to `~/.local/bin/sindri`, the TS CLI, so a bare `sindri item add` in a signal runs TS Sindri.
  - The spec's own examples hardcode `127.0.0.1:7410`.
  - Evidence: absent. Searched the spec for `AW_SINDRI_HOME`, `AW_SINDRI_PORT`, `worktree`, `PATH`, `bin/sindri`, `dotnet run`. Only `$AW_SINDRI_HOME/views.db` (line 63) and the `7410` curl (line 352) appear. `.env.example` lines 22–29. `which -a sindri` → `/Users/thor/.local/bin/sindri`.
  - Fix: In "The team", require each implementer to export:
    - `AW_SINDRI_HOME=<worktree>/artifacts/sindri-home`
    - `AW_SINDRI_PORT=<7410 + slot>`
    - a `SINDRI=<worktree>/artifacts/…/sindri` (or `dotnet run --project src/Sindri/Aw.Sindri.Host --`) alias

    Then write every signal as `$SINDRI …` and `curl "$(jq -r .url $AW_SINDRI_HOME/control.json)/sindri/…"`, not `sindri`/`7410`.

- **API surface ergonomics:** The command chain's order contradicts its audit promise and P1's signal. `AuthorizingService` (2nd) wraps `AuditingService` (4th, innermost). A command refused by authorization never reaches the auditor, yet §4.2 and the P1 signal require "the ledger gains only the `command.handled` row with outcome `refused`". Two implementers will "fix" this differently: audit outermost, or Authorizing writing its own audit row. Also unspecified:
  - whether a `ValidatingService` refusal (outermost) is audited;
  - what idempotency key a refused audit row uses, so a retried refused call doesn't append twice.
  - Evidence: plan lines 44–52, 212–213, 406.
  - Fix: Make `AuditingService` the outermost decorator, or state that `AuthorizingService` appends the refused row itself. Say whether validation failures are audited. Give the audit row's key (`<idempotency key>:command.handled:<outcome>`). Add the matching unit tests to §12.

- **API surface ergonomics / setup (P8 prover):** The proof mechanism as written may not expose the tools. MCP servers load when a Claude Code session starts. A subagent spawned from the lead (or implementer) session inherits that session's MCP set, so a `claude mcp add --scope local` run mid-session won't show up in it. `--scope local` also writes `~/.claude.json` for the current project path (the worktree), which the cleanup step has to undo exactly.
  - Evidence: plan lines 318–320 "registers it for the verification session only, with `claude mcp add --scope local …`, and removes it afterwards"; line 413 "A Claude Code subagent with `sindri mcp` registered for the session".
  - Fix: Name the mechanism. The prover is a fresh headless `claude -p` child process (run with `env -u CLAUDECODE` if nesting is refused), started with `--mcp-config <tmp>/sindri-mcp.json --strict-mcp-config` pointing at the worktree's built binary and the temp `AW_SINDRI_HOME`. Its `--output-format stream-json` transcript is the evidence. No `claude mcp add` is needed, nothing persists, and the cutover rule holds by construction. If the owner prefers `claude mcp add`, say the add happens *before* starting the prover session, and give the exact `claude mcp remove --scope local sindri` cleanup.

### MEDIUM

- **Backwards compatibility:** §3.2 and §10 describe S1 facts that are wrong. Implementers will search for code that isn't there.
  - `POST /profile/validate` isn't an S1 route. S1 runs `ValidateProfile` and `RunDoctor` in-process in the CLI ("must work while the control plane is down").
  - `GET /ping` (S1 `PingProcess`) exists but isn't in §3.1, so `ARCH-CQRS-001` ("every `MapGet` resolves an `IQuery`") fails on it unless `PingProcessRequest` is classed.
  - C1 is already fixed (`group.MapGet("/status", …)` serves `/sindri/status`).
  - §7 says clients check the epoch against `GET /status`. S1 checks `/sindri/ping`.
  - C2 says S1 "trusts the hash from any loopback caller". The S1 field is `Confirmed: bool`, and §3.2/§6 rename it to `confirmHash` while C2 says "S1's handlers don't change".
  - Evidence: plan lines 108, 127, 146–147, 285, 358–359. S1 `Host/Endpoints/EndpointTable.cs` (routes `/status`, `/ping`, `/stop`, `/profile/approve`, `/events` only), `Verbs.Validate`, `ApproveProfileRequest(ProfileHash? Hash, bool Confirmed)`.
  - Fix:
    - Drop `POST /profile/validate` (or mark it "in-process verb, no route, not a command").
    - Add `GET /ping` → `PingProcess` to §3.1.
    - Mark C1 resolved.
    - Change §7 to `/ping`.
    - Make C2 say explicitly that P1 replaces `Confirmed` with `ConfirmHash`, which is an S1 request-shape change, and list the touched files: `ApproveProfileRequest`, `Verbs.Approve`, `PublicAPI.Unshipped.txt`, aot-smoke `expected.txt`.

- **Backwards compatibility:** S1 contract changes are introduced silently.
  - §7 makes `sindri stop` "a no-op, exit 0" when not running. S1 documents and smoke-tests exit 1 ("0; 1 when it isn't running").
  - `CLAUDECODE=1 sindri stop` → `SND-AUTH-002` turns a working S1 verb into a refusal inside any agent session, including the owner's own `!sindri stop` typed in Claude Code.
  - `Sindri-Api-Version` and `apiVersion` change S1's status record.
  - Evidence: plan lines 293, 406. S1 `docs/csharp/sindri.md` line 22 (`sindri stop` exit 1). `src/Sindri/aot-smoke/expected.txt`.
  - Fix: Add these rows to §10 (C6 `stop` exit code, C7 session refusal of `stop`, C8 `GetStatusResponse.apiVersion`) with the doc and `expected.txt` updates they need. Add one owner-facing sentence to §6: "Inside a Claude Code session, `!sindri decide` is refused; run it in a plain terminal or the app."

- **API surface ergonomics:** Idempotency is underspecified for implementers.
  - "The same key with a different request body: `SND-API-005`" needs a stored body digest per key, but the spec doesn't say where (ledger row, `views.db`, or a key table) or for how long.
  - "A ULID the CLI and UI generate" and "prefixed ULIDs" for ids: there is no ULID package in `Directory.Packages.props`, and S1 generates ids with `GuidV7Ids`. The spec's own envelope sample shows `"id":"0192…"` (GUIDv7).
  - Evidence: plan lines 48–50, 151–153, 238. S1 `Infrastructure/GuidV7Ids.cs`. `Directory.Packages.props` has no ULID entry.
  - Fix: Say where the key → body-hash record lives and how long it's kept. Pick one id scheme: GUIDv7 through S1's `IIds` with a prefix, or a ULID helper in `Core` with no package. Write it into §3.2's id note.

- **API surface ergonomics:** Generic decorators over typed `IService<,>` aren't specified. `.agents/rules/csharp.md` requires decorators registered explicitly in the owning `Add…()` with a factory lambda (M.E.DI has no decorator support). `/new-slice`'s registration template wraps only the validation decorator. The spec doesn't say how each new command gets Authorizing → Idempotent → Auditing, or how a test proves every `ICommand` is wrapped. `ARCH-CQRS-001..003` check routes, constructor ports and registry rows, not wrapping. The endpoint-level 403 test catches the auth gap but not a missing idempotency or audit layer.
  - Evidence: plan lines 44–52, 88–94. `.agents/rules/csharp.md` lines 93, 127–129.
  - Fix: Specify one helper (`services.AddCommand<TCommand, TResult, THandler>()`) that registers the whole chain. Add `ARCH-CQRS-004`: resolving every `ICommand` service yields the `ValidatingService` → … → handler chain in order.

- **API surface ergonomics:** `--json` byte-equality needs a client change the spec doesn't list. S1's `ControlPlaneClient` deserializes into a typed record and `Output.Json` re-serializes it. "`sindri queue --json` byte-matches `curl …/sindri/queue`" (P3 signal) only holds if the CLI passes the raw body through. S1's client also maps every non-5xx problem to `ErrorKind.Invalid`, so the CLI loses the difference between 403 and 409 when picking exit codes.
  - Evidence: plan lines 351–352, 408. S1 `Host/Commands/ControlPlaneClient.cs` (`Send`, `Problem`).
  - Fix:
    - Add to P1's file list: `ControlPlaneClient` gets a raw-body path for `--json`, and sets `X-Sindri-Caller`, `Idempotency-Key` and `X-Sindri-After`.
    - Problem mapping keeps the HTTP status's `ErrorKind`.
    - Give a CLI exit-code table for 403/409/503.

- **Error messages & observability:** Observability of the new machinery isn't specified beyond the `command.handled` audit row. There are no log events or metrics for:
  - projector lag (cursor vs head);
  - rebuilds;
  - read-your-writes waits and timeouts;
  - SSE resets and open-stream count;
  - decorator refusals.

  `doctor` gains only "pending UI actions". The C# rules give every host `Aw.Kernel.Telemetry` with spans and metrics, so an implementer will either skip these or invent names per phase.
  - Evidence: absent. Searched the spec for `log`, `metric`, `span`, `Telemetry`, `doctor`, `lag`. The only hit is line 159 (doctor lists pending UI actions).
  - Fix: Add a short §9 table of telemetry names (e.g. `sindri.projector.lag`, `sindri.read_after.wait_ms`, `sindri.events.streams`, `sindri.events.reset`, `sindri.command.refused{class,caller}`) and a `doctor` `views` check (warn when lag > N or the schema is stale), each assigned to its phase.

- **Documentation completeness:** No phase says which docs it updates, and §12 lists no doc checks.
  - `docs/csharp/sindri.md` (binary table, files table with `views.db`, error table, hook contract) should change in P1–P8.
  - The AGENTS.md Commands block has no Sindri C# verbs or `sindri mcp`.
  - `planning/API_CONTRACT.md` is the repo's "Sindri CLI output contract" and isn't mentioned.
  - The `.agents/rules/mcp-servers.md` server list will be asked about once `sindri mcp` exists, even though it isn't registered before the switch.
  - The spec says "the UI design builds on §3 and §5" but doesn't say where the API reference lives for the UI's implementers (`GET /commands` is the only discoverable source).
  - Evidence: absent. Searched for `docs/csharp/sindri.md`, `API_CONTRACT`, `AGENTS.md`, `README`, `mcp-servers`. Only `docs/sindri/errors.md` (wrong file) and `docs/csharp/typed-ids.md` appear.
  - Fix: Add a "Docs" column to the Phases table. Minimum: P1 `docs/csharp/sindri.md` (caller classes, chain, codes) and the AGENTS.md command lines; P2 the files table (`views.db`, `sindri views rebuild`); P3/P4/P6/P7 the verb rows; P5 the event-stream contract (public envelope, `reset`); P8 a "`sindri mcp` (not registered until the switch)" note in `mcp-servers.md` and the proof recipe.

- **Ramp-up / parallel delivery:** The shared-file list misses files several phases will edit.
  - `src/Sindri/aot-smoke/run.sh` and `expected.txt`: §12 says "extended per phase with the new verbs", but they are a single golden file.
  - `Host/Commands/Usage.cs` (the usage text every verb adds to).
  - `Host/Commands/Verbs.cs`, where S1's client verbs live, with no anchor.
  - `tests/Aw.Sindri.Testing/A.cs` (the `/new-slice` builder anchor).
  - `rules/engineering/csharp/*.yaml` and `deps.yaml` (P8).
  - `Core/PublicAPI.Unshipped.txt` and the `Core` id/port files.
  - Evidence: plan lines 464–476 ("Every other file belongs to exactly one phase"); line 394. S1 `aot-smoke/expected.txt`, `Commands/Usage.cs`, `Commands/Verbs.cs`.
  - Fix: Add these to the anchored list with their anchors. For `expected.txt`, say each phase appends its journey as a new block at the end and the lead regenerates the file after a merge (as with `PublicAPI.Unshipped.txt`).

- **Setup / install:** The CLI session rule names `CLAUDECODE` and `AW_SINDRI_CHILD` but leaves the Codex and Cursor variables as "the Codex or Cursor session variables". `scripts/check-env-example.sh` (merge gate 4) fails on any newly read variable that isn't in `.env.example` or `scripts/env-scan-ignore.txt`. Only `CLAUDECODE` and `AW_SINDRI_CHILD` are already ignored.
  - Evidence: plan line 271. `scripts/env-scan-ignore.txt` lines 19, 34.
  - Fix: Name the exact Codex and Cursor variables in §6, and add "P1 adds them to `scripts/env-scan-ignore.txt` with a reason" to P1's files.

### LOW

- **API surface ergonomics:** `ValidateProfile` is classed `job` ("a query-like command") only to satisfy `ARCH-CQRS-001`, and it isn't on HTTP at all (see above). Classifying a non-route makes `GET /commands` list a POST route that 404s.
  - Evidence: plan lines 127, 146–147.
  - Fix: Leave it out of `CommandRegistry`, or list it with `route: null` / `cli-only`, and have the registry test allow that.

- **API surface ergonomics:** The `SND-ITEM-409` message is overloaded across three causes: stale version, illegal state, and decided elsewhere. Agents branching on the code alone (MCP tool errors) can't tell "re-read" from "never retry".
  - Evidence: plan line 337.
  - Fix: Keep the one code, but fix the three message prefixes in §9 (`stale:`, `state:`, `decided-elsewhere:`) and test them, or split `SND-DEC-409` out for decisions.

- **API surface ergonomics:** MCP tool naming mixes nouns and verbs (`sindri_pending_decisions` versus the `GET /decisions?pending=` route). `sindri_request` doesn't list its allowed `kind` values in the tool schema text, and an agent that guesses gets `SND-REQ-001`.
  - Evidence: plan lines 304–313.
  - Fix: Put the enum (`rescope | add-task | note | handback`) and the 500-character text cap in the tool's input schema and description.

- **Ramp-up:** Wave W1 stacks P2 on P1's in-flight branch. Neither side has a written rebase trigger beyond "at its next green point". The lead also "resolves conflicts in shared files" without a stated cadence.
  - Evidence: plan lines 438, 462.
  - Fix: One line: "the lead merges a parent's ready head into each child after every parent review round", so children don't drift.

## Tribal knowledge a new contributor (agent) would need, by name

1. S1 already uses `SND-API-001..003`. The next free API code is `SND-API-004`.
2. `docs/sindri/errors.md` is the generated TS catalogue. C# codes live in `docs/csharp/sindri.md`.
3. `/new-slice`'s anchors don't exist in Sindri's `EndpointTable.cs`/`CommandTable.cs`, and `--surface cli` is refused for Sindri (an http host).
4. `sindri` on `PATH` is the TS CLI. The C# binary is `dotnet run --project src/Sindri/Aw.Sindri.Host --` or the aot-smoke publish output.
5. `CLAUDECODE` is set inside every agent session, so the CLI acts as `session` and desk/decision verbs are refused. Human steps need `env -u CLAUDECODE` or `curl` without the header.
6. Approving a profile needs a TTY, so agents need a non-interactive way to get an approved profile into a temp home.
7. Each parallel implementer needs its own `AW_SINDRI_HOME` and `AW_SINDRI_PORT`, or they share the ledger and lease.
8. S1's process probe checks the epoch against `/sindri/ping`, not `/status`.
9. `ValidateProfile` and `doctor` run in-process in the CLI. There is no `/profile/validate` route.
10. MCP servers load at session start. The prover needs a fresh `claude -p --mcp-config …` process, not a mid-session `claude mcp add`.
11. Ids in S1 are GUIDv7 (`GuidV7Ids`), and the repo has no ULID package.
12. `ControlPlaneClient` re-serializes responses and collapses 4xx kinds to `Invalid`.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 9 |
| Setup / install (time-to-first-success: ~35 min as written, ~10 min with fixes) | HIGH | 4 |
| Error messages & observability | MEDIUM | 1 |
| Docs completeness | HIGH | 2 |
| Backwards compatibility | MEDIUM | 2 |
| Ramp-up cost (tribal-knowledge items: 12) | MEDIUM | 2 |

Time-to-first-success assumptions:
- An implementer subagent in a fresh worktree.
- Cold locked restore plus `dotnet build -warnaserror` of the solution, sharing the two heavy-job slots: ~5–8 min.
- The Sindri unit lane: ~1 min.
- A first `/new-slice` that fails `NS-EDIT`, then hand-writing the slice: ~10 min.
- Getting a running app as a human with an approved profile in a temp home: ~10–15 min of discovery.

With the anchors fixed and a written "prove as an agent" recipe, the last two drop to ~2–3 min.

## Verdict

The spec isn't blocked, because every gap has a cheap written fix. Six HIGH findings stop agents from building and proving phases as written: the `SND-API-003` collision, the wrong (generated TS) error doc, `/new-slice` anchors missing in S1, `CLAUDECODE` refusing every human signal step together with the TTY-only approval, a shared home/port/`PATH` for parallel implementers, and the audit-row ordering that P1's own signal depends on. A seventh, the unspecified prover mechanism, puts P8's proof at risk.
verdict: NEEDS_WORK
