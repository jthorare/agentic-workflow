import path from "node:path";

import { boundedInput } from "../question.js";
import type { Provider, ProviderResult, QuestionRef } from "../types.js";
import { ALL_CLASSES, childEnv, spawnFailure, strictSchema, toDecided, type ImageInput, type Spawn, type SpawnResult } from "./cli-common.js";

export const CODEX_MODEL = "gpt-6-luna";

// Features disabled for the child. Every name here must exist in the
// installed CLI — `codex exec --disable <unknown>` exits with "Unknown
// feature flag" (verified on 0.158.0), so keep this list short and stable.
// `hooks` is the load-bearing one (belt-and-braces with AW_JUDGE_CHILD=1);
// the rest trim the tool list (~4.7k fewer input tokens, ~2s faster).
const DISABLED_FEATURES = ["hooks", "apps", "plugins", "multi_agent", "shell_tool", "unified_exec"] as const;

interface CodexEvent {
  type?: unknown;
  item?: { type?: unknown; text?: unknown };
}

// `codex exec --json` prints one JSON event per line. Observed on 0.158.0:
//   {"type":"thread.started",...}
//   {"type":"turn.started"}
//   {"type":"item.completed","item":{"type":"agent_message","text":"{\"decision\":\"drop\"}"}}
//   {"type":"turn.completed","usage":{...}}
// and on a rejected request: {"type":"error",...} / {"type":"turn.failed",...}.
// The final agent_message text is the schema-constrained JSON answer.
function parseEvents<O extends string>(stdout: string): ProviderResult<O> {
  let lastMessage: string | undefined;
  let failed = false;
  for (const line of stdout.split("\n")) {
    if (line.trim() === "") continue;
    let event: CodexEvent;
    try {
      event = JSON.parse(line) as CodexEvent;
    } catch {
      continue;
    }
    if (event.type === "turn.failed" || event.type === "error") failed = true;
    if (event.type === "item.completed" && event.item?.type === "agent_message" && typeof event.item.text === "string") {
      lastMessage = event.item.text;
    }
  }
  if (lastMessage === undefined) return { status: "error", reason_code: failed ? "turn-failed" : "unparseable-output" };
  let parsed: unknown;
  try {
    parsed = JSON.parse(lastMessage);
  } catch {
    return { status: "error", reason_code: "unparseable-result" };
  }
  return toDecided<O>(parsed, "codex-cli");
}

/**
 * A small fast OpenAI model via the Codex CLI (`codex exec`), on the user's
 * ChatGPT login. Invocation verified against codex-cli 0.158.0 on 2026-09-28
 * (~3.4s wall, ~14k input tokens — Codex's own base instructions dominate):
 *
 *   codex exec --model gpt-6-luna -c model_reasoning_effort="low" --ephemeral
 *     --skip-git-repo-check --ignore-user-config --ignore-rules --disable <f>...
 *     --sandbox read-only -C <empty tmp> --output-schema <tmp>/schema.json
 *     --json [--image <abs>]...            (text: the prompt on stdin)
 *     --json [--image <abs>]... -- <prompt> (image: the prompt is short)
 *
 * Isolation: empty temp cwd (-C), --ignore-user-config (no user MCP servers,
 * profiles, or hooks config; auth still comes from CODEX_HOME), --ignore-rules,
 * --ephemeral (no session file), read-only sandbox, hooks feature disabled,
 * and AW_JUDGE_CHILD=1 (recursion guard).
 *
 * Structured output: --output-schema requires OpenAI strict mode (every
 * property required, additionalProperties false), so declared extras are
 * nullable — see strictSchema().
 *
 * Image: `-i/--image <FILE>...` attaches files to the prompt (verified: a
 * solid-red PNG was answered "red"). The flag is variadic, so the prompt is
 * always passed after `--`. Paths resolve against the question's evidenceDir;
 * cwd stays an empty temp dir since the CLI never needs to read the files.
 */
export function makeCodexCliProvider(deps: {
  spawn: Spawn;
  tmpDirFactory: () => string;
  writeFile: (file: string, contents: string) => void;
  model?: string;
}): Provider {
  return {
    name: "codex-cli",
    classes: new Set(ALL_CLASSES),
    decide: async <O extends string>(question: QuestionRef<O>, rawInput: unknown, budgetMs: number): Promise<ProviderResult<O>> => {
      const input = boundedInput(question, rawInput);
      const cwd = deps.tmpDirFactory();
      const schemaFile = path.join(cwd, "schema.json");
      const images: string[] = [];
      let prompt: string;
      let stdin: string | undefined;
      if (question.contentClass === "image") {
        const img = input as ImageInput;
        images.push("--image", path.resolve(img.evidenceDir, img.afterScreenshot));
        if (img.baselineScreenshot !== null) images.push("--image", path.resolve(img.evidenceDir, img.baselineScreenshot));
        prompt = `${question.prompt}\n\nThe screenshots are attached to this message as images, in the order they are named above.`;
      } else {
        // Passed on stdin (`codex exec` reads the prompt from there when none is given): a big input would overflow argv.
        stdin = `${question.prompt}\n\nInput:\n${JSON.stringify(input)}`;
        prompt = "";
      }

      let raw: SpawnResult;
      try {
        deps.writeFile(schemaFile, JSON.stringify(strictSchema(question)));
        raw = await deps.spawn([
          "exec",
          "--model", deps.model ?? CODEX_MODEL,
          "-c", 'model_reasoning_effort="low"',
          "--ephemeral",
          "--skip-git-repo-check",
          "--ignore-user-config",
          "--ignore-rules",
          ...DISABLED_FEATURES.flatMap((f) => ["--disable", f]),
          "--sandbox", "read-only",
          "-C", cwd,
          "--output-schema", schemaFile,
          "--json",
          ...images,
          ...(stdin === undefined ? ["--", prompt] : []),
        ], { cwd, env: childEnv(), timeoutMs: budgetMs, ...(stdin === undefined ? {} : { stdin }) });
      } catch {
        return { status: "error", reason_code: "spawn-failed" };
      }
      return spawnFailure(raw) ?? parseEvents<O>(raw.stdout);
    },
  };
}
