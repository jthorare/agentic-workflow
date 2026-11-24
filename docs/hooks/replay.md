# Hook replay: fixtures, report and policy mode

Issue #4257. This page is the contract for the replay harness. The runner, the sanitizer, the report and the fixture sets
build on it. A hook that can deny a tool call must pass replay before it blocks anyone.

## Words used here

- **Case**: one fixture file. It holds one session's hook events for one provider and what each event should decide.
- **Decision**: what one hook call did.
  - `allow`: the hook printed nothing and let the call run.
  - `advise`: the hook let the call run and printed text for the agent.
  - `deny`: the hook exited 2, or printed the host's deny shape.
  - `abstain`: the hook or the judge could not decide. It let the call run and wrote one `HOOK-FAILOPEN` line.
- **Wrong block**: the hook said `deny` where the case expects `allow` or `advise`.
- **Missed block**: the hook said `allow` or `advise` where the case expects `deny`.
- **Missed advisory**: the hook said `allow` where the case expects `advise`.
- **Recorded playback**: the judge's answers come from a file of earlier answers. No model runs.
- **Fresh run**: the real judge calls a real model. CI never does this.

## Fixture format

Schema: `schemas/replay-fixture.schema.json` (JSON Schema 2020-12, Ajv2020).

Path: `tests/fixtures/replay/<provider>/<scenario>/<case>.json`. The provider is `claude`, `codex` or `cursor`. A
fixture is at most 64 KB. Its `id` is the path under `tests/fixtures/replay/` without `.json`.

```json
{
  "schema": "aw-replay-fixture/1",
  "id": "claude/retry-intentional/gh-after-login-01",
  "provider": "claude",
  "scenario": "retry-intentional",
  "verified": "captured",
  "source": {"hostVersion": "claude-code 2.1.4", "capturedAt": "2026-11-20",
             "sanitizedBy": "judge replay sanitize 1", "reviewedBy": "joi", "reviewedAt": "2026-11-24"},
  "policy": "retry-guard/1",
  "world": {"commandsOnPath": ["git"], "files": ["__ROOT__/src/a.cs"], "authFileMtime": {"github.com": 1000}},
  "events": [
    {"n": 1, "hook": "PostToolUseFailure", "script": "retry-record.sh", "payload": {"tool_name": "Bash"}},
    {"n": 2, "world": {"authFileMtime": {"github.com": 2000}}},
    {"n": 3, "hook": "PreToolUse", "script": "retry-gate.sh", "payload": {"tool_name": "Bash"}},
    {"n": 4, "reset": "session"}
  ],
  "judge": {"verdicts": "verdicts/retry-intentional.jsonl"},
  "expect": [{"event": 3, "decision": "allow", "reasonCode": "prerequisite-changed", "textContains": null}]
}
```

### Top-level fields

| Field | Meaning |
|---|---|
| `schema` | Always `aw-replay-fixture/1`. A breaking change bumps the number. |
| `provider` | `claude`, `codex` or `cursor`. It picks the adapter. |
| `scenario` | The scenario id, from the table below. |
| `verified` | `captured`: every payload came from a real run of `source.hostVersion`. `unverified`: the shape comes from docs or another host. |
| `source` | Where the case came from and who read it. The sanitizer writes `reviewedBy: null`. A merged case names its reviewer. |
| `policy` | The policy id this case scores, a key of `config/policy/interventions.json`. |
| `pendingHook` | Optional. The issue and script of a hook that is not on main yet, such as `#4221 duplicate-script.sh`. |
| `world` | What the temp root holds before event 1. |
| `events` | The steps, in order. Each `n` is unique and ascending. |
| `judge` | The recorded judge answers for this case, or `null` when the case expects no judge call. |
| `expect` | One row per hook event the case scores. |

An `unverified` case runs and prints a report row. It never counts toward promotion. Codex and Cursor failure payloads
stay `unverified` until someone captures them on a real host.

A case with `pendingHook` still runs. The report counts each of its rows as `notCovered`, never as correct or wrong.
The fixture builder removes `pendingHook` in the PR that follows the hook's merge.

### Events

There are three kinds of event. Each has `n`.

1. A **hook event** has `hook`, `script` and one payload field.
   - `hook` is the host's own event name, as in its hook config: `PreToolUse` for Claude and Codex,
     `beforeShellExecution` for Cursor.
   - `script` is the canonical hook script in `config/hooks/`, such as `retry-gate.sh`.
   - `payload` is the host's native JSON. Codex payloads carry `turn_id`. Cursor payloads carry `conversation_id` and a
     camelCase event name. So the adapter runs too, not only the canonical script.
   - `payloadRaw` is stdin as text. Use it for a malformed payload: empty, not JSON, or cut short.
   - `payloadBase64` is stdin as bytes. Use it for bad UTF-8.
   - `padBytes` (with `payload` only) adds a field `_pad` with that many `x` characters. That makes an oversized payload
     that is still valid JSON.
   - `advanceClock` moves the fake clock forward by that many seconds first. 86400 reaches the 24 h sweep.
2. A **world event** has `world`. Its fields apply on top of the current world: a tool appears, an auth file changes.
3. A **reset event** has `reset`.
   - `session`: later payloads get a new session id. The old session's retry state must not apply.
   - `corrupt-state`: the runner overwrites the hook's state file with bytes that are not JSON.

### World

`world` is a description, not state. The runner builds it in a fresh temp directory per case.

- `commandsOnPath`: names the runner puts on `PATH` as empty stubs that exit 0.
- `files`: empty files the runner makes. Each path starts with `__ROOT__/` and has no `..` part.
- `authFileMtime`: host name to the mtime, in epoch seconds, of that host's auth file under the temp `HOME`.

The runner replaces `__ROOT__` with the temp root in every payload string. That is the same trick as
`__TRANSCRIPT__` in `config/hooks/tests/fixtures/`.

### Expectations

| Field | Meaning |
|---|---|
| `event` | The `n` of a hook event. |
| `decision` | `allow`, `advise`, `deny` or `abstain`. |
| `reasonCode` | The reason code the hook reports, or `null` for any. |
| `textContains` | A substring the hook's text must hold, or `null`. The text is stderr, or the host's reason field. |
| `unsupported` | `true` when the host has no mapping for this event (`config/hooks/adapters/README.md`). The decision is then `allow` or `advise`, and the report row says `unsupported`. |

### Sanitizing and review

1. Raw transcripts stay in `$AW_STATE_DIR/replay/raw/`, outside the repo. The runner refuses a fixture under it.
2. `judge replay sanitize <transcript.jsonl> --provider P --out DIR` writes a draft. It redacts secrets with
   Aw.Redaction, rewrites `/home/<user>` to `/work` and repo names to `repo`, drops emails and host names, and cuts tool
   output to 200 characters. It sets `reviewedBy: null`.
3. A person reads the draft and fills `reviewedBy` and `reviewedAt`. The PR diff is the review.
4. `scripts/check-replay-fixtures.sh` checks what the schema can't. The id equals the path, and `n` is ascending.
   Each `expect` row names a hook event. `reviewedBy` holds a name, and the file fits the size cap. No secret, home
   path, email or cloud key appears.

## Replay as data

Runner: `scripts/replay/run-hooks.sh <fixtures-dir|file>... --out results.jsonl [--strict]`.

Per case, the runner makes a temp `HOME`, a temp `AW_STATE_DIR` and the temp root. For each hook event it pipes the
payload on stdin to the real hook. Claude cases run the script itself. Codex and Cursor cases run
`config/hooks/adapters/<provider>.sh config/hooks/<script>`. A command inside a payload is only a JSON value the hook
reads. Nothing runs it.

The runner holds each case in a box. Each rule has a test in the runner's suite.

- `PATH` is the canary shims, then the world's stubs, then `/usr/bin:/bin`.
- The canary shims are `gh`, `curl`, `claude`, `codex`, `cursor-agent`, `git` and `ssh`. Each writes
  `$ROOT/CANARY` and exits 99. A case fails when `CANARY` exists after it.
- A case whose command is `touch __ROOT__/PWNED; curl http://example.invalid` leaves no `PWNED` and no `CANARY`.
- The runner unsets `ANTHROPIC_API_KEY` and `OPENAI_API_KEY`, and points `AW_JUDGE_JEV_URL` at a dead loopback port.
- The runner exports `AW_REPLAY=1`.

### The fake judge

1. `scripts/replay/fake-judge.sh` sits first on `PATH`, and `AW_JUDGE_CS_BIN` points at it. A hook that calls `judge`
   reaches the shim, never the C# binary.
2. The shim hashes stdin (sha256) plus the question name. It looks the key up in the case's verdicts file and prints
   that answer in the real envelope, with the real exit code (`docs/csharp/judge.md`).
3. No key: the shim prints the "judge unavailable" shape and counts one `verdictMissing`. A `--strict` run fails on
   any `verdictMissing`. CI runs strict.
4. A verdicts file is JSONL. Line 1 is a header:
   `{"header":{"question","model","promptSha256","judgeVersion","recordedAt","source":"fresh"}}`.
   Each other line has the `EvalRecordLine` shape of `judge eval --record`.
5. `judge replay report` refuses, exit 1, when a question's prompt hash differs from its header. A stale recording
   can't pass.
6. `judge replay record <fixtures> --out verdicts/` is the fresh run. It spends model usage. Each run states its
   estimated usage to meta first, and meta approves that run. CI never runs it.

## Report

Two programs, joined by a file.

1. `run-hooks.sh` writes `results.jsonl`, one line per `expect` row:

```json
{"fixture": "claude/retry-intentional/gh-after-login-01", "provider": "claude", "scenario": "retry-intentional",
 "verified": "captured", "event": 3, "hook": "PreToolUse", "decision": "allow", "reasonCode": "prerequisite-changed",
 "exit": 0, "hookMs": 14, "judgeCalls": 0, "judgeSource": "none", "failopen": false}
```

   `judgeSource` is `recorded`, `fresh`, `none` (no call) or `missing` (a call with no recorded answer).

2. `judge replay report --expected <fixtures-dir> --actual results.jsonl [--verdicts DIR] [--json|--md]` scores it.
   `--md` prints the same rows as a table for a PR body.

```json
{"policy": "retry-guard/1", "fixtures": 82,
 "rows": [{"provider": "claude", "scenario": "retry-intentional", "verified": "captured", "cases": 6, "correct": 6,
           "wrongBlock": 0, "missedBlock": 0, "missedAdvisory": 0, "abstain": 0, "notCovered": 0, "unsupported": 0,
           "judgeCalls": 2, "judgeSource": "recorded", "model": "...", "promptSha256": "...",
           "hookMsP50": 14, "hookMsP95": 31, "judgeMsP50": 1900}],
 "totals": {"wrongBlock": 0, "missedBlock": 1, "abstain": 2}, "exit": 0}
```

There is one row per provider and scenario. Model usage is the judge call count, the model and the latency
percentiles. Tokens appear only when a recorded line carries them.

Report exit codes:

| Exit | Meaning |
|---|---|
| 0 | The report is out and no `captured` case has a wrong block. |
| 1 | A wrong block on a `captured` case, a stale prompt hash, or (strict) a missing verdict. |
| 2 | A tool error. |

## Scenarios

| Scenario | What it tests | Expected |
|---|---|---|
| `duplicate-script` | The same script again, nothing changed (#4221) | `deny` or `advise`. `pendingHook` until its hook lands. |
| `retry-intentional` | The same call with `# aw-retry: <reason>` | `allow`, override recorded |
| `retry-changed-prereq` | `rg` missing, then installed; a path missing, then made | `allow` after the change |
| `retry-transient` | Timeout, 503, DNS failure | Never `deny`; `advise` with backoff |
| `polling` | The same call, new output each time, then success | `allow` every time, nothing recorded |
| `auth-expired` | 401 twice, then the auth file changes | `deny` once, then `allow` after the refresh |
| `research-no-edit` | Many reads and greps, zero edits | `allow` |
| `verify-no-edit` | Repeated test or build runs, zero edits | `allow` |
| `scope-exception` | An exception the brief accepts | `allow` |
| `malformed-payload` | Empty, not JSON, cut short, no `tool_name`, wrong `tool_input` type, 2 MB, bad UTF-8 | `abstain` |
| `unsupported-hook` | An event the host has no mapping for | `allow` or `advise`, `unsupported: true` |
| `judge-unavailable` | No recorded answer; the shim says unavailable | `abstain`, text "judge unavailable", never `deny` |
| `redaction` | A planted `ghp_` token in the command and output | The token is absent from state, results and report |
| `state-reset` | A new session id, a corrupted state file, the 24 h sweep | Old problems never match; `allow` |

Every scenario with an intervention has at least one `allow` case and one intervention case. So a hook that denies
everything can't pass.

## Policy mode

`config/policy/interventions.json` holds each policy's mode, its promotion criteria and its evidence. It is in git, so
every change is a reviewed diff.

```json
{"schema": 1, "policies": {"retry-guard/1": {"mode": "advisory", "promotion": {"...": "..."}, "evidence": null}}}
```

### Policy ids and their names

A policy id is `<name>/<version>`. The name is lowercase letters, digits and single hyphens. The version is a whole
number. A classifier change bumps the version, so the new id starts in `advisory` again.

One rule maps the id to its two override names:

| Id | Env var | Mode file |
|---|---|---|
| `retry-guard/1` | `AW_POLICY_RETRY_GUARD_1_MODE` | `$AW_STATE_DIR/policy/retry-guard-1.mode` |
| `<name>/<version>` | `AW_POLICY_` + the id in uppercase with `-` and `/` as `_` + `_MODE` | the id with `/` as `-`, then `.mode` |

### Reading the mode

`config/lib/policy.sh` defines `policy_mode <policy>`. It prints `off`, `advisory` or `block`. The first source with a
value decides:

1. The env var `AW_POLICY_<NAME>_MODE`.
2. The mode file `$AW_STATE_DIR/policy/<name>.mode`. `$AW_STATE_DIR` defaults to `~/.agentic-workflow`.
3. The policy's `mode` in `interventions.json`. The reader looks for it in `../policy/` next to the lib: `config/policy/`
   in the repo, `<hooks>/policy/` when installed.
4. `advisory`.

An empty env var counts as not set. So does a mode file with only spaces. These all read as `advisory`:

- a set value that is not `off`, `advisory` or `block` (the reader doesn't fall through to the next source);
- a policy id that is not `<name>/<version>`, and an id that is not in the JSON;
- a missing or broken JSON file, and no `jq` on `PATH`;
- a JSON `mode: block` whose `evidence` is `null`.

The reader never writes, never prints to stderr and always returns 0.

### Modes

- `off`: the hook does nothing for this policy.
- `advisory`: a would-be deny prints as advice. The hook counts it in its state file as `counters.wouldDeny`. That
  gives live evidence of wrong blocks before any block ships.
- `block`: the hook denies. Only the JSON with evidence, or the owner's mode file or env var, can set it.

Rollback: `echo off > $AW_STATE_DIR/policy/retry-guard-1.mode`. The next hook call reads it. There is no reinstall
and no restart.

### Promotion

A policy ships with `mode: advisory` and `evidence: null`. It moves to `block` only when replay meets the criteria in
its `promotion` block. For `retry-guard/1`:

1. At least 40 `captured` cases, at least 5 in each of its scenarios, from at least 2 providers.
2. 0 wrong blocks on `retry-intentional`, `retry-changed-prereq`, `polling`, `research-no-edit`, `verify-no-edit` and
   `scope-exception`.
3. At least 80% of expected denies caught.
4. At least 14 days live in advisory, with `wouldDeny` read by a person: a sample of 20, or all when there are fewer.
   The owner can shorten the 14 days.
5. Every `judge-unavailable` and `malformed-payload` case passes.

`judge replay promote-check <policy> --report report.json` exits 0 only when the report meets the criteria. It prints
the criteria hash. The PR that sets `mode: block` fills `evidence` with `reportSha256`, `fixturesSha256`,
`criteriaSha256` and `reviewedBy`. A check fails when a policy has `mode: block` and its `criteriaSha256` differs from
the criteria in the file. So nobody can loosen the criteria after the fact without a visible diff.

Judge-side policies, such as `repeat-progress`, use the same file and the same rows. The judge never denies on its
own. Hooks do.
