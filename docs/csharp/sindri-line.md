# The line: design and plan checks, packet assemblers, the stop-2 view

Phase **LN-K** of the spec-to-proven-PR line (`docs/superpowers/specs/2026-10-15-sindri-spec-to-proven-pr-line-design.md`, §4, §6, §12). This page is what the code does and the codes it uses; the spec says why.

The checks are one pure function per station over the packet's **files**, held in `LineFacts` (`Aw.Sindri.Core.Line`): a name to its text, `.json` and `.jsonl` parsed once. The names a station reads:

| Station | Files it reads |
|---|---|
| design | the design packet's own (`spec.md`, `sections.json`, `coverage.json`, `decisions.json`, `acceptance.json`, `signals.json`, `contracts.json`, `lens-rounds.jsonl`, `style.json`, `escapes.json`, `runs.json`), the scope packet's under `scope/`, a style or lens pack's `pack.json` as `pack/style.json` and `pack/lenses.json`, the repo files a contract edit quotes under `repo/`, and `options.json` (`multiVendor`) |
| plan | the plan packet's own (`tasks.json`, `graph.json`, `exec-review.json`, `holdout.json`, `answers.jsonl`), the design packet's under `design/`, aw-packets' answer as `validation.json` (`{ passed, failures[] }`), and `options.json` (`maxPhaseSize`, `maxStack`, `holdoutMin`, `parallel`) |

A rule whose files are absent is **not run** and names them; it never fails a draft, and at the evidence stage a rule that did not run is never a pass (the outcome is `incomplete`). A file that is there but does not parse is a failing finding on the rule that reads it. The findings are #258's shape (`rule`, `item`, `message`, `fix`, `severity`); a warning is shown and never changes an outcome.

## Checks

`DesignPacketChecks` (`Features/Design`) and `PlanChecks` (`Features/Plan`). aw-packets' `PKT-DSN-*` and `PKT-PLN-*` rules check the shape of the files; these check what they mean, so no rule is in both tiers. `PLN-PKT-001` is aw-packets' own answer, passed in as `validation.json`.

| Rule | Stage | Fails when |
|---|---|---|
| `DSN-COV-001` | draft | a scope surface is in no section's `covers` and not excluded with a reason, a `coverage.json` row disagrees with the sections, or a section covers an id the scope map lacks |
| `DSN-COV-002` | draft | a scope question is not answered at the scope stage, not carried by a decision or section citing it, and not deferred in `coverage.json` with an owner and a `buildMayAssume` |
| `DSN-ACC-001` | draft | a live item has no signal, or a signal has an unknown evidence kind, an unknown `runsAt`, no statement, or a `script` that is not a relative path inside the workspace (no `..`) |
| `DSN-ACC-002` | draft | a signal uses an unmeasurable term (`fast`, `quick`, `clean`, `intuitive`, `easy`, `seamless`, `robust`, `user-friendly`, `smooth`, `simple`, `snappy`) or names no number and quotes no reference |
| `DSN-ACC-003` | evidence | a scope item is gone or its text changed, an item is `passing`, a superseded item has no successor, or an added item cites no section or decision. A supersession is a warning: stop 2 confirms it |
| `DSN-DEC-001` | draft | a decision has fewer than 2 or more than 3 options, not exactly one recommended, an option with no trade-offs, no citations, or nothing affected, or an affected section lacks the `assumes` marker in `sections.json` or `spec.md` (the chosen option once answered, else the recommended one) |
| `DSN-CITE-001` | draft | a section cites nothing, or an id that resolves to no scope surface, acceptance item, decision, section, answer, ref or lens item |
| `DSN-XSPEC-001/002/003` | draft | an edit's target file or heading is not at the base commit, its quote is not in the target, or it has no numbered row naming it in "Rows to other lanes", shares a lane, or changes a span another edit changes. 001 and 002 need the repo's files and are not run without them |
| `DSN-STYLE-001` | draft | `spec.md` breaks the style pack's sections (unknown, missing, out of order, over their word limit), uses a forbidden phrase or is over the line cap |
| `DSN-LENS-001` | evidence | a round skipped a lens of the pack, or the last round leaves a CRITICAL or HIGH finding the owner did not accept at the lens desk (`acceptedByOwner`) |
| `DSN-LENS-002` | evidence, warning | the lens model is the writer's, or, with `options.json` `multiVendor`, the lens vendor is |
| `DSN-LENS-003` | evidence | a finding of any round has no written resolution |
| `DSN-ESC-001` | evidence, warning | an item or surface the scope packet lacks is not in `escapes.json` |
| `PLN-PKT-001` | draft | aw-packets did not pass the plan packet; one finding per failed check |
| `PLN-ACC-001` | draft | a live design item is in no phase or in more than one, a phase words an item differently, or carries one that is not live |
| `PLN-SIZE-001` | draft | a phase is larger than `maxPhaseSize` (`M`); the files, items and budget limits of a size are PKT-SIZE-001's |
| `PLN-SIG-001` | draft | a phase has no `kind:` rule, an item of a `fix` phase has no `base+head` signal, or a phase that is not `docs` or `config` has no real-app signal (`ui-browser`, `api-transcript`, `cli-transcript`, `ios-sim`) |
| `PLN-STACK-001` | draft | a phase depends on a phase that does not exist or on itself, one with more than one dependency is not `waitsForMerge`, or a stack is deeper than `maxStack` (4) |
| `PLN-EXEC-001` | evidence | the executability review did not cover a phase or called it not executable (its `contextMissing` is in the message) |
| `PLN-HOLD-003` | evidence | a phase has no hidden set, no `sha256:` set hash, no canary, or fewer than `holdoutMin` (2) scenarios besides the canary |

## Assemblers

`DesignPacket.Build` and `PlanPacket.Build` write the candidate packets. Code owns `packet.json` (links), `index.json` (parents and the acceptance chain: `self` is the sha256 of the packet's own `acceptance.json`, `extends` the parent's), the acceptance file's `parent`, statuses (`failing`), evidence (empty) and trust (the scope's; `untrusted` for an item the design added), and the design's escapes (one for each added item and surface the scope packet lacks; reasons the writer gave are kept). `graph.json` is derived by `PlanGraph.Derive` (`Core/Line`): stacks (a phase continues its first dependency's stack unless an earlier sibling did), waves (levels split at `workflow.line.parallel`), bases (`default-branch` or `dependency-head` of the first dependency), the critical path and the PR count. A file that comes back unchanged keeps its bytes, so the gold design and plan assemble byte for byte.

## Parent resolution (LN-PKT6)

`ResolveParents` (`Features/ResolveParents`) checks, before a reader reads a packet, that each hash in its `parents` (`LinePacketIndex.Parents`) has a freeze record (`IFrozenPackets`, over `packets/<owner>/<name>/<id>/frozen/<hex>.json` under `$AW_STATE_DIR`). A parent that does not resolve, or is no hash, is `SND-LINE-001` naming it. A record whose own hash is not the one asked for is not a freeze of it; a hash is checked for shape before it becomes part of a path.

## Verbs (class `local`, LN-API4)

| Verb | Does |
|---|---|
| `sindri design check --facts <dir> [--scope <dir>] [--repo <dir>] [--style <pack.json>] [--lenses <pack.json>] [--stage draft\|evidence] [--json]` | `DesignPacketChecks` over the directory's files. Exit 0 pass, 1 findings, 2 usage, a tool error or serve not running |
| `sindri plan check --facts <dir> [--design <dir>] [--repo <dir>] [--stage …] [--json]` | `PlanChecks`. With `--repo` and a `packet.json` in the directory, aw-packets validates the packet (`PLN-PKT-001`); a tool that cannot run leaves the rule not run |
| `sindri design show` and `plan show` (`--facts <dir>` or an address) | The stop-2 view below, as text or `--json`. An address is a `workflow.plan` instance, read through the route; a session caller's text is inside the pack fence |
| `sindri line desk <design.check\|design.evidence\|plan.check\|plan.evidence> --facts <dir> --out <dir>` | Runs the block Step's code and writes the report a desk `complete` takes: `report.json`, `findings.json` on gaps, and for an evidence Step `design-packet/` or `plan-packet/`, the hash aw-packets prints, and `designPacket.json` or `planPacket.json`. The identity comes from `--packet-id --repo-slug --base-sha` or the directory's `packet.json`, the parents from `--scope-hash --design-hash` or `scopePacket.json` and `designPacket.json` |

An `<address>` of `design check` or `plan check` is looked up (a serve that is down is `SND-API-001`, never started; another recipe's instance is `SND-LINE-003`), but the packet's files are in the run area and the API does not serve them yet, so the verb says to pass `--facts "$AW_SINDRI_RUN_DIR"`.

## The stop-2 view (LN-API2)

`GET /sindri/workflow/instances/{id}/design-stop` on a `workflow.plan` instance returns `DesignStopView` (`Core/Line`): `notYet`, `accepted[]`, `models`, `decisions[]`, `acceptance { added[], superseded[] }`, `escapes[]`, `graph`, `trace[]`, `coverage[]`, `spec`, `contracts[]`, `lensRounds[]` and `freezeRefused?`, in the order §6.4 reads them. It is live before `evidence`: a section whose files are not there is empty and named in `notYet`. Hidden scenarios are a count, never text. The instance's facts become files (`DesignStopFacts`): `fact:plan` is `tasks.json`, `fact:holdoutSets` `holdout.json`, `fact:lensRounds` the lens log and `fact:freezeRefused` the banner; `fact:design` is `schemas/line/design.json` (`sections`, `coverage`, `decisions`, `signals`, `contracts` as arrays), read into the design packet's files of those names (a section's `title` is its heading, a coverage row's `surface` is both its id and key, a signal's `runs` is `runsAt`, a contract edit is named `X<n>` in order). The spec, the acceptance list, the escapes and the run records are not in `fact:design` and no instance fact holds them yet, so `spec`, `acceptance`, `escapes` and `models` read "not yet" on the route (from `--facts` they come from the directory's files). Facts are scrubbed as the facts route scrubs them. The golden is `Golden/Api/workflow-instance-design-stop/gold-stop.json` (pinned by `DesignStopViewTests`).

**Spec, acceptance, escapes and models read the candidate design packet; notYet until it is served.** They are files of the candidate design packet that `step.design.evidence` assembles (`spec.md`, `acceptance.json`, `escapes.json`, `runs.json`), not facts. Acceptance's added and superseded items come from `fact:scope`'s acceptance compared with the design, and the escapes are also `line.escape` ledger rows (LC-7). The follow-up is serving the candidate packet's files over the API (the design analogue of API9); until then the route leaves the four sections "not yet", which the spec allows before evidence.

## Context (LN-CTX1)

`ProposePlanContext` (`Features/PlanContext`) is what `step.plan.freeze` calls after the owner's approve: the design's surfaces the map lacks (the surface escapes and the paths the coverage could not map) go to C9's port with the plan instance as the run, and the owner's final pick of each decision goes to C14's ingest (`IOwnerDecisions`) as a decision record linked to the map nodes the decision cites, tied to the plan's answer log entry (`id`, else `A` and the batch). Nothing is sent from an unapproved draft. An empty surface list sends no proposal (it would withdraw the run's). The ingest does not exist in the context engine yet, so `PendingOwnerDecisions` stores nothing and says so in the response's `reason`.

## Codes

`SND-LINE-001` a packet's hash does not match its freeze record, or a parent resolves to no frozen packet. `SND-LINE-002` a phase start is blocked (LN-L). `SND-LINE-003` a line view asked of the wrong recipe's instance. `SND-LINE-004` `line doctor` found missing setup. The `SND-PACK-*` codes are the pack lane's (LN-PK).

## Profile

The line's keys are in `docs/csharp/sindri-profile.md`. `ProfileRules.Check(profile, box)` refuses `workflow.line.parallel` above the box's heavy-slot cap; the live profile source reads the cap each time (`$AW_STATE_DIR/heavy-slots-cap`, else `AW_HEAVY_SLOTS`). `LineProfile` reads the values with their defaults and writes the sizes file for `aw-packets validate --sizes`.
