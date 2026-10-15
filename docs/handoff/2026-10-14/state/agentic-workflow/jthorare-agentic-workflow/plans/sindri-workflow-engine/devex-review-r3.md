# Plan Devex Review (pass 3, final) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 3)
**Reviewed:** 2026-10-10
**Checked against:** `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`, `scripts/scaffold/new-slice.mjs`,
`scripts/scaffold/lib/repo.mjs`, `skills/specToProvenPR/SKILL.md`, and the S1 branch (read only) at
`.claude/worktrees/sindri-s1-app-shell` (`docs/csharp/sindri.md`, `Commands/Output.cs`, `Commands/CommandTable.cs`,
`Commands/ServeCommand.cs`, `Endpoints/EndpointTable.cs`, `Profile/SindriProfile.cs`, `Profile/RepoEntry.cs`,
`Ledger/LedgerDatabase.cs`, `Ledger/Migrations/`, `Aw.Kernel.Data/EmbeddedMigrations.cs`, `MigrationRunner.cs`,
`Aw.Sindri.Host.csproj`).

## Summary

Revision 3 resolves most of pass 2's devex HIGHs:
- the phase graph (P4 now needs P2, and the eval files ship in P1);
- `workflow.item` against `RCP-WHEN-001` and `RCP-WIRE-002`;
- the migration plan (S1-D9 matches `LedgerDatabase.LoadMigrations` and the gap-free 1..n runner);
- the profile keys (S1-D11);
- the hotspot append rule;
- W27, which now matches S1's `Output.Error`.

Two problems remain that would make phases fail under `/specToProvenPR`:
- **The prover can't run the signals.** The prover is an agent. By the doc's own actor table (§6.2) it's a `session`,
  and it has no TTY. So it can't run any `human` or `human-terminal` step in the P2, P4, P5, P6 or P7 signals.
- **P4's replay step contradicts §8.** The step's expected result conflicts with `SND-WF-009`, and the prover can't
  obtain the nonce the step needs.

Several mediums are things an implementer would hit and have to invent: per-outcome required outputs, how the
in-process validator resolves a child recipe in P1, the `workflow.route` key's shape, and the dev profile's keys per
phase.

Time-to-first-success: ~12 min to validate a recipe (P1), ~25 min to a first instance moving (P4). Both are for the
owner at a real terminal; an agent can't reach the second at all.

## Findings by severity

### CRITICAL

None.

### HIGH

- **Setup / Phases (new): the `/specToProvenPR` prover can't run the P2, P4, P5, P6 or P7 signals.**
  - **Why it can't:**
    - The prover is an agent session. `/specToProvenPR` step 4 says to "test it by hand in the running app … the real
      CLI or API call".
    - Every command it runs inherits `CLAUDECODE` and `CLAUDE_CODE_ENTRYPOINT`, which §6.2 classes as `session`.
    - Its Bash tool also has no TTY, which S1's `CommandTable.Interactive()` checks (`!Console.IsInputRedirected &&
      !Console.IsOutputRedirected`).
    - So `recipe approve`, `recipe revoke`, `workflow start`, `complete`, `takeover`, `handback` and `answer --grant`
      are all refused. The S1 prerequisite `sindri profile approve <hash>` is refused too.
  - **What's left for the prover:** P4 step 4 ("From a Claude Code session … exits 2 with `SND-WF-007`") is the only
    `human`-class step it can run. The rest of P2, P4, P5 (start, takeover, handback), P6 (start, complete, revoke)
    and P7 can't be proven by the role the Phases section assigns them to. Unsetting the markers is a "crafted bypass"
    (§13), so the prover may not work around it.
  - Evidence: §6.2 lines 449–453; Phases "Roles" line 934 ("Prover: runs the phase Signal in the running app");
    signals at lines 977–981, 1015–1021, 1035–1038, 1056–1060, 1075–1080; line 881 (unsetting the harness markers);
    `skills/specToProvenPR/SKILL.md:55`; S1 `CommandTable.cs:67`.
  - Fix:
    - Mark each signal step **agent** or **owner**.
      - Agent steps are `recipe validate`, `recipe eval`, `recipe register`, the read verbs, `outbox list`, the
        notifications file and the `SND-WF-007` refusal.
      - Owner steps are every `human` and `human-terminal` verb.
    - For owner steps, the prover writes a numbered runbook (`$STAGE_DIR/owner-steps.sh`, plain commands with the
      `export AW_STATE_DIR` and the `alias` at the top).
    - The owner runs it at a terminal under `script -q owner.log`, and the prover checks `owner.log` against the
      expected lines.
    - Say in the Phases preamble that P2 and later each need one owner session of about 10–15 minutes. Make the CLI
      golden and integration tests, which use a fake channel or terminal port, the repeatable proof of the same
      behaviour.

- **API surface / Phases (new): P4 signal step 3 contradicts §8, and its `<used>` nonce can't be obtained.**
  - **Expected result:** §5.3 step 1 (key first, then `AlreadyApplied`, exit 0) and §10
    `Used_nonce_returns_already_applied` say a replay exits 0. §8 `SND-WF-009` ("The nonce was used, expired or is for
    another version") says the same replay is an error, exit 2 under W27. Nonce validity is "Unused, unexpired, and for
    the current version" (§5.4), and the version has already moved on.
  - **The likely failure:** an implementer who validates the nonce in the service before calling `Commit` (the natural
    slice shape) returns `SND-WF-009`, and P4 fails.
  - **Which key `complete` uses:** §5.4 keys desk reports by run token (`--run`), while §6.3 says every write verb takes
    `--nonce`. The doc never says which of the two is the `wf:{instance}:{trigger}` key for `complete`.
  - **The `<used>` nonce isn't available:** step 2 ran without `--nonce`, so the CLI fetched one internally. Neither the
    plain output nor the event's trigger id (the run token, if that is the key) shows it.
  - **Step 4 has an ordering problem too:** "the same `complete`" from a session is a replay. If the key check runs
    before the actor check, it exits 0, not 2.
  - Evidence: lines 333, 349–350, 357–359, 478, 595, 634, 1020–1021.
  - Fix:
    - Define the key for `complete` as the run token, with the nonce as a freshness check only.
    - Fix the order: actor check → key exists → `AlreadyApplied` (exit 0) → nonce, token and version checks. Reword
      `SND-WF-009` to "a nonce for another instance or version, or expired, on a trigger not yet applied".
    - Rewrite step 3 as "re-send the first complete, `--run 1.1` and the same outcome: exits 0 with `already applied`",
      or have `--json` echo the nonce used.
    - Run step 4 on a fresh instance.

### MEDIUM

- **API surface (new): `OutputSpec` can't express "required on outcome X".**
  - §2 has `OutputSpec` as `(fact, required)`. §12 needs `step.checks`' `fact:findings` "required on `failed`", and
    `step.verify`'s "required on `rejected`".
  - P1(e) can't write those descriptors as specified. P4's malformed-report check (`SND-WF-008`, "a missing required
    output") also has no rule to apply.
  - Evidence: lines 66, 73, 379–380, 759–761.
  - Fix: `OutputSpec(FactKey Fact, IReadOnlyList<OutcomeName>? RequiredOn)`, where null means optional and empty means
    always required. Or split per outcome. It's part of the descriptor hash.

- **Phases / API surface (new): P1's signal validates `workflow.item`, but two mechanisms it needs aren't defined.**
  - **Child recipes:** `RCP-CHILD-001` and `RCP-WHEN-001` need the child `workflow.step`'s terminals, but in-process
    validation has no registry (that's P2). The doc doesn't say whether the child is resolved from a sibling file by
    id, from the registry, or from `ValidationContext`.
  - **Blocks P1 doesn't build:** `gate.route.startable` (P7) and `effect.claim.ref` (P6) must be in P1's catalog. W1
    puts the descriptor on the block class, and W24 says no fixture code ships. So P1 has to ship those classes with
    some runtime body, and the doc doesn't say what.
  - Evidence: lines 25, 48, 424, 616, 692–694, 717, 960.
  - Fix: in-process `validate` and `eval` resolve `waitFor.child.recipe` from `<dir>/<id>.yaml` beside the file, and
    `serve` resolves it from the approved registry. P1(e) ships the P6/P7 block classes with descriptors and a body
    that returns `SND-WF-010` (fail closed, W5), and their phases replace the body without changing the descriptor.

- **Setup (new): one `recipes/dev/profile.yaml` from P1 can't hold keys that land in P4, P6 and P7.**
  - S1's profile is `JsonUnmappedMemberHandling.Disallow`. A dev profile carrying `workflow.deskTimeout` (§12),
    `effects` and `dispatchDelay` (the P6 signal) or `route` (P7) is `SND-PROFILE-002` in every phase before the one
    that adds the key.
  - The dev loop tells every phase to "install `recipes/dev/profile.yaml` … `sindri profile approve` it".
  - The loop also doesn't name the path. S1 uses `$AW_SINDRI_HOME/profile/profile.yaml` or `AW_SINDRI_PROFILE`, and
    approving takes `profile approve <hash>` plus the first 6 characters typed. P7's profile must also allowlist
    `effect.claim.ref`.
  - Evidence: lines 442–443, 566, 680–681, 1055, 1074; S1 `SindriProfile.cs:15`, `docs/csharp/sindri.md:24–25, 49`.
  - Fix:
    - P1 ships the dev profile with S1 keys only (`schemaVersion`, `user`, `repos`).
    - Each S1-D11 owner adds its keys to it in the same unit.
    - Dev loop: `export AW_SINDRI_PROFILE=$PWD/recipes/dev/profile.yaml`, then `sindri profile approve` (prints the
      hash), then `sindri profile approve <hash>`.
    - Note that P2–P4 don't need a profile, since `serve` runs without one.

- **API surface / Phases (pass 2, still open): the `workflow.route` key, the router stub's trigger and the item→repo
  mapping are unspecified.**
  - **P6(d):** it builds `effect.claim.ref` "in the item's repo". S1's `RepoEntry(Name, Path)` has no item mapping, and
    the only planned source is P7's `route` key.
  - **P7:** the doc doesn't say what makes `router.stub` emit `item.routed` for T-3, or where its `fact:stepSpec`
    content comes from.
  - Evidence: lines 566, 692–694, 700, 920–921, 1074–1076; S1 `RepoEntry.cs`.
  - Fix: define `workflow.route: { <item>: { repo: <repos name>, outcome: startable|notStartable, stepSpec: <path> } }`.
    The stub emits on the instance entering `triaged`. `claim.ref` resolves the repo through that entry, else the
    single `repos` entry, else it refuses with `SND-WF-010`. Move the item→repo part into P6(d).

- **Phases (new): the P6 revoke step depends on a recipe shape nobody pins, inside a 10 s window.**
  - **The shape:** `workflow.dev-notify` is only "a desk Step, then a transition with `effect.notify.desk`". If that
    transition goes to a terminal, which is the natural reading, T-5 is `done` before the revoke. The wake then parks
    nothing, and `outbox.refused` reaches a terminal instance (behaviour undefined). Step 2's `parked: spec-conflict`
    then fails.
  - **The window:** the owner must type the revoke challenge (`revoke workflow.dev-notify@<hash>`) within 10 s of the
    `complete`.
  - Evidence: lines 682, 1055–1060, 362–364.
  - Fix: the effect transition enters a `wait` (for example `waitFor: answer`) before a terminal. Say what
    `outbox.refused` does to a terminal instance (it records the event and changes no state). Set
    `dispatchDelay: 60s` in the dev profile, or tell the owner to open the revoke prompt in a second terminal first.

- **API surface (new): the actor classes arrive in P4, but P2's signal already needs them.**
  - `HarnessMarkers` and the actor table are P4(b). P2 is the first phase with `human-terminal` verbs and an
    architecture test about them.
  - The doc doesn't say whether a non-terminal or session `approve` is refused with `SND-RECIPE-004` or `SND-WF-007`.
    P2's signal assumes the former.
  - "script+" (§6.3) implies an order of actor classes that isn't defined: `session` can do `workflow.request`, which
    `script` can't.
  - Evidence: lines 449–454, 464, 580, 593, 918, 980.
  - Fix: move channel and actor derivation into P2's contract unit. Rule: wrong channel for a challenge is
    `SND-RECIPE-004`, wrong class for any other verb is `SND-WF-007`. Replace "script+" with an explicit list per verb.

- **Error messages (new): the session refusal tells a human in Cursor's or VS Code's terminal to "run it yourself at a
  terminal".**
  - Cursor's integrated terminal exports `CURSOR_TRACE_ID`, which §6.2 counts as a harness marker. So does any shell
    started from inside a Claude Code session.
  - The owner is at a terminal and is refused with a fix they believe they've already followed.
  - Evidence: lines 449, 580, 593.
  - Fix: the message names the marker it saw (`SND-WF-007 … session (CURSOR_TRACE_ID is set) (fix: run it from a
    terminal outside the editor or agent)`). `sindri doctor` gains a `channel` line.

- **Ramp-up cost (pass 2, wrongly resolved): "`/new-slice` generates the service, the records and the red test" is
  still false for Sindri.**
  - **The surface must be `http`:** `NS-SURFACE` requires `--surface` to equal the package host. `packageShape` reads
    S1's `Aw.Sindri.Host.csproj` (`Sdk="Microsoft.NET.Sdk.Web"`) as `http`, so `--surface none` is refused.
  - **The endpoint edit then fails:** `--surface http` inserts on `_ = app.MapGroup(...)`. S1's `EndpointTable.cs` has
    `var group = app.MapGroup("/sindri")`, so the edit fails with `NS-EDIT` and the scaffold writes nothing.
  - Evidence: line 551; `scripts/scaffold/new-slice.mjs:37, 199`; `lib/repo.mjs:143–144`; S1 `EndpointTable.cs:15`.
  - Fix: either give P1's contract unit a scaffold fix (accept `var group = app.MapGroup(` as an anchor, plus the
    `/sindri` prefix), or say slices are hand-written from `docs/csharp/scaffolds/new-slice.md`. Also note that the
    doc's routes (`/workflow/instances/...`) sit under S1's `/sindri` group.

- **Phases (new): the doc contradicts itself on when P3 may start.**
  - The Phases preamble says "Each phase after it starts once S1 has merged". Wave 2 starts P3 when "P1's contract
    unit is pushed", and P3 has "S1 dependency: none".
  - Evidence: lines 887–888, 905, 992.
  - Fix: "P2 and later start after S1 merges, except P3, which stacks on P1".

### LOW

- **Docs:** the `<state>` in `<state>/notifications.jsonl` is ambiguous between `AW_STATE_DIR` and S1's `AW_SINDRI_HOME`
  (`$AW_STATE_DIR/sindri-app`). Evidence: line 482, 1057; S1 `docs/csharp/sindri.md:40`. Fix: write
  `$AW_SINDRI_HOME/notifications.jsonl`.
- **API surface:** in `workflow.item`, the child `inputs: { spec: { fact: fact:stepSpec } }` uses the key `spec`, but
  `workflow.step` declares `inputs: [fact:spec]`. Evidence: lines 707, 717, 737. Fix: key the map by fact key
  (`fact:spec: …`), or say the prefix is implied.
- **API surface:** in the flagship recipe, `running`'s 2d `park` timeout is shorter than the child's minimum desk time.
  With three `deskCapable` Steps at ≥ `deskTimeout` (2d default) each, the parent parks on most real items. Evidence:
  lines 44, 716, 740–745. Fix: 7d, or "the sum of the child's timeouts".
- **Phases:** the P7 signal starts `workflow.item` without the declared `fact:item` input. Evidence: lines 707, 1075.
  Fix: add `--fact fact:item=<file>`, and say what `start` does about a missing input.
- **Phases:** the P2 signal hard-codes `9ab2d40c1e77`, which is illustrative. Evidence: line 979. Fix: "`<hash12>`
  approved (desk)".
- **Phases:** no contract unit owns the `workflow` group command table. P4(b), (c) and (e) all add verbs to it.
  Evidence: lines 551, 918. Fix: add it, and its arm in S1's `CommandTable`, to P4's contract unit.
- **API surface (pass 2, open):** the doc still doesn't say whether `recipe approve <file>` registers the file first.
  Evidence: lines 464–466, 977. Fix: one sentence.
- **Docs (pass 2, open):** S1-D4 is still missing from §7.2 with no note. Evidence: lines 557–566. Fix: "S1-D4:
  withdrawn".
- **Docs:** no phase builds an MCP server, yet §10 lists `Mcp_is_stamped_session` and `Ui_and_mcp_reference_no_terminal_route`
  in phases 2 and 4, and §5.2 lists `workflow.request`. Evidence: lines 452, 317, 648. Fix: say MCP is out of these
  phases, and that the architecture test checks the UI assembly only until the MCP assembly exists.
- **Error messages (pass 2, open):** "the profile when there is one" (§6.1) still doesn't say whether `RCP-EFFECT-001`
  reads the live file or the approved snapshot. Evidence: line 424. Fix: the approved snapshot, with the live file as
  a warning only.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 7 |
| Setup / install (time-to-first-success: ~12 min to validate, ~25 min to a first instance, owner at a terminal) | HIGH | 2 |
| Error messages & observability | MEDIUM | 2 |
| Docs completeness | LOW | 3 |
| Backwards compatibility | None (S1-D9 checked against `LoadMigrations` and `EmbeddedMigrations`; it works) | 0 |
| Ramp-up cost (tribal-knowledge items: 10) | MEDIUM | 2 |
| Phases (signals, graph) | HIGH | 5 |

**Time-to-first-success assumptions:**
- **P1, ~12 min:** a worktree off the S1 branch, a locked restore, a 3–5 min build, then `dotnet run … -- recipe
  validate`, in-process with no profile.
- **P4, ~25 min:**
  1. Run the dev loop exports.
  2. Start `serve` (no profile is needed).
  3. `recipe approve` the step recipe at a real terminal, with its evals run.
  4. `start`, then three `complete` calls.
  5. Read `events`.

**Tribal-knowledge list:**
1. Agent sessions, and Cursor's integrated terminal, are `session` class. Every `human` and `human-terminal` signal
   step needs the owner at a plain terminal.
2. `/new-slice` on Sindri must use `--surface http`, and it aborts on S1's `var group = app.MapGroup("/sindri")`.
3. S1's routes live under `/sindri`.
4. The profile path is `$AW_SINDRI_HOME/profile/profile.yaml` (or `AW_SINDRI_PROFILE`). Approving is `profile approve
   <hash>` plus 6 typed characters, with `serve` running.
5. The profile is closed: each `workflow:` key is valid only from the phase that adds it.
6. Which trigger key `complete` uses (run token or nonce), and the order of the key, actor and nonce checks.
7. How in-process validation finds a child recipe.
8. What the P6/P7 blocks' classes do in P1.
9. The `workflow.route` shape, which repo an item maps to, and what triggers the router stub.
10. How an output that's required only on some outcomes is declared.

## Verdict

Two HIGH findings would fail phases under `/specToProvenPR`:
- the agent prover is a `session` with no TTY, so it can't run the `human` and `human-terminal` steps of the P2, P4,
  P5, P6 and P7 signals;
- P4's replay step contradicts `SND-WF-009`, and it needs a nonce the CLI never shows.

Both can be fixed in the doc. The rest are mediums and lows an implementer would otherwise have to invent.

verdict: NEEDS_WORK
