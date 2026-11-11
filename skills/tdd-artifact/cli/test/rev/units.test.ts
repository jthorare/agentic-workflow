// The runner's pure parts: argv, the plan (fresh, resume, new cycle, --from, --only, the resume guard), key scrubbing
// and the child environment, fingerprints, the source and artifact_check lookups, and the eval boundary.
import { afterAll, describe, expect, test } from "bun:test";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { Redacted } from "effect";
import { parseArgs } from "../../src/rev/args";
import { bunEval, evalStep, pythonEval } from "../../src/rev/eval";
import { fingerprint, type Part } from "../../src/rev/hash";
import { freshState, type State, type StepRecord } from "../../src/rev/journal";
import { artifactCheckPath, findSource, resolvePaths } from "../../src/rev/paths";
import { plan } from "../../src/rev/run";
import { evalPushKeys, isSecretName, makeSecrets, withdrawSecrets } from "../../src/rev/secrets";
import { evalArgs } from "../../src/rev/stepdefs";
import { STEPS, stepName, type StepName } from "../../src/rev/steps";

const root = mkdtempSync(join(tmpdir(), "tdd-rev-units-"));
afterAll(() => rmSync(root, { recursive: true, force: true }));

describe("argv", () => {
  test("flags, steps and aliases", () => {
    const a = parseArgs(["p", "--no-shots", "--no-gh", "--from", "app_build", "--reuse", "--json"]);
    expect(a).toMatchObject({ packet: "p", noShots: true, noGh: true, from: "app-build", only: null, reuse: true, json: true, push: false });
    expect(stepName("load_db")).toBe("load-db");
    expect(stepName("check-hydrate")).toBe("hydrate");
    expect(stepName("nope")).toBeNull();
    expect(parseArgs(["p", "--published", "artifact", "https://claude.ai/x"])).toMatchObject({ published: { surface: "artifact", url: "https://claude.ai/x" } });
  });
  test("usage errors", () => {
    expect(parseArgs([])).toEqual({ error: "a packet folder is required" });
    expect(parseArgs(["a", "b"])).toEqual({ error: "one packet folder, not 2" });
    expect(parseArgs(["p", "--only"])).toMatchObject({ error: expect.stringContaining("--only needs a step") });
    expect(parseArgs(["p", "--only", "x"])).toMatchObject({ error: expect.stringContaining("unknown step x") });
    expect(parseArgs(["p", "--from", "lint", "--only", "eval"])).toEqual({ error: "--from and --only cannot be combined" });
    expect(parseArgs(["p", "--published", "slack", "https://x"])).toMatchObject({ error: expect.stringContaining("surface must be one of") });
    expect(parseArgs(["p", "--published", "notion", "ftp://x"])).toEqual({ error: "--published url must be an http(s) url" });
    expect(parseArgs(["p", "--bogus"])).toEqual({ error: "unknown option --bogus" });
    expect(parseArgs(["-h"])).toBe("help");
  });
});

const rec = (cycle: number, outcome: StepRecord["outcome"], inputs = "h"): StepRecord => ({ cycle, run: "r", at: "t", seconds: 0, outcome, inputs });
const withSteps = (cycle: number, steps: Partial<Record<StepName, StepRecord>>): State => ({ ...freshState("p", "t"), cycle, steps });
const never = () => false;

describe("plan", () => {
  test("fresh state: cycle 1 from compile; --only and --from inside the cycle", () => {
    expect(plan(freshState("p", "t"), { from: null, only: null }, never)).toMatchObject({ mode: "fresh", cycle: 1, newCycle: true, steps: [...STEPS] });
    expect(plan(freshState("p", "t"), { from: null, only: "lint" }, never)).toMatchObject({ mode: "only", cycle: 1, steps: ["lint"] });
    const s = withSteps(3, { compile: rec(3, "ok") });
    expect(plan(s, { from: "eval", only: null }, never)).toMatchObject({ mode: "from", cycle: 3, newCycle: false, steps: ["eval", "load-db", "push"] });
  });
  test("resume at the first step not done in this cycle (missing, failed, or from an older cycle)", () => {
    expect(plan(withSteps(2, { compile: rec(2, "ok"), "app-build": rec(2, "ok"), hydrate: rec(2, "failed") }), { from: null, only: null }, never).steps[0]).toBe("hydrate");
    expect(plan(withSteps(2, { compile: rec(2, "ok"), "app-build": rec(1, "ok") }), { from: null, only: null }, never).steps[0]).toBe("app-build");
    expect(plan(withSteps(2, { compile: rec(2, "skipped") }), { from: null, only: null }, never).steps[0]).toBe("app-build");
  });
  test("every step done: a new cycle", () => {
    const all = Object.fromEntries(STEPS.map((s) => [s, rec(4, s === "push" ? "skipped" : "ok")]));
    expect(plan(withSteps(4, all), { from: null, only: null }, never)).toMatchObject({ mode: "new-cycle", cycle: 5, newCycle: true });
  });
  test("the resume guard: an ok step before the frontier whose inputs changed", () => {
    const s = withSteps(2, { compile: rec(2, "ok"), "app-build": rec(2, "ok"), hydrate: rec(2, "failed") });
    const p = plan(s, { from: null, only: null }, (step) => step === "app-build");
    expect(p.steps[0]).toBe("app-build");
    expect(p.notes[0]).toContain("what app-build read or wrote changed");
  });
});

describe("secrets", () => {
  const values = new Map([
    ["BRAINTRUST_API_KEY", Redacted.make("bt-secret-123456")],
    ["DATABASE_URL", Redacted.make("postgres://me:p%40ssw0rd@db.local:5432/tdd")],
  ]);
  const s = makeSecrets(values, {
    PATH: "/bin", BRAINTRUST_API_KEY: "bt-secret-123456", LINEAR_API_KEY: "lin-123456", OPENAI_API_KEY: "sk-1234", OTHER: "x",
    AWS_REGION: "us-east-1", GH_TOKEN: "ghp_x", MY_TOKEN: "t", X_SECRET: "s", SOME_KEY: "k", DB_PASSWORD: "p",
    DATABASE_URL: "postgres://me:p%40ssw0rd@db.local:5432/tdd",
  });
  test("scrub replaces every key value and a database url's password", () => {
    expect(s.scrub("a bt-secret-123456 b bt-secret-123456")).toBe("a <redacted:BRAINTRUST_API_KEY> b <redacted:BRAINTRUST_API_KEY>");
    expect(s.scrub("url postgres://me:p%40ssw0rd@db.local:5432/tdd")).toBe("url <redacted:DATABASE_URL>");
    expect(s.scrub("password p@ssw0rd")).toBe("password <redacted:DATABASE_URL>");
    expect(String(values.get("BRAINTRUST_API_KEY"))).not.toContain("bt-secret");
    expect(s.present).toEqual(["BRAINTRUST_API_KEY", "DATABASE_URL"]);
  });
  test("a child gets no secret (named keys, AWS_*, *_KEY, *_TOKEN, *_SECRET, *_PASSWORD, DATABASE_URL) unless passed by name", () => {
    expect(s.childEnv()).toEqual({ PATH: "/bin", OTHER: "x" });
    expect(s.childEnv(evalPushKeys(true))).toEqual({ PATH: "/bin", OTHER: "x", BRAINTRUST_API_KEY: "bt-secret-123456", LINEAR_API_KEY: "lin-123456", AWS_REGION: "us-east-1" });
    expect(s.childEnv(evalPushKeys(false))).toMatchObject({ GH_TOKEN: "ghp_x" });
    expect(s.childEnv(evalPushKeys(false))).not.toHaveProperty("OPENAI_API_KEY");
    for (const n of ["GITHUB_TOKEN", "AWS_SESSION_TOKEN", "FOO_API_KEY", "A_SECRET", "PGPASSWORD_X_PASSWORD", "DATABASE_URL"]) expect(isSecretName(n)).toBe(true);
    for (const n of ["PATH", "HOME", "TDD_GENERATED", "KEYCHAIN", "TOKENS_DIR"]) expect(isSecretName(n)).toBe(false);
  });
  test("withdrawSecrets takes them out of process.env and puts them back", () => {
    process.env["N9_TEST_TOKEN"] = "abc";
    const restore = withdrawSecrets();
    expect(process.env["N9_TEST_TOKEN"]).toBeUndefined();
    restore();
    expect(process.env["N9_TEST_TOKEN"]).toBe("abc");
    delete process.env["N9_TEST_TOKEN"];
  });
});

describe("fingerprints", () => {
  test("a tree hashes paths and contents, minus exclusions and Finder noise", () => {
    const t = join(root, "tree");
    mkdirSync(join(t, "build"), { recursive: true });
    mkdirSync(join(t, "a"), { recursive: true });
    writeFileSync(join(t, "a", "x.md"), "one");
    writeFileSync(join(t, "build", "out.json"), "1");
    const f = () => fingerprint([{ tree: t, exclude: ["build"] }]);
    const h0 = f();
    writeFileSync(join(t, "build", "out.json"), "2");
    writeFileSync(join(t, "a", ".DS_Store"), "noise");
    expect(f()).toBe(h0);
    writeFileSync(join(t, "a", "x.md"), "two");
    expect(f()).not.toBe(h0);
    expect(fingerprint([{ label: "x", value: "1" }])).not.toBe(fingerprint([{ label: "x", value: "2" }]));
    expect(fingerprint([{ file: join(t, "missing") }])).toBe(fingerprint([{ file: join(t, "missing") }]));
  });
});

describe("paths", () => {
  test("the source markdown: <slug>.md, else content.json's source, else the only .md", () => {
    const p = join(root, "pk", "my-slug");
    mkdirSync(join(p, "build"), { recursive: true });
    writeFileSync(join(p, "status.md"), "");
    writeFileSync(join(p, "review-x.md"), "");
    expect(findSource(p, join(p, "build"))).toBeNull();
    writeFileSync(join(p, "doc.md"), "");
    expect(findSource(p, join(p, "build"))).toBe("doc.md");
    writeFileSync(join(p, "other.md"), "");
    writeFileSync(join(p, "build", "content.json"), JSON.stringify({ source: "other.md" }));
    expect(findSource(p, join(p, "build"))).toBe("other.md");
    writeFileSync(join(p, "my-slug.md"), "");
    expect(resolvePaths(p).source).toBe("my-slug.md");
  });
  test("artifact_check: $SYSTEM_DESIGN_SKILL first, then the sibling, ~/.claude/skills, the vendor copy", () => {
    const skill = join(root, "skills", "tdd");
    mkdirSync(join(skill, "vendor", "system-design", "scripts"), { recursive: true });
    writeFileSync(join(skill, "vendor", "system-design", "scripts", "artifact_check.mjs"), "");
    const env = join(root, "sd-env");
    mkdirSync(join(env, "scripts"), { recursive: true });
    writeFileSync(join(env, "scripts", "artifact_check.mjs"), "");
    expect(artifactCheckPath(skill, { SYSTEM_DESIGN_SKILL: env })).toBe(join(env, "scripts", "artifact_check.mjs"));
    mkdirSync(join(root, "skills", "system-design", "scripts"), { recursive: true });
    writeFileSync(join(root, "skills", "system-design", "scripts", "artifact_check.mjs"), "");
    expect(artifactCheckPath(skill, {})).toBe(join(root, "skills", "system-design", "scripts", "artifact_check.mjs"));
  });
});

describe("the eval boundary", () => {
  test("tdd eval (src/eval) as a child; eval_packet.py only when src/eval lacks what the run needs", () => {
    const p = resolvePaths(join(root, "pk", "my-slug"));
    const saved = process.env["TDD_EVAL_PYTHON"];
    const venv = join(root, "venv-python");
    writeFileSync(venv, "#!/bin/sh\n");
    process.env["TDD_EVAL_PYTHON"] = venv;
    expect(evalStep(p, false)).toBe(bunEval);
    expect(bunEval.command(p, ["--no-gh"])).toEqual({ cmd: [process.execPath, join(p.cli, "bin", "tdd.ts"), "eval", p.packet, "--no-gh"] });
    // the cli has no braintrust package: a push takes the venv, named with the reason
    const braintrust = (() => { try { Bun.resolveSync("braintrust", p.cli); return true; } catch { return false; } })();
    if (!braintrust) {
      const fb = evalStep(p, true);
      expect(fb.name).toBe("scripts/eval_packet.py (the eval venv); the push needs the braintrust package, absent for tdd eval");
      expect(fb.command(p, ["--push"])).toEqual({ cmd: [venv, "-I", join(p.skill, "scripts", "eval_packet.py"), p.packet, "--push"] });
    }
    // no bin/eval-packet.ts (an older skill copy): the venv, when it is there
    const old = { ...p, cli: join(root, "old-cli") };
    expect(evalStep(old, false).name).toBe("scripts/eval_packet.py (the eval venv); bin/eval-packet.ts is absent");
    expect(bunEval.command(old, [])).toEqual({ notRun: "not run: bin/eval-packet.ts is absent" });
    process.env["TDD_EVAL_PYTHON"] = join(root, "absent-python");
    expect(evalStep(p, true)).toBe(bunEval); // no venv to fall back on: tdd eval runs, and its push names the package
    expect(evalStep(old, false)).toBe(bunEval);
    expect(pythonEval.command(p, [])).toMatchObject({ notRun: expect.stringContaining("not run: the eval venv is absent") });
    if (saved === undefined) delete process.env["TDD_EVAL_PYTHON"];
    else process.env["TDD_EVAL_PYTHON"] = saved;
  });
  test("the eval's fingerprint follows the impl: src/eval for tdd eval, scripts/eval for the Python", () => {
    const p = resolvePaths(join(root, "pk", "my-slug"));
    const trees = (parts: readonly Part[]) => parts.map((x) => ("tree" in x ? x.tree : "file" in x ? x.file : x.label));
    expect(trees(bunEval.code(p))).toContain(join(p.cli, "src", "eval"));
    expect(trees(bunEval.code(p))).toContain(join(p.skill, "scripts", "lint_packet.py"));
    expect(trees(pythonEval.code(p))).toContain(join(p.skill, "scripts", "eval"));
  });
  test("offline unless --push", () => {
    const a = { noShots: true, noGh: true } as Parameters<typeof evalArgs>[0];
    expect(evalArgs(a, false)).toEqual(["--no-shots", "--no-gh", "--skip", "linear,aws"]);
    expect(evalArgs(a, true)).toEqual(["--no-shots", "--no-gh", "--push"]);
  });
});
