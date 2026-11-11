// Every Unicode table the kernel embeds (src/py/ucd.ts) against the live CPython, over all 1,114,112 code points:
// re's \w \d \s, str.isspace / split() / strip() / splitlines() boundaries, isprintable, lower() and upper().
import { describe, expect, test } from "bun:test";
import * as re from "../../src/py/re";
import { lower, split, splitlines, strip, upper } from "../../src/py/str";
import { PYTHON_VERSION, UNICODE_VERSION } from "../../src/py/ucd";
import { isDecimalCp, isPrintableCp, isSpaceCp, isWordCp } from "../../src/py/uni";
import { SKIP_REASON, hasPython, oracle, sweepDiff } from "./oracle";

type Runs = [number, number][];
interface Ucd {
  unicode: string; python: string; w: Runs; d: Runs; s: Runs; isspace: Runs; printable: Runs; linebreak: Runs;
  split_ws: Runs; strip_ws: Runs; lower: Record<string, string>; upper: Record<string, string>;
}

describe.skipIf(!hasPython)("unicode tables vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  const g = () => oracle<Ucd>("ucd");
  const T = 60_000;

  test("same Unicode version as the tables were generated from", () => {
    expect(g().unicode).toBe(UNICODE_VERSION);
    expect(g().python.split(".").slice(0, 2)).toEqual(PYTHON_VERSION.split(".").slice(0, 2));
  }, T);

  test("membership tables: \\w \\d \\s isprintable", () => {
    expect(sweepDiff(g().w, isWordCp)).toEqual([]);
    expect(sweepDiff(g().d, isDecimalCp)).toEqual([]);
    expect(sweepDiff(g().s, isSpaceCp)).toEqual([]);
    expect(sweepDiff(g().printable, isPrintableCp)).toEqual([]);
  }, T);

  test("re: \\w \\d \\s \\W \\D \\S and classes match one code point as Python's do", () => {
    const one = (p: string) => {
      const rx = re.compile(p);
      return (c: number) => rx.fullmatch(String.fromCodePoint(c)) !== null;
    };
    expect(sweepDiff(g().w, one("\\w"))).toEqual([]);
    expect(sweepDiff(g().d, one("\\d"))).toEqual([]);
    expect(sweepDiff(g().s, one("\\s"))).toEqual([]);
    expect(sweepDiff(g().w, (c) => !one("\\W")(c))).toEqual([]);
    expect(sweepDiff(g().d, (c) => !one("[\\D]")(c))).toEqual([]);
    expect(sweepDiff(g().s, (c) => !one("[^\\s]")(c))).toEqual([]);
    expect(sweepDiff(g().w, one("[\\w]"))).toEqual([]);
  }, T);

  test("str: split() / strip() / splitlines() boundaries over every code point", () => {
    expect(sweepDiff(g().split_ws, (c) => split("x" + String.fromCodePoint(c) + "y").length === 2)).toEqual([]);
    expect(sweepDiff(g().strip_ws, (c) => strip(String.fromCodePoint(c) + "x" + String.fromCodePoint(c)) === "x")).toEqual([]);
    expect(sweepDiff(g().linebreak, (c) => splitlines("x" + String.fromCodePoint(c) + "y").length === 2)).toEqual([]);
  }, T);

  test("lower() and upper() of every code point", () => {
    const bad: string[] = [];
    for (let c = 0; c < 0x110000 && bad.length < 8; c++) {
      const ch = String.fromCodePoint(c);
      if (lower(ch) !== (g().lower[String(c)] ?? ch)) bad.push(`lower U+${c.toString(16)}`);
      if (upper(ch) !== (g().upper[String(c)] ?? ch)) bad.push(`upper U+${c.toString(16)}`);
    }
    expect(bad).toEqual([]);
  }, T);
});
