// The Tools service against ev_common.run (subprocess.run with capture_output, text and timeout), and its deadline
// (review M1): a command whose child started a sleeping grandchild that holds the pipes returns at the timeout, as the
// Python does, instead of when the grandchild ends; the child's whole process group is killed at the deadline, when a
// scorer's own limit interrupts the run, and when this process is ended by SIGTERM or SIGINT. Only scratch commands
// (sh, sleep) run here; markers are odd fractional sleeps so a leftover is found by name and nothing else matches.
import { afterAll, describe, expect, test } from "bun:test";
import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { Effect } from "effect";
import { settle } from "../../src/eval/score";
import { type RunResult, Tools, ToolsLive } from "../../src/eval/tools";
import { cleanup, CLI, HAVE_PYTHON, leftover, oracle, tmp } from "./harness";

afterAll(cleanup);

const run = (cmd: string[], timeout: number): Promise<RunResult> =>
  Effect.runPromise(Effect.gen(function* () {
    const t = yield* Tools;
    return yield* t.run(cmd, { timeout });
  }).pipe(Effect.provide(ToolsLive)));

/** Commands still running whose line contains `marker`. */
const left = (marker: string): string[] =>
  Bun.spawnSync(["ps", "-axo", "command"]).stdout.toString().split("\n").filter((l) => l.includes(marker));

const plain = (r: RunResult) => ({ ...r, seconds: undefined });

describe.skipIf(!HAVE_PYTHON)("Tools.run equals ev_common.run, and its deadline holds", () => {
  // [cmd, timeout]; the Python runs "3.1x" markers, the TypeScript "3.2x" (its own processes must be gone at once)
  const cases = (m: string): [string[], number][] => [
    [["sh", "-c", "printf 'a\\r\\nb\\rc'; printf 'e\\n' 1>&2; exit 2"], 10],
    [["sh", "-c", `echo hi; sleep 3.${m}7 | cat`], 1],
    [["sh", "-c", `(sleep 3.${m}8 &); echo bye`], 1],
    [["sh", "-c", "kill -9 $$"], 5],
    [["no-such-binary-for-the-eval-test"], 5],
  ];
  test("records equal (seconds aside); timeouts return at the deadline; no grandchild survives", async () => {
    const file = join(tmp("tools"), "cases.json");
    writeFileSync(file, JSON.stringify(cases("1")));
    const golden = oracle<{ record: RunResult & { seconds: number }; wall: number }[]>("run", [file]);
    const mine: { record: RunResult; wall: number }[] = [];
    for (const [cmd, timeout] of cases("2")) {
      const t0 = performance.now();
      const record = await run(cmd, timeout);
      mine.push({ record, wall: (performance.now() - t0) / 1000 });
    }
    const swap = (r: RunResult) => JSON.parse(JSON.stringify(plain(r)).replaceAll("3.2", "3.1"));
    for (let i = 0; i < golden.length; i++) {
      expect(swap(mine[i]!.record)).toEqual({ ...golden[i]!.record, seconds: undefined });
      if (golden[i]!.record.timeout) {
        expect(golden[i]!.wall).toBeLessThan(2); // the Python returns at the deadline...
        expect(mine[i]!.wall).toBeLessThan(2); // ...and so does the TypeScript (it waited 3 s and more before)
      }
    }
    expect(mine[3]!.record.code).toBe(-9);
    expect(await leftover("sleep 3.27")).toEqual([]);
    expect(await leftover("sleep 3.28")).toEqual([]);
  }, 30_000);

  test("a scorer's own limit interrupts the run and kills the group", async () => {
    const t0 = performance.now();
    const o = await Effect.runPromise(settle(Effect.gen(function* () {
      const t = yield* Tools;
      return yield* t.run(["sh", "-c", "sleep 30.41 | cat"], { timeout: 60 });
    }), 0.3).pipe(Effect.provide(ToolsLive)));
    expect(o).toEqual({ _tag: "not_run", reason: "timed out after 0.3s" });
    expect(performance.now() - t0).toBeLessThan(2000);
    expect(await leftover("sleep 30.41")).toEqual([]);
  });

  for (const sig of ["SIGTERM", "SIGINT"] as const)
    test(`${sig} to the eval kills a running tool's group, then ends the process as the signal would`, async () => {
      const marker = sig === "SIGTERM" ? "sleep 30.43" : "sleep 30.47";
      const script = join(tmp("sig"), "child.ts");
      writeFileSync(script, [
        `import { Effect } from "effect";`,
        `import { Tools, ToolsLive } from ${JSON.stringify(join(CLI, "src", "eval", "tools.ts"))};`,
        `console.log("ready");`,
        `await Effect.runPromise(Effect.gen(function* () { const t = yield* Tools; return yield* t.run(["sh", "-c", ${JSON.stringify(marker + " | cat")}], { timeout: 60 }); }).pipe(Effect.provide(ToolsLive)));`,
        `console.log("not reached");`,
      ].join("\n"));
      const child = Bun.spawn(["bun", script], { cwd: CLI, stdout: "pipe", stderr: "pipe" });
      for (let i = 0; i < 100 && !left(marker).length; i++) await Bun.sleep(50);
      expect(left(marker).length).toBeGreaterThan(0);
      child.kill(sig);
      await child.exited;
      expect(child.signalCode).toBe(sig);
      expect(await leftover(marker)).toEqual([]);
      expect(await new Response(child.stdout).text()).not.toContain("not reached");
    }, 20_000);
});
