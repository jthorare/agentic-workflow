# Sindri picks a cheaper model for each agent step: Jev chooses, from every model the installed agents offer

**Status:** draft for the owner, revision 4 (autoplan round 3 fixes) · **Date:** 2026-10-26 · **Issue:** #1206 · **Owner decisions:** q123, 2026-10-26 (§1: M1 to M5; M3 is the one open question, §16) · **Builds on:** #1008 (a per-step agent, in flight on `bugfix/gh-1008`), #1175 (the judge's 100,000-character cap), the launcher spec (L-17, the model form), the workflow engine spec `2026-10-10-sindri-workflow-engine-design.md`, `docs/csharp/judge.md`, `docs/csharp/sindri-workflow.md`, `docs/csharp/sindri-profile.md`, `docs/csharp/sindri-ui.md` · **Shares a question with:** #1172 (the concept scan's size-based pick) · **Next:** /specToProvenPR takes §14's phases to PRs.

Every command name in this spec is marked **existing** (with the file that documents it) or **new** (a phase adds it). Every file path is one the author opened on main (14e2c72ea) unless it says "on `bugfix/gh-1008`".

## 0. Why

Every Sindri agent step runs on the owner's default model. Sindri launches plain `claude` and names no model, so the step gets `"model": "opus[1m]"` from `~/.claude/settings.json`. The launch side can already carry a model: `StepArgs.Model` (`src/Sindri/Aw.Sindri.Core/Launch/StepArgs.cs`) becomes `--model X` for Claude Code and `-m X` for Codex in `StepForms.Model` (`src/Sindri/Aw.Sindri.Core/Launch/Argv/StepForms.cs:32`). The one place it is never filled is `AgentLaunches.RequestFor` (`src/Sindri/Aw.Sindri.Core/Workflow/Engine/Runtime/AgentLaunches.cs:61`) and `EvaluatorRequestFor` (`:78`), which pass `Model: null`. The comment on line 61 reads "per-Step models are SC-R".

What that costs, from P-FRN-920 line/2 scoping (build f61f5f41a, 2026-10-26): all 18 agent sessions ran `claude-opus-5-5`.

| step | sessions | output tokens | cache-read tokens | cache-write tokens |
|---|---|---|---|---|
| draft | 6 | 69,584 | 14,640,829 | 676,754 |
| challenge | 3 | 45,622 | 11,430,922 | 357,701 |
| questions | 6 | 40,713 | 9,021,787 | 511,250 |
| apply | 3 | 13,912 | 1,910,135 | 227,947 |

`questions` and `apply` turn answers into options and edit JSON. They read about 11M of the 37M cache-read tokens, all at Opus rates. The challenger ran on the drafter's model too (#1008).

The owner wants the cheaper models used where the step doesn't need the top one, and wants Jev (the judge's remote model, `docs/csharp/judge.md` "Jev") to make that choice from every model the installed agents offer, not from a table someone keeps by hand.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| M1 | **Jev sees the whole picture.** The pick question gets the step's brief and its input pack (the files the launcher bundles and the facts it writes into the run directory), through the judge's normal redaction and untrusted-text fencing, bounded by #1175's rules: 100,000 characters, cut by whole files, `src/` first, the omitted files named in the prompt. Not metadata only. The number is checked against the real API before it is relied on (M-0, §4.3) | Owner q123. Whether `questions` is easy depends on what is in front of it, and a size alone can't say that |
| M2 | **The objective is the cheapest model that passes.** "Passes" means: the step's check passes, its output isn't refused, and it causes no redraft. There is no cost/quality score and no weights | Owner q123 |
| M3 | **The one escalation retry does not count against the recipe's redraft budget, and its tokens are capped at the larger of twice the failed run's spend and the state's per-round token budget** (§7.1). **OPEN: this is the one thing the owner has not decided; §16** | Default chosen so a cheap pick that fails never uses up a round the recipe gave the strong model. The cap is the larger of twice the failed spend and the state's own per-round budget, because a refused cheap run spends little and a cap equal to it would park most escalations |
| M4 | **No stopgap.** No static per-step model hint in recipes or the profile as a first step. The Jev picker is built directly | Owner q123 |
| M5 | **The pick's chain is Jev, then the rules fallback, and nothing else. It never falls to an agent CLI.** When the pick is on and Jev can't decide, the rules fallback picks **the cheapest candidate left after §4.1's constraints**; when the step has a floor, that is the floor model. When the owner turns the pick **off** (`workflow.agent.pick: off`), nothing is picked: every step runs as it does today, on the agent's default (the profile default), with no Jev call and no downgrade. Reason: an agent-CLI fallback runs the owner's default model (Opus) on a prompt of up to 100,000 characters at every step and eats the saving. And a fallback of "floor, else the profile's default" would run Opus on every state with no floor (`questions`, `apply`) whenever Jev is down, which is the problem this spec exists to fix. §16 note 2 says this tightens the owner's wording | Owner, relayed by meta 2026-10-26; the fallback wording is the lead's call on autoplan R1-01 |
| M6 | **Hard constraints are code, applied before Jev is asked.** Jev sees only the models that survive them (§4.1), so no answer of Jev's can break the recipe's floor, the profile's pin or cap, "challenger differs from drafter", or "a model the agent offers". Sindri also re-checks that the key Jev returns is one it offered (§4.5) | A rule that must always hold can't depend on a model's answer |
| M7 | **A step never launches on two models.** The pick is recorded before the session starts (§5), so an activity retry or a Temporal replay reads it back and uses it. One crash window is accepted: between Jev's answer and the append of `model.picked`, a retry may ask Jev a second time (one extra call, still before any session exists) | The same run must not start on two models; the extra call costs less than closing the window |
| M8 | **#1008 ships first.** This spec adds the model to its per-step target (§9) | It is the plumbing the issue text names |

## 2. Goals and non-goals

**Goals**
- A scoping run shows a model on every agent step's launch event, with `questions` and `apply` on a cheaper model than `draft`, and the challenger on a different model from the drafter.
- Tokens and cost per step visible from Sindri's own records (the ledger), without reading transcripts.
- A new model an agent starts offering is usable without a code change (§3).
- The owner can pin or cap the choice in the profile; a recipe can name a floor for a step.
- With the pick on and Jev unavailable (no key, down, over its limit), steps still run below the draft's model (M5). With the pick off, they run as today.

**Non-goals**
- Picking the agent itself (Claude Code, Codex, Cursor). That is #1008. This spec picks the model within the agent #1008 resolved.
- Choosing a reasoning effort or any other model setting. Only the model id.
- A cost/quality score, or a learned model of cost (M2).
- Per-repo overrides. The profile is one approved file per machine (`docs/csharp/sindri-profile.md`), and the new keys sit in it like `workflow.agent.target`. A repo can't have its own pick settings in this spec.
- Changing the context engine's model calls. #1172 shares the question (§10) but its call sites are its own.

## 3. Finding the models each agent offers

Today `TargetFacts` (`src/Sindri/Aw.Sindri.Core/Launch/TargetFacts.cs`) carries id, present, version, limited, problem, source, overridden, and no model list. `Launcher.Probe` (`src/Sindri/Aw.Sindri.Infrastructure/Launch/Launcher.cs:89`) builds one `TargetFacts` per catalog entry and the result is recorded as `launch.probed` (`LaunchProbed`, `src/Sindri/Aw.Sindri.Core/Launch/LaunchProbed.cs`).

`TargetFacts` gets `Models: IReadOnlyList<ModelFacts>` (id, display name, tier, source). The probe fills it per agent:

| Agent | Where the list comes from | Checked on this box |
|---|---|---|
| Claude Code | No list command exists, so Sindri keeps a small catalog file (`launcher.json`'s agent entry, the same file that holds `ModelForm`) with the aliases the CLI accepts (`haiku`, `sonnet`, `opus`) and the full ids, overridable in the profile (§8). `claude --help` documents `--model <model>` | `claude --help` lists `--model`; no list verb |
| Codex | `codex debug models` renders the model catalog as JSON (`--bundled` skips the refresh). On this box it printed six entries; each has `slug` (`gpt-5.5`, `gpt-5.4`, `gpt-5.4-mini`, `gpt-5.3-codex`, `gpt-5.2`, `codex-auto-review`), `visibility`, `priority` and `supported_in_api`. The probe keeps the `visibility: list` ones | Run here; the JSON shape above is what the parser is written against |
| Cursor | **Unknown.** `cursor-agent` isn't installed on this box, so its model flag and any list verb are unverified. `StepForms.Model` already refuses it (`ModelForm.CursorFlag` is "reserved until it is verified", SND-LAUNCH-013). The pick treats Cursor as offering no models until someone verifies it on a box that has it (§16, open item O2) | Not checked |

A probe that can't read an agent's list (the command fails, times out at 5 s, or isn't there) leaves that agent's `Models` empty and shows `no-model-list` as the target's `Limited` text (it is informational and never fails a launch, `SND-MODEL-004`). The pick for that agent is then the rules fallback with no candidates, which is the agent's own default (`by: default`, §6). The probe never invents a list.

**Tiers.** The rules fallback, the floor (§8) and the escalation (§7) need an order and three bands. They are computed in this order, so they never shift under the reader:
1. Take the models of the agent: the profile's `workflow.agent.models.<agent>.list` if given (cheapest first, as written), else the discovered list.
2. Order them: the profile's order if given; else Claude haiku, sonnet, opus; else Codex by `priority` (a larger number is cheaper) with a `-mini` suffix before its sibling.
3. Cut the ordered list into bands: with N models, `cheap` is the first ceil(N/3), `strong` the last ceil(N/3), `standard` the rest (N = 1 puts the model in all three; N = 2 gives cheap = first, strong = second, standard = both).
4. A model whose place can't be told is placed last (strongest) and the probe says so (`unordered: <id>`), so it is never picked as "cheaper" by accident and the owner is told it needs a place in `models`.

`sindri launch show` (**existing**, `docs/csharp/sindri-launcher.md:132`) prints, under each agent, every model with its place and tier: `haiku  cheap`, `sonnet  standard`, `opus  strong`. Phase M-3 adds those lines. The bands are computed after the profile override, so adding a model to `models` is shown at once.

**Prices.** `Budgets.PriceOf` (`src/Sindri/Aw.Sindri.Core/Launches/Budgets.cs:35`) prices a model by id prefix and returns null for models its table lacks. The pick doesn't use prices to choose (M2). Prices are for the cost shown next to a step's tokens (§6, §15.7); an unpriced model shows "cost unknown" and the totals say they are partial.

## 4. What Jev decides, and with what

### 4.1 The constraints, in code, before Jev

For one step launch, `ModelCandidates.For(step, target, ...)` (pure, `Aw.Sindri.Core/Launch/`) starts from the models the resolved agent offers (§3) and removes, in this order:

1. models below the recipe's **floor** for this step (§8);
2. models above the profile's **cap**, or, when the profile **pins** a model for the step or the whole agent, every model but the pin;
3. for a challenger step, the model the drafter ran on (**challenger differs from drafter**, SCP-CHAL-002, `src/Sindri/Aw.Sindri.Features/Scoping/ScopePacketChecks.Challenge.cs:78`). When the drafter's launch has no recorded model (an older run, `launch.started.Model` null) and the challenger's agent is the drafter's, the step parks with SND-MODEL-003 rather than skip the rule;
4. for an escalation, every model at or below the one that just failed (§7.1).

What is left is the candidate list. One candidate: no question is asked, the pick is that model, recorded as `by: constraint`. Zero: SND-MODEL-003.

### 4.2 The question

A new judge question, `model-pick`, in `src/Judge/Aw.Judge.Core/Questions/` beside `ChooseQuestion`. It is not `choose`, for two reasons: `choose` names must match `[a-z][A-Za-z0-9]{0,31}` and model ids have hyphens and dots, and `choose` drops facts past `PromptFactsCap` = 16,000 characters (`ChooseQuestion.cs`), which is far under M1's 100,000.

Sindri calls it the way it calls `choose`, through the judge CLI (**existing**: `judge <question> < input.json`, `docs/csharp/judge.md` "CLI contract") with the input record on **stdin**, never as an argument. Linux limits one argument to 131,072 bytes and a 100,000-character input with JSON escaping can pass that; stdin has no such limit. Phase M-2's test sends a 130,000-character input end to end, and M-2 ships a sample input, `tests/fixtures/judge/model-pick-input.json`, with the command that runs it (`judge model-pick < tests/fixtures/judge/model-pick-input.json`), so a contributor can try the question by hand.

**Evaluator entry.** The outputs are `m1` to `mN`, different per call, so `model-pick` can't be a fixed `QuestionDefinition`. Like `choose` (`QuestionEvaluator.Choose.cs`, `ChooseQuestion.For(input)` builds the definition per call), M-2 adds `QuestionEvaluator.ModelPick.cs` and `ModelPickQuestion.For(input)`, and registers it in the catalog next to `choose`. The rules fallback cannot use `FallbackRules` (`src/Judge/Aw.Judge.Core/Questions/QuestionDefinition.cs:43`, run by `QuestionEvaluator.cs:147`): that only runs after a model answered below its threshold, not when there is no key or Jev times out. So `ModelPickQuestion` also supplies a rules provider that answers `m1` (the candidates are sent cheapest first) whenever the chain reaches it, and Sindri's own fallback (below) covers the judge being unreachable or exiting non-zero.

Input record `ModelPickInput`:

| Field | What |
|---|---|
| `step` | the step's block id and state id (`step.interview.questions`, `questions`) |
| `kind` | `draft`, `challenge`, `questions`, `apply`, `other` (from the block's descriptor) |
| `brief` | the rendered step brief (`AgentStepBrief.Render`), redacted |
| `files` | **in the request's `state` only**: the pack bundle's files (`PackBundle.Files`, `src/Sindri/Aw.Sindri.Core/LensPacks/PackBundle.cs`) and the written facts (`StepArgs.Facts`), each as `{path, text}`, after §4.3's filters |
| `candidates` | `[{key, model, tier, price}]` after §4.1. `key` is `m1`, `m2`, ... in cheapest-first order; the question's output is a key, and the real id is looked up by Sindri, so a model id never has to fit the name pattern |
| `history` | for each candidate, this recipe step's record from §7.2: runs, refused, check failed, redraft caused. Counts and codes only |
| `previous` | for an escalation: the model that just failed and how (`refused`, `check-failed`, and the check's name). Codes only, no free text |

The prompt tells Jev the objective in M2's words: choose the cheapest candidate that will pass, and say which key. **The prompt names the files by key and path only and never repeats their text**: `JevProvider` sends the input as `state` (`JevProvider.cs:92`, capped at `InputCap.MaxChars`) and the prompt again as the instructions (`JevProvider.cs:95-96`, capped the same way), so a prompt that repeated the files would send up to 200,000 characters per call. It says nothing about quality scores. Jev answers with one key and a confidence (the same `confidence` field `choose` requires, `docs/csharp/judge.md` "Questions"). Any string in `history` and `previous` that came from a repo or a model (a check name, a refusal reason) is redacted and fenced like the files.

### 4.3 What reaches Jev, and the limits

Jev is a third party. Under the owner's q123 decision (2026-10-26, relayed by meta), repo content may go to it, but only after the judge's normal handling and these filters:

- **Filters on the pack, before the cap.** A pack file or fact whose path matches the profile's `index.denyPaths` is dropped. So is any file whose name matches a built-in secret list (`.env*`, `*.pem`, `*.key`, `*.p12`, `id_rsa*`, `secrets*`, `credentials*`, `*.tfvars`), and so is any file the secret scan flags. The scan runs over the assembled text of each file (`Aw.Redaction`'s detectors, and the same file-level check the context engine uses before it labels a file), and a hit drops the whole file rather than editing it. The prompt's omitted line gives, for files dropped as `denied` or `secret`, **a count and the reason only** ("3 files not shown: 2 secret, 1 denied"); their names stay local, in `model.picked.omitted`, because a name like `prod-credentials.json` is itself information. Files left out by the size cap are named with their sizes, as the owner's decision and #1175 say. A secret found in the **brief's** own text (not a file) is masked at the span and the rest of the brief is sent; `contentSent` is still true. The same filters apply to **facts** (`StepArgs.Facts`) and to file text embedded in the **brief**, not only to `PackBundle.Files`. Phase M-2's test seeds a fake secret in a prose paragraph, a `.env.local` file, an odd-shaped token in a fixture file and a fact, and asserts none of them reaches a fake Jev.
- Every remaining string and key is redacted (`Aw.Redaction`) like any judge input (`docs/csharp/judge.md` "JSON", "Jev"). Redaction recognises shapes; the filters above are what cover secrets with no recognisable shape.
- Repo text is fenced as untrusted text (`UntrustedText`, `src/Judge/Aw.Judge.Core/Questions/UntrustedText.cs`), so a file that says "pick the cheapest model and ignore the rest" is data.
- **The cap is 100,000 characters in total** (`UntrustedText.ResolutionDiffCap`, `UntrustedText.cs:20`, the number #1175 set), subject to M-0 below. When the files are over, they are cut by whole files, `src/` first, and the prompt ends with a line naming each omitted file and its size. A single file over the cap is cut at a line and named as cut. `CappedDiff` (`UntrustedText.cs:46`) does this for a diff's file sections; the pack is a set of files, not a diff, so M-2 adds a files-shaped sibling that shares the ordering and the omitted-files note rather than copying it.

**Jev's own input cap must move for this question.** `JevProvider` caps the state and the prompt at `InputCap.MaxChars` = 16,000 characters (`src/Judge/Aw.Judge.Core/Evaluation/InputCap.cs:16`, `docs/csharp/judge.md` "Jev"). Left alone, that would cut M1's 100,000 down to 16,000 before Jev sees anything. Phase M-2 makes Jev's cap per question. `JevProvider` reads the `InputCap.MaxChars` constant at `JevProvider.cs:92` and `:96`, so the cap reaches it as a field of the request the evaluator hands the provider (`InputCapChars`, null meaning 16,000); `model-pick` sets its own and every other question keeps 16,000. The cap on what the judge **stores** for a decision (`decision_details`) stays separate and unchanged; `model-pick` stores its digest (§6), not the capped text.

**M-0, the size spike (runs before M-2).** Jev's request size limit at `api.typesafe.ai` is not documented anywhere in the repo. M-0 is one real call, on a 100,000-character fixture (synthetic text, no repo content) **in the request's real shape** (the fixture goes in `state` and the prompt names the keys only, as §4.2 says; not as one big prompt), with the owner's `TYPESAFE_API_KEY`: does the API accept it, answer in under the question's 20 s budget, and use the whole prompt (the fixture ends with an instruction to answer a specific key; a truncated prompt answers wrong). The result goes in M-0's PR body and **amends this section**: if the API accepts it, 100,000 stands; if it accepts less, the cap for `model-pick` is the measured number (rounded down to a safe margin) and §1 M1 and §4.3 are rewritten with it. M-2 does not start its cap work until M-0 is merged. **If M-0 is not run** (it needs the owner's key), `model-pick` keeps Jev's 16,000-character cap and ships with it: the pack is then cut to 16,000 by the same whole-file rule, and the amendment to 100,000 follows when M-0 is run. A truncation at pick time can't be detected from the response, so the wrong-key check belongs to M-0 only. **When the API rejects an input or truncates it at pick time (a 413, a 4xx, or a timeout), the pick goes straight to the rules fallback with the reason `jev-input-rejected`. It does not re-cut the input and try again**: a second call costs 20 s more and the fallback is already the cheap-safe answer.

### 4.4 The chain (M5)

The question's providers are `jev` then `rules`. Not the agent CLIs. The chain is fixed in the question's definition in code, and `config.json`'s per-question `providers` entry (`docs/csharp/judge.md`, "How a call runs") can only narrow it (for example to `rules` alone), never add to it. M-2's tests: a `config.json` that lists `claude-cli` for `model-pick` still never hands the prompt to the agent-CLI provider (a fake agent CLI that records calls asserts zero), and a `--providers claude-cli` flag leaves only `rules`.

With `workflow.agent.pick: off` the chain is not used at all: no judge call, no `--model` flag, `by: default` (§6). With the pick on, the rules fallback is **the cheapest candidate left after §4.1**: with a floor, the floor model (the cheapest at or above it); without one, the cheapest the agent offers inside the cap. If the agent offers no models (Cursor today, or an unreadable list), there is no candidate and the pick is `by: default`: no `--model` flag, the agent's own default.

A Jev outcome that isn't a usable key (unavailable: no key; timeout; below the confidence threshold; an unknown key; `jev-input-rejected`) goes to the fallback and records why (`reason`: the judge's reason codes, `docs/csharp/judge.md` "Reason codes", plus `jev-input-rejected` and `unknown-key`).

Threshold and budget: confidence threshold 0.6 (same as `choose`), call budget 20 s. A pick never holds a launch longer than that.

### 4.5 Can Jev be steered?

A pack file could try to make Jev answer "cheapest" (the fence in §4.3 is the first defence). The rest is in Sindri, not in Jev:
- Sindri only accepts a key from the candidates it sent. Anything else is `unknown-key` and goes to the fallback. M-5's test sends a fake Jev answer of `m9` and `opus` and expects the fallback.
- Floors on `draft` (strong) and `challenge` (standard) in the scoping recipe (§8) mean an injected "cheapest" can't put those steps below their floor.
- `model.picked` carries `unusual: true` when the picked model has failed its check or been refused in more than half of its last 20 runs for this step (§7.2). The Runs table shows "unusual" (§6.1), so a steering attempt that works leaves a mark.

## 5. Where the pick runs

Inside the launch activity, `AgentLaunching.Launch` (`src/Sindri/Aw.Sindri.Workflows/AgentLaunching.cs:27`), after the target, workspace and bundle are resolved and before `launcher.Launch`. The order today is target, workspace, bundle, `Request`, `launcher.Launch`, `recorder.Started`. The pick goes between the bundle and the request, and its result is passed to `AgentLaunches.RequestFor` as the `Model` argument that today is null. `RequestFor` and `EvaluatorRequestFor` get a `string? model` parameter. The evaluator (the holdout evaluator, LN-LCH2) is picked like any step, with the floor of its recipe.

**All of the work is in the activity.** The workflow code (`RecipeWorkflow`) does no pick, reads no ledger and builds no candidate list. It passes the activity what it already has: `AgentLaunchRun` gains `Previous: PreviousRun?` (the model that failed, `refused` or `check-failed`, the check's name, its token spend) and `Escalation: int` (0 for the first run, 1 for the one retry). The activity reads the approved profile, the probe's model list, the history view and, for a challenger, the drafter's recorded model; builds the candidates (§4.1); calls Jev; and returns `AgentLaunched` with two new fields, `Model` (which the workflow keeps so a later step can name "the drafter's model" without reading anything) and `CanEscalate` (true when a stronger model is still allowed inside pin and cap and the step reads no `fact:workspace`). New fields on `AgentLaunchRun`, `AgentLaunched`, `AgentStepRun` and `OpenRun` are **optional last parameters with defaults** (`Previous` null, `Escalation` 0, `Model` null, `CanEscalate` false) so an in-flight instance's history still reads.

The activity is retried and idempotent per launch id (`AgentLaunching` summary; `LaunchIdFor` in `AgentLaunches.cs` is `LCH-` plus 32 hex digits of SHA-256 over `workflow:run`). A retry would call Jev again and might get a different model. So the pick is stored first:

1. The activity appends `model.picked` (§6), key `model.pick:<launchId>`, first write wins, the same pattern as `launch.outcome:<launchId>` (`AgentLaunchRecorder`).
2. If the append says the key already exists, the activity reads the stored pick and uses it. Jev is not asked.
3. `launch.started` then carries the same model.

**The escalation run has its own launch id.** `LaunchIdFor` hashes `workflow:run:escalation:<n>` for n >= 1 and stays `workflow:run` for n = 0, so every launch id that exists today is unchanged. Seven places use `LaunchIdFor` today, and all must see the escalated id, or they would watch, close and display the failed first launch:
- `AgentStepRun` (`src/Sindri/Aw.Sindri.Core/Workflow/Engine/Runtime/AgentStepRun.cs:11`) gets `Escalation: int = 0` as its last parameter, and `LaunchIdFor(AgentStepRun)` (`AgentLaunches.cs:18`) reads it. That covers `AgentLaunches.cs:60` and `:77` (the two requests) and the three workflow callers that pass the run: `RecipeWorkflow.Agent.cs:111` (the open run's `Launch`), `RecipeWorkflow.Agent.cs:143` (watch) and `RecipeWorkflow.AgentClose.cs:35` (close).
- `OpenRun` (`src/Sindri/Aw.Sindri.Core/Workflow/Engine/Instances/OpenRun.cs:8`) gets `Escalation: int = 0` as its last parameter, and the two callers that derive the id from the run token alone, `InstanceViews.cs:95` and `WorkflowEventDetail.cs:89`, pass it to `LaunchIdFor(workflow, run, escalation)` (`AgentLaunches.cs:28`, third parameter optional, default 0).
- M-6's test watches and closes an escalated run through the workflow (the fake launcher sees the `-e1` launch id for the watch and the close) and checks `InstanceViews` and `WorkflowEventDetail` show the `-e1` id.
 With the same id, the escalation would read back the failed cheap pick (first write wins, step 2) and the launcher would see the old session; and `launch.outcome:<launchId>` would collide with the failed run's outcome. A new id gives the retry a fresh `model.pick:` key, a fresh `launch.outcome:` key, a fresh worktree name (when the launcher makes the worktree, `sindri-<instance>-<run>` becomes `sindri-<instance>-<run>-e1`) and a fresh session. **Steps that read a `fact:workspace` do not escalate in v1.** For them `WorktreeFor` (`AgentLaunches.cs`) returns the existing workspace as is, so the retry would start in the failed run's dirty worktree (half-written files, commits). They fail as they do today; making them escalate needs a reset of that worktree, which is out of scope here. M-6's test: a failed pick on model A, then an escalation, gives two `model.picked` rows with different launch ids, the second with `escalatedFrom: A`, and two `launch.outcome` rows.

Temporal does not re-run a completed activity on replay, so replay is safe already; the key makes an activity retry safe too (M7).

## 6. Recording it

New event `model.picked` (Core/Events, payload `ModelPicked`):

| Field | What |
|---|---|
| `launchId`, `item`, `instance`, `state`, `attempt` | the same identifiers `launch.started` has |
| `target` | the agent |
| `model` | the model id launched; null when `by` is `default` |
| `by` | `jev`, `constraint` (one candidate), `rules` (the fallback) or `default` (no candidates) |
| `reason` | for `rules`: why Jev didn't decide (judge reason code) |
| `decision` | the judge decision id, so `judge why <id>` explains the pick |
| `candidates` | the keys and ids Jev was shown |
| `briefHash` | SHA-256 of the brief Jev saw |
| `files` | the files Jev saw, as `[{path, chars}]` |
| `omitted` | the files left out, as `[{path, chars, why}]` with `why` one of `cap`, `denied`, `secret` |
| `jevMs`, `jevCalls` | the milliseconds Jev took and the number of calls made for this pick (0 when it wasn't asked), so the cost of picking is visible next to the saving |
| `contentSent` | true when any file text or brief text went to Jev (false for `constraint`, `rules`, `default`) |
| `unusual` | §4.5 |
| `escalatedFrom` | the model that failed, for an escalation (§7), else null |

**Never file text.** `model.picked` and the stored judge decision for a `model-pick` call keep the brief's hash, the candidates, the file list with sizes and the omitted list, and no file text and no brief text. This is a special case of the judge's rule: the judge stores a redacted copy of every input for 30 days (`docs/csharp/judge.md` "Retention"), and for `model-pick` the stored form is this digest, not the 100,000 characters, so a leak the redactor missed is not kept. M-2 makes the question declare a stored form (a `StoredInput` projection beside `ModelPickInput`) and tests that a stored decision contains none of the fixture's file text.

**`model.picking`** (Core/Events, payload `ModelPicking`: `launchId`, `item`, `instance`, `state`, `attempt`, `startedAt`) is appended just before Jev is asked, with the key `model.picking:<launchId>`. It exists so the app can show "choosing model…" (§6.1); `model.picked` for the same launch ends it. A `model.picking` with no `model.picked` after 60 s reads in the Runs row as "no pick was recorded; this run is being retried" (the activity crashed and Temporal is retrying it); the next attempt of the activity appends `model.picked` as usual.

`LaunchStarted` (`src/Sindri/Aw.Sindri.Core/Events/LaunchStarted.cs`) gets `Model: string?` (a new optional last parameter, so older events read as null). `SessionUsage` already has `Model` (`SessionUsage.cs:15`), so tokens per step come with the model they were spent on; cost per step is `Budgets.PriceOf` applied to those rows.

### 6.1 Reading it: `sindri launch picks` and the app

**`sindri launch picks [--item X] [--json]` (new, M-5).** A read-only command that never starts the control plane (exit 1 with `SND-API-001` when it isn't running, like `sindri status`). It prints one line per pick, oldest first:

```
ITM-4f2a  questions     claude-code  haiku   jev         cheapest Jev expects to pass                  12k tokens  $0.02
ITM-4f2a  draft         claude-code  opus    constraint  only one model was allowed (floor: strong)    61k tokens  $1.90
ITM-4f2a  questions.e1  claude-code  sonnet  jev         retried on sonnet after haiku was refused     31k tokens  $0.14
```

`--json` prints the `ModelPicked` records with the run's token totals. Each line also carries Jev's cost of picking (`jev 1 call 2.1 s`, or `jev not asked`), so a pick that costs more than it saves can be seen; the Done-when run (§15.8) compares the two.

**The route behind it (M-5, not M-8).** `GET /sindri/launch/picks?item=<id>&instance=<address>` answers `LaunchPicks` (`picks: [LaunchPickView]`, a `LaunchPickView` being the `ModelPicked` fields, the run's tokens, its priced cost or null, its outcome word `refused`, `failed`, `passed` or `running`, and `picking: bool` while only `model.picking` exists). It is a new `ISindriApi` method (`LaunchPicks`), with its record types in `Api/`, its entry in `SindriJsonContext`, an `ApiViews` name, a golden under the API goldens, and a fake in `Aw.Sindri.Ui.Testing`. `sindri launch picks` and the app's Runs table both read it. M-8 only draws what this route serves. It is the first place anyone can see why a step got its model; it ships with M-5, not with the UI.

**What the owner sees in the app (M-8).** The app is Avalonia (`docs/csharp/sindri-ui.md`). The pick shows in the instance frame's Runs table, which is the one place (the Agents page does not get a chip: a session row has no launch id to join on, and a second place would need a join this spec doesn't define):

1. **The instance frame** (`LaunchPicks` for the instance) (U8, "the instance frame", `docs/csharp/sindri-ui.md` "U8"). The frame is read-only today (U8-min) and has no Runs table, so M-8 adds one. It goes in a step's expanded state, as a read-only table that draws what the route serves (no command, no send), inside the frame's 760 px sheet; below 1100 px the sheet covers the page as the other sheets do. One row per run of the step. Columns: **Step** (the state's display name, "Write questions for you"), **Model** (the model's short name, "Haiku"), **Tokens** (input plus output, "41k"), **Cost** ("$0.18", or "cost unknown" when `Budgets.PriceOf` has no price for that model), **Why** (one plain sentence, below). Under the table a **Total** line: "3 runs, 96k tokens, $0.61" and, when any run is unpriced, "3 runs, 96k tokens, cost partial: 1 model has no price".
2. **A pending state.** While Jev is being asked (up to 20 s, §4.4) the step's row has `picking: true` (a `model.picking` and no `model.picked` yet, §6) and shows "choosing model…" in the Model cell, with the Why cell "asking Jev". It ends in one of the pick states below when `model.picked` arrives; the 20 s budget bounds it.

**Why, in plain words** (one sentence per `by` and `reason`; the codes themselves are only in `--json` and in the detail):

| `by` / `reason` | The row says |
|---|---|
| `jev` | "Picked by Jev: the cheapest model it expects to pass." |
| `constraint` | "Only one model was allowed here (<the rule: your cap, your pin, the step's floor, or the challenger rule>)." |
| `rules`, `no-api-key` | "Jev isn't set up on this machine, so Sindri picked the cheapest model allowed." |
| `rules`, `timeout` | "Jev didn't answer in 20 seconds, so Sindri picked the cheapest model allowed." |
| `rules`, `below-threshold` or `unknown-key` | "Jev wasn't sure, so Sindri picked the cheapest model allowed." |
| `rules`, `jev-input-rejected` | "Jev couldn't take this much input, so Sindri picked the cheapest model allowed." |
| `default`, pick on | "This agent reports no models, so it ran on its own default." |
| `default`, pick off | "Model choice is off, so this ran on the agent's default." |
| `escalatedFrom` set | "Retried on <X> after <Y> was refused" or "... after <Y> failed." Each run's outcome column says one word: `refused`, `failed`, `passed` or `running`. |
| `unusual` | add "Unusual: <Y> failed most of its recent runs on this step." |

**Escalated steps.** An escalated step shows **one row per run**, not one row with two models: "Haiku, refused, 8k tokens, $0.01" then "Sonnet, retried after Haiku was refused, 31k tokens, $0.14", and a step total under both.

**Why this model (detail).** Clicking the Why sentence opens a small panel (a flyout on the row, Esc closes it and returns focus to the sentence): the candidates Jev was shown, the files it saw and their sizes, the files left out and why ("not shown: `big.log`, over the size limit"), and the command that explains it, `judge why <decision id>` (**existing**, `docs/csharp/judge.md`). It shows no file text (§6).

**Escalation rate.** The Runs table's footer and `sindri launch picks` show, per step over the last 20 runs, "retried on a stronger model in 3 of 20 runs". A fresh box has no history, so its first picks are the likeliest to be wrong; the rate is how the owner sees that in the first week.

**Accessibility.** Every state is words, never colour alone: "choosing model…", "refused", "unusual", "cost unknown" are text; the Model cell's accessible name is "Model: Haiku, picked by Jev"; each Runs row is a named row (its accessible name reads step, model, tokens, cost and why, in that order) with its column headers kept for the screen reader, built the way the Workflows list's rows are (named Border rows, not a data-grid control); the flyout is reachable by keyboard (Tab to the Why sentence, Enter opens, Esc closes). **Layering:** the Runs table sits in the instance sheet, where Esc closes the sheet. Esc closes the flyout only and returns focus to the Why sentence that opened it; the next Esc closes the sheet. (J/K are not used: they walk the Workflows page, not the instance sheet.) Enter on a Why sentence opens the flyout and Enter on its close button closes it and the "choosing model…" change is announced once through a live region like the Agents page's counts. Text-fit and accessible-name canaries are part of M-8 as in the other UI slices.

## 7. History and escalation

### 7.1 Escalation

When a step on a model that wasn't the strongest candidate fails its check, or its output is refused, the engine retries it once on a stronger model and records the new pick with `escalatedFrom` set. "Fails its check" is the step's own failure outcome the engine already has (`step.failed`, a refused report: `ReportRefusals`); this spec adds no new kind of failure. **The retry loop is in the workflow, between Close and the decision.** Today a failed run goes from `Close` (`RecipeWorkflow.AgentClose.cs:32`, which returns `AgentClosed(Tokens, EmptyDiff)`) straight to the decision with the failure. This spec adds one branch there, behind `Workflow.Patched("model-pick-escalation-v1")`, cached in a field like the other patches (`_replyFacts ??= Workflow.Patched(...)`, `RecipeWorkflow.Effects.cs:158`), so an in-flight instance's history replays unchanged: when the run failed or was refused, `Escalation == 0`, the activity's `AgentLaunched.CanEscalate` was true and the step reads no `fact:workspace`, the workflow starts the launch activity again for the same run with `Escalation = 1` and `Previous` (the failed model, `refused` or `check-failed`, the check's name, and `closed.Tokens` as the spend), then watches and closes it as usual. The escalation counter lives in the workflow's open-run record (`OpenRun.Escalation`, §5), so a replay knows the retry was already started. The branch goes where the report is settled: `RecipeWorkflow.Agent.cs` near `:196` and `:206`, where a refused or failed report is decided (the code that today returns `Decided(AgentFailed(...))`). The retry must reset what the first launch set: `_agent` and `_held` (`:109-110`), `_launchedAt`, and `open.Launch` (`:111`, which becomes the `-e1` id), before it launches again. M-6's replay test cancels the instance during the retry and replays the history. The workflow reads no candidate list and no ledger: `CanEscalate` and `Model` come from the activity. The activity removes every model at or below the failed one (§4.1 item 4), keeps the profile's pin and cap and the recipe's floor in force, and asks Jev (or the fallback) among what is left; the fallback gives the cheapest model above the one that failed, which is the next one up.

- **Within pin and cap.** An escalation never goes above the cap or off the pin. When nothing is left (the failed model was the strongest allowed), there is no retry and the step fails as it would have today.
- **One escalation per step run.** If the retry fails too, the step fails as today, on the model that failed last.
- **The tokens are bounded.** The retry may spend at most the larger of twice the failed run's tokens (`closed.Tokens`) and the state's per-round token budget. A refused cheap run spends little, so a cap equal to its spend would stop most retries. Past the cap it is stopped as a budget park (`docs/csharp/sindri-workflow.md` "Loops, budgets and `next`"), answered with the existing `--grant-tokens`. This replaces an earlier idea that the retry could start even with the budget used up.
- **The redraft budget (M3, open).** The default written here: the retry does not count against it. Mechanically it is the same run of the same state in the same loop round, so it doesn't increment the loop counter (`rounds`, `interview`, `challenge` in `recipes/sindri/workflow.scoping.yaml:146`). Its tokens do count toward the state's and the recipe's token budgets, because they were spent; what the default changes is only that the retry may start when the state's own budget has been used up by the failed run, up to the cap above. A step that reads a `fact:workspace` (the phase steps) does not escalate in v1 (§5). §16 asks the owner to decide the counting and the cap together.

### 7.2 History

A ledger projection (`views.db`, rebuilt by **existing** `sindri views rebuild`, `docs/csharp/sindri.md`) `view_model_history`, one row per (recipe id and version, state, model): `runs`, `refused`, `check_failed`, `redraft_caused`, `last_run_at`. It is built from `model.picked`, `launch.started`, the step's completion or failure events and the loop's transitions. "Redraft caused" counts a run whose step completed but whose check sent the instance back to the draft state (the `gaps` transition `check -> draft` in the scoping recipe). It is a projection, so it can be dropped and rebuilt, and nothing reads the ledger rows around it. The activity reads the projection, not the ledger.

`ModelPickInput.history` is that row for each candidate. Only the last 20 runs per (state, model) count, so an old failure fades.

## 8. Recipe and profile keys

**Recipe: `floor`.** An active state may say `model: { floor: strong }`. A floor is a tier, not a model id, so a recipe doesn't name a model that disappears: `cheap`, `standard`, `strong` (the bands of §3). No `floor` means no floor. The draft state of the scoping recipe gets `floor: strong`; `questions`, `apply` and `challenge-log` get none; `challenge` gets `floor: standard`. These values are the starting point and the owner can move them. Adding a key to a recipe changes its content, so each recipe that gains one bumps its version (`RCP-VERSION-001`, `docs/csharp/sindri-workflow.md`; the CI check `scripts/ci/recipe-versions.sh`), and the recipe's eval cases are re-run (§12).

**Profile** (`workflow.agent`, today `target` only, `docs/csharp/sindri-profile.md` line 58). New keys, each a re-approval like the existing ones:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `workflow.agent.models` | object: agent id to `{ list: string[], default: string }` | none | Overrides discovery (§3): the models of that agent in cheapest-first order. For Claude Code this is where a new alias goes |
| `workflow.agent.pin` | object: agent id or `agent/stateId` to a model id | none | Every step of that agent (or that state) runs on that model; no Jev call |
| `workflow.agent.cap` | object: agent id to a model id | none | No pick above this model |
| `workflow.agent.pick` | `on` or `off` | absent = `off` until the owner has approved a profile whose summary showed the Jev notice (below); an explicit `on` in an approved profile turns it on | `off` is the privacy switch and the old behaviour: nothing is picked and nothing is sent to Jev; every step runs on the agent's default (today's behaviour), and a `pin`, `cap` or recipe `floor` still applies only when it names a model to pass as `--model`. The cheapest-candidate fallback (§4.4) is used only when the pick is `on` and Jev can't decide |

A model id takes the id pattern only, never a catalog check, like the existing agent keys (`docs/csharp/sindri-profile.md` line 236). **A `pin` key of the form `agent/stateId` is checked against the state ids of the embedded and approved recipes at `profile validate` and `profile approve`**; an id that no recipe has is `SND-MODEL-001` with the nearest state id ("no state `question` in any recipe; did you mean `questions`?"). The agent ids in `models`, `pin`, `cap` are checked against the launcher catalog at `profile validate` and `profile approve` on the box (the catalog is per box), and an id that isn't in it is `SND-MODEL-001` with the nearest id, for the same reason. M-1 adds the recipe access the validator lacks (it is pattern-only today) and the keys' entries in `ProfileDiff.Runtimes` (`src/Sindri/Aw.Sindri.Core/Profile/ProfileDiff.cs:200`, where the keys the approval summary lists are written by hand). Without that check a typo would never match and silently do nothing. `sindri profile validate` (**existing**, `docs/csharp/sindri.md`) also prints, for each pin and cap, what it matches: `pin claude-code/questions = haiku: matches workflow.scoping state questions, workflow.design state questions`. `scripts/check-profile-doc.mjs` needs every nested key as its own backticked name in the doc (see the code index spec CI-0b for the same trap).

**Consent.** An absent `workflow.agent.pick` means off. It becomes on only when the owner approves a profile that says `pick: on` and whose approval summary carried the notice below, recorded the way setup consent is: `ApproveProfileService` appends `setup.consent.given` when a profile is approved (`src/Sindri/Aw.Sindri.Features/ApproveProfile/ApproveProfileService.cs:79`, event `EventTypes.SetupConsentGiven`, payload `SetupConsentGiven(CommandId, Steps, ProfileHash)`, key `setup.consent.given:<profileHash>`), and `McpEvents.ConsentGiven` (`src/Sindri/Aw.Sindri.Core/Setup/McpEvents.cs:41`) reads the newest one and checks it names that profile and the step. M-5 adds a step name `model-pick` to the `Steps` the approval records, only when the approved profile has `pick: on`, and a `PickConsent.Given(profile)` read beside `McpEvents.ConsentGiven`; the activity treats the pick as on only when the approved profile says `on` **and** that read is true for the approved profile's hash. An approval that did not show the notice (an older Sindri) records no such step, so the pick stays off. A profile change re-approves, which records consent for the new hash.

**Approval.** `profile approve` lists the paths that changed (existing behaviour), so a change to `workflow.agent.pick`, `pin`, `cap` or `models` is named in the approval summary like any other key. When `pick` is `on`, the summary also carries one line, added once and not as a separate prompt: "Model choice is on: each agent step's brief and input files (up to 100,000 characters, secrets and denied paths removed), and for the concept scan its map summary, are sent to Jev to pick a model."

**Plain labels in the app.** The profile keys are the machine names; the Settings page text never shows them as is.

| Key | What the Settings text says |
|---|---|
| `workflow.agent.pick` | "Let Jev choose the model for each step" |
| a recipe `floor` | "Needs a strong model" / "Needs at least a standard model" |
| `workflow.agent.cap` | "Never use anything stronger than" |
| `workflow.agent.pin` | "Always use" |
| `workflow.agent.models` | "Models this agent can use, cheapest first" |

**The toggle in the app.** The Settings page (the preferences page, as `docs/csharp/sindri-ui.md` names it) today holds local preferences (Reduce motion, Text size) and the setup checklist (`docs/csharp/sindri-ui.md` "Settings and reduced motion"); the profile is not a preference and the page does not edit it. So Settings gets one **read-only** line, "Model choice: on" or "off", with the sentence "Jev reads each step's brief and files to pick the cheapest model that works. Change it in the profile; it takes effect once you approve." and a Copy button that copies three lines: the profile file's path, `sindri profile validate` and `sindri profile approve` (both **existing**, `docs/csharp/sindri.md`). There is no edit verb and this spec adds none. The line has three states, all in words: **on** ("Model choice: on"); **off** ("Model choice: off. Steps use the agent's default, with no Jev call."); **awaiting approval** ("The profile has a change waiting for your approval"), shown whenever the profile has a pending change, which is the existing whole-profile `profile.needsApproval` flag; there is no per-key value, so the line does not say what changed, and the approve step does. Until the approval the approved value is in force.

**Disclosure.** The disclosure text above is also the Settings line's second sentence and is part of the approval summary. Jev's data-retention and training terms are not stated anywhere the author could find; the owner supplies the link and M-8 adds it to `docs/sindri/` and the Settings text. Until then the text says "retention is the provider's".

**A doctor warning.** `sindri doctor` (**existing**, `docs/csharp/sindri.md`) gets one check, `model:pick` (new, M-5): `ok` when the pick is on (with consent) and `TYPESAFE_API_KEY` is available, `warn` when `pick` is on and Jev is unavailable ("Jev has no API key; steps run on the cheapest model allowed without Jev's choice"), `ok` when the pick is off or has no consent yet, with the note "model choice is off; steps run on the agent's default" (and, when the profile says `on` without consent, "approve the profile to turn it on"), each with its fix. A doctor change updates `src/Sindri/aot-smoke/expected.txt` and runs the smoke in the gate (owner's rule 2026-10-26).

## 9. How #1008 folds in

#1008 (branch `bugfix/gh-1008`) adds `ChallengerPick` (`src/Sindri/Aw.Sindri.Workflows/ChallengerPick.cs` on that branch, constructed over `IAgentCatalog`, `ILaunchViews` and `IApprovedProfile`), a `ChallengerAsk`, and a per-step agent in recipes with `differentFrom`. The author read only that these files exist and `ChallengerPick`'s constructor, not its body. This spec ships after #1008 merges (M8) and changes it in three ways:

1. The per-step choice becomes a pair, (agent, model). `AgentStepTarget` gets the picked model, and `AgentTargetResolver` keeps resolving the agent only.
2. `ChallengerPick` becomes constraint 3 in §4.1: it supplies "the drafter's agent and model" (read from `launch.started`'s `Target` and the new `Model`), and `ModelCandidates` removes that model when the challenger's agent is the drafter's. When #1008 already put the challenger on a different agent, the model constraint is satisfied by construction and is not applied.
3. SCP-CHAL-002 then compares model as well as agent. Today it only warns, because "no run records are kept" for the model; with `launch.started.Model` the check can pass or fail on evidence.

Until #1008 merges, M-1 to M-6 don't depend on it (they use a fake per-step target); only M-7 does.

## 10. Sharing the question with #1172

The concept scan (`ScanConceptsService`, `src/Context/Aw.Context.Features/ScanConcepts/ScanConceptsService.cs:78`) calls `IModelPort.Ask(new ModelRequest(...))`; `ModelRequest` (`src/Context/Aw.Context.Core/Models/ModelRequest.cs`) has no model field and `JudgeModelPort` (`src/Sindri/Aw.Sindri.Host/Models/JudgeModelPort.cs`) walks the agent CLIs, each answering on its default model. #1172 wants a size-based pick there.

This spec gives #1172 two pieces and leaves its call site to it:

- `ModelRequest` gets an optional `Model` (string, null means the CLI's own default). `JudgeModelPort` passes it to the agent CLI provider as the CLI's model flag; a provider that can't take it falls back to its default and logs that (#1172's own wording).
- The pick question: #1172 can call `model-pick` with `kind: concept-scan`, its map summary as `files` and its own candidates. Same objective (M2), same chain (M5) and the same filters (§4.3) apply to it.

`JudgeModelPort`'s doc comment ("never to the remote 'jev' API, because the prompt names files of the owner's repo") is the rule the owner relaxed in q123 (2026-10-26), for the pick question only. Phase M-2 rewrites that comment and the matching text in `docs/csharp/judge.md`.

Which text goes to Jev: `JudgeModelPort`'s own calls (the concept scan's actual answers, which carry the repo's labelled text) still go to the agent CLIs, never to Jev. For #1172's pick, the **map summary** (the feature counts and names, the sizes) goes to Jev as `files`, through §4.3's filters. The scan's prompt, which is what carries the repo's text, does not. So #1172's pick is a smaller disclosure than a step's pick, and it needs the same notice (§8).

## 11. Privacy

Jev (`POST https://api.typesafe.ai/v1/systemone`, `docs/csharp/judge.md` "Jev") is a third party. The owner accepted (q123, 2026-10-26) that a step's brief and input pack go to it. What limits that:

- the filters of §4.3: `index.denyPaths`, the built-in secret file names and the secret scan, applied to the pack, the facts and the brief's file text;
- redaction on every string (`Aw.Redaction`) before it leaves the box, same as every judge input;
- the cap per pick (§4.3, M-0);
- the untrusted-text fence on all repo text;
- **consent first:** the pick is off until the owner has approved a profile whose approval summary showed the Jev notice (§8, "Consent"), so a profile approved before this ships sends nothing; once on, the pick runs only when `TYPESAFE_API_KEY` is available; without a key Jev is unavailable and the rules fallback runs, with no network call; with `pick: off` nothing is picked at all;
- **disclosure:** the `profile approve` summary line and the Settings text (§8) say what is sent and the cap; `model.picked.contentSent` records, per pick, whether any text was sent;
- **no file text is kept** (§6): `model.picked` and the stored judge decision hold the brief's hash, the candidates, the file list with sizes and the omitted list. The judge's usual 30-day redacted copy is replaced for this question by that digest;
- Jev's own retention and training terms are unknown to the author (§8, "Disclosure").

A machine that must never send content to a third party leaves `workflow.agent.pick` absent or sets it `off`. **Accepted:** file paths (the `src/...` names of files Jev sees) go to Jev with the pack, and the first picks on a fresh box (no history, §7.2) are the likeliest to be wrong; the escalation (§7.1) and the escalation-rate figure (§6.1) are what catch and show that.

## 12. Errors and tests

### Errors

| Code | When | Kind |
|---|---|---|
| `SND-MODEL-001` | A profile model key (`models`, `pin`, `cap`) is malformed; a `pin` or `cap` names a model outside that agent's `models` list when the list is given; a state-scoped `pin` names a state no recipe has (§8) | Invalid |
| `SND-MODEL-002` | A recipe `floor` isn't `cheap`, `standard` or `strong` | Invalid (at `recipe validate`) |
| `SND-MODEL-003` | No candidate is left after the constraints (§4.1), or the challenger's agent is the drafter's and the drafter's model is unknown | Refused (the step parks) |
| `SND-MODEL-004` | The agent's model list couldn't be read at the probe. Shown as `no-model-list` in `sindri launch show`; never fails a launch | Informational |
| `AW-JUDGE-011` | `model-pick` input is invalid (no candidates, more than 32, a duplicate key) | Invalid |

`SND-MODEL-003`, in full. Message: **"No model is allowed for step `challenge` on claude-code."** Detail, built from what the constraints removed, for example: "The step needs at least a standard model (its floor), your cap allows nothing above sonnet, and the drafter already ran on sonnet. That leaves no model." Fix: "Raise `workflow.agent.cap`, lower the step's floor, or give the agent more models in `workflow.agent.models`; then approve the profile and run the step again with `sindri workflow answer <address> --resume`." The park shows the same text on the step's row in the app. (`--resume` is **existing**: `docs/csharp/sindri.md:704` documents it for parks of the `unavailable` kind; `--resume` is documented only for `unavailable` parks, so M-5 has a named test, `SND_MODEL_003_park_resumes_after_the_profile_is_fixed`, that parks a step with SND-MODEL-003, fixes the cap, approves, and resumes it.)

The judge's error numbers `AW-JUDGE-005`, `007` and `010` exist; `011` is the next free in `docs/csharp/judge.md`. `SND-MODEL-*` is a new range; grep for `SND-MODEL` found nothing on main, and the builder confirms again. The new codes go in the error table of `docs/csharp/sindri.md` and `AW-JUDGE-011` in `docs/csharp/judge.md`.

### Test plan

- **Callers of the launch id** (engine tests): an escalated run is watched and closed under its `-e1` id; `InstanceViews` and `WorkflowEventDetail` show it; a history recorded before this change replays (the `Patched` branch is not taken).
- **Constraints** (unit, pure): a table of cases for `ModelCandidates.For`: floor removes the cheap ones; cap removes the strong ones; pin leaves one; challenger removes the drafter's model; a null drafter model with the same agent is SND-MODEL-003; an escalation removes the failed model and everything below; the four together leaving none is SND-MODEL-003; an unordered model is placed last and never "cheaper"; the bands for N = 1, 2, 3, 7.
- **Discovery** (unit, with a fake process runner): the Codex JSON fixture (the six slugs, only `visibility: list` kept), a timeout, malformed JSON and a missing binary each leave `Models` empty with `no-model-list`; Claude's list comes from the catalog entry and the profile override; `launch show` prints each model's tier after an override.
- **The question** (judge unit lane): `ModelPickQuestion` input validation; the prompt carries the objective line, the fenced files, the omitted-files line when over the cap (a fixture of 130,000 characters with `src/` first: the `src/` files are kept, the tail omitted and named); a 130,000-character input sent on stdin works end to end; the answer maps `m2` to the second candidate; an unknown key is errored; the stored decision holds no file text; the seeded secrets (prose, `.env.local`, an odd token, a fact) never reach a fake Jev and the dropped files are named in the omitted line.
- **The chain** (judge): with no key, the rules fallback decides and no agent CLI provider is called (a fake agent CLI that records calls asserts zero); a user `config.json` that lists an agent CLI for `model-pick` is ignored; `--providers` can only narrow it.
- **The pick** (Sindri engine tests, `Aw.Sindri.Engine.Tests`): the launch activity with a fake Jev picks `haiku` for `questions`; the request that reaches the fake launcher carries `--model haiku`; a second call of the activity with the same launch id reads the stored `model.picked` and does not call the fake Jev (M7); a fake Jev answer of an unknown key goes to the fallback; the fallback with no key picks the cheapest model allowed.
- **Escalation**: a fake launcher whose first run is refused; the second run has a different launch id (`LaunchIdFor(run, 1)`), is on a stronger model inside pin and cap, `escalatedFrom` is set, there are two `model.picked` and two `launch.outcome` rows, the loop counter is unchanged (M3's default), the retry stops at the failed run's spend, none-left means the step fails as today, and a second failure fails the step. An existing launch id (escalation 0) is byte for byte what it was.
- **History projection**: events in, `view_model_history` rows out; `views rebuild` gives the same rows; `unusual` is set past 50% failures in the last 20.
- **`sindri launch picks`**: text and `--json` over a fixture ledger; exit 1 with `SND-API-001` when not running; never starts the control plane.
- **Recipe eval**: a case file for `workflow.scoping` where `questions` and `apply` launch on a cheaper model than `draft`, a failed cheap run escalates once and the walk continues, and the challenger's model differs from the drafter's (`recipes/sindri/evals/workflow.scoping/`, beside `challenge-spent-same-model-stops.jsonl`).
- **Profile and recipe**: `profile validate` accepts the new keys and rejects a bad one with its key path, rejects a `pin` on an unknown state with the nearest id, and prints what each pin matches; `recipe validate` rejects a bad `floor`; the profile doc checker passes.
- **The route** (M-5): the `LaunchPicks` golden, the fake and its contract, a `picking: true` row before `model.picked`.
- **UI** (M-8): the Runs table's columns, the "choosing model…" state, every Why sentence of §6.1, an escalated step's two rows and total, "cost unknown" with a partial total, the Settings line's three states, the accessible names, the text-fit canary; UI frames saved like the other slices.
- **Doctor and aot-smoke**: the `model:pick` check's three states; `src/Sindri/aot-smoke/expected.txt` updated and the smoke run in M-5's gate.
- **M-2's PR body** records which cap shipped (100,000 or the 16,000 fallback of §4.3).
- **Gates**: phases that add event types, catalog rows or API goldens run `Aw.Sindri.Ui.Tests` in their own gate (owner's rule).

## 13. Size

The numbers are rough and mean the lines of code and tests together.

| Phase | Rough size |
|---|---|
| M-0 | tiny: a spike script, a fixture and a PR body, about 100 lines |
| M-1 | medium: about 500 lines, with the fake model catalog |
| M-2 | medium to large: about 800 lines (question, filters, stored form, per-question cap, stdin test) |
| M-3 | medium: about 500 lines |
| M-4 | medium: about 400 lines |
| M-5 | large: about 900 lines (pick in the activity, event, key, `launch picks`, doctor check) |
| M-6 | medium: about 500 lines (escalation, launch id, token cap) |
| M-7 | small: about 300 lines |
| M-8 | large: about 800 lines (the UI) |
| M-9 | small: about 250 lines, docs and recipe keys |

## 14. Phases and the parallelization plan (for /specToProvenPR)

Each phase is one PR, layered commits, red first (`/new-slice`: test, request and response, service stub, then the real thing). Builders run on Sonnet or Haiku, never Opus; builders do one light build of the project they touched; the lane lead runs the gates (owner's standing rules).

| Phase | Unit | Depends on | Observable signal |
|---|---|---|---|
| **M-0** | The size spike (§4.3): one real Jev call on a 100,000-character synthetic fixture, with the answer-the-key check; the measured limit amends §1 M1 and §4.3. A throwaway script, not shipped; the PR is the spec amendment plus the fixture | none | the PR body shows the status, time and whether the answer was right; §4.3 carries the measured cap |
| **M-1** | Pure Core: `ModelFacts`, `ModelTier` and the band computation, `ModelCandidates.For` and its table of cases, **a fake model catalog (`FakeModelCatalog` in `Aw.Sindri.Testing`) so later phases build without discovery**, `ModelPicked` and the `LaunchStarted.Model` field, `AgentLaunched.Model`, `AgentLaunchRun.Previous` and `.Escalation`, the profile keys' records and validation (`SND-MODEL-001`, the state-id check), `docs/csharp/sindri-profile.md` lines, the recipe `floor` key and `SND-MODEL-002` in the recipe validator, the validator's recipe and catalog access for the state-id and agent-id checks, `ProfileDiff.Runtimes` entries, `Escalation` on `AgentStepRun` and `OpenRun` with the seven `LaunchIdFor` callers | none | unit-lane tables; `sindri profile validate` (**existing**) accepts and rejects the new keys and prints what each pin matches; `sindri recipe validate` (**existing**) rejects a bad `floor` |
| **M-2** | The judge question: `ModelPickInput`, `ModelPickQuestion` (chain fixed to Jev then rules, threshold, budget), the stored form (digest, no file text), the pack filters (denyPaths, secret file names, secret scan, applied to files, facts and brief text), the files-shaped cap helper beside `CappedDiff`, Jev's input cap made per question (the number M-0 measured), `QuestionEvaluator.ModelPick.cs` and a rules provider answering the cheapest key, the sample input fixture, the stdin 130,000-character test, `AW-JUDGE-011`, `JudgeModelPort`'s comment and `docs/csharp/judge.md`, `ModelRequest.Model` and `JudgeModelPort` passing it (the #1172 piece) | M-0 for the cap number (the rest can start at once) | judge unit and contract tests; `judge model-pick < tests/fixtures/judge/model-pick-input.json` (**new** question on the **existing** `judge <question>` form) prints a key |
| **M-3** | Discovery: `TargetFacts.Models`, the Codex reader (`codex debug models`), the Claude catalog entry, the bands and the `unordered` warning, `sindri launch show` printing each model with its tier (the command is documented in `docs/csharp/sindri-launcher.md:132` without a usage line; M-3 adds one to that doc), `SND-MODEL-004`, `launch.probed` carrying them. Its reader can be written against the Codex JSON fixture and a local DTO before M-1 merges | M-1 (the types; the reader itself needs only the fixture) | `sindri launch show` (**existing**) lists each agent's models and tiers on this box |
| **M-4** | History: `view_model_history` projection, its rebuild, a read for the pick, the escalation-rate figure | M-1 | projection tests; `sindri views rebuild` (**existing**) reproduces the rows |
| **M-5** | The pick in the launch activity, first run only: the Sindri-side caller of the judge, the `model.picked` event and its key, the idempotent read-back, `RequestFor` and `EvaluatorRequestFor` taking the model, the rules fallback, `sindri launch picks [--item X] [--json]` (**new**) and the route behind it (`GET /sindri/launch/picks`, `ISindriApi.LaunchPicks`, records, JSON context, golden, fake), the `model.picking` event, the `pick: off` path, the `model:pick` doctor check and the aot-smoke update; `launch picks` gets its `CommandTable` entry (the table that maps a verb to its handler; the builder locates it) and is read-only, so it is not added to `Interrupt.cs`'s streaming and job-following lists (`src/Sindri/Aw.Sindri.Host/Commands/Interrupt.cs:50`, which the builder confirms) | M-1, M-2 and M-3 **merged** (the activity reads the probe's model list, which M-3 serves; the fake catalog lets it start earlier, not merge earlier); M-4 for history, or a stub that returns no history lets M-5 merge before M-4 | **In the running app:** `sindri launch picks` shows a model on every agent step of a scoping run; `questions` and `apply` below `draft`'s; with no Jev key the same holds by the rules fallback |
| **M-6** | Escalation: the workflow's retry branch behind `Workflow.Patched`, `CanEscalate`, the workflow passing `Previous`, the candidate cut above the failed model, the token cap (the larger of 2 x spend and the per-round budget), none-left behaviour, no escalation for `fact:workspace` steps, the M3 default | M-5 | **In the running app:** an induced refusal on a cheap model shows two `model.picked` rows (`sindri launch picks`) and the run goes on |
| **M-7** | #1008 fold-in: `ChallengerPick` supplies constraint 3, the per-step (agent, model) pair, SCP-CHAL-002 compares the recorded models | M-5 and #1008 merged | **In the running app:** SCP-CHAL-002 passes on recorded models in a scoping packet |
| **M-8** | The words and views (§6.1, §8), drawing what M-5's route serves and adding no API: the Runs table, the Why sentences and panel, "choosing model…", escalated rows, "cost unknown", the Settings line and its states, the approval summary line, the disclosure text; its gate runs `Aw.Sindri.Ui.Tests`. Includes `scripts/sindri-ui-fixture.sh` states for a picked, an escalated and an unpriced run | M-5 (the escalated rows render fully after M-6) | **In the running app** (a scratch home with the fixture): the screens above, with text in every state |
| **M-9** | Recipe keys and proof: `floor` on `workflow.scoping.yaml` (version bump) and its eval cases (the challenger case needs M-7), the other agent recipes' floors, docs (a short "Writing a recipe: floors" section in `docs/csharp/sindri-workflow.md` telling a recipe author when to set a floor, then `docs/csharp/sindri-workflow.md`, `docs/csharp/sindri.md` error table, `docs/csharp/sindri-launcher.md`, `docs/csharp/sindri-ui.md`, `docs/sindri/` disclosure), an `owner-check` step | M-6, M-7, M-8 | the Done-when run in §15 |

### 14.1 Parallelization plan

Four lanes, builders always busy.

| Wave | Lane A | Lane B | Lane C | Lane D |
|---|---|---|---|---|
| 0 | **M-1** Core records, constraints, keys, fake catalog | **M-0** the size spike (needs the owner's key; the owner or lead runs it), then **M-2** (its question, filters and stored form start at once; only the cap number waits for M-0) | **M-3** discovery (the Codex reader and Claude catalog against the fixture; wires to M-1's types when they open) | free: the Cursor investigation (O2), reviews |
| 1 | **M-5** the pick in the launch activity (starts on M-1's fake catalog and M-2 when open, on a fake Jev and a stub history) | **M-4** history projection | **M-8** the UI starts on the `Aw.Sindri.Testing` fixtures and the record shapes M-1 fixed | free |
| 2 | **M-6** escalation (stacks on M-5) | **M-7** the #1008 fold-in (needs #1008 merged and M-5) | **M-8** continues (stacked on M-5; its escalated rows after M-6) | free |
| 3 | **M-9** recipe keys, eval cases and proof | docs | free | free |

What can run in parallel: M-0 and M-1 share nothing. M-2 (the judge) and M-1 (Sindri Core) touch different projects. M-3 needs only a fixture to start. M-4 and M-8's start need only M-1's records. M-6 and M-7 are independent of each other once M-5 merges.

What is sequential: M-2's cap number waits for M-0. M-5 needs M-1, M-2 and M-3 to be open or merged. M-6 stacks on M-5. M-7 needs M-5 and #1008. M-9's eval cases need M-7 (one case checks the challenger). No stack is deeper than three open PRs.

If #1008 is late, M-0 to M-6, M-8 and most of M-9 still ship, and the challenger constraint lands with M-7.

## 15. Done when

From the issue: a scoping run shows per-step model picks on its launch events, with at least `questions` and `apply` on a cheaper model than `draft`, the challenger on a different model from the drafter, and per-step tokens and cost visible from Sindri's own records.

Concretely, on a scratch home (the proof is in the PR body):
1. `sindri launch show` lists the models each installed agent offers, with tiers.
2. A scoping run on a fixture item ends with `launch.started` rows whose `model` is set; the `questions` and `apply` rows name a cheaper model than `draft`'s (`sindri launch picks` shows it).
3. The `challenge` row's model differs from `draft`'s and SCP-CHAL-002 passes on that evidence.
4. An induced refusal on a cheap model shows two `model.picked` rows, the second with `escalatedFrom`, and the run goes on.
5. **With no `TYPESAFE_API_KEY`**, the same run picks by the rules fallback, calls no agent CLI for the pick, says why on `model.picked`, **and `questions` and `apply` still run on a model below `draft`'s** (the fallback is the cheapest allowed, not the default).
6. `workflow.agent.pick: off` (or absent, with no consent yet) runs every step on the agent's default with no Jev call and no `--model` flag (today's behaviour), and `sindri doctor` says model choice is off.
7. The app shows, for a finished scoping run, each step's model, tokens, cost and Why sentence, and the total.
8. **The cost rule.** `sindri launch picks` shows Jev's calls and milliseconds per pick. The PR body compares the tokens saved on the cheaper steps (priced with `Budgets.PriceOf`) with the cost of the Jev calls; if Jev's calls cost more than they save on the fixture run, the default stays off (the key stays absent in the shipped profile defaults and the owner is told in the PR), and that is reported, not hidden.

## 16. Open questions for the owner

1. **Does the one escalation retry count against the recipe's redraft budget, and how much may it spend? (M3, the one open decision.)** This spec says: the retry does not use a round, and it may spend at most the larger of twice the failed run's tokens and the state's per-round token budget. If a cheap model fails `questions` and the retry on the next model up used a round, a bad cheap pick would cost the recipe a round of its cap (`interview` is capped at 6, `challenge` at 3), and a recipe near its cap would fail after a pick the owner never made. The cost of "no round": a step that keeps failing on every model gets one extra run per round. The cost of the spend cap: a retry that needs more than that is stopped as a budget park, and you answer it with `--grant-tokens`; a cap of exactly the failed run's spend would stop most retries, because a refused cheap run spends little. Two answers: does a retry use a round, and is the cap "the larger of twice the failed spend and the per-round budget" (or another number)?
2. **One tightening of your wording, for a nod.** You said the rules fallback is "the step's floor model, else the profile default". `questions` and `apply` have no floor and the profile default is Opus, so with Jev unavailable they would run on Opus and nothing would be saved. This spec makes the fallback the cheapest model left after the constraints (the floor model when there is a floor). The cost: with Jev down, a step with no floor can run on the cheapest model, and the escalation retry (§7.1) is what catches a bad result. OK, or keep your wording?

Open items that are not the owner's call, to be settled by the builder:

- **O2: Cursor.** `cursor-agent` isn't on this box, so its model flag and any list verb are unverified (the catalog says "reserved until it is verified"). Until a box with Cursor confirms both, Cursor offers no models to the pick and its steps run on its default. Lane D's free slot in wave 0 is for this.
- **O3: the Claude list.** Claude Code has no list command, so the aliases live in a catalog file. If a model with an id that isn't an alias should be offered before the file is updated, the profile's `models` list is the way.
- **O4: one claim to check at build.** The spec says Temporal doesn't re-run a completed activity on replay and that an activity retry re-runs `AgentLaunching.Launch`. Both are standard Temporal behavior and the existing code comment says the launcher is "idempotent per launch id, so a retry or a replay starts no second session", but this spec's author didn't run it. M-5's test for M7 is what proves it.
- **O5: Jev's size limit.** Settled by M-0 (§4.3), not a build-time guess.
- **O6: Jev's retention and training terms.** Not stated in the repo; the owner supplies the link (§8).
- **O7: view and flag names in M-8.** The author named the instance frame's expanded state, the Agents row and the Settings page from `docs/csharp/sindri-ui.md`, not from the view files; the UI builder confirms the exact view classes.

## 17. Review history

The spec went through three /autoplan rounds (product, architecture, design, devex and security lenses). The consolidated tables, with every finding id and its fix, are:

- **Round 1** (14 HIGH): the fallback would have run Opus on steps with no floor, the 100,000-character size to Jev was unverified, the escalation reused the failed run's launch id, nothing printed why a step got its model, and the UI was one sentence. Fixed with the cheapest-candidate fallback, Phase M-0, its own escalation launch id, `sindri launch picks`, the §6.1 and §8 UI text. `/home/thor/.agentic-workflow/joi-fairshare-agentic-workflow/plans/1206-model-pick/r1/consolidated-review.md`
- **Round 2** (7 HIGH): five more `LaunchIdFor` callers, the workflow-side retry loop, an escalation cap that would park most retries, "choosing model…" with no served state, no API route for picks, per-key "awaiting approval", and a `retry` answer that does not exist. Fixed with `Escalation` on `AgentStepRun` and `OpenRun`, the patched retry branch, the 2 x spend cap, `model.picking`, `GET /sindri/launch/picks` and `--resume`. `/home/thor/.agentic-workflow/joi-fairshare-agentic-workflow/plans/1206-model-pick/r2/consolidated-review.md`
- **Round 3** (no HIGH, PASS): the pick defaulted on for profiles approved before the notice existed, the retry's reset points, J/K in the instance sheet, the read-only frame, the per-question Jev cap path, `--resume` for this park. Fixed with consent-first (`setup.consent.given`), the named reset points, Esc-only layering, the Runs table's host, the `InputCapChars` request field and a named resume test. `/home/thor/.agentic-workflow/joi-fairshare-agentic-workflow/plans/1206-model-pick/r3/consolidated-review.md`
