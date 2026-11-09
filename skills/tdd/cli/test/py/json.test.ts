// The lossless JSON round trip against CPython: dumps of 1,506 random value trees under ten option sets, loads of 316
// documents compared by type, value and key order, CPython's JSONDecodeError text, and (with $TDD_PARITY_PACKETS)
// parse-then-dump of real packets' build/content.json (indent 1), content.files.json (indent 2) and lint.json.
import { describe, expect, test } from "bun:test";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { PyError } from "../../src/py/errors";
import { decodeUtf8 } from "../../src/py/io";
import { dumps, loads, type DumpsOptions } from "../../src/py/json";
import { PyFloat } from "../../src/py/num";
import { PACKETS, SKIP_REASON, fromTag, hasPython, oracle, toTag, type Tagged } from "./oracle";

const T = 60_000;

type PyOpts = { indent?: number | string; ensure_ascii?: boolean; sort_keys?: boolean; separators?: [string, string] };
const opts = (o: PyOpts): DumpsOptions => ({
  ...(o.indent !== undefined ? { indent: o.indent } : {}),
  ...(o.ensure_ascii !== undefined ? { ensureAscii: o.ensure_ascii } : {}),
  ...(o.sort_keys !== undefined ? { sortKeys: o.sort_keys } : {}),
  ...(o.separators !== undefined ? { separators: o.separators } : {}),
});

describe("json: fixed cases", () => {
  test("R8 R10 R11 examples", () => {
    const v = loads('{"b":1,"2":2,"a":3,"1":4,"f":1.0,"g":[1, 2.50, 1e16]}');
    expect(dumps(v)).toBe('{"b": 1, "2": 2, "a": 3, "1": 4, "f": 1.0, "g": [1, 2.5, 1e+16]}');
    expect(dumps({ a: [1, 2] })).toBe('{"a": [1, 2]}');
    expect(dumps([new PyFloat(50), new PyFloat(1e-5), new PyFloat(0)])).toBe("[50.0, 1e-05, 0.0]");
    expect(dumps("é😀\x00", { ensureAscii: true })).toBe('"\\u00e9\\ud83d\\ude00\\u0000"');
    expect(loads("12345678901234567890")).toBe(12345678901234567890n);
  });
});

describe.skipIf(!hasPython)("json vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("dumps under every option set; loads by type, value and key order", () => {
    const g = oracle<{ opts: PyOpts[]; rows: [Tagged, string[]][]; loads: [string, Tagged, string][]; misc: Record<string, string> }>("json");
    const bad: string[] = [];
    for (const [t, outs] of g.rows) {
      const v = fromTag(t);
      g.opts.forEach((o, k) => {
        const got = dumps(v, opts(o));
        if (got !== outs[k] && bad.length < 8) bad.push(`dumps ${JSON.stringify(o)}: ${outs[k]} != ${got}`);
      });
    }
    for (const [doc, t, d1] of g.loads) {
      const v = loads(doc);
      if (JSON.stringify(toTag(v)) !== JSON.stringify(t) && bad.length < 12) bad.push(`loads ${JSON.stringify(doc).slice(0, 80)}`);
      if (dumps(v, { indent: 1, ensureAscii: false }) !== d1 && bad.length < 12) bad.push(`round trip ${JSON.stringify(doc).slice(0, 80)}`);
    }
    expect(bad).toEqual([]);
    expect(() => dumps(new PyFloat(NaN), { allowNan: false })).toThrow(g.misc.allow_nan!);
    expect(dumps(new Map<string, unknown>([["a", new Set([3, 1, 2])], ["b", [null]]]) as never, { default: (o) => [...(o as Set<number>)].sort() })).toBe(g.misc.default!);
  }, T);

  test("JSONDecodeError text", () => {
    const g = oracle<{ rows: [string, string | null][] }>("json_errors");
    const bad: string[] = [];
    for (const [doc, want] of g.rows) {
      let got: string | null = null;
      try {
        loads(doc);
      } catch (e) {
        got = e instanceof PyError ? e.message : String(e);
      }
      if (got !== want) bad.push(`${JSON.stringify(doc)}: ${want} != ${got}`);
    }
    expect(bad).toEqual([]);
  }, T);
});

describe.skipIf(PACKETS.length === 0)("json round trip on real packets ($TDD_PARITY_PACKETS)", () => {
  for (const packet of PACKETS) {
    test(`${packet.split("/").pop()}: build/content.json, content.files.json, lint.json byte for byte`, () => {
      const read = (f: string) => decodeUtf8(readFileSync(join(packet, "build", f)));
      const c = read("content.json");
      expect(dumps(loads(c), { indent: 1, ensureAscii: false })).toBe(c);
      // the compiler writes indent 2 with no newline; app/build.ts later rewrites it with JSON.stringify(m, null, 2) + "\n"
      const m = read("content.files.json");
      const md = dumps(loads(m), { indent: 2, ensureAscii: false });
      expect(m.endsWith("\n") ? md + "\n" : md).toBe(m);
      if (existsSync(join(packet, "build", "lint.json"))) {
        const l = read("lint.json");
        expect(dumps(loads(l), { indent: 1, ensureAscii: false })).toBe(l);
      }
    }, T);
  }
});
