import type { ContentClass, ProviderResult, QuestionRef } from "../types.js";

// Shared plumbing for the agent-CLI providers (claude-cli, codex-cli,
// cursor-cli). Each provider owns its own argv and output envelope; this file
// only holds the pieces that are genuinely identical across them.

export interface SpawnResult {
  stdout: string;
  code: number | null;
  timedOut: boolean;
  // The binary itself was not found (ENOENT). Distinct from a non-zero exit:
  // a CLI that isn't installed is "unavailable", not a failure worth counting.
  notFound?: boolean;
}

export interface Spawn {
  // stdin carries the user content (it can be far past the argv limit); absent, the child's stdin is closed at once.
  (args: string[], opts: { cwd: string; env: NodeJS.ProcessEnv; timeoutMs: number; stdin?: string }): Promise<SpawnResult>;
}

export const NON_IMAGE_CLASSES: readonly ContentClass[] = ["code", "diff", "brief", "transcript", "message-meta"];
export const ALL_CLASSES: readonly ContentClass[] = [...NON_IMAGE_CLASSES, "image"];

// Every agent CLI child gets AW_JUDGE_CHILD=1 so every aw:* hook exits 0
// immediately (recursion guard: a judge call must never trigger another).
export function childEnv(extra: Record<string, string> = {}): NodeJS.ProcessEnv {
  return { ...process.env, ...extra, AW_JUDGE_CHILD: "1" };
}

export interface ImageInput {
  afterScreenshot: string;
  baselineScreenshot: string | null;
  evidenceDir: string;
}

/** The question's decision enum plus any declared extra fields, all optional but decision. */
export function laxSchema<O extends string>(question: QuestionRef<O>): Record<string, unknown> {
  return {
    type: "object",
    properties: { decision: { type: "string", enum: question.outputs }, ...(question.extraProperties ?? {}) },
    required: ["decision"],
  };
}

/**
 * Strict-mode JSON Schema (OpenAI structured outputs, used by `codex exec
 * --output-schema`): every property must be listed in `required` and
 * `additionalProperties` must be false — verified against codex 0.158.0,
 * which rejects a schema without it (`invalid_json_schema`). Extras are
 * therefore made nullable instead of optional; toDecided() drops the nulls.
 */
export function strictSchema<O extends string>(question: QuestionRef<O>): Record<string, unknown> {
  const extras = question.extraProperties ?? {};
  const properties: Record<string, unknown> = { decision: { type: "string", enum: question.outputs } };
  for (const [key, fragment] of Object.entries(extras)) {
    const type = fragment.type;
    properties[key] = typeof type === "string" ? { ...fragment, type: [type, "null"] } : { anyOf: [fragment, { type: "null" }] };
  }
  return { type: "object", properties, required: Object.keys(properties), additionalProperties: false };
}

/**
 * Turns a parsed model response into a ProviderResult. Anything beyond
 * `decision` (ui-element-repair's chosenIndex, visual-critique's reasons)
 * rides through as `extra` — never part of evaluate()'s typed Decision<O>
 * contract (see types.ts). Null-valued extras (strict-schema placeholders)
 * are dropped.
 */
export function toDecided<O extends string>(parsed: unknown, reasonCode: string): ProviderResult<O> {
  if (typeof parsed !== "object" || parsed === null) return { status: "error", reason_code: "unparseable-result" };
  const { decision, ...rest } = parsed as Record<string, unknown>;
  if (typeof decision !== "string") return { status: "error", reason_code: "unparseable-result" };
  const extra: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(rest)) if (value !== null) extra[key] = value;
  return Object.keys(extra).length > 0
    ? { status: "decided", decision: decision as O, confidence: 1, reason_code: reasonCode, extra }
    : { status: "decided", decision: decision as O, confidence: 1, reason_code: reasonCode };
}

/**
 * Pulls the last parseable top-level JSON object out of free text. Needed for
 * CLIs with no schema-constrained output mode (cursor-agent): observed on
 * 2026.09.02, `.result` sometimes carries a prose preamble before the JSON
 * ("I'll open `after.png`...{"decision":"red"}").
 */
export function lastJsonObject(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    /* fall through to scanning */
  }
  const end = text.lastIndexOf("}");
  // Scan every "{" before the last "}" from right to left, so a nested
  // object's inner brace fails to parse and the enclosing one wins.
  for (let start = end - 1; start >= 0; start--) {
    if (text[start] !== "{") continue;
    try {
      return JSON.parse(text.slice(start, end + 1));
    } catch {
      /* keep scanning backwards for an enclosing brace */
    }
  }
  return undefined;
}

/** The common spawn-outcome gate every CLI provider applies before parsing stdout. */
export function spawnFailure(raw: SpawnResult): { status: "unavailable" | "error"; reason_code: string } | null {
  if (raw.notFound === true) return { status: "unavailable", reason_code: "binary-not-found" };
  if (raw.timedOut) return { status: "unavailable", reason_code: "timeout" };
  if (raw.code !== 0) return { status: "error", reason_code: `exit-${raw.code ?? "null"}` };
  return null;
}
