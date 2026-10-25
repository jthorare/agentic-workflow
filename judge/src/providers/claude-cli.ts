import { boundedInput } from "../question.js";
import type { Provider, ProviderResult, QuestionRef } from "../types.js";
import { ALL_CLASSES, childEnv, laxSchema, spawnFailure, toDecided, type ImageInput, type Spawn, type SpawnResult } from "./cli-common.js";

export type { Spawn, SpawnResult } from "./cli-common.js";

// Verified 2026-09-27 (team lead): `claude -p` accepts an image via the Read
// tool, with cwd scoped to the evidence dir (Plan 6, Task 5). Without this,
// providersFor(DEFAULT_CHAIN, "image", ...) filters claude-cli out entirely
// (Provider.classes gates every content class candidate — chain.ts, Task 3)
// and visual-critique can never be reached at all (review fix #1, BLOCKER).

// .structured_output is the primary path (validated against the schema by
// the CLI itself). Fall back to parsing .result as JSON only when
// .structured_output is absent — never trust free-form prose.
function parseEnvelope<O extends string>(stdout: string): ProviderResult<O> {
  let envelope: unknown;
  try {
    envelope = JSON.parse(stdout);
  } catch {
    return { status: "error", reason_code: "unparseable-output" };
  }
  let parsed: unknown = (envelope as { structured_output?: unknown }).structured_output;
  if (parsed === undefined) {
    const resultField = (envelope as { result?: unknown }).result;
    if (typeof resultField !== "string") return { status: "error", reason_code: "unparseable-result" };
    try {
      parsed = JSON.parse(resultField);
    } catch {
      return { status: "error", reason_code: "unparseable-result" };
    }
  }
  return toDecided<O>(parsed, "claude-cli");
}

/**
 * Haiku via the Claude Code CLI, on the user's subscription (no API key on this box).
 * Exact invocation the user measured 2026-09-26 (~2.7s wall, ~0.65s API, ~1.5k input
 * tokens with MAX_THINKING_TOKENS=0; without it, ~15s), confirmed against the
 * installed CLI (claude --version 2.1.283). Built from the question's own
 * outputs enum, so every question gets a schema for free. Runs from an empty
 * temp cwd so the child never inherits this repo's project context. Sets
 * AW_JUDGE_CHILD=1 so every aw:* hook exits 0 immediately (recursion guard).
 * `--setting-sources ""` loads no user, project or local settings and no
 * CLAUDE.md, so a settings `env` block can't re-inject what childEnv dropped
 * (same as sindri's model runner). Managed (policy) settings still apply.
 */
export function makeClaudeCliProvider(deps: { spawn: Spawn; tmpDirFactory: () => string; model?: string; effort?: string }): Provider {
  // The default (hook-chain) path is fast: thinking off. A caller that asks
  // for a non-default effort (the adjudicator's opus/high) wants the model to
  // actually think, so neither the env cap nor alwaysThinkingEnabled:false apply.
  const fast = deps.effort === undefined || deps.effort === "low";
  const settings = fast ? '{"disableAllHooks":true,"alwaysThinkingEnabled":false}' : '{"disableAllHooks":true}';
  const run = async <O extends string>(args: string[], cwd: string, budgetMs: number, stdin?: string): Promise<ProviderResult<O>> => {
    let raw: SpawnResult;
    try {
      raw = await deps.spawn(args, { cwd, env: childEnv(fast ? { MAX_THINKING_TOKENS: "0" } : {}), timeoutMs: budgetMs, ...(stdin === undefined ? {} : { stdin }) });
    } catch {
      return { status: "error", reason_code: "spawn-failed" };
    }
    return spawnFailure(raw) ?? parseEnvelope<O>(raw.stdout);
  };

  return {
    name: "claude-cli",
    classes: new Set(ALL_CLASSES),
    decide: async <O extends string>(question: QuestionRef<O>, rawInput: unknown, budgetMs: number): Promise<ProviderResult<O>> => {
      const input = boundedInput(question, rawInput);
      // Image branch (Task 5, F8): uses the Read tool instead of the
      // --tools "" structured-text invocation, with cwd scoped to the run's
      // own evidence directory — the CLI's file-access sandbox is scoped to
      // cwd, and an --allowedTools glob does not override that (an absolute
      // path outside cwd is denied even when the glob would otherwise match
      // it). Screenshots already live under this cwd (run-script.ts writes
      // them into runDir, which becomes this cwd).
      if (question.contentClass === "image") {
        const img = input as ImageInput;
        // Always asks for `reasons`, even from a question that didn't declare it.
        const schema = laxSchema(question);
        const props = schema.properties as Record<string, unknown>;
        const imgSchema = { ...schema, properties: { ...props, reasons: props.reasons ?? { type: "array", items: { type: "string" } } } };
        return run<O>([
          "-p", "--model", deps.model ?? "haiku", "--effort", deps.effort ?? "low", "--no-session-persistence",
          "--strict-mcp-config", "--mcp-config", '{"mcpServers":{}}',
          "--setting-sources", "",
          "--settings", settings,
          "--disable-slash-commands",
          "--tools", "Read", "--allowedTools", "Read(./**)",
          "--json-schema", JSON.stringify(imgSchema),
          "--system-prompt", "You inspect screenshots. Use the Read tool on the given path, then answer.",
          "--output-format", "json",
          question.prompt,
        ], img.evidenceDir, budgetMs);
      }

      return run<O>([
        "-p",
        "--model", deps.model ?? "haiku",
        "--effort", deps.effort ?? "low",
        "--no-session-persistence",
        "--strict-mcp-config",
        "--mcp-config", '{"mcpServers":{}}',
        "--setting-sources", "",
        "--settings", settings,
        "--disable-slash-commands",
        "--tools", "",
        "--system-prompt", question.prompt,
        "--json-schema", JSON.stringify(laxSchema(question)),
        "--output-format", "json",
      ], deps.tmpDirFactory(), budgetMs, JSON.stringify(input)); // no prompt argument: `claude -p` reads it from stdin
    },
  };
}
