// repr() and truthiness against CPython: 2,006 strings mixing both quotes, backslashes, C0/C1 controls, format and
// private-use characters, astral and unassigned code points, lone surrogates; 500 nested values; bool() of 221 values.
import { describe, expect, test } from "bun:test";
import { PyFloat } from "../../src/py/num";
import { PyTuple, repr, strRepr } from "../../src/py/repr";
import { truthy } from "../../src/py/value";
import { SKIP_REASON, fromTag, hasPython, oracle, type Tagged } from "./oracle";

const T = 60_000;

describe("repr / truthy: fixed cases", () => {
  test("R12 example and the JS truthiness traps", () => {
    expect(strRepr("it's \"q\" \n é")).toBe("'it\\'s \"q\" \\n é'");
    expect(repr(new Map<string, unknown>([["a", [1, new PyFloat(2), null, true]]]))).toBe("{'a': [1, 2.0, None, True]}");
    expect(repr(new PyTuple("x"))).toBe("('x',)");
    expect([truthy([]), truthy(new Map()), truthy(""), truthy(0), truthy(new PyFloat(0)), truthy(null)]).toEqual([false, false, false, false, false, false]);
    expect([truthy([0]), truthy(" "), truthy(NaN), truthy(new PyFloat(NaN)), truthy(-1n)]).toEqual([true, true, true, true, true]);
  });
});

describe.skipIf(!hasPython)("repr / truthy vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("repr of strings and nested values", () => {
    const g = oracle<{ strings: [string, string][]; values: [Tagged, string][] }>("repr");
    expect(g.strings.filter(([s, r]) => strRepr(s) !== r).slice(0, 10)).toEqual([]);
    expect(g.values.filter(([t, r]) => repr(fromTag(t)) !== r).slice(0, 10).map(([, r]) => r)).toEqual([]);
  }, T);

  test("bool(v)", () => {
    const g = oracle<{ rows: [Tagged, boolean][] }>("truthy");
    expect(g.rows.filter(([t, b]) => truthy(fromTag(t)) !== b).map(([t]) => JSON.stringify(t)).slice(0, 10)).toEqual([]);
  }, T);
});
