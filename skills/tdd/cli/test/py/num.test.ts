// Python numbers against CPython: round(x, n) half-even on the exact binary value (100,000 random values, every k/8
// tie up to 100, both signs), round(x) to int, float repr (100,000 random doubles plus every power of ten), int().
import { describe, expect, test } from "bun:test";
import { PyError } from "../../src/py/errors";
import { floatRepr, int, intRepr, round, roundInt } from "../../src/py/num";
import { SKIP_REASON, hasPython, oracle, unhex } from "./oracle";

const T = 60_000;

describe("num: fixed cases", () => {
  test("R8/R9 examples from the research", () => {
    expect(round(12.625, 2)).toBe(12.62);
    expect(round(0.125, 2)).toBe(0.12);
    expect(roundInt(2.5)).toBe(2);
    expect(roundInt(575 / 230)).toBe(2);
    expect(floatRepr(50)).toBe("50.0");
    expect(floatRepr(1e-5)).toBe("1e-05");
    expect(floatRepr(1e16)).toBe("1e+16");
    expect(floatRepr(-0)).toBe("-0.0");
    expect(intRepr(2n ** 70n)).toBe("1180591620717411303424");
  });
});

describe.skipIf(!hasPython)("num vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("round(x, n) for n in 0..3, -1, -2, +-400 and round(x)", () => {
    const g = oracle<{ ndigits: number[]; rows: [string, string, string[], string][] }>("round");
    expect(g.rows.length).toBeGreaterThanOrEqual(100_000);
    const ties = g.rows.filter(([h]) => Number.isInteger(unhex(h) * 8) && Math.abs(unhex(h)) <= 100).length;
    expect(ties).toBeGreaterThanOrEqual(1601);
    const bad: string[] = [];
    for (const [h, r, rs, ri] of g.rows) {
      const x = unhex(h);
      if (floatRepr(x) !== r) bad.push(`repr ${r} -> ${floatRepr(x)}`);
      g.ndigits.forEach((n, k) => {
        const got = floatRepr(round(x, n));
        if (got !== rs[k]) bad.push(`round(${r}, ${n}) = ${rs[k]}, got ${got}`);
      });
      let gi: string;
      try {
        gi = intRepr(roundInt(x));
      } catch (e) {
        gi = e instanceof PyError ? e.type : String(e);
      }
      if (gi !== ri) bad.push(`round(${r}) = ${ri}, got ${gi}`);
      if (bad.length > 10) break;
    }
    expect(bad).toEqual([]);
  }, T);

  test("float repr", () => {
    const g = oracle<{ rows: [string, string][] }>("repr_float");
    expect(g.rows.length).toBeGreaterThanOrEqual(100_000);
    const bad = g.rows.filter(([h, r]) => floatRepr(unhex(h)) !== r).slice(0, 10);
    expect(bad).toEqual([]);
  }, T);

  test("int(s, base): Unicode digits and whitespace, underscores, prefixes, CPython's ValueError text", () => {
    const g = oracle<{ rows: ([string, number, string] | [string, number, null, string])[] }>("int");
    const bad: string[] = [];
    let unicodeDigits = 0;
    for (const row of g.rows) {
      const [s, base, want, err] = row as [string, number, string | null, string?];
      let got: string;
      try {
        got = intRepr(int(s, base));
        if (want === null) bad.push(`${JSON.stringify(s)} base ${base}: expected ${err}, got ${got}`);
        else if (got !== want) bad.push(`${JSON.stringify(s)} base ${base}: ${want}, got ${got}`);
        else if (/[^\x00-\x7f]/.test(s)) unicodeDigits++;
      } catch (e) {
        const m = e instanceof PyError ? e.message : String(e);
        if (want !== null || m !== err) bad.push(`${JSON.stringify(s)} base ${base}: ${want ?? err}, got error ${m}`);
      }
      if (bad.length > 10) break;
    }
    expect(bad).toEqual([]);
    expect(unicodeDigits).toBeGreaterThan(600); // every Unicode decimal digit converts
  }, T);
});
