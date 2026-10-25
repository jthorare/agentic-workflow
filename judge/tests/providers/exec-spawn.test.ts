import os from "node:os";

import { describe, expect, it } from "vitest";

import { makeExecSpawn } from "../../src/providers/exec-spawn.js";

// Exercised against a real `node` child (process.execPath) — the one place a
// real process is spawned; provider tests all inject fakes.
const node = makeExecSpawn(process.execPath);
const opts = { cwd: os.tmpdir(), env: { ...process.env, JUDGE_SPAWN_TEST: "yes" }, timeoutMs: 5000 };

describe("makeExecSpawn", () => {
  it("returns stdout and exit 0, with the given cwd and env, and stdin closed", async () => {
    const result = await node(["-e", "process.stdin.resume();process.stdin.on('end',()=>process.stdout.write(process.env.JUDGE_SPAWN_TEST))"], opts);
    expect(result).toEqual({ stdout: "yes", code: 0, timedOut: false });
  });

  it("writes opts.stdin to the child and then closes it", async () => {
    const result = await node(["-e", "let s='';process.stdin.on('data',(d)=>s+=d);process.stdin.on('end',()=>process.stdout.write(s))"], { ...opts, stdin: "x".repeat(200_000) });
    expect(result).toEqual({ stdout: "x".repeat(200_000), code: 0, timedOut: false });
  });

  it("reports a non-zero exit code with whatever stdout was written", async () => {
    const result = await node(["-e", "process.stdout.write('partial');process.exit(3)"], opts);
    expect(result).toEqual({ stdout: "partial", code: 3, timedOut: false });
  });

  it("reports a timeout as timedOut with a null code", async () => {
    const result = await node(["-e", "setTimeout(()=>{},10000)"], { ...opts, timeoutMs: 100 });
    expect(result.timedOut).toBe(true);
    expect(result.code).toBeNull();
  });

  it("reports a missing binary as notFound", async () => {
    const result = await makeExecSpawn("definitely-not-a-real-binary-judge-test")([], opts);
    expect(result).toEqual({ stdout: "", code: null, timedOut: false, notFound: true });
  });
});
