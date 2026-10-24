# BugFix — `bugfix-state`

`bugfix-state` is the state helper the `/bugFixOrchestrator` skill drives a bug fix through. It owns the state file and
refuses every step the rules don't allow, so the skill can't skip a gate or edit evidence. It is the C# port of
`skills/bugFixOrchestrator/src` (spec §4a step 1): a reimplementation, not a translation. The TypeScript helper stays
installed until the switch, and nothing in `setup.sh` points at this build.

Source: `src/BugFix` (full shape: `Core`, `Features`, `Infrastructure`, `Host`; NativeAOT binary `bugfix-state`).
Tests: `tests/Aw.BugFix.Tests` (slices, rules, contract bindings, the CLI end to end) and `tests/Aw.BugFix.Testing`
(fakes and contract bases). AOT smoke: `bash src/BugFix/aot-smoke/run.sh`.

## What the skill relies on

The skill text is this CLI's only caller. It keeps working through these points; everything else is this package's own
design.

- Every command takes `--state <dir>`. After the switch the skill passes `bugfix/<repo-slug>/<ticket-slug>` (spec §4c).
- The commands and flags below.
- Exit 0 on success, 3 on a refusal. A refusal prints one stderr line starting with `refused:`, and the skill reports
  the reason.
- `run-test` and `run-ui` print `evidence`, the run record path the skill passes back to `advance reproduce` and
  `record-run`.
- `resume` prints `next`; `status` prints the state with `baseline.commit`.
- `judge` prints `decision`, `reasons` and `status` (an escalation is asked once more, then falls back to `unresolved`; the status stays `active`).

## Commands

| Command | Phase | Does |
|---|---|---|
| `init --state <dir> --ticket <ticket.json>` | (none, or finished) | Reads the ticket (`source` linear or text, `id`, `title`, `brief`, `expected`, `actual`; brief and expected not blank) and starts at `intake` |
| `advance investigate --evidence <handoff.md>` | intake → investigate | Snapshots the `/rootCause` handoff: status `diagnosed`, a `## Root Cause` section, and a `confirmed` hypothesis or at least two hypotheses not ruled out (without a confirmed one the first attempt must be mode B) |
| `run-test --check <file> [--cwd <dir>] -- <command…>` | any | Runs the test command on a clean tree and registers a run record (with the commit and the working directory) |
| `run-ui --check <script.json> [--cwd <dir>]` | any | Runs the ui-evidence script on a clean tree and registers a run record |
| `advance reproduce --evidence <run> --check <file> [--cwd <dir>]` | investigate → reproduce | Freezes the check and the failing baseline |
| `start-attempt --mode <A\|B\|C> [--abandon-judge]` | reproduce, evaluate → fix | Opens the next attempt (at most 5); `--abandon-judge` leaves behind a candidate whose judge question was asked twice without an answer, once no judge process is running |
| `record-candidate --branch <b> [--cwd <dir>] [--hypothesis <n>] [--allow-test-changes [--reason <why>]]` | fix, evaluate | Records the worktree's commit as the next candidate (`c1`, `c2`, …) |
| `record-run <candidate> --evidence <run>` | fix, evaluate → evaluate | Records the frozen check's run against the candidate |
| `judge <candidate>` | evaluate | Asks judge resolution-check once and records the verdict |
| `advance report (--candidate <id> \| --unresolved)` | → report | Finishes resolved (only with a passing run and a `resolved` verdict) or unresolved |
| `status` | any | Prints the whole state |
| `resume` | any | Prints `phase`, `status`, `attempt` and the `next` action |

`--state`, `--ticket`, `--evidence` and `--cwd` resolve against the process's working directory; `--check` resolves
against `--cwd` (the checkout the check lives in). The argv after `--` is kept as given.

## Threat model

The gates guard against an agent's **honest mistakes and shortcuts**: editing or weakening the check, running a
different command or program, recording a run against the wrong commit, a dirty or stale work tree, re-rolling a judge
verdict, skipping a phase. bugfix-state runs in a private software factory, under the skill's guidance, so a
**deliberately adversarial agent** that crafts commands or files to slip past a rule (inline code glued to its flag,
interpreter modes that don't run the script, a planted file a loader picks up) is out of scope. The rules below refuse
the common forms of those tricks where that was cheap, but they aren't a sandbox; the judge's resolution check and code
review of the fix are the backstop. A review finding that needs a crafted exploit is answered against this section,
not with another rule.

## The rules

- **Evidence is ours.** `advance reproduce` and `record-run` accept only run records that `run-test` or `run-ui` wrote for
  this bug fix and that are unchanged since (path and SHA-256 registered in the state).
- **The check reproduces the bug.** The baseline run must fail at the checkout's HEAD, on the committed check file. A
  passing check can never prove a fix, and a **broken** run is not a reproduction: a test command that couldn't start or
  was killed at the time limit, or a ui run with broken steps and no failed one (a bad selector).
- **The check is frozen.** Every later run must execute the same check file (same SHA-256); a test run must use the
  same argv **in the same working directory** (relative to the repository root) as the baseline; its program (argv[0],
  looked up once against the run's directory or PATH, started by the path found so a venv's python or an argv[0]
  shim keeps its name, and hashed by that path's real file) must be the same file (same SHA-256); and the variables
  that decide which code runs must have the same values (a refusal names the ones that differ). `run-test` refuses a command that doesn't name the check by a
  path that resolves to it from the working directory (a same-named file elsewhere doesn't count; paths resolve like
  realpath(3), links before `..`), a non-runner (`grep`, `cat`, `true`, …), a program that runs another one (`env`,
  `xargs`, `timeout`, `nice`, `nohup`, `sudo`, …), and inline code, by interpreter: shells and python `-c`, node/deno/bun
  `-e`/`-p`/`--eval`/`--print`, perl and ruby `-e`/`-E`, php `-r`, `deno eval` (anywhere in a short-flag cluster, so
  `bash -xec` counts and `bash -e` doesn't; ruby and perl value flags such as `-Itest` don't). Only the interpreter's
  own options count: they end at the script, at python's `-m` module, or at `--`, skipping the values of known options
  (`-X dev`, `-r ./x.js`, `-o pipefail`), so `python -m pytest -c pytest.ini` and `node t.js -e` are fine. The scan fails
  closed: after an option not known to take no value (`--check-hash-based-pycs`, `--redirect-warnings`), the next
  argument is taken for that option's value, not for the script, and the scan goes on, so such an option can't hide a
  later `-c`/`-e` (a rare false refusal: write the option as `--name=value`). A package runner (`npx`,
  `pnpx`, `bunx`, `uvx`, `npm exec|x`, `pnpm exec|dlx`, `yarn exec|dlx|node`, `bun x`, `uv run`, `uv tool run`,
  `poetry run`, `bundle exec`) is looked through: the first program past its verb and options is classified the same
  way, and every later argument is checked for an interpreter given inline code. Before the program (and before the
  verb) only these runner options may appear: for every runner `-y`, `--yes`, `--no`, `-q`, `--quiet`, `-s`,
  `--silent`, `--offline`, `--prefer-offline`, `--no-install`, `--bun`, `--frozen`, `--locked`, `--no-sync`,
  `--isolated`, `--all-extras`, `--no-dev`; and each runner's own: npm/npx `-p`/`--package`, `-w`/`--workspace` (with a
  value); pnpm/pnpx `--filter`/`-F` (with a value) and `-w`/`--workspace-root` (without); bun/bunx `-p`/`--package`;
  uv/uvx `--with`, `--python`, `-p`, `--extra`, `--group`, `--package`, `-m`/`--module` (with a value). The same letter
  differs between runners (npm `-w web` names a workspace; pnpm `-w` is the workspace root and takes none). A runner that is itself an interpreter (`bun`)
  has its inline flags refused before its verb (`bun -p` is print) but not after it (`bun x -p` is a package). Any other is refused: an
  unknown option could take the next argument as its value and hide the real program behind it, and some change what
  runs (`-c`/`--call`/`--shell-mode`, `--node-options`, `--env-file`, a different directory). A runner subcommand that
  isn't its runner verb or a script verb (`test`, `run`, `run-script`) is classified as the program it runs, so
  `pnpm grep …` is a non-runner.
- **The caller's environment can't steer it.** The check runs with only an allowlist of variables from bugfix-state's
  own environment. `PATH`, `HOME` and the names listed in `AW_BUGFIX_CHECK_ENV` (for a test that needs `DATABASE_URL`,
  `JAVA_HOME`, …) decide which code runs, so their values are frozen at the baseline and a later run with other values
  is refused. State and run records keep each one as a keyed digest (`hmac-sha256:` of the name, a NUL and the value, keyed with
  the bug fix's random `digest.key`, owner-only, beside `state.json` and never in it), never the value, since it may
  hold a credential; `status` shows the digests. A name listed in `AW_BUGFIX_CHECK_ENV` that also
  passes through (`TZ`, `LANG`, …) is frozen; `USER`, `LOGNAME`, `SHELL`, `TMPDIR`, `TERM`, `CI`, `LANG`, `LANGUAGE`, `TZ` and `LC_ALL`, `LC_CTYPE`,
  `LC_MESSAGES`, `LC_COLLATE`, `LC_NUMERIC`, `LC_TIME`, `LC_MONETARY` pass through as they are. Everything else
  (`BASH_ENV`, `NODE_OPTIONS`, exported shell functions, `npm_config_*`, `PYTEST_ADDOPTS`, …) is dropped, and
  `AW_BUGFIX_CHECK_ENV` can't add those. Git runs without `GIT_*` variables, and bugfix-state refuses to change state
  while `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` is set. Residual risk: what lives outside the repository isn't frozen
  beyond that: installed tools other than the check's own program, files under `HOME` (`~/.npmrc`, shell startup
  files), and dependencies in gitignored directories (`node_modules`, `.venv`).
- **A clean tree.** `run-test`, `run-ui`, `record-candidate` and `record-run` refuse a work tree with an uncommitted
  change, or with an untracked file (not ignored) that no registered run left behind: a new fixture or a module that
  shadows another could change what the check does while the commit stays the same. Each test run record keeps the
  work tree it ran in (`checkout`) and the untracked files that appeared or changed during the run (`leftovers`:
  `.coverage`, a junit report, with their SHA-256). A later step accepts such a file only if a run **of the frozen check**
  (one `record-run` would take for the same command: same check file, command, directory, program and environment)
  left it **in that same work tree**, and it is unchanged since. Before the check is frozen, leftovers of any test run
  in the same work tree count. Either way an untracked file that could change what runs (a protected file, a test or
  fixture path, or one a loader picks first; see below) is never output, whoever wrote it, and is refused with its
  own message: delete it, gitignore it (`__pycache__/`; or run python with `-B`, or name `PYTHONDONTWRITEBYTECODE` in
  `AW_BUGFIX_CHECK_ENV`), or have the check write its reports outside its own directory. Any other untracked file is
  refused as left by no run of the frozen check (AW-BUGFIX-020 for both). Residual risk: a file the check would load that no rule
  names (a conditionally sourced helper outside the test paths), written by the agent while a run of the frozen check
  is going, is accepted as that run's output.
- **Candidates are real changes on the baseline:** a clean worktree on the named branch, with commits on top of the
  baseline commit and a tree different from it, not already recorded. A fix that touches the check, its directory,
  tests, fixtures, snapshots, test-runner config, or a repository file the frozen command names (its runner
  or wrapper script; a script or config file named in an option, `--config=vt.ts`), every file a loader would try for a
  module specifier (`--require=`, `-r`, `--import=`, `--loader=`, `--setupFiles=` values and positional `./x`: the path
  itself, then `.js`, `.json`, `.node`, `.py`, `/package.json`, `/index.js`, `/index.node`, `/__init__.py`, …, whether or
  not it exists yet, so `lib/setup` or `lib/setup.node` added ahead of `lib/setup.js` counts), every file Python could import for a `-m`/`-p` module from the run's directory (each dotted
  prefix's `.py`, `/__init__.py`, `/__main__.py`), or a test-runner config the command reads without naming it (`noxfile.py`, `pytest.ini`, `.pytest.ini`, `pytest.toml`,
  `tox.ini`, `pyproject.toml`, `setup.cfg`, `conftest.py`, `.rspec`, `.runsettings`, jest/vitest/babel/mocha config, …) needs
  `--allow-test-changes`; the orchestrator passes it with `--reason <why>`, which goes into the history. Build and lock files
  (`package.json`, `Makefile`, `go.mod`, `Cargo.toml`, `Directory.Build.*`, `tsconfig*`, …) need no flag: the frozen command and its environment
  digest still decide what the check runs. So does adding a file a loader picks ahead of existing code: a Python module (`x.py`, `x/__init__.py`)
  directly in the run's directory or the check's directory (both come first on `sys.path`, so a new `argparse.py`
  shadows the standard library), compiled or native code anywhere (`.pyc`, `.pyo`, `.pyd`, `.so`, `.dylib`, `.node`,
  `.pth`), `sitecustomize.py`/`usercustomize.py`, and anything under `node_modules/`. A baseline command that names a positional path (written with `/` or a leading `.`, or with a script or
  config extension) or a script or config file in an option, that doesn't exist as a file or directory, is refused
  (AW-BUGFIX-008): a fix could add it. `advance reproduce` freezes the check only from a
  clean checkout of the baseline commit (no index or working change, no stray untracked file), and a file the command
  names that a run of the same command created in that work tree (its report, `--junitxml=out/r.xml`, also when a re-run
  wrote the same bytes again) is its output, not a command file; any other named file is protected, including one an
  earlier run of another command left untracked. Option values that aren't files (URLs, `/regexes/`, output files such as
  `--junitxml=out/r.xml`, names such as `--cov=app`) are neither required nor protected, and globs and package patterns
  (`./...`) aren't paths.
- **Attempts.** Mode A one candidate, C one candidate (split by area, in sequence), B two candidates on different open
  hypotheses, after a failed attempt or as the first attempt when no hypothesis is confirmed, and with at least two hypotheses not ruled out. A new attempt waits until
  every recorded candidate of the current one is evaluated and none resolves the ticket. While a mode-B attempt has
  one candidate, `resume` points at the second (and says to start the next attempt if its implementer produced
  nothing usable).
- **One verdict per candidate.** `judge` builds the input from state (the ticket verbatim, the snapshotted root cause, a
  description of the frozen check, the diff stat and the diff without lock files (`package-lock.json`, `*.lock`,
  `pnpm-lock.yaml`, `packages.lock.json`) and `dist/` output, capped at 1,000,000 characters), writes it to
  `judge-<candidate>.json`, marks the candidate as asked, asks judge about that file without holding the lock, and
  records the answer only when it names the judge's input digest of that file, the bug fix is still in `evaluate`, and the candidate
  belongs to the current attempt (a call that outlives an `--abandon-judge` records nothing). A verdict is never
  re-asked. A call interrupted after asking leaves the mark, and `resume` says to ask once more (one automatic retry);
  after a second unanswered call `judge` is refused and `resume` tells the orchestrator to leave the question behind with
  `start-attempt --mode <A|B|C> --abandon-judge` (recorded in the history) once no judge process is running, or to report
  unresolved; without that flag the candidate isn't evaluated, so a call still running isn't overtaken. If judge gives
  no usable verdict (or one about another input) the mark is cleared and nothing is recorded, so a retry is not a
  re-roll; the input file stays as a record of what was asked. An escalation is never recorded as a verdict: the first
  is followed by `resume` saying to `judge` the candidate again; the second records an `unresolved` verdict with reason code
  `escalated:<code>`, so the bug fix continues (next attempt or `advance report --unresolved`) with no human step, and the
  candidate can never resolve the ticket.
- **One writer at a time.** Every state change holds the state directory's lock (`state.lock`) from load to save; a
  long `run-test`, `run-ui` or `judge` takes it only to record its result onto the current state. A call that can't get
  it within `AW_BUGFIX_LOCK_WAIT_SECONDS` (default 30) is refused with AW-BUGFIX-021 and changes nothing. Only `init`
  creates the state directory; any other command on a missing one is AW-BUGFIX-002 and creates nothing.

## Output, exit codes and errors

Success prints one JSON document (the Kernel `AwJson` profile: camelCase, enums as their names) on stdout, exit 0.
Everything else is one line on stderr ending with the catalogue code, and nothing on stdout.

| Exit | Line | When |
|---|---|---|
| 0 | (JSON on stdout) | Success |
| 1 | `error: <message> (AW-BUGFIX-NNN)` | Invalid input or not found (`ErrorKind.Invalid`, `NotFound`) |
| 2 | `usage: <problem>` then the usage | Unknown command or option, a missing or empty option value, malformed `--mode`, `--hypothesis` or candidate id |
| 3 | `refused: <reason> (AW-BUGFIX-NNN)` | A rule refuses the step; judge or ui-evidence is unavailable; the state is busy; a file can't be written or read |

| Code | Kind | Meaning |
|---|---|---|
| AW-BUGFIX-001 | invalid | An argument or input file failed validation (a bad ticket, both or neither of `--candidate`/`--unresolved`) |
| AW-BUGFIX-002 | notFound | No state in the state directory: run `init` first |
| AW-BUGFIX-003 | invalid | The state file isn't a BugFix state of this schema |
| AW-BUGFIX-004 | invalid | An input file (ticket, handoff) can't be read |
| AW-BUGFIX-005 | conflict | The step isn't allowed in this phase, or an unfinished bug fix exists at `init` |
| AW-BUGFIX-006 | conflict | The handoff doesn't support a fix |
| AW-BUGFIX-007 | conflict | The baseline run doesn't reproduce the bug |
| AW-BUGFIX-008 | conflict | The check isn't committed, is outside the repository or changed since the baseline |
| AW-BUGFIX-009 | conflict | The evidence isn't a registered, unchanged run record |
| AW-BUGFIX-010 | conflict | No new attempt now |
| AW-BUGFIX-011 | conflict | The candidate can't be recorded |
| AW-BUGFIX-012 | conflict | The test command could pass without running the check |
| AW-BUGFIX-013 | notFound | Unknown candidate |
| AW-BUGFIX-014 | conflict | The run can't be recorded for the candidate |
| AW-BUGFIX-015 | conflict | The candidate can't be judged (no passing run, already judged) |
| AW-BUGFIX-016 | unavailable | judge gave no usable verdict; nothing recorded. The orchestrator retries with backoff (3 tries), then `advance report --unresolved`; no park |
| AW-BUGFIX-017 | unavailable | ui-evidence produced no usable run; nothing registered. Same retry then `advance report --unresolved` |
| AW-BUGFIX-018 | invalid | A git command failed (not a repository, unknown revision) |
| AW-BUGFIX-019 | conflict | The report step isn't allowed (already reported, candidate not eligible) |
| AW-BUGFIX-020 | conflict | A working tree has uncommitted changes, an untracked file no run of the frozen check left behind, or an untracked file that could change what runs |
| AW-BUGFIX-021 | conflict | Another bugfix-state call holds the state directory; retry when it finishes |
| AW-BUGFIX-022 | unavailable | A state, run or log file can't be written or read |
| AW-BUGFIX-023 | unavailable | The bug fix's `digest.key` is missing or malformed, so environment digests can't be made or compared |

## Files

Everything lives under the `--state` directory; the C# build never reads TS-written state (spec §4c).

- `digest.key`: the bug fix's random HMAC key for the environment digests (64 hex characters, owner-only), made by `init`
  before the state (written to a temporary file and moved into place; `init` keeps a valid key and replaces a missing or
  malformed one, since no digest uses it yet). `run-test` and `run-ui` refuse with
  AW-BUGFIX-023 when it is missing or malformed instead of making a new one: the digests in `state.json` and the run
  records are only comparable under the key they were made with.
- `state.json`: the state (`schemaVersion` 1), written to a temporary file and renamed, under `state.lock`. Fields: `ticket`, `phase`,
  `status`, `attempt`, `mode`, `investigation` (`handoff`, `rootCause`, `hypotheses`), `check` (`kind` test or ui,
  `path`, `sha256`, `command`, `directory`, `programSha256`, `environment` (digests), `commandFiles`), `baseline` (`evidence`, `commit`), `candidates` (`id`, `attempt`, `mode`, `branch`,
  `worktree`, `commit`, `hypothesis`, `changedFiles`, `run`, `verdict`, `judgeAsked`), `runs` (`evidence`, `sha256`), `resolvedBy`,
  `history`.
- `runs/<stamp>-<test|ui>-<n>.json`: a run record (`kind`, `outcome` passed, failed or broken, `commit`, `directory`,
  `checkSha256`, `command`, `programSha256`, `environment` (keyed digests), `exitCode`, `output`, `leftovers`, `checkout`); a ui run that produced nothing gives its slot back, with its `.log` (and, for ui, the ui-evidence run directory `<stem>/`).
- `judge-<candidate>.json`: the resolution-check input asked about the candidate.

## Ports and adapters

| Port (Core) | Fake (`Aw.BugFix.Testing`) | Adapter (`Infrastructure`) | Contract base |
|---|---|---|---|
| `IStateStore` (load, save, exclusive lock) | `InMemoryStateStore` | `FileStateStore` (`state.lock`, `FileShare.None`) | `StateStoreContract` |
| `IRunLedger` | `InMemoryRunLedger` | `FileRunLedger` | `RunLedgerContract` |
| `IGit`, `IFiles` | `InMemoryRepos` (one fake: git answers about the files the file port reads) | `GitProcess` (git on PATH), `LocalFiles` | `ReposContract` |
| `ICheckRunner` (returns the exit code and whether it ran to a verdict) | `ScriptedCheckRunner` | `ProcessCheckRunner`: output streamed to the log (16 MB cap); killed after `AW_BUGFIX_CHECK_TIMEOUT_SECONDS` (default 1800); a run that exits keeps its exit code even if background children still hold its output (output after the exit is dropped after 5 s, noted in the log; those children aren't killed: they left the process tree); a run killed at the limit or that couldn't start didn't run to a verdict (broken); runs without loader variables | `CheckRunnerContract` |
| `IJudge` (asks about the written input file; the answer names the judge's 16-hex input digest) | `ScriptedJudge` | `JudgeCli` (`judge resolution-check`, then `judge why`; `AW_JUDGE_BIN` else PATH; 5 minutes each) | `JudgeContract` |
| `IUiEvidence` | `ScriptedUiEvidence` | `UiEvidenceCli` (`ui-evidence run <script> <run-dir> --app-build <commit>`; `AW_UI_EVIDENCE_BIN` else PATH) | `UiEvidenceContract` |

**The judge and ui-evidence adapters** reach those tools through their CLIs (`deps.yaml` cli edges; cutover spec §4.3).
`JudgeCli` writes `judge-<cand>.json` in the judge's canonical form (compact, `ui` sent as `ui-evidence`), pipes it to
`judge resolution-check` (exit 0 decided, 2 escalated, anything else AW-BUGFIX-016), and asks `judge why <id>` for the stored
`input_digest`. The input digest is the first 16 hex of the SHA-256 of that canonical form; the helper refuses an answer about any
other input, so an agent can't forge or re-roll a verdict. `UiEvidenceCli` runs the script and reads `summary.json`: exit 0 passed,
2 a failed check, 4 broken, anything else AW-BUGFIX-017. Both read `AW_STATE_DIR` (and `AW_JUDGE_STATE_DIR`) as the hooks do, so it must match.
