# Judge — `judge`

`Aw.Judge` (`src/Judge`, tier 1, four projects) is the C# judge: the cheap-agent harness's decision CLI that the bash
hooks call (send-gate, scope-gate, done-gate, done-gate-annotate, subagent-start-map, judge-health, prompt-sort, trait-check)
and the statusline. It is a reimplementation,
not a translation of the TS judge (`judge/src/**`): the TS package is a guide to what the judge does, never to how.
Its contracts are the C# tests in `tests/Aw.Judge.Tests`, except the **hook contract** below, which the bash hooks
and the statusline read and which stays frozen (cutover spec §10). `./setup.sh` installs the NativeAOT `judge` binary
where the hooks find `judge` on `PATH`; the TS judge is gone and the C# judge starts with fresh state (cutover spec §5.3).

## Package shape

```
Aw.Judge.Host ──► Aw.Judge.Features ──► Aw.Judge.Core ◄── Aw.Judge.Infrastructure
```

| Project | Holds |
|---|---|
| `Aw.Judge.Core` | Typed ids (`Ids`: `DecisionId`, `ToolUseId`, `SessionId`, `PromptId`, `AgentId`), the domain records (`Briefs`, `Decisions`, `Health`, `Config`, `Providers`), the prompt sorter's pure side (`PromptSort`: axes, tier-1 skip, keyword heuristics, the decisive-band blend, the scaffold plan) and the trait-check domain (`Traits`), the nine questions with their input records (`Questions`), the evaluation engine with the question catalog, and the rules provider (`Evaluation`), the error catalogue (`Errors.JudgeErrors`, AW-JUDGE-NNN) and the ports (`Ports`) |
| `Aw.Judge.Features` | One slice per command: `AskQuestion` (every `judge <question>`), `SaveBrief`, `GetBrief`, `MapBriefByName`, `MapBriefByDispatch`, `SetBriefAgentId`, `ExplainDecision` (`why`), `UndoDecision`, `ApproveDecision`, `GetHealth`, `GetConfig`, `SetConfig`, `SortPrompt` (`prompt-sort`), `SetPromptSort` (`prompt-sort scaffold`, `config set prompt-sort`), `CheckTraits` (`trait-check`), `ImportLabels`, `LabelOutcomes`, `SetLabel`, `GetLabelStatus` (`label`), `Adjudicate`, `EvaluateQuestion` (`eval`), `ImportTurns` (`eval import-turns`); `AskQuestion` is decorated with `PruneAfterQuestion` (retention, below) |
| `Aw.Judge.Infrastructure` | The SQLite stores over `decisions.sqlite`, the config file, the redactor adapter over `Aw.Redaction`, the `PATH` executable locator, the process runner, the secret source (environment, then keychain), the model providers and their catalog (`Providers/`), random decision ids, and the settings read from the environment |
| `Aw.Judge.Host` | `Program.cs`, `Composition.Compose`, one command per slice in `Commands/`, the command table (which takes the providers allowlist out of argv first), `JudgeJsonContext` (the package's one source-generated JSON context) |

Core's namespaces form a graph with no cycle (`ARCH-CYCLE-001`): `Errors` and `Ids` are leaves, `Providers`,
`Config`, `Briefs`, `Decisions`, `Health`, `Questions`, `Processes` and `Secrets` sit on them, `Ports` on those, `Evaluation` on `Ports`, and
the root holds only `AddJudgeCore()`.

### Ports

| Port | Adapter (Infrastructure) | Fake and contract (`tests/Aw.Judge.Testing`) |
|---|---|---|
| `IBriefStore` | `SqliteBriefStore` (`briefs` table) | `InMemoryBriefStore`, `BriefStoreContract` |
| `IDecisionLog` | `SqliteDecisionLog` (`decisions`, `decision_details`, `failures`) | `InMemoryDecisionLog`, `DecisionLogContract` |
| `IJudgeConfigStore` | `FileJudgeConfigStore` (`config.json`) | `InMemoryJudgeConfigStore`, `JudgeConfigStoreContract` |
| `IInputRedactor` | `AwInputRedactor` (over `Aw.Redaction`) | `FakeInputRedactor`, `InputRedactorContract` |
| `IExecutableLocator` | `PathExecutableLocator` (`PATH` lookup for the agent CLIs) | `FakeExecutableLocator`, `ExecutableLocatorContract` |
| `IModelProvider` | `ClaudeCliProvider`, `CodexCliProvider`, `CursorCliProvider`, `JevProvider` (Providers below); rules is the engine's own `RulesProvider` | `ScriptedProvider`, `ModelProviderContract` (all five adapters and the fake) |
| `IProviderCatalog` | `ProviderCatalog` (the lineup from `config.json`, `AW_PROVIDER` and `PATH`) | `FakeProviderCatalog`, `ProviderCatalogContract` |
| `IPromptSortStore` | `SqlitePromptSortStore` (`prompt_sort_axes`, `prompt_sort_runs`, migration 0003) | `InMemoryPromptSortStore`, `PromptSortStoreContract` |
| `ISortSessionStore` | `FileSortSessionStore` (`sessions/<sid>.sort.json`) | `InMemorySortSessionStore`, `SortSessionStoreContract` |
| `IBatchClassifier` | `JevBatchClassifier` (one batched Jev call; unavailable, never an error, when Jev is excluded or has no key) | `FakeBatchClassifier`, `BatchClassifierContract` |
| `ITraitStore`, `ITraitFiles`, `ITraitJudge`, `ITraitCheckLog` | `FileTraitStore` (trait files), `FileTraitFiles` (the repo's text files), `JevTraitJudge`, `FileTraitCheckLog` (`trait-check/log.jsonl`) | the fakes in `tests/Aw.Judge.Tests/CheckTraits/` |
| `IEvalStore`, `IEvalFiles` | `SqliteEvalStore` (`eval_items`, `labels`, migration 0004), `FileEvalFiles` (`evals/` reports, `--record` and `--replay` files, 0600) | the fakes and contract bases in `tests/Aw.Judge.Testing` |
| `ITranscriptReader` | the Claude transcripts under `AW_JUDGE_PROJECTS_DIR` | `tests/Aw.Judge.Testing` |
| `IProcessRunner` | `SystemProcessRunner` (`System.Diagnostics.Process`) | `FakeProcessRunner` (runs `FakeProgram`s), `ProcessRunnerContract` |
| `ISecretSource` | `KeychainSecretSource` (the variable, then the macOS keychain) | `FakeSecretSource`, `SecretSourceContract` |

Plus the Kernel's `IIdGenerator` (`RandomIdGenerator`, which passes the Kernel's `IdGeneratorContract` in
`tests/Aw.Judge.Tests/Ids/`; fake `SequentialIdGenerator`) and the BCL's `TimeProvider`.

## JSON

Everything the judge reads or writes as JSON goes through the Kernel `AwJson` profile and `JudgeJsonContext`
(camelCase unless a record names its fields, required and nullable members enforced on read):

- stdin is deserialized into the command's input record (`BriefInput` for `brief save`); any failure, syntax, shape or
  an id past its 4,096-character cap, is exit 1 with one line `invalid input: <reason>`. A question's stdin is
  deserialized by the evaluator into the question's input record (Questions below); any failure, an object that
  repeats a key, an escape that isn't valid UTF-16 (a lone surrogate such as `\ud800`, in a value, an unknown field
  or a key), or a required text that is blank, is the `invalid-input` escalation, recorded like any other (text that
  can't be read and written back as JSON, `InputJson.TryParse`, is stored as one JSON string). The row of an
  escalation that comes after the input validated (`below-threshold`, `no-provider-decided`, `rule-timeout`) stores
  the input record, its declared fields only, as a decided row does; before that (`question-disabled`,
  `invalid-input`) it stores what the caller sent. Either way the stored input is redacted: every string and key is
  scrubbed, and a field whose name says it holds a secret (`IRedactor.IsSecretName`: `client_secret`, `apiKey`,
  `password`) has its whole value replaced, then capped;
- `config.json` is deserialized into `JudgeConfig` and then checked (`JudgeConfigParser`);
- the JSON columns `decisions.skipped` (a list of `{provider, reason}`) and `decision_details.probabilities` (option
  → probability) are the typed values, serialized through the same options.

The one exception is the providers' wire formats (the agent CLIs' output envelopes, an image input's paths, Jev's request
and response): those records are internal to Infrastructure, which the Host's context can't name, so they have their own
internal `ProviderWireJsonContext`, used only through `AwJson.CreateOptions` (`WireJson.Options`). An answer's free
fields (the decision plus the question's extra properties) are read as a `JsonObject` and checked against the question.

## CLI contract

`judge <command> [args]`, or `judge <question> < input.json` for any first word that isn't a command.

**The providers allowlist** is a global flag, `--providers a,b`, accepted anywhere in argv (`judge --providers rules
wake-gate` and `judge wake-gate --providers rules` are the same), else `AW_JUDGE_PROVIDERS` (hooks can't always add
flags); the flag wins over the variable. The table takes the flag out of argv before anything else (`ProvidersFlag`,
Host), so every command line accepts it, and a flag with no value is a usage error for any command. Only a question,
`prompt-sort` and `trait-check` (which ask Jev) read the list (`ProviderNames.ParseList`, Core): names are `rules`, `jev`, `claude-cli`, `codex-cli`, `cursor-cli`,
`rules` is always kept, and an empty list or an unknown name is a usage error there (64, AW-JUDGE-010). The other
commands ignore the list, so a bad `AW_JUDGE_PROVIDERS` never stops `health` or the brief commands the hooks call
(`ProvidersAllowlistCliTests`).

### Exit codes and streams

| Exit | Meaning | Streams |
|---|---|---|
| 0 | Ok | The result on stdout (JSON or a bare id), nothing on stderr |
| 1 | Error: not found, invalid input, an invalid stored config, a store that can't be used | Nothing on stdout; one stderr line, the error's message |
| 2 | Escalated: a question that couldn't decide; for `ask-check`, "continue" (Questions below) | The question's envelope on stdout |
| 64 | Usage: no command, an unknown subcommand or question, a missing or extra argument, an argument that doesn't parse, a providers allowlist the judge can't read | One stderr line starting `usage: judge ` (the synopsis, then the problem in parentheses) |

Usage lines name the command's synopsis, e.g. `usage: judge why <decision-id>`, `usage: judge brief map-by-dispatch
<session-id> <prompt-id> <subagent-type>`, `usage: judge config set <question> enabled true|false | threshold <0-1>
(unknown field 'thresh')`; with no arguments at all, `usage: judge <command> [args] | judge <question> < input.json`
(`CliUsageTests`).

Every stream that has text ends with one `\n`. Log lines (Aw.Kernel.Telemetry JSON lines, `AW_LOG_LEVEL`) also go to
stderr; the hooks discard stderr except where the hook contract says. At the default level a normal run logs nothing
(the AOT smoke runs every command with `AW_LOG_LEVEL` unset and compares stderr); the one warning is an invalid
`config.json` (read on the defaults).

### Commands

| Command | Input | Output (exit 0) | Errors |
|---|---|---|---|
| `health` | — | `HealthOutput`: `{"status": "ok"\|"degraded"\|"down", "failures24h": n}`: ok with no failure row in 24 h, down when the last five model attempts all failed (rows written before the chain, a disabled question, invalid input, a pre-rule settlement or a rule timeout, never reached a model and don't count; the list is `EvaluationReasons.BeforeChain`, which the SQLite query binds), degraded otherwise | store error → 1 |
| `brief save` | stdin `BriefInput`: `toolUseId`, `sessionId`, `promptId`, `dispatchName` (string or null), `subagentType`, `goal`, `acceptanceCriteria`, `proofCommand`, all required, strings may be empty, other keys ignored; the three texts are stored redacted, then capped at 8,000 characters | nothing | `invalid input: …` → 1; `brief already saved: <toolUseId>` → 1 |
| `brief get <toolUseId>` | — | `BriefOutput`: `toolUseId`, `agentId`, `sessionId`, `promptId`, `dispatchName`, `subagentType`, `goal`, `acceptanceCriteria`, `proofCommand`, `savedAt` | `no brief for tool_use_id: <id>` → 1 |
| `brief map-by-name <name>` | — | the oldest unmapped brief's tool use id with that dispatch name, or nothing | store error → 1 |
| `brief map-by-dispatch <sessionId> <promptId> <subagentType>` | — | the oldest unmapped matching brief's tool use id, or nothing | store error → 1 |
| `brief set-agent-id <toolUseId> <agentId>` | — | nothing (an unknown tool use id changes nothing) | store error → 1 |
| `why <decisionId>` | — | `DecisionOutput` (below) | `unknown decision: <id>` → 1 |
| `undo <decisionId>` | — | `DecisionOutput` with `undone_at` set | `unknown decision: <id>` → 1 |
| `approve <decisionId>` | — | `DecisionOutput` with `approved_at` set; approving again keeps the first time | `unknown decision: <id>` → 1 |
| `config get` | — | the effective `JudgeConfig` (an invalid `config.json` prints the defaults and logs one warning) | store error → 1 |
| `config set <question> enabled true\|false` | — | the saved `JudgeConfig` | usage → 64; a question the judge doesn't have: `unknown question: <name> (known: …)` → 1; an invalid `config.json`: `invalid config <file>: <reason>; fix or delete it, then set again` → 1 |
| `config set <question> threshold <number>` | — | the saved `JudgeConfig` | not a number → 64; outside 0–1 → 1; an invalid `config.json` → 1 as above |
| `config set prompt-sort enabled true\|false` | — | the saved `JudgeConfig` (`promptSort.enabled`; every other key kept) | anything but `enabled true\|false` → 64; an invalid `config.json` → 1 as above |
| `prompt-sort` (the hook path) | stdin `{prompt, sessionId}` (`sessionId` empty or absent: `AW_SESSION_ID`) | nothing, or `{"context": "…"}` when a scaffold fires (all four ship off) | always exit 0 once the input parses (the sorter fails open); input that isn't the record → 1. Machine text, slash commands and confirmations are skipped without a decision row; otherwise one `prompt-sort` decision, its axes (`prompt_sort_axes`) and its run, and `sessions/<sid>.sort.json` |
| `prompt-sort scaffold <brief\|bugfix\|ui-evidence\|plan-first> on\|off` | — | the saved `JudgeConfig` (`promptSort.scaffolds`) | wrong arguments → 64; an unknown scaffold, an invalid `config.json` → 1 |
| `trait-check --edit --file <p>… \| --stop [--base <ref>] \| --scan [--path <p>…] [--trait <n>…]` (`--budget-ms`, `--min-p`, `--concurrency`) | the repo in the working directory; traits `<repo>/.agents/traits/*.yaml` overlaid by `<state>/traits/<repo-name>/*.yaml` | nothing (edit, stop: findings on stderr; scan: JSON lines on stdout) | **its own exits**: 0 nothing to report or skipped, **2 findings**, **3 Jev unavailable** (excluded, no key, unreachable, past the budget), 64 usage, 1 error. The judge's table reads 2 as "escalated"; here it is findings |
| `<question>` | stdin: the question's input record (Questions below) | the decided envelope (exit 0) or the escalated one (exit 2) | an unknown question or any argument → 64 |

Each output's exact JSON is pinned by a golden file (`Aw.Kernel.Testing`'s `Golden`, through `CliGolden`; decision ids
and timestamps normalized) next to the command's CLI tests: `GetHealth/Golden/` (ok, degraded), `AskQuestion/Golden/`
(decided, escalated, decided with an extra field), `ExplainDecision/Golden/why.json`, `GetBrief/Golden/brief-get.json`,
`GetConfig/Golden/` (defaults, a file's blocks) and `SetConfig/Golden/config-set.json`, all under `tests/Aw.Judge.Tests/`.
A change to an output is a reviewed change to its golden file (`AW_GOLDEN_UPDATE=1` rewrites it locally).

#### Not yet built

Only `prompt-sort why|import|outcomes|adjudicate|eval|promote` (`prompt-sort <sub>`, unit 6.4, after the switch) is not in this build
yet (cutover spec §5.2, Q9). Each prints `judge: prompt-sort <sub> is not in this build yet` on stderr, nothing on stdout, and exits 1;
none validates an argument, opens a database, reads stdin, writes a file or calls a provider, and no hook calls one. `--providers` is
stripped first (a flag with no value exits 64), so `--help` and `-h` are no special case. An unknown `prompt-sort <sub>` is
`judge: unknown prompt-sort subcommand: <sub>` on stderr, exit 1. Pinned by `NotBuiltYetCliTests` and by the hook-surface check's stub
cases; `scripts/tests/judge-stub-invocations.test.sh` fails when a hook, lib, script, recipe or Sindri source calls one. The PR that lands a
subcommand deletes its row, its stub test line and its skill marker.

#### Labels and evals

`label`, `adjudicate` and `eval` (unit 6.3) turn stored decisions and transcripts into labelled eval items and score a provider against
them. Items and labels live in `decisions.sqlite` (State files below); nothing here runs in a hook. Every JSON output below is
printed on one line; a failure is one stderr line and nothing on stdout.

| Command | Does | Output (exit 0) |
|---|---|---|
| `label import [--question <q>] [--since <n>d]` | Adds an eval item for every stored decision of the question (default: every question; one whose input doesn't fit its record is left out) from the last `n` days (default 14d, at most 36500) whose stored input still reads as the question's input record. Idempotent: an item's `source` is `decision:<id>`, so a second run adds nothing. A decision's model answer is kept on the item, never shown to the adjudicator | `{"imported": n, "skippedUnparsable": n}` (decisions whose stored input isn't JSON) |
| `label outcomes` | Labels the `ask-check` items that have no outcome label from the next real prompt of their session's transcript (source `outcome`). No provider call; an item with no session, no transcript, no later prompt or a prompt past six hours stays unlabelled and is tried again next run | `{"labeled": n, "noSignal": n}` |
| `label set <itemId> <label\|skip>` | Stores a hand label (source `override`) for an item; the label must be one of the question's outputs or `skip`, which leaves the item out of every score | `{"item": "<id>", "label": "<label>"}` |
| `label status` | Counts per question that has items | `{"questions": [{"question", "items", "labeled", "skipped", "agreement"}]}`; `agreement` compares the outcome and adjudicator labels of items that have both. With no items: `{"questions":[]}` |
| `adjudicate <question> [--limit <n>]` | A stronger model labels the question's items that have no adjudicator label, at most `n` (default 60, at least 1): each is asked three times and gets a label only on a strict majority that is one of the question's outputs (source `adjudicator`) | `{"labeled": n, "split": n, "failed": n}` (no majority; unparseable input or every run errored) |
| `eval <question> [--provider P] [--variant V] [--labels outcome\|adjudicator\|override\|any] [--collapse LABEL] [--record FILE] [--replay FILE]` | Calls one provider directly (no chain, no `decisions` rows; default `jev`) on every labelled item of the question and scores it. `--variant` (default `as-is`) rewrites the stored input; `--labels` picks the label source (default any); `--collapse L` folds every label but `L` into `stalled`; `--record` appends each provider result to a file, `--replay` scores a recorded file with no provider call (labels always come from the store). Two reports are written under `judge/evals/` | The report: `question`, `provider`, `variant`, `labelSource`, `n`, `decided`, `accuracy`, `decisiveRate`, `accuracyDecisive`, `p50LatencyMs`, `sweep` (`[{threshold, coverage, accuracyCovered}]`) and, when both outcome and adjudicator labels exist on shared items, `perSource` (`outcome`, `adjudicator`, `agreement`) and `sourceWarning` (they disagree on more than 30%) |
| `eval import-turns <transcript.jsonl>... [--since 14d]` | Rebuilds `turn-progress` items from the Edit, Write and MultiEdit calls of Claude transcripts and labels each from what happened next in the same transcript (source `outcome`). Only this spelling is the second word: any other word after `eval` is a question name, so `eval import-turn` is an unknown question | `{"imported": n, "skipped": n, "unreadable": n}` (turns with no edits or already imported; files that can't be read). Files older than `--since` are left out |

Exit codes follow the table above: 0 ok, 1 error, 64 usage (a missing, extra or unknown argument, a flag without its value, a bad
`--limit`, an `adjudicate` or `eval` provider the `--providers` allowlist excludes: AW-JUDGE-012). Errors that exit 1: an unknown
question or variant, `--labels` or `--collapse` outside the question's values, `--since` that doesn't look like `14d`, no labelled item
to score (the message names the commands that make some), an unreadable `--replay` file, a provider whose CLI isn't on `PATH` (AW-JUDGE-013;
for `adjudicate` that is `claude`), `label set` of an unknown item (AW-JUDGE-014), a transcript that can't be read (AW-JUDGE-015), a store
that can't be used.

**Redaction.** Every writer redacts string values and object keys through the redactor before capping: the input of an eval item, the
`--record` lines, and both reports. A secret-looking value in a stored decision or a transcript never reaches `eval_items`, `labels` or a file.
Files under `judge/evals/` (`<question>-<provider>-<variant>-<unix ms>.json` and `.md`) and `--record` files are written owner-only (0600)
where the OS has file modes.

**The adjudicator's child.** `adjudicate` runs `claude -p` on the user's subscription with `--model opus --effort high`, thinking on and
`alwaysThinkingEnabled` true, and the isolation the chain's claude-cli provider has: `--no-session-persistence`, no MCP servers, `--setting-sources ""`
(no user, project or local settings, no CLAUDE.md), `--settings {"disableAllHooks":true,…}`, `--disable-slash-commands`, `--tools ""`
(no tools). It runs in a fresh empty scratch directory outside any repo. Its environment is minimal: only `PATH`, `HOME`, `USER`,
`LOGNAME`, `SHELL`, `LANG`, `LC_ALL`, `LC_CTYPE`, `TERM`, `TMPDIR`, `TZ`, `XDG_CONFIG_HOME`, `XDG_DATA_HOME`, `XDG_CACHE_HOME`,
`XDG_STATE_HOME`, `XDG_RUNTIME_DIR` and `CLAUDE_CONFIG_DIR` pass, so `ANTHROPIC_API_KEY`, `GH_TOKEN`, `AW_FORGE_TOKEN` and every other token
stay behind. It therefore needs a subscription login (the one in `HOME` or `CLAUDE_CONFIG_DIR`); an API key in the caller's environment
is not used. The stored input goes to the model redacted and inside the question's tagged untrusted block.

**Transcripts.** `label outcomes` finds a session's transcript at `<projects dir>/<project>/<session>.jsonl`; the projects dir is
`AW_JUDGE_PROJECTS_DIR`, else `~/.claude/projects`.

**An older binary meets a newer database.** The tables come from migration `0005` (State files below). A `judge` binary built before it,
run against a database that has it, refuses to open the file with AW-DATA-007 and every hook fails open (`judge health` reads down): re-run
`./setup.sh` to replace the binary. Use a scratch `AW_STATE_DIR` for by-hand runs of a development build.

`DecisionOutput` is the decision row with the `decisions` table's column names: `id`, `ts`, `question`,
`content_class`, `provider`, `decision`, `confidence`, `reason_code`, `latency_ms`, `input_digest`, `undone_at`,
`chain_position`, `skipped` (`[{provider, reason}]`, reason `unavailable|failed|timeout|below_threshold`), `outcome`
(`decided|escalated|failed`), `approved_at`, `reasons` (the model's reasons as a list of strings, from `decision_details`; null when not recorded: a row written before migration 0005, an answer that carried none, or details pruned after 30 days). Timestamps are UTC ISO-8601 (`AwJson`); `undone_at`, `approved_at` and `reasons`
are null until set.

`JudgeConfig` (`config.json`, written indented): `questions` (required: name → `{enabled, threshold?, providers?}`,
`threshold` 0–1, absent for the question's own, `providers` from `rules`, `jev`, `claude-cli`, `codex-cli`, `cursor-cli`), `providers?`
(`agentClis?`: agent CLI names in priority order, `jev?`: false drops Jev), `promptSort?` (`enabled?`, `budgetMs?`
200–1050, `cooldownPrompts?` a whole number ≥ 0, `scaffolds?`: `brief`, `bugfix`, `ui-evidence`, `plan-first` → on).
Unknown keys are ignored; a wrong type, a missing required field, an unknown name or a value out of range makes the
whole file invalid (AW-JUDGE-007): readers use the defaults (wake-gate enabled at 0.7) and log one warning, and
`config set` refuses to overwrite it. A valid file without wake-gate gets the default one.

### Questions

`judge <question> < input.json` is one command for all nine questions (the `AskQuestion` slice). It reads stdin, takes
no arguments, and prints one envelope on stdout:

| Outcome | Exit | Stdout |
|---|---|---|
| Decided | 0 | `{"decision", "confidence", "model", "reasonCode", "id"}` plus the question's declared extra fields (`chosenIndex`, `reasons`) when the deciding provider sent them; a provider's undeclared fields are dropped |
| Escalated | 2 | `{"escalated": true, "reasonCode"}`, reason `question-disabled`, `invalid-input`, `rule-timeout`, `below-threshold` or `no-provider-decided` |

`choose` is the one exception to these two shapes (the hook questions' envelopes never change): its decided envelope adds `probabilities` (option → probability) when the deciding provider sent them, and its escalated envelope is `{"escalated": true, "reasonCode", "id", "probabilities"?}`, the `id` of the decision row the escalation wrote (for `why`) and the probabilities a provider sent before the call fell below threshold. Both keys are left out, not null, when absent. Pinned by `Hook_question_envelopes_are_unchanged` and the `AskQuestion/Golden/choose-*.json` goldens.

`model` is the provider that decided (`rules` for a pre-rule or a fallback rule), `id` the decision's id for `why`,
`undo` and `approve`. Every call writes one decision row, escalations included. **Hook-contract exception:**
`ask-check` exits **0 for `ask` and for any escalation** (an unsure judge asks) and **2 for `continue`**, because
`done-gate.sh` branches on those codes; its envelope is the same as every other question's. The hooks fail open on any
other non-zero exit.

How a call runs (`AskQuestionService`): the catalog builds the lineup (Providers below) from `config.json`'s
`providers` block and the allowlist (`AskQuestionRequest.AllowedProviders`); the question's `config.json` entry
(`enabled`, `threshold`, `providers`; no entry, or an entry without a threshold, means the question's own threshold;
`config set <question> enabled …` on a question the file doesn't name writes an entry without one) becomes the evaluator's override,
which narrows that chain, and a disabled question escalates `question-disabled`. An unreadable or invalid `config.json`
runs on the defaults and logs one warning. With `--providers rules` (or `AW_JUDGE_PROVIDERS=rules`) a question runs
offline: any input no pre-rule settles escalates `no-provider-decided` (exit 2; `ask-check` asks). After the question
decides or escalates, and before its envelope is written, the judge prunes old decision details (Retention below).

| Question | Input record (stdin) | Outputs | Class | Threshold | Budget | Pre-rules (decided by `rules` without a model) | Fallback after an unsure model |
|---|---|---|---|---|---|---|---|
| `wake-gate` | `{text, senderKind}`, `senderKind` one of `teammate`, `subagent`, `main`, `background` | `send`, `batch`, `drop` | message-meta | 0.7 | 10 s | a `{"type":"ack"\|"idle_notification"…}` message → `drop`; an urgent word (blocked, blocker, failed, error, question, approve, approval; any case) → `send`; trimmed text of 12 characters or fewer → `drop` | `send` |
| `brief-scope` | `{agentType, goal, acceptanceCriteria, proofCommand, skillInternal?}` | `ready`, `missing`, `needs_design` | brief | 0.6 | 10 s | `agentType` `Explore`, `claude-code-guide` or `lean-researcher`, or `skillInternal: true` → `ready`; a blank goal or acceptance criteria → `missing` | none (escalates) |
| `ask-check` | `{transcriptTail}` | `continue`, `ask` | transcript | 0.6 | 10 s | the deny list → `ask`: merge, delete, force push (`force-push`, `force pushed`, …), or a push with `-f`, `--force`, `--force-with-lease` or a `+refspec`; a plain push isn't listed | `ask` |
| `rule-check` | `{rule, hunk}` | `violated`, `fine`, `n/a` | diff | 0.6 | 10 s | a blank hunk → `n/a` | none |
| `resolution-check` | `{brief, expected, actual, rootCause, checkKind, checkSummary, beforePassed, afterPassed, diffStat, diff?}`, `checkKind` `ui-evidence`, `api-evidence` (an HTTP or CLI contract check), `db-evidence` (a persisted-state check) or `test`; a blank `brief` is invalid input | `resolved`, `partial`, `unresolved` (+ `reasons`; a `partial` or `unresolved` answer without a non-blank reason is skipped like a failed one, failure row `no-reasons`, and the chain goes on; Jev, which only classifies, is not asked) | brief | 0.8 | 15 s | the check fails after the fix, or already passed before it → `unresolved`; a diff over 100,000 characters is cut by whole files, `src/` files first, and the prompt ends with a line naming each omitted file and its size (a single file, or a first file that alone is over the cap, is cut at a line and named as cut) | none |
| `choose` | `{prompt, state: {<fact>: <value>}, choices: [{name, criterion}], threshold?}`; per call: 2 to 32 choices, each `name` matching `[a-z][A-Za-z0-9]{0,31}`, distinct and never `uncertain`, each `criterion` 1 to 400 characters, a non-blank `prompt`, a non-empty `state`, `threshold` above 0 and at most 1; anything else is invalid input | the call's choice names (+ `probabilities` in the envelope) | brief | config.json's `choose` entry, else the call's `threshold`, else 0.6 | 15 s | none | none (escalates) |
| `turn-progress` | `{problem, turnDiff, acceptanceCriteria?, priorDiffStat?, signals?}` (the last three default to empty); a blank `problem` is invalid input | `progressing`, `stalled`, `regressing`, `off-target` | diff | 0.7 | 10 s | a blank `turnDiff` → `stalled` | none |
| `ui-element-repair` | `{brokenSelector, step, candidates: [{index, role, accessibleName, testId, text}]}` (the four strings may be null) | `repaired` (+ `chosenIndex`), `no-good-candidate` | code | 0.6 | 10 s | no candidates → `no-good-candidate` | none |
| `visual-critique` | `{afterScreenshot, evidenceDir, baselineScreenshot?}`; `evidenceDir` absolute and each screenshot a relative path that stays inside it (no `..`), none with a control character or a line or paragraph separator (U+2028, U+2029), else `invalid-input`; the prompt carries each path on one line, capped | `looks-right`, `looks-off`, `sloppy` (+ `reasons`) | image | 0.5 | 20 s | none: judging the screenshot is the question | none |

`choose` (contract: owner-approved 2026-10-17, DT spec §8.3) is the one question whose outputs, criteria and threshold come from its input: `ChooseQuestion.For(input)` builds its `QuestionDefinition` per call (`QuestionEvaluator.EvaluateChoose`, which the catalog calls). The prompt is the question, then each fact and the options with their criteria inside untrusted-text blocks. Its definition asks for a confidence (`AskConfidence`): the agent CLIs' answer schema then requires `confidence` (a number, 0 to 1), so their answers can fall below the threshold too, and an answer without a usable one is errored, never taken as certain; every other question's schema and the 1.0 default are unchanged. `uncertain` is not a choice name: the caller maps an escalation to it.

The input names of `wake-gate`, `brief-scope` and `ask-check` are hook contract; the other six (`choose` included) are the C# judge's own
design (the five that tier-2 packages call are ported to this contract; `choose`'s caller is Sindri, through a cli edge, DT spec §8.3). Prompts carry untrusted text (a ticket, a diff, a problem
statement) inside tagged blocks with the tags inside it defused, capped (`UntrustedText`). Pinned by the
`Questions/*Tests` (each question's rules, offline with `ScriptedProvider`), `AskQuestionTests` (config, allowlist,
session, the config's providers block) and `AskQuestionCliTests` (envelopes, exit codes, the ask-check exception,
invalid input, unknown question).

### Errors (AW-JUDGE-NNN)

| Code | Kind | When |
|---|---|---|
| AW-JUDGE-001 | Invalid | Input failed validation (an id past its cap, a threshold outside 0–1) |
| AW-JUDGE-002 | NotFound | `why`, `undo`, `approve` of an unknown decision |
| AW-JUDGE-003 | Conflict | A decision id already recorded |
| AW-JUDGE-004 | Conflict | A brief already saved for the tool use id |
| AW-JUDGE-005 | NotFound | `brief get` of a tool use id with no brief |
| AW-JUDGE-007 | Invalid | `config.json` can't be read as a judge config |
| AW-JUDGE-010 | Invalid | The `--providers` flag or `AW_JUDGE_PROVIDERS` is malformed (a usage error, 64) |
| AW-JUDGE-011 | Invalid | `why` found the decision row but a column can't be read (a timestamp that isn't ISO-8601 UTC text): exit 2, one stderr line naming the decision and the column |
| AW-JUDGE-012 | Invalid | `eval --provider`, or `adjudicate` (which needs `claude-cli`), names a provider the `--providers` allowlist excludes (a usage error, 64) |
| AW-JUDGE-013 | NotFound | `eval --provider` names a provider that can't be asked (its CLI is not on `PATH`, or it is `rules`), or `adjudicate` can't find `claude` |
| AW-JUDGE-014 | NotFound | `label set` of an item id no eval item has |
| AW-JUDGE-015 | NotFound | A transcript that can't be read (`label outcomes`, `eval import-turns`) |

## Providers

A question is answered by the first provider in its chain that decides (the evaluation engine, `QuestionEvaluator`).
The providers live in `Aw.Judge.Infrastructure/Providers/`; Composition reaches them through one port.

### The catalog

`IProviderCatalog.Lineup(ProvidersConfig? config, IReadOnlyCollection<ProviderName>? allowed, IModelProvider rules)` returns a
`ProviderLineup(Providers, Chain)`, which is what an `EvaluationContext` takes. It is the one place the inputs meet:

1. the agent CLIs: `config.json`'s `providers.agentClis` when set, else the default order (claude, codex, cursor)
   with the `AW_PROVIDER` host moved first (`AgentCliResolution`); either way only those whose binary is on `PATH`;
2. Jev, unless `providers.jev` is false;
3. the chain: text classes get Jev, the agent CLIs, then rules; image gets the agent CLIs, then rules
   (`ChainSpec.Build`); then the `--providers` / `AW_JUDGE_PROVIDERS` allowlist, which always keeps rules;
4. the providers: one instance of each model provider the chain names, each wrapped in `FailOpenModelProvider`, then
   the `rules` provider the caller passes (the engine's own, which `AddJudgeCore` registers; the catalog doesn't make
   one).

`IModelProvider.Decide` never throws except `OperationCanceledException` for the caller's token: every outcome is a
result (`ModelProviderContract`). The evaluator in Core relies on that and catches nothing; `FailOpenModelProvider` is
the backstop at the boundary, so an adapter bug that throws anyway becomes errored `provider-threw` (a failure row)
and the chain moves on.

`AddJudgeInfrastructure` registers it (its `AddModelProviders` part) over one instance of each model provider: Jev
(with its own `HttpClient`) and the three agent CLIs (over `IProcessRunner`). `AskQuestionService` calls it once per
question; it looks the agent CLIs up on `PATH`, but starts nothing and reads no key (no keychain call) until a provider
in the chain is asked.

### The agent CLIs

Each runs headless on a small fast model, with the recursion guard `AW_JUDGE_CHILD=1` (every `aw:*` hook in the child
exits at once) and without the judge's own secrets' variables (`SecretNames.Variables` in Core: `TYPESAFE_API_KEY`) in the environment it inherits, the question's time budget as its timeout, and a fresh empty temp directory as its working directory,
deleted afterwards. The model sees the question's prompt and nothing else: every prompt already carries the input's
fields (capped and with their delimiter tags defused, `UntrustedText`), so the raw input record is never sent. claude
and codex read the prompt on standard input, so no input size can hit the system's argument limit; cursor-agent takes
it as an argument (its `-p` has no documented standard-input mode, and the TS provider passed it the same way),
redacted first like Jev's prompt (argv is readable by any local user through `ps` and `/proc/<pid>/cmdline`, and
process auditing records it), then capped at 30,000 characters (under Linux's 128 KiB per argument; for an image
question that includes the screenshot paths, each one line, which get at most 4,000). The answer schema is the question's outputs as the decision's enum, plus
its extra properties. All three read images, so all three cover every content class.

| Provider | Binary and model | Isolation | Schema and answer |
|---|---|---|---|
| `claude-cli` | `claude -p --model haiku --effort low` | `--no-session-persistence`, no MCP servers, `--setting-sources ""` (no settings files or CLAUDE.md), hooks and slash commands off, thinking off (`MAX_THINKING_TOKENS=0`); text: no tools, a fixed one-line `--system-prompt`, the prompt as the message on stdin; image: the prompt on stdin, only `Read`, scoped to the evidence directory, which is the working directory | `--json-schema`; the envelope's `structured_output`, else its `result` text parsed as JSON |
| `codex-cli` | `codex exec --model gpt-6-luna -c model_reasoning_effort="low"` | `-C` the workspace, `--ephemeral`, `--ignore-user-config`, `--ignore-rules`, read-only sandbox, the `hooks` feature (and tool-list features) disabled; images as `--image` absolute paths; `-- -`, the prompt on stdin | `--output-schema schema.json` (in the workspace) in OpenAI strict mode: every property required, extras nullable, no others; the last `agent_message` of the `--json` event lines |
| `cursor-cli` | `cursor-agent -p --model gpt-5.6-luna-none-fast` | `--mode ask`, `--sandbox enabled`, `--trust` (a fresh workspace needs it), `--workspace` the temp directory (the evidence directory for images, named by absolute path); the prompt as the last argument, capped | no schema flag: the schema is stated in the prompt; the last JSON object in the envelope's `result` text |

The answer object is read the same way for all three: a string `decision` among the outputs; a `confidence` 0–1 when
present, else **1.0** (the CLIs answer under a schema with no confidence field, so a well-formed answer is taken as
certain; `choose` is the exception, its schema requires the field and an answer without a usable one is errored, Jev's included); each declared extra checked against its fragment's JSON `type` (and array `items`), a null one absent;
undeclared fields dropped.

### Jev

`POST https://api.typesafe.ai/v1/systemone` with `{state: <input, redacted and capped like the stored input>, model: "jev-1.13.0", questions: {decision:
{type: "choice", instructions: <prompt, redacted and capped>, criteria: {<output>: <criterion or the output>}}}}` and
`Authorization: Bearer <key>`; text classes only. The key comes from `ISecretSource` (`TYPESAFE_API_KEY`, else the
keychain), read inside the question's budget: a keychain that sits on its unlock prompt ends as `timeout`, not as
latency added in front of the call. Jev is the one provider that sends the input to a third party on the judge's own key, so `JevProvider`
redacts (then caps at `InputCap.MaxChars`) both the state and the prompt, which embeds input fields word for word
(ask-check's transcript tail, a rule-check hunk, a brief's proof command). claude and codex get the prompt as built,
on standard input: they run on the user's own model logins, the same place the agent's session already sends that
text. cursor-agent gets it redacted, because its prompt is an argument (The agent CLIs above).
The answer's `choice`, `confidence` (1.0 when absent, except for `choose`, where a missing or out-of-range one is errored `unparseable-answer`) and `probabilities` become the decision.

### Rules

`rules` closes every chain but decides nothing itself: pre-rules and fallback rules run in the engine, and the rules
provider is the engine's (`Aw.Judge.Core.Evaluation`), always unavailable, never a failure. The catalog takes it as an
argument. The question rules (input checks, pre-rules, prompts, fallbacks) are regexes with a one-second match
timeout, which a huge input can reach: a timeout writes a `rules`/`rule-timeout` failure row and escalates the call
(outcome `failed`; for `ask-check` that means ask; it counts in `failures24h` but not in `health`'s model-attempt
streak), except in a fallback, which then simply has no opinion. Any other
exception from a rule is a bug in the question and is not caught.

### Reason codes

What the engine does with each result is the engine's rule: decided → a decision (below threshold: skipped, then the
fallback rule); unavailable → skipped with no failure row, except `timeout`, which is a failure row; errored → a
failure row with the code.

| Result | Code | Providers | When |
|---|---|---|---|
| decided | `model` | all | an answer within the outputs |
| unavailable | `timeout` | agent CLIs, Jev | past the question's budget; a CLI is killed with its whole process tree |
| unavailable | `not-installed` | agent CLIs | the binary isn't on `PATH` |
| unavailable | `no-api-key` | Jev | no key in the environment or the keychain |
| unavailable | `network-error` | Jev | the API can't be reached (offline, DNS, refused) |
| errored | `spawn-failed` | agent CLIs | the binary is there but the system refused to start it (argument list too long, not a program, no permission) |
| errored | `exit-N` | agent CLIs | the CLI exited non-zero |
| errored | `provider-threw` | any (the catalog's `FailOpenModelProvider`) | the adapter threw despite its contract (a bug) |
| errored | `unparseable-output` | agent CLIs | stdout isn't the CLI's envelope (or event lines) |
| errored | `unparseable-answer` | all | no JSON answer object with a string decision |
| errored | `out-of-enum` | all | the decision isn't one of the outputs |
| errored | `extra-out-of-schema` | agent CLIs | a declared extra has the wrong JSON type |
| errored | `cli-reported-error` | agent CLIs | `is_error` (claude, cursor) or codex's `turn.failed`/`error` with no answer |
| errored | `invalid-image-input` | agent CLIs | an image question's input has no `afterScreenshot` and `evidenceDir` |
| errored | `http-NNN` | Jev | a non-success status |
| errored | `too-many-options` | Jev | more than 255 outputs |

### Processes and secrets

`SystemProcessRunner` finds a bare program name on the `PATH` the environment gives (a path is run as is), reads both
output streams concurrently from the start, each kept up to the request's `OutputLimit` (4 Mi characters by default;
the rest is read and dropped), and writes the input on its own task (so a full pipe never blocks it). On a timeout or a
cancellation it kills the whole process tree (`Process.Kill(entireProcessTree: true)`). Once the program exits, the
streams get a 2 s grace to finish, not the rest of the budget: a background child the program left holding its pipes
can't turn a finished run into a timeout. That child isn't stopped (it left the program's tree when the program
exited; reaping it would need its own process group). A program that isn't installed (not found, or the system says the file is
gone, `ENOENT`, and the file really is gone) is `NotStarted`; any other refusal to start (`E2BIG`, `ENOEXEC`, `EACCES`)
is `FailedToStart`, with the system's message as standard error, and so is a working directory that doesn't exist (an
image question's `evidenceDir` that isn't there): Unix reports that failed `chdir` as `ENOENT` too, but the program is
installed, so it is a `spawn-failed` failure row, never `not-installed`. Neither is an exception.

`KeychainSecretSource` reads a secret's variable; when it is unset or empty it runs `security find-generic-password
-s agentic-workflow-typesafe -a TYPESAFE_API_KEY -w` (5 s timeout). A missing entry, a missing `security` (not macOS)
or a hung keychain all read as unset.

## Fail-open

The hooks fail open on any judge failure, and the judge does its part: storage never stops a question.

- **A question** prints its decision with its normal exit code whatever state `decisions.sqlite` is in. The decision
  write fails open (`IDecisionLog` returns an error, never throws): the evaluator then tries to record a
  `decision-write-failed` failure row, which counts in `health` when only the decision write failed; with the whole
  store unusable nothing can be counted, and the outcome stands. The envelope's `id` then names a decision `why` won't
  find.
- **`health`** on a store it can't open exits 1 with one stderr line, so `judge-health.sh` prints `judge: health check
  failed` and the statusline shows the judge as down.
- **The brief commands** on a store they can't open exit 1 with one stderr line that isn't `no brief for …`, so
  `done-gate.sh` fails open (`judge-brief-exit-1`) to its evidence check; `scope-gate.sh` and `subagent-start-map.sh`
  ignore the exit.
- A database that opens **read-only** (the file and its directory not writable) still answers reads: `health` and
  `brief get` report what it holds, and only the writes fail with one line.
- `config get`/`set` never open the database; a usage error never touches it.

Pinned by `UnusableStorageTests` (a read-only state directory, a read-only database, a corrupt file) and the decision
log's own store-error tests.

## Retention

Questions are the only command that writes `decision_details`, so the retention policy is a decorator on that one
service: `PruneAfterQuestion`, registered in `AddJudgeFeatures` around `AskQuestion`. After a successful question it
asks `IDecisionLog.PruneDetailsBefore` for the details of decisions older than 30 days (`DecisionDetails.Retention`)
before the start of today (UTC); the decision rows stay. A question that fails prunes nothing, and the prune's own
result is ignored (best effort). The cutoff moves once a day, so only the day's first question finds anything to drop;
the SQLite log reads first (`SELECT … LIMIT 1` that scans `decision_details`, which holds only the retention window,
and looks each decision up by id, so its cost doesn't grow with the decision history; no write lock) and opens a write
transaction only when there is something to delete, so a question never waits on another writer for retention. Pinned
by `PruneAfterQuestionTests`, `PruneAfterQuestionCliTests` and `SqlitePruneTests`.

## Hook contract

What the hooks that keep running read, and nothing else. It stays frozen: a change here is a change to the hooks too, checked by the real-hook check below.

| Hook | Calls | Reads | Pinned by |
|---|---|---|---|
| `scope-gate.sh` | `judge brief save` < `{toolUseId, sessionId, promptId, dispatchName, subagentType, goal, acceptanceCriteria, proofCommand}` (`dispatchName` null when empty) | nothing (`> /dev/null 2>&1 \|\| true`) | `SaveBriefCliTests.Brief_save_stores_the_payload_and_prints_nothing` |
| `scope-gate.sh` | `judge brief-scope` < `{agentType, goal, acceptanceCriteria, proofCommand, skillInternal?}` (`AW_SESSION_ID` set) | `.decision` of stdout; a non-zero exit fails open | `AskQuestionCliTests.Brief_scope_prints_its_decision`, `BriefScopeTests` |
| `send-gate.sh` | `judge wake-gate` < `{text, senderKind}` (`AW_SESSION_ID` set) | `.decision` of stdout; a non-zero exit fails open | `AskQuestionCliTests.A_decided_question_prints_the_decided_envelope_and_exits_0`, `WakeGateTests` |
| `done-gate.sh` | `judge ask-check` < `{transcriptTail}` (`AW_SESSION_ID` set) | the exit code: 0 = ask (an escalation asks too), 2 = continue, anything else fails open | `AskQuestionCliTests.Ask_check_exits_0_for_ask_or_an_escalation_and_2_for_continue`, `AskCheckTests` |
| `done-gate.sh` | `judge brief get "task:<session>"` | stdout as the brief JSON; on a non-zero exit, stderr starting `no brief for` means "no brief", anything else fails open | `GetBriefCliTests` (the `no brief for` prefix, exit 1) |
| `done-gate-annotate.sh` | `judge brief get <toolUseId>` | `.acceptanceCriteria` | `GetBriefCliTests` |
| `subagent-start-map.sh` | `judge brief map-by-name <agentType>`, `judge brief map-by-dispatch <session> <prompt> <agentType>` | stdout: the tool use id or nothing, exit 0 | `MapBriefByNameCliTests`, `MapBriefByDispatchCliTests` |
| `subagent-start-map.sh` | `judge brief set-agent-id <toolUseId> <agentId>` | nothing (`\|\| true`) | `SetBriefAgentIdCliTests` |
| `judge-health.sh` | `judge health` | `.status` (`ok`, `degraded`, `down`) and `.failures24h`; a non-zero exit is "health check failed" | `GetHealthCliTests` |
| `config/statusline.sh` (`judge_health`, the background refresh) | `judge health` (on `PATH`, else `~/.local/bin`) | `.status` (`ok`, `degraded`; anything else, or a non-zero exit, is `down 0`) and `.failures24h` | `GetHealthCliTests` |
| `prompt-sort.sh` | `judge prompt-sort` < `{prompt, sessionId}` (`AW_SESSION_ID` set; the hook kills it after `AW_PROMPT_SORT_BUDGET_MS`, default 1500) | `.context` of stdout; the exit code is ignored | `SortPromptCliTests`, `SortPromptTests` |
| `done-gate.sh` | none: reads `judge/sessions/<sid>.sort.json`, which `judge prompt-sort` writes | `.requirements.uiEvidence`; then rewrites the file with `jq 'del(.requirements.uiEvidence)'`. The file's shape is `{prompts, lastFired, requirements: {uiEvidence?}}` and stays frozen; the sorter keeps keys it doesn't know and replaces the file whole | `FileSortSessionStoreContractTests`, `FileSortSessionStoreTests`, and the real-hook `done-ui-*` cases |
| `trait-check.sh` | `judge trait-check --edit --file <p>…` (PostToolUse) and `judge trait-check --stop` (Stop) | the exit code: 0 passes, **2** with stderr as the message to the agent, **3** is Jev unavailable (fails open, logging `jev-unavailable`), anything else fails open as `judge-exit-<n>` | `TraitCheckCliTests`, `CheckTraitsTests`, and the real-hook `tc-*` cases |

The hooks set `AW_SESSION_ID` on the question calls; the Codex and Cursor adapters export `AW_PROVIDER` (unset under
Claude Code); `AW_STATE_DIR` and `AW_JUDGE_PROVIDERS` come from the session's environment (below). The hooks also
write their own files under `$AW_STATE_DIR/judge/` (`sessions/`, `outbox`, `teammate-names.jsonl`), which the
judge doesn't own.

**The real-hook check**, `config/lib/tests/judge-cs-hook-surface.test.sh`, runs the hook scripts themselves against
the C# judge, offline (`AW_JUDGE_PROVIDERS=rules`, a scratch `AW_STATE_DIR`): send-gate, scope-gate, done-gate,
done-gate-annotate, subagent-start-map, judge-health and the statusline's own `judge_health` (taken from
`config/statusline.sh`), checking each hook's stdout, exit code and written files. send-gate and scope-gate allow, and
done-gate falls back to its evidence check, on any judge failure, so every case that expects a pass also expects no
fail-open log line; the one case that reaches a fail-open on purpose (a wake-gate escalation, exit 2) expects exactly
`judge-exit-2`. A send-gate case has a stub `claude` answer `batch`, which queues the message in
`judge/outbox/<sid>.jsonl`, and the next urgent send in the same state flushes and attaches it. The done-gate cases cover ask (a pre-rule), an escalation (asks), continue (a stub `claude` answering
`continue`, which writes `judge/sessions/<sid>.json`), a claim that meets the saved brief in words the no-brief
evidence check wouldn't accept, one that misses it (exit 2 naming the criteria), and no brief. The prompt-sort cases cover a real prompt (exit 0, nothing printed, one `prompt-sort` decision row and
`sessions/<sid>.sort.json` with `prompts: 1`) and a machine-text, a slash-command and a confirmation prompt (nothing recorded, no file).
The sorter's file feeds the done-gate cases: with `requirements.uiEvidence` set by a `jq` edit of the C# writer's own file, a
done claim with no UI evidence is blocked once (exit 2, the flag deleted, the rest kept), the same claim then passes, a claim that
shows a screenshot passes, and the next `prompt-sort` still reads the rewritten file. The trait-check cases cover a repo with no
traits (exit 0, silent) and a repo with a matching trait and no Jev (the CLI exits 3 and the hook fails open logging
`jev-unavailable`). Every subcommand of the Not yet built table is checked for its exact line and exit 1. No real-hook case reaches a Jev: offline
(`AW_JUDGE_PROVIDERS=rules`) the sorter runs on its keyword heuristics and trait-check is exit 3; a decisive axis and the findings
path are the C# unit tests' (`SortPromptTests`, `CheckTraitsTests`, over `FakeBatchClassifier`). The health cases cover
ok, degraded (a stub `claude` that exits 1 leaves one failure row) and a store that can't be opened. It builds
`src/Judge/Aw.Judge.Host` in Release itself when neither `AW_JUDGE_CS_BIN` (made absolute) nor an installed `judge` on `PATH`
is there. CI runs it in the .NET job, which a hook, statusline or
hook-library change selects (`scripts/ci/changed-areas.sh`), against the NativeAOT publish the AOT smoke step leaves at
`artifacts/aot-judge-smoke/bin/judge` (spec §4a step 3, §4b.5), so the hook paths the smoke doesn't cover (brief-scope,
the claude-cli provider, an unusable store) are checked natively too.

```bash
bash config/lib/tests/judge-cs-hook-surface.test.sh   # "All judge-cs-hook-surface tests passed."; dotnet 10 only for the build fallback
bash src/Judge/aot-smoke/run.sh && AW_JUDGE_CS_BIN=artifacts/aot-judge-smoke/bin/judge \
  bash config/lib/tests/judge-cs-hook-surface.test.sh   # as CI runs it: against the NativeAOT binary
```

## Build and NativeAOT

`judge` is a NativeAOT entry point (`AwAotEntryPoint`, `PublishAot` under a runtime identifier); local builds and
tests run JIT. `src/Judge/aot-smoke/run.sh` publishes it for the host RID on a copy of the sources it needs (the lock
files stay RID-free), fails on any IL2xxx/IL3xxx warning or a missing bundled `libe_sqlite3` beside the binary, then
runs the native binary offline (bare `PATH`, `AW_JUDGE_PROVIDERS=rules`, `AW_LOG_LEVEL` unset) on a temp state
directory: `health`, `brief save`/`get`/`map-by-name`, a missing brief, the wake-gate and ask-check pre-rules, a
question nothing decides, `config get`, a usage error, a bad allowlist on a question, a bad `AW_JUDGE_PROVIDERS`
under `health`, the offline `prompt-sort` hook path (prints nothing, writes `sessions/s1.sort.json`) `label status` on the empty eval store (`{"questions":[]}`) and one Not yet built subcommand. The output, with ids and times normalized,
must equal `expected.txt`. CI's .NET job runs it with the other `src/*/aot-smoke/run.sh` scripts. The binary is about
11 MB.

```bash
bash src/Judge/aot-smoke/run.sh   # "aot-judge-smoke: native binary passed (… judge)"
```

## State files

All under `${AW_JUDGE_STATE_DIR:-${AW_STATE_DIR:-~/.agentic-workflow}/judge}/`. The C# build starts with fresh state (spec §4c); it never
reads a TS-written file.

| File | Holds |
|---|---|
| `decisions.sqlite` | WAL SQLite (`Aw.Kernel.Data`), migrations embedded in `Aw.Judge.Infrastructure/Sqlite/Migrations/` (`0001_judge.sql`, `0002_approved_at.sql`, `0003_prompt_sort.sql`, `0004_eval_labels.sql`, `0005_decision_reasons.sql`; a migration is never edited once merged): `decisions` (one row per decision, `approved_at` from 0002), `decision_details` (the redacted input and the provider's detail, pruned after 30 days; `reasons` from 0005: the model's reasons as a JSON array of strings, redacted and capped like the input, NULL when not recorded, which is different from `[]`), `failures` (provider failures, for `health`), `briefs` (the dispatch briefs, their texts redacted and capped), `prompt_sort_axes` and `prompt_sort_runs` (from 0003: one row per sorted axis of a `prompt-sort` decision, and one run row), `eval_items` (from 0004: one row per scoreable input, `id`, `question`, `input_json` redacted and capped, `source` unique (`decision:<id>` or `transcript:<session>:<turn>`, what keeps an import idempotent), `model_decision`, `created_at`) and `labels` (from 0004: one row per item and label source, `outcome`, `adjudicator` or `override`, with `label` and `labeled_at`), `schema_version` |
| `sessions/<sid>.sort.json` | The prompt sorter's per-session state (`{prompts, lastFired, requirements}`), read and rewritten by `done-gate.sh`; the hooks sweep `sessions/*.json` after 24 h |
| `evals/<question>-<provider>-<variant>-<ms>.json\|.md` | The reports of `eval` (redacted, 0600) |
| `trait-check/log.jsonl` | One line per trait-check run (redacted) |
| `config.json` | `JudgeConfig`, written by `config set` (a temporary file in the same directory renamed over it, so a question never reads a half-written file); absent means the defaults |

## Environment

| Variable | Read by | Meaning |
|---|---|---|
| `AW_JUDGE_STATE_DIR` | Infrastructure settings | The judge directory itself (`decisions.sqlite`, `config.json`), used as is; read first. For a caller that runs its own judge with its own store (Sindri): it moves nothing but the judge's own files, so `AW_STATE_DIR`'s locks and other tools' state stay put |
| `AW_STATE_DIR` | Infrastructure settings | The state root; the judge's files are under `<root>/judge/` unless `AW_JUDGE_STATE_DIR` is set. Default `~/.agentic-workflow` |
| `AW_SESSION_ID` | the question commands (`Composition`, once) | The calling session, stored with the decision's detail row; a value past the session id's cap is ignored |
| `AW_PROVIDER` | `ProviderCatalog` | The host the calling session runs in (`claude`, `codex`, `cursor`), which leads the agent CLI order unless `config.json` sets `providers.agentClis` |
| `AW_JUDGE_PROVIDERS` | the command table (`Composition` reads it once) | A comma-separated allowlist of providers when the `--providers` flag is absent; only a question, `prompt-sort` and `trait-check` read it, and there an unreadable list is a usage error (64) |
| `PATH` | `PathExecutableLocator`, `SystemProcessRunner` | Where the agent CLIs (and `security`) are found |
| `TYPESAFE_API_KEY` | `KeychainSecretSource` | Jev's API key; when unset or empty, the keychain entry `agentic-workflow-typesafe` / `TYPESAFE_API_KEY`; without either Jev is unavailable |
| `AW_JUDGE_PROJECTS_DIR` | Infrastructure settings | Where `label outcomes` and `eval import-turns` find Claude session transcripts (`<project>/<session>.jsonl`); default `~/.claude/projects` |
| `AW_PROMPT_SORT_BUDGET_MS` | `config/hooks/prompt-sort.sh` | The hook's kill budget in milliseconds (default 1500); the judge doesn't read it: Jev's own budget is `promptSort.budgetMs` (200–1050, default 1000) |
| `AW_LOG_LEVEL` | Aw.Kernel.Telemetry | The stderr log level (default information, at which a normal run logs nothing; the hooks discard stderr) |

The judge sets `AW_JUDGE_CHILD=1` (and `MAX_THINKING_TOKENS=0` for claude) for the agent CLIs it starts; it doesn't
read them.
