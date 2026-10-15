# Plan Devex Review (pass 2) — sindri-workflow-engine

**Plan:** `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md` (revision 2)
**Reviewed:** 2026-10-10
**Checked against:** `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`, `scripts/scaffold/new-slice.mjs`, and
the S1 branch (read only) at `.claude/worktrees/sindri-s1-app-shell` (`docs/csharp/sindri.md`, `Commands/Output.cs`,
`Commands/CommandTable.cs`, `SindriErrors.cs`, `Profile/SindriProfile.cs`, `Ledger/LedgerDatabase.cs`,
`Aw.Kernel.Data/MigrationRunner.cs`, `EmbeddedMigrations.cs`, `DataErrors.cs`).

## Summary

Most pass-1 devex findings are resolved:
- the process model and addressing (§6.1);
- block authoring (W1, §2);
- the errors doc path (§8 in `docs/csharp/sindri.md`);
- telemetry and the eval format;
- most of S1 change ownership (W26, §7.2).

Still open or new:
- **Phase signals and the graph.** P2's signal still needs P3's eval files. P4 needs P2's approvals, but the graph says
  it doesn't. The flagship `workflow.item` fails its own `RCP-WHEN-001`.
- **The parallel-team section breaks on the real tree.** The reserved migration ranges can't work with Kernel.Data's
  gap-free 1..n runner, which also numbers S1's outbox migration after Sindri's own. Units can't make a zero-warning
  red commit without editing `PublicAPI.Unshipped.txt`, a hotspot they may never touch.
- **Pass-1 fixes that are wrong.** The exit codes and the error format, both said to "follow S1", contradict S1's
  `Output.Error`.
- **Unowned S1 changes.** The effect allowlist and the profile "mode" that several rules read don't exist in S1's
  closed profile schema.

Time-to-first-success: ~12 minutes to validate a recipe (P1), ~35 minutes to see a first instance move (P4).

## Findings by severity

### CRITICAL

None.

### HIGH

- **API surface / Phases (pass-1 C10, not fully resolved):** the phase signals still can't pass in their own phase,
  and the graph is missing an edge.
  - **P2's signal needs P3's files.** It approves `recipes/sindri/workflow.step.yaml`, and approval needs the `evals`
    files to exist. Those files land only in P3 (`recipes/*/evals/**`), and P2 runs in parallel with P3 (wave 2).
    P1(e) writes the recipe YAML but no eval files.
  - **P4 needs P2, but the graph says it doesn't.** `RCP-CAP-003` refuses `start` from an unapproved hash, and
    `ApplyCommand` takes the approvals port. P4's signal starts `workflow.step`, so it needs P2's `IRecipeApprovals`
    and an approved recipe. Yet the graph and wave 3 ("P2 continues if it hasn't finished") give P4 no P2 dependency.
  - **P3 edits P2's slice while both run.** P3(f) "approve running the evals" changes P2's `ApproveRecipe` slice while
    P2 is unmerged and running in parallel.
  - Evidence: line 137 (`evals` … "to approve"); 848–849 ("Until P3, approval requires the `evals` files to exist");
    851 (P2 signal); 863–864 (P3 files); 269 (`RCP-CAP-003` "start"); 466 (`ApplyCommand` takes "the approvals");
    759–771 (graph, waves); 783 (P3(f)); 887–888 (P4 signal).
  - Fix: move the first recipes' eval files (`recipes/sindri/evals/**`, and any `recipes/dev/evals/**`) into P1(e).
    Add a P2 → P4 edge (at least on P2's contract unit, plus P2 merged before P4's prover runs). Move "approve runs
    the evals" into P3 as a decorator P3 owns, or into P4 after P2 merges.

- **API surface (recipe authors):** `workflow.item` fails `RCP-WHEN-001` and `RCP-WIRE-002`.
  - **`RCP-WHEN-001`:** §3.6 limits a child wait's outcomes to "a child's terminal outcomes plus `cancelled`".
    `workflow.step` has one terminal, `done: resolved`. So `running`'s `when: { outcome: alreadySatisfied }` and the
    `any` over `unresolved` and `failed` name outcomes the source doesn't declare.
  - **`RCP-WIRE-002`:** the `triaged → startable` transition `produces: [fact:stepSpec]` from the `item.routed`
    event, but the §12 events table gives that event outcomes only, no facts.
  - Either way, P7's signal fails at approve.
  - Evidence: lines 197–199 (§3.6), 189 (`produces` rule), 598–601 (events table), 623–624 and 629–631
    (`workflow.item`), 648 (`workflow.step` terminals).
  - Fix: give `workflow.step` terminals for the outcomes the parent handles, or drop those transitions from
    `workflow.item`. Add a Facts column to the §12 events table (`item.routed`: `fact:stepSpec`). Add both recipes to
    P1's signal as "validates against the catalog", so this fails in P1, not P7.

- **Backwards compatibility / Parallel execution:** the reserved migration ranges can't work with Kernel.Data, and
  adding any Sindri migration shifts S1's outbox migration.
  - **No ranges are possible.** `MigrationRunner` takes "versions 1..n in order". A gap is `AW-DATA-005`, and a
    database whose `schema_version` isn't exactly 1..n is `AW-DATA-006`, which Sindri maps to `SND-LEDGER-001`.
    Ranges such as "P2 `W0xx`, P4 `W1xx`" can't exist.
  - **The outbox migration moves.** `LedgerDatabase.LoadMigrations` appends the outbox migration at `own.Count + 1`
    (version 3 today). P2's first `.sql` file moves the outbox to version 4, and every existing ledger then fails
    with `AW-DATA-006` on checksum drift.
  - **Parallel phases collide.** P5 and P6 run in parallel and both add migrations, so they race for the same next
    number.
  - Evidence: line 793 (reserved ranges), 722–724 (index migrations per phase), 467 (seven new tables); S1
    `LedgerDatabase.cs:103–107`; `MigrationRunner.cs:45`; `DataErrors.cs:18–25`.
  - Fix: add S1-D9 (owner P2): pin the outbox migration at version 3, with Sindri's own files numbered from 4 after
    it. Replace the ranges with "the integrator assigns the next free number at merge, in merge order". Units write
    `NNNN_<name>.sql` with a placeholder number. For dev ledgers built by earlier phases, say they are thrown away
    (`AW_STATE_DIR=$(mktemp -d)`).

- **Setup / Backwards compatibility:** the effect allowlist and the profile "mode" that the rules read don't exist,
  and no phase owns adding them.
  - **The rules that read them:** `RCP-EFFECT-001` ("Allowlisted effects only"), `RCP-CAP-007`'s run-time
    intersection, P6's dispatch-time allowlist recheck, and `gate.route.startable` ("Stub over the profile mode").
  - **What S1's `SindriProfile` has:** `schemaVersion`, `user`, `tenant`, `repos`, `services` and `api`, with
    `JsonUnmappedMemberHandling.Disallow`. A developer can't add `effects:` or `mode:` without `SND-PROFILE-002`.
  - **The effect:** once a profile exists, every effect-bearing recipe (`workflow.dev-notify`, `workflow.item`) fails
    `RCP-EFFECT-001`, so the P6 and P7 signals can't pass.
  - §6.1's "the profile from disk" is also ambiguous: it doesn't say whether that is the live file or the approved
    snapshot. S1's rule is that runtime reads only approved snapshots.
  - Evidence: lines 151, 273, 393, 476, 525, 592; S1 `SindriProfile.cs:15–22`; S1 `docs/csharp/sindri.md` Profile
    ("unknown keys are errors", "Runtime reads only these").
  - Fix: add S1-D10 (owner P1, or P6 at the latest): optional profile keys `effects: [<effect id>…]` and `mode`, as
    S1 allows ("new optional keys join version 1"), with their hash and validation. State that validation reads the
    approved snapshot, and that the live file is reported only as a warning.

- **Ramp-up cost / Parallel execution (new):** under the hotspot rules, units can't make their required commits or
  prove their own work.
  - **The red commit can't build.** csharp-testing.md requires a red commit that "compiles" with "zero warnings".
    Every unit adds public types to Core or Features (records, ports, request and response records), and each one
    raises `RS0016` (an error under `-warnaserror`) until `PublicAPI.Unshipped.txt` lists it. That file is a hotspot
    "units never edit".
  - **Units can't test their own wiring.** The `Add…()` calls and the command-table arms are the integrator's. A unit
    whose deliverable is CLI golden files or loopback endpoints can't run them in its own worktree, for example
    P1(f), P4(e) and P6(c).
  - **The rule for `SindriErrors.cs` contradicts itself.** "Units never edit these files", yet its rule is "reserved
    ranges", and `SindriErrors` is one non-partial static class.
  - Evidence: lines 786–794, 799 ("It never touches a hotspot file"), 778, 781; csharp.md line 110 (take the
    PublicAPI lines from `RS0016`); csharp-testing.md line 35.
  - Fix: let units append to `PublicAPI.Unshipped.txt`, which is line-sorted, and mark it `merge=union` in
    `.gitattributes`. The integrator re-sorts it. Make `SindriErrors` and the `Add…()` hosts `partial`, with one file
    per unit. Let a unit add its own command arm in a per-group table file, which §7.1 already plans. Keep the
    integrator only for the cross-unit files that are left.

- **Error messages / API surface (pass-1 MEDIUM, wrongly resolved):** the exit codes and the error format both say
  they follow S1, and both contradict it.
  - **Exit codes:**
    - The plan uses 1 for "refused, not found, conflict" and 2 for "can't reach `serve`".
    - S1's shared `Output.Error` returns 1 only for `SND-API-001` (not running) and 2 for every other error.
    - S1's `status` exits 1 when not running.
    - `profile validate` exits 2 when the profile is invalid, while the plan's `recipe validate` exits 1 for failed
      rules.
    - `profile approve` with no terminal exits 2 (`SND-PROFILE-004`), while P2's signal expects `echo | sindri
      recipe approve` to exit 1.
  - **Error format:** the plan prints `error: <message> (<code>)`. S1 prints `SND-<AREA>-NNN message (fix: …)`.
  - **The result:** implementers either change S1's `Output`, which no phase owns and which breaks S1's CLI golden
    files, or write a second error printer.
  - Evidence: lines 422–424, 513, 854; S1 `Output.cs:26–33`; S1 `docs/csharp/sindri.md` command table and "Errors go
    to stderr as …".
  - Fix: adopt S1's actual mapping and format, and list the exit codes per verb. Or record the change as an owned S1
    item (S1-D11), with the S1 golden updates it implies. Rewrite P2's signal to match.

### MEDIUM

- **Docs / Parallel execution:** the reserved error-code ranges don't match where the codes are needed.
  - `SND-WF-010` (a Step with no runtime parks) is in P6/P7's range, but P3 needs it for W5's
    `Step_without_runtime_parks` test.
  - P1's `recipe validate <missing file>` has no code in P1's single code: `SND-RECIPE-002` is P2's.
  - Codes `008` and `009` are skipped without a reason.
  - Evidence: lines 791, 506, 551, 493.
  - Fix: give P3 `SND-WF-010`, or move the fail-closed test to P6. Give P1 a not-found code. Say whether `008` and
    `009` are reserved or free.

- **Ramp-up cost (pass-1 MEDIUM, wrongly resolved):** "`/new-slice` generates them" is false for this CLI.
  - `/new-slice` accepts only one kebab word (`NS-COMMAND` regex `^[a-z][a-z0-9-]{1,40}$`), so it makes
    `recipe-validate`, not `recipe validate`.
  - It inserts its arm above `_ => CommandOutput.Usage`, and S1's `CommandTable` ends in `_ => await
    UnknownVerb(...)`, so its `NS-EDIT` anchor doesn't match.
  - It also edits the registration and JSON-context hotspots.
  - Evidence: line 468; `scripts/scaffold/new-slice.mjs:68–69, 190`; S1 `CommandTable.cs:52`.
  - Fix: say that slices come from `/new-slice --surface none` and that group verbs are hand-written in the
    per-group table files. Or put "`/new-slice --group <name>`" on a phase as a scaffold change.

- **Ramp-up cost / Phases:** P1 and P2 change S1's files before S1 merges, against W26 and wave 3's rule.
  - W26 says phases own their S1 changes "once S1 merges", and wave 3 waits for S1's merge because "P4 changes S1
    code". But P2 owns S1-D5 and runs in wave 2, before the merge.
  - P1 and P2 also edit S1's `CommandTable.cs`, `Composition.cs`, `SindriErrors.cs`, `Usage.cs` and
    `docs/csharp/sindri.md` while the S1 session is still changing them.
  - Evidence: lines 46, 49, 478, 746–747, 767–769.
  - Fix: define "S1 change" (a change to S1 behaviour, as opposed to additive registrations and doc rows). Allow
    additive edits on the stacked branches, and move S1-D5's challenge into new Sindri files, or gate P2's S1-D5
    unit on the merge.

- **API surface (pass-1 addressing, partly resolved):** addresses and `start` don't select an instance or version
  exactly.
  - **Child addresses drop the entry.** A child's `InstanceId` is `{parent}/{child recipe}.{parent entry}`, but its
    address is `…/<child-recipe-name>`, with no entry. A parent that re-enters a child wait (after a retry, or in a
    later recipe with a loop) gets two children at one address.
  - **`start` doesn't say which version it runs.** `workflow start <recipe>` doesn't say which approved
    version or hash it uses. Pass-1 asked for this, and it's still unstated.
  - Evidence: lines 284, 395–399, 412; absent — searched "highest approved", "latest approved", "`id@hash`" in §6.
  - Fix: let the address resolve the child of the latest entry, and accept `…/step.<entry>` explicitly. Have `start`
    use the highest approved version unless it's given `id@hash`, and show the version in `status`.

- **Setup / Phases:** the P4, P6 and P7 signals have steps a prover can't run as written.
  - **P4:** "Re-sending a `complete` with its used nonce" can't be done through the CLI, which fetches the nonce
    itself and has no `--nonce` flag. The exit code for `AlreadyApplied` isn't given either.
  - **P6, the notification:** the signal starts `workflow.dev-notify`, but effects belong to transitions, not to the
    start. It doesn't say which `complete` triggers the effect, or where "a desk notification appears" (a log, the
    ledger, or a macOS notification).
  - **P6, the revoke:** "After `recipe revoke`, a second instance's effect is refused at dispatch". But a second
    instance can't start after the revoke (`RCP-CAP-003`). The order has to be: start, revoke, then `complete`.
  - **P7:** it doesn't say which repo `effect.claim.ref` writes into, so the prover doesn't know where to run `git
    show-ref`. It also doesn't say what makes the router stub emit `item.routed` for T-3.
  - Evidence: lines 892, 924–927, 940–941, 592–593.
  - Fix: give P4 a hidden `--nonce` flag or a curl line, and say that `AlreadyApplied` exits 0. Spell out the P6
    sequence and the `notify.desk` sink. Name the claim repo (the profile's `repos` entry for the item, or the
    current working directory) and the stub's trigger.

- **Setup (pass-1 dev loop, partly resolved):** the dev loop omits the profile.
  - The loop is only `AW_STATE_DIR` plus `serve`. But `{user}` in `next`, the claim ref `sindri/{user}/{item}` and
    the allowlist all need an approved profile with `user`.
  - S1 has no `profile init`, approving needs a TTY and the first 6 characters of the hash, and the signals call
    `sindri`, which isn't on the PATH.
  - Evidence: lines 568–569, 615; S1 `docs/csharp/sindri.md` Files and Profile.
  - Fix: add the minimal `profile.yaml` (`schemaVersion: 1`, `user`, `repos`, and `effects` once it exists), `sindri
    profile approve <hash>`, and `alias sindri='dotnet run --project src/Sindri/Aw.Sindri.Host --'`.

### LOW

- **Docs:** the S1-D2 citation `SqliteLedger.cs:56` is now line 40 (`InsertOnce`). Evidence: line 475. Fix: cite the
  method name, not a line.
- **Ramp-up cost:** "`BlockContract` … runs each block's contract tests once, for every recipe that uses it" is still
  self-contradictory, as pass-1 noted. Evidence: lines 104–105. Fix: "once per registered block, plus a catalog test
  that every block a recipe names has a binding".
- **Phases:** P4's signal lists `complete` outcomes without the `--output fact:output=<file>` that `produce → checks`
  requires. Evidence: lines 650, 890. Fix: write the full commands, as §6.3 does.
- **Phases:** "then it parks a further 40 s later" ignores the 15 s timer cadence. Evidence: lines 351, 907. Fix:
  "within 40–55 s".
- **API surface:** it isn't clear whether `recipe approve <file>` registers implicitly. P2's signal approves a file
  that was never registered. Evidence: lines 407–409, 851. Fix: say that approving a file registers it first.
- **Docs:** S1-D4 is missing from the S1 changes table without a reason. Evidence: lines 474–481. Fix: note it as
  withdrawn.

## Per-dimension summary

| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 6 |
| Setup / install (time-to-first-success: ~12 min to validate, ~35 min to a first instance) | HIGH | 3 |
| Error messages & observability | HIGH | 2 |
| Docs completeness | MEDIUM | 3 |
| Backwards compatibility | HIGH | 2 |
| Ramp-up cost (tribal-knowledge items: 11) | HIGH | 4 |

**Time-to-first-success assumptions:**
- **~12 min to validate:** a worktree off the S1 branch, a locked restore, a 3–5 min solution build, then `dotnet run
  … -- recipe validate <file>`, in-process with no profile.
- **~35 min to a first instance:**
  1. Hand-write and approve a profile at a TTY.
  2. Start `serve`.
  3. Register and approve `workflow.step`, which needs its eval files.
  4. `workflow start`, then three `complete` calls with outputs.
  5. Read `workflow events`.

**Tribal-knowledge list:**
1. Kernel.Data migrations must be gap-free 1..n, and S1's outbox migration is numbered `own.Count + 1`.
2. Every new public type in Core, Features or Infrastructure needs its `PublicAPI.Unshipped.txt` lines, or the
   zero-warning build fails with `RS0016`.
3. S1's `Output.Error` exits 1 only for `SND-API-001` and 2 for everything else, and prints `CODE message (fix: …)`.
4. `/new-slice` takes one-word kebab commands and anchors on `_ => CommandOutput.Usage`, which S1's table lacks.
5. S1's profile is closed: it has no effect allowlist or mode, and unknown keys are errors.
6. The dev loop needs an approved profile with `user` (claim refs, `{user}`), written by hand and approved at a TTY
   with the hash's first 6 characters.
7. `sindri` isn't installed; the signals assume an alias for `dotnet run --project src/Sindri/Aw.Sindri.Host --`.
8. A unit can't run its own CLI or endpoint tests until the integrator wires its registration and command arm.
9. Which recipe version `workflow start <id>` runs.
10. Where a `notify.desk` notification appears.
11. Which repo `effect.claim.ref` writes the claim ref into.

## Verdict

Six HIGH findings remain, and no CRITICAL one:
- the P2 and P4 signals and the graph's missing P2 → P4 edge;
- `workflow.item` failing its own `RCP-WHEN-001` and `RCP-WIRE-002`;
- migration ranges that Kernel.Data can't express, which also shift the outbox migration;
- an effect allowlist and profile mode that S1's closed profile lacks;
- hotspot rules that block a zero-warning red commit;
- exit codes and an error format that contradict S1 while claiming to follow it.

Each one can be fixed in the doc.

verdict: NEEDS_WORK
