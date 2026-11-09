// SequenceMatcher against CPython's difflib on 2,400 seeded random pairs: ratio (as repr), get_matching_blocks,
// find_longest_match over the whole range and over a random sub-range, with autojunk on and off, an isjunk, str
// sequences (code points, astral characters included) and lists of words, short and long (b >= 200 elements, where
// the popular-element purge applies).
import { expect, test } from "bun:test";
import { SequenceMatcher } from "../../src/hotspots/difflib";
import { floatRepr } from "../../src/hotspots/_py";
import { pyJson, rng } from "./_harness";

interface Case { kind: "str" | "list"; a: string | string[]; b: string | string[]; autojunk: boolean; junk: string | null; sub: number[] }
interface PyOut { ratio: string; blocks: number[][]; flm: number[]; sub: number[] }

const PY = String.raw`
import difflib, json, sys
out = []
for c in json.load(sys.stdin):
    junk = c["junk"]
    isjunk = (lambda x: x == junk) if junk is not None else None
    a, b = c["a"], c["b"]
    sm = difflib.SequenceMatcher(isjunk, a, b, autojunk=c["autojunk"])
    alo, ahi, blo, bhi = c["sub"]
    out.append({"ratio": repr(sm.ratio()), "blocks": [list(m) for m in sm.get_matching_blocks()],
                "flm": list(sm.find_longest_match(0, len(a), 0, len(b))), "sub": list(sm.find_longest_match(alo, ahi, blo, bhi))})
print(json.dumps(out))
`;

function cases(): Case[] {
  const r = rng(20261109);
  const out: Case[] = [];
  const alphabets = ["ab", "abc", "abcd ", "acgt", "abcdefghij", "xy z", "a😀b", "étè ", "0123456789abcdefghijklmnopqrstuvwxyz -"];
  const vocab = ["the", "award", "record", "list", "shown", "queue", "rank", "post", "a", "of", "open", "shift", "tech", "flow"];
  const str = (alpha: string, n: number) => { const cs = Array.from(alpha); let s = ""; for (let i = 0; i < n; i++) s += r.pick(cs); return s; };
  const list = (n: number, v: string[]) => Array.from({ length: n }, () => r.pick(v));
  const mutate = (s: string): string => {
    const cs = Array.from(s);
    for (let k = r.int(0, 6); k > 0 && cs.length; k--) {
      const i = r.int(0, cs.length - 1), op = r.int(0, 2);
      if (op === 0) cs.splice(i, 1); else if (op === 1) cs.splice(i, 0, cs[r.int(0, cs.length - 1)]!); else cs[i] = cs[r.int(0, cs.length - 1)]!;
    }
    return cs.join("");
  };
  const sub = (la: number, lb: number) => { const alo = r.int(0, la), blo = r.int(0, lb); return [alo, r.int(alo, la), blo, r.int(blo, lb)]; };
  const push = (kind: "str" | "list", a: string | string[], b: string | string[], junk: string | null) => {
    const la = kind === "str" ? Array.from(a as string).length : a.length, lb = kind === "str" ? Array.from(b as string).length : b.length;
    out.push({ kind, a, b, autojunk: r.next() < 0.6, junk, sub: sub(la, lb) });
  };
  for (let i = 0; i < 900; i++) { const al = r.pick(alphabets); const a = str(al, r.int(0, 60)); push("str", a, r.next() < 0.5 ? mutate(a) : str(al, r.int(0, 60)), null); }
  for (let i = 0; i < 500; i++) { const al = r.pick(alphabets); const a = str(al, r.int(100, 420)); push("str", a, r.next() < 0.5 ? mutate(a) : str(al, r.int(190, 420)), null); }
  for (let i = 0; i < 400; i++) { const a = str("abc d", r.int(0, 260)); push("str", a, r.next() < 0.5 ? mutate(a) : str("abc d", r.int(0, 260)), " "); }
  for (let i = 0; i < 400; i++) { const a = list(r.int(0, 30), vocab); push("list", a, r.next() < 0.5 ? list(r.int(0, 30), vocab) : [...a].reverse(), r.next() < 0.3 ? "the" : null); }
  for (let i = 0; i < 200; i++) { const a = list(r.int(150, 300), vocab); push("list", a, list(r.int(200, 320), vocab), null); }
  return out;
}

test("SequenceMatcher matches CPython difflib on 2,400 random pairs", () => {
  const cs = cases();
  expect(cs.length).toBeGreaterThanOrEqual(2000);
  const want = pyJson<PyOut[]>(PY, cs);
  expect(want.length).toBe(cs.length);
  let mismatches = 0;
  cs.forEach((c, i) => {
    const sm = new SequenceMatcher<string>(c.junk === null ? null : (x) => x === c.junk, c.a, c.b, c.autojunk);
    const la = c.kind === "str" ? Array.from(c.a as string).length : c.a.length;
    const lb = c.kind === "str" ? Array.from(c.b as string).length : c.b.length;
    const got: PyOut = {
      ratio: floatRepr(sm.ratio()),
      blocks: sm.getMatchingBlocks().map((m) => [m.a, m.b, m.size]),
      flm: ((m) => [m.a, m.b, m.size])(sm.findLongestMatch(0, la, 0, lb)),
      sub: ((m) => [m.a, m.b, m.size])(sm.findLongestMatch(c.sub[0], c.sub[1], c.sub[2], c.sub[3])),
    };
    if (JSON.stringify(got) !== JSON.stringify(want[i])) {
      if (mismatches++ < 3) expect({ case: c, got }).toEqual({ case: c, got: want[i]! });
    }
  });
  expect(mismatches).toBe(0);
}, 60_000);

test("autojunk drops popular elements only when b has 200 or more items", () => {
  const b = "a".repeat(5) + "b".repeat(195);
  expect(new SequenceMatcher<string>(null, "ab", b).bpopular.has("b")).toBe(true);
  expect(new SequenceMatcher<string>(null, "ab", b.slice(1)).bpopular.size).toBe(0);
  expect(new SequenceMatcher<string>(null, "ab", b, false).bpopular.size).toBe(0);
});
