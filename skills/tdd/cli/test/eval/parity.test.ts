// The eval parity proof (PR 15 acceptance): on each $TDD_PARITY_PACKETS packet, eval_packet.py and bin/eval-packet.ts
// run one after the other into the same out path with --no-shots --no-gh and no key in the environment; input.json
// (digest included), output.*.json, probe.json, run.json, lint.json and the history line must be equal once the
// volatile fields are removed (captured, generated, at, seconds and the flattened *.seconds metrics), and stdout
// (the table, the counts, the cost line and the "not pushed" line), stderr and the exit code identical.
// Variants on the first packet cover the judge and agent probe without keys, owner review labels, --session over a
// fixture transcript, --skip, --json and the experiment naming flags. Nothing is pushed, no model is called.
import { afterAll, describe, expect, test } from "bun:test";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { both, canonical, cleanEnv, cleanup, firstDiff, HAVE_PYTHON, jsonFiles, label, lastHistory, packets, python, scratchRepo, snapshot, tmp, withRepo } from "./harness";

afterAll(cleanup);

const PACKETS = HAVE_PYTHON ? packets() : [];
const OUTPUTS = ["input.json", "lint.json", "output.artifact.json", "output.bugs.json", "output.implementation.json", "output.packet.json",
  "output.qa.json", "output.tokens.json", "probe.json", "run.json"];

function expectSame(r: ReturnType<typeof both>, want: string[] = OUTPUTS) {
  expect(r.ts.code).toBe(r.py.code);
  expect(r.ts.err).toBe(r.py.err);
  expect(r.ts.out).toBe(r.py.out);
  expect(jsonFiles(r.ts.dir)).toEqual(jsonFiles(r.py.dir));
  for (const f of want) {
    if (!jsonFiles(r.py.dir).includes(f)) continue;
    const a = canonical(join(r.py.dir, f));
    const b = canonical(join(r.ts.dir, f));
    expect(`${f}: ${firstDiff(a, b) ?? "same"}`).toBe(`${f}: same`);
  }
  expect(lastHistory(r.ts.dir)).toBe(lastHistory(r.py.dir));
}

describe.skipIf(!PACKETS.length)("eval parity: Python and TypeScript on the real packets", () => {
  for (const packet of PACKETS)
    test(`${label(packet)}: --no-shots --no-gh, no keys`, () => {
      const base = tmp("parity");
      const r = both(snapshot(packet, base), base, ["--no-shots", "--no-gh"]);
      expect(r.py.code).toBe(0);
      expectSame(r);
      for (const f of OUTPUTS) expect(jsonFiles(r.ts.dir)).toContain(f);
      expect(jsonFiles(r.ts.dir)).not.toContain("push.json");
      expect(r.ts.out).toContain("not pushed: run.json holds the dataset record and 6 experiments a --push would send");
      const run = JSON.parse(readFileSync(join(r.ts.dir, "run.json"), "utf8"));
      expect(run.pushed).toBe(false);
      expect(run.experiments.map((e: { name: string }) => e.name.split("-").at(-1))).toEqual(["artifact", "packet", "implementation", "qa", "bugs", "tokens"]);
    }, 240_000);

  test("judge and agent probe asked for without keys, owner labels, --skip implementation, --json", () => {
    const base = tmp("variant");
    const pk = snapshot(PACKETS[0]!, base);
    const labels = join(base, "labels.jsonl");
    writeFileSync(labels, [
      `{"target": "artifact", "anchor": "#s1", "verdict": "pass", "note": "reads well", "date": "2026-11-08"}`,
      `{"target": "artifact", "anchor": "#s2", "verdict": "needs-work", "note": "cite it", "date": "2026-11-08"}`,
      `{"target": "packet", "anchor": "README", "verdict": "question", "note": "why?", "date": "2026-11-08"}`,
      `{"target": "qa", "anchor": "x", "verdict": "fail", "note": "no test", "date": "2026-11-09", "author": "owner"}`,
      `// a comment`, `{"target": "nope", "anchor": "a", "verdict": "pass", "note": "", "date": "d"}`,
      `{"target": "bugs", "anchor": "a", "verdict": "maybe", "note": "", "date": "d"}`, `{"target": "bugs"}`, `not json`, "",
    ].join("\n"));
    const r = both(pk, base, ["--no-shots", "--no-gh", "--with-judge", "--with-agent-probe", "--labels", labels, "--skip", "implementation,linear", "--json"]);
    expect(r.py.code).toBe(0);
    expectSame(r);
    expect(r.ts.out).toContain('"judge_clarity"');
  }, 240_000);

  test("--session over a fixture transcript, naming flags, a skipped tool set", () => {
    const base = tmp("session");
    const pk = snapshot(PACKETS[0]!, base);
    const home = join(base, "home");
    const proj = join(home, ".claude", "projects", "-fixture-project");
    mkdirSync(join(proj, "sess-1", "subagents"), { recursive: true });
    mkdirSync(join(home, ".claude", "projects", "-other"), { recursive: true });
    const rec = (id: string, model: string, usage: object) => JSON.stringify({ type: "assistant", uuid: `u-${id}`, message: { id, model, usage } });
    writeFileSync(join(proj, "sess-1.jsonl"), [
      rec("m1", "claude-opus-5-5", { input_tokens: 10, output_tokens: 400, cache_read_input_tokens: 2000, cache_creation_input_tokens: 300 }),
      rec("m1", "claude-opus-5-5", { input_tokens: 12, output_tokens: 410, cache_read_input_tokens: 2000, cache_creation: { ephemeral_5m_input_tokens: 100, ephemeral_1h_input_tokens: 50 } }),
      rec("m2", "claude-fable-5-1[1m]", { input_tokens: 7, output_tokens: 9 }),
      rec("m3", "some-unpriced-model", { input_tokens: 1, output_tokens: 1 }),
      `{"type": "user", "message": {"content": "no usage here"}}`, `{"broken": "usage"`,
    ].join("\n") + "\n");
    writeFileSync(join(proj, "sess-1", "subagents", "agent-a.jsonl"), rec("s1", "claude-sonnet-5-5", { input_tokens: 5, output_tokens: 6 }) + "\n");
    writeFileSync(join(proj, "sess-1", "subagents", "agent-b.jsonl"), rec("s2", "claude-haiku-5-5", { input_tokens: 3, output_tokens: 4 }) + "\r\n");
    const r = both(pk, base, ["--no-shots", "--no-gh", "--session", "sess-1, missing-2", "--skip", "artifact_check,check_hydrate,lint,aws",
      "--project", "tdd-test", "--experiment", "stem-x", "--base", "stem-w"], cleanEnv({ HOME: home }));
    expect(r.py.code).toBe(0);
    expectSame(r);
    expect(r.ts.out).toContain("API-equivalent");
  }, 240_000);

  // review fixes (M2, M3, M1): only scratch repos and fixture transcripts; --run-tests never touches a real repo
  const FAST = ["--no-shots", "--no-gh", "--skip", "artifact_check,check_hydrate,lint,linear,aws"];

  test("the review's three-message --session: the cost line is the Python's ($38.0697)", () => {
    const base = tmp("three");
    const pk = snapshot(PACKETS[0]!, base);
    const home = join(base, "home");
    const proj = join(home, ".claude", "projects", "p1");
    mkdirSync(proj, { recursive: true });
    const rec = (id: string, model: string, input: number) => JSON.stringify({ type: "assistant", message: { id, model, usage: { input_tokens: input, output_tokens: 0 } } });
    writeFileSync(join(proj, "sess-r.jsonl"), [rec("m1", "claude-opus-5-5", 920875), rec("m2", "claude-sonnet-5-5", 8411685),
      rec("m3", "claude-haiku-5-5", 175627800)].join("\n") + "\n");
    const r = both(pk, base, [...FAST, "--session", "sess-r"], cleanEnv({ HOME: home }));
    expect(r.py.code).toBe(0);
    expectSame(r);
    expect(r.ts.out).toContain("$38.0697 API-equivalent (session $38.0697;");
  }, 240_000);

  test("a pytest repo: the declared command names the Python eval's interpreter, string for string", () => {
    const base = tmp("pytest");
    const pk = snapshot(PACKETS[0]!, base);
    const repo = join(base, "pyrepo");
    mkdirSync(repo);
    withRepo(pk, scratchRepo(repo, { "pyproject.toml": "[tool.pytest.ini_options]\n" }));
    const r = both(pk, base, FAST);
    expect(r.py.code).toBe(0);
    expectSame(r);
    const qa = JSON.parse(readFileSync(join(r.ts.dir, "output.qa.json"), "utf8"));
    const tc = qa.scores.find((x: { name: string }) => x.name === "qa_test_command");
    expect(tc.reason).toBe(`pass --run-tests to run \`${python(cleanEnv())} -m pytest -q\` (pyproject.toml [tool.pytest]) in ${repo}`);
  }, 240_000);

  test("--run-tests on a scratch repo with a dummy test command", () => {
    const base = tmp("runtests");
    const pk = snapshot(PACKETS[0]!, base);
    const repo = join(base, "jsrepo");
    mkdirSync(repo);
    withRepo(pk, scratchRepo(repo, { "package.json": `{"name": "fixture", "scripts": {"test": "echo qa-ok && exit 0"}}\n`, "bun.lock": "" }));
    const r = both(pk, base, [...FAST, "--run-tests"]);
    expect(r.py.code).toBe(0);
    expectSame(r);
    const qa = JSON.parse(readFileSync(join(r.ts.dir, "output.qa.json"), "utf8"));
    expect(qa.scores.find((x: { name: string }) => x.name === "qa_test_command")).toMatchObject({ value: 1, status: "ok" });
  }, 240_000);
});

// Opt-in (about 4 minutes): TDD_EVAL_SLOW=1. A scratch repo whose test command pipes a 170 s sleep through cat: both
// evals must stop at the 120 s cap ("too slow"); before the M1 fix the TypeScript waited the full 170 s.
describe.skipIf(!PACKETS.length || !process.env.TDD_EVAL_SLOW)("--run-tests: the 120 s cap is a deadline", () => {
  test("a test command whose grandchild holds the pipes", () => {
    const base = tmp("slow");
    const pk = snapshot(PACKETS[0]!, base);
    const repo = join(base, "slowrepo");
    mkdirSync(repo);
    withRepo(pk, scratchRepo(repo, { "package.json": `{"name": "fixture", "scripts": {"test": "sleep 170.3 | cat"}}\n`, "bun.lock": "" }));
    const t0 = performance.now();
    const r = both(pk, base, ["--no-shots", "--no-gh", "--skip", "artifact_check,check_hydrate,lint,linear,aws", "--run-tests"]);
    const wall = (performance.now() - t0) / 1000;
    expect(r.py.code).toBe(0);
    expectSame(r);
    expect(wall).toBeLessThan(2 * 120 + 40); // two evals at the cap; with the old wait it was over 290 s
    const qa = JSON.parse(readFileSync(join(r.ts.dir, "output.qa.json"), "utf8"));
    expect(qa.scores.find((x: { name: string }) => x.name === "qa_test_command")).toMatchObject({ status: "not run", reason: "too slow (> 120 s)" });
  }, 400_000);
});
