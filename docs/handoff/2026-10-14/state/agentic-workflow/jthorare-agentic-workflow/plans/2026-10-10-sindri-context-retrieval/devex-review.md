# Plan Devex Review — Sindri context retrieval engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (542 lines)
**Reviewed:** 2026-10-10

## Summary

This is a single spec document, not a `plan.md` / `engineering.md` / `TASKS.md` set. The API surface (§5.1, §8), the storage (§6.2) and the phases (§Phases) stand in for engineering.md and TASKS.md. Owner-settled decisions D1–D14, Onyx as the intent engine, MCP in Phase 1, and manual MCP registration before the switch are taken as given and not reopened. The spec is strong on the result contract (citations, versions, `why[]`, `degraded`) and on the test lanes. The developer-experience gaps are in four areas:

- **Running the pre-switch C# build.** `sindri` on PATH is the TS wrapper, and the spec doesn't say how to reach the C# binary.
- **State directory.** The spec puts `context.db` under the TS sindri's `$AW_STATE_DIR/sindri/`, which conflicts with S1's `sindri-app` home.
- **Error codes.** Only one `AW-CONTEXT` code is named. There is no catalogue or fix text, and the prefix disagrees with S1's `SND-*` ProblemDetails convention.
- **Setup.** There is no profile `context:` example, no Onyx or Linear setup runbook, and the HTTP routes put graph keys containing `/` and `#` into the path.

Time to first success, measured as the Phase 1 signal (a cited `context_query` answer in a Claude Code session): about 60 minutes as written, about 20 minutes once the setup gaps below are documented. Reaching Phase 5 (Onyx joined, company OAuth signed off) takes 2–4 hours plus Onyx's first indexing.

## Findings by severity

### CRITICAL
None.

### HIGH

- **Setup / install:** The spec doesn't say how the owner runs the pre-switch C# Sindri. Every phase signal is written as `sindri context …`.
  - Evidence: plan lines 77–78: "Before the switch … nothing in `setup.sh`, the providers or MCP registration points at the C# build". Lines 442–444 give the Phase 1 signal as `sindri context query "split pane orientation" --json`. `scripts/install-sindri.sh:2` installs the TS wrapper at `~/.local/bin/sindri`. On that PATH, `sindri context query` reaches the TS CLI and fails with `SND-CLI-001 Unknown command`. The spec is also absent on the binary path, `dotnet run`, starting the control plane and approving the profile (searched "dotnet run", "publish", "PATH", "serve", "approve").
  - Fix: add a "Running it before the switch" block to §8.5, or to the Phase 1 doc `docs/sindri/context.md`. It should give the exact invocation (for example `dotnet run --project src/Sindri/Aw.Sindri.Host -- context query …`, or the AOT-published binary under a distinct name or alias such as `sindri-next`) and the start sequence: `profile validate` → `profile approve <hash>` → `sindri --headless` → `context sources status`. It should also say what a Claude Code session sees when the control plane is down: the MCP server fails to connect, and the fix is to start `sindri`. Rewrite the phase signals with that invocation.

- **Setup / install (state layout):** The spec puts the engine's state in the TS sindri's directory. S1 keeps the C# app apart from it.
  - Evidence: plan line 76: "`$AW_STATE_DIR/sindri/context/` …". Line 207: "`$AW_STATE_DIR/sindri/context/context.db`". Line 335: "`$AW_STATE_DIR/sindri/context/eval/`". S1's `docs/csharp/sindri.md` (branch `feat/sindri-s1-app-shell`) says: "`AW_SINDRI_HOME`, default `$AW_STATE_DIR/sindri-app` … apart from the TS sindri's `$AW_STATE_DIR/sindri` until the switch".
  - Fix: root everything under `$AW_SINDRI_HOME/context/` (`context.db`, `eval/`, exports, probe results), passed in through the environment to `AddContextInfrastructure(environment)`. State the 0700/0600 modes there. Name `Aw.Context`'s own settings key for the directory, so the package stays Sindri-free (D5) and tests can point it at a temp dir.

- **Error messages & observability:** The error catalogue is missing, and the code prefix disagrees with S1's convention.
  - Evidence: plan lines 136–137: "`Error` records in `Result<T>` (`AW-CONTEXT-…` codes): `Unavailable` … `NotFound`, `Fatal`". Line 292 is the only concrete code (`"code": "AW-CONTEXT-010"`). S1 maps errors to "ProblemDetails with the `SND-*` code as `title`" and prints CLI errors as "`SND-<AREA>-NNN message`, with `(fix: …)`". The spec is absent on: the code list, fix text, HTTP status mapping, MCP `isError` mapping, CLI exit codes for `context` verbs, and the `scrub-blocked` code (line 234). Searched "AW-CONTEXT", "exit", "ProblemDetails", "isError".
  - Fix: add an §8.6 error table in the S1 `docs/csharp/sindri.md` form: code, when, fix, HTTP status, and whether it is fatal or a `degraded` entry. Cover the profile and validation errors (non-loopback Onyx URL, line 79), source unavailable, deadline missed, scrub blocked, node not found, a stale or unknown map version, an empty graph, and a missing secret pointer. Say plainly that `Aw.Context` owns `AW-CONTEXT-*` (like `AW-REDACT-001`) and that the Sindri Host passes those codes through unchanged as the ProblemDetails `title`, the CLI stderr prefix and the MCP error text. Give `context` verbs the S1 exit-code scheme: 0 ok, 1 answered but `degraded`, 2 tool error. Then `/specToProvenPR` implementers don't invent their own.

- **API surface ergonomics:** Graph keys in the HTTP path don't route.
  - Evidence: plan line 311 gives the routes `GET /sindri/context/node/{key}` and `GET /sindri/context/neighbors/{key}?depth=`. Keys at lines 168 and 174 include `surface:command/split-vertical` and `code:src/layout/SplitPane.ts#setOrientation`. A `/` splits the route segment, and a client never sends what follows `#` (the fragment).
  - Fix: take the key as a query parameter (`GET /sindri/context/node?key=…`, matching `path?from=&to=`), or as a POST body. State that keys are percent-encoded. Add an integration test that round-trips a key containing `/`, `#` and `:`.

- **Setup / install (profile):** The spec has no `context:` profile shape, and Phase 1 can't run without one.
  - Evidence: plan line 386: "Profile v1 accepting an optional `context:` section … `Aw.Context` owns its settings record". Keys appear piecemeal: `context.cascadeDepth` (191), `context.labeling.enabled` / `context.eval.adjudicator` (73–74), `context.labeling.maxTokens` (365), `context.scrub.extraPatterns` (450). The spec is absent on: the repo doc globs that `repo-docs` indexes (151), the Onyx URL (79), secret pointers (391), the Linear team filter (153), the backtest project ids, `minHits`, the fused-score floor, and the default query deadline and budget. Searched "context:", "globs", "deadline", "budget".
  - Fix: add a §3a with a complete annotated `context:` YAML example in the S1 profile style. Give defaults for every key and mark which phase adds each one. Include the minimal Phase 1 profile, which needs only repos and doc globs. Say which keys change the approval hash: all of them do, so every `extraPatterns` change needs a typed `profile approve`.

- **Setup / install (Onyx and Linear):** There is no runbook for Onyx or the Linear token.
  - Evidence: plan lines 70–71 (the owner does the OAuth at setup), 156–158 (Onyx scoping), 391 (secret pointers) and 396 ("Onyx running, scoped, with company OAuth signed off by the owner", phase 5). The spec is absent on: the Onyx version or commit (line 54 cites `search.py:81-93` with no pin), the install method (docker compose), resource needs, the loopback port, Onyx API key creation and its secret pointer, connector setup order, how long first indexing takes, the Linear API key type and least-privilege scope, and which pointer holds it. Searched "docker", "API key", "token", "version", "compose".
  - Fix: Phase 5 and Phase 6 should each list a setup doc (`docs/sindri/context-onyx.md`, a Linear section) as a deliverable. Each covers: pinned Onyx release, compose command bound to 127.0.0.1, API key → `keychain:` pointer, connectors, a verification step (`sindri context sources status` shows `onyx ok`), and teardown. For Linear: a read-only personal API key, where to create it, and its pointer. Pin the Onyx commit that the D3/D4 line references were read at, so the claims can be re-checked when Onyx moves.

### MEDIUM

- **API surface ergonomics:** MCP tool input schemas are undefined.
  - Evidence: plan line 313 names `context_query`, `context_node`, `context_neighbors` and `context_sources`. Their arguments are absent, apart from the CLI flags at line 312.
  - Fix: give each tool's arguments with types and defaults (`text?`, `node?`, `item?`, `symbol?`, `budget` default, `depth` default 2). State the one-of rule for the anchor inputs and the error returned when it is broken. Agents call these tools from descriptions alone, so write the tool descriptions too: one sentence each, plus when to use `context_query` over `context_node`.

- **API surface ergonomics:** The MCP result duplicates the JSON and text renderings in the session's context.
  - Evidence: plan line 313: "Tool results carry the same JSON plus a text rendering".
  - Fix: return the text rendering (with `[Cn]` markers) as `content` and the JSON as `structuredContent`. Then a session pays for one copy, and the packing budget (§8.3) is honest about what lands in context.

- **Error messages & observability:** The spec names no telemetry, and the hash-only query log can't be joined back to a bad answer.
  - Evidence: plan line 220: "`query_log` … query hash … timings. Never query or result text". Telemetry is absent: searched "ActivitySource", "Meter", "span", "AW_LOG_LEVEL", "Telemetry".
  - Fix: return a `queryId` in every result (and its `[Cn]` text footer) that keys the `query_log` row, so the owner can paste it into `sindri context explain <queryId>`, or at least find the row. Name the spans and metrics under `Aw.Context.*`: per-source search latency, deadline misses, degraded count, packing truncation, cache hit. Put per-source timings in the result's `degraded` and debug block. Note that logs go through `Aw.Kernel.Telemetry` and are scrubbed there.

- **Error messages & observability:** The defaults that decide `degraded` and thin results aren't given.
  - Evidence: plan line 142 ("each `Search` gets the query's remaining deadline") gives no default deadline. Line 270 ("fewer than `minHits` above a fused-score floor") gives no values.
  - Fix: state the default deadline (for example 1.5 s, inside the 2 s p95 bar at line 363), `minHits`, the score floor and the default `budget`. List them in the profile example (see the HIGH profile finding).

- **Docs completeness:** Only Phase 1 lists a doc deliverable.
  - Evidence: plan line 438: "`docs/sindri/context.md`". Phases 3–10 list no doc updates. AGENTS.md Commands and Directory Structure, `deps.yaml` notes, `.env.example` (merge gate 4, `scripts/check-env-example.sh`) and the error catalogue aren't mentioned. Searched "docs/", "AGENTS.md", ".env.example".
  - Fix: add a "Docs" line to every phase:
    - Phase 1: `docs/sindri/context.md` (contract, running before the switch, MCP registration) and the AGENTS.md Commands block.
    - Phase 2: `docs/csharp/redaction.md`, which is already listed.
    - Phase 3: map YAML format.
    - Phase 5: Onyx runbook.
    - Phase 6: Linear.
    - Phase 7: eval how-to, with the corpus and probe-file format.
    - Every phase: any new environment variable read by an `env:` pointer goes into `.env.example`.

- **Backwards compatibility / migration:** The map import file and the result JSON carry no version.
  - Evidence: plan line 459 ("`context map import <file>` (a YAML map the owner writes …)") and lines 286–296 (result JSON, no `schemaVersion`). The consumers are sessions now and the Phase 10 workflow block later.
  - Fix: give the map YAML a `schemaVersion: 1` and a JSON Schema under `schemas/`, validated with file, key path and fix in the error. Add `schemaVersion` to the query result, so the Phase 10 block and recorded fixtures fail loudly on a shape change.

- **Backwards compatibility / migration:** The spec doesn't say how this relates to the running TS `sindri scope` and `sindri index`.
  - Evidence: the spec calls the TS scope and index modules a reference (lines 10–11), and line 303 says the text output is "like today's scope maps". It doesn't say whether `sindri scope` stays the owner's tool until the switch, or which one to use for which question meanwhile. Searched "sindri scope", "deprecat", "switch".
  - Fix: add one paragraph. TS `sindri scope` and `index` keep running unchanged until the switch. The C# `context` verbs are reached only through the pre-switch invocation. At the switch, `context query` replaces `scope` for X, and nothing is migrated (clean cutover).

- **Setup / install:** The MCP registration hard-codes port 7410 at user scope.
  - Evidence: plan line 316–317: "`claude mcp add --transport http --scope user sindri http://127.0.0.1:7410/mcp`". S1 lets `api.port` and `AW_SINDRI_PORT` change the port.
  - Fix: say the URL must match the port shown by `sindri status`, and give the re-register command for a non-default port. Work context (ring 1) would then be exposed to every session, personal repos included. Note whether `--scope local` in the work repos is the intended registration. That is a doc choice inside D11, not a reopening of it.

- **Ramp-up cost:** The fixture app and the recorded fakes are undefined.
  - Evidence: plan line 63 ("A fixture app serves the test lanes"), lines 374–375 (fixture app, `FakeOnyxServer` "replaying recorded responses", "a recorded GraphQL fake") and line 379 ("Tests never touch work data or the network"). The spec doesn't say where the fixture app lives, what product it models, or how recordings are made without touching work data or the network.
  - Fix: name the fixture app: its path (for example `tests/fixtures/context-app/`), its features, surfaces and a known split-pane-style cascade, and the phase that builds it (Phase 1 for repo-docs, grown in Phase 4). State that Onyx and Linear recordings are made once against a throwaway Onyx and Linear workspace seeded with the fixture's tickets, scrubbed, and checked in with a re-record script.

### LOW

- **API surface ergonomics:** The `text` shape in Phase 1 is RRF over a single source (repo-docs), so `why[]` shows `rrf:repo-docs#n` only. Note that this is expected, so reviewers don't flag the single-source fusion as dead code.
  - Evidence: plan line 252 ("1 (sources only)") and line 265.
  - Fix: one sentence in Phase 1.

- **Docs completeness:** §14 "Review findings and answers" is a placeholder.
  - Evidence: plan line 422: "*Filled by the `/autoplan` pass below.*"
  - Fix: fill it from this review and the other lenses before `/specToProvenPR` starts.

- **Ramp-up cost:** The token estimate (characters / 4) is labeled as an estimate (line 282), but the owner can't see the real figure. Have `budget` report `{asked, used, estimator: "chars/4"}`, so a later tokenizer swap is visible in the output.
  - Evidence: plan line 281–282.
  - Fix: add the `estimator` field.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 4 |
| Setup / install (time-to-first-success: ~60 min as written, ~20 min documented; Phase 5 ~2–4 h + indexing) | HIGH | 5 |
| Error messages & observability | HIGH | 3 |
| Docs completeness | MEDIUM | 2 |
| Backwards compatibility | MEDIUM | 2 |
| Ramp-up cost (tribal-knowledge items: 10) | MEDIUM | 2 |

**Tribal-knowledge list:** facts a new implementer would have to be told.

1. How to run the C# `sindri` before the switch while `~/.local/bin/sindri` is the TS wrapper.
2. S1's `CommandTable`, `EndpointTable`, `ControlPlaneClient`, doctor and profile APIs exist only on `feat/sindri-s1-app-shell` (`docs/csharp/sindri.md` there), not on main.
3. S1's state home is `$AW_SINDRI_HOME` (`sindri-app`), not `$AW_STATE_DIR/sindri`.
4. The S1 error convention: `SND-*` as the ProblemDetails `title`, CLI `CODE message (fix: …)`, and exit codes 0/1/2.
5. ARCH-DIR-004 and the pending `compose` edge kind: whether the workflow-engine spec has already settled it.
6. Which Onyx release the D3/D4 source references describe.
7. Where the fixture app lives, and how the Onyx and Linear recordings were made.
8. The secret-pointer forms and where Onyx's and Linear's keys are stored.
9. The owner's three Linear project ids and where the probe-set file lives (outside repos).
10. graphify and its network sandbox, needed by the heavy lane (`sindri index setup` in TS today).

## Verdict

The verdict is NEEDS_WORK because of six HIGH findings: no stated way to run the pre-switch C# `sindri` (it collides with the TS wrapper), state placed in the TS sindri's directory, no `AW-CONTEXT` error catalogue and a code prefix at odds with S1, graph keys in HTTP path segments, no `context:` profile example, and no Onyx or Linear setup runbook. Each has a doc-level fix that doesn't reopen any owner decision.
verdict: NEEDS_WORK
