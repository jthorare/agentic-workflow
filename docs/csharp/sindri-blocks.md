# Sindri (C#) — judge and evidence as engine blocks

Stage S4a of the Sindri app (factory addendum §17.28.2 "Tools versus workflows", §17.29.4 step 4). The tools stay tools:
judge answers questions and ui-evidence drives a browser. In a recipe each becomes a **block** on the registry contract
in `docs/csharp/sindri-engine.md` § Blocks, and the sequence around them is a recipe. This page is the contract for
those blocks; the engine contract is unchanged.

Code: `src/Sindri/Aw.Sindri.Core/Blocks/{Judge,Evidence}`, `Blocks/IEvidenceProvider.cs`, `Blocks/EvidenceProviders.cs`,
`Registration/SindriCoreServiceCollectionExtensions.cs` (`AddSindriCore()` registers the tool blocks with the built-ins),
`Aw.Sindri.Infrastructure/Tools/` (the process adapters), `Aw.Sindri.Features/CollectEvidence/`, and the built-in recipe
`Aw.Sindri.Infrastructure/Recipes/Builtin/workflow.ui-evidence.yaml`.

## How each tool is reached

Both tools are reached **out of process, through their CLI contracts** (`deps.yaml`: `Sindri -> Judge` and
`Sindri -> Evidence`, kind `cli`). Neither is a project edge:

| Tool | Why a CLI edge, not a project edge |
|---|---|
| ui-evidence (`src/Evidence`, #158) | Its adapters (the stack probe, the database query, Playwright) live in `Aw.Evidence.Infrastructure`, which references Microsoft.Playwright and opts out of AOT. A project edge reaches only `Aw.Evidence.Core` (DEPS-PROJ-007), which has the ports but no adapters, so Sindri would have to write its own copies of the adapters. Sindri's host is NativeAOT, so it can't link Playwright at all. All four blocks (`run`, `parity`, `doctor`, `provenance`) therefore run the `ui-evidence` binary. |
| judge (`src/Judge`, #156, open) | judge is its own NativeAOT CLI with its own store (`decisions.sqlite`) and provider chain. Its contract is the CLI (`docs/csharp/judge.md` on #156). The port in this package (`IJudgeClient`) binds that contract and nothing else. |

The binaries: `AW_SINDRI_JUDGE_BIN` (default `judge` on `PATH`) and `AW_SINDRI_UI_EVIDENCE_BIN` (default `ui-evidence`
on `PATH`), read by `ToolSettings` in Infrastructure. **Before the switch**, `judge` on `PATH` is the TypeScript judge,
whose contract this binding doesn't speak, so a run of the C# blocks sets `AW_SINDRI_JUDGE_BIN` to the C# judge. Nothing
installs `ui-evidence` yet (`docs/csharp/evidence.md`, "At the switch"). Both variables, and everything the tools
themselves read (`UI_EVIDENCE_APP_TITLE`, `UI_EVIDENCE_SEED_*`, the lock variables, `AW_JUDGE_PROVIDERS`, a parity
manifest's credentials), are read from the **control plane's** environment when it starts, and the tools inherit it
(with `AW_LOG_LEVEL=warning`): set them for `sindri` / `sindri serve`, and restart the control plane to change them;
exporting them in the shell that runs `sindri workflow …` changes nothing. `sindri workflow signal`, `collect`, `unpark`
and `handback` wait up to 25 minutes for an answer, because they may run judge gates (a signal, or a parent's
`child.done`) and `collect` runs ui-evidence; every other call waits 10 s.

The binding was checked against #156 head `fabba31e` (a read-only export, built in a scratch directory): `judge
visual-critique --providers rules` escalates `no-provider-decided` (exit 2), `brief-scope` with an `Explore` agent
decides `ready` by pre-rule (exit 0), and `claude-cli,rules` decides `looks-right` or `looks-off` with a decision id
that `judge why` finds. The contract tests run against the fake and a scripted binary, so this package doesn't depend on
#156 merging.

## Ports

| Port (Core) | Adapter (Infrastructure) | Fake and contract (`tests/Aw.Sindri.Testing`) |
|---|---|---|
| `IJudgeClient.Ask(JudgeInput, ct)` → `JudgeAnswer` | `ProcessJudgeClient`: `judge <question>`, the input record on stdin | `FakeJudgeClient`, `JudgeClientContract` |
| `IUiEvidence`: `Doctor`, `Provenance`, `Run`, `Parity` | `ProcessUiEvidence`: `ui-evidence doctor`, `provenance`, `run <script> <run-dir> --host H --app-build SHA`, `parity <manifest> …` | `FakeUiEvidence`, `UiEvidenceContract` |

Each contract suite runs against the fake (unit lane) and against the process adapter driving a scripted binary that
speaks the tool's CLI contract (integration lane, `tests/Aw.Sindri.Tests/Blocks/Process*ContractTests.cs`). The process
binding adds the cases only a process has: a binary that can't be started, an exit outside the contract, a document that
doesn't read, a run that writes no document.

**Mapping the CLI contracts.** `ToolProcess` runs the binary with an argument list (no shell) and captures stdout and
stderr. At Sindri's timeout (`ToolSettings`: judge 120 s, since judge's budget is per provider and a chain tries up to
four; doctor and provenance 60 s; run and parity 20 minutes, past ui-evidence's two 120 s lock waits), or when the
caller cancels (Ctrl-C on `collect` aborts the request), the tool is stopped. ui-evidence gets SIGTERM and 30 s to exit
(it closes the browser, which can take its own 10 s bound, and then releases its machine locks); if it is still running
then, its whole tree is killed. judge handles no signal, so its tree (with any agent CLI it started) is killed at once.
Once the tool exits, its output streams get 2 s to drain, so a child left holding them can't turn a finished run into a
timeout. An error message is the tool's last stderr line that isn't a JSON log line (both tools end with their error,
after lines such as ui-evidence's `waiting on lock`).

| Tool | Exit codes read as an answer | Everything else |
|---|---|---|
| judge | 0 and 2 both carry the envelope on stdout (decided or escalated; `ask-check` swaps them: 0 is `ask` or an escalation, 2 is `continue`), so the envelope's own fields decide. It is read into `JudgeAnswer` through AwJson (the question's extra fields, `chosenIndex` and `reasons`, are ignored). An envelope with neither a decision nor `escalated: true` is refused | 1 (judge's error line), 64 (usage), any other exit, a binary that can't start, a timeout: SND-BLK-001 with judge's error line |
| ui-evidence | `doctor` 0 or 2 and `provenance` 0: the document on stdout. `run` and `parity` 0 or 2: the document written to the run directory (`summary.json`, `parity.json`), whose bytes are hashed for the digest (`sha256:<hex>`). `run` and `parity` take absolute paths only (a relative one is SND-BLK-002: the control plane's working directory isn't the caller's) | 1, 4, 75, 130 or any other exit, no document, a document that doesn't read: SND-BLK-001 with ui-evidence's line |

## The blocks

`AddSindriCore()` registers them as `IBlock` singletons (`sindri workflow blocks` lists them with their ports):

| Block | Kind | Rung | Capability | Inputs | Outputs | Passes when |
|---|---|---|---|---|---|---|
| `gate.evidence.doctor` | gate | deterministic | no | — | — | `ui-evidence doctor` says healthy; otherwise the detail lists its failures |
| `gate.evidence.provenance` | gate | deterministic | no | — | — | `ui-evidence provenance` says `seeded`; otherwise the detail gives its reason |
| `evidence.ui-browser` | evidence (provider) | — | no | `script`, `run-dir`, `host` (text) | — (the verdict and document come back from `collect`) | every step of the run passed |
| `evidence.design-parity` | evidence (provider) | — | no | `manifest`, `run-dir`, `host` (text) | — (as above) | every geometry check passed |
| `gate.judge.resolution-check` | gate | judge | yes | `resolution.brief`, `.expected`, `.actual`, `.root-cause`, `.check-kind`, `.check-summary`, `.diff-stat` (text), `.before-passed`, `.after-passed` (flag); optional `resolution.diff` | — | `resolved` |
| `gate.judge.rule-check` | gate | judge | yes | `rule.text`, `rule.hunk` | — | `fine` or `n/a` |
| `gate.judge.turn-progress` | gate | judge | yes | `progress.problem`, `progress.turn-diff` | — | `progressing` |
| `gate.judge.brief-scope` | gate | judge | yes | `brief.agent-type`, `brief.goal`, `brief.acceptance-criteria`, `brief.proof-command` | — | `ready` |
| `gate.judge.visual-critique` | gate | judge | yes | `visual.evidence-dir`, `visual.screenshot`; optional `visual.baseline` | — | `looks-right` |
| `gate.judge.ask-check` | gate | judge | yes | `ask.transcript-tail` | — | `continue` |
| `step.ui-run` | step | — | no | — | `visual.evidence-dir`, `visual.screenshot`, `visual.baseline` | (a declaration: the driver collects the evidence and reports these facts) |

- **Judge gates.** Each judge decision question that gates work is a rung-2 gate (§17.28.2). `wake-gate` (routing a message)
  and `ui-element-repair` (choosing a selector inside ui-evidence) answer no pass-or-fail question about work, so they aren't
  gates. A judge gate is **capability-bearing**: every evaluation runs judge, which may start an agent CLI on a model, so only
  an approved recipe may name it (the validator's `capability` check). The gate builds the question's input record from the
  instance's facts (a flag fact is `true` or `false`); a missing or mistyped required fact refuses without asking judge. A
  decided answer passes only on the question's passing decisions; an **escalation never passes**. The judge's confidence is
  the gate record's score, and its decision id is in the detail, for `judge why`. A judge that can't be run is an error, which
  refuses the transition too. Every text fact must be non-blank, so a blank `rule.hunk` refuses as a missing fact although
  judge's pre-rule would answer `n/a`: an empty hunk has nothing to check.
- **Evidence checks.** `doctor` and `provenance` are rung-1 gates: read-only probes of the local stack and database.
- **Evidence providers.** `run` and `parity` are evidence blocks that implement `IEvidenceProvider` (below). They aren't
  capability-bearing: they drive the local app headless and write only the run directory and the box's QA-stack and
  heavy-job lock slots (ui-evidence's host guard refuses any other host). A recipe requires their evidence the usual way, `evidence: [ui-browser@head]`.
- **Order.** The engine checks required evidence first, then runs the gates cheapest rung first and stops at the first
  failure (`docs/csharp/sindri-engine.md`). So a judge gate never runs when the evidence is missing or a deterministic gate
  refused (`UiEvidenceRecipeTests`).

## Evidence providers and `collect`

The S2 registry contract declares evidence kinds but has no way for a block to produce evidence. This stage adds, without
changing S2's types:

| Type (Core, `Aw.Sindri.Core.Blocks`) | Contract |
|---|---|
| `IEvidenceProvider : IBlock` | `ValueTask<Result<ProvidedEvidence>> Provide(EvidenceRequest request, CancellationToken ct)`; kind `Evidence`, id `evidence.<kind>`, so `BlockRegistry` accepts it as any evidence block |
| `EvidenceRequest` | `(InstanceId Instance, HeadSha Sha, IReadOnlyDictionary<string,string> Inputs)`; the inputs are exactly the names the block's `Inputs` declare, each of its type (else SND-BLK-002 naming every problem) |
| `ProvidedEvidence` | `(bool Passed, string Digest, string Document, string Detail)` |
| `EvidenceProviders` | `Find(kind)`: the registered block `evidence.<kind>` that implements `IEvidenceProvider`, or SND-BLK-003 listing the kinds that have one. It reads the registered `IBlock`s; `IBlockRegistry` has no provider lookup (S2 didn't add `Provider(BlockId)`; this class stands in for it) |

The `CollectEvidence` slice runs a provider **outside any engine transaction** (a run takes minutes; the ledger's write
lock is never held across it), then registers a **passing** run through the engine's `RegisterEvidenceCommand` (kind,
the request's sha, the document's digest). A failed run is reported and registers nothing. The kind must have a
provider, and the instance must exist and still be running (not done or cancelled), before the tool runs.

| Surface | Form |
|---|---|
| Route | `POST /sindri/workflows/{instance}/evidence/collect` `{kind, sha, inputs: {name: value}}` → `{kind, passed, detail, digest, document, registered}` (`registered` is the engine's outcome, null for a failed run) |
| CLI | `sindri workflow collect <id> <kind> <sha> [--input name=value]… [--json]`: the `script`, `manifest` and `run-dir` inputs are made absolute against the shell's directory before they're sent. Exit 0 registered, 1 the run failed (nothing registered) or the instance is held, 2 an error |

## The built-in recipe `workflow.ui-evidence`

The ui-evidence skill's sequence as a workflow (approved, built in): **stack** (wait) → `ready` through
`gate.evidence.doctor` → **run** (active, `step.ui-run`) → `ran` with `evidence: [ui-browser@head]` through
`gate.evidence.provenance` then `gate.judge.visual-critique` → **done**. `stack-down` goes back from run to stack (loop
`restack`, cap 3). Publishing (ask-first) is a later effect. As a file recipe (unapproved), the same YAML is refused for
its judge gate (`capability transitions[2].gates[1]`) and is valid without it.

The `ran` signal names the screenshot judge critiques (`visual.evidence-dir`, `visual.screenshot`); nothing yet checks
that it is the run whose digest was registered, so set them to the `run-dir` given to `collect`. Tying the two (the
provider reporting its run directory as a fact, or a gate that checks the digest) is a later change.

```bash
sindri workflow start workflow.ui-evidence ITEM-1
sindri workflow signal <id> ready                       # ui-evidence doctor
sindri workflow collect <id> ui-browser <sha> --input script=s.json --input run-dir=runs/r1 --input host=http://localhost:3000
sindri workflow signal <id> ran --sha <sha> --set visual.evidence-dir=$PWD/runs/r1 --set visual.screenshot=desktop-1-goto.png   # facts are sent as typed: absolute here
```

## Errors

| Code | Kind | When |
|---|---|---|
| SND-BLK-001 | unavailable | a block's tool couldn't be run (missing binary, timeout) or answered outside its CLI contract |
| SND-BLK-002 | invalid | an evidence provider was given inputs it doesn't declare, or missed one, or one of the wrong type, or a `run`/`parity` path that isn't absolute |
| SND-BLK-003 | not found | no registered evidence provider produces that kind |

## Checks

`tests/Aw.Sindri.Tests/Blocks/`: `JudgeGateTests` (each question's pass and fail decisions, escalation, missing facts,
every field of every input record from its own fact), `JudgeInputWireTests` (the exact JSON each record writes, the
field names judge reads), `ToolProcessTests` (SIGTERM first on a timeout or a cancel, SIGKILL after the grace for a tool
that ignores it, an immediate tree kill for a non-graceful call, the drain grace, an early stdin close, the error line),
`ToolBlockRegistrationTests`, `EvidenceGateTests`, `UiRunProviderTests`, the two contract suites against the fakes and
the process adapters, and `UiEvidenceRecipeTests` (the recipe validates approved and is refused unapproved; doctor,
collect, provenance and judge take an instance to done; an unsure judge refuses; missing evidence refuses before judge
or provenance runs). `CollectEvidence/CollectEvidenceTests` covers the slice; `Server/WorkflowApiTests` runs the whole
recipe over the API through scripted tool binaries; `scripts/tests/check-deps.test.sh` covers the deps scan reading C#,
which is how `check-deps` sees the two `cli` edges.
