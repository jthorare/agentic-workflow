// The Python-semantics helpers in src/hotspots/_py.ts against CPython: float repr, round(x, n), format(x, ".2f"),
// float(str), repr(str), str.strip / split, html.unescape over every HTML5 entity and random references, and
// html.parser's attribute unescape. Each is the stand-in for a src/py module, so these tests move with it at merge.
import { expect, test } from "bun:test";
import {
  cpLen, cpSlice, floatRepr, formatF, html5Get, jsonDumps, jsonLoads, pyFloat, pyRound, repr, splitWs, strip, unescape,
  unescapeAttr,
} from "../../src/hotspots/_py";
import { pyJson, rng } from "./_harness";

function doubles(): string[] {
  const r = rng(7);
  const hex: string[] = [];
  const dv = new DataView(new ArrayBuffer(8));
  const push = (x: number) => { dv.setFloat64(0, x); hex.push([...new Uint8Array(dv.buffer)].map((b) => b.toString(16).padStart(2, "0")).join("")); };
  for (let i = 0; i < 1500; i++) {
    for (let k = 0; k < 8; k++) dv.setUint8(k, r.int(0, 255));
    const x = dv.getFloat64(0);
    if (Number.isFinite(x)) push(x);
  }
  for (let i = 0; i < 1500; i++) push(Number((r.next() * 10 ** r.int(-8, 18) * (r.next() < 0.5 ? -1 : 1)).toPrecision(r.int(1, 17))));
  for (let k = 0; k <= 800; k++) { push(k / 8); push(-k / 8); push(k / 4 + 0.05); }
  for (const x of [0, -0, 1e16, 1e15, 123456789012345678, 1e-4, 1e-5, 5e-324, 1.7976931348623157e308, 0.1, 0.5, 2.675, 1.005, 12.625, 0.125, 0.375])
    push(x);
  return hex;
}
const toNum = (h: string): number => { const dv = new DataView(new ArrayBuffer(8)); for (let i = 0; i < 8; i++) dv.setUint8(i, parseInt(h.slice(2 * i, 2 * i + 2), 16)); return dv.getFloat64(0); };

test("float repr, round(x, 1), round(x, 2) and format(x, '.2f') match CPython", () => {
  const hs = doubles();
  const want = pyJson<string[][]>(String.raw`
import json, struct, sys
out = []
for h in json.load(sys.stdin):
    x = struct.unpack(">d", bytes.fromhex(h))[0]
    out.append([repr(x), repr(round(x, 1)), repr(round(x, 2)), format(x, ".2f") if abs(x) < 1e30 else ""])
print(json.dumps(out))`, hs);
  let bad = 0;
  hs.forEach((h, i) => {
    const x = toNum(h);
    const got = [floatRepr(x), floatRepr(pyRound(x, 1)), floatRepr(pyRound(x, 2)), Math.abs(x) < 1e30 ? formatF(x, 2) : ""];
    if (JSON.stringify(got) !== JSON.stringify(want[i])) { if (bad++ < 3) expect([x, got]).toEqual([x, want[i]!]); }
  });
  expect(bad).toBe(0);
}, 30_000);

test("float(str) matches CPython, including Unicode digits, underscores and errors", () => {
  const inputs = ["1", " 1.5 ", "1_000", "1__0", "_1", "1_", "1_.5", "1._5", "٣.٥", "١٢٣", "inf", "-Infinity", "+nan", "NaN", "1e5",
    "1e", ".5", "5.", "-.5e-3", "--1", "1-2", "", " ", "0x10", "1 2", " 1 ", "1\x1c", "\x85-2\xa0", "1e400", "-1e-400",
    "12.625", "-0", "-0.0", "0.000", "1.2.3", "-", ".", "e5", "infinity", "INF", "in_f", "１２", "1e_5", "1_e5", "﻿1"];
  const want = pyJson<string[]>(String.raw`
import json, sys
out = []
for s in json.load(sys.stdin):
    try: out.append(repr(float(s)))
    except ValueError: out.append("ValueError")
print(json.dumps(out))`, inputs);
  const got = inputs.map((s) => { try { return floatRepr(pyFloat(s)); } catch { return "ValueError"; } });
  expect(got).toEqual(want);
});

test("repr(str), strip(), split() and code point slicing match CPython", () => {
  const r = rng(11);
  const pool = ["a", "'", '"', "\\", "\n", "\t", "\r", "\x00", "\x1b", "\x7f", "\x85", "\xa0", "é", "·", "​", " ",
    "", "﻿", "😀", "\u{10ffff}", " ", "\x1c", "　", "x", "Ω", "͸", "\ud800"];
  const strs = Array.from({ length: 1500 }, () => Array.from({ length: r.int(0, 12) }, () => r.pick(pool)).join(""));
  const want = pyJson<[string, string, string[], string, number][]>(String.raw`
import json, sys
out = []
for s in json.load(sys.stdin, strict=False):
    out.append([repr(s), s.strip(), s.split(), s[:5], len(s)])
print(json.dumps(out))`, strs);
  let bad = 0;
  strs.forEach((s, i) => {
    const got = [repr(s), strip(s), splitWs(s), cpSlice(s, 0, 5), cpLen(s)];
    if (JSON.stringify(got) !== JSON.stringify(want[i])) { if (bad++ < 3) expect([s, got]).toEqual([s, want[i]!]); }
  });
  expect(bad).toBe(0);
});

test("html5 entity table: every key matches html.entities.html5 and near-misses are absent", () => {
  const table = pyJson<Record<string, string>>(String.raw`
import json, sys
from html.entities import html5
print(json.dumps(html5))`, null);
  const keys = Object.keys(table);
  expect(keys.length).toBeGreaterThan(2200);
  const wrong = keys.filter((k) => html5Get(k) !== table[k]);
  expect(wrong).toEqual([]);
  const misses = keys.flatMap((k) => [k + "x", k.endsWith(";") ? k.slice(0, -1) : k + "q;"]).filter((k) => !(k in table));
  expect(misses.filter((k) => html5Get(k) !== undefined)).toEqual([]);
});

test("html.unescape and html.parser's attribute unescape match CPython on random references", () => {
  const r = rng(13);
  const frags = ["&", "&amp", "&amp;", "&lt;", "&notin", "&notin;", "&notit;", "&#", "&#39", "&#39;", "&#x27;", "&#X1F600;", "&#0;",
    "&#13;", "&#128;", "&#x80", "&#xD800;", "&#x110000;", "&#99999999999;", "&#11;", "&#xFFFE;", "&#65", "&copy", "&copyx", "&ampx",
    "&amp=", "&quot", "&NotNestedGreaterGreater;", "&acE;", "&zz;", "a", " ", ";", "=", "x", "é", "😀", "\n", "\t", "#", "&#x;",
    "&abcdefghijklmnopqrstuvwxyzabcdefghijkl;", "&Amp;", "&AMP;", "&gt", "&GT", "&LT;"];
  const strs = Array.from({ length: 3000 }, () => Array.from({ length: r.int(1, 8) }, () => r.pick(frags)).join(""));
  const want = pyJson<[string, string][]>(String.raw`
import json, sys, html
from html.parser import _unescape_attrvalue
print(json.dumps([[html.unescape(s), _unescape_attrvalue(s)] for s in json.load(sys.stdin)]))`, strs);
  let bad = 0;
  strs.forEach((s, i) => {
    const got = [unescape(s), unescapeAttr(s)];
    if (JSON.stringify(got) !== JSON.stringify(want[i])) { if (bad++ < 3) expect([s, got]).toEqual([s, want[i]!]); }
  });
  expect(bad).toBe(0);
});

test("json load then dump reproduces CPython's json.dumps(indent=1|2, ensure_ascii=False|True)", () => {
  const docs = ['{"b": 1, "2": 2.0, "a": [1.5e-7, 1e16, -0.0, 3, -0, 1E2], "b": "dup"}', '[]', '{}', '[{}, [], "x\\u00e9\\ud83d\\ude00\\n\\u0007"]',
    '{"n": NaN, "i": -Infinity, "t": true, "f": false, "z": null, "s": "\\"q\\" \\\\ / \\b\\f\\r\\t \\u2028"}', '  [1, 2.50, 100.0]  '];
  const want = pyJson<string[][]>(String.raw`
import json, sys
print(json.dumps([[json.dumps(json.loads(d), indent=i, ensure_ascii=a) for i in (None, 1, 2) for a in (False, True)] for d in json.load(sys.stdin)]))`, docs);
  const got = docs.map((d) => [null, 1, 2].flatMap((i) => [false, true].map((a) => jsonDumps(jsonLoads(d), { indent: i, ensureAscii: a }))));
  expect(got).toEqual(want);
});
