# Judge calibration (Track A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `judge` measurable and cheaper. Store what every decision saw, label decisions automatically (from what happened next, and from a stronger model's majority vote), re-score providers offline against those labels, and stop paying for the slow CLI fallback when Jev is merely unsure.

**Architecture:** Three new tables in the existing `decisions.sqlite`:
- `decision_details`: the redacted input, Jev's probabilities, and whether the rules agreed
- `eval_items`: inputs worth labeling
- `labels`: one label per item per source: `outcome` (derived from what happened next), `adjudicator` (Opus, 2-of-3 majority) or `override` (an optional manual correction, never required)

The Jev provider is split: `jev-api.ts` (one request, many typed questions: choice, noul, score) is the base layer; `jev.ts` stays the single-question provider adapter on top of it. Two new CLI groups, `judge label …` (automatic labelers) and `judge eval …`, run the eval offline and never write to `decisions`. `evaluate()` learns an "undecided" state: a below-threshold model answer is no longer a failure, and a question's `fallbackRules` can settle it before the slow CLI runs.

**Tech Stack:** Node ≥ 20, TypeScript 5.7 strict (ESM, Node16 resolution), Zod 3, better-sqlite3 (WAL), Vitest 2 with v8 coverage at 100%.

**Spec:** `docs/superpowers/specs/2026-09-26-cheap-agent-harness-design.md` (Foundation A `judge`; "Visibility and tuning"). Background and evidence: the 2026-10-04 Navigator comparison. Navigator's `hooks/nav_hook_lib/judge.py`, `scripts/judge_label.py`, `scripts/judge_eval.py` and `.agent/tasks/TASK-80-judge-phase2-plan.md` are the inspiration. It's cloned at `$SCRATCH/navigator` if still present; otherwise `https://github.com/qf-studio/navigator`.

### Why (data from `~/.agentic-workflow/judge/decisions.sqlite`, 2026-09-27 → 2026-10-04)

- About 1,750 decisions and **0 undos**. Undo-rate was meant to be the accuracy signal, but nobody uses it, so accuracy is unknown.
- `judge health` shows "degraded (21 failures)". Nearly all of those are `jev` + `below-threshold-provider`: Jev answered, just below the bar. That's the normal "unsure" state, not a failure.
- claude-cli always reports confidence `1` (`toDecided()` in `providers/cli-common.ts` hard-codes it) at about 6s. Jev reports 0.82–0.91 at about 380ms. About 440 decisions paid the 15× latency after Jev was unsure.
- `jev.ts` sends `criteria: { resolved: "resolved", … }`. Jev picks between bare labels with no description of what each one means.
- resolution-check: Jev cleared 0.8 only 3 of 32 times.

## Global Constraints

- Node ≥ 20; TypeScript strict; ESM with `.js` extensions in every relative import (`planning/CODE_STYLE.md`).
- Files are kebab-case. Zod schemas are PascalCase with a `Schema` suffix.
- Vitest `globals: false`. Coverage 100% lines, branches, functions and statements (`judge/vitest.config.ts`). Only `src/cli.ts` is excluded. `/* v8 ignore */` is prohibited: write the test.
- No `any` outside the existing `QUESTIONS` registry line in `commands.ts`.
- SQLite: WAL, `busy_timeout=2000`. Schema changes are additive `CREATE TABLE IF NOT EXISTS` only: **never ALTER or rewrite `decisions`**. `scorer` reads this db and `/bugFixOrchestrator` compares `input_digest`.
- **`input_digest` must stay byte-identical**: `sha256(JSON.stringify(parsedInput)).slice(0,16)`. `skills/bugFixOrchestrator/src/commands.ts` rejects a verdict whose digest differs.
- The judge CLI contract (stdout envelope, exit codes 0/1/2, `ask-check` inverted codes) is unchanged.
- Fails open everywhere. A storage error in any new table never changes a decision's outcome.
- Stored inputs are redacted (`redactSecrets`) and capped (`INPUT_CAP = 16000` chars) before they touch disk. They live only under `~/.agentic-workflow/judge/` and are never committed. Retention is 30 days for `decision_details`; `eval_items` and `labels` keep until deleted.
- **No human labeling.** Joi does not label data (2026-10-04). Labels come from (a) outcome labelers that read what happened after a decision, and (b) `judge adjudicate`, which uses a stronger model than any provider in the chain (Opus via `claude -p`) and keeps only 2-of-3 majorities. `judge label set` exists only as an optional override.
- Labels must be independent of the decision being scored: the adjudicator never sees the provider's decision or confidence, and outcome labelers use only which action was taken (to observe its consequences), never the confidence or provider.
- Every eval report shows results per label source and their agreement rate wherever both exist. One source alone is never treated as ground truth when the other disagrees with it on more than 30% of shared items.
- Run at most ONE heavy job at a time (`npm install`, `npm test`, `tsc`), including your own verification runs. Run the full suite once per commit, not per edit.
- **Which `judge` binary.** `~/.local/bin/judge` is a wrapper around the *main checkout's* `judge/dist/cli.js`, which predates this plan (no `label`, `eval`, `adjudicate`) and must neither be rebuilt nor rewritten overnight. Wherever any plan (A, B, C, D) says to run a bare `judge <cmd>` in an eval, label or smoke step, run the worktree build instead: `cd judge && npm run build`, then `node "$(git rev-parse --show-toplevel)/judge/dist/cli.js" <cmd>` (rebuild after each code change; one heavy job at a time). Hook scripts and their bash tests still call plain `judge` (tests put a fake one on PATH).
- **Long Opus runs.** `judge adjudicate` makes 3 serial `claude -p` Opus calls per item (up to ~180 per question at `--limit 60`, minutes to over an hour), more than one Bash call's 10-minute cap. Run it with `run_in_background` and poll its output; labels are written per item, so an interrupted run loses nothing and a re-run continues where it stopped. Same for B's `--limit 120`.
- **No stored inputs exist yet (checked 2026-10-04).** The live `decisions.sqlite` has `decisions`, `failures` and `briefs` only: no `decision_details`, so there is nothing for `judge label import` to import, and the live judge and hooks are not updated overnight (installs are deferred). Any step below that needs imported items (Task 4 Step 7, Task 5 Step 6) must first run `judge label import`; if it prints `{"imported":0}`, record "deferred: no stored inputs until the new judge and hooks are installed and have run for a few days" in the PR description and the digest, skip the label/adjudicate/eval commands in that step (they would only exit 1 with "no labeled items"), and continue. Joi runs them later.
- Commit format: `type: short description`. End every commit with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## Review Focus

1. **RF-1: Secrets in judge inputs.** Brief text, diffs and transcripts can contain API keys or tokens. `redactSecrets` must strip every Navigator pattern (`sk-…`, `ghp_/gho_/ghu_/ghs_/ghr_…`, `AKIA…`, `xox…`, JWT shape, bare 40+ hex) before `input_json` is written (Task 1).
2. **RF-2: An old db without the new tables.** `scorer` and older `judge` builds open the same file. Scorer's judge section must still render when `decision_details` doesn't exist (Task 7), and `openDb` must create the tables on an existing db without touching `decisions` rows (Task 1).
3. **RF-3: The digest must not drift.** Storing the redacted input must not change what `input_digest` hashes (the parsed, *unredacted* input). Pinned by a test that compares the digest before and after Task 1 (Task 1).
4. **RF-4: An adjudicator that is unsure or off-enum.** A 2-1-0 split, an answer outside the question's outputs, or an unparseable reply stores no label. `judge label set <id> maybe` is rejected with the valid options listed (Task 3).
5. **RF-5: Jev returns an answer for some batched questions and not others.** `callJev` must return the answers it got. A missing key is reported per question as `unparseable-result`, not as a whole-call error (Task 2).

---

### Task 1: Store redacted inputs; add eval tables

**Files:**
- Create: `judge/src/redact.ts`
- Modify: `judge/src/db.ts` (MIGRATIONS + new row types/functions)
- Modify: `judge/src/evaluate.ts` (write details after each decision row)
- Modify: `judge/src/cli.ts` (prune details older than 30 days on startup)
- Test: `judge/tests/redact.test.ts`, `judge/tests/db.test.ts`, `judge/tests/evaluate.test.ts`

**Interfaces:**
- Produces:
  - `redactSecrets(text: string): string`
  - `capJson(value: unknown, maxChars: number): string`
  - `INPUT_CAP = 16000`
  - `interface DecisionDetailsRow { id: string; input_json: string; probabilities: Record<string, number> | null; rules_opinion: string | null; agreement: "agreed" | "overrode" | "undecided" | null; session_id?: string | null }`. `session_id` comes from the `AW_SESSION_ID` env var the hooks set, and lets outcome labelers find what happened next.
  - `recordDecisionDetails(db: Db, row: DecisionDetailsRow): void`
  - `getDecisionDetails(db: Db, id: string): DecisionDetailsRow | undefined`
  - `pruneDecisionDetails(db: Db, beforeIso: string): number` (joins `decisions.ts`)
  - `interface EvalItemRow { id: string; question: string; input_json: string; source: string; model_decision: string | null; created_at: string }`
  - `upsertEvalItem(db: Db, row: EvalItemRow): boolean` (true if inserted; `source` is unique)
  - `type LabelSource = "outcome" | "adjudicator" | "override"`
  - `recordLabel(db: Db, itemId: string, label: string, at: string, source: LabelSource = "override"): void` (one row per item and source)
  - `nextUnlabeled(db: Db, question?: string, source?: LabelSource): EvalItemRow | undefined` (oldest item with no label from that source; any source when omitted)
  - `labeledItems(db: Db, question: string, source: LabelSource | "any" = "any"): Array<EvalItemRow & { label: string; label_source: LabelSource }>` excludes `skip`. For `"any"`, precedence is override > outcome > adjudicator.
  - `labelAgreement(db: Db, question: string): { shared: number; agreed: number }` compares outcome and adjudicator labels on items that have both.
  - `labelCounts(db: Db): Array<{ question: string; items: number; labeled: number; skipped: number }>`

- [ ] **Step 1: Write the failing redact tests**

```ts
// judge/tests/redact.test.ts
import { describe, expect, it } from "vitest";

import { capJson, redactSecrets } from "../src/redact.js";

describe("redactSecrets (RF-1)", () => {
  it.each([
    ["sk-ant-api03-abcdefghijklmnopqrstuvwxyz012345"],
    ["ghp_abcdefghijklmnopqrstuvwxyz0123456789"],
    ["gho_abcdefghijklmnopqrstuvwxyz0123456789"],
    ["AKIAABCDEFGHIJKLMNOP"],
    ["xoxb-1234567890-abcdefghij"],
    ["eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.abcdefghijklmnop"],
    ["0123456789abcdef0123456789abcdef01234567"],
  ])("replaces %s with [REDACTED]", (secret) => {
    expect(redactSecrets(`key=${secret} rest`)).toBe("key=[REDACTED] rest");
  });

  it("leaves ordinary text and short hex (commit shas in prose) alone", () => {
    expect(redactSecrets("fix in 855d6f8, see chain.ts")).toBe("fix in 855d6f8, see chain.ts");
  });
});

describe("capJson", () => {
  it("serializes under the cap unchanged", () => {
    expect(capJson({ a: 1 }, 100)).toBe('{"a":1}');
  });
  it("truncates over the cap with a visible marker", () => {
    const out = capJson({ text: "x".repeat(50) }, 20);
    expect(out.startsWith('{"text":"xxxxxxxxxx')).toBe(true);
    expect(out).toMatch(/\[truncated \d+ chars\]$/);
  });
});
```

- [ ] **Step 2: Run, verify it fails**

Run: `cd judge && npx vitest run tests/redact.test.ts`
Expected: FAIL, cannot find module `../src/redact.js`.

- [ ] **Step 3: Implement `redact.ts`**

```ts
// judge/src/redact.ts
// Stored judge inputs (decision_details, eval_items) can carry brief text,
// diffs and transcript slices. Patterns follow Navigator's judge.redact_secrets
// (hooks/nav_hook_lib/judge.py); the 40+ hex rule only matches standalone runs,
// so short commit shas in prose survive.
const PATTERNS: readonly RegExp[] = [
  /\bsk-[A-Za-z0-9_-]{20,}/g,
  /\bgh[pousr]_[A-Za-z0-9]{30,}/g,
  /\bAKIA[A-Z0-9]{16}\b/g,
  /\bxox[abprs]-[A-Za-z0-9-]{10,}/g,
  /\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}/g,
  /\b[0-9a-f]{40,}\b/gi,
];

export const INPUT_CAP = 16000;

export function redactSecrets(text: string): string {
  return PATTERNS.reduce((acc, re) => acc.replace(re, "[REDACTED]"), text);
}

export function capJson(value: unknown, maxChars: number): string {
  const text = JSON.stringify(value);
  if (text.length <= maxChars) return text;
  return `${text.slice(0, maxChars)}[truncated ${text.length - maxChars} chars]`;
}
```

- [ ] **Step 4: Run, verify pass.** Run: `cd judge && npx vitest run tests/redact.test.ts`. Expected: PASS.

- [ ] **Step 5: Write the failing db tests** (append to `judge/tests/db.test.ts`)

```ts
import { getDecisionDetails, labelAgreement, labelCounts, labeledItems, nextUnlabeled, pruneDecisionDetails, recordDecisionDetails, recordLabel, upsertEvalItem } from "../src/db.js";

describe("decision_details + eval tables", () => {
  const decision = (id: string, ts: string) => ({
    id, ts, question: "wake-gate", content_class: "message-meta", provider: "jev", decision: "send", confidence: 0.9,
    reason_code: "jev", latency_ms: 300, input_digest: "d", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" as const,
  });

  it("creates the new tables on an existing db without touching decisions rows (RF-2)", () => {
    const file = tmpDb();
    const first = openDb(file);
    recordDecision(first, decision("a", "2026-10-01T00:00:00.000Z"));
    first.close();
    const reopened = openDb(file);
    expect(getDecision(reopened, "a")?.decision).toBe("send");
    recordDecisionDetails(reopened, { id: "a", input_json: "{}", probabilities: { send: 0.9, batch: 0.1 }, rules_opinion: null, agreement: null });
    expect(getDecisionDetails(reopened, "a")).toEqual({ id: "a", input_json: "{}", probabilities: { send: 0.9, batch: 0.1 }, rules_opinion: null, agreement: null, session_id: null });
  });

  it("prunes details of decisions older than the cutoff", () => {
    const db = openDb(":memory:");
    recordDecision(db, decision("old", "2026-08-01T00:00:00.000Z"));
    recordDecision(db, decision("new", "2026-10-01T00:00:00.000Z"));
    for (const id of ["old", "new"]) recordDecisionDetails(db, { id, input_json: "{}", probabilities: null, rules_opinion: null, agreement: null });
    expect(pruneDecisionDetails(db, "2026-09-01T00:00:00.000Z")).toBe(1);
    expect(getDecisionDetails(db, "old")).toBeUndefined();
    expect(getDecisionDetails(db, "new")).toBeDefined();
  });

  it("upserts eval items once per source, serves the oldest unlabeled, and lists labeled ones without skips", () => {
    const db = openDb(":memory:");
    const item = (id: string, source: string, created_at: string) => ({ id, question: "wake-gate", input_json: "{}", source, model_decision: "send", created_at });
    expect(upsertEvalItem(db, item("i1", "decision:a", "2026-10-01T00:00:00.000Z"))).toBe(true);
    expect(upsertEvalItem(db, item("i1b", "decision:a", "2026-10-02T00:00:00.000Z"))).toBe(false);
    upsertEvalItem(db, item("i2", "decision:b", "2026-10-02T00:00:00.000Z"));
    upsertEvalItem(db, item("i3", "decision:c", "2026-10-03T00:00:00.000Z"));
    expect(nextUnlabeled(db, "wake-gate")?.id).toBe("i1");
    recordLabel(db, "i1", "batch", "2026-10-04T00:00:00.000Z");
    recordLabel(db, "i2", "skip", "2026-10-04T00:00:00.000Z");
    expect(nextUnlabeled(db)?.id).toBe("i3");
    expect(labeledItems(db, "wake-gate").map((r) => [r.id, r.label])).toEqual([["i1", "batch"]]);
    expect(labelCounts(db)).toEqual([{ question: "wake-gate", items: 3, labeled: 1, skipped: 1 }]);
  });

  it("keeps one label per source, applies override > outcome > adjudicator precedence, and measures agreement", () => {
    const db = openDb(":memory:");
    for (const id of ["a", "b"]) upsertEvalItem(db, { id, question: "ask-check", input_json: "{}", source: `decision:${id}`, model_decision: "continue", created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(db, "a", "ask", "t", "adjudicator");
    recordLabel(db, "a", "continue", "t", "outcome");
    recordLabel(db, "b", "ask", "t", "adjudicator");
    recordLabel(db, "b", "ask", "t", "outcome");
    expect(labeledItems(db, "ask-check").map((r) => [r.id, r.label, r.label_source])).toEqual([["a", "continue", "outcome"], ["b", "ask", "outcome"]]);
    expect(labeledItems(db, "ask-check", "adjudicator").map((r) => r.label)).toEqual(["ask", "ask"]);
    recordLabel(db, "a", "ask", "t", "override");
    expect(labeledItems(db, "ask-check")[0]).toMatchObject({ label: "ask", label_source: "override" });
    expect(labelAgreement(db, "ask-check")).toEqual({ shared: 2, agreed: 1 });
    expect(nextUnlabeled(db, "ask-check", "adjudicator")).toBeUndefined();
  });
});
```

- [ ] **Step 6: Run, verify it fails.** Run: `cd judge && npx vitest run tests/db.test.ts`. Expected: FAIL, the new functions aren't exported.

- [ ] **Step 7: Implement in `db.ts`.** Append to `MIGRATIONS` and add the functions:

```ts
// appended inside the MIGRATIONS template string
CREATE TABLE IF NOT EXISTS decision_details (
  id TEXT PRIMARY KEY,
  input_json TEXT NOT NULL,
  probabilities TEXT,
  rules_opinion TEXT,
  agreement TEXT,
  session_id TEXT
);
CREATE TABLE IF NOT EXISTS eval_items (
  id TEXT PRIMARY KEY,
  question TEXT NOT NULL,
  input_json TEXT NOT NULL,
  source TEXT NOT NULL UNIQUE,
  model_decision TEXT,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS eval_items_question ON eval_items(question, created_at);
CREATE TABLE IF NOT EXISTS labels (
  item_id TEXT NOT NULL,
  source TEXT NOT NULL,
  label TEXT NOT NULL,
  labeled_at TEXT NOT NULL,
  PRIMARY KEY (item_id, source)
);
```

```ts
export type Agreement = "agreed" | "overrode" | "undecided";

export interface DecisionDetailsRow {
  id: string;
  input_json: string;
  probabilities: Record<string, number> | null;
  rules_opinion: string | null;
  agreement: Agreement | null;
  session_id?: string | null;
}

export function recordDecisionDetails(db: Db, row: DecisionDetailsRow): void {
  db.prepare(
    `INSERT OR REPLACE INTO decision_details (id, input_json, probabilities, rules_opinion, agreement, session_id)
     VALUES (@id, @input_json, @probabilities, @rules_opinion, @agreement, @session_id)`,
  ).run({ ...row, session_id: row.session_id ?? null, probabilities: row.probabilities === null ? null : JSON.stringify(row.probabilities) });
}

export function getDecisionDetails(db: Db, id: string): DecisionDetailsRow | undefined {
  const row = db.prepare("SELECT * FROM decision_details WHERE id = ?").get(id) as (Omit<DecisionDetailsRow, "probabilities"> & { probabilities: string | null }) | undefined;
  if (row === undefined) return undefined;
  return { ...row, probabilities: row.probabilities === null ? null : (JSON.parse(row.probabilities) as Record<string, number>) };
}

export function pruneDecisionDetails(db: Db, beforeIso: string): number {
  return db.prepare("DELETE FROM decision_details WHERE id IN (SELECT id FROM decisions WHERE ts < ?)").run(beforeIso).changes;
}

export interface EvalItemRow {
  id: string;
  question: string;
  input_json: string;
  source: string;
  model_decision: string | null;
  created_at: string;
}

export function upsertEvalItem(db: Db, row: EvalItemRow): boolean {
  return db.prepare(
    `INSERT OR IGNORE INTO eval_items (id, question, input_json, source, model_decision, created_at)
     VALUES (@id, @question, @input_json, @source, @model_decision, @created_at)`,
  ).run(row).changes === 1;
}

export type LabelSource = "outcome" | "adjudicator" | "override";
const PRECEDENCE: Record<LabelSource, number> = { override: 0, outcome: 1, adjudicator: 2 };

export function recordLabel(db: Db, itemId: string, label: string, at: string, source: LabelSource = "override"): void {
  db.prepare("INSERT OR REPLACE INTO labels (item_id, source, label, labeled_at) VALUES (?, ?, ?, ?)").run(itemId, source, label, at);
}

export function nextUnlabeled(db: Db, question?: string, source?: LabelSource): EvalItemRow | undefined {
  const params: string[] = [];
  const join = source === undefined ? "LEFT JOIN labels l ON l.item_id = e.id" : (params.push(source), "LEFT JOIN labels l ON l.item_id = e.id AND l.source = ?");
  if (question !== undefined) params.push(question);
  return db.prepare(
    `SELECT e.* FROM eval_items e ${join} WHERE l.item_id IS NULL ${question === undefined ? "" : "AND e.question = ?"}
     ORDER BY e.created_at ASC LIMIT 1`,
  ).get(...params) as EvalItemRow | undefined;
}

export function labeledItems(db: Db, question: string, source: LabelSource | "any" = "any"): Array<EvalItemRow & { label: string; label_source: LabelSource }> {
  const rows = db.prepare(
    `SELECT e.*, l.label, l.source AS label_source FROM eval_items e JOIN labels l ON l.item_id = e.id
     WHERE e.question = ? AND l.label != 'skip' ${source === "any" ? "" : "AND l.source = ?"} ORDER BY e.created_at ASC, e.id ASC`,
  ).all(...(source === "any" ? [question] : [question, source])) as Array<EvalItemRow & { label: string; label_source: LabelSource }>;
  const best = new Map<string, (typeof rows)[number]>();
  for (const r of rows) {
    const cur = best.get(r.id);
    if (cur === undefined || PRECEDENCE[r.label_source] < PRECEDENCE[cur.label_source]) best.set(r.id, r);
  }
  return [...best.values()];
}

export function labelAgreement(db: Db, question: string): { shared: number; agreed: number } {
  const row = db.prepare(
    `SELECT COUNT(*) AS shared, SUM(CASE WHEN o.label = a.label THEN 1 ELSE 0 END) AS agreed
     FROM eval_items e JOIN labels o ON o.item_id = e.id AND o.source = 'outcome' AND o.label != 'skip'
     JOIN labels a ON a.item_id = e.id AND a.source = 'adjudicator' AND a.label != 'skip' WHERE e.question = ?`,
  ).get(question) as { shared: number; agreed: number | null };
  return { shared: row.shared, agreed: row.agreed ?? 0 };
}

export function labelCounts(db: Db): Array<{ question: string; items: number; labeled: number; skipped: number }> {
  return db.prepare(
    `SELECT e.question AS question, COUNT(*) AS items,
            SUM(CASE WHEN EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label != 'skip') THEN 1 ELSE 0 END) AS labeled,
            SUM(CASE WHEN NOT EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label != 'skip')
                      AND EXISTS (SELECT 1 FROM labels l WHERE l.item_id = e.id AND l.label = 'skip') THEN 1 ELSE 0 END) AS skipped
     FROM eval_items e GROUP BY e.question ORDER BY e.question`,
  ).all() as Array<{ question: string; items: number; labeled: number; skipped: number }>;
}
```

- [ ] **Step 8: Run, verify pass.** Run: `cd judge && npx vitest run tests/db.test.ts`. Expected: PASS.

- [ ] **Step 9: Write the failing evaluate tests** (append to `judge/tests/evaluate.test.ts`, reusing that file's existing question fixture and `fakeProvider`)

```ts
it("stores the redacted, capped input in decision_details and leaves input_digest on the unredacted input (RF-1, RF-3)", async () => {
  const db = openDb(":memory:");
  const input = { text: "token sk-ant-api03-abcdefghijklmnopqrstuvwxyz012345 here" };
  const out = await evaluate(textQuestion, input, {
    db, config: DEFAULT_CONFIG, providers: [fakeProvider("jev", ["message-meta"], { status: "decided", decision: "send", confidence: 0.9, reason_code: "jev" })],
    chain: { classes: { "message-meta": ["jev"] } }, randomId: () => "id1",
  });
  expect("escalate" in out).toBe(false);
  const details = getDecisionDetails(db, "id1");
  expect(details?.input_json).toBe('{"text":"token [REDACTED] here"}');
  const expectedDigest = crypto.createHash("sha256").update(JSON.stringify(input)).digest("hex").slice(0, 16);
  expect(getDecision(db, "id1")?.input_digest).toBe(expectedDigest);
});

it("still returns the decision when the details write throws", async () => {
  const db = openDb(":memory:");
  db.exec("DROP TABLE decision_details");
  const out = await evaluate(textQuestion, { text: "hi" }, {
    db, config: DEFAULT_CONFIG, providers: [fakeProvider("jev", ["message-meta"], { status: "decided", decision: "send", confidence: 0.9, reason_code: "jev" })],
    chain: { classes: { "message-meta": ["jev"] } }, randomId: () => "id2",
  });
  expect(out).toMatchObject({ decision: "send", id: "id2" });
});
```

(`evaluate.test.ts` has no `textQuestion`; its fixture is the factory `question(overrides)` (name `wake-gate`, class `message-meta`, threshold 0.7, outputs `send|batch|drop`), so read `textQuestion` in these snippets as `question()` and `{ ...textQuestion, x }` as `question({ x })`. The `chain` argument is a `ChainSpec` (`{ classes: {...} }`) and `DEFAULT_CONFIG` has `wake-gate` enabled. Add `import crypto from "node:crypto";` and import `getDecisionDetails` and `getDecision` from `../src/db.js`.)

- [ ] **Step 10: Run, verify fail.** Run: `cd judge && npx vitest run tests/evaluate.test.ts`. Expected: the two new tests FAIL.

- [ ] **Step 11: Implement in `evaluate.ts`.** Add the import and a safe writer, and call it from `recordRow` after `recordDecision` succeeds:

```ts
import { capJson, INPUT_CAP, redactSecrets } from "./redact.js";
import { recordDecisionDetails, type Agreement } from "./db.js";

// Inside evaluate(), next to recordRow:
const recordDetails = (
  digestInput: unknown, probabilities: Record<string, number> | null = null,
  rulesOpinion: string | null = null, agreement: Agreement | null = null,
): void => {
  try {
    recordDecisionDetails(deps.db, {
      id, input_json: redactSecrets(capJson(digestInput, INPUT_CAP)),
      probabilities, rules_opinion: rulesOpinion, agreement, session_id: deps.sessionId ?? null,
    });
  } catch {
    /* details are best-effort: never change the outcome (fails open) */
  }
};
```

Call `recordDetails(digestInput)` as the last statement of the `try` block in `recordRow`. Tasks 2 and 6 extend the call with probabilities and agreement.

Add `sessionId?: string` to `EvaluateDeps`. In `cli.ts`, pass `sessionId: process.env.AW_SESSION_ID` into every `RunDeps`. Add one evaluate test: `sessionId: "s1"` lands in `getDecisionDetails(db, id)?.session_id`.

Set `AW_SESSION_ID` in the hooks that call judge: `done-gate.sh` (ask-check), `scope-gate.sh` (brief-scope) and `send-gate.sh` (wake-gate). Each already parses `session_id` from its hook input; prefix the `judge` invocation with `AW_SESSION_ID="$SESSION_ID"`. Extend each hook's bash test (`config/lib/tests/done-gate.test.sh`, `scope-gate.test.sh`, `send-gate.test.sh`) to assert the fake `judge` saw the variable, then run those three plus `bash config/hooks/tests/codex-adapter.test.sh` and `cursor-adapter.test.sh` once.

- [ ] **Step 12: Prune on CLI startup.** In `judge/src/cli.ts`, after `const db = openDb(dbPath);`:

```ts
try {
  pruneDecisionDetails(db, new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString());
} catch {
  /* retention is best-effort */
}
```

- [ ] **Step 13: Full suite with coverage.** Run: `cd judge && npm run typecheck && npm run test:coverage`. Expected: 0 type errors, all tests pass, 100% coverage.

- [ ] **Step 14: Commit**

```bash
git add judge/src/redact.ts judge/src/db.ts judge/src/evaluate.ts judge/src/cli.ts judge/tests/redact.test.ts judge/tests/db.test.ts judge/tests/evaluate.test.ts config/hooks config/lib/tests
git commit -m "feat: judge stores redacted decision inputs and eval tables"
```

---

### Task 2: Batched Jev API (choice, noul and score questions in one request)

**Files:**
- Create: `judge/src/providers/jev-api.ts`
- Modify: `judge/src/providers/jev.ts` (delegate to `callJev`; surface probabilities)
- Modify: `judge/src/types.ts` (`probabilities?` on the decided `ProviderResult`)
- Modify: `judge/src/evaluate.ts` (pass probabilities into `recordDetails`)
- Test: `judge/tests/providers/jev-api.test.ts`, `judge/tests/providers/jev.test.ts`

**Interfaces:**
- Consumes: `Fetch` (moves from `jev.ts` to `jev-api.ts`; `jev.ts` re-exports it).
- Produces:
  ```ts
  export type JevQuestion =
    | { type: "choice"; instructions: string; criteria: Record<string, string> }
    | { type: "noul"; instructions: string }
    | { type: "score"; instructions: string; criteria: string[] };
  export type JevAnswer =
    | { type: "choice"; choice: string; probabilities: Record<string, number>; confidence: number }
    | { type: "noul"; noul: number }
    | { type: "score"; score: number; probabilities: Record<string, number>; confidence: number };
  export type JevQuestionResult = { ok: true; answer: JevAnswer } | { ok: false; reason_code: "unparseable-result" };
  export type JevCallResult =
    | { status: "ok"; answers: Record<string, JevQuestionResult>; usage: { input_tokens: number; output_tokens: number } }
    | { status: "unavailable" | "error"; reason_code: string };
  export interface JevDeps { fetch: Fetch; apiKey: () => Promise<string | null>; model?: string }
  export const JEV_MODEL = "jev-1.13.0";
  export function callJev(deps: JevDeps, state: unknown, questions: Record<string, JevQuestion>, budgetMs: number): Promise<JevCallResult>;
  ```
  - `ProviderResult` decided variant gains `probabilities?: Record<string, number>`.

API facts (docs.typesafe.ai/api, confirmed 2026-10-04): `POST /v1/systemone`, body `{ state, model, questions: { <id>: Question } }`. `questions` is a map with no documented count limit. Choice takes ≤ 255 options; score takes ≤ 10 levels. Noul answer: `{ type, noul }` (0..1). Choice answer: `{ type, choice, probabilities, confidence }`. Score answer: `{ type, score, legend, probabilities, confidence }`. Response carries `usage: { input_tokens, output_tokens }`.

- [ ] **Step 1: Write failing `jev-api` tests**

```ts
// judge/tests/providers/jev-api.test.ts
import { describe, expect, it, vi } from "vitest";

import { callJev, type JevQuestion } from "../../src/providers/jev-api.js";

const ok = (body: unknown) => vi.fn().mockResolvedValue({ status: 200, json: async () => body });

const questions: Record<string, JevQuestion> = {
  is_task: { type: "noul", instructions: "Is this a task?" },
  tier: { type: "score", instructions: "How big?", criteria: ["trivial", "small", "large"] },
  kind: { type: "choice", instructions: "Which kind?", criteria: { bug: "a defect report", feature: "new behaviour" } },
};

describe("callJev", () => {
  it("sends every question in one request and returns typed answers plus usage", async () => {
    const fetch = ok({
      answers: {
        is_task: { type: "noul", noul: 0.93 },
        tier: { type: "score", score: 1.2, probabilities: { trivial: 0.1, small: 0.6, large: 0.3 }, confidence: 0.6 },
        kind: { type: "choice", choice: "bug", probabilities: { bug: 0.8, feature: 0.2 }, confidence: 0.8 },
      },
      usage: { input_tokens: 120, output_tokens: 9 },
    });
    const out = await callJev({ fetch, apiKey: async () => "k" }, { prompt: "fix it" }, questions, 1000);
    expect(fetch).toHaveBeenCalledTimes(1);
    const body = JSON.parse((fetch.mock.calls[0] as [string, { body: string }])[1].body) as { questions: Record<string, unknown>; model: string };
    expect(Object.keys(body.questions)).toEqual(["is_task", "tier", "kind"]);
    expect(body.model).toBe("jev-1.13.0");
    expect(out).toEqual({
      status: "ok",
      usage: { input_tokens: 120, output_tokens: 9 },
      answers: {
        is_task: { ok: true, answer: { type: "noul", noul: 0.93 } },
        tier: { ok: true, answer: { type: "score", score: 1.2, probabilities: { trivial: 0.1, small: 0.6, large: 0.3 }, confidence: 0.6 } },
        kind: { ok: true, answer: { type: "choice", choice: "bug", probabilities: { bug: 0.8, feature: 0.2 }, confidence: 0.8 } },
      },
    });
  });

  it("reports a missing or malformed answer per question, keeping the rest (RF-5)", async () => {
    const fetch = ok({ answers: { is_task: { type: "noul", noul: 0.2 }, tier: { type: "score" } }, usage: { input_tokens: 1, output_tokens: 1 } });
    const out = await callJev({ fetch, apiKey: async () => "k" }, "s", questions, 1000);
    expect(out.status === "ok" && out.answers).toEqual({
      is_task: { ok: true, answer: { type: "noul", noul: 0.2 } },
      tier: { ok: false, reason_code: "unparseable-result" },
      kind: { ok: false, reason_code: "unparseable-result" },
    });
  });

  it("refuses >255 choice options or >10 score levels before calling fetch", async () => {
    const fetch = vi.fn();
    const many = Object.fromEntries(Array.from({ length: 256 }, (_, i) => [`o${i}`, `o${i}`]));
    expect(await callJev({ fetch, apiKey: async () => "k" }, "s", { q: { type: "choice", instructions: "", criteria: many } }, 1000))
      .toEqual({ status: "error", reason_code: "too-many-options" });
    expect(await callJev({ fetch, apiKey: async () => "k" }, "s", { q: { type: "score", instructions: "", criteria: Array.from({ length: 11 }, (_, i) => `l${i}`) } }, 1000))
      .toEqual({ status: "error", reason_code: "too-many-levels" });
    expect(fetch).not.toHaveBeenCalled();
  });

  it("is unavailable without a key, on timeout, and on network error; an error on non-2xx", async () => {
    expect(await callJev({ fetch: vi.fn(), apiKey: async () => null }, "s", questions, 1000)).toEqual({ status: "unavailable", reason_code: "no-api-key" });
    const abort = vi.fn().mockRejectedValue(new DOMException("aborted", "AbortError"));
    expect(await callJev({ fetch: abort, apiKey: async () => "k" }, "s", questions, 1000)).toEqual({ status: "unavailable", reason_code: "timeout" });
    const net = vi.fn().mockRejectedValue(new Error("ECONNRESET"));
    expect(await callJev({ fetch: net, apiKey: async () => "k" }, "s", questions, 1000)).toEqual({ status: "unavailable", reason_code: "network-error" });
    const http = vi.fn().mockResolvedValue({ status: 503, json: async () => ({}) });
    expect(await callJev({ fetch: http, apiKey: async () => "k" }, "s", questions, 1000)).toEqual({ status: "error", reason_code: "http-503" });
  });

  it("defaults usage to zeros when the response omits it, and honours a model override", async () => {
    const fetch = ok({ answers: { is_task: { type: "noul", noul: 1 } } });
    const out = await callJev({ fetch, apiKey: async () => "k", model: "jev-latest" }, "s", { is_task: questions.is_task as JevQuestion }, 1000);
    expect(out).toMatchObject({ status: "ok", usage: { input_tokens: 0, output_tokens: 0 } });
    expect((JSON.parse((fetch.mock.calls[0] as [string, { body: string }])[1].body) as { model: string }).model).toBe("jev-latest");
  });
});
```

- [ ] **Step 2: Run, verify fail.** Run: `cd judge && npx vitest run tests/providers/jev-api.test.ts`. Expected: FAIL, module not found.

- [ ] **Step 3: Implement `jev-api.ts`**

```ts
// judge/src/providers/jev-api.ts
// One POST /v1/systemone carries any number of typed questions (docs.typesafe.ai/api,
// confirmed 2026-10-04: `questions` is a map, no documented count limit; choice ≤ 255
// options, score ≤ 10 levels). jev.ts adapts this to the single-question Provider
// contract; batched callers (the prompt sorter) use callJev directly.
export interface Fetch {
  (url: string, init: { method: string; headers: Record<string, string>; body: string; signal: AbortSignal }): Promise<{ status: number; json(): Promise<unknown> }>;
}

export const JEV_MODEL = "jev-1.13.0";
const JEV_ENDPOINT = "https://api.typesafe.ai/v1/systemone";
const MAX_CHOICE_OPTIONS = 255;
const MAX_SCORE_LEVELS = 10;

export type JevQuestion =
  | { type: "choice"; instructions: string; criteria: Record<string, string> }
  | { type: "noul"; instructions: string }
  | { type: "score"; instructions: string; criteria: string[] };

export type JevAnswer =
  | { type: "choice"; choice: string; probabilities: Record<string, number>; confidence: number }
  | { type: "noul"; noul: number }
  | { type: "score"; score: number; probabilities: Record<string, number>; confidence: number };

export type JevQuestionResult = { ok: true; answer: JevAnswer } | { ok: false; reason_code: "unparseable-result" };

export type JevCallResult =
  | { status: "ok"; answers: Record<string, JevQuestionResult>; usage: { input_tokens: number; output_tokens: number } }
  | { status: "unavailable" | "error"; reason_code: string };

export interface JevDeps {
  fetch: Fetch;
  apiKey: () => Promise<string | null>;
  model?: string;
}

const isNum = (v: unknown): v is number => typeof v === "number" && Number.isFinite(v);
const isProbs = (v: unknown): v is Record<string, number> =>
  typeof v === "object" && v !== null && Object.values(v).every(isNum);

function parseAnswer(q: JevQuestion, raw: unknown): JevQuestionResult {
  const bad = { ok: false, reason_code: "unparseable-result" } as const;
  if (typeof raw !== "object" || raw === null) return bad;
  const a = raw as Record<string, unknown>;
  if (a.type !== q.type) return bad;
  if (q.type === "noul") return isNum(a.noul) ? { ok: true, answer: { type: "noul", noul: a.noul } } : bad;
  if (q.type === "choice") {
    if (typeof a.choice !== "string" || !(a.choice in q.criteria)) return bad;
    return { ok: true, answer: { type: "choice", choice: a.choice, probabilities: isProbs(a.probabilities) ? a.probabilities : {}, confidence: isNum(a.confidence) ? a.confidence : 1 } };
  }
  if (!isNum(a.score)) return bad;
  return { ok: true, answer: { type: "score", score: a.score, probabilities: isProbs(a.probabilities) ? a.probabilities : {}, confidence: isNum(a.confidence) ? a.confidence : 1 } };
}

export async function callJev(deps: JevDeps, state: unknown, questions: Record<string, JevQuestion>, budgetMs: number): Promise<JevCallResult> {
  for (const q of Object.values(questions)) {
    if (q.type === "choice" && Object.keys(q.criteria).length > MAX_CHOICE_OPTIONS) return { status: "error", reason_code: "too-many-options" };
    if (q.type === "score" && q.criteria.length > MAX_SCORE_LEVELS) return { status: "error", reason_code: "too-many-levels" };
  }
  const apiKey = await deps.apiKey();
  if (apiKey === null) return { status: "unavailable", reason_code: "no-api-key" };

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), budgetMs);
  try {
    const response = await deps.fetch(JEV_ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiKey}` },
      body: JSON.stringify({ state, model: deps.model ?? JEV_MODEL, questions }),
      signal: controller.signal,
    });
    clearTimeout(timer);
    if (response.status < 200 || response.status >= 300) return { status: "error", reason_code: `http-${response.status}` };
    const body = (await response.json()) as { answers?: Record<string, unknown>; usage?: { input_tokens?: unknown; output_tokens?: unknown } };
    const answers: Record<string, JevQuestionResult> = {};
    for (const [id, q] of Object.entries(questions)) answers[id] = parseAnswer(q, body.answers?.[id]);
    const usage = {
      input_tokens: isNum(body.usage?.input_tokens) ? body.usage.input_tokens : 0,
      output_tokens: isNum(body.usage?.output_tokens) ? body.usage.output_tokens : 0,
    };
    return { status: "ok", answers, usage };
  } catch (e) {
    clearTimeout(timer);
    if (e instanceof DOMException && e.name === "AbortError") return { status: "unavailable", reason_code: "timeout" };
    return { status: "unavailable", reason_code: "network-error" };
  }
}
```

- [ ] **Step 4: Run, verify pass.** Run: `cd judge && npx vitest run tests/providers/jev-api.test.ts`. Expected: PASS. Add tests for any branch coverage reports as missed (for example `parseAnswer` on a non-object answer, or a choice outside `criteria`).

- [ ] **Step 5: Rewrite `jev.ts` on top of `callJev`; update its tests.** Keep every existing `jev.test.ts` assertion passing except the criteria one, which Task 5 changes. Add one test: probabilities flow through.

```ts
// judge/src/providers/jev.ts
import type { Provider, ProviderResult, QuestionRef } from "../types.js";
import { callJev, type Fetch } from "./jev-api.js";

export type { Fetch } from "./jev-api.js";

const QUESTION_KEY = "decision";

/** Single-question Provider adapter over callJev (see jev-api.ts for the API notes). */
export function makeJevProvider(deps: { fetch: Fetch; apiKey: () => Promise<string | null> }): Provider {
  return {
    name: "jev",
    classes: new Set(["message-meta", "code", "diff", "brief", "transcript"]),
    decide: async <O extends string>(question: QuestionRef<O>, input: unknown, budgetMs: number): Promise<ProviderResult<O>> => {
      const criteria: Record<string, string> = {};
      for (const option of question.outputs) criteria[option] = option;
      const out = await callJev(deps, input, { [QUESTION_KEY]: { type: "choice", instructions: question.prompt, criteria } }, budgetMs);
      if (out.status !== "ok") return out;
      const result = out.answers[QUESTION_KEY] as NonNullable<(typeof out.answers)[string]>;
      if (!result.ok || result.answer.type !== "choice") return { status: "error", reason_code: "unparseable-result" };
      return { status: "decided", decision: result.answer.choice as O, confidence: result.answer.confidence, reason_code: "jev", probabilities: result.answer.probabilities };
    },
  };
}
```

New test in `jev.test.ts`:

```ts
it("returns Jev's probability distribution with the decision", async () => {
  const fetch = vi.fn().mockResolvedValue({ status: 200, json: async () => ({ answers: { decision: { type: "choice", choice: "send", probabilities: { send: 0.7, batch: 0.2, drop: 0.1 }, confidence: 0.7 } } }) });
  const provider = makeJevProvider({ fetch, apiKey: async () => "k" });
  expect(await provider.decide(question, {}, 1000)).toEqual({ status: "decided", decision: "send", confidence: 0.7, reason_code: "jev", probabilities: { send: 0.7, batch: 0.2, drop: 0.1 } });
});
```

The existing "parses a decided answer" test now expects `probabilities: {}` (no distribution in that fixture). Update it.

- [ ] **Step 6: `types.ts`.** Add `probabilities?: Record<string, number>` to the `status: "decided"` variant of `ProviderResult`.

- [ ] **Step 7: Thread probabilities into details.** In `evaluate.ts`, extend `settle()` with a `probabilities?: Record<string, number>` parameter, pass `result.probabilities` from the provider loop, and have `recordRow` call `recordDetails(digestInput, probabilities ?? null)`. Add a test in `evaluate.test.ts`: a fake jev result with `probabilities: { send: 0.9, batch: 0.1 }` makes `getDecisionDetails(db, id)?.probabilities` equal that map.

- [ ] **Step 8: Full suite.** Run: `cd judge && npm run typecheck && npm run test:coverage`. Expected: PASS, 100%.

- [ ] **Step 9: Commit**

```bash
git add judge/src/providers/jev-api.ts judge/src/providers/jev.ts judge/src/types.ts judge/src/evaluate.ts judge/tests/providers/jev-api.test.ts judge/tests/providers/jev.test.ts judge/tests/evaluate.test.ts
git commit -m "feat: batched Jev API with choice, noul and score questions"
```

---

### Task 3: Automatic labels: ask-check outcomes, an Opus adjudicator, and optional overrides

No human labeling (Joi, 2026-10-04). Two labelers fill `labels`. Each is independent of the decision it scores.

- **Outcome labeler (`ask-check`).** It reads Joi's next real prompt after the decision in the same session's transcript:
  - The decision was `continue` and the next prompt is an interrupt or a correction → label `ask` (it should have stopped).
  - The decision was `ask` and the next prompt is a bare "continue / yes / go ahead" → label `continue` (stopping wasted a turn).
  - Otherwise the action taken was fine → label it with that action.
  - No next prompt within 6h → no label.

  It uses which action was taken (it observes that action's consequences) but never the confidence or the provider.
- **Adjudicator (`judge adjudicate <question>`).** For each item without an adjudicator label, it runs `claude -p --model opus --effort high` three times. The prompt is the question's own prompt, each option's `criteria` description and the stored input. It never sees the provider's decision. It stores a label only when at least 2 of 3 runs agree on an in-enum answer; otherwise it stores nothing (RF-4).

**Files:**
- Create: `judge/src/label.ts` (import, override, status)
- Create: `judge/src/outcomes.ts` (transcript reading + the ask-check outcome rule)
- Create: `judge/src/adjudicate.ts`
- Modify: `judge/src/providers/claude-cli.ts` (`model` / `effort` options; default `haiku` / `low`, so behaviour is unchanged)
- Modify: `judge/src/question.ts` and `judge/src/types.ts`: declare only the optional field `criteria?: Readonly<Record<O, string>>` on `QuestionModule` (and `Readonly<Record<string, string>>` on `QuestionRef`) now, because `adjudicate.ts` reads `q.criteria` and would not typecheck without it. Task 5 populates it and wires `toRef` and `jev.ts`.
- Modify: `judge/src/cli.ts` (route `label` and `adjudicate`)
- Modify: `skills/judge/SKILL.md`
- Test: `judge/tests/label.test.ts`, `judge/tests/outcomes.test.ts`, `judge/tests/adjudicate.test.ts`, `judge/tests/providers/claude-cli.test.ts`, `judge/tests/fixtures/ask-outcome.jsonl` (synthetic lines only)

**Interfaces:**
- Consumes: `upsertEvalItem`, `nextUnlabeled`, `recordLabel`, `labelCounts`, `getDecisionDetails`, `LabelSource` (Task 1); `QUESTIONS` (`commands.ts`); `toRef` (`question.ts`); `makeClaudeCliProvider`, `Spawn`.
- Produces:
  - `runLabelImport(db: Db, opts: { question?: string; sinceIso: string }, now: () => Date): { exitCode: number; stdout: string }` imports decided rows that have details (`source = "decision:<id>"`). It silently skips rows whose `question` is not a key of `QUESTIONS` (a later plan records `prompt-sort` decisions and imports those itself), and rows whose stored input no longer parses. stdout is `{"imported":n}`.
  - `runLabelSet(db: Db, itemId: string, label: string, now: () => Date): { exitCode: number; stdout: string; stderr?: string }` records an `override` label, which must be one of the question's outputs or `skip`.
  - `runLabelStatus(db: Db): { exitCode: number; stdout: string }` prints `labelCounts` plus `labelAgreement` per question.
  - `interface RealPrompt { ts: string; text: string }`
  - `realPrompts(jsonl: string): RealPrompt[]` keeps `type: "user"` lines with a `timestamp`, whose content is a string or `text` blocks, not starting with `<`, and not a `tool_result`.
  - `classifyReply(text: string): "interrupt" | "correction" | "bare-continue" | "other"`
  - `askCheckOutcome(action: "continue" | "ask", decidedAt: string, prompts: readonly RealPrompt[]): "continue" | "ask" | null`
  - `findTranscript(projectsDir: string, sessionId: string): string | undefined` looks for `<projectsDir>/*/<sessionId>.jsonl`.
  - `runOutcomeLabels(db: Db, opts: { projectsDir: string; now: () => Date; readFile?: (p: string) => string }): { labeled: number; noSignal: number }`
  - `adjudicate(db: Db, question: string, opts: { provider: Provider; limit: number; samples?: number; now: () => Date }): Promise<{ labeled: number; split: number; failed: number }>`
  - `makeClaudeCliProvider(deps: { spawn: Spawn; tmpDirFactory: () => string; model?: string; effort?: string })`
- CLI:
  - `judge label import [--question q] [--since 14d]`
  - `judge label outcomes`
  - `judge label set <itemId> <label|skip>` (optional override)
  - `judge label status`
  - `judge adjudicate <question> [--limit 60]`

Reply classification (restating scorer RF-4's intent; judge doesn't import scorer):
- `interrupt`: contains `[Request interrupted by user`
- `correction`: matches `/^\s*(no\b|nope|wait\b|stop\b|don'?t\b|actually\b|that'?s (wrong|not)|not what|undo|revert)/i`
- `bare-continue`: matches `/^\s*(continue|keep going|go on|go ahead|proceed|yes|yep|y|ok|okay|do it|carry on)[\s.!]*$/i`

- [ ] **Step 1: Synthetic fixture** `judge/tests/fixtures/ask-outcome.jsonl`, one JSON object per line, real key shapes only:
  1. `{"type":"user","timestamp":"2026-10-03T10:00:00.000Z","message":{"role":"user","content":"Refactor chain.ts"}}`
  2. `{"type":"user","timestamp":"2026-10-03T10:05:00.000Z","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"t","content":"ok"}]}}`
  3. `{"type":"user","timestamp":"2026-10-03T10:06:00.000Z","message":{"role":"user","content":"<system-reminder>x</system-reminder>"}}`
  4. `{"type":"user","timestamp":"2026-10-03T10:10:00.000Z","message":{"role":"user","content":[{"type":"text","text":"no, don't touch the tests"}]}}`

- [ ] **Step 2: Write failing tests**

```ts
// judge/tests/outcomes.test.ts
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { labeledItems, openDb, recordDecision, recordDecisionDetails, upsertEvalItem } from "../src/db.js";
import { askCheckOutcome, classifyReply, findTranscript, realPrompts, runOutcomeLabels } from "../src/outcomes.js";

const jsonl = fs.readFileSync(path.join(import.meta.dirname, "fixtures", "ask-outcome.jsonl"), "utf8");

describe("realPrompts", () => {
  it("keeps only real user prompts, in order, skipping tool results, machine text and bad lines", () => {
    expect(realPrompts(`garbage\n\n${jsonl}`)).toEqual([
      { ts: "2026-10-03T10:00:00.000Z", text: "Refactor chain.ts" },
      { ts: "2026-10-03T10:10:00.000Z", text: "no, don't touch the tests" },
    ]);
  });
});

describe("classifyReply", () => {
  it.each([
    ["[Request interrupted by user]", "interrupt"],
    ["no, don't touch the tests", "correction"],
    ["Actually use the other file", "correction"],
    ["continue", "bare-continue"],
    ["go ahead!", "bare-continue"],
    ["now add a test for buildChain", "other"],
  ])("%s -> %s", (text, kind) => expect(classifyReply(text)).toBe(kind));
});

describe("askCheckOutcome", () => {
  const p = (ts: string, text: string) => ({ ts, text });
  it("labels a continue followed by a correction as ask", () => {
    expect(askCheckOutcome("continue", "2026-10-03T10:01:00.000Z", [p("2026-10-03T10:00:00.000Z", "x"), p("2026-10-03T10:10:00.000Z", "no, stop")])).toBe("ask");
  });
  it("labels an ask answered with a bare continue as continue", () => {
    expect(askCheckOutcome("ask", "2026-10-03T10:01:00.000Z", [p("2026-10-03T10:02:00.000Z", "yes")])).toBe("continue");
  });
  it("confirms the action when the next prompt is ordinary", () => {
    expect(askCheckOutcome("continue", "2026-10-03T10:01:00.000Z", [p("2026-10-03T10:30:00.000Z", "now add docs")])).toBe("continue");
    expect(askCheckOutcome("ask", "2026-10-03T10:01:00.000Z", [p("2026-10-03T10:30:00.000Z", "use option B")])).toBe("ask");
  });
  it("gives no label without a next prompt within 6h", () => {
    expect(askCheckOutcome("ask", "2026-10-03T10:01:00.000Z", [])).toBeNull();
    expect(askCheckOutcome("ask", "2026-10-03T10:01:00.000Z", [p("2026-10-03T17:00:00.000Z", "yes")])).toBeNull();
  });
});

describe("runOutcomeLabels", () => {
  it("labels imported ask-check items from their session transcript and counts items without signal", () => {
    const projects = fs.mkdtempSync(path.join(os.tmpdir(), "proj-"));
    fs.mkdirSync(path.join(projects, "-repo"));
    fs.writeFileSync(path.join(projects, "-repo", "s1.jsonl"), jsonl);
    expect(findTranscript(projects, "s1")).toBe(path.join(projects, "-repo", "s1.jsonl"));
    expect(findTranscript(projects, "nope")).toBeUndefined();

    const db = openDb(":memory:");
    const dec = (id: string, ts: string) => ({ id, ts, question: "ask-check", content_class: "transcript", provider: "jev", decision: "continue", confidence: 0.9, reason_code: "jev", latency_ms: 1, input_digest: "d", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" as const });
    recordDecision(db, dec("d1", "2026-10-03T10:01:00.000Z"));
    recordDecision(db, dec("d2", "2026-10-03T10:01:00.000Z"));
    recordDecisionDetails(db, { id: "d1", input_json: "{}", probabilities: null, rules_opinion: null, agreement: null, session_id: "s1" });
    recordDecisionDetails(db, { id: "d2", input_json: "{}", probabilities: null, rules_opinion: null, agreement: null, session_id: null });
    for (const id of ["d1", "d2"]) upsertEvalItem(db, { id: `i-${id}`, question: "ask-check", input_json: "{}", source: `decision:${id}`, model_decision: "continue", created_at: "2026-10-03T11:00:00.000Z" });

    expect(runOutcomeLabels(db, { projectsDir: projects, now: () => new Date("2026-10-04T00:00:00.000Z") })).toEqual({ labeled: 1, noSignal: 1 });
    expect(labeledItems(db, "ask-check", "outcome").map((r) => [r.id, r.label])).toEqual([["i-d1", "ask"]]);
  });
});
```

```ts
// judge/tests/adjudicate.test.ts
import { describe, expect, it } from "vitest";

import { adjudicate } from "../src/adjudicate.js";
import { labeledItems, openDb, upsertEvalItem } from "../src/db.js";
import { fakeProvider } from "./helpers.js";

const now = () => new Date("2026-10-04T00:00:00.000Z");
const item = (id: string) => ({ id, question: "wake-gate", input_json: '{"text":"step 2 done","senderKind":"teammate"}', source: `decision:${id}`, model_decision: "send", created_at: "2026-10-01T00:00:00.000Z" });

describe("adjudicate", () => {
  it("stores a 2-of-3 majority as an adjudicator label and never shows the model decision", async () => {
    const db = openDb(":memory:");
    upsertEvalItem(db, item("a"));
    const seen: string[] = [];
    let n = 0;
    const answers = ["batch", "batch", "send"];
    const provider = fakeProvider("claude-cli", ["message-meta"], (q) => {
      seen.push(q.prompt);
      return { status: "decided", decision: answers[n++ % 3] as string, confidence: 1, reason_code: "claude-cli" };
    });
    expect(await adjudicate(db, "wake-gate", { provider, limit: 10, now })).toEqual({ labeled: 1, split: 0, failed: 0 });
    expect(labeledItems(db, "wake-gate", "adjudicator").map((r) => r.label)).toEqual(["batch"]);
    expect(seen.every((p) => !p.includes("model_decision"))).toBe(true);
  });

  it("stores nothing on a three-way split, an off-enum answer, or a failed call (RF-4)", async () => {
    const db = openDb(":memory:");
    for (const id of ["a", "b", "c"]) upsertEvalItem(db, item(id));
    const script: Record<string, string[]> = { a: ["send", "batch", "drop"], b: ["maybe", "maybe", "send"], c: [] };
    let calls = 0;
    const provider = fakeProvider("claude-cli", ["message-meta"], () => {
      const id = ["a", "b", "c"][Math.floor(calls / 3)] as string;
      const d = script[id]?.[calls++ % 3];
      return d === undefined ? { status: "error", reason_code: "exit-1" } : { status: "decided", decision: d, confidence: 1, reason_code: "x" };
    });
    expect(await adjudicate(db, "wake-gate", { provider, limit: 10, now })).toEqual({ labeled: 0, split: 2, failed: 1 });
    expect(labeledItems(db, "wake-gate")).toEqual([]);
  });

  it("errors on an unknown question and respects the limit", async () => {
    const db = openDb(":memory:");
    for (const id of ["a", "b"]) upsertEvalItem(db, item(id));
    const provider = fakeProvider("claude-cli", ["message-meta"], { status: "decided", decision: "send", confidence: 1, reason_code: "x" });
    await expect(adjudicate(db, "nope", { provider, limit: 1, now })).rejects.toThrow("unknown question: nope");
    expect(await adjudicate(db, "wake-gate", { provider, limit: 1, now })).toEqual({ labeled: 1, split: 0, failed: 0 });
  });
});
```

In `label.test.ts`, write tests for:
- `runLabelImport`: imports once; skips escalated decisions, decisions older than the cutoff, and decisions without details
- `runLabelSet`: records an `override`; rejects `maybe` with `label must be one of: send, batch, drop, skip`; rejects an unknown item
- `runLabelStatus`: returns counts plus `agreement`

In `providers/claude-cli.test.ts`: by default the argv contains `--model haiku --effort low`; `makeClaudeCliProvider({ ..., model: "opus", effort: "high" })` puts `--model opus --effort high` in both the text and image argv.

- [ ] **Step 3: Run, verify fail.** Run: `cd judge && npx vitest run tests/outcomes.test.ts tests/adjudicate.test.ts tests/label.test.ts tests/providers/claude-cli.test.ts`. Expected: FAIL.

- [ ] **Step 4: Implement `outcomes.ts`**

```ts
// judge/src/outcomes.ts
// Outcome labels: what Joi did next tells us whether a decision was right,
// with no labeling work from Joi. Only the action taken is used (its
// consequences are what we observe); never the confidence or provider.
import fs from "node:fs";
import path from "node:path";

import { recordLabel, type Db } from "./db.js";

export interface RealPrompt { ts: string; text: string }

const SIX_HOURS_MS = 6 * 60 * 60 * 1000;
const INTERRUPT = "[Request interrupted by user";
const CORRECTION = /^\s*(no\b|nope|wait\b|stop\b|don'?t\b|actually\b|that'?s (wrong|not)|not what|undo|revert)/i;
const BARE_CONTINUE = /^\s*(continue|keep going|go on|go ahead|proceed|yes|yep|y|ok|okay|do it|carry on)[\s.!]*$/i;

function promptText(content: unknown): string | null {
  if (typeof content === "string") return content;
  if (!Array.isArray(content)) return null;
  if (content.some((b) => (b as { type?: unknown }).type === "tool_result")) return null;
  const texts = content.filter((b) => (b as { type?: unknown }).type === "text").map((b) => String((b as { text?: unknown }).text ?? ""));
  return texts.length === 0 ? null : texts.join("\n");
}

export function realPrompts(jsonl: string): RealPrompt[] {
  const out: RealPrompt[] = [];
  for (const line of jsonl.split("\n")) {
    if (line.trim() === "") continue;
    let row: { type?: unknown; timestamp?: unknown; message?: { content?: unknown } };
    try {
      row = JSON.parse(line) as typeof row;
    } catch {
      continue;
    }
    if (row.type !== "user" || typeof row.timestamp !== "string") continue;
    const text = promptText(row.message?.content);
    if (text === null || text.trimStart().startsWith("<")) continue;
    out.push({ ts: row.timestamp, text });
  }
  return out;
}

export function classifyReply(text: string): "interrupt" | "correction" | "bare-continue" | "other" {
  if (text.includes(INTERRUPT)) return "interrupt";
  if (CORRECTION.test(text)) return "correction";
  if (BARE_CONTINUE.test(text)) return "bare-continue";
  return "other";
}

export function askCheckOutcome(action: "continue" | "ask", decidedAt: string, prompts: readonly RealPrompt[]): "continue" | "ask" | null {
  const t0 = Date.parse(decidedAt);
  const next = prompts.find((p) => Date.parse(p.ts) > t0);
  if (next === undefined || Date.parse(next.ts) - t0 > SIX_HOURS_MS) return null;
  const kind = classifyReply(next.text);
  if (action === "continue") return kind === "interrupt" || kind === "correction" ? "ask" : "continue";
  return kind === "bare-continue" ? "continue" : "ask";
}

export function findTranscript(projectsDir: string, sessionId: string): string | undefined {
  for (const dir of fs.readdirSync(projectsDir)) {
    const candidate = path.join(projectsDir, dir, `${sessionId}.jsonl`);
    if (fs.existsSync(candidate)) return candidate;
  }
  return undefined;
}

export function runOutcomeLabels(
  db: Db,
  opts: { projectsDir: string; now: () => Date; readFile?: (p: string) => string },
): { labeled: number; noSignal: number } {
  const read = opts.readFile ?? ((p: string) => fs.readFileSync(p, "utf8"));
  const rows = db.prepare(
    `SELECT e.id AS item_id, d.ts, d.decision, x.session_id FROM eval_items e
     JOIN decisions d ON e.source = 'decision:' || d.id
     JOIN decision_details x ON x.id = d.id
     LEFT JOIN labels l ON l.item_id = e.id AND l.source = 'outcome'
     WHERE e.question = 'ask-check' AND l.item_id IS NULL`,
  ).all() as Array<{ item_id: string; ts: string; decision: "continue" | "ask"; session_id: string | null }>;
  let labeled = 0;
  let noSignal = 0;
  for (const r of rows) {
    const file = r.session_id === null ? undefined : findTranscript(opts.projectsDir, r.session_id);
    const label = file === undefined ? null : askCheckOutcome(r.decision, r.ts, realPrompts(read(file)));
    if (label === null) {
      noSignal++;
      continue;
    }
    recordLabel(db, r.item_id, label, opts.now().toISOString(), "outcome");
    labeled++;
  }
  return { labeled, noSignal };
}
```

- [ ] **Step 5: Implement `adjudicate.ts`**

```ts
// judge/src/adjudicate.ts
// A stronger model than anything in the judge chain labels stored inputs:
// 3 samples, keep only a 2-of-3 in-enum majority. It sees the question's own
// prompt, every option's criteria description and the input, never the
// decision being scored.
import { QUESTIONS } from "./commands.js";
import { recordLabel, type Db, type EvalItemRow } from "./db.js";
import { toRef } from "./question.js";
import type { Provider } from "./types.js";

export async function adjudicate(
  db: Db, question: string,
  opts: { provider: Provider; limit: number; samples?: number; now: () => Date },
): Promise<{ labeled: number; split: number; failed: number }> {
  const q = QUESTIONS[question];
  if (q === undefined) throw new Error(`unknown question: ${question}`);
  const samples = opts.samples ?? 3;
  const outputs = q.outputs as readonly string[];
  const skippedIds = new Set<string>();
  let labeled = 0;
  let split = 0;
  let failed = 0;
  for (let n = 0; n < opts.limit; n++) {
    const item = nextUnlabeledExcept(db, question, skippedIds);
    if (item === undefined) break;
    const parsed = q.inputSchema.safeParse(JSON.parse(item.input_json));
    if (!parsed.success) {
      skippedIds.add(item.id);
      failed++;
      continue;
    }
    const ref = toRef(q, parsed.data);
    const rubric = Object.entries(q.criteria ?? {}).map(([o, d]) => `- ${o}: ${d}`).join("\n");
    const adjRef = { ...ref, prompt: rubric === "" ? ref.prompt : `${ref.prompt}\n\nOptions:\n${rubric}` };
    const votes: string[] = [];
    let errors = 0;
    for (let s = 0; s < samples; s++) {
      const r = await opts.provider.decide(adjRef, parsed.data, Math.max(q.timeBudgetMs, 60000));
      if (r.status !== "decided") errors++;
      else if (outputs.includes(r.decision)) votes.push(r.decision);
    }
    const counts = new Map<string, number>();
    for (const v of votes) counts.set(v, (counts.get(v) ?? 0) + 1);
    const [top, topCount] = [...counts.entries()].sort((a, b) => b[1] - a[1])[0] ?? ["", 0];
    if (topCount * 2 > samples) {
      recordLabel(db, item.id, top, opts.now().toISOString(), "adjudicator");
      labeled++;
    } else {
      skippedIds.add(item.id);
      if (errors === samples) failed++;
      else split++;
    }
  }
  return { labeled, split, failed };
}

function nextUnlabeledExcept(db: Db, question: string, skip: ReadonlySet<string>): EvalItemRow | undefined {
  const rows = db.prepare(
    `SELECT e.* FROM eval_items e LEFT JOIN labels l ON l.item_id = e.id AND l.source = 'adjudicator'
     WHERE l.item_id IS NULL AND e.question = ? ORDER BY e.created_at ASC, e.id ASC`,
  ).all(question) as EvalItemRow[];
  return rows.find((r) => !skip.has(r.id));
}
```

(`nextUnlabeledExcept` exists because a split item stays unlabeled and would otherwise be picked again. The adjudicator's majority must be a strict majority of `samples`.)

- [ ] **Step 6: Implement `label.ts`**, with the import / set / status behaviour from the Interfaces above. `runLabelSet` calls `recordLabel(db, itemId, label, now().toISOString(), "override")`. `runLabelStatus` prints `labelCounts(db).map((c) => ({ ...c, agreement: labelAgreement(db, c.question) }))`.

- [ ] **Step 7: `claude-cli.ts` model option.** Change the signature to `makeClaudeCliProvider(deps: { spawn: Spawn; tmpDirFactory: () => string; model?: string; effort?: string })` and replace the model/effort pair in both argv lists (the image branch has `"--model", "haiku", "--effort", "low"` on one line; the text branch has them on two lines) with `deps.model ?? "haiku"` and `deps.effort ?? "low"`.

- [ ] **Step 8: Route in `cli.ts`.**
  - `label import|outcomes|set|status`. `outcomes` uses `projectsDir = path.join(os.homedir(), ".claude", "projects")`.
  - `adjudicate <question> [--limit N]` builds `makeClaudeCliProvider({ tmpDirFactory, spawn: makeExecSpawn(AGENT_CLI_BINARIES["claude-cli"]), model: "opus", effort: "high" })` and prints the counts. Exit 1 if `claude` isn't on PATH.

- [ ] **Step 9: Document in `skills/judge/SKILL.md`.** Add a "Labels (automatic)" section:
  - `judge label import`, then `judge label outcomes` (free), then `judge adjudicate <q> --limit 60` (it costs Opus tokens: about 3 runs × 3k tokens per item)
  - `judge label status` shows coverage and how often outcome and Opus labels agree
  - `judge label set` is an optional override; no one is expected to label by hand

- [ ] **Step 10: Full suite.** Run: `cd judge && npm run typecheck && npm run test:coverage`. Expected: PASS, 100%.

- [ ] **Step 11: Commit**

```bash
git add judge/src/label.ts judge/src/outcomes.ts judge/src/adjudicate.ts judge/src/providers/claude-cli.ts judge/src/cli.ts judge/tests skills/judge/SKILL.md
git commit -m "feat: automatic judge labels from ask-check outcomes and an Opus 2-of-3 adjudicator"
```

---

### Task 4: `judge eval`: re-score a provider on labeled items, with record/replay and a threshold sweep

**Files:**
- Create: `judge/src/eval.ts` (pure scoring + the runner)
- Modify: `judge/src/cli.ts` (route `eval`; build the single provider; write the report file)
- Test: `judge/tests/eval.test.ts`

**Interfaces:**
- Consumes: `labeledItems` (Task 1); `QUESTIONS`, `toRef` (`question.ts`); `Provider`, `ProviderResult` (`types.ts`).
- Produces:
  ```ts
  export interface EvalResult { itemId: string; label: string; result: ProviderResult<string>; latencyMs: number; inputTokens?: number }
  export interface SweepRow { threshold: number; coverage: number; accuracyCovered: number }
  export interface EvalReport { question: string; provider: string; variant: string; n: number; decided: number; accuracy: number; decisiveRate: number; accuracyDecisive: number; p50LatencyMs: number; sweep: SweepRow[] }
  export const SWEEP = [0.5, 0.6, 0.7, 0.8, 0.9] as const;
  export function scoreEval(question: string, provider: string, variant: string, threshold: number, results: readonly EvalResult[]): EvalReport;
  export function renderEvalReport(r: EvalReport): string;
  export type Variant = (input: unknown) => unknown;
  export const VARIANTS: Record<string, Record<string, Variant>>; // question -> name -> transform; "as-is" is implicit
  export async function runEval(db: Db, opts: { question: string; provider: Provider; variant: string; labels?: LabelSource | "any"; record?: (line: string) => void; replay?: readonly EvalResult[] }): Promise<{ exitCode: number; stdout: string; stderr?: string; report?: EvalReport }>;
  ```
  - Definitions:
    - **accuracy** = decided and correct, out of n (an undecided item counts as not correct)
    - **decisiveRate** = decided with confidence ≥ the question's threshold, out of n
    - **accuracyDecisive** = correct among the decisive items
    - **sweep coverage(t)** = decided with confidence ≥ t, out of n
    - **accuracyCovered(t)** = correct among those covered at t
- CLI: `judge eval <question> [--provider jev|claude-cli|codex-cli|cursor-cli] [--variant <name>] [--labels outcome|adjudicator|any] [--record <file.jsonl>] [--replay <file.jsonl>]`. The default `--labels any` uses the precedence from Task 1. Each report states which label source it used. Writes the markdown report to `~/.agentic-workflow/judge/evals/<question>-<provider>-<variant>-<ts>.md`, prints the JSON report, and never touches `decisions`.

- [ ] **Step 1: Write failing tests**

```ts
// judge/tests/eval.test.ts
import { describe, expect, it } from "vitest";

import { openDb, recordLabel, upsertEvalItem } from "../src/db.js";
import { renderEvalReport, runEval, scoreEval, type EvalResult } from "../src/eval.js";
import { fakeProvider } from "./helpers.js";

const decided = (decision: string, confidence: number) => ({ status: "decided" as const, decision, confidence, reason_code: "x" });

describe("scoreEval", () => {
  it("computes accuracy, decisive rate, accuracy-on-decisive, p50 latency and the sweep", () => {
    const results: EvalResult[] = [
      { itemId: "1", label: "send", result: decided("send", 0.95), latencyMs: 100 },
      { itemId: "2", label: "send", result: decided("batch", 0.65), latencyMs: 300 },
      { itemId: "3", label: "drop", result: decided("drop", 0.55), latencyMs: 200 },
      { itemId: "4", label: "send", result: { status: "unavailable", reason_code: "timeout" }, latencyMs: 900 },
    ];
    const r = scoreEval("wake-gate", "jev", "as-is", 0.7, results);
    expect(r).toMatchObject({ n: 4, decided: 3, accuracy: 0.5, decisiveRate: 0.25, accuracyDecisive: 1, p50LatencyMs: 200 });
    expect(r.sweep.find((s) => s.threshold === 0.6)).toEqual({ threshold: 0.6, coverage: 0.5, accuracyCovered: 0.5 });
    expect(r.sweep.find((s) => s.threshold === 0.9)).toEqual({ threshold: 0.9, coverage: 0.25, accuracyCovered: 1 });
    expect(renderEvalReport(r)).toContain("| 0.9 | 25.0% | 100.0% |");
  });

  it("reports zeros, not NaN, on an empty set", () => {
    expect(scoreEval("q", "jev", "as-is", 0.7, [])).toMatchObject({ n: 0, accuracy: 0, decisiveRate: 0, accuracyDecisive: 0, p50LatencyMs: 0 });
  });
});

describe("runEval", () => {
  function labeled() {
    const db = openDb(":memory:");
    upsertEvalItem(db, { id: "i1", question: "wake-gate", input_json: '{"text":"step 2 done","senderKind":"teammate"}', source: "decision:a", model_decision: "send", created_at: "2026-10-01T00:00:00.000Z" });
    recordLabel(db, "i1", "batch", "2026-10-02T00:00:00.000Z");
    return db;
  }

  it("calls the provider once per labeled item, records each result line, and never writes decisions", async () => {
    const db = labeled();
    const lines: string[] = [];
    const out = await runEval(db, { question: "wake-gate", provider: fakeProvider("jev", ["message-meta"], decided("batch", 0.9)), variant: "as-is", record: (l) => lines.push(l) });
    expect(out.exitCode).toBe(0);
    expect(out.report).toMatchObject({ n: 1, accuracy: 1 });
    expect(JSON.parse(lines[0] as string)).toMatchObject({ itemId: "i1", label: "batch", result: { decision: "batch" } });
    expect((db.prepare("SELECT COUNT(*) n FROM decisions").get() as { n: number }).n).toBe(0);
  });

  it("replays recorded results without calling the provider", async () => {
    const db = labeled();
    const provider = fakeProvider("jev", ["message-meta"], () => { throw new Error("must not be called"); });
    const out = await runEval(db, { question: "wake-gate", provider, variant: "as-is", replay: [{ itemId: "i1", label: "batch", result: decided("send", 0.9), latencyMs: 5 }] });
    expect(out.report).toMatchObject({ n: 1, accuracy: 0 });
  });

  it("errors on an unknown question, an unknown variant, or no labeled items", async () => {
    const db = openDb(":memory:");
    const p = fakeProvider("jev", ["message-meta"], decided("send", 1));
    expect((await runEval(db, { question: "nope", provider: p, variant: "as-is" })).exitCode).toBe(1);
    expect((await runEval(db, { question: "wake-gate", provider: p, variant: "nope" })).exitCode).toBe(1);
    expect(await runEval(db, { question: "wake-gate", provider: p, variant: "as-is" })).toMatchObject({ exitCode: 1, stderr: "no labeled items for wake-gate (run: judge label import, judge label outcomes, judge adjudicate wake-gate)" });
  });
});
```

(The `wake-gate` fixture's `input_json` must satisfy `WakeGateInputSchema`. Copy the field names from `judge/src/questions/wake-gate.ts` when writing the test if they differ from the ones shown.)

- [ ] **Step 2: Run, verify fail.** Run: `cd judge && npx vitest run tests/eval.test.ts`. Expected: FAIL, module not found.

- [ ] **Step 3: Implement `eval.ts`**

```ts
// judge/src/eval.ts
// Offline re-scoring of one provider against automatic labels (Navigator
// scripts/judge_eval.py, adapted). Calls provider.decide directly: no chain,
// no evaluate(), no decisions rows. --record/--replay lets a threshold or
// report change re-run without network.
import { QUESTIONS } from "./commands.js";
import { labeledItems, type Db, type LabelSource } from "./db.js";
import { toRef } from "./question.js";
import type { Provider, ProviderResult } from "./types.js";

export interface EvalResult { itemId: string; label: string; result: ProviderResult<string>; latencyMs: number; inputTokens?: number }
export interface SweepRow { threshold: number; coverage: number; accuracyCovered: number }
export interface EvalReport {
  question: string; provider: string; variant: string; n: number; decided: number;
  accuracy: number; decisiveRate: number; accuracyDecisive: number; p50LatencyMs: number; sweep: SweepRow[];
}
export type Variant = (input: unknown) => unknown;

export const SWEEP = [0.5, 0.6, 0.7, 0.8, 0.9] as const;

// question -> variant name -> transform of the stored input. Later plans fill
// this in by key (Plan C: prompt-sort axes; Plan B: resolution-check and
// turn-progress); "as-is" is always available.
export const VARIANTS: Record<string, Record<string, Variant>> = {};

const ratio = (a: number, b: number): number => (b === 0 ? 0 : a / b);

export function scoreEval(question: string, provider: string, variant: string, threshold: number, results: readonly EvalResult[]): EvalReport {
  const n = results.length;
  const decidedRows = results.filter((r) => r.result.status === "decided") as Array<EvalResult & { result: { status: "decided"; decision: string; confidence: number } }>;
  const correct = (r: (typeof decidedRows)[number]): boolean => r.result.decision === r.label;
  const covered = (t: number) => decidedRows.filter((r) => r.result.confidence >= t);
  const decisive = covered(threshold);
  const latencies = results.map((r) => r.latencyMs).sort((a, b) => a - b);
  return {
    question, provider, variant, n, decided: decidedRows.length,
    accuracy: ratio(decidedRows.filter(correct).length, n),
    decisiveRate: ratio(decisive.length, n),
    accuracyDecisive: ratio(decisive.filter(correct).length, decisive.length),
    p50LatencyMs: latencies.length === 0 ? 0 : (latencies[Math.floor((latencies.length - 1) / 2)] as number),
    sweep: SWEEP.map((t) => {
      const c = covered(t);
      return { threshold: t, coverage: ratio(c.length, n), accuracyCovered: ratio(c.filter(correct).length, c.length) };
    }),
  };
}

const pct = (x: number): string => `${(x * 100).toFixed(1)}%`;

export function renderEvalReport(r: EvalReport): string {
  return [
    `# judge eval: ${r.question} / ${r.provider} / ${r.variant}`,
    "",
    `n=${r.n} decided=${r.decided} accuracy=${pct(r.accuracy)} decisive=${pct(r.decisiveRate)} accuracy-on-decisive=${pct(r.accuracyDecisive)} p50=${r.p50LatencyMs}ms`,
    "",
    "| Threshold | Coverage | Accuracy (covered) |",
    "|---|---|---|",
    ...r.sweep.map((s) => `| ${s.threshold} | ${pct(s.coverage)} | ${pct(s.accuracyCovered)} |`),
    "",
  ].join("\n");
}

export async function runEval(
  db: Db,
  opts: { question: string; provider: Provider; variant: string; labels?: LabelSource | "any"; record?: (line: string) => void; replay?: readonly EvalResult[] },
): Promise<{ exitCode: number; stdout: string; stderr?: string; report?: EvalReport }> {
  const question = QUESTIONS[opts.question];
  if (question === undefined) return { exitCode: 1, stdout: "", stderr: `unknown question: ${opts.question}` };
  const variant: Variant | undefined = opts.variant === "as-is" ? (i) => i : VARIANTS[opts.question]?.[opts.variant];
  if (variant === undefined) return { exitCode: 1, stdout: "", stderr: `unknown variant for ${opts.question}: ${opts.variant}` };
  const items = labeledItems(db, opts.question, opts.labels ?? "any");
  if (items.length === 0) return { exitCode: 1, stdout: "", stderr: `no labeled items for ${opts.question} (run: judge label import, judge label outcomes, judge adjudicate ${opts.question})` };

  let results: EvalResult[];
  if (opts.replay !== undefined) {
    results = [...opts.replay];
  } else {
    results = [];
    for (const item of items) {
      const parsed = question.inputSchema.safeParse(variant(JSON.parse(item.input_json)));
      const start = Date.now();
      const result: ProviderResult<string> = parsed.success
        ? await opts.provider.decide(toRef(question, parsed.data), parsed.data, question.timeBudgetMs)
        : { status: "error", reason_code: "invalid-input" };
      const row: EvalResult = { itemId: item.id, label: item.label, result, latencyMs: Date.now() - start };
      results.push(row);
      opts.record?.(JSON.stringify(row));
    }
  }
  const report = scoreEval(opts.question, opts.provider.name, opts.variant, question.threshold, results);
  return { exitCode: 0, stdout: JSON.stringify(report), report };
}
```

Add tests for the branches coverage flags: an item whose `input_json` fails the schema (the result is an `invalid-input` error and counts as undecided), and a run without `record`.

- [ ] **Step 4: Route in `cli.ts`.** Add a `case "eval":`:
  - Pick the provider by `--provider` (default `jev`) from the `providers` array already built in `cli.ts`; exit 1 if it isn't installed.
  - `--record <file>`: append each line to the file.
  - `--replay <file>`: read JSONL into `EvalResult[]`.
  - On success, write `renderEvalReport(report)` to `path.join(judgeStateDir(), "judge", "evals", \`${q}-${provider}-${variant}-${Date.now()}.md\`)` (mkdir -p), and print the JSON.

- [ ] **Step 5: Full suite.** Run: `cd judge && npm run typecheck && npm run test:coverage`. Expected: PASS, 100%.

- [ ] **Step 6: Commit**

```bash
git add judge/src/eval.ts judge/src/cli.ts judge/tests/eval.test.ts
git commit -m "feat: judge eval re-scores a provider on labeled items with record/replay and threshold sweep"
```

- [ ] **Step 7: Baseline numbers (automatic, no human time).** Use the worktree build of `judge` (Global Constraints). Run `judge label import --since 14d` first. **If it prints `{"imported":0}`, mark Steps 7 and Task 5 Step 6 deferred (see Global Constraints), copy no tables, and go on to Task 5.** Otherwise run these one at a time:
  1. `judge label import --since 14d`
  2. `judge label outcomes`
  3. `judge adjudicate brief-scope --limit 60`, `judge adjudicate wake-gate --limit 60`, `judge adjudicate ask-check --limit 60`
  4. `judge label status`: check the outcome-vs-adjudicator agreement for ask-check. Below 70% means the adjudicator's labels aren't trusted for this round; say so in the PR.
  5. For each of the three questions: `judge eval <q> --provider jev --record ~/.agentic-workflow/judge/evals/<q>-jev-baseline.jsonl`, then the same with `--provider claude-cli`

  Copy the markdown reports into the PR description as the "before" numbers for Task 5.

---

### Task 5: Give Jev real option descriptions

**Files:**
- Modify: `judge/src/question.ts` (`criteria?` on `QuestionModule`; `toRef` copies it)
- Modify: `judge/src/types.ts` (`criteria?` on `QuestionRef`)
- Modify: `judge/src/providers/jev.ts` (use `question.criteria?.[o] ?? o`)
- Modify: all 7 `judge/src/questions/*.ts` (add `criteria`)
- Test: `judge/tests/providers/jev.test.ts`, `judge/tests/questions/*.test.ts`

**Interfaces:**
- Produces: `QuestionModule.criteria?: Readonly<Record<O, string>>` and `QuestionRef.criteria?: Readonly<Record<string, string>>`.
- The two optional fields are already declared by Task 3; this task adds the `toRef` copy, the `jev.ts` use, and the per-question values.

- [ ] **Step 1: Write the failing provider test** (replace the criteria assertion in the existing "posts to /v1/systemone…" test)

```ts
it("sends each option's description as Jev criteria, falling back to the option name", async () => {
  const fetch = vi.fn().mockResolvedValue({ status: 200, json: async () => ({ answers: { decision: { type: "choice", choice: "send", confidence: 0.9 } } }) });
  const provider = makeJevProvider({ fetch, apiKey: async () => "k" });
  await provider.decide({ ...question, criteria: { send: "deliver now", batch: "queue for later" } }, {}, 1000);
  const body = JSON.parse((fetch.mock.calls[0] as [string, { body: string }])[1].body) as { questions: { decision: { criteria: Record<string, string> } } };
  expect(body.questions.decision.criteria).toEqual({ send: "deliver now", batch: "queue for later", drop: "drop" });
});
```

- [ ] **Step 2: Run, verify fail.** Run: `cd judge && npx vitest run tests/providers/jev.test.ts`. Expected: FAIL.

- [ ] **Step 3: Implement.**
  - In `types.ts`, add `criteria?: Readonly<Record<string, string>>;` to `QuestionRef`.
  - In `question.ts`, add `criteria?: Readonly<Record<O, string>>;` to `QuestionModule`, and have `toRef` copy it when defined (same pattern as `extraProperties`).
  - In `jev.ts`, change the criteria loop to `criteria[option] = question.criteria?.[option] ?? option;`.

- [ ] **Step 4: Add `criteria` to every question.** Write each description from the question's own prompt text. For example, for `resolution-check`:

```ts
criteria: {
  resolved: "every part of the reported problem is covered by the passing check and the diff",
  partial: "some part of the brief is not covered, or the diff hides the symptom without fixing the root cause",
  unresolved: "the check or the diff does not address the reported problem",
},
```

Do the same for:
- `brief-scope`: ready / missing / needs_design
- `ask-check`: continue / ask
- `wake-gate`: send / batch / drop
- `rule-check`: violated / fine / n/a
- `ui-element-repair`: repaired / no-good-candidate
- `visual-critique`: looks-right / looks-off / sloppy

Each `tests/questions/<q>.test.ts` gets one assertion: `expect(Object.keys(q.criteria ?? {})).toEqual([...q.outputs])`.

- [ ] **Step 5: Full suite.** Run: `cd judge && npm run typecheck && npm run test:coverage`. Expected: PASS, 100%.

- [ ] **Step 6: After numbers.** (Skip, as deferred, when Task 4 Step 7 was deferred.) For each question with a baseline from Task 4 Step 7, run `judge eval <q> --provider jev` (same label source as the baseline). Put the before/after table (accuracy, decisive rate, accuracy-on-decisive) in the commit body. If decisive rate *drops* for a question without an accuracy gain, revert that question's criteria and note it.

- [ ] **Step 7: Commit**

```bash
git add judge/src judge/tests
git commit -m "feat: describe every judge option to Jev instead of sending bare labels"
```

---

### Task 6: Treat "unsure" as a normal answer: `fallbackRules`, per-question provider lists, no failure row

**Files:**
- Modify: `judge/src/question.ts` (`fallbackRules?`)
- Modify: `judge/src/config.ts` (`JudgeQuestionConfig.providers?: ProviderName[]`)
- Modify: `judge/src/evaluate.ts` (undecided verdicts)
- Modify: `judge/src/questions/ask-check.ts`, `judge/src/questions/wake-gate.ts` (`fallbackRules`)
- Modify: `skills/judge/SKILL.md` (document the providers override and the "undecided" state)
- Test: `judge/tests/evaluate.test.ts`, `judge/tests/config.test.ts`, `judge/tests/questions/ask-check.test.ts`, `judge/tests/questions/wake-gate.test.ts`

**Interfaces:**
- Produces:
  - `QuestionModule.fallbackRules?: (input: I) => O | null`. This is a cheap rule-based answer used only when a model answered **below threshold**. It's distinct from `preRules`, which run before any model.
  - `JudgeQuestionConfig.providers?: ProviderName[]` restricts and orders the chain for one question (for example `["jev", "rules"]` skips the CLIs). Unknown names are dropped.
  - Decision rows settled this way have `provider = "rules"`, `reason_code = "fallback-after-undecided"`, and `decision_details.agreement = "undecided"`.
  - Below-threshold answers **no longer write a `failures` row**. They stay visible in `skipped` (`below_threshold`) and in details.

Policy (Navigator's band idea, adapted to choice confidence): when a model answers below the question's threshold, treat the answer as unsure. If the question has `fallbackRules` that return non-null, settle with the rules immediately rather than walking to a slower provider. Otherwise continue the chain as today.

- [ ] **Step 1: Write failing evaluate tests**

```ts
it("settles with fallbackRules when jev is below threshold, without calling the slower provider", async () => {
  const db = openDb(":memory:");
  const slow = vi.fn();
  const q = { ...textQuestion, threshold: 0.8, fallbackRules: () => "send" as const };
  const out = await evaluate(q, { text: "hi" }, {
    db, config: DEFAULT_CONFIG, randomId: () => "u1",
    providers: [
      fakeProvider("jev", ["message-meta"], { status: "decided", decision: "batch", confidence: 0.55, reason_code: "jev" }),
      { name: "claude-cli", classes: new Set(["message-meta"]), decide: slow },
    ],
    chain: { classes: { "message-meta": ["jev", "claude-cli"] } },
  });
  expect(out).toMatchObject({ decision: "send", model: "rules", reason_code: "fallback-after-undecided" });
  expect(slow).not.toHaveBeenCalled();
  expect(getDecisionDetails(db, "u1")?.agreement).toBe("undecided");
});

it("does not record a failure row for a below-threshold answer", async () => {
  const db = openDb(":memory:");
  await evaluate({ ...textQuestion, threshold: 0.8 }, { text: "hi" }, {
    db, config: DEFAULT_CONFIG,
    providers: [fakeProvider("jev", ["message-meta"], { status: "decided", decision: "send", confidence: 0.5, reason_code: "jev" })],
    chain: { classes: { "message-meta": ["jev"] } },
  });
  expect((db.prepare("SELECT COUNT(*) n FROM failures").get() as { n: number }).n).toBe(0);
});

it("restricts the chain to a question's configured providers", async () => {
  const db = openDb(":memory:");
  const cli = vi.fn();
  const out = await evaluate(textQuestion, { text: "hi" }, {
    db, config: { questions: { [textQuestion.name]: { enabled: true, threshold: 0.7, providers: ["jev", "rules"] } } },
    providers: [
      fakeProvider("jev", ["message-meta"], { status: "unavailable", reason_code: "no-api-key" }),
      { name: "claude-cli", classes: new Set(["message-meta"]), decide: cli },
    ],
    chain: { classes: { "message-meta": ["jev", "claude-cli", "rules"] } },
  });
  expect(cli).not.toHaveBeenCalled();
  expect(out).toMatchObject({ escalate: true });
});
```

In `config.test.ts`: `loadConfig` keeps `providers: ["jev", "rules"]` on a question and drops unknown names (`["jev", "bogus"]` becomes `["jev"]`).

- [ ] **Step 2: Run, verify fail.** Run: `cd judge && npx vitest run tests/evaluate.test.ts tests/config.test.ts`. Expected: the new tests FAIL.

- [ ] **Step 3: Implement.**
  - **`question.ts`:** add `fallbackRules?: (input: I) => O | null;`.
  - **`config.ts`:** add `providers?: ProviderName[]` to `JudgeQuestionConfig`. In `loadConfig`, map each question entry and filter `providers` to valid `ProviderName`s ("rules", "jev", or `isAgentCliName`).
  - **`evaluate.ts`:**
    - After computing `candidates`, if `qConfig?.providers` is set, filter and reorder it: `candidates = qConfig.providers.map((n) => candidates.find((p) => p.name === n)).filter(defined)`.
    - Replace the below-threshold block with:

```ts
if (result.confidence < threshold) {
  skipped.push({ provider: provider.name, reason: "below_threshold" });
  lastReason = "below-threshold";
  const fallback = question.fallbackRules?.(input) ?? null;
  if (fallback !== null) return settle(fallback, 1, "rules", "fallback-after-undecided", latencyMs, i, skipped, undefined, result.probabilities, "undecided");
  continue;
}
```

    - Extend `settle()` with `agreement?: Agreement` and pass it through to `recordDetails`.

- [ ] **Step 4: Add safe `fallbackRules`.**
  - `ask-check`: `fallbackRules: () => "ask"`. When unsure, stopping to ask is always safe, and it's what an escalation already does.
  - `wake-gate`: `fallbackRules: () => "send"`. When unsure, deliver the message, which was the behaviour before judge existed.

  Each question's test asserts the fallback value. Don't add `fallbackRules` to `resolution-check`, `brief-scope` or `rule-check`: there's no safe default, so they keep walking the chain.

- [ ] **Step 5: Document** in `skills/judge/SKILL.md`:
  - `judge config` accepts a per-question `providers` list (by hand-editing `config.json`)
  - "below threshold" is now an *undecided* answer, not a failure; `judge health` stops counting it
  - which questions settle undecided answers with rules

- [ ] **Step 6: Full suite.** Run: `cd judge && npm run typecheck && npm run test:coverage`. Expected: PASS, 100%. Also run `bash config/hooks/tests/judge-health.test.sh` (health semantics changed).

- [ ] **Step 7: Commit**

```bash
git add judge/src judge/tests skills/judge/SKILL.md
git commit -m "feat: judge treats below-threshold answers as undecided, settles them with fallback rules"
```

---

### Task 7: Agreement telemetry and scorer columns

**Files:**
- Modify: `judge/src/evaluate.ts` (rules opinion on model decisions)
- Modify: `scorer/src/judge-section.ts` (new columns; tolerate a missing `decision_details`)
- Test: `judge/tests/evaluate.test.ts`, `scorer/tests/judge-section.test.ts`

**Interfaces:**
- Consumes: `fallbackRules` (Task 6); `decision_details.agreement` / `rules_opinion` (Task 1).
- Produces: `JudgeReportRow` gains `undecided: number`, `agreed: number`, `overrode: number`, `p50LatencyMs: number`, `byProvider: Record<string, number>`.

- [ ] **Step 1: Write failing tests.**

In `judge/tests/evaluate.test.ts`, with `fallbackRules: () => "send"` and a jev answer `batch` at 0.9 (above threshold), details are `{ rules_opinion: "send", agreement: "overrode" }`. With jev answering `send`, they're `{ rules_opinion: "send", agreement: "agreed" }`. A question without `fallbackRules` gets `rules_opinion: null, agreement: null`.

In `scorer/tests/judge-section.test.ts`:

```ts
it("adds undecided/agreed/overrode counts, p50 latency and per-provider share from decision_details", () => {
  const db = openDb(":memory:");
  const ts = "2026-10-03T00:00:00.000Z";
  const row = (id: string, provider: string, latency: number) => ({
    id, ts, question: "wake-gate", content_class: "message-meta", provider, decision: "send", confidence: 0.9,
    reason_code: provider, latency_ms: latency, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" as const,
  });
  recordDecision(db, row("a", "jev", 300));
  recordDecision(db, row("b", "jev", 400));
  recordDecision(db, row("c", "rules", 0));
  recordDecisionDetails(db, { id: "a", input_json: "{}", probabilities: null, rules_opinion: "send", agreement: "agreed" });
  recordDecisionDetails(db, { id: "b", input_json: "{}", probabilities: null, rules_opinion: "send", agreement: "overrode" });
  recordDecisionDetails(db, { id: "c", input_json: "{}", probabilities: null, rules_opinion: null, agreement: "undecided" });
  const [r] = judgeSection(db, "2026-10-01T00:00:00.000Z");
  expect(r).toMatchObject({ undecided: 1, agreed: 1, overrode: 1, p50LatencyMs: 300, byProvider: { jev: 2, rules: 1 } });
});

it("renders when the judge db predates decision_details (RF-2)", () => {
  const db = openDb(":memory:");
  db.exec("DROP TABLE decision_details");
  recordDecision(db, { id: "a", ts: "2026-10-03T00:00:00.000Z", question: "q", content_class: "brief", provider: "jev", decision: "x", confidence: 1, reason_code: "jev", latency_ms: 1, input_digest: "x", undone_at: null, chain_position: 0, skipped: [], outcome: "decided" });
  expect(judgeSection(db, "2026-10-01T00:00:00.000Z")[0]).toMatchObject({ undecided: 0, agreed: 0, overrode: 0 });
});
```

- [ ] **Step 2: Run, verify fail.** Run: `cd judge && npx vitest run tests/evaluate.test.ts`, then `cd ../scorer && npx vitest run tests/judge-section.test.ts`. Expected: FAIL.

- [ ] **Step 3: Implement.**
  - **`settle()` parameter order** (Tasks 2, 6, 7 extend it): `(decision, confidence, model, reasonCode, latencyMs, chainPosition, skipped, extra?, probabilities?, agreement?, rulesOpinion?)`.
  - **`evaluate.ts`:** in the provider-success path, compute `const opinion = question.fallbackRules?.(input) ?? null;` and `const agreement = opinion === null ? null : opinion === result.decision ? "agreed" : "overrode";`, then pass both to `settle` and on to `recordDetails`.
  - **`judge-section.ts`:**
    - Detect the table with `SELECT name FROM sqlite_master WHERE type='table' AND name='decision_details'`.
    - When it's present, `LEFT JOIN decision_details` to count `agreement` values per question.
    - Compute `p50` like `p95`, and `byProvider` from the decided rows' `provider`.
    - Add the columns to `renderJudgeSection`: `Undecided | Agreed | Overrode | p50 (ms) | Providers` (the providers cell as `jev 2 · rules 1`).
    - Update the existing judge-section test's expected row objects with the new fields.

- [ ] **Step 4: Full suites, one at a time.** Run: `cd judge && npm run typecheck && npm run test:coverage`, then `cd ../scorer && npm run typecheck && npm run test:coverage`. Expected: both PASS, 100%.

- [ ] **Step 5: Commit**

```bash
git add judge/src/evaluate.ts judge/tests/evaluate.test.ts scorer/src/judge-section.ts scorer/tests/judge-section.test.ts
git commit -m "feat: judge records rules agreement; scorer reports undecided/agreed/overrode and provider share"
```

---

## Done when

- The merge gate in `AGENTS.md` passes: typecheck and tests in every package, bash tests, `sync-rules --check`, setup dry-run.
- `judge health` no longer counts below-threshold answers as failures.
- The PR description carries the Task 4 baseline and Task 5 before/after eval tables for at least `brief-scope`, `ask-check` and `wake-gate`, from automatic labels (source named), with the ask-check outcome/adjudicator agreement rate. If no stored inputs existed overnight (Global Constraints), the PR says "eval tables deferred: no stored inputs yet" and lists the commands for Joi to run after the real install; that is an accepted outcome, not a blocked task.
- No step in this plan requires Joi to label anything.
