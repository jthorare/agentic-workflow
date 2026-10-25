import { describe, expect, it, vi } from "vitest";

import { makeClaudeCliProvider } from "../src/providers/claude-cli.js";
import { makeCodexCliProvider } from "../src/providers/codex-cli.js";
import { makeJevProvider } from "../src/providers/jev.js";
import { toRef } from "../src/question.js";
import { resolutionCheck, type ResolutionCheckInput } from "../src/questions/resolution-check.js";

// Linux fails execve with E2BIG for any single argv string of 131072 bytes or more.
const MAX_ARG_STRLEN = 131072;
// Provider input must stay bounded far below that: 64 KB total leaves room for the
// fixed flags and system prompt, and is the same order as the judge's own prompt caps.
const BOUND = 64 * 1024;

const DIFF_START = "diff --git a/src/first.ts b/src/first.ts";
const diff = `${DIFF_START}\n${"+const line = 1; // padding\n".repeat(6500)}`;
const input: ResolutionCheckInput = {
  brief: "BRIEF-TEXT the export button does nothing",
  expected: "EXPECTED-TEXT export downloads a file",
  actual: "ACTUAL-TEXT nothing happens",
  rootCause: "ROOTCAUSE-TEXT handler never bound",
  checkKind: "test",
  checkSummary: "CHECKSUMMARY-TEXT export.test.ts fails then passes",
  beforePassed: false,
  afterPassed: true,
  diffStat: "DIFFSTAT-TEXT 65 files changed",
  diff,
};

const decided = { code: 0, timedOut: false, stdout: JSON.stringify({ structured_output: { decision: "resolved", reasons: [] } }) };
const codexEvents = `${JSON.stringify({ type: "item.completed", item: { id: "i", type: "agent_message", text: JSON.stringify({ decision: "resolved", reasons: [] }) } })}\n`;

// What the model can see: every argv string plus whatever else the fake exec or fetch received.
function seen(calls: unknown[][]): string {
  return calls.map((c) => JSON.stringify(c)).join("\n");
}

function expectModelSeesCappedInput(text: string): void {
  expect(text).toContain(DIFF_START);
  expect(text).toContain("[truncated");
  for (const field of ["BRIEF-TEXT", "EXPECTED-TEXT", "ACTUAL-TEXT", "ROOTCAUSE-TEXT", "CHECKSUMMARY-TEXT", "DIFFSTAT-TEXT"]) expect(text).toContain(field);
}

function expectBoundedArgv(args: string[]): void {
  for (const arg of args) expect(Buffer.byteLength(arg)).toBeLessThan(MAX_ARG_STRLEN);
  expect(args.reduce((n, a) => n + Buffer.byteLength(a), 0)).toBeLessThan(BOUND);
}

describe("a 180 KB resolution-check input", () => {
  it("claude_cli_spawns_within_argv_limits_for_a_180kb_diff", async () => {
    expect(diff.length).toBeGreaterThan(180_000);
    const spawn = vi.fn().mockResolvedValue(decided);
    const provider = makeClaudeCliProvider({ spawn, tmpDirFactory: () => "/tmp/judge-big" });
    await provider.decide(toRef(resolutionCheck, input), input, 5000);
    expect(spawn).toHaveBeenCalledTimes(1);
    expectBoundedArgv((spawn.mock.calls[0] as [string[]])[0]);
    expectModelSeesCappedInput(seen(spawn.mock.calls));
  });

  it("codex_cli_spawns_within_argv_limits_for_a_180kb_diff", async () => {
    const spawn = vi.fn().mockResolvedValue({ code: 0, timedOut: false, stdout: codexEvents });
    const provider = makeCodexCliProvider({ spawn, writeFile: vi.fn(), tmpDirFactory: () => "/tmp/judge-big" });
    await provider.decide(toRef(resolutionCheck, input), input, 5000);
    expect(spawn).toHaveBeenCalledTimes(1);
    expectBoundedArgv((spawn.mock.calls[0] as [string[]])[0]);
    expectModelSeesCappedInput(seen(spawn.mock.calls));
  });

  it("jev_request_body_is_bounded_and_omits_the_raw_diff_for_a_180kb_diff", async () => {
    const fetch = vi.fn().mockResolvedValue({ status: 200, json: async () => ({ answers: { decision: { type: "choice", choice: "resolved", confidence: 0.9 } } }) });
    const provider = makeJevProvider({ fetch, apiKey: async () => "k" });
    await provider.decide(toRef(resolutionCheck, input), input, 5000);
    const body = (fetch.mock.calls[0] as [string, { body: string }])[1].body;
    expect(Buffer.byteLength(body)).toBeLessThan(BOUND);
    expect(body).not.toContain(diff);
    expectModelSeesCappedInput(body);
  });
});
