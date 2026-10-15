# Plan Devex Review — Sindri application design epic (PR #170), pass 2

**Plan:** `plans/2026-10-11-sindri-app-epic/plan.md`, which indexes:
- README: `docs/sindri/README.md`
- API: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md`
- WF: `…-workflow-engine-design.md`
- CTX: `…-context-retrieval-design.md`

All four are read from worktree `.claude/worktrees/sindri-epic` at `1c328de9`. S1's contract was read from `origin/feat/sindri-s1-app-shell:docs/csharp/sindri.md`.

**Reviewed:** 2026-10-11

**Pass:** 2. Pass 1 is in `pass1/devex-review.md` and `pass1/consolidated-review.md` (E1–E29). Its fixes are README X19–X34, landed in `c2b61533` and `1c328de9`.

## Summary

X19–X34 resolved most pass-1 devex findings at the decision level:
- the code collision is gone (`SND-API-011`/`-012`);
- the agent runtime is decided;
- Codex and Cursor get their MCP endpoint at launch time;
- one prover wrapper is named;
- engine P4 now waits for API P3;
- context exit codes match S1.

The edits also opened new gaps:
1. **The build-order table contradicts the specs.** The README table that "every spec cites" disagrees with the API's and context's own dependency columns. None of the three parallel-team graphs was updated to match it.
2. **`sindri-prove.sh human` doesn't work as written.** It is now the only way any spec acts as the owner, but under the specs' own rules it is classed `session` (parent-process check, the new `human` token). Its interface is also never defined. WF's dev-loop alias turns `sindri profile approve` into a session call.
3. **The new tokens have no owner.** The `human` token (X31) and the `step-report` run token (X21) have no phase, no way to be sent and no error code.
4. **CTX still contradicts X22.** It still gives Codex and Cursor an "equivalent entry" and checks only `~/.claude.json`.
5. **X17's per-PR change lists can't be carried out as written.** #167 is one 9.7k-line PR, but the engine ships one phase per PR across five waves.

Time-to-first-success: **about 40 minutes**. That covers bringing up the C# Sindri in a temp home and running one engine or context verb as the owner, assuming the .NET 10 SDK is installed. It includes the time to find out that the WF dev-loop alias runs `profile approve` as `session`, and that the CTX brief's fixed ports and `$SINDRI` disagree with `sindri-prove.sh`'s port 0.

## Pass-1 resolution

| Pass-1 devex finding | E-id / X | Status | Note |
|---|---|---|---|
| Stacked PRs vs specs, no winner | E3 / X17 | **partly** | Now says "the specs win", but the change lists are incomplete and don't say whether to split (HIGH-5 below) |
| Build order written three ways | E3 / X19 | **partly** | The README table exists and WF cites it. API and CTX don't cite it, it contradicts their dependency columns, and the team graphs are stale (HIGH-1, HIGH-2) |
| Engine P4 needs items | E4 / X23 | **resolved** in text (WF P4 depends on API P3; `T-n` convention, WF 1074–1075) | Only WF's wave graph lags (HIGH-2) |
| Three prover recipes | E5 / X24 | **partly** | One wrapper, but it can't be `human` as specified, the CTX brief still uses lead-assigned ports and its own `$SINDRI`, and the WF alias is wrong (HIGH-3) |
| `SND-API-004/005` collision | E1 / X20 | **resolved** | The S1 row is still stale and there's no duplicate-code test (LOW) |
| Codex/Cursor MCP registration | E6 / X22 | **partly** | API §8 is fixed. CTX §3 still contradicts it, and nothing owns the doctor check (HIGH-4) |
| CTX exit codes | E23 / X32 | **resolved** | Small remainder: refused input exits 1 vs S1's 2 (LOW) |
| `SND-CONTEXT-041` | E16 / X30 | **resolved** | The import text around it went stale (MEDIUM-2) |
| Nonce/challenge vs `ARCH-CQRS-001` | E8 / X26 | **resolved** | |
| `--needs-me` circularity | E27 / X33 | **not resolved** | No interim behaviour stated (MEDIUM-4) |
| `/new-slice` story | E27 / X33 | **resolved** (WF §7.1 line 669) | |
| Doc map | E28 | **not resolved** | MEDIUM-6 |
| `sindri mcp register` owner | E28 / X33 | **partly** | The rule is stated, but S-1M's stage scope omits the verb and P8 still lists it (HIGH-4) |
| Repo scope for context | E7 / X25 | **resolved** | The new `context.repoTeams` key isn't in the profile schema example (MEDIUM-5) |
| `item` shape vs X6 | E4 / X23 | **partly** | Linking lands in Phase 6, but Phase 5 signals use `--item ENG-482` (MEDIUM-3) |
| UI-spec "must settle" list | (devex) | **not resolved** | Grew with X21/X22/X31/X34 (MEDIUM-7) |
| S1-D7 owner | E27 / X33 | **partly** | WF §7.2 is fixed. API §3.5 line 439 and WF P4 lines 1108 and 1201 still disagree (LOW) |
| README stale lines | E29 | **not resolved** | LOW |
| CTX `linear:issue/ENG-482` example | E29 | **not resolved** | CTX line 414 (LOW) |
| CTX tree "labeler (claude CLI)" | E29 | **not resolved** | CTX line 124 (LOW) |
| WF W27 lacks 75 | X12 | **not resolved** | WF line 56 (LOW) |
| `script -q` BSD-only | E29 | **not resolved** | API line 929 (LOW) |
| Port 0 baked into registration | E29 | **not resolved** | LOW |
| `mcp-servers.md` "global only" | E29 | **not resolved** | API line 946 (LOW) |
| `serve --headless` typo | E29 | **not resolved** | CTX line 918 (LOW) |

Of 25 rows: 7 resolved, 8 partly resolved, 10 not resolved. Every row still marked "partly" or "not" is restated as a finding below.

## Findings by severity

### CRITICAL
None.

### HIGH

**HIGH-1. Ramp-up / build order: the "one order" table contradicts the specs' own dependency columns.**
- **Evidence:**
  - README line 79 says "It is the one order every spec's dependency column cites (X19)". The table is at lines 81–91.
  - **API P3 and P2 share wave 2** (README line 85), but API P3 depends on P2 (API line 1125, "P2"). The README's "Needs" column ("API P1") is wrong for P3.
  - **API P5** is in README wave 3. The API's own table puts it in W1 alongside P2 (API line 1164, "P2 ∥ P5 once P1's contract unit is committed").
  - **API P6** is in README wave 5 with "Needs: engine P4" (line 88). API P6 depends only on "P2 + P5" (API line 1128), and the API puts it in W2 (API line 1165). This needlessly puts context S-4 and S-5B (README wave 6, "needs API P6") behind the whole engine store phase. S-5B is on CTX's critical path (CTX line 831, "S-1A → S-3 → S-5B → S-6 → S-7B → S-8").
  - README wave 6 holds "the rest of its [context] stages", but CTX's table has S-3, S-5A and S-2W needing only S-1A (CTX lines 816–819) and S-3M needing only S-1M and S-3.
  - **API P10's needs are blank** in wave 7 (README line 90 gives P8's and engine P7's only). API's own column, "P3 + P8" (line 1130), omits two things it now needs:
    - the UI design spec (README line 93);
    - engine P5. P10 now "takes it over (`pause`) in the same command", and takeover ships in engine P5 (WF line 1220).
  - **The gate is stated two ways.** API line 1163: the engine's P2 *contract unit* waits for API P1's *contract unit*. WF line 1172 and README line 85: engine P2 waits for API P1 *merged*.
- **Fix:**
  - Make README's "Needs" column per phase, not per wave row, and derive it from each spec's dependency column.
  - Put API P5 with P2, and P6 with P3 (needs P2 + P5).
  - List the context stages individually: S-3, S-5A and S-2W after S-1A; S-4 and S-5B after API P6.
  - Give P10 "API P3 + P8, engine P5, UI design spec approved", and add the same to API's P10 dependency cell.
  - Pick one gate (contract unit or merged) and write it in the API Gate row, the WF P2 line and README wave 2.
  - Add "The order is `docs/sindri/README.md` 'Build order'" to the API "Phases" intro (line 1113) and to CTX "Stages and their dependencies" (line 808). Only WF cites it today (WF line 1073).

**HIGH-2. Ramp-up / parallel-team graphs: none of the three agent-team graphs was updated, yet each tells `/specToProvenPR` to work from it.**
- **Evidence:**
  - **WF.** WF line 1084 says the team works "from this graph".
    - The graph (lines 1086–1090) and wave table (lines 1092–1098) have no P8 and no API nodes.
    - Wave 2 says "P2: also S1 merged", with no API P1. Wave 3 says "P4: P2 and P3 merged", with no API P3. Both contradict the WF phase text (lines 1172 and 1206) and README.
    - API line 1163 requires "Both wave tables must show this gate", and WF's table doesn't.
  - **API.**
    - The graph (lines 1153–1159) and waves (lines 1161–1167) have no P11, no engine P4 edge into P11, and no UI-spec gate before P10.
    - The phases intro still says "P6, P8 and P10 follow" (line 1115).
    - "What each consumer needs" omits that the engine needs P3 (items) since X23.
  - **CTX.**
    - The mermaid graph (CTX lines 833–858) has no API P1 → S-1A or API P6 → S-4/S-5B edges, though the stage table now has them (lines 816, 821–822).
    - The table's "Wave" column (S-4 and S-5B in wave 3) still assumes they start right after S-3.
  - **Four "wave" numbering schemes now coexist:** README 0–8, API W0–W4 plus "Gate", WF 1–5, CTX 0–6. A lead reading "wave 3" can't tell which is meant.
- **Fix:**
  - Redraw each graph with the cross-spec edges as external nodes (e.g. `API P1 ⇢ P2`).
  - Add WF P8 and API P11.
  - Rename each spec's local waves to something distinct, such as "lane steps" or "W-API-n", or drop them and point to the README waves.
  - State in README that README waves are epic-level and the specs' tables are intra-spec.

**HIGH-3. Setup / dev loop: `sindri-prove.sh human`, now the only way to act as the owner in all three specs (X24), can't be `human` under the specs' own rules, and its interface is undefined.**
- **Evidence:**
  - **The parent-process check.**
    - API §6 line 681: "a parent process that is an agent harness also counts" (as `session`).
    - WF §6.2 line 524 (the `human` + terminal row): "no agent-harness parent process".
    - `sindri-prove.sh human` runs inside Claude Code. API line 929 only unsets the agent variables under `script -q /dev/null`, so the ancestor check still finds the harness and classes the call `session`.
  - **The human token.**
    - API §6 lines 696–699: once the token lands, "a `human` header without it counts as `session`".
    - API §12 lines 929–933 define `human` and `curl` without reading `auth/human.token` from the guarded temp home.
    - Every owner step in WF and CTX would then be refused with `SND-AUTH-002`.
  - **The prover label.** "The ledger records these as `human:prover`" (line 935), but only the `curl` mode sends `X-Sindri-Client: prover`. The CLI `human <verb>` mode sends `cli` (API §6 table).
  - **The WF dev loop.**
    - WF lines 506–513: `alias sindri='scripts/sindri-prove.sh'` and then `sindri profile approve <hash>` and `sindri serve`.
    - API line 919 defines `sindri-prove.sh <verb>` as the agent (`session`), so `profile approve` is refused.
    - `serve` is not defined as a pass-through.
  - **The CTX brief.**
    - CTX line 882 sets `AW_SINDRI_PORT=<port>` from 7491 up, and line 886 builds `$SINDRI` with `aot-smoke/run.sh`.
    - API line 922 has the wrapper force `AW_SINDRI_PORT=0` and set its own `SINDRI`.
    - Nothing says whether caller values win.
  - **Challenge input.** WF signals type challenges ("with its challenge typed", WF line 1281; "asks for `approve workflow.step 1`", line 1170) but never show how the prover feeds the text. API's seeding note (line 938) claims `printf '<first 6>'` "works whichever confirmation S1 or the engine lands". The engine's challenge (WF §4.2 line 264) is action text, not the first 6 hash characters.
- **Fix:** specify `sindri-prove.sh` in API §12 as a CLI contract.
  - **Modes.** `<verb>` (session), `human <verb>`, `curl <method> <path> [json]`, `serve` (foreground, owner), and `--port`/`--bin` overrides that CTX's brief can use.
  - **Prover identity.** In `human` mode, set a prover marker that the CLI honours only when `AW_SINDRI_HOME` is under `$TMPDIR` or the proof folder. The marker skips the parent-process check and sends `X-Sindri-Client: prover`.
  - **Token.** Read `$AW_SINDRI_HOME/auth/human.token` in both `human` and `curl` once it exists.
  - **Challenge input.** Take the challenge text on stdin (`printf 'approve workflow.step 1\n' | sindri-prove.sh human recipe approve …`). Define profile-approve's challenge text once S1-D5 lands, and correct the seeding claim.
  - **WF dev loop.** Change it to `alias sindri='scripts/sindri-prove.sh human'` for the owner terminal, or show both `sindri-prove.sh human profile approve` and `sindri-prove.sh serve`.
  - Add a bash test (`scripts/tests/sindri-prove.test.sh`) to API P1's files.

**HIGH-4. API surface / ownership: the tokens X21 and X31 introduced have no phase, no transport and no code.**
- **Evidence:**
  - **The `human` token is pulled forward without a phase.** API §6 line 697 says "Before the app's first command (its first write phase in the UI design spec), `serve` mints `auth/human.token`". No API phase lists minting, checking, or the CLI or app sending `X-Sindri-Token`. P1–P11 (lines 1122–1132) don't mention it, and the UI spec doesn't exist yet. The spec contradicts itself in three places:
    - D4 (line 55) says "v0 is local-only, with no tokens";
    - §6's opener (line 657) says "There are **no tokens**";
    - `SND-AUTH-001` (missing or stale token) is still "reserved for the token phase (§11)" (line 807).
  - **The `step-report` token is ambiguous.** WF §6.5 line 650: the session reports with `sindri workflow complete --run <token>`, "proven by a run-scoped token `ILauncher` hands the session". WF §5.4 line 383 defines the run token as `run:{entry}.{n}` (e.g. `--run 1.1`), which `status` prints and `GET /workflow/instances/{a}` returns (WF lines 557–559). Any session that can read the instance can therefore present it, but the WF P8 signal (line 1291) expects "The same `complete` from another session exits 2 with `SND-AUTH-002`".
  - **The handoff is unnamed.** How `ILauncher` hands the secret to the session (environment variable, file, header) isn't said. A new variable must go in `.env.example` or `scripts/env-scan-ignore.txt` (merge gate step 4, `check-env-example.sh`).
  - **`ILauncher` takes the wrong inputs.** API P10 (line 1130) says `OpenItem` passes "only the item id and the repo path". WF §6.5 (line 647) calls the same `ILauncher` "with the run token, the target from the profile, the repo and the Step's injection plan". Whichever lands second has to change the port's signature.
- **Fix:**
  - Give the `human` token an explicit API phase. Make it a "P1b" before the first UI write, or fold it into P3, the first phase with a UI-writable command. Also:
    - list `auth/human.token`, `X-Sindri-Token`, `SND-AUTH-001` and the prover-wrapper change (HIGH-3);
    - reword D4 and §6's opener.
  - In WF §6.5 and API §4.1, name the step-report credential separately from the run id, e.g. a `--report-token`, or an `AW_SINDRI_RUN_TOKEN` variable that `ILauncher` sets in the launched session's environment. Say it is never returned by queries, and add the variable to `env-scan-ignore.txt`.
  - Define `ILauncher.Launch(LaunchRequest { item, repo, target, mode, run?, injectionPlan?, reportToken? })` once in API P10, so engine P8 only fills fields.

**HIGH-5. API surface / MCP: CTX still contradicts X22, and nothing owns the three-provider doctor check or the `register` verb.**
- **Evidence:**
  - CTX §3 line 101 still says "(Codex and Cursor get the equivalent entry)". Lines 102–103 still say doctor `context:mcp-scope` "warns when a user-scope `sindri` server exists in `~/.claude.json`".
  - API §8 lines 743–748 (X22): "Codex and Cursor are never registered … `sindri doctor` warns on any user-wide `sindri` entry in `~/.claude.json`, `~/.codex/config.toml` or `~/.cursor/mcp.json`". No phase in either spec lists that three-file doctor check.
  - `sindri mcp register` "ships with whichever creates the endpoint" (API line 764). Under README waves, S-1M (wave 3) always lands before API P8 (wave 7). Even so:
    - S-1M's scope (CTX line 817) and Phase 1's files (CTX lines 995–1003) list only "per-repo registration docs";
    - API P8 (line 1129) still lists `sindri mcp register [--apply]` and "created here, or by context S-1M";
    - P8 still owns "checks whether that local scope reaches sessions in the repo's worktrees" (API line 740), though S-1M registers first and the CTX Phase 1 signal (line 1008) depends on it.
- **Fix:**
  - Rewrite CTX §3's registration bullet to match API §8 (Claude local scope only; Codex and Cursor at launch through `ILauncher`).
  - Rename the check `context:mcp-scope` → `mcp:sindri-scope`, give it the three files, and assign it to S-1M.
  - Move `sindri mcp register [--apply]` and the worktree-reach check into S-1M's stage row and Phase 1's files.
  - Change API P8 to "adds the `sindri_*` tools to S-1M's endpoint".

**HIGH-6. Backwards compatibility / stacked PRs: X17's change lists are incomplete and can't be carried out as written.**
- **Evidence:**
  - **X17 can't be followed for #167 or #166.** README line 117: "Merge in the build-order table's waves. Before review, each PR changes to its spec …".
    - #167 is one PR (+9,738 lines, `gh pr view 167`) spanning engine P1–P5+, which README spreads over waves 0–5. WF says "Each phase is one PR" (line 1073).
    - #166 (+3,941) spans CTX stages S-1A, S-3, S-5A and S-7A (waves 1–4).
    - Nothing says whether each PR is split into phase or stage PRs, retired and rebuilt, or reviewed as one.
  - **Divergences pass 1 listed that X17 still omits:**
    - #167's built-in recipes ship approved, against W8;
    - #166's `LINEAR_API_KEY` environment variable, against keychain pointers (CTX D-row on secret pointers);
    - #166 must also adopt API P1's chain, `context.sessions.enabled` and repo scope (X25), and the protected import and promote (X30);
    - #169's `docs/csharp/sindri-blocks.md` and its `sindri workflow collect` verb ("drops routes" covers routes only).
  - **#166 is already open and not draft**, though X17 says the changes come "before review".
  - **#169 has no target phase.** It is based on the S2 branch and turns Judge and Evidence into blocks. No WF or CTX phase owns those blocks (WF §7.2, "with the worker recipes"), yet engine P8 depends on Judge #156.
- **Fix:** replace X17's "Changes" cell with a per-PR table: PR → spec phases or stages it covers → split, rebuild or keep → each divergence and its fix → which README wave it merges in.
  - #167 → split into engine P1/P3 (wave 0), P2 (wave 2) and P4–P5 (waves 4–5), or close it and build fresh from the spec with #167 as reference.
  - #166 → set to draft; map it onto S-1A, S-3, S-5A and S-7A.
  - #169 → park it until a spec phase owns the Judge and Evidence blocks, and name that phase (e.g. an engine P9 or a blocks spec).

### MEDIUM

**MEDIUM-1. API surface: API P10's scope grew under X29, X22 and X21, but its files, signal and size didn't.**
- **Evidence:** API line 1130 now covers the takeover-on-open, `session.launched` and `session.ended`, `ItemView.session?`, a double-launch fact key, launch-time MCP for Codex and Cursor, and service to the agent runtime. Its files are still `ILauncher.cs`, `WarpLauncher.cs` and `Features/OpenItem/`; the signal still checks only "opens a Warp tab running Claude Code"; the size is still M.
- **Evidence:** `session.launched` and `session.ended` aren't in API §3.4's event table (lines 395–410), and WF P8 (line 1285) lists the same events as its own files. That gives two owners and no `EventCatalog` row.
- **Fix:**
  - Add the session events to §3.4 under P10, and remove them from WF P8's files ("uses P10's").
  - Extend P10's signal: opening an item with a live instance shows `taken-over`; a second `open` is refused; `--with codex` launches with `-c mcp_servers.sindri…` and the session's `sindri_item` works.
  - Re-size P10 to L or split it into P10a (Claude Code, events) and P10b (Codex and Cursor targets).

**MEDIUM-2. API surface: context's new events and classes break API's class definitions and X30.**
- **`job` can't write domain events.** API line 556 defines `job` as "Read-only work … writes only its own job events". CTX line 112 has `map build` (`job`) and `sources sync` (`job`) write `context.map.built` and `context.sources.synced`.
- **`item.linked` has no owner in the API.** CTX line 1076: "`item.linked` (service `context-sync`)" writes into API's `item.*` namespace. It has no catalog row, no action class and no API §3.4 entry, and API P3's item projection doesn't fold aliases.
- **An automatic path to `current` bypasses X30.** CTX line 489: "a build whose queue is empty from the start makes it `current`". That lets a `job` make content current, though X30 makes `promote` and `import` (making content current) `protected`.
- **Stale import text.** CTX lines 1031 and 780 still say "the HTTP route imports them as `model` provenance". Import is now `protected` (API line 450), so a non-terminal HTTP caller can't import at all.
- **Fix:**
  - Widen `job` to "its own job events and the domain events named in its catalog row", listing the context ones.
  - Register the context events and `item.linked` in API §3.4 as cross-spec rows, with the owning CTX stage as their phase.
  - Have an empty-queue build write a draft and a needs-you row ("promote?"), never `current`.
  - Delete the HTTP-import sentence.

**MEDIUM-3. Ramp-up: CTX Phase 5 signals resolve item aliases that exist only from Phase 6.**
- **Evidence:**
  - CTX line 364: the `item` anchor is "a Sindri `ItemId` or one of its tracker-key aliases (API §3)", delivered in S-5B (Phase 5).
  - The Phase 5 signal (line 1069) runs `--item ENG-482`.
  - Aliases come from `item.linked`, which Phase 6 (S-6, after S-5B) owns (line 1076).
  - The CLI synopsis still shows `--item ENG-1` (line 435).
- **Fix:** have Phase 5's signal create the item (`sindri-prove.sh human item add`) and pass `ITM-…`, and move the `ENG-482` alias case to Phase 6's signal. Alternatively, let S-5B resolve a bare tracker key through context's own alias table and say so in §8.1.

**MEDIUM-4. Ramp-up: `--needs-me` still has no defined behaviour between engine P4 and API P11 (E27 not applied).**
- **Evidence:**
  - WF §6.3 (line 557): `--needs-me` reads `GET /needs-you` and shows **Decisions** (parked, escalated, idle takeovers) and **Tasks**.
  - Park, escalation and desk-task rows only exist from API P11 (README wave 5; API line 1132). Engine P4 ships in wave 4.
- **Fix:** add one line to WF P4: "until API P11, `--needs-me` lists only API's decision, failure and profile rows; desk tasks show in `workflow list --state active`". Add a P11 signal step that checks `--needs-me` shows the park.

**MEDIUM-5. API surface / config: renamed and new keys and terms weren't applied everywhere.**
- **Evidence:**
  - API §8 line 759: "off while `context.mcp.enabled` is false". The key is now `context.sessions.enabled` (CTX line 516).
  - CTX line 461: `SND-CONTEXT-020` still reads "MCP is disabled", though it now covers every transport.
  - CTX line 110 introduces `context.repoTeams`, absent from the profile example and schema (CTX lines 507–520).
  - WF §6.5 line 650 calls `step-report` a "caller class". It is an action class (API §4.1 line 554); caller classes are `human`, `session` and `service`.
  - WF §6.2's action-class table (lines 533–539) doesn't list `step-report` beside `CompleteStep`.
- **Fix:**
  - Rename the key in API §8.
  - Reword `SND-CONTEXT-020` to "session reads are disabled".
  - Add `repoTeams: { work-app: [ENG] }` to the §8.8 example and to the profile schema change in S-1A or S-5B.
  - Fix the term in WF §6.5, and add `step-report` to WF §6.2.

**MEDIUM-6. Docs completeness: still no doc map (E28), and the prover recipe and `$SINDRI` have two homes.**
- **Evidence:**
  - API line 916: the recipe "is not in `docs/csharp/sindri.md`".
  - CTX line 91: "`docs/sindri/context.md` defines `SINDRI=<that command>`".
  - API line 922: `sindri-prove.sh` sets `SINDRI`.
  - API line 946: `.agents/rules/mcp-servers.md` gets `sindri` "in P8", though the endpoint now exists from S-1M (wave 3).
  - No index of `docs/csharp/sindri*.md` exists in README or `sindri.md`.
- **Fix:**
  - Add a README "Docs" table with one row each for `sindri.md`, `sindri-workflow.md`, `sindri-context.md`, `sindri-ui.md`, `docs/sindri/context.md` and the prover recipe, saying what each holds and which phase writes it.
  - Put the prover recipe in `docs/csharp/sindri-prove.md`, a contributor doc rather than a user doc, and point CTX's `$SINDRI` at it.
  - Move the `mcp-servers.md` edit to S-1M, as a "Per-repo servers" section.

**MEDIUM-7. Docs completeness: the reserved UI design slot has no "must settle" list, and X21, X22, X31 and X34 grew it.**
- **Evidence:** README line 18 (the slot) and line 93 ("before … API P10"). Absent: searched README, API and CTX for "must settle". Items scattered across the specs:
  - launch targets and failure states (API P10);
  - Cursor's launch-time MCP form ("recorded in P10", API line 747);
  - the app's first write phase, which gates the `human` token (API line 697);
  - the recipe `inputs` start form (WF line 136, X34);
  - in-app OS notifications until engine P6 (API line 597);
  - the handback offer (X29);
  - the copy-the-CLI rows for map `import`, `promote` and `review` (X30);
  - the nonce fetch for engine writes;
  - the version-mismatch screen;
  - #168's `SND-UI-*` codes and `sindri-ui.md`.
- **Fix:** add a checklist under the slot row in README, and make "UI design spec approved" a named dependency of API P10, the human-token phase (HIGH-4) and #168.

**MEDIUM-8. Error messages: a session's step report fails with no fix line once the run token is the proof.**
- **Evidence:** WF P8's signal (line 1291) expects `SND-AUTH-002` for "another session". `SND-AUTH-002`'s fix text is about terminals and agent variables (WF §4.2 lines 266–268), so a session holding a stale or wrong report token gets no actionable fix, and no `SND-WF-*` code distinguishes "not your run" from "stale run" (`SND-WF-005`).
- **Fix:** reuse `SND-WF-005` for "a report token that doesn't match the open run", or reserve a `SND-WF-011` for it, with fix text ("only the session launched for run N can report it; a human can `complete` it at the desk"). Keep `SND-AUTH-002` for the class refusal.

### LOW

- **Docs (README stale):** README line 17 still says "API P0–P10" (P11 exists), and line 19 says "S2 (P1–P7)" though engine P8 now exists. plan.md says the same. Lines 33 and 39 still say "Claude Code sessions". Line 97 says "X19–X33 come from the epic's `/autoplan`", but the table runs to X34; so do the status lines in API (line 10), WF (line 13), CTX (line 7) and the addendum. **Fix:** update the ranges and use "agent sessions (Claude Code, Codex or Cursor)".
- **Docs (README wave 0):** wave 0 omits S-2R and S-FX (CTX wave 0), and lists engine P3 beside P1 though P3 starts after P1's contract unit (WF line 1095). **Fix:** list them, and note "P3 after P1's contract unit".
- **Error codes:** the API "Fixed from S1" row (line 42) still says `SND-PROFILE-001…005` (S1 has 006) and `SND-CLI-001/003` (S1 has 001…003). No test checks for duplicate codes across `SindriErrors`, `WorkflowErrors` and the context catalogue. **Fix:** refresh the row, and add the uniqueness test to API P1.
- **Exit codes:** CTX line 446 sends a "refused input" to exit 1, while S1 sends bad input (`SND-CLI-003`) to exit 2. WF W27 (line 56) still omits 75. **Fix:** align CTX with S1, or state the exception; add 75 to W27.
- **S1-D7 leftovers:** API §3.5 line 439 still says "whichever phase lands first". WF P4's contract unit (line 1108) and files (line 1201) still list D7. **Fix:** "API P1 (C6)" in all three.
- **CTX examples (E29):** `"key": "linear:issue/ENG-482"` (line 414) and "labeler (claude CLI)" (line 124). **Fix:** `<uuid>`, and "`ILabeler` over `IModelPort`".
- **Setup (E29):** `script -q /dev/null <cmd>` is BSD-only (API line 929). **Fix:** have the wrapper branch on `uname`, with a test.
- **Setup (E29):** the registration bakes in the port, and `api.port: 0` breaks it. **Fix:** `register` refuses when the approved port is 0, and doctor warns when a registered port differs from the profile's.
- **Setup (E29):** the CTX owner check's `$SINDRI serve --headless` and "approve at a terminal" (line 918). **Fix:** `$SINDRI serve`; `sindri-prove.sh`-style approval is the owner's own terminal here, so say "at your terminal".

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 6 (HIGH-4, HIGH-5, MEDIUM-1, MEDIUM-2, MEDIUM-5, MEDIUM-8) |
| Setup / install (time-to-first-success: ~40 min) | HIGH | 4 (HIGH-3, plus 3 LOW) |
| Error messages & observability | MEDIUM | 3 (MEDIUM-8, 2 LOW) |
| Docs completeness | MEDIUM | 5 (MEDIUM-6, MEDIUM-7, 3 LOW) |
| Backwards compatibility | HIGH | 1 (HIGH-6) |
| Ramp-up cost (tribal-knowledge items: 10) | HIGH | 5 (HIGH-1, HIGH-2, MEDIUM-3, MEDIUM-4, 1 LOW) |

**Tribal-knowledge list** (facts a new contributor would have to be told):
1. README "Build order" waves override the specs' own wave tables, and the README's "Needs" column is per row, not per phase. API P6 doesn't actually need engine P4.
2. "Wave n" means four different things, depending on which of README, API, WF or CTX you're reading.
3. `sindri-prove.sh human` is classed `session` by the parent-process rule, unless the wrapper is given an exemption the spec doesn't define.
4. In WF's dev loop, the `sindri` alias is the session side, so owner verbs need `sindri human …`.
5. The run token printed by `status` (`--run 1.1`) is not the agent runtime's report credential, and the real credential's handoff is undefined.
6. The `human` token is coming "before the app's first write", but no phase ships it. Once it lands, every prover call needs it.
7. CTX §3's Codex and Cursor "equivalent entry" is overruled by X22: never register them.
8. `sindri mcp register` belongs to S-1M (it lands first), even though API P8 lists it.
9. Engine examples' `T-n` stands for an `ITM-…` from `sindri item add`. Linear-key aliases only resolve after context Phase 6.
10. #167 and #166 can't merge as-is. Whether to split or rebuild them is the lead's call until X17 says.

## Verdict

The pass-1 decisions landed, but HIGH-1 to HIGH-6 remain:
- HIGH-1 and HIGH-2: the "one" build order contradicts the specs' dependency columns, and all three team graphs are stale;
- HIGH-3: the one prover wrapper can't act as `human` under the specs' own rules;
- HIGH-4: the new `human` and `step-report` tokens have no phase or transport;
- HIGH-5: CTX still contradicts X22;
- HIGH-6: the stacked-PR change lists can't be carried out.

verdict: NEEDS_WORK
