// The helpers under src/agents (_py.ts, the html.parser port, strip.ts) against CPython on inputs the two packets do not
// exercise: every HTML5 entity name, numeric references, tokenizer edge cases, float repr and round, lossless JSON.
import { describe, expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { HAVE_PYTHON, PY_STRIP, builds } from "./harness";
import { PyFloat, dumps, floatRepr, html5, loads, pyRound, relpath, splitext, dirname, basename, normpath, strip, unescape, cpLen, repr } from "../../src/agents/_py";
import * as sp from "../../src/agents/strip";

/** Run a Python snippet that reads JSON from stdin and prints JSON. */
function pyJson(code: string, input: unknown): unknown {
  const r = Bun.spawnSync(["python3", "-I", "-B", "-c", code], { cwd: process.cwd(), stdin: new TextEncoder().encode(JSON.stringify(input)), stdout: "pipe", stderr: "pipe" });
  if (r.exitCode !== 0) throw new Error(r.stderr.toString());
  return JSON.parse(r.stdout.toString());
}

const HTML_CASES = [
  "<p>a<p>b<div>c</div>", "<ul><li>one<li>two<ol start=3><li>x</ol></ul>", "<table><tr><td>a<td>b<tr><th>c</table>",
  "<dl><dt>t<dd>d<dt>u</dl>", "a < b and c<d", "x &amp y &ampz &notit; &notin; &#39; &#x27; &#0; &#128; &#xD800; &#1114112; &bogus;",
  "<a href='?a=1&amp;b=2&copy=3&notreal;x&amp=y'>q</a>", "<!-- c --> <!--> <!---> <!-- --!> x", "<![CDATA[ x ]]>y", "<!doctype html><p>z",
  "<?pi x?>w", "</ >v</>u</3>t", "<script>if (a<b) { x = '</p>'; }</script><p>s</p>", "<style>p{x:1}</STYLE >r",
  "<title>a &amp; <b>b</b></title><p>t</p>", "<textarea>&lt;x&gt;</textarea>", "<br/><img src=x alt=\"y\"/><span/>z",
  "<div class=\"a  b\tc\" id=d hidden>h</div><div hidden=until-found>k</div>", "<p>unterminated <b", "<p>bare &", "<p>tail &#",
  "<P CLASS=X>Upper</P>", "<a\tb=1\nc='2' d=\"3\" e>attrs</a>", "<details id=f><summary>S</summary><h3>H</h3></details>",
  "<pre data-lang=sql><code class=\"language-ts\">SELECT 1\n</code></pre><pre><code class=\"lang-py x\">print()</code></pre>",
  "<span class=chip>ok</span><span class=\"key\"><span><i class=\"sw green\"></i>new</span><span>old</span></span>",
  "<a class=ref href=#d1>D1</a> <a data-ref=x>X</a> <a href=\"#y\">Y</a> <a href=\"http://e\">E</a>", "<svg aria-label=\"L\"><title>T</title></svg><svg><title>U</title></svg>",
  "<input type=checkbox checked> done <input type=checkbox> todo <input type=text>", "<nav aria-label=Contents>n</nav><button>b</button>m",
  "<blockquote><p>q1</p><p>q2</p></blockquote><hr><h2 id=h>Head <em>em</em></h2>", "text\u00a0nbsp\u2028sep <strong> sp </strong>x",
  "<div id=tdd-root><section id=s1><h2>One</h2><p>p</p></section></div><script>x</script>", "<p>a<br>b<br/>c</p>",
  "<table><thead><tr><th>h|x</th></tr></thead><tbody><tr id=r1><td>1</td><td>2</td></tr></tbody></table>", "<plaintext><p>raw",
  "<a href=x\u00e9>\u00e9\u{1f600}</a>", "<p>&Aacute;&aacute;&lt;&GT;&ngE;&NotNestedGreaterGreater;&nbsp</p>", "<", "</", "<!", "<!--", "x<!-- open",
];

describe.skipIf(!HAVE_PYTHON)("kernel helpers against CPython", () => {
  test("html5(): every html.entities.html5 key resolves to the same value; names Python lacks resolve to nothing", () => {
    const table = pyJson("import json, html.entities as e; print(json.dumps(e.html5))", null) as Record<string, string>;
    const keys = Object.keys(table);
    expect(keys.length).toBe(2231);
    for (const k of keys) expect([k, html5(k)]).toEqual([k, table[k]]);
    for (const k of keys) if (k.endsWith(";")) expect([k, html5(k.slice(0, -1))]).toEqual([k, table[k.slice(0, -1)]]);
    for (const k of ["alpha", "notit", "ampx", "", "x;", "AMP;x"]) expect(html5(k)).toBe(table[k]);
  });

  test("unescape() equals html.unescape on references, partial names and invalid code points", () => {
    const cases = ["&amp; &amp &ampx &AMP; &notit; &notin &#65; &#x41 &#0; &#13; &#128; &#159; &#xD800; &#x110000; &#11; &#xFFFE; &#1; &#x7f;",
      "&;", "&#;", "&#x;", "&abcdefghijklmnopqrstuvwxyz0123456789;", "a&b c&d;e", "&le&ge;&lt&gt", "&Eacute&eacute;&ETH&eth", "&\u00e9;"];
    expect(cases.map(unescape)).toEqual(pyJson("import json, sys, html; print(json.dumps([html.unescape(s) for s in json.load(sys.stdin)]))", cases) as string[]);
  });

  test("floatRepr() and pyRound() equal repr(float) and round(x, n)", () => {
    const xs = [0.1, 1.0, 50.0, 1e-5, 1e-4, 1.5e16, 1e16, 9999999999999998, 123456.789, -0.0, 2.675, 0.125, 0.375, 12.625, 1 / 3, 5e-324, 1.7976931348623157e308];
    for (let i = 0; i < 300; i++) xs.push(Number((Math.random() * 200 - 100).toFixed(1 + (i % 6))) / (1 + (i % 7)));
    const asText = xs.map((x) => (Object.is(x, -0) ? "-0.0" : String(x)));   // JSON would turn 21.0 into the int 21
    const want = pyJson("import json, sys; xs = [float(s) for s in json.load(sys.stdin)]; print(json.dumps([[repr(x), repr(round(x, 1)), repr(round(x, 2))] for x in xs]))", asText) as string[][];
    expect(xs.map((x) => [floatRepr(x), floatRepr(pyRound(x, 1).value), floatRepr(pyRound(x, 2).value)])).toEqual(want);
  });

  test("loads() + dumps() reproduce json.dumps(json.loads(s), indent=1, ensure_ascii=False) on every content.json and edge values", () => {
    const docs = builds().map((b) => readFileSync(join(b, "content.json"), "utf8"));
    docs.push('{"b": 1, "2": [1.0, 2, -0.0, 1e400, 1.5e-7, 12345678901234567890], "a": {"x": "\\u00e9\\ud83d\\ude00\\n\\t\\u0001\\"", "x": "dup"}, "1": null, "t": true}');
    const want = pyJson("import json, sys; print(json.dumps([json.dumps(json.loads(s), indent=1, ensure_ascii=False) for s in json.load(sys.stdin)]))", docs) as string[];
    expect(docs.map((d) => dumps(loads(d), 1))).toEqual(want);
    expect(dumps(new Map<string, unknown>([["a", [1, new PyFloat(2)]]]))).toBe('{"a": [1, 2.0]}');
  });

  test("loads() errors read like CPython's", () => {
    const bad = ["", "{", '{"a" 1}', '{"a": 1,}', "[1 2]", '"abc', "[1] x", '"\\q"', "\ufeff{}", '{"a":\n  nul}'];
    const want = pyJson("import json, sys\nout = []\nfor s in json.load(sys.stdin):\n  try: json.loads(s); out.append('ok')\n  except ValueError as e: out.append(str(e))\nprint(json.dumps(out))", bad) as string[];
    expect(bad.map((s) => { try { loads(s); return "ok"; } catch (e) { return (e as Error).message; } })).toEqual(want);
  });

  test("path and str helpers equal os.path and str methods", () => {
    const paths = ["a/b/c.md", "a/b/", "/", "//x", "///x/../y", "a/../../b", ".bashrc", "a.b/c", "x.tar.gz", "", "a/./b//c/.."];
    const got = paths.map((p) => [normpath(p), dirname(p), basename(p), splitext(p), p ? relpath(p, "a/b") : "."]);
    const want = pyJson("import json, sys, os.path as P\nprint(json.dumps([[P.normpath(p), P.dirname(p), P.basename(p), list(P.splitext(p)), P.relpath(p, 'a/b') if p else '.'] for p in json.load(sys.stdin)]))", paths);
    expect(got as unknown).toEqual(want);
    const strs = ["\u00a0x\u2028", "\x1c y \x85", "\ufeffz", " \t\n\x0b\x0c\r", "\u{1f600}ab", "it's \"q\" \n \u00e9 \x00"];
    const want2 = pyJson("import json, sys\nprint(json.dumps([[s.strip(), len(s), repr(s)] for s in json.load(sys.stdin)]))", strs);
    expect(strs.map((s) => [strip(s), cpLen(s), repr(s)]) as unknown).toEqual(want2);
  });

  test("the html.parser port builds the same tree and renders the same text as strip_page.py", () => {
    const code = `import json, sys, importlib.util
spec = importlib.util.spec_from_file_location("sp", ${JSON.stringify(PY_STRIP)}); sp = importlib.util.module_from_spec(spec); spec.loader.exec_module(sp)
out = []
for s in json.load(sys.stdin):
    raw = sp.to_html(sp.parse(s))
    root = sp.parse(s); content, title, removed = sp.strip(root)
    out.append([raw, sp.to_html(content), title, removed, sp.render(content, "md"), sp.render(content, "txt"), sp.render_inline(s, "md"), sp.outline(content)])
print(json.dumps(out, ensure_ascii=False))`;
    const want = pyJson(code, HTML_CASES) as unknown[];
    const got = HTML_CASES.map((s) => {
      const raw = sp.toHtml(sp.parse(s));
      const [content, title, removed] = sp.strip(sp.parse(s));
      return [raw, sp.toHtml(content), title, Object.fromEntries(removed), sp.render(content, "md"), sp.render(content, "txt"), sp.renderInline(s, "md"),
        JSON.parse(dumps(sp.outline(content)))];
    });
    for (let i = 0; i < HTML_CASES.length; i++) expect([HTML_CASES[i], got[i]] as unknown).toEqual([HTML_CASES[i], want[i]]);
  });
});
