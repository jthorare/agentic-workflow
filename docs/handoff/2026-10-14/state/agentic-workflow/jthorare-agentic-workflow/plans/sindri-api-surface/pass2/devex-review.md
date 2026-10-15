# Plan Devex Review — sindri-api-surface (pass 2, rev 2)

**Plan:** docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md
**Reviewed:** 2026-10-10
**Scope:** I checked that each pass-1 HIGH and MEDIUM is resolved, and looked for new issues in rev 2. Checked against the S1 worktree `.claude/worktrees/sindri-s1-app-shell` (a7d2ada, read-only) and `scripts/scaffold/new-slice.mjs`. I didn't re-raise owner decisions (CQRS, v0 without tokens, webhooks cut, MCP proven by a subagent).

## Summary

Rev 2 resolves most of pass 1, and I checked the fixes against S1:
- **Codes, docs and S1 facts:** new API codes start at 004, codes are documented in `docs/csharp/sindri.md`, `/ping` is used, `stop` exits 1 and `{hash, confirmed}` is kept.
- **Prover setup:** `AW_SINDRI_PORT=0` works in S1 (`LoopbackBinding`, and aot-smoke uses it). `$SINDRI` replaces the TS `sindri` on `PATH`, and each agent gets its own home.
- **Chain and catalog:** the audit step is outermost, `AddCommand` is pinned by a test, and idempotency is dropped.
- **The rest:** telemetry names and per-phase docs are added, and the prover recipe with `script` works (it gives a TTY on stdin and stdout on this Mac).

Two HIGH problems remain:
1. **C7 rests on a misreading of `/new-slice --files-only`.** The flag still edits every shared file at the generator's own regex anchors. It also skips the red, build and PublicAPI checks, and the script header says "never use it on a real package".
2. **P1's first signal can't pass as written.** The body is malformed (`"hash":"x"`, and no `Content-Type`), and `GET /ledger` only ships in P5.

Smaller new issues:
- the unit, fragment and anchor ownership rules contradict each other;
- `GET /ledger` is missing from the P2–P4 stack;
- the agent-variable list includes one Cursor sets for humans, and conflicts with the env-scan rules;
- `claude -p` has no tool permissions.

Time to first success is about 15–20 min. It was about 35 min in pass 1.

## Findings by severity

### CRITICAL

None.

### HIGH

- **Setup / scaffolding (pass-1 HIGH not resolved, now a new mechanism problem):** §12 says `/new-slice --files-only --surface http` "generates the request and response records, the service stub, the validator, the red test and the builder", and that the implementer adds the shared lines by hand. The generator doesn't work that way. `writeSlice` runs the same way with or without `--files-only`; the flag only skips the checks (`sliceApi`, `build`, `expectOnlyRed`, `formatClean`, `noStrayChanges`). So `--files-only` still makes these edits:
  - It inserts the registration before `        return services;` in `SindriFeaturesServiceCollectionExtensions.cs`, using its own template. That template is `AddScoped<IService<…>>(… new ValidatingService …)`, not `AddCommand`, so a command would get a second, unordered validator or skip the chain.
  - It inserts `Map<Slice>()` on the `_ = app.MapGroup("…")…;` chain in `EndpointTable.cs`.
  - It edits `SindriJsonContext` and `tests/Aw.Sindri.Testing/A.cs`.

  Either C7's new anchors match those regexes, and every unit edits the shared files directly (against "a unit never edits these files directly"). Or they don't, and every call stops with `NS-EDIT` and reverts all its files. Both ways it drops the guarantees the slice recipe relies on: no red check and no PublicAPI declaration. And the script's header says "--files-only … (the generator's own unit tests use it; never use it on a real package)".
  - Evidence: plan lines 207 (C7 "`/new-slice --files-only` is then used against them"), 686–693. `scripts/scaffold/new-slice.mjs` lines 11–12, 158–213 (`writeSlice`; all inserts happen without condition), 245–257. Template `slice-templates.mjs:63–70`.
  - Fix: Pick one approach and write it into C7 and §12.
    - (a) P1 adds a real mode to `new-slice.mjs`, for example `--no-shared-edits`. It writes only the slice's own files and prints the shared lines as a fragment. It keeps the red, build and PublicAPI checks, and it emits an `AddCommand`/`AddQuery` line for packages that declare a command chain. It needs a scaffold test in `scripts/tests/scaffold.test.sh`.
    - (b) Make C7's anchors exactly the generator's regexes (`_ = app.MapGroup("/sindri")` chain, `return services;`) and run plain `/new-slice` (no `--files-only`). Change the registration template for Sindri, and accept that units edit the shared files and git merges them.

    Either way, delete the claim that `--files-only` generates "only" the slice files.

- **API surface / P1 signal (new):** P1's first observable signal can't be produced, and it hides an audit gap.
  - `-d '{"hash":"x","confirmed":true}'`:
    - `ProfileHash` is a Vogen id that requires `^[0-9a-f]{64}$`.
    - `"x"` fails during minimal-API body binding, before any decorator runs. The result is a 400 (or `ValueObjectValidation`'s bare 400), not 403 `SND-AUTH-002`, and no `command.handled` row is written.
    - `curl -d` without `-H 'Content-Type: application/json'` sends a form content type, which minimal APIs answer with 415.
  - "`GET /ledger` shows only `command.handled`": `ReadLedger` is a P5 slice, and P5 isn't on P1's branch.
  - Underneath: §2.1 promises that `AuditingService` audits "every attempt: accepted, refused, invalid or failed". Body and route binding failures happen in the endpoint before the chain, so they are never audited. An implementer chasing this signal will be tempted to reorder authorization ahead of binding ad hoc.
  - Evidence: plan line 766 (P1 signal), lines 100–102 (chain), line 220 (`ReadLedger` is P5). S1 `Core/Ids/ProfileHash.cs` (the 64-hex `Validate`), `Host/Endpoints/EndpointTable.cs` (`ApproveProfileRequest request` bound before `service.Handle`), `Host/ValueObjectValidation.cs`.
  - Fix:
    - Rewrite the signal with a well-formed body: the real hash from `$SINDRI profile validate --json`, and `-H 'Content-Type: application/json'`.
    - Show the audit row without `GET /ledger`. Either move `ReadLedger` into P1 (it's a small read-only slice), or have P1's evidence read `ledger.db` with `sqlite3 -readonly`.
    - In §2.1, say binding failures aren't audited (a 400 before the chain), or audit them from an endpoint filter.
    - Add `-H 'Content-Type: application/json'` to every `curl -d` in the Phases table (P1, P4) and in §12's seeding step.

### MEDIUM

- **Ramp-up / parallel delivery (new):** The rules for shared files contradict each other, and the strict version stops a unit from reaching green.
  - §12 step 2: "The implementer adds the slice's lines at the anchors."
  - Conflict rules: "A unit never edits these files directly. It writes its lines in its own fragment, and the integrator merges the fragments."
  - Lead: "Merges the anchor lines in shared files."
  - Integrator: "Merges the anchor fragments."

  A slice's integration test (`WebApplicationFactory`) and its CLI verb can't pass without its `EndpointTable`, `AddCommand` and JSON-context lines. So "red to green" in the unit's worktree needs those edits. Also, the fragment format and location aren't defined anywhere.
  - Evidence: plan lines 688–692, 825, 827, 838–849.
  - Fix:
    - State one rule: the unit edits the shared files in its own worktree, one line per anchor, so it can go green.
    - The integrator merges them, and conflicts at the same anchor resolve by keeping both lines.
    - Drop the word "fragment", or define the format and path (for example `units/<slice>/anchors.txt`).
    - Take "merges anchor lines" out of the Lead's duties.

- **Setup / evidence (new):** §12's prover recipe makes the `GET /ledger` excerpt the evidence for every phase. But P2, P3 and P4 stack P1 → P2 → P3 → P4, and none of them includes P5, which is where `ReadLedger` lands.
  - Evidence: plan lines 710, 767–769, 795–805.
  - Fix: Move `ReadLedger` to P1 (this also fixes the P1 signal above), or allow `sqlite3 -readonly $AW_SINDRI_HOME/ledger.db` as evidence until P5.

- **Setup / env (new, from the pass-1 env-var finding):** §6's agent-session list has two problems.
  - It names `CURSOR_TRACE_ID`. To my knowledge, Cursor sets that in its integrated terminal, so the owner typing `sindri decide` in Cursor's terminal would be classed as `session` and refused.
  - `CODEX_SESSION_ID` isn't confirmed to exist.
  - "These go in `.env.example`" conflicts with `scripts/check-env-example.sh`:
    - Vendor-set names belong in `scripts/env-scan-ignore.txt`.
    - `CLAUDECODE` and `AW_SINDRI_CHILD` are already there.
    - A name in both files fails `ENV-EXAMPLE-003` (merge gate 4).
  - Evidence: plan lines 494–500. `scripts/check-env-example.sh` lines 18–25. `scripts/env-scan-ignore.txt` lines 19, 34.
  - Fix:
    - Check each variable against a real Codex and Cursor agent session.
    - Use only variables set for agent runs: keep `CODEX_SANDBOX*`, keep `CURSOR_AGENT` if it's confirmed, and drop `CURSOR_TRACE_ID`.
    - Add a test that the list doesn't include a variable set in a human's terminal.
    - Say the names go in `scripts/env-scan-ignore.txt` with a reason, not in `.env.example`.

- **Setup / P8 prover (follow-on to pass-1 HIGH 7, mostly resolved):** `claude -p` is non-interactive, so a tool that isn't pre-approved is denied rather than prompted. The command as written has no `--allowedTools` (or permission mode). So `sindri_*` calls and the signal's `curl` step (a Bash call) won't run. The child also loads the user's settings and hooks (`aw:*`, judge), which can change the run.
  - Evidence: plan lines 570, 772.
  - Fix:
    - Write the full command: `claude -p --mcp-config <tmp>/sindri-mcp.json --strict-mcp-config --allowedTools 'mcp__sindri__*' 'Bash(curl:*)' --output-format stream-json "<journey>"`.
    - Choose `--setting-sources` deliberately, and say whether the user's hooks are wanted in the proof.
    - Note that the server must already be running, started as the human per §12, because `sindri mcp` never starts it.

### LOW

- **API surface:** `AddCommand<TCommand, TResult, TService, TValidator>()` requires a validator. `StopProcess`, `RebuildViews`, `PauseService` and others have no body to check. Add an overload without a validator, or a shared `NoValidation<T>`. Evidence: plan line 93.
- **API surface:** `ValidatingService` (3) runs before `AuthorizingService` (4), so a session sending a malformed `decide` first gets 400 and only after fixing the body learns it's 403 `SND-AUTH-002` ("run it in a terminal"). Consider authorizing first. Authorization needs only the command type and the caller, not the body. Evidence: plan lines 99–102.
- **Setup / evidence:** Output captured through `script -q /dev/null` has CRLF line endings and a leading `^D` (seen on this Mac). Say to use `script` only for human steps, and never for `--json` byte comparisons, or to strip `\r`. Evidence: plan line 702.
- **Docs:** `planning/API_CONTRACT.md` (the repo's "Sindri CLI output contract") and `.agents/rules/mcp-servers.md` (P8's `sindri mcp`, not registered until the switch) are still not named in "Docs per phase". Evidence: plan lines 712–715.
- **Backwards compatibility:** C2 says "S1's handlers don't change". But S1's `StopProcessRequest` and `ApproveProfileRequest` must implement `ICommand<…>`, and `GetStatusRequest`/`PingProcessRequest` must implement `IQuery<…>`, so the records and their `PublicAPI.Unshipped.txt` entries change. Say so in C2 and C5. Evidence: plan lines 184, 202.

## Pass-1 HIGH and MEDIUM resolution check

| Pass-1 finding | Status |
|---|---|
| `SND-API-003` collision | Resolved (§9; 004–008) |
| Wrong error doc | Resolved (`docs/csharp/sindri.md`) |
| `/new-slice` anchors | **Not resolved.** C7 relies on `--files-only` behaviour that doesn't exist (HIGH above) |
| Agents can't run human steps, and TTY approval | Resolved. Recipe with `env -u … script` and `curl` seeding; checked `script` and `profile validate --json` against S1's aot-smoke |
| Shared home, port and `PATH` | Resolved. S1 honours `AW_SINDRI_PORT=0` |
| Audit ordering | Resolved. Binding failures are still unaudited (in the P1 HIGH above) |
| MCP prover mechanism | Resolved in principle. Permissions are missing (MEDIUM above) |
| Stale S1 facts | Resolved |
| Silent S1 contract changes | Resolved (`stop` exits 1; owner sentence in §4.2) |
| Idempotency | Resolved (dropped from v0) |
| Decorators over `IService` | Resolved (`AddCommand` and the order test) |
| Raw `--json` | Resolved (C5) |
| Observability | Resolved (§9 Telemetry, `views` doctor line) |
| Docs per phase | Mostly resolved (LOW above) |
| Shared-file list | Resolved. The rules now contradict each other (MEDIUM above) |
| Unnamed Codex and Cursor variables | Named, but the list and their placement are wrong (MEDIUM above) |

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 3 |
| Setup / install (time-to-first-success: ~15–20 min) | HIGH | 5 |
| Error messages & observability | None | 0 |
| Docs completeness | LOW | 1 |
| Backwards compatibility | LOW | 1 |
| Ramp-up cost (tribal-knowledge items: 4) | MEDIUM | 1 |

Time-to-first-success assumptions:
- A fresh implementer worktree.
- Cold locked restore plus build: ~5–8 min.
- The first `/new-slice --files-only` either fails (`NS-EDIT`) or writes the shared files, and the agent works out what happened and hand-writes the slice: ~5–10 min.
- The prover recipe (temp home, port 0, `script`, curl approval): ~2–3 min.

Tribal knowledge still unwritten:
1. `--files-only` still edits the shared files and skips the red and PublicAPI checks.
2. `ProfileHash` must be 64 hex digits, or binding returns 400 before the chain.
3. `curl -d` needs `Content-Type: application/json`.
4. `claude -p` needs `--allowedTools` for MCP and Bash calls.

## Verdict

Two HIGH findings remain. C7 and §12 depend on a `/new-slice --files-only` mode that edits shared files and skips the red and PublicAPI checks. P1's own signal can't pass: the hash is malformed, there's no `Content-Type`, and `GET /ledger` only ships in P5. Both have small written fixes. Everything else from pass 1 is resolved or down to MEDIUM or LOW.
verdict: NEEDS_WORK
