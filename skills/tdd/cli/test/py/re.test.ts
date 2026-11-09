// Python's re against CPython: 65 patterns (the compiler's own among them: TOKEN, MARK, BOX, ATTRS, Inline's emphasis,
// the section split) over 309 strings rich in non-ASCII letters and digits, line ends and reference tokens:
// finditer spans and groups, sub with \g<0> \1 \g<1> templates, subn with count, split (and maxsplit), fullmatch,
// match, findall; re.escape; TOKEN.finditer and token_id from build_tdd.py itself.
import { describe, expect, test } from "bun:test";
import * as re from "../../src/py/re";
import { lower, replace, strip } from "../../src/py/str";
import { SKIP_REASON, cpIndex, hasPython, oracle } from "./oracle";

const T = 60_000;

describe("re: fixed cases", () => {
  test("R1 R2 R4 examples", () => {
    const TOKEN = re.compile(String.raw`(?<![\w#/.\-])((?:D|E|F|R|A|M|Q|G|OS|LN|SS|NFR|ADR)-?\d+(?:\.\d+)?|PR\s?\d+[a-z]?)(?![\w\-]|\.\d)`);
    expect(TOKEN.findall("éD7 xD7 D7")).toEqual(["D7"]);
    expect(re.findall(String.raw`\bNULL\b`, "éNULL NULL")).toEqual(["NULL"]);
    expect(re.search(String.raw`\d`, "٣")).not.toBeNull();
    expect(re.search("x$", "x\n")).not.toBeNull();
    expect(re.split("·", "a·b·c", 1)).toEqual(["a", "b·c"]);
    expect(re.sub("x*?", "-", "xx")).toBe("-----"); // CPython 3.7+ empty-match rule (JS gives -x-x-)
    expect(re.subn("a", "b", "aaa", 2)).toEqual(["bba", 2]);
    expect(re.escape("a.b*c")).toBe("a\\.b\\*c");
  });
});

describe.skipIf(!hasPython)("re vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);

  test("finditer / sub / subn / split / fullmatch / match / findall", () => {
    type R = { m: [number, number, (string | null)[]][]; sub: string; subn: [string, number]; split: (string | null)[]; split1: (string | null)[]; full: [number, number] | null; match: number | null; findall: unknown[] };
    const g = oracle<{ strings: string[]; cases: { pattern: string; flags: number; results: R[] }[]; escape: [string, string][] }>("re");
    const bad: string[] = [];
    for (const c of g.cases) {
      const rx = re.compile(c.pattern, c.flags);
      const tmpl = "[\\g<0>]" + (rx.groups >= 1 ? "\\1|\\g<1>" : "");
      g.strings.forEach((s, k) => {
        const want = c.results[k]!;
        const got: R = {
          m: [...rx.finditer(s)].map((m) => [cpIndex(s, m.start()), cpIndex(s, m.end()), m.groups()]),
          sub: rx.sub(tmpl, s),
          subn: rx.subn("#", s, 2),
          split: rx.split(s),
          split1: rx.split(s, 1),
          full: ((m) => (m ? [cpIndex(s, m.start()), cpIndex(s, m.end())] : null))(rx.fullmatch(s)) as [number, number] | null,
          match: ((m) => (m ? cpIndex(s, m.end()) : null))(rx.match(s)),
          findall: rx.findall(s),
        };
        for (const key of Object.keys(want) as (keyof R)[]) {
          if (JSON.stringify(got[key]) !== JSON.stringify(want[key]) && bad.length < 12)
            bad.push(`${c.pattern} /${c.flags} ${key}(${JSON.stringify(s)}): ${JSON.stringify(want[key])} != ${JSON.stringify(got[key])}`);
        }
      });
    }
    expect(bad).toEqual([]);
    const esc = g.escape.filter(([s, e]) => re.escape(s) !== e || re.fullmatch(re.escape(s), s) === null).slice(0, 5);
    expect(esc).toEqual([]);
  }, T);

  test("TOKEN and token_id from build_tdd.py", () => {
    const g = oracle<{ pattern: string; rows: [string, [number, number, string, string][]][] }>("token");
    const TOKEN = re.compile(g.pattern);
    const tokenId = (tok: string) => replace(re.sub("^pr(\\d)", "pr-\\1", replace(lower(strip(tok)), " ", "-")), ".", "-");
    const bad: string[] = [];
    for (const [s, want] of g.rows) {
      const got = [...TOKEN.finditer(s)].map((m) => [cpIndex(s, m.start()), cpIndex(s, m.end()), m.group(1)!, tokenId(m.group(1)!)]);
      if (JSON.stringify(got) !== JSON.stringify(want) && bad.length < 8) bad.push(`${JSON.stringify(s)}: ${JSON.stringify(want)} != ${JSON.stringify(got)}`);
    }
    expect(bad).toEqual([]);
  }, T);
});
