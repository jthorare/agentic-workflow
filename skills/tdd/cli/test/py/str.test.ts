// Python str methods against CPython on 3,000 random strings built from whitespace, line boundaries, astral and lone
// surrogate characters: split/rsplit (with and without sep and maxsplit), strip family, partition, splitlines,
// len, slicing, replace (all occurrences, count, empty old), lower/upper, isspace, and sorted() order.
import { describe, expect, test } from "bun:test";
import * as str from "../../src/py/str";
import { SKIP_REASON, hasPython, oracle } from "./oracle";

const T = 60_000;

describe("str: fixed cases", () => {
  test("R4 R5 R14 examples and the JS traps", () => {
    expect(str.split("a·b·c", "·", 1)).toEqual(["a", "b·c"]);
    expect(str.strip(" x\x1c")).toBe("x");
    expect(str.strip("﻿x")).toBe("﻿x");
    expect(str.splitlines("a\x85b c")).toEqual(["a", "b", "c"]);
    expect(str.cpSlice("😀ab", 0, 1)).toBe("😀");
    expect(str.cpLen("😀ab")).toBe(3);
    expect(str.replace("a.b.c", ".", "-")).toBe("a-b-c");
    expect(str.replace("abc", "", "-")).toBe("-a-b-c-");
    expect(str.replace("😀😀", "", "|", 2)).toBe("|😀|😀");
  });

  test("cmp orders by code point, lone surrogates included (property test against Array.from)", () => {
    const alpha = ["a", "\ud800", "\udc00", "\ue000", "\uffff", "\u{10000}", "\u{1F600}", "z"];
    const ref = (a: string, b: string) => {
      const x = Array.from(a, (c) => c.codePointAt(0)!);
      const y = Array.from(b, (c) => c.codePointAt(0)!);
      for (let i = 0; i < Math.min(x.length, y.length); i++) if (x[i] !== y[i]) return x[i]! < y[i]! ? -1 : 1;
      return Math.sign(x.length - y.length);
    };
    let seed = 7;
    const rnd = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);
    const gen = () => Array.from({ length: Math.floor(rnd() * 5) }, () => alpha[Math.floor(rnd() * alpha.length)]).join("");
    for (let k = 0; k < 20000; k++) {
      const a = gen();
      const b = gen();
      expect([a, b, str.cmp(a, b)]).toEqual([a, b, ref(a, b)]);
    }
  });
});

describe.skipIf(!hasPython)("str vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("every method on random strings", () => {
    const g = oracle<{ rows: Record<string, any>[]; sorted: { input: string[]; output: string[] } }>("str");
    const bad: string[] = [];
    const check = (row: Record<string, any>, name: string, got: unknown) => {
      if (JSON.stringify(got) !== JSON.stringify(row[name])) bad.push(`${name}(${JSON.stringify(row.s)}): ${JSON.stringify(row[name])} != ${JSON.stringify(got)}`);
    };
    for (const r of g.rows) {
      const { s, sep, chars, old, count } = r as { s: string; sep: string; chars: string; old: string; count: number };
      check(r, "split", str.split(s));
      check(r, "split1", str.split(s, null, 1));
      check(r, "split0", str.split(s, null, 0));
      check(r, "rsplit1", str.rsplit(s, null, 1));
      check(r, "rsplit2", str.rsplit(s, null, 2));
      check(r, "splitsep", str.split(s, sep));
      check(r, "splitsep1", str.split(s, sep, 1));
      check(r, "rsplitsep1", str.rsplit(s, sep, 1));
      check(r, "rsplitsep", str.rsplit(s, sep));
      check(r, "strip", str.strip(s));
      check(r, "lstrip", str.lstrip(s));
      check(r, "rstrip", str.rstrip(s));
      check(r, "stripc", str.strip(s, chars));
      check(r, "lstripc", str.lstrip(s, chars));
      check(r, "rstripc", str.rstrip(s, chars));
      check(r, "partition", str.partition(s, sep));
      check(r, "rpartition", str.rpartition(s, sep));
      check(r, "splitlines", str.splitlines(s));
      check(r, "splitlinesk", str.splitlines(s, true));
      check(r, "len", str.cpLen(s));
      check(r, "slices", (r.slices as [number | null, number | null, string][]).map(([a, b]) => [a, b, str.cpSlice(s, a, b)]));
      check(r, "replace", str.replace(s, old, "<>", count));
      check(r, "replaceall", str.replace(s, old, "-"));
      check(r, "lower", str.lower(s));
      check(r, "upper", str.upper(s));
      check(r, "isspace", str.isspace(s));
      if (bad.length > 10) break;
    }
    expect(bad).toEqual([]);
    expect(str.sorted(g.sorted.input)).toEqual(g.sorted.output);
  }, T);
});
