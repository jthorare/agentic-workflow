// Python's - / * (src/py/ops) against live CPython: every pair of a set of operands (ints small and huge, bools, floats
// with zeros, infinities and NaN, None, str, list, dict), each operator, the repr of the result or the exception's type
// and text.
import { describe, expect, test } from "bun:test";
import { PyError } from "../../src/py/errors";
import { PyFloat } from "../../src/py/num";
import { mul, sub, truediv } from "../../src/py/ops";
import { repr } from "../../src/py/repr";
import { pyDict, type PyValue } from "../../src/py/value";
import { SKIP_REASON, hasPython } from "./oracle";

/** [Python literal, the kernel's value for it] */
const OPERANDS: readonly (readonly [string, () => PyValue])[] = [
  ["0", () => 0], ["3", () => 3], ["-7", () => -7], ["400", () => 400], ["2**53 + 1", () => 2n ** 53n + 1n], ["2**60", () => 2n ** 60n],
  ["-(3**40)", () => -(3n ** 40n)], ["10**400", () => 10n ** 400n], ["10**399", () => 10n ** 399n], ["True", () => true], ["False", () => false],
  ["0.0", () => new PyFloat(0)], ["-0.0", () => new PyFloat(-0)], ["2.5", () => new PyFloat(2.5)], ["200.04", () => new PyFloat(200.04)],
  ["1e308", () => new PyFloat(1e308)], ["5e-324", () => new PyFloat(5e-324)], ["float('inf')", () => new PyFloat(Infinity)],
  ["float('nan')", () => new PyFloat(NaN)], ["None", () => null], ["'0'", () => "0"], ["'ab'", () => "ab"], ["''", () => ""],
  ["[1]", () => [1]], ["{}", () => pyDict()],
];
const OPS: Record<string, (a: unknown, b: unknown) => PyValue> = { "-": sub, "/": truediv, "*": mul };

/** str or list times an int that does not fit in memory: CPython's MemoryError / OverflowError texts are out of scope */
const huge = (a: string, b: string, op: string) => {
  const seq = (x: string) => x.startsWith("'") || x.startsWith("[");
  const big = (x: string) => x.includes("**");
  return op === "*" && ((seq(a) && big(b)) || (seq(b) && big(a)));
};

function cpython(): Record<string, string> {
  const cases = OPERANDS.flatMap(([a]) => OPERANDS.flatMap(([b]) => Object.keys(OPS).filter((op) => !huge(a, b, op)).map((op) => [a, op, b])));
  const code = `import json, sys
out = {}
for a, op, b in json.load(sys.stdin):
    try:
        r = repr(eval(f"({a}) {op} ({b})"))
    except Exception as e:
        r = f"{type(e).__name__}: {e}"
    out[f"{a} {op} {b}"] = r
print(json.dumps(out))`;
  const r = Bun.spawnSync(["python3", "-I", "-B", "-c", code], { stdin: new TextEncoder().encode(JSON.stringify(cases)), stdout: "pipe", stderr: "pipe" });
  if (r.exitCode !== 0) throw new Error(r.stderr.toString());
  return JSON.parse(r.stdout.toString());
}

describe.skipIf(!hasPython)("ops vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("a - b, a / b, a * b over every operand pair: the result's repr or the exception", () => {
    const want = cpython();
    let n = 0;
    for (const [a, va] of OPERANDS) {
      for (const [b, vb] of OPERANDS) {
        for (const [op, fn] of Object.entries(OPS)) {
          if (huge(a, b, op)) continue;
          let got: string;
          try {
            got = repr(fn(va(), vb()));
          } catch (e) {
            if (!(e instanceof PyError)) throw e;
            got = `${e.type}: ${e.message}`;
          }
          expect(`${a} ${op} ${b} => ${got}`).toBe(`${a} ${op} ${b} => ${want[`${a} ${op} ${b}`]}`);
          n++;
        }
      }
    }
    expect(n).toBeGreaterThan(1800);
  });

  test("the F5 case: a hotspot viewBox of strings, as build_tdd.py's hotspots() fails on it", () => {
    expect(() => sub(new PyFloat(12.5), "0")).toThrow("unsupported operand type(s) for -: 'float' and 'str'");
    expect(() => truediv(new PyFloat(12.5), "200.04")).toThrow("unsupported operand type(s) for /: 'float' and 'str'");
    expect(() => truediv(5, 0)).toThrow("division by zero");
    expect(() => truediv(new PyFloat(5), 0)).toThrow("float division by zero");
  });
});
