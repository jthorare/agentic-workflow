# Bug Fix Orchestrator — Design

**Date:** 2026-09-29
**Status:** Approved 2026-09-29; implemented on `feat/bug-fix-orchestrator`

## Goal

A `/bugFixOrchestrator` skill that takes a bug ticket and drives it to a **proven** resolution. The
orchestrating agent never edits product code. It investigates with `/rootCause`, dispatches
implementer subagents to fix, and decides "resolved" only when:

1. the same check that failed on the unfixed code (a `ui-evidence` script, or a regression test)
   passes on the fixed code, **and**
2. a new `judge` question, `resolution-check`, agrees that the passing check plus the diff resolve
   the problem **as reported in the ticket's brief**.

"Tests pass" alone is never enough to call a ticket resolved.

## Decisions

| # | Decision | Choice |
|---|----------|--------|
| D1 | Investigator | `/rootCause` with a new `--investigate-only` flag (`bugReport` is a health audit and has no symptom input; `bugHunt` fixes the bug itself) |
| D2 | Who decides "resolved" | Both: the hard check is the gate; `judge resolution-check` is a required second opinion |
| D3 | Ticket source | Linear issue ID/URL (via Linear MCP) or pasted text; the report goes back to Linear, ask-first |
| D4 | Implementer strategy | Serial by default (A), split by area (C), competing implementers as the escalation (B); 3 attempts total |
| D5 | Non-UI bugs | A regression test that fails before and passes after replaces the `ui-evidence` script |
| D6 | Build shape | Prose `SKILL.md` + a `bugfix-state` TypeScript CLI that owns the state file and enforces the phase rules |
| D7 | Check is frozen | The check must be byte-identical between REPRODUCE and EVALUATE (enforced by sha256) |

## Components

```
 ticket (Linear ID/URL or text)
        │
        ▼
 ┌──────────────────────── /bugFixOrchestrator (SKILL.md, prose) ─────────────────────────┐
 │  every phase change goes through ──►  bugfix-state CLI (TS, owns state.json)           │
 │                                                                                         │
 │  1 INTAKE ─► 2 INVESTIGATE ─► 3 REPRODUCE ─► 4 FIX ─► 5 EVALUATE ─► 6 REPORT           │
 │   Linear MCP   /rootCause       ui-evidence     implementer(s)  same check passes      │
 │   or text      --investigate-   script, or      mode A / C / B  + judge                 │
 │                only             failing test                    resolution-check        │
 │                                 MUST FAIL ───────────────────────┘ fail & attempt<3 ─► 4│
 └─────────────────────────────────────────────────────────────────────────────────────────┘
        │
        ▼
 resolution.md (local) ──ask-first──► Linear comment
```

| Unit | Location | Responsibility |
|------|----------|----------------|
| Orchestrator skill | `skills/bugFixOrchestrator/SKILL.md` | Phase instructions; dispatches subagents; reads evidence; never edits product code |
| State CLI | `skills/bugFixOrchestrator/src/` (bin `bugfix-state`) | Sole writer of `state.json`; validates evidence; refuses disallowed phase changes |
| Judge question | `judge/src/questions/resolution-check.ts` | Second opinion: does the passing check plus the diff resolve the reported problem? |
| rootCause flag | `skills/rootCause/SKILL.md` | `--investigate-only`: stop after Phase 3.5 and emit a hypotheses table in the handoff |

## rootCause `--investigate-only`

- Runs Phases 0–3.5. Temporary instrumentation added to confirm a hypothesis (log lines, narrowed
  test runs) is reverted before the skill exits; the working tree is left as it was.
- Skips Phase 4. Final status is `diagnosed` when a hypothesis is confirmed, `unfixed` when every one
  was ruled out. The report's Fix Applied / Verification sections read `n/a — investigate-only`.
  Never dispatches `bugHunt` (Sub-skill Dispatch is off).
- The handoff (`investigations/<slug>/handoff.md`) gains a table:

  ```markdown
  ## Hypotheses
  | # | Hypothesis | Cause-site files | Likelihood | Result |
  |---|-----------|------------------|------------|--------|
  | 1 | ... | `path/a.ts`, `path/b.tsx` | High | confirmed |
  ```

  `Result` is one of `confirmed | ruled-out | untested`.
- The JSON tail's `status` gains the value `diagnosed`, and the tail gains `handoff_path`.
- Without the flag, rootCause behaves exactly as it does today.

## Phases

### 1. INTAKE
- Linear ID/URL → fetch the issue via Linear MCP. Otherwise use the pasted text.
- Store `ticket.title` and `ticket.brief` **verbatim** (the title plus the full description as
  reported, unedited).
- Extract `expected` and `actual` behaviour. If the ticket has no clear expected behaviour, ask the
  user once; that sentence is the acceptance target.
- `bugfix-state init --ticket <json>`.

### 2. INVESTIGATE
- Invoke `/rootCause --investigate-only --depth 1 "<symptom from brief>"`; take `handoff_path` from its JSON tail.
- `bugfix-state advance investigate --evidence <handoff.md>`. The helper requires that the handoff
  parses, has status `diagnosed`, and has a `confirmed` hypothesis. No confirmed hypothesis → ask the
  user; never fix an unconfirmed cause.

### 3. REPRODUCE
- Preflight: the working tree must be clean (every helper step refuses a dirty tree, and the user's
  unrelated work must not land in the baseline); otherwise ask the user to commit or stash first.
- **UI bug** (rootCause produced navigate/click/fill repro steps against the web app): dispatch the
  `qa-runner` agent to write a `ui-evidence` script from the repro steps, with each `expectedState`
  taken from the ticket's **expected** behaviour. Commit it, then run it on the unfixed code with
  `bugfix-state run-ui`, which invokes the ui-evidence CLI itself (`--app-build` = `HEAD`) and
  registers the summary.
- **Otherwise:** dispatch one implementer to write **only** a regression test (no fix) and commit
  it. The orchestrator then runs it through the helper:
  `bugfix-state run-test --check <test-file> -- <command>`. The helper refuses a command that
  doesn't reference the check file, executes it itself, writes `test-result.json`
  `{ command, exitCode, commit, checkSha256, log }` from what it observed, and registers the file's
  hash in `state.runs`, so the result is never agent-reported. The same argv must be used for every
  later run of the check.
- `bugfix-state advance reproduce --evidence <summary.json | test-result.json>`. The helper records
  the check's path, sha256 and (for tests) argv, and **refuses unless the run failed**. The check
  must be a committed, unmodified file inside the repo, and the run must carry its hash
  (ui-evidence `scriptSha256`, run-test `checkSha256`).
- A passing baseline means the bug was not reproduced: the helper refuses and the orchestrator asks
  the user.
- A `ui-evidence` run that exits 2 only because a step is `broken` (a selector problem, not a failed
  expectation) is not a reproduction. `qa-runner` repairs the script once; if it's still broken,
  ask the user.

### 4. FIX
Mode selection, in order:

| Mode | When | What |
|------|------|------|
| B — competing | A previous attempt failed **and** the handoff has ≥ 2 hypotheses not `ruled-out` | 2 implementers in separate worktrees, each on a different hypothesis (`record-candidate --hypothesis`). Code-writing runs in parallel; every heavy step (tests, `ui-evidence`) runs under `with_stack_lock_and_heavy_job_lock`, so only one heavy job runs at a time |
| C — split | The confirmed hypothesis's cause-site files span > 1 area (distinct top-level packages or app layers, e.g. API route and UI component) | One implementer per area, in sequence; each gets the previous one's diff. One evaluation at the end |
| A — single | Otherwise | One implementer |

- Each implementer brief contains: the ticket brief, the handoff, the failing check, its assigned
  area or hypothesis, and on a retry the previous diff plus the exact failure reasons (failed
  steps/assertions and the judge's reasoning). Implementers may not modify the check file.
- Every dispatch prompt carries explicit `Goal:`, `Acceptance criteria:` and `Proof command:` lines,
  which the `brief-scope` gate reads. (No dispatch tool can set the hook's `skill_internal` flag.)
- Implementers run heavy checks once per commit, not per edit.
- `bugfix-state start-attempt --mode <A|B|C>` opens the attempt (the helper increments `attempt` and
  refuses a 4th); `bugfix-state record-candidate --branch <b> --cwd <worktree>` records each candidate.
- Every candidate worktree starts from the baseline commit (`bugfix/<ticket-slug>-a<attempt>`, and
  `-a<attempt>-h<hypothesis#>` for B),
  never from a failed attempt. The check itself is committed on `bugfix/<ticket-slug>` — never on the
  base branch.

### 5. EVALUATE
For each candidate:
1. Detach the main checkout at the candidate's commit (dependencies and the running app live there;
   for UI, restart the app from it so `--app-build` is true), and re-run the **same** check
   (`bugfix-state run-ui`, or the baseline's exact `bugfix-state run-test` command).
2. `bugfix-state record-run <candidate> --evidence <path>`. The helper requires the check's sha256
   (on disk and as executed) to match the baseline, the test argv to match, the run's commit to be
   the candidate's head, and the candidate tree to be clean. A broken ui-evidence run is refused
   rather than counted as a failed attempt.
3. If the run passed: `bugfix-state judge <candidate>` builds the judge input from state, runs
   `judge resolution-check` itself, and records that decision (verified via `judge why` and its
   `input_digest`); an escalation (exit 2) sets `status: needs-human`.
4. Outcome:
   - check passed **and** judge `resolved` → candidate eligible.
   - judge `partial | unresolved`, or check failed → back to Phase 4 with the reasons.
   - judge escalated (below threshold or timeout) → `status: needs-human`; the user decides.

With B, the first eligible candidate wins; if both are eligible, the orchestrator picks the smaller
diff and records why. The losing worktree is removed and its branch kept until the ticket closes.

### 6. REPORT
- `bugfix-state advance report --candidate <id>` (or `--unresolved`). For `resolved`, the helper requires the
  candidate to have a passed run and a judge decision of `resolved`.
- Write `resolution.md`: ticket, root cause, fix commits, before/after evidence paths, the judge
  decision ID, attempts, and the modes used.
- Offer to post it to Linear as a comment (ask-first). Artifact upload follows `ui-evidence`'s
  existing rules (approved uploader, `seeded` DB provenance only).
- The winning worktree is removed (its branch stays) so `/shipRelease` can check the branch out, and
  the branch is left ready for it. No PR is opened automatically.
- On 3 failed attempts: `status: unresolved`; every candidate and all evidence are kept, and the
  report is still offered.

## State file

`~/.agentic-workflow/<repo-slug>/bugfix/<ticket-slug>/state.json`, Zod-validated, written only by
`bugfix-state` (temp file + rename).

```jsonc
{
  "version": 1,
  "ticket": { "source": "linear|text", "id": "ENG-123", "title": "...", "brief": "<verbatim>",
              "expected": "...", "actual": "..." },
  "phase": "intake|investigate|reproduce|fix|evaluate|report",
  "status": "active|resolved|unresolved|needs-human",
  "attempt": 0,
  "attemptMode": "A|B|C|null",
  "handoff": "<path>",
  "investigation": { "rootCause": "...", "hypotheses": [ { "n": 1, "text": "...", "files": ["..."], "result": "confirmed|ruled-out|untested" } ] },
  "check": { "kind": "ui-evidence|test", "path": "<repo-relative>", "sha256": "...", "command": ["<argv>"] },
  "baseline": { "evidence": "<path>", "commit": "<sha>" },
  "candidates": [
    { "id": "c1", "attempt": 1, "mode": "A|B|C", "branch": "...", "cwd": "<worktree or repo>",
      "commit": "<sha>", "hypothesis": null, "changedFiles": ["..."], "judgeInputDigest": "<16 hex>",
      "run": { "evidence": "...", "passed": true, "commit": "<sha>", "recordedAt": "<iso>" },
      "judge": { "decisionId": "...", "decision": "resolved|partial|unresolved|escalated", "reasonCode": "..." } }
  ],
  "runs": [ { "evidence": "<path>", "sha256": "..." } ],
  "resolvedBy": null,
  "history": [ { "at": "<iso>", "command": "record-run c1", "from": "fix", "to": "evaluate", "evidence": "..." } ]
}
```

## `bugfix-state` CLI

Exit codes: `0` allowed, `3` refused (reason on stderr), `1` bad usage or invalid state.

| Command | Enforces |
|---------|----------|
| `init --ticket <json>` | Ticket has a non-empty `brief` and `expected`; no unfinished (`active` / `needs-human`) state for the slug |
| `advance investigate --evidence <handoff>` | Phase `intake`; handoff parses, has status `diagnosed`, a `confirmed` hypothesis, and Root Cause text. Snapshots the hypotheses and root cause into `investigation`; later steps never re-read the file |
| `run-test --check <file> [--cwd <dir>] -- <cmd…>` | The command passes the check file as its own argument and is not an inline-code wrapper (`sh -c`, `node -e`, …) or a non-runner (`grep`, `cat`, `test`, …); `--cwd` has no uncommitted or untracked changes; the check is committed and unmodified inside the repo. Runs the command itself (30-min timeout), writes `runs/<ts>-test-result.json` (argv, observed exit code, `HEAD`, check sha256, log) and registers its hash in `state.runs` |
| `run-ui --check <script.json> [--cwd <dir>]` | Same tree and check rules as `run-test`; runs the ui-evidence CLI itself (`--app-build` = `HEAD`) and registers the resulting `summary.json` |
| `advance reproduce --evidence <run> --check <file> [--cwd <dir>]` | Phase `investigate`; evidence registered and unmodified; run **failed** (not passed, not broken-only); run commit equals `HEAD` of `--cwd`; check committed, unmodified, inside the repo; the run's recorded check hash equals the file's. Records check path, sha256, test argv, baseline commit |
| `start-attempt --mode <A\|B\|C>` | Phase `reproduce` or `evaluate`; no eligible candidate; every candidate of the current attempt evaluated; `attempt` < 3; B only after a failed attempt and with ≥ 2 hypotheses not ruled out. Increments `attempt`, clears `needs-human` |
| `record-candidate --branch <b> [--cwd <dir>]` | Phase `fix` or `evaluate` (mode B may evaluate one candidate before recording the second); one candidate per A/C attempt, two per B attempt; `--cwd` is on `<b>`, has no uncommitted or untracked changes, `HEAD` builds on the baseline, changes its tree, and differs from every other candidate; the diff (`git diff --no-renames -z`, so renames show both paths) doesn't touch the check, test/fixture/mock/e2e directories, test-named files, snapshots, runner config or setup files, `package.json`, or the check's directory (case-insensitive) unless `--allow-test-changes` (only after the user approves); mode B candidates name a distinct, not-ruled-out `--hypothesis`. The commit and changed files are read from `--cwd` |
| `record-run <candidate> --evidence <run>` | Phase `fix` or `evaluate`; no run recorded yet; evidence registered; same check kind; not broken; same test argv as the baseline; run commit equals the candidate's commit, which is still its `HEAD`; candidate tree clean; check file and executed check hash unchanged |
| `judge <candidate>` | Phase `evaluate`; the candidate has a passing run and has not been judged. Builds `judge-<candidate>.json` from state (ticket text verbatim, the snapshotted root cause, a description of the frozen check, diff stat) in `ResolutionCheckInputSchema` key order, runs `judge resolution-check` itself, and records that decision after checking it via `judge why` (question and `input_digest`). Exit 2 records an escalation and sets `status: needs-human`; a judge failure records nothing. The agent never touches decision ids, so a verdict can't be re-rolled |
| `advance report (--candidate <id> \| --unresolved)` | Not already reported; `--candidate` needs a passing run and a `resolved` judge decision |
| `status` / `resume` | — (print the state / the current phase and the next action) |

`judge` is looked up on `PATH` only — there is no override an agent could point at a forging script.
Accepted limits (no privilege separation): an agent that edits `state.json` or the helper directly
is out of scope; gitignored files are not checked for changes; that the served app was built from the
recorded `appBuild` is the orchestrator's job (it restarts the app from the detached checkout).
Any unexpected error (not a git repo, unreadable file) exits 1 with a one-line message.

The orchestrator must report any refusal and may not work around it. After context loss it runs
`bugfix-state resume`. In `fix` or `evaluate` the main checkout may still be detached from an interrupted
evaluation; `resume`'s next action names the exact commit to detach at.

## `judge` question: `resolution-check`

- **Input:** `{ brief, expected, actual, rootCause, checkKind, checkSummary, beforePassed, afterPassed, diffStat }`
  - `brief`: the ticket title plus description, verbatim, capped at ~8k characters; if cut, a
    `[truncated N chars]` marker is appended.
  - `rootCause`: the confirmed root-cause paragraph from the handoff.
- **Outputs:** `resolved | partial | unresolved`
- **Content class:** `brief` · **threshold:** 0.8 · **time budget:** 15 000 ms
- **preRules:**
  - `afterPassed === false` → `unresolved`
  - `beforePassed === true` → `unresolved` (the check never reproduced the bug)
  - empty `brief` → escalate (nothing to judge against)
- **Prompt:** leads with the brief, then the expected behaviour, the root cause, the check summary
  and the diff stat, and asks: does the passing check, together with this diff, resolve *the problem
  as reported in the brief*? Reply `partial` if any part of the brief is not covered by the check,
  or if the diff hides the symptom without addressing the root cause.
- It never defaults to `resolved`. A timeout or low confidence escalates.

## Errors

| Situation | Behaviour |
|-----------|-----------|
| Baseline check passes | Not reproduced → helper refuses, ask the user |
| `ui-evidence` step `broken` | One `qa-runner` repair, then ask the user |
| Helper refuses (exit 3) | Report the reason; never work around it |
| Judge escalates | `needs-human`; the user decides |
| 3 attempts exhausted | `unresolved`; keep all evidence; still offer the report |
| Context loss | `bugfix-state resume` |
| Local stack unhealthy (`doctor.sh`) | Stop before REPRODUCE; UI bugs need a healthy local stack |

## Testing

Vitest, no `/* v8 ignore */`, following `.agents/rules/testing.md`.

- **`bugfix-state`:** a table test covering every allowed and refused transition: baseline passed →
  refused; broken-only `ui-evidence` summary → not a reproduction; check sha256 changed → refused;
  run commit ≠ candidate commit → refused; 4th attempt → refused; `report(resolved)` without a
  `resolved` judge decision → refused. Plus atomic writes, and `resume` output after each phase.
  Uses real temp directories; the state logic is not mocked. The `judge why` lookup goes through
  a thin injected executor, stubbed in tests.
- **`resolution-check`:** each `preRules` branch; the prompt contains the brief verbatim and the
  truncation marker when capped; output enum; threshold and escalation through the existing judge
  test helpers.
- **`rootCause --investigate-only`:** a text-level check that the flag, the `diagnosed` status, and
  the `## Hypotheses` handoff table are documented.
- **`providers/tests/install.test.sh`:** `aw_build_skill_packages` builds each package, warns and
  continues on a failed build, skips a package-less skill, only prints in dry-run; and every
  `skills/*/package.json` is in `AW_SKILL_PACKAGES`.

## Change to `ui-evidence`

`summary.json` gains `appBuild` (the `--app-build` value, or `null`). Today the value is used only
for the verdict-cache key and is not written out, so a run cannot be tied to a commit. The helper
requires `appBuild` to equal the candidate's head commit (or the baseline commit for REPRODUCE).
It also gains `scriptSha256` (hash of the script file the run executed), so the helper can prove the
run executed the frozen check. Both are recorded by the ui-evidence CLI; that the served app was
built from `appBuild` is the orchestrator's responsibility (it restarts the app from the
detached candidate checkout).

## Install, merge gate, docs

- `setup.sh`: add `bugFixOrchestrator` to `MANAGED_SKILLS`; add a step that runs
  `npm ci && npm run build` in `skills/ui-evidence` and `skills/bugFixOrchestrator` (dry-run prints
  it). This also fixes an existing gap: `ui-evidence` needs `dist/bin.js`, but `setup.sh` never
  builds it.
- AGENTS.md merge gate: add typecheck and test for both skill packages.
- Skill counts in AGENTS.md, README.md, `planning/ARCHITECTURE.md` and `.agents/rules/skills.md`;
  a "Bug fixing" line in the `setup.sh` summary.
- `planning/ARCHITECTURE.md`: a short section on the orchestrator → state CLI → judge flow.
- `skills/judge/SKILL.md`: list `resolution-check`.

## Out of scope

- iOS evidence packs (non-web bugs use the regression-test path).
- Opening PRs automatically.
- More than 2 competing implementers.
- Changes to `bugHunt` or `bugReport`.
