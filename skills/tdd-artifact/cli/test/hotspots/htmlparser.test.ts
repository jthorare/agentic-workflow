// The html.parser port replays CPython's handle_* events: same calls, same order, same arguments, on 1,500 seeded
// documents built from tldraw-export fragments and malformed markup, after feed() alone (what Shapes does) and after
// feed() + close().
import { expect, test } from "bun:test";
import { HTMLParser, type Attrs } from "../../src/hotspots/htmlparser";
import { pyJson, rng } from "./_harness";

type Ev = (string | (string | null)[][])[];
class Rec extends HTMLParser {
  ev: Ev[] = [];
  override handle_starttag(t: string, a: Attrs) { this.ev.push(["start", t, a.map(([k, v]) => [k, v])]); }
  override handle_endtag(t: string) { this.ev.push(["end", t]); }
  override handle_startendtag(t: string, a: Attrs) { this.ev.push(["startend", t, a.map(([k, v]) => [k, v])]); }
  override handle_data(d: string) { this.ev.push(["data", d]); }
  override handle_comment(d: string) { this.ev.push(["comment", d]); }
  override handle_decl(d: string) { this.ev.push(["decl", d]); }
  override handle_pi(d: string) { this.ev.push(["pi", d]); }
  override unknown_decl(d: string) { this.ev.push(["unknown", d]); }
}

const PY = String.raw`
import json, sys
from html.parser import HTMLParser
class P(HTMLParser):
    def __init__(s):
        super().__init__(convert_charrefs=True); s.ev = []
    def handle_starttag(s, t, a): s.ev.append(["start", t, [list(x) for x in a]])
    def handle_endtag(s, t): s.ev.append(["end", t])
    def handle_startendtag(s, t, a): s.ev.append(["startend", t, [list(x) for x in a]])
    def handle_data(s, d): s.ev.append(["data", d])
    def handle_comment(s, d): s.ev.append(["comment", d])
    def handle_decl(s, d): s.ev.append(["decl", d])
    def handle_pi(s, d): s.ev.append(["pi", d])
    def unknown_decl(s, d): s.ev.append(["unknown", d])
out = []
for doc in json.load(sys.stdin):
    p = P(); p.feed(doc); a = p.ev
    q = P(); q.feed(doc); q.close()
    out.append([a, q.ev])
print(json.dumps(out))
`;

const FRAGS = [
  '<svg viewBox="0 0 10 10" class="tl-container">', "</svg>", '<g transform="matrix(1, 0, 0, 1, 12.5, -3)">', "</g>", "<g/>",
  '<path d="M0 0 L10 10" stroke="#099268" stroke-dasharray="4 2"/>', "<rect x='1' y=2 width=\"3\" height=\"4\">",
  '<foreignObject x="0" y="0" width="100" height="40">', "</foreignObject>", '<div xmlns="http://www.w3.org/1999/xhtml">',
  "</div>", "<p>", "</p>", "<strong>", "</strong>", "<p dir=\"auto\"/>", "Label", " text ", "A &amp; B", "&lt;tag&gt;", "it&#x27;s",
  "&quot;q&quot;", "&notin; &notit; &amp", "a & b", "&", "&#", "&#39", "<!-- c -->", "<!-->", "<!--->", "<!-- a -- b --!>",
  "<!DOCTYPE svg>", "<!doctype html >", "<![CDATA[x<y]]>", '<?xml version="1.0"?>', "<style>a<b {c:d}</style>",
  "<style>x</ style></style >", "<script>if (a<b) {}</script>", "<title>T &amp; t</title>", "<textarea>&lt;x</textarea>",
  "<br/>", "<a b=c d>", '<x y="1"z="2">', "< not a tag", "<1>", "</>", "</ x>", "</x y>", "<!bogus>", "<?pi", "<g", '<g a="',
  "😀é", "\n", "\t", "<A HREF='X&amp;Y' Data-X=&quot;>", "<x a=&ampx b=&amp= c=&#65>", "<x/ y>", "<x  / >", "</G >", "<p\n>",
];

test("the html.parser port emits CPython's events on 1,500 random documents", () => {
  const r = rng(99);
  const docs = Array.from({ length: 1500 }, () => Array.from({ length: r.int(1, 12) }, () => r.pick(FRAGS)).join(""));
  docs.push(...FRAGS, "", "<", "<<", "a<", "&am", "x &#12");
  const want = pyJson<Ev[][][]>(PY, docs);
  expect(want.length).toBe(docs.length);
  let bad = 0;
  docs.forEach((d, i) => {
    const p = new Rec(); p.feed(d);
    const q = new Rec(); q.feed(d); q.close();
    const got = [p.ev, q.ev];
    if (JSON.stringify(got) !== JSON.stringify(want[i])) { if (bad++ < 3) expect([d, got]).toEqual([d, want[i]!]); }
  });
  expect(bad).toBe(0);
}, 30_000);
