# The eval loop (decisions D10, D12)

`scripts/eval_packet.py` measures one TDD packet after every build and keeps the numbers comparable across revisions
and across TDDs. It captures the **input** (the packet as a schema, plus labeled data) and scores **six outputs**:
the artifact page, the agent-handoff packet, implementation, QA, errors and bugs, and token usage. Everything is
written under `<build>/eval/`; nothing leaves the machine unless `--push` is given.

Run order after an edit: `build_tdd.py` -> `bun run app/build.ts` -> `write_agents.py` -> `eval_packet.py`. The last
step is part of every build and every review (D18; Who runs it and when, below).

Also available during the overlap period: `tdd rev <packet-dir>` (`cli/bin/tdd.ts`) runs SKILL.md step 5 in its
order, `artifact_check` before the eval so the eval scores this run's screenshots (the eval is `tdd eval`; this
script runs instead when a `--push` needs the `braintrust` package that `tdd eval` lacks), with a journal under `build/.rev/` so
a stopped run resumes where it stopped. Without `--push` it runs the eval offline (`--skip linear,aws`) and no child
process receives a key (the list is in SKILL.md and `tdd rev --help`); with `--push` the eval alone receives
`BRAINTRUST_API_KEY`, `LINEAR_API_KEY`, the `AWS_*` variables and, unless `--no-gh`, `GH_TOKEN` and `GITHUB_TOKEN`, and
pushes as above. It does not pass `--with-judge` or `--with-agent-probe`, so `ANTHROPIC_API_KEY` is never passed on.

## Setup (once per machine)

```
python3 -m venv ~/.local/share/tdd-skill/venv
~/.local/share/tdd-skill/venv/bin/python -m pip install braintrust==0.49.3 autoevals==0.4.1 anthropic==2.6.0 boto3
```
Installed 2026-11-08: braintrust 0.49.3, autoevals 0.4.1, anthropic 2.6.0, boto3 1.43.126 (botocore 1.43.126), on
Python 3.13. The script itself is standard library; those packages are imported only when a flag and a key need them.
Keys live in the environment only, never in a file: `BRAINTRUST_API_KEY` (push), `ANTHROPIC_API_KEY` (judge, probe),
`OPENAI_API_KEY` or `BRAINTRUST_API_KEY` (autoevals judge fallback), `LINEAR_API_KEY` (bugs), the AWS default
credential chain plus `AWS_REGION` (bugs).

## Command

```
~/.local/share/tdd-skill/venv/bin/python -I "$T/scripts/eval_packet.py" <packet-dir> \
  [--build <dir>] [--out <dir>] [--push] [--project tdd-skill] [--experiment <stem>] [--base <stem>] \
  [--with-judge] [--with-agent-probe] [--labels <labels.jsonl>] [--session <id>[,<id>]] [--run-tests] [--no-gh] \
  [--aws-log-groups <g1,g2>] [--aws-alarm-prefix <prefix>] [--probe <questions.json>] [--no-shots] [--skip <names>] [--json]
```
`--help` prints every flag. Exit 0 after a run (scores are measurements, not gates), 1 when `--push` fails, 2 on bad
usage. A full local run on the reference packet takes about 17 s (artifact_check with screenshots is most of it).

During the bun port's overlap period the same command also runs as TypeScript: `bun "$T/cli/bin/eval-packet.ts"
<packet-dir> [flags]`, or `tdd eval <packet-dir> [flags]`, with the same flags, files, table and exit codes (proved
equal to the Python's on the real packets, PR 15). Its optional SDKs (`@anthropic-ai/sdk`, `autoevals`, `braintrust`,
`@aws-sdk/client-cloudwatch-logs`, `@aws-sdk/client-cloudwatch`) are not dependencies of the cli package: add one with
`bun add --no-save <pkg>` in `$T/cli` when a flag needs it. Keys come from the environment only, as above, and AWS
credentials only from the `AWS_*` variables (no shared credentials file). Where the Python uses its own interpreter
(lint, pytest), the TypeScript uses `$TDD_PYTHON`, else the venv's python above, else `python3` on PATH. The Python script stays the documented path,
and the first TypeScript `--push` waits for the owner (D10, D18).

Files written to `<build>/eval/`: `input.json`, `output.{artifact,packet,implementation,qa,bugs,tokens}.json`,
`probe.json`, `lint.json`, `shots/` (artifact_check renders and its report), `run.json` (exactly what a push would
send), `history.jsonl` (one appended line per run), and `push.json` after a push.

Every score record has the same shape: `{name, value (0..1 or null), status ok | not run | error, reason, method,
source, metrics{}, detail}`. `method` says how the number was computed. A scorer whose tool, key or data is missing
says `not run: <reason>`; it is never a zero.

## Who runs it and when (D18)

D18 (owner, 2026-11-09): agents score as they build, implement and review; the eval is never a step someone runs
later. Every role runs the command above. The roles differ in flags, in the scorers they answer for, and in `--push`.

| Who | When | Flags | Answers for | `--push` |
|---|---|---|---|---|
| Writer, packet or skill code (edits a TDD source or a file in this skill) | after the last rebuild, before returning | the local run; `--session <id>` when it knows its session id, so output 6 counts its tokens | the scorers its files feed (`app/CONTRACTS.md`, owners table); a source edit moves outputs 1, 2 and 6 | never |
| Writer, implementation (a PR in the code repo that builds part of the design) | before returning the PR | the local run; `--run-tests` only after reading the test command, never where it loads secrets (output 4) | the trailer `TDD: <slug> PR <n>` and a test file in the PR, so `implementation_merged` and `qa_tests_touched` count it once merged; output 6 with `--session` | never |
| Reviewer of record (fresh context) | after reading the diff, on the writer's tree | the writer's flags | a score diff against the writer's run (below), not prose alone | never |
| Integrator (the session that merges) | after merge, on the merged tree | the local run; `--with-judge` and `--with-agent-probe` only when their keys are set (cost notes under Push model) | outputs 3 to 5: a PR counts as implemented only once merged | only when `BRAINTRUST_API_KEY` is in the environment (never in a file) and the owner has said go |
| Scheduled routine (when one exists; `references/ops-loop.md` installs none) | on its schedule, with no packet edit | the local run; never `--run-tests` (unattended) | outputs 3, 4 and 5, which change without a packet edit (merges, Linear issues, alarms); outputs 1 and 2 should not move while `digest` is unchanged, so a move there is a finding | never; it reports, the integrator pushes |

A local scheduled task can run the eval; a cloud routine cannot, because it sees no local file and so no packet
(`references/ops-loop.md`, section 0, What exists today).

**The score diff.** When writer and reviewer use the same build folder, the last two lines of `history.jsonl` are the
writer's run, then the reviewer's:
```
jq -s '.[-2:] as [$a,$b] | {digest_same: ($a.digest == $b.digest),
  moved: [($a.scores + $b.scores | keys[]) as $k | (($a.scores[$k] // {}) + ($b.scores[$k] // {}) | keys[]) as $n
          | {output: $k, scorer: $n, before: $a.scores[$k][$n], after: $b.scores[$k][$n]} | select(.before != .after)],
  not_run_after: $b.not_run}' build/eval/history.jsonl
```
`history.jsonl` keeps only scores with a value, so a scorer that turned `not run` shows `after: null` and is listed in
`not_run_after`. With two build folders, put the writer's line first in a two-line file and run the same jq. The
reviewer returns `moved` as a table (output, scorer, writer, reviewer, delta), whether the digest matched, and any new
not-run names.

## What is captured (input.json)

| Part | Content |
|---|---|
| `packet` | slug, repo slug, packet dir, build dir, key `<repo>/<slug>@rev<rev>` |
| `source` | the markdown source: path, sha256, bytes, words (the compiler's word regex) |
| `meta` | meta.json: title, rev, updated, sha, sha_date, repo_url, status, rev_note, repo_path, project label; meta.json sha256 |
| `files` | per lane present (`diagrams/`, `prs/`, `schema/`, `mockups/`, `research/`, `shots/`, `media/`): path, sha256, bytes |
| `content` | content.json sha256, generated time, template, stats |
| `labels.decisions` | every decision as `{question -> answer}`: id, anchor, section, status line, state, the box's first paragraph as the answer, `label` = owner-ruling (done) / open-proposal / held, date from the status |
| `labels.evidence` | every citation: id `CIT.nnnn`, text, path, line range, section, href, the sha it is pinned to |
| `labels.review_notes` | owner review notes from `--labels` (below), with any line problems |
| `digest` | sha256 over the source, meta and lane hashes: equal digests mean the same input |

## Outputs and scorers

### Output 1, the artifact (`output.artifact.json`)

| Scorer | Value | Source |
|---|---|---|
| page_exists | 1 when `build/tdd.html` exists | file system |
| page_size | 1 when page bytes <= 16 MB | file system |
| artifact_check | `node "$S/scripts/artifact_check.mjs" tdd.html <out>/shots`: exit 0 -> 1, exit 1 -> 0, exit 2 -> not run; metrics renders, failures, warnings | the system-design scripts the skill resolves (`ev_artifact.py` mirrors `sd_root` in `build_tdd.py`; last the vendored copy) |
| check_hydrate | `bun run app/check_hydrate.ts tdd.html` (jsdom hydrate and interactions): exit 0 -> 1, 1 -> 0, 2 -> not run; metrics console errors, warnings, hydration warnings, failed steps | app/ |
| lint | `lint_packet.py <packet> --build <build> --json <out>/lint.json`; value = rules with zero error findings / rules reported; per-rule error and warning counts as metrics | scripts/lint_packet.py |
| refs_linked | linked / (linked + unlinked occurrences); linked = `content.refs.linked`; unlinked occurrences = tokens in `content.refs.unlinked` counted in the compiler's linkable HTML strings outside `<a>` and `<code>` | content.json |
| cites_href | citations with an href / all citations; metrics split out the ones with no line range (no href by design) | content.json |
| sections_house | house sections matched by id or title / 6 (slots from `lint_packet.Lint.SLOTS`, mirrored) | content.json |
| decisions_status | decisions with a status / decisions; done, open, held counts | content.json |
| diagram_hotspots | diagrams with at least one hotspot / diagrams; hotspots with table, note, image | content.json |
| schema_ddl | tables named in DDL (CREATE/ALTER TABLE in sql fences and `schema[].ddl`) that have a schema card / tables named in DDL | content.json |
| reading | metric only: reading minutes, words, sections, diagrams, PRs, decisions | content.json stats |
| judge_* (4) | `--with-judge`: clarity, evidence_grounding, decision_completeness, term_consistency, 0..1 each (below) | model |
| owner_review | pass / (pass + fail + needs-work) over review notes whose target is `artifact` | labels.jsonl |

### Output 2, the packet for implementation and agent handoff (`output.packet.json`)

| Scorer | Value | Source |
|---|---|---|
| agents_present | 1 when `agents/README.md` exists | build/agents |
| agents_current | `write_agents.check(build_dir)` returns no problem: 1, else 0 (problems listed) | write_agents.py |
| readme_bytes | 1 when README.md <= `write_agents.README_MAX` (8192) | build/agents |
| largest_slice | 1 when the largest non-exempt text file <= `write_agents.BUDGET` (8000 tokens as the writer counts them, ceil(chars/4), read from `tokens.json`); exempt = `write_agents.EXEMPT` (whole-document and index files) | build/agents |
| anchors_sliced_once | content.json anchors listed by exactly one `sections/*.md` front matter `anchors:` (or, for anchors no section lists, the existing `refs.json` file such as `schema/schema.md`) / anchors | build/agents |
| sql_extracted | sql fences whose whitespace-normalised text is in `agents/sql/*.sql` / sql fences | build/agents/sql |
| map_edges_resolve | `map.json` edges whose from and to are entity ids / edges | build/agents/map.json |
| images_indexed | images in `content.files.json` with a row (`source`) in `agents/images/index.json` / images | build/agents/images |
| prs_copied | PR numbers whose `prs/pr-NN.md` is in `agents/prs/` / PR numbers | build/agents/prs |
| schema_md | 1 when `agents/schema/schema.md` names every schema table | build/agents |
| lane_size | metric only: files, bytes, tokens (bytes / 3.6) | build/agents |
| probe_answerable | the share of probe questions whose expected answer sits next to its evidence in a file the handoff loader read (no model) | probe |
| probe_agent | `--with-agent-probe`: a model given only the loaded lane answers; matched / questions | model |
| owner_review | as above, target `packet` | labels.jsonl |

### Output 3, implementation (`output.implementation.json`)

The PR plan in content.json (PR number, title, change line) is matched to the repository named by `meta.repo_path`
and `meta.repo_url`, read-only (no fetch, no checkout):
1. merged GitHub PRs whose body carries the TDD trailer (`gh pr list --search '"tdd:<slug>" in:body' --state merged`);
2. else a merged PR whose title is at least 0.85 similar to the planned title (`gh pr list --search "<title> in:title"`);
3. else, without gh, default-branch commits with the trailer (`git log -i -E --grep`) or the title (`git log -i -F --grep`).

| Scorer | Value |
|---|---|
| implementation_merged | merged / planned. Not run when gh cannot read the repo and the local clone does not contain `meta.sha` (a stale clone would undercount; the eval never fetches) |
| review_rounds | metric: per merged PR, distinct reviewed commits (`gh pr view --json reviews`, `reviews[].commit.oid`), else reviews that approve or request changes; mean |
| time_to_merge | metric: days from `meta.updated` (the rev date) to each merge; median and max |
| owner_review | target `implementation` |

Per-PR rows (number, URL, merged date, additions, deletions, files, review rounds, how it matched) are in `data.prs`.

### Output 4, QA (`output.qa.json`)

| Scorer | Value |
|---|---|
| qa_tests_touched | merged PRs whose changed files include a test file (`tests/`, `__tests__/`, `*.test.*`, `*.spec.*`, `test_*.py`, `*_test.py`) / merged PRs with a file list |
| qa_test_command | `--run-tests` only: the repo's declared command (`package.json` scripts `test`, else `test:unit`; else pytest from `pyproject.toml`) at the repo's current HEAD, capped at 120 s: exit 0 -> 1, else 0; over the cap -> `not run: too slow`. Read the command first: on web-app it is `bash scripts/with-secrets.sh --config=local -- ...`, which loads secrets, so never pass `--run-tests` unattended there |
| artifact_check, check_hydrate | copied from output 1 |
| owner_review | target `qa` |

### Output 5, errors and bugs (`output.bugs.json`), a foundation

| Scorer | Value | Runs when |
|---|---|---|
| linear_open_bugs | 1 / (1 + open issues); issues = label `tdd:<slug>`, or the slug in title or description; per issue identifier, title, state, created, URL, and the D-numbers and PRs named on its slug lines | `LINEAR_API_KEY` set (`scripts/eval/sources/linear.py`) |
| aws_errors | 1 / (1 + error events); CloudWatch Logs Insights `filter tdd = "<slug>" and level = "error"`, counted per day over 30 days | boto3, credentials, region, and `--aws-log-groups` or `TDD_EVAL_AWS_LOG_GROUPS` (`scripts/eval/sources/aws.py`) |
| aws_alarms_clear | 1 when no alarm with the TDD's prefix is in ALARM, else 0 | as above, with `--aws-alarm-prefix` or `TDD_EVAL_AWS_ALARM_PREFIX` |
| owner_review | target `bugs` | labels.jsonl |

### Output 6, tokens and efficiency (`output.tokens.json`)

| Record | Content |
|---|---|
| session_tokens | `--session <id>[,<id>]`: input, output, cache read and cache write tokens summed over `message.usage` of assistant records in `~/.claude/projects/*/<id>.jsonl` and `<id>/subagents/*.jsonl`, one per `message.id` (a streamed message is written several times; the last wins); per model; API-equivalent cost |
| eval_calls | the judge and probe calls this run made: tokens from each API response's `usage`, cost (autoevals calls are listed with usage unknown: it does not expose it) |
| build_sizes | bytes of content.json, tdd.html, content.files.json, media, the source; agents lane bytes and tokens; `*_tokens_est` = bytes / 3.6 |
| cost_per_revision | session cost + eval-call cost, and a one-line summary such as `rev 4: $X API-equivalent (...)` |

Prices (`scripts/eval/ev_llm.py`, USD per million tokens) come from the claude-api skill's model table, cached
2026-10-06 (Fable 5.1 $10/$50, Opus 5.5 $4/$20, Sonnet 5.5 $2/$10, Haiku 5.5 $0.10/$0.50, others as listed there), and
its prompt-caching page (cache reads 0.1x input, $0.25 on Fable 5.1, $0.20 on Opus 5.5 and Sonnet 5.5; cache writes
1.25x for the 5-minute TTL, 2x for 1 hour). They are API list prices: a Claude Code subscription is billed
differently, so read the dollar figures as an efficiency index, not a bill. Update the table when prices change.

## The handoff probe

`templates/eval/probe-questions.json` holds fixed question kinds; `scripts/eval/ev_probe.py` fills them from
content.json with a deterministic extractor, so the same content.json always gives the same questions and expected
answers (written to `probe.json`). A packet may override the template with `<packet>/eval/probe-questions.json`.

Kinds: which decision settles a question; whether a decision is decided or open; which tables are new; which change;
which PR first names a new table (derived: the lowest-numbered PR whose text names it); which PR has a given title; how
many PRs; stat-card figures; a new column's SQL type (constraints removed); a measured value from the schema lane;
the commit sha citations are pinned to. On the reference packet: 27 questions.

The loader simulates an agent: it reads `agents/README.md`, then the files it links in order (skipping `## Optional`,
images, external links and `llms-full.txt`), then one more level of links, taking each file whole when it fits the
budget (20,000 tokens, estimated as chars / 3.6), and records what it skipped and why.

- `probe_answerable` (no model): the expected answer must sit next to the question's evidence in one loaded file (same
  line for decided/open, anywhere in the file for table lists, within 600 characters of the evidence otherwise; a
  `PR n` answer also counts on a table row whose number cell or `pr-NN.md` link names n). It is a ceiling for an agent
  holding only the lane: a low value means the lane does not put the answer where the front door leads.
- `probe_agent` (`--with-agent-probe`, `ANTHROPIC_API_KEY`): one `claude-sonnet-5-5` call with the loaded files as
  `<document>` blocks (documents first, questions last) and a JSON-schema output; each answer is matched by its kind
  (`token`, `oneof`, `all`, `numbers`, `contains`, `prefix`, defined in the template).

## The judge

`--with-judge` grades `agents/tdd.md` (or the content.json text when the lane is absent) on four criteria, each 0..1
with a reason: clarity, evidence grounding, decision completeness, consistency of terms (the rubric text is in
`scripts/eval/ev_judge.py`). With `ANTHROPIC_API_KEY`: one `claude-sonnet-5-5` call, `output_config.format` JSON schema,
no sampling parameters, scores clamped to 0..1. Else with `OPENAI_API_KEY` or `BRAINTRUST_API_KEY`: autoevals'
`LLMClassifier`, one call per criterion with choices A..E mapped to 1, 0.75, 0.5, 0.25, 0, on autoevals' default model
(`gpt-5-mini` in autoevals 0.4.1, through the Braintrust gateway unless `OPENAI_BASE_URL` is set). Else not run. The
judge model is fixed (no refusal fallback to another model) so scores stay comparable; a refusal is recorded as an error.

## Owner review notes become labels

Write notes into `<packet>/eval/labels.jsonl` (picked up automatically) or pass `--labels <file>`; the shape is in
`templates/eval/labels.template.jsonl`: `{target, anchor, verdict, note, date, author?}`, target one of the six outputs,
verdict `pass | fail | needs-work | question`. Each note:
1. lands in `input.json` under `labels.review_notes` (bad lines are listed under `review_notes_problems`, not dropped silently);
2. becomes the `expected.review_notes` of that target's Braintrust row and part of the dataset record's `expected`;
3. sets the target's `owner_review` score: pass / (pass + fail + needs-work); question notes are counted, not scored.

Decisions are labels without any extra step: an owner ruling (state done) is a `{question -> answer}` label; an open
one is an `open-proposal` label until the owner answers and the source is rebuilt.

## Tagging convention (how work joins back to a TDD)

The slug is the packet folder name (`<slug>`). Every artifact that should count toward a TDD names it:

| Where | Convention | Read by |
|---|---|---|
| PR body or commit message | a trailer line `TDD: <slug> PR <n>`; optional `TDD-Decision: D4` lines. Also accepted: `tdd:<slug>`, `tdd:<slug>#<n>`, `tdd: <slug> pr-<n>` | implementation, qa |
| PR title | the TDD's PR title, when there is no trailer (similarity >= 0.85) | implementation |
| Branch (optional) | `tdd/<slug>/pr-<n>-<words>` | people |
| Linear issue | label `tdd:<slug>`; in the description a line `TDD: <slug> D4` or `TDD: <slug> PR 3` names the decision or PR. A bare `D4` without the slug is not counted | bugs (linear) |
| Log line | structured JSON fields `"tdd": "<slug>"`, `"level": "error"`; optional `"tdd_pr": 3`, `"tdd_decision": "D4"` | bugs (aws) |
| CloudWatch alarm | name prefix `tdd-<slug>-` | bugs (aws) |
| Feature flag (optional) | name the flag in the TDD's rollout section, so a log or alarm can be traced to it | people |

## Push model (Braintrust)

Only with `--push` and `BRAINTRUST_API_KEY`. Without it, `run.json` holds exactly what would be sent. The first push
waits for the owner's go (lead default under D10): TDD content leaves the machine.

- Project `tdd-skill` (`--project`). Dataset `tdd-inputs`: one record per packet revision, id
  `<repo>/<slug>@rev<rev>` (re-running a revision overwrites its record: `Dataset.insert` with an existing id upserts),
  `input` = input.json, `expected` = the labels, tags `tdd`, slug, `rev<n>`, repo slug.
- Experiments: one per output kind per run, `<slug>-rev<rev>-<kind>` for artifact, packet, implementation, qa, bugs,
  tokens (`--experiment` changes the stem). Each holds one row: `input` = `{packet: <repo>/<slug>, output: <kind>}`,
  `output` = the output file, `scores` = `{name: value}` (null for not run), `metrics` = the numeric metrics,
  `metadata` = packet key, rev, dataset record id, input digest, not-run reasons. The artifact row attaches `tdd.html`
  and up to 12 `*-top.png` renders (artifact_check and `<packet>/shots/`); the packet row attaches `agents/README.md`.
- Base experiment: `<slug>-rev<rev-1>-<kind>` when rev is a number above 1, else `--base <stem>`. The SDK only warns when
  the base does not exist. Without an explicit base, Braintrust picks the most recent experiment in the project, which
  could be another kind; so the base is always passed.
- Git metadata collection is off (`GitMetadataSettings(collect="none")`): the SDK would otherwise read the repo the
  command runs in, which is not the TDD's repo. Provenance is in metadata instead.

Why one experiment per output kind, not one experiment with six scored outputs: Braintrust compares an experiment with
its base row by row, matching rows on `input` by default; each kind has its own score names, so mixing them in one
experiment would give sparse score columns and a summary that blends unrelated regressions. One experiment per kind
gives a clean per-kind comparison against the previous revision, and the stable row input `{packet, output}` lines the
same packet up across revisions. Across TDDs, filter the project's experiment list by the tag of a kind (`artifact`,
`packet`, ...) and compare the score columns.

Cost notes: a local run calls no model. `--with-judge` sends the whole document once (about 36k input tokens for the
reference packet's `agents/tdd.md`, estimated at chars / 3.6) plus about 1k output tokens of JSON (more when the model
thinks; thinking is billed as output): roughly $0.08 at Sonnet 5.5 list prices, an estimate. `--with-agent-probe` sends
at most the 20k-token lane plus the questions: roughly $0.05. Both are recorded exactly from the API's usage in
output.tokens.json when they run. Braintrust's own pricing and plan limits were not checked for this doc.

## Reading a regression between runs

1. Same input? Compare `digest` in `input.json` (and in `history.jsonl`): a different digest means the source, meta or
   a lane file changed; the same digest with a different content.json `generated` means only a rebuild.
2. Locally: `history.jsonl` has one line per run with every score and the not-run list; compare the last two lines
   for the packet (`jq -s '.[-2:]' history.jsonl`). A score that turns `not run` is a tool or key problem, not a
   regression; a score that drops is a regression, and the output file's `detail` names what moved (the lint errors,
   unlinked tokens, dangling map edges, unanswerable probe questions, unindexed images).
3. In Braintrust: each experiment's summary (printed after a push and saved in `push.json`) shows per-score diffs,
   improvements and regressions against the base; open the experiment URL to see the row's output and attachments.
4. Compare like with like: the probe's questions come from content.json, so adding a decision or a PR adds questions;
   check `questions` in the probe metrics before reading `probe_answerable` as better or worse.

## Verified versus from memory (2026-11-08)

Verified:
- PyPI JSON API: braintrust latest 0.49.3 (2026-11-06); 0.3.15 exists but dates from 2025-12-22, so 0.49.3 is pinned;
  autoevals 0.4.1 (2026-10-21); anthropic 2.6.0.
- Installed braintrust 0.49.3 source: `init` and `init_dataset` create the project when absent; `Dataset.insert(input,
  expected, tags, metadata, id, output)` upserts on an existing id; `Experiment.log` requires input, output and a scores
  dict, each score a number in 0..1 or null; `dataset_record_id` is deprecated and unused; `Attachment(data=path or
  bytes, filename, content_type)` is extracted from anywhere in a logged event and uploaded by the SDK;
  `summarize()` flushes and returns `experiment_url`, `comparison_experiment_name` and `comparison`; `Experiment.close`
  is deprecated; a missing `base_experiment` only logs a warning; git metadata is collected by default per org settings.
- docs.braintrust.dev: experiments match test cases across runs on `input` by default; `base_experiment` /
  `base_experiment_id` select the baseline; `summarize()` returns per-score improvements, regressions and diffs;
  `BRAINTRUST_API_KEY` is the API key variable; `Eval(name, data=init_dataset(...), task, scores, experiment_name,
  trial_count)` is the managed alternative (not used: the outputs are computed before logging).
- Installed autoevals 0.4.1 source: `LLMClassifier(name, prompt_template, choice_scores, model=None, use_cot=True, ...)`,
  default model `gpt-5-mini` via `get_default_model()`, key `BRAINTRUST_API_KEY` or `OPENAI_API_KEY`, Braintrust gateway
  base URL by default.
- Installed anthropic 2.6.0: `claude-sonnet-5-5` is in the model list; `messages.create` takes `output_config`; the
  claude-api skill (bundled 2.1.317): JSON-schema output via `output_config.format`, no sampling parameters on Sonnet
  5.5, prices as above.
- linear.app/developers: endpoint `https://api.linear.app/graphql`; personal key in `Authorization` with no Bearer;
  comparators eq / in / contains / containsIgnoreCase; label filter `labels: {name: {...}}`; `or` on every filter.
- botocore 1.43.126 service model: logs StartQuery / GetQueryResults members and statuses; cloudwatch DescribeAlarms
  members and MetricAlarm fields.
- gh 2.102.1: `pr list` / `pr view` JSON fields include reviews, files, mergedAt, additions, deletions, mergeCommit.

From memory or unexercised (marked so in the code):
- The push path ran for the first time on 2026-11-09 with a key from the environment: `init_dataset`, `init`, `log`, `summarize` and `flush` behaved as the SDK docs say (one dataset record, six experiments, URLs in push.json). Known quirk: when the base experiment named by `--base` (or the previous rev) does not exist, the SDK compares to the most recent experiment in the project instead, so a first revision's six experiments compare to one another.
- Braintrust's handling of a reused experiment name (whether the server suffixes it) is not verified; the printed URL
  comes from the server's answer.
- Linear: the `containsIgnoreCase` comparator on `title` and `description`, and Relay pagination (`first`, `after`,
  `pageInfo`), are from memory. The source has not run with a key.
- AWS: Logs Insights field discovery for JSON logs (`tdd`, `level`) is from memory; the source has not run with credentials.
- gh `reviews[].commit.oid` is from memory; review_rounds falls back to counting approving or changes-requested reviews.
- The judge and probe Claude calls have not run with a key; an offline stub bound them to `Messages.create`'s signature.
