# Sindri evolve privacy B2: learn from every session — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `sindri evolve` telemetry, correct and reflect learn from every session Sindri can read (any repo; Claude Code, Codex, Cursor), while its output still only changes the toolkit and still passes B1's publish gate.

**Architecture:** Three provider readers (ported from `scorer/src/transcript/`) produce one `SessionLine` stream. Each line gets a `provider` and a `repo` (the profile repo whose checkout or git worktree contains its cwd, else `unknown`). The toolkit-only filter goes away; consumers filter by `repo` only where they must (reflect's branch transcript). Proposals sourced from another repo's PR render with a generic source label and no PR numbers.

**Tech Stack:** TypeScript 5.7 strict, Node ≥ 20, Zod 3, Vitest (100% coverage).

**Spec:** `docs/superpowers/specs/2026-10-09-sindri-evolve-privacy-design.md` §6 (and §5 for the gate this relies on).

**Prerequisite:** B1 (`2026-10-09-sindri-evolve-privacy-b1-publish-gate.md`) is merged. Branch off `origin/main` after that merge.

## Global Constraints

- Everything in B1's Global Constraints applies (no workplace names, fail closed, no matched text in output, 100% coverage, no `any`, one heavy job at a time, test count never drops).
- New source files live under `sindri/src/evolve/` (evaluation machinery; evolve can't self-adopt changes to them).
- Ported code is copied, not imported, from `scorer` (the packages stay independent; the precedent is `correct.ts`'s labeler). Each ported file says so in a one-line comment naming the scorer file.
- Decided while planning (the spec is updated to match): provider directories are additive keys: `sources.transcripts.dir` stays the Claude dir; `sources.transcripts.codex.dir` and `sources.transcripts.cursor.dir` are new — so no profile migration. Toolkit reflect keys stay `reflect:pr-<n>`; only other repos use `reflect:<repo>:pr-<n>`, so no ledger migration. A proposal sourced from another repo renders its source as `reflect on another repo's PR` and its `pr:` evidence is withheld.
- Accepted limits (documented in `docs/sindri/evolve.md`, not fixed here): Codex and Cursor lines carry no git branch, so reflect's branch transcript comes from Claude sessions only; telemetry's hook-fire detection matches Claude's format, so Codex and Cursor sessions contribute no hook fires; Cursor lines carry no timestamp, so they take the file's mtime and are never deduped.

## Review Focus

1. A Claude session that `cd`s from the toolkit into a private repo mid-session: each line's `repo` follows the latest cwd seen in that file, so lines after the move are attributed to the private repo. Pinned in Task 2.
2. A git worktree of a private repo that has since been removed: its sessions attribute to `unknown`, never to the toolkit. Pinned in Task 2.
3. A Codex thread imported from Claude: its copied history isn't read twice; only turns after its first `turn_context` count. Pinned in Task 1.
4. A provider directory that doesn't exist (Cursor not installed): that provider is skipped silently and the others still read. Pinned in Task 1.
5. Old proposals whose evidence uses the pre-B2 ref form `transcript:<session8>#<n>`: they still resolve (as Claude refs) for `show`, generalize and the verbatim gate. Pinned in Task 2.

---

### Task 1: Provider readers

**Files:**
- Create: `sindri/src/evolve/sources/types.ts`, `claude.ts`, `codex.ts`, `cursor.ts`
- Modify: `sindri/src/evolve/transcripts.ts` (move Claude parsing helpers into `sources/claude.ts`; keep re-exports `textBlocks`, `toolNames`, `sessionOf` from `transcripts.ts` so existing imports keep working)
- Test: `sindri/tests/evolve-sources.test.ts`, fixtures under `sindri/tests/fixtures/sessions/{claude,codex,cursor}/`

**Interfaces:**
- Produces (`types.ts`):

```ts
import type { Block } from "../transcripts.js";

export type Provider = "claude" | "codex" | "cursor";

export interface RawEntry {
  n: number;            // 1-based line number in the file
  ts: string;           // ISO timestamp; Cursor: the file's mtime
  type: string;         // "user" | "assistant" | provider-specific others (ignored downstream)
  cwd: string | null;   // the cwd this line ran in, when the provider records one
  meta: boolean;        // harness-injected (Claude isMeta)
  branch: string;       // git branch, "" when unknown
  blocks: Block[];
  tools: string[];
}

export interface RawSession {
  provider: Provider;
  file: string;
  session: string;          // short, opaque: Claude's sessionOf(file); Codex/Cursor: first 8 alphanumerics of the thread/transcript id (+ ".<agent>" for subagents)
  project: string | null;   // Cursor only: the ~/.cursor/projects/<project> directory name
  mtimeMs: number;
  entries: RawEntry[];
}
```

- `readClaude(dir: string, since: Date, maxFiles: number): RawSession[]`
- `readCodex(dir: string, since: Date, maxFiles: number): RawSession[]`
- `readCursor(dir: string, since: Date, maxFiles: number): RawSession[]`
- Each returns `[]` when `dir` doesn't exist; each skips files with `mtimeMs < since`; each keeps the newest `maxFiles` files.

Provider rules:
- **Claude:** exactly today's parsing (`parseEntry`, `blocksOf`, `toolNames`, `isMeta`, `gitBranch`, per-line `cwd`), including subagent files under `<session>/subagents/`.
- **Codex** (ported from `scorer/src/transcript/codex.ts`): files `rollout-*.jsonl` under `dir` recursively. The first line's `session_meta.payload.cwd` is every entry's `cwd`; `payload.git.branch`, when present, is every entry's `branch`. `response_item` with `payload.type === "message"` → entry `type` = `payload.role`, blocks = `input_text`/`output_text` texts; `payload.type === "function_call"` → an assistant entry with `tools: [payload.name]` and no blocks; `payload.type === "function_call_output"` → a user entry with one `toolResult` block holding `payload.output` (string) . Imported threads (ids listed in `<dir>/../external_agent_session_imports.json`) skip every line before the first `turn_context`.
- **Cursor** (ported from `scorer/src/transcript/cursor.ts`): files `<dir>/<project>/agent-transcripts/<id>/<id>.jsonl` and `<id>/subagents/<sub>.jsonl`. Lines `{ role, message: { content } }` → entry `type` = role, blocks from `text` parts, tools from `tool_use` names. `cwd: null`, `branch: ""`, `ts` = file mtime ISO, `project` = the `<project>` directory name.

- [ ] **Step 1: Write fixtures and failing tests**

Fixtures (all synthetic):
- `tests/fixtures/sessions/codex/2026/10/08/rollout-2026-10-08T10-00-00-11111111-2222-4333-8444-555555555555.jsonl`: `session_meta` (id `11111111-...`, cwd `/w/globex-app`, `git: { branch: "fix/rota" }`), a `turn_context`, a user `message` ("the rota check failed again"), a `function_call` (`shell`), a `function_call_output` ("exit 1"), an assistant `message` ("I will rerun it").
- the same directory: an imported thread `rollout-...-66666666-....jsonl` with two `message` lines before its first `turn_context` and one after; `tests/fixtures/sessions/codex-home/external_agent_session_imports.json` → put the sessions under `codex-home/sessions/` so the imports file sits at `<dir>/..`.
- `tests/fixtures/sessions/cursor/Users-dev-w-globex-app/agent-transcripts/abc123de/abc123de.jsonl`: a user line and an assistant line with a `tool_use`.

```ts
import path from "node:path";
import { describe, expect, it } from "vitest";

import { readClaude } from "../src/evolve/sources/claude.js";
import { readCodex } from "../src/evolve/sources/codex.js";
import { readCursor } from "../src/evolve/sources/cursor.js";

const FX = path.join(import.meta.dirname, "fixtures", "sessions");
const EPOCH = new Date(0);

describe("readCodex", () => {
  it("reads messages, tool calls and outputs, with the session's cwd and branch on every entry", () => {
    const [s] = readCodex(path.join(FX, "codex-home", "sessions"), EPOCH, 500).filter((x) => x.session === "11111111");
    expect(s.provider).toBe("codex");
    expect(s.entries.map((e) => [e.type, e.blocks.map((b) => b.text).join("|"), e.tools.join(",")])).toEqual([
      ["user", "the rota check failed again", ""],
      ["assistant", "", "shell"],
      ["user", "exit 1", ""],
      ["assistant", "I will rerun it", ""],
    ]);
    expect(new Set(s.entries.map((e) => `${e.cwd}@${e.branch}`))).toEqual(new Set(["/w/globex-app@fix/rota"]));
  });

  it("skips an imported thread's copied history (Review Focus 3)", () => {
    const [s] = readCodex(path.join(FX, "codex-home", "sessions"), EPOCH, 500).filter((x) => x.session === "66666666");
    expect(s.entries.map((e) => e.blocks[0]?.text)).toEqual(["after the first turn_context"]);
  });
});

describe("readCursor", () => {
  it("reads roles, text and tool names, with the project directory and the file mtime", () => {
    const [s] = readCursor(path.join(FX, "cursor"), EPOCH, 500);
    expect(s.project).toBe("Users-dev-w-globex-app");
    expect(s.entries.map((e) => [e.type, e.tools.join(",")])).toEqual([["user", ""], ["assistant", "Read"]]);
    expect(s.entries.every((e) => e.cwd === null && e.ts === new Date(s.mtimeMs).toISOString())).toBe(true);
  });
});

describe("missing provider directories (Review Focus 4)", () => {
  it("returns nothing, without throwing", () => {
    for (const read of [readClaude, readCodex, readCursor]) expect(read("/nonexistent/dir", EPOCH, 500)).toEqual([]);
  });
});
```

Add one Claude test that `readClaude` over the existing `tests/fixtures/hook-fires` directory returns the same entries `readRepoSessions` produced before the change (pin a count and one entry's text).

- [ ] **Step 2: Run to verify it fails**

Run: `cd sindri && npx vitest run tests/evolve-sources.test.ts`
Expected: FAIL (modules not found).

- [ ] **Step 3: Implement**

`sources/claude.ts`: move `parseEntry`, `contentOf`, `blocksOf`, `walk`, `tsOf`, `sessionOf`, `textBlocks`, `toolNames` from `transcripts.ts`; `readClaude` walks `dir`, filters by mtime, keeps the newest `maxFiles`, and maps each file's lines to `RawEntry` (`cwd` = `typeof e.cwd === "string" ? e.cwd : null`, carried forward from the latest line that had one; `branch` = `gitBranch` or `""`).

`sources/codex.ts`, following `scorer/src/transcript/codex.ts`'s `rollouts`, `importedThreadIds`, `SessionMetaSchema` (extended with `git: z.object({ branch: z.string().optional() }).optional()`), and `createCodexParser`'s `live` flag:

```ts
const MessagePayload = z.object({ type: z.literal("message"), role: z.string(), content: z.array(z.object({ type: z.string(), text: z.string().optional() })) });
const CallPayload = z.object({ type: z.literal("function_call"), name: z.string() });
const OutputPayload = z.object({ type: z.literal("function_call_output"), output: z.unknown() });

function entryOf(payload: unknown, base: Omit<RawEntry, "type" | "blocks" | "tools">): RawEntry | null {
  const m = MessagePayload.safeParse(payload);
  if (m.success) {
    const blocks = m.data.content.flatMap((c) => ((c.type === "input_text" || c.type === "output_text") && c.text !== undefined ? [{ text: c.text, toolResult: false }] : []));
    return { ...base, type: m.data.role, blocks, tools: [] };
  }
  const call = CallPayload.safeParse(payload);
  if (call.success) return { ...base, type: "assistant", blocks: [], tools: [call.data.name] };
  const out = OutputPayload.safeParse(payload);
  if (out.success) return { ...base, type: "user", blocks: [{ text: typeof out.data.output === "string" ? out.data.output : JSON.stringify(out.data.output), toolResult: true }], tools: [] };
  return null;
}
```

`readCodex` reads each rollout, takes `cwd`/`branch`/`id` from line 1, sets `live = !imported`, flips `live` on `turn_context`, and emits `entryOf(...)` for `response_item` lines while live. `session` = the first 8 alphanumerics of the thread id (for a subagent rollout: `<parent8>.<own id alnum, ≤40>`, mirroring Claude's `sessionOf`).

`sources/cursor.ts`, following `scorer/src/transcript/cursor.ts`'s discovery: for each `<project>` dir, each `agent-transcripts/<id>/<id>.jsonl` plus `<id>/subagents/*.jsonl`; entries from `{ role, message: { content } }` lines (`text` parts → blocks, `tool_use` → tools); other lines skipped.

`transcripts.ts` keeps exporting `Block`, `SessionLine`, `textBlocks`, `toolNames`, `sessionOf` (re-exported from `sources/claude.ts`).

- [ ] **Step 4: Run to verify it passes**

Run: `cd sindri && npx vitest run tests/evolve-sources.test.ts tests/evolve-transcripts.test.ts`
Expected: PASS.

- [ ] **Step 5: Full check, then commit**

Run (alone): `cd sindri && npm run typecheck && npm test`

```bash
git add sindri/src/evolve/sources sindri/src/evolve/transcripts.ts sindri/tests/evolve-sources.test.ts sindri/tests/fixtures/sessions
git commit -m "feat: sindri evolve reads Codex and Cursor sessions beside Claude's"
```

---

### Task 2: `readSessions` — every session, attributed to a repo

**Files:**
- Modify: `sindri/src/evolve/transcripts.ts` (replace `readRepoSessions`, `resolveRefs`, `excerptFor`, `transcriptsDir`)
- Modify: `sindri/src/evolve/proposals.ts` (`reduceEvidence` accepts the provider-qualified ref)
- Modify: `sindri/src/profile/schema.ts` (`sources.transcripts.codex.dir`, `sources.transcripts.cursor.dir`)
- Regenerate: `docs/sindri/profile.md`
- Test: `sindri/tests/evolve-transcripts.test.ts`, `sindri/tests/evolve-proposals.test.ts`

**Interfaces:**
- Consumes: Task 1 readers.
- Produces:

```ts
export interface SessionSources { claude: string; codex: string; cursor: string }
export interface RepoRoot { name: string; paths: string[] }            // checkout path + every `git worktree list` path

export interface SessionLine {                                           // `SessionLine` gains provider and repo
  provider: Provider; repo: string;                                     // a profile repo name, or "unknown"
  file: string; session: string; n: number; ref: string; ts: string; type: string; meta: boolean; branch: string; blocks: Block[]; tools: string[];
}

export function transcriptSources(ctx: EvolveCtx): SessionSources;        // ~ expanded; defaults below
export async function repoRoots(ctx: EvolveCtx): Promise<RepoRoot[]>;     // one `git worktree list --porcelain` per profile repo
export function repoOf(cwd: string | null, project: string | null, roots: readonly RepoRoot[]): string;
export function readSessions(src: SessionSources, roots: readonly RepoRoot[], since: Date, maxFiles?: number): { lines: SessionLine[]; files: number; skipped: number };
export function resolveRefs(src: SessionSources, roots: readonly RepoRoot[], refs: readonly string[], scrub?: Scrubber): Map<string, string>;
export function excerptFor(src: SessionSources, roots: readonly RepoRoot[], ref: string, max?: number, scrub?: Scrubber): string | null;
```

- Refs: `transcript:<provider>:<session>#<n>`. `resolveRefs` also accepts the legacy `transcript:<session>#<n>` and resolves it as `claude` (Review Focus 5); the map key is the ref exactly as passed.
- `repoOf`: with a `cwd`, the root with the longest path that equals or contains it (both `path.resolve`d; same rule as today's `underRepo`); with only a Cursor `project`, the root one of whose paths encodes to exactly that name (`p.split(/[^A-Za-z0-9]+/).filter(Boolean).join("-")`); else `"unknown"`.
- Claude lines keep today's rule: `repo` follows the latest cwd seen in that file (Review Focus 1).
- Dedupe (resumed and forked copies) is unchanged and runs across all providers; Cursor entries (ts from mtime) never dedupe.
- Profile: `sources.transcripts.codex: { dir: "~/.codex/sessions" }`, `sources.transcripts.cursor: { dir: "~/.cursor/projects" }`, both `.strict().default({})` objects with a `dir` string default; `sources.transcripts.dir` (Claude) unchanged.

- [ ] **Step 1: Write the failing tests**

```ts
describe("repoOf (privacy B2)", () => {
  const roots = [
    { name: "toolkit", paths: ["/w/toolkit"] },
    { name: "globex-app", paths: ["/w/globex-app", "/w/.wt/globex-app/fix-rota"] },
    { name: "globex-app-admin", paths: ["/w/globex-app-admin"] },
  ];
  it("picks the repo whose checkout or worktree contains the cwd, longest path first", () => {
    expect(repoOf("/w/toolkit/sindri", null, roots)).toBe("toolkit");
    expect(repoOf("/w/.wt/globex-app/fix-rota/src", null, roots)).toBe("globex-app");
    expect(repoOf("/w/globex-app-admin", null, roots)).toBe("globex-app-admin"); // not a prefix match on globex-app
    expect(repoOf("/w/toolkit/../globex-app", null, roots)).toBe("globex-app");
  });
  it("returns unknown for a removed worktree, a relative cwd or nothing (Review Focus 2)", () => {
    expect(repoOf("/w/.wt/globex-app/old-branch", null, roots)).toBe("unknown");
    expect(repoOf("relative/dir", null, roots)).toBe("unknown");
    expect(repoOf(null, null, roots)).toBe("unknown");
  });
  it("matches a Cursor project name against the encoded checkout and worktree paths", () => {
    expect(repoOf(null, "w-wt-globex-app-fix-rota", roots)).toBe("globex-app");
    expect(repoOf(null, "w-globex", roots)).toBe("unknown");
  });
});

describe("readSessions (privacy B2)", () => {
  it("reads all three providers, attributes each line, and uses provider-qualified refs", () => {
    const { lines } = readSessions(SOURCES, ROOTS, new Date(0));
    expect(new Set(lines.map((l) => l.provider))).toEqual(new Set(["claude", "codex", "cursor"]));
    expect(lines.find((l) => l.provider === "codex")).toMatchObject({ repo: "globex-app", ref: expect.stringMatching(/^transcript:codex:11111111#\d+$/) as string });
  });
  it("follows a Claude session that cds from the toolkit into another repo (Review Focus 1)", () => {
    const { lines } = readSessions({ ...SOURCES, codex: "/none", cursor: "/none" }, ROOTS, new Date(0));
    expect(lines.filter((l) => l.session === MOVING).map((l) => l.repo)).toEqual(["toolkit", "toolkit", "globex-app"]);
  });
});

describe("resolveRefs across providers (privacy B2)", () => {
  it("resolves provider-qualified refs and the legacy Claude form (Review Focus 5)", () => {
    const m = resolveRefs(SOURCES, ROOTS, [`transcript:claude:${MOVING}#1`, `transcript:${MOVING}#1`, "transcript:codex:11111111#2"]);
    expect(m.get(`transcript:claude:${MOVING}#1`)).toBe(m.get(`transcript:${MOVING}#1`));
    expect(m.get("transcript:codex:11111111#2")).toBe("the rota check failed again");
  });
});
```

Build `SOURCES`, `ROOTS` and the `MOVING` Claude fixture (three lines: two with `cwd` under the toolkit root, the third with `cwd: /w/globex-app`) at the top of the file, writing the Claude JSONL to a temp dir and pointing `codex`/`cursor` at Task 1's fixture dirs. Fix the fixture line numbers in the refs to the real ones. In `evolve-proposals.test.ts`, extend the `reduceEvidence` cases: `transcript:codex:11111111#2` passes through unchanged, `transcript:claude:5e55a1d0.abc#4` passes through, a provider other than the three is withheld.

- [ ] **Step 2: Run to verify it fails**

Run: `cd sindri && npx vitest run tests/evolve-transcripts.test.ts tests/evolve-proposals.test.ts`
Expected: FAIL.

- [ ] **Step 3: Implement**

`reduceEvidence`: add before the legacy pattern

```ts
    const qualified = /^transcript:(claude|codex|cursor):([A-Za-z0-9]{1,8}(?:\.[A-Za-z0-9]{1,40})?)#(\d{1,7})$/.exec(r);
```

and when it matches, `clean = \`transcript:${q[1]}:${q[2]}#${q[3]}\``. Any other `transcript:<word>:` form is withheld.

`readSessions`: call the three readers with `maxFiles` split evenly (`Math.ceil(maxFiles / 3)` each), sort all sessions by first/last timestamp then file (today's order), and for each entry compute `repo` (Claude: from the carried `cwd`; Codex: from the session cwd; Cursor: from `project`), build `ref = \`transcript:${provider}:${session}#${n}\``, apply the dedupe exactly as today, and drop the `underRepo` filter. `transcriptSources(ctx)` expands `~` for all three dirs from the profile. `repoRoots(ctx)`:

```ts
export async function repoRoots(ctx: EvolveCtx): Promise<RepoRoot[]> {
  const out: RepoRoot[] = [];
  for (const [name, cfg] of Object.entries(ctx.loaded.repos)) {
    const r = await ctx.deps.git.run(["worktree", "list", "--porcelain"], cfg.path);
    const extra = r.ok ? r.stdout.split("\n").filter((l) => l.startsWith("worktree ")).map((l) => l.slice("worktree ".length)) : [];
    out.push({ name, paths: [...new Set([cfg.path, ...extra])] });
  }
  return out;
}
```

Update every caller of `readRepoSessions`, `resolveRefs`, `excerptFor` and `transcriptsDir` (`grep -rn "readRepoSessions\|resolveRefs\|excerptFor\|transcriptsDir" sindri/src`) to pass `transcriptSources(ctx)` and `await repoRoots(ctx)`. Consumers keep today's behavior in this task except that they now see every repo's lines; Task 3 narrows reflect.

Schema (inside the `transcripts` object in `SourcesSchema`):

```ts
      .object({
        enabled: z.boolean().default(false),
        dir: z.string().default("~/.claude/projects"),
        codex: z.object({ dir: z.string().default("~/.codex/sessions") }).strict().default({}),
        cursor: z.object({ dir: z.string().default("~/.cursor/projects") }).strict().default({}),
      })
```

and extend its `.describe` to say evolve reads all three regardless of `enabled`. Run `npm run gen`.

- [ ] **Step 4: Run to verify it passes**

Run: `cd sindri && npx vitest run tests/evolve-transcripts.test.ts tests/evolve-proposals.test.ts tests/evolve-telemetry.test.ts tests/evolve-correct.test.ts tests/evolve-reflect.test.ts tests/evolve-proposals-cmd.test.ts`
Expected: PASS after updating fixtures' expected refs to the qualified form (`transcript:claude:...`). Don't weaken any assertion beyond the ref form.

- [ ] **Step 5: Full check, then commit**

Run (alone): `cd sindri && npm run typecheck && npm test`

```bash
git add sindri/src sindri/tests docs/sindri/profile.md
git commit -m "feat: sindri evolve reads every session and attributes each line to a repo"
```

---

### Task 3: `reflect --repo` and the weekly job across repos

**Files:**
- Modify: `sindri/src/evolve/reflect.ts` (`branchTranscript`), `sindri/src/evolve/cmd/reflect.ts`, `sindri/src/evolve/cmd/weekly.ts`, `sindri/src/evolve/github.ts` (only if `ghRepoOf`/`prContext`/`allowedAuthors` hard-code the toolkit path; they take a repo path today)
- Test: `sindri/tests/evolve-reflect-cmd.test.ts`, `sindri/tests/evolve-weekly.test.ts`, `sindri/tests/evolve-reflect.test.ts`

**Interfaces:**
- Produces:
  - `sindri evolve reflect [--repo <name>] --pr <n> [--json]`; `--repo` defaults to `profile.tracker.repo`; an unknown name → `SND-CLI-002` (`--repo must be one of: <names>`).
  - `reflectKey(ctx, repo: string, pr: number): string` → `pr-<n>` for the toolkit, `<repo>:pr-<n>` otherwise; the proposal source is `reflect:<key>` and the audit marker detail is `<key>`.
  - `reflectedBefore(ctx, repo, pr)` (new `repo` parameter).
  - `branchTranscript(lines: readonly SessionLine[], repo: string, branch: string, o)` filters `l.repo === repo && l.branch === branch`.
  - weekly: for each profile repo, list merged PRs (same `gh pr list` call with that repo's `ghRepoOf` and default branch) and reflect on the unreflected ones, sharing one budget; a repo whose listing fails is reported and skipped, the rest continue.

- [ ] **Step 1: Write the failing tests** (in the existing reflect-cmd and weekly test files, using their `fakeProc` `gh` stubs)

```ts
it("reflects on another repo's PR with its own gh remote and a repo-qualified key", async () => {
  // profile with extraRepos: [{ name: "globex-app", private: true }]; gh stubs answer for that repo's path
  const r = await reflectCommand(["--repo", "globex-app", "--pr", "7", "--json"], fx.ctx);
  expect(r.exitCode).toBe(0);
  expect(fx.proc.calls.some((c) => c.cwd === fx.extra["globex-app"] && c.argv.includes("view"))).toBe(true);
  expect(listProposals(fx.ctx.db).map((p) => p.source)).toEqual(["reflect:globex-app:pr-7"]);
  expect(reflectedBefore(fx.ctx, "globex-app", 7).reflected).toBe(true);
  expect(reflectedBefore(fx.ctx, fx.ctx.loaded.profile.tracker.repo, 7).reflected).toBe(false);
});

it("keeps toolkit keys as reflect:pr-<n>, so earlier reflections still count", async () => {
  await reflectCommand(["--pr", "81", "--json"], fx.ctx);
  expect(listProposals(fx.ctx.db).every((p) => p.source === "reflect:pr-81")).toBe(true);
});

it("refuses an unknown repo name", async () => {
  const r = await reflectCommand(["--repo", "nope", "--pr", "1"], fx.ctx);
  expect(r.exitCode).toBe(2);
  expect(r.stderr).toContain("--repo must be one of");
});

it("weekly reflects on merged PRs in every profile repo and keeps going past one repo's gh failure", async () => {
  // gh pr list answers [#7] for globex-app, fails for the toolkit
  const r = await weekly([], fx.ctx);
  expect(r.stdout).toContain("reflected on 1 merged PR(s)");
  expect(r.stdout).toContain(`couldn't list merged PRs for ${fx.ctx.loaded.profile.tracker.repo}`);
});

it("branchTranscript uses only lines of the given repo and branch", () => {
  const lines = [line({ repo: "globex-app", branch: "fix/rota", text: "keep" }), line({ repo: "toolkit", branch: "fix/rota", text: "drop" }), line({ repo: "globex-app", branch: "main", text: "drop" })];
  expect(branchTranscript(lines, "globex-app", "fix/rota", { cap: 10_000, since: new Date(0), dropTitles: [] })).toContain("keep");
  expect(branchTranscript(lines, "globex-app", "fix/rota", { cap: 10_000, since: new Date(0), dropTitles: [] })).not.toContain("drop");
});
```

Use the `extraRepos` fixture option from B1 Task 10. Match the weekly output strings to what `weekly.ts` prints today (adjust the expected text to the real sentence, keeping the assertion that the toolkit failure is named and the other repo was reflected).

- [ ] **Step 2: Run to verify it fails**

Run: `cd sindri && npx vitest run tests/evolve-reflect-cmd.test.ts tests/evolve-weekly.test.ts tests/evolve-reflect.test.ts`
Expected: FAIL.

- [ ] **Step 3: Implement**

In `cmd/reflect.ts`: parse `--repo`; resolve `cfg = ctx.loaded.repos[name]`; use `cfg.path` for `ghRepoOf`, `allowedAuthors` and `prContext` (they take a repo path); build the transcript with `branchTranscript(readSessions(transcriptSources(ctx), await repoRoots(ctx), since).lines, name, view.branch, {...})`; key everything with `reflectKey`. `reflectedBefore(ctx, repo, pr)` queries `source = 'reflect:' || key` and the audit detail `key`.

In `cmd/weekly.ts`: `mergedUnreflected` becomes per repo, returning `{ repo, todo: number[], skipped: number }[]` plus failures; `reflectStep` iterates repos then PRs, calling `reflectCommand(["--repo", repo, "--pr", String(n)], ctx, budget)`. The dry-run plan text lists PRs as `<repo>#<n>` for non-toolkit repos.

- [ ] **Step 4: Run to verify it passes**

Run: `cd sindri && npx vitest run tests/evolve-reflect-cmd.test.ts tests/evolve-weekly.test.ts tests/evolve-reflect.test.ts tests/evolve-commands.test.ts`
Expected: PASS.

- [ ] **Step 5: Full check, then commit**

Run (alone): `cd sindri && npm run typecheck && npm test`

```bash
git add sindri/src/evolve sindri/tests
git commit -m "feat: sindri evolve reflects on merged PRs in every profile repo"
```

---

### Task 4: Public provenance labels

**Files:**
- Modify: `sindri/src/evolve/render.ts` (`renderTask`), `sindri/src/evolve/proposals.ts` (`publicEvidence`)
- Test: `sindri/tests/evolve-render.test.ts`

**Interfaces:**
- Produces:
  - `publicSource(source: string): string` — `reflect:pr-<n>` and every non-reflect source unchanged; `reflect:<repo>:pr-<n>` → `reflect on another repo's PR`.
  - `publicEvidence(refs: readonly string[], source: string): { refs: string[]; withheld: number }` — `reduceEvidence`, then for a `reflect:<repo>:pr-<n>` source every `pr:` ref moves to `withheld`.
  - `renderTask` uses both (the "from …" line and the Evidence line).

- [ ] **Step 1: Write the failing tests**

```ts
describe("public provenance (privacy B2)", () => {
  it("never names another repo or its PR numbers", () => {
    const md = renderTask(1, "01J", { ...P, evidence: ["pr:17", "transcript:codex:11111111#2"] }, "code", "why", "reflect:globex-app:pr-17");
    expect(md).toContain("from reflect on another repo's PR");
    expect(md).not.toMatch(/globex|pr:17|#17/i);
    expect(md).toContain("transcript:codex:11111111#2");
    expect(md).toContain("(1 reference(s) withheld)");
  });
  it("keeps toolkit PR refs and sources as they are", () => {
    const md = renderTask(1, "01J", { ...P, evidence: ["pr:81"] }, "code", "why", "reflect:pr-81");
    expect(md).toContain("pr:81");
    expect(md).toContain("from reflect:pr-81");
  });
});
```

(`P` is the file's existing proposal fixture.)

- [ ] **Step 2: Run to verify it fails**

Run: `cd sindri && npx vitest run tests/evolve-render.test.ts`
Expected: FAIL.

- [ ] **Step 3: Implement**

```ts
// proposals.ts
const OTHER_REPO_REFLECT = /^reflect:[^:]+:pr-\d+$/;
export const publicSource = (source: string): string => (OTHER_REPO_REFLECT.test(source) ? "reflect on another repo's PR" : source);
export function publicEvidence(refs: readonly string[], source: string): { refs: string[]; withheld: number } {
  const r = reduceEvidence(refs);
  if (!OTHER_REPO_REFLECT.test(source)) return r;
  const kept = r.refs.filter((x) => !x.startsWith("pr:"));
  return { refs: kept, withheld: r.withheld + (r.refs.length - kept.length) };
}
```

In `renderTask`, replace `reduceEvidence(p.evidence)` with `publicEvidence(p.evidence, source)` and `oneLine(source, 60)` with `oneLine(publicSource(source), 60)`.

- [ ] **Step 4: Run to verify it passes**

Run: `cd sindri && npx vitest run tests/evolve-render.test.ts tests/evolve-publish.test.ts tests/evolve-stage.test.ts`
Expected: PASS.

- [ ] **Step 5: Full check, then commit**

Run (alone): `cd sindri && npm run typecheck && npm test`

```bash
git add sindri/src/evolve/render.ts sindri/src/evolve/proposals.ts sindri/tests/evolve-render.test.ts
git commit -m "feat: sindri published tasks never name another repo or its PRs"
```

---

### Task 5: End to end across providers, docs

**Files:**
- Modify: `sindri/tests/evolve-privacy-e2e.test.ts` (B1 Task 10)
- Modify: `docs/sindri/evolve.md`
- Modify: `AGENTS.md` only if its Sindri command list mentions `reflect --pr` (add `[--repo <name>]`)

- [ ] **Step 1: Extend the leak test**

Add a case where the synthetic workplace's story arrives through every provider at once: a Claude session whose cwd is a git worktree of `globex-app`, a Codex rollout with `cwd` = the `globex-app` path, and a Cursor transcript under the project name encoding of that path, each containing `STORY`. Save one proposal per provider with evidence pointing at that provider's ref, and a fourth sourced `reflect:globex-app:pr-7` with evidence `["pr:7", <claude ref>]`. Use the clean rewrite title. Assert:
- every proposal is published or held, and the plan file contains none of `SECRETS`, `globex-app`, `pr:7` or `#7`;
- `generalize` received each provider's evidence text (inspect the scripted runner's recorded inputs: each contains `STORY` fenced under that provider's ref).

Point the fixture profile's `sources.transcripts.codex.dir` and `.cursor.dir` at temp dirs through `extraYaml`.

- [ ] **Step 2: Run to verify it passes**

Run: `cd sindri && npx vitest run tests/evolve-privacy-e2e.test.ts`
Expected: PASS. A failure is a product bug: fix it in the owning file with a unit test there.

- [ ] **Step 3: Docs**

`docs/sindri/evolve.md`: state that evolve reads every session from Claude Code, Codex and Cursor in any repo (no longer only the toolkit's), document `sources.transcripts.codex.dir`/`.cursor.dir`, `reflect --repo`, the weekly job across repos, the provenance labels, and the three accepted limits from Global Constraints. Remove the "Known limitation: linked-worktree sessions aren't read" line if the doc carries it.

- [ ] **Step 4: Full check, then commit**

Run (alone): `cd sindri && npm run typecheck && npm run test:coverage`

```bash
git add sindri/tests/evolve-privacy-e2e.test.ts docs/sindri/evolve.md AGENTS.md
git commit -m "test: sindri privacy end to end across Claude, Codex and Cursor sessions"
```

---

### Task 6: Merge gate and PR

Same as B1 Task 11: the merge gate commands one at a time, the privacy grep on `git diff origin/main` (no output), `git push -u fork HEAD` as its own command, then `gh pr create --repo joi-fairshare/agentic-workflow --head joithor:<branch> --base main --title "feat: Sindri evolve privacy B2 — learn from every session"` with a body covering the providers, attribution, reflect across repos, provenance labels, accepted limits, test evidence, and `🤖 Generated with [Claude Code](https://claude.com/claude-code)`. Never self-merge.
