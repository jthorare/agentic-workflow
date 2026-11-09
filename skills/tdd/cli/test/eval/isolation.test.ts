// Scorer isolation: scorers run side by side, each settled to a typed outcome (scored | not_run(reason) |
// failed(error)) under its own time limit. One that throws and one that hangs past its limit leave the others'
// results intact, first on bare effects, then through the whole program with a Tools layer whose node (artifact_check)
// throws and whose lint is a real process that never ends (a sleep piped through cat, run by ToolsLive), so the hang
// goes through the live kill path: the scorer's limit interrupts it and its process group is gone afterwards.
import { afterAll, describe, expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { Effect, Layer } from "effect";
import { runEval, LIMITS } from "../../src/eval/run";
import { Failed, NotRun, type ScoreRec, notRun, score, scorer, settle } from "../../src/eval/score";
import { Tools, ToolsLive } from "../../src/eval/tools";
import { cleanup, HAVE_PYTHON, leftover, packets, snapshot, tmp } from "./harness";

afterAll(cleanup);

const spec = (name: string) => ({ name, method: `${name} method`, source: `${name} source` });
const plain = (r: ScoreRec) => JSON.parse(JSON.stringify(r, (_k, v) => (v && typeof v === "object" && "value" in v && Object.keys(v).length === 1 ? v.value : v)));

describe("settle: typed outcomes, side by side", () => {
  test("a throw and a timeout leave the other scorers intact", async () => {
    const started: string[] = [];
    const ok = Effect.sync(() => (started.push("ok"), score("ok_scorer", 0.5, "m", "s", { n: 2 })));
    const throws = Effect.sync((): ScoreRec => {
      started.push("throws");
      throw new TypeError("scorer bug");
    });
    const hangs = Effect.andThen(Effect.sync(() => started.push("hangs")), Effect.never);
    const notRunYet = Effect.fail(new NotRun({ reason: "no key", metrics: { questions: 3 } }));
    const failed = Effect.fail(new Failed({ reason: "API 500: boom" }));
    const t0 = performance.now();
    const recs = await Effect.runPromise(Effect.all([
      scorer(spec("hangs_scorer"), hangs, 0.05), scorer(spec("throws_scorer"), throws, 5), scorer(spec("ok_scorer"), ok, 5),
      scorer(spec("not_run_scorer"), notRunYet, 5), scorer(spec("failed_scorer"), failed, 5),
    ], { concurrency: "unbounded" }));
    expect(performance.now() - t0).toBeLessThan(2000);
    expect(started.sort()).toEqual(["hangs", "ok", "throws"]);
    const [h, th, o, nr, f] = recs.map(plain);
    expect(o).toEqual({ name: "ok_scorer", value: 0.5, status: "ok", method: "m", source: "s", metrics: { n: 2 } });
    expect(h).toEqual({ name: "hangs_scorer", value: null, status: "not run", method: "hangs_scorer method", source: "hangs_scorer source", reason: "timed out after 0.05s" });
    expect(th).toEqual({ name: "throws_scorer", value: null, status: "error", method: "throws_scorer method", source: "throws_scorer source", reason: "raised TypeError: scorer bug" });
    expect(nr).toEqual(plain(notRun("not_run_scorer", "no key", "not_run_scorer method", "not_run_scorer source", { questions: 3 })));
    expect(f).toMatchObject({ status: "error", reason: "API 500: boom" });
  });
  test("the outcome type itself", async () => {
    const o = await Effect.runPromise(Effect.all([settle(Effect.succeed(1), 1), settle(Effect.never, 0.01), settle(Effect.die("x"), 1),
      settle(Effect.fail(new NotRun({ reason: "r" })), 1)]));
    expect(o.map((x) => x._tag)).toEqual(["scored", "not_run", "failed", "not_run"]);
  });
});

const PACKETS = HAVE_PYTHON ? packets() : [];

describe.skipIf(!PACKETS.length)("the program keeps the other scorers when one throws and one hangs", () => {
  test("artifact_check throws, lint hangs past its limit", async () => {
    const base = tmp("isolate");
    const pk = snapshot(PACKETS[0]!, base);
    const live = await Effect.runPromise(Effect.gen(function* () { return yield* Tools; }).pipe(Effect.provide(ToolsLive)));
    const faulty = Layer.succeed(Tools)({
      which: live.which,
      run: (cmd, opts) => {
        if (cmd.some((c) => c.endsWith("artifact_check.mjs"))) return Effect.sync(() => { throw new Error("boom in artifact_check"); });
        if (cmd.some((c) => c.endsWith("lint_packet.py"))) return live.run(["sh", "-c", "sleep 30.53 | cat"], opts);
        return live.run(cmd, opts);
      },
    });
    const quiet = { out: () => {}, err: () => {} };
    const t0 = performance.now();
    const code = await runEval({ packet: pk, out: join(base, "faulty"), no_shots: true, no_gh: true }, quiet, { PATH: process.env.PATH, HOME: process.env.HOME },
      faulty, { ...LIMITS, lint: 0.5 });
    expect(code).toBe(0);
    expect(performance.now() - t0).toBeLessThan(60_000);
    expect(await leftover("sleep 30.53")).toEqual([]);
    expect(await runEval({ packet: pk, out: join(base, "clean"), no_shots: true, no_gh: true, skip: "artifact_check,lint" }, quiet,
      { PATH: process.env.PATH, HOME: process.env.HOME })).toBe(0);
    const read = (dir: string, kind: string) => JSON.parse(readFileSync(join(base, dir, `output.${kind}.json`), "utf8"));
    const art = read("faulty", "artifact");
    const byName = (o: { scores: ScoreRec[] }) => new Map(o.scores.map((s) => [s.name, s]));
    expect(byName(art).get("artifact_check")).toMatchObject({ status: "error", reason: "raised Error: boom in artifact_check", value: null });
    expect(byName(art).get("lint")).toMatchObject({ status: "not run", reason: "timed out after 0.5s", value: null });
    expect(byName(read("faulty", "qa")).get("artifact_check")).toMatchObject({ status: "error", source: "copied from output.artifact.json (<system-design skill>/scripts/artifact_check.mjs)" });
    for (const kind of ["artifact", "packet", "implementation", "qa", "bugs", "tokens"]) {
      const f = byName(read("faulty", kind));
      const c = byName(read("clean", kind));
      expect([...f.keys()]).toEqual([...c.keys()]);
      // Timings differ run to run; the Python's own volatile fields (seconds, at) are not part of the comparison.
      const stable = (v: unknown): unknown => Array.isArray(v) ? v.map(stable)
        : v && typeof v === "object" ? Object.fromEntries(Object.entries(v as Record<string, unknown>).filter(([k]) => k !== "seconds" && k !== "at").map(([k, x]) => [k, stable(x)]))
        : v;
      for (const [name, rec] of f) if (name !== "artifact_check" && name !== "lint") expect(stable(rec)).toEqual(stable(c.get(name)!));
    }
  }, 120_000);
});
