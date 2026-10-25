import { describe, expect, it, vi } from "vitest";

import { makeClaudeCliProvider } from "../../src/providers/claude-cli.js";
import type { QuestionRef } from "../../src/types.js";

const question: QuestionRef<"send" | "batch" | "drop"> = {
  name: "wake-gate", outputs: ["send", "batch", "drop"], prompt: "Classify this message.", contentClass: "message-meta",
};

describe("claude-cli provider", () => {
  it("has claude-cli in its name and covers every content class, including image (Plan 6, review fix #1)", () => {
    const provider = makeClaudeCliProvider({ spawn: vi.fn(), tmpDirFactory: () => "/tmp/x" });
    expect(provider.name).toBe("claude-cli");
    for (const cls of ["code", "diff", "brief", "transcript", "message-meta", "image"] as const) expect(provider.classes.has(cls)).toBe(true);
  });

  it("invokes exactly the measured command shape, including --json-schema built from the question's outputs", async () => {
    const spawn = vi.fn().mockResolvedValue({
      code: 0, timedOut: false,
      stdout: JSON.stringify({ structured_output: { decision: "send" }, result: JSON.stringify({ decision: "send" }) }),
    });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/judge-abc" });
    await provider.decide(question, { text: "hi" }, 5000);
    expect(spawn).toHaveBeenCalledTimes(1);
    const [args, opts] = spawn.mock.calls[0] as [string[], { cwd: string; env: NodeJS.ProcessEnv; timeoutMs: number; stdin?: string }];
    expect(args).toEqual([
      "-p", "--model", "haiku", "--effort", "low", "--no-session-persistence",
      "--strict-mcp-config", "--mcp-config", '{"mcpServers":{}}',
      "--setting-sources", "",
      "--settings", '{"disableAllHooks":true,"alwaysThinkingEnabled":false}',
      "--disable-slash-commands", "--tools", "",
      "--system-prompt", "Classify this message.",
      "--json-schema", '{"type":"object","properties":{"decision":{"type":"string","enum":["send","batch","drop"]}},"required":["decision"]}',
      "--output-format", "json",
    ]);
    expect(opts.stdin).toBe(JSON.stringify({ text: "hi" }));
    expect(opts.cwd).toBe("/tmp/judge-abc");
    expect(opts.env.MAX_THINKING_TOKENS).toBe("0");
    expect(opts.env.AW_JUDGE_CHILD).toBe("1");
    expect(opts.timeoutMs).toBe(5000);
  });

  it("passes the budgetMs argument through as the child timeout, not a hardcoded 5000", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "send" } }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    await provider.decide(question, {}, 1234);
    const [, opts] = spawn.mock.calls[0] as [string[], { timeoutMs: number }];
    expect(opts.timeoutMs).toBe(1234);
  });

  it("parses the decision out of .structured_output first", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "batch" }, result: JSON.stringify({ decision: "send" }) }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    const result = await provider.decide(question, {}, 5000);
    expect(result).toEqual({ status: "decided", decision: "batch", confidence: 1, reason_code: "claude-cli" });
  });

  it("falls back to parsing .result as JSON when .structured_output is absent", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ result: JSON.stringify({ decision: "batch" }) }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    const result = await provider.decide(question, {}, 5000);
    expect(result).toEqual({ status: "decided", decision: "batch", confidence: 1, reason_code: "claude-cli" });
  });

  it("is unavailable, not an error, on timeout (RF-1)", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: null, timedOut: true, stdout: "" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "unavailable", reason_code: "timeout" });
  });

  it("is an error with reason exit-null when the exit code itself is null (not a timeout)", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: null, timedOut: false, stdout: "" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "exit-null" });
  });

  it("is an error when there is no structured_output and no result field at all", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({}) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("is an error on a non-zero exit", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 1, timedOut: false, stdout: "" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "exit-1" });
  });

  it("is an error when stdout is not the expected JSON envelope", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: "not json" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-output" });
  });

  it("is an error when neither .structured_output nor a parseable .result is present (RF-3)", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ result: "not json" }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("is an error when .structured_output is present but has no decision field", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: {} }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("is an error when the parsed .result has no decision field and there is no structured_output", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ result: JSON.stringify({}) }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("propagates a spawn rejection as an error, not a throw (RF-1)", async () => {
    const spawn = vi.fn().mockRejectedValue(new Error("ENOENT"));
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "error", reason_code: "spawn-failed" });
  });
});

describe("claude-cli provider — extra fields (chosenIndex, reasons)", () => {
  it("threads any field beyond decision through as extra", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "batch", chosenIndex: 2 } }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    const result = await provider.decide(question, {}, 5000);
    expect(result).toEqual({ status: "decided", decision: "batch", confidence: 1, reason_code: "claude-cli", extra: { chosenIndex: 2 } });
  });
});

describe("claude-cli provider — image branch (Task 5, review fix #5)", () => {
  const imageQuestion: QuestionRef<"looks-right" | "looks-off" | "sloppy"> = {
    name: "visual-critique", outputs: ["looks-right", "looks-off", "sloppy"], prompt: "Use the Read tool to look at after.png.", contentClass: "image",
  };

  it("uses the Read tool, cwd = evidenceDir, and closed stdin (verified invocation)", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "looks-right", reasons: [] } }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/judge-abc" });
    await provider.decide(imageQuestion, { afterScreenshot: "after.png", baselineScreenshot: null, evidenceDir: "/tmp/run-42" }, 20000);
    expect(spawn).toHaveBeenCalledTimes(1);
    const [args, opts] = spawn.mock.calls[0] as [string[], { cwd: string; timeoutMs: number }];
    expect(args).toContain("--tools");
    expect(args[args.indexOf("--tools") + 1]).toBe("Read");
    expect(args).toContain("--allowedTools");
    expect(args[args.indexOf("--allowedTools") + 1]).toBe("Read(./**)");
    expect(opts.cwd).toBe("/tmp/run-42");
    expect(opts.timeoutMs).toBe(20000);
  });

  it("loads no user, project or local settings (--setting-sources \"\"), like the text branch", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "looks-right" } }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000);
    const [args] = spawn.mock.calls[0] as [string[]];
    expect(args).toContain("--setting-sources");
    expect(args[args.indexOf("--setting-sources") + 1]).toBe("");
  });

  it("does not use the --tools \"\" text-branch invocation for an image question", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "looks-right" } }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000);
    const [args] = spawn.mock.calls[0] as [string[]];
    const toolsIndex = args.indexOf("--tools");
    expect(args[toolsIndex + 1]).not.toBe("");
  });

  it("threads reasons through as extra on a decided image result", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "looks-off", reasons: ["misaligned button"] } }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    const result = await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000);
    expect(result).toEqual({ status: "decided", decision: "looks-off", confidence: 1, reason_code: "claude-cli", extra: { reasons: ["misaligned button"] } });
  });

  it("is unavailable on timeout for the image branch too", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: null, timedOut: true, stdout: "" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "unavailable", reason_code: "timeout" });
  });

  it("is an error on a non-zero exit for the image branch", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 1, timedOut: false, stdout: "" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "error", reason_code: "exit-1" });
  });

  it("is an error when stdout is unparseable for the image branch", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: "not json" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "error", reason_code: "unparseable-output" });
  });

  it("falls back to parsing .result when .structured_output is absent for the image branch", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ result: JSON.stringify({ decision: "sloppy" }) }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "decided", decision: "sloppy", confidence: 1, reason_code: "claude-cli" });
  });

  it("is an error when neither structured_output nor a parseable result field is present for the image branch", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ result: "not json" }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("is an error when structured_output has no decision field for the image branch", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: {} }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });

  it("propagates a spawn rejection as an error for the image branch", async () => {
    const spawn = vi.fn().mockRejectedValue(new Error("ENOENT"));
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "error", reason_code: "spawn-failed" });
  });
});

describe("claude-cli provider — image branch, remaining branches", () => {
  const imageQuestion: QuestionRef<"looks-right" | "looks-off" | "sloppy"> = {
    name: "visual-critique", outputs: ["looks-right", "looks-off", "sloppy"], prompt: "Use the Read tool.", contentClass: "image",
  };

  it("is an error with exit-null when the exit code is null but not a timeout, for the image branch", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: null, timedOut: false, stdout: "" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "error", reason_code: "exit-null" });
  });

  it("is an error when neither structured_output nor a result field exists at all for the image branch", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({}) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(imageQuestion, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "error", reason_code: "unparseable-result" });
  });
});

describe("claude-cli provider — shared CLI contract additions", () => {
  it("is unavailable, not an error, when the claude binary is not installed (text branch)", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: null, timedOut: false, notFound: true, stdout: "" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    expect(await provider.decide(question, {}, 5000)).toEqual({ status: "unavailable", reason_code: "binary-not-found" });
  });

  it("is unavailable when the claude binary is not installed (image branch)", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: null, timedOut: false, notFound: true, stdout: "" });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    const imageQ: QuestionRef<"looks-right"> = { name: "visual-critique", outputs: ["looks-right"], prompt: "p", contentClass: "image" };
    expect(await provider.decide(imageQ, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000)).toEqual({ status: "unavailable", reason_code: "binary-not-found" });
  });

  it("declares a question's extraProperties in --json-schema (ui-element-repair's chosenIndex)", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "repaired", chosenIndex: 1 } }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    const q: QuestionRef<"repaired" | "no-good-candidate"> = {
      name: "ui-element-repair", outputs: ["repaired", "no-good-candidate"], prompt: "Pick.", contentClass: "code",
      extraProperties: { chosenIndex: { type: "integer" } },
    };
    await provider.decide(q, {}, 10000);
    const [args] = spawn.mock.calls[0] as [string[]];
    expect(JSON.parse(args[args.indexOf("--json-schema") + 1])).toEqual({
      type: "object",
      properties: { decision: { type: "string", enum: ["repaired", "no-good-candidate"] }, chosenIndex: { type: "integer" } },
      required: ["decision"],
    });
  });

  it("image schema always carries reasons, using the question's own declaration when present", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "looks-right" } }) });
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x" });
    const declared = { type: "array", items: { type: "string", maxLength: 200 } };
    const imageQ: QuestionRef<"looks-right"> = { name: "visual-critique", outputs: ["looks-right"], prompt: "p", contentClass: "image", extraProperties: { reasons: declared } };
    await provider.decide(imageQ, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000);
    await provider.decide({ ...imageQ, extraProperties: undefined }, { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" }, 20000);
    const schemaOf = (call: number): { properties: Record<string, unknown> } => {
      const [args] = spawn.mock.calls[call] as [string[]];
      return JSON.parse(args[args.indexOf("--json-schema") + 1]) as { properties: Record<string, unknown> };
    };
    expect(schemaOf(0).properties.reasons).toEqual(declared);
    expect(schemaOf(1).properties.reasons).toEqual({ type: "array", items: { type: "string" } });
  });
  it("defaults to haiku/low and honours model and effort overrides in both text and image argv", async () => {
    const ok = { code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "send" } }) };
    const argvOf = async (deps: { model?: string; effort?: string }, q: QuestionRef<"send">, input: unknown): Promise<string[]> => {
      const spawn = vi.fn().mockResolvedValue(ok);
      await makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x", ...deps }).decide(q, input, 1000);
      return (spawn.mock.calls[0] as [string[]])[0];
    };
    const textQ: QuestionRef<"send"> = { name: "q", outputs: ["send"], prompt: "p", contentClass: "message-meta" };
    const imgQ: QuestionRef<"send"> = { ...textQ, contentClass: "image" };
    const img = { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" };
    const pair = (a: string[]): string[] => [a[a.indexOf("--model") + 1] as string, a[a.indexOf("--effort") + 1] as string];
    expect(pair(await argvOf({}, textQ, {}))).toEqual(["haiku", "low"]);
    expect(pair(await argvOf({ model: "opus", effort: "high" }, textQ, {}))).toEqual(["opus", "high"]);
    expect(pair(await argvOf({}, imgQ, img))).toEqual(["haiku", "low"]);
    expect(pair(await argvOf({ model: "opus", effort: "high" }, imgQ, img))).toEqual(["opus", "high"]);
  });
  it("keeps thinking off on the default path but lets a non-default effort think (env and argv)", async () => {
    const ok = { code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "send" } }) };
    const q: QuestionRef<"send"> = { name: "q", outputs: ["send"], prompt: "p", contentClass: "message-meta" };
    const imgQ: QuestionRef<"send"> = { ...q, contentClass: "image" };
    const img = { afterScreenshot: "a.png", baselineScreenshot: null, evidenceDir: "/tmp/r" };
    const observe = async (deps: { model?: string; effort?: string }, question: QuestionRef<"send">, input: unknown): Promise<{ settings: string; env: NodeJS.ProcessEnv }> => {
      const spawn = vi.fn().mockResolvedValue(ok);
      await makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/x", ...deps }).decide(question, input, 1000);
      const [args, opts] = spawn.mock.calls[0] as [string[], { env: NodeJS.ProcessEnv }];
      return { settings: args[args.indexOf("--settings") + 1] as string, env: opts.env };
    };
    const saved = process.env.MAX_THINKING_TOKENS;
    delete process.env.MAX_THINKING_TOKENS;
    try {
      for (const [question, input] of [[q, {}], [imgQ, img]] as const) {
        const fast = await observe({}, question, input);
        expect(fast.settings).toBe('{"disableAllHooks":true,"alwaysThinkingEnabled":false}');
        expect(fast.env.MAX_THINKING_TOKENS).toBe("0");
        const think = await observe({ model: "opus", effort: "high" }, question, input);
        expect(think.settings).toBe('{"disableAllHooks":true}');
        expect(think.env.MAX_THINKING_TOKENS).toBeUndefined();
        expect(think.env.AW_JUDGE_CHILD).toBe("1");
      }
    } finally {
      if (saved !== undefined) process.env.MAX_THINKING_TOKENS = saved;
    }
  });
});
