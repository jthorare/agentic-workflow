---
name: testAudit
description: "Invoke whenever writing, changing, reviewing, or sweeping tests. Authoring gate for new tests plus audit workflow for low-value, implementation-coupled, or duplicative tests and the test-only production seams they demand."
argument-hint: "[--author | --audit | --campaign <subsystem>] [path-or-scope]"
allowed-tools: Bash(git *), Bash(gh *), Bash(npm *), Bash(npx *), Bash(pytest *), Bash(cargo *), Bash(go *), Bash(bundle *), Bash(SHARED_DIR=*), Bash(source *), Bash(mkdir *), Bash(date *), Bash(cat *), Agent, Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion, TodoWrite, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Test Audit

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

Three modes, one value bar. **Authoring mode** gates every new or changed test at
write time. **Audit mode** runs focused sweeps of tests that re-assert source,
duplicate stronger proof, couple behavior to implementation, or keep test-only
production seams alive. Continue broad audits as separate coherent follow-up
PRs; optimize for confidence, not deletion count. **Campaign mode** prunes one
whole subsystem's test surface (every test file a package, module, or service
owns); before starting one, read [CAMPAIGN.md](CAMPAIGN.md) in this skill's directory.

Adapted from OpenClaw's `test-audit` skill (MIT). See [NOTICE.md](NOTICE.md).

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 0: Pick the mode

Parse the argument:

- **`--author [path]`**, or invoked while writing or changing tests: run the
  [Authoring gate](#authoring-gate) on each new or changed test, then stop.
  Nothing is written to the output directory.
- **`--audit [path-or-scope]`** (default when a path is given without a flag):
  run [Discovery](#discovery) → [Candidate evidence](#candidate-evidence) →
  [Edit shape](#edit-shape) → [Validation](#validation) for a few
  high-confidence candidates.
- **`--campaign <subsystem>`**: follow [CAMPAIGN.md](CAMPAIGN.md) end to end.
- No argument and no tests in the current diff: **Ask the user** which mode and
  scope — options: "Audit a path", "Campaign on one subsystem", "Gate the tests
  in my current diff".

Resolve `$AW_DIR` first. It comes from the shared slug script; source it in the bash block that uses it (shell state does not persist):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
```

Detect the test runner per `$SHARED_DIR/test-runner-detection.md` (sets
`TEST_CMD`). Audit and campaign modes record their evidence in
`$AW_DIR/qa/test-audit/<YYYY-MM-DD>-<scope-slug>.md`
(create the directory if missing). Never write the ledger into the project
directory.

## Authoring gate

Before adding any test, answer four questions; a missing answer means do not
add it yet:

1. What observable behavior, invariant, or independent contract does it protect?
2. What credible regression makes it fail?
3. Why does existing coverage not already catch that failure? Each contract has
   one primary test owner at the strongest boundary; another layer needs its
   own distinct risk, such as a transport or lifecycle failure the owner cannot
   reach. Prefer extending a table-driven case or shared fixture over a
   near-duplicate test; consolidate duplicated setup in the same change.
4. Does it need a production seam (export, flag, wrapper, injection hook) that no
   production caller needs? If yes, move the test to the real boundary instead.

Then check the test against every [junk pattern](#junk-patterns); a match fails
the gate unless the [retention bar](#retention-bar) names the contract it
independently guards. A test that would break under behavior-preserving
refactoring is asserting implementation, not behavior; rewrite it at the
owning boundary before landing it.

Bug regression tests must fail on the pre-fix code for the intended reason and
pass after the owner-boundary repair. A regression test that never demonstrably
failed proves the mock, not the fix. One regression at the owner boundary
covers the bug; do not replay the same scenario at every layer it crosses.

In authoring mode, report each test as **pass** (with its four answers) or
**fail** (with the question or junk pattern it failed and the suggested owner
boundary). Do not land a failed test.

## Junk patterns

The shared checklist for every mode: the authoring gate rejects a new test that
matches one, and audits hunt for existing tests that do.

- assertion-free coverage probes;
- self-comparisons and identity copiers;
- copied fixtures, inventories, manifests, or export lists;
- exact source, import, or string greps;
- private predicate or call-shape tests duplicated at real boundaries;
- duplicate invocations of the same contract;
- per-module replays of shared helpers already tested at their owner;
- tests whose only purpose is preserving test-only exports, globals, or wrappers;
- dead production code whose only callers are tests;
- expected values produced by the helper or renderer under test;
- mocks that implement the asserted behavior, or one identical mock standing in
  for different APIs;
- fixtures that supply the receipt, admission, or callback ordering the owner
  should produce, or persistence asserted against a store the path never writes;
- capability tests that restate declared flags instead of exercising the
  delivery or acknowledgement the flag promises;
- negative controls that pass for an unrelated reason, such as a denial from a
  different guard or a rejection the production path never reaches;
- names or fixtures that promise more than the input exercises, such as a
  "retires the window" test asserting the window was not cleared.

## Value bar

Tests justify their maintenance cost by protecting behavior, a credible
regression, or an independently meaningful contract. In an audit, an existing
test that must change for behavior-preserving source reorganization is suspect,
not automatically deletable; the authoring gate still rejects new ones.

Before judging a candidate, read the complete test and production owner, its
entry point, callers, callees, sibling implementations, overlapping tests, CI
routing, and relevant history (`git log -p --follow <file>`). Read the root and
any scoped `AGENTS.md` files and the `.agents/rules/` files whose globs match
first — a repo's testing rule may already define ownership or coverage policy.
When the test claims dependency-backed behavior, inspect the dependency source
or types directly. Use `mcp: serena/find_symbol` and
`mcp: serena/get_symbol_usages` for callers and non-test usages when available.

## Discovery

Keep discovery read-only and report evidence before editing. For broad scope,
derive **lanes** from the repo's actual top-level layout (for example: core
source, each package or service, UI and apps, scripts and tooling) plus one
cross-cutting junk-pattern sweep, then **Dispatch in parallel** one read-only
subagent per lane (see `$SHARED_DIR/parallel-dispatch.md`). Each subagent
returns candidates with the [candidate evidence](#candidate-evidence) fields
filled in; it must not edit files.

Outside campaign mode, prefer a few high-confidence candidates over a large
speculative inventory. Hunt for the [junk patterns](#junk-patterns).

## Retention bar

Keep a test when it independently enforces a public API, SDK, protocol,
config, migration, storage, security, platform, default, serialized-output,
generated cross-language, package, release, or architecture contract. Also keep:

- call ordering when order is observable behavior;
- regressions with a credible failure mode;
- source inspection when it is the cheapest independent guard: it fails when
  the contract changes (the user-facing key, byte, or path) and survives an
  identifier-only refactor;
- a retained test that fails on the baseline: treat it as a possible product
  bug, reproduce it, and repair the owner rather than deleting it (hand the
  reproduction to `/rootCause` if the cause is not obvious).

Static or slow is not a deletion reason. A test that resembles implementation
may still be the independent contract; prove otherwise before removing it.

## Candidate evidence

Record every field below in the ledger before editing. A missing field means
the candidate is not ready for deletion:

- exact test name and location;
- what failure it can actually detect;
- non-test callers of the covered production or support seam;
- stronger remaining owner-boundary proof, or why no proof is needed;
- relevant history and the reason the test or seam exists;
- production or test-support deletion unlocked;
- risk and the focused validation command.

Show the candidate list to the user, then **Ask the user** which candidates to
act on — options: "All high-confidence candidates", "Let me pick", "Report
only, no edits". Report-only ends the skill at [Handoff](#handoff).

## Edit shape

Choose one coherent owner-boundary batch. Delete obsolete test-only exports,
globals, wrappers, and dead production paths instead of preserving aliases.
Move retained regressions to their canonical owners. Consolidate repeated
package or dependency assertions into one generic contract.

Prefer net-negative production LOC. Do not add replacement tests that restate
the same implementation, and do not convert uncertain candidates into cleanup
to increase deletion counts.

## Validation

Never edit source or tests while a watch-mode test runner is running in the
checkout.

1. Run the smallest owner and sibling tests with the detected runner, scoped
   to the changed paths (e.g. `npx vitest run <path>`, `pytest <path>`,
   `go test ./<pkg>/...`, `cargo test <filter>`).
2. For removed source greps or plan assertions, run the executable script or
   dry-run that owns the real contract.
3. Run the repo's formatter and linter on the changed files, then
   `git diff --check`.
4. Run the full gate the repo requires before merge (its `AGENTS.md` Merge Gate
   section, or `$TEST_CMD` plus typecheck and lint when none is documented).
5. Inspect `git diff --numstat`; report production/tooling separately from
   tests and test support.

## Landing and continuation

Commit, push, open a PR, or land only when the user authorizes it — **Ask the
user** before any push. Follow the repo's commit conventions, and open the PR
with `$SHARED_DIR/pr-body.md`. After the PR is open, **Invoke skill `review`**
on it. Land one coherent PR at a time; after landing, refresh from the current
default branch and rerun read-only discovery for the next high-confidence batch.

## Handoff

Write the report to the ledger file and show it to the user:

- root cause and removed low-value categories;
- production owner simplifications;
- retained false positives and why they remain valuable;
- focused and full proof actually run;
- production versus test LOC;
- PR and merge state;
- named follow-ups.

## Next steps

End the response with a `## Next steps` block listing 1–3 of:

- `/review <pr>` — independent review of the pruning PR before merge
- `/rootCause` — a retained test failed on the baseline and the owner bug is not yet understood
- `/testAudit --audit <next-scope>` — the next high-confidence batch after this one lands

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/testAudit-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`testAudit`), `artifacts` (absolute paths: `$AW_DIR/qa/test-audit/<YYYY-MM-DD>-<scope-slug>.md` and the tests it changed), `summary` (one line),
plus `scope` (audit scope); `mode` (`gate | audit | sweep`, the mode it ran in).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
