// html.escape / html.unescape against CPython: every HTML5 entity name (2,231) in four to seven forms (alone, followed by
// text, without its semicolon, as a prefix of a longer name), the decimal numeric reference of every code point
// 0..0x10FFFF, hex and no-semicolon forms around the special ranges, and random mixes.
import { describe, expect, test } from "bun:test";
import { escape, unescape } from "../../src/py/html";
import { SKIP_REASON, hasPython, oracle } from "./oracle";

const T = 60_000;

describe("html: fixed cases", () => {
  test("R13 example", () => {
    expect(escape("it's")).toBe("it&#x27;s");
    expect(escape("<a href=\"x\">&</a>", false)).toBe("&lt;a href=\"x\"&gt;&amp;&lt;/a&gt;");
    expect(unescape("&notit; &#x80; &#1;x &#xD800; &amp")).toBe("¬it; € x � &");
  });
});

describe.skipIf(!hasPython)("html vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  const g = () => oracle<{ names: [string, string][]; numeric_dec: Record<string, string>; numeric_other: [string, string][]; unescape: [string, string][]; escape: [string, string, string][] }>("html");

  test("every HTML5 entity name", () => {
    expect(g().names.length).toBeGreaterThan(2231 * 4);
    const bad = g().names.filter(([s, want]) => unescape(s) !== want).slice(0, 10);
    expect(bad).toEqual([]);
  }, T);

  test("numeric references: every code point (decimal), hex and unterminated forms", () => {
    const bad: string[] = [];
    for (let n = 0; n < 0x110000 && bad.length < 10; n++) {
      const want = g().numeric_dec[String(n)] ?? String.fromCodePoint(n);
      if (unescape(`&#${n};`) !== want) bad.push(`&#${n};`);
    }
    for (const [s, want] of g().numeric_other) if (unescape(s) !== want && bad.length < 20) bad.push(s);
    expect(bad).toEqual([]);
  }, T);

  test("random mixes and escape(quote=True/False)", () => {
    expect(g().unescape.filter(([s, want]) => unescape(s) !== want).slice(0, 10)).toEqual([]);
    expect(g().escape.filter(([s, q, nq]) => escape(s) !== q || escape(s, false) !== nq).slice(0, 10)).toEqual([]);
  }, T);
});
