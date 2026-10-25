# Skill revamp: self-contained skills measured with `claude plugin eval`

**Status:** draft for the owner's read · **Date:** 2026-10-25 · **Asked by:** the owner, 2026-10-25 (issue #1107) · **Builds on:** meta's pilot (`evals-pilot/PILOT.md`, `pilot-results.json`, branch `feat/skill-evals-pilot`), `.agents/rules/skills.md`, `skills/_preamble.md`, `skills/_shared/capabilities.md`, the plugin eval docs (https://code.claude.com/docs/en/plugin-evals.md) · **Lanes:** ideas (this spec); a plumbing lane and skill-group lanes (§8) · **Next:** `/specToProvenPR` takes §8's phases to PRs.

**Keeping it app-agnostic.** Nothing here names a work app, company or customer. Fixtures are small made-up repos (a cart with a bulk-discount bug), and any outside tool is stubbed on `PATH`.

## 0. The short version

### For you

Today we cannot tell whether a skill helps. The pilot ran `claude plugin eval` on three skills and found three things: some skills never fire on natural phrasing, a skill's main output cannot be read by a check, and 50 of 52 skills only work when the toolkit sits at one fixed path in the home directory. This spec fixes those in order:

1. Make every skill self-contained: two path variables, a cheap preamble, a mode that touches no git state, a machine-readable last block.
2. Give every skill 4 eval cases, grouped into tiers by how much setup they need.
3. Run the cases in a clean clone through one script, and gate changes on them.
4. Fix descriptions routing-first, with the `rootCause` description as the worked example, and never tune a description to the eval prompts.
5. Roll it out in phases: plumbing first, then skill groups in parallel. Every group PR carries its cases and a WITH / W-OUT / Δ table and a spend tally.

Section 9 lists the decisions we need from you.

### For builders

Build order is §8. Builders write cases and skill text and never run `claude plugin eval` (it spends shared usage). The lane lead runs the evals and pastes the table. Eval spend pauses at $60 per day (§6.4).

## 1. What the pilot found

Numbers are from `PILOT.md` (3 runs per arm, Opus agent, Haiku judge, $11.77 for 11 cases):

| Skill | Result | Reading |
|---|---|---|
| `rootCause` | natural prompts fire it 0/6; forced +0.21 | routing is broken, behaviour is fine |
| `specToProvenPR` | Δ +0.44 | clearest value; weak on pressure phrasing (fires 1/3) |
| `ui-evidence` | Δ +0.50 local flow, +0.11 prod refusal | fires reliably; measured on stubs only |

Blockers that shape this spec:

1. The plugin does not load from a repo with many linked worktrees ("registers more linked worktrees than can be screened").
2. 50 of 52 skills read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md`; 46 source `_shared/` helpers from there. In an eval run `HOME` is a temp dir, so none exists.
3. Bash writes failed in the pilot's sandbox on that box; Write and Edit worked. Reports under `~/.agentic-workflow/<slug>/` cannot be written, so the main artifact of `rootCause` and `specToProvenPR` is invisible to graders.
4. The preamble costs 4-5 turns per call (`rootCause`: 10-11 turns forced, 5-6 unforced).
5. Binaries, agents and MCP servers the skills call are absent in a run.
6. The Haiku judge splits on near-identical replies (PASS FAIL PASS). Regex and tool graders are stable.

## 2. Self-contained skills

A skill is self-contained when a plugin that ships `skills/` can run it with nothing else installed except the tools it declares. Four changes, applied to all skills by one mechanical edit (phase 0).

### 2.1 `AW_TOOLKIT`

Replace every `$HOME/.agentic-workflow/toolkit` in `skills/` with `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}`. The default is today's symlink, so installed behaviour does not change. In a plugin, the eval sets `AW_TOOLKIT` to the plugin root, which already holds `skills/_preamble.md` and `skills/_shared/`. This removes the `evals/_lib/shim.sh` copy trick and the per-case `scaffold_script` stubs.

`.agents/rules/skills.md` ("Shared Fragments", "Preamble") is updated with the new text, and `scripts/` gets a check (like `check-env-example.sh`) that fails when a bare `$HOME/.agentic-workflow/toolkit` appears in `skills/**`. Files the skills need beyond `skills/` (the voice file under `config/voice/`) move under `skills/_shared/` so one root is enough. **Decision:** the toolkit root is one variable, not a path relative to the skill's base directory, because bash snippets inside a SKILL.md cannot know their own base directory on every provider.

### 2.2 `AW_OUT_DIR`

Every skill that writes a file today writes under `~/.agentic-workflow/<slug>/<kind>/`. Add `AW_OUT_DIR`, default `$HOME/.agentic-workflow`, and write to `${AW_OUT_DIR}/<slug>/<kind>/`. `skills/_shared/repo-slug.sh` gains a `aw_out_dir` helper so skills call one function, not a string. An eval sets `AW_OUT_DIR=$PWD/.aw-out` (inside the run's workspace, which is writable) and a `file_exists` or `regex` grader reads the report. State kept by tools (judge sessions, packets) stays under `AW_STATE_DIR`; this spec does not touch it.

### 2.3 A cheap or skippable preamble

The preamble (about 10 KB) lists skills, runs a bootstrap check over every skill dir, may ask the user to run `setup.sh`, and loads prism context. Changes:

1. Split `_preamble.md` into a **core** (provider capability map, the one-line voice rule; under 1 KB) and an **extended** part (skill index, bootstrap check, prism context, Session Close). A skill's SKILL.md points at the core by default and at the extended part only where it needs it.
2. `AW_PREAMBLE=off` skips the extended part entirely. An eval sets it. Interactive sessions leave it unset, so nothing changes for a person.
3. The bootstrap check moves to `setup.sh` and `sindri doctor`, where it belongs; a skill never asks the user to run setup from inside a run.

Target: a forced `rootCause` run drops from 10-11 turns to 6-7. Phase 0 measures it.

### 2.4 A no-worktree mode

Skills that make worktrees, branches, commits or PRs (`specToProvenPR` step 0, `shipRelease`, `bugHunt`, `bugFixOrchestrator`, `addressReview`) read `AW_GIT_MODE`: `full` (default) or `readonly`. In `readonly` they work in the current directory, make no branch, commit, push or PR, and print the commands they would have run under a heading `Would run:`. Cases then grade the plan and the edits, not the git state. Cases that need a real commit are not run under `claude plugin eval` (they stay with the existing bash and C# tests).

### 2.5 An output contract per skill

Each skill ends its reply with one fenced JSON block, `aw-result`, in a fixed shape:

```json
{"skill":"rootCause","status":"done","artifacts":["investigations/2026-10-25-cart.md"],"summary":"..."}
```

Required keys: `skill`, `status` (`done`, `blocked`, `refused`), `artifacts` (paths relative to `AW_OUT_DIR`), `summary` (one line). A skill may add its own keys (`rootCause` keeps `boundary` and `cause_site`). The contract lives in `skills/_shared/output-contract.md`; a skill's SKILL.md ends with a "Result block" section naming its extra keys. This is what lets a `regex` grader check the result in one line and what `/review` and `/bugFixOrchestrator` already read from `rootCause`. Skills that only talk (`enhancePrompt`, `withInterview`) use `artifacts: []`.

### 2.6 Declared dependencies

Every SKILL.md frontmatter gains `requires:` listing binaries, agents and MCP servers it calls (`requires: {bins: [gh], agents: [qa-runner], mcp: [playwright]}`). The runner uses it to decide which stubs a case needs (§5.3), and `scripts/` checks that each name exists in the stub set or the real install. Agents a skill spawns (`qa-runner`) are copied under the plugin's `agents/` in the staged copy so they exist in a run.

## 3. The eval suite

### 3.1 Four cases per skill

| Case | Kind | Purpose |
|---|---|---|
| `<s>-natural-1`, `<s>-natural-2` | routing | a person's own words that should fire the skill (two different phrasings) |
| `<s>-near-miss` | routing | a request that looks close but must not fire it |
| `<s>-forced` | behaviour | "use the `<s>` skill on ..." and grade what it does |

With 52 skills that is 208 cases. **Decision:** routing and behaviour cases are kept apart, in two directories per skill (`routing/` and `behaviour/`) and tagged `routing` or `behaviour`. Reason: with the skill unfired a behaviour score means nothing, and under `with-without` the `tool_used: Skill` grader is excluded from the score, so a skill that never fires can still show Δ near 0 and look harmless (`rc-failing-test` showed Δ -0.04). Routing cases run with `--ablation none` and the fire rate is read from `--json`; behaviour cases run with the default `with-without` so Δ is real.

### 3.2 Graders

Order of preference, and a case needs at least one grader from the first two:

1. `tool_used` (the Skill tool fired, or a Write went to the right path) and `file_exists`.
2. `regex` on the reply, the `aw-result` block, or an output file.
3. `llm` judge only where no pattern can express the check, always in a case that has at least two free graders, never as the only gate, and only after it gives the same vote on 3 of 3 re-judgements of one saved reply.

Graders that cannot fail or cannot pass are a known trap (the pilot's `no-write-outside-scope` matched `tests/` inside file content). Every new grader is checked once against a reply that should pass and one that should fail, written into the case directory as `graders/_selftest/`, before the case counts.

### 3.3 Tiers

Tiers group skills by what a case needs. Every directory under `skills/` with a SKILL.md is listed once. (`bootstrap/` is a skill outside `skills/`; it joins tier B in phase 4.)

**Tier A, prompt only (11).** A short plan or draft in the prompt, no fixture, no stubs. Start here.
`archReview`, `autoplan`, `cso`, `officeHours`, `planDesignReview`, `planDevexReview`, `productReview`, `withInterview`, `enhancePrompt`, `judge`, `prismStatus`.
(`judge` and `prismStatus` check their binary or server is missing and say so: grade the report, not the tool.)

**Tier B, small fixture repo (16).** A made-up repo built by a `fixture.sh`, plus stubbed `gh` and `git` remotes where needed, `AW_GIT_MODE=readonly`.
`rootCause`, `bugHunt`, `bugReport`, `bugFixOrchestrator`, `testAudit`, `review`, `addressReview`, `postReview`, `syncDocs`, `shipRelease`, `specToProvenPR`, `new-package`, `new-slice`, `weeklyRetro`, `landAndDeploy`, `canary`.
`review`, `addressReview` and `postReview` read a saved review JSON from `AW_OUT_DIR`; the fixture writes one. `landAndDeploy` and `canary` use a stub `curl` and a stub deploy command from `.agentic-workflow/deploy.json`.

**Tier C, stubbed binaries or servers (6).** Behaviour is checked against canned tool output: the process, the order of calls, the refusals. Not the tool.
`ui-evidence` (stub `ui-evidence`, as in the pilot), `verify-web` (mock Playwright server), `verify-ios` (mock XcodeBuildMCP), `verify-desktop` (stub `ui-evidence` desktop recipe), `verify-app` (dispatcher: which of the three it picks), `meta` (stub `aw-meta` with canned JSON).
MCP mocks live in `evals/mocks/<server>/<tool>.md`; a plugin `.mcp.json` declares the servers so they get mocked.

**Tier D, routing and dispatch only (19).** The design family needs a browser, a simulator or a reference site. Cases check that the right skill fires, that dispatchers (`design-analyze`, `-evolve`, `-implement`, `-mockup`, `-verify`) pick web, iOS or desktop from the prompt, and that the plan before the first tool call is right. No end-to-end runs.
`design-analyze`, `design-analyze-web`, `design-analyze-ios`, `design-evolve`, `design-evolve-web`, `design-evolve-ios`, `design-implement`, `design-implement-web`, `design-implement-ios`, `design-mockup`, `design-mockup-web`, `design-mockup-ios`, `design-verify`, `design-verify-web`, `design-verify-ios`, `design-verify-desktop`, `design-language`, `design-refine`, `design-shotgun`.

Tier counts: 11 + 16 + 6 + 19 = 52. Tier D gets the two routing cases and the near-miss only (3 cases), and its forced case checks the plan text. **Not evaluated:** the fetched external packs (`impeccable`, `emil-design-eng`, `taste-skill` family, `brandkit`), which are not ours.

## 4. Where the evals live

**Decision:** one `evals/` tree at the toolkit root, not one per skill directory.

```
evals/
  plugin/.claude-plugin/plugin.json     name aw-evals; skills -> ../../skills (symlink; resolved when staged)
  <skill>/routing/<case>/{prompt.md,graders/*.md}
  <skill>/behaviour/<case>/{prompt.md,case.yaml,graders/*.md}
  <skill>/fixture.sh                     shared by the skill's cases
  _lib/                                  shared fixture helpers, the stub-set loader
  mocks/<server>/<tool>.md
  stubs/bin/                             canned gh, curl, ui-evidence, aw-meta, ...
  results/                               in .gitignore, never committed
```

Reasons: the eval runner wants one directory (`--eval-dir`), the stubs and `_lib` are shared across skills, and `skills/<x>/` stays what `.agents/rules/skills.md` says it is (a manifest and prompts that every provider links). A skill's `evals/` inside its own directory would also be linked into each provider's skills dir. `.gitignore` gets `evals/results/` and `evals/**/.aw-out/`. Committed `results.json` snapshots stay out of the repo; CI keeps them as artifacts.

## 5. The clean-clone runner

### 5.1 Why

`claude plugin eval` refuses to load a plugin whose git directory registers many linked worktrees, and this repo has dozens. The pilot's `stage.sh` copied the plugin to `/tmp` outside any git repo. CI is unaffected (a fresh clone has no extra worktrees), but a lane working in a worktree needs the same thing locally.

### 5.2 `scripts/skill-evals.sh`

One script, generalised from `stage.sh`:

```
scripts/skill-evals.sh [--skills a,b | --changed | --all] [--kind routing|behaviour|both]
                       [--runs N] [--ablation none|with-without] [--model M] [--judge-model M]
                       [--threshold T] [--max-cost-usd N] [--jobs N] [--dry-run]
```

Steps: (1) resolve the skill set (`--changed` = skills whose files or evals differ from `origin/main`, plus every skill if `skills/_shared/**` or `skills/_preamble.md` changed); (2) stage `evals/plugin`, the chosen skills (`rsync -L`, links resolved), their `evals/<skill>/`, `_lib`, `mocks`, `stubs` into a fresh directory under `$AW_STATE_DIR/cache/skill-evals/` that is not inside a git repo; (3) check the day's spend (§6.4) and stop if over; (4) export `AW_TOOLKIT=<stage>`, `AW_OUT_DIR=<workspace>/.aw-out`, `AW_PREAMBLE=off`, `AW_GIT_MODE=readonly`, put `stubs/bin` first on `PATH`; (5) run `claude plugin eval <stage> --trust-plugin --scaffold --no-publish --json <out>` with the flags above and `--allow-tools Bash Write Edit`; (6) copy `results.json` and the report to `$AW_STATE_DIR/skill-evals/<date>/`, append cost to the day's tally, and print the WITH / W-OUT / Δ table in Markdown for the PR body; (7) exit with the tool's code (0 pass, 1 below threshold, 2 budget hit).

`--scaffold` runs author-written bash as you; the script passes it only for case files under `evals/` in the checked-out tree and refuses a staged tree that differs from `git ls-files` there. It never runs inside an agent session without `--dry-run`: only a lane lead or CI runs it. A bash test (`scripts/tests/skill-evals.test.sh`) covers `--dry-run`, the changed-skill resolution and the spend guard with a stub `claude`.

### 5.3 Stubs

`evals/stubs/bin/` holds one canned stand-in per binary named in any skill's `requires:`. A stub reads `evals/stubs/<name>.json` for its answers and refuses what the real tool refuses (the pilot's `ui-evidence` stub refuses a non-local host). Writes to the real network are impossible: stubs for `gh` and `curl` return fixtures and exit 1 on an unknown call, which a grader can see.

## 6. The gate

### 6.1 What runs when

| Trigger | Runs | Settings |
|---|---|---|
| PR that touches `skills/<x>/` or `evals/<x>/` | that skill's cases only | `--runs 2 --ablation none`, free graders only, `--threshold 0.8`, `--max-cost-usd 5` |
| PR that touches `skills/_shared/**` or `_preamble.md` | one case per skill in tiers A and B (forced case) | `--runs 1 --ablation none`, `--max-cost-usd 15` |
| Nightly | all cases | `--runs 3 --ablation with-without --threshold 0.7`, `--max-cost-usd 60`, `-j 4` |

The per-PR job also reads the JSON for the **routing gate**: on a skill's two natural cases the Skill-fired rate must be at least 50%, and on its near-miss case 0%. A skill that does not fire on its own natural phrasing fails the PR even when the score is fine. The nightly job fails any skill with mean Δ below 0 on its behaviour cases, so a skill that makes things worse is caught. It never charts a run with `partial: true`.

### 6.2 Cost budget per change

Pilot rates: about $0.18 per run (Opus default, judges included), $0.46 for the heaviest. A skill's 4 cases at 2 runs, one arm, is 8 runs, about $1.50 to $4. The $5 cap per PR holds that. A change that touches many skills runs them in sequence and stops at its cap; the PR body says which skills were skipped.

### 6.3 Models

**Decision:** the agent model is pinned in the script (`--model`), the same one for with and without and for every run in a table, so Δ is comparable. Pilot numbers used the default (Opus); to cut cost the per-PR job uses Sonnet 5.5, and the nightly job uses the default. The first phase measures both on the three pilot skills and reports the difference before this is fixed. The judge is `--judge-model sonnet`, not the Haiku default, and only on cases that need one (§3.2). Full-suite estimate at pilot rates: 208 cases × 3 runs × 2 arms = 1,248 runs, about $225 to $400. Per-PR only runs a small slice of that.

### 6.4 Daily spend cap

The cap is **$60 per day** of eval spend for this work (the owner's number, issue #1107). `scripts/skill-evals.sh` keeps a tally in `$AW_STATE_DIR/skill-evals/spend.jsonl` (date, command, cost from `--json`) and refuses to start when the day's total plus `--max-cost-usd` would pass $60, exiting 2. When it refuses, the lane lead stops running evals, tells meta in one line (spend so far, what was skipped), and resumes the next day. The cap is shared by everyone who runs the script; builders do not run it at all. The tally is also a line in each group PR (§8).

## 7. Description fixes

Skills are chosen by their `description`, so routing is fixed first, before behaviour is measured.

### 7.1 Worked example: `rootCause` step 1

Natural-trigger fire rates, 3 runs each, two prompts (a failing test; a customer complaint):

| Description | Fire rate |
|---|---|
| main: "4-phase systematic debugging, investigate, analyze, hypothesize, implement. ..." (what it is) | 0/3 and 0/3 |
| "Use when a test fails..." | 0/3 and 1/3 |
| "Use BEFORE fixing any bug ..., even when the fix looks small" | fires on single runs |

Two lessons. First, a "Use when" opener is not enough: Claude reads a bug as something it can fix directly, so the description must say the skill comes *before* the fix and applies even when the fix looks small. Second, one more version quoted words from the eval prompts and fired, and was rejected, because it passed by echoing the test, not by describing the skill.

### 7.2 Rules for descriptions

1. **Say when, not what.** Start with "Use when" or "Use BEFORE", name the situations in the user's words (a test fails, a bug is reported, an error needs diagnosing), and add one sentence of what it does.
2. **Add the exclusion.** One clause for the near miss ("not for writing new features").
3. **Never paraphrase a case prompt into a description.** A description may not contain a phrase of five or more words that also appears in any of that skill's case prompts. The script checks this mechanically (`scripts/skill-evals.sh --lint-descriptions`, part of the gate).
4. **Hold out phrasings.** Each skill has 2 natural cases for tuning and one more natural prompt kept in `evals/<skill>/routing/_heldout/`, written by someone other than the person editing the description and never read while editing. After a description change, the held-out prompt must fire as often as the tuned ones. If tuned cases fire and the held-out case does not, the description is overfitted; revert and widen it.
5. **Keep it short.** One to three sentences; the trigger cases come first. Long descriptions cost context in every session.
6. **Re-measure.** A description PR carries the before and after fire rates for all three natural cases at the default three runs; a single run is noise.

## 8. Rollout

Eval runs are for lane leads. Builders write cases and skill text only (standing rule: builders never build, test or run heavy work).

### Phase 0: plumbing (one lane, blocks everything)

1. `AW_TOOLKIT` and `AW_OUT_DIR` edit across `skills/` plus the check script (§2.1, §2.2).
2. Preamble split and `AW_PREAMBLE=off` (§2.3); `AW_GIT_MODE` in the five skills (§2.4).
3. `skills/_shared/output-contract.md` and the `requires:` field with its check (§2.5, §2.6).
4. `evals/` skeleton, `plugin.json`, stubs for `gh`, `curl`; `scripts/skill-evals.sh` and its bash test (§4, §5).
5. Move the pilot's three suites into the new layout; re-run once as the baseline row.
6. Update `.agents/rules/skills.md` and `planning/TESTING.md`.

Proof: the pilot's `rootCause` forced case runs in fewer turns than before; the three pilot skills score the same or better; `scripts/skill-evals.sh --dry-run` passes in CI. One lane, builders split by step 1 / step 2-3 / step 4-5 (they touch different files).

### Phases 1 to 4: skill groups, in parallel lanes

After phase 0 merges, these four groups run at once, each in its own lane and PR set. Groups touch disjoint skill directories and disjoint `evals/<skill>/` trees, so they do not conflict. Shared files (`evals/stubs/`, `evals/_lib/`) are phase 0's; a group that needs a new stub adds a new file, never edits an existing one.

| Phase | Skills | Needs |
|---|---|---|
| 1 | Tier A (11) | none; first, it is the cheapest, and it proves the case format |
| 2 | Tier B part 1: `rootCause`, `bugHunt`, `bugReport`, `bugFixOrchestrator`, `testAudit`, `new-package`, `new-slice`, `syncDocs` | fixture repo, `AW_GIT_MODE` |
| 3 | Tier B part 2: `review`, `addressReview`, `postReview`, `shipRelease`, `specToProvenPR`, `weeklyRetro`, `landAndDeploy`, `canary`, plus `bootstrap` | fixtures, stub `gh` and `curl` |
| 4 | Tier C (6) and Tier D (19) | MCP mocks, stub binaries, routing-only cases |

Inside a group, builders take one skill each (up to 8 at once), each writing: 4 cases and graders (plus self-tests), the description rewrite under §7, the `aw-result` section, and the fixture. The lane lead then runs the group's evals (`--runs 3`, with-without for behaviour, none for routing), pastes the table, and merges.

**Each group PR carries:** (a) the cases and graders; (b) a WITH / W-OUT / Δ table, one row per case, plus the Skill-fired rate for routing cases; (c) a before/after row for each description changed; (d) a spend tally ("this PR $X, today $Y of $60, total for #1107 $Z"); (e) `Refs #1107`.

Order inside the weeks: phase 0, then phases 1 to 4 together, then a nightly job turned on once phase 1 is green for three nights.

## 9. Open questions for the owner

1. **Agent model for the per-PR gate.** Run it on Sonnet 5.5 (about half the cost, a different model from what you use) or on the default Opus (full cost, same as production)? Recommendation: Sonnet per PR, Opus nightly.
2. **Full suite cost.** The complete nightly run is about $225 to $400 per night at pilot rates, which is above the $60 daily cap. Run the full suite weekly and a third of it nightly, or run all of it every night and lift the cap for that job? Recommendation: weekly full run, nightly tier A and B only.
3. **Tier D depth.** Routing and dispatch only for the 19 design skills, or also a real browser run for the web ones? Recommendation: routing only now; real runs are a later phase.
4. **Merge gate strictness.** Block a PR when a touched skill misses the routing gate (§6.1), or only warn for the first month? Recommendation: warn for a month, then block.
5. **Output contract on every skill.** Force the `aw-result` block on skills that only talk (`enhancePrompt`, `withInterview`), or leave those out? Recommendation: include them with `artifacts: []`, for uniformity.
6. **Where CI runs it.** The eval needs your account's credentials. Run it on your machine and attach the table by hand (as the lanes do now), or give CI a key with its own budget? Recommendation: by hand through the lane lead until the cap is proven.
