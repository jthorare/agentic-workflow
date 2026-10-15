# Plan Devex Review — Sindri application design epic (PR #170)

**Plan:** `plans/2026-10-11-sindri-app-epic/plan.md` → `docs/sindri/README.md` (README), `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (API), `…-workflow-engine-design.md` (WF), `…-context-retrieval-design.md` (CTX), worktree `.claude/worktrees/sindri-epic` @ `5a4f05b0`
**Reviewed:** 2026-10-11

## Summary

The three specs agree on the big moves after X1–X18: one caller and action-class model, one needs-you list, one MCP endpoint, `$AW_SINDRI_HOME` and provider-neutral model calls. Developer-facing friction remains in four places.
1. **The stacked PRs #166–#169 contradict the specs.** They use different packages, routes, verbs, block contracts and doc file names. #166 also wires ConnectAI, which the owner declined. X17 calls the fix a "rebase", and nothing says which side wins.
2. **The build order is written three ways.** A CTX stage, an engine phase and the README each give a different prerequisite for "after the API chain".
3. **There are three proving and dev-loop mechanisms.** API's `sindri-prove.sh human`, WF's owner-run `script -q` runbook, and CTX's `expect` with fixed ports and `owner-check.sh`.
4. **Code and exit-code collisions.** API's new `SND-API-004/005` are already taken in S1's catalogue, and CTX's "not running" exit code differs from S1 and WF.

Multi-provider MCP registration (Codex, Cursor) is also undefined, though API P10 depends on it.

Time-to-first-success, for a new contributor bringing up the C# Sindri at a desk and running one engine or context verb: **~45 minutes**. That assumes the .NET 10 SDK is installed and S1 is unmerged. It covers working out which branch to stack on (S1, S2 or S3), which of three dev-loop recipes to follow, a first build plus NativeAOT publish (about 10 min), writing and approving a profile at a real terminal, and finding the right port.

## Findings by severity

### CRITICAL
None.

### HIGH

- **Backwards compatibility / migration (stacked PRs):** The already-built stages are not reconciled with the specs, and no document says which wins.
  - Evidence: README line 101 (X17): "Merge S1, then API P0–P2, then rebase S2 and S3 onto the chain."
  - The PR bodies show much more than a rebase:
    - **#167 (S2):** `IBlock`/`IGateBlock`/`BlockSpec` against WF §2's `IGate`/`IDescribedBlock`/`BlockDescriptor` (WF line 28, 58–84). Routes `/sindri/workflows…` and verbs `signal`, `park`, `unpark`, `evidence` against WF §6.3's `/workflow/instances/{a}` and `answer`/`command`/`complete`. Doc `docs/csharp/sindri-engine.md` against WF line 109 `docs/csharp/sindri-workflow.md`. Built-in recipes approved, against W8.
    - **#166 (S3):** code inside `src/Sindri`, against CTX D5 `Aw.Context` (CTX line 59). `POST /sindri/context/query`, against X9. A `connectai` source, against CTX D2 and the owner's decision. `LINEAR_API_KEY` env, against keychain pointers (CTX line 506).
    - **#169 (S4a):** `docs/csharp/sindri-blocks.md` and `POST /sindri/workflows/{instance}/evidence/collect` / `sindri workflow collect`. No spec owns these; WF §7.2 line 675 says Judge and Evidence become blocks "with the worker recipes".
  - Fix: add an "X19 — built stages vs specs" table to README. Give one row per PR (#166–#169) listing each divergence (package, routes, verbs, block types, doc file, source set, ConnectAI) and the verdict: spec wins, or PR wins and the spec is amended. Mark which spec phases each PR already covers (#167 ≈ WF P1–P5; #166 ≈ parts of CTX S-1A/S-7A). Then a lead knows which phases remain and what each rebase must change. Strip ConnectAI from #166 explicitly.

- **Ramp-up / build order:** Three different "wait for the API chain" rules.
  - Evidence:
    - README line 101: S2 and S3 rebase after **API P0–P2**.
    - API line 1121: engine **P2's contract unit** waits for **API P1's contract unit**.
    - WF line 673 and line 1146: "P2 starts once API P1 has **merged**".
    - WF line 1055/1073: P1 and P3 stack on S1 and don't wait.
    - CTX line 803 (S-1A): "S1 merged · S-1C · S-FX", with **no API dependency**. Yet CTX line 93 sends every route through the API chain, and line 804 (S-1M) creates `/sindri/mcp` and stamps `session`, which needs API P1's `CallerContext`.
  - Fix: write one dependency line per spec, phrased identically and mirrored in README "Build order":
    - WF P2 depends on API P1 merged;
    - CTX S-1A and S-1M depend on API P1 merged (chain, catalog, caller context);
    - X17's "P0–P2" is narrowed to the phase actually needed, or the reason for P2 is stated (`views.db`/`Head()`).
    - Update CTX's stage table and mermaid graph accordingly.

- **Ramp-up / build order (engine needs items):** Engine P4 starts instances on items, but no item exists before API P3, and API P3 isn't a dependency.
  - Evidence:
    - WF line 1187: `sindri workflow start workflow.step --item T-1 …`.
    - WF line 493: `<item>` is an `ItemId` (`ITM-…`) or a tracker-key alias.
    - API line 263: routes resolve linked keys, and an unknown one is `SND-ITEM-404`.
    - WF wave table line 1074: "P4 when P2 and P3 merged" (engine P2/P3), with no API P3.
  - Fix: add "API P3 merged" to WF P4's dependencies and wave table. Rewrite WF P4–P7 signals to create the item first (`sindri-prove.sh human item add "…"` → `ITM-…`), or define `T-n` as a dev-profile alias seeded by the dev profile.

- **Setup / dev loop (prover recipe):** Three incompatible ways to act as the human and run the app. X10 only half-merged them.
  - Evidence:
    - **API** (lines 876–899): `scripts/sindri-prove.sh human <verb>` under `script -q /dev/null`, `AW_SINDRI_PORT=0`, the agent stands in as `human:prover`.
    - **WF** (lines 506–513, 1101–1105): `alias sindri='scripts/sindri-prove.sh'`, but every `(owner)` signal step is still a numbered runbook the **owner** runs under `script -q <transcript>`, because "an agent is a `session` by design … can't run the (owner) steps". This contradicts API's `sindri-prove.sh human`.
    - **CTX** (lines 869–876, 895–899): approval "at a pseudo-terminal with `expect`", ports "from 7491 up" assigned by the lead, owner checks on fixed port 7480, `$SINDRI` from `aot-smoke/run.sh`, and a separate `scripts/owner-check.sh`.
    - WF's alias also assumes a pass-through mode that API §12 never defines (API defines only `human` and `curl` subcommands).
  - Fix: make API §12's `sindri-prove.sh` the one entry point and specify its full interface: pass-through `<verb>`, `human <verb>`, `curl …`, `serve`, `--port`, `--home`. In WF, change `(owner)` steps to `(prover)` steps run through `sindri-prove.sh human`, keeping owner runbooks only for real-terminal-only checks such as the challenge typing, if any remain. In CTX, keep owner checks only for work data and route the rest through the same script. Either fold `owner-check.sh` into `sindri-prove.sh owner-check <stage>` or state why it differs. Pick one port policy: `0` plus `control.json`, or fixed ranges.

- **Error messages & observability:** API's new error codes collide with S1's catalogue.
  - Evidence:
    - API line 39 says S1 fixes "`SND-API-001…003`", and §13 C-01 says new API codes start at 004.
    - API line 753 reserves `SND-API-004` (views lag, 503) and line 762 `SND-API-005` (stream limits, 429).
    - S1's contract on `origin/feat/sindri-s1-app-shell` (`docs/csharp/sindri.md`, error table) already defines `SND-API-004`, "The API answered an error without a catalogue code", and `SND-API-005`, "A handler failed with an unexpected exception". S1's exit-code table branches on both.
  - Fix: renumber API's codes to the next free numbers after re-reading S1's table (e.g. `SND-API-011`/`012`). Update API line 39's "fixed from S1" row to S1's current catalogue (`SND-API-001…005`, `SND-CLI-001…003`, `SND-PROFILE-001…006`). Add a unit test that fails on any duplicate code across `SindriErrors`, `WorkflowErrors` and the context catalogue.

- **API surface / setup (MCP per repo, multi-provider):** Registration is specified only for Claude Code, but the owner's decision and API P10 need Codex and Cursor sessions to reach `/sindri/mcp`.
  - Evidence:
    - API lines 706–709: `claude mcp add --transport http --scope local …`, then "Codex and Cursor get the equivalent entry".
    - CTX lines 95–98: the same sentence.
    - API line 1088 (P10): the launched session (Claude Code, Codex or Cursor) "pulls its context through the MCP endpoint".
    - Absent: searched API §8, CTX §3/§8.5 and README X8/X15/X18 for the Codex or Cursor config file, scope or header syntax.
  - Fix: specify per provider:
    - **Codex:** where a per-repo entry lives, given Codex's MCP config is user-level `~/.codex/config.toml`, and whether HTTP plus a custom header is supported.
    - **Cursor:** `.cursor/mcp.json` sits inside the work repo's tree, so say how it's kept out of git and out of CTX's "nothing lands in a git worktree" rule.
    - Say what `doctor context:mcp-scope` checks for each.
    - Make API P10's codex and cursor targets depend on it. If a provider can't do per-repo scope, say so and fall back (e.g. launch with an explicit `--mcp-config` per session, as the proof run does).

### MEDIUM

- **Error messages & observability (exit codes):** CTX gives "the process isn't running" a different exit code.
  - Evidence: CTX line 435–437: "**2** tool error (the process isn't running, …)". S1 (`status`, `stop`, `profile approve`), WF line 54 (W27: "1 when Sindri isn't running") and API §7 line 690 all use **1** with `SND-API-001`.
  - CTX also makes "no results" exit 1, while API line 505–506 makes an empty list exit 0 and reserves 1 for `rebuilding`/`stalled`.
  - Fix: align CTX §8.6 with S1: not running = 1. Then either keep "no results = 1" as a stated exception with its reason, or make it 0 with a `no results` line. Put one app-wide exit table in `docs/csharp/sindri.md`, including 75, and have all three specs point to it.

- **API surface (protected refusal codes):** CTX keeps its own authorization-style code next to API's.
  - Evidence: CTX line 452 `SND-CONTEXT-041` "Map review needs an interactive terminal", and line 1019 (import of `human` rows refused "`SND-CONTEXT-041` otherwise"). API §3.7 line 447 makes review-apply `protected`, so the chain already answers `SND-AUTH-002` before the handler. API line 768 says other specs shouldn't add their own authorization codes. 041 also sits in S-3's range (030–039, CTX line 950) though it's labelled S-4.
  - Fix: retire `SND-CONTEXT-041` the way WF retired `SND-RECIPE-004` and `SND-WF-007`, or limit it to the CLI-local interactive prompt (not the route), and fix the range.

- **API surface (CQRS carve-outs):** The engine's nonce and challenge issuers are POSTs that aren't commands.
  - Evidence: WF line 383: "A nonce from `POST /workflow/instances/{a}/nonce`"; WF §4.2 lines 258–259: the CLI "asks the server for a challenge"; WF §7.1 line 649: slices `IssueNonce` and `IssueChallenge`. API line 243 (`ARCH-CQRS-001`): every `MapPost` resolves an `ICommand`, and a command appends exactly one domain event (line 78). X9 carved out only query-by-POST for context.
  - Fix: list both routes in API §3 with their class (catalog rows, `pause`/`desk`-equivalent callers, no domain event), and add a named, tested `ARCH-CQRS-001` exemption next to X9's. Otherwise `ARCH-CQRS-001` fails on WF P2's first PR.

- **Ramp-up (needs-you circularity):** `--needs-me` can't show parks in the phase that builds it.
  - Evidence: WF line 559 (`--needs-me` reads `GET /needs-you`) is built in WF P4 (line 1086, unit e). API line 1090: parks become needs-you rows only in **API P11**, which depends on "P4 + engine P4".
  - Fix: say in WF P4 that `--needs-me` shows only API's decision, failure and profile rows until API P11, and move the parks part of WF P4's signal to API P11's. Or make P11 part of the WF P4 contract unit.

- **Ramp-up (scaffolding story):** The specs disagree on whether `/new-slice` works for Sindri.
  - Evidence: WF line 651: "`/new-slice` doesn't fit Sindri's host yet … The command classes, the endpoints and the group tables are written by hand". API P0 (line 1080) and §12 lines 861–874 add `--slice-only --package Sindri … --surface http` for exactly this.
  - Fix: update WF §7.1 to use API P0's `--slice-only` and the anchors from API C7, and add API P0 to WF P1's dependencies or note that P1 predates it.

- **Docs completeness (where contracts live):** X11 names one home, but the specs and PRs use six files.
  - Evidence: X11 (README line 95): "Contracts and codes in `docs/csharp/sindri*.md` (add `sindri-context.md`)".
    - WF line 109 uses `sindri-workflow.md`; #167 uses `sindri-engine.md`.
    - #169 uses `sindri-blocks.md`; #168 uses `sindri-ui.md`.
    - CTX line 87–89 and 989 still put the `$SINDRI` definition, MCP registration and an output example in `docs/sindri/context.md`. API line 877 says the prover recipe is deliberately not in user docs.
  - Fix: add a doc map to README: one row per file, what it holds, which phase writes it. Pick `sindri-workflow.md` or `sindri-engine.md`. Move `$SINDRI` and the dev loop out of `docs/sindri/context.md` into wherever `sindri-prove.sh` is documented.

- **API surface (`sindri mcp register` ownership):** The verb belongs to API P8, but the endpoint will probably first ship with CTX S-1M.
  - Evidence: API line 1087 (P8 adds `sindri mcp register [--apply]`, depends on P4 + P6). CTX line 804 (S-1M, wave 2) creates `/sindri/mcp`. CTX line 997 (Phase 1 signal) registers by hand with `--scope local`, and CTX line 98–100 describes the verb as if it exists.
  - Fix: give `sindri mcp register` to whichever of S-1M or P8 creates the endpoint (the same "whichever lands first" rule as the endpoint, API line 723). Otherwise make CTX's signal and docs say the command is printed by hand until P8.

- **API surface (repo scope for context results):** Nobody defines how `X-Sindri-Repo` scopes `context_*` results.
  - Evidence: API §3.7 line 444: context queries are "session callers repo-scoped, §3.1". CTX §8.8 line 496 maps `context.repos`, but Linear, Onyx and Slack records carry no repo, and absent: searched CTX §3, §5, §8 for `X-Sindri-Repo` or "repo scope".
  - Fix: in CTX §8.5, state the rule: graph nodes and code from `context.repos` filtered to the session's repo; intent records visible when linked to a node in that repo, or always for ring-1 work repos. Add the scope test to S-1M.

- **API surface (`item` shape vs X6):** CTX's `item` anchor needs a Sindri `ItemId`, but its signals pass bare Linear keys that may have no Sindri item.
  - Evidence: CTX line 352: `item` is "a Sindri `ItemId` or one of its tracker-key aliases (API §3)". CTX line 1057 and 1071 (Phase 5/6 signals): `--item ENG-482`. API line 263: an alias resolves only when linked to an item. Absent: who creates the link before intake exists.
  - Fix: let the `item` shape also accept a tracker key resolved through CTX's own alias table (`linear:issue/<uuid>`) when no Sindri item links it, and say so in §8.1. Or have the signal create the item first (API P3) with the link.

- **Docs completeness (UI design slot):** The reserved UI spec has a growing list of things it must settle, scattered over three specs, and API P10 can't start without it.
  - Evidence: README line 18 (slot), X2, X15 (README line 99), API line 1088 ("targets and their failure states from the UI design spec").
  - Absent: searched README for a consolidated list. The UI spec must settle:
    - launch targets, defaults, failure states and "attach" (API P10);
    - how the app gets a nonce for engine writes (WF §6.3 line 567);
    - fact and file inputs for `workflow start` (1 MiB content);
    - the copy-the-CLI UX for `protected` rows;
    - the version-mismatch screen (API line 774);
    - the stale-view rules (API line 235–237);
    - registering #168's `SND-UI-001..003` and `docs/csharp/sindri-ui.md`;
    - the `X-Sindri-Client: ui` label.
  - Fix: add a "UI design must settle" checklist under the slot row in README, and mark API P10 as depending on that spec's approval.

- **Ramp-up (engine side of C6 / S1-D7):** The two specs name different owners for the same S1 change.
  - Evidence: API line 803 (C6 in **P1**) and line 432 ("one change, made by whichever phase lands first"). WF line 662 and 1180 list S1-D7 under **P4**.
  - Fix: since API P1 now gates WF P2, mark S1-D7 "done by API P1 (C6)" in WF §7.2 and drop it from P4's unit list.

### LOW

- **Docs completeness:** README has stale lines.
  - Evidence: line 17 says "API P0–P10", but API has P11 (line 1090). Lines 39 and 66 say sessions are "Claude Code sessions" and `sindri open` "starts a Claude Code session", against X15 and X18.
  - Fix: P0–P11; "an agent session (Claude Code, Codex or Cursor)".

- **Docs completeness:** CTX's result example contradicts D16.
  - Evidence: CTX line 402 `"key": "linear:issue/ENG-482"`, against D16 (line 70) which keys on the UUID.
  - Fix: `linear:issue/<uuid>` with `ENG-482` as the display alias.

- **Docs completeness:** CTX's package tree still names the claude CLI.
  - Evidence: CTX line 114 "labeler (claude CLI)", against line 140–144 and X18.
  - Fix: "labeler (`ILabeler` over Judge's `IModelProvider`)".

- **Error messages:** WF never mentions exit 75.
  - Evidence: X12 adopts 75 app-wide; WF W27 (line 54) lists 0/1/2 only.
  - Fix: one sentence in W27 pointing at the app-wide table.

- **Setup:** `script -q /dev/null <cmd>` is BSD syntax. util-linux needs `script -qc '<cmd>' /dev/null`.
  - Evidence: API line 887.
  - Fix: have `sindri-prove.sh` branch on `uname`, with a bash test on both.

- **Setup:** The MCP registration bakes in the port, and `api.port: 0` breaks it.
  - Evidence: API line 707 `http://127.0.0.1:<port>/sindri/mcp`; S1 profile `api: { port: 7410 }` allows `0`.
  - Fix: `sindri mcp register` refuses (with a fix line) when the approved profile's port is 0, and `doctor` warns when a registered port differs from the profile's.

- **Docs completeness:** `.agents/rules/mcp-servers.md` opens with "All servers are registered globally with every installed provider by `setup.sh`". API line 904 adds `sindri` there in P8, though `sindri` is per-repo and never in `setup.sh`.
  - Fix: P8's doc edit adds a "Per-repo servers" section rather than a row in the global table.

- **API surface:** CTX's owner check runs `$SINDRI serve --headless` (line 906). S1 defines `--headless` on bare `sindri`, and `serve` is already headless.
  - Fix: `$SINDRI serve` (foreground) or `$SINDRI --headless`.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 7 |
| Setup / install (time-to-first-success: ~45 min) | HIGH | 3 |
| Error messages & observability | HIGH | 3 |
| Docs completeness | MEDIUM | 6 |
| Backwards compatibility | HIGH | 1 |
| Ramp-up cost (tribal-knowledge items: 10) | HIGH | 5 |

**Tribal-knowledge list** (unwritten facts a new contributor would have to be told):
1. Which wins where the stacked PRs #166–#169 and the specs disagree, and which spec phases those PRs already cover.
2. #166's ConnectAI source is against the owner's decision (Onyx CE is the intent engine) and must come out.
3. `sindri` on `PATH` is the TS build. Every C# command goes through `$SINDRI` or `sindri-prove.sh`, and which of the three recipes applies to which spec.
4. An agent is always a `session` caller. Whether a human step is done by `sindri-prove.sh human`, an `expect` pseudo-terminal, or an owner runbook depends on which spec you're in.
5. S1's catalogue already uses `SND-API-004/005`, so the API spec's code table can't be copied as written.
6. Port conventions: default 7410, prover `0`, CTX stage ports 7491 and up, owner checks 7480.
7. Engine verbs need an existing `ITM-…` item (API P3). `T-n` in examples is shorthand, not a real id.
8. Which `docs/csharp/sindri-*.md` file takes a new code or contract (`sindri-workflow.md` vs `sindri-engine.md`, `-blocks`, `-context`, `-ui`).
9. The heavy-job lock has two slots by default (`AW_HEAVY_SLOTS`), which caps builds per agent team.
10. The threat model guards against honest mistakes only, so forged-`human` and port-squatting findings are answered in writing, not fixed.

## Verdict

The design is coherent at the decision level. It still needs work before agents build from it: the stacked PRs #166–#169 diverge from the specs with no stated winner, the build-order and dependency statements contradict each other (including engine P4's missing API P3 dependency), there are three incompatible prover and dev-loop recipes, `SND-API-004/005` collide with S1, and Codex and Cursor MCP registration is undefined.
verdict: NEEDS_WORK
