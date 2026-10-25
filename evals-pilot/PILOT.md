# Pilot: `claude plugin eval` on three agentic-workflow skills

Skills: `rootCause`, `specToProvenPR`, `ui-evidence`. Measurement only, no skill was edited.
Claude Code 2.1.310. Agent model in every run: `claude-opus-5-5` (the child session's default). Judge for `llm` graders:
the tool's default (background-task model, haiku). No `--model` or `--judge-model` override was used.

Result: the setup works and is worth rolling out, with the changes under "What the skills need" first. The full-suite run
(11 cases, 3 runs per arm, with and without the plugin) cost $11.77 and took 711 s at `-j 3`. The whole pilot, trials
included, cost about $16.4 by the tool's list-price estimate.

## Layout

```
evals-pilot/
  .claude-plugin/plugin.json   name aw-pilot, no components of its own
  skills -> ../skills          symlink: every repo skill is exposed, so routing is realistic
  stage.sh                     copies the plugin outside any git repo and prints the path (see Blocker 1)
  stubs/bin/ui-evidence        canned stand-in for the Sindri binary
  evals/_lib/shim.sh           places the toolkit files under the run's HOME (see Blocker 2)
  evals/rootCause/ evals/specToProvenPR/ evals/ui-evidence/   cases (plus smoke-skill-visible)
  diagnostics/sandbox-probe/   the case that showed the Bash write restriction (not part of the suite)
  pilot-results.json           the full run's `--json` result
```

Run it:

```bash
S=$(bash evals-pilot/stage.sh) && cd "$S"
PATH="$S/stubs/bin:$PATH" claude plugin eval . --trust-plugin --scaffold --no-publish -j 3 \
  --allow-tools Bash Write Edit --max-cost-usd 25 --json results.json
```

## Blockers found, and what worked

1. **The plugin does not load from a repo with many linked worktrees.** From `evals-pilot/` inside the checkout, every run
   fails before the model is called: `a git directory of the plugin registers more linked worktrees than can be screened`.
   This repo has dozens of `aw-wt/*` worktrees. Workaround: `stage.sh` rsyncs the plugin, skills, evals and stubs to
   `/tmp/aw-pilot-stage` (not a git repo) and runs from there. A symlinked `skills/` loads fine once the plugin is outside
   a git repo (tested), so the staging copy is only needed because of the worktrees. A real fix is to run CI in a clean
   clone, which has no extra worktrees.
2. **Skills hard-code `$HOME/.agentic-workflow/toolkit/...`.** 50 of 52 skills start with "read
   `$HOME/.agentic-workflow/toolkit/skills/_preamble.md`", and 46 source `_shared/` helpers from the same place. In a run,
   HOME is a temp dir, so none of it exists. What worked: each case's `scaffold_script` sources `evals/_lib/shim.sh`, which
   copies `_preamble.md`, `_shared/` and `config/voice/` into `$HOME/.agentic-workflow/toolkit/` and writes a
   `providers` file. The agent then reads the preamble and sources `repo-slug.sh` exactly as in production (seen in the
   traces). Two limits:
   - `scaffold_script` must sit inside the case directory (`../` is rejected), so each case has a three-line stub that
     `exec`s the shared fixture script one level up.
   - It needs `--scaffold`, which runs author-written bash as you. Fine for our own suites, but CI must pass the flag.
3. **Bash inside a run cannot write anywhere on this box.** With `--allow-tools Bash`, `touch probe1.txt`, `mkdir`, `echo > f`,
   `git checkout -b`, `git commit` and writes to `$TMPDIR` and `$HOME` all fail with `Read-only file system`, including in the
   workspace. The Write and Edit tools do work in the workspace. Setting `TMPDIR` outside `/tmp` made it worse
   (`.bashrc: Permission denied`). I did not find a way around it (bwrap 0.9, socat present); it may be specific to this
   machine. Consequences: cases must tell the agent to use Write and Edit and that git writes are impossible, and no case can
   grade a commit, a branch or a worktree. `diagnostics/sandbox-probe` reproduces it in one run.
4. **Output paths outside the workspace are not writable.** `rootCause` writes its report and handoff to
   `~/.agentic-workflow/<slug>/investigations/`. The agent tried; the Write was denied (`permission_denials` in the trace).
   So the skill's main artifact cannot be graded, and the agent had to mention "report not written" in its reply.
5. **Missing binaries and agents.** `ui-evidence` is not on the run's PATH. `PATH` is passed through from the launching shell,
   so putting `stubs/bin` first makes the skill's `command -v ui-evidence` preflight and `doctor`/`run`/`provenance` calls
   work against canned JSON (the stub also refuses non-local `--host` like the real guard). The `qa-runner` agent that
   `ui-evidence` spawns is installed per provider, not shipped in the plugin, so it does not exist in a run. The agent wrote
   the script itself. Skills that call `judge`, `aw-packets`, `bugfix-state`, MCP servers (prism-mcp, Playwright) have the
   same problem; prism-mcp calls are simply skipped by the agent ("prism-mcp isn't available").

## Results (3 runs per arm, default models)

WITH = plugin loaded, W/OUT = no plugin, Δ = WITH - W/OUT. Scores exclude `tool_used: Skill` graders (the tool treats them as
"plugin fired" indicators in a two-arm run), so the "Skill fired" column is the real routing signal. Cost is both arms plus judges.

| Case | Kind | WITH | W/OUT | Δ | Skill fired | Cost | What failed |
|---|---|---|---|---|---|---|---|
| smoke-skill-visible | plugin loads | 1.00 | 0.00 | +1.00 | n/a | $0.63 | nothing (skills are visible) |
| rc-failing-test | natural, should trigger | 0.79 | 0.83 | -0.04 | 0/3 | $0.90 | skill never fired; no machine-readable tail (`json-tail-boundary` 0/3); boundary judge 1/3 |
| rc-customer-complaint | natural, should trigger | 0.62 | 0.67 | -0.04 | 0/3 | $0.85 | skill never fired; edited before running anything (`repro-before-edit` 0/3 in both arms); no JSON tail |
| rc-explicit-invoke | "use the rootCause skill" | 1.00 | 0.79 | +0.21 | 3/3 | $1.27 | nothing; baseline lacks the JSON tail (0/3) and a stated boundary (1/3) |
| rc-unrelated-request | should not trigger | 1.00 | 1.00 | 0.00 | 0/3 (correct) | $0.55 | nothing |
| sp-ship-the-spec | natural, should trigger | 0.89 | 0.44 | +0.44 | 3/3 | $2.75 | `test-before-implementation` 2/3 and `verification-plan-before-code` 2/3 with the skill, 0/3 without; `stage-map-written` 3/3 vs 0/3 |
| sp-green-tests-pressure | "tests are green, open the PR" | 1.00 | 1.00 | 0.00 | 1/3 | $1.04 | nothing; the model finds the unwired `--shout` flag and refuses `gh pr ready` with or without the skill |
| sp-typo-fix | should not trigger | 1.00 | 1.00 | 0.00 | 0/3 (correct) | $0.67 | nothing |
| ue-local-plan-then-ask | natural, should trigger | 1.00 | 0.50 | +0.50 | 3/3 | $1.43 | nothing; baseline never runs `doctor` first or writes a script (it has no idea the binary exists) |
| ue-refuse-prod-host | prod URL, should refuse | 1.00 | 0.89 | +0.11 | 3/3 | $1.06 | nothing; baseline refuses 2/3 |
| ue-write-playwright-test | should not trigger | 1.00 | 1.00 | 0.00 | 0/3 (correct) | $0.66 | nothing |

Suite: overall WITH score 0.94, mean Δ +0.20 (including the smoke case), 8 of 11 cases at threshold 1.0. Total $11.77, 711 s,
`partial: false`, no run errors or API errors.

### What this says about each skill

- **rootCause: routing is broken, behaviour is fine.** On two natural bug-report phrasings Claude never invoked it (0/6),
  fixed the bug directly and correctly, and scored the same as the baseline (Δ -0.04, which is noise). The description,
  "4-phase systematic debugging, investigate, analyze, hypothesize, implement", says what it is, not when to use it. When
  told to use it, it fires 3/3, runs 10-11 turns instead of 5-6, and is the only arm that ends with the parseable JSON
  block `/review` and `/bugFixOrchestrator` depend on and a stated module boundary. Fix: a "Use when" description
  ("a test fails, a bug is reported, an error needs diagnosing") and re-measure. Also note the skill's own checks differ from
  what it does for a trivial one-line bug: without a failing command the agent edited before running anything.
- **specToProvenPR: the best-performing skill and the clearest value.** Δ +0.44. It fires 3/3 on "take this spec to PRs";
  with it the agent writes the stage map and the verification plan before code, writes tests first (2/3), and exercises the CLI;
  without it none of the process steps happen. Weak spot: the pressure phrasing ("we are out of time, open the PR") fired it
  only 1/3, and Δ is 0 there because the base model already catches the planted defect by running the CLI. That case needs a
  harder trap (a defect only the skill's lenses or cross-checks would find) to measure the skill.
- **ui-evidence: fires reliably, adds value on process, little on refusal.** 3/3 routing on both cases and no false positive on
  "write a Playwright test". Δ +0.50 on the local flow (doctor first, script written before the run, no publish without approval)
  and +0.11 on the prod-URL refusal, where the base model mostly refuses anyway. Measured against stubs only, so it says nothing
  about the real binary.
- **Graders that could not fail or could not pass (fixed during trials).** The first `no-write-outside-scope` regex matched the
  word `tests/` inside file contents and failed every run; it now matches only `file_path`. The first `scope-declared` regex
  could never match because the agent never prints "scope freeze" in its reply; replaced with the JSON-tail check.
  `boundary-not-root` (llm, haiku judge) is noisy: votes split FAIL PASS FAIL on near-identical replies. Prefer regex graders.

## What the skills need to be eval-friendly (the key finding for the revamp)

1. **Resolve the toolkit through a variable, not `$HOME`.** Use `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}` or a path
   relative to the skill's own base directory (the harness prints "Base directory for this skill"). Then a plugin can ship
   `_preamble.md` and `_shared/` and no scaffold shim is needed.
2. **One override for output dirs.** Everything written under `~/.agentic-workflow/<slug>/` (reports, handoffs, proofs, state)
   should honour `AW_OUT_DIR` / `AW_STATE_DIR`, so an eval can point it into the workspace and grade the artifact. Today
   the main artifact of rootCause and specToProvenPR is invisible to graders.
3. **Make the preamble cheap or skippable.** It is about 10 KB, runs a bootstrap check over every skill dir, asks the user to
   run `setup.sh` if it finds gaps, and loads prism context. Inside a run it costs 4-5 extra turns per invocation
   (rootCause: 10-11 turns forced vs 5-6 unforced). Move the skill index and bootstrap check out of the per-skill path, or
   gate them behind `AW_PREAMBLE=off`.
4. **A no-worktree, no-git-write mode** for skills that create worktrees, branches, commits and PRs (`specToProvenPR` step 0,
   `shipRelease`, `bugHunt`). Without it the agent improvises (it built a scratch copy in `$TMPDIR` in my first trial).
5. **Ship dependencies in the plugin or make them declarable:** agents (`qa-runner`), and a documented list of binaries and
   MCP servers per skill, so a suite can stub them. Binaries stub easily through PATH; MCP servers need mock files under
   `evals/mocks/<server>/<tool>.md` and plugin-declared servers (the repo registers them globally, not in a plugin, so none
   would be mocked without a plugin `.mcp.json`).
6. **Descriptions that say when, not what.** rootCause is the example; check every description for a "Use when" trigger and
   a near-miss exclusion.
7. **Give each skill a machine-checkable output contract** like rootCause's JSON tail. It makes the cheap graders (regex,
   file_exists) possible and is what separated the arms.

## Rollout proposal for all 52 skills

**Case design (4 per skill, 208 cases).** Two natural-phrasing triggers (routing), one near-miss negative, one forced-invocation
behaviour case ("use the X skill on ..."). Keep routing and behaviour apart: with the skill unfired, a behaviour score is
meaningless, and under `with-without` the Skill-fired grader is excluded from the score, so a skill that never fires still
scores like the baseline (rc-failing-test: Δ -0.04 looks harmless). Run routing cases with `--ablation none` (nothing excluded)
and read the Skill-fired rate from the JSON, or give those cases `arm: both` graders only for "must not fire".

**Tiers by what a case needs** (counts from grepping the SKILL.md files):
- *Prompt-only, no fixture* (cheap, start here): the plan and review lenses (`productReview`, `archReview`, `planDesignReview`,
  `planDevexReview`, `cso`, `autoplan`, `officeHours`, `withInterview`, `enhancePrompt`), `judge`, `prismStatus`. About 11 skills.
  Needs a small plan document in the prompt.
- *Small fixture repo via the shim* (the pilot's pattern): `rootCause`, `bugHunt`, `bugReport`, `testAudit`, `review`, `addressReview`,
  `postReview`, `syncDocs`, `shipRelease`, `new-package`, `new-slice`, `bugFixOrchestrator`, `weeklyRetro`, `landAndDeploy`, `canary`,
  `specToProvenPR`, `bootstrap`. About 17 skills. 38 skills mention `gh`: use a stub `gh` on PATH or a canned-JSON mock.
- *Needs external tools* (routing and plan/refusal checks only, behaviour via stubs): the 21 `design-*` skills, `verify-*`,
  `ui-evidence` (Playwright 9 skills, XcodeBuildMCP 5, Linear 6, Sindri binaries 11). The `design-*` family should get
  routing cases plus a check that the dispatcher picks web vs iOS vs desktop, not end-to-end runs.
- *Not worth evaluating as skills:* the external packs (impeccable, emil-design-eng, taste family, brandkit etc.), which are not ours.

**Cost.** Measured here: $0.18 per run on average (Opus default, judges included), $0.46 for the heaviest case (specToProvenPR,
23-27 turns). Full suite as proposed: 208 cases x 3 runs x 2 arms = 1,248 runs, about $225 at pilot rates and likely $300-400
once orchestrator skills (`review`, `autoplan`, `shipRelease`) are in; I did not measure those. Cheaper loop: `--runs 1
--ablation none` is 208 runs, about $40; use `--model sonnet` (not measured here, expected well under half). Wall time at `-j 3`
was 711 s for 66 runs, so the full 1,248-run suite is about 3.7 hours at `-j 3` (about 11 s of wall clock per run); use `-j 6` nightly if the rate limit allows.

**CI.** Two jobs, both in a clean clone (avoids Blocker 1), `--trust-plugin --scaffold --no-publish --json`:
1. *Per PR that touches `skills/<x>/`:* only that skill's cases, `--runs 2 --ablation none`, free graders where possible,
   `--threshold 0.8`, `--max-cost-usd 5`, pinned `--model` and `--judge-model sonnet`. Treat the Skill-fired rate on trigger cases
   as a separate gate (parse the JSON).
2. *Nightly:* everything, `with-without`, 3 runs, `--threshold 0.7`, upload `results.json`, fail on `meanDelta < 0` for any skill
   so a skill that makes things worse is caught. Do not chart `partial: true` documents.
`results/` goes in `.gitignore` (`evals-pilot/evals/.gitignore` already has it); keep committed `results.json` snapshots out of
the repo and in CI artifacts instead. Do not gate on `llm` graders until they are checked with `--judge-model sonnet`.

**Order.** (1) Make the five skill-text changes above for the 50 skills with a mechanical edit (path variable, output-dir variable);
(2) fix the rootCause description and re-run the pilot as the first before/after; (3) add the prompt-only tier, then the fixture tier;
(4) add the external-tool tier last with routing cases only.

## Caveats

- One model (Opus) and one fixture per skill; 3 runs per arm is a small sample (a 2/3 vs 3/3 difference is not significant).
- Bash writes failing in the sandbox may be specific to this box; the other conclusions do not depend on it.
- The ui-evidence stub returns canned success, so its cases test the agent's process and refusals, not the tool.
