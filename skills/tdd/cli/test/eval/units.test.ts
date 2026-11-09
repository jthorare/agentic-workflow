// The eval's constants and small functions against the Python (test/eval/oracle_eval.py loads scripts/eval/*.py by
// path): the usage text, flags, the TOKEN pattern (from src/vocab.ts), prices, the judge and probe prompts and schemas,
// the house slots, the writer's limits; unicode_escape, str.format, date differences, SequenceMatcher similarity,
// cost(), base_type, the probe matcher, README link reading; the transcript glob order; argv errors; redacted keys.
import { afterAll, describe, expect, test } from "bun:test";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { Effect, Option, Redacted } from "effect";
import * as py from "../../src/py";
import { main as evalMain, parse } from "../../bin/eval-packet";
import { HOUSE_SLOTS, houseSlots } from "../../src/eval/artifact";
import { loadKeys } from "../../src/eval/config";
import { daysBetween, similar } from "../../src/eval/impl";
import * as L from "../../src/eval/llm";
import * as J from "../../src/eval/judge";
import { EXEMPT, README_MAX, SLICE_BUDGET, frontAnchors, unicodeEscape } from "../../src/eval/packet";
import * as P from "../../src/eval/probe";
import { EVAL_PACKET_DOC } from "../../src/eval/texts";
import { sessionFiles } from "../../src/eval/tokens";
import { TOKEN_SOURCE } from "../../src/vocab";
import { cleanEnv, cleanup, HAVE_PYTHON, oracle, PY_EVAL, PYTHON, SKILL, tmp } from "./harness";

afterAll(cleanup);

const toPy = (v: unknown): unknown => py.json.loads(JSON.stringify(v));
const plain = (v: unknown): unknown => JSON.parse(py.json.dumps(v));

describe.skipIf(!HAVE_PYTHON)("eval constants equal the Python's", () => {
  const C = HAVE_PYTHON ? oracle("constants") : ({} as Record<string, any>); // eslint-disable-line @typescript-eslint/no-explicit-any
  test("usage text, flags, TOKEN, judge model, prices", () => {
    expect(EVAL_PACKET_DOC).toBe(C.doc);
    expect(TOKEN_SOURCE).toBe(C.token);
    expect(L.JUDGE_MODEL).toBe(C.judge_model);
    expect(L.PRICE_SOURCE).toBe(C.price_source);
    expect(plain(new Map(L.PRICES.map(([k, v]) => [k, new Map(Object.entries(v).map(([a, b]) => [a, py.pyFloat(b)]))])))).toEqual(
      Object.fromEntries(C.prices));
    expect(L.PRICES.map(([k]) => k)).toEqual(C.prices.map((p: [string]) => p[0]));
  });
  test("judge and probe prompts and schemas", () => {
    expect(J.CRITERIA).toEqual(C.criteria);
    expect(P.SYSTEM).toBe(C.probe_system);
    expect(P.SCHEMA).toEqual(C.probe_schema);
    const help = Bun.spawnSync(["bun", join(SKILL, "cli", "bin", "eval-packet.ts"), "--help"]).stdout.toString();
    expect(help).toBe(C.doc + "\n");
  });
  test("house slots: the mirror, and the linter's own list read from lint_packet.py", () => {
    expect(HOUSE_SLOTS.map(([n, ids, rx]) => [n, py.sorted(ids), rx])).toEqual(C.house_slots);
    const [slots, origin] = houseSlots();
    expect(origin).toBe("lint_packet.Lint.SLOTS");
    expect(slots.map(([n, ids, rx]) => [n, py.sorted(ids), rx])).toEqual(C.lint_slots);
  });
  test("the agents writer's limits", () => {
    expect(README_MAX).toBe(C.readme_max);
    expect(SLICE_BUDGET).toBe(C.budget);
    expect(py.sorted(EXEMPT)).toEqual(C.exempt);
  });
});

describe.skipIf(!HAVE_PYTHON)("eval functions equal the Python's", () => {
  const cases = {
    unicode_escape: ["plain", "a\\nb", "tab\\tq\\\"x\\\\", "\\x41\\u00e9\\U0001F600", "\\101\\0", "café", "€\\n", "\\q unknown", "\\\nline", "\\x4", "end\\"],
    front_anchors: ["---\ntitle: x\nanchors: [\"a\", \"b-2\", \"c\\u00e9\"]\n---\nbody", "---\nanchors: [plain, two words ,  ]\n---", "no front\nanchors: [\"a\"]",
      "---\nanchors: []\n---", "---\nanchors: [\"q\\\"x\", y]\n---\n---"],
    format: [["Which decision settles this question: \"{question}\"?", { question: "Q?" }], ["{a}{{b}} {c}", { a: 1, c: null }], ["{x}", { x: 2.5 }]],
    days: [["2026-11-06", "2026-11-09T10:00:00Z"], ["2026-11-09", "2026-01-01"], [null, "2026-01-01"], ["20261101", "2026-11-03"], ["2026-02-30", "2026-03-01"], ["x", "y"]],
    similar: [["Add the ranking table (#12)", "add the ranking table"], ["Open-shift intention", "open shift intention, workflow"], ["", ""], ["a b c", "x y z"]],
    cost: [["claude-opus-5-5", { input_tokens: 1000, output_tokens: 2000, cache_read_input_tokens: 300, cache_creation_input_tokens: 40 }],
      ["claude-fable-5-1[1m]", { input_tokens: 5, output_tokens: 7, cache_creation: { ephemeral_5m_input_tokens: 11, ephemeral_1h_input_tokens: 13 } }],
      ["claude-opus-5-5-20260101", { input_tokens: 3 }], ["unknown-model", { input_tokens: 3 }], [null, {}], ["claude-haiku-4-5", { output_tokens: 123456789 }]],
    base_type: ["uuid PRIMARY KEY DEFAULT gen_random_uuid()", "text not null", "  numeric(10,2)  ", "timestamptz\nDEFAULT now()", "", null],
    matches: [
      [{ expected: "PR 3", match: "token" }, "It is pr-3."], [{ expected: "PR 3", match: "token" }, "PR 31"],
      [{ expected: "decided", match: "oneof", accept: { decided: ["decided", "done"], open: ["open"] } }, "It is decided (done)."],
      [{ expected: "decided", match: "oneof", accept: { decided: ["decided"], open: ["open"] } }, "decided but open"],
      [{ expected: ["open_shift_award", "profile_seniority"], match: "all" }, "Open_Shift_Award and PROFILE_seniority"],
      [{ expected: "1,234.5 rows", match: "numbers" }, "about 1234.5"], [{ expected: "uuid", match: "contains" }, "U U I D"],
      [{ expected: "5de1ab91d9", match: "prefix" }, "at 5DE1AB9"], [{ expected: "x", match: "other" }, "x"],
    ],
    readme_links: ["# T\n[a](a.md) [b](b.md \"t\")\n## Optional\n[c](c.md)\n### More\n[d](https://x.y)\n"],
  };
  const file = join(tmp("funcs"), "cases.json");
  writeFileSync(file, JSON.stringify(cases));
  const G = HAVE_PYTHON ? oracle("funcs", [file]) : ({} as Record<string, any>); // eslint-disable-line @typescript-eslint/no-explicit-any
  test("unicode_escape and front matter anchors", () => {
    expect(cases.unicode_escape.map((s) => { try { return unicodeEscape(s); } catch { return { error: "UnicodeDecodeError" }; } })).toEqual(G.unicode_escape);
    expect(cases.front_anchors.map(frontAnchors)).toEqual(G.front_anchors);
  });
  test("str.format, days between, title similarity", () => {
    expect(cases.format.map(([t, f]) => P.pyFormat(t as string, f as Record<string, unknown>))).toEqual(G.format);
    expect(cases.days.map(([a, b]) => daysBetween(a, b))).toEqual(G.days);
    expect(cases.similar.map(([a, b]) => similar(a, b))).toEqual(G.similar);
  });
  test("cost(), base_type, the probe matcher, README links", () => {
    expect(cases.cost.map(([m, u]) => { const c = L.cost(m, toPy(u)); return c === null ? null : c.value; })).toEqual(G.cost);
    expect(cases.base_type.map(P.baseType)).toEqual(G.base_type);
    expect(cases.matches.map(([q, a]) => P.matches(toPy(q) instanceof Map ? Object.fromEntries((toPy(q) as Map<string, never>)) as never : (q as never), a))).toEqual(G.matches);
    expect(cases.readme_links.map(P.readmeLinks)).toEqual(G.readme_links);
  });
  test("transcript lookup follows glob's order under $HOME", () => {
    const home = tmp("glob");
    const root = join(home, ".claude", "projects");
    for (const d of ["zeta", "alpha", "Mid", ".hidden", "beta"]) mkdirSync(join(root, d, "s1", "subagents"), { recursive: true });
    for (const d of ["zeta", "alpha", ".hidden", "beta"]) writeFileSync(join(root, d, "s1.jsonl"), "{}\n");
    for (const f of ["b.jsonl", "a.jsonl", ".x.jsonl", "c.txt", "Z.jsonl"]) writeFileSync(join(root, "alpha", "s1", "subagents", f), "{}\n");
    writeFileSync(join(root, "beta", "s1", "subagents", "q.jsonl"), "{}\n");
    writeFileSync(join(root, "plainfile"), "x");
    const want = oracle("glob", ["s1", "nope"], cleanEnv({ HOME: home }));
    const prev = process.env.HOME;
    process.env.HOME = home;
    try {
      expect({ s1: sessionFiles("s1"), nope: sessionFiles("nope") }).toEqual(want);
    } finally {
      process.env.HOME = prev;
    }
  });
});

describe.skipIf(!HAVE_PYTHON)("argv: usage errors and exit codes as eval_packet.py", () => {
  const cases = [["--build"], ["--nope", "x"], [], ["a", "b"], ["/no/such/packet/dir"], ["--skip"]];
  for (const argv of cases)
    test(JSON.stringify(argv), () => {
      const p = Bun.spawnSync([PYTHON, "-I", "-B", PY_EVAL, ...argv], { env: cleanEnv(), stdout: "pipe", stderr: "pipe" });
      const t = Bun.spawnSync(["bun", join(SKILL, "cli", "bin", "eval-packet.ts"), ...argv], { env: cleanEnv(), stdout: "pipe", stderr: "pipe" });
      expect(t.exitCode).toBe(p.exitCode);
      expect(t.stderr.toString()).toBe(p.stderr.toString());
      expect(t.stdout.toString()).toBe(p.stdout.toString());
    });
  test("parse and main return the code instead of exiting", async () => {
    expect(parse(["--json"])).toBe(2);
    expect(await evalMain(["--project"])).toBe(2);
  });
});

describe("keys come from the environment as redacted config", () => {
  test("present, absent, empty; never printed", async () => {
    const keys = await Effect.runPromise(loadKeys({ BRAINTRUST_API_KEY: "bt-secret-123", ANTHROPIC_API_KEY: "", AWS_DEFAULT_REGION: "eu-west-1", PATH: "/bin" }));
    expect(Option.isSome(keys.braintrust)).toBe(true);
    expect(Option.isNone(keys.anthropic)).toBe(true); // an empty variable counts as absent, as in the Python
    expect(Option.isNone(keys.linear)).toBe(true);
    expect(keys.aws.region).toEqual(Option.some("eu-west-1"));
    const shown = [JSON.stringify(keys), String(Option.getOrThrow(keys.braintrust)), `${Bun.inspect(keys)}`].join("\n");
    expect(shown).not.toContain("bt-secret-123");
    expect(Redacted.value(Option.getOrThrow(keys.braintrust))).toBe("bt-secret-123");
  });
});
