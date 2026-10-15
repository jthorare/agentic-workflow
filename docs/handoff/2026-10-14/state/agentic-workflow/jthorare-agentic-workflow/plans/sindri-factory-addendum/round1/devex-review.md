# Plan Devex Review — sindri-factory-addendum

**Plan:** docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md (amends docs/superpowers/specs/2026-10-07-sindri-design.md, rev 7)
**Reviewed:** 2026-10-09

## Summary
The plan is a spec addendum, not a plan dir: there is no `plan.md`, `engineering.md` or `TASKS.md`, so the API surface was judged from §17.4–§17.14 and the rollout from §17.16. The API was checked against the existing CLI conventions in `sindri/src/cli.ts`, `docs/sindri/README.md` and `docs/sindri/errors.md`. The schemas are well specified for the reader, but the plan gives authors little to work with. Packets can be validated and frozen, but nothing creates or amends one. Rules and lenses have no versioning, no effective-set view, and no way to bind lens commands. Out-of-scope denials, the main new debugging loop, reuse the `SND-SCOPE` area that already belongs to `sindri scope`, and nothing shows a Step's compiled session spec. The plan also changes four existing skills to "sindri mode" and moves their state to per-worktree keys, with no compatibility or migration contract. Time-to-first-success (first `sindri packet validate` passing on a real repo): about 75 minutes.

## Findings by severity

### CRITICAL
None.

### HIGH
- **API surface ergonomics:** The packet lifecycle has only `validate` and `freeze`. Nothing creates a packet from a scope map, and nothing amends one, so authors hand-write a multi-part directory against a Zod schema. Amendment is a core flow ("only the affected tasks are re-planned and re-frozen"), but it has no verb, no command to show which tasks are affected, and no hash-chain display.
  - Evidence: plan lines 160–185, which name `sindri packet validate` and `sindri packet freeze` and no other packet verb. Absent: searched "packet init", "packet new", "packet amend", "scaffold", "from scope map".
  - Fix: Add `sindri packet init --from <scope-run|scope.md> [--out DIR]` to F0. It should write a skeleton `packet.yaml` with every required part stubbed, plus open questions carried over from the scope map. Add `sindri packet amend <id> --task <ids>` and `sindri packet diff <hashA> <hashB>`. Specify `validate --json` output as one record per failed check, each with a code, a path into the packet and a fix line, matching `profile validate`.

- **Error messages & observability:** Sandbox denials are given `SND-SCOPE-1xx`, but `SND-SCOPE-0xx` already means scoping errors from `sindri scope` (12 codes in `docs/sindri/errors.md`, and `SND-SCOPE-025` in main spec line 1067). Grepping a denial code will turn up scoping errors. The plan also gives no way to see why something was denied. There is no command to print a Step's compiled session spec or sandbox policy, and the denial payload is not defined (which `write` glob or allowlist failed, and which packet field to amend). The "three denials park the Step" rule makes this the most frequent debugging loop in factory mode.
  - Evidence: plan line 215, "A denied action returns `SND-SCOPE-1xx` with the reason"; lines 189–200 (session spec fields); errors.md rows `SND-SCOPE-001` … `SND-SCOPE-020`. Absent: searched "explain", "session spec show", "dry-run", "policy show".
  - Fix: Move sandbox denials to a new area, `SND-SANDBOX-1xx`, or into `SND-GATE-1xx`, and seed it in `errors.ts`. Define the denial record: `{code, step, tool, target, ruleSource: write|read|command|network|mcp, matchedAgainst, suggestedAmendment}`. Add `sindri step spec <step> [--json]` and `sindri step why-denied <step>` to F2, following the pattern of `profile explain <key>`. Add a compile dry-run, `sindri packet compile --dry-run`, that prints every Step's spec before freeze.

- **Backwards compatibility / migration:** `/specToProvenPR`, `/bugFixOrchestrator`, `/review` and `/addressReview` become Step implementations "in sindri mode", with no asks and with evidence and review state "keyed by worktree". The plan doesn't say how a skill detects sindri mode, how interactive mode stays byte-identical, or what happens to existing state files such as `~/.agentic-workflow/<slug>/reviews/<pr>.json`, which `/postReview` and `/addressReview` read today.
  - Evidence: plan lines 320–324. Absent: searched "migration", "detect", "env", "flag", "backward", "state path".
  - Fix: Write a `skills/_shared/sindri-mode.md` contract. It should cover the detection signal (one env var or flag; the main spec line 698 already ignores some env flags in sindri mode, so pick one the gate trusts), the asks-become-parks mapping, and the state-path rule. Make that rule additive: worktree-keyed paths are used only in sindri mode, and the existing paths stay unchanged for desk use. Add a regression test per skill that interactive mode is unchanged.

- **Ramp-up / skill authoring (§17.14):** The factory-mode skill worker depends on a "skill lint" for "preamble, frontmatter, size budget, and no interactive asks on sindri-mode paths". The plan defines no budget, no way for a skill to mark which paths are sindri-mode, and no format or location for "pressure scenarios". It also doesn't say how these relate to `sindri evolve check` module eval suites. Skill authors, human or agent, can't satisfy a lint whose rules aren't written down.
  - Evidence: plan lines 465–473. Absent: searched "lint", "size budget", "pressure scenario", "sindri-mode paths" for a definition.
  - Fix: Specify the lint in F1 (it gates sindri mode in the two skills). Cover the rule list, the budget in lines or tokens, the marker syntax for sindri-mode sections (for example a fenced `<!-- sindri-mode -->` region), and the location of pressure scenarios (`skills/<name>/evals/pressure/*.md`). Run the lint as `sindri skill lint [<name>]` and add it to the merge gate.

### MEDIUM
- **API surface ergonomics:** The rule, lens and workflow schemas have no `schemaVersion` and no JSON Schema export (only the packet gets one). The main spec makes `schemaVersion` plus a strict schema plus a JSON Schema export the convention for user-authored YAML.
  - Evidence: plan lines 231–240 (rule) and 267–280 (lens), with no version field; line 160–161, where only the packet gets a JSON Schema export; main spec lines 1157–1166.
  - Fix: Add `schemaVersion: 1` to rule, lens, workflow-template and packet records, export JSON Schema for each in F0, and state the same growth rule (new optional keys stay v1; renames bump the version and add a migration).

- **API surface ergonomics:** Lens `commands` use placeholders (`"<repo migrate cmd> --dry-run"`) with no binding mechanism. `select` matching semantics are unstated: AND across keys or OR within a list, the glob dialect, and what `"*"` means. `check.type` lists only `eslint`.
  - Evidence: plan line 276; lines 236–237.
  - Fix: Bind command names to keys in the repo profile (`repos/<name>.yaml: commands: { migrate: …, testDb: … }`) and write lens commands as `{cmd: migrate, args: [--dry-run]}`. Document selector semantics and the closed `check.type` enum in the rule schema doc.

- **API surface ergonomics:** The plan has no way to see the effective rule set or lens selection. Rules merge across four layers by id, and selection ranks by "specificity and recent violations", but no command shows the merged library, which layer won, or why a rule was or wasn't injected for a Step.
  - Evidence: plan lines 242–246. Absent: searched "rule list", "rule show", "lens list", "explain".
  - Fix: Add `sindri rule list [--effective --repo R] [--json]`, `sindri rule explain <id> --step <step>` (layer provenance plus selector match trace) and `sindri lens list|show` to F0, next to the loaders.

- **API surface ergonomics:** The evidence provider contract conflicts with sindri mode. `publish` keeps "ask-first rules … per provider", but sindri mode has no asks. The plan doesn't say what `publish` does unattended (park, skip, or defer to the ship Step).
  - Evidence: plan line 397 against line 322.
  - Fix: State that in sindri mode `publish` returns a pending-publish item for the swipe inbox (§17.12), and never publishes on its own.

- **Setup / install friction:** There is no worked example or fixture for the first factory run. F0's value is "packets, rules and lenses are checkable", but no example packet, rule or lens ships for ring 0 (this repo), so an author starts from a blank directory. Estimated time to first success: about 75 minutes. That assumes sindri is already installed and approved, the author reads §17.4 and the Zod source to find required fields, and writes 1 workstream and 2 tasks by hand, with 3–5 validate iterations. With `packet init` and an example, it would take about 10 minutes.
  - Evidence: plan line 494. Absent: searched "example", "fixture", "quick start".
  - Fix: Ship `sindri/packet/example/` (a ring-0 packet for one real Sindri plan task) plus example rule and lens files. Add a "Quick start: factory" block to `docs/sindri/README.md` in F0's "Turn it on" task.

- **Setup / install friction:** Host handover (§17.13) has no preflight, dry-run or progress output. It also doesn't define the partial-failure state (target unreachable mid-sync, or a checkpoint that doesn't reach a safe point) or a rollback.
  - Evidence: plan lines 454–461. Absent: searched "preflight", "dry-run", "rollback", "abort", "timeout".
  - Fix: Specify `sindri host handover <to> --dry-run` (lists running Steps, their checkpoint ETA and the sync size), `sindri host status`, an abort path that leaves the source active with its epoch unchanged, and error codes for each failure.

- **Error messages & observability:** The plan seeds none of its new error areas. The packet, rule, lens, workflow, evidence, host, session-image and quota checks all need codes, and `docs/sindri/errors.md` is generated from a registry whose test fails on undocumented codes.
  - Evidence: absent. Searched `SND-` in the plan; only `SND-SCOPE-1xx` and `SND-GATE-1xx` appear (lines 209, 215).
  - Fix: List the new areas (PACKET, RULE, LENS, WORKFLOW, EVIDENCE, HOST, QUOTA, SANDBOX) in §17.16 F0, with one code per `packet validate` check (lines 174–181).

- **Error messages & observability:** Lean bootstrap says "anything loaded that the plan didn't name is a defect with a metric", but doesn't say where the defect appears, which record or doctor line it produces, or how a developer finds what leaked in and from which settings layer.
  - Evidence: plan lines 60–61 and 339–341.
  - Fix: Define a `context-leak` ledger event (`{step, lens, source: skill|agent|mcp|rule|memory, name, tokens}`) and a `sindri doctor` line per lens, plus `sindri step context <step>` showing first-call sections against their budget.

- **Documentation completeness:** The addendum lists no docs to write or update. The main spec lists the Sindri doc set (lines 1593–1600), but the new surfaces (packet, rules, lenses, workflows, standing-workflow tiers, sandbox denials, host handover, phone enrollment, ship contract) have no `docs/sindri/*.md` target, and the README command table and "When to use which" table aren't updated.
  - Evidence: absent. Searched "docs/sindri", ".md", "README" in the plan; only `skills/_shared/ship-contract.md` is named (line 382).
  - Fix: Add a docs row to each F step: `packet.md` (F0), `rules.md` and `lenses.md` (F0, generated from the schemas like `profile.md`), `factory.md` (F2: compile, sandbox, debugging denials), `host-setup.md` handover section (F7) and `review-anywhere.md` (F4, passkey enrollment). Update the README command table each time.

- **Backwards compatibility / migration:** The metrics revision replaces the headline metric and drops the −20% total-turns condition from the auto-start kill criterion, but doesn't say how existing ledger rows and the scorer or audit reports are reclassified. Turns are now split into build turns and desk turns.
  - Evidence: plan lines 65–77.
  - Fix: Define the turn-class field, its default for past rows (for example `unclassified`, excluded from the −75% baseline), and which scorer output changes.

- **Ramp-up cost:** The plan reuses terms the repo already uses for other things. "Lens" already means verification lenses (`skills/_shared/verification-lenses.md`) and plan-review lenses (`/autoplan`). "Factory" collides with `skills/_shared/dark-factory.md` (local-inference delegation). A lean host "profile" collides with both the Sindri profile and `setup.sh --profile`, which the README already has to disambiguate.
  - Evidence: plan lines 262–299 and 343–345; README line "Not to be confused with `./setup.sh --profile`".
  - Fix: Add a glossary to §17.0, and rename where it's cheap, for example "development lens" consistently and a "lean session mode" instead of a "lean profile".

### LOW
- **API surface ergonomics:** The plan introduces `/rule` and `sindri attach` but doesn't place them in a rollout step or give their arguments.
  - Evidence: plan lines 156 and 436; absent from §17.16.
  - Fix: Assign each to an F step and give a one-line synopsis.
- **API surface ergonomics:** The holdout storage location is unspecified ("stored where the implementer can't read it"), so authors can't place it.
  - Evidence: plan line 180.
  - Fix: Name the path, for example `$AW_STATE_DIR/sindri/holdouts/<packet-id>/`, outside every worktree, and add the read path to the gate's deny list.
- **Documentation completeness:** The Q2 candidate list is marked "not re-verified", which is fine for options, but F5 has no doc on how to write an adapter.
  - Evidence: plan line 514.
  - Fix: Point F5 at `docs/sindri/adapters.md` (already in the main spec's doc list) and add the component interfaces there.

## Per-dimension summary
| Dimension | Worst severity | Findings |
|---|---|---|
| API surface ergonomics | HIGH | 7 |
| Setup / install (time-to-first-success: ~75 min) | MEDIUM | 2 |
| Error messages & observability | HIGH | 3 |
| Docs completeness | MEDIUM | 2 |
| Backwards compatibility | HIGH | 2 |
| Ramp-up cost (tribal-knowledge items: 12) | HIGH | 2 |

**Tribal-knowledge list** (facts a new contributor or agent would have to be told):
1. Where a packet directory lives (in the repo, under `$AW_STATE_DIR`, or in the profile repo).
2. Where holdout examples are stored and how the implementer is kept from reading them.
3. How lens `commands` placeholders resolve to a repo's real commands.
4. The paths of the four rule layers (core defaults, `rules/`, profile overlay, packet) and their precedence on disk.
5. That sandbox-denial codes (`SND-SCOPE-1xx`) are unrelated to scoping errors (`SND-SCOPE-0xx`).
6. How a skill detects that it is running in sindri mode.
7. The per-repo test-path convention used to widen `write` scope.
8. The change-class taxonomy (`schema`, `migration`, `query`, …) and where it is defined.
9. The task-kind enum (`feature`, `refactor`, bug, …) used by rule selectors.
10. Size classes' token, turn and wall-time budgets per lens.
11. How to rebuild the per-(repo, toolchain) session image and where its managed settings live.
12. How to enroll a phone passkey and which decision kinds it may sign.

## Verdict
Four HIGH findings drive the verdict: there is no way to create or amend a packet, sandbox denials have colliding error codes and can't be inspected, the sindri-mode change to four existing skills has no compatibility contract, and the skill lint is undefined.
verdict: NEEDS_WORK
