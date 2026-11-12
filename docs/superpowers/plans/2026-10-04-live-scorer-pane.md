# Live scorer pane (Track D) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the scorer's numbers for the *current* session live inside Claude Code, the way Navigator's `/nav` pane does: context % of window, session cost and tokens, calls over 200k, this session's judge activity (calls, who decided, agreed/overrode/unsure, latency), which gates fired, queued wakes and context-guard fires. A `/live` pane plus a one-line band above the prompt, refreshed on turn end and on a timer, never blocking the session and failing silent.

**Architecture:** Two pieces. (1) `scorer live --session <id> [--json]`: a new scorer command that incrementally ingests only that session's transcripts (the existing `ingestFile` and its byte offsets, into a per-session database) and reads the judge database read-only, joined through `decision_details.session_id` (Plan A Task 1). Report and live share one definition of each figure: the same context-size SQL (`CTX`), the same 200k line, the same percentile, one `summarizeDecisions`. (2) `mods/aw-live`, a Claude Code *mod* (an in-process function-hook plugin documented by the `plugin-authoring` skill): `/live` toggles a pane, `/live status` prints the same numbers as text, and a band shows the headline. The mod shells out to `scorer live --json`; it holds the numbers in `$.state`; host-only facts (dollar cost, context percent, window) come from `$.session.usage()`.

**Tech Stack:** Scorer: Node ≥ 20, TypeScript 5.7 strict (ESM, Node16 resolution), Zod 3, better-sqlite3, Vitest 2 at 100% coverage. Mod: TypeScript/TSX against Claude Code's function-hooks API (`claude-code` types, early access), `claude plugin validate` and `claude plugin test`. Install: bash, `claude plugin marketplace/install`. Verified on `claude --version` = `2.1.289 (Claude Code)`.

**Spec:** `~/.agentic-workflow/digests/navigator-adoption.md` (Track D) and Navigator's mod, `hooks/mod/register.tsx` with `lib/`, `ui/` and `tests/` ($SCRATCH/navigator if still present). **Depends on** `docs/superpowers/plans/2026-10-04-judge-calibration.md` (Plan A) **Task 1 only**: `decision_details.session_id`, `recordDecisionDetails`, `upsertEvalItem`, `eval_items` in `judge/src/db.ts`. Plan A Task 3 supplies `judge label import` / `judge label set` for the optional override hotkeys. Plan A Task 7 supplies the report's new columns (Task 8 here is gated on it). Plan B and C are independent of this plan.

## Where this fits, and the decisions behind it

**Claude only.** Mods are a Claude Code feature. Codex and Cursor keep exactly what they have today: the statusline segments (judge, rtk, headroom, prism) and the daily scorer report. `scripts/install-live-pane.sh --provider codex|cursor` prints that and writes nothing.

**Runtime: the mod shells out; it does not import the scorer.** A mod runs in "an environment of its own, with no DOM and no Node" (plugin-authoring reference). It reaches the machine only through `$`: `$.fs` (text and bytes), `$.process.run` (argv, no shell, 30 s default timeout; the mod sets 5 s), `$.http`. It cannot load `better-sqlite3` (a native module) or any Node library, and it cannot parse SQLite. So the numbers come from `$.process.run(["scorer","live",...])`, one short Node process per refresh. Measured on a prototype (2026-10-04, real 3-call transcript, warm): 76 ms wall, 277 ms cold. The 200 ms target is for warm calls; the first call of a long session pays the one-time ingest of the whole transcript, and the mod shows nothing until it returns.

**Which numbers come from where.**

| Number | Source | Why |
|---|---|---|
| session cost (USD) | `$.session.usage().cost.usd` | The scorer reads no prices; the host's `/cost` ledger is the truth. No price table is duplicated. |
| context % and window | `$.session.usage().context` | The window is per model (a prototype saw `window: 1000000` for the default model and `200000` for haiku). The mod passes the window to `scorer live --window`, which uses it only when the host has no percent. |
| tokens, calls, subagent calls, calls over 200k | `scorer live` (transcript) | Same SQL (`CTX`, `OVER_200K`) as the daily report. |
| judge calls, provider share, agreed/overrode/unsure, p50/p95 | `scorer live` (judge db, read-only) | `summarizeDecisions`, the one definition. Needs `decision_details.session_id`. |
| gates fired | `scorer live`: `decisions` joined to `decision_details` by session, grouped by question | scope-gate = `brief-scope`, done-gate = `ask-check`, send-gate = `wake-gate`. |
| wakes queued now | `scorer live`: lines in `judge/outbox/<session>.jsonl` | Same reader as the report's outbox figure. |
| context-guard fires | `scorer live`: `context-guard/fires.jsonl` lines whose `sessionId` matches | Task 2 makes the hook write `sessionId`. Old lines match no session. |

**Hook latency.** Probed on 2.1.289: a mod's `classic.SessionStart`, `classic.PreToolUse`, `classic.PostToolUse`, `classic.UserPromptSubmit` and `classic.Stop` hooks did not fire under `claude -p --plugin-dir` (the mod's `session.start`, `command.run` and `tool.call` hooks did), and a mod's `tool.call` hook times the tool and the settings hooks together (1.8 to 2.3 s with one 400 ms hook configured). So all-hook latency is **not observable from a mod** in this build, and the pane does not claim it. What it shows is the latency of the judge-backed gates (p50 per gate, from `decisions.latency_ms`, which is the slow part of those hooks). No timing record is added to the bash hooks: macOS bash 3.2 has no millisecond clock without forking `perl` (about 6 ms on `context-guard`, whose hot path is already about 68 ms), and gate latency already lives in `decisions`.

**Zero-cost proof path.** A mod's slash command runs under `claude -p` without a model call, and `--resume <session id>` hands the command the real session. So `claude -p "/live status" --resume <id> --plugin-dir mods/aw-live` exercises the whole chain (mod, `$.session`, `scorer live`, formatting) for free. Task 9 uses it, plus one pseudo-terminal run that draws the pane.

**Optional labels.** Nothing in this plan needs a label. If `decision_details` and `eval_items` exist and the latest decision is labelable, the pane offers `y` ("this was right") and, for the two-answer question `ask-check` only, `x` ("wrong"). Each writes `judge label set <itemId> <label>` (an `override` label, Plan A). Joi does not have to press them, and no metric reads them.

## Global Constraints

- Node ≥ 20; TypeScript strict; ESM with `.js` extensions in every relative import in `scorer/` (`planning/CODE_STYLE.md`). Files are kebab-case. Zod schemas are PascalCase with a `Schema` suffix.
- `scorer/` keeps Vitest `globals: false` and **100%** lines, branches, functions and statements. Only `src/cli.ts` is excluded. **`/* v8 ignore */` is prohibited**: write the test. **No `any`.** (v8 reports a phantom uncovered branch on the closing brace of a function whose `try` and `catch` both `return`. Assign to a variable in the `try` and return after the block, as `readJudgeLive` does.)
- SQLite is additive only. The live path writes exactly one thing: `<state>/scorer/live/<session>.sqlite`. It never writes `scorer.sqlite` (the daily report's) and never writes `decisions.sqlite`; the judge database is opened `{ readonly: true, fileMustExist: true }` and a test pins that its bytes are unchanged.
- Fails open and silent. `scorer live` exits 1 with nothing on stdout on any error. The mod never throws out of a hook, never awaits a refresh inside a hook, and keeps its last good snapshot when a refresh fails.
- Mod environment rules (from the `plugin-authoring` skill and the 2026-10-04 prototype; each cost a failed run): a module holding `import()` does not load; files are `.ts`/`.tsx`; `$.env.get` takes a **literal** name (a ternary there fails `claude plugin validate`); do not name a local `h` (it is the JSX factory); the types contract file must `export type` something (a file with only `export {}` and `declare module` is not recognised); every `$.noun.method` call must be spelled in a file `validate` can follow; `$.state` keys named by the module must be declared in the contract file.
- `session_id` and `--session` values become path components. They must match `^[A-Za-z0-9._-]+$`, or the command exits 1.
- Run at most ONE heavy job at a time (`npm install`, `npm test`, `npm run test:coverage`, `tsc`, builds, `claude -p` model runs), including your own verification runs. Run the full scorer suite once per commit, not per edit. Targeted `npx vitest run <file>` runs are fine between edits.
- Install nothing into Joi's live environment. Real installs run only under a throwaway `HOME` (`HOME="$(mktemp -d)"`). Joi runs the real `scripts/install-live-pane.sh --provider claude` after merging, from the main checkout (the marketplace path is baked into Claude Code's config).
- Fixtures are synthetic. Never commit a real transcript line, a real prompt or a real session id. Evidence files hold aggregates only.
- No human labeling (Joi, 2026-10-04). The `y`/`x` hotkeys are an optional override; no step, metric or test depends on one being pressed.
- Commit format `type: description`. End every commit with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
  `Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC`

## Review Focus

Failure modes the spec implies but no obvious test exercises; each has its pinning test in the owning task.

1. **RF-1: A session id that is a path.** `scorer live --session ../../etc/passwd` (or a name with a space or slash) must exit 1 before it touches the filesystem. It decides the live database name and the outbox file name. Pinned in Task 4 (args tests).
2. **RF-2: A judge database that predates `decision_details.session_id`, or has no `decision_details`, or is unreadable.** The pane still renders; the judge card says it is not linked yet (`unscoped`) or is absent (`none`). Pinned in Task 3 (`judgeLive` and `readJudgeLive` tests) and Task 5 (`paneSections` for `unscoped` and `none`).
3. **RF-3: `scorer` is missing, old (no `live` command), crashes, prints garbage, or a refresh is already running.** The band shows the last good numbers or nothing, nothing throws, and two refreshes never run two processes. Pinned in Task 6.
4. **RF-4: A transcript being written while we read it.** A half-written last line must not be counted, then must be counted once, exactly, when it completes. Pinned in Task 4 (`runLive` test "counts a half-written last line only once it is complete").
5. **RF-5: `/clear`, `/resume` or a new session.** The id changes mid-process. The mod must ask for the *current* id every refresh, never cache it. Pinned in Task 6.

## File structure

| File | Responsibility |
|---|---|
| `scorer/src/judge-stats.ts` (new) | `summarizeDecisions`: the one definition of judge calls, provider share, agreed/overrode/unsure, p50/p95 |
| `scorer/src/live-judge.ts` (new) | `judgeLive`, `readJudgeLive`, `GATES`, `JudgeLiveSchema`: this session's judge activity, read-only |
| `scorer/src/live-snapshot.ts` (new) | `LiveSnapshotSchema` (the contract) and `sessionUsage` (tokens, calls, context) |
| `scorer/src/live.ts` (new) | `runLive`, `findSessionFiles`, `pruneLiveDbs`, `renderLive` |
| `scorer/src/args.ts`, `cli.ts` | the `live` command (`cli.ts` stays the thin, coverage-excluded entry) |
| `scorer/src/metrics.ts`, `run.ts`, `transcript/discover.ts`, `outbox.ts`, `context-guard-fires.ts` | export the shared pieces the live path reuses |
| `config/hooks/context-guard.sh` | write `sessionId` on each fire |
| `mods/aw-live/` (new) | the mod: manifest, types contract, `hooks/register.tsx`, `hooks/lib/*`, `hooks/tests/*` |
| `mods/.claude-plugin/marketplace.json` (new) | folder marketplace `agentic-workflow-mods` (the owner tag) |
| `scripts/install-live-pane.sh`, `config/lib/tests/install-live-pane.test.sh` (new) | provider-aware, idempotent, `--dry-run`, `--uninstall` installer and its test |
| `providers/lib.sh` | add the installer to `AW_LEVER_INSTALLERS` so `setup.sh` runs it per provider |
| `scripts/live-pane-proof.py` (new) | drives a real interactive session in a pseudo-terminal to prove the pane draws |
| `docs/superpowers/evidence/2026-10-04-live-scorer-pane.md` (new) | Task 9's recorded evidence |

**Preconditions (once, in the overnight worktree, branch `feat/live-scorer-pane` per the runbook).** `ls scorer/node_modules judge/node_modules` both exist (else `npm install` in that one package, alone). `claude --version` prints 2.1.289 or newer. `python3 --version` works. Load the `plugin-authoring` skill (Skill tool) once: it writes the types file Tasks 5 and 6 type-check against.

---

### Task 1: Share the context-size SQL, state-dir resolution and per-session readers

The live path must not re-define what the report already defines. This task only exports and generalizes; nothing changes for the report.

**Files:**
- Modify: `scorer/src/metrics.ts` (export `CTX`; add `OVER_200K`, `OVER_400K`)
- Modify: `scorer/src/run.ts` (`resolveStateDir`; `resolveJudgeDbPath` uses it)
- Modify: `scorer/src/transcript/discover.ts` (`discoverSession`)
- Modify: `scorer/src/outbox.ts` (`queuedForSession`; export `countValidLines`)
- Modify: `scorer/src/context-guard-fires.ts` (optional `sessionId`; `firesForSession`)
- Test: `scorer/tests/discover.test.ts`, `outbox.test.ts`, `context-guard-fires.test.ts`, `run.test.ts` (append; the metrics tests already pin the SQL's numbers)

**Interfaces:**
- Produces:
  - `export const CTX = "(input + cache_read + cache_creation)"`, `export const OVER_200K = 200_000`, `export const OVER_400K = 400_000` (all in `metrics.ts`)
  - `resolveStateDir(options: CliOptions): string`: explicit `--state-dir`, else `AW_STATE_DIR` (non-empty), else the default
  - `discoverSession(projectsDir: string, sessionId: string, project?: string): TranscriptFile[]`: main file plus subagents; `[]` when absent
  - `queuedForSession(dir: string, sessionId: string): number`
  - `ContextGuardFire` gains `sessionId?: string`; `firesForSession(fires: readonly ContextGuardFire[], sessionId: string): ContextGuardFire[]`

- [ ] **Step 1: Write the failing tests**

Append to `scorer/tests/discover.test.ts` and change its import line to `import { discoverFiles, discoverSession } from "../src/transcript/discover.js";`:

```ts
describe("discoverSession", () => {
  function layout(): { root: string; proj: string } {
    const root = tmpDir();
    const proj = path.join(root, "-Users-dev-acme-web-app");
    fs.mkdirSync(path.join(proj, "s1", "subagents"), { recursive: true });
    fs.writeFileSync(path.join(proj, "s1.jsonl"), "");
    fs.writeFileSync(path.join(proj, "s1", "subagents", "agent-a1.jsonl"), "");
    fs.writeFileSync(path.join(proj, "s1", "subagents", "agent-a1.meta.json"), JSON.stringify({ agentType: "Explore" }));
    fs.writeFileSync(path.join(proj, "s2.jsonl"), "");
    return { root, proj };
  }

  it("finds the main file and its subagents in the hinted project, touching no other session", () => {
    const { root, proj } = layout();
    expect(discoverSession(root, "s1", "-Users-dev-acme-web-app")).toEqual([
      { provider: "claude", path: path.join(proj, "s1.jsonl"), project: "-Users-dev-acme-web-app", sessionId: "s1", agentId: "main", agentType: "main", isMain: true },
      { provider: "claude", path: path.join(proj, "s1", "subagents", "agent-a1.jsonl"), project: "-Users-dev-acme-web-app", sessionId: "s1", agentId: "a1", agentType: "Explore", isMain: false },
    ]);
  });

  it("scans every project when no hint is given, and finds nothing for an unknown session", () => {
    const { root } = layout();
    expect(discoverSession(root, "s2").map((f) => f.sessionId)).toEqual(["s2"]);
    expect(discoverSession(root, "nope")).toEqual([]);
  });

  it("returns nothing when the hinted project does not hold the session, or the projects dir is missing", () => {
    const { root } = layout();
    expect(discoverSession(root, "s1", "-some-other-project")).toEqual([]);
    expect(discoverSession(path.join(root, "missing"), "s1")).toEqual([]);
  });
});
```

Append to `scorer/tests/outbox.test.ts` and change its import to `import { queuedForSession, readOutboxState } from "../src/outbox.js";`:

```ts
describe("queuedForSession", () => {
  it("counts only that session's valid queued lines, never another session's or expired.jsonl", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "outbox-"));
    const item = JSON.stringify({ ts: "2026-09-27T00:00:00Z", agentType: null, text: "progress" });
    writeFixture(dir, "s1.jsonl", [item, "not json", item]);
    writeFixture(dir, "s2.jsonl", [item]);
    writeFixture(dir, "expired.jsonl", [item]);
    expect(queuedForSession(dir, "s1")).toBe(2);
    expect(queuedForSession(dir, "expired")).toBe(1);
    expect(queuedForSession(dir, "missing")).toBe(0);
    expect(queuedForSession(path.join(dir, "nope"), "s1")).toBe(0);
  });
});
```

Append to `scorer/tests/context-guard-fires.test.ts` and change its import to `import { firesForSession, readFiresLog } from "../src/context-guard-fires.js";`:

```ts
describe("session ids on fires", () => {
  it("reads an optional sessionId, and firesForSession keeps only matching fires (old lines match nothing)", () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), "fires-"));
    fs.writeFileSync(path.join(dir, "fires.jsonl"), [
      JSON.stringify({ ts: "2026-09-27T00:00:00Z", agentId: null, tokens: 210_000, sessionId: "s1" }),
      JSON.stringify({ ts: "2026-09-27T00:01:00Z", agentId: "a1", tokens: 220_000, sessionId: "s2" }),
      JSON.stringify({ ts: "2026-09-27T00:02:00Z", agentId: null, tokens: 230_000 }),
    ].join("\n"));
    const fires = readFiresLog(dir);
    expect(fires).toHaveLength(3);
    expect(firesForSession(fires, "s1")).toEqual([{ ts: "2026-09-27T00:00:00Z", agentId: null, tokens: 210_000, sessionId: "s1" }]);
    expect(firesForSession(fires, "s3")).toEqual([]);
  });
});
```

In `scorer/tests/run.test.ts`, change the run.js import to `import { resolveJudgeDbPath, resolveProviders, resolveStateDir, runReport, transcriptRoot } from "../src/run.js";` and insert this block immediately before `describe("resolveProviders", () => {`:

```ts
describe("resolveStateDir", () => {
  const base: CliOptions = {
    command: "report", since: NOW, until: NOW, projectsDir: "/p", stateDir: "/default/state", stateDirExplicit: false, prLookup: true, contextTokensPath: null,
  };

  function withEnv(value: string | undefined, fn: () => void): void {
    const original = process.env.AW_STATE_DIR;
    if (value === undefined) delete process.env.AW_STATE_DIR;
    else process.env.AW_STATE_DIR = value;
    try {
      fn();
    } finally {
      if (original === undefined) delete process.env.AW_STATE_DIR;
      else process.env.AW_STATE_DIR = original;
    }
  }

  it("is the explicit --state-dir, else AW_STATE_DIR, else the default; an empty AW_STATE_DIR counts as unset", () => {
    withEnv("/env/state", () => expect(resolveStateDir({ ...base, stateDir: "/x", stateDirExplicit: true })).toBe("/x"));
    withEnv("/env/state", () => expect(resolveStateDir(base)).toBe("/env/state"));
    withEnv("", () => expect(resolveStateDir(base)).toBe("/default/state"));
    withEnv(undefined, () => expect(resolveStateDir(base)).toBe("/default/state"));
  });
});
```

- [ ] **Step 2: Run, verify they fail**

Run: `cd scorer && npx vitest run tests/discover.test.ts tests/outbox.test.ts tests/context-guard-fires.test.ts tests/run.test.ts`
Expected: FAIL (`discoverSession`, `queuedForSession`, `firesForSession`, `resolveStateDir` are not exported).

- [ ] **Step 3: Implement**

`scorer/src/metrics.ts`: replace `const CTX = "(input + cache_read + cache_creation)";` with

```ts
// Shared with the live path (live-snapshot.ts): one definition of "context size of a call".
export const CTX = "(input + cache_read + cache_creation)";
export const OVER_200K = 200_000;
export const OVER_400K = 400_000;
```

and in the same file replace the three literals inside the `totals` query: `CASE WHEN ${CTX} > 200000 THEN ${CTX} ELSE 0 END` with `CASE WHEN ${CTX} > ${OVER_200K} THEN ${CTX} ELSE 0 END`; `CASE WHEN ${CTX} > 400000 THEN ${CTX} ELSE 0 END` with `CASE WHEN ${CTX} > ${OVER_400K} THEN ${CTX} ELSE 0 END`; `CASE WHEN ${CTX} > 200000 THEN 1 ELSE 0 END` with `CASE WHEN ${CTX} > ${OVER_200K} THEN 1 ELSE 0 END`.

`scorer/src/run.ts`: replace the whole `resolveJudgeDbPath` function (keep its comment block above it) with

```ts
export function resolveStateDir(options: CliOptions): string {
  const override = process.env.AW_STATE_DIR;
  if (options.stateDirExplicit) return options.stateDir;
  return override !== undefined && override !== "" ? override : options.stateDir;
}

export function resolveJudgeDbPath(options: CliOptions): string {
  return path.join(resolveStateDir(options), "judge", "decisions.sqlite");
}
```

`scorer/src/transcript/discover.ts`: insert before `function dirs(dir: string)`:

```ts
// One session's files without scanning every project: the caller tries the project named by
// the cwd first; with no `project`, every project directory is checked.
export function discoverSession(projectsDir: string, sessionId: string, project?: string): TranscriptFile[] {
  for (const candidate of project === undefined ? dirs(projectsDir) : [project]) {
    const projDir = path.join(projectsDir, candidate);
    const main = path.join(projDir, `${sessionId}.jsonl`);
    if (!fs.existsSync(main)) continue;
    return [
      { provider: "claude", path: main, project: candidate, sessionId, agentId: "main", agentType: "main", isMain: true },
      ...subagents(projDir, candidate, sessionId),
    ];
  }
  return [];
}

```

`scorer/src/outbox.ts`: change `function countValidLines(file: string): number {` to `export function countValidLines(file: string): number {` and append:

```ts

// Items queued for ONE session: its own <sessionId>.jsonl, never expired.jsonl.
// sessionId is validated by the CLI (args.ts) before it reaches a path.
export function queuedForSession(dir: string, sessionId: string): number {
  return countValidLines(path.join(dir, `${sessionId}.jsonl`));
}
```

`scorer/src/context-guard-fires.ts`: in `interface ContextGuardFire` add `sessionId?: string;` after `tokens: number;`; replace the `FireLineSchema` line with

```ts
const FireLineSchema = z.object({ ts: z.string(), agentId: z.string().nullable(), tokens: z.number(), sessionId: z.string().optional() });
```

and append:

```ts

// Fires written before the hook recorded a session id carry none and match no session.
export function firesForSession(fires: readonly ContextGuardFire[], sessionId: string): ContextGuardFire[] {
  return fires.filter((f) => f.sessionId === sessionId);
}
```

- [ ] **Step 4: Run, verify pass; then the full suite once**

Run: `cd scorer && npx vitest run tests/discover.test.ts tests/outbox.test.ts tests/context-guard-fires.test.ts tests/run.test.ts tests/metrics.test.ts`
Expected: PASS.
Run: `cd scorer && npm run typecheck && npm run test:coverage`
Expected: typecheck clean; all tests pass; coverage 100% on every file.

- [ ] **Step 5: Commit**

```bash
git add scorer/src/metrics.ts scorer/src/run.ts scorer/src/transcript/discover.ts scorer/src/outbox.ts scorer/src/context-guard-fires.ts scorer/tests/discover.test.ts scorer/tests/outbox.test.ts scorer/tests/context-guard-fires.test.ts scorer/tests/run.test.ts
git commit -m "refactor: export the shared context SQL, state-dir resolution and per-session readers" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

### Task 2: context-guard records the session id on each fire

`fires.jsonl` lines carry no session today, so "context-guard fires this session" cannot be answered. The hook already has `$SESSION_ID` and already runs `jq` on the fire path, so this adds one `--arg` and one key.

**Files:**
- Modify: `config/hooks/context-guard.sh` (the `jq -nc` that appends to `fires.jsonl`)
- Test: `config/lib/tests/context-guard.test.sh`

**Interfaces:**
- Produces: each `fires.jsonl` line is `{"ts","agentId","sessionId","tokens"}`. `scorer/src/context-guard-fires.ts` (Task 1) reads `sessionId` as optional.

- [ ] **Step 1: Write the failing assertion**

In `config/lib/tests/context-guard.test.sh`, in `test_fire_appends_one_line_to_fires_jsonl_non_fire_appends_nothing`, replace

```bash
  jq -e '.tokens == 255000 and .agentId == "a1"' "$cfg/fires.jsonl" > /dev/null || { echo "FAIL: fires.jsonl line shape wrong: $(cat "$cfg/fires.jsonl")"; exit 1; }
```

with

```bash
  jq -e '.tokens == 255000 and .agentId == "a1" and .sessionId == "s1"' "$cfg/fires.jsonl" > /dev/null || { echo "FAIL: fires.jsonl line shape wrong: $(cat "$cfg/fires.jsonl")"; exit 1; }
```

- [ ] **Step 2: Run, verify it fails**

Run: `bash config/lib/tests/context-guard.test.sh`
Expected: FAIL with `fires.jsonl line shape wrong`.

- [ ] **Step 3: Implement**

In `config/hooks/context-guard.sh` replace

```bash
jq -nc --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --arg agent "$AGENT_ID" --argjson tokens "$TOKENS" \
  '{"ts": $ts, "agentId": (if $agent == "" then null else $agent end), "tokens": $tokens}' \
  >> "$CONFIG_DIR/fires.jsonl" 2>/dev/null || true
```

with

```bash
jq -nc --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --arg agent "$AGENT_ID" --arg sid "$SESSION_ID" --argjson tokens "$TOKENS" \
  '{"ts": $ts, "agentId": (if $agent == "" then null else $agent end), "sessionId": $sid, "tokens": $tokens}' \
  >> "$CONFIG_DIR/fires.jsonl" 2>/dev/null || true
```

- [ ] **Step 4: Run, verify pass**

Run: `bash config/lib/tests/context-guard.test.sh`
Expected: every `PASS:` line, exit 0.

- [ ] **Step 5: Commit**

```bash
git add config/hooks/context-guard.sh config/lib/tests/context-guard.test.sh
git commit -m "feat: context-guard records the session id on each fire" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

### Task 3: This session's judge activity, read-only (`summarizeDecisions`, `judgeLive`)

**Files:**
- Create: `scorer/src/judge-stats.ts`, `scorer/src/live-judge.ts`
- Test: `scorer/tests/judge-stats.test.ts`, `scorer/tests/live-judge.test.ts`

**Interfaces:**
- Consumes: `percentile(values: readonly number[], p: number): number` from `metrics.ts` (existing, sorts internally); from Plan A Task 1 in `judge/src/db.ts`: `openDb`, `recordDecision`, `recordDecisionDetails(db, { id, input_json, probabilities, rules_opinion, agreement, session_id })`, `upsertEvalItem(db, { id, question, input_json, source, model_decision, created_at })`, the tables `decision_details` (with `session_id`) and `eval_items` (`source` is `decision:<decision id>`).
- Produces:
  - `interface DecisionFacts { provider: string; outcome: string; latency_ms: number; agreement: string | null }`
  - `interface DecisionSummary { calls: number; byProvider: Record<string, number>; undecided: number; agreed: number; overrode: number; p50LatencyMs: number; p95LatencyMs: number }`
  - `summarizeDecisions(rows: readonly DecisionFacts[]): DecisionSummary`
  - `GATES = { "scope-gate": "brief-scope", "done-gate": "ask-check", "send-gate": "wake-gate" } as const`
  - `JudgeLiveSchema` (zod discriminated union on `state`: `none` | `unscoped` | `ok`) and `type JudgeLive`
  - `judgeLive(db: JudgeDb, sessionId: string): JudgeLive`, `readJudgeLive(dbPath: string, sessionId: string): JudgeLive`

- [ ] **Step 0: Verify the dependency**

Run: `grep -n -A8 "CREATE TABLE IF NOT EXISTS decision_details" judge/src/db.ts | grep session_id && grep -n "recordDecisionDetails\|upsertEvalItem" judge/src/db.ts`
Expected: a `session_id TEXT` line inside the `decision_details` DDL, then matches for both functions. (A bare `grep session_id` is not enough: the `briefs` table already has that column.) If there is no match, Plan A Task 1 has not landed: stop, mark this task blocked ("needs Plan A Task 1"), and do not guess.

- [ ] **Step 1: Write the failing tests**

Create `scorer/tests/judge-stats.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { summarizeDecisions } from "../src/judge-stats.js";

const row = (provider: string, outcome: string, latency: number, agreement: string | null) => ({ provider, outcome, latency_ms: latency, agreement });

describe("summarizeDecisions", () => {
  it("counts every row as a call, but only decided rows as provider answers and latencies", () => {
    expect(summarizeDecisions([
      row("jev", "decided", 300, "agreed"),
      row("jev", "decided", 400, "overrode"),
      row("rules", "decided", 0, "undecided"),
      row("jev", "escalated", 9000, null),
      row("claude-cli", "failed", 9000, null),
    ])).toEqual({
      calls: 5, byProvider: { jev: 2, rules: 1 }, undecided: 1, agreed: 1, overrode: 1, p50LatencyMs: 300, p95LatencyMs: 400,
    });
  });

  it("is all zeros for no rows", () => {
    expect(summarizeDecisions([])).toEqual({ calls: 0, byProvider: {}, undecided: 0, agreed: 0, overrode: 0, p50LatencyMs: 0, p95LatencyMs: 0 });
  });
});
```

Create `scorer/tests/live-judge.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import Database from "better-sqlite3";
import { describe, expect, it } from "vitest";

import { openDb, recordDecision, recordDecisionDetails, upsertEvalItem } from "../../judge/src/db.js";
import type { DecisionRow } from "../../judge/src/db.js";
import { GATES, JudgeLiveSchema, judgeLive, readJudgeLive } from "../src/live-judge.js";
import { tmpDir } from "./helpers.js";

function decision(id: string, question: string, over: Partial<DecisionRow> = {}): DecisionRow {
  return {
    id, ts: "2026-10-04T10:00:00.000Z", question, content_class: "brief", provider: "jev", decision: "ready", confidence: 0.9,
    reason_code: "jev", latency_ms: 300, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided", ...over,
  };
}

function seed(db: ReturnType<typeof openDb>, id: string, session: string | null, row: Partial<DecisionRow> & { question: string }, agreement: "agreed" | "overrode" | "undecided" | null = null): void {
  recordDecision(db, decision(id, row.question, row));
  recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement, session_id: session });
}

describe("judgeLive", () => {
  it("is 'none' for a database with no decisions table, and 'unscoped' when decision_details cannot be tied to a session", () => {
    expect(judgeLive(new Database(":memory:"), "s1")).toEqual({ state: "none" });
    const noDetails = openDb(":memory:");
    noDetails.exec("DROP TABLE decision_details");
    expect(judgeLive(noDetails, "s1")).toEqual({ state: "unscoped" });
    const oldDetails = openDb(":memory:");
    oldDetails.exec("DROP TABLE decision_details; CREATE TABLE decision_details (id TEXT PRIMARY KEY, input_json TEXT NOT NULL)");
    expect(judgeLive(oldDetails, "s1")).toEqual({ state: "unscoped" });
  });

  it("is ok with zero activity for a session with no decisions", () => {
    const live = judgeLive(openDb(":memory:"), "s1");
    expect(JudgeLiveSchema.parse(live)).toMatchObject({ state: "ok", calls: 0, latest: null, labelable: true });
  });

  it("summarizes only this session: calls, providers, agreement, latencies, gates and the latest decision", () => {
    const db = openDb(":memory:");
    seed(db, "a", "s1", { question: "brief-scope", decision: "ready", latency_ms: 400, ts: "2026-10-04T10:00:00.000Z" }, "agreed");
    seed(db, "b", "s1", { question: "ask-check", decision: "continue", latency_ms: 350, ts: "2026-10-04T10:01:00.000Z" }, "overrode");
    seed(db, "c", "s1", { question: "ask-check", decision: "ask", provider: "rules", latency_ms: 0, ts: "2026-10-04T10:02:00.000Z" }, "undecided");
    seed(db, "d", "s1", { question: "wake-gate", decision: null, outcome: "escalated", latency_ms: 9000, ts: "2026-10-04T10:03:00.000Z" });
    seed(db, "e", "other", { question: "ask-check", decision: "ask" });
    seed(db, "f", null, { question: "ask-check", decision: "ask" });
    recordDecision(db, decision("g", "ask-check"));
    upsertEvalItem(db, { id: "item-c", question: "ask-check", input_json: "{}", source: "decision:c", model_decision: "ask", created_at: "2026-10-04T10:02:00.000Z" });

    const live = judgeLive(db, "s1");
    expect(JudgeLiveSchema.parse(live)).toEqual({
      state: "ok", calls: 4, byProvider: { jev: 2, rules: 1 }, undecided: 1, agreed: 1, overrode: 1, p50LatencyMs: 350, p95LatencyMs: 400,
      gates: {
        "scope-gate": { question: "brief-scope", fired: 1, byDecision: { ready: 1 }, p50LatencyMs: 400 },
        "done-gate": { question: "ask-check", fired: 2, byDecision: { continue: 1, ask: 1 }, p50LatencyMs: 0 },
        "send-gate": { question: "wake-gate", fired: 1, byDecision: {}, p50LatencyMs: 0 },
      },
      labelable: true,
      latest: { id: "c", ts: "2026-10-04T10:02:00.000Z", question: "ask-check", decision: "ask", provider: "rules", confidence: 0.9, itemId: "item-c" },
    });
  });

  it("has no item id until the decision is imported, and is not labelable without eval_items", () => {
    const db = openDb(":memory:");
    seed(db, "a", "s1", { question: "ask-check", decision: "continue" });
    expect(judgeLive(db, "s1")).toMatchObject({ labelable: true, latest: { id: "a", itemId: null } });
    db.exec("DROP TABLE eval_items");
    expect(judgeLive(db, "s1")).toMatchObject({ labelable: false, latest: { id: "a", itemId: null } });
  });

  it("maps each gate to the question it asks", () => {
    expect(GATES).toEqual({ "scope-gate": "brief-scope", "done-gate": "ask-check", "send-gate": "wake-gate" });
  });
});

describe("readJudgeLive", () => {
  it("is 'none' for a missing file, a directory, and a file that is not a database", () => {
    const dir = tmpDir();
    expect(readJudgeLive(path.join(dir, "missing.sqlite"), "s1")).toEqual({ state: "none" });
    const junk = path.join(dir, "junk.sqlite");
    fs.writeFileSync(junk, "this is not sqlite");
    expect(readJudgeLive(junk, "s1")).toEqual({ state: "none" });
    expect(readJudgeLive(dir, "s1")).toEqual({ state: "none" });
  });

  it("reads a real file read-only and leaves it unchanged", () => {
    const file = path.join(tmpDir(), "decisions.sqlite");
    const db = openDb(file);
    seed(db, "a", "s1", { question: "brief-scope" });
    db.close();
    const before = fs.readFileSync(file);
    expect(readJudgeLive(file, "s1")).toMatchObject({ state: "ok", calls: 1 });
    expect(fs.readFileSync(file).equals(before)).toBe(true);
  });
});
```

- [ ] **Step 2: Run, verify they fail**

Run: `cd scorer && npx vitest run tests/judge-stats.test.ts tests/live-judge.test.ts`
Expected: FAIL, cannot find `../src/judge-stats.js`.

- [ ] **Step 3: Implement**

Create `scorer/src/judge-stats.ts`:

```ts
import { percentile } from "./metrics.js";

// One decision as the summary needs it. `agreement` comes from decision_details
// (Plan A) and is null for decisions made before it was recorded.
export interface DecisionFacts {
  provider: string;
  outcome: string;
  latency_ms: number;
  agreement: string | null;
}

export interface DecisionSummary {
  calls: number;
  byProvider: Record<string, number>;
  undecided: number;
  agreed: number;
  overrode: number;
  p50LatencyMs: number;
  p95LatencyMs: number;
}

// The one definition of these figures. The live pane (live-judge.ts) and the daily
// report's judge section (judge-section.ts) both call it, so they cannot disagree.
//   calls         every decision row, whatever its outcome
//   byProvider    decided rows only: who actually answered
//   latency       decided rows only: an escalated or failed row never answered
export function summarizeDecisions(rows: readonly DecisionFacts[]): DecisionSummary {
  const decided = rows.filter((r) => r.outcome === "decided");
  const byProvider: Record<string, number> = {};
  for (const r of decided) byProvider[r.provider] = (byProvider[r.provider] ?? 0) + 1;
  const latencies = decided.map((r) => r.latency_ms);
  const count = (agreement: string): number => rows.filter((r) => r.agreement === agreement).length;
  return {
    calls: rows.length,
    byProvider,
    undecided: count("undecided"),
    agreed: count("agreed"),
    overrode: count("overrode"),
    p50LatencyMs: percentile(latencies, 0.5),
    p95LatencyMs: percentile(latencies, 0.95),
  };
}
```

Create `scorer/src/live-judge.ts`:

```ts
import fs from "node:fs";

import Database from "better-sqlite3";
import { z } from "zod";

import type { DecisionFacts } from "./judge-stats.js";
import { summarizeDecisions } from "./judge-stats.js";
import { percentile } from "./metrics.js";

export type JudgeDb = Database.Database;

// The hooks that fire a judge question, by the question they ask.
export const GATES = { "scope-gate": "brief-scope", "done-gate": "ask-check", "send-gate": "wake-gate" } as const;

const Count = z.number().int().nonnegative();
const GateStatsSchema = z.object({
  question: z.string(),
  fired: Count,
  byDecision: z.record(Count),
  p50LatencyMs: z.number().nonnegative(),
});
const LatestSchema = z.object({
  id: z.string(),
  ts: z.string(),
  question: z.string(),
  decision: z.string(),
  provider: z.string(),
  confidence: z.number(),
  itemId: z.string().nullable(),
});

// none      no judge database (judge never ran, or it is unreadable)
// unscoped  a database that predates decision_details.session_id: decisions cannot be tied to a session
// ok        this session's decisions
export const JudgeLiveSchema = z.discriminatedUnion("state", [
  z.object({ state: z.literal("none") }),
  z.object({ state: z.literal("unscoped") }),
  z.object({
    state: z.literal("ok"),
    calls: Count,
    byProvider: z.record(Count),
    undecided: Count,
    agreed: Count,
    overrode: Count,
    p50LatencyMs: z.number().nonnegative(),
    p95LatencyMs: z.number().nonnegative(),
    gates: z.object({ "scope-gate": GateStatsSchema, "done-gate": GateStatsSchema, "send-gate": GateStatsSchema }),
    labelable: z.boolean(),
    latest: LatestSchema.nullable(),
  }),
]);
export type JudgeLive = z.infer<typeof JudgeLiveSchema>;
type GateStats = z.infer<typeof GateStatsSchema>;

interface SessionRow extends DecisionFacts {
  id: string;
  ts: string;
  question: string;
  decision: string | null;
  confidence: number;
}

function hasTable(db: JudgeDb, name: string): boolean {
  return db.prepare("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?").get(name) !== undefined;
}

function hasColumn(db: JudgeDb, table: string, column: string): boolean {
  const cols = db.prepare(`SELECT name FROM pragma_table_info('${table}')`).all() as Array<{ name: string }>;
  return cols.some((c) => c.name === column);
}

function gateStats(rows: readonly SessionRow[], question: string): GateStats {
  const mine = rows.filter((r) => r.question === question);
  const byDecision: Record<string, number> = {};
  for (const r of mine) if (r.decision !== null) byDecision[r.decision] = (byDecision[r.decision] ?? 0) + 1;
  const decided = mine.filter((r) => r.outcome === "decided").map((r) => r.latency_ms);
  return { question, fired: mine.length, byDecision, p50LatencyMs: percentile(decided, 0.5) };
}

// This session's judge activity. `db` may be read-only; nothing here writes.
export function judgeLive(db: JudgeDb, sessionId: string): JudgeLive {
  if (!hasTable(db, "decisions")) return { state: "none" };
  if (!hasTable(db, "decision_details") || !hasColumn(db, "decision_details", "session_id")) return { state: "unscoped" };
  const rows = db.prepare(
    `SELECT d.id, d.ts, d.question, d.provider, d.decision, d.confidence, d.outcome, d.latency_ms, x.agreement
     FROM decisions d JOIN decision_details x ON x.id = d.id
     WHERE x.session_id = ? ORDER BY d.ts ASC, d.id ASC`,
  ).all(sessionId) as SessionRow[];
  const labelable = hasTable(db, "eval_items");
  const last = rows.filter((r) => r.outcome === "decided" && r.decision !== null).at(-1);
  let latest: z.infer<typeof LatestSchema> | null = null;
  if (last !== undefined) {
    const item = labelable
      ? (db.prepare("SELECT id FROM eval_items WHERE source = ?").get(`decision:${last.id}`) as { id: string } | undefined)
      : undefined;
    latest = {
      id: last.id, ts: last.ts, question: last.question, decision: last.decision as string,
      provider: last.provider, confidence: last.confidence, itemId: item?.id ?? null,
    };
  }
  return {
    state: "ok",
    ...summarizeDecisions(rows),
    gates: {
      "scope-gate": gateStats(rows, GATES["scope-gate"]),
      "done-gate": gateStats(rows, GATES["done-gate"]),
      "send-gate": gateStats(rows, GATES["send-gate"]),
    },
    labelable,
    latest,
  };
}

function openReadOnly(dbPath: string): JudgeDb | null {
  try {
    return new Database(dbPath, { readonly: true, fileMustExist: true });
  } catch {
    return null;
  }
}

// Opens the judge database read-only for one query. Any trouble (missing file,
// corrupt file, a writer holding it) reads as "no judge": the pane must never fail over this.
export function readJudgeLive(dbPath: string, sessionId: string): JudgeLive {
  const db = fs.existsSync(dbPath) ? openReadOnly(dbPath) : null;
  if (db === null) return { state: "none" };
  let live: JudgeLive = { state: "none" };
  try {
    db.pragma("busy_timeout = 200");
    live = judgeLive(db, sessionId);
  } catch {
    // unreadable or malformed: stays "none"
  } finally {
    db.close();
  }
  return live;
}
```

- [ ] **Step 4: Run, verify pass**

Run: `cd scorer && npx vitest run tests/judge-stats.test.ts tests/live-judge.test.ts --coverage --coverage.include=src/judge-stats.ts --coverage.include=src/live-judge.ts`
Expected: PASS, 100% on both files (the `--coverage.include` flags scope the report to the new files).

- [ ] **Step 5: Commit** (full scorer suite once, first: `cd scorer && npm run typecheck && npm run test:coverage`, expected clean and 100%)

```bash
git add scorer/src/judge-stats.ts scorer/src/live-judge.ts scorer/tests/judge-stats.test.ts scorer/tests/live-judge.test.ts
git commit -m "feat: session-scoped judge stats, read-only, tolerant of an old judge db" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

### Task 4: `scorer live`: incremental per-session snapshot

**Files:**
- Create: `scorer/src/live-snapshot.ts`, `scorer/src/live.ts`
- Modify (replace whole file): `scorer/src/args.ts`, `scorer/src/cli.ts`
- Test: `scorer/tests/live-snapshot.test.ts`, `scorer/tests/live.test.ts`, `scorer/tests/args.test.ts` (edit)

**Interfaces:**
- Consumes: Task 1 (`CTX`, `OVER_200K`, `resolveStateDir`, `discoverSession`, `queuedForSession`, `firesForSession`, `readFiresLog`); Task 3 (`readJudgeLive`, `JudgeLiveSchema`); existing `ingestAll(db, files): FormatHealth` (`.lines` = new lines), `openDb(path)`, `projectFromCwd(cwd)`, `formatTokens`/`formatPct` from `render.ts`.
- Produces:
  - `LiveSnapshotSchema` / `type LiveSnapshot` (v 1): `{ v:1, sessionId, at, transcript:{found,files}, ingest:{newLines,ms}, context:{tokens|null,window,percent|null}, usage:{calls,subagentCalls,contextTokens,outputTokens,callsOver200k}, judge: JudgeLive, contextGuard:{fires}, wakes:{queuedNow} }`
  - `sessionUsage(db: Db, window: number): Pick<LiveSnapshot, "context" | "usage">`
  - `findSessionFiles(projectsDir, sessionId, cwd: string | null): TranscriptFile[]`, `pruneLiveDbs(dir, keepSession, nowMs): number`, `runLive(options: CliOptions, nowMs?: () => number): LiveSnapshot`, `renderLive(s: LiveSnapshot): string`
  - CLI: `scorer live --session <id> [--cwd DIR] [--window TOKENS] [--json] [--projects-dir DIR] [--state-dir DIR]`. `CliOptions` gains `liveSession`, `liveCwd`, `liveWindow` (default 200000), `json`.

- [ ] **Step 1: Write the failing tests**

Edit `scorer/tests/args.test.ts`: in the first test ("defaults to a one-day report") replace `      contextTokensPath: null,\n    } });` with

```ts
      contextTokensPath: null, liveSession: null, liveCwd: null, liveWindow: 200_000, json: false,
    } });
```

then add this test before the `it("accepts --provider (single, list, all)` test:

```ts
  it("accepts the live command with its flags", () => {
    const r = parseArgs(["live", "--session", "abc-123_4.5", "--cwd", "/repo", "--window", "1000000", "--json", "--state-dir", "/s"], NOW, HOME);
    expect(r).toEqual({ ok: true, options: expect.objectContaining({
      command: "live", liveSession: "abc-123_4.5", liveCwd: "/repo", liveWindow: 1_000_000, json: true, stateDir: "/s", stateDirExplicit: true,
    }) });
  });
```

and these rows to the `it.each([...])("rejects %j"` table, after the `context-tokens` row (RF-1 is the `../etc/passwd` row):

```ts
    [["live"], "live needs --session <id>"],
    [["live", "--session"], "--session needs a value"],
    [["live", "--session", "../etc/passwd"], "--session must be letters, digits, '.', '_' or '-': ../etc/passwd"],
    [["live", "--session", "a b"], "--session must be letters, digits, '.', '_' or '-': a b"],
    [["live", "--session", "s1", "--window", "0"], "--window must be a positive integer: 0"],
    [["live", "--session", "s1", "--window", "big"], "--window must be a positive integer: big"],
    [["live", "--session", "s1", "--window", "1.5"], "--window must be a positive integer: 1.5"],
```

Create `scorer/tests/live-snapshot.test.ts`:

```ts
import path from "node:path";

import { describe, expect, it } from "vitest";

import { openDb } from "../src/db.js";
import { ingestFile } from "../src/ingest.js";
import { newHealth } from "../src/format-health.js";
import { LiveSnapshotSchema, sessionUsage } from "../src/live-snapshot.js";
import { assistant, tmpDir, writeLines } from "./helpers.js";

function ingest(db: ReturnType<typeof openDb>, file: string, isMain: boolean, lines: string[]): void {
  writeLines(file, lines);
  ingestFile(db, { provider: "claude", path: file, project: "p", sessionId: "s1", agentId: isMain ? "main" : "a1", agentType: isMain ? "main" : "Explore", isMain }, newHealth());
}

describe("sessionUsage", () => {
  it("is all zeros and a null context for a session with no calls", () => {
    expect(sessionUsage(openDb(":memory:"), 200_000)).toEqual({
      context: { tokens: null, window: 200_000, percent: null },
      usage: { calls: 0, subagentCalls: 0, contextTokens: 0, outputTokens: 0, callsOver200k: 0 },
    });
  });

  it("sums every call, counts subagent calls, and reads the context from the newest MAIN call", () => {
    const db = openDb(":memory:");
    const dir = tmpDir();
    ingest(db, path.join(dir, "s1.jsonl"), true, [
      assistant({ id: "m1", ts: "2026-10-04T10:00:00.000Z", input: 100, cacheRead: 50_000, cacheCreation: 10_000, output: 20 }),
      assistant({ id: "m2", ts: "2026-10-04T10:05:00.000Z", input: 200, cacheRead: 80_000, cacheCreation: 5_000, output: 30 }),
    ]);
    ingest(db, path.join(dir, "agent-a1.jsonl"), false, [
      assistant({ id: "m3", ts: "2026-10-04T10:09:00.000Z", input: 5, cacheRead: 250_000, cacheCreation: 0, output: 7, sidechain: true }),
    ]);
    expect(sessionUsage(db, 200_000)).toEqual({
      context: { tokens: 85_200, window: 200_000, percent: 43 },
      usage: { calls: 3, subagentCalls: 1, contextTokens: 60_100 + 85_200 + 250_005, outputTokens: 57, callsOver200k: 1 },
    });
  });

  it("scales the percent by the window and clamps it at 100", () => {
    const db = openDb(":memory:");
    ingest(db, path.join(tmpDir(), "s1.jsonl"), true, [assistant({ id: "m1", ts: "2026-10-04T10:00:00.000Z", input: 300_000 })]);
    expect(sessionUsage(db, 1_000_000).context.percent).toBe(30);
    expect(sessionUsage(db, 200_000).context.percent).toBe(100);
  });
});

describe("LiveSnapshotSchema", () => {
  it("rejects a snapshot of another version and a percent outside 0..100", () => {
    const base = {
      v: 1, sessionId: "s1", at: "2026-10-04T10:00:00.000Z", transcript: { found: false, files: 0 }, ingest: { newLines: 0, ms: 1 },
      context: { tokens: null, window: 200_000, percent: null },
      usage: { calls: 0, subagentCalls: 0, contextTokens: 0, outputTokens: 0, callsOver200k: 0 },
      judge: { state: "none" }, contextGuard: { fires: 0 }, wakes: { queuedNow: 0 },
    };
    expect(LiveSnapshotSchema.safeParse(base).success).toBe(true);
    expect(LiveSnapshotSchema.safeParse({ ...base, v: 2 }).success).toBe(false);
    expect(LiveSnapshotSchema.safeParse({ ...base, context: { tokens: 1, window: 200_000, percent: 120 } }).success).toBe(false);
  });
});
```

Create `scorer/tests/live.test.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { openDb as openJudgeDb, recordDecision, recordDecisionDetails } from "../../judge/src/db.js";
import { parseArgs } from "../src/args.js";
import type { CliOptions } from "../src/args.js";
import { findSessionFiles, pruneLiveDbs, renderLive, runLive } from "../src/live.js";
import type { LiveSnapshot } from "../src/live-snapshot.js";
import { LiveSnapshotSchema } from "../src/live-snapshot.js";
import { projectFromCwd } from "../src/transcript/source.js";
import { appendRaw, assistant, tmpDir, user, writeLines } from "./helpers.js";

const NOW = new Date("2026-10-04T12:00:00.000Z");
const CWD = "/work/acme.app";

interface World { root: string; options: CliOptions; transcript: string; state: string }

function world(extra: string[] = [], lines?: string[]): World {
  const root = tmpDir();
  const transcript = path.join(root, "projects", projectFromCwd(CWD), "s1.jsonl");
  if (lines !== undefined) writeLines(transcript, lines);
  const state = path.join(root, "state");
  const parsed = parseArgs(["live", "--session", "s1", "--cwd", CWD, "--window", "200000", "--projects-dir", path.join(root, "projects"), "--state-dir", state, ...extra], NOW, "/home/x");
  if (!parsed.ok) throw new Error(parsed.error);
  return { root, options: parsed.options, transcript, state };
}

const calls = (): string[] => [
  user("hello", { ts: "2026-10-04T10:00:00.000Z" }),
  assistant({ id: "m1", ts: "2026-10-04T10:00:01.000Z", input: 100, cacheRead: 40_000, output: 10 }),
  assistant({ id: "m2", ts: "2026-10-04T10:01:00.000Z", input: 100, cacheRead: 84_000, output: 20 }),
];

describe("runLive", () => {
  it("reads this session's transcript into a snapshot that satisfies the schema", () => {
    const w = world([], calls());
    const snap = runLive(w.options, () => NOW.getTime());
    expect(LiveSnapshotSchema.parse(snap)).toEqual(snap);
    expect(snap).toMatchObject({
      v: 1, sessionId: "s1", at: "2026-10-04T12:00:00.000Z",
      transcript: { found: true, files: 1 },
      context: { tokens: 84_100, window: 200_000, percent: 42 },
      usage: { calls: 2, subagentCalls: 0, contextTokens: 40_100 + 84_100, outputTokens: 30, callsOver200k: 0 },
      judge: { state: "none" }, contextGuard: { fires: 0 }, wakes: { queuedNow: 0 },
    });
    expect(snap.ingest.newLines).toBeGreaterThan(0);
  });

  it("is incremental: an unchanged transcript ingests nothing, an appended line ingests only itself", () => {
    const w = world([], calls());
    const first = runLive(w.options);
    const again = runLive(w.options);
    expect(again.ingest.newLines).toBe(0);
    expect(again.usage).toEqual(first.usage);
    appendRaw(w.transcript, `${assistant({ id: "m3", ts: "2026-10-04T10:02:00.000Z", input: 10, cacheRead: 90_000, output: 5 })}\n`);
    const grown = runLive(w.options);
    expect(grown.ingest.newLines).toBe(1);
    expect(grown.usage.calls).toBe(3);
    expect(grown.context.tokens).toBe(90_010);
    expect(fs.existsSync(path.join(w.state, "scorer", "live", "s1.sqlite"))).toBe(true);
  });

  it("counts a half-written last line only once it is complete (the offset stops at the last newline)", () => {
    const w = world([], calls());
    runLive(w.options);
    const line = assistant({ id: "m3", ts: "2026-10-04T10:02:00.000Z", input: 10, cacheRead: 90_000, output: 5 });
    appendRaw(w.transcript, line.slice(0, 40));
    expect(runLive(w.options).usage.calls).toBe(2);
    appendRaw(w.transcript, `${line.slice(40)}\n`);
    const done = runLive(w.options);
    expect(done.usage.calls).toBe(3);
    expect(done.ingest.newLines).toBe(1);
  });

  it("never touches scorer.sqlite, the daily report's database", () => {
    const w = world([], calls());
    runLive(w.options);
    expect(fs.existsSync(path.join(w.state, "scorer", "scorer.sqlite"))).toBe(false);
  });

  it("adds this session's judge activity, context-guard fires and queued wakes, and leaves the judge db unchanged", () => {
    const w = world([], calls());
    const judgeFile = path.join(w.state, "judge", "decisions.sqlite");
    fs.mkdirSync(path.dirname(judgeFile), { recursive: true });
    const db = openJudgeDb(judgeFile);
    recordDecision(db, { id: "d1", ts: "2026-10-04T10:00:30.000Z", question: "ask-check", content_class: "brief", provider: "jev", decision: "continue", confidence: 0.9, reason_code: "jev", latency_ms: 350, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" });
    recordDecisionDetails(db, { id: "d1", input_json: "{}", probabilities: null, rules_opinion: "continue", agreement: "agreed", session_id: "s1" });
    db.pragma("wal_checkpoint(TRUNCATE)");
    db.close();
    const guard = path.join(w.state, "context-guard");
    fs.mkdirSync(guard, { recursive: true });
    fs.writeFileSync(path.join(guard, "fires.jsonl"), [
      JSON.stringify({ ts: "2026-10-04T10:00:00Z", agentId: null, tokens: 210_000, sessionId: "s1" }),
      JSON.stringify({ ts: "2026-10-04T10:00:00Z", agentId: null, tokens: 210_000, sessionId: "other" }),
    ].join("\n"));
    const outbox = path.join(w.state, "judge", "outbox");
    fs.mkdirSync(outbox, { recursive: true });
    fs.writeFileSync(path.join(outbox, "s1.jsonl"), `${JSON.stringify({ ts: "2026-10-04T10:00:00Z", agentType: null, text: "step done" })}\n`);
    const before = fs.readFileSync(judgeFile);

    const snap = runLive(w.options);
    expect(snap.judge).toMatchObject({ state: "ok", calls: 1, agreed: 1, byProvider: { jev: 1 }, latest: { id: "d1", decision: "continue" } });
    expect(snap.contextGuard.fires).toBe(1);
    expect(snap.wakes.queuedNow).toBe(1);
    expect(fs.readFileSync(judgeFile).equals(before)).toBe(true);
  });

  it("reports no transcript without creating a database file when the session has no files", () => {
    const w = world();
    const snap = runLive(w.options);
    expect(snap).toMatchObject({ transcript: { found: false, files: 0 }, context: { tokens: null, percent: null }, usage: { calls: 0 } });
    expect(fs.existsSync(path.join(w.state, "scorer", "live"))).toBe(false);
  });

  it("finds the transcript without a cwd, and when the cwd names the wrong project", () => {
    const w = world([], calls());
    const noCwd = { ...w.options, liveCwd: null };
    expect(runLive(noCwd).transcript.found).toBe(true);
    expect(runLive({ ...w.options, liveCwd: "/somewhere/else" }).transcript.found).toBe(true);
  });
});

describe("findSessionFiles", () => {
  it("prefers the hinted project and falls back to a scan", () => {
    const w = world([], calls());
    const projects = path.join(w.root, "projects");
    expect(findSessionFiles(projects, "s1", CWD)).toHaveLength(1);
    expect(findSessionFiles(projects, "s1", null)).toHaveLength(1);
    expect(findSessionFiles(projects, "s1", "/other")).toHaveLength(1);
    expect(findSessionFiles(projects, "nope", CWD)).toEqual([]);
  });
});

describe("pruneLiveDbs", () => {
  const DAY = 86_400_000;

  function fill(n: number, ageDays: number): string {
    const dir = tmpDir();
    for (let i = 0; i < n; i += 1) {
      const file = path.join(dir, `old-${i}.sqlite`);
      fs.writeFileSync(file, "");
      const t = new Date(NOW.getTime() - ageDays * DAY);
      fs.utimesSync(file, t, t);
    }
    return dir;
  }

  it("does nothing while the directory is small", () => {
    const dir = fill(30, 20);
    expect(pruneLiveDbs(dir, "s1", NOW.getTime())).toBe(0);
    expect(fs.readdirSync(dir)).toHaveLength(30);
  });

  it("removes sessions untouched for a week, keeps fresh ones and the current session's files, and survives a dangling link", () => {
    const dir = fill(31, 10);
    const fresh = path.join(dir, "fresh.sqlite");
    fs.writeFileSync(fresh, "");
    fs.writeFileSync(path.join(dir, "s1.sqlite"), "");
    fs.writeFileSync(path.join(dir, "s1.sqlite-wal"), "");
    for (const keep of ["s1.sqlite", "s1.sqlite-wal"]) {
      const t = new Date(NOW.getTime() - 30 * DAY);
      fs.utimesSync(path.join(dir, keep), t, t);
    }
    fs.symlinkSync(path.join(dir, "missing-target"), path.join(dir, "dangling.sqlite"));
    expect(pruneLiveDbs(dir, "s1", Date.now())).toBe(31);
    expect(fs.readdirSync(dir).sort()).toEqual(["dangling.sqlite", "fresh.sqlite", "s1.sqlite", "s1.sqlite-wal"]);
  });
});

describe("renderLive", () => {
  const base: LiveSnapshot = {
    v: 1, sessionId: "s1", at: "2026-10-04T12:00:00.000Z", transcript: { found: true, files: 1 }, ingest: { newLines: 0, ms: 5 },
    context: { tokens: 84_100, window: 200_000, percent: 42 },
    usage: { calls: 31, subagentCalls: 9, contextTokens: 1_900_000, outputTokens: 52_000, callsOver200k: 2 },
    judge: { state: "none" }, contextGuard: { fires: 1 }, wakes: { queuedNow: 2 },
  };
  const ok = {
    state: "ok" as const, calls: 4, byProvider: { jev: 3, rules: 1 }, undecided: 1, agreed: 2, overrode: 1, p50LatencyMs: 350, p95LatencyMs: 900,
    gates: {
      "scope-gate": { question: "brief-scope", fired: 1, byDecision: {}, p50LatencyMs: 0 },
      "done-gate": { question: "ask-check", fired: 2, byDecision: {}, p50LatencyMs: 0 },
      "send-gate": { question: "wake-gate", fired: 0, byDecision: {}, p50LatencyMs: 0 },
    },
    labelable: false, latest: null,
  };

  it("prints context, session, judge, gates and wakes", () => {
    expect(renderLive({ ...base, judge: ok })).toBe([
      "context   84.1k of 200.0k (42%)",
      "session   31 calls (9 subagent) · 1.9M in · 52.0k out · 2 over 200k",
      "judge     4 calls · jev 75.0% · rules 25.0% · agreed 2 · overrode 1 · unsure 1 · p50 350ms",
      "gates     scope-gate 1 · done-gate 2 · send-gate 0",
      "wakes     2 queued · context-guard 1 fires",
      "",
    ].join("\n"));
  });

  it("says so when there is no transcript, an unknown context, no judge activity, or an unlinked judge", () => {
    expect(renderLive({ ...base, transcript: { found: false, files: 0 } })).toContain("context   no transcript found for this session");
    expect(renderLive({ ...base, context: { tokens: null, window: 200_000, percent: null } })).toContain("context   unknown");
    expect(renderLive({ ...base, judge: { state: "unscoped" } })).toContain("judge     not linked to sessions yet");
    expect(renderLive({ ...base, judge: { ...ok, calls: 0, byProvider: {} } })).toContain("judge     0 calls · agreed 2");
  });
});
```

- [ ] **Step 2: Run, verify they fail**

Run: `cd scorer && npx vitest run tests/args.test.ts tests/live-snapshot.test.ts tests/live.test.ts`
Expected: FAIL (`live-snapshot.js` / `live.js` not found; args cases fail).

- [ ] **Step 3: Implement**

Create `scorer/src/live-snapshot.ts`:

```ts
import { z } from "zod";

import type { Db } from "./db.js";
import { JudgeLiveSchema } from "./live-judge.js";
import { CTX, OVER_200K } from "./metrics.js";

const Count = z.number().int().nonnegative();

// The contract with the aw-live mod (mods/aw-live/hooks/lib/snapshot.ts). Additive
// changes only; a breaking one bumps `v` and the mod ignores snapshots it does not know.
export const LiveSnapshotSchema = z.object({
  v: z.literal(1),
  sessionId: z.string(),
  at: z.string(),
  transcript: z.object({ found: z.boolean(), files: Count }),
  ingest: z.object({ newLines: Count, ms: z.number().nonnegative() }),
  context: z.object({
    tokens: Count.nullable(),
    window: z.number().int().positive(),
    percent: z.number().min(0).max(100).nullable(),
  }),
  usage: z.object({ calls: Count, subagentCalls: Count, contextTokens: Count, outputTokens: Count, callsOver200k: Count }),
  judge: JudgeLiveSchema,
  contextGuard: z.object({ fires: Count }),
  wakes: z.object({ queuedNow: Count }),
});
export type LiveSnapshot = z.infer<typeof LiveSnapshotSchema>;

// Token figures for the session whose files `db` holds, from the same call table, the same
// context-size expression (CTX) and the same 200k line (OVER_200K) the daily report uses.
// Dollar cost is not here: the scorer reads no prices, and the host reports its own.
export function sessionUsage(db: Db, window: number): Pick<LiveSnapshot, "context" | "usage"> {
  const totals = db.prepare(`
    SELECT COUNT(*) AS calls,
      COALESCE(SUM(CASE WHEN is_main = 0 THEN 1 ELSE 0 END), 0) AS sub,
      COALESCE(SUM(${CTX}), 0) AS ctx,
      COALESCE(SUM(output), 0) AS out,
      COALESCE(SUM(CASE WHEN ${CTX} > ${OVER_200K} THEN 1 ELSE 0 END), 0) AS over
    FROM calls`).get() as { calls: number; sub: number; ctx: number; out: number; over: number };
  const last = db.prepare(`SELECT ${CTX} AS tokens FROM calls WHERE is_main = 1 ORDER BY ts DESC, rowid DESC LIMIT 1`).get() as { tokens: number } | undefined;
  const tokens = last?.tokens ?? null;
  return {
    context: { tokens, window, percent: tokens === null ? null : Math.min(100, Math.round((tokens / window) * 100)) },
    usage: { calls: totals.calls, subagentCalls: totals.sub, contextTokens: totals.ctx, outputTokens: totals.out, callsOver200k: totals.over },
  };
}
```

Create `scorer/src/live.ts`:

```ts
import fs from "node:fs";
import path from "node:path";

import type { CliOptions } from "./args.js";
import { firesForSession, readFiresLog } from "./context-guard-fires.js";
import { openDb } from "./db.js";
import type { Db } from "./db.js";
import { ingestAll } from "./ingest.js";
import { readJudgeLive } from "./live-judge.js";
import type { LiveSnapshot } from "./live-snapshot.js";
import { sessionUsage } from "./live-snapshot.js";
import { queuedForSession } from "./outbox.js";
import { formatPct, formatTokens } from "./render.js";
import { resolveStateDir } from "./run.js";
import { discoverSession } from "./transcript/discover.js";
import type { TranscriptFile } from "./transcript/source.js";
import { projectFromCwd } from "./transcript/source.js";

const PRUNE_ABOVE = 30; // files in the live dir before an old-session sweep runs
const MAX_AGE_MS = 7 * 86_400_000;

// The cwd names the project directory directly; without it (or when the session moved
// projects) every project directory is checked.
export function findSessionFiles(projectsDir: string, sessionId: string, cwd: string | null): TranscriptFile[] {
  if (cwd !== null) {
    const hinted = discoverSession(projectsDir, sessionId, projectFromCwd(cwd));
    if (hinted.length > 0) return hinted;
  }
  return discoverSession(projectsDir, sessionId);
}

// Per-session databases keep the live path off scorer.sqlite (the daily report's) and make
// "this session" a whole database, not a filter. Sessions untouched for a week are swept.
export function pruneLiveDbs(dir: string, keepSession: string, nowMs: number): number {
  const entries = fs.readdirSync(dir);
  if (entries.length <= PRUNE_ABOVE) return 0;
  let removed = 0;
  for (const name of entries) {
    if (name.startsWith(`${keepSession}.`)) continue;
    const file = path.join(dir, name);
    const stat = fs.statSync(file, { throwIfNoEntry: false });
    if (stat !== undefined && nowMs - stat.mtimeMs > MAX_AGE_MS) {
      fs.rmSync(file, { force: true });
      removed += 1;
    }
  }
  return removed;
}

function openLiveDb(stateDir: string, sessionId: string, hasFiles: boolean, nowMs: number): Db {
  if (!hasFiles) return openDb(":memory:");
  const dir = path.join(stateDir, "scorer", "live");
  fs.mkdirSync(dir, { recursive: true });
  pruneLiveDbs(dir, sessionId, nowMs);
  return openDb(path.join(dir, `${sessionId}.sqlite`));
}

// `scorer live`: this session's numbers. Incremental: each call reads only the bytes
// appended to the session's transcripts since the last call (ingest.ts keeps the offsets).
export function runLive(options: CliOptions, nowMs: () => number = Date.now): LiveSnapshot {
  const sessionId = options.liveSession as string; // args.ts guarantees it for the live command
  const stateDir = resolveStateDir(options);
  const started = performance.now();
  const files = findSessionFiles(options.projectsDir, sessionId, options.liveCwd);
  const db = openLiveDb(stateDir, sessionId, files.length > 0, nowMs());
  try {
    const health = ingestAll(db, files);
    const { context, usage } = sessionUsage(db, options.liveWindow);
    return {
      v: 1,
      sessionId,
      at: new Date(nowMs()).toISOString(),
      transcript: { found: files.length > 0, files: files.length },
      ingest: { newLines: health.lines, ms: Math.round(performance.now() - started) },
      context,
      usage,
      judge: readJudgeLive(path.join(stateDir, "judge", "decisions.sqlite"), sessionId),
      contextGuard: { fires: firesForSession(readFiresLog(path.join(stateDir, "context-guard")), sessionId).length },
      wakes: { queuedNow: queuedForSession(path.join(stateDir, "judge", "outbox"), sessionId) },
    };
  } finally {
    db.close();
  }
}

export function renderLive(s: LiveSnapshot): string {
  const lines = [
    s.transcript.found
      ? `context   ${s.context.tokens === null ? "unknown" : `${formatTokens(s.context.tokens)} of ${formatTokens(s.context.window)} (${s.context.percent}%)`}`
      : "context   no transcript found for this session",
    `session   ${s.usage.calls} calls (${s.usage.subagentCalls} subagent) · ${formatTokens(s.usage.contextTokens)} in · ${formatTokens(s.usage.outputTokens)} out · ${s.usage.callsOver200k} over 200k`,
  ];
  if (s.judge.state === "unscoped") lines.push("judge     not linked to sessions yet (judge predates decision_details.session_id)");
  if (s.judge.state === "ok") {
    const j = s.judge;
    const providers = Object.entries(j.byProvider).map(([name, n]) => `${name} ${formatPct(n / Math.max(1, j.calls))}`).join(" · ");
    lines.push(`judge     ${j.calls} calls${providers === "" ? "" : ` · ${providers}`} · agreed ${j.agreed} · overrode ${j.overrode} · unsure ${j.undecided} · p50 ${j.p50LatencyMs}ms`);
    lines.push(`gates     ${Object.entries(j.gates).map(([name, g]) => `${name} ${g.fired}`).join(" · ")}`);
  }
  lines.push(`wakes     ${s.wakes.queuedNow} queued · context-guard ${s.contextGuard.fires} fires`);
  return `${lines.join("\n")}\n`;
}
```

Replace `scorer/src/args.ts` entirely with:

```ts
import path from "node:path";

import type { ProviderName } from "./transcript/source.js";
import { isProviderName, PROVIDERS } from "./transcript/source.js";

export interface CliOptions {
  command: "report" | "probe" | "context-tokens" | "live";
  since: Date;
  until: Date;
  projectsDir: string; // Claude Code transcripts
  codexSessionsDir: string;
  cursorProjectsDir: string;
  // null = every provider whose transcript directory exists (resolved in run.ts).
  providers: ProviderName[] | null;
  stateDir: string;
  // Whether --state-dir was actually passed on the command line, vs. left at
  // its home-dir default — needed so the judge-db path lookup (run.ts's
  // resolveJudgeDbPath) can give an explicit --state-dir priority over
  // AW_STATE_DIR, and AW_STATE_DIR priority over the bare default, rather
  // than only ever checking AW_STATE_DIR regardless of --state-dir.
  stateDirExplicit: boolean;
  prLookup: boolean;
  contextTokensPath: string | null;
  // `scorer live`: one session's numbers. The window is the model's context window
  // (the host knows it; 200k is the fallback), so percent-of-window is right on any model.
  liveSession: string | null;
  liveCwd: string | null;
  liveWindow: number;
  json: boolean;
}

type ParseResult = { ok: true; options: CliOptions } | { ok: false; error: string };

const UNIT_MS: Record<string, number> = { h: 3_600_000, d: 86_400_000 };

export function parseArgs(argv: string[], now: Date, home: string): ParseResult {
  const options: CliOptions = {
    command: "report",
    since: new Date(now.getTime() - UNIT_MS.d),
    until: now,
    projectsDir: path.join(home, ".claude", "projects"),
    codexSessionsDir: path.join(home, ".codex", "sessions"),
    cursorProjectsDir: path.join(home, ".cursor", "projects"),
    providers: null,
    stateDir: path.join(home, ".agentic-workflow"),
    stateDirExplicit: false,
    prLookup: true,
    contextTokensPath: null,
    liveSession: null,
    liveCwd: null,
    liveWindow: 200_000,
    json: false,
  };
  const args = [...argv];
  while (args.length > 0) {
    const arg = args.shift() as string;
    if (arg === "probe") { options.command = "probe"; continue; }
    if (arg === "context-tokens") {
      options.command = "context-tokens";
      const value = args.shift();
      if (value === undefined) return { ok: false, error: "context-tokens needs a path" };
      options.contextTokensPath = value;
      continue;
    }
    if (arg === "live") { options.command = "live"; continue; }
    if (arg === "--json") { options.json = true; continue; }
    if (arg === "--no-pr-lookup") { options.prLookup = false; continue; }
    if (!VALUE_FLAGS.has(arg)) return { ok: false, error: `unknown argument: ${arg}` };
    const value = args.shift();
    if (value === undefined) return { ok: false, error: `${arg} needs a value` };
    if (arg === "--projects-dir") options.projectsDir = value;
    if (arg === "--codex-dir") options.codexSessionsDir = value;
    if (arg === "--cursor-dir") options.cursorProjectsDir = value;
    if (arg === "--provider") {
      const providers = parseProviders(value);
      if (typeof providers === "string") return { ok: false, error: providers };
      options.providers = providers;
    }
    if (arg === "--state-dir") { options.stateDir = value; options.stateDirExplicit = true; }
    if (arg === "--session") {
      // The id becomes part of file paths (live db, outbox file), so it must be a plain token.
      if (!SESSION_ID.test(value)) return { ok: false, error: `--session must be letters, digits, '.', '_' or '-': ${value}` };
      options.liveSession = value;
    }
    if (arg === "--cwd") options.liveCwd = value;
    if (arg === "--window") {
      const window = Number(value);
      if (!Number.isInteger(window) || window <= 0) return { ok: false, error: `--window must be a positive integer: ${value}` };
      options.liveWindow = window;
    }
    if (arg === "--since") {
      const since = parseSince(value, now);
      if (typeof since === "string") return { ok: false, error: since };
      options.since = since;
    }
  }
  if (options.command === "live" && options.liveSession === null) return { ok: false, error: "live needs --session <id>" };
  return { ok: true, options };
}

const VALUE_FLAGS: ReadonlySet<string> = new Set(["--since", "--projects-dir", "--codex-dir", "--cursor-dir", "--state-dir", "--provider", "--session", "--cwd", "--window"]);
const SESSION_ID = /^[A-Za-z0-9._-]+$/;

// "all" | "claude" | "codex,cursor" …
function parseProviders(value: string): ProviderName[] | string {
  if (value === "all") return [...PROVIDERS];
  const names = value.split(",").map((n) => n.trim());
  const valid = names.filter(isProviderName);
  if (valid.length !== names.length) return `--provider must be ${PROVIDERS.join("|")}|all (comma-separated ok): ${value}`;
  return [...new Set(valid)];
}

function parseSince(value: string, now: Date): Date | string {
  const rel = /^(\d+)([hd])$/.exec(value);
  const date = rel ? new Date(now.getTime() - Number(rel[1]) * UNIT_MS[rel[2]]) : new Date(value);
  if (Number.isNaN(date.getTime())) return `--since must be like 7d, 12h or an ISO date: ${value}`;
  if (date.getTime() >= now.getTime()) return `--since must be in the past: ${value}`;
  return date;
}
```

Replace `scorer/src/cli.ts` entirely with:

```ts
#!/usr/bin/env node
import { execFile } from "node:child_process";
import os from "node:os";
import { promisify } from "node:util";

import { parseArgs } from "./args.js";
import { estimateCurrentContextTokens } from "./context-tokens.js";
import { makeGhLookup } from "./pr-state.js";
import { renderLive, runLive } from "./live.js";
import { runProbe } from "./probe/run-probe.js";
import { resolveJudgeDbPath, runReport } from "./run.js";

const exec = promisify(execFile);
const parsed = parseArgs(process.argv.slice(2), new Date(), os.homedir());
if (!parsed.ok) {
  console.error(`scorer: ${parsed.error}`);
  console.error("usage: scorer live --session ID [--cwd DIR] [--window TOKENS] [--json] | scorer [probe] [--since 7d|12h|ISO] [--provider claude|codex|cursor|all] [--projects-dir DIR] [--codex-dir DIR] [--cursor-dir DIR] [--state-dir DIR] [--no-pr-lookup]");
  process.exit(1);
}
if (parsed.options.command === "probe") {
  process.stdout.write(runProbe(parsed.options.stateDir));
} else if (parsed.options.command === "live") {
  // Fail silent for the mod: any error is a bare exit 1 with nothing on stdout.
  try {
    const snapshot = runLive(parsed.options);
    process.stdout.write(parsed.options.json ? `${JSON.stringify(snapshot)}\n` : renderLive(snapshot));
  } catch {
    process.exit(1);
  }
} else if (parsed.options.command === "context-tokens") {
  const tokens = estimateCurrentContextTokens(parsed.options.contextTokensPath as string);
  process.stdout.write(tokens === null ? "null" : String(tokens));
} else {
  const lookup = makeGhLookup(async (cmd, args) => (await exec(cmd, args, { timeout: 10_000 })).stdout);
  const result = await runReport(parsed.options, { lookup, log: (l) => console.log(l), judgeDbPath: resolveJudgeDbPath(parsed.options) });
  if (result.status === "unknown-format") {
    console.error(`scorer: ⚠ UNKNOWN TRANSCRIPT FORMAT — see ${result.markdownPath}`);
    process.exit(3);
  }
}
```

- [ ] **Step 4: Run, verify pass; then the full suite once**

Run: `cd scorer && npx vitest run tests/args.test.ts tests/live-snapshot.test.ts tests/live.test.ts`
Expected: PASS (args 22+, live-snapshot 4, live 12).
Run: `cd scorer && npm run typecheck && npm run test:coverage`
Expected: clean; all pass; 100% on every file.

- [ ] **Step 5: CLI smoke against a build**

Run:
```bash
cd scorer && npm run build && ST="$(mktemp -d)" && node dist/cli.js live --session smoke-1 --projects-dir "$ST/none" --state-dir "$ST" --json; echo "exit=$?"
node dist/cli.js live; echo "exit=$?"
```
Expected: the first prints one JSON line containing `"v":1`, `"transcript":{"found":false,"files":0}`, `"judge":{"state":"none"}` and `exit=0`, and creates no `$ST/scorer` directory (`ls "$ST"` shows nothing). The second prints `scorer: live needs --session <id>` plus the usage line to stderr and `exit=1`.

- [ ] **Step 6: Commit**

```bash
git add scorer/src/live-snapshot.ts scorer/src/live.ts scorer/src/args.ts scorer/src/cli.ts scorer/tests/live-snapshot.test.ts scorer/tests/live.test.ts scorer/tests/args.test.ts
git commit -m "feat: scorer live, an incremental per-session snapshot for the aw-live mod" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

### Task 5: The mod's manifest, types contract and pure libraries

A mod is three files plus its contract (`plugin-authoring` skill, "WHERE TO WRITE IT"). Everything that can be pure is pure and lives in `hooks/lib/`; `register.tsx` (Task 6) owns every `$` call.

**Files:**
- Create: `mods/aw-live/.claude-plugin/plugin.json`, `mods/aw-live/hooks/hooks.json`, `mods/aw-live/types/index.d.ts`
- Create: `mods/aw-live/hooks/lib/snapshot.ts`, `format.ts`, `labels.ts`, `refresh.ts`
- Create: `mods/aw-live/hooks/register.tsx` (an empty scaffold; Task 6 replaces it)
- Create: `mods/aw-live/hooks/tests/fixtures/sample.ts`, `mods/aw-live/hooks/tests/lib.test.ts`
- Create: `scorer/tests/live-contract.test.ts`
- Modify: `.gitignore` (the engine lays `.claude-plugin/types/` into a loaded mod; do not commit it)

**Interfaces:**
- Consumes: the JSON `scorer live --json` prints (Task 4, `LiveSnapshotSchema`).
- Produces (all in `mods/aw-live/`):
  - `types/index.d.ts`: `LiveSnapshot`, `JudgeLive`, `JudgeLatest`, `GateName`, `GateStats`, `HostUsage`, `LiveStatus`, and `PluginState['aw-live'] = { snapshot, host, status }`
  - `lib/snapshot.ts`: `parseSnapshot(text: string): LiveSnapshot | null`
  - `lib/format.ts`: `fmtTokens`, `cut`, `bar`, `contextPercent`, `providerShare`, `bandText(s, host, columns): string | null`, `paneSections(s, host): Section[]`, `statusText(s | null, host): string`, `WARN_PERCENT`, types `Tone`, `Row`, `Section`
  - `lib/labels.ts`: `FLIP`, `LabelKind`, `labelFor(latest: JudgeLatest, kind: LabelKind): string | null`
  - `lib/refresh.ts`: `TIMER_MS = 10_000`, `MIN_GAP_MS = 2_000`, `RUN_TIMEOUT_MS = 5_000`, `liveArgs(sessionId, cwd, window | null): string[]`, `binCandidates(home | undefined, 'scorer' | 'judge', override?): string[]`, `isDue(lastAt | null, now, minGapMs?): boolean`
  - fixture `SAMPLE_JSON: string` (a valid snapshot with an `ok` judge, `labelable: true`, latest `ask-check` decision `d-9` with `itemId` `item-9`)

- [ ] **Step 1: Write the manifest, contract and fixture** (data, no behavior yet)

Create `mods/aw-live/.claude-plugin/plugin.json`:

```json
{
  "name": "aw-live",
  "version": "0.1.0",
  "description": "Live scorer numbers inside Claude Code: a /live pane and a one-line band for context, cost, judge activity, gates and wakes.",
  "author": { "name": "agentic-workflow" },
  "types": "./types/index.d.ts"
}
```

Create `mods/aw-live/hooks/hooks.json`:

```json
{ "modules": ["./register.tsx"] }
```

Create `mods/aw-live/types/index.d.ts`:

```ts
export type GateName = 'scope-gate' | 'done-gate' | 'send-gate'

export type GateStats = {
  question: string
  fired: number
  byDecision: Record<string, number>
  p50LatencyMs: number
}

export type JudgeLatest = {
  id: string
  ts: string
  question: string
  decision: string
  provider: string
  confidence: number
  /** The eval_items id for this decision, once `judge label import` has made one. */
  itemId: string | null
}

export type JudgeLive =
  | { state: 'none' }
  | { state: 'unscoped' }
  | {
      state: 'ok'
      calls: number
      byProvider: Record<string, number>
      undecided: number
      agreed: number
      overrode: number
      p50LatencyMs: number
      p95LatencyMs: number
      gates: Record<GateName, GateStats>
      labelable: boolean
      latest: JudgeLatest | null
    }

export type LiveSnapshot = {
  v: 1
  sessionId: string
  at: string
  transcript: { found: boolean; files: number }
  ingest: { newLines: number; ms: number }
  context: { tokens: number | null; window: number; percent: number | null }
  usage: { calls: number; subagentCalls: number; contextTokens: number; outputTokens: number; callsOver200k: number }
  judge: JudgeLive
  contextGuard: { fires: number }
  wakes: { queuedNow: number }
}

/** What the host itself reports; shown in preference to the transcript's estimate. */
export type HostUsage = { usd: number | null; percent: number | null; window: number | null }

export type LiveStatus = { ok: boolean; at: number }

declare module 'claude-code' {
  interface PluginState {
    'aw-live': { snapshot: LiveSnapshot | null; host: HostUsage | null; status: LiveStatus | null }
  }
}
```

Create `mods/aw-live/hooks/tests/fixtures/sample.ts`:

```ts
// A synthetic `scorer live --json` result. scorer/tests/live-contract.test.ts reads this
// file as text, takes the JSON between the backticks and validates it against the zod
// schema, so edit the JSON, not the delimiters.
export const SAMPLE_JSON = `{
  "v": 1,
  "sessionId": "session-1",
  "at": "2026-10-04T12:00:00.000Z",
  "transcript": { "found": true, "files": 2 },
  "ingest": { "newLines": 12, "ms": 31 },
  "context": { "tokens": 84000, "window": 200000, "percent": 42 },
  "usage": { "calls": 31, "subagentCalls": 9, "contextTokens": 1900000, "outputTokens": 52000, "callsOver200k": 2 },
  "judge": {
    "state": "ok",
    "calls": 12,
    "byProvider": { "jev": 10, "rules": 2 },
    "undecided": 3,
    "agreed": 6,
    "overrode": 3,
    "p50LatencyMs": 380,
    "p95LatencyMs": 6100,
    "gates": {
      "scope-gate": { "question": "brief-scope", "fired": 4, "byDecision": { "ready": 3, "missing": 1 }, "p50LatencyMs": 400 },
      "done-gate": { "question": "ask-check", "fired": 5, "byDecision": { "continue": 4, "ask": 1 }, "p50LatencyMs": 350 },
      "send-gate": { "question": "wake-gate", "fired": 3, "byDecision": { "send": 2, "batch": 1 }, "p50LatencyMs": 360 }
    },
    "labelable": true,
    "latest": { "id": "d-9", "ts": "2026-10-04T11:59:00.000Z", "question": "ask-check", "decision": "continue", "provider": "jev", "confidence": 0.9, "itemId": "item-9" }
  },
  "contextGuard": { "fires": 1 },
  "wakes": { "queuedNow": 2 }
}`
```

```tsx
// mods/aw-live/hooks/register.tsx  (scaffold; Task 6 replaces it)
import type { Register } from 'claude-code'

export const register: Register = () => {}
```

Append to `.gitignore`:

```
# Claude Code lays the function-hooks API types into a mod it loads
mods/*/.claude-plugin/types/
```

- [ ] **Step 2: Write the failing tests**

Create `mods/aw-live/hooks/tests/lib.test.ts`:

```ts
import { expect, test } from 'claude-code/testing'

import type { LiveSnapshot } from '../../types'
import { FLIP, labelFor } from '../lib/labels'
import { MIN_GAP_MS, binCandidates, isDue, liveArgs } from '../lib/refresh'
import { parseSnapshot } from '../lib/snapshot'
import { bandText, bar, contextPercent, cut, fmtTokens, paneSections, providerShare, statusText } from '../lib/format'
import { SAMPLE_JSON } from './fixtures/sample'

const sample = (): LiveSnapshot => JSON.parse(SAMPLE_JSON) as LiveSnapshot
const HOST = { usd: 1.839, percent: 44, window: 1_000_000 }

test('parseSnapshot accepts the sample and rejects everything else', () => {
  expect(parseSnapshot(SAMPLE_JSON)?.sessionId).toBe('session-1')
  expect(parseSnapshot('not json')).toBeNull()
  expect(parseSnapshot('[]')).toBeNull()
  expect(parseSnapshot('null')).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), v: 2 }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), sessionId: 3 }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), context: null }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), judge: { state: 'weird' } }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), judge: 'ok' }))).toBeNull()
  expect(parseSnapshot(JSON.stringify({ ...sample(), judge: { state: 'none' } }))?.judge.state).toBe('none')
})

test('number formatting', () => {
  expect([fmtTokens(950), fmtTokens(84_000), fmtTokens(1_900_000)]).toEqual(['950', '84k', '1.9M'])
  expect(bar(42, 10)).toBe('▓▓▓▓░░░░░░')
  expect(bar(null, 4)).toBe('░░░░')
  expect(bar(250, 4)).toBe('▓▓▓▓')
  expect(cut('abcdef', 4)).toBe('abc…')
  expect(cut('abc', 4)).toBe('abc')
  expect(providerShare({ jev: 10, rules: 2, 'claude-cli': 1 })).toBe('jev 77% · rules 15%')
  expect(providerShare({})).toBe('')
})

test('the host percent wins over the transcript estimate', () => {
  expect(contextPercent(sample(), HOST)).toBe(44)
  expect(contextPercent(sample(), null)).toBe(42)
  expect(contextPercent(sample(), { usd: null, percent: null, window: null })).toBe(42)
})

test('band text: headline numbers, quiet parts omitted', () => {
  expect(bandText(sample(), HOST, 200)).toBe('live · ctx 44% · $1.84 · 31 calls · 2 >200k · judge 12 (3 unsure) · gates 12 · 2 queued')
  const quiet: LiveSnapshot = { ...sample(), usage: { ...sample().usage, callsOver200k: 0 }, judge: { state: 'none' }, wakes: { queuedNow: 0 } }
  expect(bandText(quiet, null, 200)).toBe('live · ctx 42% · 31 calls')
  expect(bandText(sample(), HOST, 20)).toHaveLength(20)
})

test('band text: judge without unsure answers; nothing to say without a transcript or host', () => {
  const s = sample()
  if (s.judge.state !== 'ok') throw new Error('fixture')
  const calm: LiveSnapshot = { ...s, judge: { ...s.judge, undecided: 0 } }
  expect(bandText(calm, null, 200)).toContain('judge 12 ·')
  const none: LiveSnapshot = { ...s, transcript: { found: false, files: 0 }, context: { tokens: null, window: 200000, percent: null } }
  expect(bandText(none, null, 200)).toBeNull()
  expect(bandText(none, HOST, 200)).toContain('ctx 44%')
  const noPercent: LiveSnapshot = { ...none, usage: { ...none.usage, calls: 0, callsOver200k: 0 }, judge: { state: 'none' }, wakes: { queuedNow: 0 } }
  expect(bandText(noPercent, { usd: null, percent: null, window: null }, 200)).toBe('live · 0 calls')
})

test('pane sections for an ok judge, an unscoped judge and no judge', () => {
  const titles = (s: LiveSnapshot) => paneSections(s, HOST).map(x => x.title)
  expect(titles(sample())).toEqual(['context', 'session', 'judge', 'gates', 'wakes'])
  expect(titles({ ...sample(), judge: { state: 'unscoped' } })).toEqual(['context', 'session', 'judge', 'wakes'])
  expect(titles({ ...sample(), judge: { state: 'none' } })).toEqual(['context', 'session', 'wakes'])
  const gates = paneSections(sample(), HOST).find(x => x.title === 'gates')
  expect(gates?.rows[1]).toEqual({ label: 'done-gate', value: '5 · continue 4 · ask 1 · p50 350 ms', tone: 'ok' })
  const ctx = paneSections(sample(), { usd: null, percent: 91, window: null })[0]
  expect(ctx?.rows[0]?.tone).toBe('warn')
  expect(ctx?.rows[1]?.value).toBe('84k of 200k')
})

test('pane sections: unknown values read as dashes; quiet gates say so', () => {
  const s = sample()
  if (s.judge.state !== 'ok') throw new Error('fixture')
  const empty: LiveSnapshot = {
    ...s,
    context: { tokens: null, window: 200000, percent: null },
    usage: { ...s.usage, callsOver200k: 0 },
    wakes: { queuedNow: 0 },
    contextGuard: { fires: 0 },
    judge: { ...s.judge, byProvider: {}, undecided: 0, gates: {
      'scope-gate': { question: 'brief-scope', fired: 0, byDecision: {}, p50LatencyMs: 0 },
      'done-gate': { question: 'ask-check', fired: 0, byDecision: {}, p50LatencyMs: 0 },
      'send-gate': { question: 'wake-gate', fired: 0, byDecision: {}, p50LatencyMs: 0 },
    } },
  }
  const sections = paneSections(empty, null)
  expect(sections[0]?.rows[0]?.value).toBe('-- ░░░░░░░░░░')
  expect(sections[0]?.rows[1]?.value).toBe('-- of 200k')
  expect(sections[1]?.rows[0]?.value).toBe('--')
  expect(sections[2]?.rows[0]?.value).toBe('12')
  expect(sections[3]?.rows.map(r => r.value)).toEqual(['quiet', 'quiet', 'quiet'])
})

test('refresh helpers', () => {
  expect(liveArgs('s1', '/repo', 1_000_000)).toEqual(['live', '--session', 's1', '--cwd', '/repo', '--window', '1000000', '--json'])
  expect(liveArgs('s1', '/repo', null)).toEqual(['live', '--session', 's1', '--cwd', '/repo', '--json'])
  expect(binCandidates('/home/me', 'scorer')).toEqual(['/home/me/.local/bin/scorer', 'scorer'])
  expect(binCandidates(undefined, 'judge')).toEqual(['judge'])
  expect(binCandidates('', 'judge')).toEqual(['judge'])
  expect(binCandidates('/home/me', 'judge', '/x/judge')).toEqual(['/x/judge', '/home/me/.local/bin/judge', 'judge'])
  expect(binCandidates('/home/me', 'judge', '')).toEqual(['/home/me/.local/bin/judge', 'judge'])
  expect(isDue(null, 5)).toBe(true)
  expect(isDue(1000, 1000 + MIN_GAP_MS - 1)).toBe(false)
  expect(isDue(1000, 1000 + MIN_GAP_MS)).toBe(true)
})

test('labels: confirm keeps the decision; wrong flips only two-answer questions', () => {
  const latest = { id: 'd', ts: 't', question: 'ask-check', decision: 'continue', provider: 'jev', confidence: 0.9, itemId: 'i' }
  expect(labelFor(latest, 'confirm')).toBe('continue')
  expect(labelFor(latest, 'wrong')).toBe('ask')
  expect(labelFor({ ...latest, decision: 'ask' }, 'wrong')).toBe('continue')
  expect(labelFor({ ...latest, question: 'wake-gate', decision: 'send' }, 'wrong')).toBeNull()
  expect(labelFor({ ...latest, decision: 'maybe' }, 'wrong')).toBeNull()
  expect(Object.keys(FLIP)).toEqual(['ask-check'])
})

test('status text: band line, then each section; a hint when there are no numbers', () => {
  const text = statusText(sample(), HOST)
  expect(text.split('\n')[0]).toBe('live · ctx 44% · $1.84 · 31 calls · 2 >200k · judge 12 (3 unsure) · gates 12 · 2 queued')
  expect(text).toContain('\ncontext\n  window        44% ▓▓▓▓░░░░░░')
  expect(text).toContain('  done-gate     5 · continue 4 · ask 1 · p50 350 ms')
  expect(statusText(null, null)).toContain('scripts/install-scorer.sh')
  const bare: LiveSnapshot = { ...sample(), transcript: { found: false, files: 0 }, context: { tokens: null, window: 200000, percent: null } }
  expect(statusText(bare, null).split('\n')[0]).toBe('live')
})
```

Create `scorer/tests/live-contract.test.ts`:

```ts
import fs from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { LiveSnapshotSchema } from "../src/live-snapshot.js";

// The aw-live mod's tests run against a sample `scorer live --json` result. If the scorer's
// schema moves and the sample does not, the mod would be tested against a shape the scorer no
// longer produces. This parses the mod's own sample with the scorer's schema.
const SAMPLE = fileURLToPath(new URL("../../mods/aw-live/hooks/tests/fixtures/sample.ts", import.meta.url));

describe("aw-live contract", () => {
  it("the mod's sample snapshot is a valid LiveSnapshot", () => {
    const source = fs.readFileSync(SAMPLE, "utf8");
    const json = /SAMPLE_JSON = `([\s\S]*?)`/.exec(source)?.[1];
    expect(json).toBeDefined();
    expect(LiveSnapshotSchema.parse(JSON.parse(json as string)).sessionId).toBe("session-1");
  });
});
```

- [ ] **Step 3: Run, verify the mod tests fail**

Run: `claude plugin test mods/aw-live`
Expected: FAIL, the libs (`../lib/snapshot`, `format`, `labels`, `refresh`) do not exist.

- [ ] **Step 4: Implement the libraries**

Create `mods/aw-live/hooks/lib/snapshot.ts`:

```ts
// The contract with `scorer live --json`. scorer/src/live-snapshot.ts holds the zod
// schema for the same shape; scorer/tests/live-contract.test.ts parses this mod's
// fixture with it, so the two cannot drift apart unnoticed.

import type { LiveSnapshot } from '../../types'

const isObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const JUDGE_STATES = ['none', 'unscoped', 'ok']

/** Parses the CLI's stdout; null for anything that is not a version-1 snapshot. */
export const parseSnapshot = (text: string): LiveSnapshot | null => {
  let value: unknown
  try {
    value = JSON.parse(text)
  } catch {
    return null
  }
  if (!isObject(value) || value.v !== 1 || typeof value.sessionId !== 'string') return null
  const { context, usage, judge, contextGuard, wakes } = value
  if (!isObject(context) || !isObject(usage) || !isObject(contextGuard) || !isObject(wakes)) return null
  if (!isObject(judge) || typeof judge.state !== 'string' || !JUDGE_STATES.includes(judge.state)) return null
  return value as unknown as LiveSnapshot
}
```

Create `mods/aw-live/hooks/lib/labels.ts`:

```ts
// Optional override labels (`judge label set`). Nothing in the pane depends on them.

import type { JudgeLatest } from '../../types'

export type LabelKind = 'confirm' | 'wrong'

/** Questions with exactly two answers: "wrong" has one unambiguous alternative. */
export const FLIP: Readonly<Record<string, Readonly<Record<string, string>>>> = {
  'ask-check': { continue: 'ask', ask: 'continue' },
}

/** The label a press stands for, or null when the press has no single meaning. */
export const labelFor = (latest: JudgeLatest, kind: LabelKind): string | null =>
  kind === 'confirm' ? latest.decision : (FLIP[latest.question]?.[latest.decision] ?? null)
```

Create `mods/aw-live/hooks/lib/refresh.ts`:

```ts
// Pure pieces of the refresh loop. register.tsx owns every `$` call.

export const TIMER_MS = 10_000
export const MIN_GAP_MS = 2_000
export const RUN_TIMEOUT_MS = 5_000

/** The `scorer live` command line. `window` is the host's context window, when known. */
export const liveArgs = (sessionId: string, cwd: string, window: number | null): string[] => [
  'live', '--session', sessionId, '--cwd', cwd, ...(window === null ? [] : ['--window', String(window)]), '--json',
]

/** Where to look for an aw CLI: an explicit override, the installer's directory, then PATH. */
export const binCandidates = (home: string | undefined, name: 'scorer' | 'judge', override?: string): string[] => [
  ...(override === undefined || override === '' ? [] : [override]),
  ...(home === undefined || home === '' ? [name] : [`${home}/.local/bin/${name}`, name]),
]

/** A refresh is due when none has run yet or the last one is at least `minGapMs` old. */
export const isDue = (lastAt: number | null, now: number, minGapMs: number = MIN_GAP_MS): boolean =>
  lastAt === null || now - lastAt >= minGapMs
```

Create `mods/aw-live/hooks/lib/format.ts`:

```ts
// Pure formatting for the band and the pane. No `$` in here.

import type { HostUsage, LiveSnapshot } from '../../types'

export type Tone = 'ok' | 'warn' | 'dim'
export type Row = { label: string; value: string; tone: Tone }
export type Section = { title: string; rows: Row[] }

export const WARN_PERCENT = 70

export const fmtTokens = (n: number): string => {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`
  if (n >= 1_000) return `${Math.round(n / 1_000)}k`
  return String(n)
}

export const cut = (text: string, width: number): string =>
  text.length > width ? `${text.slice(0, Math.max(0, width - 1))}…` : text

/** `▓▓▓▓░░░░░░` for a percent; empty cells when unknown. */
export const bar = (percent: number | null, width: number): string => {
  const filled = percent === null ? 0 : Math.round((Math.min(100, Math.max(0, percent)) / 100) * width)
  return '▓'.repeat(filled) + '░'.repeat(width - filled)
}

/** The host's own context percent when it has one, else the transcript's estimate. */
export const contextPercent = (s: LiveSnapshot, host: HostUsage | null): number | null =>
  host?.percent ?? s.context.percent

/** `jev 83% · rules 17%` for the two biggest deciders; empty with none. */
export const providerShare = (byProvider: Readonly<Record<string, number>>): string => {
  const entries = Object.entries(byProvider).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
  const total = entries.reduce((sum, [, n]) => sum + n, 0)
  return entries.slice(0, 2).map(([name, n]) => `${name} ${Math.round((n / total) * 100)}%`).join(' · ')
}

const gatesFired = (s: LiveSnapshot): number =>
  s.judge.state === 'ok' ? Object.values(s.judge.gates).reduce((sum, g) => sum + g.fired, 0) : 0

/** One dim line above the prompt; null when there is nothing worth a row. */
export const bandText = (s: LiveSnapshot, host: HostUsage | null, columns: number): string | null => {
  if (!s.transcript.found && host === null) return null
  const parts: string[] = []
  const percent = contextPercent(s, host)
  if (percent !== null) parts.push(`ctx ${Math.round(percent)}%`)
  if (host?.usd != null) parts.push(`$${host.usd.toFixed(2)}`)
  parts.push(`${s.usage.calls} calls`)
  if (s.usage.callsOver200k > 0) parts.push(`${s.usage.callsOver200k} >200k`)
  if (s.judge.state === 'ok') {
    parts.push(s.judge.undecided > 0 ? `judge ${s.judge.calls} (${s.judge.undecided} unsure)` : `judge ${s.judge.calls}`)
  }
  const fired = gatesFired(s)
  if (fired > 0) parts.push(`gates ${fired}`)
  if (s.wakes.queuedNow > 0) parts.push(`${s.wakes.queuedNow} queued`)
  return cut(`live · ${parts.join(' · ')}`, columns)
}

const GATE_ORDER = ['scope-gate', 'done-gate', 'send-gate'] as const

const decisionList = (byDecision: Readonly<Record<string, number>>): string =>
  Object.entries(byDecision).sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).map(([d, n]) => `${d} ${n}`).join(' · ')

/** The pane's cards, top to bottom. */
export const paneSections = (s: LiveSnapshot, host: HostUsage | null): Section[] => {
  const percent = contextPercent(s, host)
  const window = host?.window ?? s.context.window
  const context: Section = {
    title: 'context',
    rows: [
      { label: 'window', value: `${percent === null ? '--' : `${Math.round(percent)}%`} ${bar(percent, 10)}`, tone: percent !== null && percent >= WARN_PERCENT ? 'warn' : 'ok' },
      { label: 'tokens', value: `${s.context.tokens === null ? '--' : fmtTokens(s.context.tokens)} of ${fmtTokens(window)}`, tone: 'dim' },
    ],
  }
  const session: Section = {
    title: 'session',
    rows: [
      { label: 'cost', value: host?.usd == null ? '--' : `$${host.usd.toFixed(2)}`, tone: 'ok' },
      { label: 'tokens', value: `${fmtTokens(s.usage.contextTokens)} in · ${fmtTokens(s.usage.outputTokens)} out`, tone: 'dim' },
      { label: 'calls', value: `${s.usage.calls} (${s.usage.subagentCalls} subagent)`, tone: 'dim' },
      { label: 'over 200k', value: String(s.usage.callsOver200k), tone: s.usage.callsOver200k > 0 ? 'warn' : 'dim' },
    ],
  }
  const sections = [context, session]
  if (s.judge.state === 'unscoped') {
    sections.push({ title: 'judge', rows: [{ label: 'sessions', value: 'not linked yet (judge predates decision_details.session_id)', tone: 'dim' }] })
  }
  if (s.judge.state === 'ok') {
    const j = s.judge
    sections.push({
      title: 'judge',
      rows: [
        { label: 'calls', value: `${j.calls}${providerShare(j.byProvider) === '' ? '' : ` · ${providerShare(j.byProvider)}`}`, tone: 'ok' },
        { label: 'vs rules', value: `agreed ${j.agreed} · overrode ${j.overrode} · unsure ${j.undecided}`, tone: j.undecided > 0 ? 'warn' : 'dim' },
        { label: 'latency', value: `p50 ${j.p50LatencyMs} ms · p95 ${j.p95LatencyMs} ms`, tone: 'dim' },
      ],
    })
    sections.push({
      title: 'gates',
      rows: GATE_ORDER.map((name): Row => {
        const g = j.gates[name]
        return {
          label: name,
          value: g.fired === 0 ? 'quiet' : `${g.fired} · ${decisionList(g.byDecision)} · p50 ${g.p50LatencyMs} ms`,
          tone: g.fired === 0 ? 'dim' : 'ok',
        }
      }),
    })
  }
  sections.push({
    title: 'wakes',
    rows: [
      { label: 'queued now', value: String(s.wakes.queuedNow), tone: s.wakes.queuedNow > 0 ? 'warn' : 'dim' },
      { label: 'context guard', value: `${s.contextGuard.fires} fires`, tone: s.contextGuard.fires > 0 ? 'warn' : 'dim' },
    ],
  })
  return sections
}

/** The `/live status` answer: the band line, then every pane row as plain text. */
export const statusText = (s: LiveSnapshot | null, host: HostUsage | null): string => {
  if (s === null) return 'aw-live: no numbers yet (is scorer installed? scripts/install-scorer.sh)'
  const lines = paneSections(s, host).flatMap(section => [
    `${section.title}`,
    ...section.rows.map(row => `  ${row.label.padEnd(14)}${row.value}`),
  ])
  return [bandText(s, host, 200) ?? 'live', ...lines].join('\n')
}
```

- [ ] **Step 5: Run, verify pass**

Run: `claude plugin validate mods/aw-live`
Expected: `✔ Validation passed` (a missing-`author` warning is not expected: the manifest has one).
Run: `claude plugin test mods/aw-live`
Expected: `10 pass`, `0 fail` (the `lib.test.ts` tests).
Run: `cd scorer && npx vitest run tests/live-contract.test.ts`
Expected: PASS (the mod's sample parses with the scorer's `LiveSnapshotSchema`).

- [ ] **Step 6: Type-check the mod**

Run:
```bash
TYPES="$(ls -t /private/tmp/claude-*/bundled-skills/*/*/plugin-authoring/types/claude-code.d.ts 2>/dev/null | head -1)"
test -n "$TYPES" || { echo "load the plugin-authoring skill (Skill tool) first: it writes this file"; exit 1; }
CHECK="$(mktemp -d)"
cat > "$CHECK/tsconfig.json" <<EOF
{
  "compilerOptions": {
    "target": "es2023", "lib": ["es2023"], "types": [],
    "module": "esnext", "moduleResolution": "bundler",
    "strict": true, "noUncheckedIndexedAccess": true,
    "noEmit": true, "skipLibCheck": true,
    "jsx": "react", "jsxFactory": "h", "jsxFragmentFactory": "Fragment"
  },
  "include": ["$TYPES", "$PWD/mods/aw-live/hooks", "$PWD/mods/aw-live/types"]
}
EOF
scorer/node_modules/.bin/tsc -p "$CHECK/tsconfig.json"
```
Expected: no output, exit 0.

- [ ] **Step 7: Commit**

```bash
git add mods/aw-live .gitignore scorer/tests/live-contract.test.ts
git commit -m "feat: aw-live mod manifest, types contract and pure formatting libraries" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

### Task 6: The hooks module: refresh loop, band, pane, `/live`, optional labels

**Files:**
- Replace: `mods/aw-live/hooks/register.tsx`
- Create: `mods/aw-live/hooks/tests/register.test.ts`

**Interfaces:**
- Consumes: Task 5's libs and contract; `$.session.id/cwd/usage`, `$.env.get`, `$.process.run`, `$.clock.now/every/after`, `$.command.register`, `$.ui.open/close/panes/resolve/toast`, `atom/read/update` from `claude-code`.
- Produces: the registered hooks: `session.start` (registers `/live`, starts the 10 s timer, first refresh), `turn.complete` (a refresh via `$.clock.after(0, …)`; returns at once), `command.run {command:'live'}` (toggle pane; `status` argument answers with text), `ui.render` for `AbovePrompt` (the band) and `Pane` id `aw-live`. Refresh = one `scorer live --json` per at most 2 s, joined if one is running.

Behavior facts the tests pin: the refresh never runs inside the awaited part of a hook; the CLI is looked up as `AW_SCORER_BIN`, then `$HOME/.local/bin/scorer`, then `scorer`; a failed or garbage answer keeps the last snapshot; `y`/`x` exist only when `judge.labelable` and there is a latest decision, and `x` only for two-answer questions.

- [ ] **Step 1: Write the failing tests**

Create `mods/aw-live/hooks/tests/register.test.ts`:

```ts
import { expect, mock, test } from 'claude-code/testing'
import type { Engine } from 'claude-code/testing'
import type { On } from 'claude-code'

import { SAMPLE_JSON } from './fixtures/sample'

const SURFACES = ['terminal', 'desktop'] as const
const PLUGIN = 'aw-live'
const CMD = {
  command: 'live', args: '', origin: { kind: 'composer' as const },
  presentation: { isFullscreen: false, columns: 120 },
}

type Run = { argv: string[] }
type Reply = { exitCode: number; stdout: string }

// The world beneath the plugin: a fixed session, a fake usage, and a scripted process.run.
const sessionId = { current: 'session-1' }

const world = (on: On, reply: (argv: string[]) => Reply | 'cannot-start', env?: Record<string, string>) => {
  const runs: Run[] = []
  const opened: string[] = []
  const closed: string[] = []
  const toasts: string[] = []
  let isOpen = false
  on('session.id', () => ({ value: sessionId.current }))
  on('session.cwd', () => ({ value: '/repo' }))
  on('session.usage', () => ({
    value: { startedAt: 0, context: { window: 1_000_000, percent: 44, tokens: 440_000 }, rateLimits: [], cost: { usd: 1.839 } },
  }))
  mock.env(on, { HOME: '/home/me', ...(env ?? {}) })
  const clock = mock.clock(on, { now: 1_700_000_000_000 })
  on('process.run', (_$, e) => {
    runs.push({ argv: [...e.argv] })
    const r = reply([...e.argv])
    if (r === 'cannot-start') return { deny: 'ENOENT' }
    return { value: { ...r, stderr: '', isStdoutTruncated: false, isStderrTruncated: false } }
  })
  on('ui.toast', (_$, e) => { toasts.push(e.text); return { value: undefined } })
  on('ui.open', (_$, e) => { opened.push(e.id); isOpen = true; return { value: { isPlaced: true } } })
  on('ui.close', (_$, e) => { closed.push(e.id); isOpen = false; return { value: undefined } })
  on('ui.panes', () => ({ value: isOpen ? [{ id: 'aw-live', title: 'Live scorer', isShown: true, isFocused: false, isPlaced: true }] : [] }))
  on('command.register', (_$, e) => ({ value: { command: e.name } }))
  on('session.start', (_$, e) => ({ cwd: e.cwd }))
  on('turn.complete', (_$, e) => ({ text: e.answer }))
  on('ui.render', { component: 'AbovePrompt' }, () => ({ type: 'engine', ref: 0 }))
  on('ui.render', { component: 'Pane' }, () => ({ type: 'engine', ref: 0 }))
  return { runs, opened, closed, toasts, clock }
}

const ok = (stdout: string): Reply => ({ exitCode: 0, stdout })
const start = ($: Engine) => $.session.start({ cwd: '/repo', surface: 'terminal', isInteractive: true })

const band = ($: Engine, surface: (typeof SURFACES)[number], hasSurvey = false) =>
  $.ui.mount({
    plugin: PLUGIN, surface, component: 'AbovePrompt',
    props: { hasSurvey, isWorking: false, maxRows: 5, bodyColumns: 120, scroll: { offset: 0, bodyRows: 5 }, view: {} },
  })

const pane = ($: Engine, surface: (typeof SURFACES)[number]) =>
  $.ui.mount({
    plugin: PLUGIN, surface, component: 'Pane', requestId: 'aw-live',
    props: { title: 'Live scorer', isFocused: true, bodyColumns: 70, placement: 'inline', scroll: { offset: 0, bodyRows: 30 }, view: {} },
  })

test('session start refreshes once and the band shows the headline on every surface', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  expect(runs[0]?.argv).toEqual([
    '/home/me/.local/bin/scorer', 'live', '--session', 'session-1', '--cwd', '/repo', '--window', '1000000', '--json',
  ])
  for (const surface of SURFACES) {
    const ui = await band($, surface)
    expect((await ui.find({ type: 'Text' }))?.text).toBe('live · ctx 44% · $1.84 · 31 calls · 2 >200k · judge 12 (3 unsure) · gates 12 · 2 queued')
    await ui.unmount()
  }
})

test('the band stays quiet under a survey, before any numbers, and when the CLI fails', async ($, on) => {
  world(on, () => ({ exitCode: 1, stdout: '' }))
  await start($)
  const none = await band($, 'terminal')
  expect(await none.find({ type: 'Text' })).toBeUndefined()
  await none.unmount()
  const survey = await band($, 'terminal', true)
  expect(await survey.find({ type: 'Text' })).toBeUndefined()
  await survey.unmount()
})

test('a missing binary fails silent', async ($, on) => {
  world(on, () => 'cannot-start')
  await start($)
  const ui = await band($, 'terminal')
  expect(await ui.find({ type: 'Text' })).toBeUndefined()
  await ui.unmount()
})

test('garbage output fails silent', async ($, on) => {
  world(on, () => ok('garbage'))
  await start($)
  const ui = await band($, 'terminal')
  expect(await ui.find({ type: 'Text' })).toBeUndefined()
  await ui.unmount()
})

test('the installer path is tried first, then PATH', async ($, on) => {
  const { runs, clock } = world(on, argv => (argv[0] === 'scorer' ? ok(SAMPLE_JSON) : 'cannot-start'))
  await start($)
  await clock.settle()
  expect(runs.map(r => r.argv[0])).toEqual(['/home/me/.local/bin/scorer', 'scorer'])
  const ui = await band($, 'terminal')
  expect(await ui.find({ type: 'Text' })).toBeDefined()
  await ui.unmount()
})

test('turn.complete refreshes again, but not twice inside the minimum gap', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  const first = runs.length
  await clock.advance(1_000)
  await $.turn.complete({ answer: 'x', durationMs: 1, isAborted: false, turnId: 't1', reason: 'answer' })
  await clock.settle()
  expect(runs.length).toBe(first)
  await clock.advance(1_500)
  await $.turn.complete({ answer: 'x', durationMs: 1, isAborted: false, turnId: 't2', reason: 'answer' })
  await clock.settle()
  expect(runs.length).toBe(first + 1)
})

test('the timer refreshes every ten seconds', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  const first = runs.length
  await clock.advance(10_000)
  expect(runs.length).toBe(first + 1)
})

test('/live opens the pane, shows the sections, and a second /live closes it', async ($, on) => {
  const { opened, closed } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run(CMD)
  expect(opened).toEqual(['aw-live'])
  for (const surface of SURFACES) {
    const ui = await pane($, surface)
    const text = (await ui.findAll({ type: 'Text' })).map(t => t.text).join('\n')
    expect(text).toContain('done-gate')
    expect(text).toContain('5 · continue 4 · ask 1 · p50 350 ms')
    expect(text).toContain('queued now')
    await ui.unmount()
  }
  await $.command.run(CMD)
  expect(closed).toEqual(['aw-live'])
})

test('the pane explains itself when nothing has been read yet', async ($, on) => {
  world(on, () => 'cannot-start')
  await start($)
  const ui = await pane($, 'terminal')
  expect((await ui.find({ type: 'Text' }))?.text).toContain('scripts/install-scorer.sh')
  await ui.unmount()
})

test('y writes an override label for the latest decision when it is labelable', async ($, on) => {
  const { runs, toasts } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  const ui = await pane($, 'terminal')
  await ui.press({ key: 'confirm' })
  expect(runs.at(-1)?.argv).toEqual(['/home/me/.local/bin/judge', 'label', 'set', 'item-9', 'continue'])
  expect(toasts).toContain('Labeled ask-check as continue (override)')
  await ui.press({ key: 'wrong' })
  expect(runs.at(-1)?.argv).toEqual(['/home/me/.local/bin/judge', 'label', 'set', 'item-9', 'ask'])
  await ui.unmount()
})

test('a failing label write toasts and nothing else happens', async ($, on) => {
  const { toasts } = world(on, argv => (argv.includes('label') ? { exitCode: 2, stdout: '' } : ok(SAMPLE_JSON)))
  await start($)
  const ui = await pane($, 'terminal')
  await ui.press({ key: 'confirm' })
  expect(toasts).toContain('aw-live: could not write the label')
  await ui.unmount()
})

test('a decision without an item imports first, then labels', async ($, on) => {
  const noItem = SAMPLE_JSON.replace('"itemId": "item-9"', '"itemId": null')
  let imported = false
  const { runs } = world(on, argv => {
    if (argv.includes('import')) { imported = true; return ok('{"imported":1}') }
    return ok(imported ? SAMPLE_JSON : noItem)
  })
  await start($)
  const ui = await pane($, 'terminal')
  await ui.press({ key: 'confirm' })
  const labels = runs.filter(r => r.argv.includes('label')).map(r => r.argv.slice(1))
  expect(labels).toEqual([
    ['label', 'import', '--since', '1d', '--question', 'ask-check'],
    ['label', 'set', 'item-9', 'continue'],
  ])
  await ui.unmount()
})

test('an item that never appears is reported, not labeled', async ($, on) => {
  const noItem = SAMPLE_JSON.replace('"itemId": "item-9"', '"itemId": null')
  const { runs, toasts } = world(on, () => ok(noItem))
  await start($)
  const ui = await pane($, 'terminal')
  await ui.press({ key: 'confirm' })
  expect(toasts).toContain('aw-live: this decision is not labelable yet')
  expect(runs.some(r => r.argv.includes('set'))).toBe(false)
  await ui.unmount()
})

test('no label buttons when judge is not labelable', async ($, on) => {
  world(on, () => ok(SAMPLE_JSON.replace('"labelable": true', '"labelable": false')))
  await start($)
  const ui = await pane($, 'terminal')
  expect(await ui.find({ key: 'confirm' })).toBeUndefined()
  await ui.unmount()
})

test('wrong is hidden for questions with more than two answers', async ($, on) => {
  world(on, () => ok(SAMPLE_JSON.replace('"question": "ask-check", "decision": "continue"', '"question": "wake-gate", "decision": "send"')))
  await start($)
  const ui = await pane($, 'terminal')
  expect(await ui.find({ key: 'confirm' })).toBeDefined()
  expect(await ui.find({ key: 'wrong' })).toBeUndefined()
  await ui.unmount()
})

test('AW_SCORER_BIN is tried before the installer path', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON), { AW_SCORER_BIN: '/build/scorer' })
  await start($)
  await clock.settle()
  expect(runs[0]?.argv[0]).toBe('/build/scorer')
})

test('/live status answers with plain text and opens no pane', async ($, on) => {
  const { opened } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  const answer = await $.command.run({ ...CMD, args: ' status ' })
  expect(answer.text).toContain('done-gate')
  expect(answer.text?.startsWith('live · ctx 44%')).toBe(true)
  expect(opened).toEqual([])
})

test('/live status says so when scorer is missing', async ($, on) => {
  world(on, () => 'cannot-start')
  await start($)
  const answer = await $.command.run({ ...CMD, args: 'status' })
  expect(answer.text).toContain('scripts/install-scorer.sh')
})

test('a /clear gives the session a new id and the next refresh asks for that one (RF-5)', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  sessionId.current = 'session-2'
  await clock.advance(10_000)
  expect(runs.at(-1)?.argv).toContain('session-2')
  expect(runs[0]?.argv).toContain('session-1')
  sessionId.current = 'session-1'
})

test('a refresh that arrives while another runs joins it: one process, not two (RF-3)', async ($, on) => {
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await $.command.run({ ...CMD, args: 'status' })
  await clock.settle()
  expect(runs.filter(r => r.argv.includes('live')).length).toBe(1)
})

test('no session id yet means no refresh and no crash', async ($, on) => {
  sessionId.current = ''
  const { runs, clock } = world(on, () => ok(SAMPLE_JSON))
  await start($)
  await clock.settle()
  expect(runs).toEqual([])
  sessionId.current = 'session-1'
})
```

- [ ] **Step 2: Run, verify they fail**

Run: `claude plugin test mods/aw-live`
Expected: FAIL (the scaffold registers nothing: the band and pane tests find no `Text`).

- [ ] **Step 3: Implement**

Create `mods/aw-live/hooks/register.tsx`:

```tsx
// aw-live: the scorer's numbers for THIS session, live inside Claude Code.
// `/live` toggles a pane; a one-line band above the prompt shows the headline.
// Every number comes from `scorer live --json` (one short Node process, never
// in-process: a mod has no Node and cannot load better-sqlite3). A refresh never
// blocks a hook and every failure is silent: the band just keeps its last value.

import { atom, read, update } from 'claude-code'
import type { EngineInterface, Register } from 'claude-code'

import type { HostUsage, LiveSnapshot, LiveStatus } from '../types'
import { bandText, paneSections, statusText } from './lib/format'
import type { Tone } from './lib/format'
import { labelFor } from './lib/labels'
import type { LabelKind } from './lib/labels'
import { RUN_TIMEOUT_MS, TIMER_MS, binCandidates, isDue, liveArgs } from './lib/refresh'
import { parseSnapshot } from './lib/snapshot'

const PANE = 'aw-live'
const TITLE = 'Live scorer'
const PANE_COLUMNS = 72

const snapshot = atom({ plugin: 'aw-live', key: 'snapshot' } as const, null as LiveSnapshot | null)
const host = atom({ plugin: 'aw-live', key: 'host' } as const, null as HostUsage | null)
const status = atom({ plugin: 'aw-live', key: 'status' } as const, null as LiveStatus | null)

const TONE_COLOR: Record<Tone, string | undefined> = { ok: '#7ec699', warn: '#d4a054', dim: '#8b949e' }

// Module variables restart on a hot reload; that only costs one extra refresh.
let lastAt: number | null = null
let running: Promise<void> | null = null

/** The aw CLI's candidate paths: AW_SCORER_BIN / AW_JUDGE_BIN, ~/.local/bin, then PATH. */
const candidates = async ($: EngineInterface, name: 'scorer' | 'judge'): Promise<string[]> =>
  name === 'scorer'
    ? binCandidates(await $.env.get('HOME'), name, await $.env.get('AW_SCORER_BIN'))
    : binCandidates(await $.env.get('HOME'), name, await $.env.get('AW_JUDGE_BIN'))

/** Runs an aw CLI by argv, trying each candidate path; null on any failure. */
const runCli = async ($: EngineInterface, names: string[], args: string[]): Promise<string | null> => {
  for (const name of names) {
    try {
      const r = await $.process.run([name, ...args], { timeoutMs: RUN_TIMEOUT_MS })
      return r.exitCode === 0 ? r.stdout : null
    } catch {
      // not runnable at this path: try the next candidate
    }
  }
  return null
}

const runRefresh = async ($: EngineInterface, force: boolean): Promise<void> => {
  const now = await $.clock.now()
  if (!force && !isDue(lastAt, now)) return
  lastAt = now
  let ok = false
  try {
    const sessionId = await $.session.id()
    if (sessionId === '') return
    const [cwd, usage] = await Promise.all([$.session.cwd(), $.session.usage()])
    await update($, host, (): HostUsage => ({
      usd: usage.cost?.usd ?? null,
      percent: usage.context.percent ?? null,
      window: usage.context.window,
    }))
    const out = await runCli($, await candidates($, 'scorer'), liveArgs(sessionId, cwd, usage.context.window))
    const parsed = out === null ? null : parseSnapshot(out)
    if (parsed !== null) await update($, snapshot, () => parsed)
    ok = parsed !== null
  } catch {
    // fail silent: the band keeps its last good value
  } finally {
    await update($, status, (): LiveStatus => ({ ok, at: now })).catch(() => undefined)
  }
}

/** One refresh at a time: a caller that arrives while one runs waits for that one. */
const refresh = ($: EngineInterface, force: boolean): Promise<void> => {
  if (running !== null) return running
  running = runRefresh($, force).finally(() => {
    running = null
  })
  return running
}

/** Optional: write an override label for the latest judge decision (`judge label set`). */
const labelLatest = async ($: EngineInterface, kind: LabelKind): Promise<void> => {
  const latest = (await read($, snapshot))?.judge
  if (latest === undefined || latest.state !== 'ok' || latest.latest === null) return
  const decision = latest.latest
  const label = labelFor(decision, kind)
  if (label === null) return
  const judge = await candidates($, 'judge')
  let itemId = decision.itemId
  if (itemId === null) {
    await runCli($, judge, ['label', 'import', '--since', '1d', '--question', decision.question])
    await refresh($, true)
    const again = (await read($, snapshot))?.judge
    itemId = again !== undefined && again.state === 'ok' ? (again.latest?.itemId ?? null) : null
  }
  if (itemId === null) {
    $.ui.toast('aw-live: this decision is not labelable yet')
    return
  }
  const out = await runCli($, judge, ['label', 'set', itemId, label])
  $.ui.toast(out === null ? 'aw-live: could not write the label' : `Labeled ${decision.question} as ${label} (override)`)
}

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    await $.command.register({ name: 'live', description: 'Toggle the live scorer pane (context, cost, judge, gates)' })
    $.clock.every(TIMER_MS, () => {
      void refresh($, false)
    })
    void refresh($, true)
    return next(e)
  })

  on('turn.complete', async ($, e, next) => {
    $.clock.after(0, () => {
      void refresh($, false)
    })
    return next(e)
  })

  on('command.run', { command: 'live' }, async ($, e) => {
    if (e.args.trim() === 'status') {
      await refresh($, true)
      return { text: statusText(await read($, snapshot), await read($, host)) }
    }
    if ((await $.ui.panes()).some(pane => pane.id === PANE)) {
      await $.ui.close({ id: PANE })
      return {}
    }
    await refresh($, true)
    await $.ui.open({ id: PANE, title: TITLE, focus: true, closeOnEscape: true, columns: PANE_COLUMNS })
    return {}
  })

  on('ui.render', { component: 'AbovePrompt' }, async ($, e, next) => {
    if (e.props.hasSurvey) return next(e)
    const s = await read($, snapshot)
    if (s === null) return next(e)
    const text = bandText(s, await read($, host), e.props.bodyColumns)
    if (text === null) return next(e)
    const { Box, Text } = $.ui.resolve(e)
    return (
      <Box>
        <Text color={TONE_COLOR.dim} wrap="truncate-end">{text}</Text>
      </Box>
    )
  })

  on('ui.render', { component: 'Pane', requestId: PANE }, async ($, e) => {
    const { Box, Text, Button } = $.ui.resolve(e)
    const s = await read($, snapshot)
    if (s === null) {
      return (
        <Box flexDirection="column">
          <Text color={TONE_COLOR.dim}>No numbers yet. Is `scorer` installed? (scripts/install-scorer.sh)</Text>
        </Box>
      )
    }
    const hostUsage = await read($, host)
    const latest = s.judge.state === 'ok' && s.judge.labelable ? s.judge.latest : null
    const canFlip = latest !== null && labelFor(latest, 'wrong') !== null
    return (
      <Box flexDirection="column">
        {paneSections(s, hostUsage).map(section => (
          <Box key={section.title} borderStyle="round" borderColor="#3d4450" paddingX={1} flexDirection="column">
            <Text color="#7eb8da">{section.title}</Text>
            {section.rows.map(row => (
              <Text key={row.label} wrap="truncate-end">
                <Text color={TONE_COLOR.dim}>{row.label.padEnd(14)}</Text>
                <Text color={TONE_COLOR[row.tone]}>{row.value}</Text>
              </Text>
            ))}
          </Box>
        ))}
        <Box flexDirection="row" columnGap={2} paddingX={1}>
          <Button key="refresh" label="refresh" hotkey="r" plain onPress={() => refresh($, true)} />
          {latest === null ? null : (
            <Button key="confirm" label={`${latest.question}: ${latest.decision} was right`} hotkey="y" plain onPress={() => labelLatest($, 'confirm')} />
          )}
          {canFlip ? <Button key="wrong" label="wrong" hotkey="x" plain onPress={() => labelLatest($, 'wrong')} /> : null}
        </Box>
        {latest === null ? null : <Text color={TONE_COLOR.dim}>y / x write an optional override label; nothing depends on them.</Text>}
      </Box>
    )
  })
}
```

- [ ] **Step 4: Run, verify pass**

Run: `claude plugin validate mods/aw-live`
Expected: `✔ Validation passed`; the report lists hooks `session.start, turn.complete, command.run{command=live}, ui.render{component=AbovePrompt}, ui.render{component=Pane, requestId=aw-live}` and env reads `AW_JUDGE_BIN, AW_SCORER_BIN, HOME`.
Run: `claude plugin test mods/aw-live`
Expected: 31 pass, 0 fail (10 in `lib.test.ts`, 21 in `register.test.ts`).

- [ ] **Step 5: Type-check** (same command as Task 5 Step 6)

Expected: no output, exit 0.

- [ ] **Step 6: Commit**

```bash
git add mods/aw-live/hooks/register.tsx mods/aw-live/hooks/tests/register.test.ts
git commit -m "feat: aw-live hooks: /live pane, band, refresh loop and optional override labels" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

### Task 7: Installer, marketplace, `setup.sh` wiring and docs

The install path follows the repo's lever convention: one `scripts/install-<lever>.sh --provider X`, owner-tagged, idempotent, `--dry-run`, `--uninstall`, a bash test with a throwaway `HOME` and a stubbed CLI. The owner tag is the marketplace name `agentic-workflow-mods` and plugin `aw-live`; the installer touches only those two ids.

**Files:**
- Create: `mods/.claude-plugin/marketplace.json`, `scripts/install-live-pane.sh`, `config/lib/tests/install-live-pane.test.sh`
- Modify: `providers/lib.sh` (`AW_LEVER_INSTALLERS`, banner)
- Modify: `AGENTS.md`, `planning/PROVIDERS.md`, `planning/ARCHITECTURE.md`

**Interfaces:**
- Consumes: `aw_parse_provider_args`, `aw_hooks_init`, `aw_unsupported` from `config/hooks/adapters/install-lib.sh`; `AW_DRY_RUN`; `CLAUDE_LOCAL_BIN`.
- Produces: `scripts/install-live-pane.sh [--provider claude|codex|cursor] [--dry-run] [--uninstall]`. `setup.sh` runs it for every selected provider through `aw_install_levers`; in `--dry-run` it prints `[dry-run] would run scripts/install-live-pane.sh --provider <p>`.

- [ ] **Step 1: Write the failing test**

Create `config/lib/tests/install-live-pane.test.sh`:

```bash
#!/usr/bin/env bash
# Tests for scripts/install-live-pane.sh. A stub `claude` records every call and keeps a
# tiny marketplace/plugin state, so nothing here touches a real Claude Code config.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"
INSTALL="$ROOT/scripts/install-live-pane.sh"
ID="aw-live@agentic-workflow-mods"

fail() { echo "FAIL: $*"; exit 1; }

# new_world sets BIN (stub dir), STATE, LOG, HOME. Every case gets its own.
new_world() {
  local w; w="$(mktemp -d)"
  BIN="$w/bin"; STATE="$w/state"; LOG="$w/claude.log"; mkdir -p "$BIN" "$STATE" "$w/home"
  : > "$LOG"
  export HOME="$w/home" AW_STUB_STATE="$STATE" AW_STUB_LOG="$LOG" AW_DRY_RUN=0
  unset CLAUDE_LOCAL_BIN
  cat > "$BIN/claude" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$AW_STUB_LOG"
state="$AW_STUB_STATE"
case "$1 $2 $3" in
  "plugin marketplace list") cat "$state/marketplaces.json" 2>/dev/null || echo '[]' ;;
  "plugin marketplace add") jq -n --arg l "$4" '[{name: "agentic-workflow-mods", source: "directory", path: $l, installLocation: $l}]' > "$state/marketplaces.json" ;;
  "plugin marketplace remove") echo '[]' > "$state/marketplaces.json"; echo '[]' > "$state/plugins.json" ;;
  "plugin list --json") cat "$state/plugins.json" 2>/dev/null || echo '[]' ;;
  "plugin install aw-live@agentic-workflow-mods") echo '[{"id": "aw-live@agentic-workflow-mods"}]' > "$state/plugins.json" ;;
  "plugin uninstall aw-live@agentic-workflow-mods") echo '[]' > "$state/plugins.json" ;;
esac
STUB
  chmod +x "$BIN/claude"
  export PATH="$BIN:$PATH"
}

writes() { grep -cE '^plugin (marketplace (add|remove)|install|uninstall)' "$LOG" || true; }

test_fresh_install_adds_the_marketplace_then_the_plugin_at_user_scope() {
  new_world
  bash "$INSTALL" > /dev/null
  grep -qx "plugin marketplace add $ROOT/mods --scope user" "$LOG" || fail "marketplace not added from this checkout: $(cat "$LOG")"
  grep -qx "plugin install $ID --scope user" "$LOG" || fail "plugin not installed: $(cat "$LOG")"
  echo "PASS: test_fresh_install_adds_the_marketplace_then_the_plugin_at_user_scope"
}

test_second_run_changes_nothing() {
  new_world
  bash "$INSTALL" > /dev/null
  local before; before="$(writes)"
  local out; out="$(bash "$INSTALL")"
  [ "$(writes)" = "$before" ] || fail "second run wrote again: $(cat "$LOG")"
  echo "$out" | grep -q "already installed" || fail "expected an already-installed note, got: $out"
  echo "PASS: test_second_run_changes_nothing"
}

test_dry_run_writes_nothing_and_says_what_it_would_do() {
  new_world
  local out; out="$(bash "$INSTALL" --dry-run)"
  [ "$(writes)" = "0" ] || fail "dry-run wrote: $(cat "$LOG")"
  echo "$out" | grep -q "\[dry-run\] claude plugin marketplace add" || fail "dry-run did not print the add: $out"
  echo "$out" | grep -q "would be installed" || fail "dry-run did not say it would install: $out"
  new_world
  out="$(AW_DRY_RUN=1 bash "$INSTALL")"
  [ "$(writes)" = "0" ] || fail "AW_DRY_RUN=1 wrote: $(cat "$LOG")"
  echo "PASS: test_dry_run_writes_nothing_and_says_what_it_would_do"
}

test_a_marketplace_registered_from_another_checkout_is_moved_here() {
  new_world
  jq -n '[{name: "agentic-workflow-mods", source: "directory", path: "/old/checkout/mods", installLocation: "/old/checkout/mods"}]' > "$STATE/marketplaces.json"
  echo "[{\"id\": \"$ID\"}]" > "$STATE/plugins.json"
  bash "$INSTALL" > /dev/null
  grep -qx "plugin marketplace remove agentic-workflow-mods" "$LOG" || fail "old marketplace not removed: $(cat "$LOG")"
  grep -qx "plugin marketplace add $ROOT/mods --scope user" "$LOG" || fail "this checkout not added: $(cat "$LOG")"
  grep -qx "plugin install $ID --scope user" "$LOG" || fail "plugin not reinstalled after the move: $(cat "$LOG")"
  echo "PASS: test_a_marketplace_registered_from_another_checkout_is_moved_here"
}

test_codex_and_cursor_are_skipped_with_a_reason_and_no_claude_calls() {
  local p
  for p in codex cursor; do
    new_world
    local out; out="$(bash "$INSTALL" --provider "$p")"
    echo "$out" | grep -q "skipped for $p" || fail "$p: no skip note: $out"
    echo "$out" | grep -q "Claude Code only" || fail "$p: skip note does not say why: $out"
    [ ! -s "$LOG" ] || fail "$p: claude was called: $(cat "$LOG")"
  done
  echo "PASS: test_codex_and_cursor_are_skipped_with_a_reason_and_no_claude_calls"
}

test_uninstall_removes_exactly_our_two_ids_and_is_safe_when_absent() {
  new_world
  bash "$INSTALL" > /dev/null
  : > "$LOG"
  bash "$INSTALL" --uninstall > /dev/null
  grep -qx "plugin uninstall $ID --scope user" "$LOG" || fail "plugin not uninstalled: $(cat "$LOG")"
  grep -qx "plugin marketplace remove agentic-workflow-mods" "$LOG" || fail "marketplace not removed: $(cat "$LOG")"
  : > "$LOG"
  bash "$INSTALL" --uninstall > /dev/null
  [ "$(writes)" = "0" ] || fail "uninstall with nothing installed wrote: $(cat "$LOG")"
  echo "PASS: test_uninstall_removes_exactly_our_two_ids_and_is_safe_when_absent"
}

test_a_missing_claude_cli_is_a_clean_skip() {
  new_world
  rm "$BIN/claude"
  if PATH="/usr/bin:/bin" command -v claude > /dev/null; then echo "SKIP: test_a_missing_claude_cli_is_a_clean_skip (claude lives in /usr/bin here)"; return; fi
  local out; out="$(PATH="/usr/bin:/bin" bash "$INSTALL")" || fail "exited non-zero without claude"
  echo "$out" | grep -q "the claude CLI is not on PATH" || fail "no skip note: $out"
  echo "PASS: test_a_missing_claude_cli_is_a_clean_skip"
}

test_warns_when_scorer_has_no_live_command_and_stays_quiet_when_it_does() {
  new_world
  mkdir -p "$HOME/bin2"
  printf '#!/usr/bin/env bash\nexit 1\n' > "$HOME/bin2/scorer"; chmod +x "$HOME/bin2/scorer"
  local out; out="$(CLAUDE_LOCAL_BIN="$HOME/bin2" bash "$INSTALL")"
  echo "$out" | grep -q "WARN: .* has no 'live' command" || fail "expected a scorer warning: $out"
  printf '#!/usr/bin/env bash\necho "{\\"v\\":1}"\n' > "$HOME/bin2/scorer"
  out="$(CLAUDE_LOCAL_BIN="$HOME/bin2" bash "$INSTALL")"
  echo "$out" | grep -q "WARN" && fail "unexpected warning with a live-capable scorer: $out"
  echo "PASS: test_warns_when_scorer_has_no_live_command_and_stays_quiet_when_it_does"
}

test_fresh_install_adds_the_marketplace_then_the_plugin_at_user_scope
test_second_run_changes_nothing
test_dry_run_writes_nothing_and_says_what_it_would_do
test_a_marketplace_registered_from_another_checkout_is_moved_here
test_codex_and_cursor_are_skipped_with_a_reason_and_no_claude_calls
test_uninstall_removes_exactly_our_two_ids_and_is_safe_when_absent
test_a_missing_claude_cli_is_a_clean_skip
test_warns_when_scorer_has_no_live_command_and_stays_quiet_when_it_does
echo "All install-live-pane tests passed."
```

- [ ] **Step 2: Run, verify it fails**

Run: `bash config/lib/tests/install-live-pane.test.sh`
Expected: FAIL (`scripts/install-live-pane.sh` does not exist; the first test reports the missing script).

- [ ] **Step 3: Implement**

Create `mods/.claude-plugin/marketplace.json`:

```json
{
  "name": "agentic-workflow-mods",
  "owner": { "name": "agentic-workflow" },
  "metadata": { "description": "Claude Code mods (in-process function hooks) that ship with agentic-workflow." },
  "plugins": [
    { "name": "aw-live", "source": "./aw-live", "description": "Live scorer numbers inside Claude Code: a /live pane and a band for context, cost, judge, gates and wakes." }
  ]
}
```

Create `scripts/install-live-pane.sh`:

```bash
#!/usr/bin/env bash
# Install the aw-live Claude Code mod: a /live pane and a one-line band that show the
# scorer's numbers for the current session (context, cost, judge, gates, wakes).
#
# Claude Code only. Mods are in-process function hooks of Claude Code; Codex and Cursor have
# no equivalent, so they keep the statusline segments (judge, rtk, headroom, prism) and the
# daily scorer report, and this installer says so and writes nothing for them.
#
# Owner tag: the marketplace is named "agentic-workflow-mods" and the plugin "aw-live"; this
# script only ever touches those two ids, and --uninstall removes exactly them.
#
# Usage: scripts/install-live-pane.sh [--provider claude|codex|cursor] [--dry-run] [--uninstall]
#   AW_DRY_RUN=1 is the same as --dry-run (setup.sh sets it).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

MARKETPLACE="agentic-workflow-mods"
PLUGIN="aw-live"
PLUGIN_ID="$PLUGIN@$MARKETPLACE"
MODS_DIR="$ROOT/mods"
DRY="${AW_DRY_RUN:-0}"
UNINSTALL=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    --uninstall) UNINSTALL=1 ;;
    *) echo "usage: install-live-pane.sh [--provider claude|codex|cursor] [--dry-run] [--uninstall]" >&2; exit 1 ;;
  esac
done

if [ "$AW_PROVIDER" != "claude" ]; then
  aw_hooks_init "$AW_PROVIDER"
  aw_unsupported live-pane "mods are Claude Code only; $AW_PROVIDER keeps the statusline segments and the daily scorer report"
  exit 0
fi

# Run a state-changing command, or print it under --dry-run.
run() {
  if [ "$DRY" = "1" ]; then
    printf '  [dry-run]'
    printf ' %q' "$@"
    printf '\n'
  else
    "$@"
  fi
}

if ! command -v claude >/dev/null 2>&1; then
  echo "  live-pane: skipped, the claude CLI is not on PATH"
  exit 0
fi

marketplaces="$(claude plugin marketplace list --json 2>/dev/null || echo '[]')"
plugins="$(claude plugin list --json 2>/dev/null || echo '[]')"
has_plugin() { printf '%s' "$plugins" | jq -e --arg id "$PLUGIN_ID" 'any(.[]?; .id == $id)' >/dev/null 2>&1; }
marketplace_location() {
  printf '%s' "$marketplaces" | jq -r --arg n "$MARKETPLACE" '[.[]? | select(.name == $n) | (.installLocation // "")] | first // empty' 2>/dev/null
}

if [ "$UNINSTALL" = "1" ]; then
  if has_plugin; then
    run claude plugin uninstall "$PLUGIN_ID" --scope user
    echo "  live-pane: $PLUGIN_ID uninstalled"
  else
    echo "  live-pane: $PLUGIN_ID was not installed"
  fi
  if [ -n "$(marketplace_location)" ]; then
    run claude plugin marketplace remove "$MARKETPLACE"
    echo "  live-pane: marketplace $MARKETPLACE removed"
  fi
  exit 0
fi

if [ ! -f "$MODS_DIR/aw-live/.claude-plugin/plugin.json" ]; then
  echo "  live-pane: skipped, $MODS_DIR/aw-live is missing"
  exit 0
fi

location="$(marketplace_location)"
if [ "$location" = "$MODS_DIR" ]; then
  echo "  live-pane: marketplace $MARKETPLACE up to date"
else
  # Absent, or registered from another checkout: (re)register this one.
  if [ -n "$location" ]; then
    run claude plugin marketplace remove "$MARKETPLACE"
    plugins='[]' # removing a marketplace takes its plugins with it
  fi
  run claude plugin marketplace add "$MODS_DIR" --scope user
  echo "  live-pane: marketplace $MARKETPLACE -> $MODS_DIR"
fi

if has_plugin; then
  echo "  live-pane: $PLUGIN_ID already installed (read from $MODS_DIR/aw-live; /reload-plugins picks up edits)"
else
  run claude plugin install "$PLUGIN_ID" --scope user
  if [ "$DRY" = "1" ]; then echo "  live-pane: $PLUGIN_ID would be installed"; else echo "  live-pane: $PLUGIN_ID installed"; fi
fi

# The mod shells out to `scorer live`; a scorer built before that command exists cannot feed it.
BIN_DIR="${CLAUDE_LOCAL_BIN:-$HOME/.local/bin}"
if [ "$DRY" != "1" ] && ! "$BIN_DIR/scorer" live --session aw-install-check --json 2>/dev/null | jq -e '.v == 1' >/dev/null 2>&1; then
  echo "  WARN: $BIN_DIR/scorer has no 'live' command yet; run scripts/install-scorer.sh so the pane has numbers"
fi
echo "  live-pane: open a session and type /live (pane) or /live status (text)"
```

In `providers/lib.sh` replace

```bash
AW_LEVER_INSTALLERS="install-wake-gating install-context-guard install-scope-gate install-done-gate install-external-write-guard"
```

with

```bash
AW_LEVER_INSTALLERS="install-wake-gating install-context-guard install-scope-gate install-done-gate install-external-write-guard install-live-pane"
```

and change the banner line `echo "=== Installing lever hooks for $provider (wake gating 1A, context-guard 2B, evaluator gates 3) ==="` to `echo "=== Installing lever hooks for $provider (wake gating 1A, context-guard 2B, evaluator gates 3, live pane) ==="`.

Docs. In `AGENTS.md`:
- after the line `├── scorer/        # Daily cost/involvement report from provider session transcripts` add `├── mods/          # Claude Code mods (in-process function hooks): aw-live pane + band. Claude-only`;
- after the line `scorer --since 7d [--provider claude|codex|cursor|all]  # Report to ~/.agentic-workflow/scorer/reports/` add `scorer live --session <id> [--cwd DIR] [--json]  # This session's numbers (the aw-live mod calls it)`;
- after the line `(cd skills/bugFixOrchestrator && npm test)  # Vitest (bugfix-state CLI)` add `claude plugin validate mods/aw-live && claude plugin test mods/aw-live  # the aw-live mod (needs claude >= 2.1.289)`;
- in Merge Gate item 2, append `, and \`claude plugin validate mods/aw-live\` and \`claude plugin test mods/aw-live\` pass` before the final period.

In `planning/PROVIDERS.md` add this row directly after the `| Scorer |` row of the Canonical layout table:

```
| Live pane | `mods/aw-live` (a Claude Code mod) + `scorer live` | Claude only. Codex and Cursor keep the statusline segments and the daily report |
```

and add this paragraph after the **Scorer.** paragraph:

```
**Mods.** `mods/aw-live` is a Claude Code mod (in-process function hooks, `claude plugin test`). It shows
this session's `scorer live` numbers in a `/live` pane and a band. Mods have no Codex or Cursor
equivalent, so `scripts/install-live-pane.sh --provider codex|cursor` prints a skip note and writes
nothing. Install: `scripts/install-live-pane.sh` (a folder marketplace named `agentic-workflow-mods`).
```

In `planning/ARCHITECTURE.md`, immediately before the `## Key Rules` heading add:

```
`scorer live --session <id> [--cwd DIR] [--json]` is the per-session, incremental view of the same
figures: it ingests only that session's transcripts into `~/.agentic-workflow/scorer/live/<session>.sqlite`
(never `scorer.sqlite`), reads the judge database read-only, and shares its SQL and statistics with the
daily report (`CTX`, `OVER_200K`, `percentile`, `summarizeDecisions`). The `aw-live` mod (`mods/aw-live/`)
renders it inside Claude Code.

```

- [ ] **Step 4: Run, verify pass**

Run: `chmod +x scripts/install-live-pane.sh && bash config/lib/tests/install-live-pane.test.sh`
Expected: eight `PASS:` lines (one may read `SKIP:` if `claude` lives in `/usr/bin`) and `All install-live-pane tests passed.`
Run: `bash providers/tests/install.test.sh && scripts/sync-rules.sh --check`
Expected: both exit 0.
Run: `./setup.sh --providers claude,codex,cursor --dry-run | grep -c "would run scripts/install-live-pane.sh"`
Expected: `3`.

- [ ] **Step 5: Real CLI, throwaway HOME** (the installer against the real `claude`, touching nothing of Joi's)

Run:
```bash
H="$(mktemp -d)"
# Subshell, so the orchestrator's own HOME and env are untouched. HOME alone is not enough if
# CLAUDE_CONFIG_DIR is set in the parent shell, so pin it too.
(
  export HOME="$H" CLAUDE_CONFIG_DIR="$H/.claude"
  bash scripts/install-live-pane.sh
  claude plugin list --json | jq -r '.[] | select(.id=="aw-live@agentic-workflow-mods") | .id, .enabled, .readFromFolder'
  bash scripts/install-live-pane.sh   # idempotent
  bash scripts/install-live-pane.sh --uninstall
  claude plugin list --json
)
```
Expected: the first run prints `marketplace agentic-workflow-mods -> <repo>/mods` and `aw-live@agentic-workflow-mods installed` (plus a `WARN` that `~/.local/bin/scorer` in the throwaway HOME has no `live` command, which is correct there). The `jq` line prints the id, `true`, and `<repo>/mods/aw-live`. The second run prints `up to date` and `already installed`, no `Successfully` lines. After `--uninstall` the list is `[]`.

- [ ] **Step 6: Commit**

```bash
git add mods/.claude-plugin/marketplace.json scripts/install-live-pane.sh config/lib/tests/install-live-pane.test.sh providers/lib.sh AGENTS.md planning/PROVIDERS.md planning/ARCHITECTURE.md
git commit -m "feat: install-live-pane lever (Claude-only, idempotent, dry-run, uninstall) and docs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

### Task 8: Report and live agree (gated on Plan A Task 7)

The goal is one definition per figure. Plan A Task 7 adds `undecided`, `agreed`, `overrode`, `p50LatencyMs`, `byProvider` to the report's `JudgeReportRow`, written inline. This task pins that the report and the live path give the same answer on the same decisions, so they cannot drift.

**Files:**
- Create: `scorer/tests/live-report-consistency.test.ts`
- Modify (only if the test fails): `scorer/src/judge-section.ts`

**Interfaces:**
- Consumes: `judgeSection(db, sinceIso): JudgeReportRow[]` with Plan A Task 7's fields; `judgeLive` (Task 3); `summarizeDecisions` (Task 3).

- [ ] **Step 0: Gate**

Run: `grep -n "byProvider" scorer/src/judge-section.ts`
Expected: at least one match (Plan A Task 7 landed). If there is none, skip this whole task, record "deferred: Plan A Task 7 not landed" in the PR description, and continue to Task 9.

- [ ] **Step 1: Write the test**

```ts
// scorer/tests/live-report-consistency.test.ts
import { describe, expect, it } from "vitest";

import { openDb, recordDecision, recordDecisionDetails } from "../../judge/src/db.js";
import type { DecisionRow } from "../../judge/src/db.js";
import { judgeSection } from "../src/judge-section.js";
import { judgeLive } from "../src/live-judge.js";

// One question, one session: the daily report and the live pane must compute the same figures
// from the same decisions. If this fails, make judge-section.ts compute the failing field with
// summarizeDecisions (./judge-stats.js) instead of changing this test.
describe("report and live agree", () => {
  it("gives the same provider share, agreement counts and latency percentiles", () => {
    const db = openDb(":memory:");
    const rows: Array<[string, string, number, "agreed" | "overrode" | "undecided", DecisionRow["outcome"]]> = [
      ["a", "jev", 300, "agreed", "decided"],
      ["b", "jev", 400, "overrode", "decided"],
      ["c", "rules", 0, "undecided", "decided"],
      ["d", "jev", 380, "agreed", "decided"],
      ["e", "claude-cli", 6200, "agreed", "decided"],
    ];
    for (const [id, provider, latency, agreement, outcome] of rows) {
      recordDecision(db, {
        id, ts: "2026-10-03T10:00:00.000Z", question: "ask-check", content_class: "brief", provider, decision: "continue", confidence: 0.9,
        reason_code: provider, latency_ms: latency, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome,
      });
      recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement, session_id: "s1" });
    }
    const [report] = judgeSection(db, "2026-10-01T00:00:00.000Z");
    const live = judgeLive(db, "s1");
    if (live.state !== "ok") throw new Error("expected an ok judge");
    expect(report).toMatchObject({
      byProvider: live.byProvider, agreed: live.agreed, overrode: live.overrode, undecided: live.undecided,
      p50LatencyMs: live.p50LatencyMs, p95LatencyMs: live.p95LatencyMs,
    });
  });
});
```

- [ ] **Step 2: Run**

Run: `cd scorer && npx vitest run tests/live-report-consistency.test.ts`
Expected: PASS. If it fails on a field, import `summarizeDecisions` into `judge-section.ts`, build its `DecisionFacts` rows from the question's decisions left-joined to `decision_details`, and take that field from the summary; re-run until it passes, and run Plan A's `scorer/tests/judge-section.test.ts` too (it must still pass unchanged).

- [ ] **Step 3: Full suite once, then commit**

Run: `cd scorer && npm run typecheck && npm run test:coverage`
Expected: clean, 100%.

```bash
git add scorer/tests/live-report-consistency.test.ts scorer/src/judge-section.ts
git commit -m "test: the daily report and the live pane compute judge figures identically" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

### Task 9: Proof in a real Claude Code, and the evidence file

Four independent proofs, cheapest first. Each result is recorded in the evidence file. A failing proof is a finding, not a reason to edit earlier tasks' behavior: record it, then fix in a follow-up commit only if it is a plain bug.

**Files:**
- Create: `scripts/live-pane-proof.py` (executable)
- Create: `docs/superpowers/evidence/2026-10-04-live-scorer-pane.md`

**Interfaces:**
- Consumes: everything above; `claude` ≥ 2.1.289 logged in; a built scorer (`cd scorer && npm run build` was run in Task 4).

- [ ] **Step 1: Add the pseudo-terminal harness**

Create `scripts/live-pane-proof.py`:

```python
#!/usr/bin/env python3
"""Drive a real interactive Claude Code session in a pseudo-terminal and prove the aw-live
pane renders: start `claude --plugin-dir <mod>`, send one tiny prompt (so the session has a
transcript), open /live, capture the screen text, and check the pane's sections are on it.

Evidence, not a test suite: it spends one haiku turn. Run it from a directory Claude Code
already trusts (the main checkout), or the folder-trust dialog eats the first keystrokes.

  scripts/live-pane-proof.py --plugin-dir mods/aw-live --cwd . --out evidence.txt
  env passed through: AW_SCORER_BIN, AW_STATE_DIR, AW_JUDGE_BIN (point them at a build and a scratch dir)
"""
import argparse
import fcntl
import os
import pty
import re
import select
import signal
import struct
import sys
import termios
import time

CURSOR_FORWARD = re.compile(r"\x1b\[(\d*)C")
ANSI = re.compile(r"\x1b\[[0-9;?<>=]*[ -/]*[@-~]|\x1b\][^\x07]*\x07|\x1b[()][A-Z0-9]|\x1b[=>]")
EXPECT = ["context", "session", "calls", "queuednow", "contextguard"]  # compared with whitespace removed


def drive(argv, cwd, steps, total, cols=150, rows=50):
    pid, fd = pty.fork()
    if pid == 0:
        os.chdir(cwd)
        env = dict(os.environ, TERM="xterm-256color", COLUMNS=str(cols), LINES=str(rows),
                   CLAUDE_CODE_FORCE_SESSION_PERSISTENCE="1")
        env.pop("CLAUDE_CODE_CHILD_SESSION", None)
        os.execvpe(argv[0], argv, env)
    fcntl.ioctl(fd, termios.TIOCSWINSZ, struct.pack("HHHH", rows, cols, 0, 0))
    out = b""
    start = time.time()
    pending = list(steps)
    while time.time() - start < total:
        ready, _, _ = select.select([fd], [], [], 0.2)
        if ready:
            try:
                chunk = os.read(fd, 65536)
            except OSError:
                break
            if not chunk:
                break
            out += chunk
        while pending and time.time() - start >= pending[0][0]:
            os.write(fd, pending.pop(0)[1])
    try:
        os.kill(pid, signal.SIGKILL)
        os.waitpid(pid, 0)
    except (ProcessLookupError, ChildProcessError):
        pass
    return out.decode("utf8", "replace")


def clean(raw):
    spaced = CURSOR_FORWARD.sub(lambda m: " " * int(m.group(1) or 1), raw)
    return ANSI.sub("", spaced)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plugin-dir", required=True)
    ap.add_argument("--cwd", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--model", default="haiku")
    ap.add_argument("--prompt", default="reply with the single word ok")
    args = ap.parse_args()
    steps = [
        (9, args.prompt.encode()), (10, b"\r"),   # one turn: the session gets calls in its transcript
        (30, b"/live"), (31, b"\r"),              # open the pane
        (44, b"\x1b"),                            # Esc hands the keys back
    ]
    raw = drive(["claude", "--plugin-dir", args.plugin_dir, "--model", args.model], args.cwd, steps, total=50)
    text = clean(raw)
    with open(args.out, "w") as fh:
        fh.write(text)
    squashed = re.sub(r"\s+", "", text).lower()
    missing = [w for w in EXPECT if w not in squashed]
    has_percent = re.search(r"\d+%", text) is not None
    print(f"captured {len(text)} chars; missing: {missing or 'none'}; percent shown: {has_percent}")
    return 0 if not missing and has_percent else 1


if __name__ == "__main__":
    sys.exit(main())
```

Make it executable: `chmod +x scripts/live-pane-proof.py`.

- [ ] **Step 2: Proof A: the contract and the installer** (no model call)

Run:
```bash
claude --version
claude plugin validate mods
claude plugin validate mods/aw-live
claude plugin test mods/aw-live
```
Expected: `2.1.289 (Claude Code)` or newer; validation passes for both paths (a `plugins[0].source ... is or traverses a symlink` warning must NOT appear: `mods/aw-live` is a real directory); tests `0 fail`.

- [ ] **Step 3: Proof B: warm latency of `scorer live`** (no model call; the transcript is synthetic)

Write `$TMP/bench.mjs` and run it from the repo root (`cd scorer && npm run build` was done in Task 4):

```bash
REPO="$PWD"; TMP="$(mktemp -d)"; export REPO TMP
cat > "$TMP/bench.mjs" <<'EOF'
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const { REPO, TMP } = process.env;
const dir = path.join(TMP, "projects", "-work-bench");
fs.mkdirSync(dir, { recursive: true });
const file = path.join(dir, "bench-1.jsonl");
const line = (i, ts) => JSON.stringify({
  type: "assistant", uuid: `u${i}`, sessionId: "bench-1", isSidechain: false, timestamp: ts,
  message: { id: `m${i}`, model: "claude-opus-5-5", role: "assistant", content: [{ type: "text", text: "x".repeat(200) }],
    usage: { input_tokens: 5, cache_read_input_tokens: 40000 + i, cache_creation_input_tokens: 100, output_tokens: 50 } },
});
const ts = (i) => new Date(Date.UTC(2026, 9, 4, 10, 0, 0) + i * 1000).toISOString();
fs.writeFileSync(file, Array.from({ length: 20000 }, (_, i) => line(i, ts(i))).join("\n") + "\n");

const run = () => {
  const t0 = performance.now();
  const r = spawnSync("node", [path.join(REPO, "scorer/dist/cli.js"), "live", "--session", "bench-1", "--cwd", "/work/bench",
    "--projects-dir", path.join(TMP, "projects"), "--state-dir", path.join(TMP, "state"), "--json"], { encoding: "utf8" });
  return { ms: performance.now() - t0, snap: JSON.parse(r.stdout) };
};
const cold = run();
console.log(`cold_ms=${Math.round(cold.ms)} calls=${cold.snap.usage.calls} newLines=${cold.snap.ingest.newLines}`);
const warm = [];
for (let i = 0; i < 15; i += 1) {
  fs.appendFileSync(file, line(20000 + i, ts(20000 + i)) + "\n");
  const r = run();
  if (r.snap.ingest.newLines !== 1) throw new Error(`expected 1 new line, got ${r.snap.ingest.newLines}`);
  warm.push(r.ms);
}
warm.sort((a, b) => a - b);
console.log(`warm_median_ms=${Math.round(warm[7])} warm_max_ms=${Math.round(warm[14])}`);
EOF
node "$TMP/bench.mjs"
```
Expected: `cold_ms=` a second or so (the one-time full ingest of 20,000 lines; `calls=20000`, `newLines=20000`) and `warm_median_ms=` **under 200** (each warm run ingests exactly one new line, which the script asserts). Record both. If the median is over 200 on a busy machine, re-run once; if it stays over, record the number as a finding and do not change the target.

- [ ] **Step 4: Proof C: the whole chain through a real session, as text** (one haiku turn, about $0.05)

`REPO` and `TMP` come from Step 3 (set `REPO="$PWD"` again if the shell was reset). Run:

```bash
W="$(mktemp -d)"; ST="$(mktemp -d)"; export W ST
printf '#!/usr/bin/env bash\nexec node %s/scorer/dist/cli.js "$@"\n' "$REPO" > "$W/scorer"; chmod +x "$W/scorer"
export CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1
SID="$(cd "$W" && AW_STATE_DIR="$ST" claude -p "reply with the single word ok" --model haiku --output-format json </dev/null | jq -r .session_id)"; export SID; echo "session=$SID"
(cd "$W" && AW_STATE_DIR="$ST" AW_SCORER_BIN="$W/scorer" claude -p "/live status" --resume "$SID" --plugin-dir "$REPO/mods/aw-live" --output-format json </dev/null | jq -r .result)
(cd "$W" && AW_STATE_DIR="$ST" "$W/scorer" live --session "$SID" --cwd "$W" --projects-dir "$HOME/.claude/projects")
```
Expected: the `/live status` text starts `live · ctx N% · $0.0x · K calls`, has `context`, `session`, `judge` and `wakes` sections (the `judge` card reads `not linked yet …` if the installed judge predates Plan A, nothing numeric otherwise), and its `calls` and `tokens` equal the direct `scorer live` output on the last command (the same K calls). Record the text. (`CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1` is needed because a session started from inside another Claude Code session otherwise does not save its transcript.)

- [ ] **Step 5: Proof D: judge scoping end to end** (no model call; reuses `$SID`, `$ST`, `$W`, `$REPO`)

Build judge first (its `dist` predates Plan A's tables): `cd judge && npm run build && cd ..`. Then seed three decisions for this session and read them back through the mod:

```bash
node --input-type=module -e '
const { openDb, recordDecision, recordDecisionDetails } = await import(process.env.REPO + "/judge/dist/db.js");
const db = openDb(process.env.ST + "/judge/decisions.sqlite");
const mk = (id, q, provider, decision, latency, agreement) => {
  recordDecision(db, { id, ts: new Date().toISOString(), question: q, content_class: "brief", provider, decision, confidence: 0.9, reason_code: provider, latency_ms: latency, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" });
  recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement, session_id: process.env.SID });
};
mk("proof-1", "brief-scope", "jev", "ready", 400, "agreed");
mk("proof-2", "ask-check", "jev", "continue", 350, "overrode");
mk("proof-3", "wake-gate", "rules", "send", 0, "undecided");
db.close();
'
export CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1
(cd "$W" && AW_STATE_DIR="$ST" AW_SCORER_BIN="$W/scorer" claude -p "/live status" --resume "$SID" --plugin-dir "$REPO/mods/aw-live" --output-format json </dev/null | jq -r .result)
```
Expected: the `judge` card has a `calls` row reading `3 · jev 67% · rules 33%` and a `vs rules` row reading `agreed 1 · overrode 1 · unsure 1`; the `gates` card shows `scope-gate`, `done-gate` and `send-gate` rows reading `1 · ready 1 · p50 400 ms`, `1 · continue 1 · p50 350 ms` and `1 · send 1 · p50 0 ms`. Rows that the hooks of Step 4's `claude` runs wrote with another or no session id must not appear in these counts (that is the scoping working). Record the text.

- [ ] **Step 6: Proof E: the pane draws** (a pseudo-terminal, one haiku turn, about $0.05)

Run with `cwd` the **main checkout**: Claude Code already trusts it, and an untrusted directory shows a folder-trust dialog that swallows the keystrokes. This starts a real new interactive session there; nothing is edited. `W`, `REPO`, `TMP` come from Steps 3 and 4.

```bash
ST2="$(mktemp -d)"
AW_STATE_DIR="$ST2" AW_SCORER_BIN="$W/scorer" python3 "$REPO/scripts/live-pane-proof.py" \
  --plugin-dir "$REPO/mods/aw-live" --cwd /Users/joi/personal/agentic-workflow --out "$TMP/pane.txt"; echo "exit=$?"
```
Expected: `captured N chars; missing: none; percent shown: True` and `exit=0`. Then Read `$TMP/pane.txt` and confirm with your own eyes that it holds the `/live` slash-menu entry `Toggle the live scorer pane`, a boxed `context` section with `window  NN% ▓▓…░░`, a `session` box with `cost`, `calls`, `over 200k`, and a `wakes` box with `queued now` and `context guard`. The capture is a stream of terminal redraws, so lines repeat; judge by the presence of the sections and numbers. If the harness times out (slow start, a dialog), re-run once; if it still fails, record `proof E: not obtained` with the last 40 lines of `$TMP/pane.txt`, and rely on the `claude plugin test` mount tests (they draw the pane on the terminal and desktop surfaces).

- [ ] **Step 7: Write the evidence file** `docs/superpowers/evidence/2026-10-04-live-scorer-pane.md`

Contents (fill the bracketed values from the runs above; aggregates only, no real prompts, paths or transcript lines):

```markdown
# Evidence: live scorer pane (Plan D)

- Claude Code: `claude --version` = [value]. Date: 2026-10-04 (or the run date).
- Proof A (validate + test): [pass/fail], `claude plugin test mods/aw-live` = [N pass, 0 fail].
- Proof B (latency of `scorer live`, 20,000-line synthetic transcript, `$TMP/bench.mjs`): cold [N] ms, warm median [N] ms over 15 runs each ingesting one line. Target < 200 ms warm: [met/not met].
- Proof C (`/live status` through `claude -p --resume`): [paste the status text]. `calls` and `tokens` matched the direct `scorer live` output: [yes/no].
- Proof D (judge scoping, three seeded decisions for the session): [paste the judge and gates lines]. Rows from other sessions excluded: [yes/no].
- Proof E (pane drawn in a real interactive session): [obtained/not obtained]. Lines seen: [the `window`, `cost`, `calls`, `queued now` lines].
- Findings: hook latency is not observable from a mod in this build (classic hooks did not fire under `claude -p --plugin-dir`; `tool.call` times tool and hooks together). Shown instead: per-gate p50 from `decisions.latency_ms`.
- Not proven here: the install into Joi's real `~/.claude` (run `scripts/install-live-pane.sh --provider claude` from the main checkout after merge); the y/x override labels against a real labelable decision (Plan A Task 3 must be installed first).
```

- [ ] **Step 8: Commit**

```bash
git add scripts/live-pane-proof.py docs/superpowers/evidence/2026-10-04-live-scorer-pane.md
git commit -m "docs: evidence for the live scorer pane (real session, latency, judge scoping)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014AadfQDbU9tvxuKzEh5JaC"
```

---

## Done when

- The merge gate in `AGENTS.md` passes: `scorer` typecheck and coverage at 100%, `claude plugin validate mods/aw-live` and `claude plugin test mods/aw-live`, every bash test (including `install-live-pane.test.sh` and `context-guard.test.sh`), `scripts/sync-rules.sh --check`, and `./setup.sh --providers claude,codex,cursor --dry-run` (which now prints the live-pane line for each provider).
- `scorer live --session <id> --json` returns a schema-valid snapshot, ingests only appended bytes on the second call, and is under 200 ms warm (Task 9, Proof B).
- The pane, the band and `/live status` show context, cost, calls, judge activity, gates, queued wakes and context-guard fires for the current session, and were observed in a real session (Proofs C, D, E).
- Claude-only is stated in `PROVIDERS.md`, in the installer's output for Codex and Cursor, and in the PR description.
- No step required Joi to label anything.

## Self-review

- **Spec coverage:** per-session live query (Tasks 3, 4); the numbers listed in the brief: context % (host plus transcript), session cost (host) and tokens (Task 4), calls over 200k (Task 4), judge calls / provider share / undecided / agreed / overrode / p50 (Tasks 3, 4), gates (Task 3), wakes (Task 4), context-guard fires (Tasks 2, 4), hook latency (decision: per-gate latency, rest not observable; Proof findings); the mod pane, band, `/live status`, refresh on turn end and timer, never blocking, silent (Tasks 5, 6); optional hotkeys (Task 6); Claude-only (stated, installer, docs); install with `--dry-run`, owner tag, idempotent, bash test (Task 7); report and live share code (Tasks 1, 3, 8); proof in a real session (Task 9).
- **Placeholder scan:** none; every code step carries the code. The two data-dependent steps (Proof B/D numbers) say exactly what to record.
- **Type consistency:** `LiveSnapshot` is defined once by `LiveSnapshotSchema` (scorer) and once structurally in `mods/aw-live/types/index.d.ts`, and `scorer/tests/live-contract.test.ts` parses the mod's sample with the scorer's schema. `GateName`, `JudgeLatest`, `labelFor`, `binCandidates(home, name, override?)`, `liveArgs(sessionId, cwd, window | null)` are used with the same signatures in Tasks 5 and 6.
- **Review Focus:** RF-1 to RF-5 each name their pinning test and task.
