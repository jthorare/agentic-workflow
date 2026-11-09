// The runner's program on a scripted world (harness.ts): the steps in order, the typed outcomes, the journal and the
// state, resume at the first undone step (after a failure, after an interrupt, after a source edit), --from, --only,
// --reuse, the eval's not-run, load-db's unreachable skip, and the key rules (no key in a child or a file without
// --push; with --push the eval's own push, read once).
import { afterAll, describe, expect, test } from "bun:test";
import { readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { args, FAKE_ANTHROPIC, FAKE_KEY, journal, makeWorld, outcomes, restoreEnv, runOnce, state, type World } from "./harness";

const NODE = !!Bun.which("node");
const AC = NODE ? "ok" : "skipped";
const worlds: World[] = [];
const world = () => { const w = makeWorld(); worlds.push(w); return w; };
afterAll(() => {
  restoreEnv();
  for (const w of worlds) rmSync(w.root, { recursive: true, force: true });
});

const ALL_OK = { compile: "ok", "app-build": "ok", hydrate: "ok", agents: "ok", lint: "ok", eval: "ok", "load-db": "ok", push: "skipped", "artifact-check": AC };

describe("a fresh run", () => {
  test("every step in SKILL.md's order, journaled, with the state saved", async () => {
    const w = world();
    const { report } = await runOnce(w, args(w, "--no-shots", "--no-gh"));
    expect(report!.exit).toBe(0);
    expect(report!.mode).toBe("fresh");
    expect(outcomes(report)).toEqual(ALL_OK);
    const j = journal(w);
    expect(j[0]!.event).toBe("run-start");
    expect(j.at(-1)!.event).toBe("run-end");
    expect(j.at(-1)!.exit).toBe(0);
    expect(j.filter((e) => e.event === "step").map((e) => e.step)).toEqual(Object.keys(ALL_OK));
    const s = state(w);
    expect(s.cycle).toBe(1);
    expect(s.steps.lint.detail).toBe("2 error(s), 3 warning(s) -> build/lint.json"); // findings are measured, not a gate
    expect(s.steps.lint.last_ok.inputs).toBe(s.steps.lint.inputs);
    expect(s.steps.push.reason).toContain("--push not given");
    // the in-process tools get SKILL.md's argv, relative to the packet folder
    expect(w.called).toEqual([["compile", ["demo-packet.md", "build"]], ["agents", ["build"]], ["lint", [w.paths.packet, "--json", join(w.paths.build, "lint.json")]]]);
    expect(readFileSync(join(w.paths.rev, ".gitignore"), "utf8")).toBe("*\n");
  });

  test("the next run, with every step done, starts a new cycle at compile", async () => {
    const w = world();
    await runOnce(w, args(w));
    const { report } = await runOnce(w, args(w));
    expect(report!.mode).toBe("new-cycle");
    expect(report!.cycle).toBe(2);
    expect(report!.steps[0]).toBe("compile");
  });
});

describe("resume", () => {
  test("a failed hydrate stops the run; the next run resumes at hydrate and the journal shows both runs", async () => {
    const w = world();
    w.hydrate = "fail";
    const first = await runOnce(w, args(w));
    expect(first.report!.exit).toBe(1);
    expect(outcomes(first.report)).toMatchObject({ compile: "ok", "app-build": "ok", hydrate: "failed", agents: "(not reached)" });
    const failed = state(w).steps.hydrate;
    expect(failed.error).toBe("app/check_hydrate.ts exited 1: console errors 2 | console warnings 0 (hydration 0) | failed steps 0");
    expect(failed.stderr_tail).toBe("TypeError: x is undefined");
    w.hydrate = "ok";
    w.called.length = 0;
    const second = await runOnce(w, args(w));
    expect(second.report!.exit).toBe(0);
    expect(second.report!.mode).toBe("resume");
    expect(second.report!.steps[0]).toBe("hydrate");
    expect(w.called.map((c) => c[0])).toEqual(["agents", "lint"]); // no second compile
    const runs = journal(w).filter((e) => e.event === "run-start").map((e) => [e.mode, e.steps[0]]);
    expect(runs).toEqual([["fresh", "compile"], ["resume", "hydrate"]]);
    expect(outcomes(second.report)).toMatchObject({ compile: "(ok)", "app-build": "(ok)", hydrate: "ok" });
  });

  test("an interrupt during hydrate records it; the next run resumes there", async () => {
    const w = world();
    w.hydrate = "hang";
    const ac = new AbortController();
    const interrupt = { by: null as NodeJS.Signals | null };
    const watcher = setInterval(() => {
      if (journal(w).some((e) => e.event === "step-start" && e.step === "hydrate")) {
        clearInterval(watcher);
        interrupt.by = "SIGTERM";
        ac.abort();
      }
    }, 5);
    const first = await runOnce(w, args(w), { signal: ac.signal, interrupt });
    expect(first.report).toBeNull();
    const j = journal(w);
    const hy = j.find((e) => e.event === "step" && e.step === "hydrate")!;
    expect(hy.outcome).toBe("failed");
    expect(hy.error).toBe("interrupted by SIGTERM; the next run resumes here");
    expect(j.at(-1)).toMatchObject({ event: "run-end", exit: 143, interrupted: "SIGTERM" });
    expect(state(w).steps.hydrate.outcome).toBe("failed");
    w.hydrate = "ok";
    const second = await runOnce(w, args(w));
    expect(second.report!.steps[0]).toBe("hydrate");
    expect(second.report!.exit).toBe(0);
  });

  test("a source edited after a failure moves the resume point back to compile", async () => {
    const w = world();
    w.hydrate = "fail";
    await runOnce(w, args(w));
    writeFileSync(join(w.paths.packet, "demo-packet.md"), "# Demo, edited\n");
    w.hydrate = "ok";
    const { report } = await runOnce(w, args(w));
    expect(report!.steps[0]).toBe("compile");
    expect(report!.notes[0]).toContain("the inputs of compile changed");
  });

  test("a state.json that does not match the schema starts a new cycle with a note", async () => {
    const w = world();
    await runOnce(w, args(w, "--only", "compile"));
    writeFileSync(join(w.paths.rev, "state.json"), JSON.stringify({ version: 9 }));
    const { report } = await runOnce(w, args(w));
    expect(report!.mode).toBe("fresh");
    expect(report!.notes[0]).toContain("does not match the state schema");
  });
});

describe("--from, --only, --reuse", () => {
  test("--only lint runs lint alone; --from agents runs agents to the end", async () => {
    const w = world();
    await runOnce(w, args(w));
    w.called.length = 0;
    const only = await runOnce(w, args(w, "--only", "lint"));
    expect(only.report!.rows.filter((r) => r.ran).map((r) => r.step)).toEqual(["lint"]);
    expect(only.report!.mode).toBe("only");
    w.called.length = 0;
    w.spawned.length = 0;
    const from = await runOnce(w, args(w, "--from", "agents"));
    expect(from.report!.rows.filter((r) => r.ran).map((r) => r.step)).toEqual(["agents", "lint", "eval", "load-db", "push", "artifact-check"]);
    expect(w.called.map((c) => c[0])).toEqual(["agents", "lint"]);
  });

  test("--reuse skips a step whose inputs and outputs are unchanged since its last ok, and reruns one whose output moved", async () => {
    const w = world();
    await runOnce(w, args(w));
    writeFileSync(join(w.paths.build, "lint.json"), "{}"); // lint's output edited by hand
    w.called.length = 0;
    const { report } = await runOnce(w, args(w, "--reuse"));
    const ran = Object.fromEntries(report!.rows.map((r) => [r.step, r.outcome]));
    expect(ran.compile).toBe("skipped");
    expect(report!.rows.find((r) => r.step === "compile")!.detail).toContain("reused: inputs and outputs unchanged");
    expect(ran.lint).toBe("ok");
    expect(w.called.map((c) => c[0])).toEqual(["lint"]);
    expect(journal(w).filter((e) => e.reused).length).toBeGreaterThan(0);
  });
});

describe("skips with a reason", () => {
  test("no eval venv: the eval is not run, named, and the run still exits 0", async () => {
    const w = world();
    process.env["TDD_EVAL_PYTHON"] = join(w.root, "no-such-python");
    const { report } = await runOnce(w, args(w));
    process.env["TDD_EVAL_PYTHON"] = join(w.root, "python");
    const ev = report!.rows.find((r) => r.step === "eval")!;
    expect(ev.outcome).toBe("skipped");
    expect(ev.detail).toContain("not run: the eval venv is absent");
    expect(report!.exit).toBe(0);
  });

  test("Postgres unreachable: load-db is skipped with the probe's answer", async () => {
    const w = world();
    w.probe = "Connection refused";
    const { report } = await runOnce(w, args(w));
    const db = report!.rows.find((r) => r.step === "load-db")!;
    expect(db.outcome).toBe("skipped");
    expect(db.detail).toBe("not run: Postgres is unreachable at localhost/tdd_system (Connection refused)");
    expect(w.spawned.some((s) => s.cmd.includes("load-db"))).toBe(false);
  });
});

describe("keys", () => {
  const KEYS = { BRAINTRUST_API_KEY: FAKE_KEY, ANTHROPIC_API_KEY: FAKE_ANTHROPIC };

  test("without --push: no key reaches a child, the eval runs offline, the push is skipped, no key in any file", async () => {
    const w = world();
    const { report } = await runOnce(w, args(w, "--no-gh"), { keys: KEYS });
    expect(report!.exit).toBe(0);
    for (const s of w.spawned) {
      expect(s.cmd).not.toContain("--push");
      expect(s.env["BRAINTRUST_API_KEY"]).toBeUndefined();
      expect(s.env["ANTHROPIC_API_KEY"]).toBeUndefined();
    }
    const ev = w.spawned.find((s) => s.cmd.some((c) => c.endsWith("eval_packet.py")))!;
    expect(ev.cmd.slice(-4)).toEqual([w.paths.packet, "--no-gh", "--skip", "linear,aws"]);
    expect(outcomes(report).push).toBe("skipped");
    const files = [join(w.paths.rev, "journal.jsonl"), join(w.paths.rev, "state.json")].map((f) => readFileSync(f, "utf8")).join("\n");
    expect(files).not.toContain(FAKE_KEY);
    expect(files).not.toContain(FAKE_ANTHROPIC);
    expect(journal(w)[0]!.keys).toEqual(["ANTHROPIC_API_KEY", "BRAINTRUST_API_KEY"]);
    expect(journal(w)[0]!.offline).toBe(true);
  });

  test("with --push and the key: the eval pushes once and the push step reads its result", async () => {
    const w = world();
    const { report } = await runOnce(w, args(w, "--push"), { keys: KEYS });
    const evals = w.spawned.filter((s) => s.cmd.some((c) => c.endsWith("eval_packet.py")));
    expect(evals.length).toBe(1);
    expect(evals[0]!.cmd).toContain("--push");
    expect(evals[0]!.cmd).not.toContain("--skip");
    expect(evals[0]!.env["BRAINTRUST_API_KEY"]).toBe(FAKE_KEY);
    expect(evals[0]!.env["ANTHROPIC_API_KEY"]).toBeUndefined();
    const push = report!.rows.find((r) => r.step === "push")!;
    expect(push.outcome).toBe("ok");
    expect(push.detail).toBe("pushed; dataset: https://example.invalid/ds");
    expect(readFileSync(join(w.paths.rev, "journal.jsonl"), "utf8")).not.toContain(FAKE_KEY);
  });

  test("--only push delegates to the eval's push", async () => {
    const w = world();
    await runOnce(w, args(w));
    w.spawned.length = 0;
    const { report } = await runOnce(w, args(w, "--only", "push", "--push"), { keys: KEYS });
    expect(w.spawned.length).toBe(1);
    expect(w.spawned[0]!.cmd).toContain("--push");
    expect(report!.rows.find((r) => r.step === "push")!.outcome).toBe("ok");
  });

  test("--push without BRAINTRUST_API_KEY: the eval runs offline and the push fails, named", async () => {
    const w = world();
    const { report } = await runOnce(w, args(w, "--push"));
    expect(w.spawned.find((s) => s.cmd.some((c) => c.endsWith("eval_packet.py")))!.cmd).not.toContain("--push");
    const push = report!.rows.find((r) => r.step === "push")!;
    expect(push.outcome).toBe("failed");
    expect(push.detail).toBe("--push needs BRAINTRUST_API_KEY in the environment (never in a file)");
    expect(report!.exit).toBe(1);
  });

  test("a failed push: the eval is ok (run.json written), the push step fails with the tail", async () => {
    const w = world();
    w.evalCode = 1;
    const { report } = await runOnce(w, args(w, "--push"), { keys: KEYS });
    expect(outcomes(report)).toMatchObject({ eval: "ok", "load-db": "ok", push: "failed", "artifact-check": "(not reached)" });
    expect(report!.exit).toBe(1);
  });
});

describe("the lock", () => {
  test("a second run on a locked build is refused", async () => {
    const w = world();
    await runOnce(w, args(w, "--only", "compile"));
    // a live pid that is not ours holds the lock: the parent of this test process
    writeFileSync(join(w.paths.rev, "lock"), JSON.stringify({ pid: process.ppid, run: "other" }));
    const { exit } = await runOnce(w, args(w));
    expect(exit._tag).toBe("Failure");
    rmSync(join(w.paths.rev, "lock"));
    const again = await runOnce(w, args(w));
    expect(again.report!.exit).toBe(0);
  });
});
