// A few results of the D1 surface checked without Python (the values were printed by CPython 3.13 from the vendor
// build_doc.py / build_prs.py). The oracle suites (fuzz, replay, files) are the real proof; these keep the API
// shape honest where python3 is missing, and pin the MARK override behaviour. The last block compares the surface's
// data (patterns, tables, constants) with the live Python.
import { describe, expect, test } from "bun:test";
import * as py from "../../src/py";
import type { Pattern } from "../../src/py/re";
import { DEFAULT_MARK, JSON_MAX_DEPTH, MARK, RecursionError, bd, bp, jsonLoads, parseBlocks, setMark } from "../../src/sd";
import { hasPython, runOracle, tagOf, type Tagged } from "./harness";

const TDD_MARK = py.re.compile(String.raw`^\s*\\?\[(embedded content|diagram|video|media|image|legend|reading)\s*:\s*(.*?)\s*\\?\]\s*$`, py.re.I);
const LINES = ["# Title ##", "", "- [x] done", "  - child", "", "> [!TIP]", "> hi **there**", "", "| a | b |", "|:-:|-:|", "| 1 \\| 2 | 3 |", "",
  "[legend: new = green | old = grey]", "[diagram: flow · The flow {fit}]", "1. one", "2. two", "", "![Mock](mockups/x.png \"Caption\")", "text", "more text"];
const COMMON_HEAD = [
  { t: "h", level: 1, text: "Title" },
  { t: "list", ordered: false, start: 1, items: [{ text: "done", check: true, children: [{ t: "list", ordered: false, start: 1, items: [{ text: "child", check: null, children: [] }] }] }] },
  { t: "quote", kind: "good", blocks: [{ t: "p", text: "hi **there**" }] },
  { t: "table", head: ["a", "b"], align: ["center", "right"], rows: [["1 | 2", "3"]] },
];
const COMMON_TAIL = [
  { t: "marker", kind: "diagram", arg: "flow · The flow {fit}" },
  { t: "list", ordered: true, start: 1, items: [{ text: "one", check: null, children: [] }, { text: "two", check: null, children: [] }] },
  { t: "p", text: "![Mock](mockups/x.png \"Caption\") text more text" },
];
const json = (v: unknown) => JSON.stringify(v, (_k, x) => (x instanceof Map ? Object.fromEntries(x) : x));

describe("src/sd without python", () => {
  test("parse_blocks with build_doc's MARK, then with build_tdd's (read at call time)", () => {
    expect(MARK).toBe(DEFAULT_MARK);
    expect(json(bd.parse_blocks(LINES))).toBe(json([...COMMON_HEAD, { t: "p", text: "[legend: new = green | old = grey]" }, ...COMMON_TAIL]));
    const prev = setMark(TDD_MARK);
    try {
      expect(bd.MARK).toBe(TDD_MARK);
      expect(json(bd.parse_blocks(LINES))).toBe(json([...COMMON_HEAD, { t: "marker", kind: "legend", arg: "new = green | old = grey" }, ...COMMON_TAIL]));
      expect(bd.is_block_start("[reading: x]")).toBe(true);
      expect(json(parseBlocks(["[legend: a]"], DEFAULT_MARK))).toBe(json([{ t: "p", text: "[legend: a]" }]));
    } finally {
      bd.MARK = prev;
    }
    expect(MARK).toBe(DEFAULT_MARK);
    expect(bd.is_block_start("[reading: x]")).toBe(false);
  });

  test("slug, page_title, split_marker, strip_tags", () => {
    expect(["İstanbul Ünïcode — 2026!", "", "---", "a".repeat(60) + " b", "Straße"].map(bd.slug))
      .toEqual(["i-stanbul-n-code-2026", "section", "section", "a".repeat(48), "stra-e"]);
    expect(["Open Shift Ranking Technical Design Document — v2", "The tdd skill: architecture", "One Two Three Four Five System Design", "Short"].map(bd.page_title))
      .toEqual(["Open Shift Ranking TDD", "The tdd skill", "One Two Three Four", "Short"]);
    expect(["flow · The flow", "a | b", "plain "].map(bd.split_marker)).toEqual([["flow", "The flow"], ["a", "b"], ["plain", ""]]);
    expect(bd.strip_tags(" <b>a &amp; b</b>&nbsp; ")).toBe("a & b");
  });

  test("Inline is a callable with an anchor_map", () => {
    const inl = bd.Inline();
    expect(inl.anchor_map.size).toBe(0);
    expect(inl("**b** _e_ `c` [l](#x) <https://a.b> \\* ~~d~~")).toBe(
      '<strong>b</strong> <em>e</em> <code>c</code> <a href="#x">l</a> <a href="https://a.b" target="_blank" rel="noopener">https://a.b</a> * <del>d</del>');
    expect(inl('![a](p"q.png) [x](https://e.com "t") a__b__c')).toBe('<img src="p&quot;q.png" alt="a" loading="lazy"> [x](https://e.com "t") a__b__c');
    expect(inl("[l](#x) <https://a.b>", false)).toBe("l https://a.b");
    inl.anchor_map.set("x", "sec-x");
    expect(inl("[l](#x)")).toBe('<a href="#sec-x">l</a>');
  });

  test("build_prs: front, inline, markdown, first_sentence", () => {
    const [meta, body] = bp.front("---\ntitle: \"PR 7: x\"\nmilestone: M1 # note\nempty:\n---\n# PR 7\nbody\n");
    expect([...meta]).toEqual([["title", "PR 7: x"], ["milestone", "M1"]]);
    expect(body).toBe("# PR 7\nbody\n");
    expect(bp.inline("**b** *i* `x*y*` [l](https://e.com) <tag>")).toBe('<b>b</b> <i>i</i> <code>x*y*</code> <a href="https://e.com">l</a> &lt;tag&gt;');
    expect(bp.markdown("## Summary\nOne **two** `c*d*`.\n- a\n- b\n\n| h | i |\n|---|---|\n| 1 | 2 |\n```\n<x>\n```\n")).toBe(
      "<h2>Summary</h2>\n<p>One <b>two</b> <code>c*d*</code>.</p>\n<ul><li>a</li><li>b</li></ul>\n"
      + "<table><thead><tr><th>h</th><th>i</th></tr></thead><tbody><tr><td>1</td><td>2</td></tr></tbody></table>\n<pre><code>&lt;x&gt;</code></pre>");
    expect(["Does X. Then Y.", "no stop", "a.b c"].map(bp.first_sentence)).toEqual(["Does X.", "no stop", "a.b c"]);
  });

  test("STATUS_WORDS is a dict lookup (no prototype keys); FILE_LIMIT; load_json of nothing", () => {
    expect(bd.STATUS_WORDS.get("merged")).toBe("built");
    expect(bd.STATUS_WORDS.get("constructor") ?? null).toBeNull();
    expect(bd.STATUS_WORDS.size).toBe(21);
    expect(bd.FILE_LIMIT).toBe(15728640);
    expect(bd.load_json("", "--meta")).toBeNull();
    expect(() => bd.load_json("/nonexistent/x.json", "--meta")).toThrow("--meta /nonexistent/x.json: [Errno 2] No such file or directory: '/nonexistent/x.json'");
  });

  test("jsonLoads keeps CPython's int-digit and nesting limits", () => {
    expect(jsonLoads("9".repeat(4300))).toBe(10n ** 4300n - 1n);
    expect(() => jsonLoads("-" + "9".repeat(4301))).toThrow("Exceeds the limit (4300 digits) for integer string conversion: value has 4301 digits; use sys.set_int_max_str_digits() to increase the limit");
    expect(() => jsonLoads('{"a" ' + "9".repeat(5000) + "}")).toThrow("Expecting ':' delimiter: line 1 column 6 (char 5)");
    const deep = (n: number) => "[".repeat(n) + "]".repeat(n);
    expect(() => jsonLoads(deep(JSON_MAX_DEPTH))).not.toThrow();
    expect(() => jsonLoads(deep(JSON_MAX_DEPTH + 1))).toThrow(RecursionError);
    expect(() => jsonLoads("[".repeat(100000))).toThrow("maximum recursion depth exceeded while decoding a JSON array from a unicode string");
  });
});

describe.skipIf(!hasPython)("the surface's data against the live system-design", () => {
  test("pattern sources and flags, STATUS_WORDS, GENERIC_TAILS, FILE_LIMIT, BLOCK, build_tdd's MARK", () => {
    const g = runOracle<{ patterns: Record<string, [string, number]>; tdd_mark: [string, number]; STATUS_WORDS: Tagged; GENERIC_TAILS: Tagged; FILE_LIMIT: number; BLOCK: string }>("sd_constants");
    const flags = (f: number) => f & ~py.re.UNICODE; // CPython adds re.U to every str pattern
    const ours: Record<string, Pattern> = { MARK: DEFAULT_MARK, HEAD: bd.HEAD, FENCE: bd.FENCE, LI: bd.LI, HR: bd.HR, TSEP: bd.TSEP, COMMENT: bd.COMMENT };
    for (const [n, [src, f]] of Object.entries(g.patterns)) expect([n, ours[n]!.pattern, flags(ours[n]!.flags)]).toEqual([n, src, flags(f)]);
    expect([TDD_MARK.pattern, flags(TDD_MARK.flags)]).toEqual([g.tdd_mark[0], flags(g.tdd_mark[1])]);
    expect(tagOf(bd.STATUS_WORDS)).toEqual(g.STATUS_WORDS);
    expect(tagOf(bd.GENERIC_TAILS)).toEqual(g.GENERIC_TAILS);
    expect(bd.FILE_LIMIT).toBe(g.FILE_LIMIT);
    expect(bp.BLOCK).toBe(g.BLOCK);
  });
});
