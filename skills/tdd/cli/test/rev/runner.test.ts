// The runner's program on a scripted world (harness.ts): the steps in order, the typed outcomes, the journal and the
// state, resume at the first undone step (after a failure, after an interrupt, after a source edit), --from, --only,
// --reuse, the eval's not-run, load-db's unreachable skip, and the key rules (no key in a child or a file without
// --push; with --push the eval's own push, read once).
import { afterAll, describe, expect, test } from "bun:test";
import { cpSync, existsSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { args, CLI, FAKE_ANTHROPIC, FAKE_KEY, journal, makeWorld, outcomes, restoreEnv, runOnce, state, type World } from "./harness";

const NODE = !!Bun.which("node");
const AC = NODE ? "ok" : "skipped";
const worlds: World[] = [];
const world = () => { const w = makeWorld(); worlds.push(w); return w; };
afterAll(() => {
  restoreEnv();
  for (const w of worlds) rmSync(w.root, { recursive: true, force: true });
});

const ALL_OK = { compile: "ok", "app-build": "ok", hydrate: "ok", agents: "ok", lint: "ok", "artifact-check": AC, eval: "ok", "load-db": "ok", push: "skipped" };

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
    expect(s.steps.lint.data).toEqual({ lint_errors: 2, lint_warnings: 3 });
    expect(report!.rows.find((r) => r.step === "lint")!.data).toEqual({ lint_errors: 2, lint_warnings: 3 });
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
    expect(report!.notes[0]).toContain("what compile read or wrote changed");
  });

  test("an output removed after a failure moves the resume point back to the step that wrote it", async () => {
    const w = world();
    w.hydrate = "fail";
    await runOnce(w, args(w));
    rmSync(join(w.paths.build, "tdd.html"));
    w.hydrate = "ok";
    const { report } = await runOnce(w, args(w));
    expect(report!.steps[0]).toBe("app-build");
    expect(report!.notes[0]).toContain("what app-build read or wrote changed");
  });

  test.skipIf(!NODE)("review M1: a --reuse run that fails later, then a source edit: the next plain run starts at compile", async () => {
    const w = world();
    await runOnce(w, args(w)); // cycle 1, every step done
    w.artifactCheck = "fail";
    const reuse = await runOnce(w, args(w, "--reuse", "--no-shots")); // --no-shots moves artifact-check's inputs only
    expect(outcomes(reuse.report)).toMatchObject({ compile: "skipped", "app-build": "skipped", hydrate: "skipped", agents: "skipped", lint: "skipped", "artifact-check": "failed", eval: "(not reached)" });
    expect(state(w).steps.compile.reused).toBe(true);
    writeFileSync(join(w.paths.packet, "demo-packet.md"), "# Demo, edited after the failure\n");
    w.artifactCheck = "ok";
    w.called.length = 0;
    const plain = await runOnce(w, args(w, "--no-shots"));
    expect(plain.report!.steps[0]).toBe("compile");
    expect(plain.report!.notes[0]).toContain("what compile read or wrote changed");
    expect(w.called[0]![0]).toBe("compile");
    expect(plain.report!.exit).toBe(0);
  });

  test("review M1: a build/.rev copied from another packet folder is set aside; the run starts fresh with a note", async () => {
    const a = world();
    a.hydrate = "fail";
    await runOnce(a, args(a)); // a plain run in A would now resume at hydrate
    const b = world();
    cpSync(a.paths.build, b.paths.build, { recursive: true });
    writeFileSync(join(b.paths.packet, "demo-packet.md"), "# Demo B, its own edit\n");
    const { report } = await runOnce(b, args(b));
    expect(report!.mode).toBe("fresh");
    expect(report!.steps[0]).toBe("compile");
    expect(report!.notes[0]).toContain("build/.rev came from another packet folder");
    const aside = readdirSync(b.paths.rev).filter((f) => f.includes(".foreign-")).sort();
    expect(aside).toEqual([`journal.foreign-${report!.run}.jsonl`, `state.foreign-${report!.run}.json`]);
    expect(journal(b)[0]!.event).toBe("run-start"); // B's journal starts with B's run
    expect(state(b).packet_key).not.toBe(state(a).packet_key);
  });

  test("review m3: --reuse never skips a step whose latest run failed", async () => {
    const w = world();
    await runOnce(w, args(w));
    w.hydrate = "fail";
    await runOnce(w, args(w, "--only", "hydrate"));
    w.spawned.length = 0;
    const { report } = await runOnce(w, args(w, "--only", "hydrate", "--reuse"));
    expect(outcomes(report).hydrate).toBe("failed");
    expect(w.spawned.length).toBe(1);
  });

  test("a failed step's stderr tail is in the report rows (and so in the table and --json)", async () => {
    const w = world();
    w.hydrate = "fail";
    const { report } = await runOnce(w, args(w));
    expect(report!.rows.find((r) => r.step === "hydrate")!.stderr_tail).toBe("TypeError: x is undefined");
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
    expect(from.report!.rows.filter((r) => r.ran).map((r) => r.step)).toEqual(["agents", "lint", "artifact-check", "eval", "load-db", "push"]);
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

  test("--reuse reruns load-db when $DATABASE_URL names another database (the runner reads its own snapshot)", async () => {
    const w = world();
    const A = { DATABASE_URL: "postgres://localhost/tdd_scratch_a" }, B = { DATABASE_URL: "postgres://localhost/tdd_scratch_b" };
    await runOnce(w, args(w, "--only", "load-db"), { keys: A });
    const loads = () => w.spawned.filter((s) => s.cmd.includes("load-db")).map((s) => s.env["DATABASE_URL"]);
    const same = await runOnce(w, args(w, "--only", "load-db", "--reuse"), { keys: A });
    expect(same.report!.rows.find((r) => r.step === "load-db")!.detail).toContain("reused");
    const other = await runOnce(w, args(w, "--only", "load-db", "--reuse"), { keys: B });
    expect(other.report!.rows.find((r) => r.step === "load-db")!.outcome).toBe("ok");
    expect(loads()).toEqual([A.DATABASE_URL, B.DATABASE_URL]);
  });
});

describe("skips with a reason", () => {
  test("the eval is tdd eval, a child of this bun, offline; it needs no venv", async () => {
    const w = world();
    process.env["TDD_EVAL_PYTHON"] = join(w.root, "no-such-python");
    const { report } = await runOnce(w, args(w));
    process.env["TDD_EVAL_PYTHON"] = join(w.root, "python");
    expect(outcomes(report).eval).toBe("ok");
    const ev = w.spawned.find((s) => s.cmd[2] === "eval")!;
    expect(ev.cmd).toEqual([process.execPath, join(w.paths.cli, "bin", "tdd.ts"), "eval", w.paths.packet, "--skip", "linear,aws"]);
    expect(journal(w)[0]!.eval).toBe("tdd eval (src/eval)");
    expect(report!.exit).toBe(0);
  });

  test("no tdd eval (bin/eval-packet.ts absent) and no venv: the eval is not run, named, and the run still exits 0", async () => {
    const base = world();
    const w: World = { ...base, paths: { ...base.paths, cli: join(base.root, "old-cli") } };
    process.env["TDD_EVAL_PYTHON"] = join(w.root, "no-such-python");
    const { report } = await runOnce(w, args(w));
    process.env["TDD_EVAL_PYTHON"] = join(w.root, "python");
    const ev = report!.rows.find((r) => r.step === "eval")!;
    expect(ev.outcome).toBe("skipped");
    expect(ev.detail).toBe("not run: bin/eval-packet.ts is absent");
    expect(report!.exit).toBe(0);
  });

  test("no tdd eval but the venv: eval_packet.py runs, the fallback named in the journal", async () => {
    const base = world();
    const w: World = { ...base, paths: { ...base.paths, cli: join(base.root, "old-cli") } };
    const { report } = await runOnce(w, args(w));
    expect(outcomes(report).eval).toBe("ok");
    expect(w.spawned.find((s) => s.cmd.some((c) => c.endsWith("eval_packet.py")))!.cmd.slice(0, 2)).toEqual([join(w.root, "python"), "-I"]);
    expect(journal(w)[0]!.eval).toBe("scripts/eval_packet.py (the eval venv); bin/eval-packet.ts is absent");
  });

  test("Postgres unreachable: load-db is skipped with the probe's answer", async () => {
    const w = world();
    w.probe = { ok: false, answered: false, reason: "Failed to connect" };
    const { report } = await runOnce(w, args(w));
    const db = report!.rows.find((r) => r.step === "load-db")!;
    expect(db.outcome).toBe("skipped");
    expect(db.detail).toBe("not run: Postgres is unreachable at localhost/tdd_system (Failed to connect)");
    expect(w.spawned.some((s) => s.cmd.includes("load-db"))).toBe(false);
    expect(report!.exit).toBe(0);
  });

  test("a server that answers with an error (missing database, unknown role) fails load-db: a typo is not a skip", async () => {
    const w = world();
    w.probe = { ok: false, answered: true, reason: 'database "tdd_sytem" does not exist (SQLSTATE 3D000)' };
    const { report } = await runOnce(w, args(w));
    const db = report!.rows.find((r) => r.step === "load-db")!;
    expect(db.outcome).toBe("failed");
    expect(db.detail).toBe('Postgres at localhost/tdd_system refused the connection: database "tdd_sytem" does not exist (SQLSTATE 3D000); check $DATABASE_URL');
    expect(report!.exit).toBe(1);
  });
});

describe("keys", () => {
  const KEYS = {
    BRAINTRUST_API_KEY: FAKE_KEY, ANTHROPIC_API_KEY: FAKE_ANTHROPIC, LINEAR_API_KEY: "lin_api_fake_0000_0000", GH_TOKEN: "ghp_fake_0000_0000",
    GITHUB_TOKEN: "ghs_fake_0000_0000", AWS_ACCESS_KEY_ID: "AKIAFAKE00000000", AWS_SECRET_ACCESS_KEY: "aws-fake-secret-0000",
    MY_SERVICE_TOKEN: "svc-fake-token-0000", SOME_SECRET: "some-fake-secret-0000", DATABASE_URL: "postgres://me:Sup3rSecretPw@localhost/tdd_system_test",
  };
  const secretNames = (env: Record<string, string>) => Object.keys(env).filter((k) => k in KEYS || /(_KEY|_TOKEN|_SECRET)$|^AWS_/.test(k)).sort();
  const isEval = (cmd: readonly string[]) => cmd.some((c) => c.endsWith("eval_packet.py")) || cmd[2] === "eval";
  // the cli has no braintrust package, so a push falls back to the venv's eval_packet.py (named in the journal)
  const BRAINTRUST_TS = (() => { try { Bun.resolveSync("braintrust", CLI); return true; } catch { return false; } })();

  test("without --push: no child gets a key (load-db gets only DATABASE_URL), the eval runs offline, no key in any file", async () => {
    const w = world();
    const { report } = await runOnce(w, args(w, "--no-gh"), { keys: KEYS });
    expect(report!.exit).toBe(0);
    expect(w.spawned.length).toBe(5); // app build, hydrate, artifact_check, eval, load-db
    for (const s of w.spawned) {
      expect(s.cmd).not.toContain("--push");
      expect([s.cmd.join(" ").includes("load-db") ? "load-db" : "other", secretNames(s.env)])
        .toEqual([s.cmd.join(" ").includes("load-db") ? "load-db" : "other", s.cmd.join(" ").includes("load-db") ? ["DATABASE_URL"] : []]);
    }
    const ev = w.spawned.find((s) => isEval(s.cmd))!;
    expect(ev.cmd.slice(-4)).toEqual([w.paths.packet, "--no-gh", "--skip", "linear,aws"]);
    expect(outcomes(report).push).toBe("skipped");
    const files = [join(w.paths.rev, "journal.jsonl"), join(w.paths.rev, "state.json")].map((f) => readFileSync(f, "utf8")).join("\n");
    for (const v of Object.values(KEYS)) expect(files.includes(v)).toBe(false);
    expect(files).not.toContain("Sup3rSecretPw");
    expect(journal(w)[0]!.keys).toEqual(Object.keys(KEYS).sort());
    expect(journal(w)[0]!.offline).toBe(true);
  });

  test("with --push and the key: only the eval gets the push and source keys, it pushes once, the push step reads it", async () => {
    const w = world();
    const { report } = await runOnce(w, args(w, "--push"), { keys: KEYS });
    const evals = w.spawned.filter((s) => isEval(s.cmd));
    expect(evals.length).toBe(1);
    expect(evals[0]!.cmd).toContain("--push");
    expect(evals[0]!.cmd).not.toContain("--skip");
    expect(secretNames(evals[0]!.env)).toEqual(["AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "BRAINTRUST_API_KEY", "GH_TOKEN", "GITHUB_TOKEN", "LINEAR_API_KEY"]);
    expect(evals[0]!.env["BRAINTRUST_API_KEY"]).toBe(FAKE_KEY);
    if (!BRAINTRUST_TS) {
      expect(evals[0]!.cmd.some((c) => c.endsWith("eval_packet.py"))).toBe(true);
      expect(journal(w)[0]!.eval).toBe("scripts/eval_packet.py (the eval venv); the push needs the braintrust package, absent for tdd eval");
    }
    for (const s of w.spawned.filter((x) => !isEval(x.cmd))) {
      expect(secretNames(s.env)).toEqual(s.cmd.join(" ").includes("load-db") ? ["DATABASE_URL"] : []);
    }
    const push = report!.rows.find((r) => r.step === "push")!;
    expect(push.outcome).toBe("ok");
    expect(push.detail).toBe("pushed; dataset: https://example.invalid/ds");
    expect(readFileSync(join(w.paths.rev, "journal.jsonl"), "utf8")).not.toContain(FAKE_KEY);
  });

  test("with --push and --no-gh the eval gets no gh token", async () => {
    const w = world();
    await runOnce(w, args(w, "--push", "--no-gh"), { keys: KEYS });
    expect(secretNames(w.spawned.find((s) => isEval(s.cmd))!.env)).toEqual(["AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "BRAINTRUST_API_KEY", "LINEAR_API_KEY"]);
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
    expect(w.spawned.find((s) => isEval(s.cmd))!.cmd).not.toContain("--push");
    const push = report!.rows.find((r) => r.step === "push")!;
    expect(push.outcome).toBe("failed");
    expect(push.detail).toBe("--push needs BRAINTRUST_API_KEY in the environment (never in a file)");
    expect(report!.exit).toBe(1);
  });

  test("a failed push: the eval is ok (run.json written), the push step fails with the tail", async () => {
    const w = world();
    w.evalCode = 1;
    const { report } = await runOnce(w, args(w, "--push"), { keys: KEYS });
    expect(outcomes(report)).toMatchObject({ "artifact-check": AC, eval: "ok", "load-db": "ok", push: "failed" });
    expect(report!.exit).toBe(1);
  });

  test("a key a tool prints is scrubbed from the table row, the progress line, the journal and the state", async () => {
    const w = world();
    w.leak = `(connected to ${KEYS.DATABASE_URL})`;
    const { report } = await runOnce(w, args(w, "--only", "compile"), { keys: KEYS });
    const row = report!.rows.find((r) => r.step === "compile")!;
    expect(row.detail).toBe("wrote build/content.json: 1 sections (connected to <redacted:DATABASE_URL>)");
    const everything = [JSON.stringify(report), w.err.join(""), readFileSync(join(w.paths.rev, "journal.jsonl"), "utf8"), readFileSync(join(w.paths.rev, "state.json"), "utf8")].join("\n");
    expect(everything).not.toContain("Sup3rSecretPw");
    expect(everything).toContain("<redacted:DATABASE_URL>");
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
