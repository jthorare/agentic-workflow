---
name: judge
description: Tune, inspect, and undo cheap-agent-harness `judge` decisions in-session — config get/set, why, undo, health.
---

# /judge

`judge` is the cheap-decision layer (rules -> per-content-class model chain) described in
`docs/superpowers/specs/2026-09-26-cheap-agent-harness-design.md`. This skill is the in-session
way to tune it without leaving the conversation.

## Commands this skill wraps

- `judge health` — status (`ok`/`degraded`) and failures in the last 24h.
- `judge why <id>` — the full stored decision: question, content class, provider, decision,
  confidence, reason code, latency.
- `judge undo <id>` — marks a decision undone. Undos count as errors; a rising error rate for one question is a strong signal to raise its threshold or
  disable it.
- `judge approve <id>` — clears an escalated decision (prefixes its `reason_code` with
  `approved:`) so it's visibly resolved when reviewed later.
- `judge config get` — prints the live config (`~/.agentic-workflow/judge/config.json`),
  merged over defaults.
- `judge config set <question> enabled <true|false>` — turn a question on or off.
- `judge config set <question> threshold <0-1>` — raise or lower the confidence bar below
  which `judge` escalates instead of deciding.

## Questions

`wake-gate`, `ui-element-repair`, `visual-critique` (used by `/ui-evidence`), `brief-scope`,
`rule-check`, `ask-check`, and `resolution-check` — `/bugFixOrchestrator`'s second opinion: given the
ticket brief verbatim, the root cause, and a check that failed before and passes after the fix, is the
reported problem resolved (`resolved | partial | unresolved`, threshold 0.8)? A failed after-run or a
check that never reproduced the bug is `unresolved` without a model call; an empty brief escalates.

## Labels (automatic)

No one labels data by hand. Labels come from what happened next and from a stronger model:

1. `judge label import [--question q] [--since 14d]` turns stored decisions into eval items.
   Not in this build yet: it prints `judge: label is not in this build yet` and exits 1. Do not retry it or look for a workaround; tell the user it lands in a later update.
2. `judge label outcomes` (free) labels `ask-check` items from Joi's next real prompt in the session transcript.
   Not in this build yet: it prints `judge: label is not in this build yet` and exits 1. Do not retry it or look for a workaround; tell the user it lands in a later update.
3. `judge adjudicate <question> --limit 60` (costs Opus tokens: about 3 runs x 3k tokens per item) asks
   `claude -p --model opus --effort high` three times per item and stores a label only on a 2-of-3
   in-enum majority. It never sees the judge's own decision. Run it in the background; labels are
   written per item, so a re-run continues where it stopped.
   Not in this build yet: it prints `judge: adjudicate is not in this build yet` and exits 1. Do not retry it or look for a workaround; tell the user it lands in a later update.
4. `judge label status` shows coverage and how often the outcome and Opus labels agree.
   Not in this build yet: it prints `judge: label is not in this build yet` and exits 1. Do not retry it or look for a workaround; tell the user it lands in a later update.

`judge label set <itemId> <label|skip>` is an optional override; no one is expected to label by hand.
Not in this build yet: it prints `judge: label is not in this build yet` and exits 1. Do not retry it or look for a workaround; tell the user it lands in a later update.

## When to reach for this

- the user sees a `judge` fallback notice (`systemMessage` text starting `judge: "<question>"
  escalated`) and wants to know why: run `judge why <id>` using the id in the notice.
- A decision looks wrong: `judge undo <id>`, then consider `judge config set <question>
  threshold <higher>` if it keeps happening for that question.
- The status line shows `judge ⚠ n failures` or `judge ✗ down`: run `judge health` for detail,
  then check whether it's a CLI provider (`claude-cli`, `codex-cli`, or `cursor-cli` — the host
  agent CLI's small model via subscription, no API key needed) or the Jev provider (needs `TYPESAFE_API_KEY`, unavailable — not a failure — until it's configured).
  Any one of the `claude`, `codex`, or `cursor-agent` CLIs on `PATH` is enough. Order: config
  `providers.agentClis` if set, else claude → codex → cursor with the current host (`AW_PROVIDER`)
  first. `cursor-cli` is slow (~8–13s), so on Cursor-only machines text questions often time out
  to `rules`. Set `providers.agentClis` in `~/.agentic-workflow/judge/config.json` to change the order.
- To restrict or reorder the chain for one question, hand-edit `config.json`: add a per-question
  `providers` list, e.g. `{"questions":{"wake-gate":{"enabled":true,"threshold":0.7,"providers":["jev","rules"]}}}`
  skips the CLIs. Unknown names are dropped. (`judge config set` does not write this field.)
- "Below threshold" is an *undecided* answer, not a failure: it writes no `failures` row, so `judge
  health` does not count it. It stays visible in the decision's `skipped` list (`below_threshold`)
  and in its details (`agreement: "undecided"` when rules settle it). `ask-check` settles undecided
  answers with `ask` and `wake-gate` with `send` (reason `fallback-after-undecided`, provider
  `rules`) without waiting on a slower provider. `resolution-check`, `brief-scope` and `rule-check`
  have no safe default, so they keep walking the chain and escalate.

## Prompt sorter (shadow mode)

`config/hooks/prompt-sort.sh` (`UserPromptSubmit`) sorts every real prompt with ONE batched Jev
request: task? loop? complexity, ambiguity, scope/limits/approach/verification defined, bug report?
UI? research? Each axis uses Jev only when decisive (a yes/no outside 0.4-0.6, a score at confidence
>= 0.4); otherwise a keyword heuristic answers. One `prompt-sort` decision row per prompt, with the
per-axis answers in `prompt_sort_axes`. Skipped without a call: machine text, slash commands,
confirmations.

- `judge prompt-sort why <decision-id>` — the decision, its 11 axes (value, heuristic, source,
  agreed/overrode/undecided) and which scaffolds would have fired.
  Not in this build yet: it prints `judge: prompt-sort why is not in this build yet` and exits 1. Do not retry it or look for a workaround; tell the user it lands in a later update.
- `judge prompt-sort scaffold <brief|bugfix|ui-evidence|plan-first> <on|off>` — a scaffold switch.
  All ship OFF; leave scaffolds off; `promote` is not in this build yet.
- `judge prompt-sort promote` — would say when a scaffold is ready to turn on.
  Not in this build yet: it prints `judge: prompt-sort promote is not in this build yet` and exits 1. Do not retry it or look for a workaround; tell the user it lands in a later update.
- `judge config set prompt-sort enabled false` — turns the sorter off entirely (no Jev call).
- `promptSort` in `config.json`: `enabled`, `budgetMs` (200-1400, default 1000), `cooldownPrompts`
  (default 5), `scaffolds`.

## What this skill does not cover

- Installing or updating `judge` itself — run `./setup.sh` from the toolkit checkout (it installs the `judge` binary and, per provider,
  the hooks through `scripts/install-judge-hooks.sh`).
- Adding a new question module — that's application code in `judge/src/questions/`, following
  the `QuestionModule` interface in `judge/src/question.ts`.
- Wiring `judge wake-gate` into a live hook — that's Plan 4 (rollout step 4), gated on the
  hook-input probe.
