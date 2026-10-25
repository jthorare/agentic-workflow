import type { ZodType } from "zod";

import type { ContentClass, JsonSchemaFragment, QuestionRef } from "./types.js";

export interface QuestionModule<I, O extends string> {
  name: string;
  inputSchema: ZodType<I>;
  outputs: readonly O[];
  prompt: (input: I) => string;
  threshold: number;
  contentClass: ContentClass;
  timeBudgetMs: number;
  preRules?: (input: I) => O | null;
  // What the providers receive in place of the raw input: the same fields with
  // their free text capped as the prompt caps it. The prompt, the input digest
  // and the decision record still come from the raw input.
  providerInput?: (input: I) => unknown;
  // Cheap rule-based answer used only when a model answered below threshold
  // (distinct from preRules, which run before any model).
  fallbackRules?: (input: I) => O | null;
  extraProperties?: Readonly<Record<string, JsonSchemaFragment>>;
  // What each output means, for the adjudicator (and, from Task 5, Jev).
  criteria?: Readonly<Record<O, string>>;
}

export function toRef<I, O extends string>(q: QuestionModule<I, O>, input: I): QuestionRef<O> {
  const ref: QuestionRef<O> = { name: q.name, outputs: q.outputs, prompt: q.prompt(input), contentClass: q.contentClass };
  const withBound = q.providerInput === undefined ? ref : { ...ref, providerInput: q.providerInput as (input: unknown) => unknown };
  const withExtra = q.extraProperties === undefined ? withBound : { ...withBound, extraProperties: q.extraProperties };
  return q.criteria === undefined ? withExtra : { ...withExtra, criteria: q.criteria };
}

/** The input as a provider may use it: bounded when the question says how, else as given. Every provider calls this first. */
export function boundedInput<O extends string>(question: QuestionRef<O>, input: unknown): unknown {
  return question.providerInput?.(input) ?? input;
}
