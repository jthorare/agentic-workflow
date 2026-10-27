# The cutover: the install moves from TypeScript to C# in one switch (spec)

**Status:** draft for owner review, rev 12 (2026-10-23). Docs only. The code lands in the phases of §12.

The Rev 2 to Rev 4 paragraphs record what each earlier round changed; where rev 5 or later reversed one of them (the hook unmerge, the Q2 cut, "P0 asks only Q1, Q2, Q6, Q7"), the later text wins.

**Rev 2 (2026-10-23), /autoplan round 1.** Fixed: the install and the removal script now run in one safe order
(publish, then clean up and delete the TS judge state, then link; §2, §7.1, §8), so no window has the C# `judge` reading the
TS database or `setup.sh` stopping half switched; the v3 hook bakes the stable `~/.local/bin/sindri`, not a stamped
directory (§6.2); the TS repo list is read from `$AW_STATE_DIR/profile/repos/*.yaml`, not a path that does not exist (§6.2, §8);
the C# `scrub --staged` also serves v2 and v1 hooks unchanged, so a missed repo costs one stderr line, not a refused commit
(§6.2); §9 no longer says the PR comes after the flip; the switch edits to `setup.sh` land in P3, not P1; the post-switch
profile and onboarding step and the printed record of what was deleted are specified (§8, P4); installed `prompt-sort` and
`trait-check` registrations are removed (§7.4); `reflect` and `correct` go on `DEPRECATED_SKILLS` (§9, §15); the statusline keeps
its judge segment without the scorer (§10); `ui-evidence` is granted per verb, not as a whole (§3.1); script exit codes and
flags are defined (§7.1, §8.1); more consumers of `sindri-app` and the TS packages are listed (§9).

**Rev 3 (2026-10-23), /autoplan round 2.** Fixed: `setup.sh` runs the removal script on every run, so each delete is now gated
on the TS shape and the TS-only path, and a second run on a flipped box deletes nothing (§8, P2 proof: the C# state survives a
second `./setup.sh`); the judge-state delete sits next to the link and is re-checked after it, with `judge health` as the
last post-link step (§8); step 1 also refuses while a C# `sindri-app` serve runs from the build directory step 7 removes;
the repo list, the judge config as `judge config set` lines and the profile text are logged when read, before any delete
(§8); step 2 runs the stamped `sindri` with `DOTNET_ROOT` and bakes the stable path explicitly; the live-pane removal is
inlined, since `install-live-pane.sh` is deleted by the same PR; the install sequence runs after `install-deps.sh`
(§7.2); `bin/cs/current` moves at link time only (§7.1); profile links are unlinked, never followed (§8 step 8); the hook
unmerge matches only `aw:` entries (§5.2); next steps are printed again at the end of `setup.sh` (§8 step 10); P0 asks
only Q1, Q2, Q6, Q7 before P1 (§12).

**Rev 4 (2026-10-23), /autoplan round 3.** Fixed: the two database tests that gate step 1 and step 5 no longer need a `sqlite3`
CLI the toolkit does not install: the TS judge database is recognised by the stamped C# `judge health` refusing it with
AW-DATA-009 (a read-only check), and the TS ledger by a file-holder check (§8); the post-link gate is its own step with one
exit code (§8, §8.1); `judge/config.json` goes before the database so a stop between the two cannot strand it; step 9's
`aw_remove_retired_dir` is limited to ignored build output in the checkout; `sindri scrub --install-pre-commit` takes
`--bin` and the cleanup script sets `DOTNET_ROOT` itself (§6.2, §8); the log path is printed on every exit and a re-run
reprints an unfinished log's next steps (§8, §8.1); unit 3.1's installer and statusline edits and unit 4.2's `--with-sindri`
work move to the switch PR, and Q3 joins the questions asked before P1 (§12); the unit 1.5 grant text matches §3.1;
the bug-fix skill's `locks.sh` path follows `config/lib/locks.sh`; both rewritten skills open with a one-line "run
`./setup.sh`" preflight; the `better-sqlite3` build-tools check in `setup.sh` is replaced (§3.5, §4.3, §7.2).

**Rev 5 (2026-10-23): owner answers.** The owner answered §17 (Decided: Q1, port the Evidence script features; Q2, **port all
of the Judge**, so nothing of it is cut (§5.2 maps every TS judge verb to an existing or a new C# verb; the new units 6.0 to
6.5 join P0, P1 and §13; the unmerge of the `prompt-sort` and `trait-check` registrations, the deleted hooks and the `/judge`
skill cuts are gone); Q6, **Sindri owns the `ui-evidence` binary**, not the skill (§3.1: built, published, installed and
versioned in Sindri's install next to `sindri`, with Sindri's stamp; the skill calls Sindri's binary; a `sindri evidence`
verb is the rejected reading); Q4, Q5 and Q8 as recommended. Q3 and Q7 were not answered separately and kept their
Recommended answers as decisions (the owner confirmed both in rev 9). §17 is now "Owner answers (2026-10-23)", with one new open
question (Q9, answered in rev 9).

**Rev 6 (2026-10-23), /autoplan round 1 on the owner answers.** Fixed: `AW_UI_EVIDENCE_BIN` was described as already resolved by BugFix's adapter, but the variable is new (no code or `.env.example` names it), so the text now says who reads it and that `.env.example` lists it (§3.1, §9); `ui-evidence` has no `help` or `version` verb, so unit 1.4 adds `version` and the doctor line uses it (§3.1, §12); the skill preflights now stop (`exit 1`); the sorter's `<sid>.sort.json` is read and rewritten by `done-gate.sh`, so its shape is a frozen hook contract with a third real-hook case (§5.2, §10, unit 6.1, 6.5); the removal script logs the owner's `prompt-sort scaffold <id> on` choices and the `promptSort` values before it deletes `config.json` (§8 step 2); unit 6.0 owns both migrations with final schemas, the public API stubs, the command routes, the JSON context and the `deps.yaml` entries, so 6.1 to 6.5 have disjoint scopes (§12, §13), and a forward-migration test from schema 2 to 4; `ClaudeCliProvider` hard-codes `haiku`, so 6.3 owns a model and effort option; `trait-check` sends diffs to Jev unredacted in TS, so the C# one redacts every provider text and recorded eval file (§5.2); unit 1.5 asserts the exact per-verb grants; 6.5 gets a cold-start target and waits for 3.1; `trait-check`'s exits 2 and 3 are documented as its own; §17 gets a Status column, Q3 and Q7 are marked "confirm before P1", the Q6 reading is flagged for one-line confirmation, and Q9 is a yes/no with defined behaviour for the alternative.

**Rev 7 (2026-10-23), /autoplan round 2 on the owner answers.** Fixed: the §17 Status column of rev 6 had not been applied to the rows (the table was malformed); it is now, with Q3 and Q7 "confirm before P1" and one deadline worded the same in the Rev 5 paragraph, P0 and §17; the Jev batch is real code in unit 6.0 (`JevProvider` and its wire types are internal and ask one question), with a fallback of heuristics-only if the wire cannot batch; 6.0's stubs answer "not in this build yet" (exit 1) and never throw, routed before the `rest.Length != 1` arm of `CommandTable`; the `promptSort` config write path (`ConfigCommands`, `SetConfig*`, `JudgeConfig`) belongs to 6.1; 6.3 owns the provider lookup by name and the `ClaudeCliProvider` option, which must set `MAX_THINKING_TOKENS` too; the Judge units edit their own `PublicAPI.Unshipped.txt` lines (the 999 lines are Core's) instead of queueing on the integrator; 6.4 gets its own Core and Infrastructure folders; the TS verbs redact at rest, so the C# writers of `eval_items`, `prompt_sort_runs` and the decision details do too; `adjudicate` runs its child with no tool grants; the removal log is created first, owner-only, redacted, and a failed log write stops with nothing changed; the TS `judge label status` counts are logged so the owner sees what is lost; the install check no longer claims every binary answers `version` or `help` (only `ui-evidence` gets it; 5.1 pins a known-good call per binary); the lever installers' "run install-judge.sh" refusal text is edited in 3.1; the skill preflights say to run `setup.sh` from the toolkit checkout; P4 checks `judge why`, not a 6.4 verb; Q9 names the critical path and the alternative's behaviour; `trait-check` with Jev excluded exits 3.

**Rev 8 (2026-10-23), /autoplan round 3 on the owner answers.** Fixed: a per-verb `ui-evidence` grant does not restrict flags (a prefix grant allows any flags of its verb), so the text no longer claims it keeps `--allow-writes` and a non-local `--host` away; the binary's host guard holds them (§3.1, unit 1.5); unit 6.0 now has its own row in the P1 table, and is the one implementer allowed to edit its package's hotspot files, once, ahead of 6.1 to 6.4, which replace stub bodies only (§12, §13); the verb map no longer gives the migrations, the Jev batch and the routes to 6.1 to 6.3 (§5.2); 6.0's Jev batch honours `--providers`, `AW_JUDGE_PROVIDERS` and `providers.jev: false`, and its service stubs throw as the slice recipe has it while the Host maps that to "not in this build yet"; the install check names a state-free call per binary (`judge` on a scratch `AW_STATE_DIR`, since at `--publish` the real database is still TS-shaped and would answer AW-DATA-009; `sindri-ui` by file presence), 6.0 proves YamlDotNet under NativeAOT, `migrations from 0005 on` go through the integrator; the Q9 alternative no longer contradicts the graph, and the graph shows the 3.1 to 6.5 edge; `bin/` in the kept list excepts `bin/sindri-app/`; the adjudicator child runs with tools, settings and hooks off in a scratch directory; the removal log's directory mode is specified; the AW-DATA-009 citations name `src/Aw.Kernel.Data/`; one message to the owner covers Q3, Q7, the Q6 reading and Q9 before wave 1; a header note says that rev 5 and later win over Rev 2 to 4 where they reversed something.

**Rev 9 (2026-10-23): owner confirms Q3, Q6, Q7 and answers Q9.** The owner confirmed Q3 (the nudge line drops "shape signals") and Q7 (`gh-publish` through the `gh` CLI) as recommended, and confirmed the Q6 reading (Sindri owns, builds, installs and versions the `ui-evidence` binary; it stays separate from `sindri`; the skill calls it). The owner picked the alternative on Q9, "Yes, land later": the by-hand Judge verbs (`label`, `adjudicate`, `eval` with `import-turns`, and the `prompt-sort` eval side; units 6.3 and 6.4) land after the switch, and until then print `judge: <verb> is not in this build yet` on stderr with empty stdout and exit 1. §5.2 specifies that stub exactly (verbs, line, exit, what it must not do, where `judge.md`, the tests and the `/judge` skill say it); §12 drops 6.3 and 6.4 from P1, adds the stub checks to the P2 and P3 exits and adds P5 for the post-switch units; §13 moves them to wave 5; the migrations are renumbered (6.0 ships `0003_prompt_sort.sql`, 6.3 ships `0004_eval_labels.sql`), so a box at the flip carries no empty eval tables; §15 swaps the "Judge port is the longest item" risk for the hook port and adds the missing-verbs risk; §17 records each answer and has no open question. The hooks (6.1, 6.2), 6.0's contract and what the recipes use still land before the switch, per Q2. One estimate of mine is corrected: the hook-path part of `prompt-sort/` is about 800 TS lines, not 1,000.

**Rev 10 (2026-10-23), /autoplan round 1 on rev 9.** Fixed: the stub routing is now exact (each stubbed first word goes into `_routed` and gets an arm before `rest.Length != 1`); the claim that `--providers` and `AW_JUDGE_PROVIDERS` are validated first was false on main (only a question reads the list), so the stub strips the flag only; the `<verb>` rule covers `label` alone, `eval <question>` and an unknown `prompt-sort <sub>`; the hook-grep guard is specified as an invocation pattern with a positive control and extended to recipes and `src/Sindri` (bare `eval` and `label` are shell words in `config/lib/sys-pkg.sh` and in test scripts); tests are per verb string; the `/judge` skill marker says not to retry, and the scaffold advice that depends on `promote` is reworded; the skew window is described correctly (pull to flip, then flip to P5); the provider-lookup port method moves from 6.0 to 6.3's contract step so no interface changes with no consumer before the switch; 6.3's contract step declares 6.4's surface too; 6.3's migration number is "the next one after the last shipped"; P5 gets a 30-day target because decision details are kept 30 days, plus the older-binary-than-database risk (AW-DATA-007); the 6.1 proof gains a secret-in-prompt redaction test and P5's exit requires the adjudicator argv-and-cwd test and the redaction tests; P2 checks a two-word stub verb; the 6.0 wording separates red stubs from the permanent-until-P5 route.

**Rev 11 (2026-10-23), /autoplan round 2 on rev 9.** Fixed: `prompt-sort` is one arm in `CommandTable` that 6.0 owns and the second word decides (none or `scaffold` to 6.1, a stubbed subcommand to the stub, anything else to the unknown-subcommand answer, whose exact string the test pins because the TS printed it without the `judge:` prefix); P0 no longer says every new verb answers the stub line, only the shared 6.3/6.4 handler does, and 6.1's and 6.2's verbs are ordinary red stubs; the migration rule is one rule (6.0 ships `0003_prompt_sort.sql`, a fix after it merges is a new migration, 6.3 takes the next free number, its test is N to N+1); the invocation test has a named owner and file (`scripts/tests/judge-stub-invocations.test.sh`, 6.0), covers `scripts/` and `skills/` (not `skills/judge/`), every variable form of the judge binary and a second pattern for C# argv, and loses a list entry in the PR that lands a verb; the P5 target is 21 days (so it lands before the 30-day detail retention ends); the stale-binary risk names the likelier causes (a binary earlier on `PATH`, `dotnet run` against the real state dir) and the signal (the statusline reads down); 6.3's contract step leaves 6.4's types as compiling stubs so no red test is on main after the flip, and the adjudicator test asserts four named properties; the orphaned clause in P0 and a missing period in the skill text are fixed.

**Rev 12 (2026-10-23), /autoplan round 3 on rev 9.** Fixed: the unknown-`prompt-sort`-subcommand answer had two owners and a contradictory string; it is 6.0's single arm, with the C# prefix, on stderr, exit 1, all pinned by its test; 6.4 needed a hotspot edit to delete stub routes, so 6.0 now routes every stubbed verb to its own handler (over a shared line-printing helper) and 6.3 and 6.4 replace handler bodies and delete no route; the invocation test is owned by 6.0 in every place (the 6.0 row, §10, 6.5), excludes its own fixtures and `scripts/tests/`, and lists the wrapper forms it accepts missing; the stale hard-coded `0004` names became "the next free number"; the 21-day target is for 6.3 only and 6.3 starts the day the flip is confirmed (P4); the `adjudicate` child test asserts five properties, a minimal environment among them; a mistyped `eval import-turn` and bare-versus-`prompt-sort` examples are specified.

**Decision it implements (owner, 2026-10-20 and 2026-10-23):** the toolkit's install becomes the C# build. Plain `sindri`
is the C# app (today `scripts/sindri-app.sh` and the name `sindri-app`). `setup.sh` and the provider installers install
the C# binaries and **remove** the TypeScript packages (`sindri/`, `judge/`, `scorer/`, the TS of `skills/ui-evidence`,
the TS of `skills/bugFixOrchestrator`) and their installs. The switch is one-way: no rollback, no `--runtime` selector, no
state archive tooling. The hook contract stays green. The scorer and the TS Sindri commands that have no C# version are
**cut**; they come back later as C# recipes built from scratch (§14).

**The owner's main care (2026-10-23):** the `ui-evidence` skill and the Sindri recipe's evidence steps run **the same
code**. §3 specifies one C# Evidence path and maps every SKILL.md command to its C# verb, with the gaps.

**Supersedes:** `2026-10-09-agent-readiness-design.md` §4c (the reversible switch, `--runtime cs|ts`, the TS state
archive, the 14-day TS tag, "the always-on box first, the laptop a day later"). **Amends:** see §15.

**Evidence.** Every file:line below was read on `origin/main` (75428215d) on 2026-10-23. `EV` is `docs/csharp/evidence.md`.
Where a claim is not verified in code, the text says so.

## 1. Goal, and what is cut

**Goal.** After the switch a box has no Node-built toolkit package, no `scorer`, no TS `sindri`, and one install path:
the C# binaries published by one script. Nothing the hooks, skills or recipes call is missing.

| Item | Decision | Why |
|---|---|---|
| `scorer` (reports, `live`, `probe`, `audit`, launchd jobs) | CUT | owner 2026-10-23; rebuilt inside Sindri later (§14) |
| TS `sindri evolve`, `scope`, `observe`, `shape`, `profile explain` | CUT | owner 2026-10-20 (`project_evolve_scope_observe_recipes`); recipes later |
| TS `sindri profile migrate`, `repo onboard --template`, `channel`, `--schema-version` | DROP | no C# profile migration (no TS state is migrated); channels were the TS installer's; sindri-cmds.md rows 17, 27, 32, 34 |
| skills `correct`, `reflect` | REMOVED | they only drive `sindri evolve` (`skills/correct/SKILL.md:5,28`, `skills/reflect/SKILL.md:5,29`) |
| judge `prompt-sort`, `trait-check`, `label`, `adjudicate`, `eval`, `import-turns` | PORTED (Q2, owner); `label`, `adjudicate`, `eval`, `import-turns` and the `prompt-sort` eval side after the switch (Q9, owner) | owner 2026-10-23: port all of the Judge; §5.2 maps every verb; units 6.0 to 6.5, with 6.3 and 6.4 in P5 |
| `mods/aw-live` (the /live pane) | UNINSTALLED at the switch (Q4, owner) | it only reads `scorer live` (`mods/aw-live/hooks/register.tsx:39-72`) |
| Evidence script features (scroll, drag, press, ...) | PORTED (Q1, owner) | §3.3 |

## 2. What the switch is

One-way, in this order, each step proven before the next phase (§12):

1. Close the C# gaps (§3 to §6). Nothing installs yet.
2. Land `scripts/install-cs.sh` (§7) and `scripts/cutover-clean-ts.sh` (§8) as standalone scripts, proven on a scratch
   box (P2). Nothing calls them yet: until the switch, nothing in `setup.sh`, the providers, MCP registration or launchd
   points at the C# build (AGENTS.md, "C# build"). Unit 5.1 writes the scripts and their test only; the `setup.sh` and
   provider edits that call them are part of the switch PR.
3. **The switch PR** (§9): `setup.sh` and the provider installers call the two scripts, and the same PR deletes the TS
   directories, CI jobs, gates, suites and docs. Merging it changes no box by itself; any checkout that pulls it and runs
   `./setup.sh` flips that box (the owner's boxes included: P4 is the planned run).
4. **The flip:** the owner pulls main and runs `./setup.sh` once on each box, right after the pull. That run publishes the
   C# binaries, cleans up the TS install and links the C# binaries (§8). Until it runs, the installed hooks (copied files)
   and the TS wrappers keep working, but the skills are linked from the checkout, so a pulled `ui-evidence` or
   `bugFixOrchestrator` skill already names binaries that exist only after the run.

There is no step that restores TS. A box that needs TS again checks out an older commit and runs that commit's
`setup.sh`; this spec builds nothing for it. The C# build keeps no byte or state compatibility with TS (AGENTS.md,
"C# build"); the exception stays the hook contract (§10).

## 3. Evidence: one code path

### 3.1 The design

The only implementation of UI, API and DB evidence is `Aw.Evidence.Features` (with `Core` and `Infrastructure`). It is
reached **only** through the `ui-evidence` binary (`Aw.Evidence.Host`, AssemblyName `ui-evidence`, framework-dependent;
evidence.md:7, :90).

**Ownership (owner answer to Q6, 2026-10-23: "no, sindri owns the binary, not the skill").** `ui-evidence` is **Sindri's
binary**. It is built, published, installed and versioned as part of Sindri, and the skill calls Sindri's binary; the skill
owns no tool that Sindri borrows. In practice:

- **Packaging and version.** `install-cs.sh` (§7.1) publishes `ui-evidence` in the same stamped directory as `sindri` and
  `sindri-ui` (`$AW_STATE_DIR/bin/cs/<stamp>/`), from one build with one commit stamp (`SourceRevisionId`, `AwBuildTime`).
  `bin/cs/current` moves them together at link time, so there is no separate evidence install, stamp or version: Sindri's
  stamp is the evidence tool's stamp. The framework-dependent publish and the `install-browser` step stay as §7.1 has them.
- **Install location.** The `~/.local/bin/ui-evidence` wrapper is written by the same `--link` step as the `sindri` wrapper
  and execs the binary next to `sindri`. The `sindri` wrapper also exports `AW_UI_EVIDENCE_BIN=<current>/ui-evidence`
  (as it does `AW_SINDRI_JUDGE` and `AW_PACKETS_BIN`, §7.1); the variable is new in this spec (no code or `.env.example` names it today): BugFix's adapter will resolve it (§4.3 a, unit 2.1), `sindri doctor` reads it (unit 4.2), and `.env.example` lists it (the integrator, with the other new variables of §7).
  So the skill, a human, bugfix and the recipe's evidence agent steps run the same file.
- **Who says so.** `docs/sindri/README.md` (a short "Evidence" paragraph: the binary ships in Sindri's install),
  `docs/csharp/evidence.md` ("At the switch" opens with the ownership and the install location),
  `docs/csharp/sindri.md`, the `Evidence` node `note` in `deps.yaml` ("ships in Sindri's install; same stamp as `sindri`"),
  and the skill text: the rewritten SKILL.md opens "This skill runs Sindri's `ui-evidence` binary" and its preflight says
  `command -v ui-evidence >/dev/null || { echo "run ./setup.sh from the agentic-workflow checkout (the evidence tool ships with Sindri)"; exit 1; }` (§4.3 d, §3.5).
- **Doctor.** `sindri doctor` gets a `setup:ui-evidence` line: found next to `sindri` and answering `ui-evidence version`; a
  warning when the binary is missing (unit 4.2; a warning, not a failure). `ui-evidence` has no `help` or `version` verb today
  (an unknown command is a usage error, `CommandTable.cs:69`), so unit 1.4 adds `version` (the stamp). The line covers the
  binary only; a missing browser is reported by `ui-evidence doctor`.
- **What ownership is not.** The code stays one implementation, `Aw.Evidence.*` (Core, Features, Infrastructure, Host), in
  its own package so Playwright and Npgsql stay out of the Sindri host. Ownership is about who ships, installs and
  versions the binary, not a project reference: the declared `Sindri -> Evidence` edge stays `kind: file` (below).

- **The skill** runs Sindri's `ui-evidence <verb>` from a Bash call. SKILL.md is its instructions.
- **The recipe's evidence agent steps** run the same binary. `step.evidence.ui-run`, `api-run` and `db-run` are Agent
  runtime steps (`EvidenceSteps.cs:21,24,27`); the plan steps are too (`:12,15,18`). The engine launches an agent session
  with a brief (`AgentLaunches.RequestFor`), and the agent runs the CLI.
- **The permission.** `AgentLaunches.cs:12` defines `_evidence = ["Bash(ui-evidence:*)"]` but adds it only in
  `EvaluatorRequestFor` (`:74`). `RequestFor` (`:57`) gives a normal agent step `_report` and, for write scope, `_commit`.
  The cutover adds grants to `RequestFor` **per verb**, not `Bash(ui-evidence:*)`: a step gets the verbs its brief names
  (`ui-evidence run`, `parity`, `api-run`, `db-run`, `provenance`, `doctor` for the run and plan steps; `gh-publish` only
  for a publish step). A blanket grant would also give `gh-publish` to every step. A per-verb grant does not
  restrict flags (a prefix grant allows any flags of its verb), so `--allow-writes` and a non-local `--host` are held by the
  binary's own host guard (a bare loopback origin only, AW-EVIDENCE-004; a preview host needs `--allow-preview-host`, and its
  write-looking steps need `--allow-writes`; evidence.md host-guard table), not by the grant. Without the grants the recipe's run step cannot run (evidence.md:64). The skill's own `allowed-tools` (§3.5) keeps
  the blanket `Bash(ui-evidence:*)`, since the human drives that session.
- **The brief.** The step brief says "follow the ui-evidence skill" and names the verb and its arguments. The skill text
  is the step's instructions, so one text describes both.
- **The deterministic steps.** `step.evidence.none` and `step.evidence.combine` are Block runtime with no `IBlockStep`
  class today (evidence.md:60). They become `IBlockStep` implementations that read summary files by the file contract:
  schemas `ui-evidence.run/1`, `evidence.api.summary/1` and `evidence.db.summary/1`. `ProveCollectStep`
  (`ProveCollectStep.cs:25-27`) already reads `fact:evidenceSummary` that way.
- **No project edge.** Sindri gets no `compose` or `project` edge to `Aw.Evidence.*`. Reasons: it would pull Playwright and
  Npgsql into the Sindri host (and block an AOT split of other heads, evidence.md:75); a second composition of
  `IMachineLocks` and `IJudge`; and the Block runtime's 2-minute `MaxDuration` cannot hold a browser run that
  "outlasts 10 minutes" (evidence.md:83). The declared `Sindri -> Evidence` edge stays `kind: file` (heavy-job lock slot,
  `deps.yaml:~342`).
- **Rejected for now:** an `IUiEvidence` port in Sindri Core, a `UiEvidenceTool : IUiEvidence` adapter in Sindri
  Infrastructure that spawns `ui-evidence` (the `AwPacketsTool` pattern, `AwPacketsTool.cs:17`), and a `Sindri -> Evidence
  kind: cli` edge. It would also keep one implementation. It is worth building only when a non-agent Block step must run a
  check. Exit mapping for that day: 0 pass, 2 check failed (a result), 3 review pending, 4 run errored, 75 lock busy
  (park), 1 refused (evidence.md:82).

**Decision (Q6, owner 2026-10-23, reading confirmed in rev 9): Sindri owns the binary; the recipe's evidence run steps are agent steps that run it.**
The owner's words: "no, sindri owns the binary, not the skill". Read for Q6: the evidence binary belongs to Sindri (see
"Ownership" above), the `ui-evidence` skill calls Sindri's binary, and the agent steps run the same binary briefed from the
skill text (the descriptors already say Agent). The design is still one implementation reached through one binary by both
callers, with no in-process compose into the Sindri host (Playwright; the 2-minute block cap).

Rejected readings. (1) A `sindri evidence <verb>` subcommand, which "Sindri owns it" could be read as. Rejected: the
command would run Playwright and Npgsql inside the `sindri` host (framework-dependent, with a browser download), a browser
run that "outlasts 10 minutes" would sit in the control plane's own process, and the per-verb agent grant of §3.1 would have
to become a grant on `sindri` itself, widening every evidence step to every `sindri` verb. The binary is Sindri's by
ownership and packaging, not by being a verb of `sindri`. (2) Block steps through an `IUiEvidence` port and a CLI adapter:
costs a `cli` edge and a port, and the 2-minute block bound has to be lifted for it (kept under "Rejected for now").

### 3.2 Command map: skill command to C# verb

SKILL.md = `skills/ui-evidence/SKILL.md`. Line numbers are SKILL.md unless marked. "Verdict" is equivalent, partial,
missing, or C# only. The C# verb table is `CommandTable.cs:15-18,54-70`.

| # | Skill says (line) | TS entry | C# verb | Verdict |
|---|---|---|---|---|
| 1 | `bash scripts/doctor.sh` (L10) | `scripts/doctor.sh`, `settings-files.ts` | `ui-evidence doctor` (`DoctorCommand.cs`; EV L31, L534-560) | partial: no worktree-slot checks (G-d) |
| 2 | spawn `qa-runner` to write a script (L11) | `script-schema.ts` | none; input is `UiScript` (EV L82-115) | the C# schema is a strict subset (§3.3) |
| 3 | `with_stack_lock_and_heavy_job_lock 120 node dist/bin.js <script> <run-dir> [--baseline] [--app-build] [--fixtures] [--cache]` (L12) | `cli.ts`, `run-script.ts` | `ui-evidence run <script.json> <run-dir> [--baseline] [--app-build] [--fixtures] [--cache] [--host] [--allow-preview-host] [--allow-writes] [--lock-timeout s]` (`RunScriptCommand.cs:12-14`) | equivalent; takes both locks itself (EV L469-495), so the bash wrapper goes; exits add 4, 75, 130 |
| 4 | `checkDbProvenance` before Linear upload (L13) | `db-provenance.ts` | `ui-evidence provenance` (`CheckProvenanceCommand.cs`) | equivalent; SKILL must name the verb, not an internal function |
| 5 | visual verdict, cache, `judge visual-critique` (L14) | `visual-gate.ts`, `verdict-cache.ts`, `visual-critique.ts` | inside `run` (`Core/Visual/VisualGate.cs`, `VerdictCache.cs`) | partial: judge is `UnavailableJudge`, verdict stays `unchecked` (G-c) |
| 6 | `node dist/bin.js gh-publish <run-dir> --repo --pr --db-provenance seeded --reviewed f,f` (L23) | `gh-publish-cli.ts`, `github-publish.ts` | **none** | missing (G-b): no hit for `gh-publish`, `gh api` or `ui-evidence/pr` under `src/Evidence` or EV |
| 7 | text-only PR comment via `renderPrComment` from `dist/publish.js` (L24) | `publish.ts` | `ui-evidence publish-plan <run-dir> --provenance seeded\|unknown [--linear-issue id]` (`PublishPlanCommand.cs:12`) | equivalent; flag name differs (G-g) |
| 8 | `parity <manifest> <run-dir> [--host --allow-preview-host] [--allow-writes] [--app-build] [--db-provenance ...]` (L56-60) | `parity-cli.ts`, `parity-run.ts` | `ui-evidence parity` (EV L25) | equivalent; exits add 75, 130 |
| 9 | `parity-plan <run-dir> --reviewed f,f` (L72) | `parity-cli.ts` | `ui-evidence parity-plan` (EV L26), exit 0/3/1 | equivalent |
| 10 | helpers `buildPutCommand`, `selectStale`, `renderLinearComment`, `renderParityPrComment` (L73) | `parity-publish.ts` | `parity-put --upload f --file png`, `parity-stale --attachments f --keep ids`, `parity-comment <run-dir> --for linear\|github` (EV L27-29) | C# is better: real verbs; L73-76 are rewritten |
| 11 | Linear MCP attachments (L74-76), `gh pr comment` (L19, L24), `gh api -X DELETE` of the evidence branch (L26) | agent tools | n/a | unchanged; the agent's, not the binary's |
| 12 | selector repair via `judge ui-element-repair` (L90) | `repair.ts`, `judge-exec.ts` | inside `run` through `IJudge.RepairSelectorAsync`; rules in `Core/Steps/RepairRules.cs` | partial: judge not wired (G-c) |
| 13 | lint for sloppiness (frontmatter L3) | `lint-page.ts` | `Core/Lint/PageLint.cs`, inside `run` | equivalent |
| 14 | `npx playwright install chromium` (implicit) | node playwright | `ui-evidence install-browser` (`PlaywrightInstaller.cs:20-28`) | equivalent |
| 15 | locks: `with_stack_lock_and_heavy_job_lock 120` (L12, L56) | `scripts/lib/locks.sh` | built into `run`, `parity`, `api-run`, `db-run` (`DirectoryLocks.cs`); interoperates with `config/lib/locks.sh` | equivalent |
| 16 | (no text) | none | `api-run`, `db-run` (EV L23-24, L167-240) | C# only; the skill needs text (G-f) |

Also changed and not a verb: `summary.json` has a new schema (EV L626-640: `startedAt`/`finishedAt` for `ts`, `visual`
is an object, no `steps[].name`). The run dir must be `$AW_STATE_DIR/ui-evidence/runs/<id>`; a stable `--cache` with
`--app-build` gives cache hits (EV L601-607).

### 3.3 The gaps, and what closes each

| Gap | What is missing | Closed by |
|---|---|---|
| G-a | script features: actions `scroll`, `drag`, `press`; step `index`, `to`, `grabOffset`, `dropOffset`, `holdAlt`, `key`; target prefixes `role:<role>\|<name>`, `text:<text>`, a trailing `#<n>`; `expectedState` kinds `url-search`, `testid-absent`; script-level `login` and `localStorage`; the write-guard keyword rules for the new actions. `StepAction` has only `goto\|click\|fill\|expect-visible`; unknown keys are refused (evidence.md:30-35; `Core/Scripts/StepAction.cs`, `Browser/Locator.cs`, `Guard/StepGuard.cs`; `LoginSpec` exists only in the parity manifest, `Core/Design/LoginSpec.cs`) | decision Q1; unit 1.1 |
| G-b | `gh-publish`: push PNGs to the branch `ui-evidence/pr-<n>`, write `pr-comment.md`, exit 0/1/3/4; refuses without `--reviewed` and a provenance value, as the TS verb did | decision Q7; unit 1.2 |
| G-c | `JudgeCliJudge : IJudge` in `Aw.Evidence.Infrastructure/Judging`, replacing `UnavailableJudge` in `Composition.cs`: spawn `judge <question>` with JSON on stdin, parse the envelope. The contract is on main (`docs/csharp/judge.md:176-177`: `ui-element-repair` input `{brokenSelector, step, candidates:[...]}`, outputs `repaired(+chosenIndex)\|no-good-candidate`, threshold 0.6, 10 s; `visual-critique` input `{afterScreenshot, evidenceDir, baselineScreenshot?}`) | unit 1.3 |
| G-d | `doctor` lacks the slot checks (`UI_EVIDENCE_SLOT_*`, `UI_EVIDENCE_WORKTREE`, foreign-checkout refusal, printing `--host` and the slot DB; SKILL L28-36); `run` and `parity` read no env file, so the SKILL "Configuration" section (L102-139) is wrong for C# (evidence.md:558-560) | unit 1.4: a loader for `deployment.env` and `.ui-evidence.local.env` in `run` and `parity` (plain `KEY=value` lines parsed, never sourced; an allowlist of keys; a variable already set in the environment is never overridden), and the doctor checks |
| G-e | per-artifact `commit` and `at` (DTC9, `EvidenceSteps.cs:7`): the C# summary has `appBuild` and `finishedAt` but not clearly a per-artifact `commit`. **Not verified in code** | unit 1.4 first checks, then adds if missing |
| G-f | SKILL.md rewrite (§3.5) and the text for `api-run` and `db-run` | unit 1.5 |
| G-g | `--db-provenance` (gh-publish, parity) against `--provenance` (publish-plan) | one name across the verbs. Recommended: `--provenance` everywhere, with `--db-provenance` accepted on `parity` for one release only if a caller needs it; decided in unit 1.4 |

**Decision (Q1, owner 2026-10-23, "the recommended answer"): port the script features of G-a.** They were added for a real
app's flows, and the skill text and the live qa-runner depend on them. The cut alternative (SKILL.md and qa-runner on the C#
subset of four actions and three target kinds) is closed.

**Decision (Q7, taken as recommended and confirmed by the owner in rev 9): `gh-publish` talks to
GitHub through the `gh` CLI**, with the runner style Evidence already has (`ProcessCliRunner`), behind a port with a fake
(git data API calls: blob, tree, commit, ref on `ui-evidence/pr-<n>`). `Aw.Forge`'s GitHub adapter is documented read-side
(evidence.md gap list), so using it would need write methods and Forge's credential rules first.

### 3.4 The skill and the recipe stay one path

Two rules keep it so, both checked by a test in phase 1:

1. **No second implementation.** A repo check (a bash test in `scripts/tests/`) fails when any file outside
   `src/Evidence/` spawns Playwright, or when SKILL.md or a recipe brief names a command other than a `ui-evidence` verb for
   running a check.
2. **One text.** The brief of each evidence agent step is generated from, or quotes by file path, the skill's verb lines.
   Unit 1.5 adds a test that the verbs named in the briefs are exactly verbs in `CommandTable.cs`.

### 3.5 SKILL.md rewrite: lines to change

| Where | Change |
|---|---|
| L10 | `bash scripts/doctor.sh` becomes `ui-evidence doctor` |
| L12, L56 | drop `with_stack_lock_and_heavy_job_lock 120 node dist/bin.js`; run `ui-evidence run ...` and `ui-evidence parity ...`; exits 0/1/2/3/4/75/130 |
| L13 | name `ui-evidence provenance` instead of `checkDbProvenance` |
| L14 | verdict text matches `visual` as an object and the `unchecked` rule |
| L23 | `gh-publish` becomes `ui-evidence gh-publish` (after G-b), flag names per G-g |
| L24, L73 | no `node` import of `dist/publish.js` or `parity-publish`; use `publish-plan`, `parity-put`, `parity-stale`, `parity-comment` |
| L42-96 | the script schema section follows the C# schema (G-a) |
| L90 | repair through `run` (the judge adapter, G-c) |
| L102-139 | "Configuration": the env-file loader (G-d) or the plain environment |
| new | `api-run` and `db-run` sections (`--at base\|head`, `--host`, targets from `<repo>/.agentic-workflow/evidence.json`, `summary.json` on stdout) |
| install | `npx playwright install chromium` becomes `ui-evidence install-browser`, run once by the installer (§7) |
| run dir | pass `$AW_STATE_DIR/ui-evidence/runs/<id>` |
| frontmatter | `allowed-tools` loses `node`, `npm`, `npx`; gains `Bash(ui-evidence:*)` |

`skills/ui-evidence/scripts/` (`doctor.sh`, `lib/locks.sh`) is deleted when nothing references it (EV "At the switch",
L599-625); `config/lib/locks.sh` stays.

## 4. BugFix: the skill onto `src/BugFix`

### 4.1 The state of C# today

C# `bugfix-state` cannot finish a ticket. Its `judge` and `run-ui` verbs are wired to stubs that always refuse with exit 3:
`src/BugFix/Aw.BugFix.Infrastructure/UnavailableJudge.cs:12-17` (AW-BUGFIX-016) and `UnavailableUiEvidence.cs:12-17`
(AW-BUGFIX-017). `src/BugFix/aot-smoke/expected.txt:18` pins the refusal. Only `--unresolved` reports work (bugfix-judge.md
§0.1). The judge contract now exists (`docs/csharp/judge.md`), and so does the Evidence CLI; the adapters were not written.

### 4.2 Command map

Line numbers are `skills/bugFixOrchestrator/SKILL.md`. TS is `src/cli.ts` (dispatch :74-121) and `commands.ts`; C# is
`src/BugFix/Aw.BugFix.Host/Commands/*` (`CommandTable.cs:11-26`).

| Skill step (line) | Command | C# verb | Verdict |
|---|---|---|---|
| Resume (46) | `resume --state` | `ResumeCommand` | equivalent |
| Intake (70) | `init --state --ticket` | `InitCommand`; also makes `digest.key` | equivalent |
| Investigate (76) | `advance investigate --evidence` | `AdvanceInvestigateCommand` | equivalent |
| Reproduce test (114) | `run-test --state --check <f> --cwd <d> -- <cmd...>` | `RunTestCommand`; stdout adds `outcome` (passed/failed/broken); stricter command rules (runner allowlist, env freeze, clean tree) | equivalent, stricter |
| Reproduce ui (101) | `run-ui --state --check <script.json> --cwd` | `RunUiCommand` into `UnavailableUiEvidence` | **missing** until §4.3 (a) |
| Reproduce (119) | `advance reproduce --evidence --check --cwd` | `AdvanceReproduceCommand` | equivalent |
| Fix (131) | `start-attempt --mode A\|B\|C` | `StartAttemptCommand`; adds `--abandon-judge` | equivalent, one extra flag |
| Fix (167) | `record-candidate --branch --cwd [--hypothesis n] [--allow-test-changes]` | `RecordCandidateCommand` | equivalent (mode B needs `--hypothesis`) |
| Evaluate (139, 185) | `status --state`; `record-run <cand> --evidence` | `StatusCommand`, `RecordRunCommand` | equivalent |
| Evaluate (187) | `judge <cand> --state` | `JudgeCandidateCommand`; `{candidate,decision,decisionId,reasonCode,reasons,status}` | partial: verb present, adapter is a stub |
| Report (209) | `advance report (--candidate <id> \| --unresolved)` | `AdvanceReportCommand` | equivalent; the resolved path needs the judge adapter |

### 4.3 What closes the stubs

(a) **UI evidence adapter.** BugFix's own Infrastructure gets an adapter for its UI-evidence port that spawns the
`ui-evidence` binary (`ui-evidence run <script> <run-dir> --app-build <commit>`, the C# verb of §3.2 row 3), resolves it by
`AW_UI_EVIDENCE_BIN` else PATH, and maps exits (0 pass, 2 check failed, 4 errored to `broken`, others refused). So bugfix,
the skill and the recipe reach the same Evidence code.

(b) **Judge adapter.** BugFix's own Infrastructure gets an adapter for its judge port that spawns the `judge` binary
(`judge resolution-check` with JSON on stdin, then `judge why <id>`), bounded to 5 minutes as TS did
(`deps.ts:43-45,63-74`). It resolves by `AW_JUDGE_BIN` else PATH. It reads the same judge state as the hooks, so
`AW_STATE_DIR` must match (or `AW_JUDGE_STATE_DIR`).

(c) **Two mismatches, resolved here.**
- *Input digest.* The port expects "the SHA-256 of the input file" (`Core/Model/JudgeAnswer.cs:12`; bugfix.md:236-239). The
  judge stores `input_digest` as the first 16 hex of SHA-256 over its canonical re-serialization (`Aw.Judge.Core/Evaluation/InputJson.cs:74-75`).
  Resolved: the adapter writes `judge-<cand>.json` in the judge's canonical form and compares the 16-hex prefix; the
  port's doc and `JudgeAnswer` wording change to "the judge's input digest (16 hex)". The TS helper did the same with a
  16-char digest (`commands.ts:442`).
- *Check kind.* BugFix serializes `CheckKind` as `test` or `ui` (`Core/Model/CheckKind.cs`); the judge's `resolution-check`
  takes `ui-evidence`, `api-evidence`, `db-evidence`, `test`. The adapter maps `ui` to `ui-evidence`. BugFix has no verb for
  api or db checks, so those kinds stay unreachable from the skill; adding `run-api`/`run-db` is out of scope (§14).

(d) **Install and the skill.** `bugfix-state` (NativeAOT, no SQLite) goes on PATH (§7). The skill drops
`BFS_JS="$TK/skills/bugFixOrchestrator/dist/bin.js"` and every `node "$BFS_JS"` (SKILL.md:31-44), the `npm ci && npm run build`
fallback, and `allowed-tools` `Bash(node *)`, `Bash(npm *)`, `Bash(npx *)`, `Bash(BFS_JS=*)`, `Bash(TK=*)` (L5); it gains
`Bash(bugfix-state:*)`. `STATE` becomes `$AW_STATE_DIR/bugfix/<repo-slug>/<ticket-slug>` (bugfix.md:17, `deps.yaml:224`).
The Setup block's `locked()` sources `config/lib/locks.sh` (the one that stays) instead of `skills/ui-evidence/scripts/lib/locks.sh` (SKILL.md:36, :155), and the `doctor.sh` line follows §3.5. Both rewritten skills (this one and `ui-evidence`) open with a one-line preflight: `command -v <binary> >/dev/null || { echo "run ./setup.sh from the agentic-workflow checkout"; exit 1; }` (a stop, not a note), since the skills are linked from the checkout and can run before the flip's `setup.sh`. Sindri already finds the binary by `AW_BUGFIX_BIN` else
`bugfix-state` on PATH (`BugFixStateTool.cs:11`).

(e) **Text the skill must absorb.** Exit codes: C# adds 2 (usage) beside TS's 0, 1, 3 (the skill branches only on 3, L20).
New refusals the skill does not mention: a dirty tree or untracked files on `run-test`, `run-ui`, `record-candidate`,
`record-run` (AW-BUGFIX-020); command classification (AW-BUGFIX-012); the env freeze (`AW_BUGFIX_CHECK_ENV`); a busy
`state.lock` (AW-BUGFIX-021, `AW_BUGFIX_LOCK_WAIT_SECONDS`); refusal while `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` is set. The
run timeout stays 30 minutes (`AW_BUGFIX_CHECK_TIMEOUT_SECONDS`). Open bug fixes restart at the switch (C# never reads TS
state, bugfix.md:205).

## 5. Judge: every caller onto `src/Judge`

### 5.1 Callers and coverage

| Caller | Call | C# |
|---|---|---|
| `config/hooks/send-gate.sh:114-116` | `judge wake-gate` | covered |
| `config/hooks/scope-gate.sh:63-65,74` | `judge brief-scope`, `judge brief save` | covered |
| `config/hooks/done-gate.sh:148-152,182-184`, `done-gate-annotate.sh:16` | `judge ask-check` (exit only), `judge brief get` | covered (documented exception: exit 0 = ask) |
| `config/hooks/subagent-start-map.sh:25-30` | `brief map-by-name`, `map-by-dispatch`, `set-agent-id` | covered |
| `config/hooks/judge-health.sh:6-14`, `config/statusline.sh:116-132` | `judge health` (`find_bin`: PATH, else `~/.local/bin/judge`) | covered |
| `config/hooks/prompt-sort.sh:38,53` | `judge prompt-sort` | **BUILD** (unit 6.1; today an unknown question exits 64 and the hook prints nothing) |
| `config/hooks/trait-check.sh:62-75` | `${AW_JUDGE_BIN:-judge} trait-check` | **BUILD** (unit 6.2; no verb today) |
| `skills/judge/SKILL.md` | `health`, `why`, `undo`, `approve`, `config get`, `config set <q> enabled\|threshold` | covered |
| same skill | `label import\|outcomes\|set\|status`, `adjudicate`, `prompt-sort why\|scaffold\|promote`, `config set prompt-sort ...` | **BUILD** (6.1 before the switch; 6.3 and 6.4 after it, Q9, with the not-yet-built stub of §5.2 until then; today `config set prompt-sort` fails `unknown question`) |
| `src/Evidence` (was `skills/ui-evidence/src/judge-exec.ts`) | `ui-element-repair`, `visual-critique` | questions exist; adapter §3.3 G-c |
| `src/BugFix` (was `skills/bugFixOrchestrator/src/deps.ts:63-74`) | `resolution-check`, `why` | questions exist; adapter §4.3 |
| `src/Sindri/Aw.Sindri.Infrastructure/Judge/JudgeCliChoice.cs:24` | `judge choose`; `AW_SINDRI_JUDGE` else PATH | covered (C# only) |
| `scripts/install-{done-gate,scope-gate,trait-check,wake-gating,external-write-guard}.sh` | `command -v ${AW_JUDGE_BIN:-judge}`; refuse when missing | works with any `judge` on PATH; the message text names `install-judge.sh` |
| hook-written files (`judge/sessions/<sid>.json`, `judge/outbox/<sid>.jsonl`, `judge/teammate-names.jsonl`) | written by bash hooks, not the judge | frozen (§10) |

Judge verbs in C#: `health`, `brief save|get|map-by-name|map-by-dispatch|set-agent-id`, `why`, `undo`, `approve`,
`config get|set`, and nine questions (`QuestionCatalog.cs:11-19`). Exits: 0 ok, 1 error, 2 escalated (and `ask-check`
continue), 64 usage. The rows marked BUILD are the port of §5.2.

### 5.2 The port: every TS judge verb has a C# verb

**Decision (Q2, owner 2026-10-23): "port all of Judge hooks. parts of Judge should already be ported for the line recipe i
think".** Nothing of the TS judge is cut. `prompt-sort`, `trait-check`, `label`, `adjudicate`, `eval` and `eval import-turns`
all get C# versions (the hooks before the switch, the by-hand verbs after it: Q9 below); the `prompt-sort` and `trait-check` hooks stay installed (no unmerge, no hook
deletion), and the `/judge` skill keeps its `label`, `adjudicate` and `prompt-sort` text. The owner's second sentence is
right and is checked here: the part of Judge the line recipe needs is already in C#. The shipped recipes use one judge
verb, `judge choose` (`step.judge.choose`: docs-sync, evidence, pr-babysitter, reproduce and scoping call it through
`JudgeCliChoice`, `src/Sindri/Aw.Sindri.Infrastructure/Judge/JudgeCliChoice.cs:24`); no recipe or Sindri code calls
`prompt-sort`, `trait-check`, `label`, `adjudicate` or `eval` (grep of `recipes/sindri` and `src/Sindri` for
`IJudgeChoice`, `AW_SINDRI_JUDGE` and `step.judge` finds only `choose`). `choose` has no TS version.

**Decision (Q9, owner 2026-10-23, "Yes, land later").** The by-hand verbs (`label`, `adjudicate`, `eval` with `import-turns`,
and the `prompt-sort` eval side; units 6.3 and 6.4) may land after the switch. The switch PR (P3) does not wait for them;
they are the post-switch wave (§12 P5, §13 wave 5). What lands before the switch is unchanged by Q2: the hooks
(`prompt-sort` and `trait-check`, units 6.1 and 6.2), 6.0's contract and everything the recipes use (`choose`, already in C#).

**The not-yet-built stub.** Until 6.3 and 6.4 land, these verbs answer the same way:

| Verbs | Stdout | Stderr (exactly one line) | Exit |
|---|---|---|---|
| `label` (any subcommand, or none), `adjudicate`, `eval` (including `eval import-turns`), `prompt-sort why\|import\|outcomes\|adjudicate\|eval\|promote` | empty | `judge: <verb> is not in this build yet` | 1 |

`<verb>` is the first argument, plus the second only for `eval import-turns` and for each stubbed `prompt-sort <sub>` (so `label` alone, `label status` and `eval <question>` print `label` and `eval`), so
`judge: label is not in this build yet`, `judge: adjudicate is not in this build yet`, `judge: eval is not in this build yet`,
`judge: eval import-turns is not in this build yet` and `judge: prompt-sort why is not in this build yet`. Not stubbed, because
6.1 builds them before the switch: `prompt-sort` with no subcommand (the hook path), `prompt-sort scaffold` and
`config set prompt-sort`. The stub validates no argument, opens no database, reads no stdin, writes no file or config, and
calls no provider. The `--providers` flag is stripped first by `ProvidersFlag.Take`, as for every verb (a flag with no value exits 64); the list itself is not validated, because only a question reads it (`CommandTable.Question`), so `judge label status --providers bogus` prints the stub line. Exit 1 is the judge's own "error"; no hook branches on it, because no hook calls these verbs
(`config/hooks/prompt-sort.sh` calls only the no-subcommand form). It is a shared helper that prints the line, called from one handler per stubbed verb family (`label`, `adjudicate`, `eval`, and `prompt-sort` with a stubbed subcommand); 6.0 routes them all, so 6.3 and 6.4 replace handler bodies and delete no route and no `CommandTable` line. Each stubbed first word is added to `_routed` and gets an arm before
the `rest.Length != 1` arm of `CommandTable` (a word missing from `_routed` falls through to `unknown question`, exit 64; one
behind the `rest.Length != 1` arm gets a decision-id usage error). An unknown
`prompt-sort <sub>` (not `scaffold`, not a stubbed one) answers with the TS text plus the C# prefix, `judge: unknown prompt-sort subcommand: <sub>`,
on stderr with exit 1, from 6.0's `prompt-sort` arm, which its test pins (string, stream and exit). A mistyped `eval import-turn` prints the one-word `eval` line. `--help` or `-h` on a stubbed verb is no special case: it prints the stub line. Where the stub is written down:

- **`docs/csharp/judge.md`:** a "Not yet built" table (the verbs, the line, the exit, the unit that lands each). The hook table
  is unchanged, since no hook calls a stubbed verb.
- **Tests (6.0 pins the line and the exit per verb string; 6.5 adds the hook-surface cases):** one case per verb string
  asserting the line, empty stdout and exit 1, and a bash test, `scripts/tests/judge-stub-invocations.test.sh` (owned by 6.0, and the list of stubbed verbs in it loses an entry in the PR that lands that verb), that no non-test file (not `scripts/tests/`, not its own positive-control fixture) under `config/hooks/` or `config/lib/`, `scripts/`, or `skills/` (except `skills/judge/`), no recipe under `recipes/` and nothing under `src/Sindri` invokes a stubbed verb. It matches invocations of the
  judge binary (`judge`, `command judge`, and the variable forms `$JUDGE_BIN`, `${JUDGE_BIN}`, `$AW_JUDGE_BIN` and `${AW_JUDGE_BIN:-judge}`, quoted or not, then the verb as a word, with `prompt-sort` needing one of the
  stubbed subcommands); C# callers (`src/Evidence`, `src/BugFix`, `src/Sindri`) get a second pattern for a `"judge"` argv element followed by a stubbed word, accepting false negatives (wrapper forms such as `exec judge`, `env judge` or `xargs judge` among them), since the real-hook cases are the actual backstop for hooks and the P2 and P4 exits for the rest; `skills/judge/` is guarded only by its marker; not bare words (`eval` and `label` are shell words in `config/lib/sys-pkg.sh:71,82,130` and in test
  scripts), and it carries a positive control: a fixture line that must match.
- **`/judge` skill text (edited in the switch PR, unit 3.1):** each place that names a stubbed verb (the four "Labels
  (automatic)" steps and `label set`, `prompt-sort why` and `promote`) carries the line "Not in this build yet: it prints
  `judge: <verb> is not in this build yet` and exits 1. Do not retry it or look for a workaround; tell the user it lands in a
  later update." The skill's advice to keep scaffolds off "until `judge prompt-sort promote` says go" (`SKILL.md:82-85`) is reworded
  to "leave scaffolds off; `promote` is not in this build yet". The PR that lands a verb deletes its route,
  its stub test line, its row of the "Not yet built" table and its skill marker. The skill is linked from the checkout and the
  binary from the install, so the two disagree from the pull until the flip's `setup.sh` (the TS judge still answers the verbs
  then, §2 step 4) and again after the flip until P5 lands each verb; the marker is the only claim the skill makes about it. No
  workaround exists after the flip, since the TS judge is removed; the stub line is exact because the owner set it.

**What C# already has to build on** (`src/Judge`, read on `origin/main` b75434304):

- The evaluator and the nine questions (`QuestionCatalog.cs:11-19`), the provider chain (rules, `claude-cli`, `codex-cli`,
  `cursor-cli`, Jev), the `--providers` allowlist and `AW_JUDGE_PROVIDERS` (`ProvidersFlag.cs`, `ProvidersVariable.cs`), the
  redactor over `Aw.Redaction`, `IProcessRunner` and the `PATH` locator, the secret source (environment, then keychain), and
  the SQLite stores with a migration runner (`Migrations/0001_judge.sql`, `0002_approved_at.sql`).
- The `promptSort` block of `config.json` is already parsed and validated (`Core/Config/PromptSortConfig.cs`: `enabled`,
  `budgetMs` 200 to 1050, `cooldownPrompts`, `scaffolds` for `brief`, `bugfix`, `ui-evidence`, `plan-first`), and its doc
  comment says "J2 reads them". Nothing reads it.
- `docs/csharp/judge.md:368` already lists the `prompt-sort.sh` hook row as "J2".
- The Jev wire request (`Providers/JevRequest.cs`) carries a map of questions, but the adapter asks one, `decision`; the
  prompt sorter's one batched request of eleven axes needs a batched call (unit 6.1; whether the wire accepts it as is is
  **not verified**).
- YamlDotNet is pinned (`Directory.Packages.props:44`) for the trait files.
- **Not there:** the tables `eval_items`, `labels`, `prompt_sort_axes` and `prompt_sort_runs` (the TS database has them,
  `judge/src/db.ts`; the C# migrations have `decisions`, `failures`, `briefs`, `decision_details` only); any transcript
  reader; a model and effort option on `ClaudeCliProvider` (**not verified**; the TS adjudicator and `prompt-sort
  adjudicate` ask `claude -p --model opus --effort high`).

**The verb map.** TS source is `judge/src/**` (`cli.ts` is the dispatch); sizes are TS lines, a measure of work and not of
the C# code. Exits: the judge's own 0 ok, 1 error, 2 escalated, 64 usage, except where the row says.

| TS verb | What it does, inputs and outputs | Called by | C# today | What to build (unit) |
|---|---|---|---|---|
| `<question>` (`wake-gate`, `brief-scope`, `ask-check`, `rule-check`, `resolution-check`, `turn-progress`, `ui-element-repair`, `visual-critique`) | stdin JSON, one envelope on stdout | the hooks, `src/Evidence`, `src/BugFix` | present, plus C#-only `choose` | nothing |
| `undo`, `why`, `approve`, `health`, `config get\|set`, `brief save\|get\|map-by-name\|map-by-dispatch\|set-agent-id` | as `docs/csharp/judge.md` | hooks, statusline, the skill | present | nothing |
| `prompt-sort` (no subcommand; the hook path) | stdin `{prompt, sessionId}`; skips machine text, slash commands and confirmations (`tier1.ts`); one batched Jev request for 11 axes (nine yes/no, `complexity`, `ambiguity`), each axis blended with a keyword heuristic (`blend.ts`, `heuristics.ts`); plans scaffolds (`scaffolds.ts`, four, each behind `promptSort.scaffolds`, all ship OFF, so by default it records and prints nothing); records one `prompt-sort` decision, `prompt_sort_axes` rows and a `prompt_sort_runs` row; session state `judge/sessions/<sid>.sort.json`; stdout `{context?}`; always exit 0, fails open, hard stop at the budget (`AW_PROMPT_SORT_BUDGET_MS`, hook default 1500) | `config/hooks/prompt-sort.sh` (UserPromptSubmit) | **absent** (config block only) | 6.1: Core `PromptSort` (axes, tier-1 skip, heuristics, blend, scaffold plan), the stores' SQL and the session-state file in Infrastructure, the Host handler; the migration `0003_prompt_sort.sql`, the batched Jev call and the route are 6.0's; reads `promptSort` config; honours `--providers` (no Jev when `jev` is excluded: heuristics only). About 800 of the 1,413 TS lines in `prompt-sort/` (axes, blend, heuristics, run, scaffolds, scaffold-switch, session-state, sort, store, tier1); the other 600 or so are the eval side, 6.4's |
| `prompt-sort scaffold <name> on\|off`; `config set prompt-sort enabled <bool>` | writes the scaffold switch or `promptSort.enabled` in `config.json` | `/judge` skill | absent; `config set prompt-sort` fails `unknown question` | 6.1: both write the `promptSort` block (it must keep a file the config parser accepts) |
| `prompt-sort why <decision-id>` | prints the decision, its details, its axes and its run | `/judge` skill | absent; stub until 6.4 | 6.4, after the switch (Q9) |
| `prompt-sort import [--since 14d]`, `outcomes`, `adjudicate [--limit 80]`, `eval`, `promote [--apply]` | the sorter's eval side: sort items from stored decisions, outcome labels from the transcripts, a stronger-model adjudicator (needs the `claude` CLI; exit 1 without it), an eval report written to `judge/evals/prompt-sort-<stamp>.json` and `.md`, and a promotion verdict per scaffold that `--apply` writes into `config.json` | `/judge` skill, by hand | absent; stub until 6.4 | 6.4, after the switch (Q9); reads 6.1's stores and 6.3's label and eval code |
| `trait-check --edit --file <p>... \| --stop [--base ref] \| --scan [--path p...] [--trait n...]`, with `--budget-ms`, `--min-p`, `--concurrency` | judges the repo's traits (`<repo>/.agents/traits/*.yaml`, overlaid by `<state>/traits/<repo-name>/*.yaml`; `paths` globs scope a trait without a model call; severity `block` or `warn`) against the edited files' diff (edit), the branch diff against `origin/main` (stop) or whole files (scan), in batched Jev requests. Exit 0 nothing to report or skipped, **2 findings** (edit and stop: stderr; scan: JSON lines on stdout), **3 Jev unavailable**, 64 usage. Appends `judge/trait-check/log.jsonl` | `config/hooks/trait-check.sh` (PostToolUse edit, Stop; opt-in lever `install-trait-check.sh`); `--scan` by hand | **absent** (an unknown question, exit 64) | 6.2: Core `Traits` (the trait file, glob match, the diff and request grouping), Infrastructure (git through `IProcessRunner`, the log file; the Jev batch is 6.0's), the Host handler; no database; honours `--providers`. 525 TS lines |
| `label import [--question q] [--since 14d]`, `label outcomes`, `label set <itemId> <label\|skip>`, `label status` | `import` turns stored decisions into eval items; `outcomes` (free) labels `ask-check` items from the next real prompt in the Claude transcripts (`~/.claude/projects`); `set` is a manual override; `status` prints coverage and how often the outcome and adjudicator labels agree. JSON on stdout | `/judge` skill, by hand | absent; stub until 6.3 | 6.3, after the switch (Q9): the transcript reader, the four commands, their SQL and the the eval-label migration (the next free number, `0004` while 0003 is the last on main; `eval_items`, `labels`) |
| `adjudicate <question> [--limit 60]` | asks `claude -p --model opus --effort high` three times per item, stores a label only on a 2-of-3 in-enum majority, never shows the model the judge's own decision; refuses with exit 64 when `--providers` excludes `claude-cli`, exit 1 when the CLI is not on `PATH`; summary JSON on stdout | `/judge` skill, by hand | absent; stub until 6.3 | 6.3, after the switch (Q9); a model and effort option on the Claude CLI provider |
| `eval <question> [--provider jev] [--variant v] [--labels outcome\|adjudicator\|override\|any] [--collapse x] [--record f] [--replay f]` | re-scores one provider against the labels, calling the provider directly (no chain, no `decisions` rows); `--record` and `--replay` re-run a report change without network; writes a report under `judge/evals/`; exit 64 when the provider is excluded by the allowlist, 1 for an unknown provider or bad `--labels` | `/judge` skill, by hand | absent; stub until 6.3 | 6.3, after the switch (Q9) |
| `eval import-turns <transcript.jsonl>... [--since 14d]` | builds per-turn eval items from the Edit, Write and MultiEdit calls in transcripts, labelled by line survival and then the next real prompt; does not touch `decisions` | by hand | absent; stub until 6.3 | 6.3, after the switch (Q9) |

The judge units (6.1 and 6.2 before the switch, 6.3 and 6.4 after it, over 6.0's contracts) reimplement in C#; the TS package says what each verb does, never how (`.agents/rules/csharp.md`,
"Porting a TS package"). The contracts are the C# tests, written red first by the Judge contract unit 6.0 (§12 P0), and
the two hook rows of §10. The prompt sorter's scaffolds stay OFF by default (shadow mode, as TS shipped them); turning one
on stays the owner's call through `promote`.

**Port details the units keep.**

- **The sorter's session file is a hook contract.** `config/hooks/done-gate.sh:162-175` reads `requirements.uiEvidence` from
  `judge/sessions/<sid>.sort.json` and then rewrites the file with `jq del`. So the file's shape is frozen (§10), 6.1 writes
  it preserving keys it does not know, through a temp file and a rename, and a file a TS sorter left behind reads as is (the
  24 h sweep expires it).
- **Redaction.** The TS `prompt-sort` redacts the prompt (`redactSecrets`); the TS `trait-check` sends diffs and whole files
  to Jev with no redaction call. The C# versions put every text sent to a provider (the prompt, the trait diffs, the
  adjudicator's transcript turns) and every recorded eval file (`--record`, the `judge/evals/` reports, the trait-check log)
  through `Aw.Redaction`, with a test per path. The TS verbs also redact at rest (`turn-import.ts:135` stores `redactDeep(input)` into `eval_items`, `prompt-sort/store.ts:44` redacts what it stores), so the C# writers of `eval_items`, `prompt_sort_runs` and the decision details redact string values and object keys before capping, and port the TS pattern set at minimum, with a test per writer. They write recorded files owner-only where the OS has file modes.
- **Exit codes.** `trait-check` uses 2 for findings and 3 for Jev unavailable, which the judge's table reads as "escalated"
  and nothing; `judge.md` documents them as `trait-check`'s own (unit 6.5), and the hook branches on them as it does today.
- **Scaffold switches survive only by hand.** The removal script deletes `config.json`, so the owner's `scaffold <id> on`
  choices are logged and re-entered (§8 step 2); every scaffold ships OFF again.

**What the installers and the skill keep.** `scripts/install-judge-hooks.sh` (the renamed hook half of
`install-judge.sh`, §7.4) keeps installing the `prompt-sort` UserPromptSubmit hook and `AW_NO_PROMPT_SORT` keeps its
meaning; `scripts/install-trait-check.sh` stays an opt-in lever; `config/hooks/prompt-sort.sh`, `trait-check.sh` and their
install tests stay (retargeted, §9). No hook registration is unmerged and no hook copy is deleted. The `/judge` skill keeps
its `label`, `adjudicate`, `prompt-sort` and `config set prompt-sort` sections, with the not-yet-built marker on the stubbed verbs (above); the switch PR edits only what names the
TS build: the sentence that sends undo counts to "the scorer's Judge section" (`skills/judge/SKILL.md:17`), the pointer to
`scripts/install-judge.sh` and the pointer to `judge/src/questions/` (it becomes `src/Judge/Aw.Judge.Core/Questions/`),
and the `budgetMs` range, which the skill gives as 200-1400 where the C# config accepts 200 to 1050.

### 5.3 Judge state

The C# judge uses `$AW_STATE_DIR/judge/decisions.sqlite`, the same path the TS judge writes. The C# migration runner
refuses a file that has tables but no matching `schema_version` (AW-DATA-009, defined in `src/Aw.Kernel.Data/DataErrors.cs`, raised in `src/Aw.Kernel.Data/MigrationChecks.cs`; the TS
file has `decisions`, `failures`, `briefs`, `decision_details`, `eval_items`, `labels`, `prompt_sort_*`). Every hook would
see judge exit 1 and fail open. So the switch **deletes** `judge/decisions.sqlite{,-shm,-wal}` and `judge/config.json`
(one-way, no archive) and **keeps** `judge/sessions/`, `judge/outbox/` and `judge/teammate-names.jsonl`, plus the files the
ported verbs write outside the database (`judge/evals/` reports, `judge/trait-check/log.jsonl`; §8.1). The deletion runs
**before** the C# `judge` is linked (§8 step 5, then the link at step 6), so no hook ever sees the C# `judge` over the TS database.
The only guard beyond that order is the generic migration refusal (AW-DATA-009); the readiness spec's "refuse to start,
excluding the three hook files" check is **not found in `src`** (verified), so it is not a net this spec relies on.

With the port (§5.2) the C# database gains `prompt_sort_axes` and `prompt_sort_runs` through migration `0003` before the
switch, and `eval_items` and `labels` through the next migration when 6.3 lands, so a C# file always carries a `schema_version` and the TS-shape test of §8 (tables, no
`schema_version`) stays right. The TS rows of those tables are **not** migrated, like every other TS state: the labels
re-derive from the transcripts once 6.3 lands (`label import`, `label outcomes` and `eval import-turns` are free; the transcripts stay on disk), the eval history starts
empty, and re-running `adjudicate` costs Opus tokens (about three runs of 3k tokens per item, `skills/judge/SKILL.md`),
which the owner chooses to spend or not. The prompt sorter's session files (`judge/sessions/<sid>.sort.json`) live in a kept
directory, so a pending requirement carries over.

The `scorer` read of `judge/decisions.sqlite` (`scorer/src/live-judge.ts`) goes with the scorer.

## 6. Sindri: the TS commands against the C# app

### 6.1 What must exist in C# before the switch (KEEP-needed)

| TS verb | C# today | Needed |
|---|---|---|
| `profile init`, `validate`, `approve` | ported (`CommandTable.Groups.cs:11-13`); no `--ring0/--plans/--dir/--force` | docs change (`docs/sindri/README.md:263`); nothing to build |
| `doctor` | ported, new checks | per-repo `pre-commit:<repo>` line (§6.2) |
| `index build\|status\|query\|setup` | service `code-index-main-move` (no verb by design, code-index spec CI1); `code-index status\|symbols\|callers\|callees` | message text only; the graphify pin moves (§6.3) |
| `repo add`, `repo onboard`, `repo status [--nudge]` | ported; `onboard` hook slot is `NoSecretCheckHook` (installs nothing) | the real `IRepoHooks` (§6.2) |
| `ledger` | ported (`--after`, no `--since`) | none |
| `scrub` (stdin, `--staged`, `--install-pre-commit [--repo]`) | **none** (`Aw.Redaction` is the engine; no verb) | build it (§6.2) |

### 6.2 `scrub` and the pre-commit hook (the blocker)

**The hazard.** Repos onboarded by TS have `.git/hooks/pre-commit` with marker `# sindri-pre-commit v2` (or legacy
`# sindri-scrub-pre-commit v1`) and an absolute `SINDRI='<~/.local/bin/sindri>'` baked in (`sindri/src/scrub/commands.ts:31-65`).
It runs `"$SINDRI" scrub --staged || exit 1`, then `"$SINDRI" shape --record --staged || true`. Once `sindri` is the C#
binary and has no `scrub`, the verb is unknown, exits 2, and the hook refuses **every commit** in each onboarded repo. With
`scrub` built (item 1), the v2 and v1 hooks work unchanged: `scrub --staged` answers them, and the `shape --record` line fails
as an unknown verb but is ignored (`|| true`), costing one stderr line per commit. So a repo the removal script cannot reach
is noisy, not blocked.

**Build in C# (a slice in `src/Sindri`, with `Aw.Redaction` as the engine):**
1. `sindri scrub` reads stdin and writes the redacted text; `sindri scrub --staged` reads `git diff --cached --unified=0`
   (added lines only, as `parseAddedLines` did, `commands.ts:80-120`) and exits 1 on a finding; `sindri scrub
   --install-pre-commit [--repo PATH] [--bin PATH]` installs the hook (`--bin` is the path baked into it; the default is the
   running binary's own, which is why the cleanup script passes it). Keep the error codes: `SND-SCRUB-002` (a staged secret, exit 1;
   `scripts/sindri-guard-proof.sh` greps it), `SND-SCRUB-003` (a foreign hook is present), `SND-SCRUB-004` (not a repo),
   `SND-SCRUB-005` (git diff failed). It resolves the hook path with `git rev-parse --git-path hooks/pre-commit` and respects `core.hooksPath`.
2. A real `IRepoHooks` replaces `NoSecretCheckHook`; `repo onboard` calls it only after approval, as before, and the
   `setup:repo-hook` doctor warning (`SetupDoctor.cs:24`) turns into a real per-repo check.
3. **Hook body v3** (marker `# sindri-pre-commit v3`): no `shape` line; the **stable** path `~/.local/bin/sindri` (the
   wrapper of §7.1, never a stamped build directory, which pruning deletes) baked in at install; fails closed when the
   binary is missing (exit 1, `sindri-scrub: ... not found`), as v2 did, with the fix text naming `./setup.sh` (not the
   deleted `install-sindri.sh`). `isSindriHook`
   recognises v2 and v1 so the installer replaces them.
4. `scripts/sindri-guard-proof.sh` keeps working against the C# verb (it expects `SND-SCRUB-002`).

**Order.** The removal script (§8) reinstalls the v3 hook in every profile repo **before** it deletes TS state. The TS
profile is the only list of those repos; it is resolved as the TS loader does (`sindri/src/profile/load.ts:32-37`: `--profile`,
else `AW_PROFILE_DIR`, else `$AW_STATE_DIR/profile`, a directory or a link), and each repo is a `repos/<name>.yaml` with an
absolute `path` (`sindri/src/profile/schema.ts:241`). After its deletion they cannot be found, so the script also prints the
list (name and path) in its report (§8 step 10).

### 6.3 Other Sindri changes

- **graphify pin.** It lives in `sindri/package.json` (`sindri.graphifyPin`, `graphifyPinDate`) and is read by
  `scripts/install-deps.sh:106-111`, `scripts/code-index-lock.sh:33,128-131` (rule `CODEIDX-LOCK-007`), the tests
  `scripts/tests/install-deps.test.sh:39-40,262` and `code-index-lock.test.sh:124-126`, and a comment in
  `GraphifyOptions.cs:4`. It moves before `sindri/` is deleted. Recommended: `binaries.lock` style (one pinned row), with
  `install-deps.sh` reading it; `context/code-index.lock` becomes consistent with it and rule 007 changes to compare those two.
  Alternative: a small `context/graphify.pin` file.
- **Nudge text.** `config/lib/tests/sindri-cs-hook-surface.test.sh:70` pins `sindri: <repo> is not onboarded (no secret
  scan, shape signals or index); run: sindri repo onboard`.
- **launchd.** The six plists (`config/launchd/com.agentic-workflow.{scorer,scorer-audit,sindri-observe,sindri-index-quick,sindri-index,sindri-evolve}.plist`)
  are removed with `config/launchd/`. The code index runs as the `code-index-main-move` service; nothing replaces the rest.
- **`sindri live`.** Not built (§14).

**Decision (Q3, taken as recommended and confirmed by the owner in rev 9):** amend the frozen
hook-contract line to drop "shape signals" (a cut feature), with the test updated in the same PR. Keeping the text would
advertise a feature that no longer exists.

## 7. The install

### 7.1 One C# install path

A new script, `scripts/install-cs.sh`, is the only install of the toolkit's binaries. `setup.sh` and the provider
installers call it. It follows `scripts/install-meta.sh:101-140` and `scripts/sindri-app.sh`:

1. Pin and install the .NET SDK from `global.json` into `$DOTNET_ROOT` when missing; fetch the pinned `temporal` binaries
   (`scripts/binaries-lock.sh fetch`).
2. Copy the needed source tree to a scratch copy (the same set each `aot-smoke/run.sh` copies), under the box-wide heavy
   lock, and publish into `$AW_STATE_DIR/bin/cs/<stamp>/` (its own subdirectory: `bin/` also holds `aw-meta`, and the old `bin/sindri-app/<stamp>/` is retired by §8), stamped with the commit (`SourceRevisionId`, `AwBuildTime`). Like `sindri-app.sh` today, it stages the shipped recipes and runs their eval gate, and it never replaces or prunes a build a running `serve` executes (it prints `run sindri stop, then re-run` instead):

| Binary | Project | Publish | Notes |
|---|---|---|---|
| `sindri` | `src/Sindri/Aw.Sindri.Host` | framework-dependent | Temporal worker cannot be trimmed (README X54) |
| `sindri-ui` | `src/Sindri/Aw.Sindri.Ui.Desktop` | NativeAOT | found next to `sindri` |
| `judge` | `src/Judge/Aw.Judge.Host` | NativeAOT, ~11 MB | with `libe_sqlite3.*` beside it |
| `aw-packets` | `src/Packets/Aw.Packets.App` | NativeAOT | |
| `bugfix-state` | `src/BugFix/Aw.BugFix.Host` | NativeAOT | no SQLite |
| `ui-evidence` | `src/Evidence/Aw.Evidence.Host` | framework-dependent | Sindri's evidence binary (§3.1): published with `sindri` and `sindri-ui`, same stamp; Playwright has no AOT (EV L562-567) |
| `aw-readiness` | `src/Readiness/Aw.Readiness.App` | NativeAOT | |

   `aw-meta` stays opt-in through `--with-meta` and keeps `install-meta.sh`.
3. Verify each binary answers a no-argument or read-only call ("install didn't take" check as `install-meta.sh` does; only `ui-evidence` gets a `version` verb, unit 1.4, and `judge`, `bugfix-state`, `aw-packets` and `aw-readiness` have no `version` or `help` route today, so unit 5.1 pins one state-free known-good call per binary in its test: `judge` runs with a scratch `AW_STATE_DIR`, never the real one, which at `--publish` time still holds the TS-shaped database and would answer AW-DATA-009; `sindri-ui` is checked by file presence only, since any argument opens a window); keep the newest
   two stamped dirs. `--publish` leaves `current` alone; `--link` switches the symlink `bin/cs/current` atomically and
   writes the wrappers. The running-serve guard names build directories, not the symlink.
4. **Link** (a separate step, §8 for where it runs): write the wrappers below to `~/.local/bin/{sindri,judge,bugfix-state,ui-evidence,aw-packets,aw-readiness}`
   (`CLAUDE_LOCAL_BIN`), each replacing any TS wrapper of the same name atomically (write a temp file, then `mv`). `judge`
   and `sindri` must be linked **before** the lever installers run, because each refuses to install without `judge` on PATH
   (`scripts/install-done-gate.sh:28,46`, `install-wake-gating.sh:68`, and the others in §5.1).
5. `ui-evidence install-browser`, once per box (it needs the .NET runtime, not Node; it downloads Chromium, so it needs
   network, and fails with a one-line message offline; the SDK and `temporal` downloads likewise). An offline browser download is a warning with the one command to re-run,
   not an install failure; the SDK and `temporal` are required and fail it.

Flags: `install-cs.sh [--publish | --link] [--rebuild] [--dry-run]`; with neither of `--publish` and `--link` it does both.
`--publish` is A7a's `--publish-only`. Exit 0 done or current, 1 failed (nothing on PATH changed by a failed `--publish`),
2 usage. Output lines start `install-cs:`, no colour.

Every `~/.local/bin` entry is a **small wrapper**, never a bare link (a link would break when pruning removes a stamp, and
NativeAOT finding `libe_sqlite3` through a symlink on macOS is unverified, so P2/P4 prove the resolved-path run). The wrapper
exports `DOTNET_ROOT` and execs `bin/cs/current/<name>` by its resolved path. The `sindri` wrapper also exports
`AW_SINDRI_JUDGE=<current>/judge` and `AW_PACKETS_BIN=<current>/aw-packets` and `AW_UI_EVIDENCE_BIN=<current>/ui-evidence` (the first two are what `sindri-app.sh:22-23` injected), then `exec`s the real `sindri` by its
resolved path, so bare `sindri` finds `sindri-ui` next to it (`UiLauncher.Locate` is called with `AppContext.BaseDirectory`,
`CommandTable.cs:241`, which is the real binary's directory when the wrapper execs it by its resolved path). The wrapper does not publish; publishing is the installer's job, run by `setup.sh` and by hand as `scripts/install-cs.sh --publish` (§7.3).

### 7.2 What `setup.sh` and the providers change

| Where | Change |
|---|---|
| `setup.sh:254-260` | delete the `install-scorer.sh` call |
| `setup.sh:256-259` | `install-judge.sh --build-only` becomes the `install-cs.sh` call |
| `setup.sh:262-268` | the `--with-sindri` block (npm build, wrapper, four launchd jobs) is deleted; the C# install is not optional |
| `setup.sh:269-283` | the uv step for the TS index goes; `install-deps.sh` (`:287`) already installs uv and graphify |
| `setup.sh:209-216` | the native build-tools check exists for `better-sqlite3`; it goes, `install-deps.sh` installs the NativeAOT toolchain |
| `setup.sh:293`; `providers/lib.sh:236-262` | delete `aw_build_skill_packages` and `AW_SKILL_PACKAGES`; delete the case in `providers/tests/install.test.sh:293-305` |
| `setup.sh:15,697`, messages naming `sindri index setup` | text |
| `setup.sh:622-635,717`; `config/lib/setup-onyx-step.sh:6`; `scripts/install-onyx.sh` | `sindri-app` becomes `sindri` |
| `providers/{claude,codex,cursor}/install.sh` | hook installs move out of `install-judge.sh` and `install-sindri.sh` (§7.4) |
| `providers/lib.sh:411` | drop `install-live-pane` from the lever list (Q4) |
| `setup.sh:229,235` | remove `reflect` and `correct` from `MANAGED_SKILLS`; add them to `DEPRECATED_SKILLS`; the skill-count messages follow |

All edits in this table are made in the switch PR (P3), not in P1. The `install-cs.sh` publish needs the NativeAOT toolchain
that `install-deps.sh` installs (`setup.sh:287` today, after the old build step), so the PR moves the `install-deps.sh` call
above the §8 sequence.

`--with-sindri` is no longer a switch. **Decision (Q8, owner 2026-10-23, "all Recommended"):** it is accepted as a no-op that
prints one line ("the C# Sindri is always installed"), for one release, so owner scripts do not break.

### 7.3 Order against setup-once A7a/A7b

Setup-once §9 (A7a) has `setup.sh` run `sindri-app --publish-only` first, and §5.3 and SO-D5 define how shipped recipes are
embedded and approved at publish. A7a is **not on main** (`scripts/sindri-app.sh:8-12` has the flag and its header says `setup.sh` calls it, but `setup.sh`
has no call; install.md §1.1 row 33). This spec reuses A7a's order and its exit codes (0 published or current, 1 failed; a failed
publish stops `setup.sh` with exit 1; re-running is safe by the stamp check and never replaces a running `serve`). The
`install-cs.sh` publish **is** A7a's publish, extended to the seven binaries, and the TS builds it ordered itself before no
longer exist. Whichever of A7a and the cutover's switch PR (P3) merges first owns the `setup.sh` call; the other rebases. The
Onyx default (`--no-onyx` today, `--with-onyx` after A7b) is not decided here.

### 7.4 The hook installers keep their hooks

The hooks stay bash and stay installed (`judge-health` for all providers; `prompt-sort`; `sindri-nudge`; the opt-in levers, `trait-check` among them). What goes is
the TS half of the two installers:

- `scripts/install-judge.sh`: drop the `npm install && npm run build` and the wrapper (`:35-57`, `package.json` guard
  `:41-44`). The hook half (`--hook-only`) is renamed `scripts/install-judge-hooks.sh`; its callers are
  `providers/claude/install.sh:185-189`, `providers/codex/install.sh:48-49`, `providers/cursor/install.sh:84-85`, and four
  tests (§9). It keeps installing the `prompt-sort` hook and `AW_NO_PROMPT_SORT` keeps its meaning; `install-trait-check.sh`
  stays the opt-in lever (Q2, §5.2). Nothing is unmerged and no hook copy is deleted.
- `scripts/install-sindri.sh`: drop channels, the wrapper, the four launchd jobs. The `--hook-only` nudge install (`:57-77`)
  moves to `scripts/install-sindri-hooks.sh`; callers `providers/claude/install.sh:191-196`, `codex/install.sh:50-52`,
  `cursor/install.sh:86-88`. With the C# install always present, the nudge is installed whenever the provider is (Q8 aside).

### 7.5 The `sindri-app` name

**Decision (Q5, owner 2026-10-23, "all Recommended"): `sindri-app` is retired at the switch.** The switch is one-way and the name
existed only for the side-by-side period; the removal script deletes the `~/.local/bin/sindri-app` link and
`$AW_STATE_DIR/bin/sindri-app/`, and `scripts/sindri-app.sh`'s publish logic moves to `scripts/install-cs.sh` (its run mode
is just `sindri`) and the old file goes. Every `sindri-app <verb>` in docs becomes `sindri <verb>` (§9). No alias is kept.

## 8. The removal script

`scripts/cutover-clean-ts.sh`, idempotent, with `--dry-run` (the `aw_dry` style; the `aw_remove_retired_dir` pattern of
`providers/lib.sh:391-406`). `setup.sh` calls it **after** `install-cs.sh --publish` has published and verified, and links in the middle of it; never
before. It removes by shape, never by name list alone, and **it runs on every `./setup.sh`**, so every delete is gated on
something only the TS install has: the `sindri/`, `scorer/` and `audit/` directories are TS-only (the C# home is
`$AW_STATE_DIR/sindri-app/`, and the C# profile lives under it, not at `$AW_STATE_DIR/profile`); `judge/decisions.sqlite`
and `judge/config.json` share their paths with the C# judge, so they are deleted only when the database has the TS shape
(tables but no `schema_version`). The test is the stamped C# `judge health` refusing the file with AW-DATA-009: that check
opens the file read-only and runs before any write (`src/Aw.Kernel.Data/MigrationChecks.cs`), so it never changes the file, and
it needs no SQLite tool in the script. On a flipped box a second run finds nothing, deletes nothing and
prints `cutover: nothing to remove`. In this order:

Order in `setup.sh` (one sequence, so no window has the C# `judge` over the TS database or `setup.sh` stopped half
switched): `install-cs.sh --publish` (nothing on PATH changes; a failure stops with the box untouched), then
`cutover-clean-ts.sh --pre-link` (steps 1 to 5), then `install-cs.sh --link` (step 6), then `cutover-clean-ts.sh --post-link`
(steps 6b to 10), then the lever installers. With neither flag the script runs all of its steps. Step 2 runs the C# `sindri`
by its stamped path, because nothing is linked yet; the cleanup script sets `DOTNET_ROOT` itself from the pinned location
`install-cs.sh` installs to (one shared `scripts/lib/cs-env.sh`), since it runs after `install-cs.sh` has exited.

| Step | Action | Why this position |
|---|---|---|
| 1 | Refuse (exit 1, **nothing changed yet**; the message names the job, says nothing was changed, and gives the retry) if a TS job is mid-run, or a C# serve runs from `$AW_STATE_DIR/bin/sindri-app/` (the pid in its `control.json`, alive, build dir in its command line; `sindri-app stop` first): on macOS a loaded `com.agentic-workflow.*` launchd job that `launchctl print` shows running; on any OS a process holding the TS ledger (`$AW_STATE_DIR/sindri/ledger.db`) open, asked of the file by `lsof` or `fuser` (whichever exists; when neither does, the script says it could not check and continues). No `sqlite3` CLI is assumed: the toolkit does not install one. The TS code writes no pid file (verified: no `control.json` in `sindri/src`), and there is no `pkill` or `pgrep -f` | first, so a refusal leaves the box exactly as it was |
| 2 | For every repo of the TS profile (`$AW_STATE_DIR/profile/repos/*.yaml`, or `AW_PROFILE_DIR`; §6.2), install hook v3 with the stamped C# `sindri scrub --install-pre-commit --repo <path>` (replaces markers v2 and v1). `--bin ~/.local/bin/sindri` makes the stable wrapper path the baked one, not the stamped path it runs from (§6.2 item 3); a repo that is gone or unreadable is reported, not fatal. Under `--dry-run` this step only prints `would install hook v3` (nothing is published then). Before it, the repo list (name, path), the judge config as `judge config set` lines, one `judge prompt-sort scaffold <id> on` line per scaffold that was on, the TS `judge label status` output when a TS `judge` is still on `PATH` (the label and eval counts that will be lost; the rows are not dumped), the `providers` and `promptSort` values as JSON (a `budgetMs` above 1050 is not accepted by the C# config, §5.2) and the text of `profile.yaml` and `repos/*.yaml` are written to the log (step 10's file), so a stop later loses nothing. The log is created first, owner-only (the file 0600, in a 0700 directory it creates, or alone when `logs/` already exists), and the profile text and config lines pass through the redaction patterns; if the log cannot be written the script stops with exit 1 and nothing changed | the TS profile is the only list; step 8 deletes it |
| 3 | macOS: `launchctl bootout gui/$(id -u)/<label>` and delete the six plists (`scorer`, `scorer-audit`, `sindri-observe`, `sindri-index-quick`, `sindri-index`, `sindri-evolve`) in `${AW_LAUNCH_AGENTS_DIR:-~/Library/LaunchAgents}`; Linux: print "remove any cron line you added by hand" | nothing is running when state goes |
| 4 | `git config --global --get init.templateDir`; unset only when it equals `$AW_STATE_DIR/sindri/git-template` | the template copy lives in the TS state |
| 5 | If `judge/decisions.sqlite` has the TS shape: delete `judge/config.json` first, then the database with `-shm` and `-wal` (the config values are already in the log; this order means a stop between the two leaves a database that is still detected, never a stranded config), immediately before step 6 | before the link: no C# `judge` ever sees it (§5.3) |
| 6 | `install-cs.sh --link`: the C# wrappers replace the TS `judge` and `sindri` in `~/.local/bin` atomically | the C# state is clean, hook v3 points at this path |
| 6b | First action of `--post-link`: a TS hook running in the gap between steps 5 and 6 can have recreated a TS-shaped database, so test the shape again, delete and retry once; then run `judge health`. The gate fails (exit 3) only on AW-DATA-009 or a `judge` that does not run; `degraded` or `down` for another reason is printed as a warning, not a failure | catches the one window the order leaves |
| 7 | Remove the remaining TS wrappers when the file matches the TS shape (`exec <node> .../dist/cli.js`; `install-sindri.sh:97` refuses a symlink target): `scorer`, `sindri-next`, and the `sindri-app` link with `$AW_STATE_DIR/bin/sindri-app/`. Anything else (for example `sindri-view`, which no script in the repo creates) is left alone and reported | the links of step 6 already replaced `judge` and `sindri` |
| 8 | Back up and remove TS state: `$AW_STATE_DIR/sindri/` (ledger, index, channels, git-template, spool, logs), `$AW_STATE_DIR/profile` (a link is unlinked, never followed or recursed into; any delete whose resolved path is outside `$AW_STATE_DIR` is refused), `$AW_STATE_DIR/scorer/`, `$AW_STATE_DIR/audit/`. Bug-fix state (`<repo-slug>/bugfix/`) is kept: the C# `bugfix-state` and the TS helper lanes still run share that layout (#1104) | one-way, but every removed state path is moved to `$AW_STATE_DIR/backups/cutover-<stamp>/<its path under the state dir>`, not deleted (#1104). Every removal is guarded: `AW_STATE_DIR` must be absolute and non-empty (the guard of `aw_remove_retired_dir`, `providers/lib.sh:391-406`) |
| 9 | Run `claude plugin uninstall aw-live@agentic-workflow-mods --scope user` and remove the marketplace `agentic-workflow-mods`, best effort (inlined here, because `install-live-pane.sh` is deleted by the same PR); `aw_remove_retired_dir` on `<checkout>/{judge,scorer,sindri}/{dist,node_modules}` only (the checkout, never `$AW_STATE_DIR/judge/`, whose hook files are kept) | the mod's backend is gone; tracked content stays |
| 10 | Print, and write to `$AW_STATE_DIR/logs/cutover-<timestamp>.log`: what was removed, what was kept, what was left alone, the repos found (name and path), the judge config values that were deleted, and the **next steps** as copy-pasteable lines: `sindri profile init` then the typed `sindri profile approve`, then `sindri repo onboard <path>` for each listed repo, then `sindri doctor` (the C# profile is new and starts empty, so the session nudge says "not onboarded" in every repo until then; that is expected; no TS profile is migrated). `setup.sh` prints the same next-steps lines again (the commands and the repo list, never the config or profile text) as its last output, so the lever installers do not bury them. Printed only when something was removed, and again by the next run while the latest log has no `completed` line (written after step 10) | the owner needs the list, and the C# side starts unapproved |

### 8.1 Script contract

`cutover-clean-ts.sh [--pre-link | --post-link] [--dry-run]`: exit 0 done or nothing to do, 1 refused at step 1 (nothing
changed), 3 stopped partway or failed the step 6b gate (re-run `./setup.sh`; every step is idempotent, and `setup.sh` prints that line and the log path, exits 1, and runs no lever installer), 2 usage. The log path is printed on every exit. Lines start `cutover:`, no colour. A step that deletes prints one line with a count; with
`--dry-run` the same lines are prefixed `would`. The unique irreversible step is 5 and 8: before P4 the owner runs
`cutover-clean-ts.sh --dry-run` against the **real** state (it is read-only), and the report is read before the flip.

**Kept, never touched:** `judge/sessions/`, `judge/outbox/`, `judge/teammate-names.jsonl` (hook-written); `judge/evals/` and
`judge/trait-check/` (written by the ported verbs, §5.3);
`context-guard/`, `locks/` (heavy-job, qa-stack, `heavy-slots-cap`, `heavy-meta-reserve`), `hooks/`, `fleet/`;
`ui-evidence/runs/` (old TS summaries stay unread, EV L619-625); `<repo-slug>/{plans,reviews,proof,verification,design}`,
`digests/`, `toolkit/`, `logs/`, `meta/`, `bin/` (but `bin/sindri-app/`, step 7), `cache/`; `$AW_STATE_DIR/sindri-app` (the C# Sindri's home, never touched
by this script; its name is the state dir, not the command); per-repo MCP entries (`X-Sindri-Repo`; the C# app re-registers
per setup-once §5.4 when the port differs).

If step 5 is skipped, the generic migration refusal (AW-DATA-009) makes the C# `judge` exit 1 and every gate fail open; the
order above is what prevents it, and `judge health` in the P4 exit would show it.

## 9. The switch PR

One PR, before the flip (§12 P3; the flip is P4). Every item is a deletion or a text edit, plus the `setup.sh` and provider edits of §7.2 and §7.4.

**Directories:** `scorer/`, `judge/`, `sindri/` (121, 88 and 226 tracked files); the TS of `skills/ui-evidence/`
(`src/`, `package.json`, `scripts/` once unreferenced) and `skills/bugFixOrchestrator/` (`src/`, `package.json`); `config/launchd/`;
`mods/aw-live` and its marketplace entry (Q4); `skills/correct/`, `skills/reflect/`; `scripts/install-scorer.sh`,
`scripts/transcript-audit/`; the `ts-scorer` gate step of `skills/meta/examples/agentic-workflow.team.json:48-50`; `deps.yaml`
`binPaths` naming TS packages; `config/rules/web-app-rules.json`, `scripts/install-live-pane.sh`, `scripts/probe.sh:125`,
`skills/judge/SKILL.md:17` and `scripts/readiness-baseline.mjs` where they name TS packages (re-grep `scorer/`, `judge/`,
`sindri/`, `dist/cli.js` on the PR's tree); `.github/ci-tools/` and `scripts/ci/no-any.mjs` with `scripts/ci/no-any-baseline.json` (holds only
`{"judge/src/commands.ts":2}`).

**CI and gates.** `.github/workflows/ci.yml`: the `ts` job (`:48-115`); the npm cache and "Build the CLIs the tests call"
in `bash-tests` (`:117-160`), replaced by the dotnet publish of `judge` and `sindri` the suites call; in `gate` the
coverage-ignore grep, the ci-tools install, the no-any gate and `claude plugin validate|test mods/aw-live` (`:162-226`);
add an "install end to end" step running the phase 2 proof in the .NET job. `scripts/ci/changed-areas.sh:17-35`: drop
`TS_PACKAGES` and `dependents_of`, and the `packages` output the workflow reads (`ci.yml:36,58`).
`.github/dependabot.yml:20-34`: trim the npm directories. `.github/labels.yml:37`: drop `area:scorer`.
`deps.yaml`: delete the `Scorer` package and every edge naming it (`:197-202,264-270,310-320,360,380-390,483,563`); drop the
`ts:` and `scan:` fields and `db: [sindri/ledger.db]`; update `schemas/deps.schema.json` and the scan path in
`scripts/check-deps.mjs` and `scripts/deps/scan.mjs`. `.env.example`: drop variables only TS reads (`AW_SKIP_LAUNCHD`, `AW_SINDRI_SRC`, `AW_SCORER_BIN`, the scorer audit block, `CLAUDE_LOCAL_BIN` text); keep `AW_SKIP_BUILD` (read by `install-meta.sh` and `config/lib/setup-meta-step.sh:12`) and
`AW_LAUNCH_AGENTS_DIR` (read by the removal script); add the new variables of §7 and §8 (`AW_UI_EVIDENCE_BIN` among them, with the wrapper-exported `AW_SINDRI_JUDGE` and `AW_PACKETS_BIN` if not listed); the `sindri-app` build-path
defaults point at `bin/cs/`; `check-env-example.sh` fails otherwise.
**`sindri-app` consumers:** `scripts/context-first-run.sh` (16 hits), `scripts/owner-check.sh` (8), `scripts/sindri-ui-fixture.sh`,
`config/lib/setup-onyx-step.sh`, `scripts/install-onyx.sh`, `.env.example`, and 28 hits under `src/` (messages, comments and
docs strings; `git grep -n sindri-app` on the PR's tree is the checklist), all read `sindri`; `scripts/tests/sindri-app.test.sh`
becomes the install test. `scripts/readiness-baseline.mjs:17` `APPS` drops `scorer`, `judge`, `sindri`,
`skills/*`; `readiness-baseline.test.sh:14-18,251` fixtures are rewritten.

**AGENTS.md** (`CLAUDE.md` is a symlink): the tagline; the "C# build — in progress, not installed until the cutover"
paragraph becomes a short statement of the installed build; the Tech Stack TS rows; Directory Structure `judge/`, `scorer/`,
`sindri/`, `mods/`; the Commands block (TS packages, `scorer ...`, `sindri doctor|index|evolve|scope|shape|observe`,
`install-scorer.sh`, `install-sindri.sh`, the `sindri-app` lines); the bash test list; **Merge Gate** items 1, 2 (TS parts and
`claude plugin ...`), 6 and 7 (item 8 becomes the gate); Required Context's `CODE_STYLE.md` note. Then `scripts/sync-rules.sh`.
Rules: `.agents/rules/testing.md` (Vitest policy), `.agents/rules/hooks.md:171` (sindri-nudge installer).

**Bash suites deleted:** `scripts/tests/install-sindri.test.sh`, `scripts/tests/install-scorer-audit.test.sh`,
`config/lib/tests/install-live-pane.test.sh` (Q4), `config/lib/tests/judge-brief-real-cli.test.sh`,
`config/lib/tests/send-gate-real-judge.test.sh` (both run `node judge/dist/cli.js`; the C# hook-surface test covers the
contract; retarget at the C# binary if the owner wants them), the case in `providers/tests/install.test.sh:293-305`.
**Retargeted:** `config/lib/tests/install-prompt-sort.test.sh` and `install-trait-check.test.sh` (they stay under Q2; wherever they call
`install-judge.sh` they call `install-judge-hooks.sh`, and their judge is the C# one; unit 6.5); `scripts/tests/install-deps.test.sh` and `code-index-lock.test.sh` (pin source); `scripts/tests/ci-changed-areas.test.sh`,
`ci-bash-tests.test.sh:28,69-80` (the `install-judge.sh` build guard is moot); `scripts/tests/sindri-app.test.sh` (the
rename; becomes the install test); `config/lib/tests/install-wake-gating.test.sh:23`, `installed-hooks-smoke.test.sh`,
`aw-state-dir-isolation.test.sh:59-68,114`, `config/hooks/tests/provider-install-hooks.test.sh:123-126` (they call
`install-judge.sh` and expect a `judge` wrapper in `CLAUDE_LOCAL_BIN`); `config/lib/tests/statusline.test.sh` (fake scorer,
`:27,186-195`: keep the "missing scorer shows `--`" case, delete the refresh cases). `scripts/ci/bash-tests.sh:84-106` lists.

**Docs.** Edit: `README.md` (`:3,5,199,237-238,264`), `ONBOARDING.md` (`:11,28,241,253-257`), `planning/ARCHITECTURE.md`
(`:10,116,210,355-361` and the TS Sindri text), `planning/PROVIDERS.md` (`:4,39-40,64,72`), `planning/TESTING.md`
(`:13,22,27`), `planning/LOCAL_DEV.md` (`:7,45,57,90`), `planning/CODE_STYLE.md:9`, `planning/ERD.md` (`scorer.sqlite`),
`planning/DEPENDENCY_GRAPH.md:15`, `planning/PR_GUIDE.md`, `planning/COMMIT_STRATEGY.md`; `docs/sindri/README.md`
(`:25,35-36,108,229-262`), `docs/sindri/SETUP.md` (`:20,52,114-120,407`), `docs/sindri/OPERATIONS.md:7` (the "read `sindri` as
`sindri-app`" notices go), and the TS command docs `index.md`, `evolve.md`, `scope.md`, `profile.md`, `linear.md`,
`onyx.md`, `context.md`; `docs/csharp/{judge,bugfix,evidence,sindri,sindri-ui,sindri-profile,sindri-context}.md` (every
"until the switch" sentence; evidence.md "At the switch" is adopted wholesale); `skills/_preamble.md:5,17,18,102` (including its `/reflect` and `/correct` rows), the toolkit counts ("53 native skills" in AGENTS.md and READMEs), `.agents/rules/skills.md:148`,
`.agents/rules/csharp.md:42`, `.agents/rules/mcp-servers.md`, every other `install-judge`/`install-sindri` mention (`git grep` in README, ONBOARDING, `docs/sindri/SETUP.md`), `docs/devcontainer.md`, `docs/ui-evidence-cost.md`,
`docs/dispatch-conventions.md`, `docs/readiness/*`. Comment-only citations: `config/hooks/context-guard.sh:9`,
`probe-log.sh:6`, `done-gate.sh:65`, `config/lib/failopen.sh:11`. Historical specs under `docs/superpowers/` stay.

**Stays Node** (so Node stays a setup prerequisite): `dembrandt` (`setup.sh:461`), `prism-mcp` and `xcodebuildmcp` through `npx`
(`setup.sh:566-577`), `scripts/package.json` (Ajv for `check-launcher.mjs`, `check-fleet.mjs`, `check-scoping-schemas.mjs`),
`.nvmrc`.

## 10. The hook contract stays green

The contract (readiness spec §4c, frozen): the commands and arguments the bash hooks and statusline call, the JSON fields
they `jq`, the exit codes and stderr prefixes they branch on, and the files hooks write.

- `config/lib/tests/judge-cs-hook-surface.test.sh` becomes C#-only: delete `SIDES`, `TS_CLI`, `shim-ts`, `same()` and every
  `same <case>` call and every `if [ "$SIDES" = "cs ts" ]` block (lines 8, 33-35, 38, 40, 89-97, 132-136, 152-157, 174, 184, 218,
  246-251), the "(sides: ...)" suffix (253-254) and the "TS" wording in messages (93-96, 134-135, 155, 249). Binary
  resolution: `AW_JUDGE_CS_BIN` first (CI), then the installed `judge` on PATH, then the build-it fallback for local work.
  The shim keeps the hooks calling bare `judge` as installed hooks do.
- `config/lib/tests/sindri-cs-hook-surface.test.sh` (the nudge) stays; it resolves the installed `sindri`. With Q3 its pinned
  line changes in the same PR.
- A new case set for the pre-commit hook (§6.2): a staged secret is refused with `SND-SCRUB-002`; v2 and v1 hooks are
  replaced; a foreign hook is refused `SND-SCRUB-003`.
- `statusline.sh` keeps its `find_bin scorer` guard: with no scorer the Live column prints `--` and no refresh is scheduled
  (`config/statusline.sh:155,184`). The scorer refresh branch and the Live cache code are removed in the same PR as the
  scorer. `judge_health` (`config/statusline.sh:125`) is called today only from `live_refresh` (`:159`) and the segment returns `--`
  when no scorer exists (`:184`), so removing the scorer would drop the judge segment. The PR therefore moves the
  `judge_health` call into the render path (cached, as the Live cache was), and `statusline.test.sh` keeps a judge-health case.
- Three rows join the frozen table of `docs/csharp/judge.md` and `judge-cs-hook-surface.test.sh` (unit 6.5): `done-gate.sh` reading `.requirements.uiEvidence` from `judge/sessions/<sid>.sort.json` and deleting it (a real-hook case: a sorted UI session blocks a done claim without evidence, once); and `prompt-sort.sh`
  (`judge prompt-sort` < `{prompt, sessionId}`; reads `.context`; ignores the exit; the hook's kill budget) and
  `trait-check.sh` (`judge trait-check --edit --file ... | --stop`; branches on exit 0, 2 with stderr as the message, 3 as
  `jev-unavailable`, anything else as `judge-exit-N`). Real-hook cases, offline (`AW_JUDGE_PROVIDERS=rules`): `prompt-sort`
  exits 0, prints nothing and records a decision row (the heuristics path), and a machine-text and a slash-command prompt
  never reach the judge; `trait-check` in a repo with no traits exits 0, and with a trait and no Jev exits 3 and the hook
  fails open logging `jev-unavailable`. The findings path and a decisive prompt-sort axis use a stub Jev in the C# unit
  tests; how a real-hook case reaches a stub Jev is decided in unit 6.5.
- The not-yet-built stub (§5.2, Q9) is pinned by 6.0's tests (the exact line and exit per verb) and by 6.5's hook-surface cases, and by the invocation test of §5.2 (`scripts/tests/judge-stub-invocations.test.sh`, owned by 6.0, so 6.5 does not carry it). It is not a hook row, since no hook calls one.
- `config/hooks/tests/judge-health.test.sh` and `failopen.test.sh` use stubs and do not change.

## 11. The scorer cut, in detail

| Consumer | Action |
|---|---|
| `scripts/install-scorer.sh`, plists `scorer`, `scorer-audit`, `$AW_STATE_DIR/scorer/` | removed (§8) |
| `config/statusline.sh:155,184` | guard stays, refresh goes (§10) |
| `mods/aw-live` | removed (Q4, owner) |
| `deps.yaml` Scorer package and edges | deleted (§9). The Evidence `files` edge for `ui-evidence/runs` and the `context-guard` fires-log file need a new owner or removal; unit 3.2 decides, and `check-deps.mjs` is the check |
| CI, dependabot, labels, `.env.example`, readiness-baseline | §9 |
| `scorer probe` and `scripts/probe.sh` docs | the reader is gone; `probe.sh` keeps writing, docs say so |
| daily cost report | none until Sindri rebuilds it (§14) |

**Decision (Q4, owner 2026-10-23, "all Recommended"): the `aw-live` mod is uninstalled at the switch**
(`install-live-pane.sh --uninstall`) and deleted from the repo until Sindri has a `sindri live`. Keeping the `scorer` package
installed only for the pane would contradict the owner's cut and keep Node and the TS build in the install.

## 12. Phases

Each phase has exit criteria and a proof. Contract units come first (§13).

### P0. Contracts

This spec's tables become test lists, red first (`/new-slice` style). Exit: every §3.2, §4.2, §5.1 and §6.1 row marked
equivalent has a named test or an existing one; every gap row has a red test or a stub. Proof: `dotnet build -warnaserror`
zero warnings; the red tests are listed in the PR. The Judge contract unit 6.0 writes the red tests and stubs for the verbs of 6.1 and 6.2, the not-yet-built route for the verbs of 6.3 and 6.4 (§5.2; the stubs of 6.1's and 6.2's verbs are ordinary red stubs that those units replace, while the route for 6.3's and 6.4's verbs stays until P5), and owns the shared files, so that 6.1, 6.2 and 6.5 write to disjoint scopes (6.3 and 6.4 get theirs from 6.3's own contract step in P5): the migration with its final schema (`0003_prompt_sort.sql`, drawn from the TS DDL in `judge/src/db.ts`; the runner checks the sequence and checksums, so a fix after it has merged to main is a new migration, never an edit; 6.3's eval-label migration comes with 6.3 after the switch and takes the next free number, `0004_eval_labels.sql` while 0003 is the last on main), a test that a C# database at schema 0002 migrates forward to 0003 with its rows kept, the public surface of 6.1, 6.2 and 6.5 as stubs (`PublicAPI.Unshipped.txt` and an undeclared public type fails the build, RS0016, so, 6.0 having declared the surface, the Judge units edit the three `PublicAPI.Unshipped.txt` files themselves only for a type they add late, each in its own namespace; this is the one exception to the integrator-only rule, and a line-level conflict is resolved by the later merger; the 999 lines are Core's, Features has 318 and Infrastructure 3), the `JudgeJsonContext` entries, the `Composition.cs` registrations, and the `CommandTable.cs` routes (`prompt-sort` with no subcommand to the 6.1 handler, `trait-check` to 6.2's, and `prompt-sort <sub>`, `label`, `adjudicate` and `eval` to the one not-yet-built handler that 6.3 and 6.4 replace route by route, so no pre-switch unit edits the table), the `deps.yaml` entries (the Judge note names the new verbs; the `git` runner; the transcript read of `~/.claude/projects`, outside the state root the checker's `files` edges are relative to, so 6.0 decides how it is declared), and the shared Jev batch, as real code and not a spike: a Core port, a batch method on `JevProvider`, the wire types and the `ProviderWireJsonContext` entries (`JevProvider` and its wire types are internal to Infrastructure and ask one question, `decision`, today), with a contract test against a fake HTTP endpoint, so 6.1 and 6.2 only call it. The batch honours `--providers`, `AW_JUDGE_PROVIDERS` and `providers.jev: false` the way `ProviderCatalog.Lineup` does, and a Jev that is unavailable or excluded comes back as `unavailable`, not an error: that is what makes `trait-check` exit 3 and the sorter heuristics-only. If the wire cannot batch, 6.1 ships heuristics-only with Jev off and the PR says so. 6.0 also adds the `YamlDotNet` reference and lock file to Judge.Infrastructure (6.2 proves it under NativeAOT), and the not-yet-built handlers for the verbs of 6.3 and 6.4 (one per verb family, over a shared helper) answers `judge: <verb> is not in this build yet` with exit 1 and never throws (it has no service behind it; `Program.cs` has no catch). The verbs of 6.1 and 6.2 are ordinary red stubs that those units replace, so the `prompt-sort` hook path and `trait-check` do not answer the stub line. Routing: every stubbed first word is in `_routed` with an arm before the `rest.Length != 1` arm of `CommandTable` (a word missing from `_routed` falls through to `unknown question`, exit 64), and `prompt-sort` is one arm that 6.0 owns: the second word decides, with no second word or `scaffold` going to 6.1's handler, a stubbed subcommand to the stub handler, and anything else to the unknown-subcommand answer (6.0 pins each case; the exact string `judge: unknown prompt-sort subcommand: <sub>` is pinned by its test, since the TS printed it without the prefix). The JSON contexts are two (`JudgeJsonContext` and `ProviderWireJsonContext`). The owner answered Q1 to Q8 on 2026-10-23 and confirmed Q3, Q6 and Q7 and answered Q9 in rev 9 (§17); no question is open.

### P1. Close the gaps (parallel waves)

| Unit | Package | Work | Proof |
|---|---|---|---|
| 1.1 | Evidence | G-a script features with guard rules | unit lane; heavy `BrowserSessionContract` run through the heavy lock |
| 1.2 | Evidence | G-b `gh-publish` (port, gh runner, fake) | unit lane with the fake; owner's scratch-repo check, skipped without a token |
| 1.3 | Evidence | G-c `JudgeCliJudge`; registered in `Composition.cs` | contract test with a stub `judge`; one run with the real C# judge offline (`AW_JUDGE_PROVIDERS=rules`) |
| 1.4 | Evidence | G-d doctor slots and env loader; G-e per-artifact `commit` and `at`; G-g flag name; a `version` verb printing the build stamp (§3.1) | unit lane |
| 1.5 | Evidence + skills | G-f SKILL.md rewrite; the brief text; `step.evidence.none` and `combine` as `IBlockStep`; per-verb `ui-evidence` grants in `AgentLaunches.RequestFor` (§3.1); §3.4 checks; a test that asserts the exact per-verb grant strings, with `gh-publish` only on the publish step, and that no step brief names `--allow-writes` or a non-loopback `--host` (the flags are held by the host guard, which has its own Evidence tests) | Sindri unit lane; the Sindri Ui tests in their own gate if a catalog row changes (feedback_lane_gate_ui_tests) |
| 2.1 | BugFix | §4.3 (a) and (b): both adapters, the digest and check-kind fixes, `aot-smoke` and `expected.txt:18` updated | unit lane; `bash src/BugFix/aot-smoke/run.sh` with a fake `judge` and `ui-evidence` |
| 2.2 | BugFix skill | §4.3 (d) text | the skill's commands run once in a temp repo through the smoke |
| 3.1 | Judge installers, skill, statusline | `install-judge.sh` split into `install-judge-hooks.sh` (the TS half dropped, the `prompt-sort` hook install kept, §7.4; the refusal text of the lever installers `install-done-gate`, `install-scope-gate`, `install-trait-check`, `install-wake-gating` and `install-external-write-guard`, which say "run scripts/install-judge.sh first", now says "run ./setup.sh"), the `/judge` skill text edits of §5.2 and the statusline judge segment (§10). Written and tested in P1 but only **lands in the switch PR**; nothing is unmerged | `judge-cs-hook-surface`, the `install-*` tests and `statusline.test.sh` |
| 3.2 | deps/CI | Scorer package out of `deps.yaml`; graphify pin moved (§6.3) | `node scripts/check-deps.mjs`; `install-deps.test.sh`; `code-index-lock.test.sh` |
| 4.1 | Sindri | `scrub` verbs, `IRepoHooks`, hook v3, `doctor` per-repo line | unit and integration lanes; `scripts/sindri-guard-proof.sh` |
| 4.2 | Sindri | Q3 text and test; the `setup:ui-evidence` doctor line (§3.1); text of `sindri index setup` messages (the `--with-sindri` no-op of Q8 is a `setup.sh` edit of the switch PR) | `sindri-cs-hook-surface`; Sindri unit lane |
| 5.1 | Install | `scripts/install-cs.sh` (`--publish`, `--link`), the wrappers (with the `AW_UI_EVIDENCE_BIN` export) and their test; `cutover-clean-ts.sh` is P2. The `setup.sh` and provider edits of §7.2 and §7.4 wait for P3 | the install proof of P2, dry run |
| 6.0 | Judge (contract, P0) | the shared files and contracts of §12 P0: red tests and stubs for the verbs of 6.1 and 6.2, the not-yet-built handlers and routes and their per-verb line-and-exit tests for the verbs of 6.3 and 6.4, the single `prompt-sort` arm with its three-way second-word routing and the unknown-subcommand test (string, stream, exit), `scripts/tests/judge-stub-invocations.test.sh`, migration `0003_prompt_sort.sql` and a schema 2 to 3 forward-migration test, the public surface and JSON-context entries of 6.1, 6.2 and 6.5, the service registrations and the Host routes, the `deps.yaml` Judge entries, the `YamlDotNet` reference (Infrastructure, Host, Tests and Testing lock files) with an `aot-smoke` publish proving it loads under NativeAOT, the shared Jev batch with a fake-HTTP contract test | `dotnet build -warnaserror` zero warnings; the red tests listed in the PR; the Jev batch contract test green; the AOT publish |
| 6.1 | Judge | `prompt-sort` hook path, `prompt-sort scaffold`, `config set prompt-sort` and the `promptSort` write path (`ConfigCommands`, `SetConfig*`, `JudgeConfig`; 6.4's `promote --apply` calls it), and the `<sid>.sort.json` writer (§5.2; the migrations, routes, Jev batch and public stubs are 6.0's) | unit lane with a stub Jev; the offline real-hook cases for `prompt-sort` and `done-gate`; a secret in a prompt is redacted in the request to Jev, in `decision_details.input_json` and in `<sid>.sort.json`; `aot-smoke` |
| 6.2 | Judge | `trait-check` (`--edit`, `--stop`, `--scan`), the trait files and the Jev batch, with the Jev request redacted (§5.2); with Jev excluded by `--providers` or `AW_JUDGE_PROVIDERS` it exits 3, which is how the offline hook case of §10 gets its exit | unit lane with a stub Jev and a temp git repo; `aot-smoke` proves YamlDotNet under NativeAOT (the Sindri profile uses it, but Sindri is framework-dependent) |
| 6.5 | Judge + hooks | the hook rows and real-hook cases of §10 (`prompt-sort`, `trait-check`, `done-gate` on `.sort.json`), the `judge.md` rows and its "Not yet built" table, the stub hook-surface cases (§5.2) (`judge.md` and `aot-smoke/expected.txt` are 6.5's alone before the switch; other units send it lines), the retargeted `install-prompt-sort` and `install-trait-check` tests, which land in the switch PR with 3.1's rename (until then they call `install-judge.sh`) | `bash config/lib/tests/judge-cs-hook-surface.test.sh`; `bash src/Judge/aot-smoke/run.sh`; a cold `judge prompt-sort` (the offline, heuristics path) through the wrapper in under 500 ms, a target the measurement may revise (the hook kills at 1500 ms) |

Exit: every gap row of §3.3 and every KEEP-needed row of §6.1 is closed or cut by an owner answer, and every verb of §5.2 that is not marked post-switch has a C# verb, the others answering the not-yet-built stub; `dotnet build
-warnaserror`, both lanes, `dotnet format --verify-no-changes`, `scripts/dotnet-verify-packages.sh` pass.

### P2. The install switch and the removal script, on a scratch box

Write `scripts/cutover-clean-ts.sh` and its test; prove the whole switch **without the real HOME**. Isolation follows
`scripts/sindri-prove.sh`: a guarded temp state dir (`AW_STATE_DIR`, `CLAUDE_LOCAL_BIN`, `AW_LAUNCH_AGENTS_DIR`,
`GIT_CONFIG_GLOBAL` set to temp paths, plus the provider settings and hook paths and the Playwright browser cache, a fake TS
install seeded in it: wrappers, state, plists, a profile with one repo, one onboarded repo with a v2 hook, a TS-shaped
`decisions.sqlite`, and registered `prompt-sort`/`trait-check` entries). Never `HOME=$(mktemp -d)` for dotnet or heavy work (each leaks a 3.3 GB NuGet cache; `feedback_no_temp_home_dotnet`).
Exit and proof: on the scratch box, `install-cs.sh` publishes, links and verifies; the cleanup removes exactly the §8
list and leaves the §8 "kept" list byte-identical; the v2 hook is now v3 and refuses a staged secret and passes a clean
commit; the real hooks, run against the installed binaries, pass the §10 suites (the `prompt-sort` and `trait-check` hooks included, whose seeded registrations stay untouched), and `judge label status` and `judge eval import-turns x` through the wrapper print their not-yet-built lines and exit 1; `./setup.sh --providers claude,codex,cursor
--dry-run` is clean; a second run changes nothing, and a flipped box with live C# judge state (a schema-versioned `decisions.sqlite`, a `config.json`) keeps it through a second `./setup.sh`; a cold `sindri repo status --nudge` through the wrapper stays inside the nudge hook's time
budget (measured, not assumed; the framework-dependent `sindri` is the slowest binary); a failed `--publish` leaves the seeded
TS install untouched, and a step-1 refusal changes nothing. The step runs in CI (§9). The macOS case (a wrapper exec of a
NativeAOT `judge` finding `libe_sqlite3`) cannot run in Linux CI; it is the P4 check on the owner's Mac.

### P3. The switch PR

§9 in one PR: `setup.sh` and the provider installers call `install-cs.sh` then `cutover-clean-ts.sh`, and the TS
directories, CI jobs, gates, suites and docs are deleted. Merging it changes no box (the flip is P4). Exit: the Merge
Gate with item 8 as the gate passes; `scripts/sync-rules.sh --check`, `check-deps.mjs`, `check-env-example.sh`,
`check-rules.mjs` pass; no tracked file under the deleted directories; no `npm` in any install path; the P2 scratch-box
proof re-run on the PR's tree; the owner has read the output of `cutover-clean-ts.sh --dry-run` against the real state; the `/judge` skill text carries the not-yet-built marker on every stubbed verb and `judge.md` has its "Not yet built" table. P3 does not wait for 6.3 or 6.4.
Proof: the CI run.

### P4. The owner's flip on the real box

The owner pulls main and runs `./setup.sh` once per box. Exit: `sindri doctor` is ok; `judge health` answers `ok`; a real hook fires (a session
start prints no `judge: not found`); a real `ui-evidence run` produces a summary with the judge adapter wired; one
`bugfix-state` run reaches a resolved report; `git commit` in an onboarded repo is checked by v3; a prompt in a real session records a `prompt-sort` decision (`judge why <id>`); `sindri profile init`, the typed `profile approve` and `repo onboard` for
each repo the report listed have been run (the C# profile starts empty); bare `sindri` opens the app, or on a box with no
display prints the status and says how to open it. 6.3 is started (P5). No rollback is built; if
a step fails, the owner reports it and the fix goes forward.

### P5. After the flip: the by-hand Judge verbs (Q9)

Units 6.3 and 6.4 are ordinary PRs to main after P3 merges; the flip (P4) does not wait for them and they change no installed
hook. 6.3 goes first. Because it is then the only Judge unit running, it starts with its own contract step under the same
sanction as 6.0 (it declares the DTOs, registrations and public surface of both 6.3 and 6.4 (6.4's types as compiling stubs behind the stub route, so no red test is on main after the flip), adds the next free migration (`<n>_eval_labels.sql`, `0004` while 0003 is the last on main) for
`eval_items` and `labels` with a schema N to N+1 forward-migration test, and replaces its stub routes); 6.4 follows 6.3 and
reuses 6.1's stores, the config write path and 6.3's label and eval code.

| Unit | Work | Proof |
|---|---|---|
| 6.3 | `label import\|outcomes\|set\|status`, `adjudicate`, `eval`, `eval import-turns`, the transcript reader, the lookup of a provider by name (`IProviderCatalog` returns a chain only; the port method and `ProviderCatalog.cs` are 6.3's, declared in its contract step, so no interface change goes in before the switch with no consumer) and a model and effort option on `ClaudeCliProvider` (it hard-codes `--model haiku --effort low` and `MAX_THINKING_TOKENS=0`, `ClaudeCliProvider.cs:67,71`; the option sets both; 6.3 owns that file). `adjudicate` runs its `claude` child with tools, project settings and hooks off and a scratch working directory outside any repo (the way the existing providers use `TempWorkspace`; 6.3 names the exact flags), treats transcript turns as untrusted text, redacted first, and its stub-`claude` test asserts the exact argv and cwd (§5.2) | unit lane over transcript fixtures; a stub `claude` for the adjudicator; `aot-smoke` |
| 6.4 | `prompt-sort why\|import\|outcomes\|adjudicate\|eval\|promote` (§5.2) | unit lane |

Timing: no date is promised, but the target for 6.3 (the unit whose `label import` depends on stored details) is within 21 days of the flip, so that it lands before day 30; 6.3 is started the day the flip (P4) is confirmed, and 6.4 follows it with no date. The C# decision details are kept 30 days (`DecisionDetails.Retention`), and `label import` builds eval items from stored decisions, so a later landing loses the earliest items; the transcript-based labels (`label outcomes`, `eval import-turns`) lose nothing. A binary older than the database (after 6.3's migration ships) fails with AW-DATA-007 and every hook fails open, so `setup.sh` is re-run to update the binary.

Exit, per unit: its verbs answer for real, so the stub route, the stub test line, the "Not yet built" row in `judge.md` and the
`/judge` skill marker are gone in the same PR; `aot-smoke/expected.txt` and the `judge.md` rows are updated by that PR; the
merge gate passes; 6.3's argv-and-cwd test for the `adjudicate` child (it asserts five properties: no tools, no project settings, no hooks, a working directory outside any repo, and a minimal environment with no tokens such as `GH_TOKEN` or `AW_FORGE_TOKEN`) and a redaction test per writer (`eval_items`, `labels`, `judge/evals/` files) pass before it merges. The owner's by-hand use (the eval history starts empty, §5.3) begins here.

## 13. Parallel execution with an agent team

**Dependency graph.**

```
P0 contracts (Wave 0)
 |
 +-- 1.1  Evidence script features ----+
 +-- 1.2  Evidence gh-publish ---------+
 +-- 1.3  Evidence JudgeCliJudge ---+---+--> 1.5 skill + steps + AgentLaunches  --+
 +-- 1.4  Evidence doctor/env/DTC9 -+                                              |
 +-- 2.1  BugFix adapters  --(needs the 1.3 judge contract, not its code)--> 2.2 ---+
 +-- 3.1  Judge installers + skill + statusline ---------------------------------+
 +-- 6.0  Judge contract --+-- 6.1  prompt-sort hook path --+--> 6.5  hook-surface + tests (needs 6.1, 6.2, 3.1)
                           +-- 6.2  trait-check -------------+
                           (6.3 label/adjudicate/eval, then 6.4 prompt-sort eval side: after P3, wave 5)
 +-- 3.2  deps/CI + graphify pin ------------------------------------------------+
 +-- 4.1  Sindri scrub + IRepoHooks + hook v3 --> 4.2 nudge text -----------------+
 +-- 5.1  install-cs.sh (needs the binary list, not the gaps) -------------------+
                                                                                  v
                                            P2 switch proof --> P3 switch PR --> P4 flip
```

**Waves.**

| Wave | Units | Rule |
|---|---|---|
| 0 | P0 contracts, 6.0 for Judge | one unit per package, merged first; later units build on red tests |
| 1 | 1.1, 1.2, 1.3, 1.4, 2.1, 3.1, 3.2, 4.1, 5.1, 6.1, 6.2 | disjoint write scopes; all start at once |
| 2 | 1.5, 2.2, 4.2, 6.5 | wait for their wave-1 inputs (6.5 waits for 6.1, 6.2 and 3.1) |
| 3 | P2 proof, then P3 | one integrator unit each |
| 4 | P4 | the owner |
| 5 | 6.3, then 6.4 (P5) | after P3 merges, as ordinary PRs; the flip does not wait for them (Q9) |

**Contract unit first, then disjoint scopes.** In each package the first unit writes the failing tests and the stubs
(request, response, port, adapter with no constructor parameters that throws `NotSupportedException`) and merges red; the
implementers then fill disjoint write scopes: 1.1 owns `Core/Scripts`, `Core/Guard`, `Browser/Locator.cs`; 1.2 owns a new
`Features/GhPublish`; 1.3 owns `Infrastructure/Judging`; 1.4 owns `Features/Doctor` and a new env loader; 2.1 owns
`src/BugFix/Aw.BugFix.Infrastructure`; 4.1 owns a new `Aw.Sindri.Features/Scrub` and `OnboardRepo/IRepoHooks`; 6.1 owns a new
`Core/PromptSort`, `Features/SortPrompt`, `Infrastructure/PromptSort` and the `prompt-sort` Host command; 6.2 owns `Core/Traits`,
`Features/CheckTraits` and `Infrastructure/Traits`; 6.3 (post-switch) owns `Core/Evals`, the label, adjudicate, eval and import-turns slices
and `Infrastructure/Evals`; 6.4 (post-switch) owns `Features/PromptSortEval` with its own `Core` and `Infrastructure` folders for the eval side (the stores it shares with 6.1 are 6.0's ports). The new Core namespaces join the acyclic graph (`ARCH-CYCLE-001`).

**Hotspot files owned by the integrator only** (the package's contract unit, such as Judge's 6.0, is the one implementer allowed to edit the hotspot files of its own package once, ahead of the units that follow; it declares every DTO, registers every service and routes every verb, and 6.1 to 6.4 replace stub bodies only, so they do not touch a hotspot; a type found late goes through the integrator, as does any migration other than 6.0's `0003` and 6.3's eval-label migration (the next free number); 6.3, running alone in wave 5, takes the same sanction for its own contract step). `deps.yaml`, `AgenticWorkflow.slnx`, `Directory.Packages.props`,
`packages.lock.json`, `PublicAPI.*.txt`, `CommandTable.cs` of each host, `Composition.cs` / the package's `Add...()` and its
JSON context, `.env.example`, `.editorconfig` baseline block, `scripts/ci/bash-tests.sh`, `.github/workflows/ci.yml`,
`AGENTS.md`, `setup.sh`, `providers/lib.sh`. An implementer asks the integrator for an edit to one of these; it does not
make it. The integrator serializes them so two units never conflict on a hotspot.

**Roles.**

| Role | Does | Never does |
|---|---|---|
| Orchestrator | holds the graph, assigns units, tracks the heavy lock queue, answers the owner through the lead | codes |
| Implementers | one unit each, in their own git worktree, red then green, formatting only the `.cs` they touched | touch another unit's scope or a hotspot; run a full-solution format; run pattern kills |
| Integrator | owns hotspots, merges units in dependency order, runs the batch gate (build `-warnaserror`, both lanes, full `dotnet format --verify-no-changes`, `check-deps`, `check-env-example`, `check-rules`, `sync-rules --check`) | write feature code |
| Prover | runs the P2 scratch-box proof and the real-binary smokes (`aot-smoke` scripts, `sindri-guard-proof.sh`, the hook-surface suites) and reports exit codes and output | fix what it finds; it reports to the owning implementer |
| Reviewers | read each unit's diff against this spec's tables, not a summary of them; fix-only findings loop, suggestions answered in writing (`feedback_review_loop_severity`) | block on nits |

Builders run on Sonnet or Haiku, never Opus (`feedback_builders_on_sonnet`). Worktrees are removed by the meta, never by hand
(`feedback_worktree_removal_via_meta`). A lane never asks the owner a blocking question; it routes it through the meta
(`feedback_no_blocking_questions_in_lanes`). Each PR worktree gets `heavy dotnet clean AgenticWorkflow.slnx` right after its
`ready` (`feedback_clean_after_ready`). No process is killed by pattern, ever: recorded PIDs only.

**Heavy jobs.** Builds, test lanes, NativeAOT publishes, browser runs and the P2 proof take slots on the box-wide heavy lock
(`config/lib/locks.sh`; cap and graded memory guard, `project_heavy_lock_two_slots`). The number of agents is not the limit;
the lock is. A unit that cannot get a slot waits in the queue and does other non-heavy work (tests written red, docs) in the
meantime. Lanes and the meta are the only ones that run heavy jobs in demo mode; outside it, any unit does, through the lock.
Scoped lane gates: a lane runs build and the unit lane of its touched projects only; the full-solution format and the
integration lane run once, in the integrator's batch (`project_scoped_lane_gates`, `feedback_scoped_dotnet_format`). A PR
that touches a Sindri catalog row, action id or API golden also runs `Aw.Sindri.Ui.Tests` in its own gate.

## 14. Out of scope

- The scorer rebuild, the daily cost report and `scorer audit`.
- Recipes for `evolve`, `scope`, `observe`, `shape` and `profile explain` (rebuilt from scratch in C#, owner 2026-10-20).
- `sindri live` (the replacement for `scorer live` and the `aw-live` pane's numbers).
- `bugfix-state run-api` and `run-db` verbs (the judge's `api-evidence` and `db-evidence` kinds stay unreachable from the skill).
- An `IUiEvidence` port and a `Sindri -> Evidence cli` edge (§3.1, rejected under Q6).
- The by-hand Judge verbs before the switch: units 6.3 and 6.4 are planned work after it (§12 P5, Q9), not cancelled.
- Windows; a second user; any rollback tooling.

## 15. Risks

| Risk | Effect | Mitigation |
|---|---|---|
| Stale per-repo hooks (v2, v1) in repos the TS profile does not list, or a repo moved since onboarding | the v2 `shape` line fails (one stderr line per commit); `scrub --staged` still answers, so no commit is refused | the C# `scrub --staged` serves v2 and v1 hooks unchanged (§6.2); v3 recognises v1 and v2; doctor reports `pre-commit:<repo>` for profile repos; the removal script prints repos it could not reach and how to reinstall (`sindri scrub --install-pre-commit --repo <path>`); the fail-closed message names the fix |
| Judge state refusal (AW-DATA-009) if `decisions.sqlite` survives | every hook sees judge exit 1; fail-open logs; judge health `down` | §5.3 and §8: step 5 deletes it before the link; the P2 proof seeds a TS-shaped file |
| .NET runtime missing for the framework-dependent `sindri` and `ui-evidence` | the wrappers fail | `install-cs.sh` installs the pinned SDK into `$DOTNET_ROOT`; the wrappers export it; the install verifies each binary answers |
| Node stays required (dembrandt, prism-mcp, xcodebuildmcp, Ajv scripts) | "no TS" is read as "no Node" | §9 lists what stays Node; `setup.sh` keeps its Node check |
| The Judge hook port is the longest P1 item (6.1 and 6.2: about 800 TS lines of the sorter and 525 of `trait-check`) | the switch waits on it | 6.0 lands first so 6.1 and 6.2 start together; the P3 gate needs the hooks and 6.0, not 6.3 or 6.4 |
| The by-hand Judge verbs are missing after the flip (Q9) | a user types `judge label ...` and gets the stub; if P5 slips past day 30 the earliest eval items are gone | the line is exact and says why (`judge: <verb> is not in this build yet`), the `/judge` skill carries the same marker and says not to retry, no hook or recipe invokes a stubbed verb (a bash test with a positive control), the P5 target is 21 days, and P5 removes each stub in the PR that lands the verb |
| An older `judge` binary meets a newer database (after 6.3's migration) | AW-DATA-007; every hook fails open and `judge health` reads down | re-run `./setup.sh`; one install owns both the binary and the database's migrations, so the skew needs a stale binary earlier on `PATH`, a pinned `AW_JUDGE_BIN`, or a dev checkout run with `dotnet run` against the real `$AW_STATE_DIR` (use a scratch `AW_STATE_DIR` for by-hand testing); the statusline's judge segment reads down, which is the signal |
| A ported hook is slower than its budget (`prompt-sort`: killed at 1500 ms by default; a cold start through the wrapper) | `prompt-sort` prints nothing, `trait-check` fails open | both hooks already fail open; unit 6.5 times a cold `judge prompt-sort` through the wrapper against the hook budget |
| Ownership read as a verb: someone adds `sindri evidence` | Playwright enters the Sindri host (§3.1, Q6) | the repo check of §3.4 fails when a file outside `src/Evidence/` spawns Playwright |
| Heavy lock contention (P1 builds, AOT publishes, the browser) | waves stall, a gate starves lanes | §13: queue by depth, the meta reserves and clears its slot (`feedback_meta_clear_gate_reserve`); no per-agent cap |
| The statusline's Live column shows `--` forever | looks broken | the guard is intended (§10); the README says why |
| `reflect` and `correct` skills removed | users type a removed command | `setup.sh` removes their links: they move from `MANAGED_SKILLS` (`setup.sh:229`) to `DEPRECATED_SKILLS` (`:235`); removing a name from `MANAGED_SKILLS` alone leaves the link (the prompt answers no when not interactive) |
| Evidence summary schema change (EV L626-640) | old `summary.json` files unread, bugfix and scorer readers differ | old runs stay on disk; BugFix reads only the C# schema; no migration |
| The cleanup stops partway (a step fails) | a half-switched box | the order of §8 puts the only refusal first, every step is idempotent, and `setup.sh` stops with exit 1 and "re-run `./setup.sh`" before any lever installer runs |
| A7a not on main | the `setup.sh` call lands twice | §7.3: whichever merges first owns it |
| Judge adapters in BugFix and Evidence read the judge's store | wrong `AW_STATE_DIR` gives empty `why` rows | both adapters take the same env as the hooks; `AW_JUDGE_STATE_DIR` for Sindri's separate store is not reused (judge.md; DT spec §8.3) |

## 16. Amends

- `2026-10-09-agent-readiness-design.md` §4c: **superseded** (see the header). §4a step 3 ("one reversible switch"), the
  `--runtime` and `--prefix` flags, the archive and the 14-day tag are replaced by §2 and §8. Kept from §4c: the hook
  contract, "no data compatibility", the refuse-to-start check, the bugfix namespace move.
- `2026-10-19-sindri-setup-once-design.md`: the command name (`sindri-app` is read as `sindri` from the switch on; §7.5), the
  statement that `sindri` is the TypeScript build, and the A7a order sentence naming the scorer, judge and TS builds
  (§7.3). Not amended: SO-D5, §5.3, §5.4, the A7a exit codes, §13.1's proof shape.
- `docs/csharp/evidence.md` "At the switch": adopted; G-a to G-g added; it states that Sindri owns the binary and installs it (§3.1).
- `docs/csharp/bugfix.md`: the digest wording (§4.3 c) and the adapters; `docs/csharp/judge.md`: the hook-table row `prompt-sort` (J2
  becomes built), a new `trait-check` row, the verbs of §5.2 and a "Not yet built" table for the stubbed verbs (Q9).
- `AGENTS.md` and `.agents/rules/{hooks,testing}.md`: §9.

## 17. Owner answers (2026-10-23, confirmed in rev 9)

Relayed by the meta; the owner's words are quoted where the owner gave words. Rev 8 asked the owner to confirm Q3, Q7 and the
Q6 reading and to answer Q9; the owner answered all four (relayed by the meta) and this rev 9 records them. Nothing below is open.

| # | Question | Status | Answer | Owner's words | Where it landed |
|---|---|---|---|---|---|
| Q1 | The Evidence script features (G-a) | decided (owner) | Port them | the recommended answer | §1, §3.3, unit 1.1 |
| Q2 | `prompt-sort`, `trait-check`, `label`, `adjudicate`, `eval` | decided (owner) | **Not cut: port all of the Judge** | "port all of Judge hooks. parts of Judge should already be ported for the line recipe i think" | §1, §5, §7.4, §9, §10, P0 and P1 units 6.0 to 6.5, §13 |
| Q3 | The nudge line text | decided (owner confirmed, rev 9) | Amend the frozen line to drop "shape signals" | confirmed as recommended | §6.3, unit 4.2 |
| Q4 | The `aw-live` mod | decided (owner) | Uninstall and delete until `sindri live` | "all Recommended" | §8 step 9, §9, §11 |
| Q5 | `sindri-app` | decided (owner) | Retire at the switch | "all Recommended" | §7.5, §8 step 7 |
| Q6 | How the recipe's evidence run steps reach Evidence | decided (owner; reading confirmed, rev 9) | **Sindri owns, builds, installs and versions the `ui-evidence` binary, separate from `sindri`; the skill calls it** | "no, sindri owns the binary, not the skill"; the reading confirmed in rev 9 | §3.1 "Ownership", the Q6 decision, §7.1, unit 4.2 |
| Q7 | `gh-publish` transport | decided (owner confirmed, rev 9) | The `gh` CLI runner | confirmed as recommended | §3.3, unit 1.2 |
| Q8 | `--with-sindri` after the switch | decided (owner) | A no-op for one release | "all Recommended" | §7.2 |
| Q9 | May the by-hand Judge verbs land after the switch? | decided (owner, rev 9) | Yes: 6.3 and 6.4 are a post-switch wave; until then the verbs print the not-yet-built line and exit 1 | "Yes, land later" | §5.2 "The not-yet-built stub", §12 P1, P3, P5, §13 wave 5, §15 |

**The Q6 reading.** The owner's "no" answers whether the skill owns the evidence tool. The reading taken, and confirmed: the
`ui-evidence` binary belongs to Sindri (built, published, installed and versioned in Sindri's install, next to `sindri`, with
Sindri's stamp), it stays a separate binary and not a `sindri evidence <verb>` subcommand, and the skill calls it. The
subcommand is rejected because it would put Playwright and Npgsql in the `sindri` host and widen every evidence step's grant to
all `sindri` verbs (§3.1).

### Owner approval

On 2026-10-23 the owner approved this spec ("mark 853 good to go", relayed by the meta). Approval covers the text as of rev 12.

### Open questions

None.
