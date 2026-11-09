// Two Python rules the eval's records depend on, checked against CPython (test/eval/oracle_eval.py, suite "sum"):
//   sum()        CPython 3.12+ adds floats with Neumaier compensation, so a naive left fold drifts in the 4th decimal
//                of a cost line (review M2); pySum must equal sum() on cost-like sets, wide magnitudes, int/float/bool
//                mixes, signed zeros, infinities, NaN and ints past a C long.
//   interpreter  lint_packet.py and pytest run under the Python eval's own sys.executable; python() derives the same
//                absolute path from $TDD_PYTHON, the tdd-skill venv or python3 on PATH (review M3), and the pytest
//                command declared for a repo is the Python's string for string.
import { afterAll, describe, expect, test } from "bun:test";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import * as py from "../../src/py";
import { PyError } from "../../src/py/errors";
import { PyFloat } from "../../src/py/num";
import { pyStr } from "../../src/py/repr";
import { pySum } from "../../src/eval/common";
import { python } from "../../src/eval/artifact";
import { declaredTestCommand } from "../../src/eval/impl";
import { cost } from "../../src/eval/llm";
import { cleanEnv, cleanup, HAVE_PYTHON, oracle, PYTHON, scratchRepo, tmp } from "./harness";

afterAll(cleanup);

/** A seeded generator (a 31-bit LCG), so a failure names a reproducible case. */
function rng(seed: number): () => number {
  let s = seed >>> 0;
  return () => {
    s = (Math.imul(s, 1103515245) + 12345) >>> 0;
    return (s & 0x7fffffff) / 0x80000000;
  };
}

/** The review's three-message --session case: per-model costs as ev_tokens.sum_usage rounds them (round(0.0 + c, 6)). */
const REVIEW_THREE = ([["claude-opus-5-5", 920875], ["claude-sonnet-5-5", 8411685], ["claude-haiku-5-5", 175627800]] as const).map(([m, t]) =>
  new PyFloat(py.round(cost(m, py.json.loads(`{"input_tokens": ${t}, "output_tokens": 0}`))!.value, 6)));

function mkdir(p: string): string {
  mkdirSync(p, { recursive: true });
  return p;
}

function cases(): unknown[][] {
  const r = rng(20261109);
  const f = (x: number) => new PyFloat(x);
  const out: unknown[][] = [REVIEW_THREE];
  // per-model costs as ev_tokens rounds them (6 dp, $0..$50), 2 to 5 models: the review's 0.17 % drift lives here
  for (let i = 0; i < 4000; i++) out.push(Array.from({ length: 2 + Math.floor(r() * 4) }, () => f(py.round(r() * 50, 6))));
  // wide magnitudes with signs, 1 to 9 terms
  for (let i = 0; i < 1500; i++)
    out.push(Array.from({ length: 1 + Math.floor(r() * 9) }, () => f((r() < 0.5 ? -1 : 1) * r() * 10 ** Math.floor(r() * 40 - 20))));
  // ints, bools and floats mixed (the int phase, then the float phase, ints inside the float phase)
  for (let i = 0; i < 800; i++)
    out.push(Array.from({ length: 1 + Math.floor(r() * 7) }, () => {
      const k = r();
      return k < 0.35 ? Math.floor(r() * 2e6) - 1e6 : k < 0.45 ? r() < 0.5 : k < 0.55 ? f(Math.floor(r() * 100)) : f((r() - 0.5) * 1e3);
    }));
  const big = 2n ** 63n;
  out.push(
    [], [0], [true, true], [f(-0)], [f(-0), f(-0)], [0, f(-0)], [f(-0), 0], [f(1e16), f(1), f(-1e16)], [f(1), f(1e100), f(1), f(-1e100)],
    [f(0.1), f(0.2), f(0.3)], [f(Infinity), f(1)], [f(Infinity), f(-Infinity)], [f(1e308), f(1e308)], [f(1e308), f(1e308), f(-1e308)],
    [f(NaN), f(1)], [f(1.5), big], [big, f(1.5)], [f(1.5), 2n ** 64n, f(0.1)], [2n ** 62n, 2n ** 62n], [big - 1n, 1], [10n ** 400n, f(1)],
    [f(0.5), 10n ** 400n], [f(1e-300), f(1e300), f(-1e300), f(1e-300)], [3, f(0.1), 4, f(0.2), true],
  );
  return out;
}

const shown = (v: unknown): string => (v instanceof PyFloat ? `float:${pyStr(v)}` : `int:${String(v)}`);

describe.skipIf(!HAVE_PYTHON)("sum() as CPython 3.13 computes it", () => {
  const all = cases();
  const dir = tmp("sum");
  const file = join(dir, "cases.json");
  writeFileSync(file, py.json.dumps(all));
  const golden = oracle<{ sums: string[] }>("sum", [file]);
  test(`pySum equals sum() on ${all.length} lists`, () => {
    const loaded = py.json.loads(py.json.dumps(all)) as unknown[][]; // the kernel's own values, as the eval reads them
    const mine = loaded.map((xs) => {
      try {
        return shown(pySum(xs));
      } catch (e) {
        return `error:${e instanceof PyError ? e.type : "?"}`;
      }
    });
    const diffs = mine.flatMap((m, i) => (m === golden.sums[i] ? [] : [`#${i} ${py.json.dumps(all[i])}: python ${golden.sums[i]} | ts ${m}`]));
    expect(diffs.slice(0, 10)).toEqual([]);
    expect(golden.sums.length).toBe(all.length);
  });
  test("the review's three-message session: $38.0697 as Python prints it, where a left fold gives $38.0696", () => {
    expect(golden.sums[0]).toBe(shown(pySum(REVIEW_THREE)));
    expect(py.round((pySum(REVIEW_THREE) as PyFloat).value, 4)).toBe(38.0697);
    expect(py.round(REVIEW_THREE.reduce((a, c) => a + c.value, 0), 4)).toBe(38.0696); // the old pySum's answer
  });
});

describe.skipIf(!HAVE_PYTHON)("the interpreter: python() is the Python eval's sys.executable", () => {
  const dir = tmp("interp");
  const pyRepo = scratchRepo(mkdir(join(dir, "pyrepo")), { "pyproject.toml": "[tool.pytest.ini_options]\n" });
  const cases = join(dir, "none.json");
  writeFileSync(cases, "[]");
  test("in this environment: the same executable and the same declared pytest command", () => {
    const g = oracle<{ commands: [string[] | null, string | null][]; executable: string }>("sum", [cases, pyRepo]);
    expect(python()).toBe(g.executable);
    expect(declaredTestCommand(pyRepo)).toEqual(g.commands[0]!);
    expect(g.commands[0]![0]).toEqual([g.executable, "-m", "pytest", "-q"]);
  });
  test("without the venv (a HOME that has none) and with TDD_PYTHON: each still matches sys.executable", () => {
    const home = join(dir, "home");
    mkdirSync(home);
    const env = cleanEnv({ HOME: home });
    delete env.TDD_PYTHON;
    const plain = python(env);
    expect(plain.startsWith("/")).toBe(true);
    expect(oracle<{ executable: string }>("sum", [cases], env, plain).executable).toBe(plain);
    const explicit = python({ ...env, TDD_PYTHON: PYTHON });
    expect(explicit).toBe(PYTHON);
    expect(oracle<{ executable: string }>("sum", [cases], env, explicit).executable).toBe(explicit);
  });
});
