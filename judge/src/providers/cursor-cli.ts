import path from "node:path";

import { boundedInput } from "../question.js";
import type { Provider, ProviderResult, QuestionRef } from "../types.js";
import { ALL_CLASSES, childEnv, laxSchema, lastJsonObject, spawnFailure, toDecided, type ImageInput, type Spawn, type SpawnResult } from "./cli-common.js";

export const CURSOR_MODEL = "gpt-5.6-luna-none-fast";

// `cursor-agent -p --output-format json` prints a single envelope. Observed on
// 2026.09.02:
//   {"type":"result","subtype":"success","is_error":false,"duration_ms":3369,
//    "result":"{\"decision\":\"send\"}","session_id":"...","usage":{...}}
// `.result` is the model's free text — the CLI has no schema flag — so the
// decision is the last JSON object in it (a prose preamble has been observed).
function parseEnvelope<O extends string>(stdout: string): ProviderResult<O> {
  let envelope: unknown;
  try {
    envelope = JSON.parse(stdout);
  } catch {
    return { status: "error", reason_code: "unparseable-output" };
  }
  if (typeof envelope !== "object" || envelope === null) return { status: "error", reason_code: "unparseable-output" };
  const { is_error: isError, result } = envelope as { is_error?: unknown; result?: unknown };
  if (isError === true) return { status: "error", reason_code: "cli-reported-error" };
  if (typeof result !== "string") return { status: "error", reason_code: "unparseable-result" };
  return toDecided<O>(lastJsonObject(result), "cursor-cli");
}

function jsonInstruction<O extends string>(question: QuestionRef<O>): string {
  return `Respond with only a single JSON object matching this JSON Schema, and no other text:\n${JSON.stringify(laxSchema(question))}`;
}

/**
 * A small fast model via the Cursor CLI (`cursor-agent -p`), on the user's
 * Cursor login. Invocation verified against cursor-agent 2026.09.02 on
 * 2026-09-28:
 *
 *   cursor-agent -p --output-format json --model gpt-5.6-luna-none-fast
 *     --mode ask --sandbox enabled --trust --workspace <dir> <prompt>
 *
 * Latency: ~8-11s wall (the API call itself is ~3-4s; the rest is fixed CLI
 * startup), noticeably slower than claude-cli/codex-cli — which is why it is
 * last in the default agent-CLI order (detect.ts).
 *
 * Isolation: --workspace is an empty temp dir (the evidence dir for images),
 * --mode ask is read-only, --sandbox enabled, MCP servers are never
 * auto-approved (no --approve-mcps), and AW_JUDGE_CHILD=1 makes every aw:*
 * hook exit 0 (recursion guard). --trust is required: without it a fresh
 * workspace exits 1 with "Workspace Trust Required" in headless mode.
 *
 * Structured output: none available, so the schema is stated in the prompt
 * and the answer is parsed leniently (lastJsonObject); evaluate() still
 * rejects anything outside the question's outputs enum.
 *
 * Image: verified the agent reads a PNG from its workspace with its read tool
 * (solid-blue and solid-green PNGs were each named correctly; an end-to-end
 * `judge visual-critique` run returned a grounded critique), so the image
 * branch runs with --workspace set to the evidence dir, like claude-cli's
 * Read-tool branch, and names each file by absolute path.
 */
export function makeCursorCliProvider(deps: { spawn: Spawn; tmpDirFactory: () => string; model?: string }): Provider {
  return {
    name: "cursor-cli",
    classes: new Set(ALL_CLASSES),
    decide: async <O extends string>(question: QuestionRef<O>, rawInput: unknown, budgetMs: number): Promise<ProviderResult<O>> => {
      const input = boundedInput(question, rawInput);
      let workspace: string;
      let prompt: string;
      if (question.contentClass === "image") {
        const img = input as ImageInput;
        workspace = img.evidenceDir;
        // Absolute paths + "do not search": observed that given only a bare
        // filename the agent reaches for its glob tool, which finds nothing
        // in a temp dir and it then reports the file missing; told the path,
        // it goes straight to its read tool (which reads images).
        const files = [img.afterScreenshot, ...(img.baselineScreenshot === null ? [] : [img.baselineScreenshot])]
          .map((f) => path.resolve(img.evidenceDir, f));
        prompt = `${question.prompt}\n\nOpen each screenshot directly with your file-read tool (it reads images), at these paths — do not search for them: ${files.join(", ")}\n\n${jsonInstruction(question)}`;
      } else {
        workspace = deps.tmpDirFactory();
        prompt = `${question.prompt}\n\nInput:\n${JSON.stringify(input)}\n\n${jsonInstruction(question)}`;
      }

      let raw: SpawnResult;
      try {
        raw = await deps.spawn([
          "-p",
          "--output-format", "json",
          "--model", deps.model ?? CURSOR_MODEL,
          "--mode", "ask",
          "--sandbox", "enabled",
          "--trust",
          "--workspace", workspace,
          prompt,
        ], { cwd: workspace, env: childEnv(), timeoutMs: budgetMs });
      } catch {
        return { status: "error", reason_code: "spawn-failed" };
      }
      return spawnFailure(raw) ?? parseEnvelope<O>(raw.stdout);
    },
  };
}
