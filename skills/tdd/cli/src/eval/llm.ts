// ev_llm.py: the model calls the eval makes (judge and handoff probe) and the price table behind every cost line.
//
// Every call is gated on a key from the environment (ANTHROPIC_API_KEY for Claude; OPENAI_API_KEY or
// BRAINTRUST_API_KEY for autoevals) and on a flag (--with-judge, --with-agent-probe), exactly as in the Python, and
// returns the ledger row with the usage the API reported, so output.tokens.json accounts for the eval's own spend.
// The SDKs (@anthropic-ai/sdk, autoevals) are optional: imported lazily, only when a call is about to be made, and
// never installed by this package (`bun add --no-save @anthropic-ai/sdk autoevals` in skills/tdd/cli to use them).
import { Effect, Option } from "effect";
import * as py from "../py";
import { type Py, PyFloat, fl, get, importOptional, n, now } from "./common";
import { type Keys, reveal } from "./config";

export const JUDGE_MODEL = "claude-sonnet-5-5"; // ev_llm.JUDGE_MODEL

/** USD per million tokens, Anthropic first-party list prices (ev_llm.PRICES, same order, same values). */
export const PRICES: [string, { input: number; output: number; cache_read?: number }][] = [
  ["claude-fable-5-1", { input: 10.0, output: 50.0, cache_read: 0.25 }],
  ["claude-mythos-5-1", { input: 10.0, output: 50.0, cache_read: 0.25 }],
  ["claude-fable-5", { input: 10.0, output: 50.0 }],
  ["claude-opus-5-5", { input: 4.0, output: 20.0, cache_read: 0.20 }],
  ["claude-opus-5", { input: 5.0, output: 25.0 }],
  ["claude-opus-4-8", { input: 5.0, output: 25.0 }],
  ["claude-opus-4-7", { input: 5.0, output: 25.0 }],
  ["claude-opus-4-6", { input: 5.0, output: 25.0 }],
  ["claude-sonnet-5-5", { input: 2.0, output: 10.0, cache_read: 0.20 }],
  ["claude-sonnet-5", { input: 2.0, output: 10.0 }],
  ["claude-sonnet-4-6", { input: 3.0, output: 15.0 }],
  ["claude-haiku-5-5", { input: 0.10, output: 0.50 }],
  ["claude-haiku-4-5", { input: 1.0, output: 5.0 }],
];
export const PRICE_SOURCE = "claude-api skill model table (cached 2026-10-06) and its prompt-caching page; API list prices, not a subscription bill";

export function priceFor(model: Py): [string, (typeof PRICES)[number][1]] | null {
  if (!py.truthy(model)) return null;
  const base = String(model).split("[")[0]!;
  const keys = PRICES.map(([k], i) => [k, i] as const).sort((a, b) => b[0].length - a[0].length || a[1] - b[1]);
  for (const [k, i] of keys) if (base === k || base.startsWith(k + "-")) return [k, PRICES[i]![1]];
  return null;
}

/** usage.get(k) on a dict from a transcript (Map) or an object from an SDK. */
const u = (usage: Py, k: string): Py => (usage instanceof Map ? get(usage, k) : usage?.[k] ?? null);

/** USD for one usage record; None when the model is not priced (ev_llm.cost). */
export function cost(model: Py, usage: Py): PyFloat | null {
  const hit = priceFor(model);
  if (!hit) return null;
  const p = hit[1];
  const i = n(u(usage, "input_tokens"));
  const o = n(u(usage, "output_tokens"));
  const cr = n(u(usage, "cache_read_input_tokens"));
  const cc = py.truthy(u(usage, "cache_creation")) ? u(usage, "cache_creation") : new Map();
  let c5: Py = u(cc, "ephemeral_5m_input_tokens");
  let c1: Py = u(cc, "ephemeral_1h_input_tokens");
  if (c5 === null && c1 === null) {
    c5 = n(u(usage, "cache_creation_input_tokens"));
    c1 = 0;
  }
  const readRate = p.cache_read ?? p.input * 0.1;
  const usd = (i * p.input + o * p.output + cr * readRate + n(c5) * p.input * 1.25 + n(c1) * p.input * 2.0) / 1e6;
  return fl(py.round(usd, 6));
}

export interface LedgerRow {
  purpose: string; provider: string; model: string | null; at: string;
  error?: string; seconds?: PyFloat; usage?: Record<string, Py> | null; usd?: PyFloat | null; stop_reason?: string | null;
  response_model?: string | null; note?: string;
}

export type LlmKeys = Pick<Keys, "anthropic" | "openai" | "braintrust">;

const SDK = "@anthropic-ai/sdk";
const AUTOEVALS = "autoevals";
/** A lazily imported optional module, or null when it is not installed (common.importOptional; another load failure
 * is a defect the scorer's settle records as "raised ..."). */
export const optional = (spec: string): Effect.Effect<Py | null> => Effect.promise(() => importOptional(spec));

export const claudeAvailable = (keys: LlmKeys): Effect.Effect<[boolean, string | null]> => Effect.gen(function* () {
  if (Option.isNone(keys.anthropic)) return [false, "no ANTHROPIC_API_KEY"];
  if (!(yield* optional(SDK))) return [false, `${SDK} not installed (bun add --no-save ${SDK} in skills/tdd/cli)`];
  return [true, null];
});

export const autoevalsAvailable = (keys: LlmKeys): Effect.Effect<[boolean, string | null]> => Effect.gen(function* () {
  if (Option.isNone(keys.openai) && Option.isNone(keys.braintrust)) return [false, "no OPENAI_API_KEY or BRAINTRUST_API_KEY for autoevals"];
  if (!(yield* optional(AUTOEVALS))) return [false, `autoevals not installed (bun add --no-save autoevals in skills/tdd/cli)`];
  return [true, null];
});

const elapsed = (t0: number): PyFloat => fl(py.round((performance.now() - t0) / 1000, 2));

/** One Messages call with a JSON-schema output (output_config.format): [data | null, error | null, ledger row]. No
 * sampling parameters; thinking at the model default (ev_llm.claude_json). */
export const claudeJson = (keys: LlmKeys, purpose: string, system: string, user: string, schema: object, model = JUDGE_MODEL, maxTokens = 16000) =>
  Effect.gen(function* () {
    const mod = yield* optional(SDK);
    const Anthropic = mod.default;
    const client = new Anthropic({ apiKey: reveal(keys.anthropic) });
    const t0 = performance.now();
    const row: LedgerRow = { purpose, provider: "anthropic", model, at: now() };
    const res = yield* Effect.promise<{ ok: Py } | { err: Py }>(() => client.messages.create({
      model, max_tokens: maxTokens, system, messages: [{ role: "user", content: user }],
      output_config: { format: { type: "json_schema", schema } },
    }).then((r: Py) => ({ ok: r }), (e: Py) => ({ err: e })));
    const msg = (e: Py) => py.cpSlice(String(e?.message ?? e), 0, 160);
    if ("err" in res) {
      const e = res.err;
      if (e instanceof Anthropic.RateLimitError) row.error = `rate limited: ${msg(e)}`;
      else if (e instanceof Anthropic.APIConnectionError) row.error = `connection: ${msg(e)}`;
      else if (e instanceof Anthropic.APIError) row.error = `API ${e.status ?? "?"}: ${msg(e)}`;
      else return yield* Effect.die(e);
      row.seconds = elapsed(t0);
      return [null, row.error, row] as const;
    }
    const resp = res.ok;
    const us = resp.usage ?? {};
    const usage = { input_tokens: us.input_tokens || 0, output_tokens: us.output_tokens || 0,
      cache_read_input_tokens: us.cache_read_input_tokens || 0, cache_creation_input_tokens: us.cache_creation_input_tokens || 0 };
    Object.assign(row, { usage, usd: cost(resp.model || model, usage), stop_reason: resp.stop_reason, seconds: elapsed(t0), response_model: resp.model });
    if (resp.stop_reason === "refusal") return [null, "refusal (stop_reason)", row] as const;
    if (resp.stop_reason === "max_tokens") return [null, "hit max_tokens", row] as const;
    const block = (resp.content ?? []).find((b: Py) => b?.type === "text");
    try {
      return [py.json.loads(block ? block.text : ""), null, row] as const;
    } catch (e) {
      return [null, `response is not JSON (${(e as Error).message})`, row] as const;
    }
  });

/** autoevals' LLMClassifier over the document for one criterion (the judge's fallback without an Anthropic key). */
export const autoevalsClassify = (name: string, promptTemplate: string, choiceScores: Record<string, number>, output: string) =>
  Effect.gen(function* () {
    const mod = yield* optional(AUTOEVALS);
    const make = mod?.LLMClassifierFromTemplate;
    if (typeof make !== "function") return yield* Effect.die(new Error("autoevals exports no LLMClassifierFromTemplate"));
    const scorer = make({ name, promptTemplate, choiceScores, useCoT: true });
    const r: Py = yield* Effect.promise(() => Promise.resolve(scorer({ output })));
    return { score: r?.score ?? null, rationale: String(r?.metadata?.rationale ?? ""), model: String(mod.DEFAULT_MODEL ?? "autoevals default") };
  });
