import { describe, expect, it, vi } from "vitest";

import { CODEX_MODEL, makeCodexCliProvider } from "../../src/providers/codex-cli.js";
import type { QuestionRef } from "../../src/types.js";

const question: QuestionRef<"send" | "batch" | "drop"> = {
  name: "wake-gate", outputs: ["send", "batch", "drop"], prompt: "Classify this message.", contentClass: "message-meta",
};

const imageQuestion: QuestionRef<"looks-right" | "looks-off" | "sloppy"> = {
  name: "visual-critique", outputs: ["looks-right", "looks-off", "sloppy"], prompt: "Look at the screenshot after.png.", contentClass: "image",
  extraProperties: { reasons: { type: "array", items: { type: "string" } } },
};

// The exact JSONL shape observed from `codex exec --json` on 0.158.0.
function events(text: string): string {
  return [
    JSON.stringify({ type: "thread.started", thread_id: "t1" }),
    JSON.stringify({ type: "turn.started" }),
    JSON.stringify({ type: "item.completed", item: { id: "item_0", type: "agent_message", text } }),
    JSON.stringify({ type: "turn.completed", usage: { input_tokens: 13864, output_tokens: 15 } }),
  ].join("\n") + "\n";
}

function setup(stdout: string | Error, overrides: { code?: number | null; timedOut?: boolean; notFound?: boolean } = {}) {
  const spawn = stdout instanceof Error
    ? vi.fn().mockRejectedValue(stdout)
    : vi.fn().mockResolvedValue({ stdout, code: overrides.code === undefined ? 0 : overrides.code, timedOut: overrides.timedOut ?? false, notFound: overrides.notFound });
  const writeFile = vi.fn();
  const provider = makeCodexCliProvider({ spawn, writeFile, tmpDirFactory: () => "/tmp/judge-codex" });
  return { spawn, writeFile, provider };
}

describe("codex-cli provider", () => {
  it("is named codex-cli and covers every content class, including image", () => {
    const { provider } = setup("");
    expect(provider.name).toBe("codex-cli");
    for (const cls of ["code", "diff", "brief", "transcript", "message-meta", "image"] as const) expect(provider.classes.has(cls)).toBe(true);
  });

  it("invokes exactly the verified `codex exec` shape from an empty temp cwd, with the recursion guard set", async () => {
    const { spawn, provider } = setup(events('{"decision":"send"}'));
    await provider.decide(question, { text: "hi" }, 5000);
    expect(spawn).toHaveBeenCalledTimes(1);
    const [args, opts] = spawn.mock.calls[0] as [string[], { cwd: string; env: NodeJS.ProcessEnv; timeoutMs: number; stdin?: string }];
    expect(args).toEqual([
      "exec", "--model", CODEX_MODEL, "-c", 'model_reasoning_effort="low"',
      "--ephemeral", "--skip-git-repo-check", "--ignore-user-config", "--ignore-rules",
      "--disable", "hooks", "--disable", "apps", "--disable", "plugins", "--disable", "multi_agent",
      "--disable", "shell_tool", "--disable", "unified_exec",
      "--sandbox", "read-only", "-C", "/tmp/judge-codex",
      "--output-schema", "/tmp/judge-codex/schema.json", "--json",
    ]);
    expect(opts.stdin).toBe('Classify this message.\n\nInput:\n{"text":"hi"}');
    expect(opts.cwd).toBe("/tmp/judge-codex");
    expect(opts.env.AW_JUDGE_CHILD).toBe("1");
    expect(opts.timeoutMs).toBe(5000);
  });

  it("uses a model override when given", async () => {
    const spawn = vi.fn().mockResolvedValue({ stdout: events('{"decision":"send"}'), code: 0, timedOut: false });
    const provider = makeCodexCliProvider({ spawn, writeFile: vi.fn(), tmpDirFactory: () => "/tmp/x", model: "gpt-6-sol" });
    await provider.decide(question, {}, 5000);
    const [args] = spawn.mock.calls[0] as [string[]];
    expect(args[args.indexOf("--model") + 1]).toBe("gpt-6-sol");
  });

  it("writes a strict-mode schema (all required, additionalProperties false) built from the outputs enum", async () => {
    const { writeFile, provider } = setup(events('{"decision":"send"}'));
    await provider.decide(question, {}, 5000);
    expect(writeFile).toHaveBeenCalledWith("/tmp/judge-codex/schema.json", JSON.stringify({
      type: "object",
      properties: { decision: { type: "string", enum: ["send", "batch", "drop"] } },
      required: ["decision"],
      additionalProperties: false,
    }));
  });

  it("declares extras as nullable required fields in the strict schema", async () => {
    const { writeFile, provider } = setup(events('{"decision":"repaired","chosenIndex":2}'));
    const q: QuestionRef<"repaired" | "no-good-candidate"> = {
      name: "ui-element-repair", outputs: ["repaired", "no-good-candidate"], prompt: "Pick.", contentClass: "code",
      extraProperties: { chosenIndex: { type: "integer" } },
    };
    const result = await provider.decide(q, {}, 10000);
    const schema = JSON.parse((writeFile.mock.calls[0] as [string, string])[1]) as { properties: Record<string, unknown>; required: string[] };
    expect(schema.properties.chosenIndex).toEqual({ type: ["integer", "null"] });
    expect(schema.required).toEqual(["decision", "chosenIndex"]);
    expect(result).toEqual({ status: "decided", decision: "repaired", confidence: 1, reason_code: "codex-cli", extra: { chosenIndex: 2 } });
  });

  it("drops a null extra (strict-schema placeholder) rather than surfacing it", async () => {
    const { provider } = setup(events('{"decision":"no-good-candidate","chosenIndex":null}'));
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "decided", decision: "no-good-candidate", confidence: 1, reason_code: "codex-cli" });
  });

  it("parses the decision from the last agent_message event", async () => {
    const stdout = [
      JSON.stringify({ type: "item.completed", item: { type: "agent_message", text: '{"decision":"send"}' } }),
      "not json at all",
      JSON.stringify({ type: "item.completed", item: { type: "reasoning", text: "thinking" } }),
      JSON.stringify({ type: "item.completed", item: { type: "agent_message", text: '{"decision":"batch"}' } }),
    ].join("\n");
    const { provider } = setup(stdout);
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "decided", decision: "batch", confidence: 1, reason_code: "codex-cli" });
  });

  it("is an error (turn-failed) when the turn failed and no agent_message arrived", async () => {
    const stdout = [
      JSON.stringify({ type: "turn.started" }),
      JSON.stringify({ type: "error", message: "invalid_json_schema" }),
      JSON.stringify({ type: "turn.failed", error: { message: "x" } }),
    ].join("\n");
    const { provider } = setup(stdout);
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "turn-failed" });
  });

  it("is an error (unparseable-output) when stdout has no agent_message at all", async () => {
    const { provider } = setup("garbage\n");
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-output" });
  });

  it("ignores an agent_message whose text is not a string", async () => {
    const { provider } = setup(JSON.stringify({ type: "item.completed", item: { type: "agent_message", text: 5 } }));
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-output" });
  });

  it("ignores an item.completed event without an item", async () => {
    const { provider } = setup(JSON.stringify({ type: "item.completed" }));
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-output" });
  });

  it("is an error (unparseable-result) when the agent_message is not JSON", async () => {
    const { provider } = setup(events("I think send."));
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("is an error (unparseable-result) when the answer has no decision field", async () => {
    const { provider } = setup(events("{}"));
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("is an error (unparseable-result) when the answer is JSON but not an object", async () => {
    const { provider } = setup(events("null"));
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("is unavailable, not an error, on timeout", async () => {
    const { provider } = setup("", { code: null, timedOut: true });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "unavailable", reason_code: "timeout" });
  });

  it("is unavailable when the codex binary is not installed", async () => {
    const { provider } = setup("", { code: null, notFound: true });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "unavailable", reason_code: "binary-not-found" });
  });

  it("is an error on a non-zero exit", async () => {
    const { provider } = setup("", { code: 1 });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "exit-1" });
  });

  it("is an error with exit-null when the exit code is null but not a timeout", async () => {
    const { provider } = setup("", { code: null });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "exit-null" });
  });

  it("propagates a spawn rejection as an error, not a throw", async () => {
    const { provider } = setup(new Error("EACCES"));
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "spawn-failed" });
  });

  it("is an error, not a throw, when the schema file cannot be written", async () => {
    const spawn = vi.fn();
    const provider = makeCodexCliProvider({ spawn, tmpDirFactory: () => "/tmp/x", writeFile: () => { throw new Error("ENOSPC"); } });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "spawn-failed" });
    expect(spawn).not.toHaveBeenCalled();
  });
});

describe("codex-cli provider — image branch", () => {
  it("attaches the after and baseline screenshots via --image (resolved against evidenceDir), prompt after --", async () => {
    const { spawn, provider } = setup(events('{"decision":"looks-right","reasons":[]}'));
    await provider.decide(imageQuestion, { afterScreenshot: "after.png", baselineScreenshot: "/abs/main.png", evidenceDir: "/tmp/run-42" }, 20000);
    const [args, opts] = spawn.mock.calls[0] as [string[], { cwd: string; timeoutMs: number }];
    const dashDash = args.indexOf("--");
    expect(args.slice(dashDash - 4, dashDash)).toEqual(["--image", "/tmp/run-42/after.png", "--image", "/abs/main.png"]);
    expect(args[dashDash + 1]).toContain("Look at the screenshot after.png.");
    expect(args[dashDash + 1]).toContain("attached to this message as images");
    expect(args[dashDash + 1]).not.toContain("Input:");
    // cwd stays the empty temp dir — codex never needs to read the evidence dir itself.
    expect(opts.cwd).toBe("/tmp/judge-codex");
    expect(opts.timeoutMs).toBe(20000);
  });

  it("attaches only the after screenshot when there is no baseline", async () => {
    const { spawn, provider } = setup(events('{"decision":"looks-right","reasons":[]}'));
    await provider.decide(imageQuestion, { afterScreenshot: "after.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000);
    const [args] = spawn.mock.calls[0] as [string[]];
    expect(args.filter((a) => a === "--image")).toHaveLength(1);
  });

  it("threads reasons through as extra on a decided image result", async () => {
    const { provider } = setup(events('{"decision":"looks-off","reasons":["misaligned button"]}'));
    const result = await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000);
    expect(result).toEqual({ status: "decided", decision: "looks-off", confidence: 1, reason_code: "codex-cli", extra: { reasons: ["misaligned button"] } });
  });

  it("is unavailable on timeout for the image branch too", async () => {
    const { provider } = setup("", { code: null, timedOut: true });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "unavailable", reason_code: "timeout" });
  });

  it("is an error on malformed output for the image branch", async () => {
    const { provider } = setup(events("looks fine to me"));
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });
});
