# Skill revamp: self-contained skills measured with `claude plugin eval`

**Status:** draft for the owner's read · **Date:** 2026-10-25 · **Asked by:** the owner, 2026-10-25 (issue #1107) · **Builds on:** meta's pilot (`evals-pilot/PILOT.md`, `pilot-results.json`, branch `feat/skill-evals-pilot`), `.agents/rules/skills.md`, `skills/_preamble.md`, `skills/_shared/capabilities.md`, the plugin eval docs (https://code.claude.com/docs/en/plugin-evals.md) · **Lanes:** ideas (this spec); a plumbing lane and skill-group lanes (§8) · **Next:** `/specToProvenPR` takes §8's phases to PRs.

**Keeping it app-agnostic.** Nothing here names a work app, company or customer. Fixtures are small made-up repos (a cart with a bulk-discount bug), and any outside tool is stubbed on `PATH`.

## 0. The short version

### For you

Today we cannot tell whether a skill helps. The pilot ran `claude plugin eval` on three skills and found three things: some skills never fire on natural phrasing, a skill's main output cannot be read by a check, and 50 of 52 skills only work when the toolkit sits at one fixed path in the home directory. This spec fixes those in order:

1. Make every skill self-contained: one toolkit-root variable, all output through `$AW_DIR`, a cheap preamble, a mode that touches no git state, a machine-readable result file.
2. Give every skill 3 or 4 eval cases (plus a held-out prompt), grouped into tiers by how much setup they need.
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

A skill is self-contained when a plugin that ships `skills/` can run it with nothing else installed except the tools it declares. **Goal:** this is for measurement first: the eval runner loads the skills as a plugin in a temp `HOME`. Installing the toolkit as a plugin for other people is not a goal of this spec and is not proven here; the changes keep that door open at no extra cost. The changes below are applied to all skills in phase 0. Most are a scripted rewrite, but not all one mechanical edit: 72 lines in `skills/` spell `~/.agentic-workflow/` by hand in prose, examples and bash, and each is reviewed after the rewrite.

### 2.1 `AW_TOOLKIT`

Replace every `$HOME/.agentic-workflow/toolkit` in `skills/` with `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}`. The default is today's symlink, so installed behaviour does not change. In a plugin, the eval sets `AW_TOOLKIT` to the plugin root, which already holds `skills/_preamble.md` and `skills/_shared/`. This removes the `evals/_lib/shim.sh` copy trick and the per-case `scaffold_script` stubs.

`.agents/rules/skills.md` ("Shared Fragments", "Preamble") is updated with the new text, and `scripts/` gets a check (like `check-env-example.sh`) that fails when a bare `$HOME/.agentic-workflow/toolkit` appears in `skills/**`. `AW_TOOLKIT` is the toolkit root, not the skills dir, so files the skills read beyond `skills/` (`config/voice/joi.md`, read by `_preamble.md`) stay where they are: the eval stage copies them to the same relative path. The stage copies `config/voice/joi-examples.md` too, since `joi.md:32` points at it by its installed `~/` path, and the check from §2.1 also scans `config/voice/`. Nothing moves, so `providers/lib.sh`, `providers/cursor/install.sh` and the three `config/agents/lean-*.md` files that read `config/voice/` are untouched. **Decision:** the toolkit root is one variable, not a path relative to the skill's base directory, because bash snippets inside a SKILL.md cannot know their own base directory on every provider.

### 2.2 Output under `AW_DIR`, no new variable

`skills/_shared/repo-slug.sh:13` already sets `AW_DIR="${AW_STATE_DIR:-$HOME/.agentic-workflow}/$REPO_SLUG"`. The fix is to make every skill write through `$AW_DIR` (today some SKILL.md text spells `~/.agentic-workflow/<slug>/` by hand; the check from §2.1 flags it: its pattern is any `agentic-workflow/` followed by `<` or `$` in `skills/**`, which catches the `<repo-slug>`, `<slug>` and `$REPO_SLUG` forms after `~` or `$HOME`, about 71 lines today). No `AW_OUT_DIR` is added. An eval sets `AW_STATE_DIR=<workspace>/.aw-state`, inside the run's writable workspace, which does two things at once: a `file_exists` or `regex` grader can read the report, and the state tools write (judge sessions, packets, `aw-meta` logs) lands in the throwaway workspace instead of the owner's real state dir (§5.4).

### 2.3 A cheap or skippable preamble

The preamble (about 10 KB) lists skills, runs a bootstrap check over every skill dir, may ask the user to run `setup.sh`, and loads prism context. Changes:

1. Split `_preamble.md` (50 skills) and `_design-preamble.md` (17 design skills, read on top of the first) the same way, each into a **core** (provider capability map, the one-line voice rule; under 1 KB) and an **extended** part (skill index, bootstrap check, prism context, Session Close). A skill's SKILL.md points at the core by default and at the extended part only where it needs it.
2. `AW_PREAMBLE=off` skips the extended part entirely. An eval sets it. Interactive sessions leave it unset, so nothing changes for a person.
3. The bootstrap check moves to `setup.sh` and `sindri doctor`, where it belongs; a skill never asks the user to run setup from inside a run.

Target: a forced `rootCause` run drops from 10-11 turns to 6-7. Phase 0 measures it.

### 2.4 A no-worktree mode

Skills that make worktrees, branches, commits, PRs or PR comments read `AW_GIT_MODE`. A grep today finds at least 9 (`specToProvenPR`, `shipRelease`, `bugHunt`, `bugFixOrchestrator`, `addressReview`, `review`, `postReview`, `syncDocs`, `ui-evidence`), and `landAndDeploy`, `canary`, `meta` and the `design-implement-*` pair mention worktrees or `gh`; phase 0 fixes the list from a grep for git and `gh` write commands, and a check keeps it current. They read it: `full` (default) or `readonly`. In `readonly` they work in the current directory, make no branch, commit, push or PR, and print the commands they would have run under a heading `Would run:`. Cases then grade the plan and the edits, not the git state. Because the mode is only an instruction, the eval's `git` and `gh` wrappers are the backstop. The `git` wrapper is a pass-through, not a canned stub: it runs the real `git` for everything (status, diff, log, show, ...) and under `readonly` refuses only `commit`, `push`, `worktree add`, `checkout -b`, `pr create|merge|comment` with exit 1 and a line a grader can match, so a skill that ignores the mode fails its case instead of writing. The wrappers go on `PATH` only for the agent's run; `fixture.sh` and `--selftest` use the real `git` (they need `git init` and `git commit`). Cases that need a real commit are not run under `claude plugin eval` (they stay with the existing bash and C# tests).

### 2.5 An output contract per skill

Some skills already end with a machine-readable tail that callers parse: `rootCause` ends its reply with `{status: fixed|unfixed|scope-breach|diagnosed, report_path, handoff_path, boundary}` (absolute paths; `bugFixOrchestrator` takes `handoff_path` from it), the review lenses end their report file with `verdict: PASS|NEEDS_WORK|BLOCKED`, and 50 skills end the reply with `## Next steps`. The contract **extends** those, never replaces them:

1. Under `AW_RESULT=1` every skill writes one result file, `$AW_DIR/results/<skill>-<timestamp>.json`; outside it, no file is written, so a person's normal runs leave nothing new behind. Required keys: `skill`, `artifacts` (absolute paths, so callers need no root), `summary` (one line). Everything else is the skill's own: `rootCause` keeps its `status` enum and its three path keys, a lens skill adds `verdict` mirroring its report's last line. No shared `status` enum is forced on skills that already have one.
2. In the reply, a skill that already prints a JSON tail (`rootCause`, and any other a phase 0 grep finds) keeps printing it always, as a fenced `aw-result` block with the two new keys added. Every other skill prints the block **only when `AW_RESULT=1`** (the eval runner sets it), so a person's reply stays as it is today.
3. Order: the `aw-result` block, then `## Next steps`. A lens's `verdict:` stays the last line of its report file.

The contract lives in `skills/_shared/output-contract.md`; a skill's SKILL.md ends with a "Result block" section naming its own keys. Skills that only talk (`enhancePrompt`, `withInterview`) use `artifacts: []`.

### 2.6 Declared dependencies

Every SKILL.md frontmatter gains `requires:` listing binaries, agents and MCP servers it calls (`requires: {bins: [gh], agents: [qa-runner], mcp: [playwright]}`). Phase 0 first checks that Claude Code, Codex and Cursor each still load a SKILL.md carrying the extra key (one skill, each provider, the description shows up); if any rejects or mangles it, the list goes in a sidecar `skills/<x>/requires.yaml` instead, with the same shape. The runner uses it to decide which stubs a case needs (§5.3), and `scripts/` checks that each name exists in the stub set or the real install. Agents a skill spawns (`qa-runner`, from `config/agents/`) are copied under the plugin's `agents/` in the staged copy; plugin agents are namespaced, so phase 0 checks that a skill spawning one by its bare name still finds it, and the skill text names it the way the plugin needs if not. Skills that call another plugin's skills (`specToProvenPR` calls `superpowers:using-git-worktrees` and `superpowers:writing-plans`) list them under `requires.skills` and are not self-contained: their forced case is graded on what happens up to that call, and the call itself is checked with `tool_used`.

## 3. The eval suite

### 3.1 Four cases per skill

| Case | Kind | Purpose |
|---|---|---|
| `<s>-natural-1`, `<s>-natural-2` | routing | a person's own words that should fire the skill (two different phrasings); `natural-1` is seeded from a real prompt (a scrubbed session prompt or an issue's text, named in `case.yaml`), `natural-2` is written |
| `<s>-near-miss` | routing | a request that belongs to the skill's closest sibling (§3.1a), so it must not fire this one; a skill with no sibling uses a request that only looks close |
| `<s>-forced` | behaviour | "use the `<s>` skill on ..." and grade what it does |

Tier D skills have no forced case (§3.3). Counting it all: 33 skills × 4 + 19 × 3 = 189 cases, plus one held-out natural prompt per skill (§7.2, 52) and the family case sets (§3.1a, about 40 prompts): about 280 cases, of which 33 are behaviour cases and the rest are routing.

**Decision:** routing and behaviour cases are kept apart, in two directories per skill (`routing/` and `behaviour/`) and tagged `routing` or `behaviour`. Reason: with the skill unfired a behaviour score means nothing, and under `with-without` the `tool_used: Skill` grader is excluded from the score, so a skill that never fires can still show Δ near 0 and look harmless (`rc-failing-test` showed Δ -0.04). Routing cases run with `--ablation none` and the fire rate is read from `--json`; behaviour cases run with the default `with-without` so Δ is real.

### 3.1a Sibling families

The real routing risk is a skill stealing a sibling's prompts, not firing on an unrelated request. `rootCause`'s merged description ("Use BEFORE fixing any bug, even when the fix looks small") can win prompts meant for `bugHunt`, `bugFixOrchestrator` or `bugReport`, and each skill's own gate would still pass. So skills are grouped into families, and each family has one **family case set** in `evals/_families/<family>/`: one prompt per member, graded on *which* skill fired first (a `regex` on the first Skill tool call's argument; later calls don't count, since `bugFixOrchestrator` calls `rootCause` on purpose), run with `--ablation none` whenever any member's description changes. The design dispatch family is too big to run whole (about 19 prompts): a change runs only the changed skill's dispatcher and its members. A family run has its own per-PR cap of $12, separate from the skill's $8.

| Family | Members |
|---|---|
| bug | `rootCause`, `bugHunt`, `bugReport`, `bugFixOrchestrator` |
| review | `review`, `addressReview`, `postReview` |
| plan review | `autoplan`, `archReview`, `productReview`, `planDesignReview`, `planDevexReview`, `cso` |
| ship | `shipRelease`, `landAndDeploy`, `canary` |
| verify | `verify-app`, `verify-web`, `verify-ios`, `verify-desktop`, `ui-evidence`, and each `verify-*`'s design twin (`design-verify-web`, `-ios`, `-desktop`) |
| intake | `officeHours`, `withInterview`, `enhancePrompt` |
| design dispatch | each dispatcher (`design-analyze`, `-evolve`, `-implement`, `-mockup`, `-verify`) with its `-web`, `-ios`, `-desktop` members |

Phase 0 runs the bug family once on today's descriptions as its baseline, before Phase 2 rewrites any of them. A description change that raises its own fire rate but drops a sibling's member below the routing gate (§6.1) fails.

### 3.2 Graders

Order of preference, and a case needs at least one grader from the first two:

1. `tool_used` (the Skill tool fired, or a Write went to the right path) and `file_exists`.
2. `regex` on the reply, the `aw-result` block, or an output file.
3. `llm` judge only where no pattern can express the check, always in a case that has at least two free graders, never as the only gate, and only after it gives the same vote on 3 of 3 re-judgements of one saved reply.

Graders that cannot fail or cannot pass are a known trap (the pilot's `no-write-outside-scope` matched `tests/` inside file content). Every new grader is checked once against a reply that should pass and one that should fail, written into the case directory as `graders/_selftest/`, before the case counts.

### 3.3 Tiers

Tiers group skills by what a case needs. Every directory under `skills/` with a SKILL.md is listed once. (`bootstrap/` is a skill outside `skills/`; it joins tier B in phase 3.)

**Tier A, prompt only (11).** A short plan or draft in the prompt, no fixture, no stubs. Start here.
`archReview`, `autoplan`, `cso`, `officeHours`, `planDesignReview`, `planDevexReview`, `productReview`, `withInterview`, `enhancePrompt`, `judge`, `prismStatus`.
(`judge` and `prismStatus` check their binary or server is missing and say so: grade the report, not the tool.)

**Tier B, small fixture repo (16).** A made-up repo built by a `fixture.sh`, plus stubbed `gh` and `git` remotes where needed, `AW_GIT_MODE=readonly`.
`rootCause`, `bugHunt`, `bugReport`, `bugFixOrchestrator`, `testAudit`, `review`, `addressReview`, `postReview`, `syncDocs`, `shipRelease`, `specToProvenPR`, `new-package`, `new-slice`, `weeklyRetro`, `landAndDeploy`, `canary`.
`review`, `addressReview` and `postReview` read a saved review JSON from `$AW_DIR/reviews/`; the fixture writes one. `landAndDeploy` and `canary` use a stub `curl` and a stub deploy command from `.agentic-workflow/deploy.json`.

**Tier C, stubbed binaries or servers (6).** Behaviour is checked against canned tool output: the process, the order of calls, the refusals. Not the tool.
`ui-evidence` (stub `ui-evidence`, as in the pilot), `verify-web` (mock Playwright server), `verify-ios` (mock XcodeBuildMCP), `verify-desktop` (stub `ui-evidence` desktop recipe), `verify-app` (dispatcher: which of the three it picks), `meta` (stub `aw-meta` with canned JSON).
MCP mocks live in `evals/mocks/<server>/<tool>.md`; a plugin `.mcp.json` declares the servers so they get mocked.

**Tier D, routing and dispatch only (19).** The design family needs a browser, a simulator or a reference site. Cases check that the right skill fires, that dispatchers (`design-analyze`, `-evolve`, `-implement`, `-mockup`, `-verify`) pick web, iOS or desktop from the prompt, and that the plan before the first tool call is right. No end-to-end runs.
`design-analyze`, `design-analyze-web`, `design-analyze-ios`, `design-evolve`, `design-evolve-web`, `design-evolve-ios`, `design-implement`, `design-implement-web`, `design-implement-ios`, `design-mockup`, `design-mockup-web`, `design-mockup-ios`, `design-verify`, `design-verify-web`, `design-verify-ios`, `design-verify-desktop`, `design-language`, `design-refine`, `design-shotgun`.

Tier counts: 11 + 16 + 6 + 19 = 52. Tier D gets the two routing cases and the near-miss only (3 cases), no forced case: a plan-text check without a real browser run says little. The fetched packs (`impeccable`, `emil-design-eng`, the `taste-skill` family, `brandkit`) are still **staged** for routing cases when they are installed, because they compete with the design skills for the same prompts in a real session. The same goes for other plugins' skills that compete with ours (`superpowers:systematic-debugging` and `compound-engineering:ce-debug` against `rootCause`, `superpowers:brainstorming` against `officeHours`): the stage adds a description-only stand-in for each installed one (its SKILL.md frontmatter and a one-line body), so routing numbers include them. A skill installed on a person's machine but not on the eval box is a known gap; the phase 0 PR lists which stand-ins were staged. **Not evaluated:** the fetched external packs (`impeccable`, `emil-design-eng`, `taste-skill` family, `brandkit`), which are not ours.

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

Reasons: the eval runner wants one directory (`--eval-dir`), the stubs and `_lib` are shared across skills, and `skills/<x>/` stays what `.agents/rules/skills.md` says it is (a manifest and prompts that every provider links). A skill's `evals/` inside its own directory would also be linked into each provider's skills dir. `.gitignore` gets `evals/results/` and `evals/**/.aw-state/`. Committed `results.json` snapshots stay out of the repo; CI keeps them as artifacts.

## 5. The clean-clone runner

### 5.1 Why

`claude plugin eval` refuses to load a plugin whose git directory registers many linked worktrees, and this repo has dozens. The pilot's `stage.sh` copied the plugin to `/tmp` outside any git repo. CI is unaffected (a fresh clone has no extra worktrees), but a lane working in a worktree needs the same thing locally.

### 5.2 `scripts/skill-evals.sh`

One script, generalised from `stage.sh`:

```
scripts/skill-evals.sh [--skills a,b | --changed | --all] [--kind routing|behaviour|both]
                       [--runs N] [--ablation none|with-without] [--model M] [--judge-model M]
                       [--threshold T] [--max-cost-usd N] [--jobs N] [--gate warn|block]
                       [--dry-run | --selftest | --lint-descriptions | --status]
```

Steps: (1) resolve the skill set (`--changed` = skills whose files or evals differ from `origin/main`, plus every skill if `skills/_shared/**` or `skills/_preamble.md` changed); (2) stage `evals/plugin`, **every** skill's SKILL.md, the chosen skills' full directories, `skills/_shared/`, both preambles, `config/voice/` and the `config/agents/` files a chosen skill spawns (`rsync -L`, links resolved), the chosen skills' `evals/<skill>/`, `_lib`, `mocks`, `stubs` into a fresh directory under `$EVAL_HOME/cache/` that is not inside a git repo; (3) check the day's spend (§6.4) and stop if over; (4) build the run's environment from scratch (§5.4): `AW_TOOLKIT=<stage>`, `AW_STATE_DIR=<workspace>/.aw-state`, `AW_PREAMBLE=off`, `AW_GIT_MODE=readonly`, `AW_RESULT=1`, `stubs/bin` first on `PATH`; none of these is exported into the caller's shell; (5) run `claude plugin eval <stage> --trust-plugin --scaffold --no-publish --json <out>` with the flags above and `--allow-tools Bash Write Edit`; (6) copy `results.json` and the report to `$EVAL_HOME/<date>/`, append cost to the day's tally, and print the WITH / W-OUT / Δ table in Markdown for the PR body; (7) exit 0 pass, 1 below threshold, 4 a routing gate missed (§6.1), 2 budget refused, 3 environment (`claude` missing, not logged in, a staged path resolving outside the repo). `--changed` that resolves to no skills prints `no skills changed` and exits 0.

`--scaffold` runs author-written bash (`fixture.sh`, scaffold scripts) as the owner. The script passes it only for a committed tree (it refuses uncommitted changes under `evals/` or `skills/`) on `main` or on a branch of this repo's own lanes; it never runs cases from a PR by an outside author until a lane has reviewed and re-committed them. `rsync -L` refuses any link that resolves outside the repo, and the stage is refused if it holds a `hooks/` dir or an `.mcp.json` other than the mocks'.

**Who runs it.** Lane lead sessions and the owner run the real thing; builders never do (they spend shared usage). Builders run `scripts/skill-evals.sh --selftest`, which calls no model and costs nothing: it lints the case files, runs each `fixture.sh` in a temp dir, runs every `regex`, `file_exists` and `tool_used` grader against its `graders/_selftest/` pass and fail replies (an `llm` grader gets a file-shape check only; the lead runs its 3-of-3 re-judge), and runs `--lint-descriptions`. A case is ready for the lead only when `--selftest` passes.

A bash test (`scripts/tests/skill-evals.test.sh`) covers `--dry-run`, `--selftest`, the changed-skill resolution, the exit codes, and the spend guard with a stub `claude` (including that the tally lands in `$EVAL_HOME/spend.jsonl` and survives a run whose child `AW_STATE_DIR` is the workspace).

Every skill is staged because routing is a choice among all of them: with only the tested skill loaded, a natural prompt has nothing else to pick and a near miss has no sibling to go to, so both numbers would look better than a real session. Only fixtures, cases and stubs are per skill.

### 5.3 Stubs

`evals/stubs/bin/` holds one canned stand-in per binary named in any skill's `requires:`. A stub reads `evals/stubs/<name>.json` for its answers and refuses what the real tool refuses (the pilot's `ui-evidence` stub refuses a non-local host). Stubs for `gh` and `curl` return fixtures and exit 1 on an unknown call, which a grader can see. This guards against honest mistakes, not a determined agent: the real tools are still reachable by absolute path, which §5.4 limits.

### 5.4 The run's environment

The script resolves the caller's eval home once, `EVAL_HOME=${AW_STATE_DIR:-$HOME/.agentic-workflow}/skill-evals`, before it builds anything; the tally, the stage cache and saved results always use `EVAL_HOME`, and only the child environment sees the workspace `AW_STATE_DIR`. So the tally never resets per run.

The run starts from `env -i` with an allowlist: `PATH` (stubs first), `TERM`, `LANG`, and the `AW_*` values from step 4. The outer `claude` process keeps the login it already has (its own config dir); the agent and the scaffold bash run in the temp `HOME` the eval tool makes for them, as in the pilot. *To check in phase 0:* a probe case that prints `env` and lists `$HOME` in the agent shows no token and no real home; if a credential does reach the agent, the script scrubs token patterns from `results.json` and transcripts before saving. `GH_TOKEN`, `GITHUB_TOKEN`, `AW_FORGE_TOKEN` and any other token are not passed. `HOME` is a temp dir and `AW_STATE_DIR` points inside the workspace, so judge, packets and meta state never land in the owner's real state dir. Fixture repos have no remote, so a stray real `git push` has nowhere to go. Saved transcripts and `results.json` stay under `EVAL_HOME` on the box; only the table goes into a PR body. `--selftest` runs `fixture.sh` in the same scrubbed environment, and lints fixtures for deletes or writes outside `$PWD`.

**Lane work goes through the script only.** A hand-typed `claude plugin eval` skips the spend tally and the environment above. Before a lane's first real run on a branch, the lane lead reads `git diff origin/main -- 'evals/**/*.sh'`, since that bash runs as the owner.

**On a failure** the script prints, per failing case: the case, the grader that failed, what it expected and what it got (first 200 characters), and the transcript path under `EVAL_HOME`. It prints one line per case as each finishes, so a long run shows progress.

## 6. The gate

### 6.1 What runs when

| Trigger | Runs | Settings |
|---|---|---|
| PR that touches `skills/<x>/` or `evals/<x>/` | that skill's cases, plus its family's case set (§3.1a) when its description changed | `--runs 3 --ablation none`, free graders only, `--threshold 0.8`, `--max-cost-usd 8` |
| PR that touches `skills/_shared/**` or `_preamble.md` | one case per skill in tiers A and B (forced case) | `--runs 1 --ablation none`, `--max-cost-usd 15` |
| Nightly | a rotating slice: it starts where last night stopped and runs cases from all four tiers (C and D included), cheapest tier first, until the next case's expected cost (its last run's cost) would pass $40; tiers A and B alone are about $90 to $250 at pilot rates, so a full cycle takes several nights | `--runs 3`, with-without on behaviour, `--threshold 0.7`, `--max-cost-usd 40`, `-j 4` |
| Weekly (only if you say yes, §9 Q2) | all cases | same settings, its own budget |

The per-PR job also reads the JSON for the **routing gate**: on a skill's two natural cases the Skill-fired rate must be at least 50%, and on its near-miss case 0%. A skill that does not fire on its own natural phrasing fails the PR even when the score is fine. The nightly job fails any skill with mean Δ below 0 on its behaviour cases, so a skill that makes things worse is caught. It never charts a run with `partial: true`.

### 6.2 Cost budget per change

Pilot rates: about $0.18 per run (Opus default, judges included), $0.46 for the heaviest. A skill's 4 cases plus its held-out prompt at 3 runs, one arm, is 15 runs, about $2.70 to $7. The $8 cap per PR holds that. A family case set (3 to 7 prompts at 3 runs, about $2 to $10) runs under its own $12 cap (§3.1a).

The per-PR gate is a cheap, one-arm check for later edits. It is not the table a group PR carries: a group PR (§8) runs its own full measurement once (`--runs 3`, with-without on behaviour cases), and that is the table in its body. A change that touches many skills runs them in sequence and stops at its cap; the PR body says which skills were skipped.

### 6.3 Models

**Decision:** the agent model is pinned in the script (`--model`), the same one for with and without and for every run in a table, so Δ is comparable. Pilot numbers used the default (Opus); to cut cost the per-PR job uses Sonnet 5.5, and the nightly job uses the default. The first phase measures both on the three pilot skills and reports the difference before this is fixed. The judge is `--judge-model sonnet`, not the Haiku default, and only on cases that need one (§3.2). Full-suite estimate at pilot rates ($0.18 to $0.46 a run): routing about 237 cases × 3 runs × 1 arm = 711 runs, behaviour 33 × 3 × 2 arms = 198 runs, about 900 runs, $160 to $420. Per-PR only runs a small slice of that.

### 6.4 Daily spend cap

The cap is **$60 per day** of eval spend for this work (the owner's number, issue #1107). `scripts/skill-evals.sh` keeps a tally in `$EVAL_HOME/spend.jsonl` (date, command, cost from `--json`) and refuses to start when the day's total plus `--max-cost-usd` would pass $60, exiting 2. The check and a **reserve row** (the full `--max-cost-usd`) are written under one `flock` on the tally file, so two lanes starting at once cannot both fit under the cap; when the run ends, a second row replaces the reserve with the real cost. An unreadable or malformed tally fails closed (exit 2). A reserve row whose process is gone (pid recorded in the row) or that is older than 3 hours counts as zero at the next start, so a crashed run does not eat the pool. Once the nightly is on (§8), the tally keeps two pools inside the $60, nightly $40 and everything else $20, and each job is checked against its own pool, so a nightly never starves a PR gate. A weekly full run, if added (§9 Q2), needs its own budget. When it refuses, the lane lead stops running evals, tells meta in one line (spend so far, what was skipped), and resumes the next day. The cap is shared by everyone who runs the script; builders do not run it at all. The tally is also a line in each group PR (§8).

## 7. Description fixes

Skills are chosen by their `description`, so routing is fixed first, before behaviour is measured.

### 7.1 Worked example: `rootCause` step 1 (merged as #1110)

Natural-trigger fire rates, 3 runs each, two prompts (a failing test; a customer complaint), with-without ablation:

| Description | Fire rate | Mean Δ (4 cases) |
|---|---|---|
| old: "4-phase systematic debugging, investigate, analyze, hypothesize, implement. ..." (what it is) | 0/3 and 0/3 | +0.03 |
| "Use when a test fails, a bug or customer complaint is reported..." | 0/3 and 1/3 | +0.06 |
| "Use BEFORE fixing any bug ..., even when the fix looks small" (merged) | 3/3 and 3/3; near miss not fired | +0.18 (4/4 pass) |

Spend for the whole step: $13.17.

Two lessons. First, a "Use when" opener is not enough: Claude reads a bug as something it can fix directly, so the description must say the skill comes *before* the fix and applies even when the fix looks small. Second, one more version quoted words from the eval prompts and fired, and was rejected, because it passed by echoing the test, not by describing the skill.

### 7.2 Rules for descriptions

1. **Say when, not what.** Start with "Use when" or "Use BEFORE", name the situations in the user's words (a test fails, a bug is reported, an error needs diagnosing), and add one sentence of what it does.
2. **Add the exclusion.** One clause for the near miss ("not for writing new features").
3. **Never paraphrase a case prompt into a description.** A description may not contain a phrase of five or more words that also appears in any of that skill's case prompts. The script checks this mechanically (`scripts/skill-evals.sh --lint-descriptions`, part of the gate).
4. **Hold out phrasings.** Each skill has 2 natural cases for tuning and one more natural prompt kept in `evals/<skill>/routing/_heldout/`, written by someone other than the person editing the description and never read while editing. After a description change, the held-out prompt must fire as often as the tuned ones. If tuned cases fire and the held-out case does not, the description is overfitted; revert and widen it.
5. **Keep it short.** One to three sentences; the trigger cases come first. Long descriptions cost context in every session, so `--lint-descriptions` fails any single description over 400 characters, and fails a PR that takes the total bytes of all descriptions past the Phase 0 baseline plus 25%. The rewrites add a trigger clause and an exclusion to most descriptions, so a tighter total would trip by the third group for no real cost.
6. **Re-measure.** A description PR carries the before and after fire rates for all three natural cases at the default three runs; a single run is noise.

## 8. Rollout

Eval runs are for lane leads. Builders write cases and skill text only (standing rule: builders never build, test or run heavy work).

### Phase 0: plumbing (one lane, blocks everything)

0. **Probe first.** The design rests on three things the pilot either saw fail or never tested. One probe case, run before any other phase 0 work, checks each and decides its fallback:
   - *Bash can write in the workspace* (pilot blocker 3 says Bash writes failed there; `repo-slug.sh:14` runs `mkdir -p "$AW_DIR"` through Bash). Fallback: skills create report and result files with the Write tool, which worked in the pilot, and `repo-slug.sh`'s `mkdir` failing is harmless because Write creates parent dirs.
   - *`AW_*` values reach the agent's shell* (the pilot only shows `PATH` passing through). Fallback (the pilot's `evals/_lib/shim.sh` approach, no file is sourced): the stage puts its copy of the toolkit at the default path inside the eval's temp `HOME` (`$HOME/.agentic-workflow/toolkit`), so `AW_TOOLKIT` and `AW_STATE_DIR` resolve by their defaults, and the staged copy of the skills has the eval values baked into its defaults (`${AW_PREAMBLE:-off}`, `${AW_GIT_MODE:-readonly}`, `${AW_RESULT:-1}`). Nothing in a real session changes, because only the staged copy is rewritten.
   - *`claude` keeps its login, and the agent sees no token and no real `HOME`* (§5.4). Fallback: the token scrub in §5.4.
   The probe's results go in the phase 0 PR body; the rest of phase 0 uses whichever path each one picked.
1. `AW_TOOLKIT` edit and the `$AW_DIR`-only writes across `skills/`, plus the check script (§2.1, §2.2). The rewrite is a script, not a hand edit, so a skill PR in flight when it lands rebases by re-running the script on its branch; meta holds new skill-text PRs for the hour it merges.
2. Preamble split and `AW_PREAMBLE=off` (§2.3); `AW_GIT_MODE` in every skill the grep finds, and the stub `git` / `gh` refusals (§2.4).
3. `skills/_shared/output-contract.md` and the `requires:` field with its check (§2.5, §2.6).
4. `evals/` skeleton, `plugin.json`, stubs for `gh`, `curl`; `scripts/skill-evals.sh` and its bash test (§4, §5).
5. Move the pilot's three suites into the new layout; re-run once as the baseline row. The baseline also records: (a) the 3 pilot skills' forced cases once with `AW_PREAMBLE` on and once off, so we know whether the eval's mode changes Δ (if it moves by more than 0.1, one preamble-on behaviour case per tier B skill joins the nightly); (b) the bug family's case set (§3.1a) on today's descriptions; (c) the total bytes of all 52 descriptions.
6. Update `.agents/rules/skills.md`, `planning/TESTING.md`, AGENTS.md (the script in Commands, its bash test in the bash test list and Merge Gate), and `.env.example` (`AW_TOOLKIT`, `AW_PREAMBLE`, `AW_GIT_MODE`, `AW_RESULT`) so `scripts/check-env-example.sh` passes.
7. Per-skill status: one small committed file per skill, `evals/<skill>/status.md` (fire rate on its natural cases, behaviour Δ, date of the last run, the PR), and `scripts/skill-evals.sh --status` prints them as one table, marking any row older than 7 days as stale (the rotating nightly can leave a skill unmeasured for several nights). One file per skill, so parallel group PRs never edit the same file.

Proof: the pilot's `rootCause` forced case runs in fewer turns than before; the three pilot skills score the same or better; `scripts/skill-evals.sh --dry-run` passes in CI. One lane, builders split by step 1 / step 2-3 / step 4-5 (they touch different files).

### Phases 1 to 4: skill groups, in parallel lanes

After phase 0 merges, these four groups run at once, each in its own lane and PR set. Groups touch disjoint skill directories and disjoint `evals/<skill>/` trees, so they do not conflict. Shared files (`evals/stubs/`, `evals/_lib/`) are phase 0's; a group that needs a new stub adds a new file, never edits an existing one.

| Phase | Skills | Needs |
|---|---|---|
| 1 | Tier A (11) | none; first, it is the cheapest, and it proves the case format |
| 2 | Tier B part 1: `rootCause`, `bugHunt`, `bugReport`, `bugFixOrchestrator`, `testAudit`, `new-package`, `new-slice`, `syncDocs` | fixture repo, `AW_GIT_MODE` |
| 3 | Tier B part 2: `review`, `addressReview`, `postReview`, `shipRelease`, `specToProvenPR`, `weeklyRetro`, `landAndDeploy`, `canary`, plus `bootstrap` | fixtures, stub `gh` and `curl` |
| 4 | Tier C (6) and Tier D (19) | MCP mocks, stub binaries, routing-only cases |

Inside a group, builders take one skill each (up to 8 at once), each writing: 4 cases and graders (plus self-tests), the description rewrite under §7, the Result block section (§2.5), and the fixture. The lane lead then runs the group's evals (`--runs 3`, with-without for behaviour, none for routing), pastes the table, and merges.

**Each group PR carries:** (a) the cases and graders; (b) a WITH / W-OUT / Δ table, one row per case, plus the Skill-fired rate for routing cases; (c) a before/after row for each description changed; (d) a spend tally ("this PR $X, today $Y of $60, total for #1107 $Z"); (e) `Refs #1107`.

**Rollout spend.** At pilot rates the four groups' measurement runs come to about $200 to $300, so several days under the cap. While groups run in parallel the day's $60 is split evenly among the lanes running evals that day (two lanes: $30 each; four: $15 each), enforced through the tally: a lane registers there on its first run of the day (its lane name, from `AW_META_LANE` or `--lane`), and a run's cap is 60 divided by the lanes registered at its start, minus what that lane already spent, so the first lane to start cannot starve the rest. A lane that registers late lowers the share only for runs that start after it. Phase 1 (tier A, cheapest) goes first so the format is proven before money goes to the others.

Order inside the weeks: phase 0, then phases 1 to 4 together, then the nightly job, turned on only after the four groups have merged. Until then the whole $60 a day is for the lanes' runs. After that the cap splits into two pools: nightly $40, per-PR and lane runs $20. The nightly is the rotating slice in §6.1.

## 9. Open questions for the owner

1. **Agent model for the per-PR gate.** Run it on Sonnet 5.5 (about half the cost, a different model from what you use) or on the default Opus (full cost, same as production)? Recommendation: Sonnet per PR, Opus nightly.
2. **Full suite cost.** The complete run is about $160 to $420 at pilot rates, above the $60 daily cap, so the nightly is a rotating slice inside $40 (§6.1) and a full cycle takes several nights. Is that enough, or do you also want a weekly full run with its own budget, so every skill has a same-day number once a week? Recommendation: rotating nightly only, until a regression slips through it.
3. **Tier D depth.** Routing and dispatch only for the 19 design skills, or also a real browser run for the web ones? Recommendation: routing only now; real runs are a later phase.
4. **Merge gate strictness.** Block a PR when a touched skill misses the routing gate (§6.1), or only warn for the first month? Recommendation: warn for a month, then block. Mechanically: the script takes `--gate warn|block`. Under `warn` a missed routing gate prints a `ROUTING GATE MISSED: <skill> fired <n>/<m>` line, marks the row in the PR table, and exits 0; under `block` it exits 4.
5. **Output contract on every skill.** Write the result file on skills that only talk (`enhancePrompt`, `withInterview`), or leave those out? The block shows in a reply only under `AW_RESULT=1`, so people never see it either way. Recommendation: include them with `artifacts: []`, for uniformity.
6. **Where CI runs it.** The eval needs your account's credentials. Run it on your machine and attach the table by hand (as the lanes do now), or give CI a key with its own budget? Recommendation: by hand through the lane lead until the cap is proven. If CI gets a key later, the job runs only on pushes to `main` and on PRs from this repo's own branches, never on a fork or an outside author's PR, because `--scaffold` runs the cases' bash.
