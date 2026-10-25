import { boundedInput } from "../question.js";
import type { Provider, ProviderResult, QuestionRef } from "../types.js";
import { callJev, type Fetch } from "./jev-api.js";

export type { Fetch } from "./jev-api.js";

const QUESTION_KEY = "decision";

/** Single-question Provider adapter over callJev (see jev-api.ts for the API notes). */
export function makeJevProvider(deps: { fetch: Fetch; apiKey: () => Promise<string | null> }): Provider {
  return {
    name: "jev",
    classes: new Set(["message-meta", "code", "diff", "brief", "transcript"]),
    decide: async <O extends string>(question: QuestionRef<O>, rawInput: unknown, budgetMs: number): Promise<ProviderResult<O>> => {
      const input = boundedInput(question, rawInput);
      const criteria: Record<string, string> = {};
      for (const option of question.outputs) criteria[option] = question.criteria?.[option] ?? option;
      const out = await callJev(deps, input, { [QUESTION_KEY]: { type: "choice", instructions: question.prompt, criteria } }, budgetMs);
      if (out.status !== "ok") return out;
      const result = out.answers[QUESTION_KEY] as NonNullable<(typeof out.answers)[string]>;
      if (!result.ok || result.answer.type !== "choice") return { status: "error", reason_code: "unparseable-result" };
      return { status: "decided", decision: result.answer.choice as O, confidence: result.answer.confidence, reason_code: "jev", probabilities: result.answer.probabilities };
    },
  };
}
