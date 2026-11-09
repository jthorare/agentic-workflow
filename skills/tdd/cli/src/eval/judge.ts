// ev_judge.py: the LLM-judged rubric over the document text (--with-judge only): clarity, evidence grounding,
// decision completeness, consistency of terms, each 0..1 with a one-line reason. Claude (claude-sonnet-5-5) with
// ANTHROPIC_API_KEY; else autoevals' LLMClassifier with OPENAI_API_KEY or BRAINTRUST_API_KEY; else "not run".
import { Cause, Effect, Exit } from "effect";
import * as py from "../py";
import { float } from "../py/num";
import { type Py, contentText, fl, get, now, readText } from "./common";
import * as L from "./llm";
import { type ScoreRec, error, notRun, score } from "./score";

const path = py.path;

export const CRITERIA: [string, string][] = [
  ["clarity", "A reader new to the project can follow what is being built, for whom and why; terms are defined before use; each section stands on its own."],
  ["evidence_grounding", "Claims about today's system and every number carry evidence (a path:line citation at a commit, a measured source and date); proposals are marked as proposals, never stated as facts."],
  ["decision_completeness", "Every open question is an explicit decision with its options, owner and status; decided ones say who decided and when, and their consequences are stated."],
  ["term_consistency", "One concept keeps one name throughout; identifiers (D-numbers, PR numbers, table and column names) are used the same way everywhere."],
];
const SYSTEM = "You grade technical design documents against a fixed rubric. Score each criterion from 0 to 1 (0 = absent, 0.5 = partly met, " +
  "1 = fully met) and give a reason of at most two sentences that cites a concrete place in the document. Grade only what the text shows.";
const SCHEMA = { type: "object", additionalProperties: false, required: CRITERIA.map(([k]) => k),
  properties: Object.fromEntries(CRITERIA.map(([k]) => [k, { type: "object", additionalProperties: false, required: ["score", "reason"],
    properties: { score: { type: "number" }, reason: { type: "string" } } }])) };
const CHOICES = { A: 1.0, B: 0.75, C: 0.5, D: 0.25, E: 0.0 };

export const NAMES = CRITERIA.map(([k]) => `judge_${k}`);
export const METHOD = "LLM rubric (references/eval-loop.md, Judge): one structured-output call, 0..1 per criterion";

export function docText(build: string, content: Py): [string | null, string | null] {
  const p = path.join(build, "agents", "tdd.md");
  if (py.io.isfile(p)) return [readText(p), "build/agents/tdd.md"];
  if (py.truthy(content)) return [contentText(content), "content.json text (C.content_text)"];
  return [null, null];
}

export interface JudgeResult { scores: ScoreRec[]; ledger: L.LedgerRow[] }

export const judge = (build: string, content: Py, enabled: boolean, keys: L.LlmKeys) => Effect.gen(function* () {
  const ledger: L.LedgerRow[] = [];
  if (!enabled) return { scores: NAMES.map((nm) => notRun(nm, "pass --with-judge", METHOD, "judge")), ledger };
  const [text, src] = docText(build, content);
  if (!text) return { scores: NAMES.map((nm) => notRun(nm, "no document text (no agents/tdd.md, no content.json)", METHOD, "judge")), ledger };
  const [ok, why] = yield* L.claudeAvailable(keys);
  if (ok) {
    const user = `<document>\n${text}\n</document>\n\nRubric:\n` + CRITERIA.map(([k, v]) => `- ${k}: ${v}`).join("\n") + "\n\nReturn the JSON object.";
    const [data, err, row] = yield* L.claudeJson(keys, "judge", SYSTEM, user, SCHEMA);
    ledger.push(row);
    const m = { input_tokens: row.usage?.input_tokens ?? null, output_tokens: row.usage?.output_tokens ?? null };
    if (err) return { scores: NAMES.map((nm) => error(nm, err, METHOD + ` (${L.JUDGE_MODEL})`, src!, m)), ledger };
    const scores = CRITERIA.map(([k]) => {
      const c = get(data, k);
      const raw = get(c instanceof Map ? c : new Map(), "score", 0);
      const v = Math.max(0.0, Math.min(1.0, float(raw)));
      return score(`judge_${k}`, fl(v), METHOD + ` (${L.JUDGE_MODEL}, anthropic SDK, output_config json_schema; clamped to 0..1)`, src!,
        k === "clarity" ? m : null, { reason: get(c instanceof Map ? c : new Map(), "reason", "") });
    });
    return { scores, ledger };
  }
  const [ok2, why2] = yield* L.autoevalsAvailable(keys);
  if (ok2) {
    const scores: ScoreRec[] = [];
    for (const [k, crit] of CRITERIA) {
      const prompt = "You are grading a technical design document on one criterion.\nCriterion: " + crit +
        "\n\nDocument:\n{{output}}\n\nChoose one: (A) fully met, (B) mostly met, (C) partly met, (D) barely met, (E) absent.";
      const ex = yield* Effect.exit(L.autoevalsClassify(`judge_${k}`, prompt, CHOICES, text));
      if (Exit.isFailure(ex)) {
        const e: Py = Cause.squash(ex.cause);
        scores.push(error(`judge_${k}`, `autoevals raised ${e?.name ?? "Error"}: ${py.cpSlice(String(e?.message ?? e), 0, 160)}`, METHOD, src!));
        continue;
      }
      const r = ex.value;
      ledger.push({ purpose: `judge_${k}`, provider: "autoevals", model: r.model, at: now(), usage: null, usd: null,
        note: "autoevals does not expose token usage on the Score" });
      scores.push(score(`judge_${k}`, r.score !== null ? r.score : 0, METHOD + ` (autoevals LLMClassifier, ${r.model}; A..E -> 1..0)`, src!, null,
        { rationale: py.cpSlice(r.rationale, 0, 400) }));
    }
    return { scores, ledger };
  }
  return { scores: NAMES.map((nm) => notRun(nm, `${why}; ${why2}`, METHOD, src || "judge")), ledger };
});
