# Plan Devex Review (pass 4) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 4; scratchpad copy `wf-engine-pr/`)
**Reviewed:** 2026-10-11
**Checked against:** `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`, `skills/specToProvenPR/SKILL.md`, and the
S1 branch (read only) at `.claude/worktrees/sindri-s1-app-shell` @ `ae8b6da3` (`docs/csharp/sindri.md`,
`Commands/CommandTable.cs`, `Endpoints/EndpointTable.cs`, `Core/Profile/SindriProfile.cs`, `Ledger/Migrations/`).
**Scope:** only (1) earlier devex findings that are still open and (2) new problems in revision 4. Resolved items and
§13 answers that hold up are left out.

## Summary

Revision 4 fixes most of pass 3's devex findings: the (agent)/(owner) split, the `Commit` order, `requiredOn`, how a
child recipe is resolved, P1 shipping the P6/P7 block descriptors, the dev profile growing one phase at a time, the
route and repo shape, the `/new-slice` note, the P3 wave, the `dev-notify` wait and the marker-naming refusal.

Two problems would still make a phase signal fail:
- **The owner's runbook and the Prover never share a server.** Every **(agent)** step and every "the Prover checks the
  ledger" step is a client of `sindri serve`. The client finds the server through `$AW_SINDRI_HOME/control.json`. The
  dev loop gives each terminal its own `mktemp -d`, so the Prover's CLI looks in the wrong home.
- **In P6, the engine's own desk-task notification can satisfy `dev-notify`'s `sent` wait.** Then T-5 reaches `done`
  before the revoke, and step 2's `parked: spec-conflict` never shows.

The rest are mediums an implementer or runbook writer would otherwise trip on, mostly in the P2, P4, P5 and P7 signals,
plus one conflict with how `/specToProvenPR` handles branches.

Time-to-first-success: about 12 min to validate a recipe (P1, agent). About 30 min to a first instance moving (P4): the
owner works at a terminal and the Prover checks the result.

## Findings by severity

### CRITICAL

None.

### HIGH

- **Setup / Phases (new): the (owner) runbook and the Prover's (agent) steps aren't tied to one server or state
  directory.**
  - **How the client finds the server:** S1's client reads `$AW_SINDRI_HOME/control.json`, which defaults to
    `$AW_STATE_DIR/sindri-app` (S1 `docs/csharp/sindri.md`, "Files"). If that file is missing, or names a pid that
    doesn't answer, the result is `SND-API-001`, exit 1.
  - **Why the two halves miss each other:** the dev loop (§6.1) runs `export AW_STATE_DIR=$(mktemp -d)` in each
    terminal. The Phases "Prover" bullet says the owner runs the runbook in a plain terminal. Nothing says:
    - who starts `sindri serve`;
    - that the runbook prints its `AW_STATE_DIR`;
    - that the server is still running after the runbook ends;
    - that the Prover exports the same directory before it runs its steps or `workflow events`.
  - **What fails:**
    - P2's "(agent) … exits 2 with `SND-WF-007` naming the marker" exits 1 (`SND-API-001`) instead.
    - So does P4 step 4's (agent) `complete`.
    - Every Prover check (`workflow events` in P2, P4, P5 and P7; `outbox list --json` in P6) reads an empty or
      foreign home, or none at all.
  - **Unchecked assumption:** the Prover runs in Claude Code's sandbox. The doc assumes that sandbox can reach
    `127.0.0.1:7410` and read the owner's `/var/folders/…` temporary directory. Neither is stated or checked.
  - Evidence: plan lines 491–501 (dev loop), 1073–1076 (Prover), 1119–1121, 1155–1162; S1 `docs/csharp/sindri.md`
    (`control.json`, `AW_SINDRI_HOME`, `SND-API-001`). Absent — searched "AW_STATE_DIR", "serve", "runbook",
    "control.json" in Phases.
  - Fix: add a "Runbook contract" to the Phases Roles section:
    - The Prover picks the state directory, for example `$STAGE_DIR/state`, and writes it at the top of the runbook as
      `export AW_STATE_DIR=…`.
    - The runbook's step 0 is `sindri serve &` (or bare `sindri`), so the owner's server runs as the owner.
    - The runbook ends without running `sindri stop`. The Prover then runs its (agent) steps and the ledger checks with
      the same `AW_STATE_DIR` exported, and runs `sindri stop` last.
    - If the sandbox blocks loopback, the Prover runs those steps with the sandbox off for loopback only, and says so
      in `manual-test.md`. Or it checks the ledger read-only from `ledger.db`.

- **Phases P6 (new): the desk-task notification's `notify.delivered` can satisfy `dev-notify`'s `sent` wait, so step 2
  never reaches the revoke park.**
  - **The notification:** §6.3 says an instance's first desk task interrupts once, as an engine-owned `notify.desk`
    command. For `dev-notify`, that is `ask`'s task, enqueued at `start` and dispatched 60 s later (`dispatchDelay:
    60s`).
  - **The event:** the dispatcher writes `notify.delivered` (producer `system:dispatcher`, §12). The doc doesn't limit
    that event to recipe effects. The router resolves its target through `workflow_commands`, which maps the command
    to the instance and to `ask`'s entry (§5.4, §5.7).
  - **The row:** if the owner takes more than 60 s between `start` and `complete`, the row is written while T-5 is in
    `ask`. That row was "written before any matching wait", so it is held (§5.7) and "applies … in the next matching
    wait". That wait is `sent`. T-5 goes to `done` within one inbox tick (5 s) of the `complete`.
  - **The result:** the revoke then parks nothing. `status T-5` shows `done: resolved`, not `parked: spec-conflict`, and
    signal step 2 fails.
  - **How likely:** more than 60 s is likely. The owner has to read `status` for the run token, and every `dotnet run`
    call rebuilds first. Step 1 (T-4) "passes" for the same wrong reason.
  - Evidence: plan lines 392–395, 433–440, 548–552, 762–766, 1203–1210.
  - Fix (any one of these):
    - The dispatcher writes `notify.delivered` only for recipe-emitted effects, and engine-owned notifications write
      nothing back.
    - A row resolved through `causation` (via `workflow_commands`) applies only to the frame entry it names, and is
      never held for the "next matching wait". The early-hold rule is for events resolved by `item` and attempt.
    - Add an eval case or a unit test: `Engine_notification_delivery_doesnt_satisfy_a_recipe_wait`.

### MEDIUM

- **API surface (pass 3, still open in part): the code for a `script`-class `approve` is undefined, and P2's
  `< /dev/null` step depends on it.**
  - §6.2 classes "CLI without a TTY, no marker" as `script`, and `script` can't run `approve`.
  - §4.2 gives `SND-RECIPE-004` "when the request came from a human without a TTY", but no actor class like that exists.
  - The generic actor refusal (§5.3 step 1) is `SND-WF-007`. An implementer who follows the table returns 007, and P2's
    owner step expects 004.
  - Evidence: lines 257–259, 351, 505–512, 1119–1121.
  - Fix: "A `script`-class request for a `human-terminal` verb is `SND-RECIPE-004`, and a `session`-class one is
    `SND-WF-007`." Also add a §10 test, `Script_channel_approve_is_recipe_004`.

- **Phases P4 (pass 3 HIGH, still open in part): step 3's second half can't be run as written, and its expected code
  depends on a check order the doc doesn't give.**
  - **The nonce isn't available:** step 3 needs "the nonce from `--json`", but step 2's first `complete` runs without
    `--json` or `--verbose`. The nonce isn't in the event either, because a desk report's trigger id is the run token
    (§5.4).
  - **The expected code depends on order:** with a "new `--run`" on an instance that is already `done`, three checks
    could fire:
    - the run-token check (`SND-WF-005`, §5.4 "valid when");
    - the state check (`SND-WF-003`, the trigger isn't allowed in a terminal state);
    - the nonce check (`SND-WF-009`).

    §5.3 orders the actor, key, nonce, fence, approval and version checks, but doesn't place the token or state checks
    among them.
  - Evidence: lines 351–360, 373, 543–544, 1155–1160.
  - Fix:
    - Step 2's first `complete` takes `--json`, so its nonce is recorded.
    - Name the new token (`--run 1.2`).
    - In §5.3, put the token and state checks after the nonce check, or change the expected code to whichever check
      comes first.

- **Phases P5 (new): the second instance's address is unnamed, and the bare-address rule sends the final `answer` to
  the wrong instance.**
  - **Starting the second instance:** `start workflow.dev-timeout --item T-2` a second time returns the existing
    instance (§6.1). The runbook writer will therefore pick either a new item or `--attempt next`.
  - **Answering the first one:** with `--attempt next`, the final step `sindri workflow answer T-2 --resume` addresses
    "the root of the item's latest attempt", which is the taken-over second instance. The answer is `SND-WF-003`, and
    the first instance stays parked.
  - **The timing:** a 20 s takeover window and a ±15 s tolerance are tight when each call goes through
    `dotnet run --project …`, which builds first. The alias also doesn't expand in a non-interactive `bash runbook.sh`.
  - Evidence: lines 487–488, 494–496, 1176–1186.
  - Fix:
    - Name the instances: `T-2` and `T-2b`, answered as `T-2/dev-timeout/1`.
    - In the dev loop, build once and use `SINDRI=src/Sindri/Aw.Sindri.Host/bin/Debug/net10.0/sindri` or
      `dotnet run --no-build`.
    - Write the runbook as typed steps, not a script that relies on an alias.

- **Phases P7 / API surface (new): the P7 signal's profile and inputs miss three things the recipe needs.**
  - **The allowlist:** `effect.claim.ref` must be allowlisted in `workflow.effects`, or it is refused at dispatch and
    T-3 parks with `spec-conflict: dispatch`. The signal lists only the temp repo and `workflow.route`, while P6's
    signal names its allowlist.
  - **The spec:** `router.stub` copies `fact:stepSpec` "from `fact:item`'s spec" (§12), but `fact:item` is defined as
    `{ id, repo }` with no spec field. The child needs `fact:spec` as file content (§6.4 shows `spec.md sha256:…`).
  - **The budget:** `gate.budget.available` "reads the profile budget" (§13), but no S1-D11 row and no S1 profile field
    defines a budget key. S1's profile is closed (`SindriProfile`: `schemaVersion`, `user`, `tenant`, `repos`,
    `services`, `api`).
  - **The repo:** `git init` alone leaves no commit for `sindri/{user}/T-3` to point at.
  - Evidence: lines 642, 776–792, 816, 999–1000, 1214–1231; S1 `Core/Profile/SindriProfile.cs:16–22`.
  - Fix:
    - Make `fact:item` `{ id, repo, spec }`, with `spec` a repo-relative path that the stub reads.
    - Add `workflow.budget.tokens` to S1-D11 (P7), or say the gate passes when the key is absent.
    - The P7 signal names `effects: [effect.claim.ref, effect.notify.desk]` and "a temp repo with one commit".

- **Phases (new): the dev recipes have no evals named anywhere, but P5 and P6 must approve them.**
  - §3.2 makes `evals` required to approve, and approval refuses missing evals (`SND-RECIPE-008`).
  - §12 lists evals only for `recipes/sindri/`. The P3 signal scaffolds a case for `workflow.dev-timeout`, which
    suggests it ships without one.
  - If P1(e)'s "recipes/** including the evals" is read as the real recipes only, `workflow start workflow.dev-timeout`
    (P5) and `dev-notify` (P6) refuse with `SND-RECIPE-003`.
  - Evidence: lines 133, 271, 759–766, 1098, 1139.
  - Fix: §12 lists `recipes/dev/evals/workflow.dev-timeout/happy.jsonl` and `…/workflow.dev-notify/happy.jsonl`, made
    in P1(e). The P3 scaffold step writes to a scratch copy.

- **API surface (pass 3 LOW, still open): the child input map key `spec` doesn't match `workflow.step`'s declared
  `fact:spec`.**
  - §13 says "child inputs use `{ fact: <key> }`", but the key in `inputs: { spec: { fact: fact:stepSpec } }` is still
    `spec`.
  - If `RCP-CHILD-001` checks that the child's declared inputs are covered, `workflow.item` fails validation, and so
    does P1's signal.
  - Evidence: lines 808, 828, 1008.
  - Fix: key the map by fact key (`inputs: { fact:spec: { fact: fact:stepSpec } }`), or state that the `fact:` prefix is
    implied.

- **Docs (pass 3 written answer, wrong): the notifications path `$AW_STATE_DIR/sindri/notifications.jsonl` is in the TS
  sindri's directory.**
  - S1 keeps the C# home at `$AW_STATE_DIR/sindri-app`, "apart from the TS sindri's `$AW_STATE_DIR/sindri` until the
    switch".
  - The P6 signal checks "the notifications file".
  - Evidence: lines 548, 1007; S1 `docs/csharp/sindri.md` "Files".
  - Fix: `$AW_SINDRI_HOME/notifications.jsonl`.

- **Phases vs `/specToProvenPR` (new): the waves and the stacked bases go against the skill's git and merge rules, and
  the doc doesn't say how to reconcile them.**
  - The skill says "each stage gets its own branch off base", and "after the human merges, start the next stage".
  - The doc's plan:
    - P1 and P3 stack on `feat/sindri-s1-app-shell`, not base.
    - P3 starts on P1's contract unit while P1 is still in review.
    - Reviews overlap the next wave.
  - That can work, but stages.md's Git topology contract and Autonomy contract have to record it. The Orchestrator
    also has to rebase P3 after every P1 review fix, and re-run P3's step 5 proof if a rebase changes P1 source that P3
    uses (skill step 9).
  - Evidence: Phases lines 1027–1050, 1077–1083; `skills/specToProvenPR/SKILL.md:45–47, 72`.
  - Fix: one paragraph in Phases saying:
    - stages.md records base = S1's branch for P1 and P3, retargeting to `main` once S1 merges;
    - autonomy = continuous, with human merges as the only waits;
    - a rebase onto changed parent code re-runs the child phase's proof.

- **Phases P2 (new): P2(e), "the harness probe and its golden test", needs runs in Codex (each sandbox mode) and in
  Cursor's agent and integrated terminal. A Claude Code implementer can't make those runs.**
  - The probe runs aren't in the P2 owner runbook, so the unit can't finish on its own.
  - Evidence: lines 517–520, 1095.
  - Fix: add them as **(owner)** steps in P2: one `env | sort` and `ps -o comm= -p $PPID` chain per harness, captured to
    `$STAGE_DIR/probe/<harness>.txt`, from which P2(e) builds the golden file.

### LOW

- **Phases P4:** step 2 has no **(agent)**/**(owner)** tag, but `complete` is `human`. Evidence: line 1155. Fix: mark
  it **(owner)**.
- **Phases:** each runbook starts from a fresh state directory, but the prerequisites aren't listed:
  - write the dev profile, `profile approve <hash>`;
  - `recipe approve` each recipe the phase uses, with its evals from P4 on;
  - create the input files (`spec.md`, `out.md`, `item.json`).

  P4's "approved (P2)" doesn't carry across. Evidence: lines 1153–1154. Fix: a "Runbook prelude" list per phase.
- **API surface (pass 3, still open):** `recipe register` is still `script+`, an order of actor classes the doc doesn't
  define. Evidence: line 528. Fix: list the classes, for example "human, script".
- **Phases:** hotspots leave out `docs/csharp/sindri.md`'s error table, which several units append codes to, and the
  group tables are switch expressions, where "append" means "before the default arm". Evidence: lines 1066–1069.
  Fix: add both to the hotspot list.
- **Phases:** each phase's contract unit commits red tests that other units own, so an implementer's own test run
  includes failures from other units. Evidence: lines 1051–1052. Fix: units run their own test classes
  (`--filter-class`), and only the Integrator runs the full lanes.
- **Phases:** "The session cap is respected" doesn't give the number. Evidence: line 1081. Fix: give the number, or
  name where it is set.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | MEDIUM | 4 |
| Setup / install (time-to-first-success: ~12 min P1, ~30 min P4) | HIGH | 3 |
| Error messages & observability | MEDIUM | 1 |
| Docs completeness | MEDIUM | 2 |
| Backwards compatibility | None | 0 |
| Ramp-up cost (tribal-knowledge items: 8) | MEDIUM | 1 |
| Phases (signals, waves, units) | HIGH | 8 |

**Time-to-first-success assumptions:**
- **P1, about 12 min:** a worktree off S1, a locked restore, a 3–5 min build, then `recipe validate` in-process. This
  needs no profile or server.
- **P4, about 30 min:**
  1. Run the dev loop.
  2. Write and approve the profile.
  3. Start `serve`.
  4. `recipe approve` with the evals run.
  5. `start` and three `complete` calls, each about 5 s through `dotnet run`.
  6. The Prover replays the checks against the same state directory.

**Tribal-knowledge list:**
1. The Prover and the owner must share one `AW_STATE_DIR`, and the server has to stay up after the runbook.
2. Whether Claude Code's sandbox allows loopback calls to `sindri serve`.
3. `dotnet run` builds on every call, which matters for the P5 timing windows. The alias also doesn't expand in a
   script.
4. A bare `T-2` resolves to the latest attempt.
5. Engine-owned notifications also produce `notify.delivered`.
6. Which code a `script`-class `approve` gets.
7. The P7 profile must allowlist `effect.claim.ref`, and the doc doesn't define the budget key.
8. Dev recipes need eval files before anyone can approve them.

## Verdict

Two HIGH findings would fail phase signals. The owner runbook and the Prover don't share a server or state directory,
so the P2 and P4 (agent) steps exit 1 and every ledger check reads the wrong home. In P6, the desk-task notification's
`notify.delivered` can complete `dev-notify` before the revoke. Both take a few lines in the doc to fix. The mediums
are concrete gaps in the P2, P4, P5 and P7 signals and one branch-topology note for `/specToProvenPR`.

verdict: NEEDS_WORK
